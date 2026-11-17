#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Utility;

public static partial class LuminTreeStorage
{
    /// <summary>Transfers the observed nonoverlapping fields of a padded pair between checked, disjoint windows.</summary>
    /// <typeparam name="TKey">The already verified native key format.</typeparam>
    /// <typeparam name="TValue">The already verified native value format.</typeparam>
    private static class PaddedPairLayout<TKey, TValue> where TKey : unmanaged where TValue : unmanaged
    {
        private static readonly int KeyOffset = LuminFieldLayout.ValueField(typeof(KeyValuePair<TKey, TValue>), "key");
        private static readonly int ValueOffset = LuminFieldLayout.ValueField(typeof(KeyValuePair<TKey, TValue>), "value");
        private static readonly bool Available = CheckLayout();
        private static readonly Vector128<byte> PackMask = Available ? CreateMask(expand: false) : default;
        private static readonly Vector128<byte> UnpackMask = Available ? CreateMask(expand: true) : default;

        private static int Stride => Unsafe.SizeOf<KeyValuePair<TKey, TValue>>();
        private static int KeyWidth => Unsafe.SizeOf<TKey>();
        private static int Width => KeyWidth + Unsafe.SizeOf<TValue>();

        /// <summary>Checks the actual fields and excludes layouts outside one complete shuffle lane.</summary>
        /// <returns>Whether the padded storage supports a bounded lane transfer.</returns>
        private static bool CheckLayout() => Stride is 4 or 8 or 16 && Width < Stride &&
            (uint)KeyOffset <= (uint)(Stride - KeyWidth) &&
            (uint)ValueOffset <= (uint)(Stride - Unsafe.SizeOf<TValue>()) &&
            (KeyOffset + KeyWidth <= ValueOffset || ValueOffset + Unsafe.SizeOf<TValue>() <= KeyOffset);

        /// <summary>Selects only batches large enough to benefit from the available transfer.</summary>
        /// <param name="count">The number of complete source records.</param>
        /// <returns>Whether the current hardware and pair layout support the transfer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool CanTransfer(int count) => (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported) &&
            (Stride is 4 or 8 || Stride == 16 && count >= 256 && (Avx2.IsSupported || Avx512BW.IsSupported)) && Available;

        /// <summary>Writes a validated wire window when its typed staging storage is disjoint.</summary>
        /// <param name="writer">The writer after reserving the complete wire window.</param>
        /// <param name="pairs">The staging array proven to hold the complete count.</param>
        /// <param name="count">The number of populated pairs.</param>
        /// <param name="bytes">The validated length of their wire window.</param>
        /// <returns>Whether the window was filled.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryPack(ref LuminPackWriter writer, KeyValuePair<TKey, TValue>[] pairs, int count, int bytes)
        {
            if ((long)count * Stride > int.MaxValue)
                return false;
            var source = MemoryMarshal.AsBytes(pairs.AsSpan(0, count));
            var payload = writer.Buffer.Slice(writer.Position, bytes);
            if (source.Overlaps(payload))
                return false;
            Pack(source, payload, count);
            writer.CompleteRun(bytes);
            return true;
        }

        /// <summary>Reads a validated wire window when its typed staging storage is disjoint.</summary>
        /// <param name="reader">The reader positioned before the complete wire window.</param>
        /// <param name="pairs">The staging array proven to hold the complete count.</param>
        /// <param name="count">The number of pairs to read.</param>
        /// <param name="bytes">The validated length of their wire window.</param>
        /// <returns>Whether the window was read.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryUnpack(ref LuminPackReader reader, KeyValuePair<TKey, TValue>[] pairs, int count, int bytes)
        {
            if ((long)count * Stride > int.MaxValue)
                return false;
            var input = reader.Buffer;
            var storage = MemoryMarshal.AsBytes(pairs.AsSpan(0, count));
            if (storage.Overlaps(input))
                return false;
            var payload = input.Slice(reader.Position, bytes);
            reader.Advance(bytes);
            Unpack(payload, storage, count);
            return true;
        }

