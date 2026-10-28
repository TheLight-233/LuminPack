using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace LuminPack.Utility;
/// <summary>Contains a complete immutable shuffle plan shared by Core and optional native backends.</summary>
/// <remarks>This process-local descriptor is not serialized. Native callbacks receive it only while its owner is pinned.</remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct LuminLayoutPlan
{
    /// <summary>The complete input stride.</summary>
    public readonly int SourceStride;
    /// <summary>The complete output stride.</summary>
    public readonly int DestinationStride;
    /// <summary>Zero for byte runs, one for grouped elements, two for a wide element.</summary>
    public readonly int Mode;
    /// <summary>The elements processed by one bounded shuffle.</summary>
    public readonly int PerBlock;
    /// <summary>The minimum elements covering every whole-vector access.</summary>
    public readonly int Reserve;
    /// <summary>The complete input vectors touched by one block.</summary>
    public readonly int InputVectors;
    /// <summary>The complete output vectors touched by one block.</summary>
    public readonly int OutputVectors;
    /// <summary>The first eight bytes of the low-input/low-output mask.</summary>
    public readonly ulong Mask0Low;
    /// <summary>The last eight bytes of the low-input/low-output mask.</summary>
    public readonly ulong Mask0High;
    /// <summary>The first eight bytes of the high-input/low-output mask.</summary>
    public readonly ulong Mask1Low;
    /// <summary>The last eight bytes of the high-input/low-output mask.</summary>
    public readonly ulong Mask1High;
    /// <summary>The first eight bytes of the low-input/high-output mask.</summary>
    public readonly ulong Mask2Low;
    /// <summary>The last eight bytes of the low-input/high-output mask.</summary>
    public readonly ulong Mask2High;
    /// <summary>The first eight bytes of the high-input/high-output mask.</summary>
    public readonly ulong Mask3Low;
    /// <summary>The last eight bytes of the high-input/high-output mask.</summary>
    public readonly ulong Mask3High;
    /// <summary>Captures the complete scalar plan and exact mask bytes.</summary>
    /// <param name="sourceStride">Input stride.</param><param name="destinationStride">Output stride.</param><param name="mode">Kernel mode.</param><param name="perBlock">Elements per block.</param><param name="reserve">Access reserve.</param><param name="inputVectors">Input vector count.</param><param name="outputVectors">Output vector count.</param><param name="masks">Four sixteen-byte masks.</param>
    private LuminLayoutPlan(int sourceStride, int destinationStride, int mode, int perBlock, int reserve, int inputVectors, int outputVectors, ReadOnlySpan<byte> masks)
    {
        SourceStride = sourceStride; DestinationStride = destinationStride; Mode = mode; PerBlock = perBlock;
        Reserve = reserve; InputVectors = inputVectors; OutputVectors = outputVectors;
        Mask0Low = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks)); Mask0High = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks.Slice(8)));
        Mask1Low = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks.Slice(16))); Mask1High = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks.Slice(24)));
        Mask2Low = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks.Slice(32))); Mask2High = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks.Slice(40)));
        Mask3Low = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks.Slice(48))); Mask3High = global::LuminPack.Core.LuminPackReader.Read<ulong>(ref MemoryMarshal.GetReference(masks.Slice(56)));
    }
    /// <summary>Builds Core's original bounded masks once for an immutable layout.</summary>
    /// <param name="source">Source offsets.</param><param name="sourceStride">Input stride.</param><param name="destination">Destination offsets.</param><param name="destinationStride">Output stride.</param><param name="segments">Established contiguous run count.</param>
    /// <returns>The established plan, or a zero-mode descriptor when ineligible.</returns>
    public static LuminLayoutPlan Create(ReadOnlySpan<int> source, int sourceStride, ReadOnlySpan<int> destination, int destinationStride, int segments)
    {
        if (source.Length == 0 || source.Length != destination.Length || sourceStride > 32 || destinationStride > 32 || sourceStride <= 0 || destinationStride <= 0) return default;
        for (int index = 0; index < source.Length; index++)
            if ((uint)source[index] >= (uint)sourceStride || (uint)destination[index] >= (uint)destinationStride) return default;
        Span<byte> masks = stackalloc byte[64];
        masks.Fill(0xFF);
        int mode, perBlock, inputVectors, outputVectors;
        if (sourceStride <= 16 && destinationStride <= 16)
        {
            if (segments < 2) return default;
            perBlock = Math.Min(16 / sourceStride, 16 / destinationStride);
            for (int element = 0; element < perBlock; element++)
                for (int index = 0; index < source.Length; index++)
                    masks[element * destinationStride + destination[index]] = (byte)(element * sourceStride + source[index]);
            inputVectors = outputVectors = 1;
            mode = 1;
        }
        else
        {
            if (segments < 3) return default;
            perBlock = 1;
            inputVectors = sourceStride > 16 ? 2 : 1;
            outputVectors = destinationStride > 16 ? 2 : 1;
            for (int index = 0; index < source.Length; index++)
            {
                int target = destination[index], from = source[index];
                masks[target / 16 * 32 + from / 16 * 16 + target % 16] = (byte)(from % 16);
                masks[target / 16 * 32 + (1 - from / 16) * 16 + target % 16] = 0xFF;
            }
            mode = 2;
        }
        int reserve = Math.Max(perBlock, Math.Max((inputVectors * 16 + sourceStride - 1) / sourceStride, (outputVectors * 16 + destinationStride - 1) / destinationStride));
        return new LuminLayoutPlan(sourceStride, destinationStride, mode, perBlock, reserve, inputVectors, outputVectors, masks);
    }

    /// <summary>Builds the established portable pack/unpack masks from one captured field-address description.</summary>
    /// <param name="offsets">Storage offsets in wire order.</param><param name="size">Native element width.</param><param name="wire">Packed width.</param>
    /// <param name="packPlan">Receives the original pack plan.</param><param name="unpackPlan">Receives the original unpack plan.</param>
    internal static void CreatePortable(ReadOnlySpan<int> offsets, int size, int wire, out LuminLayoutPlan packPlan, out LuminLayoutPlan unpackPlan)
    {
        packPlan = unpackPlan = default;
        if (wire <= 0 || size <= 0 || size > 32 || wire > 32 || offsets.Length != wire) return;
        foreach (int offset in offsets)
            if ((uint)offset >= (uint)size) return;
        Span<byte> pack = stackalloc byte[64], unpack = stackalloc byte[64];
        pack.Fill(0xFF); unpack.Fill(0xFF);
        int mode, perBlock, reserve;
        if (size <= 16 && wire <= 16)
        {
            perBlock = Math.Min(16 / size, 16 / wire);
            for (int element = 0; element < perBlock; element++)
                for (int index = 0; index < wire; index++)
                {
                    pack[element * wire + index] = (byte)(element * size + offsets[index]);
                    unpack[element * size + offsets[index]] = (byte)(element * wire + index);
                }
            reserve = Math.Max(perBlock, Math.Max((16 + size - 1) / size, (16 + wire - 1) / wire));
            mode = 1;
        }
        else
        {
            int runs = 1;
            for (int index = 1; index < wire; index++)
                if (offsets[index] != offsets[index - 1] + 1) runs++;
            if (runs < 4) return;
            for (int index = 0; index < wire; index++)
            {
                int offset = offsets[index];
                pack[index / 16 * 32 + offset / 16 * 16 + index % 16] = (byte)(offset % 16);
                int storage = offset / 16 * 32 + offset % 16;
                unpack[storage + index / 16 * 16] = (byte)(index % 16);
                unpack[storage + (1 - index / 16) * 16] = 0xFF;
            }
            perBlock = 1;
            reserve = Math.Max((32 + size - 1) / size, ((wire + 15) / 16 * 16 + wire - 1) / wire);
            mode = 2;
        }
        int nativeVectors = mode == 2 ? 2 : 1, wireVectors = (wire + 15) / 16;
        packPlan = new LuminLayoutPlan(size, wire, mode, perBlock, reserve, nativeVectors, wireVectors, pack);
        unpackPlan = new LuminLayoutPlan(wire, size, mode, perBlock, reserve, wireVectors, nativeVectors, unpack);
    }
}


