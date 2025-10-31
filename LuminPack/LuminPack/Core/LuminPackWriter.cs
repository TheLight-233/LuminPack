using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
#if NET7_0_OR_GREATER
using System.Text.Unicode;
#endif
using LuminPack.Interface;
using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Parsers;
using LuminPack.Utility;
using LuminPack.Utility.VIewModel;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Core
{
    [StructLayout(LayoutKind.Auto)]
    public unsafe ref struct LuminPackWriter
    {
        
        private Span<byte> _bufferReference;
        internal IntPtr _bufferStart;
        private int _currentIndex;
        private int _threshold; // _bufferReference.Length * 9 / 8

        private IBufferWriter<byte>? _writerBuffer = null;
        
        private readonly LuminPackWriterOptionalState _optionState;

        private readonly bool SerializeStringAsUtf8;
        private readonly bool SerializeStringRecordAsToken;
        
        public LuminPackWriterOptionalState OptionState => _optionState;
        public LuminPackSerializerOption Option => _optionState.Option;
        
        public int CurrentIndex => _currentIndex;

        public LuminPackWriter(LuminPackWriterOptionalState? option = null)
        {
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            _bufferReference = default;
            _bufferStart = new IntPtr(Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference)));
            _currentIndex = 0;
            _threshold = (int)(_bufferReference.Length * 0.9f); // 90% 阈值
            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {
            
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            _bufferReference = bufferReference;
            _bufferStart = new IntPtr(Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference)));
            _currentIndex = 0;
            _threshold = (int)(_bufferReference.Length * 0.9f); // 90% 阈值
            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackWriter(ref ReadOnlySpan<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {
            
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            var ptr = bufferReference.GetPinnableReference();
            _bufferReference = CreateSpan(ref ptr, bufferReference.Length);
            _bufferStart = new IntPtr(Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference)));
            _currentIndex = 0;
            _threshold = (int)(_bufferReference.Length * 0.9f); // 90% 阈值
            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackWriter(IBufferWriter<byte>? bufferWriter, LuminPackWriterOptionalState? option = null)
        {
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            (bufferWriter as ReusableLinkedArrayBufferWriter)?.SetCurrentIndexPtr(ref _currentIndex);
            _bufferReference = bufferWriter.GetSpan();
            _bufferStart = new IntPtr(Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference)));
            _writerBuffer = bufferWriter;
            _currentIndex = 0;
            _threshold = (int)(_bufferReference.Length * 0.9f); // 90% 阈值
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
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);

        /// <summary>
        /// 移动指针
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            
            _writerBuffer!.Advance(_currentIndex);
            
            if (_currentIndex > _threshold)
            {
                if (_writerBuffer is not null)
                {
                    _bufferReference = _writerBuffer.GetSpan();
                    _bufferStart = new IntPtr(Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference)));
                    _threshold = (int)(_bufferReference.Length * 0.9f); // 90% 阈值
                }
            }
#if DEBUG
             if (_bufferReference.Length < _currentIndex)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
