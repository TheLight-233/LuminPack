package luminpack;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.Arrays;
import java.util.List;
import java.util.Map;

/** Checks modeled collection storage independently of the managed interop fixtures. */
public final class RawLayoutChecks {
    private RawLayoutChecks() {}

    public static void main(String[] args) {
        String fields = """
            "wireSize":10,"fields":[{"name":"Code","storage":{"wireSize":1}},
             {"name":"Enabled","storage":{"wireSize":1}}, {"name":"Amount","storage":{"wireSize":8}}]
            """;
        LuminPack packed = codec("\"storage\":{" + fields + "}");
        byte[] packedBytes = rows(0, 10, 2, 0);
        require(Arrays.equals(packedBytes, packed.writeBinary(packed.readBinary(packedBytes))), "Packed byte copy");

        LuminPack tagged = codec("\"storage\":{\"tagged\":true,\"modelSizes\":[16,12],"
            + "\"modelOffsets\":[[0,1,8,9,10,11,12,13,14,15],[0,1,4,5,6,7,8,9,10,11]]," + fields + "}");
        byte[] taggedBytes = rows(1, 16, 8, 0);
        byte[] alternate = rows(2, 12, 4, 0x73);
        require(Arrays.equals(taggedBytes, tagged.writeBinary(tagged.readBinary(alternate))), "Tagged layout conversion");
        require(Arrays.equals(taggedBytes, tagged.writeBinary(tagged.readBinary(taggedBytes))), "Tagged byte copy");
        byte[] unknown = alternate.clone();
        unknown[4] = 3;
        reject(tagged, unknown);
        reject(tagged, Arrays.copyOf(alternate, alternate.length - 1));
        alternate[5 + 12 + 1] = 2;
        reject(tagged, alternate);

        LuminPack nativeCodec = codec("\"nativeStrategy\":0,\"nativeStorage\":{\"modelSizes\":[16],"
            + "\"modelOffsets\":[[0,1,8,9,10,11,12,13,14,15]]," + fields + "}");
        byte[] dirty = rows(0, 16, 8, 0x41);
        List<Object> decoded = asList(nativeCodec.readBinary(dirty));
        require(Arrays.equals(dirty, nativeCodec.writeBinary(decoded)), "Native dirty padding byte copy");
        Map<String, Object> first = asRecord(decoded.get(0));
        require(first == decoded.get(0), "Materialized rows retain identity");
        first.put("Amount", -117L);
        byte[] edited = dirty.clone();
        ByteBuffer.wrap(edited).order(ByteOrder.LITTLE_ENDIAN).putLong(4 + 8, -117L);
        require(Arrays.equals(edited, nativeCodec.writeBinary(decoded)), "Native edit retains both rows' padding");
        Object row = decoded.remove(0);
        decoded.add(row);
        byte[] reordered = edited.clone();
        System.arraycopy(edited, 20, reordered, 4, 16);
        System.arraycopy(edited, 4, reordered, 20, 16);
        require(Arrays.equals(reordered, nativeCodec.writeBinary(decoded)), "Native structural edit remains writable");
    }

    private static byte[] rows(int tag, int stride, int scalar, int padding) {
        int start = tag == 0 ? 4 : 5;
        byte[] result = new byte[start + stride * 2];
        var buffer = ByteBuffer.wrap(result).order(ByteOrder.LITTLE_ENDIAN);
        buffer.putInt(2);
        if (tag != 0) buffer.put((byte) tag);
        for (int at = start; at < result.length; at += stride) {
            result[at] = (byte) (at + 11);
            result[at + 1] = 1;
            Arrays.fill(result, at + 2, at + scalar, (byte) padding);
            buffer.putLong(at + scalar, at == start ? 123 : Long.MIN_VALUE);
        }
        result[start + stride] = 64;
        return result;
    }

    private static LuminPack codec(String storage) {
        return LuminPack.fromManifest("""
            {"version":1,"root":"Row[]","requireCompleteBinaryInput":true,
             "rootWire":{"kind":"sequence","element":{"kind":"contract","type":"Row"}},
             "contracts":[{"id":"Row","type":"Row","kind":"Struct","profile":{},"members":[
               {"name":"Code","profile":{},"wire":{"kind":"scalar","type":"byte"}},
               {"name":"Enabled","profile":{},"wire":{"kind":"scalar","type":"bool"}},
               {"name":"Amount","profile":{},"wire":{"kind":"scalar","type":"long"}}],
               %s}]}
            """.formatted(storage));
    }

    @SuppressWarnings("unchecked")
    private static List<Object> asList(Object value) { return (List<Object>) value; }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> asRecord(Object value) { return (Map<String, Object>) value; }

    private static void reject(LuminPack codec, byte[] value) {
        try { codec.readBinary(value); }
        catch (IllegalArgumentException expected) { return; }
        throw new AssertionError("Malformed storage was accepted");
    }

    private static void require(boolean condition, String description) {
        if (!condition) throw new AssertionError(description);
    }
}
