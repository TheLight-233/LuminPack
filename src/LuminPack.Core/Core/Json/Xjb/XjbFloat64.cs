namespace LuminPack.Core
{
    /// <summary>
    /// xjb binary64 -> shortest decimal core (scalar port of the x64 path of xjb64_v2_f64_to_dec).
    ///
    /// Reference: https://github.com/xjb714/xjb @ 80cc89574a8f8457ffbf951afa2fd27c2459bd4a
    ///   bench/xjb/float_to_decimal/xjb64_i.cpp, function xjb64_v2_f64_to_dec.
    /// The upstream function has an aarch64 specialization that reads the precomputed h + 7 table;
    /// the portable x64 path computes h from q with the same expression and was verified against the
    /// upstream h7 table for all 2048 exponent values (see JsonXjbTableTest).
    /// </summary>
    internal static class XjbFloat64
    {
        private const int Offset = 6;
        private const int Pow10Bias = 584; // index of 10^(-k-1) for k == 0: 2 * (293 - 1)

        /// <summary>Converts a finite binary64 bit pattern to the shortest decimal <c>digits * 10^exponent10</c>.</summary>
        /// <param name="bits">The bits used by this operation.</param>
        /// <param name="digits">Receives the digits produced by the operation.</param>
        /// <param name="exponent10">Receives the exponent10 produced by the operation.</param>
        internal static void Convert(ulong bits, out ulong digits, out int exponent10)
        {
            ulong sig = bits & ((1UL << 52) - 1);
            int exp = (int)((bits << 1) >> 53);

            bool irregular = sig == 0;
            bool subnormal = exp == 0;

            ulong c = sig | (1UL << 52);
            int q = exp - 1075;

            if (subnormal)
            {
                if (irregular)
                {
                    digits = 0;
                    exponent10 = 0;
                    return;
                }

                q = -1074;
                c = sig;
            }

            if (irregular)
            {
                // Only 2046 values: c == 1 << 52 with exp in [1, 2046]; asymmetric rounding interval.
                int kIrr = (q * 315653 - 131072) >> 20;
                int hIrr = q + ((kIrr * -217707 - 217707) >> 16);
                ulong pow10HiIrr = XjbFloat64Tables.Pow10[Pow10Bias - (kIrr << 1)];

                ulong halfUlpIrr = pow10HiIrr >> (-hIrr);
                ulong dotOneIrr = pow10HiIrr << (53 + hIrr);
                ulong tenIrr = (pow10HiIrr >> (11 - hIrr)) * 10;
                ulong oneIrr = ((dotOneIrr >> (53 + hIrr)) * 5 + (1UL << (9 - hIrr))) >> (10 - hIrr);

                if ((((dotOneIrr >> 54) * 5) & ((1UL << 9) - 1)) > ((halfUlpIrr >> 55) * 5))
                    oneIrr = (((dotOneIrr >> 54) * 5) >> 9) + 1;
                if (dotOneIrr == (1UL << 62))
                    oneIrr = 2;

                oneIrr = (halfUlpIrr >> 1) > dotOneIrr ? 0UL : oneIrr;
                oneIrr = halfUlpIrr > ulong.MaxValue - dotOneIrr ? 10UL : oneIrr;

                digits = tenIrr + oneIrr;
                exponent10 = kIrr;
            }
            else
            {
                // Regular values retain the original products and rounding expressions.
                int k = ((exp - 1075) * 78913) >> 18;
                int getE10 = -1 - k;
                int h = q + ((getE10 * 217707) >> 16);
                ulong cb = c << (h + 1 + Offset); // h in [-4, 0], so the shift stays in [3, 6]

                int p = Pow10Bias - (k << 1);     // == 2 * (293 + getE10)
                ulong pow10Hi = XjbFloat64Tables.Pow10[p];
                ulong pow10Lo = XjbFloat64Tables.Pow10[p + 1];

                XjbUIntMath.Mul64x64(cb, pow10Hi, out ulong hiA, out ulong loA);
                XjbUIntMath.Mul64x64(cb, pow10Lo, out ulong hiB, out _);

                ulong lo = loA + hiB;
                ulong hi = hiA + (lo < loA ? 1UL : 0UL);

                ulong dotOne = (lo >> Offset) | (hi << (64 - Offset));
                ulong halfUlp = (pow10Hi >> (-h)) + ((bits + 1) & 1UL);
                ulong ten = (hi >> Offset) * 10; // upstream: (hi128 >> (offset + 64)) * 10

                XjbUIntMath.Mul64x64(dotOne, 10, out ulong oneHi, out ulong oneLo);
                ulong addend = dotOne == (1UL << 62) ? 0UL : (1UL << 63) + 6;
                ulong sumLo = oneLo + addend;
                ulong one = oneHi + (sumLo < oneLo ? 1UL : 0UL);

                one = dotOne < halfUlp ? 0UL : one;
                one = ulong.MaxValue - dotOne < halfUlp ? 10UL : one;

                digits = ten + one;
                exponent10 = k;
            }

            XjbUIntMath.NormalizeDecimal(ref digits, ref exponent10);
        }
    }
}
