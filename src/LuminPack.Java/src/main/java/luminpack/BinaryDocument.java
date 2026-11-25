package luminpack;

import static luminpack.Schema.Wire.*;
import static luminpack.Schema.fail;
import static luminpack.Schema.object;

import java.math.BigInteger;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;
import java.util.IdentityHashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Binary cursor, Version schemas and identity state owned by one operation. */
final class BinaryDocument {
    private static final int NULL = 255;
    private static final int REFERENCE = 250;
    private static final int VERSION = 251;
    private static final long MAX_UINT32 = 0xffff_ffffL;

    private final Schema schema;
    private final LuminPack.Bindings bindings;
    private final ArrayList<long[]> versions = new ArrayList<>();
    private final HashMap<Long, Reference> references = new HashMap<>();
    private final IdentityHashMap<Object, Reference> written = new IdentityHashMap<>();
    private final int depthLimit;

    private record Reference(Schema.Contract type, Object value, long id) {}

    BinaryDocument(Schema schema, LuminPack.Bindings bindings) {
        this.schema = schema;
        this.bindings = bindings;
        depthLimit = Math.min(schema.rootProfile.depth(), 512);
    }

    Map<?, ?> valueLayout(String type) { return schema.valueLayouts.get(Schema.normalize(type)); }

    int nativeScalarAlignment() { return schema.nativeScalarAlignment; }

    Object read(byte[] input, int profile) {
        var reader = new BinaryCursor.Reader(input, schema.rootProfile.collection(), schema.rootProfile.string());
        Object result = schema.rootWire == null ? readContract(reader, schema.root, profile, 0, false) :
            readWire(reader, schema.rootWire, profile, 0);
        if (schema.completeBinaryInput) reader.requireEnd();
        return result;
    }

    byte[] write(Object value, int profile) {
        var output = new BinaryCursor.Writer();
        if (schema.rootWire == null) writeContract(output, schema.root, value, profile, 0, false);
        else writeWire(output, schema.rootWire, value, profile, 0);
        return output.toByteArray();
    }

    private Object readContract(BinaryCursor.Reader input, Schema.Contract type, int inherited, int depth, boolean caseBody) {
        int profile = type.profile.binary(inherited);
        if (type.sequenceWire != null) {
            checkDepth(depth, depthLimit);
            return readWire(input, type.sequenceWire, profile, depth + 1);
        }
        if (type.kind.equals("Union")) return readUnion(input, type, profile, depth);
        if (type.kind.equals("Custom")) return bindings.custom(type).readBinary(new LuminPack.WireReader(input, profile));
        boolean markerPresent = type.versioned || (!isInlineStruct(type) && (!caseBody || type.profile.track()));
        if (!type.profile.track() && markerPresent && input.peekU8() == NULL) { input.skip(1); return null; }
        if (type.kind.equals("Struct") && !type.profile.track()) {
            checkDepth(depth, depthLimit);
            Map<String, Object> optimized = Layout.readStruct(this, input, type);
            if (optimized != null) return optimized;
        }
        Map<String, Object> result;
        if (type.profile.track()) {
            int marker = input.readU8();
            if (marker == NULL) return null;
            if (marker != REFERENCE && marker > 249) throw fail("Invalid LuminPack reference marker");
            long id = readReferenceId(input);
            if (marker == REFERENCE) {
                Reference entry = references.get(id);
                if (entry == null || entry.type != type) throw fail("Invalid LuminPack reference ID or type");
                return entry.value;
            }
            result = new LinkedHashMap<>(type.members.size());
            if ((!type.sequenceBase && input.peekU8() == NULL) || references.putIfAbsent(id, new Reference(type, result, id)) != null)
                throw fail("Invalid LuminPack reference definition");
        } else {
            result = type.versioned || type.kind.equals("Struct") ? new LinkedHashMap<>(type.members.size()) : null;
        }

        checkDepth(depth, depthLimit);
        if (type.versioned) {
            return readVersion(input, type, result, profile, depth + 1) ? result : null;
        }
        if (markerPresent && !type.sequenceBase) {
            int count = input.readU8();
            if (count == NULL) return null;
            if (count != type.binaryMemberCount) throw fail("Unexpected LuminPack member count in " + type.id);
        }
        if (type.kind.equals("Struct") && Layout.readStruct(this, input, type, result, profile, depth + 1, false)) return result;
        if (result == null) {
            Schema.Field[] fields = type.jsonFields;
            Object[] slots = new Object[fields.length];
            for (int i = 0; i < fields.length; i++) slots[i] = readField(input, fields[i], profile, depth + 1, 0);
            return new PositionalRecord(type.positions, slots);
        }
        readMembers(input, type, result, profile, depth + 1);
        return result;
    }

