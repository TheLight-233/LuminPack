using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Text.Unicode;
#endif
using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Utility;
using LuminPack.Utility.ViewModel;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Core
{
    /// <summary>Writes LuminPack binary values to a caller-supplied or reusable buffer.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref partial struct LuminPackWriter
    {

#if NET8_0_OR_GREATER
        private int _bufferLength;
        /// <summary>Reconstructs the buffer view from its single GC-tracked start reference.</summary>
        private Span<byte> _bufferReference
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => LuminPackMarshal.CreateSpan(ref _bufferStart, _bufferLength);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _bufferLength = value.Length;
                _bufferStart = ref MemoryMarshal.GetReference(value);
            }
        }
#else
        private Span<byte> _bufferReference;
        /// <summary>Gets the complete retained buffer capacity.</summary>
        private int _bufferLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _bufferReference.Length;
        }
#endif
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif
        internal int _currentIndex;
        private uint _bufferCheckLimit;
        private LuminBufferWriter? _writerBuffer = null;

        private readonly LuminPackWriterOptionalState _optionState;

        // 0: UTF8+Token, 1: UTF8+Length, 2: UTF16+Token, 3: UTF16+Length.
        private readonly byte _stringSerializationMode;
        /// <summary>Gets the serialize string as utf8.</summary>
        private bool SerializeStringAsUtf8 => (_stringSerializationMode & 2) == 0;
        /// <summary>Gets the serialize string record as token.</summary>
        private bool SerializeStringRecordAsToken => (_stringSerializationMode & 1) == 0;

        /// <summary>Gets the option state.</summary>
        public LuminPackWriterOptionalState OptionState => _optionState;
        /// <summary>Gets the option.</summary>
        public LuminPackSerializerOption Option => _optionState.Option;

        /// <summary>Gets the current index.</summary>
        public ref int CurrentIndex => ref _currentIndex;

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name="option">The configuration to apply to this operation.</param>
        public LuminPackWriter(LuminPackWriterOptionalState? option = null)
        {
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            _bufferReference = default;
#if !NET8_0_OR_GREATER
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _stringSerializationMode = GetStringSerializationMode(_optionState.Option);
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="option">The configuration to apply to this operation.</param>
        public LuminPackWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {

            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            _bufferReference = bufferReference;
#if !NET8_0_OR_GREATER
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _stringSerializationMode = GetStringSerializationMode(_optionState.Option);
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="option">The configuration to apply to this operation.</param>
        public LuminPackWriter(ref ReadOnlySpan<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {

            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            ref var ptr = ref Unsafe.AsRef(in bufferReference.GetPinnableReference());
            _bufferReference = CreateSpan(ref ptr, bufferReference.Length);
#if !NET8_0_OR_GREATER
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _stringSerializationMode = GetStringSerializationMode(_optionState.Option);
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name="bufferWriter">The writer receiving the output data.</param>
        /// <param name="option">The configuration to apply to this operation.</param>
        public LuminPackWriter(LuminBufferWriter? bufferWriter, LuminPackWriterOptionalState? option = null)
        {
            _optionState = option ?? LuminPackWriterOptionalState.NullOption;
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            _bufferReference = bufferWriter.BeginWrite(out _currentIndex);
            _bufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
#if !NET8_0_OR_GREATER
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _writerBuffer = bufferWriter;
            _stringSerializationMode = GetStringSerializationMode(_optionState.Option);
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name="bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LuminPackWriter(LuminBufferWriter bufferWriter)
        {
            _optionState = bufferWriter.WriterState;
            _bufferReference = bufferWriter.BeginWrite(out _currentIndex);
            _bufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
#if !NET8_0_OR_GREATER
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _writerBuffer = bufferWriter;
            _stringSerializationMode = GetStringSerializationMode(bufferWriter.Option);
        }

        /// <summary>Obtains the string serialization mode for the supplied input.</summary>
        /// <param name="option">The configuration to apply to this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetStringSerializationMode(LuminPackSerializerOption option) => option.StringSerializationMode;

        /// <summary>
        /// Returns a reference to the span's first element.
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

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureAdditionalCapacity(int count) => EnsureCapacity(_currentIndex, count);

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(int index, int count)
        {
            if ((uint)index <= (uint)_bufferLength &&
                (uint)count <= (uint)(_bufferLength - index))
                return;

            EnsureCapacitySlow(index, count);
        }

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureCapacitySlow(int index, int count)
        {
            if (_writerBuffer is null)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + count));

            _writerBuffer!.EnsureCapacity(checked(index + count));
            FlushBuffer();
        }

        /// <summary>Performs the ensure writable advance step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("DEBUG")]
        private void EnsureWritableAdvance(int index, int count)
        {
            if ((uint)index > (uint)_bufferLength ||
                (uint)count > (uint)(_bufferLength - index))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + count));
        }

        /// <summary>
        /// Advances the target pointer.
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            EnsureWritableAdvance(_currentIndex, count);
            _currentIndex += count;

        }

        /// <summary>
        /// Advances the target pointer.
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceSafe(int count)
        {
            int originalIndex = _currentIndex;
            int nextIndex = originalIndex + count;
            CheckBufferPosition(nextIndex);
            EnsureWritableAdvance(originalIndex, count);
            _currentIndex = nextIndex;
        }

        /// <summary>Updates the write buffer used by the current operation.</summary>
        /// <param name="bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWriteBuffer(LuminBufferWriter? bufferWriter)
        {
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();

            _writerBuffer = bufferWriter;
            _bufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
        }

        /// <summary>
        /// Replaces the current buffer span.
        /// </summary>
        /// <param name="span"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            _bufferReference = span;
