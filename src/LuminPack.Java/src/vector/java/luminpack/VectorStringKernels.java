package luminpack;

import jdk.incubator.vector.ByteVector;
import jdk.incubator.vector.VectorOperators;
import jdk.incubator.vector.VectorSpecies;

/** Bounded Binary string scans using the fastest vector species supported by the JVM. */
public final class VectorStringKernels implements StringKernels {
    private static final VectorSpecies<Byte> BYTES = ByteVector.SPECIES_PREFERRED;

    @Override public int firstNonAscii(byte[] data, int from, int to) {
        int lanes = BYTES.length();
        int at = from;
        for (; at <= to - lanes; at += lanes) {
            int first = ByteVector.fromArray(BYTES, data, at).compare(VectorOperators.LT, (byte) 0).firstTrue();
            if (first != lanes) return at + first;
        }
        for (; at < to; at++) if (data[at] < 0) return at;
        return to;
    }

    @Override public int firstZeroByte(byte[] data, int from, int to) {
        int lanes = BYTES.length();
        int at = from;
        for (; at <= to - lanes; at += lanes) {
            int first = ByteVector.fromArray(BYTES, data, at).eq((byte) 0).firstTrue();
            if (first != lanes) return at + first;
        }
        for (; at < to; at++) if (data[at] == 0) return at;
        return to;
    }

    @Override public int firstZeroUtf16(byte[] data, int from, int to) {
        int span = BYTES.length();
        int lanes = span / Short.BYTES;
        int at = from;
        for (; at <= to - span; at += span) {
            int first = ByteVector.fromArray(BYTES, data, at).reinterpretAsShorts().eq((short) 0).firstTrue();
            if (first != lanes) return at + first * Short.BYTES;
        }
        for (; at <= to - Short.BYTES; at += Short.BYTES) {
            if (data[at] == 0 && data[at + 1] == 0) return at;
        }
        return to;
    }
}
