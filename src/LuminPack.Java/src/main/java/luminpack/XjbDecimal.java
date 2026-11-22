package luminpack;

/** Binary32 and binary64 shortest-decimal cores ported from LuminPack's C# xjb implementation. */
// Adapted from xjb714/xjb at 80cc89574a8f8457ffbf951afa2fd27c2459bd4a under Apache-2.0.
// See docs/xjb-NOTICE.md and docs/xjb-LICENSE.txt.
final class XjbDecimal {
    private static final int FLOAT_BIT = 36;
    private static final int DOUBLE_OFFSET = 6;
    private static final int DOUBLE_POW10_BIAS = 584;

    long digits;
    int exponent10;

    void fromFloat(int bits) {
        int significand = bits & ((1 << 23) - 1);
        int exponent = (bits << 1) >>> 24;
        int q = exponent - 150;
        long c = significand | (1 << 23);
        if (exponent == 0) {
            if (significand == 0) { digits = 0; exponent10 = 0; return; }
            q = -149;
            c = significand;
        }

        if (significand == 0) {
            // Exact powers of two have asymmetric rounding intervals.
            int k = (q * 1233 - 512) >> 12;
            int h = q + ((k * -1701 - 1701) >> 9);
            long pow10 = XjbTables.FLOAT32_POW10[45 + k];
            long high = pow10 >>> (4 - h);
            long ten = (high >>> FLOAT_BIT) * 10;
            long dotOne = high & ((1L << FLOAT_BIT) - 1);
            long halfUlp = pow10 >>> (64 - FLOAT_BIT - h);
            long offset = (1L << (FLOAT_BIT - 2)) - 7 + (dotOne >>> (FLOAT_BIT - 4));
            long one = (dotOne * 5 + offset) >>> (FLOAT_BIT - 1);
            if ((halfUlp >>> 1) > dotOne) one = 0;
            if (exponent == 31 || exponent == 214 || exponent == 217) one++;
            if (halfUlp > ((1L << FLOAT_BIT) - 1) - dotOne) one = 10;
            normalize(ten + one, k);
        } else {
            int h37 = XjbTables.FLOAT32_H37[exponent];
            int k = (q * 1233) >> 12;
            long pow10 = XjbTables.FLOAT32_POW10[45 + k];
            long high = multiplyHighUnsigned(c << h37, pow10);
            long halfUlp = (pow10 >>> (65 - h37)) + ((significand + 1) & 1);
            long shorter = ((high + halfUlp) >>> FLOAT_BIT) * 10;
            long longer = (high * 5 + (1L << (FLOAT_BIT - 2)) - 7 + ((high >>> 32) & 15)) >>> (FLOAT_BIT - 1);
            normalize(((high - halfUlp) >>> FLOAT_BIT) < ((high + halfUlp) >>> FLOAT_BIT) ? shorter : longer, k);
        }
    }

    void fromDouble(long bits) {
        long significand = bits & ((1L << 52) - 1);
        int exponent = (int) ((bits << 1) >>> 53);
        int q = exponent - 1075;
        long c = significand | (1L << 52);
        if (exponent == 0) {
            if (significand == 0) { digits = 0; exponent10 = 0; return; }
            q = -1074;
            c = significand;
        }

        if (significand == 0) {
            int k = (q * 315653 - 131072) >> 20;
            int h = q + ((k * -217707 - 217707) >> 16);
            long pow10 = XjbTables.FLOAT64_POW10[DOUBLE_POW10_BIAS - (k << 1)];
            long halfUlp = pow10 >>> -h;
            long dotOne = pow10 << (53 + h);
            long ten = (pow10 >>> (11 - h)) * 10;
            long one = ((dotOne >>> (53 + h)) * 5 + (1L << (9 - h))) >>> (10 - h);
            long next = (dotOne >>> 54) * 5;
            if ((next & 511) > (halfUlp >>> 55) * 5) one = (next >>> 9) + 1;
            if (dotOne == (1L << 62)) one = 2;
            if (lessUnsigned(dotOne, halfUlp >>> 1)) one = 0;
            if (lessUnsigned(~dotOne, halfUlp)) one = 10;
            normalize(ten + one, k);
        } else {
            int k = ((exponent - 1075) * 78913) >> 18;
            int h = q + (((-1 - k) * 217707) >> 16);
            long cb = c << (h + 1 + DOUBLE_OFFSET);
            int index = DOUBLE_POW10_BIAS - (k << 1);
            long pow10Hi = XjbTables.FLOAT64_POW10[index];
            long pow10Lo = XjbTables.FLOAT64_POW10[index + 1];

            long highA = multiplyHighUnsigned(cb, pow10Hi);
            long lowA = cb * pow10Hi;
            long low = lowA + multiplyHighUnsigned(cb, pow10Lo);
            long high = highA + (lessUnsigned(low, lowA) ? 1 : 0);
            long dotOne = (low >>> DOUBLE_OFFSET) | (high << (64 - DOUBLE_OFFSET));
            long halfUlp = (pow10Hi >>> -h) + ((bits + 1) & 1);
            long ten = (high >>> DOUBLE_OFFSET) * 10;

            long oneHigh = multiplyHighUnsigned(dotOne, 10);
            long oneLow = dotOne * 10;
            long addend = dotOne == (1L << 62) ? 0 : Long.MIN_VALUE + 6;
            long one = oneHigh + (lessUnsigned(oneLow + addend, oneLow) ? 1 : 0);
            if (lessUnsigned(dotOne, halfUlp)) one = 0;
            if (lessUnsigned(~dotOne, halfUlp)) one = 10;
            normalize(ten + one, k);
        }
    }

    private void normalize(long value, int exponent) {
        while (value % 10 == 0) {
            value /= 10;
            exponent++;
        }
        digits = value;
        exponent10 = exponent;
    }

    static long multiplyHighUnsigned(long a, long b) {
        return Math.multiplyHigh(a, b) + ((a >> 63) & b) + ((b >> 63) & a);
    }

    private static boolean lessUnsigned(long a, long b) { return Long.compareUnsigned(a, b) < 0; }
}
