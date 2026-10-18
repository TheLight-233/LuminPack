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
    /// <summary>Gets the largest native buffer retained by the pool.</summary>
    public const int MaxPooledBufferSize = 4 * 1024 * 1024; // 4MB
    /// <summary>Gets the maximum number of writers retained in the shared central slots.</summary>
    public const int MaxPoolSize = 32;

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
        if (writer is not null)
        {
            t_first = null;
            return writer;
        }

        return RentSlow();
    }

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter RentSlow()
    {
#if LUMINPACK_BUFFERWRITER_SECOND_TLS
        var second = t_second;
        if (second is not null)
        {
            t_second = null;
            return second;
        }
#endif

        return s_centralPool.TryTake() ?? new LuminBufferWriter(true);
    }

    /// <summary>Returns an exclusively owned writer to the pool.</summary>
    /// <param name="writer">The writer to reset and return.</param>
    /// <remarks>
    /// Return clears the published payload, writer reference tracking, reader reference tracking, and restores
    /// <see cref="LuminBufferWriter.WriterState"/> to LuminPack defaults. Oversized native buffers are disposed instead of cached.
    /// The instance must not be accessed after this call unless it is rented again.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(LuminBufferWriter writer)
    {
        writer.ResetForPool();

        if (writer.TotalLength >= MaxPooledBufferSize)
        {
            writer.Dispose();
            return;
        }

        if (t_first is null)
        {
            t_first = writer;
            return;
        }

        ReturnSlow(writer);
    }

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReturnSlow(LuminBufferWriter writer)
    {
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
    /// <remarks>This diagnostic/maintenance API does not drain central slots owned by the process.</remarks>
    public static void ClearThreadLocalCache()
    {
        var first = t_first;
        t_first = null;

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
/// Owns a reusable native byte buffer together with the writer and reader reference states used by
/// high-performance LuminPack operations.
/// </summary>
/// <remarks>
/// <para>A single instance is not thread-safe. From Rent until Return it must be owned by one call flow.</para>
/// <para>Do not concurrently serialize and deserialize with the same instance, and do not use it for same-instance
/// nested serialization. Rent a second writer for nested operations.</para>
/// <para><see cref="WriterState"/> remains configured for the entire Rent-to-Return lifetime. Returning the writer restores
/// both operation states. Serialization options are statically compiled.</para>
/// </remarks>
public sealed class LuminBufferWriter : IDisposable
{
    internal const int InitialBufferSize = 32 * 1024;
    internal const int GrowthNumerator = 3;
    internal const int GrowthDenominator = 2;
    private const int MinimumReservedHeadroom = 256;

    private BufferSegment _buffer;

    /// <summary>Gets the current native buffer growth threshold.</summary>
    internal int BufferCheckLimit => _buffer.ResizeThreshold;

    private unsafe int* _currentIndex;

    internal int _writtenCount;

    private bool _disposed;

    /// <summary>Gets the writer operation state owned by this buffer.</summary>
    public readonly LuminPackWriterOptionalState WriterState;

    /// <summary>Gets the reader operation state owned by this buffer.</summary>
    public readonly LuminPackReaderOptionalState ReaderState;

    /// <summary>Gets the number of bytes currently published as the writer's payload.</summary>
    public unsafe int CurrentIndex
    {
        get
        {
            return _currentIndex is null || _writtenCount != 0 ? _writtenCount : *_currentIndex;
        }
    }

    /// <summary>Gets the capacity of the native buffer in bytes.</summary>
    public int TotalLength => _buffer.TotalLength;

    /// <summary>Gets whether this writer currently owns an allocated native buffer.</summary>
    public bool UseFirstBuffer => !_buffer.IsNull;

    /// <summary>Creates a reusable writer and operation context.</summary>
    /// <param name="useFirstBuffer">Whether to allocate the initial native buffer immediately.</param>
    /// <remarks>Directly constructed instances must be disposed. Pool users should call <see cref="LuminBufferWriterPool.Return"/>.</remarks>
    public LuminBufferWriter(bool useFirstBuffer)
    {

        WriterState = new LuminPackWriterOptionalState();
        ReaderState = new LuminPackReaderOptionalState();
        this._buffer = useFirstBuffer
            ? new BufferSegment(InitialBufferSize)
            : default;
    }

    /// <summary>Releases native resources when the instance is finalized.</summary>
    ~LuminBufferWriter()
    {
        Dispose();
    }

    /// <summary>Gets the complete writable native buffer without limiting the span to the published payload.</summary>
    /// <returns>A span over the current native allocation.</returns>
    /// <remarks>The span is invalidated by resize, reset that frees storage, Return, or Dispose. Callers must publish a
    /// valid length separately and must not retain the span.</remarks>
    public Span<byte> DangerousGetBuffer() => _buffer.WrittenBuffer;

    /// <summary>Copies the published payload from <paramref name="index"/> into newly allocated managed memory.</summary>
    /// <param name="index">The zero-based payload offset at which copying begins.</param>
    /// <returns>A new memory block containing the requested payload suffix.</returns>
    [Obsolete("This method causes GC allocations. Avoid calling it frequently; consider using GetSpan instead.")]
    public Memory<byte> GetMemory(int index = 0)
    {
        if (_buffer.IsNull)
            AllocateInitialBuffer();

        if ((uint)index > (uint)CurrentIndex)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(index));

        return new Memory<byte>(GetSpan().Slice(index).ToArray());
    }

    /// <summary>Gets the currently published payload without allocation.</summary>
    /// <returns>A span whose length is <see cref="CurrentIndex"/>.</returns>
    /// <remarks>The span is borrowed and is invalidated by subsequent writes, resize, Return, or Dispose.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan()
    {
        return _buffer.WrittenBuffer.Slice(0, CurrentIndex);
    }

    /// <summary>Gets the complete writable native buffer, allocating the initial segment if necessary.</summary>
    /// <returns>A span over the full current capacity.</returns>
    /// <remarks>This is a low-level borrowed span and is invalidated by resize, Return, or Dispose.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetFullSpan()
    {
        if (_buffer.IsNull)
            AllocateInitialBuffer();

        return _buffer.WrittenBuffer;
    }

    /// <summary>Copies a raw sequence into this reusable native buffer and publishes its length.</summary>
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

    /// <summary>Performs the allocate initial buffer step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AllocateInitialBuffer()
    {
        _buffer = new BufferSegment(InitialBufferSize);
    }

    /// <summary>Associates the native buffer with an externally managed write index.</summary>
    /// <param name="index">The index whose address remains valid while the writer uses the buffer.</param>
    /// <remarks>This low-level API is intended for LuminPack writer implementations. The reference must not outlive its scope.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetCurrentIndexPtr(ref int index)
    {
        int* position = (int*)Unsafe.AsPointer(ref index);
        _currentIndex = position;
        _buffer.SetCurrentIndexPtr(position);
    }

    /// <summary>Begins an appended record without resetting its existing payload or reference state.</summary>
    /// <param name="index">Receives the current absolute position and supplies the active writer's cursor storage.</param>
    /// <returns>The full writable allocation, including the existing payload prefix.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe Span<byte> BeginWrite(out int index)
    {
        index = CurrentIndex;
        var span = GetFullSpan();
        // An append stream must support the same initial fixed record as a fresh writer.
        if (index != 0 && (index > _buffer.ResizeThreshold || _buffer.TotalLength - index < InitialBufferSize))
        {
            PrepareAppend(index);
            span = _buffer.WrittenBuffer;
        }
        _writtenCount = 0;
        int* position = (int*)Unsafe.AsPointer(ref index);
        _currentIndex = position;
        _buffer.SetCurrentIndexPtr(position);
        return span;
    }

    /// <summary>Restores fresh-writer fixed-record headroom at the append container boundary.</summary>
    /// <param name="index">The absolute end of the existing payload.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PrepareAppend(int index)
    {
        _buffer.Resize(CalculateExpandedCapacity(_buffer.TotalLength, checked(index + InitialBufferSize), reserveWriteHeadroom: true));
    }

    /// <summary>Publishes <paramref name="count" /> bytes written by a LuminPack writer.</summary>
    /// <param name="count">The number of entries involved in the operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void CompleteWrite(int count)
    {
        _writtenCount = count;
        _currentIndex = null;
        _buffer.Flush();
    }

    /// <summary>Ensures capacity for the current binary writer position without advancing it.</summary>
    /// <param name="writer">The binary writer whose buffer reference is refreshed after a resize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackWriter writer) => _buffer.Check(ref writer);

    /// <summary>Ensures capacity for the current JSON writer position without advancing it.</summary>
    /// <param name="writer">The JSON writer whose buffer reference is refreshed after a resize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackJsonWriter writer) => _buffer.Check(ref writer);

    /// <summary>Copies the published payload to a new byte array and resets the published buffer length.</summary>
    /// <returns>A newly allocated array, or an empty array when no bytes are published.</returns>
    /// <remarks>This method produces result GC allocation. It resets bytes while preserving the native buffer owner.</remarks>
    public unsafe byte[] ToArrayAndReset()
    {
        var length = CurrentIndex;
        if (length == 0) return [];

        var result = AllocateUninitializedArray<byte>(length);
        var dest = result.AsSpan();

        if (UseFirstBuffer)
        {
            _buffer.WrittenBuffer.Slice(0, length).CopyTo(dest);
        }

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
            dest.AsSpan(0, compressedSize).CopyTo(_buffer.WrittenBuffer);
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
            dest.AsSpan(0, decompressedSize).CopyTo(_buffer.WrittenBuffer);
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
    public unsafe void WriteToAndReset(ref LuminPackWriter writer)
    {
        var length = CurrentIndex;
        if (length == 0) return;

#if NET8_0_OR_GREATER
        Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref writer._bufferStart, (nint)(uint)writer.CurrentIndex), ref _buffer.WrittenBuffer.GetPinnableReference(), (uint)length);
#else
        Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(writer._bufferStart), (nint)(uint)writer.CurrentIndex), ref _buffer.WrittenBuffer.GetPinnableReference(), (uint)length);
