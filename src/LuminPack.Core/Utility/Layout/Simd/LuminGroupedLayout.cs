#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.InteropServices;

namespace LuminPack.Utility;

/// <summary>Caches bounded grouped permutations for padded layouts on supported 64-bit hardware.</summary>
internal sealed class LuminGroupedLayout
{
    internal readonly int SourceStride, DestinationStride, PerBlock, Reserve, InputVectors, OutputVectors, Width;
    internal readonly int[][] Mapping;
    internal readonly Vector256<byte>[,] Masks256;
    internal readonly int[] Banks;
    internal readonly Vector256<int>[] Dwords256;
    internal readonly Vector256<byte>[] Select256, Valid256;
    internal readonly Vector512<byte>[] Indices512, Valid512;
    internal readonly Vector512<int>[] Dwords512;
    internal readonly Vector128<byte>[] Indices128;
    internal readonly bool DwordEligible, HasPadding;
    private readonly int _kind;

    /// <summary>Builds one immutable hardware-width plan from validated byte offsets.</summary>
    /// <param name="sourceOffsets">Source offsets in declaration order.</param>
    /// <param name="sourceStride">Complete source element width.</param>
    /// <param name="destinationOffsets">Destination offsets in the same declaration order.</param>
    /// <param name="destinationStride">Complete destination element width.</param>
    /// <param name="width">The selected native vector width in bytes.</param>
    private LuminGroupedLayout(ReadOnlySpan<int> sourceOffsets, int sourceStride, ReadOnlySpan<int> destinationOffsets, int destinationStride, int width)
    {
        Width = width;
        SourceStride = sourceStride;
        DestinationStride = destinationStride;
        int window = width == 16 ? 64 : width * 2;
        PerBlock = window / Math.Max(sourceStride, destinationStride);
        InputVectors = (PerBlock * SourceStride + width - 1) / width;
        OutputVectors = (PerBlock * DestinationStride + width - 1) / width;
        Reserve = Math.Max(PerBlock, Math.Max((InputVectors * width + SourceStride - 1) / SourceStride, (OutputVectors * width + DestinationStride - 1) / DestinationStride));
        Mapping = new int[OutputVectors][];
        for (int v = 0; v < OutputVectors; v++) Mapping[v] = Enumerable.Repeat(-1, width).ToArray();
        for (int e = 0; e < PerBlock; e++)
            for (int j = 0; j < sourceOffsets.Length; j++)
            {
                int from = e * SourceStride + sourceOffsets[j];
                int to = e * DestinationStride + destinationOffsets[j];
                Mapping[to / width][to % width] = from; // Later overlapping fields retain their established precedence.
            }
        HasPadding = Mapping.Any(m => m.Any(i => i < 0));
        DwordEligible = Mapping.All(m => Enumerable.Range(0, width / 4).All(d =>
            Enumerable.Range(0, 4).All(b => m[d * 4 + b] < 0) ||
            (m[d * 4] >= 0 && m[d * 4] % 4 == 0 && Enumerable.Range(0, 4).All(b => m[d * 4 + b] == m[d * 4] + b))));
        _kind = width == 64 ? (Avx512Vbmi.IsSupported ? 3 : 4) : width == 16 ? 5 : DwordEligible ? 2 : 1;
        Banks = new int[OutputVectors];
        Masks256 = new Vector256<byte>[OutputVectors, 4];
        Dwords256 = new Vector256<int>[OutputVectors]; Select256 = new Vector256<byte>[OutputVectors]; Valid256 = new Vector256<byte>[OutputVectors];
        Indices512 = new Vector512<byte>[OutputVectors]; Valid512 = new Vector512<byte>[OutputVectors]; Dwords512 = new Vector512<int>[OutputVectors];
        Indices128 = new Vector128<byte>[OutputVectors];
        for (int v = 0; v < OutputVectors; v++)
        {
            var map = Mapping[v];
            if (width == 16) Indices128[v] = Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(map.Select(i => i < 0 ? (byte)255 : (byte)i).ToArray()));
            if (width == 32)
            {
                for (int bank = 0; bank < 4; bank++)
                {
                    byte[] mask = Enumerable.Repeat((byte)255, 32).ToArray();
                    for (int j = 0; j < 32; j++)
                    {
                        int from = map[j];
                        if (from < 0) continue;
                        int which = (from / 32) * 2 + ((from / 16 % 2) ^ (j / 16));
                        if (which != bank) continue;
                        mask[j] = (byte)(from % 16); Banks[v] |= 1 << bank;
                    }
                    Masks256[v, bank] = Vector256.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(mask));
                }
                Dwords256[v] = Vector256.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(Enumerable.Range(0, 8).Select(d => Math.Max(0, map[d * 4]) / 4).ToArray()));
                Select256[v] = Vector256.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(map.Select(i => i >= 32 ? (byte)255 : (byte)0).ToArray()));
                Valid256[v] = Vector256.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(map.Select(i => i >= 0 ? (byte)255 : (byte)0).ToArray()));
            }
            if (width == 64)
            {
                Indices512[v] = Vector512.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(map.Select(i => (byte)Math.Max(0, i)).ToArray()));
                Valid512[v] = Vector512.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(map.Select(i => i >= 0 ? (byte)255 : (byte)0).ToArray()));
                Dwords512[v] = Vector512.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(Enumerable.Range(0, 16).Select(d => Math.Max(0, map[d * 4]) / 4).ToArray()));
            }
        }
    }

    /// <summary>Selects a grouped plan while retaining the original path for unsupported shapes and hardware.</summary>
    /// <param name="source">Source byte offsets.</param><param name="sourceStride">Complete input stride.</param>
    /// <param name="destination">Destination byte offsets.</param><param name="destinationStride">Complete output stride.</param>
    /// <returns>A cached grouped plan, or null for the established fallback.</returns>
    internal static LuminGroupedLayout? Create(ReadOnlySpan<int> source, int sourceStride, ReadOnlySpan<int> destination, int destinationStride)
    {
        if (Unsafe.SizeOf<IntPtr>() != 8 || sourceStride == destinationStride || sourceStride <= 0 || destinationStride <= 0 ||
            sourceStride > 32 || destinationStride > 32 || source.Length == 0 || source.Length != destination.Length)
            return null;
        for (int i = 0; i < source.Length; i++)
            if ((uint)source[i] >= (uint)sourceStride || (uint)destination[i] >= (uint)destinationStride) return null;
        if (Vector512.IsHardwareAccelerated && Avx512F.IsSupported)
        {
            var plan = new LuminGroupedLayout(source, sourceStride, destination, destinationStride, 64);
            if (Avx512Vbmi.IsSupported || plan.DwordEligible) return plan;
        }
        if (Vector256.IsHardwareAccelerated && Avx2.IsSupported)
            return new LuminGroupedLayout(source, sourceStride, destination, destinationStride, 32);
        return AdvSimd.Arm64.IsSupported ? new LuminGroupedLayout(source, sourceStride, destination, destinationStride, 16) : null;
    }

    /// <summary>Converts only complete bounded groups; the caller retains its original field or byte-run tail.</summary>
    /// <param name="source">The complete validated input owner.</param>
    /// <param name="destination">The complete validated output owner.</param>
    /// <param name="count">The logical element count.</param>
    /// <returns>The completed prefix length in elements.</returns>
    internal int ConvertPrefix(ref byte source, ref byte destination, int count)
        => _kind switch
        {
            1 when Avx2.IsSupported => Avx2Bytes(ref source, ref destination, count),
            2 when Avx2.IsSupported => Avx2Dwords(ref source, ref destination, count),
            3 when Avx512F.IsSupported && Avx512Vbmi.IsSupported && Vector512.IsHardwareAccelerated => Vbmi(ref source, ref destination, count),
            4 when Avx512F.IsSupported && Vector512.IsHardwareAccelerated => Avx512Dwords(ref source, ref destination, count),
            5 when AdvSimd.Arm64.IsSupported => ArmTable(ref source, ref destination, count),
            _ => 0
        };

    /// <summary>Processes AVX2 groups using lane swaps and two-source byte shuffles.</summary>
    /// <param name="source">Validated source owner.</param><param name="destination">Validated destination owner.</param><param name="count">Element count.</param>
    /// <returns>The completed prefix length in elements.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int Avx2Bytes(ref byte source, ref byte destination, int count)
    {
        if (!Avx2.IsSupported) return 0;
        int done = 0;
        if (count >= Reserve)
        {
            int blocks = (count - Reserve) / PerBlock + 1;
            nint si = 0, di = 0;
            var a0 = Masks256[0,0]; var a1 = Masks256[0,1]; var a2 = Masks256[0,2]; var a3 = Masks256[0,3];
            var b0 = OutputVectors > 1 ? Masks256[1,0] : default; var b1 = OutputVectors > 1 ? Masks256[1,1] : default;
            var b2 = OutputVectors > 1 ? Masks256[1,2] : default; var b3 = OutputVectors > 1 ? Masks256[1,3] : default;
            int used0 = Banks[0], used1 = OutputVectors > 1 ? Banks[1] : 0;
            for (int b = 0; b < blocks; b++)
            {
                var low = Vector256.LoadUnsafe(ref source, (nuint)si);
                var high = InputVectors == 2 ? Vector256.LoadUnsafe(ref source, (nuint)(si + 32)) : default;
                var swapLow = ((used0 | used1) & 2) != 0 ? Avx2.Permute2x128(low, low, 1) : default;
                var swapHigh = ((used0 | used1) & 8) != 0 ? Avx2.Permute2x128(high, high, 1) : default;
                var first = Avx2.Shuffle(low, a0);
                if ((used0 & 2) != 0) first |= Avx2.Shuffle(swapLow, a1);
                if ((used0 & 4) != 0) first |= Avx2.Shuffle(high, a2);
                if ((used0 & 8) != 0) first |= Avx2.Shuffle(swapHigh, a3);
                first.StoreUnsafe(ref destination, (nuint)di);
                if (OutputVectors == 2)
                {
                    var second = Avx2.Shuffle(low, b0);
                    if ((used1 & 2) != 0) second |= Avx2.Shuffle(swapLow, b1);
                    if ((used1 & 4) != 0) second |= Avx2.Shuffle(high, b2);
                    if ((used1 & 8) != 0) second |= Avx2.Shuffle(swapHigh, b3);
                    second.StoreUnsafe(ref destination, (nuint)(di + 32));
                }
                si += (nint)(uint)PerBlock * SourceStride; di += (nint)(uint)PerBlock * DestinationStride;
            }
            done = blocks * PerBlock;
        }
        return done;
    }

    /// <summary>Processes only layouts representable by complete aligned dwords.</summary>
    /// <param name="source">Validated input.</param><param name="destination">Validated output.</param><param name="count">Count.</param>
    /// <returns>The completed prefix length in elements.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int Avx2Dwords(ref byte source, ref byte destination, int count)
    {
        if (!Avx2.IsSupported || !DwordEligible) return 0;
        var i0 = Dwords256[0]; var i1 = OutputVectors > 1 ? Dwords256[1] : default;
        var s0 = Select256[0]; var s1 = OutputVectors > 1 ? Select256[1] : default;
        var z0 = Valid256[0]; var z1 = OutputVectors > 1 ? Valid256[1] : default;
        int done = 0;
        for (; count - done >= Reserve; done += PerBlock)
        {
            nuint si = (nuint)(uint)done * (uint)SourceStride, di = (nuint)(uint)done * (uint)DestinationStride;
            var low = Vector256.LoadUnsafe(ref source, si).AsInt32();
            var high = InputVectors == 2 ? Vector256.LoadUnsafe(ref source, si + 32).AsInt32() : default;
            var first = Avx2.PermuteVar8x32(low, i0).AsByte();
            if (InputVectors == 2) first = Avx2.BlendVariable(first, Avx2.PermuteVar8x32(high, i0).AsByte(), s0);
            if (HasPadding) first &= z0;
            first.StoreUnsafe(ref destination, di);
            if (OutputVectors == 2)
            {
                var second = Avx2.PermuteVar8x32(low, i1).AsByte();
                if (InputVectors == 2) second = Avx2.BlendVariable(second, Avx2.PermuteVar8x32(high, i1).AsByte(), s1);
                if (HasPadding) second &= z1;
                second.StoreUnsafe(ref destination, di + 32);
            }
        }
        return done;
    }

    /// <summary>Processes full byte-table groups on actual AVX512VBMI hardware.</summary>
    /// <param name="source">Validated input.</param><param name="destination">Validated output.</param><param name="count">Count.</param>
    /// <returns>The completed prefix length in elements.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int Vbmi(ref byte source, ref byte destination, int count)
    {
        if (!Avx512Vbmi.IsSupported || !Vector512.IsHardwareAccelerated) return 0;
        int done = 0;
        for (; count - done >= Reserve; done += PerBlock)
        {
            nuint si = (nuint)(uint)done * (uint)SourceStride, di = (nuint)(uint)done * (uint)DestinationStride;
            var low = Vector512.LoadUnsafe(ref source, si);
            var high = InputVectors == 2 ? Vector512.LoadUnsafe(ref source, si + 64) : default;
            for (int v = 0; v < OutputVectors; v++)
            {
                var value = InputVectors == 2 ? Avx512Vbmi.PermuteVar64x8x2(low, Indices512[v], high) : Avx512Vbmi.PermuteVar64x8(low, Indices512[v]);
                if (HasPadding) value &= Valid512[v];
                value.StoreUnsafe(ref destination, di + (nuint)(uint)(v * 64));
            }
        }
        return done;
    }

    /// <summary>Processes dword-aligned groups using AVX512F rather than requiring VBMI.</summary>
    /// <param name="source">Validated input.</param><param name="destination">Validated output.</param><param name="count">Count.</param>
    /// <returns>The completed prefix length in elements.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int Avx512Dwords(ref byte source, ref byte destination, int count)
    {
        if (!Avx512F.IsSupported || !Vector512.IsHardwareAccelerated || !DwordEligible) return 0;
        int done = 0;
        for (; count - done >= Reserve; done += PerBlock)
        {
            nuint si = (nuint)(uint)done * (uint)SourceStride, di = (nuint)(uint)done * (uint)DestinationStride;
            var low = Vector512.LoadUnsafe(ref source, si).AsInt32();
            var high = InputVectors == 2 ? Vector512.LoadUnsafe(ref source, si + 64).AsInt32() : default;
            for (int v = 0; v < OutputVectors; v++)
            {
                var value = InputVectors == 2 ? Avx512F.PermuteVar16x32x2(low, Dwords512[v], high) : Avx512F.PermuteVar16x32(low, Dwords512[v]);
                var bytes = value.AsByte(); if (HasPadding) bytes &= Valid512[v];
                bytes.StoreUnsafe(ref destination, di + (nuint)(uint)(v * 64));
            }
        }
        return done;
    }

    /// <summary>Processes a table of up to four contiguous ARM64 vector registers.</summary>
    /// <param name="source">Validated input.</param><param name="destination">Validated output.</param><param name="count">Count.</param>
    /// <returns>The completed prefix length in elements.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ArmTable(ref byte source, ref byte destination, int count)
    {
        if (!AdvSimd.Arm64.IsSupported) return 0;
        int done = 0;
        for (; count - done >= Reserve; done += PerBlock)
        {
            nuint si = (nuint)(uint)done * (uint)SourceStride, di = (nuint)(uint)done * (uint)DestinationStride;
            var a = Vector128.LoadUnsafe(ref source, si);
            var b = InputVectors > 1 ? Vector128.LoadUnsafe(ref source, si + 16) : default;
            var c = InputVectors > 2 ? Vector128.LoadUnsafe(ref source, si + 32) : default;
            var d = InputVectors > 3 ? Vector128.LoadUnsafe(ref source, si + 48) : default;
            for (int v = 0; v < OutputVectors; v++)
                AdvSimd.Arm64.VectorTableLookup((a,b,c,d), Indices128[v]).StoreUnsafe(ref destination, di + (nuint)(uint)(v * 16));
        }
        return done;
    }
}

#endif
