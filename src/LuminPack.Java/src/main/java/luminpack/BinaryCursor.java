package luminpack;

import java.lang.invoke.MethodHandles;
import java.lang.invoke.VarHandle;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.Objects;

/** Checked little-endian cursors shared by the Java binary codecs. */
final class BinaryCursor {
    private static final VarHandle INT16 = MethodHandles.byteArrayViewVarHandle(short[].class, ByteOrder.LITTLE_ENDIAN);
    private static final VarHandle INT32 = MethodHandles.byteArrayViewVarHandle(int[].class, ByteOrder.LITTLE_ENDIAN);
    private static final VarHandle INT64 = MethodHandles.byteArrayViewVarHandle(long[].class, ByteOrder.LITTLE_ENDIAN);
    private static final long ASCII_MASK = 0x8080808080808080L;
    private static final long BYTE_ONES = 0x0101010101010101L;
    private static final long UTF16_ONES = 0x0001000100010001L;
    private static final long UTF16_HIGH_BITS = 0x8000800080008000L;
    private static final long UTF16_LATIN1_MASK = 0xff00ff00ff00ff00L;
    private static final int VECTOR_SCAN_THRESHOLD = 64;
    private static final StringKernels STRING_KERNELS = StringKernels.load();

    private BinaryCursor() {}

    static final class Reader {
        private final byte[] data;
        private final int start;
        private final int end;
        private final int maxCollectionLength;
        private final int maxStringLength;
        private int index;

        Reader(byte[] data) {
            this(data, Integer.MAX_VALUE, Integer.MAX_VALUE);
        }

        Reader(byte[] data, int maxStringLength) {
            this(data, Integer.MAX_VALUE, maxStringLength);
        }

        Reader(byte[] data, int maxCollectionLength, int maxStringLength) {
            this(data, 0, data.length, maxCollectionLength, maxStringLength);
            if (maxCollectionLength < 0 || maxStringLength < 0) {
                throw new IllegalArgumentException("Negative LuminPack input limit.");
            }
        }

        private Reader(byte[] data, int start, int end, int maxCollectionLength, int maxStringLength) {
            this.data = data;
            this.start = start;
            this.end = end;
            this.maxCollectionLength = maxCollectionLength;
            this.maxStringLength = maxStringLength;
            this.index = start;
        }

        int position() {
            return index - start;
        }

        int remaining() {
            return end - index;
        }

        void requireEnd() {
            if (index != end) {
                throw new IllegalArgumentException("Trailing bytes after the LuminPack record.");
            }
        }

        private void require(int width) {
            if (width < 0 || width > remaining()) {
                throw new IllegalArgumentException("Truncated LuminPack record.");
            }
        }

        byte[] take(int width) {
            require(width);
            byte[] result = Arrays.copyOfRange(data, index, index + width);
            index += width;
            return result;
        }

        void skip(int width) {
            require(width);
            index += width;
        }

        Reader subrecord(int width) {
            require(width);
            Reader result = new Reader(data, index, index + width, maxCollectionLength, maxStringLength);
            index += width;
            return result;
        }

        int peekU8() {
            require(1);
            return data[index] & 0xff;
        }

        int readU8() {
            int value = peekU8();
            index++;
            return value;
        }

        int readU16() {
            require(2);
            int value = (short) INT16.get(data, index) & 0xffff;
            index += 2;
            return value;
        }

        int readI32() {
            require(4);
            int value = (int) INT32.get(data, index);
            index += 4;
            return value;
        }

        long readI64() {
            require(8);
            long value = (long) INT64.get(data, index);
            index += 8;
            return value;
        }

        int readCollectionCount(int minimumWidth) {
            require(4);
            int count = (int) INT32.get(data, index);
            if (count < -1 || count > maxCollectionLength || minimumWidth < 0 ||
                    (minimumWidth != 0 && count > (remaining() - 4) / minimumWidth)) {
                throw new IllegalArgumentException("Invalid LuminPack collection count.");
            }
            index += 4;
            return count;
        }

        private int readLength(boolean variable) {
            if (!variable) {
                return readI32();
            }
            long value = 0;
            for (int shift = 0; shift <= 28; shift += 7) {
                int next = readU8();
                if (shift == 28 && next > 8) {
                    break;
                }
                value |= (long) (next & 127) << shift;
                if (next < 128) {
                    if ((shift != 0 && next == 0) || value > 0x80000000L) {
                        break;
                    }
                    return (int) (value - 1);
                }
            }
            throw new IllegalArgumentException("Invalid LuminPack variable string length.");
        }

