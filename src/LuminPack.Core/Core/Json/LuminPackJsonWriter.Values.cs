using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core
{
    public ref partial struct LuminPackJsonWriter
    {
        /// <summary>Writes byte raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw(byte value)
        {
            WriteByteRaw<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<byte> value)
        {
            if (Output is not null)
            {
                WriteRawToOutput(value);
                return;
            }

            value.CopyTo(ReserveCheckedDestination(value.Length));
            Advance(value.Length);
        }

        /// <summary>Writes raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<char> value)
        {
            if (Output is not null)
            {
                WriteRawToOutput(MemoryMarshal.AsBytes(value));
                return;
            }

            var byteCount = unchecked(value.Length << 1);
            var destination = ReserveCheckedDestination(byteCount);
            MemoryMarshal.Cast<char, byte>(value).CopyTo(destination);
            Advance(byteCount);
        }

        // Keep segmented-output loops out of each inlined generated JSON prefix.
        /// <summary>Writes raw to output to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteRawToOutput(scoped ReadOnlySpan<byte> value)
        {
            while (!value.IsEmpty)
            {
                int position = Position;
                if (position == Buffer.Length)
                {
                    EnsureCapacity(Math.Min(4096, value.Length));
                    position = Position;
                }
                Span<byte> destination = Buffer.Slice(position);
                if (value.TryCopyTo(destination))
                {
                    Advance(value.Length);
                    return;
                }
                int count = destination.Length;
                value.Slice(0, count).CopyTo(destination);
                Advance(count);
                value = value.Slice(count);
            }
        }

        /// <summary>Generator-only copy of an 8..16-byte literal packed into two overlapping native words.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedLiteral(ulong first, ulong last, int byteCount)
        {
            if ((uint)(byteCount - 8) > 8)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(byteCount);
            if ((uint)Position > (uint)Buffer.Length)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(Position);
            EnsureCapacity(byteCount);
            ref byte destination = ref Current;
            LuminPackWriter.Write(ref destination, first);
            LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(byteCount - 8)), last);
            Advance(byteCount);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, int value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, uint value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, long value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, ulong value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes comma if needed to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded()
        {
            WriteCommaIfNeeded<LuminPackDefaultOptions>();
        }

        /// <summary>Writes object start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart()
        {
            WriteObjectStart<LuminPackDefaultOptions>();
        }

        /// <summary>Writes object end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd()
        {
            WriteObjectEnd<LuminPackDefaultOptions>();
        }

        /// <summary>Writes array start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart()
        {
            WriteArrayStart<LuminPackDefaultOptions>();
        }

        /// <summary>Writes array end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd()
        {
            WriteArrayEnd<LuminPackDefaultOptions>();
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf8PropertyName">The utf8 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
        {
            WritePropertyName<LuminPackDefaultOptions>(utf8PropertyName);
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf16PropertyName">The utf16 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<char> utf16PropertyName)
        {
            WritePropertyName<LuminPackDefaultOptions>(utf16PropertyName);
        }

        /// <summary>
        /// Emits an already formatted, escape-free ASCII payload as a JSON string.
        /// Round-trip dates/times, GUIDs, constant TimeSpan and invariant integer
        /// formats never require JSON escaping. Their bounded stack payloads can
        /// be transcoded or copied without another escape scan.
        /// </summary>
        /// <param name = "text">The text used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedValue(scoped ReadOnlySpan<char> text)
        {
            WriteFormattedValue<LuminPackDefaultOptions>(text);
        }

        /// <summary>Writes a <see cref = "DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(DateTime value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(DateTimeOffset value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "Guid"/> in its "D" form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(Guid value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(TimeSpan value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes a date in its round-trip JSON string form.</summary>
        /// <param name="value">The date to write.</param>
        public void Write(DateOnly value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes a time in its round-trip JSON string form.</summary>
        /// <param name="value">The time to write.</param>
        public void Write(TimeOnly value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes a signed 128-bit integer as an invariant decimal JSON string.</summary>
        /// <param name="value">The integer to write.</param>
        public void Write(Int128 value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes an unsigned 128-bit integer as an invariant decimal JSON string.</summary>
        /// <param name="value">The integer to write.</param>
        public void Write(UInt128 value) => Write<LuminPackDefaultOptions>(value);
#endif

        // Conversion: xjb (https://github.com/xjb714/xjb) shortest-decimal core, ported in
        // Core/Json/Xjb. The previous "try 1..9/17 fraction digits and verify the round trip"
        // loop plus its Utf8Formatter fallback is gone:
        //   1. XjbFloat32/XjbFloat64 turn the IEEE-754 bit pattern into {digits, exponent10} directly;
        //   2. LuminPackJsonFloatFormatter lays the digits out as JSON (policy documented there and
        //      kept byte compatible with the previous writer);
        //   3. NaN/Infinity still throw the existing exception and -0 keeps its sign bit.
        /// <summary>Writes float to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(float value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes double to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(double value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(int value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(uint value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(byte value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(sbyte value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(short value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(ushort value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(long value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(ulong value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(decimal value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes char to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(char value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes bool to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(bool value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes null to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        {
            WriteNull<LuminPackDefaultOptions>();
        }

        /// <summary>Writes null internal to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal()
        {
            WriteNullInternal<LuminPackDefaultOptions>();
        }

        /// <summary>Writes byte raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw<TOptions>(byte value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonAscii(ref this, value);
        }

        /// <summary>Writes an ASCII byte at the current UTF-8 position.</summary>
        /// <param name="value">The ASCII code unit.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteJsonAsciiByte(byte value)
        {
            EnsureCapacity(1);
            Current = value;
            Advance(1);
        }

        /// <summary>Writes an ASCII character at the current UTF-16 position.</summary>
        /// <param name="value">The ASCII code unit.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteJsonAsciiChar(byte value)
        {
            EnsureCapacity(2);
            LuminPackWriter.Write(ref Current, (char)value);
            Advance(2);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, int value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 11);
            int written = LuminPackJsonNumberFormatter.WriteInt32(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, uint value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 10);
            int written = LuminPackJsonNumberFormatter.WriteUInt32(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, long value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 20);
            int written = LuminPackJsonNumberFormatter.WriteLong(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, ulong value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 20);
            int written = LuminPackJsonNumberFormatter.WriteULong(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes comma if needed to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (!IsFirstElement)
            {
                WriteByteRaw<TOptions>(Comma);
            }

            IsFirstElement = false;
        }

        /// <summary>Writes object start to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(OpenBrace);
            Depth++;
            IsFirstElement = true;
        }

        /// <summary>Writes object end to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteByteRaw<TOptions>(CloseBrace);
            Depth--;
            IsFirstElement = false;
        }

        /// <summary>Writes array start to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (IsFirstElement || !LuminPackMarshal.ConstantStringProfile<TOptions>() || !TryWriteSeparatedArrayStart<TOptions>())
            {
                WriteCommaIfNeeded<TOptions>();
                WriteByteRaw<TOptions>(OpenBracket);
            }
            Depth++;
            IsFirstElement = true;
        }

        /// <summary>Writes the separator and array opener together when both fit without growing or committing output.</summary>
        /// <typeparam name="TOptions">The already certified fixed document profile.</typeparam>
        /// <returns>Whether both punctuation characters were written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteSeparatedArrayStart<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).JsonByteCount(1) == 1;
            int length = utf8 ? 2 : 4;
            if (!HasEnumerationCapacity(length))
                return false;

            ref byte output = ref Current;
            if (utf8)
            {
                output = Comma;
                Unsafe.Add(ref output, 1) = OpenBracket;
            }
            else
            {
                LuminPackWriter.Write(ref output, (char)Comma);
                LuminPackWriter.Write(ref Unsafe.Add(ref output, 2), (char)OpenBracket);
            }
            Advance(length);
            return true;
        }

        /// <summary>Writes array end to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteByteRaw<TOptions>(CloseBracket);
            Depth--;
            IsFirstElement = false;
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf8PropertyName">The utf8 property name used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName<TOptions>(ReadOnlySpan<byte> utf8PropertyName)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).JsonByteCount(1) == 1))
                WriteStringContentUtf8Raw<TOptions>(utf8PropertyName);
            else
                WriteUtf8NameAsUtf16<TOptions>(utf8PropertyName);
            WriteByteRaw<TOptions>(Quote);
            WriteByteRaw<TOptions>(Colon);
            IsFirstElement = true;
        }

        /// <summary>Transcodes a UTF-8 property name for a UTF-16 document in stack or pooled storage.</summary>
        /// <param name = "utf8PropertyName">The UTF-8 name.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile.</typeparam>
        /// <remarks>A UTF-8 sequence never decodes to more UTF-16 code units than it has bytes, so no intermediate string is needed.</remarks>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteUtf8NameAsUtf16<TOptions>(ReadOnlySpan<byte> utf8PropertyName)
            where TOptions : struct, ILuminPackOptions
        {
            char[]? rented = null;
            Span<char> chars = utf8PropertyName.Length <= 256
                ? stackalloc char[256]
                : (rented = System.Buffers.ArrayPool<char>.Shared.Rent(utf8PropertyName.Length));
            try
            {
                int length = Utf8PlatformDecoder.Decode(utf8PropertyName, chars);
                WriteStringContentUtf16<TOptions>(chars.Slice(0, length));
            }
            finally
            {
                if (rented is not null)
                    System.Buffers.ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf16PropertyName">The utf16 property name used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName<TOptions>(ReadOnlySpan<char> utf16PropertyName)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).JsonByteCount(1) == 1))
            {
                WriteStringContentUtf8<TOptions>(utf16PropertyName);
            }
            else
            {
                WriteStringContentUtf16<TOptions>(utf16PropertyName);
            }

            WriteByteRaw<TOptions>(Quote);
            WriteByteRaw<TOptions>(Colon);
            IsFirstElement = true;
        }

        /// <summary>
        /// Emits an already formatted, escape-free ASCII payload as a JSON string.
        /// Round-trip dates/times, GUIDs, constant TimeSpan and invariant integer
        /// formats never require JSON escaping. Their bounded stack payloads can
        /// be transcoded or copied without another escape scan.
        /// </summary>
        /// <param name = "text">The text used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedValue<TOptions>(scoped ReadOnlySpan<char> text)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).JsonByteCount(1) == 1))
                WriteFormattedAscii(text);
            else
                WriteRaw(text);
            WriteByteRaw<TOptions>(Quote);
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes the same quoted scalar framing around an already formatted UTF-8 payload.</summary>
        /// <param name="text">The bounded ASCII scalar bytes.</param>
        /// <typeparam name="TOptions">The unchanged UTF-8 document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedUtf8Value<TOptions>(scoped ReadOnlySpan<byte> text) where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            WriteFormattedUtf8Payload(text);
            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Preserves the original whole-payload reservation and publication boundary.</summary>
        /// <param name="text">The completely formatted payload.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteFormattedUtf8Payload(scoped ReadOnlySpan<byte> text)
        {
            text.CopyTo(ReserveCheckedDestination(text.Length, keepTailOnMiss: true));
            Advance(text.Length);
        }
#endif

        /// <summary>Transcodes bounded, escape-free scalar text without importing encoder state into each formatter.</summary>
        /// <param name="text">The known ASCII scalar representation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteFormattedAscii(scoped ReadOnlySpan<char> text)
        {
            EnsureCapacity(text.Length);
            var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(text, Buffer.Slice(Position), out _, out int written);
            if (status != OperationStatus.Done)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            Advance(written);
        }

        /// <summary>Writes a <see cref = "DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(DateTime value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteDateTimeOperation<TOptions>, DateTime>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteDateTimeOperation<TOptions> : ILuminPackJsonWriteOperation<DateTime> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, DateTime value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(DateTime value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[33];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            // "yyyy-MM-ddTHH:mm:ss.fffffff" is 27 characters.
            Span<char> buffer = stackalloc char[33];
            if (value.TryFormat(buffer, out int written, "O"))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a <see cref = "DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(DateTimeOffset value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteDateTimeOffsetOperation<TOptions>, DateTimeOffset>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteDateTimeOffsetOperation<TOptions> : ILuminPackJsonWriteOperation<DateTimeOffset> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, DateTimeOffset value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(DateTimeOffset value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[33];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            // Round-trip text plus the "+hh:mm" offset is 33 characters.
            Span<char> buffer = stackalloc char[33];
            if (value.TryFormat(buffer, out int written, "O"))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a <see cref = "Guid"/> in its "D" form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(Guid value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteGuidOperation<TOptions>, Guid>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteGuidOperation<TOptions> : ILuminPackJsonWriteOperation<Guid> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, Guid value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(Guid value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                WriteGuidUtf8Destination<TOptions>(value);
                return;
            }
#endif

            Span<char> buffer = stackalloc char[36];
            if (value.TryFormat(buffer, out int written))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString());
        }

