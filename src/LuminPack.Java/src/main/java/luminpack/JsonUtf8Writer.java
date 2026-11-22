package luminpack;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;

/** Checked UTF-8 output cursor for generated JSON property heads and typed scalar values. */
final class JsonUtf8Writer {
    private static final byte[] NULL = {'n', 'u', 'l', 'l'};
    private static final byte[] TRUE = {'t', 'r', 'u', 'e'};
    private static final byte[] FALSE = {'f', 'a', 'l', 's', 'e'};
    private static final byte[] HEX = "0123456789abcdef".getBytes(StandardCharsets.US_ASCII);
    private static final byte[] DIGIT_PAIRS = new byte[200];

    static {
        for (int i = 0; i < 100; i++) {
            DIGIT_PAIRS[i * 2] = (byte) ('0' + i / 10);
            DIGIT_PAIRS[i * 2 + 1] = (byte) ('0' + i % 10);
        }
    }

    private byte[] buffer = new byte[512];
    private int position;

    byte[] finish() { return Arrays.copyOf(buffer, position); }

    private void reserve(int bytes) {
        if (bytes <= buffer.length - position) return;
        if (bytes > Integer.MAX_VALUE - position) throw Schema.fail("LuminPack JSON output exceeds the Java array limit");
        long grown = buffer.length + (long) (buffer.length >>> 1);
        buffer = Arrays.copyOf(buffer, Math.max(position + bytes, (int) Math.min(grown, Integer.MAX_VALUE - 8L)));
    }

    void token(char value) {
        reserve(1);
        buffer[position++] = (byte) value;
    }

    void raw(byte[] value) {
        reserve(value.length);
        System.arraycopy(value, 0, buffer, position, value.length);
        position += value.length;
    }

    void nullValue() { raw(NULL); }

    void booleanValue(boolean value) { raw(value ? TRUE : FALSE); }

    void integer(long value) {
        if (value >= 0 && value < 10) {
            token((char) ('0' + value));
            return;
        }
        reserve(20);
        boolean negative = value < 0;
        long remaining = negative ? value : -value;
        int end = position + 20;
        int start = end;
        while (remaining <= -100) {
            long quotient = remaining / 100;
            int pair = (int) (quotient * 100 - remaining) * 2;
            buffer[--start] = DIGIT_PAIRS[pair + 1];
            buffer[--start] = DIGIT_PAIRS[pair];
            remaining = quotient;
        }
        int last = (int) -remaining * 2;
        buffer[--start] = DIGIT_PAIRS[last + 1];
        if (last >= 20) buffer[--start] = DIGIT_PAIRS[last];
        if (negative) buffer[--start] = '-';
        System.arraycopy(buffer, start, buffer, position, end - start);
        position += end - start;
    }

    void floating(float value) {
        int bits = Float.floatToRawIntBits(value);
        int exponent = (bits >>> 23) & 0xff;
        boolean legacyRange = exponent >= 95 && exponent <= 159;
        if (!Float.isFinite(value)) throw Schema.fail("LuminPack JSON cannot encode a non-finite float");
        if (!smallIntegral(value, bits < 0, legacyRange, 16_777_216.0))
            decimal(Float.toString(value), 9, legacyRange);
    }

    void floating(double value) {
        long bits = Double.doubleToRawLongBits(value);
        int exponent = (int) ((bits >>> 52) & 0x7ff);
        boolean legacyRange = exponent >= 991 && exponent <= 1055;
        if (!Double.isFinite(value)) throw Schema.fail("LuminPack JSON cannot encode a non-finite double");
        if (!smallIntegral(value, bits < 0, legacyRange, 9_007_199_254_740_992.0))
            decimal(Double.toString(value), 17, legacyRange);
    }

