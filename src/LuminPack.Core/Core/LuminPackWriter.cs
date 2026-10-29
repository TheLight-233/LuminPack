using System.Buffers;
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
    public unsafe ref struct LuminPackWriter
    {
#if NET8_0_OR_GREATER
        /// <summary>The advancing data cursor; caller-owned managed storage must remain pinned on netstandard2.1.</summary>
        public ref byte Cursor;
#endif
#if !NET8_0_OR_GREATER
        /// <summary>The advancing data cursor; caller-owned managed storage must remain pinned on netstandard2.1.</summary>
        public byte* Cursor;
#endif
        private Span<byte> _buffer;
        private uint _bufferCheckLimit = uint.MaxValue;
        /// <summary>The first byte no unchecked record may reach: the storage end or the owner's growth threshold.</summary>
        /// <remarks>Cached with every storage or threshold change, so a record check is one subtraction from the cursor.</remarks>
#if NET8_0_OR_GREATER
        private ref byte _recordLimit;
#else
        private byte* _recordLimit;
#endif
        /// <summary>Gets the complete retained storage window used for dynamic bounds and framing patches.</summary>
        /// <remarks>Only the writer replaces its storage, so the cached record limit always describes it.</remarks>
        public Span<byte> Buffer {[MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get => _buffer; internal set { _buffer = value; RefreshRecordLimit(); } }

        /// <summary>Gets the cached owned-buffer growth threshold; fixed spans use the maximum value.</summary>
        public uint BufferCheckLimit {[MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get => _bufferCheckLimit; internal set { _bufferCheckLimit = value; RefreshRecordLimit(); } }

        /// <summary>Gets the optional growable output owner.</summary>
        public LuminBufferWriter? Owner {[MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get; internal set; }
        /// <summary>The explicit or materialized reference context, or null before an implicit scope requests state.</summary>
        /// <remarks>Use <see cref="OptionState"/> when reference tracking requires an initialized context.</remarks>
        public LuminPackWriterOptionalState? Options;

        #region Constructors

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        public LuminPackWriter(LuminPackWriterOptionalState? option = null)
        {
            Options = option;
            Buffer = default;
            Position = 0;
            RefreshRecordLimit();
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        /// <remarks>On netstandard2.1, caller-owned managed span storage must remain pinned for the complete operation lifetime.</remarks>
        public LuminPackWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState? option = null)
        {
            Options = option;
            Buffer = bufferReference;
            Position = 0;
            RefreshRecordLimit();
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        /// <param name = "option">An explicit shared reference context, or null to use the buffer owner's context.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackWriter(LuminBufferWriter? bufferWriter, LuminPackWriterOptionalState? option = null)
        {
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            Options = option ?? bufferWriter.WriterState;
            Buffer = bufferWriter.BeginCursorWrite(out int position);
            BufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
            Position = position;
            Owner = bufferWriter;
            RefreshRecordLimit();
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriter</c>.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LuminPackWriter(LuminBufferWriter bufferWriter)
        {
            Options = bufferWriter.WriterState;
            Buffer = bufferWriter.BeginCursorWrite(out int position);
            BufferCheckLimit = (uint)bufferWriter.BufferCheckLimit + 1;
            Position = position;
            Owner = bufferWriter;
            RefreshRecordLimit();
        }

        #endregion

        #region Cursor and helpers

        /// <summary>Reserves a dynamic framing or collection block at its absolute position.</summary>
        /// <param name = "position">The first byte of the complete dynamic block.</param>
        /// <param name = "length">The complete checked byte width.</param>
        /// <returns>The writable block after any required storage growth.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> Reserve(int position, int length)
        {
            EnsureCapacity(position, length);
            return LuminPackMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(position)), length);
        }

        /// <summary>Dereferences the architecture-specific advancing cursor inside shared implementation kernels.</summary>
        private ref byte Current {[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
#if NET8_0_OR_GREATER
            ref Cursor;
#else
            ref Unsafe.AsRef<byte>(Cursor);
#endif
        }

        /// <summary>Copies individual cursor fields into an isolated operation state without taking this state's address.</summary>
        /// <param name = "destination">The state used by an outlined compound operation.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyTo(ref LuminPackWriter destination)
        {
            destination.Options = Options;
#if NET8_0_OR_GREATER
            destination.Cursor = ref Cursor;
#else
            destination.Cursor = Cursor;
#endif
            destination.Owner = Owner;
            destination.BufferCheckLimit = BufferCheckLimit;
            destination.Buffer = Buffer;
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
                if (Options is null) Options = CreateReferenceState();
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
                if ((ulong)(uint)index + (uint)count <= (uint)Buffer.Length)
                    return;
            }
            else
            {
                if ((uint)index <= (uint)Buffer.Length && (uint)count <= (uint)(Buffer.Length - index))
                    return;
            }

            // The growth runs in a static helper that returns the refreshed cursor fields, so no call receives this writer's
            // address and an operation-local writer stays eligible for register promotion.
            Apply(GrowForCapacity(Owner, Position, index, count));
        }

        /// <summary>Grows the owning buffer for a capacity request outside the hot comparison.</summary>
        /// <param name = "owner">The owning native output buffer.</param>
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

        /// <summary>Grows native storage without passing the ref-struct state across a call boundary.</summary>
        /// <param name = "owner">The owning native output buffer.</param>
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
            uint limit = Math.Min((uint)Buffer.Length, BufferCheckLimit - 1);
#if NET8_0_OR_GREATER
            _recordLimit = ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)limit);
#else
            _recordLimit = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)) + limit;
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
            return (nint)(_recordLimit - Cursor);
#endif
        }

        /// <summary>Restores the hot cursor fields after native storage has grown.</summary>
        /// <param name = "storage">The cursor fields over the grown storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Apply(GrownStorage storage)
        {
#if NET8_0_OR_GREATER
            Cursor = ref storage.Cursor;
#else
            Cursor = storage.Cursor;
#endif
            Buffer = storage.Buffer;
            BufferCheckLimit = storage.CheckLimit;
            RefreshRecordLimit();
        }

        /// <summary>The cursor fields over grown native storage, returned by value so growth never receives a writer's address.</summary>
        private readonly ref struct GrownStorage
        {
#if NET8_0_OR_GREATER
            /// <summary>The cursor at the preserved position.</summary>
            public readonly ref byte Cursor;
#else
            /// <summary>The cursor at the preserved position.</summary>
            public readonly byte* Cursor;
#endif
            /// <summary>The complete grown storage window.</summary>
            public readonly Span<byte> Buffer;
            /// <summary>The owner's growth threshold after growth.</summary>
            public readonly uint CheckLimit;

            /// <summary>Captures the owner's current storage.</summary>
            /// <param name = "owner">The owning native output buffer.</param>
            /// <param name = "position">The preserved absolute output position.</param>
            public GrownStorage(LuminBufferWriter owner, int position)
            {
                byte* buffer = (byte*)owner.BufferPointer;
                Buffer = new Span<byte>(buffer, owner.TotalLength);
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.AsRef<byte>(buffer + position);
#else
                Cursor = buffer + position;
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
            if (LuminPackReader.IsOutOfRange(index, count, Buffer.Length))
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
            Cursor += count;
#endif
        }

        /// <summary>
        /// Advances the target pointer.
        /// </summary>
        /// <param name = "count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            EnsureWritableAdvance(Position, count);
#if NET8_0_OR_GREATER
            Cursor = ref Unsafe.Add(ref Cursor, (nint)(uint)count);
#else
            Cursor += count;
#endif
        }

        /// <summary>
        /// Advances the target pointer.
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
            RefreshRecordLimit();
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
            if (Owner is null) BufferCheckLimit = uint.MaxValue;
            RefreshRecordLimit();
            Position = position;
        }

        /// <summary>
        /// Releases the destination span and resets its cursor without modifying caller memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            Buffer = null;
            RefreshRecordLimit();
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
                RefreshRecordLimit();
            }
        }

        /// <summary>
        /// Returns the span currently held by the writer.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return Buffer[..Position];
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
            get => (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(Buffer), ref Cursor);