    private void writeContract(BinaryCursor.Writer output, Schema.Contract type, Object value, int inherited, int depth, boolean caseBody) {
        int profile = type.profile.binary(inherited);
        if (type.sequenceWire != null) {
            checkDepth(depth, depthLimit);
            writeWire(output, type.sequenceWire, value, profile, depth + 1);
            return;
        }
        if (type.kind.equals("Union")) {
            writeUnion(output, type, value, profile, depth);
            return;
        }
        if (type.kind.equals("Custom")) {
            bindings.custom(type).writeBinary(new LuminPack.WireWriter(output, profile), value);
            return;
        }
        if (type.profile.track()) {
            if (value == null) { output.writeU8(NULL); return; }
            Reference known = written.get(value);
            if (known != null) {
                if (known.type != type) throw fail("A LuminPack reference has more than one type");
                output.writeU8(REFERENCE);
                writeReferenceId(output, known.id);
                return;
            }
            long id = written.size();
            written.put(value, new Reference(type, value, id));
            output.writeU8(type.binaryMemberCount);
            writeReferenceId(output, id);
        }
        if (value == null) {
            if (isInlineStruct(type) || caseBody) throw fail("Missing LuminPack value for " + type.id);
            output.writeU8(NULL);
            return;
        }
        checkDepth(depth, 512);
        Map<?, ?> map = object(value);
        if (type.versioned) {
            writeVersion(output, type, map, profile, depth + 1);
            return;
        }
        if (type.kind.equals("Struct") && Layout.writeStruct(this, output, type, map, profile, depth + 1, false)) return;
        if (!type.sequenceBase && !isInlineStruct(type) && (!caseBody || type.profile.track())) output.writeU8(type.binaryMemberCount);
        writeMembers(output, type, map, profile, depth + 1);
    }

    private Object readUnion(BinaryCursor.Reader input, Schema.Contract union, int profile, int depth) {
        long hash = Integer.toUnsignedLong(input.readI32());
        if (hash == 0) return null;
        if (hash == MAX_UINT32) {
            if (!union.unionRegistration) throw fail("Union registration is disabled");
            int tag = input.readU8();
            if (union.wideTags) {
                if (tag == 250) tag = input.readU16();
                else if (tag > 250) tag = 0;
            }
            return new LuminPack.RegisteredUnionValue(tag,
                bindings.registered(union, tag).readBinary(new LuminPack.WireReader(input, profile)));
        }
        Schema.UnionCase item = union.cases.get(hash);
        if (item == null) throw fail("Unknown LuminPack Union case " + hash + " in " + union.id);
        Object value = readContract(input, item.contract(), profile, depth, true);
        return value == null ? null : new LuminPack.UnionValue(hash, value);
    }

    private void writeUnion(BinaryCursor.Writer output, Schema.Contract union, Object value, int profile, int depth) {
        if (value == null) { output.writeI32(0); return; }
        if (value instanceof LuminPack.RegisteredUnionValue item) {
            if (!union.unionRegistration) throw fail("Union registration is disabled");
            int tag = item.tag();
            if (tag < 0 || tag > union.maximumRegisteredTag()) throw fail("Registered Union tag is out of range");
            output.writeI32(-1);
            if (union.wideTags && tag >= 250) { output.writeU8(250); output.writeU16(tag); }
            else output.writeU8(tag);
            bindings.registered(union, tag).writeBinary(new LuminPack.WireWriter(output, profile), item.value());
            return;
        }
        if (!(value instanceof LuminPack.UnionValue item)) throw fail("Expected a LuminPack Union value");
        Schema.UnionCase active = union.cases.get(item.hash());
        if (active == null) throw fail("Unknown LuminPack Union case " + item.hash());
        output.writeI32((int) item.hash());
        writeContract(output, active.contract(), item.value(), profile, depth, true);
    }

