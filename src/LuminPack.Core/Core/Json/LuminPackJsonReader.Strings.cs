using System.Buffers;
using System.Buffers.Text;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;
#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics;
#endif

namespace LuminPack.Core
{
    public ref partial struct LuminPackJsonReader
    {
#if NET8_0_OR_GREATER
        /// <summary>Reads a quoted framework value through its original parser.</summary>
        /// <typeparam name="T">The requested framework scalar type.</typeparam>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>The parsed scalar.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadQuotedScalar<T, TOptions>() where T : struct where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadQuotedScalarOperation<T, TOptions>, T>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="T">The requested scalar type.</typeparam>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadQuotedScalarOperation<T, TOptions> : ILuminPackJsonReadOperation<T> where T : struct where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadQuotedScalarEncoded<T, TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="T">The requested scalar type.</typeparam>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ReadQuotedScalarEncoded<T, TOptions, TCodeUnit>() where T : struct where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if (typeof(T) != typeof(DateTime) && typeof(T) != typeof(DateTimeOffset) &&
                typeof(T) != typeof(TimeSpan) && typeof(T) != typeof(Guid) &&
                typeof(T) != typeof(TimeOnly) && typeof(T) != typeof(Int128) && typeof(T) != typeof(UInt128))
            {
                LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
                return default;
            }

            if (CurrentTokenType == JsonTokenType.String && CanReadQuotedScalarToken<TOptions>())
            {
                const int StackTokenLimit = 128;
                if (Unsafe.SizeOf<TCodeUnit>() == 1)
                {
                    var utf8 = ReadSelectedStringUtf8<TOptions>(true);
                    if (utf8.Length <= StackTokenLimit)
                    {
                        Span<char> decoded = stackalloc char[utf8.Length];
                        int length = Utf8PlatformDecoder.Decode(utf8, decoded);
                        CompleteValue();
                        return ParseQuotedScalar<T>(decoded.Slice(0, length));
                    }

                    string decodedString = CreateUtf8String(utf8);
                    CompleteValue();
                    return ParseQuotedScalar<T>(decodedString);
                }

                var token = ReadSelectedStringUtf16<TOptions>(true);
                if (token.Length <= StackTokenLimit)
                {
                    // A culture callback can reenter the serializer or change the original input.
                    Span<char> text = stackalloc char[token.Length];
                    token.CopyTo(text);
                    CompleteValue();
                    if (typeof(TOptions) == typeof(LuminPackUtf16LengthOptions) &&
                        (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTimeOffset)) &&
                        TryParseCanonicalTimestamp(text, out T timestamp))
                        return timestamp;
                    if (typeof(TOptions) == typeof(LuminPackUtf16LengthOptions) &&
                        (typeof(T) == typeof(TimeOnly) || typeof(T) == typeof(TimeSpan)) &&
                        TryParseCanonicalClock(text, out long ticks))
                    {
                        if (typeof(T) == typeof(TimeOnly))
                        {
                            var time = new TimeOnly(ticks);
                            return Unsafe.As<TimeOnly, T>(ref time);
                        }
                        var duration = new TimeSpan(ticks);
                        return Unsafe.As<TimeSpan, T>(ref duration);
                    }
                    return ParseQuotedScalar<T>(text);
                }

