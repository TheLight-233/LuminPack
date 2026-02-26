using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Option;

namespace LuminPack.Core
{
    internal static class NumberCharLookup
    {
        internal static readonly bool[] IsNumberChar;
        
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
                    {
                        _currentIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                while (_currentIndex + 1 < _bufferReference.Length)
                {
                    char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        _currentIndex += 2;
                    }
                    else
                    {
                        break;
                    }
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
                var numberSpan = ReadNumberSpan();
                Utf8Parser.TryParse(numberSpan, out float value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                float.TryParse(numberChars, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value);
                return value;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            if (SerializeStringAsUtf8)
            {
                var numberSpan = ReadNumberSpan();
                Utf8Parser.TryParse(numberSpan, out double value, out _);
                return value;
            }
            else
            {
                var numberChars = ReadNumberChars();
                double.TryParse(numberChars, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value);
                return value;
            }
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
                decimal.TryParse(numberChars, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal value);
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
            
            return _bufferReference.Slice(start, _currentIndex - start);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<char> ReadNumberChars()
        {
            int start = _currentIndex;
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