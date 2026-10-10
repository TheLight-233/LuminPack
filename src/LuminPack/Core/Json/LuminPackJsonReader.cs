using System;
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
    internal static class NumberCharLookup
    {
#if NET8_0_OR_GREATER
        internal static readonly SearchValues<byte> ControlCharsSearchValues =
            SearchValues.Create(Enumerable.Range(0, 0x20).Select(static i => (byte)i).ToArray());

        internal static readonly SearchValues<char> ControlCharsCharSearchValues =
            SearchValues.Create(Enumerable.Range(0, 0x20).Select(static i => (char)i).ToArray());
#endif
    }
    
    [StructLayout(LayoutKind.Sequential)]
#if NET8_0_OR_GREATER
    [SkipLocalsInit]
#endif
    public unsafe ref struct LuminPackJsonReader
    {
        private ReadOnlySpan<byte> _bufferReference;
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif
        
        private readonly LuminPackReaderOptionalState _state;
        private readonly bool SerializeStringAsUtf8;
        private readonly int _maxDepth;
        /// <summary>True when the reader must validate the full RFC 8259 grammar (option ValidateJsonSyntax).</summary>
        private readonly bool _validateJsonSyntax;
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
        
        public enum JsonTokenType : byte
        {
            None,
            ObjectStart,
            ObjectEnd,
            ArrayStart,
            ArrayEnd,
            PropertyName,
            String,
            Number,
            True,
            False,
            Null
        }
        
        public JsonTokenType CurrentTokenType { get; internal set; }
        public int CurrentIndex => _currentIndex;
        public int Depth => _depth;
        
        public LuminPackReaderOptionalState OptionState => _state;
        public LuminPackSerializerOption Option => _state.Option;
        
        #region Constructors
        
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
            SerializeStringAsUtf8 = _state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            _maxDepth = _state.Option.MaxJsonDepth;
            _validateJsonSyntax = _state.Option.ValidateJsonSyntax;
            _utf8StringScratch = null;
            _utf16StringScratch = null;
        }
        
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
            SerializeStringAsUtf8 = _state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            _maxDepth = _state.Option.MaxJsonDepth;
            _validateJsonSyntax = _state.Option.ValidateJsonSyntax;
            _utf8StringScratch = null;
            _utf16StringScratch = null;
        }

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
            SerializeStringAsUtf8 = bufferWriter.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            _maxDepth = bufferWriter.Option.MaxJsonDepth;
            _validateJsonSyntax = bufferWriter.Option.ValidateJsonSyntax;
            _utf8StringScratch = null;
            _utf16StringScratch = null;
        }
        
        #endregion
        
        #region Core Methods
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetSpanReference(int index) =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
#if DEBUG
            if (_bufferReference.Length < _currentIndex + count)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