        private void checkStringLength(int length) {
            if (length > maxStringLength) {
                throw new IllegalArgumentException("LuminPack string exceeds the configured length limit.");
            }
        }

        String readString(int profile) {
            return readString(profile, false);
        }

        void skipString(int profile) {
            readString(profile, true);
        }

        private String readString(int profile, boolean skip) {
            checkProfile(profile);
            if (profile == 2 && index <= end - Integer.BYTES) {
                int header = (int) INT32.get(data, index);
                if (header > 1 && header <= 33 && (header & 1) != 0) {
                    int length = header >>> 1;
                    checkStringLength(length);
                    require(Integer.BYTES + length);
                    String value = skip ? null : new String(data, index + Integer.BYTES, length, StandardCharsets.ISO_8859_1);
                    index += Integer.BYTES + length;
                    return value;
                }
            }
            int saved = index;
            try {
                boolean utf8 = usesUtf8(profile);
                boolean token = usesToken(profile);
                int characters = -1;
                boolean latin1 = false;
                int width;
                int terminator = 0;
                if (token) {
                    terminator = utf8 ? 1 : 2;
                    width = tokenWidth(utf8);
                } else {
                    boolean variable = usesVarint(profile);
                    int header = readLength(variable);
                    if (utf8) {
                        characters = readLength(variable);
                    }
                    if (header < -1) {
                        throw new IllegalArgumentException("Invalid LuminPack string length.");
                    }
                    if (utf8 && variable && (header == -1 ? characters != -1 :
                            characters < 0 || characters > header || (header != 0 && characters == 0))) {
                        throw new IllegalArgumentException("Invalid LuminPack UTF-8 character count.");
                    }
                    if (header == -1) {
                        return null;
                    }
                    latin1 = !utf8 && (header & 1) != 0;
                    width = latin1 ? header >>> 1 : header;
                    require(width);
                }
                String result;
                if (utf8) {
                    result = decodeUtf8(width, characters, skip);
                } else {
                    int length = latin1 ? width : width >>> 1;
                    checkStringLength(length);
                    result = skip ? null : latin1 ? new String(data, index, width, StandardCharsets.ISO_8859_1) :
                        decodeUtf16(length, token);
                }
                index += width + terminator;
                return result;
            } catch (IllegalArgumentException error) {
                index = saved;
                throw error;
            }
        }

        private int tokenWidth(boolean utf8) {
            int at = index;
            long ones = utf8 ? BYTE_ONES : UTF16_ONES;
            long high = utf8 ? ASCII_MASK : UTF16_HIGH_BITS;
            while (at <= end - Long.BYTES) {
                long word = (long) INT64.get(data, at);
                long zeroes = zeroBits(word, ones, high);
                if (zeroes != 0) {
                    int lane = Long.numberOfTrailingZeros(zeroes);
                    return at - index + (utf8 ? lane >>> 3 : (lane >>> 4) * Character.BYTES);
                }
                at += Long.BYTES;
                if (STRING_KERNELS != null && end - at >= VECTOR_SCAN_THRESHOLD) {
                    int terminator = utf8 ? STRING_KERNELS.firstZeroByte(data, at, end) :
                        STRING_KERNELS.firstZeroUtf16(data, at, end);
                    if (terminator != end) return terminator - index;
                    throw new IllegalArgumentException("Missing LuminPack string token terminator.");
                }
            }
            int unit = utf8 ? 1 : Character.BYTES;
            for (; at <= end - unit; at += unit) {
                if (data[at] == 0 && (utf8 || data[at + 1] == 0)) {
                    return at - index;
                }
            }
            throw new IllegalArgumentException("Missing LuminPack string token terminator.");
        }

        private String decodeUtf16(int length, boolean replace) {
            if (length == 0) {
                return "";
            }
            char[] result = new char[length];
            for (int i = 0, at = index; i < length; ++i, at += Character.BYTES) {
                result[i] = (char) (short) INT16.get(data, at);
            }
            if (replace) {
                for (int i = 0; i < length; ++i) {
                    char value = result[i];
                    if (Character.isHighSurrogate(value)) {
                        if (i + 1 < length && Character.isLowSurrogate(result[i + 1])) {
                            i++;
                        } else {
                            result[i] = '\ufffd';
                        }
                    } else if (Character.isLowSurrogate(value)) {
                        result[i] = '\ufffd';
                    }
                }
            }
            return new String(result);
        }

