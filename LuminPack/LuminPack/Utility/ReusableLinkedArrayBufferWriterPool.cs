using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Utility;



public static class ReusableLinkedArrayBufferWriterPool
{
    public const int MaxPooledBufferSize = 4 * 1024 * 1024; // 4MB
    public const int MaxPoolSize = 32;
    
#if NET8_0_OR_GREATER
    private static readonly ObjectPool<ReusableLinkedArrayBufferWriter> _pool = 
        new(MaxPoolSize);
#else
    private static readonly ObjectPool<ReusableLinkedArrayBufferWriter> _pool = 
        new(new BufferWriterPolicy(), MaxPoolSize);
#endif
    
    public static ReusableLinkedArrayBufferWriter Rent() => _pool.Rent();
    
    public static void Return(ReusableLinkedArrayBufferWriter writer) => 
        _pool.Return(writer);

#if !NET8_0_OR_GREATER
    private sealed class BufferWriterPolicy : IPooledObjectPolicy<ReusableLinkedArrayBufferWriter>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReusableLinkedArrayBufferWriter Create() => new(true);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(ReusableLinkedArrayBufferWriter writer)
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
public sealed class ReusableLinkedArrayBufferWriter :
#if NET8_0_OR_GREATER
    IBufferWriter<byte>, IDisposable, IPooledObjectPolicy<ReusableLinkedArrayBufferWriter>
#else
    IBufferWriter<byte>, IDisposable
#endif
{
    
    const int InitialBufferSize = 262144; // 256K(32768, 65536, 131072, 262144)
    
    private BufferSegment _buffer;

    private unsafe int* _currentIndex;
    
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

    public ReusableLinkedArrayBufferWriter(bool useFirstBuffer)
    {
        
        this._buffer = useFirstBuffer
            ? new BufferSegment(InitialBufferSize)
            : default;
    }
    
    ~ReusableLinkedArrayBufferWriter()
    {
        Dispose();
    }

    public Span<byte> DangerousGetBuffer() => _buffer.WrittenBuffer;

    public Memory<byte> GetMemory(int index = 0)
    {
        // LuminPack don't use GetMemory.
        throw new NotSupportedException();
    }

    public Span<byte> GetSpan(int index = 0)
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
        _buffer.SetCurrentIndexPtr(ref index);
    }

    /// <summary>
    /// 不进行偏移，仅检测边界。
    /// 偏移完全交由LuminPackWriter
    /// </summary>
    /// <param name="count"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _buffer.Advance(count);

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

    public unsafe void WriteToAndReset(ref LuminPackWriter writer)
    {
        if (_currentIndex is null || *_currentIndex == 0) return;
        
        
        Unsafe.CopyBlockUnaligned(ref writer.GetSpanReference(writer.CurrentIndex), ref _buffer.WrittenBuffer.Slice(0, CurrentIndex).GetPinnableReference(), (uint)CurrentIndex);

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

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ReusableLinkedArrayBufferWriter IPooledObjectPolicy<ReusableLinkedArrayBufferWriter>.Create() 
        => new(true);
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IPooledObjectPolicy<ReusableLinkedArrayBufferWriter>.Return(ReusableLinkedArrayBufferWriter writer)
    {
        if (writer.TotalLength < ReusableLinkedArrayBufferWriterPool.MaxPooledBufferSize)
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
    int* _written;
    int _totalLength;
    bool _disposed;

    public bool IsNull => _buffer == IntPtr.Zero;
    
    public IntPtr BufferPtr => _buffer;

    public int WrittenCount => *_written;
    
    public int TotalLength => _totalLength;
    
    public Span<byte> WrittenBuffer => new Span<byte>(_buffer.ToPointer(), _totalLength);
    
    public BufferSegment(int size)
    {
#if NET8_0_OR_GREATER
        _buffer = new IntPtr(NativeMemory.Alloc((nuint)size));
#else
        _buffer = Marshal.AllocHGlobal(size);
#endif
        _totalLength = size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        
        if (*_written > (_totalLength - (_totalLength >> 3))) // 87.5% 阈值
        {
            Resize(_totalLength << 1); // 双倍扩容
        }
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_buffer != IntPtr.Zero)
        {
#if NET8_0_OR_GREATER
            NativeMemory.Free(_buffer.ToPointer());
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
    public void Resize(int newSize)
    {
        if (newSize <= _totalLength) return;

#if NET8_0_OR_GREATER
        IntPtr newPtr = new IntPtr(NativeMemory.Realloc(_buffer.ToPointer(), (nuint)newSize));
#else
        IntPtr newPtr = Marshal.ReAllocHGlobal(_buffer, (IntPtr)newSize);
#endif
        
        if (newPtr != IntPtr.Zero)
        {
            _buffer = newPtr;
            _totalLength = newSize;
        }
        else
        {
            throw new OutOfMemoryException("Failed to reallocate unmanaged memory");
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        Clear();
        
        _disposed = true;
    }
}