        /// <summary>Copies pairs into their padding-free wire format without crossing either window.</summary>
        /// <param name="input">The complete typed staging storage as bytes.</param>
        /// <param name="output">The complete destination wire bytes.</param>
        /// <param name="count">The validated pair count.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Pack(ReadOnlySpan<byte> input, Span<byte> output, int count)
        {
            const int lane = 16;
            int step = lane / Stride, laneBytes = step * Width;
            int limit = count - (lane - laneBytes + Width - 1) / Width;
            ref byte source = ref MemoryMarshal.GetReference(input);
            ref byte destination = ref MemoryMarshal.GetReference(output);
            var mask = PackMask;
            int i = 0;
            // A lane writes sixteen bytes. Following lanes or the scalar tail replace the spare bytes within the window.
            if (Avx512BW.IsSupported)
            {
                var half = Vector256.Create(mask, mask);
                var wide = Vector512.Create(half, half);
                for (; i <= limit - step * 4; i += step * 4)
                {
                    var packed = Avx512BW.Shuffle(Vector512.LoadUnsafe(ref source, (nuint)(i * Stride)), wide);
                    StoreLanes(ref destination, i * Width, laneBytes, packed.GetLower());
                    StoreLanes(ref destination, i * Width + 2 * laneBytes, laneBytes, packed.GetUpper());
                }
            }
            if (Avx2.IsSupported)
            {
                var wide = Vector256.Create(mask, mask);
                for (; i <= limit - step * 2; i += step * 2)
                {
                    var packed = Avx2.Shuffle(Vector256.LoadUnsafe(ref source, (nuint)(i * Stride)), wide);
                    StoreLanes(ref destination, i * Width, laneBytes, packed);
                }
            }
            for (; i <= limit - step; i += step)
                Shuffle(Vector128.LoadUnsafe(ref source, (nuint)(i * Stride)), mask).StoreUnsafe(ref destination, (nuint)(i * Width));
            for (; i < count; i++)
            {
                ref byte pair = ref Unsafe.Add(ref source, i * Stride);
                ref byte wire = ref Unsafe.Add(ref destination, i * Width);
                LuminPackMarshal.CopyBlockUnaligned(ref wire, ref Unsafe.Add(ref pair, KeyOffset), (uint)KeyWidth);
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref wire, KeyWidth), ref Unsafe.Add(ref pair, ValueOffset), (uint)Unsafe.SizeOf<TValue>());
            }
        }

        /// <summary>Restores wire pairs to their field slots in the checked private staging storage.</summary>
        /// <param name="input">The complete reserved wire bytes.</param>
        /// <param name="output">The complete typed staging storage as bytes.</param>
        /// <param name="count">The validated pair count.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Unpack(ReadOnlySpan<byte> input, Span<byte> output, int count)
        {
            const int lane = 16;
            int step = lane / Stride, laneBytes = step * Width;
            int limit = count - (lane - laneBytes + Width - 1) / Width;
            ref byte source = ref MemoryMarshal.GetReference(input);
            ref byte destination = ref MemoryMarshal.GetReference(output);
            var mask = UnpackMask;
            int i = 0;
            // Keep every overlapping sixteen-byte load inside the checked wire payload.
            if (Avx512BW.IsSupported)
            {
                var half = Vector256.Create(mask, mask);
                var wide = Vector512.Create(half, half);
                for (; i <= limit - step * 4; i += step * 4)
                {
                    int offset = i * Width;
                    var packed = Vector512.Create(LoadLanes(ref source, offset, laneBytes), LoadLanes(ref source, offset + 2 * laneBytes, laneBytes));
                    Avx512BW.Shuffle(packed, wide).StoreUnsafe(ref destination, (nuint)(i * Stride));
                }
            }
            if (Avx2.IsSupported)
            {
                var wide = Vector256.Create(mask, mask);
                for (; i <= limit - step * 2; i += step * 2)
                    Avx2.Shuffle(LoadLanes(ref source, i * Width, laneBytes), wide).StoreUnsafe(ref destination, (nuint)(i * Stride));
            }
            for (; i <= limit - step; i += step)
                Shuffle(Vector128.LoadUnsafe(ref source, (nuint)(i * Width)), mask).StoreUnsafe(ref destination, (nuint)(i * Stride));
            for (; i < count; i++)
            {
                ref byte wire = ref Unsafe.Add(ref source, i * Width);
                ref byte pair = ref Unsafe.Add(ref destination, i * Stride);
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref pair, KeyOffset), ref wire, (uint)KeyWidth);
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref pair, ValueOffset), ref Unsafe.Add(ref wire, KeyWidth), (uint)Unsafe.SizeOf<TValue>());
            }
        }

        /// <summary>Maps the verified field bytes within one lane, leaving padding out of the wire.</summary>
        /// <param name="expand">Whether the lane maps from wire bytes to the pair slots.</param>
        /// <returns>The byte indices used by the lane shuffle.</returns>
        private static Vector128<byte> CreateMask(bool expand)
        {
            Span<byte> mask = stackalloc byte[16];
            mask.Fill(0x80);
            for (int pair = 0; pair < mask.Length / Stride; pair++)
            {
                for (int field = 0; field < Width; field++)
                {
                    int storage = pair * Stride + (field < KeyWidth ? KeyOffset + field : ValueOffset + field - KeyWidth);
                    int wire = pair * Width + field;
                    mask[expand ? storage : wire] = (byte)(expand ? wire : storage);
                }
            }
            return Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(mask));
        }

        /// <summary>Stores both compact lanes at adjacent wire offsets.</summary>
        /// <param name="destination">The validated destination start.</param>
        /// <param name="offset">The first lane's wire offset.</param>
        /// <param name="laneBytes">The unpadded bytes represented by one lane.</param>
        /// <param name="packed">The two packed lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreLanes(ref byte destination, int offset, int laneBytes, Vector256<byte> packed)
        {
            packed.GetLower().StoreUnsafe(ref destination, (nuint)offset);
            packed.GetUpper().StoreUnsafe(ref destination, (nuint)(offset + laneBytes));
        }

        /// <summary>Loads two compact wire lanes at their original record offsets.</summary>
        /// <param name="source">The validated source start.</param>
        /// <param name="offset">The first lane's wire offset.</param>
        /// <param name="laneBytes">The unpadded bytes represented by one lane.</param>
        /// <returns>The lanes to expand into padded pair slots.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> LoadLanes(ref byte source, int offset, int laneBytes)
            => Vector256.Create(Vector128.LoadUnsafe(ref source, (nuint)offset), Vector128.LoadUnsafe(ref source, (nuint)(offset + laneBytes)));

        /// <summary>Shuffles a validated lane on either 128-bit target.</summary>
        /// <param name="input">The raw input lane.</param>
        /// <param name="mask">The verified field mapping.</param>
        /// <returns>The packed or expanded lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Shuffle(Vector128<byte> input, Vector128<byte> mask)
            => Ssse3.IsSupported ? Ssse3.Shuffle(input, mask) : AdvSimd.Arm64.VectorTableLookup(input, mask);
    }
}
#endif
