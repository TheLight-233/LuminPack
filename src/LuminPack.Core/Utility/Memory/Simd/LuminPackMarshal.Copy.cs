using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Code;

public static partial class LuminPackMarshal
{
    /// <summary>Copies the exact bytes of two non-overlapping, reference-free storage ranges.</summary>
    /// <param name="destination">The first writable byte of the complete destination range.</param>
    /// <param name="src">The first readable byte of the complete source range.</param>
    /// <param name="byteCount">The exact byte count; zero does not access either reference.</param>
    /// <remarks>
    /// The caller supplies valid ranges; this method does not perform bounds checks or provide
    /// memmove, atomic or volatile semantics. Managed references stay tracked. On modern 64-bit
    /// SIMD targets, blocks through 32 bytes use bounded cached accesses. Reused blocks from 256 bytes
    /// through 32 KiB use destination-aligned 256-bit stores, and from 128 bytes on Intel and AMD family 1Ah processors
    /// with accelerated 512-bit vectors, destination-aligned 512-bit stores (see <see cref="LuminKernelPolicy"/>
    /// for the measurements behind both ranges). Other lengths, older targets,
    /// 32-bit targets and unsupported hardware retain <c>Unsafe.CopyBlockUnaligned</c>.
    /// An explicitly registered netstandard2.1 native copy hook may handle complete blocks of at least sixteen bytes on 64-bit.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyBlockUnaligned(ref byte destination, ref byte src, uint byteCount)
        => CopyBlockUnaligned(ref destination, ref src, byteCount, destinationIsNew: false);

    /// <summary>Copies the exact bytes into a destination that was just allocated and will be fully overwritten.</summary>
    /// <param name="destination">The first writable byte of the complete fresh destination range.</param>
    /// <param name="src">The first readable byte of the complete source range.</param>
    /// <param name="byteCount">The exact byte count; zero does not access either reference.</param>
    /// <param name="destinationIsNew">Whether the destination was newly allocated and is not yet in cache.</param>
    /// <remarks>
    /// Fresh destinations above the inline short range use the runtime block copy: vector stores
    /// read newly allocated lines before writing them, while the runtime copy writes complete lines.
    /// Reused destinations keep the aligned medium schedule. The same caller contracts as the public overload apply.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CopyBlockUnaligned(ref byte destination, ref byte src, uint byteCount, bool destinationIsNew)
    {
#if NETSTANDARD2_1
        if (Unsafe.SizeOf<IntPtr>() == 8 && byteCount >= 16 && global::LuminPack.Core.LuminPackBurst.TryCopy(ref destination, ref src, byteCount))
            return;
#endif
#if NET8_0_OR_GREATER
        if (Unsafe.SizeOf<IntPtr>() == 8 && Vector128.IsHardwareAccelerated)
        {
            if (LuminKernelPolicy.UseShortCopy(true, true, Vector256.IsHardwareAccelerated, Vector512.IsHardwareAccelerated, byteCount))
            {
                CopyShortBlock(ref destination, ref src, byteCount);
                return;
            }

            if (!destinationIsNew)
            {
                if (LuminKernelPolicy.UseWideCopy(true, WideCopyHardware.Preferred, byteCount))
                {
                    CopyWideBlock(ref destination, ref src, byteCount);
                    return;
                }

                if (LuminKernelPolicy.UseMediumCopy(true, Vector256.IsHardwareAccelerated, byteCount))
                {
                    CopyMediumBlock(ref destination, ref src, byteCount);
                    return;
                }
            }

            // if ((Avx2.IsSupported || Avx512F.IsSupported) && byteCount >= 1024 * 1024)
            // {
            //     CopyStreamingBlock(ref destination, ref src, byteCount);
            //     return;
            // }
        }
#endif
        Unsafe.CopyBlockUnaligned(ref destination, ref src, byteCount);
    }

#if NET8_0_OR_GREATER
    /// <summary>Copies 256 bytes through 32 KiB with 256-bit loads and destination-aligned stores.</summary>
    /// <param name="destination">The valid writable byte range.</param>
    /// <param name="source">The valid readable non-overlapping byte range.</param>
    /// <param name="count">A byte count from 256 through 32 KiB.</param>
    /// <remarks>The first and final vectors are stored unaligned and overlap the aligned body; every access stays inside the
    /// two complete ranges. The alignment is only a position hint: a relocation during the copy changes speed, not bytes.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CopyMediumBlock(ref byte destination, ref byte source, uint count)
    {
        Vector256.LoadUnsafe(ref source).StoreUnsafe(ref destination);
        nuint last = count - (nuint)Vector256<byte>.Count;
        // Complement the address bits to avoid the .NET 11 JIT fallback when computing the next vector boundary.
        nuint address = (nuint)Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref destination);
        nuint offset = (~address & (nuint)(Vector256<byte>.Count - 1)) + 1;
        for (; offset + 3 * (nuint)Vector256<byte>.Count <= last; offset += 4 * (nuint)Vector256<byte>.Count)
        {
            var first = Vector256.LoadUnsafe(ref source, offset);
            var second = Vector256.LoadUnsafe(ref source, offset + (nuint)Vector256<byte>.Count);
            var third = Vector256.LoadUnsafe(ref source, offset + 2 * (nuint)Vector256<byte>.Count);
            var fourth = Vector256.LoadUnsafe(ref source, offset + 3 * (nuint)Vector256<byte>.Count);
            first.StoreUnsafe(ref destination, offset);
            second.StoreUnsafe(ref destination, offset + (nuint)Vector256<byte>.Count);
            third.StoreUnsafe(ref destination, offset + 2 * (nuint)Vector256<byte>.Count);
            fourth.StoreUnsafe(ref destination, offset + 3 * (nuint)Vector256<byte>.Count);
        }