#endif
            _currentIndex += count;
        }
        
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference() =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetSpan()
        {
            return _bufferReference[.._currentIndex];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref int GetCurrentSpanOffset() => ref _currentIndex;
        
        #endregion
        
        #region JSON Basic Methods
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SkipWhitespace()
        {
            if (SerializeStringAsUtf8)
            {
                while (_currentIndex < _bufferReference.Length)
                {
                    byte b = _bufferReference[_currentIndex];
                    if (b == ' ' || b == '\t' || b == '\r' || b == '\n')
                        _currentIndex++;
                    else
                        break;
                }
            }
            else
            {
                while (_currentIndex + 1 < _bufferReference.Length)
                {
                    char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                        _currentIndex += 2;
                    else
                        break;
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValueState(byte state) => state <= JsonStateValueOrEnd;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNameState(byte state) => state == JsonStateName || state == JsonStateNameOrEnd;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowMalformedJson(string message) =>
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCurrentContainerObject()
        {
            if (_depth == 0)
                return false;
            return _depth <= 64
                ? (_containerKindBits & (1UL << (_depth - 1))) != 0
                : _deepContainerKinds![_depth - 1] != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushContainer(bool isObject)
        {
            _depth++;
            if (_maxDepth > 0 && _depth > _maxDepth)
                ThrowMalformedJson("JSON nesting depth exceeds the configured maximum");

            if (_validateJsonSyntax)
            {
                if (_depth <= 64)
                {
                    if (isObject)
                        _containerKindBits |= 1UL << (_depth - 1);
                    else
                        _containerKindBits &= ~(1UL << (_depth - 1));
                }
                else
                {
                    SetDeepContainerKind(_depth, isObject);
                }
            }

            _jsonState = isObject ? JsonStateNameOrEnd : JsonStateValueOrEnd;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopContainer(bool isObject)
        {
            if (_validateJsonSyntax && (_depth == 0 || IsCurrentContainerObject() != isObject))
                ThrowMalformedJson("Mismatched JSON closing bracket");

            if (_depth > 0)
                _depth--;
            _jsonState = JsonStateAfterValue;
        }

        /// <summary>Re-reads the next character after a separator; returns false at end of input.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadCurrentChar(out char c)
        {
            if (SerializeStringAsUtf8)
            {
                if (_currentIndex >= _bufferReference.Length)
                {
                    c = default;
                    return false;
                }

                c = (char)_bufferReference[_currentIndex];
                return true;
            }

            if (_currentIndex + 1 >= _bufferReference.Length)
            {
                c = default;
                return false;
            }

            c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
            return true;
        }

        /// <summary>Ensures a number or literal token ends at EOF or at a JSON structural delimiter.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequireTokenEndBoundary(int index)
        {
            if (!_validateJsonSyntax)
                return;
            if (!IsValueDelimiterAt(index))
                ThrowMalformedJson("Invalid JSON token: expected a value delimiter");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValueDelimiterAt(int index)
        {
            if (SerializeStringAsUtf8)
            {
                if ((uint)index >= (uint)_bufferReference.Length)
                    return true;

                byte b = _bufferReference[index];
                return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r' ||
                       b == (byte)',' || b == (byte)']' || b == (byte)'}';
            }

            if (index + 1 >= _bufferReference.Length)
                return true;

            char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(index));
            return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == ',' || c == ']' || c == '}';
        }

        /// <summary>
        /// Fast exit for plain integer tokens (what game data is made of): the pre-change reader returned
        /// 1-2 digit values from two byte comparisons and left the rest to Utf8Parser, and benchmarking showed
        /// integer parsing 3.5x slower without a fast path. The grammar and the token boundary are still
        /// enforced — a leading zero must not be followed by a digit, the token must end at a value delimiter,
        /// and the magnitude must fit in 19 digits / <see cref="long"/> — while every other shape (fractions,
        /// exponents, out-of-range literals) falls through to <see cref="ScanJsonNumber"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadPlainInteger(out long value)
        {
            value = 0;
            int step = SerializeStringAsUtf8 ? 1 : 2;
            int i = _currentIndex;

            bool negative = false;
            if (!IsNumberAtEnd(i) && NumberCharAt(i) == '-')
            {
                negative = true;
                i += step;
            }

            uint first = NumberDigitOrNonDigit(i);
            if (first > 9u)
                return false;

            if (first == 0u)
            {
                if (NumberDigitOrNonDigit(i + step) <= 9u || !IsValueDelimiterAt(i + step))
                    return false;

                _currentIndex = i + step;
                return true;
            }

            ulong magnitude = first;
            int digits = 1;
            int j = i + step;
            while (true)
            {
                uint digit = NumberDigitOrNonDigit(j);
                if (digit > 9u)
                    break;

                if (digits == 19)
                {
                    // 20 digits can never fit; let the strict scanner report the range error.
                    if (!IsNumberAtEnd(j))
                        return false;

                    break;
                }

                magnitude = magnitude * 10 + digit;
                digits++;
                j += step;
            }

            if (!IsValueDelimiterAt(j))
                return false;

            if (negative ? magnitude > 9_223_372_036_854_775_808UL : magnitude > 9_223_372_036_854_775_807UL)
                return false;

            _currentIndex = j;
            value = negative ? unchecked(-(long)magnitude) : (long)magnitude;
            return true;
        }

        /// <summary>Digit value at <paramref name="index"/> or 10 when the buffer ends there.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitOrNonDigit(int index) =>
            IsNumberAtEnd(index) ? 10u : NumberDigitAt(index);
        
        /// <summary>
        /// Advances to the next JSON token: the original lenient tokenizer when the document is trusted
        /// (option ValidateJsonSyntax = false, the default), the fully validated tokenizer otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read() => _validateJsonSyntax ? ReadStrict() : ReadTrusted();

        private bool ReadTrusted()
        {
            SkipWhitespace();
            
            if (_currentIndex >= _bufferReference.Length)
                return false;
            
            char c;
            if (SerializeStringAsUtf8)
            {
                byte b = _bufferReference[_currentIndex];
                c = (char)b;
            }
            else
            {
                if (_currentIndex + 1 >= _bufferReference.Length)
                    return false;
                c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
            }
            
            while (true)
            {
                switch (c)
                {
                    case '{':
                        CurrentTokenType = JsonTokenType.ObjectStart;
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        _depth++;
                        if (_maxDepth > 0 && _depth > _maxDepth)
                            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON nesting depth exceeds the configured maximum");
                        return true;
                        
                    case '}':
                        CurrentTokenType = JsonTokenType.ObjectEnd;
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        _depth--;
                        return true;
                        
                    case '[':
                        CurrentTokenType = JsonTokenType.ArrayStart;
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        _depth++;
                        if (_maxDepth > 0 && _depth > _maxDepth)
                            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON nesting depth exceeds the configured maximum");
                        return true;
                        
                    case ']':
                        CurrentTokenType = JsonTokenType.ArrayEnd;
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        _depth--;
                        return true;
                        
                    case '"':
                        CurrentTokenType = JsonTokenType.String;
                        return true;
                        
                    case 't':
                    case 'f':
                        return ReadBoolean();
                        
                    case 'n':
                        return ReadNull();
                        
                    case ',':
                    case ':':
                    {
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        SkipWhitespace();
                        if (_currentIndex >= _bufferReference.Length)
                            return false;
                        if (SerializeStringAsUtf8)
                            c = (char)_bufferReference[_currentIndex];
                        else
                        {
                            if (_currentIndex + 1 >= _bufferReference.Length)
                                return false;
                            c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
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
        
        private bool ReadStrict()
        {
            SkipWhitespace();
            
            if (_currentIndex >= _bufferReference.Length)
            {
                if (_validateJsonSyntax && _depth > 0)
                    ThrowMalformedJson("Unterminated JSON object or array");
                return false;
            }
            
            char c;
            if (SerializeStringAsUtf8)
            {
                byte b = _bufferReference[_currentIndex];
                c = (char)b;
            }
            else
            {
                if (_currentIndex + 1 >= _bufferReference.Length)
                {
                    if (_depth > 0)
                        ThrowMalformedJson("Unterminated JSON object or array");
                    return false;
                }
                c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
            }
            
            while (true)
            {
                byte state = _jsonState;
                
                switch (c)
                {
                    case '{':
                        if (_validateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        PushContainer(true);
                        CurrentTokenType = JsonTokenType.ObjectStart;
                        return true;
                        
                    case '}':
                        if (_validateJsonSyntax && state != JsonStateNameOrEnd && state != JsonStateAfterValue)
                            ThrowMalformedJson("Unexpected '}' in JSON input");
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        PopContainer(true);
                        CurrentTokenType = JsonTokenType.ObjectEnd;
                        return true;
                        
                    case '[':
                        if (_validateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        PushContainer(false);
                        CurrentTokenType = JsonTokenType.ArrayStart;
                        return true;
                        
                    case ']':
                        if (_validateJsonSyntax && state != JsonStateValueOrEnd && state != JsonStateAfterValue)
                            ThrowMalformedJson("Unexpected ']' in JSON input");
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        PopContainer(false);
                        CurrentTokenType = JsonTokenType.ArrayEnd;
                        return true;
                        
                    case '"':
                        if (IsNameState(state))
                        {
                            _jsonState = JsonStateColon;
                        }
                        else
                        {
                            if (!IsValueState(state))
                                ThrowMalformedJson("Expected a JSON value");
                            _jsonState = JsonStateAfterValue;
                        }
                        CurrentTokenType = JsonTokenType.String;
                        return true;
                        
                    case 't':
                    case 'f':
                        if (_validateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        return ReadBoolean();
                        
                    case 'n':
                        if (_validateJsonSyntax && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        return ReadNull();
                        
                    case ',':
                    {
                        if (_validateJsonSyntax)
                        {
                            if (state != JsonStateAfterValue)
                                ThrowMalformedJson("Unexpected ',' in JSON input");
                            if (_depth == 0)
                                ThrowMalformedJson("Unexpected ',' outside a JSON container");
                        }
                        _jsonState = IsCurrentContainerObject() ? JsonStateName : JsonStateValue;
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        SkipWhitespace();
                        if (!TryReadCurrentChar(out c))
                        {
                            ThrowMalformedJson(_depth > 0
                                ? "Unterminated JSON object or array"
                                : "Unexpected end of JSON input after ','");
                        }
                        continue;
                    }
                        
                    case ':':
                    {
                        if (_validateJsonSyntax && state != JsonStateColon)
                            ThrowMalformedJson("Unexpected ':' in JSON input");
                        _jsonState = JsonStateValue;
                        _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                        SkipWhitespace();
                        if (!TryReadCurrentChar(out c))
                            ThrowMalformedJson("Unexpected end of JSON input after ':'");
                        continue;
                    }
                        
                    default:
                        if ((c >= '0' && c <= '9') || c == '-')
                        {
                            if (!IsValueState(state))
                                ThrowMalformedJson("Expected a JSON value");
                            _jsonState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.Number;
                            return true;
                        }
                        return false;
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeArrayStart()
        {
            if (CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
    
            if (Read() && CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
    
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON array");
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNull()
        {
            return CurrentTokenType == JsonTokenType.Null;
        }
        
        public void Skip()
        {
            switch (CurrentTokenType)
            {
                case JsonTokenType.ObjectStart:
                case JsonTokenType.ArrayStart:
                {
                    int initialDepth = _depth;
                    while (Read())
                    {
                        if ((CurrentTokenType == JsonTokenType.ObjectEnd ||
                             CurrentTokenType == JsonTokenType.ArrayEnd) &&
                            _depth < initialDepth)
                        {
                            return;
                        }

                        SkipScalarToken();
                    }

                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON object or array");
                    break;
                }
                    
                case JsonTokenType.String:
                case JsonTokenType.PropertyName:
                    SkipStringToken();
                    break;
                    
                case JsonTokenType.Number:
                    SkipNumberToken();
                    break;
                    
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>Verifies that no non-whitespace JSON remains after the current top-level value.</summary>
        public void EnsureEndOfDocument()
        {
            SkipWhitespace();
            if (_currentIndex != _bufferReference.Length)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unexpected data after the top-level JSON value");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipScalarToken()
        {
            switch (CurrentTokenType)
            {
                case JsonTokenType.String:
                case JsonTokenType.PropertyName:
                    SkipStringToken();
                    break;
                case JsonTokenType.Number:
                    SkipNumberToken();
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringToken()
        {
            if (SerializeStringAsUtf8)
                SkipStringUtf8();
            else
                SkipStringUtf16();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadStringUtf8() => ReadStringUtf8(true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadStringUtf8(bool unescape)
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");

            if (!SerializeStringAsUtf8)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-16 JSON");
            
            _currentIndex++;
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            var remaining = _bufferReference.Slice(_currentIndex);
            int special = remaining.IndexOfAny((byte)'"', (byte)'\\');
            if (special >= 0 && remaining[special] == '"')
            {
                if (remaining.Slice(0, special).ContainsAny(NumberCharLookup.ControlCharsSearchValues))
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                _currentIndex += special + 1;
                return _bufferReference.Slice(start, special);
            }

            if (special < 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
#endif

            int scan = _currentIndex;
            bool hasEscape = false;
            while (scan < _bufferReference.Length)
            {
                byte b = _bufferReference[scan];
                
                if (b == '"')
                {
                    var raw = _bufferReference.Slice(start, scan - start);
                    _currentIndex = scan + 1;
                    if (!hasEscape) return raw;
                    if (unescape) return UnescapeUtf8(raw);
                    ValidateStringEscapes(raw);
                    return raw;
                }
                else if (b == '\\')
                {
                    hasEscape = true;
                    scan++;
                    if (scan >= _bufferReference.Length)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                    scan++;
                }
                else
                {
                    if (b < 0x20)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    scan++;
                }
            }
            
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            return default;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            if (SerializeStringAsUtf8)
            {
                var utf8 = ReadStringUtf8();
                return Encoding.UTF8.GetString(utf8);
            }
            else
            {
                var utf16 = ReadStringUtf16();
                return new string(utf16);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> ReadStringUtf16() => ReadStringUtf16(true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadStringUtf16(bool unescape)
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");

            if (SerializeStringAsUtf8)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-8 JSON");
            
            _currentIndex += 2;
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            int available = (_bufferReference.Length - _currentIndex) & ~1;
            var charSpan = MemoryMarshal.Cast<byte, char>(
                _bufferReference.Slice(_currentIndex, available));
            int special = charSpan.IndexOfAny('"', '\\');
            if (special >= 0 && charSpan[special] == '"')
            {
                if (charSpan[..special].ContainsAny(NumberCharLookup.ControlCharsCharSearchValues))
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                var result = charSpan[..special];
                _currentIndex += (special + 1) * 2;
                return result;
            }

            if (special < 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
#endif

            int scan = _currentIndex;
            bool hasEscape = false;
            while (scan + 1 < _bufferReference.Length)
            {
                char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(scan));
                if (c == '"')
                {
                    int byteCount = scan - start;
                    var bytes = _bufferReference.Slice(start, byteCount);
                    var raw = MemoryMarshal.Cast<byte, char>(bytes);
                    _currentIndex = scan + 2;
                    if (!hasEscape) return raw;
                    if (unescape) return UnescapeUtf16(raw);
                    ValidateStringEscapes(raw);
                    return raw;
                }

                if (c == '\\')
                {
                    hasEscape = true;
                    scan += 2;
                    if (scan + 1 >= _bufferReference.Length)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                    scan += 2;
                }
                else
                {
                    if (c < 0x20)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                    scan += 2;
                }
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf8()
        {
            _ = ReadStringUtf8(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf16()
        {
            _ = ReadStringUtf16(false);
        }

        // The shared scanner already checked literal control characters and the closing quote.
        // Skipping still validates every escape, but never creates a scratch buffer.
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
                    case (byte)'"': case (byte)'\\': case (byte)'/':
                    case (byte)'b': case (byte)'f': case (byte)'n': case (byte)'r': case (byte)'t':
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
                    case '"': case '\\': case '/':
                    case 'b': case 'f': case 'n': case 'r': case 't':
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexValue(int value)
        {
            if ((uint)(value - '0') <= 9u) return value - '0';
            value |= 0x20;
            if ((uint)(value - 'a') <= 5u) return value - 'a' + 10;
            return -1;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadStringChoice(
            ReadOnlySpan<byte> firstUtf8,
            ReadOnlySpan<char> firstUtf16,
            ReadOnlySpan<byte> secondUtf8,
            ReadOnlySpan<char> secondUtf16)
        {
            if (SerializeStringAsUtf8)
            {
                var value = ReadStringUtf8();
                if (value.SequenceEqual(firstUtf8)) return 1;
                if (value.SequenceEqual(secondUtf8)) return 2;
            }
            else
            {
                var value = ReadStringUtf16();
                if (value.SequenceEqual(firstUtf16)) return 1;
                if (value.SequenceEqual(secondUtf16)) return 2;
            }

            return 0;
        }
        
        /// <summary>
        /// 用于嵌套对象 - 如果已经是 ObjectStart 就不再 Read
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeObjectStart()
        {
            if (CurrentTokenType == JsonTokenType.ObjectStart)
            {
                return true;
            }
            
            if (Read() && CurrentTokenType == JsonTokenType.ObjectStart)
            {
                return true;
            }
            
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON object");
            return false;
        }
        
        #endregion
        
        #region Primitive Types

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadNextFloatValue()
        {
            if (!Read() || CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Expected a number in JSON array");
            return ReadFloat();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConsumeArrayEnd()
        {
            if (!Read() || CurrentTokenType != JsonTokenType.ArrayEnd)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Expected the end of a JSON array");
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

            internal bool IsInteger => !HasFraction && !HasExponent;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNumberAtEnd(int index) =>
            SerializeStringAsUtf8 ? index >= _bufferReference.Length : index + 1 >= _bufferReference.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char NumberCharAt(int index) =>
            SerializeStringAsUtf8
                ? (char)_bufferReference[index]
                : Unsafe.ReadUnaligned<char>(ref GetSpanReference(index));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NumberDigitAt(int index)
        {
            char c = NumberCharAt(index);
            return (uint)(c - '0');
        }

        /// <summary>Validates a complete number token: [minus] int [frac] [exp] followed by EOF or a value delimiter.</summary>
        private JsonNumberShape ScanJsonNumber(int start)
        {
            int step = SerializeStringAsUtf8 ? 1 : 2;
            int i = start;
            bool negative = false;

            if (!IsNumberAtEnd(i) && NumberCharAt(i) == '-')
            {
                negative = true;
                i += step;
            }

            int intStart = i;
            int intDigits = 0;
            if (!IsNumberAtEnd(i) && NumberCharAt(i) == '0')
            {
                i += step;
                intDigits = 1;
            }
            else
            {
                while (!IsNumberAtEnd(i))
                {
                    uint d = NumberDigitAt(i);
                    if (d > 9u) break;
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
            if (!IsNumberAtEnd(i) && NumberCharAt(i) == '.')
            {
                hasFraction = true;
                i += step;
                fracStart = i;
                while (!IsNumberAtEnd(i))
                {
                    uint d = NumberDigitAt(i);
                    if (d > 9u) break;
                    i += step;
                }

                fracEnd = i;
                if (fracEnd == fracStart)
                    ThrowMalformedJson("Invalid JSON number: expected digits after '.'");
            }

            int exponent = 0;
            bool hasExponent = false;
            if (!IsNumberAtEnd(i))
            {
                char marker = NumberCharAt(i);
                if (marker == 'e' || marker == 'E')
                {
                    hasExponent = true;
                    i += step;
                    bool expNegative = false;
                    if (!IsNumberAtEnd(i))
                    {
                        char sign = NumberCharAt(i);
                        if (sign == '+' || sign == '-')
                        {
                            expNegative = sign == '-';
                            i += step;
                        }
                    }

                    int expStart = i;
                    int expValue = 0;
                    while (!IsNumberAtEnd(i))
                    {
                        uint d = NumberDigitAt(i);
                        if (d > 9u) break;
                        if (expValue < 1_000_000)
                            expValue = expValue * 10 + (int)d;
                        i += step;
                    }

                    if (i == expStart)
                        ThrowMalformedJson("Invalid JSON number: expected digits in the exponent");

                    exponent = expNegative ? -expValue : expValue;
                }
            }

            RequireTokenEndBoundary(i);
            return new JsonNumberShape(i, intStart, intEnd, fracStart, fracEnd, exponent, negative, hasFraction, hasExponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ParseUnsignedDigits(int start, int end, string error)
        {
            int step = SerializeStringAsUtf8 ? 1 : 2;
            if ((end - start) / step > 20)
                ThrowMalformedJson(error);

            if (end - start == step)
                return NumberDigitAt(start);

            ulong acc = 0;
            for (int i = start; i < end; i += step)
            {
                ulong d = NumberDigitAt(i);
                if (acc > 1_844_674_407_370_955_161UL || (acc == 1_844_674_407_370_955_161UL && d > 5UL))
                    ThrowMalformedJson(error);
                acc = acc * 10 + d;
            }

            return acc;
        }

        /// <summary>Converts token digits to mantissa * 10^exponent10 without dropping significant digits.</summary>
        private bool TryAccumulateDecimalDigits(in JsonNumberShape shape, out ulong mantissa, out int exponent10)
        {
            int step = SerializeStringAsUtf8 ? 1 : 2;
            mantissa = 0;
            exponent10 = 0;
            int digits = 0;
            bool started = false;

            for (int i = shape.IntStart; i < shape.IntEnd; i += step)
            {
                uint d = NumberDigitAt(i);
                if (!started)
                {
                    if (d == 0u) continue;
                    started = true;
                }

                if (digits == 19) return false;
                mantissa = mantissa * 10 + d;
                digits++;
            }

            for (int i = shape.FracStart; i < shape.FracEnd; i += step)
            {
                uint d = NumberDigitAt(i);
                if (!started)
                {
                    if (d == 0u)
                    {
                        if (exponent10 > -400) exponent10--;
                        continue;
                    }
                    started = true;
                }

                if (digits == 19) return false;
                mantissa = mantissa * 10 + d;
                digits++;
                if (exponent10 > -400) exponent10--;
            }

            if (shape.HasExponent)
                exponent10 += shape.Exponent;

            return true;
        }

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

        /// <summary>
        /// Exact fast path: exactly representable operands need a single IEEE-754 operation, so the
        /// result equals correct rounding of the decimal literal. Every other input returns false and
        /// is handled by the general-purpose parser.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseDoubleExact(in JsonNumberShape shape, out double value)
        {
            value = 0;
            if (!TryAccumulateDecimalDigits(shape, out ulong mantissa, out int exponent10))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseFloatExact(in JsonNumberShape shape, out float value)
        {
            value = 0;
            if (!TryAccumulateDecimalDigits(shape, out ulong mantissa, out int exponent10))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain) && plain >= int.MinValue && plain <= int.MaxValue)
                return (int)plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON integer");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON integer");
            if (shape.Negative)
            {
                if (magnitude > 2_147_483_648UL)
                    ThrowMalformedJson("Invalid JSON integer");
                _currentIndex = shape.End;
                return unchecked((int)(0UL - magnitude));
            }

            if (magnitude > 2_147_483_647UL)
                ThrowMalformedJson("Invalid JSON integer");
            _currentIndex = shape.End;
            return (int)magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain) && plain >= 0 && plain <= uint.MaxValue)
                return (uint)plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned integer");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned integer");
            if (magnitude > 4_294_967_295UL)
                ThrowMalformedJson("Invalid JSON unsigned integer");
            _currentIndex = shape.End;
            return (uint)magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain) && plain >= 0 && plain <= byte.MaxValue)
                return (byte)plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON byte");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON byte");
            if (magnitude > 255UL)
                ThrowMalformedJson("Invalid JSON byte");
            _currentIndex = shape.End;
            return (byte)magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain) && plain >= sbyte.MinValue && plain <= sbyte.MaxValue)
                return (sbyte)plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON signed byte");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON signed byte");
            if (shape.Negative)
            {
                if (magnitude > 128UL)
                    ThrowMalformedJson("Invalid JSON signed byte");
                _currentIndex = shape.End;
                return unchecked((sbyte)(0UL - magnitude));
            }

            if (magnitude > 127UL)
                ThrowMalformedJson("Invalid JSON signed byte");
            _currentIndex = shape.End;
            return (sbyte)magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain) && plain >= short.MinValue && plain <= short.MaxValue)
                return (short)plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON short integer");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON short integer");
            if (shape.Negative)
            {
                if (magnitude > 32_768UL)
                    ThrowMalformedJson("Invalid JSON short integer");
                _currentIndex = shape.End;
                return unchecked((short)(0UL - magnitude));
            }

            if (magnitude > 32_767UL)
                ThrowMalformedJson("Invalid JSON short integer");
            _currentIndex = shape.End;
            return (short)magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUShort()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain) && plain >= 0 && plain <= ushort.MaxValue)
                return (ushort)plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned short integer");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned short integer");
            if (magnitude > 65_535UL)
                ThrowMalformedJson("Invalid JSON unsigned short integer");
            _currentIndex = shape.End;
            return (ushort)magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain))
                return plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON long integer");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON long integer");
            if (shape.Negative)
            {
                if (magnitude > 9_223_372_036_854_775_808UL)
                    ThrowMalformedJson("Invalid JSON long integer");
                _currentIndex = shape.End;
                return unchecked((long)(0UL - magnitude));
            }

            if (magnitude > 9_223_372_036_854_775_807UL)
                ThrowMalformedJson("Invalid JSON long integer");
            _currentIndex = shape.End;
            return (long)magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadULong()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            if (TryReadPlainInteger(out long plain) && plain >= 0)
                return (ulong)plain;
            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned long integer");

            ulong magnitude = ParseUnsignedDigits(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned long integer");
            _currentIndex = shape.End;
            return magnitude;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            float value;
            if (!TryParseFloatExact(shape, out value))
            {
                value = SerializeStringAsUtf8
                    ? FallbackParseFloatUtf8(_bufferReference.Slice(_currentIndex, shape.End - _currentIndex))
                    : FallbackParseFloatUtf16(NumberChars(_currentIndex, shape.End));
            }

            _currentIndex = shape.End;
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            double value;
            if (!TryParseDoubleExact(shape, out value))
            {
                value = SerializeStringAsUtf8
                    ? FallbackParseDoubleUtf8(_bufferReference.Slice(_currentIndex, shape.End - _currentIndex))
                    : FallbackParseDoubleUtf16(NumberChars(_currentIndex, shape.End));
            }

            _currentIndex = shape.End;
            return value;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf8(ReadOnlySpan<byte> span)
        {
            if (!Utf8Parser.TryParse(span, out float v, out int consumed) || consumed != span.Length)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (float.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }
        
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
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf8(ReadOnlySpan<byte> span)
        {
            if (!Utf8Parser.TryParse(span, out double v, out int consumed) || consumed != span.Length)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid JSON floating-point number");
            if (double.IsInfinity(v))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON number overflows the target type");
            return v;
        }
        
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");

            JsonNumberShape shape = ScanJsonNumber(_currentIndex);
            decimal value;
            if (SerializeStringAsUtf8)
            {
                var token = _bufferReference.Slice(_currentIndex, shape.End - _currentIndex);
                if (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length)
                    ThrowMalformedJson("Invalid JSON decimal number");
            }
            else
            {
                if (!decimal.TryParse(NumberChars(_currentIndex, shape.End), System.Globalization.NumberStyles.Float,
                        System.Globalization.NumberFormatInfo.InvariantInfo, out value))
                    ThrowMalformedJson("Invalid JSON decimal number");
            }

            _currentIndex = shape.End;
            return value;
        }
        
        public char ReadChar()
        {
            if (SerializeStringAsUtf8)
            {
                var utf8 = ReadStringUtf8();
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
                var utf16 = ReadStringUtf16();
                return utf16.Length > 0 ? utf16[0] : '\0';
            }
        }
        
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
            _currentIndex = ScanJsonNumber(_currentIndex).End;
        }
        
        /// <summary>Reinterprets the validated UTF-16 number token as characters (callers only use this in UTF-16 mode).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> NumberChars(int start, int end)
        {
            var byteSpan = _bufferReference.Slice(start, end - start);
            ref char charRef = ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(byteSpan));
            return MemoryMarshal.CreateReadOnlySpan(ref charRef, (end - start) >> 1);
        }
        
        private bool ReadBoolean()
        {
            if (SerializeStringAsUtf8)
            {
                if (_bufferReference[_currentIndex] == 't')
                {
                    if (_currentIndex + 4 <= _bufferReference.Length &&
                        _bufferReference[_currentIndex + 1] == 'r' &&
                        _bufferReference[_currentIndex + 2] == 'u' &&
                        _bufferReference[_currentIndex + 3] == 'e')
                    {
                        _currentIndex += 4;
                        RequireTokenEndBoundary(_currentIndex);
                        _jsonState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.True;
                        return true;
                    }
                }
                else if (_bufferReference[_currentIndex] == 'f')
                {
                    if (_currentIndex + 5 <= _bufferReference.Length &&
                        _bufferReference[_currentIndex + 1] == 'a' &&
                        _bufferReference[_currentIndex + 2] == 'l' &&
                        _bufferReference[_currentIndex + 3] == 's' &&
                        _bufferReference[_currentIndex + 4] == 'e')
                    {
                        _currentIndex += 5;
                        RequireTokenEndBoundary(_currentIndex);
                        _jsonState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.False;
                        return true;
                    }
                }
            }
            else
            {
                char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                if (c == 't')
                {
                    if (_currentIndex + 8 <= _bufferReference.Length)
                    {
                        char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 2));
                        char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 4));
                        char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 6));
                        if (c1 == 'r' && c2 == 'u' && c3 == 'e')
                        {
                            _currentIndex += 8;
                            RequireTokenEndBoundary(_currentIndex);
                            _jsonState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.True;
                            return true;
                        }
                    }
                }
                else if (c == 'f')
                {
                    if (_currentIndex + 10 <= _bufferReference.Length)
                    {
                        char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 2));
                        char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 4));
                        char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 6));
                        char c4 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 8));
                        if (c1 == 'a' && c2 == 'l' && c3 == 's' && c4 == 'e')
                        {
                            _currentIndex += 10;
                            RequireTokenEndBoundary(_currentIndex);
                            _jsonState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.False;
                            return true;
                        }
                    }
                }
            }
            
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid boolean value");
            return false;
        }
        
        private bool ReadNull()
        {
            if (SerializeStringAsUtf8)
            {
                if (_currentIndex + 4 <= _bufferReference.Length &&
                    _bufferReference[_currentIndex] == 'n' &&
                    _bufferReference[_currentIndex + 1] == 'u' &&
                    _bufferReference[_currentIndex + 2] == 'l' &&
                    _bufferReference[_currentIndex + 3] == 'l')
                {
                    _currentIndex += 4;
                    RequireTokenEndBoundary(_currentIndex);
                    _jsonState = JsonStateAfterValue;
                    CurrentTokenType = JsonTokenType.Null;
                    return true;
                }
            }
            else
            {
                if (_currentIndex + 8 <= _bufferReference.Length)
                {
                    char c0 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                    char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 2));
                    char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 4));
                    char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 6));
                    if (c0 == 'n' && c1 == 'u' && c2 == 'l' && c3 == 'l')
                    {
                        _currentIndex += 8;
                        RequireTokenEndBoundary(_currentIndex);
                        _jsonState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.Null;
                        return true;
                    }
                }
            }
            
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid null value");
            return false;
        }
        
        #endregion
    }
}
