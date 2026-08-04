using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LuminPack.Code;
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
    /// <returns>A writer whose payload is empty, whose <see cref="LuminBufferWriter.Option"/> has default values,
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
    /// <see cref="LuminBufferWriter.Option"/> to LuminPack defaults. Oversized native buffers are disposed instead of cached.
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

    internal static void ClearCentralCache() => s_centralPool.Drain(static writer => writer.Dispose());
}

/// <summary>
/// Owns a reusable native byte buffer together with the option, writer state, and reader state used by
/// high-performance LuminPack operations.
/// </summary>
/// <remarks>
/// <para>A single instance is not thread-safe. From Rent until Return it must be owned by one call flow.</para>
/// <para>Do not concurrently serialize and deserialize with the same instance, and do not use it for same-instance
/// nested serialization. Rent a second writer for nested operations.</para>
/// <para><see cref="Option"/> remains configured for the entire Rent-to-Return lifetime. Returning the writer restores
/// default options and clears both operation states.</para>
/// </remarks>
public sealed class LuminBufferWriter : IDisposable
{
    
    const int InitialBufferSize = 262144; // 256K(32768, 65536, 131072, 262144)
    
    private BufferSegment _buffer;

    private unsafe int* _currentIndex;

    internal int _writtenCount;
    
    private bool _disposed;

    internal readonly LuminPackWriterOptionalState WriterState;
    internal readonly LuminPackReaderOptionalState ReaderState;

    /// <summary>Gets the mutable configuration used by BufferWriter-based Serialize, Deserialize, and JSON APIs.</summary>
    /// <remarks>
    /// The instance is owned by this writer and cannot be replaced. Its values persist until
    /// <see cref="LuminBufferWriterPool.Return"/> restores defaults; they must not be expected to survive another Rent.
    /// </remarks>
    public LuminPackSerializerOption Option { get; }

    /// <summary>Gets the number of bytes currently published as the writer's payload.</summary>
    public unsafe int CurrentIndex
    {
        get
        {
            return _writtenCount != 0 || _currentIndex is null ? _writtenCount : *_currentIndex;
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
        Option = new LuminPackSerializerOption();
        WriterState = new LuminPackWriterOptionalState(Option);
        ReaderState = new LuminPackReaderOptionalState(Option);
        this._buffer = useFirstBuffer
            ? new BufferSegment(InitialBufferSize)
            : default;
    }
    
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
        {
            _buffer = AllocatedBuffer();
        }

        if ((uint)index > (uint)CurrentIndex)
            throw new ArgumentOutOfRangeException(nameof(index));

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
        {
            _buffer = AllocatedBuffer();
        }
        
        return _buffer.WrittenBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe BufferSegment AllocatedBuffer()
    {
        return new BufferSegment(InitialBufferSize);
    }

    /// <summary>Associates the native buffer with an externally managed write index.</summary>
    /// <param name="index">The index whose address remains valid while the writer uses the buffer.</param>
    /// <remarks>This low-level API is intended for LuminPack writer implementations. The reference must not outlive its scope.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetCurrentIndexPtr(ref int index)
    {
        _currentIndex = (int*)Unsafe.AsPointer(ref index);
        _buffer.SetCurrentIndexPtr(_currentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe Span<byte> BeginWrite(ref int index)
    {
        index = 0;
        _writtenCount = 0;
        _currentIndex = (int*)Unsafe.AsPointer(ref index);
        _buffer.SetCurrentIndexPtr(_currentIndex);
        return GetFullSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe void CompleteWrite(int count)
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
    /// <remarks>This method produces result GC allocation. It resets bytes but does not end the Option Rent-to-Return lifetime.</remarks>
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
            throw new ArgumentException("Source and destination writers must be different instances.", nameof(destination));
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
            throw new ArgumentException("Source and destination writers must be different instances.", nameof(destination));
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

    /// <summary>Resets the published byte count without freeing the native buffer or changing <see cref="Option"/>.</summary>
    /// <remarks>This low-level reset does not clear operation state. Pool users should call
    /// <see cref="LuminBufferWriterPool.Return"/> to reset the complete context.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void ResetCore()
    {
        _writtenCount = 0;
        _buffer.Flush();
        _currentIndex = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetForPool()
    {
        ResetCore();
        WriterState.ResetOperationState();
        ReaderState.ResetOperationState();
        Option.ResetToDefault();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PublishLength(int length)
    {
        _writtenCount = length;
    }

    /// <summary>When bytes are published, clears and releases the native payload buffer while preserving the context object.</summary>
    /// <remarks>This does not return the instance to the pool and does not reset <see cref="Option"/>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Reset()
    {
        if (CurrentIndex == 0) return;

        _buffer.Clear();
        
        ResetCore();
    }

    /// <summary>Releases the native buffer and clears the option and operation state owned by this instance.</summary>
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
    internal void EnsureCapacity(int minCapacity)
    {
        if (_buffer.IsNull)
            _buffer = new BufferSegment(Math.Max(minCapacity, InitialBufferSize));
        else if (_buffer.TotalLength < minCapacity)
        {
            // Variable-size values reserve once before their bulk write. Keep enough
            // headroom that the normal 87.5% post-write check does not immediately
            // resize the same buffer a second time.
            long doubled = (long)_buffer.TotalLength << 1;
            long requiredWithHeadroom = minCapacity + ((long)minCapacity >> 2);
            int newCapacity = (int)Math.Min(int.MaxValue, Math.Max(doubled, requiredWithHeadroom));
            _buffer.Resize(newCapacity);
        }
    }

    /// <summary>
    /// Overwrites <see cref="_writtenCount"/> with an absolute byte count.
    /// Used by <see cref="LuminCompressor"/> after writing a complete compressed/decompressed block.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void SetWrittenCount(int count) => PublishLength(count);

}

internal unsafe struct BufferSegment : IDisposable
{
    IntPtr _buffer;
    int _resizeThreshold;
    int* _written;
    int _totalLength;
    bool _disposed;

    private const int Alignment = 16;

    public bool IsNull => _buffer == IntPtr.Zero;
    
    public IntPtr BufferPtr => _buffer;

    public int WrittenCount => *_written;
    
    public int TotalLength => _totalLength;
    
    public Span<byte> WrittenBuffer => new Span<byte>(_buffer.ToPointer(), _totalLength);
    
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackWriter writer)
    {
        
        if (writer._currentIndex > _resizeThreshold) // 87.5% 阈值
        {
            Resize(_totalLength << 1); // 双倍扩容
            writer.FlushBuffer();
            _resizeThreshold = _totalLength - (_totalLength >> 3);
        }
        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackJsonWriter writer)
    {
        
        if (writer._currentIndex > _resizeThreshold) // 87.5% 阈值
        {
            Resize(_totalLength << 1); // 双倍扩容
            writer.FlushBuffer();
            _resizeThreshold = _totalLength - (_totalLength >> 3);
        }
        
    }

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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        _written = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCurrentIndexPtr(ref int index)
    {
        _written = (int*)Unsafe.AsPointer(ref index);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCurrentIndexPtr(int* index)
    {
        _written = index;
    }
    
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
    
    public void Dispose()
    {
        if (_disposed) return;
        
        Clear();
        
        _disposed = true;
    }
}