#else
            get => (int)(Cursor - (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)));
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)value);
#else
                Cursor = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)) + value;
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
            LuminPackWriter.Write<int>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)position), count);
        }

        #endregion

        #region Values

        /// <summary>Copies a proven fixed storage group and advances once without adding a Release bounds check.</summary>
        /// <param name = "source">The first byte of the proven adjacent fields.</param>
        /// <param name = "byteCount">The complete fixed storage width.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(scoped ref byte source, int byteCount)
        {
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Current, ref source, (uint)byteCount);
            Advance(byteCount);
        }

        /// <summary>Writes and advances past a null object header.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader()
        {
            Write(LuminPackCode.NullObject);
        }

        /// <summary>Writes and advances past a collection length.</summary>
        /// <param name = "length">The collection element count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCollectionHeader(int length)
        {
            Write(length);
        }

        /// <summary>Writes and advances past a null collection header.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullCollectionHeader()
        {
            Write(LuminPackCode.NullCollection);
        }

        /// <summary>Writes and advances past an object member count.</summary>
        /// <param name = "memberCount">The serialized member count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectHeader(byte memberCount)
        {
            Write(memberCount);
        }

        /// <summary>Writes and advances past a narrow Union tag.</summary>
        /// <param name = "tag">The declared narrow tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnionHeader(ushort tag)
        {
            Write((byte)tag);
        }

        /// <summary>Writes and advances past a narrow or wide Union tag.</summary>
        /// <param name = "tag">The declared Union tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteWideUnionHeader(ushort tag)
        {
            if (tag < LuminPackCode.WideTag)
            {
                WriteUnionHeader(tag);
                return;
            }

            ref byte current = ref Current;
            current = LuminPackCode.WideTag;
            global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref current, 1), tag);
            Advance(3);
        }

        /// <summary>Writes and advances past a null Union header.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullUnionHeader()
        {
            WriteNullObjectHeader();
        }

        /// <summary>Writes a reference marker and its variable-length object identifier.</summary>
        /// <param name = "referenceId">The identifier already registered in the operation state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectReferenceId(uint referenceId)
        {
            Write(LuminPackCode.ReferenceId);
            this.WriteVarInt(referenceId);
        }

        /// <summary>Writes contiguous storage with a caller-selected logical count, including bit-oriented containers.</summary>
        /// <typeparam name = "T">The reference-free element storage type.</typeparam>
        /// <param name = "value">The complete contiguous payload.</param>
        /// <param name = "count">The logical count recorded in the header.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(scoped ReadOnlySpan<T> value, int count)
            where T : unmanaged
        {
            int bytes = checked(value.Length * Unsafe.SizeOf<T>());
            int length = checked(sizeof(int) + bytes);
            ref byte source = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value));
            // The original reservation, borrowed-source lifetime and completed-position growth ordering are retained;
            // growth goes through the static helpers, so the writer's address stays unexposed.
            bool fits = FitsRecord(length);
            if (!fits) EnsureAdditionalCapacity(length);
            WritePayload(ref source, count, bytes);
            if (fits) Advance(length);
            else AdvanceSafe(length);
        }

        /// <summary>Copies an already reserved contiguous record without repeating capacity or completion checks.</summary>
        /// <param name="source">The first byte of the complete source storage.</param>
        /// <param name="count">The logical count written in the header.</param>
        /// <param name="bytes">The checked payload width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WritePayload(ref byte source, int count, int bytes)
        {
            ref byte current = ref Current;
            LuminPackWriter.Write<int>(ref current, count);
            if (bytes != 0)
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref current, sizeof(int)),
                    ref source, (uint)bytes);
        }

        /// <summary>Proves the complete record and following fixed-value headroom with one unsigned capacity comparison.</summary>
        /// <param name="length">The nonnegative complete record width.</param>
        /// <returns>Whether both existing dynamic boundary checks are already satisfied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FitsRecord(int length)
        {
            // The cached limit is min(storage, threshold - 1) from the base: one signed comparison proves both.
            return RecordDistance() >= (nint)(uint)length;
        }

        /// <summary>Writes a complete scalar storage value and advances the binary cursor.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <param name = "value">The complete scalar storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(in T value)
            where T : unmanaged
        {
            Write(ref Current, value);
            Advance(Unsafe.SizeOf<T>());
        }

        /// <summary>Forwards writable source storage to the single contiguous </summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved block.</param>
        /// <param name = "value">The source storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <returns>The actual complete byte width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(scoped Span<byte> destination, scoped Span<T> value, int count, bool header, bool compressed) => Write<T>(destination, (ReadOnlySpan<T>)value, count, header, compressed);

        /// <summary>Forwards writable source storage and reports its actual complete width.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved block.</param>
        /// <param name = "value">The source storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <param name = "bytes">Receives the actual complete byte width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(scoped Span<byte> destination, scoped Span<T> value, int count, bool header, bool compressed, out int bytes) => bytes = Write<T>(destination, (ReadOnlySpan<T>)value, count, header, compressed);

        /// <summary>Writes one contiguous record into its complete reservation.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved output block.</param>
        /// <param name = "value">The contiguous source storage.</param>
        /// <param name = "count">The selected element count, or minus one for null.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <returns>The actual complete byte width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(scoped Span<byte> destination, scoped ReadOnlySpan<T> value, int count, bool header, bool compressed)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(T));
            int prefix = header ? sizeof(int) : 0;
            if (header)
                Write(ref MemoryMarshal.GetReference(destination), count);
            if (count <= 0)
                return prefix;
            int bytes = checked(count * Unsafe.SizeOf<T>());
            ref byte source = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value));
            if (!compressed)
            {
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), (nint)(uint)(prefix)), ref source, (uint)bytes);
                return prefix + bytes;
            }

            int capacity = LuminCompressor.GetMaxCompressedSize(bytes);
            byte[] temporary = ArrayPool<byte>.Shared.Rent(capacity);
            try
            {
                int length = LuminCompressor.Compress(LuminPackMarshal.CreateReadOnlySpan(ref source, bytes), temporary.AsSpan(0, capacity));
                Write(ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), (nint)(uint)(prefix)), length);
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), (nint)(uint)(prefix + sizeof(int))), ref MemoryMarshal.GetReference(temporary.AsSpan()), (uint)length);
                return checked(prefix + sizeof(int) + length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temporary);
            }
        }

        /// <summary>Writes a reserved contiguous record and reports its actual byte width.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved output block.</param>
        /// <param name = "value">The contiguous source storage.</param>
        /// <param name = "count">The selected element count.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <param name = "bytes">Receives the actual complete byte width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(scoped Span<byte> destination, scoped ReadOnlySpan<T> value, int count, bool header, bool compressed, out int bytes) => bytes = Write(destination, value, count, header, compressed);

        /// <summary>Processes a fixed storage batch without introducing a Release bounds check.</summary>
        /// <param name = "reference">The first byte of the complete fixed storage batch.</param>
        /// <typeparam name = "T">The reference-free storage type.</typeparam>
        /// <param name = "value1">The field participating in the memory operation.</param>
        /// <returns>The complete packed wire width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(ref byte reference, in T value1)
        {
            if (Unsafe.SizeOf<IntPtr>() == 8 || Unsafe.SizeOf<T>() <= 4)
            {
                Unsafe.WriteUnaligned(ref reference, value1);
            }
            else
            {
                var view = UnmanagedViewModel.Create(value1);
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref reference, ref Unsafe.As<UnmanagedViewModel<T>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T>>());
            }

            return Unsafe.SizeOf<T>();
        }

        #endregion

        #region String

        /// <summary>Returns the fixed framing width or the variable-length header upper bound of the current mode.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength()
        {
            return StringRecordLength<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>
        /// Writes the string length header.
        /// </summary>
        /// <param name = "index"></param>
        /// <param name = "length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringRecordLengthHeader(int index, int length)
        {
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)), ref Unsafe.As<int, byte>(ref length), sizeof(int));
        }

        /// <summary>Writes null utf8 length header to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullUtf8LengthHeader(int index)
        {
            WriteEmptyOrNullUtf8LengthHeader(index, LuminPackCode.NullCollection);
        }

        /// <summary>Writes empty or null utf8 length header to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteEmptyOrNullUtf8LengthHeader(int index, int length)
        {
            EnsureCapacity(index, 8);
            // Both words belong to the record, even when the reader ignores the
            // character count. Initialize them together in a reused buffer.
            WriteReservedUtf8Header(index, length, length);
        }

        /// <summary>Writes utf8 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteUtf8WithToken(int index, string? value)
        {
            return WriteUtf8WithToken<global::LuminPack.Option.LuminPackDefaultOptions>(index, value);
        }

        /// <summary>Writes utf8 with length to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteUtf8WithLength(int index, string? value)
        {
            if (value is null)
            {
                WriteNullUtf8LengthHeader(index);
                return 0;
            }

            if (value.Length == 0)
            {
                WriteEmptyOrNullUtf8LengthHeader(index, 0);
                return 0;
            }

            var source = value.AsSpan();
#if NET8_0_OR_GREATER
            // Short ASCII records: encode straight into the reserved payload and store both header words at once,
            // without the status protocol's spilled out values or the worst-case width computation.
            if ((ulong)(uint)index + 8 + (uint)source.Length <= (uint)Buffer.Length &&
                ShortAsciiTranscoder.TryEncode(source, CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)index + 8), source.Length), escapeJson: false))
            {
                WriteReservedUtf8Header(index, source.Length, source.Length);
                return source.Length;
            }
