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
        internal static readonly SearchValues<byte> StringSpecialBytes =
            SearchValues.Create(Enumerable.Range(0, 0x20).Select(static i => (byte)i)
                .Concat(new byte[] { (byte)'"', (byte)'\\' }).ToArray());

        internal static readonly SearchValues<char> StringSpecialChars =
            SearchValues.Create(Enumerable.Range(0, 0x20).Select(static i => (char)i)
                .Concat(new char[] { '"', '\\' }).ToArray());
#endif
    }

    /// <summary>Reads JSON tokens and scalar values with configurable structural validation and UTF encoding.</summary>
    [StructLayout(LayoutKind.Sequential)]
#if NET8_0_OR_GREATER
    [SkipLocalsInit]
#endif
    public unsafe ref partial struct LuminPackJsonReader
    {
        private ReadOnlySpan<byte> _bufferReference;
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif

        private readonly LuminPackReaderOptionalState _state;


        /// <summary>True when the reader must validate the full RFC 8259 grammar (option ValidateJsonSyntax).</summary>

        private byte[]? _utf8StringScratch;
        private char[]? _utf16StringScratch;

        internal int _currentIndex;
        internal int _depth;

        // --- RFC 8259 token sequence state -----------------------------------
        // Read() previously skipped ',', ':' unconditionally and never checked
        // which bracket closed which container, so malformed input such as
        // `[1 2]`, `{"a" 1}`, `[1,]` or `{"a":[1}}` was silently accepted.
        // The token returned last decides which separators may follow next.
        private const byte JsonStateValue = 0;       // value required: document start, after ':' or after ',' in an array
        private const byte JsonStateValueOrEnd = 1;   // after '[': a value or ']'
        private const byte JsonStateName = 2;        // after ',' in an object: a property name is required
        private const byte JsonStateNameOrEnd = 3;   // after '{': a property name or '}'
        private const byte JsonStateColon = 4;       // after a property name: ':' is required
        private const byte JsonStateAfterValue = 5;  // after a value: ',' or the matching closing bracket

        private byte _jsonState;
        /// <summary>Bit (depth - 1) is set while the container at that depth is an object.</summary>
        private ulong _containerKindBits;
        /// <summary>Container kinds for depths above 64; allocated only for unusually deep documents.</summary>
        private byte[]? _deepContainerKinds;

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

        /// <summary>Gets or sets the current token type.</summary>
        public JsonTokenType CurrentTokenType { get; internal set; }
        /// <summary>Gets the current index.</summary>
        public int CurrentIndex => _currentIndex;
        /// <summary>Gets the depth.</summary>
        public int Depth => _depth;

        /// <summary>Gets the option state.</summary>
        public LuminPackReaderOptionalState OptionState => _state;

        #region Constructors

        /// <summary>Initializes a new instance of <c>LuminPackJsonReader</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="state">The reusable state associated with the operation.</param>
        public LuminPackJsonReader(ref Span<byte> bufferReference, LuminPackReaderOptionalState state)
        {
            _state = state;
            _bufferReference = bufferReference;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _depth = 0;
            _jsonState = JsonStateValue;
            _containerKindBits = 0;
            _deepContainerKinds = null;
            CurrentTokenType = JsonTokenType.None;



            _utf8StringScratch = null;
            _utf16StringScratch = null;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonReader</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="state">The reusable state associated with the operation.</param>
        public LuminPackJsonReader(ref ReadOnlySpan<byte> bufferReference, LuminPackReaderOptionalState state)
        {
            _state = state;
            _bufferReference = bufferReference;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _depth = 0;
            _jsonState = JsonStateValue;
            _containerKindBits = 0;
            _deepContainerKinds = null;
            CurrentTokenType = JsonTokenType.None;



            _utf8StringScratch = null;
            _utf16StringScratch = null;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonReader</c>.</summary>
        /// <param name="bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LuminPackJsonReader(LuminBufferWriter bufferWriter)
        {
            _state = bufferWriter.ReaderState;
            _bufferReference = bufferWriter.GetSpan();
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _depth = 0;
            _jsonState = JsonStateValue;
            _containerKindBits = 0;
            _deepContainerKinds = null;
            CurrentTokenType = JsonTokenType.None;



            _utf8StringScratch = null;
            _utf16StringScratch = null;
        }

        #endregion

        #region Core Methods

        /// <summary>Obtains the span reference for the supplied input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetSpanReference(int index) =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

        /// <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
#if DEBUG
            if (_bufferReference.Length < _currentIndex + count)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
#endif
            _currentIndex += count;
        }

        /// <summary>Updates the span used by the current operation.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            _bufferReference = span;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
        }

        /// <summary>Publishes pending buffer contents to the configured destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _bufferReference = default;
#if NET8_0_OR_GREATER
            _bufferStart = ref Unsafe.NullRef<byte>();
#else
            _bufferStart = null;
#endif
            _currentIndex = 0;
            _depth = 0;
            _jsonState = JsonStateValue;
            _containerKindBits = 0;
            _deepContainerKinds = null;
            CurrentTokenType = JsonTokenType.None;
        }

        /// <summary>Obtains the current span reference for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference() =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

        /// <summary>Obtains the span for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetSpan()
        {
            return _bufferReference[.._currentIndex];
        }

        /// <summary>Obtains the current span offset for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref int GetCurrentSpanOffset() => ref _currentIndex;

        #endregion

        #region JSON Basic Methods

        /// <summary>Performs the skip whitespace step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SkipWhitespace()
        { SkipWhitespace<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Determines whether the supplied value has the value state classification.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValueState(byte state) => state <= JsonStateValueOrEnd;

        /// <summary>Determines whether the supplied value has the name state classification.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNameState(byte state) => state == JsonStateName || state == JsonStateNameOrEnd;

        /// <summary>Raises the exception for malformed json.</summary>
        /// <param name="message">The message describing the failure.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowMalformedJson(string message) =>
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(message);

        /// <summary>Determines whether the supplied value has the current container object classification.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCurrentContainerObject()
        {
            if (_depth == 0)
                return false;
            return _depth <= 64
                ? (_containerKindBits & (1UL << (_depth - 1))) != 0
                : _deepContainerKinds![_depth - 1] != 0;
        }

        /// <summary>Performs the push container step for the current operation.</summary>
        /// <param name="isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushContainer(bool isObject)
        { PushContainer<global::LuminPack.Option.LuminPackDefaultOptions>(isObject); }

        /// <summary>Updates the deep container kind used by the current operation.</summary>
        /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
        /// <param name="isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SetDeepContainerKind(int depth, bool isObject)
        {
            byte[] kinds = _deepContainerKinds!;
            if (kinds is null || depth > kinds.Length)
            {
                int size = Math.Max(depth, 256);
                Array.Resize(ref kinds, size);
                _deepContainerKinds = kinds;
            }

            kinds[depth - 1] = isObject ? (byte)1 : (byte)0;
        }

        /// <summary>Performs the pop container step for the current operation.</summary>
        /// <param name="isObject">The is object used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopContainer(bool isObject)
        { PopContainer<global::LuminPack.Option.LuminPackDefaultOptions>(isObject); }

        /// <summary>Re-reads the next character after a separator; returns false at end of input.</summary>
        /// <param name="c">Receives the c produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadCurrentChar(out char c)
        { return TryReadCurrentChar<global::LuminPack.Option.LuminPackDefaultOptions>(out c); }

        /// <summary>Ensures a number or literal token ends at EOF or at a JSON structural delimiter.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequireTokenEndBoundary(int index)
        { RequireTokenEndBoundary<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Determines whether the supplied value has the value delimiter at classification.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValueDelimiterAt(int index)
        { return IsValueDelimiterAt<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>
        /// Fast exit for plain integer tokens (what game data is made of): the pre-change reader returned
        /// 1-2 digit values from two byte comparisons and left the rest to Utf8Parser, and benchmarking showed
        /// integer parsing 3.5x slower without a fast path. The grammar and the token boundary are still
        /// enforced — a leading zero must not be followed by a digit, the token must end at a value delimiter,
        /// and the magnitude must fit in 19 digits / <see cref="long"/> — while every other shape (fractions,
        /// exponents, out-of-range literals) falls through to <see cref="ScanJsonNumber"/>.
        /// </summary>
        /// <param name="value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadPlainInteger(out long value)
        { return TryReadPlainInteger<global::LuminPack.Option.LuminPackDefaultOptions>(out value); }

        /// <summary>Digit value at <paramref name="index"/> or 10 when the buffer ends there.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitOrNonDigit(int index) { return NumberDigitOrNonDigit<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>
        /// Advances to the next JSON token: the original lenient tokenizer when the document is trusted
        /// (option ValidateJsonSyntax = false, the default), the fully validated tokenizer otherwise.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read() { return Read<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>
        /// Matches an unescaped generated property name and advances to its value token.
        /// A mismatch leaves the reader unchanged so ordinary property lookup can handle
        /// whitespace, reordered, missing, unknown, duplicate or escaped names.
        /// </summary>
        /// <param name="utf8Name">The utf8 name used by this operation.</param>
        /// <param name="utf16Name">The utf16 name used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadPropertyName(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<char> utf16Name)
        { return TryReadPropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(utf8Name, utf16Name); }

        /// <summary>Reads the next JSON token using the configured trusted-input path.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadTrusted()
        { return ReadTrusted<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads the next JSON token with structural validation enabled.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadStrict()
        { return ReadStrict<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Attempts to consume array start and reports whether it succeeded.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeArrayStart()
        { return TryConsumeArrayStart<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Determines whether the supplied value has the null classification.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNull()
        {
            return CurrentTokenType == JsonTokenType.Null;
        }

        /// <summary>Skips the current JSON value, including any nested containers.</summary>
        public void Skip()
        { Skip<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Verifies that no non-whitespace JSON remains after the current top-level value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureEndOfDocument()
        { EnsureEndOfDocument<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Performs the skip scalar token step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipScalarToken()
        { SkipScalarToken<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Performs the skip string token step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringToken()
        { SkipStringToken<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadStringUtf8() { return ReadStringUtf8<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads string utf8 from the current input.</summary>
        /// <param name="unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadStringUtf8(bool unescape)
        { return ReadStringUtf8<global::LuminPack.Option.LuminPackDefaultOptions>(unescape); }

        /// <summary>Reads string from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        { return ReadString<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads string utf16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> ReadStringUtf16() { return ReadStringUtf16<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads string utf16 from the current input.</summary>
        /// <param name="unescape">The unescape used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadStringUtf16(bool unescape)
        { return ReadStringUtf16<global::LuminPack.Option.LuminPackDefaultOptions>(unescape); }

        /// <summary>Performs the skip string utf8 step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf8()
        { SkipStringUtf8<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Performs the skip string utf16 step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf16()
        { SkipStringUtf16<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        // The shared scanner already checked literal control characters and the closing quote.
        // Skipping still validates every escape, but never creates a scratch buffer.
        /// <summary>Validates string escapes before the operation continues.</summary>
        /// <param name="source">The input data to process.</param>
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
        /// <param name="source">The input data to process.</param>
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
        /// <param name="source">The input data to process.</param>
        /// <returns>The result of the operation.</returns>
        private ReadOnlySpan<byte> UnescapeUtf8(ReadOnlySpan<byte> source)
        {
            if (_utf8StringScratch is null || _utf8StringScratch.Length < source.Length)
                _utf8StringScratch = new byte[source.Length];

            Span<byte> destination = _utf8StringScratch;
            int sourceIndex = 0;
            int destinationIndex = 0;

            while (sourceIndex < source.Length)
            {
                byte value = source[sourceIndex++];
                if (value != '\\')
                {
                    if (value < 0x20)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
                    continue;
                }

                if (sourceIndex >= source.Length)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");

                byte escape = source[sourceIndex++];
                switch (escape)
                {
                    case (byte)'"': destination[destinationIndex++] = (byte)'"'; break;
                    case (byte)'\\': destination[destinationIndex++] = (byte)'\\'; break;
                    case (byte)'/': destination[destinationIndex++] = (byte)'/'; break;
                    case (byte)'b': destination[destinationIndex++] = (byte)'\b'; break;
                    case (byte)'f': destination[destinationIndex++] = (byte)'\f'; break;
                    case (byte)'n': destination[destinationIndex++] = (byte)'\n'; break;
                    case (byte)'r': destination[destinationIndex++] = (byte)'\r'; break;
                    case (byte)'t': destination[destinationIndex++] = (byte)'\t'; break;
                    case (byte)'u':
                        {
                            int scalar = ParseHex4(source, sourceIndex);
                            sourceIndex += 4;

                            if ((uint)(scalar - 0xD800) <= 0x3FFu)
                            {
                                if (sourceIndex + 6 > source.Length ||
                                    source[sourceIndex] != '\\' || source[sourceIndex + 1] != 'u')
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
            }

            return _utf8StringScratch.AsSpan(0, destinationIndex);
        }

        /// <summary>Performs the unescape utf16 step for the current operation.</summary>
        /// <param name="source">The input data to process.</param>
        /// <returns>The result of the operation.</returns>
        private ReadOnlySpan<char> UnescapeUtf16(ReadOnlySpan<char> source)
        {
            if (_utf16StringScratch is null || _utf16StringScratch.Length < source.Length)
                _utf16StringScratch = new char[source.Length];

            Span<char> destination = _utf16StringScratch;
            int sourceIndex = 0;
            int destinationIndex = 0;

            while (sourceIndex < source.Length)
            {
                char value = source[sourceIndex++];
                if (value != '\\')
                {
                    if (value < 0x20)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
                    continue;
                }

                if (sourceIndex >= source.Length)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");

                char escape = source[sourceIndex++];
                switch (escape)
                {
                    case '"': destination[destinationIndex++] = '"'; break;
                    case '\\': destination[destinationIndex++] = '\\'; break;
                    case '/': destination[destinationIndex++] = '/'; break;
                    case 'b': destination[destinationIndex++] = '\b'; break;
                    case 'f': destination[destinationIndex++] = '\f'; break;
                    case 'n': destination[destinationIndex++] = '\n'; break;
                    case 'r': destination[destinationIndex++] = '\r'; break;
                    case 't': destination[destinationIndex++] = '\t'; break;
                    case 'u':
                        {
                            int codeUnit = ParseHex4(source, sourceIndex);
                            sourceIndex += 4;

                            if ((uint)(codeUnit - 0xD800) <= 0x3FFu)
                            {
                                if (sourceIndex + 6 > source.Length ||
                                    source[sourceIndex] != '\\' || source[sourceIndex + 1] != 'u')
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
            }

            return _utf16StringScratch.AsSpan(0, destinationIndex);
        }

        /// <summary>Performs the parse hex4 step for the current operation.</summary>
        /// <param name="source">The input data to process.</param>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHex4(ReadOnlySpan<byte> source, int index)
        {
            if (index < 0 || source.Length - index < 4)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Incomplete Unicode escape sequence");

            int result = 0;
            for (int i = 0; i < 4; i++)
            {
                int hex = HexValue(source[index + i]);
                if (hex < 0)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
                result = (result << 4) | hex;
            }
            return result;
        }

        /// <summary>Performs the parse hex4 step for the current operation.</summary>
        /// <param name="source">The input data to process.</param>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHex4(ReadOnlySpan<char> source, int index)
        {
            if (index < 0 || source.Length - index < 4)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Incomplete Unicode escape sequence");

            int result = 0;
            for (int i = 0; i < 4; i++)
            {
                int hex = HexValue(source[index + i]);
                if (hex < 0)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid Unicode escape sequence");
                result = (result << 4) | hex;
            }
            return result;
        }

        /// <summary>Performs the hex value step for the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexValue(int value)
        {
            if ((uint)(value - '0') <= 9u) return value - '0';
            value |= 0x20;
            if ((uint)(value - 'a') <= 5u) return value - 'a' + 10;
            return -1;
        }

        /// <summary>Writes utf8 scalar to the current output.</summary>
        /// <param name="destination">The destination receiving the result.</param>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="scalar">The scalar used by this operation.</param>
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
        /// <param name="firstUtf8">The first utf8 used by this operation.</param>
        /// <param name="firstUtf16">The first utf16 used by this operation.</param>
        /// <param name="secondUtf8">The second utf8 used by this operation.</param>
        /// <param name="secondUtf16">The second utf16 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadStringChoice(
            ReadOnlySpan<byte> firstUtf8,
            ReadOnlySpan<char> firstUtf16,
            ReadOnlySpan<byte> secondUtf8,
            ReadOnlySpan<char> secondUtf16)
        { return ReadStringChoice<global::LuminPack.Option.LuminPackDefaultOptions>(firstUtf8, firstUtf16, secondUtf8, secondUtf16); }

        /// <summary>
        /// For nested objects, reuses an existing ObjectStart token instead of advancing again.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeObjectStart()
        { return TryConsumeObjectStart<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        #endregion

        #region Primitive Types

        /// <summary>Reads next float value from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadNextFloatValue()
        { return ReadNextFloatValue<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Performs the consume array end step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConsumeArrayEnd()
        { ConsumeArrayEnd<global::LuminPack.Option.LuminPackDefaultOptions>(); }

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
            /// <param name="end">The end used by this operation.</param>
            /// <param name="intStart">The int start used by this operation.</param>
            /// <param name="intEnd">The int end used by this operation.</param>
            /// <param name="fracStart">The frac start used by this operation.</param>
            /// <param name="fracEnd">The frac end used by this operation.</param>
            /// <param name="exponent">The exponent used by this operation.</param>
            /// <param name="negative">The negative used by this operation.</param>
            /// <param name="hasFraction">The has fraction used by this operation.</param>
            /// <param name="hasExponent">The has exponent used by this operation.</param>
            internal JsonNumberShape(int end, int intStart, int intEnd, int fracStart, int fracEnd,
                int exponent, bool negative, bool hasFraction, bool hasExponent)
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

        /// <summary>Determines whether the supplied value has the number at end classification.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNumberAtEnd(int index) { return IsNumberAtEnd<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Performs the number char at step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char NumberCharAt(int index) { return NumberCharAt<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Performs the number digit at step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitAt(int index)
        { return NumberDigitAt<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Validates a complete number token: [minus] int [frac] [exp] followed by EOF or a value delimiter.</summary>
        /// <param name="start">The start used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private JsonNumberShape ScanJsonNumber(int start)
        { return ScanJsonNumber<global::LuminPack.Option.LuminPackDefaultOptions>(start); }

        /// <summary>Performs the parse unsigned digits step for the current operation.</summary>
        /// <param name="start">The start used by this operation.</param>
        /// <param name="end">The end used by this operation.</param>
        /// <param name="error">The error used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ParseUnsignedDigits(int start, int end, string error)
        { return ParseUnsignedDigits<global::LuminPack.Option.LuminPackDefaultOptions>(start, end, error); }

        /// <summary>Converts token digits to mantissa * 10^exponent10 without dropping significant digits.</summary>
        /// <param name="shape">The shape used by this operation.</param>
        /// <param name="mantissa">Receives the mantissa produced by the operation.</param>
        /// <param name="exponent10">Receives the exponent10 produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool TryAccumulateDecimalDigits(in JsonNumberShape shape, out ulong mantissa, out int exponent10)
        { return TryAccumulateDecimalDigits<global::LuminPack.Option.LuminPackDefaultOptions>(in shape, out mantissa, out exponent10); }

        private static readonly double[] s_pow10Double =
        {
            1e0,  1e1,  1e2,  1e3,  1e4,  1e5,  1e6,  1e7,  1e8,  1e9,
            1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
            1e20, 1e21, 1e22
        };

        private static readonly float[] s_pow10Float =
        {
            1e0f, 1e1f, 1e2f, 1e3f, 1e4f, 1e5f, 1e6f, 1e7f, 1e8f, 1e9f, 1e10f
        };

        // Scan and accumulate together. A valid but inexact/long token can go straight
        // to the platform parser, without another grammar scan or digit accumulation.
        // A failed scan leaves the cursor unchanged for the general grammar/error path.
        /// <summary>Attempts to scan floating point and reports whether it succeeded.</summary>
        /// <param name="mantissa">Receives the mantissa produced by the operation.</param>
        /// <param name="exponent10">Receives the exponent10 produced by the operation.</param>
        /// <param name="negative">Receives the negative produced by the operation.</param>
        /// <param name="end">Receives the end produced by the operation.</param>
        /// <param name="exactMantissa">Receives the exact mantissa produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool TryScanFloatingPoint(out ulong mantissa, out int exponent10, out bool negative,
            out int end, out bool exactMantissa)
        { return TryScanFloatingPoint<global::LuminPack.Option.LuminPackDefaultOptions>(out mantissa, out exponent10, out negative, out end, out exactMantissa); }

        /// <summary>
        /// Exact fast path: exactly representable operands need a single IEEE-754 operation, so the
        /// result equals correct rounding of the decimal literal. Every other input returns false and
        /// is handled by the general-purpose parser.
        /// </summary>
        /// <param name="shape">The shape used by this operation.</param>
        /// <param name="value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseDoubleExact(in JsonNumberShape shape, out double value)
        { return TryParseDoubleExact<global::LuminPack.Option.LuminPackDefaultOptions>(in shape, out value); }

        /// <summary>Attempts to parse float exact and reports whether it succeeded.</summary>
        /// <param name="shape">The shape used by this operation.</param>
        /// <param name="value">Receives the value produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseFloatExact(in JsonNumberShape shape, out float value)
        { return TryParseFloatExact<global::LuminPack.Option.LuminPackDefaultOptions>(in shape, out value); }

        /// <summary>Reads int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        { return ReadInt<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads u int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt()
        { return ReadUInt<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        { return ReadByte<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads s byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
        { return ReadSByte<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        { return ReadShort<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads u short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUShort()
        { return ReadUShort<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()
        { return ReadLong<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads u long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadULong()
        { return ReadULong<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads float from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        { return ReadFloat<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads double from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        { return ReadDouble<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Performs the fallback parse float utf8 step for the current operation.</summary>
        /// <param name="span">The buffer used by this operation.</param>
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
        /// <param name="chars">The chars used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf16(ReadOnlySpan<char> chars)
        {
            if (!float.TryParse(chars, System.Globalization.NumberStyles.Float,
                    System.Globalization.NumberFormatInfo.InvariantInfo, out float v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (float.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Performs the fallback parse double utf8 step for the current operation.</summary>
        /// <param name="span">The buffer used by this operation.</param>
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
        /// <param name="chars">The chars used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf16(ReadOnlySpan<char> chars)
        {
            if (!double.TryParse(chars, System.Globalization.NumberStyles.Float,
                    System.Globalization.NumberFormatInfo.InvariantInfo, out double v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (double.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }

        /// <summary>Reads decimal from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
        { return ReadDecimal<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads char from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public char ReadChar()
        { return ReadChar<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Returns the value represented by the current true or false JSON token.</summary>
        /// <returns>The current boolean value.</returns>
        /// <exception cref="InvalidOperationException">The current token is not a boolean.</exception>
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
        { SkipNumberToken<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reinterprets the validated UTF-16 number token as characters (callers only use this in UTF-16 mode).</summary>
        /// <param name="start">The start used by this operation.</param>
        /// <param name="end">The end used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> NumberChars(int start, int end)
        {
            var byteSpan = _bufferReference.Slice(start, end - start);
            ref char charRef = ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(byteSpan));
            return MemoryMarshal.CreateReadOnlySpan(ref charRef, (end - start) >> 1);
        }

        /// <summary>Consumes a true or false literal and updates the current token and reader position.</summary>
        /// <returns>True when a complete boolean literal was consumed.</returns>
        private bool ReadBoolean()
        { return ReadBoolean<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Reads null from the current input.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadNull()
        { return ReadNull<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        #endregion
    }
}