#endif
            
            _currentIndex += count;
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWriteBuffer(IBufferWriter<byte>? bufferWriter)
        {
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            
            _writerBuffer = bufferWriter;
        }

        /// <summary>
        /// 设置Span
        /// </summary>
        /// <param name="span"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            _bufferReference = span;
            _bufferStart = new IntPtr(Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference)));
        }

        /// <summary>
        /// 刷新Writer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _bufferReference = null;
            _bufferStart = IntPtr.Zero;
            _currentIndex = 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackParser<T> GetParser<T>()
        {
            return LuminPackParseProvider.Cache<T>.Parser!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsReferenceOrContainsReferences<T>()
        {
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> CreateSpan<T>(scoped ref T reference, int length)
        {
            return MemoryMarshal.CreateSpan(ref reference, length);
        }

        /// <summary>
        /// 获取当前数组指针
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference() => 
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)_currentIndex);

        /// <summary>
        /// 获取Writer持有的Span
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _bufferReference[.._currentIndex];
        }

        /// <summary>
        /// 获取当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref int GetCurrentSpanOffset() => ref _currentIndex;
        
        /// <summary>
        /// 获取当前SpanOffsetEvaluator
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref LuminPackEvaluator GetCurrentSpanEvaluator()
        {
            var eval = new LuminPackEvaluator(ref _currentIndex);
            return ref eval;
        }
        
        /// <summary>
        /// 获取当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCurrentSpanIndex() => _currentIndex;

        /// <summary>
        /// 边界检测
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushCurrentIndex(int offset)
        {
            _writerBuffer!.Advance(offset);

            _currentIndex = offset;
        }

        /// <summary>
        /// 获取字符串长度
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStringLength(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            
            return SerializeStringAsUtf8 ? LuminPackMarshal.CalculateStringByteCount(value) : checked(value.Length * 2);
        }

        /// <summary>
        /// 获取字符串字节数，包括标记符
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStringWriteLength(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return SerializeStringRecordAsToken ? 1 : 8;
            }

            return (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
            {
                (true, true) => LuminPackMarshal.CalculateStringByteCount(value) + 1,
                (true, false) => LuminPackMarshal.CalculateStringByteCount(value) + 8,
                (false, true) => checked(value.Length * 2) + 1,
                (false, false) => checked(value.Length * 2) + 4
            };
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength()
        {
            return SerializeStringRecordAsToken ? 1 : SerializeStringAsUtf8 ? 8 : 4;
        }
        
        /// <summary>
        /// 获取值类型数组字节数
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetUnManageArrayWriteLength<T>(T[]? value)
            where T : unmanaged
        {
            if (value == null || value.Length == 0)
            {
                return 4;
            }

            return (Unsafe.SizeOf<T>() * value.Length) + 4;
        }

        #region Write

        /// <summary>
        /// 序列化空Object字节
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader(ref int index)
        {
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index) = LuminPackCode.NullObject;
        }
        
        /// <summary>
        /// 序列化空Object字节
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader()
        {
            _bufferReference[_currentIndex] = LuminPackCode.NullObject;
            Advance(1);
        }

        /// <summary>
        /// 序列化集合字节
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length">集合长度</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCollectionHeader(ref int index, int length)
        {
            Unsafe.WriteUnaligned(ref _bufferReference[index], length);
        }
        
        /// <summary>
        /// 序列化空集合字节
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullCollectionHeader(ref int index)
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index), LuminPackCode.NullCollection);
        }

        /// <summary>
        /// 序列化空字符串
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullStringHeader(ref int index)
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index), 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullStringHeader(ref int index, out int offset)
        {
            if (SerializeStringRecordAsToken)
            {
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index), 0);

                offset = 1;
            }
            else
            {
                WriteStringRecordLengthHeader(ref _currentIndex, 0);

                offset = 4;
            }
        }

        /// <summary>
        /// 序列化字符串终止符
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringRecordTokenHeader(ref int index)
        {
            _bufferReference[index] = 0;
        }
        
        /// <summary>
        /// 序列化字符串字节数
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringRecordLengthHeader(ref int index, int length)
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectHeader(ref int index, byte memberCount)
        {
            _bufferReference[index] = memberCount;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader(ref int index, byte memberCount)
        {
            _bufferReference[index] = LuminPackCode.NullObject;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnionHeader(ushort tag)
        {
            if (tag < LuminPackCode.WideTag)
            {
                Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)_currentIndex) = (byte)tag;
                Advance(1);
            }
            else
            {
                ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)_currentIndex);
                Unsafe.WriteUnaligned(ref spanRef, LuminPackCode.WideTag);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, 1), tag);
                Advance(3);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnionHeader(ref int index, ushort tag)
        {
            if (tag < LuminPackCode.WideTag)
            {
                Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index) = (byte)tag;
                Advance(1);
            }
            else
            {
                ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
                Unsafe.WriteUnaligned(ref spanRef, LuminPackCode.WideTag);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, 1), tag);
                Advance(3);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullUnionHeader(ref int index)
        {
            WriteNullObjectHeader(ref index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectReferenceId(ref int index, uint referenceId)
        {
            Unsafe.Add(ref Unsafe.AsRef<byte>(this._bufferStart.ToPointer()), (nint)index) = (byte) 250;
            Advance(1);
            this.WriteVarInt(referenceId);
            Advance(LuminPackEvaluator.CalculateVarInt(referenceId));
        }

        /// <summary>
        /// 序列化字符串
        /// </summary>
        /// <param name="value"></param>
        /// <returns>偏移量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteString(string? value) => (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => WriteUtf8WithToken(_currentIndex, value),
            (true, false) => WriteUtf8WithLength(_currentIndex, value),
            (false, true) => WriteUtf16WithToken(_currentIndex, value),
            (false, false) => WriteUtf16WithLength(_currentIndex, value),
        };
        
        /// <summary>
        /// 序列化字符串(已知长度)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="length"></param>
        /// <returns>偏移量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string? value, int length)
        {
            switch (SerializeStringAsUtf8, SerializeStringRecordAsToken)
            {
                case (true, true) : WriteUtf8WithToken(_currentIndex, value, length); break;
                case (true, false) : WriteUtf8WithLength(_currentIndex, value, length); break;
                case (false, true) : WriteUtf16WithToken(_currentIndex, value, length); break;
                case (false, false) : WriteUtf16WithLength(_currentIndex, value, length); break;
            }
        }
        
        /// <summary>
        /// 序列化字符串
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <returns>偏移量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteString(ref int index, string? value) => (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => WriteUtf8WithToken(index, value),
            (true, false) => WriteUtf8WithLength(index, value),
            (false, true) => WriteUtf16WithToken(index, value),
            (false, false) => WriteUtf16WithLength(index, value),
        };
        
        /// <summary>
        /// 序列化字符串(已知长度)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <param name="length"></param>
        /// <returns>偏移量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(ref int index, string? value, int length)
        {
            switch (SerializeStringAsUtf8, SerializeStringRecordAsToken)
            {
                case (true, true) : WriteUtf8WithToken(index, value, length); break;
                case (true, false) : WriteUtf8WithLength(index, value, length); break;
                case (false, true) : WriteUtf16WithToken(index, value, length); break;
                case (false, false) : WriteUtf16WithLength(index, value, length); break;
            }
        }

        /// <summary>
        /// 序列化字符串 (ReadOnlySpan)
        /// </summary>
        /// /// <param name="index"></param>
        /// <param name="value"></param>
        /// <returns>偏移量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteString(ref int index, ReadOnlySpan<char> value) => (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => WriteUtf8WithToken(index, value),
            (true, false) => WriteUtf8WithLength(index, value),
            (false, true) => WriteUtf16WithToken(index, value),
            (false, false) => WriteUtf16WithLength(index, value),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf8WithToken(int index, string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }
            
            var status = StringSerializer.Serialize(value, _bufferReference.Slice(index), out var _, out var bytesWritten, replaceInvalidSequences: false);
            
            if (status != OperationStatus.Done)
            {
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
            
            var tokenIndex = index + bytesWritten;
            WriteStringRecordTokenHeader(ref tokenIndex);
            
            return bytesWritten;
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8WithToken(int index, string? value, int length)
        {
            if (length is 0)
            {
                WriteStringRecordTokenHeader(ref index);
                return;
            }
            var status = StringSerializer.Serialize(value.AsSpan(), _bufferReference.Slice(index, length), out _, out var bytesWritten, replaceInvalidSequences: false);
            
            if (status != OperationStatus.Done)
            {
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
            
            var tokenIndex = index + length;
            WriteStringRecordTokenHeader(ref tokenIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf8WithToken(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }

            if (!MemoryMarshal.AsBytes(value).TryCopyTo(_bufferReference.Slice(index)))
            {
                LuminPackExceptionHelper.ThrowFailedEncodingUtf8();
            }
            
            var tokenIndex = index + checked(value.Length * sizeof(char));
            WriteStringRecordTokenHeader(ref tokenIndex);
            
            return checked(value.Length * sizeof(char));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf8WithLength(int index, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteStringRecordLengthHeader(ref index, LuminPackCode.NullCollection);
                return 0;
            }
            
            var source = value.AsSpan();
            
            // UTF8.GetMaxByteCount -> (length + 1) * 3
            var maxByteCount = (value.Length + 1) * 3;
            
            ref var destPointer = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);

            var dest = CreateSpan(ref Unsafe.Add(ref destPointer, 8), maxByteCount);

            var status = StringSerializer.Serialize(source, dest, out _, out var bytesWritten, replaceInvalidSequences: false);

            if (status != OperationStatus.Done)
            { 
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
                
            As<byte, UnmanagedViewModel<int, int>>(ref destPointer) = new UnmanagedViewModel<int, int>(bytesWritten, value.Length);

            return bytesWritten;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8WithLength(int index, string? value, int length)
        {
            if (length is 0)
            {
                WriteStringRecordLengthHeader(ref index, -1);
                
                return;
            }
            
            WriteStringRecordLengthHeader(ref index, length);

            int index1 = index + 4;
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index1), length);
            
            var startIndex = index + 8;
            var target = LuminPackMarshal.CreateSpan(ref _bufferReference[startIndex], length);

            var status = StringSerializer.Serialize(value.AsSpan(), target, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf8WithLength(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                WriteStringRecordLengthHeader(ref index, -1);
                return 0;
            }
            
            var length = value.Length * sizeof(char);
            
            WriteStringRecordLengthHeader(ref index, length);
            
            var startIndex = index + 8;
            
            if (!MemoryMarshal.AsBytes(value).TryCopyTo(_bufferReference.Slice(startIndex)))
            {
                LuminPackExceptionHelper.ThrowFailedEncodingUtf8();
            }
            
            return length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithToken(int index, string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }
            
            StringSerializer.Serialize(value, _bufferReference.Slice(index), out _, out var bytesWritten, SerializeMode.Utf16, replaceInvalidSequences: false);
            
            var tokenIndex = index + bytesWritten;
            
            WriteStringRecordTokenHeader(ref tokenIndex);
            
            return bytesWritten;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16WithToken(int index, string? value, int length)
        {
            if (length is 0)
            {
                WriteStringRecordTokenHeader(ref index);
                
                return;
            }
            
            StringSerializer.Serialize(value, _bufferReference.Slice(index, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);
            
            var tokenIndex = index + length;
            
            WriteStringRecordTokenHeader(ref tokenIndex);
        }
        
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithToken(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }
            
            var length = checked(value.Length * 2);
            
            StringSerializer.Serialize(value, _bufferReference.Slice(index, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);
            
            var tokenIndex = index + length;
            
            WriteStringRecordTokenHeader(ref tokenIndex);
            
            return length;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithLength(int index, string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                WriteStringRecordLengthHeader(ref index, -1);
                return 0;
            }
            
            var length = unchecked(value.Length * 2);
            
            WriteStringRecordLengthHeader(ref index, length);
            var startIndex = index + 4;
            StringSerializer.Serialize(value, _bufferReference.Slice(startIndex, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);
            
            return length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16WithLength(int index, string? value, int length)
        {
            if (length is 0)
            {
                WriteStringRecordLengthHeader(ref index, -1);
                
                return;
            }
            
            WriteStringRecordLengthHeader(ref index, length);
            
            var startIndex = index + 4;
            
            StringSerializer.Serialize(value, _bufferReference.Slice(startIndex, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithLength(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                WriteStringRecordLengthHeader(ref index, -1);
                return 0;
            }
            
            var length = checked(value.Length * sizeof(char));
            
            WriteStringRecordLengthHeader(ref index, length);
            
            var startIndex = index + 4;
            
            StringSerializer.Serialize(value, _bufferReference.Slice(startIndex, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);
            
            return length;
        }

        
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public void WriteValue<T>(scoped in T? value)
        // {
        //     LuminPackParseProvider.Cache<T>.Parser!.Serialize(ref this, ref Unsafe.AsRef(in value));
        // }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueWithParser<TParser, T>(TParser parser, scoped in T? value)
            where TParser : ILuminPackableParser<T>
        {
            parser.Serialize(ref this, ref Unsafe.AsRef(in value));
        }
        
        /// <summary>
        /// 序列化数组
        /// </summary>
        /// <param name="array"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(T?[]? array)
        {
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousWriteUnmanagedArray(ref _currentIndex, array, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (array is null)
            {
                WriteNullCollectionHeader(ref _currentIndex);
                
                Advance(4);
                
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            
            WriteCollectionHeader(ref _currentIndex, array.Length);
            
            Advance(4);
            
            foreach (ref var value in array.AsSpan())
            {
                parser.Serialize(ref this, ref value);
            }
        }
        
        /// <summary>
        /// 序列化数组
        /// </summary>
        /// <param name="index"></param>
        /// <param name="array"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(scoped ref int index, T?[]? array)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousWriteUnmanagedArray(ref index, array, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (array is null)
            {
                WriteNullCollectionHeader(ref index);
                
                Advance(4);
                
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            
            WriteCollectionHeader(ref index, array.Length);
            Advance(4);
            
            foreach (ref var value in array.AsSpan())
            {
                parser.Serialize(ref this, ref value);
            }
        }
        
        /// <summary>
        /// 序列化Span
        /// </summary>
        /// <param name="span"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(scoped Span<T> span)
        {
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousWriteUnmanagedSpan(ref _currentIndex, span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref _currentIndex);
                
                Advance(4);
                
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            WriteCollectionHeader(ref _currentIndex, span.Length);
            Advance(4);
            
            foreach (ref var value in span)
            {
                parser.Serialize(ref this, ref value!);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(scoped ReadOnlySpan<T> span)
        {
            var index = _currentIndex;
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousWriteUnmanagedSpan(ref index, span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref index);
                
                Advance(4);
                
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            
            WriteCollectionHeader(ref index, span.Length);
            Advance(4);

            foreach (var value in span)
            {
                var v = value;
                parser.Serialize(ref this, ref v);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpanWithOutHeader<T>(scoped Span<T> span)
        {
            var index = _currentIndex;
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                int spanOffset;
                
                if (span.Length == 0)
                {
                    return;
                }
            
                var srcLength = Unsafe.SizeOf<T>() * span.Length;

                ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
                ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());
            
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
                spanOffset = srcLength;
                
                Advance(spanOffset);
                
                return;
            }

            if (span.IsEmpty)
            {
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            foreach (ref var value in span)
            {
                parser.Serialize(ref this, ref value!);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpanWithOutHeader<T>(scoped ReadOnlySpan<T> span)
        {
            var index = _currentIndex;
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                int spanOffset;
                
                if (span.Length == 0)
                {
                    return;
                }
            
                var srcLength = Unsafe.SizeOf<T>() * span.Length;

                ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
                ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
            
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
                spanOffset = srcLength;
                
                Advance(spanOffset);
                
                return;
            }

            if (span.IsEmpty)
            {
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            foreach (var value in span)
            {
                var v = value;
                parser.Serialize(ref this, ref v);
            }
        }
        
        /// <summary>
        /// 序列化Span
        /// </summary>
        /// <param name="index"></param>
        /// <param name="span"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(scoped ref int index, scoped Span<T> span)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousWriteUnmanagedSpan(ref index, span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref index);
                
                Advance(4);
                
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            
            WriteCollectionHeader(ref index, span.Length);
            Advance(4);
            foreach (ref var value in span)
            {
                parser.Serialize(ref this, ref value!);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousWriteUnmanagedSpan(ref index, span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref index);
                
                Advance(4);
                
                return;
            }
            
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            
            
            WriteCollectionHeader(ref index, span.Length);
            Advance(4);
            foreach (var value in span)
            {
                var v = value;
                parser.Serialize(ref this, ref v);
            }
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArray<T>(scoped ref T[]? array) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedArray(ref _currentIndex, array!);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref T[]? array) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedArrayWithOutHeader(ref _currentIndex, array!);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArray<T>(scoped ref int index, T[]? array) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedArray(ref index, array!);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedArrayWithOutHeader(ref index, array!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedSpan(ref index, span);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedSpan(ref index, span);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedSpanWithOutHeader(ref index, span);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span) 
            where T : unmanaged
        {
            DangerousWriteUnmanagedSpanWithOutHeader(ref index, span);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArray<T>(scoped ref int index, T[]? array, out int length) 
        {
            DangerousWriteUnmanagedArray(ref index, array!, out length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, out int length) 
        {
            DangerousWriteUnmanagedArrayWithOutHeader(ref index, array!, out length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span, out int length) 
        {
            DangerousWriteUnmanagedSpan(ref index, span, out length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int length) 
        {
            DangerousWriteUnmanagedSpan(ref index, span, out length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span, out int length) 
        {
            DangerousWriteUnmanagedSpanWithOutHeader(ref index, span, out length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int length) 
        {
            DangerousWriteUnmanagedSpanWithOutHeader(ref index, span, out length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArray<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
        {
            DangerousWriteUnmanagedArray(ref index, array!, length, out spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
        {
            DangerousWriteUnmanagedArrayWithOutHeader(ref index, array!, length, out spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset) 
        {
            DangerousWriteUnmanagedSpan(ref index, span, length, out spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset) 
        {
            DangerousWriteUnmanagedSpan(ref index, span, length, out spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset) 
        {
            DangerousWriteUnmanagedSpanWithOutHeader(ref index, span, length, out spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset) 
        {
            DangerousWriteUnmanagedSpanWithOutHeader(ref index, span, length, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref T[]? array) 
        {
            var index = _currentIndex;
            
            if (array is null)
            {
                WriteNullCollectionHeader(ref index);
                return;
            }
            
            if (array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref T[]? array) 
        {
            var index = _currentIndex;
            
            if (array is null)
            {
                return;
            }
            
            if (array.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref int index, T[]? array) 
        {
            if (array is null)
            {
                WriteNullCollectionHeader(ref index);
                return;
            }
            
            if (array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array) 
        {
            if (array is null)
            {
                return;
            }
            
            if (array.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref int index, T[]? array, out int length) 
        {
            if (array is null)
            {
                WriteNullCollectionHeader(ref index);
                length = 4;
                return;
            }
            
            if (array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                length = 4;
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            length = srcLength + 4;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, out int length) 
        {
            if (array is null)
            {
                length = 0;
                return;
            }
            
            if (array.Length == 0)
            {
                length = 0;
                return;
            }

            length = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
        {
            if (array is null)
            {
                WriteNullCollectionHeader(ref index);
                spanOffset = 4;
                return;
            }
            
            if (array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }
            
            
            var srcLength = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength + 4;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
        {
            if (array is null)
            {
                spanOffset = 0;
                return;
            }
            
            if (array.Length == 0)
            {
                spanOffset = 0;
                return;
            }
            
            
            spanOffset = Unsafe.SizeOf<T>() * array.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span)
        {
            
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span)
        {
            
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span)
        {
            if (span.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span)
        {
            
            if (span.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span, out int spanOffset)
        {
            
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }
            
            WriteCollectionHeader(ref index, span.Length);
            
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength + 4;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int spanOffset)
        {
            
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }
            
            WriteCollectionHeader(ref index, span.Length);
            
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength + 4;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span, out int spanOffset)
        {
            
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }
            
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }
            
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset)
        {
            
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            WriteCollectionHeader(ref index, length);
            
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength + 4;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset)
        {
            
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            WriteCollectionHeader(ref index, length);
            
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength + 4;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset)
        {
            if (span.Length is 0)
            {
                spanOffset = 0;
                return;
            }
            
            spanOffset = Unsafe.SizeOf<T>() * span.Length;
            
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset)
        {
            
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }
            
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
            
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
            
            spanOffset = srcLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedList<T>(scoped ref int index, scoped ref List<T>? list, out int spanOffset) 
            where T : struct
        {
            if (list is null || list.Count == 0)
            {
                WriteCollectionHeader(ref index, 0);
                
                spanOffset = 4;
                
                return;
            }
            
            var span = LuminPackMarshal.GetListSpan(list, list.Count);
            
            WriteUnmanagedSpan(ref index, span, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedList<T>(scoped ref int index, scoped ref List<T>? list, int length, out int spanOffset) 
            where T : struct
        {
            if (list is null || list.Count == 0)
            {
                WriteCollectionHeader(ref index, 0);
                
                spanOffset = 4;
                
                return;
            }

            var span = LuminPackMarshal.GetListSpan(list, list.Count);
            
            WriteUnmanagedSpan(ref index, span, length, out spanOffset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedListWithOutHeader<T>(scoped ref int index, scoped ref List<T>? list, out int spanOffset) 
            where T : struct
        {
            if (list is null || list.Count == 0)
            {
                spanOffset = 0;
                
                return;
            }
            
            var span = LuminPackMarshal.GetListSpan(list, list.Count);
            
            WriteUnmanagedSpanWithOutHeader(ref index, span, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedListWithOutHeader<T>(scoped ref int index, scoped ref List<T>? list, int length, out int spanOffset) 
            where T : struct
        {
            if (list is null || list.Count == 0)
            {
                spanOffset = 0;
                
                return;
            }

            var span = LuminPackMarshal.GetListSpan(list, list.Count);
            
            WriteUnmanagedSpanWithOutHeader(ref index, span, length, out spanOffset);
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1>(in T1 value) 
            where T1 : unmanaged
        {
            
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)_currentIndex);
            Unsafe.WriteUnaligned(ref spanRef, value);

            return Unsafe.SizeOf<T1>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2>(in T1 value1, in T2 value2) 
            where T1 : unmanaged
        {
            
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)_currentIndex);
            var offset = 0;
            Unsafe.WriteUnaligned(ref spanRef, value1);
            offset += Unsafe.SizeOf<T1>();
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, offset), value2);

            return offset + Unsafe.SizeOf<T2>();
        }
        
        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1>(ref int index, in T1 value) 
            where T1 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            Unsafe.WriteUnaligned(ref spanRef, value);

            return Unsafe.SizeOf<T1>();
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1>(in T1 value) 
            where T1 : unmanaged
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)_currentIndex), value);
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1>(ref int index, in T1 value) 
            where T1 : unmanaged
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2>(ref int index, in T1 value1, in T2 value2) 
            where T1 : unmanaged
            where T2 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2>(ref int index, in T1 value1, in T2 value2) 
            where T1 : unmanaged 
            where T2 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            int offset = 0;
            Unsafe.WriteUnaligned(ref spanRef, value1);
            offset += Unsafe.SizeOf<T1>();
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, offset), value2);
            offset += Unsafe.SizeOf<T2>();
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, offset), value3);
            offset += Unsafe.SizeOf<T3>();

            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3,
            in T4 value4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4,
            in T5 value5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4,
            in T5 value5, in T6 value6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2, in T3 value3,
            in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1, in T2 value2, in T3 value3,
            in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
            where T15 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
            where T15 : unmanaged
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            
        }
        
        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1>(in T1 value)
        {
            var index = _currentIndex;
            
            var size = Unsafe.SizeOf<T1>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            Unsafe.WriteUnaligned(ref spanRef, value);

            return size;
        }
        
        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1>(ref int index, in T1 value) 
        {
            var size = Unsafe.SizeOf<T1>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            Unsafe.WriteUnaligned(ref spanRef, value);

            return size;
        }

        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1>(in T1 value) 
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)_currentIndex), value);
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1>(ref int index, in T1 value) 
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2>(ref int index, in T1 value1, in T2 value2) 
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2>(ref int index, in T1 value1, in T2 value2) 
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3,
            in T4 value4)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4,
            in T5 value5)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4,
            in T5 value5, in T6 value6)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2, in T3 value3,
            in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1, in T2 value2, in T3 value3,
            in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1, in T2 value2,
            in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
            var size = Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(
            ref int index,
            in T1 value1,
            in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10,
            in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart.ToPointer()), (nint)index);
            
            Unsafe.WriteUnaligned(ref spanRef, value1);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            
        }
        
        public void WriteVarInt(byte x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(WriteUnmanaged((sbyte)x));
            }
            else
            {
                Advance(WriteUnmanaged(VarIntCodes.Byte, x));
            }
        }

        public void WriteVarInt(sbyte x)
        {
            if (VarIntCodes.MinSingleValue <= x)
            {
                Advance(WriteUnmanaged(x));
            }
            else
            {
                Advance(WriteUnmanaged(VarIntCodes.SByte, x));
            }
        }

        public void WriteVarInt(ushort x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(WriteUnmanaged((sbyte)x));
            }
            else
            {
                Advance(WriteUnmanaged(VarIntCodes.UInt16, (UInt16)x));
            }
        }

        public void WriteVarInt(short x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(WriteUnmanaged((sbyte)x));
                }
                else
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int16, (Int16)x));
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(WriteUnmanaged((sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    Advance(WriteUnmanaged(VarIntCodes.SByte, (SByte)x));
                }
                else
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int16, (Int16)x));
                }
            }
        }

        public void WriteVarInt(uint x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(WriteUnmanaged((sbyte)x));
            }
            else if (x <= ushort.MaxValue)
            {
                Advance(WriteUnmanaged(VarIntCodes.UInt16, (UInt16)x));
            }
            else
            {
                Advance(WriteUnmanaged(VarIntCodes.UInt32, (UInt32)x));
            }
        }

        public void WriteVarInt(int x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(WriteUnmanaged((sbyte)x));
                }
                else if (x <= short.MaxValue)
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int16, (Int16)x));
                }
                else
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int32, (Int32)x));
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(WriteUnmanaged((sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    Advance(WriteUnmanaged(VarIntCodes.SByte, (SByte)x));
                }
                else if (short.MinValue <= x)
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int16, (Int16)x));
                }
                else
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int32, (Int32)x));
                }
            }
        }

        public void WriteVarInt(ulong x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(WriteUnmanaged((sbyte)x));
            }
            else if (x <= ushort.MaxValue)
            {
                Advance(WriteUnmanaged(VarIntCodes.UInt16, (UInt16)x));
            }
            else if (x <= uint.MaxValue)
            {
                Advance(WriteUnmanaged(VarIntCodes.UInt32, (UInt32)x));
            }
            else
            {
                Advance(WriteUnmanaged(VarIntCodes.UInt64, (UInt64)x));
            }
        }

        public void WriteVarInt(long x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(WriteUnmanaged((sbyte)x));
                }
                else if (x <= short.MaxValue)
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int16, (Int16)x));
                }
                else if (x <= int.MaxValue)
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int32, (Int32)x));
                }
                else
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int64, (Int64)x));
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(WriteUnmanaged((sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    Advance(WriteUnmanaged(VarIntCodes.SByte, (SByte)x));
                }
                else if (short.MinValue <= x)
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int16, (Int16)x));
                }
                else if (int.MinValue <= x)
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int32, (Int32)x));
                }
                else
                {
                    Advance(WriteUnmanaged(VarIntCodes.Int64, (Int64)x));
                }
            }
        }
        
        #endregion
    }
}