#if NET8_0_OR_GREATER
        /// <summary>Formats the fixed 36-byte GUID payload into the originally reserved output range.</summary>
        /// <param name="value">The immutable GUID value captured by the caller.</param>
        /// <typeparam name="TOptions">The existing UTF-8 profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteGuidUtf8Destination<TOptions>(Guid value) where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            EnsureCapacity(36);
            if (!value.TryFormat(Buffer.Slice(Position, 36), out int written))
                LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.DestinationTooSmall);
            Advance(written);
            WriteByteRaw<TOptions>(Quote);
        }
#endif

        /// <summary>Writes a <see cref = "TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(TimeSpan value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteTimeSpanOperation<TOptions>, TimeSpan>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteTimeSpanOperation<TOptions> : ILuminPackJsonWriteOperation<TimeSpan> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, TimeSpan value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(TimeSpan value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[32];
                if (value.TryFormat(utf8, out int utf8Written, "c"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString());
                return;
            }
#endif

            // "-10675199.02:48:05.4775808" is 26 characters.
            Span<char> buffer = stackalloc char[32];
            if (value.TryFormat(buffer, out int written, "c"))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString());
        }

        /// <summary>Writes an invariant quoted decimal integer with the library default profile.</summary>
        /// <param name="value">The arbitrary-precision integer.</param>
        public void WriteBigInteger(System.Numerics.BigInteger value) => WriteBigInteger<LuminPackDefaultOptions>(value);

        /// <summary>Writes invariant quoted digits while keeping zero outside the formatting buffer path.</summary>
        /// <param name="value">The arbitrary-precision integer.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBigInteger<TOptions>(System.Numerics.BigInteger value) where TOptions : struct, ILuminPackOptions
        {
            if (value.IsZero)
                Write<TOptions>("0");
            else
            {
                int byteCount = value.GetByteCount();
                if (byteCount <= sizeof(int))
                    WriteBigIntegerSmall<TOptions>((int)value);
                else
                    WriteBigIntegerValue<TOptions>(value, byteCount);
            }
        }

        /// <summary>Formats signed Int32 magnitudes without arbitrary-precision scratch or pool management.</summary>
        /// <param name="value">The integer proven to fit a signed Int32.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBigIntegerSmall<TOptions>(int value) where TOptions : struct, ILuminPackOptions
        {
            Span<char> text = stackalloc char[11];
            if (value.TryFormat(text, out int written, provider: System.Globalization.CultureInfo.InvariantCulture))
                WriteFormattedValue<TOptions>(text.Slice(0, written));
            else
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Keeps arbitrary-precision formatting and pooled storage outside generated callers.</summary>
        /// <param name="value">The nonzero integer.</param>
        /// <param name="byteCount">Its exact signed byte width.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBigIntegerValue<TOptions>(System.Numerics.BigInteger value, int byteCount) where TOptions : struct, ILuminPackOptions
        {
            // log10(256) < 2.5; ten spare characters also cover a padded decimal group and its sign.
            long capacity = (long)byteCount * 5 / 2 + 10;
            if (capacity > int.MaxValue)
            {
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            char[]? rented = null;
            Span<char> text = capacity <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent((int)capacity));
            try
            {
#if NET10_0_OR_GREATER
                if (value.TryFormat(text, out int written, provider: System.Globalization.CultureInfo.InvariantCulture))
                    WriteFormattedValue<TOptions>(text.Slice(0, written));
                else
                    Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
#else
                int start = FormatBigIntegerMagnitude(value, byteCount, text);
                WriteFormattedValue<TOptions>(text.Slice(start));
#endif
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<char>.Shared.Return(rented);
            }
        }

#if !NET10_0_OR_GREATER
        /// <summary>Converts public magnitude bytes into decimal groups without older BCL formatter allocations.</summary>
        /// <param name="value">The nonzero integer outside the signed Int32 range.</param>
        /// <param name="byteCount">Its signed byte width.</param>
        /// <param name="text">The bounded destination, including space for one padded group and the sign.</param>
        /// <returns>The first character of the decimal suffix written to the destination.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int FormatBigIntegerMagnitude(System.Numerics.BigInteger value, int byteCount, Span<char> text)
        {
            int count = (byteCount - 1) / sizeof(uint) + 1;
            uint[]? rented = null;
            int decimalCapacity = (int)((long)count * 10 / 9 + 1);
            int workspaceLength = count + decimalCapacity;
            Span<uint> workspace = workspaceLength <= 128 ? stackalloc uint[128] : (rented = ArrayPool<uint>.Shared.Rent(workspaceLength));
            Span<uint> words = workspace.Slice(0, count);
            Span<uint> groups = workspace.Slice(count, decimalCapacity);
            try
            {
                // Only the final partial word can contain bytes not written by TryWriteBytes.
                words[count - 1] = 0;
                bool success = System.Numerics.BigInteger.Abs(value).TryWriteBytes(MemoryMarshal.AsBytes(words), out _, isUnsigned: true);
                System.Diagnostics.Debug.Assert(success);
                if (!BitConverter.IsLittleEndian)
                    for (int i = 0; i < count; i++)
                        words[i] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(words[i]);

                while (words[count - 1] == 0)
                    count--;
                int end = text.Length;
                if (count <= 2)
                {
                    ulong magnitude = words[0];
                    if (count == 2)
                        magnitude |= (ulong)words[1] << 32;
                    end -= 20;
                    success = magnitude.TryFormat(text.Slice(end, 20), out _, "D20", System.Globalization.CultureInfo.InvariantCulture);
                    System.Diagnostics.Debug.Assert(success);
                }
                else
                {
                    const uint decimalBase = 1_000_000_000;
                    int used = 0;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        uint carry = words[i];
                        for (int j = 0; j < used; j++)
                        {
                            // Horner conversion carries the quotient, avoiding a remainder dependency between groups.
                            ulong expanded = ((ulong)groups[j] << 32) | carry;
                            uint next = (uint)(expanded / decimalBase);
                            groups[j] = (uint)(expanded - (ulong)next * decimalBase);
                            carry = next;
                        }
                        if (carry != 0)
                        {
                            uint high = carry / decimalBase;
                            groups[used++] = carry - high * decimalBase;
                            if (high != 0)
                                groups[used++] = high;
                        }
                    }
                    ReadOnlySpan<byte> pairs = LuminPackJsonFloatFormatter.DigitPairs;
                    for (int i = 0; i < used; i++)
                    {
                        end -= 9;
                        Span<char> digits = text.Slice(end, 9);
                        uint group = groups[i];
                        for (int position = 8; position > 0; position -= 2)
                        {
                            uint high = group / 100;
                            int pair = (int)(group - high * 100) * 2;
                            digits[position] = (char)pairs[pair + 1];
                            digits[position - 1] = (char)pairs[pair];
                            group = high;
                        }
                        digits[0] = (char)('0' + group);
                    }
                }
                while (text[end] == '0')
                    end++;
                if (value.Sign < 0)
                    text[--end] = '-';
                return end;
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<uint>.Shared.Return(rented);
            }
        }
