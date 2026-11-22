package luminpack;

import java.lang.invoke.MethodHandles;
import java.lang.invoke.VarHandle;
import java.nio.ByteOrder;
import java.util.Arrays;

/** ASCII numeric kernels shared by the UTF-8 and UTF-16 JSON writers. */
final class JsonNumberFormatter {
    static final int MAX_SIGNED32_BYTES = 11;
    static final int MAX_UNSIGNED32_BYTES = 10;
    static final int MAX_INTEGER64_BYTES = 20;
    static final int MAX_BYTES = 32;
    private static final VarHandle PAIR = MethodHandles.byteArrayViewVarHandle(short[].class, ByteOrder.LITTLE_ENDIAN);
    private static final VarHandle QUAD = MethodHandles.byteArrayViewVarHandle(int[].class, ByteOrder.LITTLE_ENDIAN);
    private static final long[] POW10 = {1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000,
        1_000_000_000, 10_000_000_000L, 100_000_000_000L, 1_000_000_000_000L, 10_000_000_000_000L,
        100_000_000_000_000L, 1_000_000_000_000_000L, 10_000_000_000_000_000L, 100_000_000_000_000_000L,
        1_000_000_000_000_000_000L, 0x8ac7_2304_89e8_0000L};
    private static final short[] DIGIT_PAIRS = new short[100];
    private static final int[] DIGIT_QUADS = new int[10_000];
    private static final int[] PACKED_HEADS = new int[10_000];
    private static final byte[] HEAD_LENGTHS = new byte[10_000];

    static {
        for (int value = 0; value < DIGIT_PAIRS.length; value++)
            DIGIT_PAIRS[value] = (short) (('0' + value / 10) | (('0' + value % 10) << 8));
        for (int value = 0; value < DIGIT_QUADS.length; value++) {
            int high = value / 100;
            int low = value - high * 100;
            int quad = (DIGIT_PAIRS[high] & 0xffff) | (DIGIT_PAIRS[low] << 16);
            int length = value < 10 ? 1 : value < 100 ? 2 : value < 1_000 ? 3 : 4;
            DIGIT_QUADS[value] = quad;
            PACKED_HEADS[value] = quad >>> ((4 - length) * 8);
            HEAD_LENGTHS[value] = (byte) length;
        }
    }

    private final XjbDecimal decimal = new XjbDecimal();

    // Callers reserve the entire numeric window. Packed heads may write past their returned digit count.
    static int signed32(byte[] output, int offset, int value) {
        if (value >= 0) return unsigned32(output, offset, value);
        output[offset] = '-';
        return 1 + unsigned32(output, offset + 1, -(long) value);
    }

    static int unsigned32(byte[] output, int offset, long value) {
        if (value < 10_000) return writePackedHead(output, offset, (int) value);
        long high = value / 10_000;
        int low = (int) (value - high * 10_000);
        int head;
        if (high < 10_000) {
            head = writePackedHead(output, offset, (int) high);
        } else {
            int top = (int) (high / 10_000);
            head = writePackedHead(output, offset, top);
            writeQuad(output, offset + head, (int) (high - top * 10_000));
            head += 4;
        }
        writeQuad(output, offset + head, low);
        return head + 4;
    }

    static int signed64(byte[] output, int offset, long value) {
        if (value >= 0) return unsigned64(output, offset, value);
        output[offset] = '-';
        return 1 + unsigned64(output, offset + 1, -value);
    }

    static int unsigned64(byte[] output, int offset, long value) {
        if (value >= 0 && value < 10_000) return writePackedHead(output, offset, (int) value);
        int length = digitCount(value);
        int end = offset + length;
        while (value < 0 || value >= 10_000) {
            long quotient = value < 0 ? Long.divideUnsigned(value, 10_000) : value / 10_000;
            end -= 4;
            writeQuad(output, end, (int) (value - quotient * 10_000));
            value = quotient;
        }
        writeHead(output, offset, (int) value);
        return length;
    }

    private static int writePackedHead(byte[] output, int offset, int value) {
        QUAD.set(output, offset, PACKED_HEADS[value]);
        return HEAD_LENGTHS[value];
    }

    private static void writeHead(byte[] output, int offset, int value) {
        if (value >= 1_000) writeQuad(output, offset, value);
        else if (value >= 100) {
            int first = value / 100;
            output[offset] = (byte) ('0' + first);
            writePair(output, offset + 1, value - first * 100);
        } else if (value >= 10) writePair(output, offset, value);
        else output[offset] = (byte) ('0' + value);
    }

    private static void writeQuad(byte[] output, int offset, int value) { QUAD.set(output, offset, DIGIT_QUADS[value]); }

    private static void writePair(byte[] output, int offset, int value) { PAIR.set(output, offset, DIGIT_PAIRS[value]); }

    private static int digitCount(long value) {
        if (value == 0) return 1;
        int log2 = 63 - Long.numberOfLeadingZeros(value);
        int estimate = ((log2 * 1233) >> 12) + 1;
        return estimate < 20 && Long.compareUnsigned(value, POW10[estimate]) >= 0 ? estimate + 1 : estimate;
    }

