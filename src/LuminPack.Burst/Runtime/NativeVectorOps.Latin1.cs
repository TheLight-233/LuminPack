#if LUMINPACK_BURST
using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
namespace LuminPack.Burst
{
internal static unsafe partial class NativeVectorOps
{
    /// <summary>Loads the original complete native vector.</summary><param name="source">Complete input.</param><returns>The native vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Load128(void* source) { if(X86.Sse2.IsSse2Supported)return X86.Sse2.loadu_si128(source);return Vector128.Load((byte*)source).Value; }
    /// <summary>Broadcasts one word without changing lane order.</summary><param name="value">Word.</param><returns>The native vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Broadcast16_128(ushort value) { if(X86.Sse2.IsSse2Supported)return X86.Sse2.set1_epi16(unchecked((short)value));return Vector128.Create(value).Value; }
    /// <summary>Compares the original word lanes.</summary><param name="left">First.</param><param name="right">Second.</param><returns>The native lane mask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Equals16_128(v128 left,v128 right){if(X86.Sse2.IsSse2Supported)return X86.Sse2.cmpeq_epi16(left,right);return Vector128.Equals(new Vector128<ushort>{Value=left},new Vector128<ushort>{Value=right}).Value;}
    /// <summary>Tests complete vector equality.</summary><param name="left">First.</param><param name="right">Second.</param><returns>Whether all bits match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool EqualsAll128(v128 left,v128 right){if(X86.Sse2.IsSse2Supported)return X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi8(left,right))==0xFFFF;return Vector128.EqualsAll(new Vector128<byte>{Value=left},new Vector128<byte>{Value=right});}
    /// <summary>Extracts the original word sign mask.</summary><param name="value">Words.</param><returns>The sign bits in lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint WordSigns128(v128 value){if(X86.Sse2.IsSse2Supported){uint mask=(uint)X86.Sse2.movemask_epi8(X86.Sse2.packs_epi16(value,value));return mask&255u;}return Vector128.ExtractMostSignificantBits(new Vector128<ushort>{Value=value});}
    /// <summary>Selects bits using the original comparison mask.</summary><param name="mask">Mask.</param><param name="left">Set bits.</param><param name="right">Clear bits.</param><returns>The selected bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Select128(v128 mask,v128 left,v128 right){if(X86.Sse2.IsSse2Supported)return X86.Sse2.xor_si128(right,X86.Sse2.and_si128(mask,X86.Sse2.xor_si128(left,right)));return Vector128.ConditionalSelect(new Vector128<ushort>{Value=mask},new Vector128<ushort>{Value=left},new Vector128<ushort>{Value=right}).Value;}
    /// <summary>Combines native vector bits.</summary><param name="left">First.</param><param name="right">Second.</param><returns>The combined bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 And128(v128 left,v128 right){if(X86.Sse2.IsSse2Supported)return X86.Sse2.and_si128(left,right);return (new Vector128<byte>{Value=left}&new Vector128<byte>{Value=right}).Value;}
    /// <summary>Combines native vector bits.</summary><param name="left">First.</param><param name="right">Second.</param><returns>The combined bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Or128(v128 left,v128 right){if(X86.Sse2.IsSse2Supported)return X86.Sse2.or_si128(left,right);return (new Vector128<byte>{Value=left}|new Vector128<byte>{Value=right}).Value;}
    /// <summary>Shifts the original unsigned word lanes.</summary><param name="value">Words.</param><param name="count">Bits.</param><returns>The shifted lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Left16_128(v128 value,int count){if(X86.Sse2.IsSse2Supported)return X86.Sse2.sll_epi16(value,new v128((ulong)(count&15),0UL));return (new Vector128<ushort>{Value=value}<<count).Value;}
    /// <summary>Shifts the original unsigned word lanes.</summary><param name="value">Words.</param><param name="count">Bits.</param><returns>The shifted lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Right16_128(v128 value,int count){if(X86.Sse2.IsSse2Supported)return X86.Sse2.srl_epi16(value,new v128((ulong)(count&15),0UL));return (new Vector128<ushort>{Value=value}>>count).Value;}
    /// <summary>Loads the original complete native vector.</summary><param name="source">Complete input.</param><returns>The native vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Load256(void* source) { if(X86.Avx2.IsAvx2Supported)return X86.Avx.mm256_loadu_si256(source);return Vector256.Load((byte*)source).Value; }
    /// <summary>Broadcasts one word without changing lane order.</summary><param name="value">Word.</param><returns>The native vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Broadcast16_256(ushort value) { if(X86.Avx2.IsAvx2Supported)return new v256(value);return Vector256.Create(value).Value; }
    /// <summary>Compares the original word lanes.</summary><param name="left">First.</param><param name="right">Second.</param><returns>The native lane mask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Equals16_256(v256 left,v256 right){if(X86.Avx2.IsAvx2Supported)return X86.Avx2.mm256_cmpeq_epi16(left,right);return Vector256.Equals(new Vector256<ushort>{Value=left},new Vector256<ushort>{Value=right}).Value;}
    /// <summary>Tests complete vector equality.</summary><param name="left">First.</param><param name="right">Second.</param><returns>Whether all bits match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool EqualsAll256(v256 left,v256 right){if(X86.Avx2.IsAvx2Supported)return X86.Avx2.mm256_movemask_epi8(X86.Avx2.mm256_cmpeq_epi8(left,right))==-1;return Vector256.EqualsAll(new Vector256<byte>{Value=left},new Vector256<byte>{Value=right});}
    /// <summary>Extracts the original word sign mask.</summary><param name="value">Words.</param><returns>The sign bits in lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint WordSigns256(v256 value){if(X86.Avx2.IsAvx2Supported){uint mask=(uint)X86.Avx2.mm256_movemask_epi8(X86.Avx2.mm256_packs_epi16(value,value));return (mask&255u)|((mask>>8)&0xFF00u);}return Vector256.ExtractMostSignificantBits(new Vector256<ushort>{Value=value});}
    /// <summary>Selects bits using the original comparison mask.</summary><param name="mask">Mask.</param><param name="left">Set bits.</param><param name="right">Clear bits.</param><returns>The selected bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Select256(v256 mask,v256 left,v256 right){if(X86.Avx2.IsAvx2Supported)return X86.Avx2.mm256_xor_si256(right,X86.Avx2.mm256_and_si256(mask,X86.Avx2.mm256_xor_si256(left,right)));return Vector256.ConditionalSelect(new Vector256<ushort>{Value=mask},new Vector256<ushort>{Value=left},new Vector256<ushort>{Value=right}).Value;}
    /// <summary>Combines native vector bits.</summary><param name="left">First.</param><param name="right">Second.</param><returns>The combined bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 And256(v256 left,v256 right){if(X86.Avx2.IsAvx2Supported)return X86.Avx2.mm256_and_si256(left,right);return (new Vector256<byte>{Value=left}&new Vector256<byte>{Value=right}).Value;}
    /// <summary>Shifts the original unsigned word lanes.</summary><param name="value">Words.</param><param name="count">Bits.</param><returns>The shifted lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Left16_256(v256 value,int count){if(X86.Avx2.IsAvx2Supported)return X86.Avx2.mm256_sll_epi16(value,new v128((ulong)(count&15),0UL));return (new Vector256<ushort>{Value=value}<<count).Value;}
    /// <summary>Shifts the original unsigned word lanes.</summary><param name="value">Words.</param><param name="count">Bits.</param><returns>The shifted lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v256 Right16_256(v256 value,int count){if(X86.Avx2.IsAvx2Supported)return X86.Avx2.mm256_srl_epi16(value,new v128((ulong)(count&15),0UL));return (new Vector256<ushort>{Value=value}>>count).Value;}
    /// <summary>Selects bytes in one complete sixteen-byte native vector.</summary><param name="value">Bytes.</param><param name="mask">Selectors.</param><returns>The selected bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static v128 Shuffle128(v128 value,v128 mask){if(X86.Ssse3.IsSsse3Supported)return X86.Ssse3.shuffle_epi8(value,mask);return Ssse3.Shuffle(new Vector128<byte>{Value=value},new Vector128<byte>{Value=mask}).Value;}
}
}
#endif

