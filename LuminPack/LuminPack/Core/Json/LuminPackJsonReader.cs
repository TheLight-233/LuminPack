using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Option;

namespace LuminPack.Core
{
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
        
        public bool Read()
        {
            SkipWhitespace();
            
            if (_currentIndex >= _bufferReference.Length)
                return false;
            
            byte b = _bufferReference[_currentIndex];
            
            switch (b)
            {
                case (byte)'{':
                    CurrentTokenType = JsonTokenType.ObjectStart;
                    _currentIndex++;
                    _depth++;
                    return true;
                    
                case (byte)'}':
                    CurrentTokenType = JsonTokenType.ObjectEnd;
                    _currentIndex++;
                    _depth--;
                    return true;
                    
                case (byte)'[':
                    CurrentTokenType = JsonTokenType.ArrayStart;
                    _currentIndex++;
                    _depth++;
                    return true;
                    
                case (byte)']':
                    CurrentTokenType = JsonTokenType.ArrayEnd;
                    _currentIndex++;
                    _depth--;
                    return true;
                    
                case (byte)'"':
                    CurrentTokenType = JsonTokenType.String;
                    return true;
                    
                case (byte)'t':
                case (byte)'f':
                    return ReadBoolean();
                    
                case (byte)'n':
                    return ReadNull();
                    
                case (byte)',':
                case (byte)':':
                    _currentIndex++;
                    return Read();
                    
                default:
                    if ((b >= '0' && b <= '9') || b == '-')
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
            var utf8 = ReadStringUtf8();
            return Encoding.UTF8.GetString(utf8);
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
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out int value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out uint value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out byte value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out sbyte value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out short value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUShort()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out ushort value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out long value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadULong()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out ulong value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out float value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out double value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
        {
            if (CurrentTokenType != JsonTokenType.Number)
                throw new InvalidOperationException("Expected number");
            
            var numberSpan = ReadNumberSpan();
            Utf8Parser.TryParse(numberSpan, out decimal value, out _);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar()
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
                if ((b >= '0' && b <= '9') || b == '.' || b == 'e' || b == 'E' || b == '+' || b == '-')
                {
                    _currentIndex++;
                }
                else
                {
                    break;
                }
            }
            
            return _bufferReference.Slice(start, _currentIndex - start);
        }
        
        private bool ReadBoolean()
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
            
            throw new FormatException("Invalid boolean value");
        }
        
        private bool ReadNull()
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
            
            throw new FormatException("Invalid null value");
        }
        
        #endregion
    }
}