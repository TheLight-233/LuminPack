using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Option;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Utility;



/// <summary>Provides a thread-safe pool of reusable <see cref="LuminBufferWriter"/> operation contexts.</summary>
/// <remarks>The pool is thread-safe. A writer rented from it is not thread-safe and must remain exclusively owned.</remarks>
public static class LuminBufferWriterPool
{
    /// <summary>Gets the largest pooled buffer retained by the pool.</summary>
    public const int MaxPooledBufferSize = 4 * 1024 * 1024; // 4MB
    /// <summary>Gets the maximum number of writers retained in the shared central slots.</summary>
    public const int MaxPoolSize = 32;

    // The thread's writer stays in this slot while it is rented; LuminBufferWriter._rented marks the lease. Renting and
    // returning it therefore touch the slot once and store no reference, so neither needs a GC write barrier.
    [ThreadStatic]
    private static LuminBufferWriter? t_first;

#if LUMINPACK_BUFFERWRITER_SECOND_TLS
    [ThreadStatic]
    private static LuminBufferWriter? t_second;
#endif

    private static readonly AtomicSlotPool<LuminBufferWriter> s_centralPool = new(MaxPoolSize);

    /// <summary>Rents a clean operation context for high-performance serialization or deserialization.</summary>
    /// <returns>A writer whose payload is empty, whose <see cref="LuminBufferWriter.WriterState"/> has default values,
    /// and whose writer and reader operation states contain no references from earlier uses.</returns>
    /// <remarks>The caller exclusively owns the result and must pass it to <see cref="Return"/> when finished.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminBufferWriter Rent()
    {
        var writer = t_first;
        // Acquire: a lease released on another thread published its reset before clearing the flag.
        if (writer is not null && !Volatile.Read(ref writer._rented))
        {
            writer._rented = true;
            return writer;
        }

        return RentSlow();
    }

    /// <summary>Obtains a reusable instance when the thread's writer is absent, disposed, or already rented.</summary>
    /// <returns>A rented writer that becomes the thread's cached writer.</returns>
    /// <remarks>An active thread writer, held by a nested or long-lived operation, is detached so that its eventual
    /// return reaches the shared slots; the replacement keeps later rents on this thread on the fast path.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter RentSlow()
    {
        var previous = t_first;
        if (previous is not null) previous._threadCached = false;

        LuminBufferWriter? writer = null;
#if LUMINPACK_BUFFERWRITER_SECOND_TLS
        writer = t_second;
        if (writer is not null) t_second = null;
#endif
        writer ??= s_centralPool.TryTake() ?? new LuminBufferWriter(true);
        writer._threadCached = true;
        writer._rented = true;
        t_first = writer;
        return writer;
    }

    /// <summary>Returns an exclusively owned writer to the pool.</summary>
    /// <param name="writer">The writer to reset and return.</param>
    /// <remarks>
    /// Return clears the published payload, writer reference tracking, reader reference tracking, and restores
    /// <see cref="LuminBufferWriter.WriterState"/> to LuminPack defaults. Oversized pooled buffers are disposed instead of cached.
    /// The instance must not be accessed after this call unless it is rented again.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(LuminBufferWriter writer)
    {
        writer.ResetForPool();

        if (writer._threadCached)
        {
            // The writer remains its thread's cached instance, so an oversized buffer is released rather than the writer.
            if (writer.TotalLength >= MaxPooledBufferSize) writer.ReleaseStorage();
            // Release: the reset above is visible before another rent observes the cleared flag.
            Volatile.Write(ref writer._rented, false);
            return;
        }

        ReturnSlow(writer);
    }

    /// <summary>Returns a writer that is not its thread's cached instance to the pool for reuse.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReturnSlow(LuminBufferWriter writer)
    {
        writer._rented = false;
        if (writer.TotalLength >= MaxPooledBufferSize)
        {
            writer.Dispose();
            return;
        }

        var first = t_first;
        if (first is null || first._disposed)
        {
            writer._threadCached = true;
            t_first = writer;
            return;
        }

#if LUMINPACK_BUFFERWRITER_SECOND_TLS
        if (t_second is null)
        {
            t_second = writer;
            return;
        }
#endif

        if (!s_centralPool.TryStore(writer))
            writer.Dispose();
    }