        for (; offset <= last; offset += (nuint)Vector256<byte>.Count)
            Vector256.LoadUnsafe(ref source, offset).StoreUnsafe(ref destination, offset);
        Vector256.LoadUnsafe(ref source, last).StoreUnsafe(ref destination, last);
    }

    /// <summary>Selects 512-bit medium copies only where the CI 57 round-4 measurements showed a gain.</summary>
    /// <remarks>
    /// Accelerated 512-bit vectors alone do not decide it: on the EPYC 9V74 (Zen 4, which splits 512-bit operations into two
    /// 256-bit halves) the matrix raw-collection reuse rows took 1.04 times as long with the 512-bit kernel, while on the
    /// Xeon 6975P-C they took 0.87 to 0.92. Only the GenuineIntel vendor string is accepted; other vendors, including the
    /// unsampled Zen 5, keep the 256-bit schedule.
    /// </remarks>
    private static class WideCopyHardware
    {
        /// <summary>Whether this processor uses the 512-bit medium schedule.</summary>
        internal static readonly bool Preferred = Vector512.IsHardwareAccelerated && PrefersWideStores();

        /// <summary>Reads the CPUID vendor and AMD display family.</summary>
        /// <returns>Whether the processor belongs to a measured wide-copy family.</returns>
        private static bool PrefersWideStores()
        {
            if (!X86Base.IsSupported)
                return false;
            (int maxLeaf, int ebx, int ecx, int edx) = X86Base.CpuId(0, 0);
            // "Genu" "ineI" "ntel" in EBX, EDX, ECX.
            if (ebx == 0x756E6547 && edx == 0x49656E69 && ecx == 0x6C65746E)
                return true;

            // "Auth" "enti" "cAMD" in EBX, EDX, ECX. Only family 0Fh uses the extended family bits.
            if (maxLeaf < 1 || ebx != 0x68747541 || edx != 0x69746E65 || ecx != 0x444D4163)
                return false;
            int signature = X86Base.CpuId(1, 0).Eax;
            return ((signature >> 8) & 0xF) == 0xF && ((signature >> 20) & 0xFF) == 0x1A - 0xF;
        }
    }

    /// <summary>Copies 128 bytes through 32 KiB with 512-bit loads and destination-aligned stores.</summary>
    /// <param name="destination">The valid writable byte range.</param>
    /// <param name="source">The valid readable non-overlapping byte range.</param>
    /// <param name="count">A byte count from 128 through 32 KiB.</param>
    /// <remarks>The same schedule as <see cref="CopyMediumBlock"/> at twice the width: the first and final vectors are stored
    /// unaligned and overlap the aligned body, and every access stays inside the two complete ranges. The alignment is only a
    /// position hint: a relocation during the copy changes speed, not bytes. Measured against the runtime block copy on the
    /// Xeon 6975P-C (reused destination, one cache-resident target): 0.81 at 128 bytes, 0.48 at 512 bytes, 0.57 at 1 KiB,
    /// 0.86 at 4 KiB and 0.93 at 16 KiB; see <see cref="LuminKernelPolicy"/> for the matrix results.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CopyWideBlock(ref byte destination, ref byte source, uint count)
    {
        Vector512.LoadUnsafe(ref source).StoreUnsafe(ref destination);
        nuint last = count - (nuint)Vector512<byte>.Count;
        // Complement the address bits to avoid the .NET 11 JIT fallback when computing the next vector boundary.
        nuint address = (nuint)Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref destination);
        nuint offset = (~address & (nuint)(Vector512<byte>.Count - 1)) + 1;
        for (; offset + 3 * (nuint)Vector512<byte>.Count <= last; offset += 4 * (nuint)Vector512<byte>.Count)
        {
            var first = Vector512.LoadUnsafe(ref source, offset);
            var second = Vector512.LoadUnsafe(ref source, offset + (nuint)Vector512<byte>.Count);
            var third = Vector512.LoadUnsafe(ref source, offset + 2 * (nuint)Vector512<byte>.Count);
            var fourth = Vector512.LoadUnsafe(ref source, offset + 3 * (nuint)Vector512<byte>.Count);
            first.StoreUnsafe(ref destination, offset);
            second.StoreUnsafe(ref destination, offset + (nuint)Vector512<byte>.Count);
            third.StoreUnsafe(ref destination, offset + 2 * (nuint)Vector512<byte>.Count);
            fourth.StoreUnsafe(ref destination, offset + 3 * (nuint)Vector512<byte>.Count);
        }

        for (; offset <= last; offset += (nuint)Vector512<byte>.Count)
            Vector512.LoadUnsafe(ref source, offset).StoreUnsafe(ref destination, offset);
        Vector512.LoadUnsafe(ref source, last).StoreUnsafe(ref destination, last);
    }

    /// <summary>Copies at most 32 bytes with exact cached lanes and no long vector loop.</summary>
    /// <param name="destination">The valid writable byte range.</param>
    /// <param name="source">The valid readable non-overlapping byte range.</param>
    /// <param name="count">A byte count from zero through 32.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyShortBlock(ref byte destination, ref byte source, uint count)
    {
        if (count <= 16)
        {
            CopyScalarBlock(ref destination, ref source, count);
            return;
        }

        // This range needs only 128-bit lanes. Keep it inline without introducing the dirty
        // upper 256-bit state that the former outlined helper had to clear.
        Unsafe.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<Vector128<byte>>(ref source));
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nint)(count - 16)),
            Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref source, (nint)(count - 16))));
    }

    /// <summary>Copies at most sixteen bytes with scalar head and tail accesses inside the ranges.</summary>
    /// <param name="destination">The valid writable range on a 64-bit target.</param>
    /// <param name="source">The valid readable non-overlapping range.</param>
    /// <param name="count">A byte count from zero through sixteen.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyScalarBlock(ref byte destination, ref byte source, uint count)
    {
        if (count >= 8)
        {
            ulong head = Unsafe.ReadUnaligned<ulong>(ref source);
            ulong tail = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, (nuint)(count - 8)));
            Unsafe.WriteUnaligned(ref destination, head);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nuint)(count - 8)), tail);
        }
        else if (count >= 4)
        {
            uint head = Unsafe.ReadUnaligned<uint>(ref source);
            uint tail = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (nuint)(count - 4)));
            Unsafe.WriteUnaligned(ref destination, head);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nuint)(count - 4)), tail);
        }
        else if (count >= 2)
        {
            ushort head = Unsafe.ReadUnaligned<ushort>(ref source);
            byte tail = Unsafe.Add(ref source, (nuint)(count - 1));
            Unsafe.WriteUnaligned(ref destination, head);
            Unsafe.Add(ref destination, (nuint)(count - 1)) = tail;
        }
        else if (count != 0)
        {
            destination = source;
        }
    }

    /// <summary>Copies a large block with aligned full-line NT stores and a fence before unpinning.</summary>
    /// <param name="destination">The writable range pinned within this helper.</param>
    /// <param name="source">The complete readable non-overlapping range.</param>
    /// <param name="count">The exact byte count, at least one MiB, on x64 AVX2 or preferred AVX-512.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void CopyStreamingBlock(ref byte destination, ref byte source, uint count)
    {
        fixed (byte* destinationPointer = &destination)
        {
            byte* target = destinationPointer;
            ref byte input = ref source;
            uint prefix = (uint)(unchecked((nuint)0 - (nuint)target) & 63);
            if (prefix != 0)
            {
                CopyBlockUnaligned(ref *target, ref input, prefix);
                target += prefix;
                input = ref Unsafe.Add(ref input, (nint)prefix);
                count -= prefix;
            }

            if (Vector512.IsHardwareAccelerated && Avx512F.IsSupported)
            {
                while (count >= 256)
                {
                    Vector512<byte> a = Vector512.LoadUnsafe(ref input);
                    Vector512<byte> b = Vector512.LoadUnsafe(ref input, 64);
                    Vector512<byte> c = Vector512.LoadUnsafe(ref input, 128);
                    Vector512<byte> d = Vector512.LoadUnsafe(ref input, 192);
                    Avx512F.StoreAlignedNonTemporal(target, a);
                    Avx512F.StoreAlignedNonTemporal(target + 64, b);
                    Avx512F.StoreAlignedNonTemporal(target + 128, c);
                    Avx512F.StoreAlignedNonTemporal(target + 192, d);
                    target += 256;
                    input = ref Unsafe.Add(ref input, (nint)256);
                    count -= 256;
                }

                while (count >= 64)
                {
                    Avx512F.StoreAlignedNonTemporal(target, Vector512.LoadUnsafe(ref input));
                    target += 64;
                    input = ref Unsafe.Add(ref input, (nint)64);
                    count -= 64;
                }
            }
            else
            {
                while (count >= 256)
                {
                    Vector256<byte> a = Vector256.LoadUnsafe(ref input);
                    Vector256<byte> b = Vector256.LoadUnsafe(ref input, 32);
                    Vector256<byte> c = Vector256.LoadUnsafe(ref input, 64);
                    Vector256<byte> d = Vector256.LoadUnsafe(ref input, 96);
                    Vector256<byte> e = Vector256.LoadUnsafe(ref input, 128);
                    Vector256<byte> f = Vector256.LoadUnsafe(ref input, 160);
                    Vector256<byte> g = Vector256.LoadUnsafe(ref input, 192);
                    Vector256<byte> h = Vector256.LoadUnsafe(ref input, 224);
                    Avx.StoreAlignedNonTemporal(target, a);
                    Avx.StoreAlignedNonTemporal(target + 32, b);
                    Avx.StoreAlignedNonTemporal(target + 64, c);
                    Avx.StoreAlignedNonTemporal(target + 96, d);
                    Avx.StoreAlignedNonTemporal(target + 128, e);
                    Avx.StoreAlignedNonTemporal(target + 160, f);
                    Avx.StoreAlignedNonTemporal(target + 192, g);
                    Avx.StoreAlignedNonTemporal(target + 224, h);
                    target += 256;
                    input = ref Unsafe.Add(ref input, (nint)256);
                    count -= 256;
                }

                while (count >= 64)
                {
                    Avx.StoreAlignedNonTemporal(target, Vector256.LoadUnsafe(ref input));
                    Avx.StoreAlignedNonTemporal(target + 32, Vector256.LoadUnsafe(ref input, 32));
                    target += 64;
                    input = ref Unsafe.Add(ref input, (nint)64);
                    count -= 64;
                }

            }

            if (count != 0)
                CopyBlockUnaligned(ref *target, ref input, count);
            Sse.StoreFence();
        }
    }
#endif
}
