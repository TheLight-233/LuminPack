using System.Buffers;
using System.Buffers.Binary;
using LuminPack.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;
using LuminPack.Utility;
using LuminPack.Utility.ViewModel;
using static LuminPack.Code.LuminPackMarshal;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Numerics;
using System.Text.Unicode;

#endif
namespace LuminPack.Core
{
    /// <summary>Writes complete binary records with an advancing output cursor.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public ref partial struct LuminPackWriter
    {
#if NET8_0_OR_GREATER
        /// <summary>The advancing data cursor.</summary>
        public ref byte Cursor;
#endif
#if !NET8_0_OR_GREATER
        /// <summary>A one-element span carrying the cursor address. Use only after validating the accessed range.</summary>
        public Span<byte> Cursor;
#endif
        private Span<byte> _buffer;
        private uint _bufferCheckLimit = uint.MaxValue;
        /// <summary>The first byte no unchecked record may reach: the storage end or the owner's growth threshold.</summary>
        /// <remarks>Cached with every storage or threshold change, so a record check is one subtraction from the cursor.</remarks>
#if NET8_0_OR_GREATER
        private ref byte _recordLimit;
#else
        private Span<byte> _recordLimit;
#endif
        /// <summary>Gets the complete retained storage window used for dynamic bounds and framing patches.</summary>
        /// <remarks>Only the writer replaces its storage, so the cached record limit always describes it.</remarks>
        public Span<byte> Buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => _buffer;
            internal set
            {
                _buffer = value;
                RefreshRecordLimit();
            }
        }

        /// <summary>Gets the cached owned-buffer growth threshold; fixed spans use the maximum value.</summary>
        public uint BufferCheckLimit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => _bufferCheckLimit;
            internal set
            {
                _bufferCheckLimit = value;
                RefreshRecordLimit();
            }
        }

        /// <summary>Gets the optional growable output owner.</summary>
        public LuminBufferWriter? Owner {[MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get; internal set; }
        private LuminPackWriterOptionalState? _options;
        /// <summary>Identifies this operation to version schemas, which write their member table once per operation; zero until first needed.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int SchemaOperation;
        [ThreadStatic] private static int t_schemaOperations;
        /// <summary>The explicit or materialized reference context, or null before an implicit scope requests state.</summary>
        /// <remarks>An explicit context wins over the owning buffer's context. Writers constructed from a buffer
        /// resolve the buffer's own context without storing it, so construction leaves this unset.</remarks>
        public LuminPackWriterOptionalState? Options
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _options ?? Owner?.WriterState;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _options = value;
        }

        #region Constructors

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        public LuminPackWriter(LuminPackWriterOptionalState? option = null)
        {
            _options = option;
            Buffer = default;
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        public LuminPackWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {
            _options = option;
            Buffer = bufferReference;
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c> with the buffer's own reference context.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackWriter(LuminBufferWriter bufferWriter)
        {
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            _buffer = bufferWriter.BeginCursorWrite(out int position);
            BufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
            Position = position;
            Owner = bufferWriter;
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c> with an explicit shared reference context.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        /// <param name = "option">The reference context shared with the caller instead of the buffer's own context.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackWriter(LuminBufferWriter bufferWriter, LuminPackWriterOptionalState option)
        {
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            _options = option;
            _buffer = bufferWriter.BeginCursorWrite(out int position);
            BufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
            Position = position;
            Owner = bufferWriter;
        }

        #endregion

        #region Cursor and helpers

        /// <summary>Exposes the advancing cursor to internal reference-based kernels on every target.</summary>
        internal ref byte CurrentReference
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Current;
        }

        /// <summary>Reserves a dynamic framing or collection block at its absolute position.</summary>
        /// <param name = "position">The first byte of the complete dynamic block.</param>
        /// <param name = "length">The complete checked byte width.</param>
        /// <returns>The writable block after any required storage growth.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> Reserve(int position, int length)
        {
            EnsureCapacity(position, length);
            return LuminPackMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(position)), length);
        }

        /// <summary>Dereferences the architecture-specific advancing cursor inside shared implementation kernels.</summary>
        private readonly ref byte Current {[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
#if NET8_0_OR_GREATER
            ref Cursor;
#else
            ref MemoryMarshal.GetReference(Cursor);
#endif
        }

        /// <summary>Copies individual cursor fields into an isolated operation state without taking this state's address.</summary>
        /// <param name = "destination">The state used by an outlined compound operation.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyTo(ref LuminPackWriter destination)
        {
            destination._options = _options;
            destination.SchemaOperation = SchemaOperation;
#if NET8_0_OR_GREATER
            destination.Cursor = ref Cursor;
#else
            destination.Cursor = Cursor;
#endif
            destination.Owner = Owner;
            // Transfer the complete window and its already computed limit together.
            destination._bufferCheckLimit = _bufferCheckLimit;
            destination._buffer = _buffer;
#if NET8_0_OR_GREATER
            destination._recordLimit = ref _recordLimit;
#else
            destination._recordLimit = _recordLimit;
#endif
        }

        /// <summary>Gets the initialized reference context, creating an independent implicit scope on first access.</summary>
        /// <remarks>Initialize this context before copying an uninitialized writer when the copies must share references.</remarks>
        public LuminPackWriterOptionalState OptionState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Options is null)
                    Options = CreateReferenceState();
                return Options;
            }
        }

        /// <summary>Creates reference storage outside the hot cursor operation.</summary>
        /// <returns>An independent reference context owned by the requesting writer.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static LuminPackWriterOptionalState CreateReferenceState() => new LuminPackWriterOptionalState();

        /// <summary>Gets the current index.</summary>
        public int CurrentIndex {[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Position; [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Position = value; }

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name = "count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureAdditionalCapacity(int count) => EnsureCapacity(Position, count);

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(int index, int count)
        {
            if (Unsafe.SizeOf<IntPtr>() == 8)
            {
                // Widen both unsigned inputs so a single comparison also rejects negative values and overflow.
                if ((ulong)(uint)index + (uint)count <= (uint)_buffer.Length)
                    return;
            }
            else
            {
                if ((uint)index <= (uint)_buffer.Length && (uint)count <= (uint)(_buffer.Length - index))
                    return;
            }

            // The growth runs in a static helper that returns the refreshed cursor fields, so no call receives this writer's
            // address and an operation-local writer stays eligible for register promotion.
            Apply(GrowForCapacity(Owner, Position, index, count));
        }

        /// <summary>Grows the owning buffer for a capacity request outside the hot comparison.</summary>
        /// <param name = "owner">The owning output buffer.</param>
        /// <param name = "position">The current absolute output position.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "count">The number of bytes required from that position.</param>
        /// <returns>The cursor fields over the grown storage.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GrownStorage GrowForCapacity(LuminBufferWriter? owner, int position, int index, int count)
        {
            if (index < 0 || count < 0)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(index);
            return Grow(owner, position, checked(index + count), threshold: false);
        }

        /// <summary>Grows managed storage without passing the ref-struct state across a call boundary.</summary>
        /// <param name = "owner">The owning output buffer.</param>
        /// <param name = "position">The current absolute output position, which growth preserves.</param>
        /// <param name = "required">The absolute requested capacity or threshold position.</param>
        /// <param name = "threshold">Whether to apply the established collection growth threshold.</param>
        /// <returns>The cursor fields over the grown storage.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GrownStorage Grow(LuminBufferWriter? owner, int position, int required, bool threshold)
        {
            if (owner is null)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(required);
            if (threshold)
                owner!.CheckPosition(required);
            else
                owner!.EnsureCapacity(required);
            return new GrownStorage(owner, position);
        }

        /// <summary>Recomputes the cached record limit after the storage or the threshold changes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RefreshRecordLimit()
        {
            uint limit = Math.Min((uint)_buffer.Length, BufferCheckLimit - 1);
#if NET8_0_OR_GREATER
            _recordLimit = ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)limit);
#else
            _recordLimit = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)limit), 0);