#endif
            var startIndex = checked(index + 8);
            var dest = (uint)startIndex <= (uint)Buffer.Length ? Buffer.Slice(startIndex) : Span<byte>.Empty;
            // UTF8.GetMaxByteCount -> (length + 1) * 3
            var maxByteCount = checked((source.Length + 1) * 3);
            var status = StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(source, dest, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                {
                    EnsureCapacity(index, checked(maxByteCount + 8));
                    bytesWritten = EncodeUtf8Slow(source, Buffer.Slice(index + 8));
                }
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            WriteReservedUtf8Header(index, bytesWritten, source.Length);
            return bytesWritten;
        }

        /// <summary>Encodes a UTF-8 payload into storage that was grown to its worst-case width.</summary>
        /// <param name = "value">The source characters.</param>
        /// <param name = "destination">The payload storage after the reserved header.</param>
        /// <returns>The encoded payload width.</returns>
        /// <remarks>Static, so retrying after growth never takes the writer's address.</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int EncodeUtf8Slow(ReadOnlySpan<char> value, Span<byte> destination)
        {
            var status = StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(value, destination, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            return bytesWritten;
        }

        /// <summary>Writes utf16 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteUtf16WithToken(int index, string? value)
        {
            return WriteUtf16WithToken<global::LuminPack.Option.LuminPackDefaultOptions>(index, value);
        }

        /// <summary>Returns the size of the string header used by the current recording mode.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            // UTF-16 token mode uses a two-byte 0x0000 terminator because individual zero bytes are common within code units.
            var recording = default(TOptions).StringRecording;
            if (recording == LuminPackStringRecording.VarInt)
                return default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8 ? 10 : 5;
            return (recording == LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2) : (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 8 : 4;
        }

        /// <summary>
        /// Writes the string terminator.
        /// </summary>
        /// <param name = "index"></param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteStringRecordTokenHeader<TOptions>(int index)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                // UTF-8 token mode uses a single zero-byte terminator.
#if NET8_0_OR_GREATER
                Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)index) = 0;
#else
                Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(Buffer)), (nint)(uint)index) = 0;
