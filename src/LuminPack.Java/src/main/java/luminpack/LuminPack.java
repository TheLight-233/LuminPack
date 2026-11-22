package luminpack;

import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;

/** An immutable LuminPack wire contract with document-local operation state. */
public final class LuminPack {
    public static final int UTF8 = 0;
    public static final int UTF16 = 1;

    private final Schema schema;
    private final Bindings bindings;

    private LuminPack(Schema schema, Bindings bindings) {
        this.schema = schema;
        this.bindings = bindings;
    }

    /** Binds the normalized portable manifest emitted by LuminPack's code generator. */
    public static LuminPack fromManifest(String manifest) {
        return new LuminPack(new Schema(manifest), new Bindings(Map.of(), Map.of()));
    }

    /** Binds an application's Custom formatter without mutating this contract. */
    public LuminPack withCustom(String type, CustomCodec codec) {
        var custom = new HashMap<>(bindings.custom());
        custom.put(Schema.normalize(type), Objects.requireNonNull(codec));
        return new LuminPack(schema, new Bindings(Map.copyOf(custom), bindings.registered()));
    }

    /** Registers a formatter for an application Union case. */
    public LuminPack withUnionCase(String unionType, int tag, CustomCodec codec) {
        Schema.Contract type = schema.contractsByIdentity.get(Schema.normalize(unionType));
        if (type == null || !type.kind.equals("Union") || !type.unionRegistration)
            throw Schema.fail("Unknown registrable LuminPack Union " + unionType);
        type.checkRegistrationTag(tag);
        var registered = new HashMap<>(bindings.registered());
        registered.put(new Registration(type.id, tag), Objects.requireNonNull(codec));
        return new LuminPack(schema, new Bindings(bindings.custom(), Map.copyOf(registered)));
    }

    /** Reads a Binary document using the projected root profile and boundary policy. */
    public Object readBinary(byte[] input) { return readBinary(input, schema.binaryProfile); }

    /** Reads a Binary document using an explicit built-in string profile where the contract permits it. */
    public Object readBinary(byte[] input, int profile) {
        checkBinaryProfile(profile);
        checkInput(input);
        try {
            return new BinaryDocument(schema, bindings).read(input, profile);
        } catch (StackOverflowError exhausted) {
            throw Schema.fail("LuminPack Binary depth limit exceeded");
        }
    }

    /** Writes a Binary document using the projected root profile. */
    public byte[] writeBinary(Object value) { return writeBinary(value, schema.binaryProfile); }

    /** Writes a Binary document using an explicit built-in string profile where permitted. */
    public byte[] writeBinary(Object value, int profile) {
        checkBinaryProfile(profile);
        try {
            return new BinaryDocument(schema, bindings).write(value, profile);
        } catch (StackOverflowError exhausted) {
            throw Schema.fail("LuminPack Binary depth limit exceeded");
        }
    }

    /** Reads a JSON document using the projected root encoding. */
    public Object readJson(byte[] input) { return readJson(input, schema.rootProfile.json()); }

