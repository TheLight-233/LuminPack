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
            if (_currentIndex == null) 
                LuminPackExceptionHelper.ThrowBufferWriterNoInit();
            
            return *_currentIndex;
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

        return new Memory<byte>(_buffer.WrittenBuffer.ToArray());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan()
    {
        return _buffer.WrittenBuffer.Slice(0, _writtenCount);
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
        if (_currentIndex is null)
        {
            LuminPackExceptionHelper.ThrowBufferWriterNoInit();
        }
        
        return new BufferSegment(InitialBufferSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetCurrentIndexPtr(ref int index)
    {
        _currentIndex = (int*)Unsafe.AsPointer(ref index);
        _buffer.SetCurrentIndexPtr(_currentIndex);
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
        if (_currentIndex is null || *_currentIndex == 0) return [];

        var result = AllocateUninitializedArray<byte>(CurrentIndex);
        var dest = result.AsSpan();

        if (UseFirstBuffer)
        {
            _buffer.WrittenBuffer.CopyTo(dest);
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
        var dest = ArrayPool<byte>.Shared.Rent(LuminCompressor.GetMaxCompressedSize(_writtenCount));
        try
        {
            int compressedSize = LuminCompressor.Compress(span, dest);
            dest.AsSpan().CopyTo(_buffer.WrittenBuffer);
            _writtenCount = compressedSize;
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
        var dest = ArrayPool<byte>.Shared.Rent(LuminCompressor.GetDecompressedSize(_buffer.WrittenBuffer));
        try
        {
            int decompressedSize = LuminCompressor.Decompress(span, dest);
            dest.AsSpan().CopyTo(_buffer.WrittenBuffer);
            _writtenCount = decompressedSize;
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
        if (_currentIndex is null || *_currentIndex == 0) return;

#if NET8_0_OR_GREATER
        Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref writer._bufferStart, (nint)(uint)writer.CurrentIndex), ref _buffer.WrittenBuffer.Slice(0, CurrentIndex).GetPinnableReference(), (uint)CurrentIndex);
#else
        Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref Unsafe.AsRef<byte>(writer._bufferStart), (nint)(uint)writer.CurrentIndex), ref _buffer.WrittenBuffer.Slice(0, CurrentIndex).GetPinnableReference(), (uint)CurrentIndex);
#endif

        writer.Advance(CurrentIndex);
        
        ResetCore();
    }

    public async ValueTask WriteToAndResetAsync(Stream stream, CancellationToken cancellationToken)
    {
        unsafe
        {
            if (_currentIndex is null || CurrentIndex == 0) return;
        }
        

        if (UseFirstBuffer)
        {
            await stream.WriteAsync(new Memory<byte>(_buffer.WrittenBuffer.Slice(0, CurrentIndex).ToArray()), cancellationToken).ConfigureAwait(false);
        }

        ResetCore();
    }

    // reset without dispose BufferSegment memory
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void ResetCore()
    {
        _buffer.Flush();
        _currentIndex = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Reset()
    {
        if (_currentIndex is null || *_currentIndex == 0) return;

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
            _buffer.Resize(minCapacity);
    }

    /// <summary>
    /// Overwrites <see cref="_writtenCount"/> with an absolute byte count.
    /// Used by <see cref="LuminCompressor"/> after writing a complete compressed/decompressed block.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void SetWrittenCount(int count) => _writtenCount = count;

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

#if !NET8_0_OR_GREATER
    IntPtr _rawBuffer; // 未对齐的原始指针，用于 FreeHGlobal
#endif
    
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
        var raw = Marshal.AllocHGlobal(size + Alignment - 1);
        _rawBuffer = raw;
        _buffer = new IntPtr(((long)raw + Alignment - 1) & ~(long)(Alignment - 1));
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
            Marshal.FreeHGlobal(_rawBuffer);
            _rawBuffer = IntPtr.Zero;
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
        _totalLength = newSize;
#else
        var newRaw = Marshal.AllocHGlobal(newSize + Alignment - 1);
        var newAligned = new IntPtr(((long)newRaw + Alignment - 1) & ~(long)(Alignment - 1));
        Unsafe.CopyBlockUnaligned(newAligned.ToPointer(), _buffer.ToPointer(), (uint)_totalLength);
        Marshal.FreeHGlobal(_rawBuffer);
        _rawBuffer = newRaw;
        _buffer = newAligned;
        _totalLength = newSize;
#endif
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        Clear();
        
        _disposed = true;
    }
}