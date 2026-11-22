package luminpack;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;

/** Checks that Java's application binding follows the exported Custom and Union contracts. */
public final class RegistrationChecks {
    private static final LuminPack.CustomCodec APPLICATION_CODEC = new LuminPack.CustomCodec() {
        @Override public Object readBinary(LuminPack.WireReader input) { return input.readInt32(); }
        @Override public void writeBinary(LuminPack.WireWriter output, Object value) {
            output.writeInt32(((Number) value).intValue());
        }
        @Override public Object readJson(Object value) { return ((Number) value).intValue(); }
        @Override public Object writeJson(Object value) { return value; }
    };

    private RegistrationChecks() {}

    public static void main(String[] args) {
        checkCustomJson();
        checkRegistration();
    }

    private static void checkCustomJson() {
        var version = """
            {"version":1,"root":"Opaque","contracts":[{"id":"Opaque","type":"Opaque","kind":"Custom",
             "profile":{},"customJson":%s}]}
            """;
        var codec = LuminPack.fromManifest(version.formatted(true)).withCustom("Opaque", APPLICATION_CODEC);
        var binaryOnly = LuminPack.fromManifest(version.formatted(false)).withCustom("Opaque", APPLICATION_CODEC);
        byte[] utf16 = "42".getBytes(StandardCharsets.UTF_16LE);
        byte[] utf8 = "42".getBytes(StandardCharsets.UTF_8);
        require(codec.readJson(utf16, LuminPack.UTF16).equals(42), "Automatic UTF-16 Custom reader");
        require(Arrays.equals(utf16, codec.writeJson(42, LuminPack.UTF16)), "Automatic UTF-16 Custom writer");
        require(binaryOnly.readBinary(binaryOnly.writeBinary(42)).equals(42), "Binary-only Custom formatter");
        reject(() -> codec.readJson(utf8, LuminPack.UTF8));
        reject(() -> codec.writeJson(42, LuminPack.UTF8));
        reject(() -> binaryOnly.readJson(utf16, LuminPack.UTF16));
        reject(() -> binaryOnly.writeJson(42, LuminPack.UTF16));
    }

    private static void checkRegistration() {
        var manifest = """
            {"version":1,"root":"Union","contracts":[{"id":"Union","type":"Example.Union","kind":"Union",
             "profile":{},"unionRegistration":true,"unionWideTags":%s,
             "cases":[{"id":"Generated","type":"Generated","hash":23,"tag":5}]},
             {"id":"Generated","type":"Generated","kind":"Record","profile":{}},
             {"id":"Disabled","type":"Disabled","kind":"Union","profile":{}}]}
            """;
        var narrow = LuminPack.fromManifest(manifest.formatted(false));
        var wide = LuminPack.fromManifest(manifest.formatted(true));
        reject(() -> narrow.withUnionCase("Union", 256, APPLICATION_CODEC));
        reject(() -> narrow.withUnionCase("Union", 5, APPLICATION_CODEC));
        reject(() -> narrow.withUnionCase("Union", 23, APPLICATION_CODEC));
        reject(() -> wide.withUnionCase("Union", -1, APPLICATION_CODEC));
        reject(() -> wide.withUnionCase("Union", 65536, APPLICATION_CODEC));
        reject(() -> wide.withUnionCase("Disabled", 10, APPLICATION_CODEC));
        reject(() -> wide.withUnionCase("Generated", 10, APPLICATION_CODEC));
        reject(() -> wide.withUnionCase("Missing", 10, APPLICATION_CODEC));

        var boundNarrow = narrow.withUnionCase("global::Example.Union", 255, APPLICATION_CODEC);
        var boundWide = wide.withUnionCase("Union", 600, APPLICATION_CODEC);
        require(Arrays.equals(boundNarrow.writeBinary(new LuminPack.RegisteredUnionValue(255, 42)),
            new byte[] {-1, -1, -1, -1, -1, 42, 0, 0, 0}), "Narrow registration uses one byte");
        require(Arrays.equals(boundWide.writeBinary(new LuminPack.RegisteredUnionValue(600, 42)),
            new byte[] {-1, -1, -1, -1, (byte) 250, 88, 2, 42, 0, 0, 0}), "Wide registration uses its escape and tag");

        var rebound = boundNarrow.withUnionCase("Union", 255, APPLICATION_CODEC);
        for (int encoding : new int[] {LuminPack.UTF8, LuminPack.UTF16}) {
            var value = new LuminPack.RegisteredUnionValue(255, 42);
            require(rebound.readJson(rebound.writeJson(value, encoding), encoding).equals(value),
                "Registered Union JSON callbacks support either encoding");
        }
    }

    private static void reject(Runnable action) {
        try {
            action.run();
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("Accepted an invalid LuminPack registration or encoding");
    }

    private static void require(boolean condition, String description) {
        if (!condition) throw new AssertionError(description);
    }
}