#endif
        }

        /// <summary>Returns the signed distance from the cursor to the record limit.</summary>
        /// <returns>The bytes an unchecked record may use, negative once the threshold is passed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private nint RecordDistance()
        {
#if NET8_0_OR_GREATER
            return Unsafe.ByteOffset(ref Cursor, ref _recordLimit);
#else
            return Unsafe.ByteOffset(ref Current, ref MemoryMarshal.GetReference(_recordLimit));
#endif
        }

        /// <summary>Restores the hot cursor fields after managed storage has grown.</summary>
        /// <param name = "storage">The cursor fields over the grown storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Apply(GrownStorage storage)
        {
#if NET8_0_OR_GREATER
            Cursor = ref storage.Cursor;
#else
            Cursor = storage.Cursor;
#endif
            _buffer = storage.Buffer;
            BufferCheckLimit = storage.CheckLimit;
        }

        /// <summary>The cursor fields over grown managed storage, returned by value so growth never receives a writer's address.</summary>
        private readonly ref struct GrownStorage
        {
#if NET8_0_OR_GREATER
            /// <summary>The cursor at the preserved position.</summary>
            public readonly ref byte Cursor;
#else
            /// <summary>The cursor at the preserved position.</summary>
            public readonly Span<byte> Cursor;
#endif
            /// <summary>The complete grown storage window.</summary>
            public readonly Span<byte> Buffer;
            /// <summary>The owner's growth threshold after growth.</summary>
            public readonly uint CheckLimit;

            /// <summary>Captures the owner's current storage.</summary>
            /// <param name = "owner">The owning output buffer.</param>
            /// <param name = "position">The preserved absolute output position.</param>
            public GrownStorage(LuminBufferWriter owner, int position)
            {
                Buffer = owner.DangerousGetBuffer();
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)position);
#else
                Cursor = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)position), 1);
