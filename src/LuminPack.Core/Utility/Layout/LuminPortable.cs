using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Utility;

/// <summary>Describes the storage of a portable struct for the vectorized padding-free transfer.</summary>
/// <remarks>Generated layouts are stateless and derive every offset from the running process's actual field addresses.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ILuminPortableLayout
{
    /// <summary>Gets the packed, declaration-ordered wire width of one element.</summary>
    int WireSize { get; }

    /// <summary>Writes, for each wire byte, the offset of the storage byte it carries.</summary>
    /// <param name="storageOffsets">One entry per wire byte.</param>
    void Describe(Span<int> storageOffsets);
}

/// <summary>Moves contiguous portable structs between their padded storage and the packed wire with byte shuffles.</summary>
/// <typeparam name="T">The portable struct.</typeparam>
/// <typeparam name="TLayout">The generated layout of <typeparamref name="T"/>.</typeparam>
/// <remarks>
/// <para>
/// The wire is unchanged: fields are declaration-ordered and unpadded on every architecture. A shuffle mask built once
/// from the actual field offsets gathers whole elements per 16-byte block, so padding removal costs one load, one shuffle
/// and one store for several small elements, or a few shuffles for an element of up to 32 bytes.
/// Eligible padded layouts on supported 64-bit hardware can use wider cached grouped permutations for complete prefixes.
/// </para>
/// <para>
/// Each block stores a full vector and later blocks overwrite the unused tail, so every call stops while a complete
/// vector still fits in both the source and the destination; the caller's field-wise loop finishes the remaining elements.
/// Modern CoreCLR and Native AOT targets with byte-shuffle hardware use the built-in kernel. The netstandard2.1 target
/// can use an explicitly registered native layout hook; without one it reports zero and keeps the established field-wise path.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPortable<T, TLayout>
    where TLayout : struct, ILuminPortableLayout
{
#if NETSTANDARD2_1
    /// <summary>Creates native byte descriptors only after a backend is explicitly installed.</summary>
    private static class NativeLayout
    {
        internal static readonly int Size = Unsafe.SizeOf<T>();
        internal static readonly int Wire = default(TLayout).WireSize;
        internal static readonly int[] Storage = Array.Empty<int>();
        internal static readonly int[] Packed = Array.Empty<int>();
        internal static readonly bool Valid;
        internal static readonly LuminLayoutPlan PackPlan, UnpackPlan;

        /// <summary>Snapshots and validates the generated field-address description.</summary>
        static NativeLayout()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || Wire <= 0) return;
            var offsets = new int[Wire];
            default(TLayout).Describe(offsets);
            foreach (int offset in offsets)
                if ((uint)offset >= (uint)Size) return;
            Storage = offsets;
            Packed = new int[Wire];
            for (int index = 0; index < Wire; index++) Packed[index] = index;
            Valid = true;
            LuminLayoutPlan.CreatePortable(offsets, Size, Wire, out PackPlan, out UnpackPlan);
        }
    }

#endif
#if NET8_0_OR_GREATER
    /// <summary>Zero for the field-wise path, one for several elements per vector, two for one element in two vectors.</summary>
    private static readonly int Mode;
    /// <summary>The elements gathered by one single-vector block.</summary>
    private static readonly int PerBlock;
    /// <summary>The storage and wire widths.</summary>
    private static readonly int Size, Wire;
    /// <summary>The fewest remaining elements whose storage and wire both cover every full-vector access of one block.</summary>
    private static readonly int Reserve;
    /// <summary>Pack masks: output vector <c>o</c> takes storage vector <c>i</c> through <c>Pack[o * 2 + i]</c>.</summary>
    private static readonly Vector128<byte> Pack0, Pack1, Pack2, Pack3;
    /// <summary>Unpack masks: storage vector <c>o</c> takes wire vector <c>i</c> through <c>Unpack[o * 2 + i]</c>.</summary>
    private static readonly Vector128<byte> Unpack0, Unpack1, Unpack2, Unpack3;
    private static readonly LuminGroupedLayout? GroupedPack, GroupedUnpack;

    /// <summary>Builds the masks once from the running process's actual storage offsets.</summary>
    static LuminPortable()
    {
        int size = Unsafe.SizeOf<T>(), wire = default(TLayout).WireSize;
        if (!(Ssse3.IsSupported || AdvSimd.Arm64.IsSupported) || wire <= 0 || size > 32 || wire > 32)
            return;
        Span<int> offsets = stackalloc int[wire];
        default(TLayout).Describe(offsets);
        foreach (int offset in offsets)
            if ((uint)offset >= (uint)size)
                return;
        LuminLayoutPlan.CreatePortable(offsets, size, wire, out var pack, out var unpack);
        if (pack.Mode == 0) return;
        Size = size; Wire = wire; Mode = pack.Mode; PerBlock = pack.PerBlock; Reserve = pack.Reserve;
        Pack0 = Vector128.Create(pack.Mask0Low, pack.Mask0High).AsByte();
        Pack1 = Vector128.Create(pack.Mask1Low, pack.Mask1High).AsByte();
        Pack2 = Vector128.Create(pack.Mask2Low, pack.Mask2High).AsByte();
        Pack3 = Vector128.Create(pack.Mask3Low, pack.Mask3High).AsByte();
        Unpack0 = Vector128.Create(unpack.Mask0Low, unpack.Mask0High).AsByte();
        Unpack1 = Vector128.Create(unpack.Mask1Low, unpack.Mask1High).AsByte();
        Unpack2 = Vector128.Create(unpack.Mask2Low, unpack.Mask2High).AsByte();
        Unpack3 = Vector128.Create(unpack.Mask3Low, unpack.Mask3High).AsByte();
        Span<int> packed = stackalloc int[wire];
        for (int i = 0; i < wire; i++) packed[i] = i;
        GroupedPack = LuminGroupedLayout.Create(offsets, size, packed, wire);
        GroupedUnpack = LuminGroupedLayout.Create(packed, wire, offsets, size);
    }

    /// <summary>Selects bytes by index, producing zero for out-of-range indices on every supported instruction set.</summary>
    /// <param name="source">The source vector.</param>
    /// <param name="indices">The per-byte indices, 0xFF for zero.</param>
    /// <returns>The gathered bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Shuffle(Vector128<byte> source, Vector128<byte> indices)
        => Ssse3.IsSupported ? Ssse3.Shuffle(source, indices) : AdvSimd.Arm64.VectorTableLookup(source, indices);