    void readMembers(BinaryCursor.Reader input, Schema.Contract type, Map<String, Object> value, int profile, int depth) {
        for (Schema.Field field : type.members) value.put(field.name, readField(input, field, profile, depth, 0));
    }

    void writeMembers(BinaryCursor.Writer output, Schema.Contract type, Map<?, ?> value, int profile, int depth) {
        Object[] slots = PositionalRecord.slots(value, type.positions);
        Schema.Field[] fields = type.jsonFields;
        for (int i = 0; i < fields.length; i++) {
            Schema.Field field = fields[i];
            writeField(output, field, slots == null ? value.get(field.name) : slots[i], profile, depth, 0);
        }
    }

    Object readField(BinaryCursor.Reader input, Schema.Field field, int profile, int depth, int nullableWidth) {
        profile = field.profile.binary(profile);
        if (field.compressed) return readCompressed(input, field.wire, profile, depth);
        if (nullableWidth > 0 && field.wire.kind == NULLABLE) {
            int marker = input.readU8();
            if (marker == NULL) { input.skip(nullableWidth - 1); return null; }
            if (marker != 0) throw fail("Invalid LuminPack nullable marker");
            return readWire(input, field.wire.element, profile, depth);
        }
        return readWire(input, field.wire, profile, depth);
    }

    void writeField(BinaryCursor.Writer output, Schema.Field field, Object value, int profile, int depth, int nullableWidth) {
        profile = field.profile.binary(profile);
        if (field.compressed) { writeCompressed(output, field.wire, value, profile, depth); return; }
        if (nullableWidth > 0 && field.wire.kind == NULLABLE && value == null) {
            output.writeU8(NULL);
            for (int index = 1; index < nullableWidth; index++) output.writeU8(0);
            return;
        }
        writeWire(output, field.wire, value, profile, depth);
    }

    Object readWire(BinaryCursor.Reader input, Schema.Wire wire, int profile, int depth) {
        return switch (wire.kind) {
            case SCALAR -> readScalar(input, wire.type);
            case STRING -> input.readString(profile);
            case CONTRACT -> readContract(input, wire.contract, profile, depth, false);
            case SEQUENCE -> readSequence(input, wire, profile, depth);
            case DICTIONARY -> readDictionary(input, wire, profile, depth);
            case PAIR, TUPLE -> Layout.readAggregate(this, input, wire, profile, depth);
            case RECTANGULAR -> readRectangular(input, wire, profile, depth);
            case NULLABLE -> {
                int marker = input.readU8();
                if (marker != 0 && marker != NULL) throw fail("Invalid LuminPack nullable marker");
                yield marker == NULL ? null : readWire(input, wire.element, profile, depth);
            }
            default -> throw fail("Unsupported Binary wire descriptor");
        };
    }

    void writeWire(BinaryCursor.Writer output, Schema.Wire wire, Object value, int profile, int depth) {
        switch (wire.kind) {
            case SCALAR -> writeScalar(output, wire.type, value);
            case STRING -> output.writeString(value == null ? null : Schema.string(value), profile);
            case CONTRACT -> writeContract(output, wire.contract, value, profile, depth, false);
            case SEQUENCE -> writeSequence(output, wire.element, value, profile, depth, value == null && wire.nullSequenceIsEmpty(), wire.segmented());
            case DICTIONARY -> writeDictionary(output, wire, value, profile, depth);
            case PAIR, TUPLE -> Layout.writeAggregate(this, output, wire, value, profile, depth);
            case RECTANGULAR -> writeRectangular(output, wire, value, profile, depth);
            case NULLABLE -> {
                output.writeU8(value == null ? NULL : 0);
                if (value != null) writeWire(output, wire.element, value, profile, depth);
            }
            default -> throw fail("Unsupported Binary wire descriptor");
        }
    }

