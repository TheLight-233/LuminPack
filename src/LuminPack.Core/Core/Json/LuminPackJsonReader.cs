using System.Buffers;
using System.Buffers.Text;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core
{
    /// <summary>Provides number char lookup functionality.</summary>
    internal static class NumberCharLookup
    {
#if NET8_0_OR_GREATER
        public static readonly SearchValues<byte> StringSpecialBytes = SearchValues.Create(Enumerable.Range(0, 0x20).Select(static i => (byte)i).Concat(new byte[] { (byte)'"', (byte)'\\' }).ToArray());
        /// <summary>The characters requiring JSON string scanning.</summary>
        public static readonly SearchValues<char> StringSpecialChars = SearchValues.Create(Enumerable.Range(0, 0x20).Select(static i => (char)i).Concat(new char[] { '"', '\\' }).ToArray());
#else
        /// <summary>Checks eight bytes for a quote, a backslash, or a JSON control byte.</summary>
        /// <param name="value">Eight bytes read as a native word.</param>
        /// <returns>Whether any byte needs scalar handling.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasJsonSpecial(ulong value)
        {
            const ulong ones = 0x0101010101010101UL;
            const ulong high = 0x8080808080808080UL;
            ulong quote = value ^ 0x2222222222222222UL;
            ulong slash = value ^ 0x5C5C5C5C5C5C5C5CUL;
            ulong control = value & 0xE0E0E0E0E0E0E0E0UL;
            return ((quote - ones) & ~quote & high) != 0 ||
                   ((slash - ones) & ~slash & high) != 0 ||
                   ((control - ones) & ~control & high) != 0;
        }
#endif
    }

    /// <summary>Reads JSON tokens and scalar values with configurable structural validation and UTF encoding.</summary>
    [StructLayout(LayoutKind.Sequential)]
#if NET8_0_OR_GREATER
    [SkipLocalsInit]
#endif
#if NET8_0_OR_GREATER
    public ref partial struct LuminPackJsonReader
#else
    public unsafe ref partial struct LuminPackJsonReader
#endif
    {
        private ReadOnlySpan<byte> _buffer;
        /// <summary>The first byte after the input window, cached with every window change.</summary>
#if NET8_0_OR_GREATER
        private ref byte _end;
#else
        private byte* _end;
#endif

        /// <summary>Gets the complete JSON input window.</summary>
        /// <remarks>Only the reader replaces its window, so the cached end always describes it.</remarks>
        public ReadOnlySpan<byte> Buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get => _buffer;
            internal set
            {
                _buffer = value;
#if NET8_0_OR_GREATER
                _end = ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)value.Length);
#else
                _end = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(value)) + value.Length;
#endif
            }
        }

        /// <summary>Gets the signed number of input bytes from the cursor to the end of the window.</summary>
        private readonly nint Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
            get => Unsafe.ByteOffset(ref Cursor, ref _end);
#else
            // Native-width byte offsets avoid Mono's element-size division and retain pointer wrap semantics.
            get => unchecked((nint)_end - (nint)Cursor);
#endif
        }
        /// <summary>The operation's options and reference tracking state.</summary>
        private LuminPackReaderOptionalState? _options;
        /// <summary>Gets or sets the operation reference context.</summary>
        public LuminPackReaderOptionalState? Options
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _options;
            [MethodImpl(MethodImplOptions.AggressiveInlining)] set => _options = value;
        }
        /// <summary>Pooled storage for decoding escaped UTF-8 strings.</summary>
        public byte[]? Utf8StringScratch;
        /// <summary>Pooled storage for decoding escaped UTF-16 strings.</summary>
        public char[]? Utf16StringScratch;
#if NET8_0_OR_GREATER
        /// <summary>The advancing JSON data cursor.</summary>
        public ref byte Cursor;
#else
        /// <summary>The advancing JSON data cursor for compatibility targets.</summary>
        public byte* Cursor;
#endif
        /// <summary>The current JSON container nesting depth.</summary>
        public int Depth;
        // --- RFC 8259 token sequence state -----------------------------------
        // Read() previously skipped ',', ':' unconditionally and never checked
        // which bracket closed which container, so malformed input such as
        // `[1 2]`, `{"a" 1}`, `[1,]` or `{"a":[1}}` was silently accepted.
        // The token returned last decides which separators may follow next.
        /// <summary>Grammar state requiring a value.</summary>
        public const byte JsonStateValue = 0;
        /// <summary>Grammar state allowing the first array value or its closing delimiter.</summary>
        public const byte JsonStateValueOrEnd = 1;
        /// <summary>Grammar state requiring an object property name.</summary>
        public const byte JsonStateName = 2;
        /// <summary>Grammar state allowing the first property name or the object's closing delimiter.</summary>
        public const byte JsonStateNameOrEnd = 3;
        /// <summary>Grammar state requiring the property name and value separator.</summary>
        public const byte JsonStateColon = 4;
        /// <summary>Grammar state requiring an element separator or the matching closing delimiter.</summary>
        public const byte JsonStateAfterValue = 5;
        /// <summary>The expected token sequence when syntax validation is enabled.</summary>
        public byte GrammarState;
        private bool _pooledLookahead;
        /// <summary>Bit (depth - 1) is set while the container at that depth is an object.</summary>
        public ulong ObjectContainerBits;
        /// <summary>Container kinds for depths 65 through 192; no allocation is needed for these depths.</summary>
        private ulong _deepContainerBits0;
        private ulong _deepContainerBits1;
        /// <summary>Container kinds for depths above 192; allocated only for unusually deep documents.</summary>
        public byte[]? DeepContainerKinds;
        /// <summary>The selected token, released after its complete value operation.</summary>
        public JsonTokenType CurrentTokenType;
        /// <summary>Exact small powers of ten used by double parsing.</summary>
        public static readonly double[] s_pow10Double =
        {
            1e0,
            1e1,
            1e2,
            1e3,
            1e4,
            1e5,
            1e6,
            1e7,
            1e8,
            1e9,
            1e10,
            1e11,
            1e12,
            1e13,
            1e14,
            1e15,
            1e16,
            1e17,
            1e18,
            1e19,
            1e20,
            1e21,
            1e22
        };
        /// <summary>Exact small powers of ten used by single-precision parsing.</summary>
        public static readonly float[] s_pow10Float =
        {
            1e0f,
            1e1f,
            1e2f,
            1e3f,
            1e4f,
            1e5f,
            1e6f,
            1e7f,
            1e8f,
            1e9f,
            1e10f
        };

