using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Code;

/// <summary>Provides validated binary integer construction without signed-magnitude trimming buffers.</summary>
public static partial class LuminPackMarshal
{
    /// <summary>Constructs a signed integer from a little-endian two's-complement payload.</summary>
    /// <param name="source">The complete validated integer payload.</param>
    /// <param name="value">Receives the signed value, including zero for an empty payload.</param>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadBigInteger(ReadOnlySpan<byte> source, out BigInteger value)
    {
        if (source.Length > 4 && source[source.Length - 1] == byte.MaxValue &&
            ((source.Length & 3) == 1 || source[source.Length - 2] >= 128))
            value = ReadNegativeBigInteger(source);
        else
            value = new BigInteger(source);
    }

    /// <summary>Normalizes negative sign extension before constructing the final magnitude.</summary>
    /// <param name="source">A negative payload ending in a full sign byte.</param>
    /// <returns>The exact signed integer.</returns>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static BigInteger ReadNegativeBigInteger(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        // Span construction can accept magnitudes beyond the bit-operation limits enforced by negation or shifts.
        // Preserve that constructor's behavior near the signed bit-count limit, including its allocation failures.
        if (length >= int.MaxValue / 8 - 32)
            return new BigInteger(source);
        while (length > 1 && source[length - 1] == byte.MaxValue && source[length - 2] >= 128)
            length--;
        source = source.Slice(0, length);
        if (length <= 8)
        {
            long value = -1;
            for (int index = length - 1; index >= 0; index--)
                value = (value << 8) | source[index];
            return new BigInteger(value);
        }
        // Only a separate full sign byte can leave a zero high limb after complementing a canonical negative payload.
        // Four-byte boundaries also cover runtimes with wider limbs, without depending on BigInteger's private layout.
        if ((length & 3) != 1 || source[length - 1] != byte.MaxValue)
            return new BigInteger(source);
        if (length > 256 && length <= int.MaxValue / 8 && source[0] == 0 && IsZeroBigIntegerMagnitude(source.Slice(0, length - 1)))
            return -(BigInteger.One << ((length - 1) * 8));

        byte[]? rented = null;
        Span<byte> magnitude = length <= 256 ? stackalloc byte[256] : (rented = ArrayPool<byte>.Shared.Rent(length));
        try
        {
            ref byte input = ref MemoryMarshal.GetReference(source);
            ref byte output = ref MemoryMarshal.GetReference(magnitude);
            uint carry = 1;
            int index = 0;
            for (; index < length - 1; index += 4)
            {
                uint original = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref input, (nint)(uint)index));
                if (!BitConverter.IsLittleEndian)
                    original = BinaryPrimitives.ReverseEndianness(original);
                uint result = unchecked(0u - original);
                if (!BitConverter.IsLittleEndian)
                    result = BinaryPrimitives.ReverseEndianness(result);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)index), result);
                if (original != 0)
                {
                    carry = 0;
                    index += 4;
                    break;
                }
            }
            // Keep vector registers out of the constructor's frame. The outlined helper clears their upper halves
            // before returning on x64 AVX paths, avoiding a vector transition at the subsequent BCL constructor call.
            if (IntPtr.Size == 8 && Vector.IsHardwareAccelerated && length - 1 - index >= Vector<byte>.Count)
            {
                ComplementBigIntegerMagnitude(ref Unsafe.Add(ref input, (nint)(uint)index), ref Unsafe.Add(ref output, (nint)(uint)index), length - 1 - index);
                index = length - 1;
            }
            for (; index < length - 1; index += 4)
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)index), ~Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref input, (nint)(uint)index)));
            magnitude[index] = (byte)carry;
            return -new BigInteger(magnitude.Slice(0, length), isUnsigned: true);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Checks whether a validated magnitude prefix contains only zero bytes.</summary>
    /// <param name="source">The complete prefix, excluding its negative sign byte, with length divisible by four.</param>
    /// <returns>Whether every byte is zero.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsZeroBigIntegerMagnitude(ReadOnlySpan<byte> source)
    {
        ref byte input = ref MemoryMarshal.GetReference(source);
        int index = 0;
        if (IntPtr.Size == 8 && Vector.IsHardwareAccelerated)
        {
            Vector<byte> low = Vector<byte>.Zero, high = Vector<byte>.Zero;
            for (; index <= source.Length - Vector<byte>.Count * 2; index += Vector<byte>.Count * 2)
            {
                low |= Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.Add(ref input, (nint)(uint)index));
                high |= Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.Add(ref input, (nint)(uint)(index + Vector<byte>.Count)));
            }
            if (!Vector.EqualsAll(low | high, Vector<byte>.Zero))
                return false;
        }
        for (; index < source.Length; index += 4)
            if (Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref input, (nint)(uint)index)) != 0)
                return false;
        return true;
    }

    /// <summary>Complements a validated magnitude range in a separate vector-using frame.</summary>
    /// <param name="input">The first source byte.</param>
    /// <param name="output">The first destination byte in disjoint temporary storage.</param>
    /// <param name="length">The validated byte length, a multiple of four.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ComplementBigIntegerMagnitude(ref byte input, ref byte output, int length)
    {
        int index = 0;
        if (IntPtr.Size == 8 && Vector.IsHardwareAccelerated)
        {
            for (; index <= length - Vector<byte>.Count; index += Vector<byte>.Count)
            {
                var bytes = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.Add(ref input, (nint)(uint)index));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)index), ~bytes);
            }
        }
        for (; index < length; index += 4)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)index), ~Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref input, (nint)(uint)index)));
    }
}
