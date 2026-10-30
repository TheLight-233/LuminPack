using System.Buffers;
using System.Buffers.Text;
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
    public ref struct LuminPackJsonReader
#else
    public unsafe ref struct LuminPackJsonReader
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
            get => (nint)(_end - Cursor);
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
        /// <summary>Bit (depth - 1) is set while the container at that depth is an object.</summary>
        public ulong ObjectContainerBits;
        /// <summary>Container kinds for depths above 64; allocated only for unusually deep documents.</summary>
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
            DeepContainerKinds = null;
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
            DeepContainerKinds = null;
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
            DeepContainerKinds = null;
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
            get => (int)(Cursor - (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)));
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
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(position + count));
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
            DeepContainerKinds = null;
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
            SkipWhitespace<global::LuminPack.Option.LuminPackDefaultOptions>();
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
            return Depth <= 64 ? (ObjectContainerBits & (1UL << (Depth - 1))) != 0 : DeepContainerKinds![Depth - 1] != 0;
        }

        /// <summary>Performs the push container step for the current operation.</summary>
        /// <param name = "isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushContainer(bool isObject)
        {
            PushContainer<global::LuminPack.Option.LuminPackDefaultOptions>(isObject);
        }

        /// <summary>Updates the deep container kind used by the current operation.</summary>
        /// <param name = "depth">The nesting depth used to distinguish generated local variables.</param>
        /// <param name = "isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SetDeepContainerKind(int depth, bool isObject)
        {
            byte[] kinds = DeepContainerKinds!;
            if (kinds is null || depth > kinds.Length)
            {
                int size = Math.Max(depth, 256);
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
            PopContainer<global::LuminPack.Option.LuminPackDefaultOptions>(isObject);
        }

        /// <summary>Re-reads the next character after a separator; returns false at end of input.</summary>
        /// <param name = "c">Receives the c produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadCurrentChar(out char c)
        {
            return TryReadCurrentChar<global::LuminPack.Option.LuminPackDefaultOptions>(out c);
        }

        /// <summary>Ensures a number or literal token ends at EOF or at a JSON structural delimiter.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequireTokenEndBoundary(int index)
        {
            RequireTokenEndBoundary<global::LuminPack.Option.LuminPackDefaultOptions>(index);
        }

        /// <summary>Determines whether the supplied value has the value delimiter at classification.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValueDelimiterAt(int index)
        {
            return IsValueDelimiterAt<global::LuminPack.Option.LuminPackDefaultOptions>(index);
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
            return TryReadPlainInteger<global::LuminPack.Option.LuminPackDefaultOptions>(out value);
        }

        /// <summary>Digit value at <paramref name = "index"/> or 10 when the buffer ends there.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitOrNonDigit(int index)
        {
            return NumberDigitOrNonDigit<global::LuminPack.Option.LuminPackDefaultOptions>(index);
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
            return TryReadPropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(utf8Name, utf16Name);
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
            Skip<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Verifies that no non-whitespace JSON remains after the current top-level value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureEndOfDocument()
        {
            EnsureEndOfDocument<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Performs the skip scalar token step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipScalarToken()
        {
            SkipScalarToken<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Performs the parse hex4 step for the current operation.</summary>
        /// <param name = "source">The input data to process.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHex4(ReadOnlySpan<byte> source, int index)
        {
            if (index < 0 || source.Length - index < 4)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Incomplete Unicode escape sequence");
            ref byte table = ref MemoryMarshal.GetReference(HexDigits.Values);
            int a = Unsafe.Add(ref table, (nint)(uint)source[index]);
            int b = Unsafe.Add(ref table, (nint)(uint)source[index + 1]);
            int c = Unsafe.Add(ref table, (nint)(uint)source[index + 2]);
            int d = Unsafe.Add(ref table, (nint)(uint)source[index + 3]);
            if (((a | b | c | d) & 0xF0) != 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
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
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Incomplete Unicode escape sequence");
            uint a = source[index], b = source[index + 1], c = source[index + 2], d = source[index + 3];
            if ((a | b | c | d) > 255)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
            ref byte table = ref MemoryMarshal.GetReference(HexDigits.Values);
            int first = Unsafe.Add(ref table, (nint)a), second = Unsafe.Add(ref table, (nint)b);
            int third = Unsafe.Add(ref table, (nint)c), fourth = Unsafe.Add(ref table, (nint)d);
            if (((first | second | third | fourth) & 0xF0) != 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
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
            return TryConsumeObjectStart<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Determines whether the supplied value has the number at end classification.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNumberAtEnd(int index)
        {
            return IsNumberAtEnd<global::LuminPack.Option.LuminPackDefaultOptions>(index);
        }

        /// <summary>Performs the number char at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char NumberCharAt(int index)
        {
            return NumberCharAt<global::LuminPack.Option.LuminPackDefaultOptions>(index);
        }

        /// <summary>Performs the number digit at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitAt(int index)
        {
            return NumberDigitAt<global::LuminPack.Option.LuminPackDefaultOptions>(index);
        }

        /// <summary>Validates a complete number token: [minus] int [frac] [exp] followed by EOF or a value delimiter.</summary>
        /// <param name = "start">The start used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private JsonNumberShape ScanJsonNumber(int start)
        {
            return ScanJsonNumber<global::LuminPack.Option.LuminPackDefaultOptions>(start);
        }

        /// <summary>Converts token digits to mantissa * 10^exponent10 without dropping significant digits.</summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "mantissa">Receives the mantissa produced by the operation.</param>
        /// <param name = "exponent10">Receives the exponent10 produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool TryAccumulateDecimalDigits(in JsonNumberShape shape, out ulong mantissa, out int exponent10)
        {
            return TryAccumulateDecimalDigits<global::LuminPack.Option.LuminPackDefaultOptions>(in shape, out mantissa, out exponent10);
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
            return TryScanFloatingPoint<global::LuminPack.Option.LuminPackDefaultOptions>(out mantissa, out exponent10, out negative, out end, out exactMantissa);
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
            return TryParseDoubleExact<global::LuminPack.Option.LuminPackDefaultOptions>(in shape, out value);
        }

        /// <summary>Attempts to parse float exact and reports whether it succeeded.</summary>
        /// <param name = "shape">The shape used by this operation.</param>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseFloatExact(in JsonNumberShape shape, out float value)
        {
            return TryParseFloatExact<global::LuminPack.Option.LuminPackDefaultOptions>(in shape, out value);
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
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Current token is not a boolean");
            return false;
        }

        /// <summary>Advances past the current number token using the same strict grammar as the numeric readers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipNumberToken()
        {
            SkipNumberToken<global::LuminPack.Option.LuminPackDefaultOptions>();
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
                    char c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Current);
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
        }

        /// <summary>Performs the push container step for the current operation.</summary>
        /// <param name = "isObject">The is object used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void PushContainer<TOptions>(bool isObject)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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

            c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Current);
            return true;
        }

        /// <summary>Ensures a number or literal token ends at EOF or at a JSON structural delimiter.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void RequireTokenEndBoundary<TOptions>(int index)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                if ((uint)index >= (uint)Buffer.Length)
                    return true;
                byte b = Buffer[index];
                return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r' || b == (byte)',' || b == (byte)']' || b == (byte)'}';
            }

            if (index + 1 >= Buffer.Length)
                return true;
            char c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)));
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            value = 0;
            int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => IsNumberAtEnd<TOptions>(index) ? 10u : NumberDigitAt<TOptions>(index);
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            int index = Position;
            if ((uint)index >= (uint)Buffer.Length)
                return false;
            bool comma;
            int end;
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            switch (CurrentTokenType)
            {
                case JsonTokenType.ObjectStart:
                case JsonTokenType.ArrayStart:
                {
                    int initialDepth = Depth;
                    while (Read<TOptions>())
                    {
                        if ((CurrentTokenType == JsonTokenType.ObjectEnd || CurrentTokenType == JsonTokenType.ArrayEnd) && Depth < initialDepth)
                        {
                            return;
                        }

                        SkipScalarToken<TOptions>();
                    }

                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON object or array");
                    break;
                }

                case JsonTokenType.String:
                case JsonTokenType.PropertyName:
                    SkipStringToken<TOptions>();
                    break;
                case JsonTokenType.Number:
                    SkipNumberToken<TOptions>();
                    break;
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    break;
            }
        }

        /// <summary>Verifies that no non-whitespace JSON remains after the current top-level value.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void EnsureEndOfDocument<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            SkipWhitespace<TOptions>();
            if (Remaining != 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unexpected data after the top-level JSON value");
        }

        /// <summary>Performs the skip scalar token step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipScalarToken<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType == JsonTokenType.ObjectStart)
            {
                return true;
            }

            if (Read<TOptions>() && CurrentTokenType == JsonTokenType.ObjectStart)
            {
                return true;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON object");
            return false;
        }

        /// <summary>Determines whether the supplied value has the number at end classification.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool IsNumberAtEnd<TOptions>(int index)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? index >= Buffer.Length : index + 1 >= Buffer.Length;
        /// <summary>Performs the number char at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private char NumberCharAt<TOptions>(int index)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? (char)Buffer[index] : global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)));
        /// <summary>Performs the number digit at step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitAt<TOptions>(int index)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
            int i = start;
            bool negative = false;
            if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '-')
            {
                negative = true;
                i += step;
            }

            int intStart = i;
            int intDigits = 0;
            if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '0')
            {
                i += step;
                intDigits = 1;
            }
            else
            {
                while (!IsNumberAtEnd<TOptions>(i))
                {
                    uint d = NumberDigitAt<TOptions>(i);
                    if (d > 9u)
                        break;
                    i += step;
                    intDigits++;
                }
            }

            if (intDigits == 0)
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            value = 0;
            if (!TryAccumulateDecimalDigits<TOptions>(shape, out ulong mantissa, out int exponent10))
                return false;
            if (mantissa == 0)
            {
                value = shape.Negative ? -0.0 : 0.0;
                return true;
            }

            if (exponent10 == 0)
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            value = 0;
            if (!TryAccumulateDecimalDigits<TOptions>(shape, out ulong mantissa, out int exponent10))
                return false;
            if (mantissa == 0)
            {
                value = shape.Negative ? -0f : 0f;
                return true;
            }

            if (exponent10 == 0)
            {
                value = shape.Negative ? -(float)mantissa : (float)mantissa;
                return true;
            }

            if (mantissa > (1UL << 24) || exponent10 < -10 || exponent10 > 10)
                return false;
            float m = mantissa;
            float result = exponent10 > 0 ? m * s_pow10Float[exponent10] : m / s_pow10Float[-exponent10];
            value = shape.Negative ? -result : result;
            return true;
        }

        /// <summary>Advances past the current number token using the same strict grammar as the numeric readers.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipNumberToken<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            Position = ScanJsonNumber<TOptions>(Position).End;
        }