                string fullText = new string(token);
                CompleteValue();
                return ParseQuotedScalar<T>(fullText);
            }
            return ParseQuotedScalar<T>(Read<string, TOptions>());
        }

        /// <summary>Parses a complete invariant timestamp with an explicit UTC marker or numeric offset.</summary>
        /// <typeparam name="T">DateTime or DateTimeOffset.</typeparam>
        /// <param name="text">The complete private token.</param>
        /// <param name="value">The parsed value when the exact format is recognized.</param>
        /// <returns>Whether the token matches the bounded round-trip form.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryParseCanonicalTimestamp<T>(scoped ReadOnlySpan<char> text, out T value) where T : struct
        {
            value = default;
            bool utc = typeof(T) == typeof(DateTime);
            int width = utc ? 28 : 33;
            if (text.Length != width || (utc ? text[27] != 'Z' : text[27] is not ('+' or '-')) ||
                !ReferenceEquals(CultureInfo.CurrentCulture, CultureInfo.InvariantCulture))
                return false;

            Span<byte> bytes = stackalloc byte[width];
            if (Ascii.FromUtf16(text, bytes, out _) != OperationStatus.Done)
                return false;

            if (utc)
            {
                if (!Utf8Parser.TryParse(bytes, out DateTime time, out int consumed, 'O') || consumed != width ||
                    time.Kind != DateTimeKind.Utc)
                    return false;
                value = Unsafe.As<DateTime, T>(ref time);
                return true;
            }

            if (!Utf8Parser.TryParse(bytes, out DateTimeOffset offset, out int offsetConsumed, 'O') || offsetConsumed != width)
                return false;
            value = Unsafe.As<DateTimeOffset, T>(ref offset);
            return true;
        }

        /// <summary>Parses the shared invariant round-trip form of a clock or sub-day duration.</summary>
        /// <param name="text">The complete, private UTF-16 token.</param>
        /// <param name="ticks">The parsed sub-day ticks.</param>
        /// <returns>Whether the entire token exactly matches the bounded round-trip form.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseCanonicalClock(scoped ReadOnlySpan<char> text, out long ticks)
        {
            ticks = 0;
            if (text.Length != 16 || text[2] != ':' || text[5] != ':' || text[8] != '.' ||
                !ReferenceEquals(CultureInfo.CurrentCulture, CultureInfo.InvariantCulture))
                return false;

            if (!TryParseClockPair(text[0], text[1], 23, out uint hours) ||
                !TryParseClockPair(text[3], text[4], 59, out uint minutes) ||
                !TryParseClockPair(text[6], text[7], 59, out uint seconds))
                return false;

            uint fraction = 0;
            for (int index = 9; index < 16; index++)
            {
                uint digit = (uint)(text[index] - '0');
                if (digit > 9)
                    return false;
                fraction = fraction * 10 + digit;
            }

            ticks = ((hours * 60 + minutes) * 60 + seconds) * TimeSpan.TicksPerSecond + fraction;
            return true;
        }

        /// <summary>Decodes two ASCII clock digits with the supplied field limit.</summary>
        /// <param name="first">The tens character.</param>
        /// <param name="second">The units character.</param>
        /// <param name="maximum">The highest valid value for this field.</param>
        /// <param name="value">The parsed field.</param>
        /// <returns>Whether both characters are digits and the field is in range.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseClockPair(char first, char second, uint maximum, out uint value)
        {
            uint high = (uint)(first - '0');
            uint low = (uint)(second - '0');
            value = high * 10 + low;
            return high <= 9 && low <= 9 && value <= maximum;
        }

        /// <summary>Parses the exact invariant round-trip form of a decoded date token.</summary>
        /// <param name="text">The complete decoded token.</param>
        /// <returns>The parsed date.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static DateOnly ParseRoundTripDate(scoped ReadOnlySpan<char> text)
        {
            if (text.Length == 10 && text[4] == '-' && text[7] == '-' &&
                TryParseClockPair(text[0], text[1], 99, out uint century) &&
                TryParseClockPair(text[2], text[3], 99, out uint yearOfCentury) &&
                TryParseClockPair(text[5], text[6], 12, out uint month) &&
                TryParseClockPair(text[8], text[9], 31, out uint day))
            {
                int year = (int)(century * 100 + yearOfCentury);
                if (year != 0 && month != 0 && day != 0 &&
                    (day <= 28 || day <= DateTime.DaysInMonth(year, (int)month)))
                    return new DateOnly(year, (int)month, (int)day);
            }

            return DateOnly.ParseExact(text, "O", CultureInfo.InvariantCulture);
        }

        /// <summary>Recognizes built-in profiles whose encoding and token rules have no user callbacks.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>Whether token reads use only built-in option getters.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanReadQuotedScalarToken<TOptions>() where TOptions : struct, ILuminPackOptions =>
            LuminPackMarshal.ConstantStringProfile<TOptions>() ||
            typeof(TOptions) == typeof(LuminPackOptions<LuminPackUtf8LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>) ||
            typeof(TOptions) == typeof(LuminPackOptions<LuminPackUtf16LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>) ||
            typeof(TOptions) == typeof(LuminPackFixedOptions<LuminPackOptions<LuminPackUtf8LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>) ||
            typeof(TOptions) == typeof(LuminPackFixedOptions<LuminPackOptions<LuminPackUtf16LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>);

        /// <summary>Parses a complete private character range through the framework's span overloads.</summary>
        /// <typeparam name="T">The validated framework scalar type.</typeparam>
        /// <param name="text">The isolated characters.</param>
        /// <returns>The parsed scalar.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ParseQuotedScalar<T>(scoped ReadOnlySpan<char> text) where T : struct
        {
            if (typeof(T) == typeof(DateTime))
            {
                DateTime value = DateTime.Parse(text, null, DateTimeStyles.RoundtripKind);
                return Unsafe.As<DateTime, T>(ref value);
            }
            if (typeof(T) == typeof(DateTimeOffset))
            {
                DateTimeOffset value = DateTimeOffset.Parse(text, null, DateTimeStyles.RoundtripKind);
                return Unsafe.As<DateTimeOffset, T>(ref value);
            }
            if (typeof(T) == typeof(TimeSpan))
            {
                TimeSpan value = TimeSpan.Parse(text);
                return Unsafe.As<TimeSpan, T>(ref value);
            }
            if (typeof(T) == typeof(TimeOnly))
            {
                TimeOnly value = TimeOnly.Parse(text);
                return Unsafe.As<TimeOnly, T>(ref value);
            }
            if (typeof(T) == typeof(Int128))
            {
                Int128 value = Int128.Parse(text, CultureInfo.InvariantCulture);
                return Unsafe.As<Int128, T>(ref value);
            }
            if (typeof(T) == typeof(UInt128))
            {
                UInt128 value = UInt128.Parse(text, CultureInfo.InvariantCulture);
                return Unsafe.As<UInt128, T>(ref value);
            }
            Guid guid = Guid.Parse(text);
            return Unsafe.As<Guid, T>(ref guid);
        }

        /// <summary>Preserves the original string overloads, including their behavior on null tokens.</summary>
        /// <typeparam name="T">The validated framework scalar type.</typeparam>
        /// <param name="text">The decoded string from the original reader.</param>
        /// <returns>The parsed scalar.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ParseQuotedScalar<T>(string text) where T : struct
        {
            if (typeof(T) == typeof(DateTime))
            {
                DateTime value = DateTime.Parse(text, null, DateTimeStyles.RoundtripKind);
                return Unsafe.As<DateTime, T>(ref value);
            }
            if (typeof(T) == typeof(DateTimeOffset))
            {
                DateTimeOffset value = DateTimeOffset.Parse(text, null, DateTimeStyles.RoundtripKind);
                return Unsafe.As<DateTimeOffset, T>(ref value);
            }
            if (typeof(T) == typeof(TimeSpan))
            {
                TimeSpan value = TimeSpan.Parse(text);
                return Unsafe.As<TimeSpan, T>(ref value);
            }
            if (typeof(T) == typeof(TimeOnly))
            {
                TimeOnly value = TimeOnly.Parse(text);
                return Unsafe.As<TimeOnly, T>(ref value);
            }
            if (typeof(T) == typeof(Int128))
            {
                Int128 value = Int128.Parse(text, CultureInfo.InvariantCulture);
                return Unsafe.As<Int128, T>(ref value);
            }
            if (typeof(T) == typeof(UInt128))
            {
                UInt128 value = UInt128.Parse(text, CultureInfo.InvariantCulture);
                return Unsafe.As<UInt128, T>(ref value);
            }
            Guid guid = Guid.Parse(text);
            return Unsafe.As<Guid, T>(ref guid);
        }
#endif

        /// <summary>Reads an invariant quoted decimal integer with the library default profile.</summary>
        /// <returns>The parsed arbitrary-precision integer.</returns>
        public System.Numerics.BigInteger ReadBigInteger() => ReadBigInteger<LuminPackDefaultOptions>();

        /// <summary>Parses the complete decoded string before publishing the integer.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>The parsed arbitrary-precision integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public System.Numerics.BigInteger ReadBigInteger<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadBigIntegerOperation<TOptions>, System.Numerics.BigInteger>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadBigIntegerOperation<TOptions> : ILuminPackJsonReadOperation<System.Numerics.BigInteger> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public System.Numerics.BigInteger Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadBigIntegerEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private System.Numerics.BigInteger ReadBigIntegerEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if (CurrentTokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null or JsonTokenType.PropertyName) && !Read<TOptions>())
                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value.");
            if (CurrentTokenType == JsonTokenType.Null)
            {
                // Keep the original BCL null-string exception after consuming the token.
                CompleteValue();
                return System.Numerics.BigInteger.Parse((string)null!, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                var utf8 = ReadStringUtf8<TOptions>();
                CompleteValue();
                return ParseBigIntegerUtf8(utf8);
            }
            var text = ReadStringUtf16<TOptions>();
            CompleteValue();
#if NET10_0_OR_GREATER
            return System.Numerics.BigInteger.Parse(text, provider: System.Globalization.CultureInfo.InvariantCulture);
#else
            return ParseBigIntegerText(text);
#endif
        }

        /// <summary>Decodes UTF-8 with platform replacement semantics without allocating a token string.</summary>
        /// <param name="utf8">The complete unescaped token content.</param>
        /// <returns>The parsed arbitrary-precision integer.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static System.Numerics.BigInteger ParseBigIntegerUtf8(ReadOnlySpan<byte> utf8)
        {
            char[]? rented = null;
            Span<char> text = utf8.Length <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent(utf8.Length));
            try
            {
                int written = Utf8PlatformDecoder.Decode(utf8, text);
#if NET10_0_OR_GREATER
                return System.Numerics.BigInteger.Parse(text.Slice(0, written), provider: System.Globalization.CultureInfo.InvariantCulture);
#else
                return ParseBigIntegerText(text.Slice(0, written));
#endif
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<char>.Shared.Return(rented);
            }
        }