#endif

        /// <summary>Writes a version string with the library default profile.</summary>
        /// <param name="value">The version, or null.</param>
        public void WriteVersion(Version? value) => WriteVersion<LuminPackDefaultOptions>(value);

        /// <summary>Writes the established component string without allocating an intermediate string.</summary>
        /// <param name="value">The version, or null.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVersion<TOptions>(Version? value) where TOptions : struct, ILuminPackOptions
        {
            if (value is null)
                WriteNull<TOptions>();
            else
                WriteVersionValue<TOptions>(value);
        }

        /// <summary>Keeps bounded component formatting outside generated callers and null writes.</summary>
        /// <param name="value">The nonnull version.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteVersionValue<TOptions>(Version value) where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteVersionValueVersionOperation<TOptions>, Version>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteVersionValueVersionOperation<TOptions> : ILuminPackJsonWriteOperation<Version> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, Version value) where TCodeUnit : unmanaged =>
                writer.WriteVersionValueEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteVersionValueEncoded<TOptions, TCodeUnit>(Version value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            // Four nonnegative Int32 components and three separators require at most 43 characters.
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[43];
                if (value.TryFormat(utf8, out int written))
                {
                    WriteCommaIfNeeded<TOptions>();
                    WriteByteRaw<TOptions>(Quote);
                    WriteRaw(utf8.Slice(0, written));
                    WriteByteRaw<TOptions>(Quote);
                    return;
                }
                Write<TOptions>(value.ToString());
                return;
            }