#endregion

#region Fused dictionary entries
        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref string key, ref long value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref string key, ref int value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref string key, ref string value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref int key, ref long value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref int key, ref int value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref int key, ref string value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Reads the key, value and closing bracket of a dictionary entry array whose opening bracket is the current token.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed and the token path reads it.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref string key, ref long value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, string, long>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref string key, ref int value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, string, int>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref string key, ref string value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, string, string>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref int key, ref long value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, int, long>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref int key, ref int value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, int, int>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref int key, ref string value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, int, string>(ref key, ref value);

        /// <summary>Parses <c>key , value ]</c> directly from a trusted UTF-8 document.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise the cursor and token are restored.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        /// <typeparam name = "TKey">The string or int key type.</typeparam>
        /// <typeparam name = "TValue">The string, int or long value type.</typeparam>
        /// <remarks>
        /// Strict documents, UTF-16 documents and any other token shape keep the validated token path. Strings use the
        /// same decoder as <see cref = "Read{TOptions}(ref string)"/>; integers use the same plain-decimal parser.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadEntryCore<TOptions, TKey, TValue>(ref TKey key, ref TValue value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).ValidateJsonSyntax || default(TOptions).StringEncoding != LuminPackStringEncoding.UTF8 ||
                CurrentTokenType != JsonTokenType.ArrayStart)
                return false;
            int start = Position;
            SkipWhitespace<TOptions>();
            if (!TryReadEntryScalar<TOptions, TKey>(out TKey parsedKey))
                goto Restore;
            SkipWhitespace<TOptions>();
            if (Remaining <= 0 || Current != (byte)',')
                goto Restore;
            Advance(1);
            SkipWhitespace<TOptions>();
            if (!TryReadEntryScalar<TOptions, TValue>(out TValue parsedValue))
                goto Restore;
            SkipWhitespace<TOptions>();
            if (Remaining <= 0 || Current != (byte)']')
                goto Restore;
            Advance(1);
            Depth--;
            CurrentTokenType = JsonTokenType.ArrayEnd;
            key = parsedKey;
            value = parsedValue;
            return true;
        Restore:
            Position = start;
            CurrentTokenType = JsonTokenType.ArrayStart;
            return false;
        }

        /// <summary>Parses one quoted string or plain decimal integer at the cursor.</summary>
        /// <param name = "result">Receives the parsed scalar.</param>
        /// <returns>Whether the token had the expected shape.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        /// <typeparam name = "T">The string, int or long scalar type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadEntryScalar<TOptions, T>(out T result)
            where TOptions : struct, ILuminPackOptions
        {
            result = default!;
            if (typeof(T) == typeof(string))
            {
                if (Remaining <= 0 || Current != (byte)'"')
                    return false;
                CurrentTokenType = JsonTokenType.String;
                string text = ReadString<TOptions>();
                result = Unsafe.As<string, T>(ref text);
                return true;
            }

            if (!TryReadPlainInteger<TOptions>(out long number))
                return false;
            if (typeof(T) == typeof(int))
            {
                if (number < int.MinValue || number > int.MaxValue)
                    return false;
                int narrow = (int)number;
                result = Unsafe.As<int, T>(ref narrow);
                return true;
            }

            result = Unsafe.As<long, T>(ref number);
            return true;
        }
