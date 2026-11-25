package luminpack;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.SplittableRandom;

/** Exercises the default SWAR kernels and the optional Vector API provider. */
public final class FastPathChecks {
    private FastPathChecks() {}

    public static void main(String[] args) {
        StringKernels provider = StringKernels.load();
        boolean vector = args.length == 1 && args[0].equals("vector");
        require((provider != null) == vector, "Unexpected vector provider state");
        if (provider != null) checkScans(provider);
        checkPositionalRecord();
        checkStrings();
        checkScalarBlock();
        checkScalarRoot();
        checkOrderedJson();
        checkFloatingJson();
    }

    private static void checkPositionalRecord() {
        var fields = new PositionalRecord.Fields(new String[]{"value", "next"});
        var slots = new Object[]{42, null};
        var record = new PositionalRecord(fields, slots);
        require(record.containsKey("next") && !record.containsKey("old"), "Positional record key presence");
        require(PositionalRecord.slots(record, fields) == slots, "Schema-local field slots");
        var view = record.entrySet();
        var first = view.iterator().next();
        require(first.getKey().equals("value") && first.setValue(3).equals(42), "Mutable ordered entry");
        record.put("extra", true);
        record.remove("next");
        require(view.size() == 2 && record.get("value").equals(3), "Live record view after mutation");
        require(PositionalRecord.slots(record, fields) == null, "Updated records use current entries");
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

    private static void checkOrderedJson() {
        var codec = LuminPack.fromManifest("""
            {"version":1,"root":"Shape","contracts":[{"id":"Shape","type":"Shape","kind":"Record",
             "profile":{"jsonDepth":2,"maxCollection":3},"members":[
              {"name":"Id","profile":{},"wire":{"kind":"scalar","type":"int"}},
              {"name":"Name","oldNames":["Previous"],"profile":{},"wire":{"kind":"string"}},
              {"name":"Values","profile":{},"wire":{"kind":"sequence","element":{"kind":"scalar","type":"int"}}}]}]}
            """);
        String canonical = "{\"Id\":7,\"Name\":\"\u00e9\\n\",\"Values\":[-2147483648,0,2147483647]}";
        byte[] expected = canonical.getBytes(StandardCharsets.UTF_8);
        Object model = codec.readJson(expected, LuminPack.UTF8);
        require(Arrays.equals(expected, codec.writeJson(model, LuminPack.UTF8)), "Ordered UTF-8 JSON bytes");
        require(((Map<?, ?>) model).get("Values") instanceof ScalarBlock, "Contiguous JSON Int32 sequence");
        String alias = "{\"Previous\":\"\u00e9\\n\",\"Values\":[-2147483648,-0,2147483647],\"I\\u0064\":7}";
        require(codec.readJson(alias.getBytes(StandardCharsets.UTF_8), LuminPack.UTF8).equals(model), "Escaped and previous names");
        String duplicate = canonical.replace("\"Id\":7", "\"Id\":\"unused\",\"Id\":7");
        require(codec.readJson(duplicate.getBytes(StandardCharsets.UTF_8), LuminPack.UTF8).equals(model), "Duplicate key fallback");
        String replacement = canonical.replace("\u00e9", "\ufffd");
        byte[] replacementBytes = replacement.getBytes(StandardCharsets.UTF_8);
        require(Arrays.equals(replacementBytes, codec.writeJson(codec.readJson(replacementBytes, LuminPack.UTF8), LuminPack.UTF8)),
            "Valid replacement character retains its UTF-8 bytes");
        byte[] malformed = expected.clone();
        for (int i = 0; i < malformed.length; i++) {
            if (malformed[i] != (byte) 0xc3) continue;
            malformed[i + 1] = 'x';
            break;
        }
        try {
            codec.readJson(malformed, LuminPack.UTF8);
            throw new AssertionError("Accepted malformed UTF-8");
        } catch (IllegalArgumentException expectedFailure) {
            // Replacement must never silently repair a malformed JSON document.
        }
        for (String invalid : List.of(canonical.replace("\"Id\":7", "\"Id\":07"), canonical + "true",
                canonical.replace("2147483647]", "2147483648]"), canonical.replace("2147483647]", "2147483647,0]"),
                canonical.substring(0, canonical.length() - 1) + ",\"Unknown\":[[0]]}")) {
            try {
                codec.readJson(invalid.getBytes(StandardCharsets.UTF_8), LuminPack.UTF8);
                throw new AssertionError("Accepted invalid ordered JSON");
            } catch (IllegalArgumentException | ArithmeticException expectedFailure) {
                // The ordered path and its fallback must enforce the same syntax, width and shape limits.
            }
        }
    }

    private static void checkFloatingJson() {
        for (String token : List.of("-0", "-0.00", "0.000001", "9999999", "-321.125", "9.999999", "12.123456",
            "1.000000059604644775390625", "1e-38")) {
            var reader = new Json.Reader(token, 4);
            double shortcut = reader.shortDecimal();
            Number value = Double.isNaN(shortcut) ? (Number) reader.value(0) : Double.valueOf(shortcut);
            require(reader.atEnd() && Double.doubleToRawLongBits(value.doubleValue()) ==
                Double.doubleToRawLongBits(Double.parseDouble(token)), "Double decimal parsing");
            require(Float.floatToRawIntBits(value.floatValue()) == Float.floatToRawIntBits(Float.parseFloat(token)),
                "Float decimal parsing");
        }
        var random = new SplittableRandom(881627);
        for (int index = 0; index < 4096; index++) {
            float single = Float.intBitsToFloat(random.nextInt());
            double wide = Double.longBitsToDouble(random.nextLong());
            if (Float.isFinite(single)) {
                var writer = new JsonUtf8Writer();
                writer.floating(single);
                require(new String(writer.finish(), StandardCharsets.UTF_8).equals(Json.write(single, 1)), "Float JSON digits");
            }
            if (Double.isFinite(wide)) {
                var writer = new JsonUtf8Writer();
                writer.floating(wide);
                require(new String(writer.finish(), StandardCharsets.UTF_8).equals(Json.write(wide, 1)), "Double JSON digits");
            }
        }
    }

    private static void require(boolean condition, String description) {
        if (!condition) throw new AssertionError(description);
    }
}