#if !NET8_0_OR_GREATER
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
        }

        /// <summary>
        /// Releases the destination span and resets its cursor without modifying caller memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _bufferReference = null;
#if NET8_0_OR_GREATER
            _bufferStart = ref Unsafe.NullRef<byte>();
#else
            _bufferStart = default;
#endif
            _currentIndex = 0;
        }

        /// <summary>
        /// Publishes pending buffer contents.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FlushBuffer()
        {
            if (_writerBuffer is not null)
            {
                _bufferReference = _writerBuffer.GetFullSpan();
                _bufferCheckLimit = (uint)_writerBuffer.BufferCheckLimit + 1;
#if !NET8_0_OR_GREATER
                _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            }
        }

        /// <summary>Determines whether the value type requires managed reference handling.</summary>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsReferenceOrContainsReferences<T>()
        {
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        }

        /// <summary>Creates span from the supplied input.</summary>
        /// <param name="reference">The reference used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> CreateSpan<T>(scoped ref T reference, int length)
        {
            return MemoryMarshal.CreateSpan(ref reference, length);
        }

        /// <summary>
        /// Obtains a reference to the current array data.
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
        /// Returns the span currently held by the writer.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _bufferReference[.._currentIndex];
        }

        /// <summary>
        /// Returns a reference to the current buffer offset.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref int GetCurrentSpanOffset() => ref _currentIndex;

        /// <summary>
        /// Returns a reference to the size accumulator's current offset.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref LuminPackEvaluator GetCurrentSpanEvaluator()
        {
            var eval = new LuminPackEvaluator(ref _currentIndex);
            return ref eval;
        }

        /// <summary>
        /// Returns a reference to the current buffer offset.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCurrentSpanIndex() => _currentIndex;

        /// <summary>
        /// Validate the buffer bounds.
        /// </summary>
        /// <returns></returns>
        /// <param name="offset">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushCurrentIndex(int offset)
        {
            CheckBufferPosition(offset);
            _currentIndex = offset;
        }

        /// <summary>
        /// Validate the buffer bounds.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckBuffer()
        {
            CheckBufferPosition(_currentIndex);
        }

        /// <summary>Checks a shared or completed position without introducing a returned-cursor merge.</summary>
        /// <param name="index">The absolute byte position to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckBufferPosition(int index)
        {
            if ((uint)index >= _bufferCheckLimit) CheckCollectionBufferSlow(index);
        }

        /// <summary>Checks a collection's actual cursor before writing its next element.</summary>
        /// <param name="index">The absolute collection write cursor.</param>
        /// <returns>The unchanged absolute cursor, allowing callers to end its previous register lifetime.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CheckBuffer(int index)
        {
            return (uint)index >= _bufferCheckLimit ? CheckCollectionBufferSlow(index) : index;
        }

        /// <summary>Publishes the collection cursor before the original buffer-growth operation.</summary>
        /// <param name="index">The next element's actual byte position.</param>
        /// <returns>The unchanged absolute position after the original check.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int CheckCollectionBufferSlow(int index)
        {
            int originalIndex = _currentIndex;
            bool completed = false;
            _currentIndex = index;
            try
            {
                _writerBuffer!.Check(ref this);
                _bufferCheckLimit = (uint)_writerBuffer.BufferCheckLimit + 1;
                completed = true;
                return index;
            }
            finally
            {
                if (!completed) _currentIndex = originalIndex;
            }
        }

        /// <summary>
        /// Calculates the serialized string length.
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
        /// Calculates the string payload size, including its header or terminator.
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

            if (SerializeStringAsUtf8)
                return SerializeStringRecordAsToken
                    ? LuminPackMarshal.CalculateStringByteCount(value) + 1
                    : LuminPackMarshal.CalculateStringByteCount(value) + 8;
            return SerializeStringRecordAsToken
                ? checked(value.Length * 2) + 1
                : checked(value.Length * 2) + 4;
        }

        /// <summary>Returns the size of the string header used by the current recording mode.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength()
        {
            // UTF-16 token mode uses a two-byte 0x0000 terminator because individual zero bytes are common within code units.
            return SerializeStringRecordAsToken ? (SerializeStringAsUtf8 ? 1 : 2) : SerializeStringAsUtf8 ? 8 : 4;
        }

        /// <summary>
        /// Calculates the byte size of an unmanaged array.
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
        /// Writes the null object header.
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
        /// Writes the null object header.
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
        /// Writes the collection header.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length">The collection length.</param>
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
        /// Generator fast path for a non-null list: obtains its backing span and writes the matching
        /// collection header in one call boundary.
        /// </summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="list">The list used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T?> WriteListHeaderAndGetSpan<T>(ref int index, List<T?> list)
        {
            var listSpan = LuminPackMarshal.GetListSpan(list);
            WriteCollectionHeader(ref index, listSpan.Length);
            return listSpan;
        }

        /// <summary>
        /// Writes the null collection header.
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
        /// Writes the null string marker.
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullStringHeader(ref int index)
        {
            if (SerializeStringRecordAsToken)
            {
                WriteStringRecordTokenHeader(ref index);
                return;
            }

            if (SerializeStringAsUtf8)
            {
                WriteNullUtf8LengthHeader(ref index);
                return;
            }

            WriteStringRecordLengthHeader(ref index, LuminPackCode.NullCollection);
        }

        /// <summary>Writes null string header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="offset">Receives the offset produced by the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullStringHeader(ref int index, out int offset)
        {
            if (SerializeStringRecordAsToken)
            {
                WriteStringRecordTokenHeader(ref index);
                offset = SerializeStringAsUtf8 ? 1 : 2;
                return;
            }

            if (SerializeStringAsUtf8)
            {
                WriteNullUtf8LengthHeader(ref index);
                offset = sizeof(int) * 2;
                return;
            }

            WriteStringRecordLengthHeader(ref index, LuminPackCode.NullCollection);
            offset = sizeof(int);
        }

        /// <summary>
        /// Writes the string terminator.
        /// </summary>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringRecordTokenHeader(ref int index)
        {
            if (SerializeStringAsUtf8)
            {
                // UTF-8 token mode uses a single zero-byte terminator.
#if NET8_0_OR_GREATER
                Unsafe.Add(ref _bufferStart, (nint)(uint)index) = 0;
#else
                Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = 0;
#endif
            }
            else
            {
                // UTF-16 token mode terminates at the two-byte null character 0x0000.
#if NET8_0_OR_GREATER
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (ushort)0);
#else
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (ushort)0);
#endif
            }
        }

        /// <summary>
        /// Writes the string length header.
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

        /// <summary>Writes null utf8 length header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullUtf8LengthHeader(ref int index)
        {
            WriteEmptyOrNullUtf8LengthHeader(index, LuminPackCode.NullCollection);
        }

        /// <summary>Writes empty or null utf8 length header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteEmptyOrNullUtf8LengthHeader(int index, int length)
        {
            EnsureCapacity(index, 8);
            // Both words belong to the record, even when the reader ignores the
            // character count. Initialize them together in a reused buffer.
            long header = (long)(uint)length | ((long)length << 32);
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), header);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), header);
#endif
        }

        /// <summary>Writes object header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="memberCount">The member count used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectHeader(ref int index, byte memberCount)
        {
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = memberCount;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = memberCount;
#endif
        }

        /// <summary>Writes null object header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="memberCount">The member count used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader(ref int index, byte memberCount)
        {
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = LuminPackCode.NullObject;
#else
            Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = LuminPackCode.NullObject;
#endif
        }

        /// <summary>Writes union header to the current output.</summary>
        /// <param name="tag">The tag used by this operation.</param>
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

        /// <summary>Writes union header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="tag">The tag used by this operation.</param>
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

        /// <summary>Writes wide union header to the current output.</summary>
        /// <param name="tag">The tag used by this operation.</param>
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

        /// <summary>Writes wide union header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="tag">The tag used by this operation.</param>
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

        /// <summary>Writes null union header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullUnionHeader(ref int index)
        {
            WriteNullObjectHeader(ref index);
        }

        /// <summary>Writes object reference id to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="referenceId">The reference id used by this operation.</param>
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
        /// Writes a string using the configured encoding and recording mode.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>The number of bytes contributed to the payload.</returns>