#endif

        writer.Advance(length);

        ResetCore();
    }

    /// <summary>Asynchronously copies the published payload to a stream and then resets this buffer's payload.</summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">A token that can cancel the stream write.</param>
    /// <returns>A task-like value that completes after the write.</returns>
    public async ValueTask WriteToAndResetAsync(Stream stream, CancellationToken cancellationToken)
    {
        var length = CurrentIndex;
        if (length == 0) return;

        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            _buffer.WrittenBuffer.Slice(0, length).CopyTo(buffer);
            await stream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, length), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        ResetCore();
    }

    /// <summary>Resets the published byte count without freeing the native buffer or changing <see cref="WriterState"/>.</summary>
    /// <remarks>This low-level reset does not clear reference state. Use <see cref="ResetForReuse"/> for an independent session,
    /// or <see cref="LuminBufferWriterPool.Return"/> when returning a pooled buffer.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void ResetCore()
    {
        _writtenCount = 0;
        _buffer.Flush();
        _currentIndex = null;
    }

    /// <summary>Clears the payload and both reference tables while retaining the native capacity for reuse.</summary>
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

    /// <summary>Performs the publish length step for the current operation.</summary>
    /// <param name="length">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PublishLength(int length)
    {
        _writtenCount = length;
    }

    /// <summary>Clears both reference tables and releases a nonempty payload's native storage.</summary>
    /// <remarks>This does not return the instance to the pool. Use <see cref="ResetForReuse"/> to retain native capacity.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Reset()
    {
        if (CurrentIndex != 0) _buffer.Clear();
        ResetForReuse();
    }

    /// <summary>Releases the native buffer and clears the reference state owned by this instance.</summary>
    /// <remarks>Pool-rented instances should normally be returned through <see cref="LuminBufferWriterPool.Return"/>.</remarks>
    public void Dispose()
    {
        if (_disposed) return;

        ResetForPool();
        _buffer.Dispose();

        GC.SuppressFinalize(this);

        _disposed = true;
    }

    /// <summary>
    /// Ensures the unmanaged buffer is at least <paramref name="minCapacity"/> bytes,
    /// allocating or resizing in place as needed.
    /// Called by <see cref="LuminCompressor"/> before writing compressed/decompressed output.
    /// </summary>
    /// <param name="minCapacity">The min capacity used by this operation.</param>
    internal void EnsureCapacity(int minCapacity)
    {
        if (minCapacity < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(minCapacity));

        if (_buffer.IsNull)
            _buffer = new BufferSegment(CalculateExpandedCapacity(0, minCapacity, reserveWriteHeadroom: true));
        else if (_buffer.TotalLength < minCapacity)
        {
            int newCapacity = CalculateExpandedCapacity(
                _buffer.TotalLength,
                minCapacity,
                reserveWriteHeadroom: true);
            _buffer.Resize(newCapacity);
        }
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
        return capacity >= int.MaxValue ? int.MaxValue : (int)capacity;
    }

    /// <summary>
    /// Overwrites <see cref="_writtenCount"/> with an absolute byte count.
    /// Used by <see cref="LuminCompressor"/> after writing a complete compressed/decompressed block.
    /// </summary>
    /// <param name="count">The number of entries involved in the operation.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void SetWrittenCount(int count) => PublishLength(count);

}

