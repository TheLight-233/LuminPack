using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Code;

public static partial class LuminPackMarshal
{
    /// <summary>Expands packed raw pairs into the already allocated, unpublished entries of a fresh dictionary.</summary>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    /// <param name="payload">The complete pair bytes validated by the reader.</param>
    /// <param name="view">The validated view of the fresh dictionary.</param>
    /// <param name="count">The number of wire pairs and empty entry slots.</param>
    /// <returns>Whether the complete entry range was populated for the existing bucket builder.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryExpandDenseRawPairs<TKey, TValue>(scoped ReadOnlySpan<byte> payload, DictionaryView<TKey, TValue> view, int count)
        where TKey : unmanaged where TValue : unmanaged
    {
#if NET8_0_OR_GREATER
        const int pairWidth = sizeof(ulong);
        if (IntPtr.Size != 8 || !(Sse2.IsSupported || AdvSimd.Arm64.IsSupported) ||
            Unsafe.SizeOf<TKey>() + Unsafe.SizeOf<TValue>() != pairWidth ||
            Unsafe.SizeOf<DictionaryView<TKey, TValue>.Entry>() != 2 * pairWidth ||
            count < 32 || count > 1024 || payload.Length < count * pairWidth ||
            !AdjacentPair<TKey, TValue>.Value || !AdjacentPair<TKey, TValue>.UpperHalf)
            return false;

        var entries = view._entries;
        if (view._count != 0 || entries is null || count > entries.Length)
            return false;
        var destination = MemoryMarshal.AsBytes(entries.AsSpan(0, count));
        if (destination.Overlaps(payload))
            return false;
        ref ulong pairs = ref Unsafe.As<byte, ulong>(ref MemoryMarshal.GetReference(payload));
        ref ulong target = ref Unsafe.As<byte, ulong>(ref MemoryMarshal.GetReference(destination));
        ExpandPairs(ref pairs, ref target, count);
        return true;
#else
        return false;
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>Places each pair in the upper half of its entry, leaving zeroed metadata for the bucket builder.</summary>
    /// <param name="source">The first validated wire pair.</param>
    /// <param name="destination">The first empty entry's metadata lane.</param>
    /// <param name="count">The number of complete pairs and entries.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExpandPairs(ref ulong source, ref ulong destination, int count)
    {
        int index = 0;
        if (Avx512F.IsSupported)
        {
            var zero = Vector512<ulong>.Zero;
            var first = Vector512.Create(0UL, 8UL, 0UL, 9UL, 0UL, 10UL, 0UL, 11UL);
            var second = Vector512.Create(0UL, 12UL, 0UL, 13UL, 0UL, 14UL, 0UL, 15UL);
            for (; index <= count - 8; index += 8)
            {
                nuint offset = (nuint)index;
                var pairs = Vector512.LoadUnsafe(ref source, offset);
                Avx512F.PermuteVar8x64x2(zero, first, pairs).StoreUnsafe(ref destination, offset * 2);
                Avx512F.PermuteVar8x64x2(zero, second, pairs).StoreUnsafe(ref destination, offset * 2 + 8);
            }
        }
        if (Avx2.IsSupported)
        {
            var zero = Vector256<ulong>.Zero;
            for (; index <= count - 4; index += 4)
            {
                nuint offset = (nuint)index;
                var pairs = Vector256.LoadUnsafe(ref source, offset);
                var low = Avx2.UnpackLow(zero, pairs);
                var high = Avx2.UnpackHigh(zero, pairs);
                Avx2.Permute2x128(low, high, 0x20).StoreUnsafe(ref destination, offset * 2);
                Avx2.Permute2x128(low, high, 0x31).StoreUnsafe(ref destination, offset * 2 + 4);
            }
        }
        var empty = Vector128<ulong>.Zero;
        for (; index <= count - 2; index += 2)
        {
            nuint offset = (nuint)index;
            var pairs = Vector128.LoadUnsafe(ref source, offset);
            var low = Sse2.IsSupported ? Sse2.UnpackLow(empty, pairs) : AdvSimd.Arm64.ZipLow(empty, pairs);
            var high = Sse2.IsSupported ? Sse2.UnpackHigh(empty, pairs) : AdvSimd.Arm64.ZipHigh(empty, pairs);
            low.StoreUnsafe(ref destination, offset * 2);
            high.StoreUnsafe(ref destination, offset * 2 + 2);
        }
        if (index < count)
        {
            ref byte target = ref Unsafe.As<ulong, byte>(ref Unsafe.Add(ref destination, 2 * index + 1));
            ref byte pair = ref Unsafe.As<ulong, byte>(ref Unsafe.Add(ref source, index));
            Unsafe.WriteUnaligned(ref target, Unsafe.ReadUnaligned<ulong>(ref pair));
        }
    }
#endif
}