#endregion

#region Values
        /// <summary>Reads a complete statically selected JSON value through the canonical API.</summary>
        /// <typeparam name = "T">The scalar or immutable string type selected by the caller.</typeparam>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The parsed value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T, TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null or JsonTokenType.PropertyName) && !Read<TOptions>())
                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value.");
            T value = ReadValueCore<T, TOptions>();
            CurrentTokenType = JsonTokenType.None;
            return value;
        }

        /// <summary>Decodes the already selected token through a JIT-specialized scalar kernel.</summary>
        /// <typeparam name = "T">The statically selected value type.</typeparam>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The parsed token value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValueCore<T, TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (typeof(T) == typeof(int))
            {
                int value = ReadInt<TOptions>();
                return Unsafe.As<int, T>(ref value);
            }

            if (typeof(T) == typeof(uint))
            {
                uint value = ReadUInt<TOptions>();
                return Unsafe.As<uint, T>(ref value);
            }

            if (typeof(T) == typeof(byte))
            {
                byte value = ReadByte<TOptions>();
                return Unsafe.As<byte, T>(ref value);
            }

            if (typeof(T) == typeof(sbyte))
            {
                sbyte value = ReadSByte<TOptions>();
                return Unsafe.As<sbyte, T>(ref value);
            }

            if (typeof(T) == typeof(short))
            {
                short value = ReadShort<TOptions>();
                return Unsafe.As<short, T>(ref value);
            }

            if (typeof(T) == typeof(ushort))
            {
                ushort value = ReadUShort<TOptions>();
                return Unsafe.As<ushort, T>(ref value);
            }

            if (typeof(T) == typeof(long))
            {
                long value = ReadLong<TOptions>();
                return Unsafe.As<long, T>(ref value);
            }

            if (typeof(T) == typeof(ulong))
            {
                ulong value = ReadULong<TOptions>();
                return Unsafe.As<ulong, T>(ref value);
            }

            if (typeof(T) == typeof(float))
            {
                float value = ReadFloat<TOptions>();
                return Unsafe.As<float, T>(ref value);
            }

            if (typeof(T) == typeof(double))
            {
                double value = ReadDouble<TOptions>();
                return Unsafe.As<double, T>(ref value);
            }

            if (typeof(T) == typeof(decimal))
            {
                decimal value = ReadDecimal<TOptions>();
                return Unsafe.As<decimal, T>(ref value);
            }

            if (typeof(T) == typeof(char))
            {
                char value = ReadChar<TOptions>();
                return Unsafe.As<char, T>(ref value);
            }

            if (typeof(T) == typeof(string))
            {
                string value = CurrentTokenType == JsonTokenType.Null ? null! : ReadString<TOptions>();
                return Unsafe.As<string, T>(ref value);
            }

            if (typeof(T) == typeof(bool))
            {
                bool value = GetBoolean();
                return Unsafe.As<bool, T>(ref value);
            }

            LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
            return default!;
        }

        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref int value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref int value)
            where TOptions : struct, ILuminPackOptions => value = Read<int, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref uint value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref uint value)
            where TOptions : struct, ILuminPackOptions => value = Read<uint, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref byte value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref byte value)
            where TOptions : struct, ILuminPackOptions => value = Read<byte, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref sbyte value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref sbyte value)
            where TOptions : struct, ILuminPackOptions => value = Read<sbyte, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref short value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref short value)
            where TOptions : struct, ILuminPackOptions => value = Read<short, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref ushort value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref ushort value)
            where TOptions : struct, ILuminPackOptions => value = Read<ushort, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref long value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref long value)
            where TOptions : struct, ILuminPackOptions => value = Read<long, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref ulong value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref ulong value)
            where TOptions : struct, ILuminPackOptions => value = Read<ulong, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref float value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref float value)
            where TOptions : struct, ILuminPackOptions => value = Read<float, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref double value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref double value)
            where TOptions : struct, ILuminPackOptions => value = Read<double, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref decimal value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref decimal value)
            where TOptions : struct, ILuminPackOptions => value = Read<decimal, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref char value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref char value)
            where TOptions : struct, ILuminPackOptions => value = Read<char, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref bool value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref bool value)
            where TOptions : struct, ILuminPackOptions => value = Read<bool, TOptions>();
        /// <summary>
        /// Advances to the next JSON token: the original lenient tokenizer when the document is trusted
        /// (option ValidateJsonSyntax = false, the default), the fully validated tokenizer otherwise.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read()
        {
            return Read<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads the next JSON token using the configured trusted-input path.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadTrusted()
        {
            return ReadTrusted<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads the next JSON token with structural validation enabled.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadStrict()
        {
            return ReadStrict<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads next float value from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadNextFloatValue()
        {
            return ReadNextFloatValue<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadInt()
        {
            return ReadInt<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads u int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadUInt()
        {
            return ReadUInt<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadByte()
        {
            return ReadByte<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads s byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private sbyte ReadSByte()
        {
            return ReadSByte<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short ReadShort()
        {
            return ReadShort<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads u short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadUShort()
        {
            return ReadUShort<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadLong()
        {
            return ReadLong<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads u long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadULong()
        {
            return ReadULong<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads float from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadFloat()
        {
            return ReadFloat<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads double from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double ReadDouble()
        {
            return ReadDouble<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads decimal from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private decimal ReadDecimal()
        {
            return ReadDecimal<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads char from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        private char ReadChar()
        {
            return ReadChar<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Consumes a true or false literal and updates the current token and reader position.</summary>
        /// <returns>True when a complete boolean literal was consumed.</returns>
        private bool ReadBoolean()
        {
            return ReadBoolean<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads null from the current input.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadNull()
        {
            return ReadNull<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>
        /// Advances to the next JSON token: the original lenient tokenizer when the document is trusted
        /// (option ValidateJsonSyntax = false, the default), the fully validated tokenizer otherwise.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public bool Read<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).ValidateJsonSyntax)
                return ReadStrict<TOptions>();
            if (TryReadAdjacentScalarToken<TOptions>())
                return true;
            return ReadTrusted<TOptions>();
        }
        /// <summary>Selects a complete adjacent scalar head while retaining checked separator consumption.</summary>
        /// <typeparam name="TOptions">The stateless encoding and grammar profile.</typeparam>
        /// <returns>Whether the original String or Number token was selected without consuming its content.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadAdjacentScalarToken<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (!TryReadCurrentChar<TOptions>(out char head))
                return false;
            int skip = 0;
            if (head == ',' || head == ':')
            {
                int step = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? 1 : 2;
                int position = Position;
                if ((uint)position > (uint)Buffer.Length || Buffer.Length - position < step * 2)
                    return false;
                head = NumberCharAt<TOptions>(position + step);
                skip = step;
            }

            JsonTokenType selected;
            if (head == '"')
                selected = JsonTokenType.String;
            else if ((uint)(head - '0') <= 9u || head == '-')
                selected = JsonTokenType.Number;
            else
                return false;
            if (skip != 0)
                Advance(skip);
            CurrentTokenType = selected;
            return true;
        }

        /// <summary>Reads the next JSON token using the configured trusted-input path.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool ReadTrusted<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            SkipWhitespace<TOptions>();
            if (Remaining <= 0)
                return false;
            char c;
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                byte b = Current;
                c = (char)b;
            }
            else
            {
                if (Remaining <= 1)
                    return false;
                c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Current);
            }

            while (true)
            {
                switch (c)
                {
                    case '{':
                        CurrentTokenType = JsonTokenType.ObjectStart;
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        Depth++;
                        if (default(TOptions).MaxJsonDepth > 0 && Depth > default(TOptions).MaxJsonDepth)
                            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON nesting depth exceeds the configured maximum");
                        return true;
                    case '}':
                        CurrentTokenType = JsonTokenType.ObjectEnd;
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        Depth--;
                        return true;
                    case '[':
                        CurrentTokenType = JsonTokenType.ArrayStart;
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        Depth++;
                        if (default(TOptions).MaxJsonDepth > 0 && Depth > default(TOptions).MaxJsonDepth)
                            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON nesting depth exceeds the configured maximum");
                        return true;
                    case ']':
                        CurrentTokenType = JsonTokenType.ArrayEnd;
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        Depth--;
                        return true;
                    case '"':
                        CurrentTokenType = JsonTokenType.String;
                        return true;
                    case 't':
                    case 'f':
                        return ReadBoolean<TOptions>();
                    case 'n':
                        return ReadNull<TOptions>();
                    case ',':
                    case ':':
                    {
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        SkipWhitespace<TOptions>();
                        if (Remaining <= 0)
                            return false;
                        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                            c = (char)Current;
                        else
                        {
                            if (Remaining <= 1)
                                return false;
                            c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Current);
                        }

                        continue;
                    }

                    default:
                        if ((c >= '0' && c <= '9') || c == '-')
                        {
                            CurrentTokenType = JsonTokenType.Number;
                            return true;
                        }

                        return false;
                }
            }
        }

        /// <summary>Reads the next JSON token with structural validation enabled.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadStrict<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            SkipWhitespace<TOptions>();
            if (Remaining <= 0)
            {
                if (default(TOptions).ValidateJsonSyntax && Depth > 0)
                    ThrowMalformedJson("Unterminated JSON object or array");
                return false;
            }

            char c;
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                byte b = Current;
                c = (char)b;
            }
            else
            {
                if (Remaining <= 1)
                {
                    if (Depth > 0)
                        ThrowMalformedJson("Unterminated JSON object or array");
                    return false;
                }

                c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Current);
            }

            while (true)
            {
                byte state = GrammarState;
                switch (c)
                {
                    case '{':
                        if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        PushContainer<TOptions>(true);
                        CurrentTokenType = JsonTokenType.ObjectStart;
                        return true;
                    case '}':
                        if (default(TOptions).ValidateJsonSyntax && state != JsonStateNameOrEnd && state != JsonStateAfterValue)
                            ThrowMalformedJson("Unexpected '}' in JSON input");
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        PopContainer<TOptions>(true);
                        CurrentTokenType = JsonTokenType.ObjectEnd;
                        return true;
                    case '[':
                        if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        PushContainer<TOptions>(false);
                        CurrentTokenType = JsonTokenType.ArrayStart;
                        return true;
                    case ']':
                        if (default(TOptions).ValidateJsonSyntax && state != JsonStateValueOrEnd && state != JsonStateAfterValue)
                            ThrowMalformedJson("Unexpected ']' in JSON input");
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        PopContainer<TOptions>(false);
                        CurrentTokenType = JsonTokenType.ArrayEnd;
                        return true;
                    case '"':
                        if (IsNameState(state))
                        {
                            GrammarState = JsonStateColon;
                        }
                        else
                        {
                            if (!IsValueState(state))
                                ThrowMalformedJson("Expected a JSON value");
                            GrammarState = JsonStateAfterValue;
                        }

                        CurrentTokenType = JsonTokenType.String;
                        return true;
                    case 't':
                    case 'f':
                        if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        return ReadBoolean<TOptions>();
                    case 'n':
                        if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        return ReadNull<TOptions>();
                    case ',':
                    {
                        if (default(TOptions).ValidateJsonSyntax)
                        {
                            if (state != JsonStateAfterValue)
                                ThrowMalformedJson("Unexpected ',' in JSON input");
                            if (Depth == 0)
                                ThrowMalformedJson("Unexpected ',' outside a JSON container");
                        }

                        GrammarState = IsCurrentContainerObject() ? JsonStateName : JsonStateValue;
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        SkipWhitespace<TOptions>();
                        if (!TryReadCurrentChar<TOptions>(out c))
                        {
                            ThrowMalformedJson(Depth > 0 ? "Unterminated JSON object or array" : "Unexpected end of JSON input after ','");
                        }

                        continue;
                    }

                    case ':':
                    {
                        if (default(TOptions).ValidateJsonSyntax && state != JsonStateColon)
                            ThrowMalformedJson("Unexpected ':' in JSON input");
                        GrammarState = JsonStateValue;
                        Advance((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2);
                        SkipWhitespace<TOptions>();
                        if (!TryReadCurrentChar<TOptions>(out c))
                            ThrowMalformedJson("Unexpected end of JSON input after ':'");
                        continue;
                    }

                    default:
                        if ((c >= '0' && c <= '9') || c == '-')
                        {
                            if (!IsValueState(state))
                                ThrowMalformedJson("Expected a JSON value");
                            GrammarState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.Number;
                            return true;
                        }

                        return false;
                }
            }
        }

        /// <summary>Reads next float value from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float ReadNextFloatValue<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (!Read<TOptions>() || CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Expected a number in JSON array");
            return ReadFloat<TOptions>();
        }

        /// <summary>Reads int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int ReadInt<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= int.MinValue && plain <= int.MaxValue)
                return (int)plain;
            return ReadIntSlow<TOptions>();
        }

        /// <summary>Parses an int token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private int ReadIntSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON integer");
            if (shape.Negative)
            {
                if (magnitude > 2_147_483_648UL)
                    ThrowMalformedJson("Invalid JSON integer");
                Position = shape.End;
                return unchecked((int)(0UL - magnitude));
            }

            if (magnitude > 2_147_483_647UL)
                ThrowMalformedJson("Invalid JSON integer");
            Position = shape.End;
            return (int)magnitude;
        }

        /// <summary>Reads u int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private uint ReadUInt<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= uint.MaxValue)
                return (uint)plain;
            return ReadUIntSlow<TOptions>();
        }

        /// <summary>Parses a uint token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private uint ReadUIntSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned integer");
            if (magnitude > 4_294_967_295UL)
                ThrowMalformedJson("Invalid JSON unsigned integer");
            Position = shape.End;
            return (uint)magnitude;
        }

        /// <summary>Reads byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private byte ReadByte<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= byte.MaxValue)
                return (byte)plain;
            return ReadByteSlow<TOptions>();
        }

        /// <summary>Parses a byte token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private byte ReadByteSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON byte");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON byte");
            if (magnitude > 255UL)
                ThrowMalformedJson("Invalid JSON byte");
            Position = shape.End;
            return (byte)magnitude;
        }

        /// <summary>Reads s byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private sbyte ReadSByte<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= sbyte.MinValue && plain <= sbyte.MaxValue)
                return (sbyte)plain;
            return ReadSByteSlow<TOptions>();
        }

        /// <summary>Parses an sbyte token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private sbyte ReadSByteSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON signed byte");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON signed byte");
            if (shape.Negative)
            {
                if (magnitude > 128UL)
                    ThrowMalformedJson("Invalid JSON signed byte");
                Position = shape.End;
                return unchecked((sbyte)(0UL - magnitude));
            }

            if (magnitude > 127UL)
                ThrowMalformedJson("Invalid JSON signed byte");
            Position = shape.End;
            return (sbyte)magnitude;
        }

        /// <summary>Reads short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private short ReadShort<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= short.MinValue && plain <= short.MaxValue)
                return (short)plain;
            return ReadShortSlow<TOptions>();
        }

        /// <summary>Parses a short token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private short ReadShortSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON short integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON short integer");
            if (shape.Negative)
            {
                if (magnitude > 32_768UL)
                    ThrowMalformedJson("Invalid JSON short integer");
                Position = shape.End;
                return unchecked((short)(0UL - magnitude));
            }

            if (magnitude > 32_767UL)
                ThrowMalformedJson("Invalid JSON short integer");
            Position = shape.End;
            return (short)magnitude;
        }

        /// <summary>Reads u short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ushort ReadUShort<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= ushort.MaxValue)
                return (ushort)plain;
            return ReadUShortSlow<TOptions>();
        }

        /// <summary>Parses a ushort token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ushort ReadUShortSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned short integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned short integer");
            if (magnitude > 65_535UL)
                ThrowMalformedJson("Invalid JSON unsigned short integer");
            Position = shape.End;
            return (ushort)magnitude;
        }

        /// <summary>Reads long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private long ReadLong<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain))
                return plain;
            return ReadLongSlow<TOptions>();
        }

        /// <summary>Parses a long token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private long ReadLongSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON long integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON long integer");
            if (shape.Negative)
            {
                if (magnitude > 9_223_372_036_854_775_808UL)
                    ThrowMalformedJson("Invalid JSON long integer");
                Position = shape.End;
                return unchecked((long)(0UL - magnitude));
            }

            if (magnitude > 9_223_372_036_854_775_807UL)
                ThrowMalformedJson("Invalid JSON long integer");
            Position = shape.End;
            return (long)magnitude;
        }

        /// <summary>Reads u long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ulong ReadULong<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0)
                return (ulong)plain;
            return ReadULongSlow<TOptions>();
        }

        /// <summary>Parses a ulong token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ulong ReadULongSlow<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned long integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned long integer");
            Position = shape.End;
            return magnitude;
        }

        /// <summary>Reads float from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private float ReadFloat<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent, out bool negative, out int end, out bool exact))
            {
                float result;
                if (exact && (exponent == 0 || (mantissa <= (1UL << 24) && exponent >= -10 && exponent <= 10)))
                {
                    result = exponent == 0 ? (float)mantissa : exponent > 0 ? (float)mantissa * s_pow10Float[exponent] : (float)mantissa / s_pow10Float[-exponent];
                    if (negative)
                        result = -result;
                }
                else
                {
                    result = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseFloatUtf8(Buffer.Slice(Position, end - Position)) : FallbackParseFloatUtf16(NumberChars(Position, end));
                }

                Position = end;
                return result;
            }

            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            float value;
            if (!TryParseFloatExact<TOptions>(shape, out value))
            {
                value = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseFloatUtf8(Buffer.Slice(Position, shape.End - Position)) : FallbackParseFloatUtf16(NumberChars(Position, shape.End));
            }

            Position = shape.End;
            return value;
        }

        /// <summary>Reads double from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private double ReadDouble<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent, out bool negative, out int end, out bool exact))
            {
                double result;
                if (exact && (exponent == 0 || (mantissa <= (1UL << 53) && exponent >= -22 && exponent <= 22)))
                {
                    result = exponent == 0 ? (double)mantissa : exponent > 0 ? (double)mantissa * s_pow10Double[exponent] : (double)mantissa / s_pow10Double[-exponent];
                    if (negative)
                        result = -result;
                }
                else
                {
                    result = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseDoubleUtf8(Buffer.Slice(Position, end - Position)) : FallbackParseDoubleUtf16(NumberChars(Position, end));
                }

                Position = end;
                return result;
            }

            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            double value;
            if (!TryParseDoubleExact<TOptions>(shape, out value))
            {
                value = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseDoubleUtf8(Buffer.Slice(Position, shape.End - Position)) : FallbackParseDoubleUtf16(NumberChars(Position, shape.End));
            }

            Position = shape.End;
            return value;
        }

        /// <summary>Reads decimal from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private decimal ReadDecimal<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            decimal value;
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                var token = Buffer.Slice(Position, shape.End - Position);
                if (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length)
                    ThrowMalformedJson("Invalid JSON decimal number");
            }
            else
            {
                if (!decimal.TryParse(NumberChars(Position, shape.End), System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out value))
                    ThrowMalformedJson("Invalid JSON decimal number");
            }

            Position = shape.End;
            return value;
        }

        /// <summary>Reads char from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private char ReadChar<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                var utf8 = ReadStringUtf8<TOptions>();
                if (utf8.Length == 0)
                    return '\0';
                if (utf8.Length == 1 && utf8[0] <= 0x7F)
                    return (char)utf8[0];
                Span<char> chars = stackalloc char[2];
                int len = Encoding.UTF8.GetChars(utf8, chars);
                return len > 0 ? chars[0] : '\0';
            }
            else
            {
                var utf16 = ReadStringUtf16<TOptions>();
                return utf16.Length > 0 ? utf16[0] : '\0';
            }
        }

        /// <summary>Consumes a true or false literal and updates the current token and reader position.</summary>
        /// <returns>True when a complete boolean literal was consumed.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadBoolean<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                if (Current == 't')
                {
                    if (Remaining >= 4 && Buffer[Position + 1] == 'r' && Buffer[Position + 2] == 'u' && Buffer[Position + 3] == 'e')
                    {
                        Advance(4);
                        RequireTokenEndBoundary<TOptions>(Position);
                        GrammarState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.True;
                        return true;
                    }
                }
                else if (Current == 'f')
                {
                    if (Remaining >= 5 && Buffer[Position + 1] == 'a' && Buffer[Position + 2] == 'l' && Buffer[Position + 3] == 's' && Buffer[Position + 4] == 'e')
                    {
                        Advance(5);
                        RequireTokenEndBoundary<TOptions>(Position);
                        GrammarState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.False;
                        return true;
                    }
                }
            }
            else
            {
                char c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Current);
                if (c == 't')
                {
                    if (Remaining >= 8)
                    {
                        char c1 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 2)));
                        char c2 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 4)));
                        char c3 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 6)));
                        if (c1 == 'r' && c2 == 'u' && c3 == 'e')
                        {
                            Advance(8);
                            RequireTokenEndBoundary<TOptions>(Position);
                            GrammarState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.True;
                            return true;
                        }
                    }
                }
                else if (c == 'f')
                {
                    if (Remaining >= 10)
                    {
                        char c1 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 2)));
                        char c2 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 4)));
                        char c3 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 6)));
                        char c4 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 8)));
                        if (c1 == 'a' && c2 == 'l' && c3 == 's' && c4 == 'e')
                        {
                            Advance(10);
                            RequireTokenEndBoundary<TOptions>(Position);
                            GrammarState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.False;
                            return true;
                        }
                    }
                }
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid boolean value");
            return false;
        }

        /// <summary>Reads null from the current input.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadNull<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                if (Remaining >= 4 && Current == 'n' && Buffer[Position + 1] == 'u' && Buffer[Position + 2] == 'l' && Buffer[Position + 3] == 'l')
                {
                    Advance(4);
                    RequireTokenEndBoundary<TOptions>(Position);
                    GrammarState = JsonStateAfterValue;
                    CurrentTokenType = JsonTokenType.Null;
                    return true;
                }
            }
            else
            {
                if (Remaining >= 8)
                {
                    char c0 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Current);
                    char c1 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 2)));
                    char c2 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 4)));
                    char c3 = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 6)));
                    if (c0 == 'n' && c1 == 'u' && c2 == 'l' && c3 == 'l')
                    {
                        Advance(8);
                        RequireTokenEndBoundary<TOptions>(Position);
                        GrammarState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.Null;
                        return true;
                    }
                }
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid null value");
            return false;
        }