#region Storage types
        /// <summary>Provides json token type functionality.</summary>
        public enum JsonTokenType : byte
        {
            /// <summary>Specifies none.</summary>
            None,
            /// <summary>Specifies object start.</summary>
            ObjectStart,
            /// <summary>Specifies object end.</summary>
            ObjectEnd,
            /// <summary>Specifies array start.</summary>
            ArrayStart,
            /// <summary>Specifies array end.</summary>
            ArrayEnd,
            /// <summary>Specifies property name.</summary>
            PropertyName,
            /// <summary>Specifies string.</summary>
            String,
            /// <summary>Specifies number.</summary>
            Number,
            /// <summary>Specifies true.</summary>
            True,
            /// <summary>Specifies false.</summary>
            False,
            /// <summary>Specifies null.</summary>
            Null
        }

        // -----------------------------------------------------------------
        // Strict RFC 8259 number scanning (P0 correctness)
        //
        // The previous implementation scanned a "candidate character" set and then
        // relied on Utf8Parser/TryParse, while ReadInt/ReadUInt returned 1-2 digit
        // prefixes without checking the token ended. `1.5`, `1e2`, `01`, `1.`, `1e+`
        // and `1x` were therefore accepted or silently truncated. All numeric readers
        // now share this validated scanner.
        // -----------------------------------------------------------------
        /// <summary>Validated layout of a JSON number token. Indices are byte offsets into the reader buffer,
        /// so UTF-8 and UTF-16 (two bytes per character) share the same shape.</summary>
        private readonly struct JsonNumberShape
        {
            internal readonly int End;
            internal readonly int IntStart;
            internal readonly int IntEnd;
            internal readonly int FracStart;
            internal readonly int FracEnd;
            internal readonly int Exponent;
            internal readonly bool Negative;
            internal readonly bool HasFraction;
            internal readonly bool HasExponent;
            /// <summary>Initializes a new instance of <c>JsonNumberShape</c>.</summary>
            /// <param name = "end">The end used by this operation.</param>
            /// <param name = "intStart">The int start used by this operation.</param>
            /// <param name = "intEnd">The int end used by this operation.</param>
            /// <param name = "fracStart">The frac start used by this operation.</param>
            /// <param name = "fracEnd">The frac end used by this operation.</param>
            /// <param name = "exponent">The exponent used by this operation.</param>
            /// <param name = "negative">The negative used by this operation.</param>
            /// <param name = "hasFraction">The has fraction used by this operation.</param>
            /// <param name = "hasExponent">The has exponent used by this operation.</param>
            internal JsonNumberShape(int end, int intStart, int intEnd, int fracStart, int fracEnd, int exponent, bool negative, bool hasFraction, bool hasExponent)
            {
                End = end;
                IntStart = intStart;
                IntEnd = intEnd;
                FracStart = fracStart;
                FracEnd = fracEnd;
                Exponent = exponent;
                Negative = negative;
                HasFraction = hasFraction;
                HasExponent = hasExponent;
            }

            /// <summary>Gets the is integer.</summary>
            internal bool IsInteger => !HasFraction && !HasExponent;
        }

#endregion

#region Constructors
        /// <summary>Initializes a new instance of <c>LuminPackJsonReader</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "state">The reusable state associated with the operation.</param>
        /// <remarks>On netstandard2.1, caller-owned managed input must remain pinned throughout parsing, allocations and callbacks.</remarks>
        public LuminPackJsonReader(ref Span<byte> bufferReference, LuminPackReaderOptionalState state)
        {
            _options = state;
            Buffer = bufferReference;
            Position = 0;
            Depth = 0;
            GrammarState = JsonStateValue;
            ObjectContainerBits = 0;
            _deepContainerBits0 = 0;
            _deepContainerBits1 = 0;
            DeepContainerKinds = null;
            _pooledLookahead = false;
            CurrentTokenType = JsonTokenType.None;
            Utf8StringScratch = null;
            Utf16StringScratch = null;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonReader</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "state">The reusable state associated with the operation.</param>
        /// <remarks>On netstandard2.1, caller-owned managed input must remain pinned throughout parsing, allocations and callbacks.</remarks>
        public LuminPackJsonReader(ref ReadOnlySpan<byte> bufferReference, LuminPackReaderOptionalState state)
        {
            _options = state;
            Buffer = bufferReference;
            Position = 0;
            Depth = 0;
            GrammarState = JsonStateValue;
            ObjectContainerBits = 0;
            _deepContainerBits0 = 0;
            _deepContainerBits1 = 0;
            DeepContainerKinds = null;
            _pooledLookahead = false;
            CurrentTokenType = JsonTokenType.None;
            Utf8StringScratch = null;
            Utf16StringScratch = null;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonReader</c> using the buffer's input and reference context.</summary>
        /// <param name = "bufferWriter">The buffer containing the input data and reader state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackJsonReader(LuminBufferWriter bufferWriter)
        {
            _options = bufferWriter.ReaderState;
            Buffer = bufferWriter.GetSpan();
            Position = 0;
            Depth = 0;
            GrammarState = JsonStateValue;
            ObjectContainerBits = 0;
            _deepContainerBits0 = 0;
            _deepContainerBits1 = 0;
            DeepContainerKinds = null;
            _pooledLookahead = false;
            CurrentTokenType = JsonTokenType.None;
            Utf8StringScratch = null;
            Utf16StringScratch = null;
        }

#endregion

#region Cursor and helpers
        /// <summary>Dereferences the current cursor inside shared token implementation kernels.</summary>
        private ref byte Current {[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
#if NET8_0_OR_GREATER
            ref Cursor;
#else
            ref Unsafe.AsRef<byte>(Cursor);
#endif
        }

        /// <summary>Gets or restores the absolute cursor position inside the current buffer.</summary>
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
            get => (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(Buffer), ref Cursor);
#else
            get => (int)unchecked((nint)Cursor - (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)));
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)value);
#else
                Cursor = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)) + value;
#endif
            }
        }

        /// <summary>Gets the current index.</summary>
        public int CurrentIndex => Position;
        /// <summary>Gets the option state.</summary>
        public LuminPackReaderOptionalState OptionState => Options ?? (Options = new LuminPackReaderOptionalState());

        /// <summary>Releases the selected token after a complete generated value operation.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompleteValue() => CurrentTokenType = JsonTokenType.None;
        /// <summary>Consumes one already validated token byte using the advancing cursor.</summary>
        /// <returns>The current token byte.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte TakeByte()
        {
            byte value = Current;
            Advance(1);
            return value;
        }

        /// <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        /// <param name = "count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            int position = Position;
            if ((uint)position > (uint)Buffer.Length || (uint)count > (uint)(Buffer.Length - position))
                LuminPackExceptionHelper.ThrowJsonAdvanceOutOfRange(position, count);