    /// <summary>Gets an approximate diagnostic count of writers currently held in central slots.</summary>
    public static int ApproximateCentralCount => s_centralPool.ApproximateCount;

    /// <summary>Disposes writers cached by the current thread and clears its local pool slots.</summary>
    /// <remarks>This diagnostic/maintenance API does not drain central slots owned by the process. A cached writer that
    /// is currently rented is only detached; its later return reaches the shared slots.</remarks>
    public static void ClearThreadLocalCache()
    {
        var first = t_first;
        t_first = null;
        if (first is not null)
        {
            first._threadCached = false;
            if (Volatile.Read(ref first._rented)) first = null;
        }

#if LUMINPACK_BUFFERWRITER_SECOND_TLS
        var second = t_second;
        t_second = null;
#endif

        first?.Dispose();
#if LUMINPACK_BUFFERWRITER_SECOND_TLS
        second?.Dispose();
#endif
    }

    /// <summary>Performs the clear central cache step for the current operation.</summary>
    internal static void ClearCentralCache() => s_centralPool.Drain(static writer => writer.Dispose());
}

/// <summary>
/// Owns a reusable, cache-line aligned managed byte buffer together with the writer and reader reference states used by
/// high-performance LuminPack operations.
/// </summary>
/// <remarks>
/// <para>A single instance is not thread-safe. From Rent until Return it must be owned by one call flow.</para>
/// <para>Do not concurrently serialize and deserialize with the same instance, and do not use it for same-instance
/// nested serialization. Rent a second writer for nested operations.</para>
/// <para><see cref="WriterState"/> remains configured for the entire Rent-to-Return lifetime. Returning the writer restores
/// both operation states. Serialization options are statically compiled.</para>
/// <para>Storage is one <see cref="ArrayPool{T}.Shared"/> array whose usable range starts at the first 64-byte boundary
/// observed when it is rented. On netstandard2.1 a pinning handle, taken only when an array is rented, keeps it stationary
/// for pointer cursors until the array is returned. Its logical capacity follows the 32 KiB initial size, 1.5x growth and 87.5 percent threshold;
/// rented arrays come in power-of-two buckets, so a growth that still fits the array extends the range in place without
/// copying. Every exposed byte was cleared or written by this owner. As an <see cref="IBufferWriter{T}"/>, the writer
/// appends after <see cref="CurrentIndex"/>.</para>
/// </remarks>
public sealed class LuminBufferWriter : IBufferWriter<byte>, IDisposable
{
    internal const int InitialBufferSize = 32 * 1024;
    internal const int GrowthNumerator = 3;
    internal const int GrowthDenominator = 2;
    private const int MinimumReservedHeadroom = 256;

    /// <summary>The cache-line size of the usable range's start.</summary>
    private const int Alignment = 64;

    /// <summary>The largest byte array length the runtime accepts.</summary>
    internal const int MaxArrayLength = 0x7FFFFFC7;

    /// <summary>The storage array; an empty array stands for released storage, so the range accessors need no null test.</summary>
    private byte[] _buffer = [];

    /// <summary>The array index of the first usable byte.</summary>
    private int _offset;

    /// <summary>The logical capacity.</summary>
    private int _capacity;

    /// <summary>The established 87.5 percent growth threshold of <see cref="_capacity"/>.</summary>
    private int _threshold;

    internal int _writtenCount;

    /// <summary>The address of a live external write index, or zero once a length is published.</summary>
    private nint _liveIndex;

#if !NET8_0_OR_GREATER
    /// <summary>Keeps the rented array stationary for netstandard2.1 pointer cursors, from rent until return.</summary>
    /// <remarks>Allocated only when storage is rented or relocated, never on a write or a cursor bind.</remarks>
    private GCHandle _pin;
#endif

    internal bool _disposed;

    /// <summary>Whether this writer is the instance held by its thread's pool slot.</summary>
    internal bool _threadCached;

    /// <summary>Whether a pool lease currently owns this writer.</summary>
    internal bool _rented;

    /// <summary>Gets the writer operation state owned by this buffer.</summary>
    public readonly LuminPackWriterOptionalState WriterState;

