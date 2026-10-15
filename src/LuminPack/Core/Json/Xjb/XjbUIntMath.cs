using System.Runtime.CompilerServices;

namespace LuminPack.Core
{
    /// <summary>
    /// Portable 64-bit integer helpers for the xjb cores. UInt128 is unavailable on
    /// netstandard2.1, so the 64x64 -> 128 bit product is spelled out; net8.0+ uses the
    /// single-instruction Math.BigMul.
    /// </summary>
    internal static class XjbUIntMath
    {
        /// <summary>Full 64x64 -> 128 bit product split into a high and a low word.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="hi">Receives the hi produced by the operation.</param>
        /// <param name="lo">Receives the lo produced by the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Mul64x64(ulong a, ulong b, out ulong hi, out ulong lo)
        {
#if NET8_0_OR_GREATER
            // Math.BigMul returns the high word and stores the low word in the out parameter.
            hi = Math.BigMul(a, b, out lo);
#else
            Mul64x64Portable(a, b, out hi, out lo);
#endif
        }

        /// <summary>
        /// Portable 64x64 -> 128 bit product. Compiled on every target, so the tests always cover the
        /// netstandard2.1 / IL2CPP code path even on modern runtimes.
        /// </summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="hi">Receives the hi produced by the operation.</param>
        /// <param name="lo">Receives the lo produced by the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Mul64x64Portable(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong a0 = (uint)a;
            ulong a1 = a >> 32;
            ulong b0 = (uint)b;
            ulong b1 = b >> 32;

            ulong p00 = a0 * b0;
            ulong p01 = a0 * b1;
            ulong p10 = a1 * b0;
            ulong p11 = a1 * b1;

            ulong middle = (p00 >> 32) + (uint)p01 + (uint)p10;
            lo = (middle << 32) | (uint)p00;
            hi = p11 + (p01 >> 32) + (p10 >> 32) + (middle >> 32);
        }

        /// <summary>High 64 bits of a 64x64 product.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong MulHi64(ulong a, ulong b)
        {
#if NET8_0_OR_GREATER
            return Math.BigMul(a, b, out _);
#else
            Mul64x64Portable(a, b, out ulong hi, out _);
            return hi;
#endif
        }

        /// <summary>Removes insignificant trailing zeros from the xjb result (value = digits * 10^exponent10).</summary>
        /// <param name="digits">The digits used by this operation.</param>
        /// <param name="exponent10">The exponent10 used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void NormalizeDecimal(ref ulong digits, ref int exponent10)
        {
            while (digits % 10UL == 0UL)
            {
                digits /= 10UL;
                exponent10++;
            }
        }
    }
}