/// <summary>Provides buffer segment functionality.</summary>
internal unsafe struct BufferSegment : IDisposable
{
    IntPtr _buffer;
    int _resizeThreshold;

    /// <summary>Gets the established 87.5 percent growth threshold.</summary>
    internal readonly int ResizeThreshold => _resizeThreshold;
    int* _written;
    int _totalLength;
    bool _disposed;

    private const int Alignment = 16;

    /// <summary>Gets the is null.</summary>
    public bool IsNull => _buffer == IntPtr.Zero;

    /// <summary>Gets the buffer ptr.</summary>
    public IntPtr BufferPtr => _buffer;

    /// <summary>Gets the written count.</summary>
    public int WrittenCount => *_written;

    /// <summary>Gets the total length.</summary>
    public int TotalLength => _totalLength;

    /// <summary>Gets the written buffer.</summary>
    public Span<byte> WrittenBuffer => new Span<byte>(_buffer.ToPointer(), _totalLength);

    /// <summary>Initializes a new instance of <c>BufferSegment</c>.</summary>
    /// <param name="size">The number of bytes or elements to process.</param>
    public BufferSegment(int size)
    {
#if NET8_0_OR_GREATER
        _buffer = new IntPtr(NativeMemory.AlignedAlloc((nuint)size, Alignment));
        Unsafe.InitBlockUnaligned(_buffer.ToPointer(), 0, (uint)size);
#else
        // Unity's HGlobal allocator already returns memory aligned for native data.
        // Keep exactly one owning pointer so resize/free cannot disagree about which
        // address belongs to the native heap.
        _buffer = Marshal.AllocHGlobal(size);
        Unsafe.InitBlockUnaligned(_buffer.ToPointer(), 0, (uint)size);
#endif
        _totalLength = size;
        _resizeThreshold = _totalLength - (_totalLength >> 3);
    }