#if !NET10_0_OR_GREATER
        /// <summary>Parses invariant decimal groups without older BCL token-building allocations.</summary>
        /// <param name="text">The complete decoded string.</param>
        /// <returns>The integer, with only its final magnitude allocated on the ordinary decimal path.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static System.Numerics.BigInteger ParseBigIntegerText(ReadOnlySpan<char> text)
        {
            // Retain the BCL's asymptotically faster parser beyond its naive-conversion range.
            if (text.Length > 20_000)
                goto Fallback;
            int start = 0, end = text.Length;
            while (end != 0 && text[end - 1] == '\0')
                end--;
            while (start < end && (text[start] == ' ' || (uint)(text[start] - '\t') <= 4))
                start++;
            while (end > start && (text[end - 1] == ' ' || (uint)(text[end - 1] - '\t') <= 4))
                end--;
            bool negative = start < end && text[start] == '-';
            if (start < end && (negative || text[start] == '+'))
                start++;
            if (start == end)
                goto Fallback;
            for (int i = start; i < end; i++)
                if ((uint)(text[i] - '0') > 9)
                    goto Fallback;
            while (start < end && text[start] == '0')
                start++;
            if (end - start <= 18)
            {
                long small = 0;
                for (int i = start; i < end; i++)
                    small = small * 10 + text[i] - '0';
                return new System.Numerics.BigInteger(negative ? -small : small);
            }
            int capacity = (end - start + 8) / 9;
            uint[]? rented = null;
            Span<uint> words = capacity <= 64 ? stackalloc uint[64] : (rented = ArrayPool<uint>.Shared.Rent(capacity));
            try
            {
                int used = 0;
                while (start < end)
                {
                    int groupEnd = Math.Min(start + 9, end);
                    uint group = 0, scale = 1;
                    while (start < groupEnd)
                    {
                        group = group * 10 + (uint)(text[start++] - '0');
                        scale *= 10;
                    }
                    ulong carry = group;
                    for (int i = 0; i < used; i++)
                    {
                        ulong expanded = (ulong)words[i] * scale + carry;
                        words[i] = (uint)expanded;
                        carry = expanded >> 32;
                    }
                    if (carry != 0)
                        words[used++] = (uint)carry;
                }
                words = words.Slice(0, used);
                if (!BitConverter.IsLittleEndian)
                    for (int i = 0; i < used; i++)
                        words[i] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(words[i]);
                var magnitude = new System.Numerics.BigInteger(MemoryMarshal.AsBytes(words), isUnsigned: true);
                return negative ? -magnitude : magnitude;
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<uint>.Shared.Return(rented);
            }
        Fallback:
            return System.Numerics.BigInteger.Parse(text, provider: System.Globalization.CultureInfo.InvariantCulture);
        }
#endif

        /// <summary>Reads a time-zone snapshot using the library default profile.</summary>
        /// <returns>The cached or newly parsed zone.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeZoneInfo? ReadTimeZoneInfo() => ReadTimeZoneInfo<LuminPackDefaultOptions>();

        /// <summary>Reads complete snapshot characters before consulting the existing bounded time-zone cache.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>The original cached or newly parsed zone.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeZoneInfo? ReadTimeZoneInfo<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadTimeZoneInfoOperation<TOptions>, TimeZoneInfo?>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadTimeZoneInfoOperation<TOptions> : ILuminPackJsonReadOperation<TimeZoneInfo?> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TimeZoneInfo? Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadTimeZoneInfoEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private TimeZoneInfo? ReadTimeZoneInfoEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if (CurrentTokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null or JsonTokenType.PropertyName) && !Read<TOptions>())
                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value.");
            if (CurrentTokenType == JsonTokenType.Null)
            {
                CompleteValue();
                // Generated selected-null branches return before this entry. A null discovered here
                // retains the original Read<string> followed by Parse failure and cursor state.
                return LuminPackTimeZoneInfoCache.Parse(null!);
            }
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
                return ParseTimeZoneUtf8(ReadStringUtf8<TOptions>());
            var text = ReadStringUtf16<TOptions>();
            CompleteValue();
            return LuminPackTimeZoneInfoCache.ParseSnapshot(text);
        }

        /// <summary>Uses bounded character scratch only for snapshots that the existing cache may retain.</summary>
        /// <param name="utf8">The complete unescaped token.</param>
        /// <returns>The original cached or newly parsed zone.</returns>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private TimeZoneInfo ParseTimeZoneUtf8(ReadOnlySpan<byte> utf8)
        {
            const int limit = LuminPackTimeZoneInfoCache.MaximumCachedTextLength;
            // A UTF-16 code unit accounts for at most three UTF-8 bytes. Longer snapshots cannot fit
            // the cache, so retain the original decode without an additional full-input count pass.
            if (utf8.Length <= limit * 3)
            {
                Span<char> buffer = stackalloc char[limit];
                if (Utf8PlatformDecoder.TryDecode(utf8, buffer, out int length))
                {
                    CompleteValue();
                    return LuminPackTimeZoneInfoCache.ParseSnapshot(buffer.Slice(0, length));
                }
            }
            string source = CreateUtf8String(utf8);
            CompleteValue();
            return LuminPackTimeZoneInfoCache.Parse(source);
        }

        /// <summary>Reads a version string with the library default profile.</summary>
        /// <returns>The parsed version, or null.</returns>
        public Version? ReadVersion() => ReadVersion<LuminPackDefaultOptions>();

        /// <summary>Parses the complete token through the available BCL span parser before publishing its result.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>The parsed version, or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Version? ReadVersion<TOptions>() where TOptions : struct, ILuminPackOptions => ReadVersionValue<TOptions>();

        /// <summary>Keeps span decoding and BCL parsing state outside generated callers.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>The parsed version, or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Version? ReadVersionValue<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadVersionValueOperation<TOptions>, Version?>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadVersionValueOperation<TOptions> : ILuminPackJsonReadOperation<Version?> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Version? Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadVersionValueEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Version? ReadVersionValueEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET10_0_OR_GREATER
            if (CurrentTokenType == JsonTokenType.String)
                goto SelectedString;
#endif
            if (CurrentTokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null or JsonTokenType.PropertyName) && !Read<TOptions>())
                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value.");
            if (CurrentTokenType == JsonTokenType.Null)
            {
                CompleteValue();
                return null;
            }
#if NET10_0_OR_GREATER
            // The original unselected path decoded characters before parsing, including in its exception messages.
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
                return ParseVersionUtf8(ReadStringUtf8<TOptions>());
        SelectedString:
#endif
            if (Unsafe.SizeOf<TCodeUnit>() != 1)
            {
                var text = ReadStringUtf16<TOptions>();
                CompleteValue();
                return Version.Parse(text);
            }
            var utf8 = ReadStringUtf8<TOptions>();
#if NET10_0_OR_GREATER
            CompleteValue();
            return Version.Parse(utf8);