    private Object readSequence(BinaryCursor.Reader input, Schema.Wire wire, int profile, int depth) {
        int count = input.readCollectionCount(minimum(wire.element));
        if (count < 0) return wire.nullSequenceIsEmpty() ? List.of() : null;
        return readElements(input, wire.element, count, profile, depth, wire.segmented());
    }

    private List<Object> readElements(BinaryCursor.Reader input, Schema.Wire element, int count, int profile, int depth,
                                      boolean segmented) {
        if (ScalarBlock.supported(element)) {
            int width = scalarWidth(element.type);
            return new ScalarBlock(element.type, width, input.take(count * width));
        }
        List<Object> storage = Layout.readCollection(this, input, element, count, segmented);
        if (storage != null) return storage;
        var values = new ArrayList<Object>(Math.min(count, 256));
        for (int index = 0; index < count; index++) values.add(readWire(input, element, profile, depth));
        return values;
    }

    private void writeSequence(BinaryCursor.Writer output, Schema.Wire element, Object value, int profile, int depth, boolean memory,
                               boolean segmented) {
        if (value == null) { output.writeI32(memory ? 0 : -1); return; }
        List<?> values = Schema.array(value);
        output.writeI32(values.size());
        if (values instanceof ScalarBlock block && ScalarBlock.supported(element)) {
            byte[] raw = block.raw(element.type, scalarWidth(element.type));
            if (raw != null) { output.writeBytes(raw); return; }
        }
        if (Layout.writeCollection(this, output, element, values, profile, depth, segmented)) return;
        for (Object item : values) writeWire(output, element, item, profile, depth);
    }

    private Object readDictionary(BinaryCursor.Reader input, Schema.Wire wire, int profile, int depth) {
        int count = input.readCollectionCount(minimum(wire.key) + minimum(wire.value));
        if (count < 0) return null;
        var values = new LinkedHashMap<Object, Object>(Math.min(count, 256));
        var keys = new HashSet<Object>();
        for (int index = 0; index < count; index++) {
            Object key = readWire(input, wire.key, profile, depth);
            if (key == null || !keys.add(normalizeKey(key))) throw fail("Null or duplicate LuminPack dictionary key");
            values.put(key, Layout.readDictionaryValue(this, input, wire.value, profile, depth));
        }
        return values;
    }

    private void writeDictionary(BinaryCursor.Writer output, Schema.Wire wire, Object value, int profile, int depth) {
        if (value == null) { output.writeI32(-1); return; }
        Map<?, ?> map = object(value);
        output.writeI32(map.size());
        var keys = new HashSet<Object>();
        for (var entry : map.entrySet()) {
            Object key = entry.getKey();
            if (key == null || !keys.add(normalizeKey(key))) throw fail("Null or duplicate LuminPack dictionary key");
            writeWire(output, wire.key, key, profile, depth);
            Layout.writeDictionaryValue(this, output, wire.value, entry.getValue(), profile, depth);
        }
    }

    static Object normalizeKey(Object key) {
        if (key instanceof Double item && item == 0) return 0.0;
        if (key instanceof Float item && item == 0) return 0.0f;
        return key;
    }

    private Object readRectangular(BinaryCursor.Reader input, Schema.Wire wire, int profile, int depth) {
        int marker = input.readU8();
        if (marker == NULL) return null;
        if (marker > 249 || wire.rank < 2) throw fail("Invalid LuminPack rectangular marker or rank");
        var lengths = new ArrayList<Integer>(wire.rank);
        var bounds = new ArrayList<Integer>(wire.rank);
        for (int index = 0; index < wire.rank; index++) { lengths.add(input.readI32()); bounds.add(0); }
        int expected = rectangularCount(lengths);
        // Read and validate the shape before allocating or decoding any element slots.
        int count = input.readCollectionCount(minimum(wire.element));
        if (count != expected) throw fail("LuminPack rectangular dimensions and element count differ");
        List<Object> values = readElements(input, wire.element, count, profile, depth, false);
        return rectangle(lengths, bounds, values);
    }