#if NET8_0_OR_GREATER
            Cursor = ref Unsafe.Add(ref Cursor, (nint)(uint)count);
#else
            Cursor += count;
#endif
        }

        /// <summary>Updates the span used by the current operation.</summary>
        /// <param name = "span">The buffer used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            int position = Position;
            Buffer = span;
            Position = position;
        }

        /// <summary>Publishes pending buffer contents to the configured destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            Buffer = default;
            Position = 0;
            Depth = 0;
            GrammarState = JsonStateValue;
            ObjectContainerBits = 0;
            _deepContainerBits0 = 0;
            _deepContainerBits1 = 0;
            DeepContainerKinds = null;
            _pooledLookahead = false;
            CurrentTokenType = JsonTokenType.None;
        }

        /// <summary>Obtains the span for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetSpan()
        {
            return Buffer[..Position];
        }

        /// <summary>Performs the skip whitespace step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SkipWhitespace()
        {
            SkipWhitespace<LuminPackDefaultOptions>();
        }

        /// <summary>Determines whether the supplied value has the value state classification.</summary>
        /// <param name = "state">The reusable state associated with the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValueState(byte state) => state <= JsonStateValueOrEnd;
        /// <summary>Determines whether the supplied value has the name state classification.</summary>
        /// <param name = "state">The reusable state associated with the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNameState(byte state) => state == JsonStateName || state == JsonStateNameOrEnd;
        /// <summary>Determines whether the supplied value has the current container object classification.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCurrentContainerObject()
        {
            if (Depth == 0)
                return false;
            if (Depth <= 64)
                return (ObjectContainerBits & (1UL << (Depth - 1))) != 0;
            if (Depth <= 192)
                return GetDeepContainerKind(Depth);
            return DeepContainerKinds![Depth - 1] != 0;
        }

        /// <summary>Reads the inline container kind used by depths 65 through 192.</summary>
        /// <param name = "depth">The one-based nesting depth.</param>
        /// <returns>Whether the container at this depth is an object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetDeepContainerKind(int depth)
        {
            int bit = depth - 65;
            return bit < 64
                ? (_deepContainerBits0 & (1UL << bit)) != 0
                : (_deepContainerBits1 & (1UL << (bit - 64))) != 0;
        }

        /// <summary>Updates the inline container kind used by depths 65 through 192.</summary>
        /// <param name = "depth">The one-based nesting depth.</param>
        /// <param name = "isObject">Whether the container is an object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDeepContainerKindInline(int depth, bool isObject)
        {
            int bit = depth - 65;
            if (bit < 64)
            {
                ulong mask = 1UL << bit;
                _deepContainerBits0 = isObject ? _deepContainerBits0 | mask : _deepContainerBits0 & ~mask;
            }
            else
            {
                ulong mask = 1UL << (bit - 64);
                _deepContainerBits1 = isObject ? _deepContainerBits1 | mask : _deepContainerBits1 & ~mask;
            }
        }

        /// <summary>Performs the push container step for the current operation.</summary>
        /// <param name = "isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushContainer(bool isObject)
        {
            PushContainer<LuminPackDefaultOptions>(isObject);
        }

        /// <summary>Updates the deep container kind used by the current operation.</summary>
        /// <param name = "depth">The nesting depth used to distinguish generated local variables.</param>
        /// <param name = "isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SetDeepContainerKind(int depth, bool isObject)
        {
            if (depth <= 192)
            {
                SetDeepContainerKindInline(depth, isObject);
                return;
            }

            byte[] kinds = DeepContainerKinds!;
            if (kinds is null || depth > kinds.Length)
            {
                int size = Math.Max(depth, 256);
                if (_pooledLookahead)
                {
                    var next = ArrayPool<byte>.Shared.Rent(size);
                    if (kinds is not null)
                    {
                        kinds.AsSpan().CopyTo(next);
                        ArrayPool<byte>.Shared.Return(kinds);
                    }
                    kinds = next;
                }
                else
                    Array.Resize(ref kinds, size);
                DeepContainerKinds = kinds;
            }

            kinds[depth - 1] = isObject ? (byte)1 : (byte)0;
        }

        /// <summary>Performs the pop container step for the current operation.</summary>
        /// <param name = "isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopContainer(bool isObject)
        {
            PopContainer<LuminPackDefaultOptions>(isObject);
        }

        /// <summary>Re-reads the next character after a separator; returns false at end of input.</summary>
        /// <param name = "c">Receives the c produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadCurrentChar(out char c)
        {
            return TryReadCurrentChar<LuminPackDefaultOptions>(out c);
        }

        /// <summary>Ensures a number or literal token ends at EOF or at a JSON structural delimiter.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequireTokenEndBoundary(int index)
        {
            RequireTokenEndBoundary<LuminPackDefaultOptions>(index);
        }

        /// <summary>Determines whether the supplied value has the value delimiter at classification.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValueDelimiterAt(int index)
        {
            return IsValueDelimiterAt<LuminPackDefaultOptions>(index);
        }

        /// <summary>
        /// Fast exit for plain integer tokens (what game data is made of): the pre-change reader returned
        /// 1-2 digit values from two byte comparisons and left the rest to Utf8Parser, and benchmarking showed
        /// integer parsing 3.5x slower without a fast path. The grammar and the token boundary are still
        /// enforced — a leading zero must not be followed by a digit, the token must end at a value delimiter,
        /// and the magnitude must fit in 19 digits / <see cref = "long "/> — while every other shape (fractions,
        /// exponents, out-of-range literals) falls through to <see cref = "ScanJsonNumber"/>.
        /// </summary>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadPlainInteger(out long value)
        {
            return TryReadPlainInteger<LuminPackDefaultOptions>(out value);
        }

        /// <summary>Digit value at <paramref name = "index"/> or 10 when the buffer ends there.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitOrNonDigit(int index)
        {
            return NumberDigitOrNonDigit<LuminPackDefaultOptions>(index);
        }

        /// <summary>
        /// Matches an unescaped generated property name and advances to its value token.
        /// A mismatch leaves the reader unchanged so ordinary property lookup can handle
        /// whitespace, reordered, missing, unknown, duplicate or escaped names.
        /// </summary>
        /// <param name = "utf8Name">The utf8 name used by this operation.</param>
        /// <param name = "utf16Name">The utf16 name used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadPropertyName(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<char> utf16Name)
        {
            return TryReadPropertyName<LuminPackDefaultOptions>(utf8Name, utf16Name);
        }

        /// <summary>Determines whether the supplied value has the null classification.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNull()
        {
            return CurrentTokenType == JsonTokenType.Null;
        }

        /// <summary>Skips the current JSON value, including any nested containers.</summary>
        public void Skip()
        {
            Skip<LuminPackDefaultOptions>();
        }

        /// <summary>Verifies that no non-whitespace JSON remains after the current top-level value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureEndOfDocument()
        {
            EnsureEndOfDocument<LuminPackDefaultOptions>();
        }

        /// <summary>Performs the skip scalar token step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipScalarToken()
        {
            SkipScalarToken<LuminPackDefaultOptions>();
        }

        /// <summary>Performs the parse hex4 step for the current operation.</summary>
        /// <param name = "source">The input data to process.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHex4(ReadOnlySpan<byte> source, int index)
        {
            if (index < 0 || source.Length - index < 4)
                LuminPackExceptionHelper.ThrowFormatException("Incomplete Unicode escape sequence");
            ref byte table = ref MemoryMarshal.GetReference(HexDigits.Values);
            int a = Unsafe.Add(ref table, (nint)(uint)source[index]);
            int b = Unsafe.Add(ref table, (nint)(uint)source[index + 1]);
            int c = Unsafe.Add(ref table, (nint)(uint)source[index + 2]);
            int d = Unsafe.Add(ref table, (nint)(uint)source[index + 3]);
            if (((a | b | c | d) & 0xF0) != 0)
                LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
            return (a << 12) | (b << 8) | (c << 4) | d;
        }

        /// <summary>Parses four validated UTF-16 hexadecimal code units with the same ASCII table.</summary>
        /// <param name="source">The complete source window.</param>
        /// <param name="index">The first hexadecimal character.</param>
        /// <returns>The decoded UTF-16 code unit.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHex4(ReadOnlySpan<char> source, int index)
        {
            if (index < 0 || source.Length - index < 4)
                LuminPackExceptionHelper.ThrowFormatException("Incomplete Unicode escape sequence");
            uint a = source[index], b = source[index + 1], c = source[index + 2], d = source[index + 3];
            if ((a | b | c | d) > 255)
                LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
            ref byte table = ref MemoryMarshal.GetReference(HexDigits.Values);
            int first = Unsafe.Add(ref table, (nint)a), second = Unsafe.Add(ref table, (nint)b);
            int third = Unsafe.Add(ref table, (nint)c), fourth = Unsafe.Add(ref table, (nint)d);
            if (((first | second | third | fourth) & 0xF0) != 0)
                LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
            return (first << 12) | (second << 8) | (third << 4) | fourth;
        }

        /// <summary>Keeps the immutable hexadecimal table separate from ordinary token initialization.</summary>
        private static class HexDigits
        {
            internal static ReadOnlySpan<byte> Values => new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 255, 255, 255, 255, 255, 255, 255, 10, 11, 12, 13, 14, 15, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 10, 11, 12, 13, 14, 15, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
        }

        /// <summary>Performs the hex value step for the current operation.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexValue(int value)
        {
            if ((uint)(value - '0') <= 9u)
                return value - '0';
            value |= 0x20;
            if ((uint)(value - 'a') <= 5u)
                return value - 'a' + 10;
            return -1;
        }

        /// <summary>
        /// For nested objects, reuses an existing ObjectStart token instead of advancing again.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeObjectStart()
        {
            return TryConsumeObjectStart<LuminPackDefaultOptions>();
        }

        /// <summary>Determines whether the supplied value has the number at end classification.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNumberAtEnd(int index)
        {
            return IsNumberAtEnd<LuminPackDefaultOptions>(index);
        }

        /// <summary>Performs the number char at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char NumberCharAt(int index)
        {
            return NumberCharAt<LuminPackDefaultOptions>(index);
        }

        /// <summary>Performs the number digit at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitAt(int index)
        {
            return NumberDigitAt<LuminPackDefaultOptions>(index);
        }

        /// <summary>Validates a complete number token: [minus] int [frac] [exp] followed by EOF or a value delimiter.</summary>
        /// <param name = "start">The start used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private JsonNumberShape ScanJsonNumber(int start)
        {
            return ScanJsonNumber<LuminPackDefaultOptions>(start);
        }

        /// <summary>Converts token digits to mantissa * 10^exponent10 without dropping significant digits.</summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "mantissa">Receives the mantissa produced by the operation.</param>
        /// <param name = "exponent10">Receives the exponent10 produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool TryAccumulateDecimalDigits(in JsonNumberShape shape, out ulong mantissa, out int exponent10)
        {
            return TryAccumulateDecimalDigits<LuminPackDefaultOptions>(in shape, out mantissa, out exponent10);
        }

        // Scan and accumulate together. A valid but inexact/long token can go straight
        // to the platform parser, without another grammar scan or digit accumulation.
        // A failed scan leaves the cursor unchanged for the general grammar/error path.
        /// <summary>Attempts to scan floating point and reports whether it succeeded.</summary>
        /// <param name = "mantissa">Receives the mantissa produced by the operation.</param>
        /// <param name = "exponent10">Receives the exponent10 produced by the operation.</param>
        /// <param name = "negative">Receives the negative produced by the operation.</param>
        /// <param name = "end">Receives the end produced by the operation.</param>
        /// <param name = "exactMantissa">Receives the exact mantissa produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool TryScanFloatingPoint(out ulong mantissa, out int exponent10, out bool negative, out int end, out bool exactMantissa)
        {
            return TryScanFloatingPoint<LuminPackDefaultOptions>(out mantissa, out exponent10, out negative, out end, out exactMantissa);
        }

        /// <summary>
        /// Exact fast path: exactly representable operands need a single IEEE-754 operation, so the
        /// result equals correct rounding of the decimal literal. Every other input returns false and
        /// is handled by the general-purpose parser.
        /// </summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseDoubleExact(in JsonNumberShape shape, out double value)
        {
            return TryParseDoubleExact<LuminPackDefaultOptions>(in shape, out value);
        }

        /// <summary>Attempts to parse float exact and reports whether it succeeded.</summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseFloatExact(in JsonNumberShape shape, out float value)
        {
            return TryParseFloatExact<LuminPackDefaultOptions>(in shape, out value);
        }

        /// <summary>Returns the value represented by the current true or false JSON token.</summary>
        /// <returns>The current boolean value.</returns>
        /// <exception cref = "InvalidOperationException">The current token is not a boolean.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetBoolean()
        {
            if (CurrentTokenType == JsonTokenType.True)
                return true;
            else if (CurrentTokenType == JsonTokenType.False)
                return false;
            LuminPackExceptionHelper.ThrowInvalidOperationException("Current token is not a boolean");
            return false;
        }

        /// <summary>Advances past the current number token using the same strict grammar as the numeric readers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipNumberToken()
        {
            SkipNumberToken<LuminPackDefaultOptions>();
        }

        /// <summary>Reinterprets the validated UTF-16 number token as characters (callers only use this in UTF-16 mode).</summary>
        /// <param name = "start">The start used by this operation.</param>
        /// <param name = "end">The end used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> NumberChars(int start, int end)
        {
            var byteSpan = Buffer.Slice(start, end - start);
            ref char charRef = ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(byteSpan));
            return MemoryMarshal.CreateReadOnlySpan(ref charRef, (end - start) >> 1);
        }

        /// <summary>Performs the skip whitespace step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal void SkipWhitespace<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            nint bytes = Remaining;
            int count = 0;
            ref byte first = ref Current;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                while (count < bytes)
                {
                    ref byte current = ref Unsafe.Add(ref first, count);
                    byte value = current;
                    if (!IsJsonWhitespace(value))
                        break;
                    if (count >= 32)
                    {
                        count += CountWhitespaceTail(Buffer.Slice(Position + count));
                        break;
                    }
                    count += value == ' ' && bytes - count >= 4 && Unsafe.ReadUnaligned<uint>(ref current) == 0x20202020u ? 4 : 1;
                }
            }
            else
            {
                while (bytes - count >= 2)
                {
                    ref byte current = ref Unsafe.Add(ref first, count);
                    char value = Unsafe.ReadUnaligned<char>(ref current);
                    if (!IsJsonWhitespace(value))
                        break;
                    if (count >= 64)
                    {
                        count += CountWhitespaceTail(MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position + count))) * 2;
                        break;
                    }
                    if (value == ' ' && Unsafe.SizeOf<IntPtr>() == 8 && bytes - count >= 8 &&
                        Unsafe.ReadUnaligned<ulong>(ref current) == 0x0020002000200020UL)
                        count += 8;
                    else
                        count += 2;
                }
            }
            if (count != 0)
                Advance(count);