#endif

    /// <summary>Packs leading elements into the wire.</summary>
    /// <param name="source">The first storage element.</param>
    /// <param name="count">The element count.</param>
    /// <param name="destination">The first wire byte of a reserved <c>count * WireSize</c> payload.</param>
    /// <returns>The number of leading elements written; the caller writes the rest field-wise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Pack(ref T source, int count, ref byte destination)
    {
#if NETSTANDARD2_1
        if (count > 0 && global::LuminPack.Core.LuminPackBurst.UseLayout(count) && NativeLayout.Valid)
            return global::LuminPack.Core.LuminPackBurst.TryLayout(ref Unsafe.As<T, byte>(ref source), ref destination,
                count, NativeLayout.Size, NativeLayout.Wire, NativeLayout.Storage, NativeLayout.Packed, in NativeLayout.PackPlan);
#endif
#if NET8_0_OR_GREATER
        if (Mode == 0 || count <= 0)
            return 0;
        if (GroupedPack is { } grouped && count >= grouped.Reserve)
            return grouped.ConvertPrefix(ref Unsafe.As<T, byte>(ref source), ref destination, count);
        return PackCore(ref Unsafe.As<T, byte>(ref source), count, ref destination);
#else
        return 0;
#endif
    }

    /// <summary>Unpacks leading elements from the wire.</summary>
    /// <param name="source">The first wire byte of a validated <c>count * WireSize</c> payload.</param>
    /// <param name="count">The element count.</param>
    /// <param name="destination">The first storage element.</param>
    /// <returns>The number of leading elements read; the caller reads the rest field-wise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Unpack(ref byte source, int count, ref T destination)
    {
#if NETSTANDARD2_1
        if (count > 0 && global::LuminPack.Core.LuminPackBurst.UseLayout(count) && NativeLayout.Valid)
            return global::LuminPack.Core.LuminPackBurst.TryLayout(ref source, ref Unsafe.As<T, byte>(ref destination),
                count, NativeLayout.Wire, NativeLayout.Size, NativeLayout.Packed, NativeLayout.Storage, in NativeLayout.UnpackPlan);
#endif
#if NET8_0_OR_GREATER
        if (Mode == 0 || count <= 0)
            return 0;
        if (GroupedUnpack is { } grouped && count >= grouped.Reserve)
            return grouped.ConvertPrefix(ref source, ref Unsafe.As<T, byte>(ref destination), count);
        return UnpackCore(ref source, count, ref Unsafe.As<T, byte>(ref destination));
#else
        return 0;
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>Counts the blocks whose full-vector accesses stay inside both buffers.</summary>
    /// <param name="count">The element count.</param>
    /// <returns>The number of blocks of <see cref="PerBlock"/> elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint Blocks(int count) => count < Reserve ? 0 : (count - Reserve) / PerBlock + 1;

    /// <summary>Gathers elements while a complete vector fits in both the storage and the wire.</summary>
    /// <param name="input">The first storage byte.</param>
    /// <param name="count">The element count.</param>
    /// <param name="output">The first wire byte.</param>
    /// <returns>The elements written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int PackCore(ref byte input, int count, ref byte output)
    {
        nint blocks = Blocks(count);
        nint inputStep = (nint)PerBlock * Size, outputStep = (nint)PerBlock * Wire;
        int done = (int)blocks * PerBlock;
        nint initial = blocks;
        LuminByteShuffle.CopyWide(ref input, ref output, ref blocks, inputStep, outputStep, Mode == 2, Wire > 16, Pack0, Pack1, Pack2, Pack3);
        input = ref Unsafe.Add(ref input, (initial - blocks) * inputStep);
        output = ref Unsafe.Add(ref output, (initial - blocks) * outputStep);
        var pack0 = Pack0;
        if (Mode == 1)
        {
            for (; blocks >= 2; blocks -= 2)
            {
                var first = Vector128.LoadUnsafe(ref input);
                var second = Vector128.LoadUnsafe(ref input, (nuint)inputStep);
                Shuffle(first, pack0).StoreUnsafe(ref output);
                Shuffle(second, pack0).StoreUnsafe(ref output, (nuint)outputStep);
                input = ref Unsafe.Add(ref input, inputStep * 2);
                output = ref Unsafe.Add(ref output, outputStep * 2);
            }
            if (blocks != 0)
                Shuffle(Vector128.LoadUnsafe(ref input), pack0).StoreUnsafe(ref output);
            return done;
        }

        var pack1 = Pack1;
        if (Wire <= 16)
        {
            for (; blocks != 0; blocks--)
            {
                (Shuffle(Vector128.LoadUnsafe(ref input), pack0) | Shuffle(Vector128.LoadUnsafe(ref input, 16), pack1)).StoreUnsafe(ref output);
                input = ref Unsafe.Add(ref input, inputStep);
                output = ref Unsafe.Add(ref output, outputStep);
            }
            return done;
        }

        var pack2 = Pack2; var pack3 = Pack3;
        for (; blocks != 0; blocks--)
        {
            var low = Vector128.LoadUnsafe(ref input);
            var high = Vector128.LoadUnsafe(ref input, 16);
            (Shuffle(low, pack0) | Shuffle(high, pack1)).StoreUnsafe(ref output);
            (Shuffle(low, pack2) | Shuffle(high, pack3)).StoreUnsafe(ref output, 16);
            input = ref Unsafe.Add(ref input, inputStep);
            output = ref Unsafe.Add(ref output, outputStep);
        }
        return done;
    }

    /// <summary>Scatters elements while a complete vector fits in both the wire and the storage.</summary>
    /// <param name="input">The first wire byte.</param>
    /// <param name="count">The element count.</param>
    /// <param name="output">The first storage byte.</param>
    /// <returns>The elements read.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int UnpackCore(ref byte input, int count, ref byte output)
    {
        nint blocks = Blocks(count);
        nint inputStep = (nint)PerBlock * Wire, outputStep = (nint)PerBlock * Size;
        int done = (int)blocks * PerBlock;
        nint initial = blocks;
        LuminByteShuffle.CopyWide(ref input, ref output, ref blocks, inputStep, outputStep, Mode == 2 && Wire > 16, Mode == 2, Unpack0, Unpack1, Unpack2, Unpack3);
        input = ref Unsafe.Add(ref input, (initial - blocks) * inputStep);
        output = ref Unsafe.Add(ref output, (initial - blocks) * outputStep);
        var unpack0 = Unpack0;
        if (Mode == 1)
        {
            for (; blocks >= 2; blocks -= 2)
            {
                var first = Vector128.LoadUnsafe(ref input);
                var second = Vector128.LoadUnsafe(ref input, (nuint)inputStep);
                Shuffle(first, unpack0).StoreUnsafe(ref output);
                Shuffle(second, unpack0).StoreUnsafe(ref output, (nuint)outputStep);
                input = ref Unsafe.Add(ref input, inputStep * 2);
                output = ref Unsafe.Add(ref output, outputStep * 2);
            }
            if (blocks != 0)
                Shuffle(Vector128.LoadUnsafe(ref input), unpack0).StoreUnsafe(ref output);
            return done;
        }

        var unpack1 = Unpack1; var unpack2 = Unpack2; var unpack3 = Unpack3;
        if (Wire <= 16)
        {
            for (; blocks != 0; blocks--)
            {
                var packed = Vector128.LoadUnsafe(ref input);
                Shuffle(packed, unpack0).StoreUnsafe(ref output);
                Shuffle(packed, unpack2).StoreUnsafe(ref output, 16);
                input = ref Unsafe.Add(ref input, inputStep);
                output = ref Unsafe.Add(ref output, outputStep);
            }
            return done;
        }

        for (; blocks != 0; blocks--)
        {
            var low = Vector128.LoadUnsafe(ref input);
            var high = Vector128.LoadUnsafe(ref input, 16);
            (Shuffle(low, unpack0) | Shuffle(high, unpack1)).StoreUnsafe(ref output);
            (Shuffle(low, unpack2) | Shuffle(high, unpack3)).StoreUnsafe(ref output, 16);
            input = ref Unsafe.Add(ref input, inputStep);
            output = ref Unsafe.Add(ref output, outputStep);
        }
        return done;
    }
#endif
}