#endif
                CheckLimit = (uint)owner.BufferCheckLimit + 1;
            }
        }

        /// <summary>Performs the ensure writable advance step for the current operation.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("DEBUG")]
        private void EnsureWritableAdvance(int index, int count)
        {
            if (LuminPackReader.IsOutOfRange(index, count, _buffer.Length))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + count));
        }

        /// <summary>Moves the cursor past bytes a preceding capacity proof already covers.</summary>
        /// <param name = "count">A nonnegative byte count inside the proven range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceProven(int count)
        {
#if NET8_0_OR_GREATER
            Cursor = ref Unsafe.Add(ref Cursor, (nint)(uint)count);
#else
            Cursor = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref Current, (nint)count), 1);
#endif
        }

        /// <summary>
        /// Advances the current cursor.
        /// </summary>
        /// <param name = "count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            EnsureWritableAdvance(Position, count);
#if NET8_0_OR_GREATER
            Cursor = ref Unsafe.Add(ref Cursor, (nint)(uint)count);
#else
            Cursor = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref Current, (nint)count), 1);
#endif
        }

        /// <summary>
        /// Advances the current cursor.
        /// </summary>
        /// <param name = "count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceSafe(int count)
        {
            CheckBufferPosition(Position + count);
            Advance(count);
        }

        /// <summary>Updates the write buffer used by the current operation.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWriteBuffer(LuminBufferWriter? bufferWriter)
        {
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            Owner = bufferWriter;
            BufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
        }

        /// <summary>
        /// Replaces the current buffer span.
        /// </summary>
        /// <param name = "span"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            int position = Position;
            Buffer = span;
            if (Owner is null)
                BufferCheckLimit = uint.MaxValue;
            Position = position;
        }

        /// <summary>
        /// Releases the destination span and resets its cursor without modifying caller memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            Buffer = null;
            Position = 0;
        }

        /// <summary>
        /// Publishes pending buffer contents.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FlushBuffer()
        {
            if (Owner is not null)
            {
                int position = Position;
                Buffer = Owner.GetFullSpan();
                Position = position;
                BufferCheckLimit = (uint)Owner.BufferCheckLimit + 1;
            }
        }

        /// <summary>
        /// Returns the span currently held by the writer.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _buffer[..Position];
        }

        /// <summary>
        /// Validate the buffer bounds.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckBuffer()
        {
            // Position >= threshold exactly when the cursor is past the cached limit; a limit at the storage end never is.
            if (Owner is not null && RecordDistance() < 0)
                Apply(GrowForThreshold(Owner, Position, Position));
        }

        /// <summary>Checks a shared or completed position without introducing a returned-cursor merge.</summary>
        /// <param name = "index">The absolute byte position to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckBufferPosition(int index)
        {
            if (Owner is not null && (uint)index >= BufferCheckLimit)
                Apply(GrowForThreshold(Owner, Position, index));
        }

        /// <summary>Checks a collection's actual cursor before writing its next element.</summary>
        /// <param name = "index">The absolute collection write cursor.</param>
        /// <returns>The unchanged absolute cursor, allowing callers to end its previous register lifetime.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CheckBuffer(int index)
        {
            if ((uint)index >= BufferCheckLimit && Owner is not null)
                Apply(GrowForThreshold(Owner, Position, index));
            return index;
        }

        /// <summary>Gets or replaces the absolute position for framing and random-access implementation kernels.</summary>
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
            get => (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref Cursor);
#else
            get => (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref Current);
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)value);
#else
                Cursor = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)value), 1);
#endif
            }
        }

        /// <summary>Attempts one aggregate reservation while retaining a checked traversal for unrepresentable upper bounds.</summary>
        /// <param name = "byteCount">The complete remaining upper bound, calculated without narrowing overflow.</param>
        /// <returns>True when the complete bound can be reserved; false when the caller must check its actual records individually.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnsureCapacity(long byteCount)
        {
            const int maximumBufferLength = 0x7fffffc7;
            int position = Position;
            if (position < 0 || byteCount < 0 || byteCount > (long)maximumBufferLength - position)
                return false;
            EnsureAdditionalCapacity((int)byteCount);
            return true;
        }

        /// <summary>Preserves owned-buffer headroom after a guaranteed record while retaining valid caller-span writes.</summary>
        /// <remarks>The complete record has already been reserved. An unowned span requires no owner publication.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompleteReservedRecord()
        {
            if (Owner is not null)
                CheckBufferPosition(Position);
        }

        /// <summary>Reserves the count header of a collection whose count is published after its single traversal.</summary>
        /// <returns>The absolute header position passed to <see cref="EndCollection"/>.</returns>
        /// <remarks>Owned storage grows contiguously, so the absolute position stays valid across element writes. Concurrent collections use this instead of a separate locking or snapshot-taking count.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BeginCollection()
        {
            EnsureAdditionalCapacity(sizeof(int));
            int position = Position;
            Advance(sizeof(int));
            return position;
        }

        /// <summary>Publishes the count of a collection started by <see cref="BeginCollection"/>.</summary>
        /// <param name="position">The header position returned by <see cref="BeginCollection"/>.</param>
        /// <param name="count">The number of elements actually written.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndCollection(int position, int count)
        {
            LuminPackWriter.Write<int>(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)position), count);
        }

        #endregion

}
}