        private String decodeUtf8(int width, int expected, boolean skip) {
            if (width == 0) {
                return skip ? null : "";
            }
            if (expected > width) {
                throw new IllegalArgumentException("Invalid LuminPack UTF-8 character count.");
            }
            checkStringLength(expected);
            int limit = index + width;
            int ascii;
            if (STRING_KERNELS != null && width >= VECTOR_SCAN_THRESHOLD) {
                ascii = STRING_KERNELS.firstNonAscii(data, index, limit);
            } else {
                ascii = index;
                while (ascii <= limit - Long.BYTES && ((long) INT64.get(data, ascii) & ASCII_MASK) == 0)
                    ascii += Long.BYTES;
                while (ascii < limit && data[ascii] >= 0) ascii++;
            }
            if (ascii == limit) {
                checkCharacterCount(expected, width);
                return skip ? null : new String(data, index, width, StandardCharsets.ISO_8859_1);
            }

            boolean replace = expected <= 0 && !skip;
            int characters = ascii - index;
            for (int at = ascii; at < limit; ) {
                long scalar = utf8Scalar(at, limit, replace);
                at = (int) (scalar >>> 32);
                characters += (int) scalar <= 0xffff ? 1 : 2;
            }
            checkCharacterCount(expected, characters);
            if (skip) {
                return null;
            }
            char[] result = new char[characters];
            int written = ascii - index;
            for (int i = 0; i < written; i++) {
                result[i] = (char) data[index + i];
            }
            for (int at = ascii; at < limit; ) {
                long scalar = utf8Scalar(at, limit, replace);
                at = (int) (scalar >>> 32);
                int value = (int) scalar;
                if (value <= 0xffff) {
                    result[written++] = (char) value;
                } else {
                    result[written++] = Character.highSurrogate(value);
                    result[written++] = Character.lowSurrogate(value);
                }
            }
            return new String(result);
        }

        private void checkCharacterCount(int expected, int actual) {
            if (expected > 0 && expected != actual) {
                throw new IllegalArgumentException("Invalid LuminPack UTF-8 character count.");
            }
            checkStringLength(actual);
        }

        // The upper half is the next offset, including the valid prefix of any malformed sequence.
        private long utf8Scalar(int at, int limit, boolean replace) {
            int first = data[at++] & 0xff;
            if (first < 0x80) {
                return ((long) at << 32) | first;
            }
            int width = first >= 0xc2 && first <= 0xdf ? 2 : first >= 0xe0 && first <= 0xef ? 3 :
                first >= 0xf0 && first <= 0xf4 ? 4 : 0;
            if (width != 0) {
                int value = first & (0x7f >>> width);
                int lower = first == 0xe0 ? 0xa0 : first == 0xf0 ? 0x90 : 0x80;
                int upper = first == 0xed ? 0x9f : first == 0xf4 ? 0x8f : 0xbf;
                for (int part = 1; part < width && at < limit; part++) {
                    int next = data[at] & 0xff;
                    if (next < lower || next > upper) {
                        break;
                    }
                    at++;
                    value = value << 6 | (next & 0x3f);
                    if (part + 1 == width) {
                        return ((long) at << 32) | value;
                    }
                    lower = 0x80;
                    upper = 0xbf;
                }
            }
            if (!replace) {
                throw new IllegalArgumentException("Invalid UTF-8 in a LuminPack string.");
            }
            return ((long) at << 32) | 0xfffd;
        }
    }

    static final class Writer {
        private byte[] data;
        private int index;

        Writer() {
            this(256);
        }

        Writer(int initialCapacity) {
            if (initialCapacity < 0) {
                throw new IllegalArgumentException("Negative LuminPack output capacity.");
            }
            data = new byte[initialCapacity];
        }

        int position() {
            return index;
        }

        byte[] toByteArray() {
            return Arrays.copyOf(data, index);
        }

        private void ensure(int width) {
            if (width < 0 || width > Integer.MAX_VALUE - index) {
                throw new IllegalArgumentException("LuminPack output exceeds the Java array limit.");
            }
            int required = index + width;
            if (required > data.length) {
                long grown = data.length + (long) (data.length >>> 1);
                data = Arrays.copyOf(data, Math.max(required, (int) Math.min(grown, Integer.MAX_VALUE - 8L)));
            }
        }