#else
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                while (Remaining > 0)
                {
                    byte b = Current;
                    if (b == ' ' || b == '\t' || b == '\r' || b == '\n')
                    {
                        if (b == ' ' && Remaining >= 4 && Unsafe.ReadUnaligned<uint>(ref Current) == 0x20202020u)
                            Advance(4);
                        else
                            Advance(1);
                    }
                    else
                        break;
                }
            }
            else
            {
                while (Remaining > 1)
                {
                    char c = LuminPackReader.Read<char>(ref Current);
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        if (c == ' ' && Remaining >= 8 && Unsafe.SizeOf<IntPtr>() == 8 && Unsafe.ReadUnaligned<ulong>(ref Current) == 0x0020002000200020UL)
                            Advance(8);
                        else
                            Advance(2);
                    }
                    else
                        break;
                }
            }
#endif
        }

#if NET8_0_OR_GREATER
        /// <summary>Tests the four JSON whitespace code points.</summary>
        /// <param name="value">A byte or UTF-16 code unit.</param>
        /// <returns>Whether the code point is JSON whitespace.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsJsonWhitespace(int value) => value is ' ' or '\t' or '\r' or '\n';

        /// <summary>Counts the bounded tail of an established whitespace run without a search table.</summary>
        /// <param name="tail">The remaining complete code units.</param>
        /// <returns>The number of whitespace code units before the next token.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CountWhitespaceTail(ReadOnlySpan<byte> tail)
        {
            int spaces = tail.IndexOfAnyExcept((byte)' ');
            if (spaces < 0)
                return tail.Length;
            if (!IsJsonWhitespace(tail[spaces]))
                return spaces;
            int other = tail.Slice(spaces).IndexOfAnyExcept(" \t\r\n"u8);
            return other < 0 ? tail.Length : spaces + other;
        }

        /// <summary>Counts the bounded tail of an established whitespace run without a search table.</summary>
        /// <param name="tail">The remaining complete code units.</param>
        /// <returns>The number of whitespace code units before the next token.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CountWhitespaceTail(ReadOnlySpan<char> tail)
        {
            int spaces = tail.IndexOfAnyExcept(' ');
            if (spaces < 0)
                return tail.Length;
            if (!IsJsonWhitespace(tail[spaces]))
                return spaces;
            int other = tail.Slice(spaces).IndexOfAnyExcept(" \t\r\n".AsSpan());
            return other < 0 ? tail.Length : spaces + other;
        }
