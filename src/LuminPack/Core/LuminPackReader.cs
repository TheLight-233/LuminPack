using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Numerics;
using System.Text.Unicode;
#endif

using LuminPack.Interface;
using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Parsers;
using LuminPack.Utility;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref partial struct LuminPackReader
    {
        
        private Span<byte> _bufferReference;
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif
        private int _currentIndex;

        private readonly LuminPackReaderOptionalState _optionState;

        private readonly bool SerializeStringAsUtf8;
        private readonly bool SerializeStringRecordAsToken;

        public LuminPackReaderOptionalState OptionState => _optionState;
        public LuminPackSerializerOption Option => _optionState.Option;

        public LuminPackReader(LuminPackReaderOptionalState? option = null)
        {

            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = default;
            _currentIndex = 0;
            
            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }

        public LuminPackReader(ref Span<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = bufferReference;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;

            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackReader(ref ReadOnlySpan<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = LuminPackMarshal.CreateSpan(ref GetNonNullPinnableReference(bufferReference), bufferReference.Length);
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;

            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackReader(ref ReadOnlySequence<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            var span = bufferReference.FirstSpan;
            _bufferReference = LuminPackMarshal.CreateSpan(ref GetNonNullPinnableReference(span), span.Length);
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;

            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }

        
        /// <summary>
        /// 获取Span数组指针
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetSpanReference(int index) =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
        
        /// <summary>
        /// 移动指针
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
#if DEBUG
            if (_bufferReference.Length < count)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
#endif
            
            _currentIndex += count;
            
        }

        /// <summary>
        /// 设置Span
        /// </summary>
        /// <param name="span"></param>
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

        /// <summary>
        /// 刷新Writer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _bufferReference = null;
            _bufferStart = default;
            _currentIndex = 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackParser<T> GetParser<T>()
        {
            return LuminPackParseProvider.Cache<T>.Parser!;
        }

        /// <summary>
        /// 获取当前数组指针
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference() =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

        /// <summary>
        /// 获取Reader持有的Span
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _bufferReference;
        }
        
        /// <summary>
        /// 获取当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref int GetCurrentSpanOffset()
        {
            //int* ptr = (int*)Unsafe.AsPointer(ref _currentIndex);
            
            return ref _currentIndex;
        }
        
        /// <summary>
        /// 获取当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCurrentSpanIndex()
        {
            return _currentIndex;
        }
        
        /// <summary>
        /// 刷新当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushCurrentIndex(int offset)
        {
            _currentIndex = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsReferenceOrContainsReferences<T>()
        {
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> CreateSpan<T>(scoped ref T reference, int length)
        {
            return LuminPackMarshal.CreateSpan(ref reference, length);
        }

        /// <summary>
        /// 反序列化集合长度Head
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCollectionHead(ref int index, out int length)
        {
#if NET8_0_OR_GREATER
            length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index));
#else
            length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index));
#endif

            return length is not LuminPackCode.NullCollection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullObject(ref int index)
        {
#if NET8_0_OR_GREATER
            return Unsafe.Add(ref _bufferStart, (nint)(uint)index) == LuminPackCode.NullObject;
#else
            return Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) == LuminPackCode.NullObject;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullCollection(ref int index)
        {
#if NET8_0_OR_GREATER
            return Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index)) == LuminPackCode.NullCollection;
#else
            return Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index)) == LuminPackCode.NullCollection;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullString(ref int index)
        {
            return SerializeStringRecordAsToken 
#if NET8_0_OR_GREATER
                ? Unsafe.Add(ref _bufferStart, (nint)(uint)index) == 0 
                : Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index)) == LuminPackCode.NullCollection;
#else
                ? Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) == 0 
                : Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index)) == LuminPackCode.NullCollection;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead(ref int index)
        {
#if NET8_0_OR_GREATER
            var code = Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            var code = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            return code is not LuminPackCode.NullObject;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead(ref int index, out byte code)
        {
#if NET8_0_OR_GREATER
            code = Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            code = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            return code is not LuminPackCode.NullObject;
        }

        /// <summary>
        /// 反序列化字符串长度(Length模式)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadStringHead(ref int index, out int length)
        {
            if (SerializeStringRecordAsToken)
                LuminPackExceptionHelper.ThrowFailedParseStringWithLength();

#if NET8_0_OR_GREATER
            length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index));