    /// <summary>Grows the buffer when the writer crosses its resize threshold and refreshes the storage reference.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackWriter writer)
    {
        if (writer._currentIndex > _resizeThreshold) // 87.5% load threshold.
            ResizeAndRefresh(ref writer);
    }

    /// <summary>Grows the buffer when the writer crosses its resize threshold and refreshes the storage reference.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackJsonWriter writer)
    {
        if (writer._currentIndex > _resizeThreshold) // 87.5% load threshold.
            ResizeAndRefresh(ref writer);
    }

    /// <summary>Performs the resize and refresh step for the current operation.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeAndRefresh(ref LuminPackWriter writer)
    {
        Resize(LuminBufferWriter.CalculateExpandedCapacity(
            _totalLength,
            _totalLength,
            reserveWriteHeadroom: false));
        writer.FlushBuffer();
        _resizeThreshold = _totalLength - (_totalLength >> 3);
    }

    /// <summary>Performs the resize and refresh step for the current operation.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeAndRefresh(ref LuminPackJsonWriter writer)
    {
        Resize(LuminBufferWriter.CalculateExpandedCapacity(
            _totalLength,
            _totalLength,
            reserveWriteHeadroom: false));
        writer.FlushBuffer();
        _resizeThreshold = _totalLength - (_totalLength >> 3);
    }

    /// <summary>Clears the entries or buffered contents retained by this instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_buffer != IntPtr.Zero)
        {
#if NET8_0_OR_GREATER
            NativeMemory.AlignedFree(_buffer.ToPointer());
#else
            Marshal.FreeHGlobal(_buffer);
#endif
        }
        _buffer = IntPtr.Zero;
        _written = null;
    }

    /// <summary>Publishes pending buffer contents to the configured destination.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        _written = null;
    }

    /// <summary>Updates the current index ptr used by the current operation.</summary>
    /// <param name="index">The position at which the operation starts.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCurrentIndexPtr(ref int index)
    {
        _written = (int*)Unsafe.AsPointer(ref index);
    }

    /// <summary>Updates the current index ptr used by the current operation.</summary>
    /// <param name="index">The position at which the operation starts.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCurrentIndexPtr(int* index)
    {
        _written = index;
    }

    /// <summary>Performs the resize step for the current operation.</summary>
    /// <param name="newSize">The new size used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Resize(int newSize)
    {
        if (newSize <= _totalLength) return;

#if NET8_0_OR_GREATER
        _buffer = new IntPtr(NativeMemory.AlignedRealloc(_buffer.ToPointer(), (nuint)newSize, Alignment));
#else
        _buffer = Marshal.ReAllocHGlobal(_buffer, (IntPtr)newSize);
#endif
        _totalLength = newSize;
        _resizeThreshold = newSize - (newSize >> 3);
    }

    /// <summary>Releases the resources or pooled state owned by this instance.</summary>
    public void Dispose()
    {
        if (_disposed) return;

        Clear();

        _disposed = true;
    }
}
