using System.Buffers;
using System.Runtime.CompilerServices;
using LuminPack.Code;

namespace LuminPack.Internal;

internal struct FixedArrayBufferWriter : IBufferWriter<byte>
{
    byte[] buffer;
    int written;

    public FixedArrayBufferWriter(byte[] buffer)
    {
        this.buffer = buffer;
        this.written = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        this.written += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        var memory = buffer.AsMemory();
        if (memory.Length >= sizeHint)
        {
            return memory;
        }

        LuminPackExceptionHelper.ThrowMessage("Requested invalid sizeHint.");
        return memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        var span = buffer.AsSpan();
        if (span.Length >= sizeHint)
        {
            return span;
        }

        LuminPackExceptionHelper.ThrowMessage("Requested invalid sizeHint.");
        return span;
    }

    public byte[] GetFilledBuffer()
    {
        if (written != buffer.Length)
        {
            LuminPackExceptionHelper.ThrowMessage("Not filled buffer.");
        }

        return buffer;
    }
}