#else
            return ParseVersionUtf8(utf8);
#endif
        }

        /// <summary>Decodes characters before completing the token and calling the original character parser.</summary>
        /// <param name="utf8">The complete unescaped token.</param>
        /// <returns>The newly parsed version.</returns>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Version ParseVersionUtf8(ReadOnlySpan<byte> utf8)
        {
            using var buffer = new Utf8TextBuffer(utf8.Length, stackalloc char[64]);
            var decoded = buffer.Decode(utf8);
            CompleteValue();
            return Version.Parse(decoded);
        }

        /// <summary>Reads a complete builder value with the library default profile.</summary>
        /// <param name="value">The reusable builder target.</param>
        public void ReadStringBuilder(ref StringBuilder? value) => ReadStringBuilder<LuminPackDefaultOptions>(ref value);

        /// <summary>Reuses a builder after complete decoding while preserving fresh construction and failure publication.</summary>
        /// <param name="value">The reusable builder target.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ReadStringBuilder<TOptions>(ref StringBuilder? value) where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType == JsonTokenType.String)
                goto SelectedString;
            if (CurrentTokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null or JsonTokenType.PropertyName) && !Read<TOptions>())
                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value.");
            if (CurrentTokenType == JsonTokenType.Null)
            {
                CompleteValue();
                value = null;
                return;
            }
        SelectedString:
            if (default(TOptions).JsonByteCount(1) != 1)
            {
                var text = ReadStringUtf16<TOptions>();
                CompleteValue();
                PopulateStringBuilder(ref value, text);
                return;
            }
            var utf8 = ReadStringUtf8<TOptions>();
            using var buffer = new Utf8TextBuffer(utf8.Length, stackalloc char[256]);
            var decoded = buffer.Decode(utf8);
            CompleteValue();
            PopulateStringBuilder(ref value, decoded);
        }

        /// <summary>Preserves the fresh string constructor's capacity and the existing clear-before-append reuse contract.</summary>
        /// <param name="value">The target published after fresh construction or cleared before reuse.</param>
        /// <param name="text">The completely decoded token content.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PopulateStringBuilder(ref StringBuilder? value, ReadOnlySpan<char> text)
        {
            if (value is null)
            {
                var created = new StringBuilder(Math.Max(16, text.Length));
                created.Append(text);
                value = created;
            }
            else
            {
                value.Clear();
                value.Append(text);
            }
        }

        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref string value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref string value)
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null or JsonTokenType.PropertyName) && !Read<TOptions>())
                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value.");
            if (CurrentTokenType == JsonTokenType.Null)
            {
                value = null!;
                CompleteValue();
                return;
            }
            if (CurrentTokenType != JsonTokenType.String)
                LuminPackExceptionHelper.ThrowFormatException("Expected string token");
            if (default(TOptions).JsonByteCount(1) == 1)
            {
                var text = ReadStringUtf8<TOptions>();
                if (value is null || !StringSerializer.MatchesUtf8(text, value))
                    value = CreateUtf8String(text);
            }
            else
            {
                var text = ReadStringUtf16<TOptions>();
                if (value is null || !text.SequenceEqual(value.AsSpan()))
                    value = new string(text);
            }
            CompleteValue();
        }
        /// <summary>Raises the exception for malformed json.</summary>
        /// <param name = "message">The message describing the failure.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowMalformedJson(string message) => LuminPackExceptionHelper.ThrowFormatException(message);
        /// <summary>Performs the skip string token step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringToken()
        {
            SkipStringToken<LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadStringUtf8()
        {
            return ReadStringUtf8<LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadStringUtf8(bool unescape)
        {
            return ReadStringUtf8<LuminPackDefaultOptions>(unescape);
        }

        /// <summary>Reads string from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ReadString()
        {
            return ReadString<LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> ReadStringUtf16()
        {
            return ReadStringUtf16<LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf16 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadStringUtf16(bool unescape)
        {
            return ReadStringUtf16<LuminPackDefaultOptions>(unescape);
        }

        /// <summary>Performs the skip string utf8 step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf8()
        {
            SkipStringUtf8<LuminPackDefaultOptions>();
        }

        /// <summary>Performs the skip string utf16 step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf16()
        {
            SkipStringUtf16<LuminPackDefaultOptions>();
        }

        // The shared scanner already checked literal control characters and the closing quote.
        // Skipping still validates every escape, but never creates a scratch buffer.
        /// <summary>Validates string escapes before the operation continues.</summary>
        /// <param name = "source">The input data to process.</param>
        private static void ValidateStringEscapes(ReadOnlySpan<byte> source)
        {
            int index;
            while ((index = source.IndexOf((byte)'\\')) >= 0)
            {
                source = source.Slice(index + 1);
                if (source.IsEmpty)
                    LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                byte escape = source[0];
                source = source.Slice(1);
                switch (escape)
                {
                    case (byte)'"':
                    case (byte)'\\':
                    case (byte)'/':
                    case (byte)'b':
                    case (byte)'f':
                    case (byte)'n':
                    case (byte)'r':
                    case (byte)'t':
                        break;
                    case (byte)'u':
                    {
                        int codeUnit = ParseHex4(source, 0);
                        source = source.Slice(4);
                        if ((uint)(codeUnit - 0xD800) <= 0x3FFu)
                        {
                            if (source.Length < 6 || source[0] != '\\' || source[1] != 'u')
                                LuminPackExceptionHelper.ThrowFormatException("A high surrogate must be followed by a low surrogate");
                            int low = ParseHex4(source, 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                LuminPackExceptionHelper.ThrowFormatException("Invalid low surrogate in JSON string");
                            source = source.Slice(6);
                        }
                        else if ((uint)(codeUnit - 0xDC00) <= 0x3FFu)
                            LuminPackExceptionHelper.ThrowFormatException("Unexpected low surrogate in JSON string");
                        break;
                    }

                    default:
                        LuminPackExceptionHelper.ThrowFormatException("Invalid JSON escape sequence");
                        break;
                }
            }
        }

        /// <summary>Validates string escapes before the operation continues.</summary>
        /// <param name = "source">The input data to process.</param>
        private static void ValidateStringEscapes(ReadOnlySpan<char> source)
        {
            int index;
            while ((index = source.IndexOf('\\')) >= 0)
            {
                source = source.Slice(index + 1);
                if (source.IsEmpty)
                    LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                char escape = source[0];
                source = source.Slice(1);
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/':
                    case 'b':
                    case 'f':
                    case 'n':
                    case 'r':
                    case 't':
                        break;
                    case 'u':
                    {
                        int codeUnit = ParseHex4(source, 0);
                        source = source.Slice(4);
                        if ((uint)(codeUnit - 0xD800) <= 0x3FFu)
                        {
                            if (source.Length < 6 || source[0] != '\\' || source[1] != 'u')
                                LuminPackExceptionHelper.ThrowFormatException("A high surrogate must be followed by a low surrogate");
                            int low = ParseHex4(source, 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                LuminPackExceptionHelper.ThrowFormatException("Invalid low surrogate in JSON string");
                            source = source.Slice(6);
                        }
                        else if ((uint)(codeUnit - 0xDC00) <= 0x3FFu)
                            LuminPackExceptionHelper.ThrowFormatException("Unexpected low surrogate in JSON string");
                        break;
                    }

                    default:
                        LuminPackExceptionHelper.ThrowFormatException("Invalid JSON escape sequence");
                        break;
                }
            }
        }

        /// <summary>Performs the unescape utf8 step for the current operation.</summary>
        /// <param name = "source">The input data to process.</param>
        /// <returns>The result of the operation.</returns>
        private ReadOnlySpan<byte> UnescapeUtf8(ReadOnlySpan<byte> source)
        {
            if (Utf8StringScratch is null || Utf8StringScratch.Length < source.Length)
            {
                var state = OptionState;
                if (state.JsonUtf8StringScratch is null || state.JsonUtf8StringScratch.Length < source.Length)
                    state.JsonUtf8StringScratch = new byte[source.Length];
                Utf8StringScratch = state.JsonUtf8StringScratch;
            }
            Span<byte> destination = Utf8StringScratch;
            int sourceIndex = 0;
            int destinationIndex = 0;
#if NET8_0_OR_GREATER
            int literalProbe = 16;
#endif
            while (sourceIndex < source.Length)
            {
                byte value = source[sourceIndex++];
                if (value != '\\')
                {
                    if (value < 0x20)
                        LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
#if NET8_0_OR_GREATER
                    if (sourceIndex == literalProbe && source.Length - sourceIndex >= 16 && source[sourceIndex] != '\\')
                    {
                        var remaining = source.Slice(sourceIndex);
                        int runLength = remaining.IndexOfAny(NumberCharLookup.StringSpecialBytes);
                        if (runLength < 0)
                            runLength = remaining.Length;
                        // Public scratch can alias the input, so keep overlap-safe copying.
                        remaining.Slice(0, runLength).CopyTo(destination.Slice(destinationIndex));
                        sourceIndex += runLength;
                        destinationIndex += runLength;
                        literalProbe = unchecked(sourceIndex + 16);
                    }
#endif
                    continue;
                }

                if (sourceIndex >= source.Length)
                    LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                byte escape = source[sourceIndex++];
                switch (escape)
                {
                    case (byte)'"':
                        destination[destinationIndex++] = (byte)'"';
                        break;
                    case (byte)'\\':
                        destination[destinationIndex++] = (byte)'\\';
                        break;
                    case (byte)'/':
                        destination[destinationIndex++] = (byte)'/';
                        break;
                    case (byte)'b':
                        destination[destinationIndex++] = (byte)'\b';
                        break;
                    case (byte)'f':
                        destination[destinationIndex++] = (byte)'\f';
                        break;
                    case (byte)'n':
                        destination[destinationIndex++] = (byte)'\n';
                        break;
                    case (byte)'r':
                        destination[destinationIndex++] = (byte)'\r';
                        break;
                    case (byte)'t':
                        destination[destinationIndex++] = (byte)'\t';
                        break;
                    case (byte)'u':
                    {
                        int scalar = ParseHex4(source, sourceIndex);
                        sourceIndex += 4;
                        if ((uint)(scalar - 0xD800) <= 0x3FFu)
                        {
                            if (sourceIndex + 6 > source.Length || source[sourceIndex] != '\\' || source[sourceIndex + 1] != 'u')
                            {
                                LuminPackExceptionHelper.ThrowFormatException("A high surrogate must be followed by a low surrogate");
                            }

                            int low = ParseHex4(source, sourceIndex + 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                LuminPackExceptionHelper.ThrowFormatException("Invalid low surrogate in JSON string");
                            sourceIndex += 6;
                            scalar = 0x10000 + ((scalar - 0xD800) << 10) + (low - 0xDC00);
                        }
                        else if ((uint)(scalar - 0xDC00) <= 0x3FFu)
                        {
                            LuminPackExceptionHelper.ThrowFormatException("Unexpected low surrogate in JSON string");
                        }

                        WriteUtf8Scalar(destination, ref destinationIndex, scalar);
                        break;
                    }

                    default:
                        LuminPackExceptionHelper.ThrowFormatException("Invalid JSON escape sequence");
                        break;
                }
#if NET8_0_OR_GREATER
                literalProbe = unchecked(sourceIndex + 16);
#endif
            }

            return Utf8StringScratch.AsSpan(0, destinationIndex);
        }

        /// <summary>Performs the unescape utf16 step for the current operation.</summary>
        /// <param name = "source">The input data to process.</param>
        /// <returns>The result of the operation.</returns>
        private ReadOnlySpan<char> UnescapeUtf16(ReadOnlySpan<char> source)
        {
            if (Utf16StringScratch is null || Utf16StringScratch.Length < source.Length)
            {
                var state = OptionState;
                if (state.JsonUtf16StringScratch is null || state.JsonUtf16StringScratch.Length < source.Length)
                    state.JsonUtf16StringScratch = new char[source.Length];
                Utf16StringScratch = state.JsonUtf16StringScratch;
            }
            Span<char> destination = Utf16StringScratch;
            int sourceIndex = 0;
            int destinationIndex = 0;
#if NET8_0_OR_GREATER
            int literalProbe = 16;
#endif
            while (sourceIndex < source.Length)
            {
                char value = source[sourceIndex++];
                if (value != '\\')
                {
                    if (value < 0x20)
                        LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
#if NET8_0_OR_GREATER
                    if (sourceIndex == literalProbe && source.Length - sourceIndex >= 16 && source[sourceIndex] != '\\')
                    {
                        var remaining = source.Slice(sourceIndex);
                        int runLength = remaining.IndexOfAny(NumberCharLookup.StringSpecialChars);
                        if (runLength < 0)
                            runLength = remaining.Length;
                        // Public scratch can alias the input, so keep overlap-safe copying.
                        remaining.Slice(0, runLength).CopyTo(destination.Slice(destinationIndex));
                        sourceIndex += runLength;
                        destinationIndex += runLength;
                        literalProbe = unchecked(sourceIndex + 16);
                    }
#endif
                    continue;
                }

                if (sourceIndex >= source.Length)
                    LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                char escape = source[sourceIndex++];
                switch (escape)
                {
                    case '"':
                        destination[destinationIndex++] = '"';
                        break;
                    case '\\':
                        destination[destinationIndex++] = '\\';
                        break;
                    case '/':
                        destination[destinationIndex++] = '/';
                        break;
                    case 'b':
                        destination[destinationIndex++] = '\b';
                        break;
                    case 'f':
                        destination[destinationIndex++] = '\f';
                        break;
                    case 'n':
                        destination[destinationIndex++] = '\n';
                        break;
                    case 'r':
                        destination[destinationIndex++] = '\r';
                        break;
                    case 't':
                        destination[destinationIndex++] = '\t';
                        break;
                    case 'u':
                    {
                        int codeUnit = ParseHex4(source, sourceIndex);
                        sourceIndex += 4;
                        if ((uint)(codeUnit - 0xD800) <= 0x3FFu)
                        {
                            if (sourceIndex + 6 > source.Length || source[sourceIndex] != '\\' || source[sourceIndex + 1] != 'u')
                            {
                                LuminPackExceptionHelper.ThrowFormatException("A high surrogate must be followed by a low surrogate");
                            }

                            int low = ParseHex4(source, sourceIndex + 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                LuminPackExceptionHelper.ThrowFormatException("Invalid low surrogate in JSON string");
                            sourceIndex += 6;
                            destination[destinationIndex++] = (char)codeUnit;
                            destination[destinationIndex++] = (char)low;
                        }
                        else if ((uint)(codeUnit - 0xDC00) <= 0x3FFu)
                        {
                            LuminPackExceptionHelper.ThrowFormatException("Unexpected low surrogate in JSON string");
                        }
                        else
                        {
                            destination[destinationIndex++] = (char)codeUnit;
                        }

                        break;
                    }

                    default:
                        LuminPackExceptionHelper.ThrowFormatException("Invalid JSON escape sequence");
                        break;
                }
#if NET8_0_OR_GREATER
                literalProbe = unchecked(sourceIndex + 16);
#endif
            }

            return Utf16StringScratch.AsSpan(0, destinationIndex);
        }

        /// <summary>Writes utf8 scalar to the current output.</summary>
        /// <param name = "destination">The destination receiving the result.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "scalar">The scalar used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUtf8Scalar(Span<byte> destination, ref int index, int scalar)
        {
            if (scalar <= 0x7F)
            {
                destination[index++] = (byte)scalar;
            }
            else if (scalar <= 0x7FF)
            {
                destination[index++] = (byte)(0xC0 | (scalar >> 6));
                destination[index++] = (byte)(0x80 | (scalar & 0x3F));
            }
            else if (scalar <= 0xFFFF)
            {
                destination[index++] = (byte)(0xE0 | (scalar >> 12));
                destination[index++] = (byte)(0x80 | ((scalar >> 6) & 0x3F));
                destination[index++] = (byte)(0x80 | (scalar & 0x3F));
            }
            else
            {
                destination[index++] = (byte)(0xF0 | (scalar >> 18));
                destination[index++] = (byte)(0x80 | ((scalar >> 12) & 0x3F));
                destination[index++] = (byte)(0x80 | ((scalar >> 6) & 0x3F));
                destination[index++] = (byte)(0x80 | (scalar & 0x3F));
            }
        }

        /// <summary>Reads string choice from the current input.</summary>
        /// <param name = "firstUtf8">The first utf8 used by this operation.</param>
        /// <param name = "firstUtf16">The first utf16 used by this operation.</param>
        /// <param name = "secondUtf8">The second utf8 used by this operation.</param>
        /// <param name = "secondUtf16">The second utf16 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int ReadStringChoice(ReadOnlySpan<byte> firstUtf8, ReadOnlySpan<char> firstUtf16, ReadOnlySpan<byte> secondUtf8, ReadOnlySpan<char> secondUtf16)
        {
            return ReadStringChoice<LuminPackDefaultOptions>(firstUtf8, firstUtf16, secondUtf8, secondUtf16);
        }

        /// <summary>Performs the parse unsigned digits step for the current operation.</summary>
        /// <param name = "start">The start used by this operation.</param>
        /// <param name = "end">The end used by this operation.</param>
        /// <param name = "error">The error used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ParseUnsignedDigits(int start, int end, string error)
        {
            return ParseUnsignedDigits<LuminPackDefaultOptions>(start, end, error);
        }

#if NET8_0_OR_GREATER
        /// <summary>Bounds the legacy parser to complete mantissas with an unambiguous nonzero tail.</summary>
        /// <param name="span">The nonempty ASCII number token already validated by numeric scanning.</param>
        /// <returns>Whether the parser can avoid its untrimmed, all-zero fractional-tail rounding path.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanParseShortFloatingPointUtf8(ReadOnlySpan<byte> span)
        {
            if ((uint)(span.Length - 1) >= 19u)
                return false;
            int exponent = span.IndexOfAny((byte)'e', (byte)'E');
            return span[exponent < 0 ? span.Length - 1 : exponent - 1] != (byte)'0';
        }
#endif

        /// <summary>Performs the fallback parse float utf8 step for the current operation.</summary>
        /// <param name = "span">The buffer used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf8(ReadOnlySpan<byte> span)
        {
#if NET8_0_OR_GREATER
            float v;
            bool parsed;
            if (CanParseShortFloatingPointUtf8(span))
                parsed = Utf8Parser.TryParse(span, out v, out int consumed) && consumed == span.Length;
            else
                parsed = float.TryParse(span, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out v);
            if (!parsed)
                LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (float.IsInfinity(v))
                LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
#else
            char[]? rented = null;
            Span<char> chars = span.Length <= 256 ? stackalloc char[span.Length] : (rented = ArrayPool<char>.Shared.Rent(span.Length));
            try
            {
                // Numeric scanning has already validated the complete ASCII token.
                for (int i = 0; i < span.Length; i++)
                    chars[i] = (char)span[i];
                return FallbackParseFloatUtf16(chars.Slice(0, span.Length));
            }
            finally
            {
                if (rented != null)
                    ArrayPool<char>.Shared.Return(rented);
            }
#endif
        }

        /// <summary>Performs the fallback parse float utf16 step for the current operation.</summary>
        /// <param name = "chars">The chars used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf16(ReadOnlySpan<char> chars)
        {
            if (!float.TryParse(chars, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out float v))
                LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (float.IsInfinity(v))
                LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Performs the fallback parse double utf8 step for the current operation.</summary>
        /// <param name = "span">The buffer used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf8(ReadOnlySpan<byte> span)
        {
#if NET8_0_OR_GREATER
            double v;
            bool parsed;
            if (CanParseShortFloatingPointUtf8(span))
                parsed = Utf8Parser.TryParse(span, out v, out int consumed) && consumed == span.Length;
            else
                parsed = double.TryParse(span, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out v);
            if (!parsed)
                LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (double.IsInfinity(v))
                LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
#else
            char[]? rented = null;
            Span<char> chars = span.Length <= 256 ? stackalloc char[span.Length] : (rented = ArrayPool<char>.Shared.Rent(span.Length));
            try
            {
                for (int i = 0; i < span.Length; i++)
                    chars[i] = (char)span[i];
                return FallbackParseDoubleUtf16(chars.Slice(0, span.Length));
            }
            finally
            {
                if (rented != null)
                    ArrayPool<char>.Shared.Return(rented);
            }
#endif
        }

        /// <summary>Performs the fallback parse double utf16 step for the current operation.</summary>
        /// <param name = "chars">The chars used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf16(ReadOnlySpan<char> chars)
        {
            if (!double.TryParse(chars, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out double v))
                LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (double.IsInfinity(v))
                LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Performs the skip string token step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipStringToken<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            // The scalar token switch already selected a string or a property name.
            // Keep the original encoding checks for profiles that can execute customer code.
            if (default(TOptions).JsonByteCount(1) == 1)
            {
                if (LuminPackMarshal.ConstantStringProfile<TOptions>())
                    _ = ReadSelectedStringUtf8<TOptions>(false);
                else
                    SkipStringUtf8<TOptions>();
            }
            else if (LuminPackMarshal.ConstantStringProfile<TOptions>())
                _ = ReadSelectedStringUtf16<TOptions>(false);
            else
                SkipStringUtf16<TOptions>();
        }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadStringUtf8<TOptions>()
            where TOptions : struct, ILuminPackOptions => ReadStringUtf8<TOptions>(true);
        /// <summary>Reads string utf8 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadStringUtf8<TOptions>(bool unescape)
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");
            if (!(default(TOptions).JsonByteCount(1) == 1))
                LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-16 JSON");
            return ReadSelectedStringUtf8<TOptions>(unescape);
        }

        /// <summary>Checks and consumes UTF-8 content after selecting a string token and its encoding.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <param name="unescape">Whether to decode escapes after validating them.</param>
        /// <returns>The string content.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadSelectedStringUtf8<TOptions>(bool unescape)
            where TOptions : struct, ILuminPackOptions
        {
            Advance(1);
            int start = Position;
            int scan = Position;
#if NET8_0_OR_GREATER
            var remaining = Buffer.Slice(Position);
            int special = remaining.IndexOfAny(NumberCharLookup.StringSpecialBytes);
            if (special >= 0 && remaining[special] == '"')
            {
                default(TOptions).ValidateJsonUtf8(remaining.Slice(0, special));
                AdvanceFromPosition(start, special + 1);
                return remaining.Slice(0, special);
            }

            if (special < 0)
                LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            if (remaining[special] < 0x20)
                LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
            scan += special;
#endif
            return ReadStringUtf8Escaped<TOptions>(start, scan, unescape);
        }

        /// <summary>Scans a string token containing escapes or other special bytes, outside the inlined fast path.</summary>
        /// <param name = "start">The first content position.</param>
        /// <param name = "scan">The first position not yet scanned.</param>
        /// <param name = "unescape">Whether escapes are decoded rather than only validated.</param>
        /// <returns>The string content.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ReadOnlySpan<byte> ReadStringUtf8Escaped<TOptions>(int start, int scan, bool unescape)
            where TOptions : struct, ILuminPackOptions
        {
            bool hasEscape = false;
#if NET8_0_OR_GREATER
            // Probe only after a literal run so dense escapes keep scalar scanning.
            int literalProbe = unchecked(scan + 32);
#else
            bool scanWithSwar = Unsafe.SizeOf<IntPtr>() == 8 && Buffer.Length - scan >= 32;
#endif
            while (scan < Buffer.Length)
            {
#if !NET8_0_OR_GREATER
                if (scanWithSwar && scan + 8 <= Buffer.Length)
                {
                    ulong eight = Unsafe.ReadUnaligned<ulong>(
                        ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)scan));
                    if (!NumberCharLookup.HasJsonSpecial(eight))
                    {
                        scan += 8;
                        continue;
                    }
                }
#endif
                byte b = Buffer[scan];
                if (b == '"')
                {
                    var raw = Buffer.Slice(start, scan - start);
                    default(TOptions).ValidateJsonUtf8(raw);
                    Position = scan + 1;
                    if (!hasEscape)
                        return raw;
                    if (unescape)
                        return UnescapeUtf8(raw);
                    ValidateStringEscapes(raw);
                    return raw;
                }
                else if (b == '\\')
                {
                    hasEscape = true;
                    scan++;
                    if (scan >= Buffer.Length)
                        LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                    scan++;
#if NET8_0_OR_GREATER
                    literalProbe = unchecked(scan + 32);
#endif
                }
                else
                {
                    if (b < 0x20)
                        LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    scan++;
#if NET8_0_OR_GREATER
                    if (scan == literalProbe && Buffer.Length - scan >= 32)
                    {
                        int special = FindStringSpecial<TOptions>(scan);
                        if (special < 0)
                            break;
                        scan += special;
                    }
#endif
                }
            }

            LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            return default;
        }

        /// <summary>Reads string from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private string ReadString<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadStringOperation<TOptions>, string>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadStringOperation<TOptions> : ILuminPackJsonReadOperation<string> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public string Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadStringEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private string ReadStringEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                var utf8 = ReadStringUtf8<TOptions>();
                return CreateUtf8String(utf8);
            }
            else
            {
                var utf16 = ReadStringUtf16<TOptions>();
                return new string (utf16);
            }
        }

        /// <summary>Creates decoded UTF-8 text through the shared original string allocation kernel.</summary>
        /// <param name="utf8">The complete, already decoded token content.</param>
        /// <returns>The immutable token string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string CreateUtf8String(ReadOnlySpan<byte> utf8)
        {
#if NET8_0_OR_GREATER
            if ((uint)(utf8.Length - 1) <= 2)
            {
                var tinyAscii = StringSerializer.ReadTinyAsciiString(ref MemoryMarshal.GetReference(utf8), utf8.Length);
                if (tinyAscii is not null)
                    return tinyAscii;
            }
            if ((uint)(utf8.Length - 17) <= 15)
            {
                var ascii = global::LuminPack.Internal.ShortAsciiTranscoder.TryDecodeMedium(ref MemoryMarshal.GetReference(utf8), utf8.Length);
                if (ascii is not null)
                    return ascii;
            }

#endif
            if ((uint)(utf8.Length - 4) <= 12)
            {
                var ascii = StringSerializer.TryDecodeShortAscii(ref MemoryMarshal.GetReference(utf8), utf8.Length);
                if (ascii is not null)
                    return ascii;
            }
            return Encoding.UTF8.GetString(utf8);
        }

        /// <summary>Reads string utf16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> ReadStringUtf16<TOptions>()
            where TOptions : struct, ILuminPackOptions => ReadStringUtf16<TOptions>(true);
        /// <summary>Reads string utf16 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadStringUtf16<TOptions>(bool unescape)
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");
            if ((default(TOptions).JsonByteCount(1) == 1))
                LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-8 JSON");
            return ReadSelectedStringUtf16<TOptions>(unescape);
        }

        /// <summary>Checks and consumes UTF-16 content after selecting a string token and its encoding.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <param name="unescape">Whether to decode escapes after validating them.</param>
        /// <returns>The string content.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadSelectedStringUtf16<TOptions>(bool unescape)
            where TOptions : struct, ILuminPackOptions
        {
            int position = Position;
            AdvanceFromPosition(position, sizeof(char));
            int start = position + sizeof(char);
            int scan = start;
#if NET8_0_OR_GREATER
            int available = (int)((uint)(Buffer.Length - start) >> 1);
            ref char first = ref Unsafe.As<byte, char>(ref Current);
            var charSpan = LuminPackMarshal.CreateReadOnlySpan(ref first, available);
            int special = unescape ? -1 : FindPlainUtf16Quote<TOptions>(charSpan);
            if (special < 0)
                special = charSpan.IndexOfAny(NumberCharLookup.StringSpecialChars);
            if (special >= 0 && charSpan[special] == '"')
            {
                AdvanceFromPosition(start, (special + 1) * sizeof(char));
                return LuminPackMarshal.CreateReadOnlySpan(ref first, special);
            }

            if (special < 0)
                LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            if (charSpan[special] < 0x20)
                LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
            scan += special * 2;
#endif
            return ReadStringUtf16Escaped<TOptions>(start, scan, unescape);
        }

        /// <summary>Scans a string token containing escapes or other special bytes, outside the inlined fast path.</summary>
        /// <param name = "start">The first content position.</param>
        /// <param name = "scan">The first position not yet scanned.</param>
        /// <param name = "unescape">Whether escapes are decoded rather than only validated.</param>
        /// <returns>The string content.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ReadOnlySpan<char> ReadStringUtf16Escaped<TOptions>(int start, int scan, bool unescape)
            where TOptions : struct, ILuminPackOptions
        {
            bool hasEscape = false;
#if NET8_0_OR_GREATER
            // Positions are byte offsets; thirty-two complete UTF-16 code units form a run.
            int literalProbe = unchecked(scan + 64);
#endif
            while (scan + 1 < Buffer.Length)
            {
                char c = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(scan)));
                if (c == '"')
                {
                    int byteCount = scan - start;
                    var bytes = Buffer.Slice(start, byteCount);
                    var raw = MemoryMarshal.Cast<byte, char>(bytes);
                    Position = scan + 2;
                    if (!hasEscape)
                        return raw;
                    if (unescape)
                        return UnescapeUtf16(raw);
                    ValidateStringEscapes(raw);
                    return raw;
                }

                if (c == '\\')
                {
                    hasEscape = true;
                    scan += 2;
                    if (scan + 1 >= Buffer.Length)
                        LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                    scan += 2;
#if NET8_0_OR_GREATER
                    literalProbe = unchecked(scan + 64);
#endif
                }
                else
                {
                    if (c < 0x20)
                        LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    scan += 2;
#if NET8_0_OR_GREATER
                    if (scan == literalProbe && Buffer.Length - scan >= 64)
                    {
                        int special = FindStringSpecial<TOptions>(scan);
                        if (special < 0)
                            break;
                        scan += special;
                    }
#endif
                }
            }

            LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            return default;
        }

