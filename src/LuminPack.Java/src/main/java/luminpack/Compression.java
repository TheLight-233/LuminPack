package luminpack;

import java.lang.invoke.MethodHandles;
import java.lang.invoke.VarHandle;
import java.nio.ByteOrder;
import java.util.Arrays;

/** Portable LUM2 byte codec for compressed binary members. */
final class Compression {
    private static final int MAGIC = 0x324d554c;
    private static final int HEADER = 8;
    private static final int MAX_SOURCE_LENGTH = 2_130_836_464;
    private static final int MIN_MATCH = 4;
    private static final int LAST_LITERALS = 5;
    private static final int MATCH_GUARD = 12;
    private static final int SATURATED = 15;
    private static final int LENGTH_ESCAPE = 255;
    private static final VarHandle INT32 = MethodHandles.byteArrayViewVarHandle(int[].class, ByteOrder.LITTLE_ENDIAN);
    private static final VarHandle INT64 = MethodHandles.byteArrayViewVarHandle(long[].class, ByteOrder.LITTLE_ENDIAN);
    private static final ThreadLocal<char[]> POSITIONS = ThreadLocal.withInitial(() -> new char[0]);

    private Compression() {}

    static byte[] compress(byte[] input) {
        int size = input.length;
        if (size > MAX_SOURCE_LENGTH) {
            throw new IllegalArgumentException("LUM2 source exceeds the maximum length.");
        }
        if (size == 0) {
            byte[] frame = new byte[HEADER];
            INT32.set(frame, 0, MAGIC);
            return frame;
        }
        Encoder encoder = new Encoder(input);
        if (size >= MIN_MATCH + LAST_LITERALS + MATCH_GUARD) {
            encoder.match();
        }
        encoder.literals(encoder.anchor, size - encoder.anchor, 0);
        return Arrays.copyOf(encoder.output, encoder.written);
    }

    static byte[] decompress(byte[] frame, int expectedLength) {
        if (frame.length < HEADER || (int) INT32.get(frame, 0) != MAGIC ||
                expectedLength < 0 || (int) INT32.get(frame, 4) != expectedLength) {
            throw invalidFrame();
        }
        if (expectedLength == 0) {
            if (frame.length != HEADER) {
                throw invalidFrame();
            }
            return new byte[0];
        }
        int body = frame.length - HEADER;
        if (body == 0) {
            throw invalidFrame();
        }
        if ((expectedLength - 1) / LENGTH_ESCAPE >= body) {
            // Validate the full structure before allocating storage for a high-ratio expansion.
            readBody(frame, null, expectedLength);
        }
        byte[] result = new byte[expectedLength];
        readBody(frame, result, expectedLength);
        return result;
    }

    private static IllegalArgumentException invalidFrame() {
        return new IllegalArgumentException("Invalid LUM2 compression frame.");
    }

    private static int matchHash(int word, int shift) {
        return (word * 0x9e3779b1) >>> shift;
    }

    private static int backwardMatch(byte[] source, int anchor, int position, int candidate) {
        int start = position;
        while (position > anchor && candidate != 0 && source[position - 1] == source[candidate - 1]) {
            position--;
            candidate--;
        }
        return start - position;
    }

    private static int matchLength(byte[] source, int first, int second, int length) {
        if (length >= 8) {
            long difference = (long) INT64.get(source, first) ^ (long) INT64.get(source, second);
            if (difference != 0) {
                return Long.numberOfTrailingZeros(difference) >>> 3;
            }
            // The JVM can use its intrinsic array mismatch for the rest of the bounded window.
            int mismatch = Arrays.mismatch(source, first + 8, first + length, source, second + 8, second + length);
            return mismatch < 0 ? length : 8 + mismatch;
        }
        int matched = 0;
        while (matched < length && source[first + matched] == source[second + matched]) {
            matched++;
        }
        return matched;
    }

    private static final class Encoder {
        private final byte[] input;
        private final byte[] output;
        private int anchor;
        private int written = HEADER;

        Encoder(byte[] input) {
            this.input = input;
            int size = input.length;
            output = new byte[HEADER + size + (size >>> 7) + 16];
            INT32.set(output, 0, MAGIC);
            INT32.set(output, 4, size);
        }