    private void writeRectangular(BinaryCursor.Writer output, Schema.Wire wire, Object value, int profile, int depth) {
        if (value == null) { output.writeU8(NULL); return; }
        Map<?, ?> map = object(value);
        List<?> lengths = Schema.array(map.get("Lengths"));
        List<?> bounds = Schema.array(map.get("LowerBounds"));
        List<?> values = Schema.array(map.get("Values"));
        if (lengths.size() != wire.rank || bounds.size() != wire.rank || rectangularCount(lengths) != values.size())
            throw fail("Invalid LuminPack rectangular array shape");
        for (Object bound : bounds)
            if (Schema.longValue(bound) != 0) throw fail("LuminPack Binary rectangular arrays require zero lower bounds");
        output.writeU8(0);
        for (Object length : lengths) output.writeI32(Math.toIntExact(Schema.longValue(length)));
        writeSequence(output, wire.element, values, profile, depth, false, false);
    }

    static int rectangularCount(List<?> lengths) {
        int total = 1;
        for (Object value : lengths) {
            int length = Math.toIntExact(Schema.longValue(value));
            if (length < 0 || (length != 0 && total > Integer.MAX_VALUE / length))
                throw fail("Invalid LuminPack rectangular dimension");
            total *= length;
        }
        return total;
    }

    static Map<String, Object> rectangle(List<?> lengths, List<?> bounds, List<?> values) {
        var result = new LinkedHashMap<String, Object>(3);
        result.put("Lengths", lengths);
        result.put("LowerBounds", bounds);
        result.put("Values", values);
        return result;
    }

    private static boolean isInlineStruct(Schema.Contract type) {
        Object storage = type.metadata.get("storageKind");
        return type.kind.equals("Struct") && !"Managed".equals(storage) && !"Object".equals(storage);
    }

    private static void checkDepth(int depth, int limit) {
        if (depth >= limit) throw fail("LuminPack Binary depth limit exceeded");
    }

    int minimum(Schema.Wire wire) {
        return switch (wire.kind) {
            case SCALAR -> scalarWidth(wire.type);
            case SEQUENCE, DICTIONARY -> Integer.BYTES;
            case CONTRACT -> wire.contract.kind.equals("Union") ? Integer.BYTES : 1;
            default -> 1;
        };
    }

    int scalarWidth(String type) {
        return switch (type) {
            case "byte", "sbyte", "bool" -> 1;
            case "short", "ushort", "char" -> 2;
            case "int", "uint", "float" -> 4;
            case "long", "ulong", "double" -> 8;
            case "nint", "nuint" -> schema.nativeBytes;
            default -> throw fail("Unsupported LuminPack scalar " + type);
        };
    }

    Object readScalar(BinaryCursor.Reader input, String type) {
        return readScalar(input, type, schema.nativeBytes);
    }

    static Object readScalar(BinaryCursor.Reader input, String type, int nativeWidth) {
        return switch (type) {
            case "bool" -> {
                int marker = input.readU8();
                if (marker > 1) throw fail("Invalid LuminPack Boolean marker");
                yield marker != 0;
            }
            case "byte" -> input.readU8();
            case "sbyte" -> (int) (byte) input.readU8();
            case "short" -> (int) (short) input.readU16();
            case "ushort" -> input.readU16();
            case "char" -> (char) input.readU16();
            case "int" -> input.readI32();
            case "uint" -> Integer.toUnsignedLong(input.readI32());
            case "long" -> input.readI64();
            case "ulong" -> unsigned64(input.readI64());
            case "nint" -> nativeWidth == 8 ? input.readI64() : (long) input.readI32();
            case "nuint" -> nativeWidth == 8 ? unsigned64(input.readI64()) : BigInteger.valueOf(Integer.toUnsignedLong(input.readI32()));
            case "float" -> Float.intBitsToFloat(input.readI32());
            case "double" -> Double.longBitsToDouble(input.readI64());
            default -> throw fail("Unsupported LuminPack scalar " + type);
        };
    }

