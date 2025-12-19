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
    [StructLayout(LayoutKind.Sequential)]
#if NET8_0_OR_GREATER
    [SkipLocalsInit]
#endif
    public unsafe ref struct LuminPackJsonWriter
    {
        private Span<byte> _bufferReference;
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif
        private int _currentIndex;
        private LuminBufferWriter? _writerBuffer;
        private readonly LuminPackWriterOptionalState _state;
        
        private int _depth;
        private bool _isFirstElement;
        
        public LuminPackWriterOptionalState OptionState => _state;
        public LuminPackSerializerOption Option => _state.Option;
        
        private const byte OpenBrace = (byte)'{';
        private const byte CloseBrace = (byte)'}';
        private const byte OpenBracket = (byte)'[';
        private const byte CloseBracket = (byte)']';
        private const byte Quote = (byte)'"';
        private const byte Colon = (byte)':';
        private const byte Comma = (byte)',';
        private const byte Backslash = (byte)'\\';
        
        public ref int CurrentIndex => ref _currentIndex;
        public int Depth => _depth;
        
        #region Constructors
        
        public LuminPackJsonWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState state)
        {
            _state = state;
            _bufferReference = bufferReference;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _writerBuffer = null;
            _depth = 0;
            _isFirstElement = true;
        }
        
        public LuminPackJsonWriter(LuminBufferWriter? bufferWriter, LuminPackWriterOptionalState state)
        {
            _state = state;
            
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            
            bufferWriter.SetCurrentIndexPtr(ref _currentIndex);
            _bufferReference = bufferWriter.GetSpan();
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _writerBuffer = bufferWriter;
            _currentIndex = 0;
            _depth = 0;
            _isFirstElement = true;
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
            _writerBuffer.Check(ref this);
            
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
            _isFirstElement = true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FlushBuffer()
        {
            if (_writerBuffer != null)
            {
                _bufferReference = _writerBuffer.GetSpan();
#if NET8_0_OR_GREATER
                _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
                _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference() =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _bufferReference[.._currentIndex];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref int GetCurrentSpanOffset() => ref _currentIndex;
        
        #endregion
        
        #region JSON Basic Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValue<T>(ref T? value)
        {
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref this, ref value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw(byte value)
        {
            GetCurrentSpanReference() = value;
            Advance(1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteRaw(ReadOnlySpan<byte> value)
        {
            value.CopyTo(_bufferReference.Slice(_currentIndex, value.Length));
            Advance(value.Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded()
        {
            if (!_isFirstElement)
            {
                WriteByteRaw(Comma);
            }
            _isFirstElement = false;
        }
        
        #endregion
        
        #region Object/Array
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart()
        {
            WriteCommaIfNeeded();
            WriteByteRaw(OpenBrace);
            _depth++;
            _isFirstElement = true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd()
        {
            WriteByteRaw(CloseBrace);
            _depth--;
            _isFirstElement = false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart()
        {
            WriteCommaIfNeeded();
            WriteByteRaw(OpenBracket);
            _depth++;
            _isFirstElement = true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd()
        {
            WriteByteRaw(CloseBracket);
            _depth--;
            _isFirstElement = false;
        }
        
        #endregion
        
        #region Property Name
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);
            WriteRaw(utf8PropertyName);
            WriteByteRaw(Quote);
            WriteByteRaw(Colon);
            
            _isFirstElement = true;
        }
        
        public void WritePropertyName(string propertyName)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);
            
            bool isAscii = true;
            for (int i = 0; i < propertyName.Length; i++)
            {
                char c = propertyName[i];
                if (c > 0x7F || c < 0x20 || c == '"' || c == '\\')
                {
                    isAscii = false;
                    break;
                }
            }
            
            if (isAscii)
            {
                for (int i = 0; i < propertyName.Length; i++)
                {
                    WriteByteRaw((byte)propertyName[i]);
                }
            }
            else
            {
                WriteStringContentSlow(propertyName.AsSpan());
            }
            
            WriteByteRaw(Quote);
            WriteByteRaw(Colon);
            
            _isFirstElement = true;
        }
        
        #endregion
        
        #region String
        
        public void WriteString(string? value)
        {
            if (value == null)
            {
                WriteNullInternal();
                return;
            }
            
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);
            
            bool needsSlow = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 0x7F || c < 0x20 || c == '"' || c == '\\')
                {
                    needsSlow = true;
                    break;
                }
            }
            
            if (!needsSlow)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    WriteByteRaw((byte)value[i]);
                }
            }
            else
            {
                WriteStringContentSlow(value.AsSpan());
            }
            
            WriteByteRaw(Quote);
        }
        
        private void WriteStringContentSlow(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                
                if (c == '"')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'"');
                }
                else if (c == '\\')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'\\');
                }
                else if (c == '\n')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'n');
                }
                else if (c == '\r')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'r');
                }
                else if (c == '\t')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'t');
                }
                else if (c < 0x20)
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'u');
                    WriteByteRaw((byte)'0');
                    WriteByteRaw((byte)'0');
                    WriteByteRaw(HexChar((c >> 4) & 0xF));
                    WriteByteRaw(HexChar(c & 0xF));
                }
                else if (c <= 0x7F)
                {
                    WriteByteRaw((byte)c);
                }
                else
                {
                    Span<byte> utf8Buf = stackalloc byte[4];
                    Span<char> charBuf = stackalloc char[1] { c };
                    int len = Encoding.UTF8.GetBytes(charBuf, utf8Buf);
                    WriteRaw(utf8Buf[..len]);
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte HexChar(int value)
        {
            return (byte)(value < 10 ? '0' + value : 'a' + (value - 10));
        }
        
        #endregion
        
        #region Primitive Types
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt(int value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[11];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt(uint value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[10];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[3];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[4];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShort(short value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[6];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUShort(ushort value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[5];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLong(long value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[20];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteULong(ulong value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[20];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[32];
            Utf8Formatter.TryFormat(value, buffer, out int written, new StandardFormat('G'));
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[32];
            Utf8Formatter.TryFormat(value, buffer, out int written, new StandardFormat('G'));
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimal(decimal value)
        {
            WriteCommaIfNeeded();
            Span<byte> buffer = stackalloc byte[31];
            Utf8Formatter.TryFormat(value, buffer, out int written);
            WriteRaw(buffer[..written]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);
            
            if (value == '"')
            {
                WriteByteRaw(Backslash);
                WriteByteRaw((byte)'"');
            }
            else if (value == '\\')
            {
                WriteByteRaw(Backslash);
                WriteByteRaw((byte)'\\');
            }
            else if (value >= 0x20 && value <= 0x7F)
            {
                WriteByteRaw((byte)value);
            }
            else
            {
                Span<byte> utf8Buf = stackalloc byte[4];
                Span<char> charBuf = stackalloc char[1] { value };
                int len = Encoding.UTF8.GetBytes(charBuf, utf8Buf);
                WriteRaw(utf8Buf[..len]);
            }
            
            WriteByteRaw(Quote);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value)
        {
            WriteCommaIfNeeded();
            if (value)
            {
                WriteByteRaw((byte)'t');
                WriteByteRaw((byte)'r');
                WriteByteRaw((byte)'u');
                WriteByteRaw((byte)'e');
            }
            else
            {
                WriteByteRaw((byte)'f');
                WriteByteRaw((byte)'a');
                WriteByteRaw((byte)'l');
                WriteByteRaw((byte)'s');
                WriteByteRaw((byte)'e');
            }
        }
        
        #endregion
        
        #region Null
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        {
            WriteCommaIfNeeded();
            WriteNullInternal();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal()
        {
            WriteByteRaw((byte)'n');
            WriteByteRaw((byte)'u');
            WriteByteRaw((byte)'l');
            WriteByteRaw((byte)'l');
        }
        
        #endregion
    }
}