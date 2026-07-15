using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;

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
        public void ReadValue<T>(ref T? value)
        {
            LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref this, ref value);
        }
        
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
            int initialDepth = _depth;
            
            switch (CurrentTokenType)
            {
                case JsonTokenType.ObjectStart:
                case JsonTokenType.ArrayStart:
                    while (Read() && _depth > initialDepth) { }
                    break;
                    
                case JsonTokenType.String:
                    ReadString();
                    break;
                    
                case JsonTokenType.Number:
                    ReadNumberSpan();
                    break;
                    
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    break;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadStringUtf8()
        {
            if (CurrentTokenType != JsonTokenType.String)
                throw new InvalidOperationException("Expected string");
            
            _currentIndex++;
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            while (true)
            {
                var remaining = _bufferReference.Slice(_currentIndex);
                int pos = remaining.IndexOfAny((byte)'"', (byte)'\\');
                
                if (pos < 0)
                    throw new InvalidOperationException("Unterminated string");
                
                _currentIndex += pos;
                
                if (remaining[pos] == '"')
                {
                    var result = _bufferReference.Slice(start, _currentIndex - start);
                    _currentIndex++;
                    return result;
                }
                
                _currentIndex += 2;
            }
#else
            while (_currentIndex < _bufferReference.Length)
            {
                byte b = _bufferReference[_currentIndex];
                
                if (b == '"')
                {
                    var result = _bufferReference.Slice(start, _currentIndex - start);
                    _currentIndex++;
                    return result;
                }
                else if (b == '\\')
                {
                    _currentIndex++;
                    if (_currentIndex < _bufferReference.Length)
                        _currentIndex++;
                }
                else
                {
                    _currentIndex++;
                }
            }
            
            throw new InvalidOperationException("Unterminated string");
#endif
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
            if (CurrentTokenType != JsonTokenType.String)
                throw new InvalidOperationException("Expected string");
            
            _currentIndex += 2;
            int start = _currentIndex;
            
#if NET8_0_OR_GREATER
            int available = (_bufferReference.Length - _currentIndex) & ~1;
            var charSpan = MemoryMarshal.Cast<byte, char>(
                _bufferReference.Slice(_currentIndex, available));
            int pos = charSpan.IndexOf('"');
            int charCount = pos < 0 ? charSpan.Length : pos;
            
            var byteSpan = _bufferReference.Slice(start, charCount * 2);
            ref char charRef = ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(byteSpan));
            _currentIndex = start + charCount * 2 + 2;
            return MemoryMarshal.CreateReadOnlySpan(ref charRef, charCount);
#else
            int charCount = 0;
            int tempIndex = _currentIndex;
            while (tempIndex + 1 < _bufferReference.Length)
            {
                ushort charValue = Unsafe.ReadUnaligned<ushort>(ref GetSpanReference(tempIndex));
                if (charValue == '"')
                    break;
                charCount++;
                tempIndex += 2;
            }
            
            var byteSpan = _bufferReference.Slice(start, charCount * 2);
            ref char charRef = ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(byteSpan));
            ReadOnlySpan<char> result = MemoryMarshal.CreateReadOnlySpan(ref charRef, charCount);
            
            _currentIndex = start + charCount * 2 + 2;
            return result;
#endif
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
                Utf8Parser.TryParse(numberSpan, out int result, out _);
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
                int.TryParse(numberChars, out int result);
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
                Utf8Parser.TryParse(numberSpan, out uint value, out _);
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
                uint.TryParse(numberChars, out uint value);
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
                Utf8Parser.TryParse(numberSpan, out byte value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                byte.TryParse(numberChars, out byte value);
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
                Utf8Parser.TryParse(numberSpan, out sbyte value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                sbyte.TryParse(numberChars, out sbyte value);
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
                Utf8Parser.TryParse(numberSpan, out short value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                short.TryParse(numberChars, out short value);
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
                Utf8Parser.TryParse(numberSpan, out ushort value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                ushort.TryParse(numberChars, out ushort value);
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
                Utf8Parser.TryParse(numberSpan, out long value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                long.TryParse(numberChars, out long value);
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
                Utf8Parser.TryParse(numberSpan, out ulong value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                ulong.TryParse(numberChars, out ulong value);
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
            Utf8Parser.TryParse(span, out float v, out _);
            return v;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float FallbackParseFloatUtf16(ReadOnlySpan<char> chars)
        {
            float.TryParse(chars, System.Globalization.NumberStyles.Float,
                System.Globalization.NumberFormatInfo.InvariantInfo, out float v);
            return v;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf8(ReadOnlySpan<byte> span)
        {
            Utf8Parser.TryParse(span, out double v, out _);
            return v;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FallbackParseDoubleUtf16(ReadOnlySpan<char> chars)
        {
            double.TryParse(chars, System.Globalization.NumberStyles.Float,
                System.Globalization.NumberFormatInfo.InvariantInfo, out double v);
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
                Utf8Parser.TryParse(numberSpan, out decimal value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                decimal.TryParse(numberChars, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out decimal value);
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