    void writeScalar(BinaryCursor.Writer output, String type, Object value) {
        if (value == null) value = defaultScalar(type);
        switch (type) {
            case "bool" -> output.writeU8(checkedBoolean(value) ? 1 : 0);
            case "char" -> output.writeU16(character(value));
            case "byte" -> output.writeU8((int) checkedInteger(value, 0, 255));
            case "sbyte" -> output.writeU8((int) checkedInteger(value, Byte.MIN_VALUE, Byte.MAX_VALUE) & 255);
            case "short" -> output.writeU16((int) checkedInteger(value, Short.MIN_VALUE, Short.MAX_VALUE) & 65535);
            case "ushort" -> output.writeU16((int) checkedInteger(value, 0, 65535));
            case "int" -> output.writeI32((int) checkedInteger(value, Integer.MIN_VALUE, Integer.MAX_VALUE));
            case "uint" -> output.writeI32((int) checkedInteger(value, 0, MAX_UINT32));
            case "long" -> output.writeI64(Schema.longValue(value));
            case "ulong" -> output.writeI64(checkedUnsigned64(value));
            case "nint" -> {
                if (schema.nativeBytes == 8) output.writeI64(Schema.longValue(value));
                else output.writeI32((int) checkedInteger(value, Integer.MIN_VALUE, Integer.MAX_VALUE));
            }
            case "nuint" -> {
                if (schema.nativeBytes == 8) output.writeI64(checkedUnsigned64(value));
                else output.writeI32((int) checkedInteger(value, 0, MAX_UINT32));
            }
            case "float" -> output.writeI32(Float.floatToRawIntBits(number(value).floatValue()));
            case "double" -> output.writeI64(Double.doubleToRawLongBits(number(value).doubleValue()));
            default -> throw fail("Unsupported LuminPack scalar " + type);
        }
    }

    static Number number(Object value) {
        if (value instanceof Number number) return number;
        throw fail("Expected a LuminPack numeric value");
    }

    static boolean checkedBoolean(Object value) {
        if (value instanceof Boolean bool) return bool;
        throw fail("Expected a LuminPack Boolean");
    }

    static char character(Object value) {
        if (value instanceof Character character) return character;
        if (value instanceof String text && text.length() == 1) return text.charAt(0);
        throw fail("Expected one LuminPack character");
    }

    static Object defaultScalar(String type) {
        return switch (type) {
            case "bool" -> false;
            case "char" -> '\0';
            case "float" -> 0.0f;
            case "double" -> 0.0;
            case "ulong", "nuint" -> BigInteger.ZERO;
            case "long", "uint", "nint" -> 0L;
            default -> 0;
        };
    }

    static long checkedInteger(Object value, long min, long max) {
        long number = Schema.longValue(value);
        if (number < min || number > max) throw fail("LuminPack integer exceeds its width");
        return number;
    }

    static long checkedUnsigned64(Object value) {
        BigInteger number = Schema.bigInteger(value);
        if (number.signum() < 0 || number.bitLength() > 64) throw fail("LuminPack unsigned integer exceeds its width");
        return number.longValue();
    }

    static BigInteger unsigned64(long bits) {
        return bits < 0 ? BigInteger.valueOf(bits & Long.MAX_VALUE).setBit(63) : BigInteger.valueOf(bits);
    }

    private static long readReferenceId(BinaryCursor.Reader input) {
        int marker = (byte) input.readU8();
        long value = switch (marker) {
            case -121 -> input.readU8();
            case -122 -> (byte) input.readU8();
            case -123 -> input.readU16();
            case -124 -> (short) input.readU16();
            case -125 -> Integer.toUnsignedLong(input.readI32());
            case -126 -> input.readI32();
            case -127, -128 -> input.readI64();
            default -> marker;
        };
        if (value < 0 || value > MAX_UINT32) throw fail("Invalid LuminPack reference ID");
        return value;
    }

    private static void writeReferenceId(BinaryCursor.Writer output, long value) {
        if (value <= 127) output.writeU8((int) value);
        else if (value <= 65535) { output.writeU8(133); output.writeU16((int) value); }
        else { output.writeU8(131); output.writeI32((int) value); }
    }

