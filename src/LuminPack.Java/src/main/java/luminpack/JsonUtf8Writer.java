package luminpack;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;

/** Checked UTF-8 output cursor for generated JSON property heads and typed scalar values. */
final class JsonUtf8Writer {
    private static final byte[] NULL = {'n', 'u', 'l', 'l'};
    private static final byte[] TRUE = {'t', 'r', 'u', 'e'};
    private static final byte[] FALSE = {'f', 'a', 'l', 's', 'e'};
    private static final byte[] HEX = "0123456789abcdef".getBytes(StandardCharsets.US_ASCII);
    private final JsonNumberFormatter numbers = new JsonNumberFormatter();

    private byte[] buffer = new byte[512];
    private int position;

    private static final ThreadLocal<JsonUtf8Writer> CACHE = new ThreadLocal<>();

    static JsonUtf8Writer acquire() {
        JsonUtf8Writer writer = CACHE.get();
        if (writer == null) return new JsonUtf8Writer();
        CACHE.set(null);
        return writer;
    }

    void release() {
        if (buffer.length <= 1024 * 1024) {
            position = 0;
            CACHE.set(this);
        }
    }

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

    void integer32(int value) {
        reserve(JsonNumberFormatter.MAX_SIGNED32_BYTES);
        position += JsonNumberFormatter.signed32(buffer, position, value);
    }

    void unsignedInteger32(long value) {
        reserve(JsonNumberFormatter.MAX_UNSIGNED32_BYTES);
        position += JsonNumberFormatter.unsigned32(buffer, position, value);
    }

    void integer(long value) {
        reserve(JsonNumberFormatter.MAX_INTEGER64_BYTES);
        position += JsonNumberFormatter.signed64(buffer, position, value);
    }

    void unsignedInteger(long value) {
        reserve(JsonNumberFormatter.MAX_INTEGER64_BYTES);
        position += JsonNumberFormatter.unsigned64(buffer, position, value);
    }

    void floating(float value) {
        reserve(JsonNumberFormatter.MAX_BYTES);
        int integer = (int) value;
        if (integer == value && integer > -16777216 && integer < 16777216 && Float.floatToRawIntBits(value) != Integer.MIN_VALUE)
            position += JsonNumberFormatter.signed32(buffer, position, integer);
        else position += numbers.floating(buffer, position, value);
    }

    void floating(double value) {
        reserve(JsonNumberFormatter.MAX_BYTES);
        position += numbers.floating(buffer, position, value);
    }

    void string(String text) {
        if (text == null) { nullValue(); return; }
        token('"');
        // Copy an ordinary ASCII prefix with one capacity check. Escaped and
        // non-ASCII tails retain the checked Unicode path.
        reserve(text.length());
        int i = 0;
        for (; i < text.length(); i++) {
            char c = text.charAt(i);
            if (c < ' ' || c >= 128 || c == '"' || c == '\\') break;
            buffer[position++] = (byte) c;
        }
        for (; i < text.length(); i++) {
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