#else
            length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index));
#endif

            if (length > _bufferReference.Length)
                LuminPackExceptionHelper.ThrowInSufficientBuffer(length);

            return length is not LuminPackCode.NullCollection;
        }

        /// <summary>
        /// 获取二进制字符串编码长度
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadStringLength(ref int index, out int length)
        {
            if (SerializeStringRecordAsToken)
            {
#if NET8_0_OR_GREATER
                ref byte start = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
                ref byte start = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

                if (!SerializeStringAsUtf8)
                {
                    // UTF-16 Token 模式：扫描第一个 0x0000（ushort 零值）
                    // 不能按单字节扫，UTF-16 LE 的 ASCII 高字节全是 0x00 会误判
#if NET8_0_OR_GREATER
                    if (Vector512.IsHardwareAccelerated)
                    {
                        ref ushort cStart = ref Unsafe.As<byte, ushort>(ref start);
                        ref ushort current = ref cStart;
                        var zero = Vector512<ushort>.Zero;
                        while (true)
                        {
                            ulong mask = Vector512.Equals(Vector512.LoadUnsafe(ref current), zero)
                                            .ExtractMostSignificantBits();
                            if (mask != 0)
                            {
                                int position = BitOperations.TrailingZeroCount(mask);
                                length = (int)(nint)Unsafe.ByteOffset(
                                    ref Unsafe.As<ushort, byte>(ref cStart),
                                    ref Unsafe.As<ushort, byte>(ref current)) + position * 2;
                                return;
                            }
                            current = ref Unsafe.Add(ref current, Vector512<ushort>.Count);
                        }
                    }
                    else if (Vector256.IsHardwareAccelerated)
                    {
                        ref ushort cStart = ref Unsafe.As<byte, ushort>(ref start);
                        ref ushort current = ref cStart;
                        var zero = Vector256<ushort>.Zero;
                        while (true)
                        {
                            uint mask = Vector256.Equals(Vector256.LoadUnsafe(ref current), zero)
                                            .ExtractMostSignificantBits();
                            if (mask != 0)
                            {
                                int position = BitOperations.TrailingZeroCount(mask);
                                length = (int)(nint)Unsafe.ByteOffset(
                                    ref Unsafe.As<ushort, byte>(ref cStart),
                                    ref Unsafe.As<ushort, byte>(ref current)) + position * 2;
                                return;
                            }
                            current = ref Unsafe.Add(ref current, Vector256<ushort>.Count);
                        }
                    }
                    else if (Vector128.IsHardwareAccelerated)
                    {
                        ref ushort cStart = ref Unsafe.As<byte, ushort>(ref start);
                        ref ushort current = ref cStart;
                        var zero = Vector128<ushort>.Zero;
                        while (true)
                        {
                            uint mask = Vector128.Equals(Vector128.LoadUnsafe(ref current), zero)
                                            .ExtractMostSignificantBits();
                            if (mask != 0)
                            {
                                int position = BitOperations.TrailingZeroCount(mask);
                                length = (int)(nint)Unsafe.ByteOffset(
                                    ref Unsafe.As<ushort, byte>(ref cStart),
                                    ref Unsafe.As<ushort, byte>(ref current)) + position * 2;
                                return;
                            }
                            current = ref Unsafe.Add(ref current, Vector128<ushort>.Count);
                        }
                    }
                    else
#endif
                    {
                        ref char cStart = ref Unsafe.As<byte, char>(ref start);
                        int charLen = 0;
                        while (Unsafe.Add(ref cStart, charLen) != '\0') charLen++;
                        length = charLen * 2;
                    }
                    return;
                }

#if NET8_0_OR_GREATER
                if (Vector512.IsHardwareAccelerated && Vector512<byte>.Count > 1)
                {
                    ref byte current = ref start;
                    Vector512<byte> zero = Vector512<byte>.Zero;
            
                    while (true)
                    {
                        Vector512<byte> data = Vector512.LoadUnsafe(ref current);
                        Vector512<byte> equalsZero = Vector512.Equals(data, zero);
                        ulong mask = equalsZero.ExtractMostSignificantBits();
                
                        if (mask != 0)
                        {
                            int position = BitOperations.TrailingZeroCount(mask);
                            length = (int)((nint)Unsafe.ByteOffset(ref start, ref current) + position);
                            return;
                        }
                
                        current = ref Unsafe.Add(ref current, Vector512<byte>.Count);
                    }
                }
                else if (Vector256.IsHardwareAccelerated && Vector256<byte>.Count > 1)
                {
                    ref byte current = ref start;
                    Vector256<byte> zero = Vector256<byte>.Zero;
            
                    while (true)
                    {
                        Vector256<byte> data = Vector256.LoadUnsafe(ref current);
                        Vector256<byte> equalsZero = Vector256.Equals(data, zero);
                        uint mask = equalsZero.ExtractMostSignificantBits();
                
                        if (mask != 0)
                        {
                            int position = BitOperations.TrailingZeroCount(mask);
                            length = (int)((nint)Unsafe.ByteOffset(ref start, ref current) + position);
                            return;
                        }
                
                        current = ref Unsafe.Add(ref current, Vector256<byte>.Count);
                    }
                }
                else if (Vector128.IsHardwareAccelerated && Vector128<byte>.Count > 1)
                {
                    ref byte current = ref start;
                    Vector128<byte> zero = Vector128<byte>.Zero;
            
                    while (true)
                    {
                        Vector128<byte> data = Vector128.LoadUnsafe(ref current);
                        Vector128<byte> equalsZero = Vector128.Equals(data, zero);
                        uint mask = equalsZero.ExtractMostSignificantBits();
                
                        if (mask != 0)
                        {
                            int position = BitOperations.TrailingZeroCount(mask);
                            length = (int)((nint)Unsafe.ByteOffset(ref start, ref current) + position);
                            return;
                        }
                
                        current = ref Unsafe.Add(ref current, Vector128<byte>.Count);
                    }
                }
                else
#endif
                {
                    ref byte current = ref start;
                    int len = 0;
                    while (current != 0)
                    {
                        current = ref Unsafe.Add(ref current, 1);
                        len++;
                    }
                    length = len;
                }
            }
            else
            {
#if NET8_0_OR_GREATER
                length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index));