#endif
            Span<char> text = stackalloc char[43];
            if (value.TryFormat(text, out int charsWritten))
                WriteFormattedValue<TOptions>(text.Slice(0, charsWritten));
            else
                Write<TOptions>(value.ToString());
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes a date in its round-trip JSON string form without an intermediate string.</summary>
        /// <param name="value">The date to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(DateOnly value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteDateOnlyOperation<TOptions>, DateOnly>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteDateOnlyOperation<TOptions> : ILuminPackJsonWriteOperation<DateOnly> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, DateOnly value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(DateOnly value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[10];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[10];
            if (value.TryFormat(buffer, out int written, "O"))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a time in its round-trip JSON string form without an intermediate string.</summary>
        /// <param name="value">The time to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(TimeOnly value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteTimeOnlyOperation<TOptions>, TimeOnly>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteTimeOnlyOperation<TOptions> : ILuminPackJsonWriteOperation<TimeOnly> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, TimeOnly value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(TimeOnly value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[16];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[16];
            if (value.TryFormat(buffer, out int written, "O"))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a signed 128-bit integer as an invariant decimal JSON string without an intermediate string.</summary>
        /// <param name="value">The integer to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(Int128 value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteInt128Operation<TOptions>, Int128>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteInt128Operation<TOptions> : ILuminPackJsonWriteOperation<Int128> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, Int128 value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(Int128 value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[40];
                if (value.TryFormat(utf8, out int utf8Written, default, System.Globalization.CultureInfo.InvariantCulture))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[40];
            if (value.TryFormat(buffer, out int written, default, System.Globalization.CultureInfo.InvariantCulture))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Writes an unsigned 128-bit integer as an invariant decimal JSON string without an intermediate string.</summary>
        /// <param name="value">The integer to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(UInt128 value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteUInt128Operation<TOptions>, UInt128>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteUInt128Operation<TOptions> : ILuminPackJsonWriteOperation<UInt128> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, UInt128 value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(UInt128 value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<TCodeUnit>() == 1)
            {
                Span<byte> utf8 = stackalloc byte[39];
                if (value.TryFormat(utf8, out int utf8Written, default, System.Globalization.CultureInfo.InvariantCulture))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[39];
            if (value.TryFormat(buffer, out int written, default, System.Globalization.CultureInfo.InvariantCulture))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
#endif

        /// <summary>Writes float to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(float value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            Span<byte> destination = ReserveFloatingDestination<TOptions>(float.IsFinite(value), 24, out bool utf8);
            if (utf8)
                Advance(LuminPackJsonFloatFormatter.WriteFloatUtf8(destination, value));
            else
                Advance(LuminPackJsonFloatFormatter.WriteFloatUtf16(MemoryMarshal.Cast<byte, char>(destination), value) * 2);
        }

        /// <summary>Writes double to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(double value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            Span<byte> destination = ReserveFloatingDestination<TOptions>(double.IsFinite(value), 32, out bool utf8);
            if (utf8)
                Advance(LuminPackJsonFloatFormatter.WriteDoubleUtf8(destination, value));
            else
                Advance(LuminPackJsonFloatFormatter.WriteDoubleUtf16(MemoryMarshal.Cast<byte, char>(destination), value) * 2);
        }

        /// <summary>Reserves a floating-point output window while following the original failure order.</summary>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        /// <param name="finite">Whether the value can be represented as a JSON number.</param>
        /// <param name="utf8ByteCount">The formatter's UTF-8 capacity requirement.</param>
        /// <param name="utf8">The encoding selected for the number.</param>
        /// <returns>The destination for the complete numeric token.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> ReserveFloatingDestination<TOptions>(bool finite, int utf8ByteCount, out bool utf8)
            where TOptions : struct, ILuminPackOptions
        {
            utf8 = default(TOptions).JsonByteCount(1) == 1;
            int byteCount = utf8 ? utf8ByteCount : 64;
            if (LuminPackMarshal.ConstantStringProfile<TOptions>())
            {
                if (!finite)
                    RejectNonFiniteAfterCapacity(byteCount);
                return ReserveCheckedDestination(byteCount, keepTailOnMiss: true);
            }

            EnsureCapacity(byteCount);
            if (!finite)
                LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
            utf8 = default(TOptions).JsonByteCount(1) == 1;
            return Buffer.Slice(Position);
        }

        /// <summary>Preserves capacity and output-callback failures ahead of a nonfinite value diagnostic.</summary>
        /// <param name="byteCount">The existing floating-point capacity requirement.</param>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RejectNonFiniteAfterCapacity(int byteCount)
        {
            EnsureCapacity(byteCount);
            LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
        }

        /// <summary>Writes int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(int value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Reserves the required output and checks that the public cursor is inside the current window.</summary>
        /// <param name="byteCount">The complete required output width.</param>
        /// <param name="keepTailOnMiss">Whether the original checked path takes the entire remaining window.</param>
        /// <returns>The validated destination at the current write position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> ReserveCheckedDestination(int byteCount, bool keepTailOnMiss = false)
        {
            Span<byte> buffer = Buffer;
            ref byte cursor = ref Current;
            if ((uint)byteCount > (uint)buffer.Length ||
                unchecked((ulong)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(buffer), ref cursor)) > (uint)(buffer.Length - byteCount))
                cursor = ref ReserveCheckedDestinationSlow(byteCount, keepTailOnMiss);
            return MemoryMarshal.CreateSpan(ref cursor, byteCount);
        }

        /// <summary>Uses existing growth and checked slicing when a cursor does not fit the current window.</summary>
        /// <param name="byteCount">The complete required output width.</param>
        /// <param name="keepTailOnMiss">Whether the formatter previously took the entire remaining window.</param>
        /// <returns>The start of the validated destination in the current or refreshed window.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ref byte ReserveCheckedDestinationSlow(int byteCount, bool keepTailOnMiss)
        {
            EnsureCapacity(byteCount);
            Span<byte> destination = keepTailOnMiss
                ? Buffer.Slice(Position).Slice(0, byteCount)
                : Buffer.Slice(Position, byteCount);
            return ref MemoryMarshal.GetReference(destination);
        }

        /// <summary>Writes the complete int JSON value for scalar and fused collection callers.</summary>
        /// <param name = "value">The integer to format.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValue<TOptions>(int value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteIntegerValueInt32Operation<TOptions>, int>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteIntegerValueInt32Operation<TOptions> : ILuminPackJsonWriteOperation<int> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, int value) where TCodeUnit : unmanaged =>
                writer.WriteIntegerValueEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValueEncoded<TOptions, TCodeUnit>(int value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                Advance(LuminPackJsonNumberFormatter.WriteInt32(ReserveCheckedDestination(11, keepTailOnMiss: true), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(22));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes u int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(uint value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteUInt32Operation<TOptions>, uint>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteUInt32Operation<TOptions> : ILuminPackJsonWriteOperation<uint> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, uint value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(uint value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                Advance(LuminPackJsonNumberFormatter.WriteUInt32(ReserveCheckedDestination(10, keepTailOnMiss: true), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(20));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(byte value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteByteOperation<TOptions>, byte>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteByteOperation<TOptions> : ILuminPackJsonWriteOperation<byte> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, byte value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(byte value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                // The packed formatter writes four bytes, even for shorter numbers.
                Advance(LuminPackJsonNumberFormatter.WriteByte(ReserveCheckedDestination(4, keepTailOnMiss: true), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(6));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(sbyte value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteSByteOperation<TOptions>, sbyte>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteSByteOperation<TOptions> : ILuminPackJsonWriteOperation<sbyte> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, sbyte value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(sbyte value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                // Reserve the sign and the complete four-byte packed store.
                Advance(LuminPackJsonNumberFormatter.WriteSByte(ReserveCheckedDestination(5, keepTailOnMiss: true), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(8));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(short value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteInt16Operation<TOptions>, short>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteInt16Operation<TOptions> : ILuminPackJsonWriteOperation<short> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, short value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(short value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                EnsureCapacity(6);
                Advance(LuminPackJsonNumberFormatter.WriteShort(Buffer.Slice(Position), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(12));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes u short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(ushort value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteUInt16Operation<TOptions>, ushort>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteUInt16Operation<TOptions> : ILuminPackJsonWriteOperation<ushort> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, ushort value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(ushort value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                Advance(LuminPackJsonNumberFormatter.WriteUShort(ReserveCheckedDestination(5, keepTailOnMiss: true), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(10));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(long value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Writes the complete long JSON value for scalar and fused collection callers.</summary>
        /// <param name = "value">The integer to format.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValue<TOptions>(long value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteIntegerValueInt64Operation<TOptions>, long>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteIntegerValueInt64Operation<TOptions> : ILuminPackJsonWriteOperation<long> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, long value) where TCodeUnit : unmanaged =>
                writer.WriteIntegerValueEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValueEncoded<TOptions, TCodeUnit>(long value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                Advance(LuminPackJsonNumberFormatter.WriteLong(ReserveCheckedDestination(20), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(40));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes u long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(ulong value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteUInt64Operation<TOptions>, ulong>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteUInt64Operation<TOptions> : ILuminPackJsonWriteOperation<ulong> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, ulong value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(ulong value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                Advance(LuminPackJsonNumberFormatter.WriteULong(ReserveCheckedDestination(20), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(40));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(decimal value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteDecimalOperation<TOptions>, decimal>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteDecimalOperation<TOptions> : ILuminPackJsonWriteOperation<decimal> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, decimal value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(decimal value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                Advance(LuminPackJsonNumberFormatter.WriteDecimal(ReserveCheckedDestination(31, keepTailOnMiss: true), value));
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(ReserveCheckedDestination(62));
                value.TryFormat(charSpan, out int written, default, System.Globalization.NumberFormatInfo.InvariantInfo);
                Advance(written << 1);
            }
        }

        /// <summary>Writes char to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(char value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteCharOperation<TOptions>, char>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteCharOperation<TOptions> : ILuminPackJsonWriteOperation<char> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, char value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(char value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if (value == '"' || value == '\\')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)value);
            }
            else if (value == '\b' || value == '\f' || value == '\n' || value == '\r' || value == '\t')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>(value switch
                {
                    '\b' => (byte)'b',
                    '\f' => (byte)'f',
                    '\n' => (byte)'n',
                    '\r' => (byte)'r',
                    _ => (byte)'t'
                });
            }
            else if (value < 0x20 || char.IsSurrogate(value))
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'u');
                WriteByteRaw<TOptions>(HexChar((value >> 12) & 0xF));
                WriteByteRaw<TOptions>(HexChar((value >> 8) & 0xF));
                WriteByteRaw<TOptions>(HexChar((value >> 4) & 0xF));
                WriteByteRaw<TOptions>(HexChar(value & 0xF));
            }
            else if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                if (value <= 0x7F)
                {
                    WriteByteRaw<TOptions>((byte)value);
                }
                else
                {
                    Span<byte> utf8Buf = stackalloc byte[4];
                    Span<char> charBuf = stackalloc char[1]
                    {
                        value
                    };
                    int len = Encoding.UTF8.GetBytes(charBuf, utf8Buf);
                    WriteRaw(utf8Buf[..len]);
                }
            }
            else
            {
                WriteUtf16CharDirect(value);
            }

            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Writes bool to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(bool value)
            where TOptions : struct, ILuminPackOptions
        {
            default(TOptions).WriteJsonValue<WriteBooleanOperation<TOptions>, bool>(ref this, value);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct WriteBooleanOperation<TOptions> : ILuminPackJsonWriteOperation<bool> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, bool value) where TCodeUnit : unmanaged =>
                writer.WriteEncoded<TOptions, TCodeUnit>(value);
        }

        /// <summary>Writes the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <param name="value">The value to encode.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteEncoded<TOptions, TCodeUnit>(bool value) where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            WriteCommaIfNeeded<TOptions>();
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                if (value)
                {
                    EnsureCapacity(4);
                    ref byte dst = ref Current;
                    LuminPackWriter.Write(ref dst, 0x65757274u); // "true"
                    Advance(4);
                }
                else
                {
                    EnsureCapacity(5);
                    ref byte dst = ref Current;
                    LuminPackWriter.Write(ref dst, 0x736C6166u); // "fals"
                    Unsafe.Add(ref dst, 4) = (byte)'e';
                    Advance(5);
                }

                return;
            }

            if (BitConverter.IsLittleEndian && TryWriteUtf16Token(
                    value ? 0x0065007500720074UL : 0x0073006C00610066UL, (ushort)'e', value ? 8 : 10))
                return;

            if (value)
            {
                WriteByteRaw<TOptions>((byte)'t');
                WriteByteRaw<TOptions>((byte)'r');
                WriteByteRaw<TOptions>((byte)'u');
                WriteByteRaw<TOptions>((byte)'e');
            }
            else
            {
                WriteByteRaw<TOptions>((byte)'f');
                WriteByteRaw<TOptions>((byte)'a');
                WriteByteRaw<TOptions>((byte)'l');
                WriteByteRaw<TOptions>((byte)'s');
                WriteByteRaw<TOptions>((byte)'e');
            }
        }

        /// <summary>Writes null to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteNull<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteNullInternal<TOptions>();
        }

        /// <summary>Writes null internal to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).JsonByteCount(1) == 1))
            {
                EnsureCapacity(4);
                ref byte dst = ref Current;
                LuminPackWriter.Write(ref dst, 0x6C6C756Eu); // "null"
                Advance(4);
                return;
            }

            if (BitConverter.IsLittleEndian && TryWriteUtf16Token(0x006C006C0075006EUL, 0, 8))
                return;

            WriteByteRaw<TOptions>((byte)'n');
            WriteByteRaw<TOptions>((byte)'u');
            WriteByteRaw<TOptions>((byte)'l');
            WriteByteRaw<TOptions>((byte)'l');
        }


        /// <summary>Writes a complete fixed UTF-16 token only when its existing buffer window is sufficient.</summary>
        /// <param name = "first">The first four UTF-16 characters in native little-endian storage.</param>
        /// <param name = "last">The optional fifth UTF-16 character.</param>
        /// <param name = "byteCount">The token width, eight or ten bytes at the private call sites.</param>
        /// <returns>Whether the complete token was written without acquiring or growing storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16Token(ulong first, ushort last, int byteCount)
        {
#if NET8_0_OR_GREATER
            if ((long)Unsafe.ByteOffset(ref Cursor, ref _end) < byteCount)
#else
            if ((long)Unsafe.ByteOffset(ref Current, ref MemoryMarshal.GetReference(_end)) < byteCount)
#endif
                return false;
            ref byte destination = ref Current;
            LuminPackWriter.Write(ref destination, first);
            if (byteCount == 10)
                LuminPackWriter.Write(ref Unsafe.Add(ref destination, 8), last);
            Advance(byteCount);
            return true;
        }
    }
}
