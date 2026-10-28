#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace LuminPack.Code;

public static partial class LuminPackMarshal
{
    /// <summary>Copies 64 through 256 bytes with bounded 512-bit head and tail lanes.</summary>
    /// <param name="destination">The writable range.</param>
    /// <param name="source">The readable non-overlapping range.</param>
    /// <param name="count">The exact count from 64 through 256.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyVector512Block(ref byte destination, ref byte source, uint count)
    {
        if (count == 64)
        {
            Vector512.LoadUnsafe(ref source).StoreUnsafe(ref destination);
            return;
        }

        Vector512<byte> head = Vector512.LoadUnsafe(ref source);
        Vector512<byte> tail = Vector512.LoadUnsafe(ref source, count - 64);
        head.StoreUnsafe(ref destination);
        tail.StoreUnsafe(ref destination, count - 64);
        if (count <= 128)
            return;

        // 129-192 B requires three 64 B lanes rather than four.
        Vector512.LoadUnsafe(ref source, 64).StoreUnsafe(ref destination, 64);
        if (count <= 192)
            return;

        // 193-256 B: four lanes, exact coverage with no OOB access.
        Vector512.LoadUnsafe(ref source, 128).StoreUnsafe(ref destination, 128);
    }
}
#endif
