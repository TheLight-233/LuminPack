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
        internal int _currentIndex;
        private LuminBufferWriter? _writerBuffer;
        private readonly LuminPackWriterOptionalState _state;
        private readonly bool SerializeStringAsUtf8;
        
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
        
        private static readonly StandardFormat s_floatFormatG = new('G');
        private static readonly StandardFormat s_doubleFormatG = new('G');
        
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
            SerializeStringAsUtf8 = _state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
        }
        
        public LuminPackJsonWriter(LuminBufferWriter? bufferWriter, LuminPackWriterOptionalState state)
        {
            _state = state;
            
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            
            bufferWriter.SetCurrentIndexPtr(ref _currentIndex);
            _bufferReference = bufferWriter.GetFullSpan();
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _writerBuffer = bufferWriter;
            _currentIndex = 0;
            _depth = 0;
            _isFirstElement = true;
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
                _bufferReference = _writerBuffer.GetFullSpan();
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
        public void SetFirstElement(bool value)
        {
            _isFirstElement = value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw(byte value)
        {
            if (SerializeStringAsUtf8)
            {
                GetCurrentSpanReference() = value;
                Advance(1);
            }
            else
            {
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.WriteUnaligned(ref dst, (char)value);
                Advance(2);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<byte> value)
        {
            value.CopyTo(_bufferReference.Slice(_currentIndex, value.Length));
            Advance(value.Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<char> value)
        {
            MemoryMarshal.Cast<char, byte>(value).CopyTo(_bufferReference.Slice(_currentIndex, value.Length << 1));
            Advance(value.Length << 1);
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<char> utf16PropertyName)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);
            
            if (SerializeStringAsUtf8)
            {
                WriteStringContentUtf8(utf16PropertyName);
            }
            else
            {
                int byteCount = utf16PropertyName.Length * 2;
                ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(utf16PropertyName));
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
            
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
            
            if (SerializeStringAsUtf8)
            {
                WriteStringContentUtf8(value.AsSpan());
            }
            else
            {
                WriteStringContentUtf16(value.AsSpan());
            }
            
            WriteByteRaw(Quote);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8(ReadOnlySpan<char> value)
        {
            bool needsEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 0x7F || c < 0x20 || c == '"' || c == '\\')
                {
                    needsEscape = true;
                    break;
                }
            }
            
            if (!needsEscape)
            {
                ref byte dst = ref GetCurrentSpanReference();
                for (int i = 0; i < value.Length; i++)
                {
                    Unsafe.Add(ref dst, i) = (byte)value[i];
                }
                Advance(value.Length);
            }
            else
            {
                WriteStringContentSlow(value);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf16(ReadOnlySpan<char> value)
        {
            bool needsEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c < 0x20 || c == '"' || c == '\\')
                {
                    needsEscape = true;
                    break;
                }
            }
            
            if (!needsEscape)
            {
                int byteCount = value.Length * 2;
                ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value));
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
            else
            {
                WriteStringContentUtf16Slow(value);
            }
        }
        
        private void WriteStringContentUtf16Slow(ReadOnlySpan<char> value)
        {
            int lastWritten = 0;
            
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool needsEscape = false;
                
                if (c == '"' || c == '\\' || c == '\n' || c == '\r' || c == '\t' || c < 0x20)
                {
                    needsEscape = true;
                }
                
                if (needsEscape)
                {
                    if (i > lastWritten)
                    {
                        var segment = value.Slice(lastWritten, i - lastWritten);
                        int byteCount = segment.Length * 2;
                        ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
                        ref byte dst = ref GetCurrentSpanReference();
                        Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                        Advance(byteCount);
                    }
                    
                    if (c == '"')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('"');
                    }
                    else if (c == '\\')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('\\');
                    }
                    else if (c == '\n')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('n');
                    }
                    else if (c == '\r')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('r');
                    }
                    else if (c == '\t')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('t');
                    }
                    else if (c < 0x20)
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('u');
                        WriteUtf16CharDirect(HexCharUtf16((c >> 12) & 0xF));
                        WriteUtf16CharDirect(HexCharUtf16((c >> 8) & 0xF));
                        WriteUtf16CharDirect(HexCharUtf16((c >> 4) & 0xF));
                        WriteUtf16CharDirect(HexCharUtf16(c & 0xF));
                    }
                    
                    lastWritten = i + 1;
                }
            }
            
            if (lastWritten < value.Length)
            {
                var segment = value.Slice(lastWritten);
                int byteCount = segment.Length * 2;
                ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16CharDirect(char c)
        {
            ref byte dst = ref GetCurrentSpanReference();
            Unsafe.WriteUnaligned(ref dst, c);
            Advance(2);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char HexCharUtf16(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }
        
        private void WriteStringContentSlow(ReadOnlySpan<char> value)
        {
            Span<byte> utf8Buf = stackalloc byte[4];
            Span<char> charBuf = stackalloc char[1];
            
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
                    charBuf[0] = c;
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

        #region Fast float/double serialization
        // 算法：验证后最短表示（round-trip verified shortest representation）
        //
        // 原理：float 最多 9 位有效十进制数字。从 1 位小数试到 9 位，
        //       每次做 round-trip 验证：(float)(scaled / scale) == value
        //       第一个通过的即为最短正确表示，直接输出。
        //
        // 正确性：对所有有限 float 保证 IEEE754 round-trip（反序列化后得到原值）。
        //         NaN/Infinity 自动 fallback 到 Ryu。
        //
        // 性能来源：
        //   1. 整数快速路径（约 40% 真实数据是整数）
        //   2. 多数值 dec=1..3 即通过，只需 1~3 次整数乘除
        //   3. 消除 Ryu 的 pow10 表查找和 128-bit 乘法
        //   4. stackalloc 临时 buffer 避免提前写入不确定数据

        // 幂次查找表（float 最多 dec=9，double 最多 dec=17）
        private static readonly double[] s_floatPow10d =
            { 1.0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9 };

        private static readonly long[] s_floatPow10L =
            { 1L, 10L, 100L, 1_000L, 10_000L, 100_000L, 1_000_000L, 10_000_000L, 100_000_000L, 1_000_000_000L };

        private static readonly double[] s_doublePow10d =
        {
            1e0,  1e1,  1e2,  1e3,  1e4,  1e5,  1e6,  1e7,  1e8,  1e9,
            1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17
        };

        private static readonly long[] s_doublePow10L =
        {
            1L, 10L, 100L, 1_000L, 10_000L, 100_000L,
            1_000_000L, 10_000_000L, 100_000_000L, 1_000_000_000L,
            10_000_000_000L, 100_000_000_000L, 1_000_000_000_000L,
            10_000_000_000_000L, 100_000_000_000_000L, 1_000_000_000_000_000L,
            10_000_000_000_000_000L, 100_000_000_000_000_000L
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUInt64Utf8(Span<byte> dest, ulong value)
        {
            if (value < 10) { dest[0] = (byte)('0' + value); return 1; }
            int digits = 0;
            ulong tmp = value;
            while (tmp != 0) { tmp /= 10; digits++; }
            int pos = digits;
            while (value != 0) { dest[--pos] = (byte)('0' + value % 10); value /= 10; }
            return digits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUInt64Utf16(Span<char> dest, ulong value)
        {
            if (value < 10) { dest[0] = (char)('0' + value); return 1; }
            int digits = 0;
            ulong tmp = value;
            while (tmp != 0) { tmp /= 10; digits++; }
            int pos = digits;
            while (value != 0) { dest[--pos] = (char)('0' + value % 10); value /= 10; }
            return digits;
        }

#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TryFloatToUtf8(float value, Span<byte> dest)
        {
            uint bits = Unsafe.As<float, uint>(ref value);
            
            if ((bits & 0x7F800000u) == 0x7F800000u) return 0;

            int pos = 0;
            
            if ((bits & 0x80000000u) != 0)
            {
                dest[pos++] = (byte)'-';
                value = -value;
                bits &= 0x7FFFFFFFu;
            }
            
            if (bits == 0) { dest[pos++] = (byte)'0'; return pos; }

            double d = value;
            
            if (d < 1e15 && value == MathF.Floor(value))
            {
                pos += WriteUInt64Utf8(dest.Slice(pos), (ulong)d);
                return pos;
            }
            
            for (int dec = 1; dec <= 9; dec++)
            {
                double scale   = s_floatPow10d[dec];
                double scaledD = d * scale + 0.5;
                
                if (scaledD >= 9.2e18) continue;

                long scaled = (long)scaledD;
                long scaleL = s_floatPow10L[dec];
                
                if ((float)((double)scaled / scale) != value) continue;

                ulong intPart = (ulong)(scaled / scaleL);
                int   fracRaw = (int)(scaled % scaleL); 

                pos += WriteUInt64Utf8(dest.Slice(pos), intPart);

                if (fracRaw > 0)
                {
                    dest[pos++] = (byte)'.';
                    
                    Span<byte> fracBuf = stackalloc byte[10];
                    int fr = fracRaw;
                    for (int i = dec - 1; i >= 0; i--)
                    {
                        fracBuf[i] = (byte)('0' + fr % 10);
                        fr /= 10;
                    }
                    int writeLen = dec;
                    while (writeLen > 0 && fracBuf[writeLen - 1] == (byte)'0') writeLen--;

                    fracBuf.Slice(0, writeLen).CopyTo(dest.Slice(pos));
                    pos += writeLen;
                }

                return pos;
            }

            return 0;
        }

#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TryFloatToUtf16(float value, Span<char> dest)
        {
            uint bits = Unsafe.As<float, uint>(ref value);

            if ((bits & 0x7F800000u) == 0x7F800000u) return 0;

            int pos = 0;

            if ((bits & 0x80000000u) != 0)
            {
                dest[pos++] = '-';
                value = -value;
                bits &= 0x7FFFFFFFu;
            }

            if (bits == 0) { dest[pos++] = '0'; return pos; }

            double d = value;

            if (d < 1e15 && value == MathF.Floor(value))
            {
                pos += WriteUInt64Utf16(dest.Slice(pos), (ulong)d);
                return pos;
            }

            for (int dec = 1; dec <= 9; dec++)
            {
                double scale   = s_floatPow10d[dec];
                double scaledD = d * scale + 0.5;
                if (scaledD >= 9.2e18) continue;

                long scaled = (long)scaledD;
                long scaleL = s_floatPow10L[dec];

                if ((float)((double)scaled / scale) != value) continue;

                ulong intPart = (ulong)(scaled / scaleL);
                int   fracRaw = (int)(scaled % scaleL);

                pos += WriteUInt64Utf16(dest.Slice(pos), intPart);

                if (fracRaw > 0)
                {
                    dest[pos++] = '.';
                    Span<char> fracBuf = stackalloc char[10];
                    int fr = fracRaw;
                    for (int i = dec - 1; i >= 0; i--)
                    {
                        fracBuf[i] = (char)('0' + fr % 10);
                        fr /= 10;
                    }
                    int writeLen = dec;
                    while (writeLen > 0 && fracBuf[writeLen - 1] == '0') writeLen--;

                    fracBuf.Slice(0, writeLen).CopyTo(dest.Slice(pos));
                    pos += writeLen;
                }

                return pos;
            }

            return 0;
        }

#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TryDoubleToUtf8(double value, Span<byte> dest)
        {
            ulong bits64 = Unsafe.As<double, ulong>(ref value);

            if ((bits64 & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL) return 0;

            int pos = 0;

            if ((bits64 & 0x8000_0000_0000_0000UL) != 0)
            {
                dest[pos++] = (byte)'-';
                value = -value;
                bits64 &= 0x7FFF_FFFF_FFFF_FFFFUL;
            }

            if (bits64 == 0) { dest[pos++] = (byte)'0'; return pos; }

            if (value < 1e15 && value == Math.Floor(value))
            {
                pos += WriteUInt64Utf8(dest.Slice(pos), (ulong)value);
                return pos;
            }

            for (int dec = 1; dec <= 17; dec++)
            {
                double scale   = s_doublePow10d[dec];
                double scaledD = value * scale + 0.5;
                if (scaledD >= 9.2e18) continue;

                long scaled = (long)scaledD;
                long scaleL = s_doublePow10L[dec];

                if ((double)scaled / scale != value) continue;

                ulong intPart = (ulong)(scaled / scaleL);
                long  fracRaw = scaled % scaleL;

                pos += WriteUInt64Utf8(dest.Slice(pos), intPart);

                if (fracRaw > 0)
                {
                    dest[pos++] = (byte)'.';
                    Span<byte> fracBuf = stackalloc byte[18];
                    long fr = fracRaw;
                    for (int i = dec - 1; i >= 0; i--)
                    {
                        fracBuf[i] = (byte)('0' + fr % 10);
                        fr /= 10;
                    }
                    int writeLen = dec;
                    while (writeLen > 0 && fracBuf[writeLen - 1] == (byte)'0') writeLen--;

                    fracBuf.Slice(0, writeLen).CopyTo(dest.Slice(pos));
                    pos += writeLen;
                }

                return pos;
            }

            return 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteFloat(float value)
        {
            WriteCommaIfNeeded();
            _writerBuffer.Check(ref this);
            if (SerializeStringAsUtf8)
            {
                Span<byte> tmp = stackalloc byte[24];
                int len = TryFloatToUtf8(value, tmp);
                if (len > 0)
                {
                    tmp.Slice(0, len).CopyTo(_bufferReference.Slice(_currentIndex));
                    _currentIndex += len;
                }
                else
                {
                    Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int fb, s_floatFormatG);
                    _currentIndex += fb;
                }
            }
            else
            {
                Span<char> tmp = stackalloc char[24];
                int len = TryFloatToUtf16(value, tmp);
                if (len > 0)
                {
                    MemoryMarshal.Cast<char, byte>(tmp.Slice(0, len))
                        .CopyTo(_bufferReference.Slice(_currentIndex, len * 2));
                    _currentIndex += len * 2;
                }
                else
                {
                    var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 64));
                    value.TryFormat(charSpan, out int fb, default, System.Globalization.NumberFormatInfo.InvariantInfo);
                    _currentIndex += fb * 2;
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteDouble(double value)
        {
            WriteCommaIfNeeded();
            _writerBuffer.Check(ref this);
            if (SerializeStringAsUtf8)
            {
                Span<byte> tmp = stackalloc byte[32];
                int len = TryDoubleToUtf8(value, tmp);
                if (len > 0)
                {
                    tmp.Slice(0, len).CopyTo(_bufferReference.Slice(_currentIndex));
                    _currentIndex += len;
                }
                else
                {
                    Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int fb, s_doubleFormatG);
                    _currentIndex += fb;
                }
            }
            else
            {
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 64));
                value.TryFormat(charSpan, out int fb, default, System.Globalization.NumberFormatInfo.InvariantInfo);
                _currentIndex += fb * 2;
            }
        }

        #endregion

        #region Primitive Types
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteInt(int value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 22));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteUInt(uint value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 20));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteByte(byte value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 6));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteSByte(sbyte value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 8));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteShort(short value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 12));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteUShort(ushort value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 10));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteLong(long value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 40));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteULong(ulong value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 40));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteDecimal(decimal value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                _writerBuffer.Check(ref this);
                Utf8Formatter.TryFormat(value, _bufferReference.Slice(_currentIndex), out int written);
                _currentIndex += written;
            }
            else
            {
                _writerBuffer.Check(ref this);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 62));
                value.TryFormat(charSpan, out int written, default, System.Globalization.NumberFormatInfo.InvariantInfo);
                _currentIndex += written << 1;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16Chars(scoped ReadOnlySpan<char> chars)
        {
            int byteCount = chars.Length * 2;
            ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(chars));
            ref byte dst = ref GetCurrentSpanReference();
            Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
            Advance(byteCount);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteChar(char value)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);
            
            if (value == '"')
            {
                WriteByteRaw(Backslash);
                if (SerializeStringAsUtf8)
                    WriteByteRaw((byte)'"');
                else
                    WriteUtf16CharDirect('"');
            }
            else if (value == '\\')
            {
                WriteByteRaw(Backslash);
                if (SerializeStringAsUtf8)
                    WriteByteRaw((byte)'\\');
                else
                    WriteUtf16CharDirect('\\');
            }
            else if (SerializeStringAsUtf8)
            {
                if (value >= 0x20 && value <= 0x7F)
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
            }
            else
            {
                WriteUtf16CharDirect(value);
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