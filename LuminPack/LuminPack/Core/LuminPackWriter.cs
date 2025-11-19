using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Text.Unicode;
#endif
using LuminPack.Interface;
using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Parsers;
using LuminPack.Utility;
using LuminPack.Utility.ViewModel;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Core
{
    [StructLayout(LayoutKind.Auto)]
    public unsafe ref partial struct LuminPackWriter
    {
        
        private Span<byte> _bufferReference;
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif
        private int _currentIndex;
        private LuminBufferWriter? _writerBuffer = null;
        
        private readonly LuminPackWriterOptionalState _optionState;

        private readonly bool SerializeStringAsUtf8;
        private readonly bool SerializeStringRecordAsToken;
        
        public LuminPackWriterOptionalState OptionState => _optionState;
        public LuminPackSerializerOption Option => _optionState.Option;
        
        public ref int CurrentIndex => ref _currentIndex;

        public LuminPackWriter(LuminPackWriterOptionalState? option = null)
        {
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            _bufferReference = default;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {
            
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
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
        
        public LuminPackWriter(ref ReadOnlySpan<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {
            
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            var ptr = bufferReference.GetPinnableReference();
            _bufferReference = CreateSpan(ref ptr, bufferReference.Length);
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackWriter(LuminBufferWriter? bufferWriter, LuminPackWriterOptionalState? option = null)
        {
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
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
            
            _writerBuffer!.Check(ref this);
#if DEBUG
             if (_bufferReference.Length < _currentIndex)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
#endif
            
            _currentIndex += count;
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWriteBuffer(LuminBufferWriter? bufferWriter)
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

        /// <summary>
        /// 刷新Buffer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FlushBuffer()
        {
            if (_writerBuffer is not null)
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
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

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
            _writerBuffer!.Check(ref this);

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
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = LuminPackCode.NullObject;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = LuminPackCode.NullObject;
#endif
        }
        
        /// <summary>
        /// 序列化空Object字节
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader()
        {
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex) = LuminPackCode.NullObject;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex) = LuminPackCode.NullObject;
#endif
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
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), length);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), length);
#endif
        }
        
        /// <summary>
        /// 序列化空集合字节
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullCollectionHeader(ref int index)
        {
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), LuminPackCode.NullCollection);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), LuminPackCode.NullCollection);
#endif
            
        }

        /// <summary>
        /// 序列化空字符串
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullStringHeader(ref int index)
        {
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), 0);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), 0);
#endif
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullStringHeader(ref int index, out int offset)
        {
            if (SerializeStringRecordAsToken)
            {
#if NET8_0_OR_GREATER
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), 0);
#else
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), 0);
#endif

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
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = 0;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = 0;
#endif
        }
        
        /// <summary>
        /// 序列化字符串字节数
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringRecordLengthHeader(ref int index, int length)
        {
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), length);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectHeader(ref int index, byte memberCount)
        {
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = memberCount;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = memberCount;
#endif
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader(ref int index, byte memberCount)
        {
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = LuminPackCode.NullObject;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = LuminPackCode.NullObject;
#endif
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnionHeader(ushort tag)
        {
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex) = (byte)tag;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex) = (byte)tag;
#endif
            Advance(1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnionHeader(ref int index, ushort tag)
        {
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = (byte)tag;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = (byte)tag;
#endif
            Advance(1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteWideUnionHeader(ushort tag)
        {
            if (tag < LuminPackCode.WideTag)
            {
#if NET8_0_OR_GREATER
                Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex) = (byte)tag;
#else
                Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex) = (byte)tag;
#endif
                Advance(1);
            }
            else
            {
#if NET8_0_OR_GREATER
                ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
                ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
                Unsafe.WriteUnaligned(ref spanRef, LuminPackCode.WideTag);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, 1), tag);
                Advance(3);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteWideUnionHeader(ref int index, ushort tag)
        {
            if (tag < LuminPackCode.WideTag)
            {
#if NET8_0_OR_GREATER
                Unsafe.Add(ref _bufferStart, (nint)(uint)index) = (byte)tag;
#else
                Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = (byte)tag;
#endif
                Advance(1);
            }
            else
            {
#if NET8_0_OR_GREATER
                ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
                ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = LuminPackCode.ReferenceId;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = LuminPackCode.ReferenceId;
#endif
            Advance(1);
            this.WriteVarInt(referenceId);
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
            
#if NET8_0_OR_GREATER
            ref var destPointer = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var destPointer = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

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
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index1), length);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index1), length);
#endif
            
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

#if NET8_0_OR_GREATER
                ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
                ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
                ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
                ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref T[]? array) 
            where T : unmanaged
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArray<T>(scoped ref int index, T[]? array) 
            where T : unmanaged
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array) 
            where T : unmanaged
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArray<T>(scoped ref int index, T[]? array, out int length) 
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            length = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, out int length) 
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)length);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArray<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span) 
            where T : unmanaged
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span) 
            where T : unmanaged
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span) 
            where T : unmanaged
        {
            if (span.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span) 
            where T : unmanaged
        {
            if (span.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));

            Unsafe.WriteUnaligned(ref dest, span.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }
        
            WriteCollectionHeader(ref index, span.Length);
        
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }
        
            WriteCollectionHeader(ref index, span.Length);
        
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }
        
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }
        
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            WriteCollectionHeader(ref index, length);
        
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            WriteCollectionHeader(ref index, length);
        
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }
        
            spanOffset = Unsafe.SizeOf<T>() * span.Length;
        
#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)spanOffset);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedSpanWithOutHeader<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }
        
            var srcLength = Unsafe.SizeOf<T>() * span.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref T[]? array) 
        {
            var index = _currentIndex;
        
            if (array!.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref T[]? array) 
        {
            var index = _currentIndex;
        
            if (array!.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref int index, T[]? array) 
        {
            if (array!.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array) 
        {
            if (array!.Length == 0)
            {
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref int index, T[]? array, out int length) 
        {
            if (array!.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                length = 4;
                return;
            }

            var srcLength = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            length = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, out int length) 
        {
            if (array!.Length == 0)
            {
                length = 0;
                return;
            }

            length = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.WriteUnaligned(ref dest, array.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)length);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArray<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
        {
            if (array!.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }
        
            var srcLength = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);
        
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);
        
            spanOffset = srcLength + 4;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeader<T>(scoped ref int index, T[]? array, int length, out int spanOffset) 
        {
            if (array!.Length == 0)
            {
                spanOffset = 0;
                return;
            }
        
            spanOffset = Unsafe.SizeOf<T>() * array.Length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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
            
#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
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