#if NET8_0_OR_GREATER
        /// <summary>Finds a closing quote in the first vector when no preceding character needs validation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <param name="characters">The complete UTF-16 code units inside the current input window.</param>
        /// <returns>The quote index, or minus one to use the full character search.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindPlainUtf16Quote<TOptions>(ReadOnlySpan<char> characters) where TOptions : struct, ILuminPackOptions
        {
            if (Unsafe.SizeOf<IntPtr>() != 8 || !Vector128.IsHardwareAccelerated ||
                !LuminPackMarshal.ConstantStringProfile<TOptions>() || characters.Length < Vector128<ushort>.Count)
                return -1;

            ref ushort first = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(characters));
            var block = Vector128.LoadUnsafe(ref first);
            var quote = Vector128.Equals(block, Vector128.Create((ushort)'"'));
            uint quotes = Vector128.ExtractMostSignificantBits(quote.AsByte());
            if (quotes == 0)
                return -1;

            var slash = Vector128.Equals(block, Vector128.Create((ushort)'\\'));
            var upper = block & Vector128.Create((ushort)0xffe0);
            var control = Vector128.Equals(upper, Vector128<ushort>.Zero);
            uint invalid = Vector128.ExtractMostSignificantBits((slash | control).AsByte());
            int firstQuote = BitOperations.TrailingZeroCount(quotes);
            // Each UTF-16 lane contributes two bits. Only the lanes before the first quote belong to this token.
            return (invalid & ((1u << firstQuote) - 1)) == 0 ? firstQuote >> 1 : -1;
        }

        /// <summary>Finds the next string delimiter, escape or control within the remaining input window.</summary>
        /// <typeparam name="TOptions">The compiled document encoding.</typeparam>
        /// <param name="scan">The first unscanned byte offset.</param>
        /// <returns>The byte distance to the next special character, or minus one if none remains.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int FindStringSpecial<TOptions>(int scan) where TOptions : struct, ILuminPackOptions
        {
            // Keep the bounded search setup outside the scalar loop used by dense escapes.
            if (default(TOptions).JsonByteCount(1) == 1)
                return Buffer.Slice(scan).IndexOfAny(NumberCharLookup.StringSpecialBytes);
            int available = (Buffer.Length - scan) & ~1;
            var remaining = MemoryMarshal.Cast<byte, char>(Buffer.Slice(scan, available));
            int special = remaining.IndexOfAny(NumberCharLookup.StringSpecialChars);
            return special < 0 ? -1 : special * sizeof(char);
        }
