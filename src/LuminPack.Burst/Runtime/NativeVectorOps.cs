#if LUMINPACK_BURST
using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
namespace LuminPack.Burst
{
/// <summary>Preserves Burst's intrinsic vector ABI at outlined Core shuffle boundaries.</summary>
internal static unsafe partial class NativeVectorOps
{
    /// <summary>Loads a complete bounded vector at the original byte offset.</summary><param name="source">Input.</param><param name="offset">Byte offset.</param><returns>The native vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Load128(ref byte source, nuint offset) => Vector128.LoadUnsafe(ref source, offset).Value;
    /// <summary>Stores a complete bounded vector at the original byte offset.</summary><param name="value">Native vector.</param><param name="destination">Output.</param><param name="offset">Byte offset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Store128(v128 value, ref byte destination, nuint offset) => Vector128.StoreUnsafe(new Vector128<byte> { Value = value }, ref destination, offset);
    /// <summary>Joins complete independent halves without exposing a wrapper aggregate ABI.</summary><param name="low">Low half.</param><param name="high">High half.</param><returns>The native joined vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Join256(v128 low, v128 high)
    {
        if (X86.Avx2.IsAvx2Supported) return X86.Avx2.mm256_inserti128_si256(X86.Avx.mm256_castsi128_si256(low), high, 1);
        return new v256(low, high);
    }
    /// <summary>Maps the original per-half byte shuffle.</summary><param name="value">Input.</param><param name="mask">Mask.</param><returns>The selected bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Shuffle256(v256 value, v256 mask)
    {
        if (X86.Avx2.IsAvx2Supported) return X86.Avx2.mm256_shuffle_epi8(value, mask);
        return Avx2.Shuffle(new Vector256<byte> { Value = value }, new Vector256<byte> { Value = mask }).Value;
    }
    /// <summary>Combines native vector bits without wrapper parameter decomposition.</summary><param name="left">First.</param><param name="right">Second.</param><returns>The combined bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Or256(v256 left, v256 right)
    {
        if (X86.Avx2.IsAvx2Supported) return X86.Avx2.mm256_or_si256(left, right);
        return new v256(left.ULong0 | right.ULong0, left.ULong1 | right.ULong1, left.ULong2 | right.ULong2, left.ULong3 | right.ULong3);
    }
}
}
#endif