#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public int WriteString(string? value)
        {
#if NET8_0_OR_GREATER
            return _stringSerializationMode == 1
                ? WriteUtf8WithLength(_currentIndex, value)
                : WriteStringOtherMode(_currentIndex, value);
#else
            var mode = _stringSerializationMode;
            if (mode == 3)
                return WriteUtf16WithLength(_currentIndex, value);
            if (mode == 1)
                return WriteUtf8WithLength(_currentIndex, value);
            return mode == 0
                ? WriteUtf8WithToken(_currentIndex, value)
                : WriteUtf16WithToken(_currentIndex, value);
#endif
        }

        /// <summary>
        /// Writes a string with a known length.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="length"></param>
        /// <returns>The number of bytes contributed to the payload.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteString(string? value, int length)
        {
            var mode = _stringSerializationMode;
            if (mode == 3)
                WriteUtf16WithLength(_currentIndex, value, length);
            else if (mode == 1)
                WriteUtf8WithLength(_currentIndex, value, length);
            else if (mode == 0)
                WriteUtf8WithToken(_currentIndex, value, length);
            else
                WriteUtf16WithToken(_currentIndex, value, length);
        }

        /// <summary>
        /// Writes a string using the configured encoding and recording mode.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <returns>The number of bytes contributed to the payload.</returns>