        void writeU8(int value) {
            ensure(1);
            data[index++] = (byte) value;
        }

        void writeU16(int value) {
            ensure(2);
            INT16.set(data, index, (short) value);
            index += 2;
        }

        void writeI32(int value) {
            ensure(4);
            INT32.set(data, index, value);
            index += 4;
        }

        void patchI32(int position, int value) {
            Objects.checkFromIndexSize(position, 4, index);
            INT32.set(data, position, value);
        }

        void writeI64(long value) {
            ensure(8);
            INT64.set(data, index, value);
            index += 8;
        }

        void writeBytes(byte[] value) {
            ensure(value.length);
            System.arraycopy(value, 0, data, index, value.length);
            index += value.length;
        }

        private void writeVariableLength(int value) {
            int remaining = value + 1;
            while ((remaining & ~127) != 0) {
                data[index++] = (byte) (remaining | 128);
                remaining >>>= 7;
            }
            data[index++] = (byte) remaining;
        }

        private void ensureRecord(int width, int overhead) {
            if (width > Integer.MAX_VALUE - overhead)
                throw new IllegalArgumentException("LuminPack text record exceeds the Java array limit.");
            ensure(width + overhead);
        }

        private void writeUtf8Header(int width, int length, boolean variable) {
            if (variable) {
                writeVariableLength(width);
                writeVariableLength(length);
            } else {
                INT64.set(data, index, (Integer.toUnsignedLong(length) << 32) | Integer.toUnsignedLong(width));
                index += Long.BYTES;
            }
        }

        void writeString(String value, int profile) {
            checkProfile(profile);
            boolean utf8 = usesUtf8(profile);
            boolean token = usesToken(profile);
            boolean variable = usesVarint(profile);
            if (value == null || value.isEmpty()) {
                if (token) {
                    if (utf8) {
                        writeU8(0);
                    } else {
                        writeU16(0);
                    }
                } else {
                    int header = value == null ? -1 : 0;
                    if (utf8) {
                        ensure(variable ? 2 : Long.BYTES);
                        writeUtf8Header(header, header, variable);
                    } else if (variable) writeU8(header + 1);
                    else writeI32(header);
                }
                return;
            }
            int length = value.length();
            if (utf8) {
                int width = utf8Width(value);
                int overhead = token ? 1 : variable ? varintSize(width) + varintSize(length) : Long.BYTES;
                ensureRecord(width, overhead);
                if (!token) {
                    writeUtf8Header(width, length, variable);
                }
                if (width == length) writeNarrow(value, length);
                else writeUtf8(value);
                if (token) {
                    data[index++] = 0;
                }
                return;
            }
            if (length > Integer.MAX_VALUE / 2) {
                throw new IllegalArgumentException("LuminPack UTF-16 record exceeds the wire length.");
            }
            if (profile == 2 && length >= 4 && length <= 16) {
                writeShortUtf16(value, length);
                return;
            }
            boolean latin1 = !token && isLatin1(value, length);
            int header = length * Character.BYTES | (latin1 ? 1 : 0);
            int overhead = token ? Character.BYTES : variable ? varintSize(header) : Integer.BYTES;
            ensureRecord(latin1 ? length : length * Character.BYTES, overhead);
            if (!token) {
                if (variable) writeVariableLength(header);
                else {
                    INT32.set(data, index, header);
                    index += Integer.BYTES;
                }
            }
            if (latin1) {
                writeNarrow(value, length);
            } else {
                for (int i = 0; i < length; ++i) {
                    INT16.set(data, index, (short) value.charAt(i));
                    index += Character.BYTES;
                }
            }
            if (token) {
                INT16.set(data, index, (short) 0);
                index += Character.BYTES;
            }
        }

