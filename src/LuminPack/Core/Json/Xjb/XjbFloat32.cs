using System.Runtime.CompilerServices;

namespace LuminPack.Core
{
    /// <summary>
    /// xjb binary32 -> shortest decimal core (scalar port of xjb_v2_f32_to_dec).
    ///
    /// Reference: https://github.com/xjb714/xjb @ 80cc89574a8f8457ffbf951afa2fd27c2459bd4a
    ///   bench/xjb/float_to_decimal/xjb32_i.cpp, function xjb_v2_f32_to_dec.
    /// The upstream 128-bit expression `(q * 1233)` uses the same signed arithmetic shift as
    /// C/C++ on every legal exponent, which <c>(q * 1233) &gt;&gt; 12</c> reproduces in C#.
    /// The regular path of the upstream function is always executed and then overridden for
    /// irregular values (fraction == 0 with a non-zero exponent); the port keeps that order
    /// because the override is part of the reference behaviour.
    /// </summary>
    internal static class XjbFloat32
    {
        private const int Bit = 36;

        /// <summary>Converts a finite binary32 bit pattern to the shortest decimal <c>digits * 10^exponent10</c>.</summary>
        internal static void Convert(uint bits, out ulong digits, out int exponent10)
        {
            uint sig = bits & ((1u << 23) - 1);
            uint exp = (bits << 1) >> 24;

            bool irregular = sig == 0;
            ulong c = sig | (1u << 23);
            int q = (int)exp - 150;

            if (exp == 0)
            {
                if (sig == 0)
                {
                    digits = 0;
                    exponent10 = 0;
                    return;
                }

                q = -149;
                c = sig;
            }

            // Regular values (also computed for irregular ones before the override below).
            uint h37 = XjbFloat32Tables.H37[exp];              // h + 37, h in [-4, -1]
            int k = (q * 1233) >> 12;                          // floor(q * log10(2))
            ulong pow10 = XjbFloat32Tables.Pow10Reverse[45 + k]; // 10^(-k-1)

            ulong cb = c << (int)h37;                          // c is at most 24 bits, so this cannot overflow
            ulong hi64 = XjbUIntMath.MulHi64(cb, pow10);
            ulong halfUlp = (pow10 >> (65 - (int)h37)) + ((sig + 1UL) & 1UL);
            ulong shorter = ((hi64 + halfUlp) >> Bit) * 10;
            ulong longer = (hi64 * 5 + (1UL << (Bit - 2)) - 7 + ((hi64 >> 32) & 15)) >> (Bit - 1);

            digits = ((hi64 - halfUlp) >> Bit) < ((hi64 + halfUlp) >> Bit) ? shorter : longer;
            exponent10 = k;

            if (irregular)
            {
                // 254 values only: c == 1 << 23 and exp in [1, 254]; the rounding interval is asymmetric.
                int kIrr = (q * 1233 - 512) >> 12;
                int h = q + ((kIrr * -1701 - 1701) >> 9);
                ulong pow10Hi = XjbFloat32Tables.Pow10Reverse[45 + kIrr];

                ulong sigHi = pow10Hi >> (4 - h);
                ulong ten = (sigHi >> Bit) * 10;
                ulong dotOne = sigHi & ((1UL << Bit) - 1);
                ulong halfUlpIrr = pow10Hi >> ((64 - Bit) - h);
                ulong offsetNum = (1UL << (Bit - 2)) - 7 + (dotOne >> (Bit - 4));
                ulong one = (dotOne * 5 + offsetNum) >> (Bit - 1);

                one = (halfUlpIrr >> 1) > dotOne ? 0UL : one;
                if (q == 31 - 150 || q == 214 - 150 || q == 217 - 150)
                    one++;
                one = halfUlpIrr > ((1UL << Bit) - 1) - dotOne ? 10UL : one;

                digits = ten + one;
                exponent10 = kIrr;
            }

            XjbUIntMath.NormalizeDecimal(ref digits, ref exponent10);
        }
    }
}