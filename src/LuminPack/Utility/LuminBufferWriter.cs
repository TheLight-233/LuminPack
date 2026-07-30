using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LuminPack.Code;
using LuminPack.Core;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Utility;



public static class LuminBufferWriterPool
{
    public const int MaxPooledBufferSize = 4 * 1024 * 1024; // 4MB
    public const int MaxPoolSize = 32;
    
#if NET8_0_OR_GREATER
    private static readonly ObjectPool<LuminBufferWriter> _pool = 
        new(MaxPoolSize);
#else
    private static readonly ObjectPool<LuminBufferWriter> _pool = 
        new(new BufferWriterPolicy(), MaxPoolSize);
#endif
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminBufferWriter Rent() => _pool.Rent();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(LuminBufferWriter writer) => 
        _pool.Return(writer);

#if !NET8_0_OR_GREATER
    private sealed class BufferWriterPolicy : IPooledObjectPolicy<LuminBufferWriter>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminBufferWriter Create() => new(true);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(LuminBufferWriter writer)
        {
            if (writer.TotalLength < MaxPooledBufferSize)
            {
                writer.ResetCore();
                return true; // 可回收
            }
            writer.Dispose();
            return false; // 不可回收
        }
    }
#endif
    
}

// This class has large buffer so should cache [ThreadStatic] or Pool.
public sealed class LuminBufferWriter :
#if NET8_0_OR_GREATER
    IDisposable, IPooledObjectPolicy<LuminBufferWriter>
#else
    IDisposable
#endif
{
    
    const int InitialBufferSize = 262144; // 256K(32768, 65536, 131072, 262144)
    
    private BufferSegment _buffer;

    private unsafe int* _currentIndex;

    internal int _writtenCount;
    
    private bool _disposed;

    public unsafe int CurrentIndex
    {
        get
        {
            return _writtenCount != 0 || _currentIndex is null ? _writtenCount : *_currentIndex;
        }
    }

    public int TotalLength => _buffer.TotalLength;
    
    public bool UseFirstBuffer => !_buffer.IsNull;

    public LuminBufferWriter(bool useFirstBuffer)
    {
        
        this._buffer = useFirstBuffer
            ? new BufferSegment(InitialBufferSize)
            : default;
    }
    
    ~LuminBufferWriter()
    {
        Dispose();
    }

    public Span<byte> DangerousGetBuffer() => _buffer.WrittenBuffer;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan()
    {
        return _buffer.WrittenBuffer.Slice(0, CurrentIndex);
    }
    
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
    
    /// <summary>
    /// 不进行偏移，仅检测边界。
    /// 偏移完全交由LuminPackWriter
    /// </summary>
    /// <param name="writer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackWriter writer) => _buffer.Check(ref writer);
    
    /// <summary>
    /// 不进行偏移，仅检测边界。
    /// 偏移完全交由LuminPackJsonWriter
    /// </summary>
    /// <param name="writer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check(ref LuminPackJsonWriter writer) => _buffer.Check(ref writer);

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

    /// <summary>
    /// 原地压缩
    /// </summary>
    /// <returns>压缩后的长度</returns>
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

    /// <summary>
    /// 原地解压缩
    /// </summary>
    /// <returns></returns>
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
    
    /// <summary>
    /// 压缩到目标位置
    /// </summary>
    /// <param name="destination"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompressTo(LuminBufferWriter destination)
    {
        return LuminCompressor.Compress(this, destination);
    }
    
    /// <summary>
    /// 压缩并重置
    /// </summary>
    /// <param name="destination"></param>
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
    
    /// <summary>
    /// 解压缩到目标位置
    /// </summary>
    /// <param name="destination"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecompressTo(LuminBufferWriter destination)
    {
        return LuminCompressor.Decompress(this, destination);
    }
    
    /// <summary>
    /// 解压缩
    /// </summary>
    /// <param name="destination"></param>
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

    // reset without dispose BufferSegment memory
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void ResetCore()
    {
        _writtenCount = 0;
        _buffer.Flush();
        _currentIndex = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PublishLength(int length)
    {
        _writtenCount = length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Reset()
    {
        if (CurrentIndex == 0) return;

        _buffer.Clear();
        
        ResetCore();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
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

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static LuminBufferWriter IPooledObjectPolicy<LuminBufferWriter>.Create() 
        => new(true);
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IPooledObjectPolicy<LuminBufferWriter>.Return(LuminBufferWriter writer)
    {
        if (writer.TotalLength < LuminBufferWriterPool.MaxPooledBufferSize)
        {
            writer.ResetCore();
            return true; // 可回收
        }
        
        return false; // 不可回收
    }
#endif
    
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