    int floating(byte[] output, int offset, float value) {
        int bits = Float.floatToRawIntBits(value);
        int magnitudeBits = bits & Integer.MAX_VALUE;
        int exponent = magnitudeBits >>> 23;
        if (exponent == 255) throw Schema.fail("LuminPack JSON cannot encode a non-finite float");
        boolean legacy = exponent >= 95 && exponent <= 159;
        boolean negative = bits < 0;
        int shortcut = shortcut(output, offset, Math.abs((double) value), negative, legacy, true);
        if (shortcut != 0) return shortcut;
        decimal.fromFloat(magnitudeBits);
        return emit(output, offset, negative, legacy, 9);
    }

    int floating(byte[] output, int offset, double value) {
        long bits = Double.doubleToRawLongBits(value);
        long magnitudeBits = bits & Long.MAX_VALUE;
        int exponent = (int) (magnitudeBits >>> 52);
        if (exponent == 2047) throw Schema.fail("LuminPack JSON cannot encode a non-finite double");
        boolean legacy = exponent >= 991 && exponent <= 1055;
        boolean negative = bits < 0;
        int shortcut = shortcut(output, offset, Math.abs(value), negative, legacy, false);
        if (shortcut != 0) return shortcut;
        decimal.fromDouble(magnitudeBits);
        return emit(output, offset, negative, legacy, 17);
    }

    private static int shortcut(byte[] output, int offset, double magnitude, boolean negative, boolean legacy,
                                boolean single) {
        int position = offset;
        if (magnitude == 0) {
            if (negative) output[position++] = '-';
            output[position++] = '0';
            return position - offset;
        }
        if (!legacy) return 0;
        if (magnitude < (single ? 16_777_216.0 : 9_007_199_254_740_992.0) && magnitude == Math.floor(magnitude)) {
            if (negative) output[position++] = '-';
            return position - offset + unsigned64(output, position, (long) magnitude);
        }
        if (magnitude > 2_147_483_648.0) return 0;
        if (!single) {
            double prefilter = magnitude * 1000;
            if (prefilter != Math.floor(prefilter)) return 0;
        }
        for (int fraction = 2; fraction <= 4; fraction += 2) {
            double scale = fraction == 2 ? 100.0 : 10000.0;
            long scaled = (long) (magnitude * scale + 0.5);
            if (scaled <= 0) continue;
            double quotient = single ? scaled * (fraction == 2 ? 0.01 : 0.0001) : scaled / scale;
            if ((single ? (float) quotient : quotient) != magnitude) continue;
            int decimals = fraction;
            while (scaled % 10 == 0) {
                scaled /= 10;
                decimals--;
            }
            int length = digitCount(scaled);
            if (length > (single ? 6 : 15)) return 0;
            if (negative) output[position++] = '-';
            return position - offset + fixed(output, position, scaled, -decimals, length);
        }
        return 0;
    }

    private int emit(byte[] output, int offset, boolean negative, boolean legacy, int precision) {
        int position = offset;
        if (negative) output[position++] = '-';
        int length = digitCount(decimal.digits);
        int exponent = decimal.exponent10;
        int leadingExponent = exponent + length - 1;
        if ((legacy && exponent >= -precision) || (leadingExponent >= -4 && leadingExponent < precision))
            return position - offset + fixed(output, position, decimal.digits, exponent, length);
        return position - offset + scientific(output, position, decimal.digits, length, leadingExponent);
    }

    private static int fixed(byte[] output, int offset, long digits, int exponent, int length) {
        if (exponent >= 0) {
            unsigned64(output, offset, digits);
            Arrays.fill(output, offset + length, offset + length + exponent, (byte) '0');
            return length + exponent;
        }
        int fraction = -exponent;
        if (fraction < length) {
            if (fraction <= 2) {
                long divisor = fraction == 1 ? 10 : 100;
                long whole = digits / divisor;
                int wholeLength = unsigned64(output, offset, whole);
                int dot = offset + wholeLength;
                output[dot] = '.';
                int suffix = (int) (digits - whole * divisor);
                if (fraction == 1) output[dot + 1] = (byte) ('0' + suffix);
                else writePair(output, dot + 1, suffix);
                return wholeLength + fraction + 1;
            }
            unsigned64(output, offset, digits);
            int dot = offset + length - fraction;
            System.arraycopy(output, dot, output, dot + 1, fraction);
            output[dot] = '.';
            return length + 1;
        }
        output[offset] = '0';
        output[offset + 1] = '.';
        int start = offset + 2 + fraction - length;
        Arrays.fill(output, offset + 2, start, (byte) '0');
        unsigned64(output, start, digits);
        return fraction + 2;
    }

    private static int scientific(byte[] output, int offset, long digits, int length, int leadingExponent) {
        unsigned64(output, offset, digits);
        int position = offset + length;
        if (length > 1) {
            System.arraycopy(output, offset + 1, output, offset + 2, length - 1);
            output[offset + 1] = '.';
            position++;
        }
        output[position++] = 'E';
        output[position++] = (byte) (leadingExponent < 0 ? '-' : '+');
        int exponent = Math.abs(leadingExponent);
        if (exponent >= 100) {
            int hundreds = exponent / 100;
            output[position++] = (byte) ('0' + hundreds);
            exponent -= hundreds * 100;
        }
        writePair(output, position, exponent);
        return position + 2 - offset;
    }
}