    /** Reads a JSON document in UTF-8 or little-endian UTF-16. */
    public Object readJson(byte[] input, int encoding) {
        checkInput(input);
        var charset = charset(encoding);
        try {
            boolean direct = useDirectJson(encoding);
            String text = direct ? new String(input, StandardCharsets.UTF_8) : null;
            // HotSpot can decode an ordinary UTF-8 array directly. Its replacement character also
            // identifies when an exact decoder must distinguish malformed input from a valid U+FFFD.
            if (text == null || text.indexOf('\ufffd') >= 0)
                text = charset.newDecoder().onMalformedInput(CodingErrorAction.REPORT)
                    .onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(input)).toString();
            if (direct) return new JsonDirect(schema, bindings).read(text);
            return new JsonDocument(schema, bindings, encoding).read(text);
        } catch (CharacterCodingException error) {
            throw new IllegalArgumentException("Invalid LuminPack JSON text encoding.", error);
        } catch (StackOverflowError exhausted) {
            throw Schema.fail("LuminPack JSON depth limit exceeded");
        }
    }

    /** Writes a JSON document using the projected root encoding. */
    public byte[] writeJson(Object value) { return writeJson(value, schema.rootProfile.json()); }

    /** Writes a JSON document in UTF-8 or little-endian UTF-16. */
    public byte[] writeJson(Object value, int encoding) {
        try {
            if (useDirectJson(encoding)) return new JsonDirect(schema, bindings).write(value);
            return new JsonDocument(schema, bindings, encoding).write(value).getBytes(charset(encoding));
        } catch (StackOverflowError exhausted) {
            throw Schema.fail("LuminPack JSON depth limit exceeded");
        }
    }

    private boolean useDirectJson(int encoding) {
        return encoding == UTF8 && schema.directJson && bindings.custom().isEmpty() && bindings.registered().isEmpty();
    }

    private void checkInput(byte[] input) {
        if (input.length > schema.rootProfile.input()) throw Schema.fail("LuminPack input exceeds the configured size limit");
    }

    private static void checkBinaryProfile(int profile) {
        if (profile < 0 || profile > 5) throw Schema.fail("Unknown LuminPack Binary profile");
    }

    private static java.nio.charset.Charset charset(int encoding) {
        return switch (encoding) {
            case UTF8 -> StandardCharsets.UTF_8;
            case UTF16 -> StandardCharsets.UTF_16LE;
            default -> throw Schema.fail("Unknown LuminPack JSON encoding");
        };
    }

    /** A generated Union case selected by its unsigned 32-bit name hash. */
    public record UnionValue(long hash, Object value) {}

    /** An application Union case selected by its registered tag. */
    public record RegisteredUnionValue(int tag, Object value) {}

    /** Application-defined Binary and JSON payload formats. */
    public interface CustomCodec {
        Object readBinary(WireReader input);
        void writeBinary(WireWriter output, Object value);
        Object readJson(Object value);
        Object writeJson(Object value);
    }

    /** A checked Binary cursor supplied to a Custom codec. */
    public static final class WireReader {
        private final BinaryCursor.Reader input;
        private final int profile;

        WireReader(BinaryCursor.Reader input, int profile) { this.input = input; this.profile = profile; }

        public int remaining() { return input.remaining(); }
        public int readUInt8() { return input.readU8(); }
        public int readUInt16() { return input.readU16(); }
        public int readInt32() { return input.readI32(); }
        public long readInt64() { return input.readI64(); }
        public String readString() { return input.readString(profile); }
        public byte[] readBytes(int size) { return input.take(size); }
    }

    /** A Binary cursor supplied to a Custom codec. */
    public static final class WireWriter {
        private final BinaryCursor.Writer output;
        private final int profile;

        WireWriter(BinaryCursor.Writer output, int profile) { this.output = output; this.profile = profile; }

        public void writeUInt8(int value) { output.writeU8(value); }
        public void writeUInt16(int value) { output.writeU16(value); }
        public void writeInt32(int value) { output.writeI32(value); }
        public void writeInt64(long value) { output.writeI64(value); }
        public void writeString(String value) { output.writeString(value, profile); }
        public void writeBytes(byte[] value) { output.writeBytes(value); }
    }

    record Registration(String union, int tag) {}
    record Bindings(Map<String, CustomCodec> custom, Map<Registration, CustomCodec> registered) {
        CustomCodec custom(Schema.Contract type) {
            CustomCodec codec = custom.get(type.id);
            if (codec == null) throw Schema.fail("Missing LuminPack Custom binding " + type.id);
            return codec;
        }

        CustomCodec registered(Schema.Contract type, int tag) {
            CustomCodec codec = registered.get(new Registration(type.id, tag));
            if (codec == null) throw Schema.fail("Unknown registered LuminPack Union tag " + tag + " in " + type.id);
            return codec;
        }
    }
}
