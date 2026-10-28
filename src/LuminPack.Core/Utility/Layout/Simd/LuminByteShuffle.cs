using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Utility;

/// <summary>Combines independent bounded 128-bit shuffle blocks into wider hardware lanes.</summary>
internal static class LuminByteShuffle
{
#if NET8_0_OR_GREATER
    /// <summary>Moves leading full groups and leaves narrower blocks to the caller.</summary>
    /// <param name="input">The current complete input window.</param>
    /// <param name="output">The current complete output window.</param>
    /// <param name="blocks">The remaining individually validated 128-bit blocks.</param>
    /// <param name="inputStep">The input distance between blocks.</param>
    /// <param name="outputStep">The output distance between blocks.</param>
    /// <param name="wideInput">Whether each block reads a second 128-bit vector.</param>
    /// <param name="wideOutput">Whether each block writes a second 128-bit vector.</param>
    /// <param name="mask0">The low-input to low-output mask.</param>
    /// <param name="mask1">The high-input to low-output mask.</param>
    /// <param name="mask2">The low-input to high-output mask.</param>
    /// <param name="mask3">The high-input to high-output mask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CopyWide(ref byte input, ref byte output, ref nint blocks, nint inputStep, nint outputStep,
        bool wideInput, bool wideOutput, Vector128<byte> mask0, Vector128<byte> mask1, Vector128<byte> mask2, Vector128<byte> mask3)
    {
        nint initial = blocks;
        if (Vector512.IsHardwareAccelerated && Avx512BW.IsSupported && blocks >= 4)
            Copy512(ref input, ref output, ref blocks, inputStep, outputStep, wideInput, wideOutput, mask0, mask1, mask2, mask3);
        input = ref Unsafe.Add(ref input, (initial - blocks) * inputStep);
        output = ref Unsafe.Add(ref output, (initial - blocks) * outputStep);
        if (Vector256.IsHardwareAccelerated && Avx2.IsSupported && blocks >= 2)
            Copy256(ref input, ref output, ref blocks, inputStep, outputStep, wideInput, wideOutput, mask0, mask1, mask2, mask3);
    }
    /// <summary>Shuffles complete independent blocks without extending any source or destination access.</summary>
    /// <param name="input">The validated input cursor.</param>
    /// <param name="output">The validated output cursor.</param>
    /// <param name="blocks">The number of complete blocks still available.</param>
    /// <param name="inputStep">The input block stride.</param>
    /// <param name="outputStep">The output block stride.</param>
    /// <param name="wideInput">Whether a block has two input vectors.</param>
    /// <param name="wideOutput">Whether a block has two output vectors.</param>
    /// <param name="mask0">The first shuffle mask.</param>
    /// <param name="mask1">The second shuffle mask.</param>
    /// <param name="mask2">The third shuffle mask.</param>
    /// <param name="mask3">The fourth shuffle mask.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Copy256(ref byte input, ref byte output, ref nint blocks, nint inputStep, nint outputStep,
        bool wideInput, bool wideOutput, Vector128<byte> mask0, Vector128<byte> mask1, Vector128<byte> mask2, Vector128<byte> mask3)
    {
        var m0 = Vector256.Create(mask0, mask0);
        var m1 = Vector256.Create(mask1, mask1);
        var m2 = Vector256.Create(mask2, mask2);
        var m3 = Vector256.Create(mask3, mask3);
        for (; blocks >= 2; blocks -= 2)
        {
            var low = Vector256.Create(Vector128.LoadUnsafe(ref input, (nuint)(0 * inputStep + 0)), Vector128.LoadUnsafe(ref input, (nuint)(1 * inputStep + 0)));
            var high = wideInput ? Vector256.Create(Vector128.LoadUnsafe(ref input, (nuint)(0 * inputStep + 16)), Vector128.LoadUnsafe(ref input, (nuint)(1 * inputStep + 16))) : default;
            var first = Avx2.Shuffle(low, m0);
            if (wideInput) first |= Avx2.Shuffle(high, m1);
            var second = wideOutput ? Avx2.Shuffle(low, m2) : default;
            if (wideOutput && wideInput) second |= Avx2.Shuffle(high, m3);
            first.GetLower().StoreUnsafe(ref output, (nuint)(0 * outputStep));
            if (wideOutput) second.GetLower().StoreUnsafe(ref output, (nuint)(0 * outputStep + 16));
            first.GetUpper().StoreUnsafe(ref output, (nuint)(1 * outputStep));
            if (wideOutput) second.GetUpper().StoreUnsafe(ref output, (nuint)(1 * outputStep + 16));
            input = ref Unsafe.Add(ref input, inputStep * 2);
            output = ref Unsafe.Add(ref output, outputStep * 2);
        }
    }
    /// <summary>Shuffles complete independent blocks without extending any source or destination access.</summary>
    /// <param name="input">The validated input cursor.</param>
    /// <param name="output">The validated output cursor.</param>
    /// <param name="blocks">The number of complete blocks still available.</param>
    /// <param name="inputStep">The input block stride.</param>
    /// <param name="outputStep">The output block stride.</param>
    /// <param name="wideInput">Whether a block has two input vectors.</param>
    /// <param name="wideOutput">Whether a block has two output vectors.</param>
    /// <param name="mask0">The first shuffle mask.</param>
    /// <param name="mask1">The second shuffle mask.</param>
    /// <param name="mask2">The third shuffle mask.</param>
    /// <param name="mask3">The fourth shuffle mask.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Copy512(ref byte input, ref byte output, ref nint blocks, nint inputStep, nint outputStep,
        bool wideInput, bool wideOutput, Vector128<byte> mask0, Vector128<byte> mask1, Vector128<byte> mask2, Vector128<byte> mask3)
    {
        var m0 = Vector512.Create(Vector256.Create(mask0, mask0), Vector256.Create(mask0, mask0));
        var m1 = Vector512.Create(Vector256.Create(mask1, mask1), Vector256.Create(mask1, mask1));
        var m2 = Vector512.Create(Vector256.Create(mask2, mask2), Vector256.Create(mask2, mask2));
        var m3 = Vector512.Create(Vector256.Create(mask3, mask3), Vector256.Create(mask3, mask3));
        for (; blocks >= 4; blocks -= 4)
        {
            var low = Vector512.Create(Vector256.Create(Vector128.LoadUnsafe(ref input, (nuint)(0 * inputStep + 0)), Vector128.LoadUnsafe(ref input, (nuint)(1 * inputStep + 0))), Vector256.Create(Vector128.LoadUnsafe(ref input, (nuint)(2 * inputStep + 0)), Vector128.LoadUnsafe(ref input, (nuint)(3 * inputStep + 0))));
            var high = wideInput ? Vector512.Create(Vector256.Create(Vector128.LoadUnsafe(ref input, (nuint)(0 * inputStep + 16)), Vector128.LoadUnsafe(ref input, (nuint)(1 * inputStep + 16))), Vector256.Create(Vector128.LoadUnsafe(ref input, (nuint)(2 * inputStep + 16)), Vector128.LoadUnsafe(ref input, (nuint)(3 * inputStep + 16)))) : default;
            var first = Avx512BW.Shuffle(low, m0);
            if (wideInput) first |= Avx512BW.Shuffle(high, m1);
            var second = wideOutput ? Avx512BW.Shuffle(low, m2) : default;
            if (wideOutput && wideInput) second |= Avx512BW.Shuffle(high, m3);
            first.GetLower().GetLower().StoreUnsafe(ref output, (nuint)(0 * outputStep));
            if (wideOutput) second.GetLower().GetLower().StoreUnsafe(ref output, (nuint)(0 * outputStep + 16));
            first.GetLower().GetUpper().StoreUnsafe(ref output, (nuint)(1 * outputStep));
            if (wideOutput) second.GetLower().GetUpper().StoreUnsafe(ref output, (nuint)(1 * outputStep + 16));
            first.GetUpper().GetLower().StoreUnsafe(ref output, (nuint)(2 * outputStep));
            if (wideOutput) second.GetUpper().GetLower().StoreUnsafe(ref output, (nuint)(2 * outputStep + 16));
            first.GetUpper().GetUpper().StoreUnsafe(ref output, (nuint)(3 * outputStep));
            if (wideOutput) second.GetUpper().GetUpper().StoreUnsafe(ref output, (nuint)(3 * outputStep + 16));
            input = ref Unsafe.Add(ref input, inputStep * 4);
            output = ref Unsafe.Add(ref output, outputStep * 4);
        }
    }
#endif
}
