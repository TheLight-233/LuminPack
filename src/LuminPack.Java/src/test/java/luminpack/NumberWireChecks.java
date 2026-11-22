package luminpack;

import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Map;

/** Checks typed JSON numeric bytes in both encodings and accepts a managed raw-bit TSV oracle. */
public final class NumberWireChecks {
    private static final LuminPack FLOAT = scalar("float");
    private static final LuminPack DOUBLE = scalar("double");

    private NumberWireChecks() {}

    public static void main(String[] args) throws Exception {
        check(scalar("int"), Integer.MIN_VALUE, "-2147483648");
        check(scalar("int"), 9999, "9999");
        check(scalar("int"), 10000, "10000");
        check(scalar("uint"), 0xffff_ffffL, "4294967295");
        check(scalar("long"), Long.MIN_VALUE, "-9223372036854775808");
        check(scalar("ulong"), new BigInteger("18446744073709551615"), "18446744073709551615");
        check(FLOAT, -0.0f, "-0");
        check(DOUBLE, -0.0, "-0");
        check(FLOAT, Float.MIN_VALUE, "1E-45");
        check(DOUBLE, Double.MIN_VALUE, "5E-324");
        check(FLOAT, 1.0e9f, "1000000000");
        check(FLOAT, 1.0e10f, "1E+10");
        check(DOUBLE, 1.0e-5, "0.00001");
        check(DOUBLE, 1.0e-20, "1E-20");
        check(DOUBLE, 3.0e100, "3E+100");
        for (int encoding : new int[] {LuminPack.UTF8, LuminPack.UTF16}) {
            reject(() -> FLOAT.writeJson(Float.NaN, encoding));
            reject(() -> DOUBLE.writeJson(Double.POSITIVE_INFINITY, encoding));
        }
        checkOpaque();
        checkTypedFallback();
        if (args.length == 1) verifyOracle(Path.of(args[0]));
    }

    private static void checkTypedFallback() {
        var tracked = LuminPack.fromManifest("""
            {"version":1,"root":"Tracked","contracts":[{"id":"Tracked","type":"Tracked","kind":"Record",
             "profile":{"trackReferences":true},"members":[{"name":"Value","profile":{},
             "wire":{"kind":"scalar","type":"float"}}]}]}
            """);
        String text = "{\"$id\":0,\"Value\":1E-45}";
        var value = Map.of("Value", Float.MIN_VALUE);
        require(Arrays.equals(text.getBytes(StandardCharsets.UTF_8), tracked.writeJson(value, LuminPack.UTF8)),
            "Typed UTF-8 fallback fields use the managed numeric formatter");
        require(Arrays.equals(text.getBytes(StandardCharsets.UTF_16LE), tracked.writeJson(value, LuminPack.UTF16)),
            "Typed UTF-16LE fallback fields use the managed numeric formatter");
    }

    private static void checkOpaque() {
        var binding = new LuminPack.CustomCodec() {
            @Override public Object readBinary(LuminPack.WireReader input) { throw new UnsupportedOperationException(); }
            @Override public void writeBinary(LuminPack.WireWriter output, Object value) { throw new UnsupportedOperationException(); }
            @Override public Object readJson(Object value) { return value; }
            @Override public Object writeJson(Object value) { return value; }
        };
        var custom = LuminPack.fromManifest("""
            {"version":1,"root":"Custom","contracts":[{"id":"Custom","type":"Custom","kind":"Custom",
             "profile":{},"customJson":true}]}
            """).withCustom("Custom", binding);
        var union = LuminPack.fromManifest("""
            {"version":1,"root":"Union","contracts":[{"id":"Union","type":"Union","kind":"Union",
             "profile":{},"unionRegistration":true}]}
            """).withUnionCase("Union", 7, binding);
        require(Arrays.equals("1.4E-45".getBytes(StandardCharsets.UTF_16LE), custom.writeJson(Float.MIN_VALUE, LuminPack.UTF16)),
            "Opaque Custom callback values retain their lexical policy");
        var value = new LuminPack.RegisteredUnionValue(7, Float.MIN_VALUE);
        require(Arrays.equals("{\"$type\":7,\"$value\":1.4E-45}".getBytes(StandardCharsets.UTF_8), union.writeJson(value, LuminPack.UTF8)),
            "Opaque registered Union callback values retain their lexical policy");
    }

    private static void verifyOracle(Path path) throws Exception {
        int count = 0;
        try (var lines = Files.newBufferedReader(path)) {
            String row = lines.readLine();
            require("type\tbits\tjson".equals(row), "Invalid managed numeric TSV header");
            while ((row = lines.readLine()) != null) {
                String[] fields = row.split("\t", 3);
                require(fields.length == 3, "Invalid managed numeric TSV row");
                String kind = fields[0];
                try {
                    if (kind.equals("f32")) check(FLOAT, Float.intBitsToFloat(Integer.parseUnsignedInt(fields[1], 16)), fields[2]);
                    else if (kind.equals("f64")) check(DOUBLE, Double.longBitsToDouble(Long.parseUnsignedLong(fields[1], 16)), fields[2]);
                    else throw new IllegalArgumentException("Unexpected managed number type " + kind);
                } catch (AssertionError error) {
                    throw new AssertionError(kind + " " + fields[1] + ": " + error.getMessage(), error);
                }
                count++;
            }
        }
        System.out.println("Matched " + count + " managed numeric tokens in UTF-8 and UTF-16LE.");
    }

    private static LuminPack scalar(String type) {
        return LuminPack.fromManifest("""
            {"version":1,"root":"%s","rootWire":{"kind":"scalar","type":"%s"}}
            """.formatted(type, type));
    }

    private static void check(LuminPack codec, Number input, String expected) {
        byte[] utf8 = codec.writeJson(input, LuminPack.UTF8);
        require(Arrays.equals(utf8, expected.getBytes(StandardCharsets.UTF_8)),
            "Expected " + expected + ", received " + new String(utf8, StandardCharsets.UTF_8));
        byte[] utf16 = codec.writeJson(input, LuminPack.UTF16);
        require(Arrays.equals(utf16, expected.getBytes(StandardCharsets.UTF_16LE)), "Unexpected UTF-16LE digits for " + expected);
    }

    private static void reject(Runnable action) {
        try {
            action.run();
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("Accepted a non-finite JSON value");
    }

    private static void require(boolean condition, String description) {
        if (!condition) throw new AssertionError(description);
    }
}