        void match() {
            int size = input.length;
            int tableBits = Math.min(15, Math.max(8, 32 - Integer.numberOfLeadingZeros((size - 1) | 1)));
            int tableSize = 1 << tableBits;
            int shift = 32 - tableBits;
            char[] table = POSITIONS.get();
            if (table.length < tableSize) {
                table = new char[tableSize];
                POSITIONS.set(table);
            }
            int matchLimit = size - MATCH_GUARD;
            int extensionLimit = size - LAST_LITERALS;
            table[matchHash((int) INT32.get(input, 0), shift)] = 0;
            int position = 1;
            int skip = 32;
            while (position < matchLimit) {
                int word = (int) INT32.get(input, position);
                int hash = matchHash(word, shift);
                int distance = (position - table[hash]) & 0xffff;
                table[hash] = (char) position;
                // Retained positions can come from a previous frame or a wrap of this frame.
                if (distance == 0 || distance > position || (int) INT32.get(input, position - distance) != word) {
                    position += skip++ >>> 5;
                    continue;
                }
                skip = 32;

                int candidate = position - distance;
                int backward = backwardMatch(input, anchor, position, candidate);
                position -= backward;
                candidate -= backward;
                int length = MIN_MATCH + matchLength(input, position + MIN_MATCH,
                        candidate + MIN_MATCH, extensionLimit - position - MIN_MATCH);

                // Publish the next hash even if the lazy match loses.
                int lazy = position + 1;
                if (lazy < matchLimit) {
                    int lazyWord = (int) INT32.get(input, lazy);
                    int lazyHash = matchHash(lazyWord, shift);
                    int lazyDistance = (lazy - table[lazyHash]) & 0xffff;
                    table[lazyHash] = (char) lazy;
                    if (lazyDistance != 0 && lazyDistance <= lazy && extensionLimit - lazy > length) {
                        int lazyCandidate = lazy - lazyDistance;
                        if ((int) INT32.get(input, lazyCandidate) == lazyWord &&
                                (int) INT32.get(input, lazy + length - 3) ==
                                (int) INT32.get(input, lazyCandidate + length - 3)) {
                            int lazyLength = MIN_MATCH + matchLength(input, lazy + MIN_MATCH,
                                    lazyCandidate + MIN_MATCH, extensionLimit - lazy - MIN_MATCH);
                            if (lazyLength > length) {
                                backward = backwardMatch(input, anchor, lazy, lazyCandidate);
                                position = lazy - backward;
                                candidate = lazyCandidate - backward;
                                length = lazyLength + backward;
                            }
                        }
                    }
                }

                int match = length - MIN_MATCH;
                literals(anchor, position - anchor, match);
                int offset = position - candidate;
                output[written++] = (byte) offset;
                output[written++] = (byte) (offset >>> 8);
                if (match >= SATURATED) {
                    extension(match - SATURATED);
                }
                position += length;
                anchor = position;
                if (position < matchLimit) {
                    for (int boundary = position - 2; boundary < position; boundary++) {
                        int key = (int) INT32.get(input, boundary);
                        table[matchHash(key, shift)] = (char) boundary;
                    }
                }
            }
        }

        void literals(int start, int length, int match) {
            output[written++] = (byte) (Math.min(length, SATURATED) << 4 | Math.min(match, SATURATED));
            if (length >= SATURATED) {
                extension(length - SATURATED);
            }
            if (length != 0) {
                System.arraycopy(input, start, output, written, length);
                written += length;
            }
        }

        private void extension(int length) {
            if (length >= LENGTH_ESCAPE) {
                output[written++] = (byte) LENGTH_ESCAPE;
                length -= LENGTH_ESCAPE;
                while (length >= 128) {
                    output[written++] = (byte) (length | 0x80);
                    length >>>= 7;
                }
            }
            output[written++] = (byte) length;
        }
    }

    private static void readBody(byte[] frame, byte[] destination, int length) {
        BinaryCursor.Reader input = new BinaryCursor.Reader(frame);
        input.skip(HEADER);
        int produced = 0;
        while (input.remaining() != 0) {
            int token = input.readU8();
            int literals = token >>> 4;
            int match = token & 15;
            if (literals == SATURATED) {
                literals = readExtension(input, literals, length - produced);
            }
            if (literals > input.remaining() || literals > length - produced) {
                throw invalidFrame();
            }
            if (destination != null && literals != 0) {
                System.arraycopy(frame, input.position(), destination, produced, literals);
            }
            input.skip(literals);
            produced += literals;
            if (input.remaining() == 0) {
                if (match != 0 || produced != length) {
                    throw invalidFrame();
                }
                return;
            }

            int distance = input.readU16();
            if (distance == 0 || distance > produced) {
                throw invalidFrame();
            }
            int matchLength = match + MIN_MATCH;
            if (match == SATURATED) {
                matchLength = readExtension(input, matchLength, length - produced);
            }
            if (matchLength > length - produced) {
                throw invalidFrame();
            }
            if (destination != null) {
                copyMatch(destination, produced, matchLength, distance);
            }
            produced += matchLength;
        }
        throw invalidFrame();
    }

    private static int readExtension(BinaryCursor.Reader input, int length, int maximum) {
        if (length > maximum) {
            throw invalidFrame();
        }
        int first = input.readU8();
        if (first > maximum - length) {
            throw invalidFrame();
        }
        length += first;
        if (first != LENGTH_ESCAPE) {
            return length;
        }
        int extra = 0;
        for (int shift = 0; ; shift += 7) {
            int next = input.readU8();
            if (shift == 28 && next > 7) {
                throw invalidFrame();
            }
            extra |= (next & 127) << shift;
            if (extra > maximum - length) {
                throw invalidFrame();
            }
            if (next < 128) {
                if (shift != 0 && next == 0) {
                    throw invalidFrame();
                }
                return length + extra;
            }
        }
    }

    private static void copyMatch(byte[] destination, int at, int length, int distance) {
        int source = at - distance;
        if (distance == 1) {
            Arrays.fill(destination, at, at + length, destination[source]);
        } else if (length <= distance) {
            System.arraycopy(destination, source, destination, at, length);
        } else if (length >= 256 || (distance < 8 && length >= 64)) {
            for (int copied = 0; copied < length; ) {
                int count = Math.min(distance + copied, length - copied);
                System.arraycopy(destination, source, destination, at + copied, count);
                copied += count;
            }
        } else {
            int copied = 0;
            if (distance >= 8) {
                int width = Math.min(distance, 32) & ~7;
                while (copied <= length - width) {
                    System.arraycopy(destination, source + copied, destination, at + copied, width);
                    copied += width;
                }
            }
            for (; copied < length; copied++) {
                destination[at + copied] = destination[source + copied];
            }
        }
    }
}
