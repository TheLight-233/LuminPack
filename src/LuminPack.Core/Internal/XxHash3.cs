using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Internal
{
    /// <summary>Provides xx hash3 functionality.</summary>
    public static class XxHash3
    {
        private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
        private const ulong Prime64_3 = 0x165667B19E3779F9UL;

        /// <summary>Determines whether h64 utf8 are present.</summary>
        /// <param name="data">The metadata describing the serializable type.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Hash64Utf8(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
                return Prime64_3 ^ 11400714819323198485UL ^ 2870177450012600261UL;

            ref byte dataRef = ref MemoryMarshal.GetReference(data);
            return ComputeHash(ref dataRef, data.Length);
        }

        /// <summary>Determines whether h64 utf16 are present.</summary>
        /// <param name="chars">The chars used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Hash64Utf16(ReadOnlySpan<char> chars)
        {
            if (chars.Length == 0)
                return Prime64_3 ^ 11400714819323198485UL ^ 2870177450012600261UL;

            ref char charRef = ref MemoryMarshal.GetReference(chars);
            ref byte byteRef = ref Unsafe.As<char, byte>(ref charRef);
            return ComputeHash(ref byteRef, chars.Length * 2);
        }

        /// <summary>Performs the compute hash step for the current operation.</summary>
        /// <param name="input">The input data to process.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ComputeHash(ref byte input, int length)
        {
            if (length == 0)
                return Prime64_3 ^ 11400714819323198485UL ^ 2870177450012600261UL;

            if (length <= 16)
            {
                if (length > 8)
                {
                    ulong low = Read64(ref input) ^ (11400714819323198549UL);
                    ulong high = Read64(ref Unsafe.Add(ref input, length - 8)) ^ (2870177450012600325UL);
                    ulong hash = low + high + (ulong)length;
                    hash ^= hash >> 37;
                    hash *= Prime64_3;
                    hash ^= hash >> 32;
                    return hash;
                }

                if (length >= 4)
                {
                    ulong val1 = Unsafe.ReadUnaligned<uint>(ref input);
                    ulong val2 = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref input, length - 4));
                    ulong hash = (2870177450012600261UL + val1 + ((val2 << 32) | val2));
                    hash ^= hash >> 37;
                    hash *= Prime64_3;
                    hash ^= hash >> 32;
                    return hash;
                }

                byte c1 = input;
                byte c2 = Unsafe.Add(ref input, length >> 1);
                byte c3 = Unsafe.Add(ref input, length - 1);
                uint combined = ((uint)c1 << 16) | ((uint)c2 << 24) | c3 | ((uint)length << 8);
                ulong hash2 = (11400714819323198485UL + combined);
                hash2 ^= hash2 >> 37;
                hash2 *= Prime64_3;
                hash2 ^= hash2 >> 32;
                return hash2;
            }

            ulong acc = (ulong)length * Prime64_1;
            int remaining = length;
            ref byte current = ref input;

            while (remaining >= 16)
            {
                ulong v1 = Read64(ref current);
                ulong v2 = Read64(ref Unsafe.Add(ref current, 8));
                acc += v1 * Prime64_1;
                acc += v2 * Prime64_3;
                current = ref Unsafe.Add(ref current, 16);
                remaining -= 16;
            }

            if (remaining > 0)
            {
                if (remaining >= 8)
                {
                    acc += Read64(ref current) * Prime64_1;
                    current = ref Unsafe.Add(ref current, 8);
                    remaining -= 8;
                }
                if (remaining >= 4)
                {
                    acc += Unsafe.ReadUnaligned<uint>(ref current) * Prime64_3;
                    current = ref Unsafe.Add(ref current, 4);
                    remaining -= 4;
                }
                while (remaining > 0)
                {
                    acc += current * Prime64_1;
                    current = ref Unsafe.Add(ref current, 1);
                    remaining--;
                }
            }

            acc ^= acc >> 37;
            acc *= Prime64_3;
            acc ^= acc >> 32;
            return acc;
        }
        /// <summary>Reads one eight-byte word while retaining the 32-bit block-copy contract.</summary>
        /// <param name="source">The validated word's first byte.</param>
        /// <returns>The complete unaligned word.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Read64(ref byte source)
        {
            if (Unsafe.SizeOf<IntPtr>() == 8) return Unsafe.ReadUnaligned<ulong>(ref source);
            ulong value = 0;
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<ulong, byte>(ref value), ref source, 8);
            return value;
        }
    }
}
