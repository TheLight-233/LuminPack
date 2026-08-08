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
        internal static readonly bool[] IsNumberChar;
        
#if NET8_0_OR_GREATER
        internal static readonly SearchValues<byte> NumberSearchValues =
            SearchValues.Create("0123456789.eE+-"u8);
        
        internal static readonly SearchValues<char> NumberSearchValuesChar =
            SearchValues.Create("0123456789.eE+-");
#endif
        
        // 共享幂次查找表，Reader 和 Writer 均使用
        internal static readonly double[] Pow10d =
        {
            1e0,  1e1,  1e2,  1e3,  1e4,  1e5,  1e6,  1e7,  1e8,  1e9,
            1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17
        };
        
        static NumberCharLookup()
        {
            IsNumberChar = new bool[256];
            for (int i = '0'; i <= '9'; i++) IsNumberChar[i] = true;
            IsNumberChar['.'] = true;
            IsNumberChar['e'] = true;
            IsNumberChar['E'] = true;
            IsNumberChar['+'] = true;
            IsNumberChar['-'] = true;
        }
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
        private byte[]? _utf8StringScratch;
        private char[]? _utf16StringScratch;
        
        internal int _currentIndex;
        internal int _depth;
        
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
            CurrentTokenType = JsonTokenType.None;
            SerializeStringAsUtf8 = _state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
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
            CurrentTokenType = JsonTokenType.None;
            SerializeStringAsUtf8 = _state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
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
            CurrentTokenType = JsonTokenType.None;
            SerializeStringAsUtf8 = bufferWriter.Option.StringEncoding is LuminPackStringEncoding.UTF8;
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
        
        public bool Read()
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
            
            switch (c)
            {
                case '{':
                    CurrentTokenType = JsonTokenType.ObjectStart;
                    _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                    _depth++;
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
                    _currentIndex += SerializeStringAsUtf8 ? 1 : 2;
                    return Read();
                    
                default:
                    if ((c >= '0' && c <= '9') || c == '-')
                    {
                        CurrentTokenType = JsonTokenType.Number;
                        return true;
                    }
                    return false;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeArrayStart()
        {
            if (CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
    
            if (Read() && CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
    
            throw new InvalidOperationException("Expected JSON array");
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

                    throw new FormatException("Unterminated JSON object or array");
                }
                    
                case JsonTokenType.String:
                case JsonTokenType.PropertyName:
                    SkipStringToken();
                    break;
                    
                case JsonTokenType.Number:
                    if (SerializeStringAsUtf8)
                        ReadNumberSpan();
                    else
                        ReadNumberChars();
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
                throw new FormatException("Unexpected data after the top-level JSON value");
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
                    if (SerializeStringAsUtf8)
                        ReadNumberSpan();
                    else
                        ReadNumberChars();
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
        public ReadOnlySpan<byte> ReadStringUtf8()
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                throw new InvalidOperationException("Expected string");

            if (!SerializeStringAsUtf8)
                throw new InvalidOperationException("The reader is configured for UTF-16 JSON");
            
            _currentIndex++;
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            var remaining = _bufferReference.Slice(_currentIndex);
            int special = remaining.IndexOfAny((byte)'"', (byte)'\\');
            if (special >= 0 && remaining[special] == '"')
            {
                _currentIndex += special + 1;
                return _bufferReference.Slice(start, special);
            }

            if (special < 0)
                throw new FormatException("Unterminated JSON string");
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
                    return hasEscape ? UnescapeUtf8(raw) : raw;
                }
                else if (b == '\\')
                {
                    hasEscape = true;
                    scan++;
                    if (scan >= _bufferReference.Length)
                        throw new FormatException("Unterminated JSON escape sequence");
                    scan++;
                }
                else
                {
                    if (b < 0x20)
                        throw new FormatException("Unescaped control character in JSON string");
                    scan++;
                }
            }
            
            throw new FormatException("Unterminated JSON string");
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
        public ReadOnlySpan<char> ReadStringUtf16()
        {
            if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
                throw new InvalidOperationException("Expected string");

            if (SerializeStringAsUtf8)
                throw new InvalidOperationException("The reader is configured for UTF-8 JSON");
            
            _currentIndex += 2;
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            int available = (_bufferReference.Length - _currentIndex) & ~1;
            var charSpan = MemoryMarshal.Cast<byte, char>(
                _bufferReference.Slice(_currentIndex, available));
            int special = charSpan.IndexOfAny('"', '\\');
            if (special >= 0 && charSpan[special] == '"')
            {
                var result = charSpan[..special];
                _currentIndex += (special + 1) * 2;
                return result;
            }

            if (special < 0)
                throw new FormatException("Unterminated JSON string");
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
                    return hasEscape ? UnescapeUtf16(raw) : raw;
                }

                if (c == '\\')
                {
                    hasEscape = true;
                    scan += 2;
                    if (scan + 1 >= _bufferReference.Length)
                        throw new FormatException("Unterminated JSON escape sequence");
                    scan += 2;
                }
                else
                {
                    if (c < 0x20)
                        throw new FormatException("Unescaped control character in JSON string");
                    scan += 2;
                }
            }

            throw new FormatException("Unterminated JSON string");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf8()
        {
            _ = ReadStringUtf8();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipStringUtf16()
        {
            _ = ReadStringUtf16();
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
                        throw new FormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
                    continue;
                }

                if (sourceIndex >= source.Length)
                    throw new FormatException("Unterminated JSON escape sequence");

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
                                throw new FormatException("A high surrogate must be followed by a low surrogate");
                            }

                            int low = ParseHex4(source, sourceIndex + 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                throw new FormatException("Invalid low surrogate in JSON string");

                            sourceIndex += 6;
                            scalar = 0x10000 + ((scalar - 0xD800) << 10) + (low - 0xDC00);
                        }
                        else if ((uint)(scalar - 0xDC00) <= 0x3FFu)
                        {
                            throw new FormatException("Unexpected low surrogate in JSON string");
                        }

                        WriteUtf8Scalar(destination, ref destinationIndex, scalar);
                        break;
                    }
                    default:
                        throw new FormatException("Invalid JSON escape sequence");
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
                        throw new FormatException("Unescaped control character in JSON string");
                    destination[destinationIndex++] = value;
                    continue;
                }

                if (sourceIndex >= source.Length)
                    throw new FormatException("Unterminated JSON escape sequence");

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
                                throw new FormatException("A high surrogate must be followed by a low surrogate");
                            }

                            int low = ParseHex4(source, sourceIndex + 2);
                            if ((uint)(low - 0xDC00) > 0x3FFu)
                                throw new FormatException("Invalid low surrogate in JSON string");

                            sourceIndex += 6;
                            destination[destinationIndex++] = (char)codeUnit;
                            destination[destinationIndex++] = (char)low;
                        }
                        else if ((uint)(codeUnit - 0xDC00) <= 0x3FFu)
                        {
                            throw new FormatException("Unexpected low surrogate in JSON string");
                        }
                        else
                        {
                            destination[destinationIndex++] = (char)codeUnit;
                        }

                        break;
                    }
                    default:
                        throw new FormatException("Invalid JSON escape sequence");
                }
            }

            return _utf16StringScratch.AsSpan(0, destinationIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHex4(ReadOnlySpan<byte> source, int index)
        {
            if (index < 0 || source.Length - index < 4)
                throw new FormatException("Incomplete Unicode escape sequence");

            int result = 0;
            for (int i = 0; i < 4; i++)
            {
                int hex = HexValue(source[index + i]);
                if (hex < 0)
                    throw new FormatException("Invalid Unicode escape sequence");
                result = (result << 4) | hex;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHex4(ReadOnlySpan<char> source, int index)
        {
            if (index < 0 || source.Length - index < 4)
                throw new FormatException("Incomplete Unicode escape sequence");

            int result = 0;
            for (int i = 0; i < 4; i++)
            {
                int hex = HexValue(source[index + i]);
                if (hex < 0)
                    throw new FormatException("Invalid Unicode escape sequence");
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
            
            throw new InvalidOperationException("Expected JSON object");
        }
        
        #endregion
        
        #region Primitive Types

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float ReadNextFloatValue()
        {
            if (!Read() || CurrentTokenType != JsonTokenType.Number)
                throw new FormatException("Expected a number in JSON array");
            return ReadFloat();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ConsumeArrayEnd()
        {
            if (!Read() || CurrentTokenType != JsonTokenType.ArrayEnd)
                throw new FormatException("Expected the end of a JSON array");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                int start = _currentIndex;
                bool isNegative = false;
                
                if (_bufferReference[start] == '-')
                {
                    isNegative = true;
                    start++;
                }
                
                if (start + 2 <= _bufferReference.Length)
                {
                    byte b1 = _bufferReference[start];
                    if (b1 >= '0' && b1 <= '9')
                    {
                        byte b2 = _bufferReference[start + 1];
                        if (b2 < '0' || b2 > '9')
                        {
                            _currentIndex = start + 1;
                            int value = b1 - '0';
                            return isNegative ? -value : value;
                        }
                        else if (start + 2 < _bufferReference.Length)
                        {
                            byte b3 = _bufferReference[start + 2];
                            if (b3 < '0' || b3 > '9')
                            {
                                _currentIndex = start + 2;
                                int value = (b1 - '0') * 10 + (b2 - '0');
                                return isNegative ? -value : value;
                            }
                        }
                    }
                }
                _currentIndex = isNegative ? (start - 1) : start;
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out int result, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON integer");
                return result;
            }
            else
            {
                int start = _currentIndex;
                bool isNegative = false;
                
                if (_currentIndex + 1 < _bufferReference.Length)
                {
                    char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                    if (c == '-')
                    {
                        isNegative = true;
                        start += 2;
                    }
                    
                    if (start + 2 <= _bufferReference.Length)
                    {
                        char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(start));
                        if (c1 >= '0' && c1 <= '9')
                        {
                            if (start + 4 <= _bufferReference.Length)
                            {
                                char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(start + 2));
                                if (c2 < '0' || c2 > '9')
                                {
                                    _currentIndex = start + 2;
                                    int value = c1 - '0';
                                    return isNegative ? -value : value;
                                }
                                else if (start + 6 <= _bufferReference.Length)
                                {
                                    char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(start + 4));
                                    if (c3 < '0' || c3 > '9')
                                    {
                                        _currentIndex = start + 4;
                                        int value = (c1 - '0') * 10 + (c2 - '0');
                                        return isNegative ? -value : value;
                                    }
                                }
                            }
                        }
                    }
                }
                _currentIndex = isNegative ? (start - 2) : start;
                var numberChars = ReadNumberChars();
                if (!int.TryParse(numberChars, out int result))
                    throw new FormatException("Invalid JSON integer");
                return result;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                int start = _currentIndex;
                
                if (start + 2 <= _bufferReference.Length)
                {
                    byte b1 = _bufferReference[start];
                    if (b1 >= '0' && b1 <= '9')
                    {
                        byte b2 = _bufferReference[start + 1];
                        if (b2 < '0' || b2 > '9')
                        {
                            _currentIndex = start + 1;
                            return (uint)(b1 - '0');
                        }
                        else if (start + 2 < _bufferReference.Length)
                        {
                            byte b3 = _bufferReference[start + 2];
                            if (b3 < '0' || b3 > '9')
                            {
                                _currentIndex = start + 2;
                                return (uint)((b1 - '0') * 10 + (b2 - '0'));
                            }
                        }
                    }
                }
                
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out uint value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON unsigned integer");
                return value;
            }
            else
            {
                int start = _currentIndex;
                
                if (start + 2 <= _bufferReference.Length)
                {
                    char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(start));
                    if (c1 >= '0' && c1 <= '9')
                    {
                        if (start + 4 <= _bufferReference.Length)
                        {
                            char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(start + 2));
                            if (c2 < '0' || c2 > '9')
                            {
                                _currentIndex = start + 2;
                                return (uint)(c1 - '0');
                            }
                            else if (start + 6 <= _bufferReference.Length)
                            {
                                char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(start + 4));
                                if (c3 < '0' || c3 > '9')
                                {
                                    _currentIndex = start + 4;
                                    return (uint)((c1 - '0') * 10 + (c2 - '0'));
                                }
                            }
                        }
                    }
                }
                
                var numberChars = ReadNumberChars();
                if (!uint.TryParse(numberChars, out uint value))
                    throw new FormatException("Invalid JSON unsigned integer");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out byte value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON byte");
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                if (!byte.TryParse(numberChars, out byte value))
                    throw new FormatException("Invalid JSON byte");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out sbyte value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON signed byte");
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                if (!sbyte.TryParse(numberChars, out sbyte value))
                    throw new FormatException("Invalid JSON signed byte");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out short value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON short integer");
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                if (!short.TryParse(numberChars, out short value))
                    throw new FormatException("Invalid JSON short integer");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUShort()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out ushort value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON unsigned short integer");
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                if (!ushort.TryParse(numberChars, out ushort value))
                    throw new FormatException("Invalid JSON unsigned short integer");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out long value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON long integer");
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                if (!long.TryParse(numberChars, out long value))
                    throw new FormatException("Invalid JSON long integer");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadULong()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out ulong value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON unsigned long integer");
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                if (!ulong.TryParse(numberChars, out ulong value))
                    throw new FormatException("Invalid JSON unsigned long integer");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var span = ReadNumberSpan();
                return TryParseFloatUtf8(span, out float fast) ? fast : FallbackParseFloatUtf8(span);
            }
            else
            {
                var chars = ReadNumberChars();
                return TryParseFloatUtf16(chars, out float fast) ? fast : FallbackParseFloatUtf16(chars);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var span = ReadNumberSpan();
                return TryParseDoubleUtf8(span, out double fast) ? fast : FallbackParseDoubleUtf8(span);
            }
            else
            {
                var chars = ReadNumberChars();
                return TryParseDoubleUtf16(chars, out double fast) ? fast : FallbackParseDoubleUtf16(chars);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseFloatUtf8(ReadOnlySpan<byte> span, out float value)
        {
            if (span.IsEmpty) { value = 0; return false; }
            
            int pos = 0;
            bool neg = span[0] == (byte)'-';
            if (neg) pos++;
            
            if (pos >= span.Length) { value = 0; return false; }
            
            ulong intPart = 0;
            int intDigits = 0;
            while (pos < span.Length)
            {
                uint d = span[pos] - (uint)'0';
                if (d > 9u) break;
                intPart = intPart * 10 + d;
                pos++;
                if (++intDigits > 15) { value = 0; return false; }
            }

            if (intDigits == 0) { value = 0; return false; }
            
            ulong fracPart = 0;
            int fracDigits = 0;
            if (pos < span.Length && span[pos] == (byte)'.')
            {
                pos++;
                while (pos < span.Length)
                {
                    uint d = span[pos] - (uint)'0';
                    if (d > 9u) break;
                    fracPart = fracPart * 10 + d;
                    pos++;
                    if (++fracDigits > 9) { value = 0; return false; }
                }
                if (fracDigits == 0) { value = 0; return false; }
            }
            
            // 剩余字符（e/E/+/超长数字）→ fallback
            if (pos < span.Length) { value = 0; return false; }
            
            double result = (double)intPart;
            if (fracDigits > 0)
                result += (double)fracPart / NumberCharLookup.Pow10d[fracDigits];
            
            value = neg ? -(float)result : (float)result;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseFloatUtf16(ReadOnlySpan<char> span, out float value)
        {
            if (span.IsEmpty) { value = 0; return false; }
            
            int pos = 0;
            bool neg = span[0] == '-';
            if (neg) pos++;
            
            if (pos >= span.Length) { value = 0; return false; }
            
            ulong intPart = 0;
            int intDigits = 0;
            while (pos < span.Length)
            {
                uint d = span[pos] - (uint)'0';
                if (d > 9u) break;
                intPart = intPart * 10 + d;
                pos++;
                if (++intDigits > 15) { value = 0; return false; }
            }

            if (intDigits == 0) { value = 0; return false; }
            
            ulong fracPart = 0;
            int fracDigits = 0;
            if (pos < span.Length && span[pos] == '.')
            {
                pos++;
                while (pos < span.Length)
                {
                    uint d = span[pos] - (uint)'0';
                    if (d > 9u) break;
                    fracPart = fracPart * 10 + d;
                    pos++;
                    if (++fracDigits > 9) { value = 0; return false; }
                }
                if (fracDigits == 0) { value = 0; return false; }
            }
            
            if (pos < span.Length) { value = 0; return false; }
            
            double result = (double)intPart;
            if (fracDigits > 0)
                result += (double)fracPart / NumberCharLookup.Pow10d[fracDigits];
            
            value = neg ? -(float)result : (float)result;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseDoubleUtf8(ReadOnlySpan<byte> span, out double value)
        {
            if (span.IsEmpty) { value = 0; return false; }
            
            int pos = 0;
            bool neg = span[0] == (byte)'-';
            if (neg) pos++;
            
            if (pos >= span.Length) { value = 0; return false; }
            
            ulong intPart = 0;
            int intDigits = 0;
            while (pos < span.Length)
            {
                uint d = span[pos] - (uint)'0';
                if (d > 9u) break;
                intPart = intPart * 10 + d;
                pos++;
                if (++intDigits > 15) { value = 0; return false; }
            }

            if (intDigits == 0) { value = 0; return false; }
            
            ulong fracPart = 0;
            int fracDigits = 0;
            if (pos < span.Length && span[pos] == (byte)'.')
            {
                pos++;
                while (pos < span.Length)
                {
                    uint d = span[pos] - (uint)'0';
                    if (d > 9u) break;
                    fracPart = fracPart * 10 + d;
                    pos++;
                    if (++fracDigits > 17) { value = 0; return false; }
                }
                if (fracDigits == 0) { value = 0; return false; }
            }
            
            if (pos < span.Length) { value = 0; return false; }
            
            double result = (double)intPart;
            if (fracDigits > 0)
                result += (double)fracPart / NumberCharLookup.Pow10d[fracDigits];
            
            value = neg ? -result : result;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseDoubleUtf16(ReadOnlySpan<char> span, out double value)
        {
            if (span.IsEmpty) { value = 0; return false; }
            
            int pos = 0;
            bool neg = span[0] == '-';
            if (neg) pos++;
            
            if (pos >= span.Length) { value = 0; return false; }
            
            ulong intPart = 0;
            int intDigits = 0;
            while (pos < span.Length)
            {
                uint d = span[pos] - (uint)'0';
                if (d > 9u) break;
                intPart = intPart * 10 + d;
                pos++;
                if (++intDigits > 15) { value = 0; return false; }
            }

            if (intDigits == 0) { value = 0; return false; }
            
            ulong fracPart = 0;
            int fracDigits = 0;
            if (pos < span.Length && span[pos] == '.')
            {
                pos++;
                while (pos < span.Length)
                {
                    uint d = span[pos] - (uint)'0';
                    if (d > 9u) break;
                    fracPart = fracPart * 10 + d;
                    pos++;
                    if (++fracDigits > 17) { value = 0; return false; }
                }
                if (fracDigits == 0) { value = 0; return false; }
            }
            
            if (pos < span.Length) { value = 0; return false; }
            
            double result = (double)intPart;
            if (fracDigits > 0)
                result += (double)fracPart / NumberCharLookup.Pow10d[fracDigits];
            
            value = neg ? -result : result;
            return true;
        }
        
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf8(ReadOnlySpan<byte> span)
        {
            if (!Utf8Parser.TryParse(span, out float v, out int consumed) || consumed != span.Length)
                throw new FormatException("Invalid JSON floating-point number");
            return v;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf16(ReadOnlySpan<char> chars)
        {
            if (!float.TryParse(chars, System.Globalization.NumberStyles.Float,
                    System.Globalization.NumberFormatInfo.InvariantInfo, out float v))
                throw new FormatException("Invalid JSON floating-point number");
            return v;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf8(ReadOnlySpan<byte> span)
        {
            if (!Utf8Parser.TryParse(span, out double v, out int consumed) || consumed != span.Length)
                throw new FormatException("Invalid JSON floating-point number");
            return v;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf16(ReadOnlySpan<char> chars)
        {
            if (!double.TryParse(chars, System.Globalization.NumberStyles.Float,
                    System.Globalization.NumberFormatInfo.InvariantInfo, out double v))
                throw new FormatException("Invalid JSON floating-point number");
            return v;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                if (!Utf8Parser.TryParse(numberSpan, out decimal value, out int consumed) || consumed != numberSpan.Length)
                    throw new FormatException("Invalid JSON decimal number");
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                if (!decimal.TryParse(numberChars, System.Globalization.NumberStyles.Float,
                        System.Globalization.NumberFormatInfo.InvariantInfo, out decimal value))
                    throw new FormatException("Invalid JSON decimal number");
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
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
            
            throw new InvalidOperationException("Current token is not a boolean");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadNumberSpan()
        {
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            if (_currentIndex < _bufferReference.Length && _bufferReference[_currentIndex] == '-')
                _currentIndex++;
            
            var remaining = _bufferReference.Slice(_currentIndex);
            int end = remaining.IndexOfAnyExcept(NumberCharLookup.NumberSearchValues);
            _currentIndex += end < 0 ? remaining.Length : end;
#else
            if (_currentIndex < _bufferReference.Length && _bufferReference[_currentIndex] == '-')
                _currentIndex++;
            
            while (_currentIndex < _bufferReference.Length)
            {
                byte b = _bufferReference[_currentIndex];
                if (NumberCharLookup.IsNumberChar[b])
                    _currentIndex++;
                else
                    break;
            }
#endif
            
            return _bufferReference.Slice(start, _currentIndex - start);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadNumberChars()
        {
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            if (_currentIndex + 1 < _bufferReference.Length)
            {
                char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                if (c == '-')
                    _currentIndex += 2;
            }
            
            int available = (_bufferReference.Length - _currentIndex) & ~1;
            if (available > 0)
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(
                    _bufferReference.Slice(_currentIndex, available));
                int end = charSpan.IndexOfAnyExcept(NumberCharLookup.NumberSearchValuesChar);
                _currentIndex += (end < 0 ? charSpan.Length : end) * 2;
            }
            
            int totalBytes = _currentIndex - start;
            var byteSpan = _bufferReference.Slice(start, totalBytes);
            ref char charRef = ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(byteSpan));
            return MemoryMarshal.CreateReadOnlySpan(ref charRef, totalBytes / 2);
#else
            int charCount = 0;
            
            if (_currentIndex + 1 < _bufferReference.Length)
            {
                char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                if (c == '-')
                {
                    _currentIndex += 2;
                    charCount++;
                }
            }
            
            while (_currentIndex + 1 < _bufferReference.Length)
            {
                char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                if (c < 256 && NumberCharLookup.IsNumberChar[c])
                {
                    _currentIndex += 2;
                    charCount++;
                }
                else
                {
                    break;
                }
            }
            
            var byteSpan = _bufferReference.Slice(start, charCount * 2);
            ref char charRef = ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(byteSpan));
            return MemoryMarshal.CreateReadOnlySpan(ref charRef, charCount);
#endif
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
                        CurrentTokenType = JsonTokenType.True;
                        _currentIndex += 4;
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
                        CurrentTokenType = JsonTokenType.False;
                        _currentIndex += 5;
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
                            CurrentTokenType = JsonTokenType.True;
                            _currentIndex += 8;
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
                            CurrentTokenType = JsonTokenType.False;
                            _currentIndex += 10;
                            return true;
                        }
                    }
                }
            }
            
            throw new FormatException("Invalid boolean value");
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
                    CurrentTokenType = JsonTokenType.Null;
                    _currentIndex += 4;
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
                        CurrentTokenType = JsonTokenType.Null;
                        _currentIndex += 8;
                        return true;
                    }
                }
            }
            
            throw new FormatException("Invalid null value");
        }
        
        #endregion
    }
}