    static long schemaHash(long[] descriptors) {
        final long prime = 1099511628211L;
        long hash = (0xcbf29ce484222325L ^ descriptors.length) * prime;
        for (long descriptor : descriptors) {
            for (int index = 0; index < Long.BYTES; index++, descriptor >>>= 8) hash = (hash ^ (descriptor & 255)) * prime;
        }
        return hash;
    }

    private static int readOrdinal(BinaryCursor.Reader input, int maximum) {
        int value = 0;
        for (int shift = 0; shift <= 28; shift += 7) {
            int next = input.readU8();
            if (shift == 28 && next > (maximum >>> 28)) break;
            value |= (next & 127) << shift;
            if (next < 128) {
                if (shift == 0 || next != 0) return value;
                break;
            }
        }
        throw fail("Invalid LuminPack Version ordinal");
    }

    private static void writeOrdinal(BinaryCursor.Writer output, int ordinal) {
        while (ordinal >= 128) {
            output.writeU8((ordinal & 127) | 128);
            ordinal >>>= 7;
        }
        output.writeU8(ordinal);
    }

    private boolean readVersion(BinaryCursor.Reader input, Schema.Contract type, Map<String, Object> result, int profile, int depth) {
        int marker = input.readU8();
        if (marker == NULL) return false;
        if (marker != VERSION) throw fail("Invalid LuminPack Version marker");
        int form = input.readU8();
        if (form == 1) {
            readLegacyVersion(input, type, result, profile, depth);
            return true;
        }
        long[] descriptors;
        if (form == (0x50 | profile)) {
            int ordinal = readOrdinal(input, (1 << 30) - 1);
            if (ordinal >= versions.size()) throw fail("Unknown LuminPack Version ordinal");
            descriptors = versions.get(ordinal);
        } else if (form == (0x40 | profile)) {
            int count = input.readU8();
            if (count > 249) throw fail("Invalid LuminPack Version descriptor count");
            long hash = input.readI64();
            if (count > input.remaining() / Long.BYTES) throw fail("Truncated LuminPack Version descriptors");
            descriptors = new long[count];
            var seen = new HashSet<Integer>();
            for (int index = 0; index < count; index++) {
                long descriptor = input.readI64();
                int width = (int) (descriptor >>> 56);
                if ((width > 250 && width < 254) || !seen.add((int) descriptor)) throw fail("Invalid LuminPack Version descriptor");
                descriptors[index] = descriptor;
            }
            if (hash != schemaHash(descriptors)) throw fail("Invalid LuminPack Version schema hash");
            versions.add(descriptors);
        } else throw fail("Unexpected LuminPack Version mode or string profile");

        Schema.Field[] mapped = Arrays.equals(type.versionDescriptors, descriptors) ? type.versionFields :
            resolveVersionFields(type, descriptors);
        for (int index = 0; index < descriptors.length; index++) {
            int width = (int) (descriptors[index] >>> 56);
            Schema.Field field = mapped[index];
            if (width == 254) {
                if (field == null) input.skipString(profile);
                else result.put(field.name, readField(input, field, profile, depth, 0));
            } else {
                int size = width == 255 ? input.readI32() : width;
                if (field == null) input.skip(size);
                else result.put(field.name, readVersionSlice(input.subrecord(size), field, profile, depth, width, width == 255));
            }
        }
        defaults(type, result);
        return true;
    }

    private static Schema.Field[] resolveVersionFields(Schema.Contract type, long[] descriptors) {
        var mapped = new Schema.Field[descriptors.length];
        var present = new HashSet<Schema.Field>();
        for (int index = 0; index < descriptors.length; index++) {
            long descriptor = descriptors[index];
            Schema.Field field = type.versionNames.get((int) descriptor);
            if (field != null) {
                if ((field.descriptor >>> 32) != (descriptor >>> 32) || !present.add(field))
                    throw fail("Incompatible or duplicate LuminPack Version member");
                mapped[index] = field;
            }
        }
        return mapped;
    }