#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public int WriteString(ref int index, string? value)
        {
#if NET8_0_OR_GREATER
            return _stringSerializationMode == 1
                ? WriteUtf8WithLength(index, value)
                : WriteStringOtherMode(index, value);
#else
            var mode = _stringSerializationMode;
            if (mode == 3)
                return WriteUtf16WithLength(index, value);
            if (mode == 1)
                return WriteUtf8WithLength(index, value);
            return mode == 0
                ? WriteUtf8WithToken(index, value)
                : WriteUtf16WithToken(index, value);
#endif
        }

#if NET8_0_OR_GREATER
        // Only the small mode test is inlined into generated parsers. Each mode
        // keeps its own frame; UTF-8 identifiers never prepare token/copy state.
        /// <summary>Writes string other mode to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int WriteStringOtherMode(int index, string? value)
        {
            var mode = _stringSerializationMode;
            if (mode == 3)
                return WriteUtf16WithLength(index, value);
            return mode == 0
                ? WriteUtf8WithToken(index, value)
                : WriteUtf16WithToken(index, value);
        }
#endif

        /// <summary>
        /// Writes a string with a known length.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <param name="length"></param>
        /// <returns>The number of bytes contributed to the payload.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteString(ref int index, string? value, int length)
        {
            var mode = _stringSerializationMode;
            if (mode == 3)
                WriteUtf16WithLength(index, value, length);
            else if (mode == 1)
                WriteUtf8WithLength(index, value, length);
            else if (mode == 0)
                WriteUtf8WithToken(index, value, length);
            else
                WriteUtf16WithToken(index, value, length);
        }

        /// <summary>
        /// Writes a string from a read-only character span.
        /// </summary>
        /// /// <param name="index"></param>
        /// <param name="value"></param>
        /// <returns>The number of bytes contributed to the payload.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int WriteString(ref int index, ReadOnlySpan<char> value)
        {
            var mode = _stringSerializationMode;
            if (mode == 3)
                return WriteUtf16WithLength(index, value);
            if (mode == 1)
                return WriteUtf8WithLength(index, value);
            return mode == 0
                ? WriteUtf8WithToken(index, value)
                : WriteUtf16WithToken(index, value);
        }

        /// <summary>Writes utf8 with token to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf8WithToken(int index, string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                EnsureCapacity(index, 1);
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }

            var destination = (uint)index <= (uint)_bufferLength ? _bufferReference.Slice(index) : Span<byte>.Empty;
            var status = StringSerializer.Serialize(value, destination, out var _, out var bytesWritten, replaceInvalidSequences: false);

            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                    bytesWritten = RetryWriteUtf8WithToken(index, value);
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            EnsureCapacity(index, checked(bytesWritten + 1));
            var tokenIndex = index + bytesWritten;
            WriteStringRecordTokenHeader(ref tokenIndex);

            return bytesWritten;

        }

        /// <summary>Writes utf8 with token to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8WithToken(int index, string? value, int length)
        {
            if (length is 0)
            {
                EnsureCapacity(index, 1);
                WriteStringRecordTokenHeader(ref index);
                return;
            }
            if (length > 0) EnsureCapacity(index, checked(length + 1));
            var status = StringSerializer.Serialize(value.AsSpan(), _bufferReference.Slice(index, length), out _, out var bytesWritten, replaceInvalidSequences: false);

            if (status != OperationStatus.Done)
            {
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            var tokenIndex = index + length;
            WriteStringRecordTokenHeader(ref tokenIndex);
        }

        /// <summary>Writes utf8 with token to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf8WithToken(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                EnsureCapacity(index, 1);
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }

            var destination = (uint)index <= (uint)_bufferLength ? _bufferReference.Slice(index) : Span<byte>.Empty;
            var status = StringSerializer.Serialize(value, destination, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                    bytesWritten = RetryWriteUtf8WithToken(index, value);
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            EnsureCapacity(index, checked(bytesWritten + 1));
            var tokenIndex = index + bytesWritten;
            WriteStringRecordTokenHeader(ref tokenIndex);

            return bytesWritten;
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes utf8 with length to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
#else
        /// <summary>Writes utf8 with length to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public int WriteUtf8WithLength(int index, string? value)
        {
            if (value is null)
            {
                WriteNullUtf8LengthHeader(ref index);
                return 0;
            }

            if (value.Length == 0)
            {
                WriteEmptyOrNullUtf8LengthHeader(index, 0);
                return 0;
            }

            var source = value.AsSpan();

            var startIndex = checked(index + 8);
            var dest = (uint)startIndex <= (uint)_bufferLength
                ? _bufferReference.Slice(startIndex)
                : Span<byte>.Empty;

#if NET8_0_OR_GREATER
            // Common identifiers need neither partial-conversion state nor the
            // general transcoder's argument frame. Validate before writing.
            if (ShortAsciiTranscoder.TryEncode(source, dest, escapeJson: false))
            {
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index),
                    ((ulong)(uint)value.Length << 32) | (uint)value.Length);
                return value.Length;
            }
            return WriteUtf8WithLengthSlow(index, source, dest);
        }

        // Keep general transcoding, retry state and its argument frame out of
        // the short ASCII path.
        /// <summary>Writes utf8 with length slow to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="source">The input data to process.</param>
        /// <param name="dest">The destination receiving the result.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int WriteUtf8WithLengthSlow(int index, ReadOnlySpan<char> source, Span<byte> dest)
        {
#endif
            // UTF8.GetMaxByteCount -> (length + 1) * 3
            var maxByteCount = checked((source.Length + 1) * 3);
            var status = StringSerializer.Serialize(source, dest, out _, out var bytesWritten, replaceInvalidSequences: false);

            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                    bytesWritten = RetryWriteUtf8WithLength(index, source, maxByteCount);
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

#if NET8_0_OR_GREATER
            ref var destPointer = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var destPointer = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            As<byte, UnmanagedViewModel<int, int>>(ref destPointer) = new UnmanagedViewModel<int, int>(bytesWritten, source.Length);

            return bytesWritten;
        }

        /// <summary>Writes utf8 with length to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8WithLength(int index, string? value, int length)
        {
            if (value is null)
            {
                WriteNullUtf8LengthHeader(ref index);
                return;
            }

            if (length is 0 || value.Length is 0)
            {
                WriteEmptyOrNullUtf8LengthHeader(index, 0);
                return;
            }

            if (length > 0) EnsureCapacity(index, checked(length + 8));
            WriteStringRecordLengthHeader(ref index, length);

            int index1 = index + 4;
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index1), value.Length);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index1), value.Length);
#endif

            var startIndex = index + 8;
            var target = LuminPackMarshal.CreateSpan(ref _bufferReference[startIndex], length);

            var status = StringSerializer.Serialize(value.AsSpan(), target, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

        }

        /// <summary>Writes utf8 with length to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf8WithLength(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                WriteEmptyOrNullUtf8LengthHeader(index, 0);
                return 0;
            }

            var startIndex = index + 8;
            var destination = (uint)startIndex <= (uint)_bufferLength ? _bufferReference.Slice(startIndex) : Span<byte>.Empty;
            var status = StringSerializer.Serialize(value, destination, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                    bytesWritten = RetryWriteUtf8WithLength(index, value, checked((value.Length + 1) * 3));
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            WriteStringRecordLengthHeader(ref index, bytesWritten);
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref _bufferStart, (nint)(uint)(index + 4)), value.Length);
#else
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)(index + 4)), value.Length);
#endif

            return bytesWritten;
        }

        /// <summary>Performs the retry write utf8 with token step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int RetryWriteUtf8WithToken(int index, ReadOnlySpan<char> value)
        {
            EnsureCapacity(index, checked((value.Length + 1) * 3 + 1));
            var status = StringSerializer.Serialize(value, _bufferReference.Slice(index), out _, out var bytesWritten,
                replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            return bytesWritten;
        }

        /// <summary>Performs the retry write utf8 with length step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="maxByteCount">The max byte count used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int RetryWriteUtf8WithLength(int index, ReadOnlySpan<char> value, int maxByteCount)
        {
            EnsureCapacity(index, checked(maxByteCount + 8));
            var status = StringSerializer.Serialize(value, _bufferReference.Slice(index + 8), out _, out var bytesWritten,
                replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            return bytesWritten;
        }

        /// <summary>Writes utf16 with token to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithToken(int index, string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                EnsureCapacity(index, 2);
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }

            int bytesWritten = checked(value.Length * sizeof(char));
            EnsureCapacity(index, checked(bytesWritten + 2));
            Unsafe.CopyBlockUnaligned(ref GetSpanReference(index),
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytesWritten);

            var tokenIndex = index + bytesWritten;

            WriteStringRecordTokenHeader(ref tokenIndex);

            return bytesWritten;
        }

        /// <summary>Writes utf16 with token to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16WithToken(int index, string? value, int length)
        {
            if (length is 0)
            {
                EnsureCapacity(index, 2);
                WriteStringRecordTokenHeader(ref index);

                return;
            }

            if (length > 0) EnsureCapacity(index, checked(length + 2));
            StringSerializer.Serialize(value, _bufferReference.Slice(index, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);

            var tokenIndex = index + length;

            WriteStringRecordTokenHeader(ref tokenIndex);
        }



        /// <summary>Writes utf16 with token to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithToken(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                EnsureCapacity(index, 2);
                WriteStringRecordTokenHeader(ref index);
                return 0;
            }

            var length = checked(value.Length * 2);
            EnsureCapacity(index, checked(length + 2));

            Unsafe.CopyBlockUnaligned(ref GetSpanReference(index),
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value)), (uint)length);

            var tokenIndex = index + length;

            WriteStringRecordTokenHeader(ref tokenIndex);

            return length;
        }

        /// <summary>Copies an already reserved UTF-16 payload using exact overlapping short lanes.</summary>
        /// <param name="destination">The first reserved destination byte.</param>
        /// <param name="source">The first immutable character byte.</param>
        /// <param name="length">The complete even payload length.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyUtf16Payload(ref byte destination, ref byte source, uint length)
        {
            if (IntPtr.Size == 8 && length <= 64)
            {
#if NET8_0_OR_GREATER
                if (global::System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated && length >= 32)
                {
                    Unsafe.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<global::System.Runtime.Intrinsics.Vector256<byte>>(ref source));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nint)(length - 32)), Unsafe.ReadUnaligned<global::System.Runtime.Intrinsics.Vector256<byte>>(ref Unsafe.Add(ref source, (nint)(length - 32))));
                    return;
                }
                if (length >= 16 && length <= 32)
                {
                    Unsafe.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<global::System.Runtime.Intrinsics.Vector128<byte>>(ref source));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nint)(length - 16)), Unsafe.ReadUnaligned<global::System.Runtime.Intrinsics.Vector128<byte>>(ref Unsafe.Add(ref source, (nint)(length - 16))));
                    return;
                }