#endregion

#region String
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
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Expected string token");
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
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
        private static void ThrowMalformedJson(string message) => global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(message);
        /// <summary>Performs the skip string token step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringToken()
        {
            SkipStringToken<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadStringUtf8()
        {
            return ReadStringUtf8<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadStringUtf8(bool unescape)
        {
            return ReadStringUtf8<global::LuminPack.Option.LuminPackDefaultOptions>(unescape);
        }

        /// <summary>Reads string from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ReadString()
        {
            return ReadString<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> ReadStringUtf16()
        {
            return ReadStringUtf16<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Reads string utf16 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadStringUtf16(bool unescape)
        {
            return ReadStringUtf16<global::LuminPack.Option.LuminPackDefaultOptions>(unescape);
        }

        /// <summary>Performs the skip string utf8 step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf8()
        {
            SkipStringUtf8<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Performs the skip string utf16 step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf16()
        {
            SkipStringUtf16<global::LuminPack.Option.LuminPackDefaultOptions>();
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
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
#if NET8_0_OR_GREATER
                    if (sourceIndex == literalProbe && source.Length - sourceIndex >= 16 && source[sourceIndex] != '\\')
                    {
                        var remaining = source.Slice(sourceIndex);
                        int runLength = remaining.IndexOfAny(NumberCharLookup.StringSpecialBytes);
                        if (runLength < 0) runLength = remaining.Length;
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
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
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
                                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("A high surrogate must be followed by a low surrogate");
                            }

                            int low = ParseHex4(source, sourceIndex + 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid low surrogate in JSON string");
                            sourceIndex += 6;
                            scalar = 0x10000 + ((scalar - 0xD800) << 10) + (low - 0xDC00);
                        }
                        else if ((uint)(scalar - 0xDC00) <= 0x3FFu)
                        {
                            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unexpected low surrogate in JSON string");
                        }

                        WriteUtf8Scalar(destination, ref destinationIndex, scalar);
                        break;
                    }

                    default:
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON escape sequence");
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
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
#if NET8_0_OR_GREATER
                    if (sourceIndex == literalProbe && source.Length - sourceIndex >= 16 && source[sourceIndex] != '\\')
                    {
                        var remaining = source.Slice(sourceIndex);
                        int runLength = remaining.IndexOfAny(NumberCharLookup.StringSpecialChars);
                        if (runLength < 0) runLength = remaining.Length;
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
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
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
                                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("A high surrogate must be followed by a low surrogate");
                            }

                            int low = ParseHex4(source, sourceIndex + 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid low surrogate in JSON string");
                            sourceIndex += 6;
                            destination[destinationIndex++] = (char)codeUnit;
                            destination[destinationIndex++] = (char)low;
                        }
                        else if ((uint)(codeUnit - 0xDC00) <= 0x3FFu)
                        {
                            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unexpected low surrogate in JSON string");
                        }
                        else
                        {
                            destination[destinationIndex++] = (char)codeUnit;
                        }

                        break;
                    }

                    default:
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON escape sequence");
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
        internal int ReadStringChoice(ReadOnlySpan<byte> firstUtf8, ReadOnlySpan<char> firstUtf16, ReadOnlySpan<byte> secondUtf8, ReadOnlySpan<char> secondUtf16)
        {
            return ReadStringChoice<global::LuminPack.Option.LuminPackDefaultOptions>(firstUtf8, firstUtf16, secondUtf8, secondUtf16);
        }

        /// <summary>Performs the parse unsigned digits step for the current operation.</summary>
        /// <param name = "start">The start used by this operation.</param>
        /// <param name = "end">The end used by this operation.</param>
        /// <param name = "error">The error used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ParseUnsignedDigits(int start, int end, string error)
        {
            return ParseUnsignedDigits<global::LuminPack.Option.LuminPackDefaultOptions>(start, end, error);
        }

        /// <summary>Performs the fallback parse float utf8 step for the current operation.</summary>
        /// <param name = "span">The buffer used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf8(ReadOnlySpan<byte> span)
        {
            if (!Utf8Parser.TryParse(span, out float v, out int consumed) || consumed != span.Length)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (float.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Performs the fallback parse float utf16 step for the current operation.</summary>
        /// <param name = "chars">The chars used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf16(ReadOnlySpan<char> chars)
        {
            if (!float.TryParse(chars, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out float v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (float.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Performs the fallback parse double utf8 step for the current operation.</summary>
        /// <param name = "span">The buffer used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf8(ReadOnlySpan<byte> span)
        {
            if (!Utf8Parser.TryParse(span, out double v, out int consumed) || consumed != span.Length)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (double.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Performs the fallback parse double utf16 step for the current operation.</summary>
        /// <param name = "chars">The chars used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf16(ReadOnlySpan<char> chars)
        {
            if (!double.TryParse(chars, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out double v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (double.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Performs the skip string token step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipStringToken<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                SkipStringUtf8<TOptions>();
            else
                SkipStringUtf16<TOptions>();
        }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadStringUtf8<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => ReadStringUtf8<TOptions>(true);
        /// <summary>Reads string utf8 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadStringUtf8<TOptions>(bool unescape)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");
            if (!(default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-16 JSON");
            Advance(1);
            int start = Position;
            int scan = Position;
#if NET8_0_OR_GREATER
            var remaining = Buffer.Slice(Position);
            int special = remaining.IndexOfAny(NumberCharLookup.StringSpecialBytes);
            if (special >= 0 && remaining[special] == '"')
            {
                if (default(TOptions).ValidateJsonSyntax && !StringSerializer.IsValidUtf8(remaining.Slice(0, special)))
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid UTF-8 in JSON string");
                Advance(special + 1);
                return Buffer.Slice(start, special);
            }

            if (special < 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            if (remaining[special] < 0x20)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
                        ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), scan));
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
                    if (default(TOptions).ValidateJsonSyntax && !StringSerializer.IsValidUtf8(raw))
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid UTF-8 in JSON string");
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
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                    scan++;
#if NET8_0_OR_GREATER
                    literalProbe = unchecked(scan + 32);
#endif
                }
                else
                {
                    if (b < 0x20)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            return default;
        }

        /// <summary>Reads string from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private string ReadString<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => ReadStringUtf16<TOptions>(true);
        /// <summary>Reads string utf16 from the current input.</summary>
        /// <param name = "unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadStringUtf16<TOptions>(bool unescape)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-8 JSON");
            Advance(2);
            int start = Position;
            int scan = Position;
#if NET8_0_OR_GREATER
            int available = (Buffer.Length - Position) & ~1;
            var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, available));
            int special = charSpan.IndexOfAny(NumberCharLookup.StringSpecialChars);
            if (special >= 0 && charSpan[special] == '"')
            {
                var result = charSpan[..special];
                Advance((special + 1) * 2);
                return result;
            }

            if (special < 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            if (charSpan[special] < 0x20)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            bool hasEscape = false;
#if NET8_0_OR_GREATER
            // Positions are byte offsets; thirty-two complete UTF-16 code units form a run.
            int literalProbe = unchecked(scan + 64);
#endif
            while (scan + 1 < Buffer.Length)
            {
                char c = global::LuminPack.Core.LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(scan)));
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
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                    scan += 2;
#if NET8_0_OR_GREATER
                    literalProbe = unchecked(scan + 64);
#endif
                }
                else
                {
                    if (c < 0x20)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            return default;
        }

#if NET8_0_OR_GREATER
        /// <summary>Finds the next string delimiter, escape or control within the remaining input window.</summary>
        /// <typeparam name="TOptions">The compiled document encoding.</typeparam>
        /// <param name="scan">The first unscanned byte offset.</param>
        /// <returns>The byte distance to the next special character, or minus one if none remains.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int FindStringSpecial<TOptions>(int scan) where TOptions : struct, ILuminPackOptions
        {
            // Keep the bounded search setup outside the scalar loop used by dense escapes.
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            _ = ReadStringUtf8<TOptions>(false);
        }

        /// <summary>Performs the skip string utf16 step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf16<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
        internal int ReadStringChoice<TOptions>(ReadOnlySpan<byte> firstUtf8, ReadOnlySpan<char> firstUtf16, ReadOnlySpan<byte> secondUtf8, ReadOnlySpan<char> secondUtf16)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
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

#endregion

#region Collections
        /// <summary>Counts validated array elements without advancing this reader or reading application values.</summary>
        /// <returns>The number of elements in the current array.</returns>
        public int CountArrayElements() => CountArrayElements<LuminPackDefaultOptions>();

        /// <summary>Counts array elements with the selected document profile without advancing this reader.</summary>
        /// <typeparam name="TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The number of complete elements in the current array.</returns>
        public int CountArrayElements<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            var scan = this;
            // Deep grammar storage is mutable; a lookahead must own its copy.
            scan.DeepContainerKinds = DeepContainerKinds is null ? null : DeepContainerKinds.AsSpan().ToArray();
            scan.TryConsumeArrayStart<TOptions>();
            int count = 0;
            while (scan.Read<TOptions>())
            {
                if (scan.CurrentTokenType == JsonTokenType.ArrayEnd)
                    return count;
                scan.Skip<TOptions>();
                count = checked(count + 1);
            }
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON array");
            return 0;
        }

        /// <summary>Attempts to consume array start and reports whether it succeeded.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeArrayStart()
        {
            return TryConsumeArrayStart<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Performs the consume array end step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConsumeArrayEnd()
        {
            ConsumeArrayEnd<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Attempts to consume array start and reports whether it succeeded.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeArrayStart<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
            if (Read<TOptions>() && CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON array");
            return false;
        }

        /// <summary>Performs the consume array end step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void ConsumeArrayEnd<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (!Read<TOptions>() || CurrentTokenType != JsonTokenType.ArrayEnd)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Expected the end of a JSON array");
        }
#endregion
    }
}