    private void readLegacyVersion(BinaryCursor.Reader input, Schema.Contract type, Map<String, Object> result, int profile, int depth) {
        if (input.readU8() != profile) throw fail("LuminPack legacy Version profile mismatch");
        int count = input.readU8();
        if (count > 249) throw fail("Invalid LuminPack legacy Version member count");
        var mapped = new Schema.Field[count];
        var present = new HashSet<Schema.Field>();
        for (int index = 0; index < count; index++) {
            String name = new String(input.take(readOrdinal(input, Integer.MAX_VALUE)), java.nio.charset.StandardCharsets.UTF_8);
            String contract = new String(input.take(readOrdinal(input, Integer.MAX_VALUE)), java.nio.charset.StandardCharsets.UTF_8);
            for (Schema.Field field : type.members) {
                if (!field.legacyName(name)) continue;
                if (!contract.equals(field.legacy) || !present.add(field)) throw fail("Incompatible LuminPack legacy Version member");
                mapped[index] = field;
                break;
            }
        }
        int[] lengths = new int[count];
        long total = 0;
        for (int index = 0; index < count; index++) {
            long length = readReferenceId(input);
            total += length;
            if (total > Integer.MAX_VALUE) throw fail("LuminPack legacy Version exceeds its payload limit");
            lengths[index] = (int) length;
        }
        if (total > input.remaining()) throw fail("Truncated LuminPack legacy Version payload");
        for (int index = 0; index < count; index++) {
            Schema.Field field = mapped[index];
            if (field == null) input.skip(lengths[index]);
            else result.put(field.name, readVersionSlice(input.subrecord(lengths[index]), field, profile, depth, 0, true));
        }
        defaults(type, result);
    }

    private Object readVersionSlice(BinaryCursor.Reader child, Schema.Field field, int profile, int depth, int width, boolean scoped) {
        int start = versions.size();
        try {
            Object value = readField(child, field, profile, depth, width <= 250 ? width : 0);
            child.requireEnd();
            return value;
        } finally {
            if (scoped) versions.subList(start, versions.size()).clear();
        }
    }

    private void writeVersion(BinaryCursor.Writer output, Schema.Contract type, Map<?, ?> value, int profile, int depth) {
        long[] descriptors = type.versionDescriptors;
        int ordinal = 0;
        while (ordinal < versions.size() && !Arrays.equals(descriptors, versions.get(ordinal))) ordinal++;
        output.writeU8(VERSION);
        if (ordinal == versions.size()) {
            output.writeU8(0x40 | profile);
            output.writeU8(descriptors.length);
            output.writeI64(type.versionHash);
            for (long descriptor : descriptors) output.writeI64(descriptor);
            versions.add(descriptors);
        } else {
            output.writeU8(0x50 | profile);
            writeOrdinal(output, ordinal);
        }
        for (int index = 0; index < descriptors.length; index++) {
            Schema.Field field = type.versionFields[index];
            int width = (int) (descriptors[index] >>> 56);
            Object item = value.get(field.name);
            if (width == 255) {
                int offset = output.position();
                output.writeI32(0);
                int scope = versions.size();
                try { writeField(output, field, item, profile, depth, 0); }
                finally { versions.subList(scope, versions.size()).clear(); }
                output.patchI32(offset, output.position() - offset - Integer.BYTES);
            } else {
                int start = output.position();
                writeField(output, field, item, profile, depth, width <= 250 ? width : 0);
                if (width <= 250 && output.position() - start != width) throw fail("Unexpected LuminPack Version member width");
            }
        }
    }

    static void defaults(Schema.Contract type, Map<String, Object> members) {
        for (Schema.Field field : type.members)
            if (!members.containsKey(field.name)) members.put(field.name,
                field.wire.kind == SCALAR ? defaultScalar(field.wire.type) : (field.wire.required || field.wire.memory()) ? List.of() : null);
    }

    private Object readCompressed(BinaryCursor.Reader input, Schema.Wire wire, int profile, int depth) {
        return CompressedField.read(this, input, wire, profile, depth);
    }

    private void writeCompressed(BinaryCursor.Writer output, Schema.Wire wire, Object value, int profile, int depth) {
        CompressedField.write(this, output, wire, value, profile, depth);
    }
}