#endif
                if (length > 16) { Unsafe.CopyBlockUnaligned(ref destination, ref source, length); return; }
                if (length >= 8)
                {
                    Unsafe.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<ulong>(ref source));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nint)(length - 8)), Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, (nint)(length - 8))));
                }
                else if (length >= 4)
                {
                    Unsafe.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<uint>(ref source));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nint)(length - 4)), Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (nint)(length - 4))));
                }
                else if (length != 0)
                    Unsafe.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<ushort>(ref source));
                return;
            }
            Unsafe.CopyBlockUnaligned(ref destination, ref source, length);
        }

        /// <summary>Writes utf16 with length to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithLength(int index, string? value)
        {
            if (value is null)
            {
                EnsureCapacity(index, 4);
                WriteStringRecordLengthHeader(ref index, LuminPackCode.NullCollection);
                return 0;
            }

            if (value.Length is 0)
            {
                EnsureCapacity(index, 4);
                WriteStringRecordLengthHeader(ref index, 0);
                return 0;
            }

            var length = checked(value.Length * sizeof(char));
            EnsureCapacity(index, checked(length + 4));

            WriteStringRecordLengthHeader(ref index, length);
            var startIndex = index + 4;
            CopyUtf16Payload(ref GetSpanReference(startIndex),
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)length);

            return length;
        }

        /// <summary>Writes utf16 with length to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf16WithLength(int index, string? value, int length)
        {
            if (value is null)
            {
                EnsureCapacity(index, 4);
                WriteStringRecordLengthHeader(ref index, LuminPackCode.NullCollection);
                return;
            }

            if (length is 0 || value.Length is 0)
            {
                EnsureCapacity(index, 4);
                WriteStringRecordLengthHeader(ref index, 0);
                return;
            }

            if (length > 0) EnsureCapacity(index, checked(length + 4));
            WriteStringRecordLengthHeader(ref index, length);

            var startIndex = index + 4;

            StringSerializer.Serialize(value, _bufferReference.Slice(startIndex, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);
        }

        /// <summary>Writes utf16 with length to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUtf16WithLength(int index, ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
            {
                EnsureCapacity(index, 4);
                WriteStringRecordLengthHeader(ref index, 0);
                return 0;
            }

            var length = checked(value.Length * sizeof(char));
            EnsureCapacity(index, checked(length + 4));

            WriteStringRecordLengthHeader(ref index, length);

            var startIndex = index + 4;

            Unsafe.CopyBlockUnaligned(ref GetSpanReference(startIndex),
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value)), (uint)length);

            return length;
        }


        /// <summary>
        /// Writes the array contents.
        /// </summary>
        /// <param name="array"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(T?[]? array)
        {
            if (array is null)
            {
                EnsureAdditionalCapacity(sizeof(int));
                WriteNullCollectionHeader(ref _currentIndex);
                Advance(sizeof(int));
                return;
            }

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                EnsureAdditionalCapacity(checked(sizeof(int) + array.Length * Unsafe.SizeOf<T>()));
                DangerousWriteUnmanagedArray(ref _currentIndex, array, out var offset);

                Advance(offset);

                CheckBuffer();

                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>
        /// Writes the array contents.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="array"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(scoped ref int index, T?[]? array)
        {
            if (array is null)
            {
                EnsureCapacity(index, sizeof(int));
                WriteNullCollectionHeader(ref index);
                Advance(sizeof(int));
                return;
            }

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                EnsureCapacity(index, checked(sizeof(int) + array.Length * Unsafe.SizeOf<T>()));
                DangerousWriteUnmanagedArray(ref index, array, out var offset);

                Advance(offset);

                CheckBuffer();

                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>
        /// SerializeSpan
        /// </summary>
        /// <param name="span"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(scoped Span<T> span)
        {

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                EnsureAdditionalCapacity(checked(sizeof(int) + span.Length * Unsafe.SizeOf<T>()));
                DangerousWriteUnmanagedSpan(ref _currentIndex, span, out var offset);

                Advance(offset);

                CheckBuffer();

                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref _currentIndex);

                Advance(4);

                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Writes span to the current output.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(scoped ReadOnlySpan<T> span)
        {
            var index = _currentIndex;

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                EnsureAdditionalCapacity(checked(sizeof(int) + span.Length * Unsafe.SizeOf<T>()));
                DangerousWriteUnmanagedSpan(ref index, span, out var offset);

                Advance(offset);

                CheckBuffer();

                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref index);

                Advance(4);

                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Writes span with out header to the current output.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

                var srcLength = checked(Unsafe.SizeOf<T>() * span.Length);
                EnsureAdditionalCapacity(srcLength);

#if NET8_0_OR_GREATER
                ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
                ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
                ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference());

                Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)srcLength);

                spanOffset = srcLength;

                Advance(spanOffset);
                return;
            }

            if (span.IsEmpty)
            {
                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Writes span with out header to the current output.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

                var srcLength = checked(Unsafe.SizeOf<T>() * span.Length);
                EnsureAdditionalCapacity(srcLength);

#if NET8_0_OR_GREATER
                ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
                ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
                ref var src = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));

                Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)srcLength);

                spanOffset = srcLength;

                Advance(spanOffset);
                return;
            }

            if (span.IsEmpty)
            {
                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>
        /// SerializeSpan
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

                CheckBuffer();

                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref index);

                Advance(4);

                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Writes span to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(scoped ref int index, scoped ReadOnlySpan<T> span)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousWriteUnmanagedSpan(ref index, span, out var offset);

                Advance(offset);

                CheckBuffer();

                return;
            }

            if (span.IsEmpty)
            {
                WriteNullCollectionHeader(ref index);

                Advance(4);

                return;
            }

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }


        /// <summary>Writes unmanaged array to the current output.</summary>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array with out header to the current output.</summary>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">Receives the length produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">Receives the length produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            var srcLength = Unsafe.SizeOf<T>() * length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)srcLength);

            spanOffset = srcLength + 4;
        }

        /// <summary>Writes unmanaged array with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            spanOffset = Unsafe.SizeOf<T>() * length;

#if NET8_0_OR_GREATER
            ref var dest = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var dest = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            ref var src = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)spanOffset);
        }

        /// <summary>Writes unmanaged span to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array with out header using the caller-validated buffer layout.</summary>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">Receives the length produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">Receives the length produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged array with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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


        /// <summary>Writes unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged span with out header using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged list to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="list">The list used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged list to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="list">The list used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged list with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="list">The list used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes unmanaged list with out header to the current output.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="list">The list used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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

        /// <summary>Writes var int to the current output.</summary>
        /// <param name="x">The x used by this operation.</param>
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