    /// <summary>Gets the reader operation state owned by this buffer.</summary>
    public readonly LuminPackReaderOptionalState ReaderState;

    /// <summary>Creates a reusable writer and operation context.</summary>
    /// <param name="useFirstBuffer">Whether to acquire the initial buffer immediately.</param>
    /// <remarks>Directly constructed instances should be disposed. Pool users should call <see cref="LuminBufferWriterPool.Return"/>.</remarks>
    public LuminBufferWriter(bool useFirstBuffer)
    {
        WriterState = new LuminPackWriterOptionalState();
        ReaderState = new LuminPackReaderOptionalState();
        if (useFirstBuffer)
            Grow(InitialBufferSize);
    }

    /// <summary>Gets the current buffer growth threshold.</summary>
    internal int BufferCheckLimit => _threshold;

    /// <summary>Gets the number of bytes currently published as the writer's payload.</summary>
    public int CurrentIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _liveIndex == 0 || _writtenCount != 0 ? _writtenCount : RefAt<int>(_liveIndex);
    }

    /// <summary>Gets the logical capacity of the buffer in bytes.</summary>
    public int TotalLength => _capacity;

    /// <summary>Gets whether this writer currently owns storage.</summary>
    public bool UseFirstBuffer => _capacity != 0;

    /// <summary>Gets the first usable byte; a reference to the empty array's data without storage.</summary>
    private ref byte Data
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref GetNotNullArrayReference(_buffer), (nint)(uint)_offset);
    }

    /// <summary>Gets the complete logical range.</summary>
    private Span<byte> Window
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref Data, _capacity);
    }

    #region IBufferWriter

    /// <summary>Publishes <paramref name="count"/> bytes written after <see cref="CurrentIndex"/>.</summary>
    /// <param name="count">The number of bytes written into the last returned span or memory.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        int index = CurrentIndex;
        if ((uint)count > (uint)(_capacity - index))
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(count));
        _writtenCount = index + count;
        _liveIndex = 0;
    }

    /// <summary>Returns writable memory after <see cref="CurrentIndex"/>.</summary>
    /// <param name="sizeHint">The minimum length; zero requests a nonempty buffer.</param>
    /// <returns>The writable memory.</returns>
    Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
    {
        int index = ReserveFree(sizeHint);
        return new Memory<byte>(_buffer, _offset + index, _capacity - index);
    }

    /// <summary>Returns a writable span after <see cref="CurrentIndex"/>.</summary>
    /// <param name="sizeHint">The minimum length; zero requests a nonempty buffer.</param>
    /// <returns>The writable span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
    {
        int index = ReserveFree(sizeHint);
        return MemoryMarshal.CreateSpan(ref Unsafe.Add(ref Data, (nint)(uint)index), _capacity - index);
    }

    /// <summary>Ensures free capacity after the payload for the buffer-writer contract.</summary>
    /// <param name="sizeHint">The requested free bytes.</param>
    /// <returns>The current payload length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReserveFree(int sizeHint)
    {
        int index = CurrentIndex;
        int free = _capacity - index;
        // A negative hint also exceeds the free range as an unsigned value and is rejected on the growth path.
        if ((uint)sizeHint > (uint)free || free == 0)
            GrowForHint(index, sizeHint);
        return index;
    }

    /// <summary>Validates a size hint and grows until it fits.</summary>
    /// <param name="index">The current payload length.</param>
    /// <param name="sizeHint">The requested free bytes.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowForHint(int index, int sizeHint)
    {
        if (sizeHint < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(sizeHint));
        EnsureCapacity(checked(index + Math.Max(sizeHint, 1)));
    }

    #endregion

    #region Payload access

    /// <summary>Gets the complete writable buffer without limiting the span to the published payload.</summary>
    /// <returns>A span over the current logical capacity.</returns>
    /// <remarks>The span is invalidated by resize, reset that frees storage, Return, or Dispose. Callers must publish a
    /// valid length separately and must not retain the span.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> DangerousGetBuffer() => Window;

    /// <summary>Copies the published payload from <paramref name="index"/> into newly allocated managed memory.</summary>
    /// <param name="index">The zero-based payload offset at which copying begins.</param>
    /// <returns>A new memory block containing the requested payload suffix.</returns>
    [Obsolete("This method causes GC allocations. Avoid calling it frequently; consider using GetSpan instead.")]
    public Memory<byte> GetMemory(int index = 0)
    {
        if (_capacity == 0)
            Grow(InitialBufferSize);

        if ((uint)index > (uint)CurrentIndex)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(index));

        return new Memory<byte>(GetSpan().Slice(index).ToArray());
    }

    /// <summary>Gets the currently published payload without allocation.</summary>
    /// <returns>A span whose length is <see cref="CurrentIndex"/>.</returns>
    /// <remarks>The span is borrowed and is invalidated by subsequent writes, resize, Return, or Dispose.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan() => Window.Slice(0, CurrentIndex);

    /// <summary>Gets the complete writable buffer, acquiring the initial storage if necessary.</summary>
    /// <returns>A span over the full current capacity.</returns>
    /// <remarks>This is a low-level borrowed span and is invalidated by resize, Return, or Dispose.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetFullSpan()
    {
        if (_capacity == 0)
            Grow(InitialBufferSize);
        return Window;
    }

    /// <summary>Copies input into owned storage and publishes its length.</summary>
    /// <param name="source">The complete input payload.</param>
    public void LoadSpan(ReadOnlySpan<byte> source)
    {
        ResetCore();
        EnsureCapacity(source.Length);
        source.CopyTo(GetFullSpan());
        CompleteWrite(source.Length);
    }

    /// <summary>Copies a raw sequence into this reusable buffer and publishes its length.</summary>
    /// <remarks>Invalidates previous borrowed spans. Oversized buffers follow the normal pool retention limit.</remarks>
    /// <param name="source">The input data to process.</param>
    public void LoadSequence(in ReadOnlySequence<byte> source)
    {
        int length = checked((int)source.Length);
        ResetCore();
        EnsureCapacity(length);
        source.CopyTo(GetFullSpan().Slice(0, length));
        CompleteWrite(length);
    }

    #endregion

    #region Cursor sessions

    /// <summary>Associates the buffer with an externally managed write index.</summary>
    /// <param name="index">The stationary index whose address remains valid while the writer uses the buffer.</param>
    /// <remarks>This low-level API is intended for LuminPack writer implementations. The reference must not outlive its scope.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCurrentIndexPtr(ref int index) => _liveIndex = AddressOf(ref index);

    /// <summary>Begins an appended record without resetting its existing payload or reference state.</summary>
    /// <param name="index">Receives the current absolute position and supplies the active writer's cursor storage.</param>
    /// <returns>The full writable allocation, including the existing payload prefix.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<byte> BeginWrite(out int index)
    {
        var span = BeginCursorWrite(out index);
        _writtenCount = 0;
        _liveIndex = AddressOf(ref index);
        return span;
    }

    /// <summary>Prepares an append operation whose cursor stays inside the binary writer.</summary>
    /// <param name="index">Receives the absolute start of the appended record.</param>
    /// <returns>The complete allocation after restoring fixed-record headroom.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<byte> BeginCursorWrite(out int index)
    {
        index = CurrentIndex;
        if (_capacity == 0)
            Grow(InitialBufferSize);
        // An append stream must support the same initial fixed record as a fresh writer.
        else if (index != 0 && (index > _threshold || _capacity - index < InitialBufferSize))
            PrepareAppend(index);
        return Window;
    }

    /// <summary>Restores fresh-writer fixed-record headroom at the append container boundary.</summary>
    /// <param name="index">The absolute end of the existing payload.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PrepareAppend(int index)
    {
        Grow(CalculateExpandedCapacity(_capacity, checked(index + InitialBufferSize), reserveWriteHeadroom: true));
    }

    /// <summary>Publishes <paramref name="count" /> bytes written by a LuminPack writer.</summary>
    /// <param name="count">The number of entries involved in the operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteWrite(int count)
    {
        _writtenCount = count;
        _liveIndex = 0;
    }

    /// <summary>Ensures capacity for the current binary writer position without advancing it.</summary>
    /// <param name="writer">The binary writer whose buffer reference is refreshed after a resize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackWriter writer)
    {
        if (writer.Position > _threshold) // 87.5% load threshold.
            GrowAndRefresh(ref writer);
    }

    /// <summary>Ensures capacity for the current JSON writer position without advancing it.</summary>
    /// <param name="writer">The JSON writer whose buffer reference is refreshed after a resize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackJsonWriter writer)
    {
        if (writer.Position > _threshold) // 87.5% load threshold.
            GrowAndRefresh(ref writer);
    }

    /// <summary>Grows past the threshold and refreshes the binary writer's storage window.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndRefresh(ref LuminPackWriter writer)
    {
        Grow(CalculateExpandedCapacity(_capacity, _capacity, reserveWriteHeadroom: false));
        writer.FlushBuffer();
    }

    /// <summary>Grows past the threshold and refreshes the JSON writer's storage window.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndRefresh(ref LuminPackJsonWriter writer)
    {
        Grow(CalculateExpandedCapacity(_capacity, _capacity, reserveWriteHeadroom: false));
        writer.FlushBuffer();
    }

    /// <summary>Applies the established collection headroom threshold without escaping binary writer state.</summary>
    /// <param name="position">The successful absolute collection cursor.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckPosition(int position)
    {
        if (position > _threshold)
            Grow(CalculateExpandedCapacity(_capacity, _capacity, reserveWriteHeadroom: false));
    }

    #endregion

    #region Output

    /// <summary>Copies the published payload to a new byte array and resets the published buffer length.</summary>
    /// <returns>A newly allocated array, or an empty array when no bytes are published.</returns>
    /// <remarks>This method produces result GC allocation. It resets bytes while preserving the storage.</remarks>
    public byte[] ToArrayAndReset()
    {
        var length = CurrentIndex;
        if (length == 0) return [];

        var result = AllocateUninitializedArray<byte>(length);
        GetSpan().CopyTo(result);
        ResetCore();
        return result;
    }

    /// <summary>Compresses the published payload in place.</summary>
    /// <returns>The compressed payload length now published by this writer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compress()
    {
        var span = GetSpan();
        var dest = ArrayPool<byte>.Shared.Rent(LuminCompressor.GetMaxCompressedSize(span.Length));
        try
        {
            int compressedSize = LuminCompressor.Compress(span, dest);
            EnsureCapacity(compressedSize);
            dest.AsSpan(0, compressedSize).CopyTo(Window);
            PublishLength(compressedSize);
            return compressedSize;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dest);
        }
    }

    /// <summary>Decompresses the published payload in place.</summary>
    /// <returns>The decompressed payload length now published by this writer.</returns>
    public int Decompress()
    {
        var span = GetSpan();
        var dest = ArrayPool<byte>.Shared.Rent(LuminCompressor.GetDecompressedSize(span));
        try
        {
            int decompressedSize = LuminCompressor.Decompress(span, dest);
            EnsureCapacity(decompressedSize);
            dest.AsSpan(0, decompressedSize).CopyTo(Window);
            PublishLength(decompressedSize);
            return decompressedSize;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dest);
        }
    }

    /// <summary>Compresses this writer's payload into another writer.</summary>
    /// <param name="destination">The distinct writer that receives the compressed payload.</param>
    /// <returns>The compressed byte count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompressTo(LuminBufferWriter destination)
    {
        return LuminCompressor.Compress(this, destination);
    }

    /// <summary>Compresses into another writer and resets this writer's published payload afterward.</summary>
    /// <param name="destination">The distinct writer that receives the compressed payload.</param>
    /// <returns>The compressed byte count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompressToAndReset(LuminBufferWriter destination)
    {
        if (ReferenceEquals(this, destination))
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentException("Source and destination writers must be different instances.", nameof(destination));
        try
        {
            return LuminCompressor.Compress(this, destination);
        }
        finally
        {
            ResetCore();
        }
    }

    /// <summary>Decompresses this writer's payload into another writer.</summary>
    /// <param name="destination">The distinct writer that receives the decompressed payload.</param>
    /// <returns>The decompressed byte count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecompressTo(LuminBufferWriter destination)
    {
        return LuminCompressor.Decompress(this, destination);
    }

    /// <summary>Decompresses into another writer and resets this writer's published payload afterward.</summary>
    /// <param name="destination">The distinct writer that receives the decompressed payload.</param>
    /// <returns>The decompressed byte count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecompressToAndReset(LuminBufferWriter destination)
    {
        if (ReferenceEquals(this, destination))
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentException("Source and destination writers must be different instances.", nameof(destination));
        try
        {
            return LuminCompressor.Decompress(this, destination);
        }
        finally
        {
            ResetCore();
        }
    }

    /// <summary>Copies the published payload to a binary writer and then resets this buffer's payload.</summary>
    /// <param name="writer">The destination binary writer.</param>
    /// <remarks>Reserves the complete destination before copying. A failed reservation leaves the source payload intact.</remarks>
    public void WriteToAndReset(ref LuminPackWriter writer)
    {
        var length = CurrentIndex;
        if (length == 0) return;

        writer.EnsureAdditionalCapacity(length);
        CopyBlockUnaligned(ref writer.CurrentReference, ref Data, (uint)length);
        writer.Advance(length);
        ResetCore();
    }

    /// <summary>Asynchronously copies the published payload to a stream and then resets this buffer's payload.</summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">A token that can cancel the stream write.</param>
    /// <returns>A task-like value that completes after the write.</returns>
    /// <remarks>The stream reads the owned array directly; the writer must stay unchanged until the task completes.</remarks>
    public async ValueTask WriteToAndResetAsync(Stream stream, CancellationToken cancellationToken)
    {
        var length = CurrentIndex;
        if (length == 0) return;

        await stream.WriteAsync(new ReadOnlyMemory<byte>(_buffer, _offset, length), cancellationToken).ConfigureAwait(false);
        ResetCore();
    }

    #endregion

    #region Reset and lifetime

    /// <summary>Resets the published byte count without releasing the storage or changing <see cref="WriterState"/>.</summary>
    /// <remarks>This low-level reset does not clear reference state. Use <see cref="ResetForReuse"/> for an independent session,
    /// or <see cref="LuminBufferWriterPool.Return"/> when returning a pooled buffer.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetCore()
    {
        _writtenCount = 0;
        _liveIndex = 0;
    }

    /// <summary>Clears the payload and both reference tables while retaining the storage capacity for reuse.</summary>
    /// <remarks>Call this before starting an independent serialization session or after a failed append.
    /// Serialization into this buffer never resets its contents or reference tables automatically.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForReuse()
    {
        ResetCore();
        WriterState.ResetOperationState();
        ReaderState.ResetOperationState();
    }

    /// <summary>Clears the complete context before returning it to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetForPool()
    {
        ResetForReuse();
    }

    /// <summary>Clears both reference tables and returns a nonempty payload's storage to the array pool.</summary>
    /// <remarks>This does not return the instance to the pool. Use <see cref="ResetForReuse"/> to retain the capacity.</remarks>
    public void Reset()
    {
        if (CurrentIndex != 0) ReleaseStorage();
        ResetForReuse();
    }

    /// <summary>Returns the storage and clears the reference state owned by this instance.</summary>
    /// <remarks>Pool-rented instances should normally be returned through <see cref="LuminBufferWriterPool.Return"/>.</remarks>
    public void Dispose()
    {
        if (_disposed) return;

        ResetForPool();
        ReleaseStorage();
        _disposed = true;
#if !NET8_0_OR_GREATER
        GC.SuppressFinalize(this);
#endif
    }