#endif

        /// <summary>Performs the push container step for the current operation.</summary>
        /// <param name = "isObject">The is object used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void PushContainer<TOptions>(bool isObject)
            where TOptions : struct, ILuminPackOptions
        {
            Depth++;
            if (default(TOptions).MaxJsonDepth > 0 && Depth > default(TOptions).MaxJsonDepth)
                ThrowMalformedJson("JSON nesting depth exceeds the configured maximum");
            if (default(TOptions).ValidateJsonSyntax)
            {
                if (Depth <= 64)
                {
                    if (isObject)
                        ObjectContainerBits |= 1UL << (Depth - 1);
                    else
                        ObjectContainerBits &= ~(1UL << (Depth - 1));
                }
                else
                {
                    SetDeepContainerKind(Depth, isObject);
                }
            }

            GrammarState = isObject ? JsonStateNameOrEnd : JsonStateValueOrEnd;
        }

        /// <summary>Performs the pop container step for the current operation.</summary>
        /// <param name = "isObject">The is object used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void PopContainer<TOptions>(bool isObject)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).ValidateJsonSyntax && (Depth == 0 || IsCurrentContainerObject() != isObject))
                ThrowMalformedJson("Mismatched JSON closing bracket");
            if (Depth > 0)
                Depth--;
            GrammarState = JsonStateAfterValue;
        }

        /// <summary>Re-reads the next character after a separator; returns false at end of input.</summary>
        /// <param name = "c">Receives the c produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool TryReadCurrentChar<TOptions>(out char c)
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                if (Remaining <= 0)
                {
                    c = default;
                    return false;
                }

                c = (char)Current;
                return true;
            }

            if (Remaining <= 1)
            {
                c = default;
                return false;
            }

            c = LuminPackReader.Read<char>(ref Current);
            return true;
        }

        /// <summary>Ensures a number or literal token ends at EOF or at a JSON structural delimiter.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void RequireTokenEndBoundary<TOptions>(int index)
            where TOptions : struct, ILuminPackOptions
        {
            if (!default(TOptions).ValidateJsonSyntax)
                return;
            if (!IsValueDelimiterAt<TOptions>(index))
                ThrowMalformedJson("Invalid JSON token: expected a value delimiter");
        }

        /// <summary>Determines whether the supplied value has the value delimiter at classification.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool IsValueDelimiterAt<TOptions>(int index)
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                if ((uint)index >= (uint)Buffer.Length)
                    return true;
                byte b = Buffer[index];
                return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r' || b == (byte)',' || b == (byte)']' || b == (byte)'}';
            }

            if (index + 1 >= Buffer.Length)
                return true;
            char c = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)));
            return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == ',' || c == ']' || c == '}';
        }

        /// <summary>
        /// Fast exit for plain integer tokens (what game data is made of): the pre-change reader returned
        /// 1-2 digit values from two byte comparisons and left the rest to Utf8Parser, and benchmarking showed
        /// integer parsing 3.5x slower without a fast path. The grammar and the token boundary are still
        /// enforced — a leading zero must not be followed by a digit, the token must end at a value delimiter,
        /// and the magnitude must fit in 19 digits / <see cref = "long "/> — while every other shape (fractions,
        /// exponents, out-of-range literals) falls through to <see cref = "ScanJsonNumber"/>.
        /// </summary>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool TryReadPlainInteger<TOptions>(out long value)
            where TOptions : struct, ILuminPackOptions
        {
            value = 0;
            int step = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 1 : 2;
            int i = Position;
            bool negative = false;
            if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '-')
            {
                negative = true;
                i += step;
            }

            uint first = NumberDigitOrNonDigit<TOptions>(i);
            if (first > 9u)
                return false;
            if (first == 0u)
            {
                if (NumberDigitOrNonDigit<TOptions>(i + step) <= 9u || !IsValueDelimiterAt<TOptions>(i + step))
                    return false;
                Position = i + step;
                return true;
            }

            ulong magnitude = first;
            int digits = 1;
            int j = i + step;
            while (true)
            {
                uint digit = NumberDigitOrNonDigit<TOptions>(j);
                if (digit > 9u)
                    break;
                if (digits == 19)
                {
                    // 20 digits can never fit; let the strict scanner report the range error.
                    if (!IsNumberAtEnd<TOptions>(j))
                        return false;
                    break;
                }

                magnitude = magnitude * 10 + digit;
                digits++;
                j += step;
            }

            if (!IsValueDelimiterAt<TOptions>(j))
                return false;
            if (negative ? magnitude > 9_223_372_036_854_775_808UL : magnitude > 9_223_372_036_854_775_807UL)
                return false;
            Position = j;
            value = negative ? unchecked(-(long)magnitude) : (long)magnitude;
            return true;
        }

        /// <summary>Digit value at <paramref name = "index"/> or 10 when the buffer ends there.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitOrNonDigit<TOptions>(int index)
            where TOptions : struct, ILuminPackOptions => IsNumberAtEnd<TOptions>(index) ? 10u : NumberDigitAt<TOptions>(index);
        /// <summary>
        /// Matches an unescaped generated property name and advances to its value token.
        /// A mismatch leaves the reader unchanged so ordinary property lookup can handle
        /// whitespace, reordered, missing, unknown, duplicate or escaped names.
        /// </summary>
        /// <param name = "utf8Name">The utf8 name used by this operation.</param>
        /// <param name = "utf16Name">The utf16 name used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool TryReadPropertyName<TOptions>(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<char> utf16Name)
            where TOptions : struct, ILuminPackOptions
        {
            int index = Position;
            if ((uint)index >= (uint)Buffer.Length)
                return false;
            bool comma;
            int end;
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                comma = Buffer[index] == ',';
                if (comma)
                    index++;
                var remaining = Buffer.Slice(index);
                if (remaining.Length < utf8Name.Length + 3 || remaining[0] != '"' || remaining[utf8Name.Length + 1] != '"' || remaining[utf8Name.Length + 2] != ':' || !MatchesShortName(remaining.Slice(1, utf8Name.Length), utf8Name))
                    return false;
                end = index + utf8Name.Length + 3;
            }
            else
            {
                var remaining = MemoryMarshal.Cast<byte, char>(Buffer.Slice(index));
                if (remaining.IsEmpty)
                    return false;
                comma = remaining[0] == ',';
                if (comma)
                    remaining = remaining.Slice(1);
                if (remaining.Length < utf16Name.Length + 3 || remaining[0] != '"' || remaining[utf16Name.Length + 1] != '"' || remaining[utf16Name.Length + 2] != ':' || !MatchesShortName(MemoryMarshal.AsBytes(remaining.Slice(1, utf16Name.Length)), MemoryMarshal.AsBytes(utf16Name)))
                    return false;
                end = index + (utf16Name.Length + 3 + (comma ? 1 : 0)) * 2;
            }

            if (default(TOptions).ValidateJsonSyntax)
            {
                // Preserve exactly the separator/container checks performed by ReadStrict.
                if (Depth == 0 || !IsCurrentContainerObject() || (comma ? GrammarState != JsonStateAfterValue : !IsNameState(GrammarState)))
                    return false;
                GrammarState = JsonStateValue;
            }

            Position = end;
            if (!default(TOptions).ValidateJsonSyntax && TryReadCurrentChar<TOptions>(out char head))
            {
                if (head == '"')
                {
                    CurrentTokenType = JsonTokenType.String;
                    return true;
                }
                if ((uint)(head - '0') <= 9u || head == '-')
                {
                    CurrentTokenType = JsonTokenType.Number;
                    return true;
                }
            }
            if (!Read<TOptions>())
                ThrowMalformedJson("Expected a JSON property value");
            return true;
        }

        /// <summary>Compares a complete sixteen-byte name with two scalar windows; other sizes retain the general matcher.</summary>
        /// <param name="source">The validated current name.</param><param name="expected">The complete generated name.</param>
        /// <returns>Whether every byte matches.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchesShortName(ReadOnlySpan<byte> source, ReadOnlySpan<byte> expected)
        {
            if (Unsafe.SizeOf<IntPtr>() == 8 && expected.Length == 16)
            {
                ref byte a = ref MemoryMarshal.GetReference(source), b = ref MemoryMarshal.GetReference(expected);
                return Unsafe.ReadUnaligned<ulong>(ref a) == Unsafe.ReadUnaligned<ulong>(ref b) &&
                    Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref a, 8)) == Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 8));
            }
            return source.SequenceEqual(expected);
        }

        /// <summary>Skips the current JSON value, including any nested containers.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Skip<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType is JsonTokenType.ObjectStart or JsonTokenType.ArrayStart)
                SkipContainer<TOptions>();
            else
                SkipScalarToken<TOptions>();
        }

        /// <summary>Skips an open container through the same tokenizer and scalar validation as the outer reader.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SkipContainer<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            int initialDepth = Depth;
            while (Read<TOptions>())
            {
                if ((CurrentTokenType is JsonTokenType.ObjectEnd or JsonTokenType.ArrayEnd) && Depth < initialDepth)
                    return;
                SkipScalarToken<TOptions>();
            }
            LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON object or array");
        }

        /// <summary>Verifies that no non-whitespace JSON remains after the current top-level value.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void EnsureEndOfDocument<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            SkipWhitespace<TOptions>();
            if (Remaining != 0)
                LuminPackExceptionHelper.ThrowFormatException("Unexpected data after the top-level JSON value");
        }

        /// <summary>Performs the skip scalar token step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipScalarToken<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            switch (CurrentTokenType)
            {
                case JsonTokenType.String:
                case JsonTokenType.PropertyName:
                    SkipStringToken<TOptions>();
                    break;
                case JsonTokenType.Number:
                    SkipNumberToken<TOptions>();
                    break;
            }
        }

        /// <summary>
        /// For nested objects, reuses an existing ObjectStart token instead of advancing again.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeObjectStart<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType == JsonTokenType.ObjectStart)
            {
                return true;
            }

            if (Read<TOptions>() && CurrentTokenType == JsonTokenType.ObjectStart)
            {
                return true;
            }

            LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON object");
            return false;
        }

        /// <summary>Determines whether the supplied value has the number at end classification.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool IsNumberAtEnd<TOptions>(int index)
            where TOptions : struct, ILuminPackOptions => (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? index >= Buffer.Length : index + 1 >= Buffer.Length;
        /// <summary>Performs the number char at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private char NumberCharAt<TOptions>(int index)
            where TOptions : struct, ILuminPackOptions => (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? (char)Buffer[index] : LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)));
        /// <summary>Performs the number digit at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitAt<TOptions>(int index)
            where TOptions : struct, ILuminPackOptions
        {
            char c = NumberCharAt<TOptions>(index);
            return (uint)(c - '0');
        }

        /// <summary>Validates a complete number token: [minus] int [frac] [exp] followed by EOF or a value delimiter.</summary>
        /// <param name = "start">The start used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private JsonNumberShape ScanJsonNumber<TOptions>(int start)
            where TOptions : struct, ILuminPackOptions
        {
            int step = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 1 : 2;
            int i = start;
            bool negative = false;
            if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '-')
            {
                negative = true;
                i += step;
            }

            int intStart = i;
            if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '0')
            {
                i += step;
            }
            else
            {
                while (!IsNumberAtEnd<TOptions>(i))
                {
                    uint d = NumberDigitAt<TOptions>(i);
                    if (d > 9u)
                        break;
                    i += step;
                }
            }

            if (i == intStart)
                ThrowMalformedJson("Invalid JSON number: expected an integer part");
            int intEnd = i;
            int fracStart = i;
            int fracEnd = i;
            bool hasFraction = false;
            if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '.')
            {
                hasFraction = true;
                i += step;
                fracStart = i;
                while (!IsNumberAtEnd<TOptions>(i))
                {
                    uint d = NumberDigitAt<TOptions>(i);
                    if (d > 9u)
                        break;
                    i += step;
                }

                fracEnd = i;
                if (fracEnd == fracStart)
                    ThrowMalformedJson("Invalid JSON number: expected digits after '.'");
            }

            int exponent = 0;
            bool hasExponent = false;
            if (!IsNumberAtEnd<TOptions>(i))
            {
                char marker = NumberCharAt<TOptions>(i);
                if (marker == 'e' || marker == 'E')
                {
                    hasExponent = true;
                    i += step;
                    bool expNegative = false;
                    if (!IsNumberAtEnd<TOptions>(i))
                    {
                        char sign = NumberCharAt<TOptions>(i);
                        if (sign == '+' || sign == '-')
                        {
                            expNegative = sign == '-';
                            i += step;
                        }
                    }

                    int expStart = i;
                    int expValue = 0;
                    while (!IsNumberAtEnd<TOptions>(i))
                    {
                        uint d = NumberDigitAt<TOptions>(i);
                        if (d > 9u)
                            break;
                        if (expValue < 1_000_000)
                            expValue = expValue * 10 + (int)d;
                        i += step;
                    }

                    if (i == expStart)
                        ThrowMalformedJson("Invalid JSON number: expected digits in the exponent");
                    exponent = expNegative ? -expValue : expValue;
                }
            }

            RequireTokenEndBoundary<TOptions>(i);
            return new JsonNumberShape(i, intStart, intEnd, fracStart, fracEnd, exponent, negative, hasFraction, hasExponent);
        }

        /// <summary>Converts token digits to mantissa * 10^exponent10 without dropping significant digits.</summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "mantissa">Receives the mantissa produced by the operation.</param>
        /// <param name = "exponent10">Receives the exponent10 produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool TryAccumulateDecimalDigits<TOptions>(in JsonNumberShape shape, out ulong mantissa, out int exponent10)
            where TOptions : struct, ILuminPackOptions
        {
            int step = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 1 : 2;
            mantissa = 0;
            exponent10 = 0;
            int digits = 0;
            bool started = false;
            for (int i = shape.IntStart; i < shape.IntEnd; i += step)
            {
                uint d = NumberDigitAt<TOptions>(i);
                if (!started)
                {
                    if (d == 0u)
                        continue;
                    started = true;
                }

                if (digits == 19)
                    return false;
                mantissa = mantissa * 10 + d;
                digits++;
            }

            for (int i = shape.FracStart; i < shape.FracEnd; i += step)
            {
                uint d = NumberDigitAt<TOptions>(i);
                if (!started)
                {
                    if (d == 0u)
                    {
                        if (exponent10 > -400)
                            exponent10--;
                        continue;
                    }

                    started = true;
                }

                if (digits == 19)
                    return false;
                mantissa = mantissa * 10 + d;
                digits++;
                if (exponent10 > -400)
                    exponent10--;
            }

            if (shape.HasExponent)
                exponent10 += shape.Exponent;
            return true;
        }

        /// <summary>Attempts to scan floating point and reports whether it succeeded.</summary>
        /// <param name = "mantissa">Receives the mantissa produced by the operation.</param>
        /// <param name = "exponent10">Receives the exponent10 produced by the operation.</param>
        /// <param name = "negative">Receives the negative produced by the operation.</param>
        /// <param name = "end">Receives the end produced by the operation.</param>
        /// <param name = "exactMantissa">Receives the exact mantissa produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent10, out bool negative, out int end, out bool exactMantissa)
            where TOptions : struct, ILuminPackOptions
        {
            int step = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 1 : 2;
            int index = Position;
            negative = !IsNumberAtEnd<TOptions>(index) && NumberCharAt<TOptions>(index) == '-';
            if (negative)
                index += step;
            mantissa = 0;
            exponent10 = 0;
            int places = 0;
            exactMantissa = true;
            end = index;
            uint first = NumberDigitOrNonDigit<TOptions>(index);
            if (first > 9)
                return false;
            mantissa = first;
            index += step;
            int digits = 1;
            uint digit;
            while ((digit = NumberDigitOrNonDigit<TOptions>(index)) <= 9)
            {
                if (first == 0)
                    return false;
                if (digits < 19)
                {
                    mantissa = mantissa * 10 + digit;
                    digits++;
                }
                else
                    exactMantissa = false;
                index += step;
            }

            if (!IsNumberAtEnd<TOptions>(index) && NumberCharAt<TOptions>(index) == '.')
            {
                index += step;
                int fractionStart = index;
                while ((digit = NumberDigitOrNonDigit<TOptions>(index)) <= 9)
                {
                    if (digits < 19)
                    {
                        mantissa = mantissa * 10 + digit;
                        digits++;
                    }
                    else
                        exactMantissa = false;
                    places++;
                    index += step;
                }

                if (index == fractionStart)
                    return false;
            }

            int exponent = 0;
            if (!IsNumberAtEnd<TOptions>(index) && (NumberCharAt<TOptions>(index) == 'e' || NumberCharAt<TOptions>(index) == 'E'))
            {
                index += step;
                bool negativeExponent = false;
                if (!IsNumberAtEnd<TOptions>(index) && (NumberCharAt<TOptions>(index) == '+' || NumberCharAt<TOptions>(index) == '-'))
                {
                    negativeExponent = NumberCharAt<TOptions>(index) == '-';
                    index += step;
                }

                int exponentStart = index;
                while ((digit = NumberDigitOrNonDigit<TOptions>(index)) <= 9)
                {
                    if (exponent < 10_000)
                        exponent = exponent * 10 + (int)digit;
                    else
                        exactMantissa = false;
                    index += step;
                }

                if (index == exponentStart)
                    return false;
                if (negativeExponent)
                    exponent = -exponent;
            }

            if (!IsValueDelimiterAt<TOptions>(index))
                return false;
            long scale = (long)exponent - places;
            if (scale < -400 || scale > 400)
                exactMantissa = false;
            else
                exponent10 = (int)scale;
            end = index;
            return true;
        }

        /// <summary>
        /// Exact fast path: exactly representable operands need a single IEEE-754 operation, so the
        /// result equals correct rounding of the decimal literal. Every other input returns false and
        /// is handled by the general-purpose parser.
        /// </summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool TryParseDoubleExact<TOptions>(in JsonNumberShape shape, out double value)
            where TOptions : struct, ILuminPackOptions
        {
            value = 0;
            if (!TryAccumulateDecimalDigits<TOptions>(shape, out ulong mantissa, out int exponent10))
                return false;
            if (mantissa == 0)
            {
                value = shape.Negative ? -0.0 : 0.0;
                return true;
            }

            // Older runtimes can convert UInt64 through a signed double plus 2^64.
            if (exponent10 == 0
#if !NET9_0_OR_GREATER
                && mantissa <= long.MaxValue
#endif
                )
            {
                value = shape.Negative ? -(double)mantissa : (double)mantissa;
                return true;
            }

            if (mantissa > (1UL << 53) || exponent10 < -22 || exponent10 > 22)
                return false;
            double m = mantissa;
            double result = exponent10 > 0 ? m * s_pow10Double[exponent10] : m / s_pow10Double[-exponent10];
            value = shape.Negative ? -result : result;
            return true;
        }

        /// <summary>Attempts to parse float exact and reports whether it succeeded.</summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool TryParseFloatExact<TOptions>(in JsonNumberShape shape, out float value)
            where TOptions : struct, ILuminPackOptions
        {
            value = 0;
            if (!TryAccumulateDecimalDigits<TOptions>(shape, out ulong mantissa, out int exponent10))
                return false;
            if (mantissa == 0)
            {
                value = shape.Negative ? -0f : 0f;
                return true;
            }

            // Older integer-to-single conversions can round through double first.
            if (exponent10 == 0
#if !NET10_0_OR_GREATER
                && mantissa <= (1UL << 53)
#endif
                )
            {
                value = shape.Negative ? -(float)mantissa : (float)mantissa;
                return true;
            }

            if (mantissa > (1UL << 24) || exponent10 < -10 || exponent10 > 10)
                return false;
            // The scaled fast path has already bounded the mantissa to 24 bits.
            float m = (int)mantissa;
            float result = exponent10 > 0 ? m * s_pow10Float[exponent10] : m / s_pow10Float[-exponent10];
            value = shape.Negative ? -result : result;
            return true;
        }

        /// <summary>Advances past the current number token using the same strict grammar as the numeric readers.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipNumberToken<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            Position = ScanJsonNumber<TOptions>(Position).End;
        }

#endregion

    }
}
