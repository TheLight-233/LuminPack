package luminpack;

import java.util.Arrays;
import java.util.List;

/** Exercises the default SWAR kernels and the optional Vector API provider. */
public final class FastPathChecks {
    private FastPathChecks() {}

    public static void main(String[] args) {
        StringKernels provider = StringKernels.load();
        boolean vector = args.length == 1 && args[0].equals("vector");
        require((provider != null) == vector, "Unexpected vector provider state");
        if (provider != null) checkScans(provider);
        checkStrings();
        checkScalarBlock();
        checkScalarRoot();
    }

    private static void checkScans(StringKernels provider) {
        byte[] data = new byte[165];
        int from = 3;
        for (int to = from; to < data.length; to++) {
            Arrays.fill(data, (byte) 1);
            data[from - 1] = 0;
            data[to] = -1;
            require(provider.firstNonAscii(data, from, to) == to, "ASCII scan crossed its boundary");
            data[to] = 0;
            require(provider.firstZeroByte(data, from, to) == to, "Byte scan crossed its boundary");
            require(provider.firstZeroUtf16(data, from, to) == to, "UTF-16 scan crossed its boundary");
            for (int at = from; at < to; at++) {
                data[at] = -1;
                require(provider.firstNonAscii(data, from, to) == at, "ASCII scan lane");
                data[at] = 0;
                require(provider.firstZeroByte(data, from, to) == at, "NUL byte scan lane");
                require(provider.firstZeroUtf16(data, from, to) == to, "One zero byte is not a NUL code unit");
                if (((at - from) & 1) == 0 && at + 1 < to) {
                    data[at + 1] = 0;
                    require(provider.firstZeroUtf16(data, from, to) == at, "NUL code unit scan lane");
                    data[at + 1] = 1;
                }
                data[at] = 1;
            }
        }
    }

    private static void checkStrings() {
        List<String> text = List.of("a", "caf\u00e9", "\u03bb", "\u4e2d", "\ud83d\ude80", "a\u00e9\u4e2d\ud83d\ude80");
        for (int profile = 0; profile < 6; profile++) {
            for (String part : text) {
                for (int length = 0; length <= 130; length++) {
                    String value = part.repeat(length);
                    var writer = new BinaryCursor.Writer(0);
                    writer.writeU8(0xab);
                    writer.writeString(value, profile);
                    writer.writeU8(0x9a);
                    byte[] bytes = writer.toByteArray();
                    var reader = new BinaryCursor.Reader(bytes);
                    reader.skip(1);
                    require(value.equals(reader.readString(profile)), "Unaligned string round trip");
                    require(reader.readU8() == 0x9a, "Reader cursor after string");
                    reader.requireEnd();

                    reader = new BinaryCursor.Reader(bytes);
                    reader.skip(1);
                    reader.skipString(profile);
                    require(reader.readU8() == 0x9a, "Reader cursor after skipping string");
                }
            }
        }
    }

    private static void checkScalarBlock() {
        var writer = new BinaryCursor.Writer();
        writer.writeI32(20);
        writer.writeI32(-3);
        byte[] input = writer.toByteArray();
        var block = new ScalarBlock("int", Integer.BYTES, input);
        require(block.equals(List.of(20, -3)), "Scalar block elements");
        require(block.raw("int", Integer.BYTES) == input, "Scalar block passthrough");
        require(block.raw("float", Integer.BYTES) == null, "Block cannot copy into a different representation");
        block.set(1, 60);
        block.add(0, 23);
        block.remove(1);
        require(block.equals(List.of(23, 60)), "Scalar block remains writable");
        require(block.raw("int", Integer.BYTES) == null, "Modified block must be encoded from its values");
    }

    private static void checkScalarRoot() {
        var codec = LuminPack.fromManifest("""
            {"version":1,"root":"int[]","requireCompleteBinaryInput":true,
             "rootWire":{"kind":"sequence","element":{"kind":"scalar","type":"int"}}}
            """);
        byte[] wire = {2, 0, 0, 0, 1, 0, 0, 0, -1, -1, -1, -1};
        Object decoded = codec.readBinary(wire);
        require(decoded.equals(List.of(1, -1)), "Root scalar sequence framing");
        require(Arrays.equals(codec.writeBinary(decoded), wire), "Root scalar block passthrough");
        byte[] json = codec.writeJson(decoded, LuminPack.UTF8);
        require(codec.readJson(json, LuminPack.UTF8).equals(decoded), "Root sequence JSON");
    }

    private static void require(boolean condition, String description) {
        if (!condition) throw new AssertionError(description);
    }
}