#if !NET8_0_OR_GREATER
    /// <summary>Frees the pinning handle of a writer that was never disposed or returned.</summary>
    ~LuminBufferWriter()
    {
        ReleaseStorage();
    }
#endif

    /// <summary>Returns the storage while keeping the writer usable; the next write acquires the initial capacity.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void ReleaseStorage()
    {
        var buffer = _buffer;
        _buffer = [];
        _offset = 0;
        _capacity = 0;
        _threshold = 0;
#if !NET8_0_OR_GREATER
        if (_pin.IsAllocated) _pin.Free();
#endif
        if (buffer.Length != 0) ArrayPool<byte>.Shared.Return(buffer);
    }

    #endregion

    #region Capacity

    /// <summary>
    /// Ensures the buffer is at least <paramref name="minCapacity"/> bytes, acquiring or growing storage as needed.
    /// Called by <see cref="LuminCompressor"/> before writing compressed/decompressed output.
    /// </summary>
    /// <param name="minCapacity">The min capacity used by this operation.</param>
    internal void EnsureCapacity(int minCapacity)
    {
        if (minCapacity < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(minCapacity));

        if (_capacity < minCapacity)
            Grow(CalculateExpandedCapacity(_capacity, minCapacity, reserveWriteHeadroom: true));
    }

    /// <summary>Grows the logical capacity, preserving every existing byte and clearing the added range.</summary>
    /// <param name="capacity">The new logical capacity.</param>
    /// <remarks>Growth inside the current array keeps its storage; otherwise the bytes move to a larger rented array and the
    /// previous array is unpinned and returned only after the copy. Renting and pinning are the only steps that can fail.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int capacity)
    {
        int current = _capacity;
        if (capacity <= current) return;

        var buffer = _buffer;
        if (capacity > buffer.Length - _offset)
        {
            // The slack lets the usable range start on a cache-line boundary; only the largest arrays forgo it.
            var next = ArrayPool<byte>.Shared.Rent(capacity <= MaxArrayLength - (Alignment - 1) ? capacity + (Alignment - 1) : capacity);
#if !NET8_0_OR_GREATER
            // Pin before the offset is measured, so pointer cursors and the alignment stay valid until the array is returned.
            GCHandle pin;
            try { pin = GCHandle.Alloc(next, GCHandleType.Pinned); }
            catch (Exception exception)
            {
                ArrayPool<byte>.Shared.Return(next);
                LuminPack.Code.LuminPackExceptionHelper.Rethrow(exception);
                return;
            }
#endif
            ref byte start = ref GetNotNullArrayReference(next);
            int offset = (int)(-AddressOf(ref start) & (Alignment - 1));
            if (next.Length - offset < capacity) offset = 0;
            if (current != 0) CopyBlockUnaligned(ref Unsafe.Add(ref start, (nint)(uint)offset), ref Data, (uint)current);
            _buffer = next;
            _offset = offset;
#if !NET8_0_OR_GREATER
            if (_pin.IsAllocated) _pin.Free();
            _pin = pin;
#endif
            if (buffer.Length != 0) ArrayPool<byte>.Shared.Return(buffer);
        }

        MemoryMarshal.CreateSpan(ref Unsafe.Add(ref Data, (nint)(uint)current), capacity - current).Clear();
        _capacity = capacity;
        _threshold = capacity - (capacity >> 3);
    }

    /// <summary>Calculates the serialized size contribution of expanded capacity.</summary>
    /// <param name="currentCapacity">The current capacity used by this operation.</param>
    /// <param name="minCapacity">The min capacity used by this operation.</param>
    /// <param name="reserveWriteHeadroom">The reserve write headroom used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CalculateExpandedCapacity(
        int currentCapacity,
        int minCapacity,
        bool reserveWriteHeadroom)
    {
        long geometricGrowth = (long)currentCapacity * GrowthNumerator / GrowthDenominator;
        long requiredCapacity = minCapacity;

        if (reserveWriteHeadroom && minCapacity > currentCapacity)
        {
            // A one-sixth reserve keeps the requested payload below the 87.5% check
            // threshold without the 25% slack previously retained by bulk writes.
            requiredCapacity += Math.Max((long)minCapacity / 6, MinimumReservedHeadroom);
        }

        long capacity = Math.Max(InitialBufferSize, Math.Max(geometricGrowth, requiredCapacity));
        // Managed storage ends at the runtime's array limit; a larger explicit request fails when it is allocated.
        return capacity > MaxArrayLength ? Math.Max(MaxArrayLength, minCapacity) : (int)capacity;
    }

    /// <summary>Performs the publish length step for the current operation.</summary>
    /// <param name="length">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PublishLength(int length)
    {
        _writtenCount = length;
    }

    /// <summary>
    /// Overwrites <see cref="_writtenCount"/> with an absolute byte count.
    /// Used by <see cref="LuminCompressor"/> after writing a complete compressed/decompressed block.
    /// </summary>
    /// <param name="count">The number of entries involved in the operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetWrittenCount(int count) => PublishLength(count);

    #endregion
}