        private void writeShortUtf16(String value, int length) {
            // Complete words at the ends and interior cover the record with bounded loads.
            long first = utf16Word(value, 0);
            long last = utf16Word(value, length - 4);
            long second = length > 8 ? utf16Word(value, 4) : 0;
            long third = length > 12 ? utf16Word(value, 8) : 0;
            boolean latin1 = ((first | second | third | last) & UTF16_LATIN1_MASK) == 0;
            int width = latin1 ? length : length * Character.BYTES;
            ensure(Integer.BYTES + width);
            INT32.set(data, index, length * Character.BYTES | (latin1 ? 1 : 0));
            int target = index + Integer.BYTES;
            if (latin1) {
                INT32.set(data, target, narrowWord(first));
                if (length > 8) INT32.set(data, target + 4, narrowWord(second));
                if (length > 12) INT32.set(data, target + 8, narrowWord(third));
                INT32.set(data, target + width - 4, narrowWord(last));
            } else {
                INT64.set(data, target, first);
                if (length > 8) INT64.set(data, target + 8, second);
                if (length > 12) INT64.set(data, target + 16, third);
                INT64.set(data, target + width - Long.BYTES, last);
            }
            index = target + width;
        }

        private void writeNarrow(String value, int length) {
            for (int i = 0; i < length; ++i) {
                data[index++] = (byte) value.charAt(i);
            }
        }

        private void writeUtf8(String value) {
            for (int i = 0, length = value.length(); i < length; ++i) {
                int code = value.charAt(i);
                if (code < 0x80) {
                    data[index++] = (byte) code;
                } else if (code < 0x800) {
                    data[index++] = (byte) (0xc0 | (code >>> 6));
                    data[index++] = (byte) (0x80 | (code & 63));
                } else if (Character.isHighSurrogate((char) code)) {
                    code = Character.toCodePoint((char) code, value.charAt(++i));
                    data[index++] = (byte) (0xf0 | (code >>> 18));
                    data[index++] = (byte) (0x80 | ((code >>> 12) & 63));
                    data[index++] = (byte) (0x80 | ((code >>> 6) & 63));
                    data[index++] = (byte) (0x80 | (code & 63));
                } else {
                    data[index++] = (byte) (0xe0 | (code >>> 12));
                    data[index++] = (byte) (0x80 | ((code >>> 6) & 63));
                    data[index++] = (byte) (0x80 | (code & 63));
                }
            }
        }
    }

    private static void checkProfile(int profile) {
        if (profile < 0 || profile > 5) {
            throw new IllegalArgumentException("Unknown LuminPack string profile.");
        }
    }

    private static boolean usesUtf8(int profile) {
        return profile == 0 || profile == 1 || profile == 4;
    }

    private static boolean usesToken(int profile) {
        return profile == 1 || profile == 3;
    }

    private static boolean usesVarint(int profile) {
        return profile == 4 || profile == 5;
    }

    private static long zeroBits(long word, long ones, long high) {
        // Subtraction identifies the lowest zero lane even when borrows affect later lanes.
        return (word - ones) & ~word & high;
    }

    private static long utf16Word(String value, int start) {
        return (long) value.charAt(start) | ((long) value.charAt(start + 1) << 16) |
            ((long) value.charAt(start + 2) << 32) | ((long) value.charAt(start + 3) << 48);
    }

    private static int narrowWord(long word) {
        return (int) ((word & 0xffL) | (word >>> 8 & 0xff00L) |
            (word >>> 16 & 0xff0000L) | (word >>> 24 & 0xff000000L));
    }

    private static boolean isLatin1(String value, int length) {
        for (int i = 0; i < length; i++)
            if (value.charAt(i) > 0xff) return false;
        return true;
    }

    private static int varintSize(int length) {
        int bits = Integer.SIZE - Integer.numberOfLeadingZeros(length + 1);
        return Math.max(1, (bits + 6) / 7);
    }

    private static int utf8Width(String value) {
        long width = 0;
        for (int i = 0, length = value.length(); i < length; ++i) {
            char character = value.charAt(i);
            if (character < 0x80) {
                width++;
            } else if (character < 0x800) {
                width += 2;
            } else if (Character.isHighSurrogate(character)) {
                if (++i == length || !Character.isLowSurrogate(value.charAt(i))) {
                    throw new IllegalArgumentException("LuminPack UTF-8 encoding requires valid UTF-16.");
                }
                width += 4;
            } else if (Character.isLowSurrogate(character)) {
                throw new IllegalArgumentException("LuminPack UTF-8 encoding requires valid UTF-16.");
            } else {
                width += 3;
            }
        }
        if (width > Integer.MAX_VALUE) {
            throw new IllegalArgumentException("LuminPack UTF-8 record exceeds the wire length.");
        }
        return (int) width;
    }
}
