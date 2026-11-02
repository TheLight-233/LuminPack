using System.Buffers;
using System.ComponentModel;

namespace LuminPack.Utility;

/// <summary>Holds a complete stream payload in pooled storage for one asynchronous read operation.</summary>
/// <remarks>Generated asynchronous facades read the stream into rented storage, deserialize from it and return it to the
/// shared pool, so buffering allocates no staging stream and makes no final copy.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct LuminPackStreamInput : IDisposable
{
    private const int InitialCapacity = 4096;
    private byte[]? _array;
    private int _length;

    /// <summary>Gets the number of payload bytes.</summary>
    public readonly int Length => _length;

    /// <summary>Gets the payload.</summary>
    public readonly ReadOnlySpan<byte> Span => _array is null ? default : new ReadOnlySpan<byte>(_array, 0, _length);

    /// <summary>Gets the payload as memory.</summary>
    public readonly ReadOnlyMemory<byte> Memory => _array is null ? default : new ReadOnlyMemory<byte>(_array, 0, _length);

    /// <summary>Reads a stream to its end into rented storage.</summary>
    /// <param name="stream">The caller-owned source.</param>
    /// <param name="maximum">The encoded byte limit; <see cref="int.MaxValue"/> leaves the input unbounded.</param>
    /// <param name="cancellationToken">A token that can cancel stream reads.</param>
    /// <returns>The pooled payload, which the caller disposes.</returns>
    /// <remarks>At most one byte beyond the limit is read before the input is rejected, and the storage is returned on any failure.</remarks>
    public static async ValueTask<LuminPackStreamInput> ReadAsync(Stream stream, int maximum, CancellationToken cancellationToken)
    {
        if (stream is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(stream));
        long ceiling = (long)maximum + 1;
        long capacity = InitialCapacity;
        // A seekable stream reports what remains, so the first rental usually holds the whole payload.
        if (stream.CanSeek) capacity = Math.Max(1, stream.Length - stream.Position);
        byte[] array = ArrayPool<byte>.Shared.Rent((int)Math.Min(Math.Min(capacity, ceiling), MaxArrayLength));
        int length = 0;
        try
        {
            while (true)
            {
                if (length == array.Length)
                {
                    if (length >= MaxArrayLength) global::LuminPack.Security.LuminPackInputPolicy.CheckInput((long)length + 1, MaxArrayLength);
                    byte[] grown = ArrayPool<byte>.Shared.Rent((int)Math.Min(Math.Min((long)length * 2, ceiling), MaxArrayLength));
                    Buffer.BlockCopy(array, 0, grown, 0, length);
                    ArrayPool<byte>.Shared.Return(array);
                    array = grown;
                }
                int request = (int)Math.Min(array.Length - length, ceiling - length);
                int read = await stream.ReadAsync(array.AsMemory(length, request), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                length += read;
                global::LuminPack.Security.LuminPackInputPolicy.CheckInput(length, maximum);
            }
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(array);
            throw;
        }
        return new LuminPackStreamInput { _array = array, _length = length };
    }

    /// <summary>Returns the storage to the shared pool.</summary>
    public void Dispose()
    {
        var array = _array;
        _array = null;
        _length = 0;
        if (array is not null) ArrayPool<byte>.Shared.Return(array);
    }

    /// <summary>The largest byte array the runtime allocates.</summary>
    private const int MaxArrayLength = 0x7FFFFFC7;
}
