import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.Map;
import luminpack.LuminPack;

/** Exchanges a single compiled contract between two encodings. */
public final class PortableRoundTrip {
    private PortableRoundTrip() {}

    public static void main(String[] args) throws Exception {
        if (args.length < 5 || args.length > 6) {
            throw new IllegalArgumentException("Expected schema, input format, output format, input path, output path and optional fixture binding.");
        }
        LuminPack codec = LuminPack.fromManifest(Files.readString(Path.of(args[0])));
        String fixture = args.length == 6 ? args[5] : "";
        boolean edit = fixture.startsWith("edit-");
        if (!fixture.isEmpty() && !edit) codec = bind(codec, fixture);
        byte[] input = Files.readAllBytes(Path.of(args[3]));
        Object model = switch (args[1]) {
            case "binary" -> codec.readBinary(input);
            case "json-utf8" -> codec.readJson(input, LuminPack.UTF8);
            case "json-utf16" -> codec.readJson(input, LuminPack.UTF16);
            default -> throw new IllegalArgumentException("Invalid input format: " + args[1]);
        };
        if (edit) editLayout(model, fixture);
        byte[] output = switch (args[2]) {
            case "binary" -> codec.writeBinary(model);
            case "json-utf8" -> codec.writeJson(model, LuminPack.UTF8);
            case "json-utf16" -> codec.writeJson(model, LuminPack.UTF16);
            default -> throw new IllegalArgumentException("Invalid output format: " + args[2]);
        };
        Files.write(Path.of(args[4]), output);
    }

    @SuppressWarnings("unchecked")
    private static void editLayout(Object model, String fixture) {
        var record = (Map<String, Object>) model;
        switch (fixture) {
            case "edit-packed" -> {
                var nested = (Map<String, Object>) ((List<?>) record.get("Nested")).get(0);
                ((Map<String, Object>) nested.get("Data")).put("End", (short) 1234);
            }
            case "edit-tagged" -> ((Map<String, Object>) ((List<?>) record.get("Values")).get(0)).put("Code", 0x01234567);
            case "edit-native" -> ((Map<String, Object>) ((List<?>) record.get("Items")).get(0)).put("Code", 0x01234567);
            default -> throw new IllegalArgumentException("Unknown layout edit: " + fixture);
        }
    }

    private static LuminPack bind(LuminPack codec, String fixture) {
        return switch (fixture) {
            case "graph-external" -> codec.withUnionCase("LuminPackCppReferenceFixtures.IGraphChoice", 600,
                    new XorFormatter(4, 59, 71));
            case "large-graph-external" -> codec.withUnionCase("LuminPackCppVirtualUnionFixtures.LargeGraph", 612,
                    new XorFormatter(4, 23, 73));
            case "registered-narrow" -> codec.withUnionCase("LuminPackCppRegisteredUnionFixtures.INarrow", 100,
                    new XorFormatter(4, 85, 137));
            case "registered-wide" -> codec.withUnionCase("LuminPackCppRegisteredUnionFixtures.IWide", 600,
                    new XorFormatter(4, 85, 137));
            case "custom-shifted" -> codec.withCustom("LuminPackCppCustomFixtures.Shifted", new XorFormatter(8, 85, 137));
            case "custom-binary-only" -> codec.withCustom("LuminPackCppCustomFixtures.BinaryOnly",
                    new XorFormatter(1, 85, 0));
            default -> throw new IllegalArgumentException("Unknown fixture binding: " + fixture);
        };
    }

    // Mirrors the managed fixture's application-owned payload, after any runtime Union framing.
    private record XorFormatter(int width, int binaryMask, int jsonMask) implements LuminPack.CustomCodec {
        @Override public Object readBinary(LuminPack.WireReader input) {
            long payload = switch (width) {
                case 1 -> input.readUInt8();
                case 4 -> input.readInt32();
                case 8 -> input.readInt64();
                default -> throw new IllegalStateException();
            };
            return decode(payload, binaryMask);
        }

        @Override public void writeBinary(LuminPack.WireWriter output, Object value) {
            long payload = encode(value, binaryMask);
            switch (width) {
                case 1 -> output.writeUInt8(Math.toIntExact(payload));
                case 4 -> output.writeInt32(Math.toIntExact(payload));
                case 8 -> output.writeInt64(payload);
                default -> throw new IllegalStateException();
            }
        }

        @Override public Object readJson(Object value) {
            if (width == 1) throw new UnsupportedOperationException("Binary-only fixture.");
            return decode(((Number) value).longValue(), jsonMask);
        }

        @Override public Object writeJson(Object value) {
            if (width == 1) throw new UnsupportedOperationException("Binary-only fixture.");
            return encode(value, jsonMask);
        }

        private Object decode(long payload, int mask) {
            return width == 8 && payload == Long.MIN_VALUE ? null : Math.toIntExact(payload) ^ mask;
        }

        private long encode(Object value, int mask) {
            return width == 8 && value == null ? Long.MIN_VALUE : ((Number) value).intValue() ^ mask;
        }
    }
}