#endif
            }
            else
            {
                // UTF-16 token mode terminates at the two-byte null character 0x0000.
                ushort token = 0;
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)), ref Unsafe.As<ushort, byte>(ref token), sizeof(ushort));
            }
        }

        /// <summary>Writes utf8 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int WriteUtf8WithToken<TOptions>(int index, string? value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                EnsureCapacity(index, 1);
                WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(index);
                return 0;
            }

            var destination = (uint)index <= (uint)Buffer.Length ? Buffer.Slice(index) : Span<byte>.Empty;
            var status = StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(value, destination, out var _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                {
                    EnsureCapacity(index, checked((value.Length + 1) * 3 + 1));
                    bytesWritten = EncodeUtf8Slow(value.AsSpan(), Buffer.Slice(index));
                }
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            EnsureCapacity(index, checked(bytesWritten + 1));
            var tokenIndex = index + bytesWritten;
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(tokenIndex);
            return bytesWritten;
        }

        /// <summary>Writes utf16 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int WriteUtf16WithToken<TOptions>(int index, string? value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                EnsureCapacity(index, 2);
                WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(index);
                return 0;
            }

            int bytesWritten = checked(value.Length * sizeof(char));
            EnsureCapacity(index, checked(bytesWritten + 2));
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytesWritten);
            var tokenIndex = index + bytesWritten;
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(tokenIndex);
            return bytesWritten;
        }

        /// <summary>Writes the original two-word UTF-8 length header with the required architecture-specific access.</summary>
        /// <param name = "index">The reserved header position.</param>
        /// <param name = "bytes">The payload byte count or original null marker.</param>
        /// <param name = "characters">The UTF-16 code-unit count or original null marker.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteReservedUtf8Header(int index, int bytes, int characters)
        {
            long header = (long)(uint)bytes | ((long)characters << 32);
            if (Unsafe.SizeOf<IntPtr>() == 8)
                LuminPackWriter.Write(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)), header);
            else
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(index)), ref Unsafe.As<long, byte>(ref header), 8);
        }

        /// <summary>Writes a complete string using the default compiled profile.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string? value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes a complete string record, including capacity, header and cursor advancement.</summary>
        /// <typeparam name = "TOptions">The stateless binary profile.</typeparam>
        /// <param name = "value">The string, including null or empty.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(string? value)
            where TOptions : struct, ILuminPackOptions => Write<TOptions>(value, capacityReserved: false);

        /// <summary>Writes a complete default-profile string within a generator-proven capacity region.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        /// <param name = "capacityReserved">Whether the complete enclosing record has already reserved its proven upper bound.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string? value, bool capacityReserved) => Write<LuminPackDefaultOptions>(value, capacityReserved);

        /// <summary>Writes and advances a string, optionally using the enclosing record's complete capacity proof.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "value">The string, including null or empty.</param>
        /// <param name = "capacityReserved">True only after reserving a proven bound for the complete enclosing operation.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(string? value, bool capacityReserved)
            where TOptions : struct, ILuminPackOptions
        {
            if (capacityReserved)
            {
                WriteReservedRecord<TOptions>(value);
                return;
            }

            bool utf16Length = false;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16)
            {
                var recording = default(TOptions).StringRecording;
                if (recording == LuminPackStringRecording.VarInt)
                {
                    WriteVarIntString<TOptions>(value, false);
                    return;
                }
                utf16Length = recording == LuminPackStringRecording.Length;
            }
            if (utf16Length)
            {
                // Room for the raw UTF-16 form; Latin-1 text then takes one byte per character. A string holds at
                // most 0x3FFFFFDF characters, so the payload and its header always fit in int.
                int recordLength = sizeof(int) + (value is null ? 0 : value.Length * sizeof(char));
                if (FitsRecord(recordLength))
                {
                    // The record check proved the raw bound, which covers every written form.
                    AdvanceProven(WriteUtf16Record(ref Current, value));
                    return;
                }

                // The static helpers keep this writer's address unexposed. An exactly sized destination may still
                // hold the compact record; otherwise the raw form's capacity is established first.
                int written = WriteUtf16RecordSlow(ref Current, Buffer.Length - Position, value);
                if (written < 0)
                {
                    EnsureAdditionalCapacity(recordLength);
                    written = WriteUtf16RecordSlow(ref Current, Buffer.Length - Position, value);
                }

                if (value is null) Advance(written);
                else AdvanceSafe(written);
                return;
            }

            int length = WriteEncodedString<TOptions>(value, out bool completeRecord);
            if (!completeRecord) length += StringRecordLength<TOptions>();
            if (value is null)
                Advance(length);
            else
                AdvanceSafe(length);
        }

        /// <summary>Writes consecutive string records using the default compiled profile.</summary>
        /// <param name = "values">The strings, each including null or empty.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStrings(scoped ReadOnlySpan<string?> values) => WriteStrings<LuminPackDefaultOptions>(values);

        /// <summary>Writes every string of a span as consecutive records, exactly as one record write per element.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "values">The strings, each including null or empty.</param>
        /// <remarks>UTF-16 length records are written in runs whose raw upper bounds fit the existing storage, with the
        /// same per-record capacity proof as a single write; a record that does not fit takes the single-record path,
        /// which grows the storage. Other profiles write each record through the single-record path.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStrings<TOptions>(scoped ReadOnlySpan<string?> values)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                int index = 0;
                while (true)
                {
                    uint limit = Math.Min((uint)Buffer.Length, BufferCheckLimit - 1);
                    uint position = (uint)Position;
                    Advance(WriteUtf16Run(ref Current, position < limit ? limit - position : 0, values, ref index));
                    if (index >= values.Length) return;
                    Write<TOptions>(values[index++]);
                }
            }

            foreach (string? value in values)
                Write<TOptions>(value);
        }

        /// <summary>Returns whether the default profile writes UTF-16 length records, the form of reserved runs.</summary>
        /// <returns>Whether reserved runs apply.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesUtf16LengthRecords() => UsesUtf16LengthRecords<LuminPackDefaultOptions>();

        /// <summary>Returns whether a profile writes UTF-16 length records, the form of reserved runs.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <returns>A compile-time constant for the profile.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesUtf16LengthRecords<TOptions>()
            where TOptions : struct, ILuminPackOptions
            => default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length;

        #region Records

        /// <summary>Returns the signed distance from a caller-held cursor to the record limit.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <returns>The bytes an unchecked record may use there, negative once the threshold is passed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly nint RecordDistance(ref byte cursor)
        {
#if NET8_0_OR_GREATER
            return Unsafe.ByteOffset(ref cursor, ref _recordLimit);
#else
            return (nint)(_recordLimit - (byte*)Unsafe.AsPointer(ref cursor));
#endif
        }

        /// <summary>Publishes a caller-held cursor as this writer's cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <remarks>Generated operations hold the cursor in a local while they write consecutive records and publish it before
        /// any operation that uses the writer itself and resume from the writer's cursor afterwards.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CommitRecords(ref byte cursor)
        {
#if NET8_0_OR_GREATER
            Cursor = ref cursor;
#else
            Cursor = (byte*)Unsafe.AsPointer(ref cursor);
#endif
        }

        /// <summary>Returns the room an unchecked record may use at a caller-held cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <returns>The available bytes, negative once the threshold is passed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int RecordRoom(ref byte cursor) => (int)Math.Min(RecordDistance(ref cursor), int.MaxValue);

        /// <summary>Ensures room for a record of known width at a caller-held cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "length">The record width.</param>
        /// <returns>The record position, followed by at least <paramref name = "length"/> writable bytes.</returns>
        /// <remarks>A record that does not fit publishes the cursor, applies the collection threshold and grows, exactly as a
        /// failed record check does, and continues at the published cursor.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte ReserveRecord(ref byte cursor, int length)
        {
            if (RecordDistance(ref cursor) >= (nint)(uint)length)
                return ref cursor;
            return ref GrowRecord(ref this, ref cursor, length);
        }

        /// <summary>Writes one complete fixed value at a caller-held cursor.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "value">The value.</param>
        /// <returns>The position after the value.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecord<T>(ref byte cursor, scoped in T value)
            where T : unmanaged
        {
            ref byte at = ref ReserveRecord(ref cursor, Unsafe.SizeOf<T>());
            Write(ref at, in value);
            return ref Unsafe.Add(ref at, (nint)Unsafe.SizeOf<T>());
        }

        /// <summary>Copies a proven contiguous fixed field block at a caller-held cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "source">The first byte of the block.</param>
        /// <param name = "length">The block width.</param>
        /// <returns>The position after the block.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordBlock(ref byte cursor, scoped ref byte source, int length)
        {
            ref byte at = ref ReserveRecord(ref cursor, length);
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref at, ref source, (uint)length);
            return ref Unsafe.Add(ref at, (nint)(uint)length);
        }

        /// <summary>Writes a string record at a caller-held cursor using the default compiled profile.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordString(ref byte cursor, string? value) => ref WriteRecordString<LuminPackDefaultOptions>(ref cursor, value);

        /// <summary>Writes a string record of the selected profile at a caller-held cursor.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        /// <remarks>A UTF-16 length record that fits its raw form is stored at the cursor, as the record check proves it. A record
        /// that does not fit and every other profile publish the cursor and take this writer's own string operation.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordString<TOptions>(ref byte cursor, string? value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                if (RecordDistance(ref cursor) >= (nint)(uint)Utf16RecordBound(value))
                    return ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedUtf16Record(ref cursor, value));
                return ref WriteRecordStringOutlined<TOptions>(ref this, ref cursor, value);
            }

            CommitRecords(ref cursor);
            Write<TOptions>(value);
            return ref Current;
        }

        /// <summary>Writes a raw contiguous array record at a caller-held cursor.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "array">The array, or null.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordSpan<T>(ref byte cursor, T[]? array)
            where T : unmanaged
        {
            ReadOnlySpan<T> elements = array;
            ref byte at = ref ReserveRecord(ref cursor, SpanRecordBound<T>(array is null, elements.Length));
            return ref Unsafe.Add(ref at, (nint)(uint)WriteReservedSpanOutlined(ref at, array is null, elements));
        }

        /// <summary>Writes a raw contiguous list record from one snapshot of its storage at a caller-held cursor.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "list">The list, or null.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordSpan<T>(ref byte cursor, List<T>? list)
            where T : unmanaged
        {
            ReadOnlySpan<T> elements = list is null ? default : LuminPackMarshal.GetListSpan(list);
            ref byte at = ref ReserveRecord(ref cursor, SpanRecordBound<T>(list is null, elements.Length));
            return ref Unsafe.Add(ref at, (nint)(uint)WriteReservedSpanOutlined(ref at, list is null, elements));
        }

        /// <summary>Writes a raw-pair dictionary record of its current count at a caller-held cursor.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "dictionary">The dictionary, or null.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordPairs<TKey, TValue>(ref byte cursor, Dictionary<TKey, TValue>? dictionary)
            where TKey : unmanaged where TValue : unmanaged
        {
            int count = dictionary is null ? 0 : dictionary.Count;
            ref byte at = ref ReserveRecord(ref cursor, PairRecordBound<TKey, TValue>(dictionary is null, count));
            return ref Unsafe.Add(ref at, (nint)(uint)WriteReservedRawPairs(ref at, dictionary, count));
        }

        /// <summary>Writes a raw contiguous record outside the calling root.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        /// <param name = "destination">The record position, followed by room for its exact width.</param>
        /// <param name = "isNull">Whether the collection is null.</param>
        /// <param name = "elements">The captured elements.</param>
        /// <returns>The written record width.</returns>
        /// <remarks>A bulk copy dominates its own call; keeping the size-dispatched copy out of every root preserves the root's
        /// inlining budget for its other members.</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteReservedSpanOutlined<T>(ref byte destination, bool isNull, ReadOnlySpan<T> elements)
            where T : unmanaged
            => WriteReservedSpan(ref destination, isNull, elements);

        /// <summary>Returns the raw upper bound of a string record of the selected profile.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The framing plus the widest encoding of every character.</returns>
        /// <remarks>UTF-16 records hold two bytes per character, UTF-8 at most three, so a reservation of this width covers every
        /// form <see cref = "WriteReservedRecordString{TOptions}(ref byte, string)"/> writes.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int RecordBound<TOptions>(string? value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            int length = value is null ? 0 : value.Length;
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            var recording = default(TOptions).StringRecording;
            if (recording == LuminPackStringRecording.VarInt)
                return utf8 ? checked(length * 3 + 10) : checked(length * sizeof(char) + 5);
            if (recording == LuminPackStringRecording.Token)
                return utf8 ? checked(length * 3 + 1) : length * sizeof(char) + sizeof(char);
            return utf8 ? checked(length * 3 + sizeof(int) * 2) : length * sizeof(char) + sizeof(int);
        }

        /// <summary>Returns the raw upper bound of a default-profile string record.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The framing plus the widest encoding of every character.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int RecordBound(string? value) => RecordBound<LuminPackDefaultOptions>(value);

        /// <summary>Writes a string record of the selected profile into room a reservation of its bound proved.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by at least the record's bound.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteReservedRecordString<TOptions>(ref byte cursor, string? value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
                return ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedUtf16Record(ref cursor, value));
            CommitRecords(ref cursor);
            WriteReservedRecord<TOptions>(value);
            return ref Current;
        }

        /// <summary>Writes a default-profile string record into room a reservation of its bound proved.</summary>
        /// <param name = "cursor">The caller's current position, followed by at least the record's bound.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteReservedRecordString(ref byte cursor, string? value) => ref WriteReservedRecordString<LuminPackDefaultOptions>(ref cursor, value);

        /// <summary>Stores one complete fixed value into reserved room.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the value.</param>
        /// <param name = "value">The value.</param>
        /// <returns>The position after the value.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReserved<T>(ref byte cursor, scoped in T value)
            where T : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)Write(ref cursor, in value));

        /// <summary>Copies a proven contiguous fixed field block into reserved room.</summary>
        /// <param name = "cursor">The caller's current position, followed by room for the block.</param>
        /// <param name = "source">The first byte of the block.</param>
        /// <param name = "length">The block width.</param>
        /// <returns>The position after the block.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReservedBlock(ref byte cursor, scoped ref byte source, int length)
        {
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref cursor, ref source, (uint)length);
            return ref Unsafe.Add(ref cursor, (nint)(uint)length);
        }

        /// <summary>Captures an array as one raw contiguous record.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "array">The array, or null.</param>
        /// <returns>The record's elements, null state and exact width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RawRecord<T> Capture<T>(T[]? array)
            where T : unmanaged
            => new RawRecord<T>(array, array is null);

        /// <summary>Captures one snapshot of a list's storage as a raw contiguous record.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "list">The list, or null.</param>
        /// <returns>The record's elements, null state and exact width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RawRecord<T> Capture<T>(List<T>? list)
            where T : unmanaged
            => new RawRecord<T>(list is null ? default : LuminPackMarshal.GetListSpan(list), list is null);

        /// <summary>Captures a raw-pair dictionary record with its current count.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "dictionary">The dictionary, or null.</param>
        /// <returns>The dictionary, its captured count and the record's exact width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RawPairsRecord<TKey, TValue> Capture<TKey, TValue>(Dictionary<TKey, TValue>? dictionary)
            where TKey : unmanaged where TValue : unmanaged
            => new RawPairsRecord<TKey, TValue>(dictionary, dictionary is null ? 0 : dictionary.Count);

        /// <summary>Writes a captured raw contiguous record into reserved room.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the record's width.</param>
        /// <param name = "record">The captured record.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReserved<T>(ref byte cursor, RawRecord<T> record)
            where T : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedSpanOutlined(ref cursor, record.IsNull, record.Elements));

        /// <summary>Writes a captured raw-pair dictionary record of exactly its captured count into reserved room.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the record's width.</param>
        /// <param name = "record">The captured record.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReserved<TKey, TValue>(ref byte cursor, RawPairsRecord<TKey, TValue> record)
            where TKey : unmanaged where TValue : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedRawPairs(ref cursor, record.Dictionary, record.Count));

        /// <summary>A raw contiguous record whose elements are read once for both its width and its stores.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly ref struct RawRecord<T>
            where T : unmanaged
        {
            /// <summary>The captured elements.</summary>
            public readonly ReadOnlySpan<T> Elements;
            /// <summary>Whether the collection was null.</summary>
            public readonly bool IsNull;

            /// <summary>Captures a record.</summary>
            /// <param name = "elements">The elements.</param>
            /// <param name = "isNull">Whether the collection was null.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawRecord(ReadOnlySpan<T> elements, bool isNull) { Elements = elements; IsNull = isNull; }

            /// <summary>Gets the record's exact width.</summary>
            public int Bound {[MethodImpl(MethodImplOptions.AggressiveInlining)] get => SpanRecordBound<T>(IsNull, Elements.Length); }
        }

        /// <summary>A raw-pair dictionary record whose count is read once for both its width and its stores.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly struct RawPairsRecord<TKey, TValue>
            where TKey : unmanaged where TValue : unmanaged
        {
            /// <summary>The dictionary, or null.</summary>
            public readonly Dictionary<TKey, TValue>? Dictionary;
            /// <summary>The captured pair count.</summary>
            public readonly int Count;

            /// <summary>Captures a record.</summary>
            /// <param name = "dictionary">The dictionary.</param>
            /// <param name = "count">The captured count.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawPairsRecord(Dictionary<TKey, TValue>? dictionary, int count) { Dictionary = dictionary; Count = count; }

            /// <summary>Gets the record's exact width.</summary>
            public int Bound {[MethodImpl(MethodImplOptions.AggressiveInlining)] get => PairRecordBound<TKey, TValue>(Dictionary is null, Count); }
        }

        /// <summary>Publishes a cursor, applies the collection threshold and grows for a record.</summary>
        /// <param name = "writer">The writer.</param>
        /// <param name = "cursor">The caller's current position.</param>
        /// <param name = "length">The record width.</param>
        /// <returns>The published cursor after growth.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref byte GrowRecord(ref LuminPackWriter writer, ref byte cursor, int length)
        {
            writer.CommitRecords(ref cursor);
            writer.CheckBuffer();
            writer.EnsureAdditionalCapacity(length);
            return ref writer.Current;
        }

        /// <summary>Publishes a cursor and writes a string that did not fit through the original operation.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "writer">The writer.</param>
        /// <param name = "cursor">The caller's current position.</param>
        /// <param name = "value">The string.</param>
        /// <returns>The position after the record.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref byte WriteRecordStringOutlined<TOptions>(ref LuminPackWriter writer, ref byte cursor, string? value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            writer.CommitRecords(ref cursor);
            writer.Write<TOptions>(value);
            return ref writer.Current;
        }

        #endregion

        /// <summary>Moves the cursor past records written into room a preceding record check proved.</summary>
        /// <param name = "written">The run's written width, at most its reserved bound.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompleteRun(int written) => AdvanceProven(written);

        /// <summary>Returns the raw upper bound of a UTF-16 length string record.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The header plus two bytes per character.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Utf16RecordBound(string? value) => sizeof(int) + (value is null ? 0 : value.Length * sizeof(char));

        /// <summary>Returns the exact width of a raw contiguous collection record.</summary>
        /// <typeparam name = "T">The raw element storage type.</typeparam>
        /// <param name = "isNull">Whether the collection is null.</param>
        /// <param name = "length">The captured element count.</param>
        /// <returns>The header plus the checked payload width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SpanRecordBound<T>(bool isNull, int length)
            where T : unmanaged
            => isNull ? sizeof(int) : checked(sizeof(int) + length * Unsafe.SizeOf<T>());

        /// <summary>Writes a UTF-16 length string record into reserved room of its raw bound.</summary>
        /// <param name = "destination">The record position, followed by room for its raw bound.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The written record width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteReservedUtf16Record(ref byte destination, string? value)
        {
#if NET8_0_OR_GREATER
            if (value is not null && Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(value.Length - 1) <= 15)
            {
                int header = StringSerializer.TryWriteShortLatin1Payload(ref Unsafe.Add(ref destination, sizeof(int)), value);
                if (header != 0)
                {
                    LuminPackWriter.Write(ref destination, header);
                    return sizeof(int) + value.Length;
                }
            }
#endif
            if (string.IsNullOrEmpty(value))
            {
                LuminPackWriter.Write<int>(ref destination, value is null ? LuminPackCode.NullCollection : 0);
                return sizeof(int);
            }

            // Every other record takes exactly one outlined call for its payload.
            int payloadHeader = StringSerializer.WriteUtf16RecordPayloadSlow(ref Unsafe.Add(ref destination, sizeof(int)), value);
            LuminPackWriter.Write(ref destination, payloadHeader);
            return sizeof(int) + StringSerializer.Utf16RecordPayload(payloadHeader);
        }

        /// <summary>Writes a raw contiguous collection record into reserved room.</summary>
        /// <typeparam name = "T">The raw element storage type.</typeparam>
        /// <param name = "destination">The record position, followed by room for its exact width.</param>
        /// <param name = "isNull">Whether the collection is null.</param>
        /// <param name = "elements">The captured elements.</param>
        /// <returns>The written record width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteReservedSpan<T>(ref byte destination, bool isNull, scoped ReadOnlySpan<T> elements)
            where T : unmanaged
        {
            if (isNull)
            {
                LuminPackWriter.Write<int>(ref destination, LuminPackCode.NullCollection);
                return sizeof(int);
            }

            int bytes = elements.Length * Unsafe.SizeOf<T>();
            LuminPackWriter.Write<int>(ref destination, elements.Length);
            if (bytes != 0)
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref destination, sizeof(int)),
                    ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(elements)), (uint)bytes);
            return sizeof(int) + bytes;
        }

        /// <summary>Returns the exact width of a raw-pair dictionary record.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "isNull">Whether the dictionary is null.</param>
        /// <param name = "count">The captured pair count.</param>
        /// <returns>The header plus the checked pair payload.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PairRecordBound<TKey, TValue>(bool isNull, int count)
            where TKey : unmanaged where TValue : unmanaged
            => isNull ? sizeof(int) : checked(sizeof(int) + count * (Unsafe.SizeOf<TKey>() + Unsafe.SizeOf<TValue>()));

        /// <summary>Writes a raw-pair dictionary record of exactly <paramref name = "count"/> pairs into reserved room.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "destination">The record position, followed by room for its exact width.</param>
        /// <param name = "dictionary">The dictionary, or null.</param>
        /// <param name = "count">The pair count captured with the bound.</param>
        /// <returns>The written record width.</returns>
        /// <remarks>Dense 64-bit storage copies the live entries and then checks the version, count and free count as the
        /// original dense path does; any other dictionary enumerates and must yield exactly the captured count.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int WriteReservedRawPairs<TKey, TValue>(ref byte destination, Dictionary<TKey, TValue>? dictionary, int count)
            where TKey : unmanaged where TValue : unmanaged
        {
            if (dictionary is null)
            {
                LuminPackWriter.Write<int>(ref destination, LuminPackCode.NullCollection);
                return sizeof(int);
            }

            LuminPackWriter.Write<int>(ref destination, count);
            int pair = Unsafe.SizeOf<TKey>() + Unsafe.SizeOf<TValue>();
            if (count == 0)
                return sizeof(int);
            if (Unsafe.SizeOf<IntPtr>() == 8)
            {
                var view = LuminPackMarshal.GetDictionaryView(dictionary);
                if (view._count == count && view._freeCount == 0)
                {
                    int version = view._version;
                    LuminPackMarshal.CopyDenseRawPairs(ref Unsafe.Add(ref destination, sizeof(int)), dictionary, count);
                    if (view._version != version || view._count != count || view._freeCount != 0)
                        LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
                    return sizeof(int) + count * pair;
                }
            }

            int written = 0;
            foreach (var item in dictionary)
            {
                if (written == count)
                    LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
                ref byte target = ref Unsafe.Add(ref destination, unchecked((nint)(uint)(sizeof(int) + written * pair)));
                LuminPackWriter.Write(ref target, item.Key);
                LuminPackWriter.Write(ref Unsafe.Add(ref target, unchecked((nint)(uint)(Unsafe.SizeOf<TKey>()))), item.Value);
                written++;
            }

            if (written != count)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            return sizeof(int) + count * pair;
        }

        /// <summary>Returns the room an unchecked record may use at the cursor: the storage and the collection threshold.</summary>
        /// <returns>The available bytes, negative once the threshold is passed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int RecordRoom() => (int)RecordDistance();

        /// <summary>Writes key and value strings as alternating records using the default compiled profile.</summary>
        /// <param name = "keys">The keys.</param>
        /// <param name = "values">The values, at least as many as the keys.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringPairs(scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) => WriteStringPairs<LuminPackDefaultOptions>(keys, values);

        /// <summary>Writes key and value strings as alternating records, exactly as a buffer check and two record writes per pair.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "keys">The keys.</param>
        /// <param name = "values">The values, at least as many as the keys.</param>
        /// <remarks>UTF-16 length pairs are written in runs whose raw upper bounds fit the existing storage, where the
        /// buffer check cannot act; a pair that does not fit takes the per-pair path.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringPairs<TOptions>(scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values)
            where TOptions : struct, ILuminPackOptions
        {
            values = values.Slice(0, keys.Length);
            int index = 0;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                while (true)
                {
                    uint limit = Math.Min((uint)Buffer.Length, BufferCheckLimit - 1);
                    uint position = (uint)Position;
                    Advance(WriteUtf16PairRun(ref Current, position < limit ? limit - position : 0, keys, values, ref index));
                    if (index >= keys.Length) return;
                    CheckBuffer();
                    Write<TOptions>(keys[index]);
                    Write<TOptions>(values[index++]);
                }
            }

            for (; index < keys.Length; index++)
            {
                CheckBuffer();
                Write<TOptions>(keys[index]);
                Write<TOptions>(values[index]);
            }
        }

        /// <summary>Writes UTF-16 length record pairs while the raw upper bound of the next pair fits the remaining room.</summary>
        /// <param name="current">The first record position.</param>
        /// <param name="room">The bytes available from <paramref name="current"/> under the capacity and completion limits.</param>
        /// <param name="keys">The keys.</param>
        /// <param name="values">The values, exactly as many as the keys.</param>
        /// <param name="index">The first pair to write, updated to the first pair not written.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteUtf16PairRun(ref byte current, uint room, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, ref int index)
        {
            ref string? firstKey = ref MemoryMarshal.GetReference(keys);
            ref string? firstValue = ref MemoryMarshal.GetReference(values);
            uint used = 0;
            int i = index;
            for (; (uint)i < (uint)keys.Length; i++)
            {
                string? key = Unsafe.Add(ref firstKey, (nint)(uint)i);
                string? value = Unsafe.Add(ref firstValue, (nint)(uint)i);
                // Two strings hold at most 2 * 0x3FFFFFDF characters, so the raw bound fits uint.
                uint bound = 2 * sizeof(int) + ((key is null ? 0u : (uint)key.Length) + (value is null ? 0u : (uint)value.Length)) * sizeof(char);
                if (bound > room - used) break;
                used += (uint)WriteUtf16Record(ref Unsafe.Add(ref current, (nint)used), key);
                used += (uint)WriteUtf16Record(ref Unsafe.Add(ref current, (nint)used), value);
            }

            index = i;
            return (int)used;
        }

        /// <summary>Writes UTF-16 length records while the raw upper bound of the next record fits the remaining room.</summary>
        /// <param name="current">The first record position.</param>
        /// <param name="room">The bytes available from <paramref name="current"/> under the capacity and completion limits.</param>
        /// <param name="values">The strings.</param>
        /// <param name="index">The first string to write, updated to the first string not written.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteUtf16Run(ref byte current, uint room, scoped ReadOnlySpan<string?> values, ref int index)
        {
            ref string? first = ref MemoryMarshal.GetReference(values);
            uint used = 0;
            int i = index;
            for (; (uint)i < (uint)values.Length; i++)
            {
                string? value = Unsafe.Add(ref first, (nint)(uint)i);
                // A string holds at most 0x3FFFFFDF characters, so the raw bound fits uint.
                uint bound = sizeof(int) + (value is null ? 0u : (uint)value.Length * sizeof(char));
                if (bound > room - used) break;
                used += (uint)WriteUtf16Record(ref Unsafe.Add(ref current, (nint)used), value);
            }

            index = i;
            return (int)used;
        }

        /// <summary>Writes a UTF-16 length record into the remaining storage when it fits.</summary>
        /// <param name="current">The record position.</param>
        /// <param name="room">The bytes available from <paramref name="current"/>.</param>
        /// <param name="value">The original nullable string.</param>
        /// <returns>The written record width, or -1 when the record needs more storage.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteUtf16RecordSlow(ref byte current, int room, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return room >= sizeof(int) ? WriteUtf16Record(ref current, value) : -1;
            int length = value.Length;
            if (room - sizeof(int) >= length * sizeof(char))
                return WriteUtf16Record(ref current, value);
            if (room - sizeof(int) >= length && StringSerializer.TryNarrowLatin1(ref MemoryMarshal.GetReference(value.AsSpan()), ref Unsafe.Add(ref current, sizeof(int)), length))
            {
                LuminPackWriter.Write(ref current, StringSerializer.Latin1Header(length));
                return sizeof(int) + length;
            }

            return -1;
        }

        /// <summary>Writes a UTF-16 length record after capacity for its raw form has been established.</summary>
        /// <param name="current">The record position.</param>
        /// <param name="value">The original nullable string.</param>
        /// <returns>The complete written record width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUtf16Record(ref byte current, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                LuminPackWriter.Write<int>(ref current, value is null ? LuminPackCode.NullCollection : 0);
                return sizeof(int);
            }

            int header = StringSerializer.WriteUtf16RecordPayload(ref Unsafe.Add(ref current, sizeof(int)), value);
            LuminPackWriter.Write(ref current, header);
            return sizeof(int) + StringSerializer.Utf16RecordPayload(header);
        }

        /// <summary>Encodes a complete string using an already reserved upper bound without a second capacity check.</summary>
        /// <typeparam name = "TOptions">The compile-time encoding and framing profile.</typeparam>
        /// <param name = "value">The original string, including null.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteReservedRecord<TOptions>(string? value)
            where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            var recording = default(TOptions).StringRecording;
            if (recording == LuminPackStringRecording.VarInt)
            {
                WriteVarIntString<TOptions>(value, true);
                return;
            }
            bool token = recording == LuminPackStringRecording.Token;
            int header = token ? 0 : utf8 ? 8 : 4;
            int bytes = 0;
            int utf16Header = 0;
            ref byte current = ref Current;
            if (!string.IsNullOrEmpty(value))
            {
                if (utf8)
                {
                    Span<byte> destination = Buffer.Slice(Position + header);
                    {
                        var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value.AsSpan(), destination, out _, out bytes, replaceInvalidSequences: false);
                        if (status != OperationStatus.Done)
                            LuminPackExceptionHelper.ThrowFailedEncoding(status);
                    }
                }
                else if (token)
                {
                    bytes = checked(value.Length * sizeof(char));
                    StringSerializer.SerializeUtf16(ref Unsafe.Add(ref current, (nint)(uint)(header)), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytes);
                }
                else
                {
                    utf16Header = StringSerializer.WriteUtf16RecordPayload(ref Unsafe.Add(ref current, (nint)(uint)(header)), value);
                    bytes = StringSerializer.Utf16RecordPayload(utf16Header);
                }
            }

            if (token)
            {
                Unsafe.Add(ref current, (nint)(uint)(bytes)) = 0;
                if (!utf8)
                    Unsafe.Add(ref current, (nint)(uint)(bytes + 1)) = 0;
                Advance(bytes + (utf8 ? 1 : 2));
            }
            else
            {
                int length = value is null ? LuminPackCode.NullCollection : utf8 ? bytes : utf16Header;
                LuminPackWriter.Write(ref current, length);
                if (utf8)
                    LuminPackWriter.Write(ref Unsafe.Add(ref current, sizeof(int)), value is null ? LuminPackCode.NullCollection : value.Length);
                Advance(bytes + header);
            }
        }

        /// <summary>Selects the non-default encoding kernel using a stateless profile.</summary>
        /// <typeparam name = "TOptions">The compile-time encoding and framing profile.</typeparam>
        /// <param name = "value">The string payload.</param>
        /// <param name="completeRecord">Whether the returned width includes variable-length framing.</param>
        /// <returns>The encoded payload width, including framing only when the complete-record flag is set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteEncodedString<TOptions>(string? value, out bool completeRecord)
            where TOptions : struct, ILuminPackOptions
        {
            completeRecord = false;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                var recording = default(TOptions).StringRecording;
                if (recording == LuminPackStringRecording.VarInt)
                {
                    completeRecord = true;
                    return WriteVarIntStringBytes<TOptions>(value, false);
                }
                return recording == LuminPackStringRecording.Length ? WriteUtf8WithLength(Position, value) : WriteUtf8WithToken<TOptions>(Position, value);
            }
            return WriteUtf16WithToken<TOptions>(Position, value);
        }

        /// <summary>Writes an opt-in variable-length string record while retaining capacity ownership.</summary>
        /// <typeparam name="TOptions">The stateless string profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <param name="capacityReserved">Whether the enclosing record has reserved the raw upper bound.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteVarIntString<TOptions>(string? value, bool capacityReserved) where TOptions : struct, ILuminPackOptions
            => AdvanceProven(WriteVarIntStringBytes<TOptions>(value, capacityReserved));

        /// <summary>Writes an exact variable-length record without advancing the owning cursor.</summary>
        /// <typeparam name="TOptions">The stateless string profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <param name="capacityReserved">Whether the enclosing record has reserved the raw upper bound.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteVarIntStringBytes<TOptions>(string? value, bool capacityReserved) where TOptions : struct, ILuminPackOptions
        {
            int length = VarIntRecordLength<TOptions>(value, out int payload);
            if (!capacityReserved && !FitsRecord(length)) EnsureAdditionalCapacity(length);
            WriteVarIntRecord<TOptions>(Buffer.Slice(Position, length), value, payload);
            return length;
        }

        /// <summary>Returns the binary profile tag without changing the four established tags.</summary>
        /// <typeparam name="TOptions">The stateless string profile.</typeparam>
        /// <returns>The distinct binary profile tag.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte BinaryProfile<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            var encoding = default(TOptions).StringEncoding;
            var recording = default(TOptions).StringRecording;
            return (byte)(recording == LuminPackStringRecording.VarInt ? 4 + (int)encoding : ((int)encoding << 1) | (int)recording);
        }


        /// <summary>Returns the unsigned base-128 width of a header plus one.</summary>
        /// <param name="header">The length header, or minus one for null.</param>
        /// <returns>One through five bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int VarIntHeaderLength(int header)
        {
            uint value = unchecked((uint)header + 1);
            return value < 128 ? 1 : value < 16384 ? 2 : value < 2097152 ? 3 : value < 268435456 ? 4 : 5;
        }


        /// <summary>Writes a header into its caller-proven complete range.</summary>
        /// <param name="destination">Room for the complete header.</param>
        /// <param name="header">The length header, or minus one for null.</param>
        /// <returns>The written byte count.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteVarIntHeader(Span<byte> destination, int header)
        {
            uint value = unchecked((uint)header + 1);
            int offset = 0;
            while (value >= 128) { destination[offset++] = (byte)(value | 128); value >>= 7; }
            destination[offset++] = (byte)value;
            return offset;
        }


        /// <summary>Calculates exact opt-in framing and payload sizes without changing the text representation.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <returns>The complete record byte count.</returns>
        internal static int VarIntRecordLength<TOptions>(string? value) where TOptions : struct, ILuminPackOptions
            => VarIntRecordLength<TOptions>(value, out _);


        /// <summary>Calculates framing once and retains the payload width for the following write.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <param name="payload">Receives the payload byte count.</param>
        /// <returns>The complete record byte count.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int VarIntRecordLength<TOptions>(string? value, out int payload) where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            payload = 0;
            if (string.IsNullOrEmpty(value)) return utf8 ? 2 : 1;
            payload = utf8 ? LuminPackMarshal.CalculateStringByteCount(value) : StringSerializer.Utf16RecordPayloadLength(value);
            int header = utf8 ? payload : payload == value.Length ? StringSerializer.Latin1Header(value.Length) : payload;
            return checked(payload + VarIntHeaderLength(header) + (utf8 ? VarIntHeaderLength(value.Length) : 0));
        }


        /// <summary>Writes an exact-sized opt-in record through the established bounded text kernels.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="destination">The complete exact-sized output range.</param>
        /// <param name="value">The nullable string.</param>
        /// <param name="payload">The exact payload width established by the size pass.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteVarIntRecord<TOptions>(Span<byte> destination, string? value, int payload) where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            if (string.IsNullOrEmpty(value))
            {
                int header = value is null ? -1 : 0;
                int offset = WriteVarIntHeader(destination, header);
                if (utf8) WriteVarIntHeader(destination.Slice(offset), header);
                return;
            }
            bool latin1 = !utf8 && payload == value.Length;
            int prefix = WriteVarIntHeader(destination, latin1 ? StringSerializer.Latin1Header(value.Length) : payload);
            if (utf8) prefix += WriteVarIntHeader(destination.Slice(prefix), value.Length);
            ref byte target = ref MemoryMarshal.GetReference(destination.Slice(prefix));
            if (utf8)
            {
                var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value, destination.Slice(prefix), out _, out int written, replaceInvalidSequences: false);
                if (status != OperationStatus.Done || written != payload) LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
            else if (latin1) StringSerializer.TryNarrowLatin1(ref MemoryMarshal.GetReference(value.AsSpan()), ref target, value.Length);
            else StringSerializer.SerializeUtf16(ref target, ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)payload);
        }

        #endregion

        #region Collections

        /// <summary>Applies the established collection growth threshold to the owning buffer outside the hot comparison.</summary>
        /// <param name = "owner">The owning native output buffer.</param>
        /// <param name = "position">The current absolute output position.</param>
        /// <param name = "index">The next element's actual byte position.</param>
        /// <returns>The cursor fields over the grown storage.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GrownStorage GrowForThreshold(LuminBufferWriter owner, int position, int index) => Grow(owner, position, index, threshold: true);

        /// <summary>Infers the element type while calculating a contiguous record reservation.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "value">The contiguous element storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <returns>The complete reservation width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>(scoped Span<T> value, int count, bool header, bool compressed) => Size<T>(count, header, compressed);

        /// <summary>Infers the element type while calculating a contiguous record reservation.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "value">The contiguous element storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <returns>The complete reservation width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>(scoped ReadOnlySpan<T> value, int count, bool header, bool compressed) => Size<T>(count, header, compressed);

        /// <summary>Calculates the complete reservation for one raw or compressed contiguous record.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "count">The element count, or minus one for null.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <returns>The complete maximum byte width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>(int count, bool header, bool compressed)
        {
            if (count < -1)
                LuminPackExceptionHelper.ThrowInvalidCollectionLength(count);
            int prefix = header ? sizeof(int) : 0;
            if (count <= 0)
                return prefix;
            int bytes = checked(count * Unsafe.SizeOf<T>());
            return checked(prefix + (compressed ? sizeof(int) + LuminCompressor.GetMaxCompressedSize(bytes) : bytes));
        }

        #endregion

        #region Framing

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(byte x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else
            {
                ref byte __fixed0Reference = ref Current;
                int __fixed0Length = 0;
                __fixed0Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, (nint)(uint)(__fixed0Length)), VarIntCodes.Byte);
                __fixed0Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, (nint)(uint)(__fixed0Length)), x);
                Advance(__fixed0Length);
            }
        }

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(sbyte x)
        {
            if (VarIntCodes.MinSingleValue <= x)
            {
                Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, x));
            }
            else
            {
                ref byte __fixed1Reference = ref Current;
                int __fixed1Length = 0;
                __fixed1Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed1Reference, (nint)(uint)(__fixed1Length)), VarIntCodes.SByte);
                __fixed1Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed1Reference, (nint)(uint)(__fixed1Length)), x);
                Advance(__fixed1Length);
            }
        }

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(ushort x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else
            {
                ref byte __fixed2Reference = ref Current;
                int __fixed2Length = 0;
                __fixed2Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed2Reference, (nint)(uint)(__fixed2Length)), VarIntCodes.UInt16);
                __fixed2Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed2Reference, (nint)(uint)(__fixed2Length)), (UInt16)x);
                Advance(__fixed2Length);
            }
        }

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(short x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else
                {
                    ref byte __fixed3Reference = ref Current;
                    int __fixed3Length = 0;
                    __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, (nint)(uint)(__fixed3Length)), VarIntCodes.Int16);
                    __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, (nint)(uint)(__fixed3Length)), (Int16)x);
                    Advance(__fixed3Length);
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    ref byte __fixed4Reference = ref Current;
                    int __fixed4Length = 0;
                    __fixed4Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed4Reference, (nint)(uint)(__fixed4Length)), VarIntCodes.SByte);
                    __fixed4Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed4Reference, (nint)(uint)(__fixed4Length)), (SByte)x);
                    Advance(__fixed4Length);
                }
                else
                {
                    ref byte __fixed5Reference = ref Current;
                    int __fixed5Length = 0;
                    __fixed5Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed5Reference, (nint)(uint)(__fixed5Length)), VarIntCodes.Int16);
                    __fixed5Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed5Reference, (nint)(uint)(__fixed5Length)), (Int16)x);
                    Advance(__fixed5Length);
                }
            }
        }

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(uint x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else if (x <= ushort.MaxValue)
            {
                ref byte __fixed6Reference = ref Current;
                int __fixed6Length = 0;
                __fixed6Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed6Reference, (nint)(uint)(__fixed6Length)), VarIntCodes.UInt16);
                __fixed6Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed6Reference, (nint)(uint)(__fixed6Length)), (UInt16)x);
                Advance(__fixed6Length);
            }
            else
            {
                ref byte __fixed7Reference = ref Current;
                int __fixed7Length = 0;
                __fixed7Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed7Reference, (nint)(uint)(__fixed7Length)), VarIntCodes.UInt32);
                __fixed7Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed7Reference, (nint)(uint)(__fixed7Length)), (UInt32)x);
                Advance(__fixed7Length);
            }
        }

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(int x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (x <= short.MaxValue)
                {
                    ref byte __fixed8Reference = ref Current;
                    int __fixed8Length = 0;
                    __fixed8Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed8Reference, (nint)(uint)(__fixed8Length)), VarIntCodes.Int16);
                    __fixed8Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed8Reference, (nint)(uint)(__fixed8Length)), (Int16)x);
                    Advance(__fixed8Length);
                }
                else
                {
                    ref byte __fixed9Reference = ref Current;
                    int __fixed9Length = 0;
                    __fixed9Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed9Reference, (nint)(uint)(__fixed9Length)), VarIntCodes.Int32);
                    __fixed9Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed9Reference, (nint)(uint)(__fixed9Length)), (Int32)x);
                    Advance(__fixed9Length);
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    ref byte __fixed10Reference = ref Current;
                    int __fixed10Length = 0;
                    __fixed10Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed10Reference, (nint)(uint)(__fixed10Length)), VarIntCodes.SByte);
                    __fixed10Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed10Reference, (nint)(uint)(__fixed10Length)), (SByte)x);
                    Advance(__fixed10Length);
                }
                else if (short.MinValue <= x)
                {
                    ref byte __fixed11Reference = ref Current;
                    int __fixed11Length = 0;
                    __fixed11Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed11Reference, (nint)(uint)(__fixed11Length)), VarIntCodes.Int16);
                    __fixed11Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed11Reference, (nint)(uint)(__fixed11Length)), (Int16)x);
                    Advance(__fixed11Length);
                }
                else
                {
                    ref byte __fixed12Reference = ref Current;
                    int __fixed12Length = 0;
                    __fixed12Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed12Reference, (nint)(uint)(__fixed12Length)), VarIntCodes.Int32);
                    __fixed12Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed12Reference, (nint)(uint)(__fixed12Length)), (Int32)x);
                    Advance(__fixed12Length);
                }
            }
        }

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(ulong x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else if (x <= ushort.MaxValue)
            {
                ref byte __fixed13Reference = ref Current;
                int __fixed13Length = 0;
                __fixed13Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed13Reference, (nint)(uint)(__fixed13Length)), VarIntCodes.UInt16);
                __fixed13Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed13Reference, (nint)(uint)(__fixed13Length)), (UInt16)x);
                Advance(__fixed13Length);
            }
            else if (x <= uint.MaxValue)
            {
                ref byte __fixed14Reference = ref Current;
                int __fixed14Length = 0;
                __fixed14Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed14Reference, (nint)(uint)(__fixed14Length)), VarIntCodes.UInt32);
                __fixed14Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed14Reference, (nint)(uint)(__fixed14Length)), (UInt32)x);
                Advance(__fixed14Length);
            }
            else
            {
                ref byte __fixed15Reference = ref Current;
                int __fixed15Length = 0;
                __fixed15Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed15Reference, (nint)(uint)(__fixed15Length)), VarIntCodes.UInt64);
                __fixed15Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed15Reference, (nint)(uint)(__fixed15Length)), (UInt64)x);
                Advance(__fixed15Length);
            }
        }

        /// <summary>Writes var int to the current output.</summary>
        /// <param name = "x">The x used by this operation.</param>
        public void WriteVarInt(long x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (x <= short.MaxValue)
                {
                    ref byte __fixed16Reference = ref Current;
                    int __fixed16Length = 0;
                    __fixed16Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed16Reference, (nint)(uint)(__fixed16Length)), VarIntCodes.Int16);
                    __fixed16Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed16Reference, (nint)(uint)(__fixed16Length)), (Int16)x);
                    Advance(__fixed16Length);
                }
                else if (x <= int.MaxValue)
                {
                    ref byte __fixed17Reference = ref Current;
                    int __fixed17Length = 0;
                    __fixed17Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed17Reference, (nint)(uint)(__fixed17Length)), VarIntCodes.Int32);
                    __fixed17Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed17Reference, (nint)(uint)(__fixed17Length)), (Int32)x);
                    Advance(__fixed17Length);
                }
                else
                {
                    ref byte __fixed18Reference = ref Current;
                    int __fixed18Length = 0;
                    __fixed18Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed18Reference, (nint)(uint)(__fixed18Length)), VarIntCodes.Int64);
                    __fixed18Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed18Reference, (nint)(uint)(__fixed18Length)), (Int64)x);
                    Advance(__fixed18Length);
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(global::LuminPack.Core.LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    ref byte __fixed19Reference = ref Current;
                    int __fixed19Length = 0;
                    __fixed19Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed19Reference, (nint)(uint)(__fixed19Length)), VarIntCodes.SByte);
                    __fixed19Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed19Reference, (nint)(uint)(__fixed19Length)), (SByte)x);
                    Advance(__fixed19Length);
                }
                else if (short.MinValue <= x)
                {
                    ref byte __fixed20Reference = ref Current;
                    int __fixed20Length = 0;
                    __fixed20Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed20Reference, (nint)(uint)(__fixed20Length)), VarIntCodes.Int16);
                    __fixed20Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed20Reference, (nint)(uint)(__fixed20Length)), (Int16)x);
                    Advance(__fixed20Length);
                }
                else if (int.MinValue <= x)
                {
                    ref byte __fixed21Reference = ref Current;
                    int __fixed21Length = 0;
                    __fixed21Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed21Reference, (nint)(uint)(__fixed21Length)), VarIntCodes.Int32);
                    __fixed21Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed21Reference, (nint)(uint)(__fixed21Length)), (Int32)x);
                    Advance(__fixed21Length);
                }
                else
                {
                    ref byte __fixed22Reference = ref Current;
                    int __fixed22Length = 0;
                    __fixed22Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed22Reference, (nint)(uint)(__fixed22Length)), VarIntCodes.Int64);
                    __fixed22Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed22Reference, (nint)(uint)(__fixed22Length)), (Int64)x);
                    Advance(__fixed22Length);
                }
            }
        }

        /// <summary>Writes the named version-tolerant header with the default binary string profile.</summary>
        /// <param name = "count">The number of table entries, at most 249.</param>
        public void WriteVersionHeader(byte count) => WriteVersionHeader<LuminPackDefaultOptions>(count);

        /// <summary>Writes the record marker, revision, binary string profile and member count.</summary>
        /// <typeparam name = "TOptions">The compiled binary profile.</typeparam>
        /// <param name = "count">The number of table entries, at most 249.</param>
        public void WriteVersionHeader<TOptions>(byte count)
            where TOptions : struct, ILuminPackOptions
        {
            byte profile = BinaryProfile<TOptions>();
            EnsureAdditionalCapacity(4);
            ref byte __fixed23Reference = ref Current;
            int __fixed23Length = 0;
            __fixed23Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed23Reference, (nint)(uint)(__fixed23Length)), (byte)251);
            __fixed23Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed23Reference, (nint)(uint)(__fixed23Length)), (byte)1);
            __fixed23Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed23Reference, (nint)(uint)(__fixed23Length)), profile);
            __fixed23Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed23Reference, (nint)(uint)(__fixed23Length)), count);
            Advance(__fixed23Length);
        }

        /// <summary>Copies the complete cached name and type table at its dynamic framing boundary.</summary>
        /// <param name = "metadata">The generated immutable metadata table.</param>
        /// <param name = "count">The number of payload lengths that follow the metadata.</param>
        public void WriteVersionMetadata(scoped ReadOnlySpan<byte> metadata, int count)
        {
            EnsureAdditionalCapacity(checked(metadata.Length + count * 5));
            metadata.CopyTo(Buffer.Slice(Position));
            Advance(metadata.Length);
        }

        #endregion
}
}