    // Convert the JVM's shortest digits directly to the established LuminPack notation.
    private void decimal(String text, int precision, boolean legacyRange) {
        boolean negative = text.charAt(0) == '-';
        int point = text.indexOf('.');
        int exponentAt = text.indexOf('E');
        int end = exponentAt < 0 ? text.length() : exponentAt;
        int exponent = exponentAt < 0 ? 0 : Integer.parseInt(text, exponentAt + 1, text.length(), 10);
        int first = negative ? 1 : 0;
        while (first == point || text.charAt(first) == '0') first++;
        int last = end - 1;
        while (last == point || text.charAt(last) == '0') last--;
        int digitCount = last - first + 1 - (point > first && point < last ? 1 : 0);
        int leadingExponent = point - first - 1 + exponent + (first > point ? 1 : 0);
        int integralDigits = leadingExponent + 1;
        boolean fixed = (legacyRange && digitCount - integralDigits <= precision) ||
            (leadingExponent >= -4 && leadingExponent < precision);
        if (negative) token('-');
        if (fixed) {
            if (integralDigits <= 0) {
                token('0');
                token('.');
                for (int i = integralDigits; i < 0; i++) token('0');
            }
            int digits = 0;
            for (int i = first; i <= last; i++) {
                if (i == point) continue;
                if (digits > 0 && digits == integralDigits) token('.');
                token(text.charAt(i));
                digits++;
            }
            for (; digits < integralDigits; digits++) token('0');
        } else {
            token(text.charAt(first));
            if (digitCount > 1) {
                token('.');
                for (int i = first + 1; i <= last; i++) if (i != point) token(text.charAt(i));
            }
            token('E');
            token(leadingExponent < 0 ? '-' : '+');
            int magnitude = Math.abs(leadingExponent);
            if (magnitude < 10) token('0');
            integer(magnitude);
        }
    }

    private boolean smallIntegral(double value, boolean negative, boolean legacyRange, double limit) {
        if (value == 0) {
            if (negative) token('-');
            token('0');
            return true;
        }
        if (!legacyRange || Math.abs(value) >= limit || (long) value != value) return false;
        integer((long) value);
        return true;
    }

    void ascii(String text) {
        reserve(text.length());
        for (int i = 0; i < text.length(); i++) buffer[position++] = (byte) text.charAt(i);
    }

    void string(String text) {
        if (text == null) { nullValue(); return; }
        token('"');
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);
            reserve(6);
            if (c >= ' ' && c < 128 && c != '"' && c != '\\') buffer[position++] = (byte) c;
            else if (c < 128) escape(c);
            else if (c < 0x800) {
                buffer[position++] = (byte) (0xc0 | (c >>> 6));
                buffer[position++] = (byte) (0x80 | (c & 63));
            } else if (!Character.isSurrogate(c)) {
                buffer[position++] = (byte) (0xe0 | (c >>> 12));
                buffer[position++] = (byte) (0x80 | ((c >>> 6) & 63));
                buffer[position++] = (byte) (0x80 | (c & 63));
            } else if (Character.isHighSurrogate(c) && i + 1 < text.length() && Character.isLowSurrogate(text.charAt(i + 1))) {
                int scalar = Character.toCodePoint(c, text.charAt(++i));
                buffer[position++] = (byte) (0xf0 | (scalar >>> 18));
                buffer[position++] = (byte) (0x80 | ((scalar >>> 12) & 63));
                buffer[position++] = (byte) (0x80 | ((scalar >>> 6) & 63));
                buffer[position++] = (byte) (0x80 | (scalar & 63));
            } else unicode(c);
        }
        token('"');
    }

    private void escape(char value) {
        int escape = switch (value) {
            case '"', '\\' -> value;
            case '\b' -> 'b';
            case '\t' -> 't';
            case '\n' -> 'n';
            case '\f' -> 'f';
            case '\r' -> 'r';
            default -> -1;
        };
        if (escape == -1) { unicode(value); return; }
        buffer[position++] = '\\';
        buffer[position++] = (byte) escape;
    }

    private void unicode(char c) {
        buffer[position++] = '\\';
        buffer[position++] = 'u';
        buffer[position++] = HEX[c >>> 12];
        buffer[position++] = HEX[(c >>> 8) & 15];
        buffer[position++] = HEX[(c >>> 4) & 15];
        buffer[position++] = HEX[c & 15];
    }
}