#else
                length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index));
#endif
            }

        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength()
        {
            // UTF-16 Token 模式用 0x0000 (2字节) 作为终止符，因为 UTF-16 内部含大量单字节 0x00
            return SerializeStringRecordAsToken ? (SerializeStringAsUtf8 ? 1 : 2) : SerializeStringAsUtf8 ? 8 : 4;
        }

        /// <summary>
        /// 反序列化字符串
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString()
        {
            if (SerializeStringAsUtf8)
                return SerializeStringRecordAsToken
                    ? ReadUtf8StringWithToken(_currentIndex)
                    : ReadUtf8StringWithLength(_currentIndex);
            return SerializeStringRecordAsToken
                ? ReadUtf16StringWithToken(_currentIndex)
                : ReadUtf16StringWithLength(_currentIndex);
        }
        
        /// <summary>
        /// 反序列化字符串(已知长度)
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(int length)
        {
            if (SerializeStringAsUtf8)
                return SerializeStringRecordAsToken
                    ? ReadUtf8StringWithToken(_currentIndex, length)
                    : ReadUtf8StringWithLength(_currentIndex, length);
            return SerializeStringRecordAsToken
                ? ReadUtf16StringWithToken(_currentIndex, length)
                : ReadUtf16StringWithLength(_currentIndex, length);
        }

        /// <summary>
        /// 反序列化字符串
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(ref int index)
        {
            if (SerializeStringAsUtf8)
                return SerializeStringRecordAsToken
                    ? ReadUtf8StringWithToken(index)
                    : ReadUtf8StringWithLength(index);
            return SerializeStringRecordAsToken
                ? ReadUtf16StringWithToken(index)
                : ReadUtf16StringWithLength(index);
        }

        /// <summary>
        /// 反序列化字符串(已知长度)
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(int index, int length)
        {
            if (SerializeStringAsUtf8)
                return SerializeStringRecordAsToken
                    ? ReadUtf8StringWithToken(index, length)
                    : ReadUtf8StringWithLength(index, length);
            return SerializeStringRecordAsToken
                ? ReadUtf16StringWithToken(index, length)
                : ReadUtf16StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithToken(int index)
        {
            ReadStringLength(ref index, out var length);
            
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            return Encoding.UTF8.GetString(CreateSpan(ref spanRef, length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithToken(int index, int length)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            return Encoding.UTF8.GetString(CreateSpan(ref spanRef, length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithToken(int index)
        {
            ReadStringLength(ref index, out var length);

#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            return Encoding.Unicode.GetString(CreateSpan(ref spanRef, length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithToken(int index, int length)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            return Encoding.Unicode.GetString(CreateSpan(ref spanRef, length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithLength(int index)
        {
            ReadStringLength(ref index, out var length);

            return ReadUtf8StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithLength(int index, int length)
        {
            if (length is 0) return null;

            int index1 = index + 4;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index1);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index1);
#endif
            
            var utf16Length = Unsafe.ReadUnaligned<int>(ref spanRef);
            
            if (utf16Length <= 0)
            {
                var src = LuminPackMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref spanRef, 4), length);
                return Encoding.UTF8.GetString(src);
            }
            else
            {

                // regular path, know decoded UTF16 length will gets faster decode result
                unsafe
                {
                    fixed (byte* p = &Unsafe.Add(ref spanRef, 4))
                    {
                        
                        return string.Create(utf16Length, ((IntPtr)p, length), static (dest, state) =>
                        {
                            var src = LuminPackMarshal.CreateSpan(ref Unsafe.AsRef<byte>((byte*)state.Item1), state.Item2);
                            var status = StringSerializer.Deserialize(src, dest, out var bytesRead, out var charsWritten,
                                replaceInvalidSequences: false);
                            if (status != OperationStatus.Done)
                            {
                                LuminPackExceptionHelper.ThrowFailedEncoding(status);
                            }
                        });
                    }
                }
            }

        }
    

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithLength(int index)
        {
            ReadStringLength(ref index, out var length);

            return ReadUtf16StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithLength(int index, int length)
        {
            if (length is 0) return null;

            int index1 = index + 4;
            
            var utf16Length = length >> 1;
            
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index1);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index1);
#endif
            
            fixed (byte* p = &spanRef)
            {
                return string.Create(utf16Length, ((IntPtr)p, length), static (dest, state) =>
                {
                    var src = LuminPackMarshal.CreateSpan(ref Unsafe.AsRef<byte>((byte*)state.Item1), state.Item2);
                    var status = StringSerializer.Deserialize(src, dest, out var bytesRead, out var charsWritten,
                        replaceInvalidSequences: false, mode : SerializeMode.Utf16);
                    if (status != OperationStatus.Done)
                    {
                        LuminPackExceptionHelper.ThrowFailedEncoding(status);
                    }
                });
            }
        }
        
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public void ReadValue<T>(scoped ref T? value)
        // {
        //     LuminPackParseProvider.Cache<T>.Parser!.Deserialize(ref this, ref value);
        // }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? ReadValue<T>()
        {
            T? value = default;
            LuminPackParseProvider.Cache<T>.Parser!.Deserialize(ref this, ref value);
            return value;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueWithParser<TParser, T>(TParser parser, scoped ref T? value)
            where TParser : ILuminPackableParser<T>
        {
            parser.Deserialize(ref this, ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? ReadValueWithParser<TParser, T>(TParser parser)
            where TParser : ILuminPackableParser<T>
        {
            T? value = default;
            parser.Deserialize(ref this, ref value);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadObjectHeader(ref int index, out byte memberCount)
        {
            memberCount = _bufferReference[index];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekUnionHeader(out ushort tag)
        {
#if NET8_0_OR_GREATER
            tag = Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            tag = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            Advance(1);
            return tag is not LuminPackCode.NullObject;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekUnionHeader(ref int index, out ushort tag)
        {
#if NET8_0_OR_GREATER
            tag = Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            tag = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            index += 1;
            return tag is not LuminPackCode.NullObject;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekWideUnionHeader(out ushort tag)
        {
#if NET8_0_OR_GREATER
            ref var firstTag = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var firstTag = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            if (firstTag < LuminPackCode.WideTag)
            {
                tag = firstTag;
                Advance(1);
                return true;
            }
            else if (firstTag == LuminPackCode.WideTag)
            {
                tag = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref firstTag, 1));
                Advance(3);
                return true;
            }
            else
            {
                tag = 0;
                Advance(1);
                return false;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekWideUnionHeader(ref int index, out ushort tag)
        {
#if NET8_0_OR_GREATER
            ref var firstTag = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var firstTag = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            if (firstTag < LuminPackCode.WideTag)
            {
                tag = firstTag;
                index += 1;
                return true;
            }
            else if (firstTag == LuminPackCode.WideTag)
            {
                tag = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref firstTag, 1));
                index += 3;
                return true;
            }
            else
            {
                tag = 0;
                index += 1;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T?[]? ReadArray<T>()
        {
            T?[]? value = default;
            ReadArray(ref value);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<T>(scoped ref T[]? array)
        {
            var index = _currentIndex;

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedArray(ref index, ref array!, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                
                Advance(4);
                
                return;
            }

            Advance(4);
            
            if (length is 0)
            {
                array = Array.Empty<T>();
                
                return;
            }
            
            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            ref T first = ref LuminPackMarshal.GetArrayReference(array);
            for (nint i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref Unsafe.Add(ref first, i)!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<T>(scoped ref int index, ref T[]? array)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedArray(ref index, ref array!, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                
                Advance(4);
                
                return;
            }

            Advance(4);
            
            if (length is 0)
            {
                array = Array.Empty<T>();
                
                return;
            }
            
            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            ref T first = ref LuminPackMarshal.GetArrayReference(array);
            for (nint i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref Unsafe.Add(ref first, i)!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadSpan<T>(scoped ref Span<T?> span)
        {
            var index = _currentIndex;

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedSpan(ref index, ref span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = null;
                
                Advance(4);
                
                return;
            }

            Advance(4);
            
            if (length is 0)
            {
                span = Array.Empty<T>();
                
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var parser = LuminPackParseProvider.Cache<T>.Parser!;


            ref var first = ref MemoryMarshal.GetReference(span);
            for (nint i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref Unsafe.Add(ref first, i)!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadSpan<T>(scoped ref int index, scoped ref Span<T?> span)
        {

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedSpan(ref index, ref span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = null;
                
                Advance(4);
                
                return;
            }
            
            Advance(4);

            if (length is 0)
            {
                span = Array.Empty<T>();
                
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            

            ref var first = ref MemoryMarshal.GetReference(span);
            for (nint i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref Unsafe.Add(ref first, i)!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<T>(scoped ref int index, int length, ref T[]? array)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedArray(ref index, ref array!, length, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                
                return;
            }
            
            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            ref T first = ref LuminPackMarshal.GetArrayReference(array);
            for (nint i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref Unsafe.Add(ref first, i)!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadSpan<T>(scoped ref int index, int length, scoped ref Span<T?> span)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedSpan(ref index, ref span, length, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (length is 0)
            {
                span = Array.Empty<T>();
                
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            ref var first = ref MemoryMarshal.GetReference(span);
            for (nint i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref Unsafe.Add(ref first, i)!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref T[]? array)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref _currentIndex, ref array!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, length, out var offset);
            Advance(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, length, out var offset);
            Advance(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, length, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, length, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref T[]? array)
        {

            var index = _currentIndex;

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref Span<T> span)
        {
            var index = _currentIndex;
            
            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = default;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = default;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, out int spanOffset)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                spanOffset = 4;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                spanOffset = 4;
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

            var srcLength = length * Unsafe.SizeOf<T>();
            
#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)srcLength);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)srcLength);
#endif

            spanOffset = srcLength + 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, out int spanOffset)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = default;
                spanOffset = 4;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                spanOffset = 4;
                return;
            }

            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }
            
            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());
            
            var srcLength = length * Unsafe.SizeOf<T>();

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)srcLength);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)srcLength);
#endif

            spanOffset = srcLength + 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length)
        {
            if (length is LuminPackCode.NullCollection)
            {
                array = null;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

            var offset = length * Unsafe.SizeOf<T>();
            
#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length)
        {

            if (length is LuminPackCode.NullCollection)
            {
                span = default;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }
            
            var offset = length * Unsafe.SizeOf<T>();
            
            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length, out int spanOffset)
        {
            if (length is LuminPackCode.NullCollection)
            {
                array = null;
                spanOffset = 0;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                spanOffset = 0;
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);
            
            var offset = length * Unsafe.SizeOf<T>();

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif

            spanOffset = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length,
            out int spanOffset)
        {
            if (length is LuminPackCode.NullCollection)
            {
                span = default;
                spanOffset = 0;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                spanOffset = 0;
                return;
            }

            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }
            
            ref var buffer = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

            var offset = length * Unsafe.SizeOf<T>();
            
#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref buffer, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref buffer, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif

            spanOffset = offset;
        }
        
        public byte ReadVarIntByte()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((byte)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return checked((byte)ReadUnmanaged<byte>());
                case VarIntCodes.Int16:
                    return checked((byte)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return checked((byte)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((byte)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((byte)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((byte)ReadUnmanaged<long>());
                default:
                    return checked((byte)typeCode);
            }
        }

        public sbyte ReadVarIntSByte()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return checked((sbyte)ReadUnmanaged<byte>());
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return checked((sbyte)ReadUnmanaged<ushort>());
                case VarIntCodes.Int16:
                    return checked((sbyte)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return checked((sbyte)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((sbyte)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((sbyte)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((sbyte)ReadUnmanaged<long>());
                default:
                    return typeCode;
            }
        }

        public ushort ReadVarIntUInt16()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((ushort)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return checked((ushort)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return checked((ushort)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((ushort)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((ushort)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((ushort)ReadUnmanaged<long>());
                default:
                    return checked((ushort)typeCode);
            }
        }

        public short ReadVarIntInt16()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return checked((short)ReadUnmanaged<ushort>());
                case VarIntCodes.Int16:
                    return ReadUnmanaged<short>();
                case VarIntCodes.UInt32:
                    return checked((short)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((short)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((short)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((short)ReadUnmanaged<long>());
                default:
                    return typeCode;
            }
        }

        public uint ReadVarIntUInt32()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((uint)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return checked((uint)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return ReadUnmanaged<uint>();
                case VarIntCodes.Int32:
                    return checked((uint)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((uint)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((uint)ReadUnmanaged<long>());
                default:
                    return checked((uint)typeCode);
            }
        }

        public int ReadVarIntInt32()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));
            
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return ReadUnmanaged<short>();
                case VarIntCodes.UInt32:
                    return checked((int)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return ReadUnmanaged<int>();
                case VarIntCodes.UInt64:
                    return checked((int)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((int)ReadUnmanaged<long>());
                default:
                    return typeCode;
            }
        }

        public ulong ReadVarIntUInt64()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((ulong)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return checked((ulong)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return ReadUnmanaged<uint>();
                case VarIntCodes.Int32:
                    return checked((ulong)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return ReadUnmanaged<ulong>();
                case VarIntCodes.Int64:
                    return checked((ulong)ReadUnmanaged<long>());
                default:
                    return checked((ulong)typeCode);
            }
        }

        public long ReadVarIntInt64()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return ReadUnmanaged<short>();
                case VarIntCodes.UInt32:
                    return ReadUnmanaged<uint>();
                case VarIntCodes.Int32:
                    return ReadUnmanaged<int>();
                case VarIntCodes.UInt64:
                    return checked((long)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return ReadUnmanaged<long>();
                default:
                    return typeCode;
            }
        }

        #region Delegate

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Delegate ReadAction(Type type, object? instance, MethodInfo methodInfo)
        {
            return Delegate.CreateDelegate(type, instance, methodInfo);
        }

        #endregion
        
    }
}