#endif

        /// <summary>Performs the skip string utf8 step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf8<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            _ = ReadStringUtf8<TOptions>(false);
        }

        /// <summary>Performs the skip string utf16 step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf16<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            _ = ReadStringUtf16<TOptions>(false);
        }

        /// <summary>Reads string choice from the current input.</summary>
        /// <param name = "firstUtf8">The first utf8 used by this operation.</param>
        /// <param name = "firstUtf16">The first utf16 used by this operation.</param>
        /// <param name = "secondUtf8">The second utf8 used by this operation.</param>
        /// <param name = "secondUtf16">The second utf16 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int ReadStringChoice<TOptions>(ReadOnlySpan<byte> firstUtf8, ReadOnlySpan<char> firstUtf16, ReadOnlySpan<byte> secondUtf8, ReadOnlySpan<char> secondUtf16)
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).JsonByteCount(1) == 1))
            {
                var value = ReadStringUtf8<TOptions>();
                if (value.SequenceEqual(firstUtf8))
                    return 1;
                if (value.SequenceEqual(secondUtf8))
                    return 2;
            }
            else
            {
                var value = ReadStringUtf16<TOptions>();
                if (value.SequenceEqual(firstUtf16))
                    return 1;
                if (value.SequenceEqual(secondUtf16))
                    return 2;
            }

            return 0;
        }

        /// <summary>Performs the parse unsigned digits step for the current operation.</summary>
        /// <param name = "start">The start used by this operation.</param>
        /// <param name = "end">The end used by this operation.</param>
        /// <param name = "error">The error used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ulong ParseUnsignedDigits<TOptions>(int start, int end, string error)
            where TOptions : struct, ILuminPackOptions
        {
            int step = default(TOptions).JsonByteCount(1);
            if ((end - start) / step > 20)
                ThrowMalformedJson(error);
            if (end - start == step)
                return NumberDigitAt<TOptions>(start);
            ulong acc = 0;
            for (int i = start; i < end; i += step)
            {
                ulong d = NumberDigitAt<TOptions>(i);
                if (acc > 1_844_674_407_370_955_161UL || (acc == 1_844_674_407_370_955_161UL && d > 5UL))
                    ThrowMalformedJson(error);
                acc = acc * 10 + d;
            }

            return acc;
        }
    }
}
