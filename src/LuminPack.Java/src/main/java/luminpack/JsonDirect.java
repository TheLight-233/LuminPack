package luminpack;

import static luminpack.Schema.Wire.*;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.IdentityHashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Direct UTF-8 operations for contracts without callbacks or reference identities. */
final class JsonDirect {
    private static final long UINT32_MAX = 0xffff_ffffL;
    private static final byte[] TYPE_HEAD = "{\"$type\":".getBytes(StandardCharsets.UTF_8);
    private static final byte[] VALUE_HEAD = ",\"$value\":".getBytes(StandardCharsets.UTF_8);

    private final Schema schema;
    private final JsonDocument general;
    private final int maximumDepth;

    JsonDirect(Schema schema, LuminPack.Bindings bindings) {
        this.schema = schema;
        general = new JsonDocument(schema, bindings, LuminPack.UTF8);
        maximumDepth = schema.rootProfile.jsonDepth();
    }

    Object read(String input) {
        try {
            var reader = new Json.Reader(input, maximumDepth);
            Object value = schema.rootWire == null ? readContract(reader, schema.root, 0) : readWire(reader, schema.rootWire, 0);
            reader.space();
            if (reader.atEnd()) return value;
        } catch (IllegalArgumentException | ArithmeticException invalidTypedValue) {
            // A later duplicate key can replace an invalid earlier value in the ordinary reader.
        }
        return general.read(input);
    }

    byte[] write(Object value) {
        var output = new JsonUtf8Writer();
        if (schema.rootWire == null) writeContract(output, schema.root, value, 0);
        else writeWire(output, schema.rootWire, value, 0);
        return output.finish();
    }

    private Object readContract(Json.Reader reader, Schema.Contract type, int depth) {
        if (reader.consumeNull()) {
            if (type.kind.equals("Struct")) throw Schema.fail("Missing LuminPack JSON struct");
            return null;
        }
        int start = reader.position();
        reader.checkDepth(depth);
        reader.expect('{');
        if (type.kind.equals("Union")) {
            reader.space();
            if (reader.match("\"$type\":")) {
                long hash = checked(reader.signedInteger(), 0, UINT32_MAX);
                Schema.UnionCase active = type.cases.get(hash);
                reader.space();
                if (active != null && reader.match(",\"$value\":")) {
                    Object value = readContract(reader, active.contract(), depth + 1);
                    reader.space();
                    if (reader.consume('}')) return value == null ? null : new LuminPack.UnionValue(hash, value);
                }
            }
        } else {
            Schema.Field[] fields = type.jsonFields;
            var result = new LinkedHashMap<String, Object>(fields.length * 4 / 3 + 1);
            int index = 0;
            for (; index < fields.length; index++) {
                reader.space();
                if (index != 0 && !reader.consume(',')) break;
                reader.space();
                Schema.Field field = fields[index];
                if (!reader.match(field.jsonName.token())) break;
                result.put(field.name, readWire(reader, field.wire, depth + 1));
            }
            reader.space();
            if (index == fields.length && reader.consume('}')) return result;
        }
        // The general reader handles sparse, reordered, escaped, former, unknown and duplicate names.
        reader.position(start);
        return general.readContract(type, reader.value(depth));
    }

    private Object readWire(Json.Reader reader, Schema.Wire wire, int depth) {
        return switch (wire.kind) {
            case SCALAR -> readScalar(reader, wire.type, depth);
            case STRING -> readString(reader, depth);
            case CONTRACT -> readContract(reader, wire.contract, depth);
            case SEQUENCE -> readSequence(reader, wire, depth);
            case DICTIONARY -> readDictionary(reader, wire, depth);
            case PAIR, TUPLE -> readTuple(reader, wire, depth);
            case NULLABLE -> reader.consumeNull() ? null : readWire(reader, wire.element, depth);
            case RECTANGULAR -> general.readWire(wire, reader.value(depth));
            default -> throw Schema.fail("Unsupported LuminPack JSON wire descriptor");
        };
    }

    private String readString(Json.Reader reader, int depth) {
        if (reader.next('"')) return general.string(reader.string());
        if (reader.consumeNull()) return null;
        return general.string(reader.value(depth));
    }

    private Object readScalar(Json.Reader reader, String type, int depth) {
        return switch (type) {
            case "int" -> Math.toIntExact(reader.signedInteger());
            case "long" -> reader.signedInteger();
            case "uint" -> checked(reader.signedInteger(), 0, UINT32_MAX);
            case "short" -> (int) checked(reader.signedInteger(), Short.MIN_VALUE, Short.MAX_VALUE);
            case "ushort" -> (int) checked(reader.signedInteger(), 0, 65535);
            case "sbyte" -> (int) checked(reader.signedInteger(), Byte.MIN_VALUE, Byte.MAX_VALUE);
            case "byte" -> (int) checked(reader.signedInteger(), 0, 255);
            case "nint" -> {
                long value = reader.signedInteger();
                yield schema.nativeBytes == 8 ? value : (int) value;
            }
            default -> general.scalar(type, reader.value(depth));
        };
    }

    private Object readSequence(Json.Reader reader, Schema.Wire wire, int depth) {
        if (reader.consumeNull()) return wire.memory() ? List.of() : null;
        reader.checkDepth(depth);
        reader.expect('[');
        reader.space();
        if (reader.consume(']')) return new ArrayList<>();
        if (isInt32(wire.element)) return readInt32Elements(reader);
        var values = new ArrayList<Object>();
        do {
            general.checkCollection(values.size() + 1);
            values.add(readWire(reader, wire.element, depth + 1));
            reader.space();
        } while (reader.consume(','));
        reader.expect(']');
        return values;
    }

    private ScalarBlock readInt32Elements(Json.Reader reader) {
        var data = new BinaryCursor.Writer(32);
        int count = 0;
        do {
            general.checkCollection(++count);
            data.writeI32(Math.toIntExact(reader.signedInteger()));
            reader.space();
        } while (reader.consume(','));
        reader.expect(']');
        return new ScalarBlock("int", Integer.BYTES, data.toByteArray());
    }

    private Object readDictionary(Json.Reader reader, Schema.Wire wire, int depth) {
        if (reader.consumeNull()) return null;
        reader.checkDepth(depth);
        var values = new LinkedHashMap<Object, Object>();
        if (wire.key.kind == STRING && reader.next('{')) {
            reader.expect('{');
            reader.space();
            if (!reader.consume('}')) {
                do {
                    reader.space();
                    String key = general.string(reader.string());
                    reader.expect(':');
                    values.put(key, readWire(reader, wire.value, depth + 1));
                    general.checkCollection(values.size());
                    reader.space();
                } while (reader.consume(','));
                reader.expect('}');
            }
        } else {
            reader.expect('[');
            reader.space();
            if (!reader.consume(']')) {
                int count = 0;
                do {
                    general.checkCollection(++count);
                    reader.checkDepth(depth + 1);
                    reader.expect('[');
                    Object key = readWire(reader, wire.key, depth + 2);
                    if (key == null) throw Schema.fail("Null LuminPack JSON dictionary key");
                    reader.expect(',');
                    Object value = readWire(reader, wire.value, depth + 2);
                    reader.expect(']');
                    values.put(BinaryDocument.normalizeKey(key), value);
                    reader.space();
                } while (reader.consume(','));
                reader.expect(']');
            }
        }
        return values;
    }

    private List<Object> readTuple(Json.Reader reader, Schema.Wire wire, int depth) {
        reader.checkDepth(depth);
        reader.expect('[');
        var values = new ArrayList<Object>(wire.elements.size());
        for (Schema.Wire element : wire.elements) {
            if (!values.isEmpty()) reader.expect(',');
            values.add(readWire(reader, element, depth + 1));
        }
        reader.expect(']');
        return values;
    }

    private void writeContract(JsonUtf8Writer output, Schema.Contract type, Object value, int depth) {
        if (value == null) {
            if (type.kind.equals("Struct")) throw Schema.fail("Missing LuminPack JSON struct");
            output.nullValue();
            return;
        }
        if (type.kind.equals("Union")) {
            writeUnion(output, type, value, depth);
            return;
        }
        checkDepth(depth);
        Map<?, ?> record = Schema.object(value);
        output.token('{');
        Schema.Field[] fields = type.jsonFields;
        for (int i = 0; i < fields.length; i++) {
            Schema.Field field = fields[i];
            output.raw(i == 0 ? field.firstJsonHead : field.nextJsonHead);
            writeWire(output, field.wire, record.get(field.name), depth + 1);
        }
        output.token('}');
    }

    private void writeUnion(JsonUtf8Writer output, Schema.Contract type, Object value, int depth) {
        if (!(value instanceof LuminPack.UnionValue union)) {
            writeGeneral(output, general.writeUnion(type, value), depth);
            return;
        }
        Schema.UnionCase active = type.cases.get(union.hash());
        if (active == null) throw Schema.fail("Unknown LuminPack JSON Union case");
        if (union.value() == null && active.contract().versioned) { output.nullValue(); return; }
        checkDepth(depth);
        output.raw(TYPE_HEAD);
        output.integer(union.hash());
        output.raw(VALUE_HEAD);
        writeContract(output, active.contract(), union.value(), depth + 1);
        output.token('}');
    }

    private void writeWire(JsonUtf8Writer output, Schema.Wire wire, Object value, int depth) {
        switch (wire.kind) {
            case SCALAR -> writeScalar(output, wire.type, value);
            case STRING -> output.string(general.string(value));
            case CONTRACT -> writeContract(output, wire.contract, value, depth);
            case SEQUENCE -> writeSequence(output, wire, value, depth);
            case DICTIONARY -> writeDictionary(output, wire, value, depth);
            case PAIR, TUPLE -> writeTuple(output, wire, value, depth);
            case NULLABLE -> {
                if (value == null) output.nullValue();
                else writeWire(output, wire.element, value, depth);
            }
            case RECTANGULAR -> writeGeneral(output, general.writeWire(wire, value), depth);
            default -> throw Schema.fail("Unsupported LuminPack JSON wire descriptor");
        }
    }

    private void writeScalar(JsonUtf8Writer output, String type, Object value) {
        Object scalar = value == null ? BinaryDocument.defaultScalar(type) : general.scalar(type, value);
        if (scalar instanceof Boolean flag) output.booleanValue(flag);
        else if (scalar instanceof Character character) output.string(character.toString());
        else if (scalar instanceof Float number) output.floating(number);
        else if (scalar instanceof Double number) output.floating(number);
        else if (scalar instanceof java.math.BigInteger number) output.unsignedInteger(number.longValue());
        else if (scalar instanceof Integer number) output.integer32(number);
        else if (type.equals("uint")) output.unsignedInteger32(((Number) scalar).longValue());
        else output.integer(((Number) scalar).longValue());
    }

    private void writeSequence(JsonUtf8Writer output, Schema.Wire wire, Object value, int depth) {
        if (value == null && !wire.memory()) { output.nullValue(); return; }
        checkDepth(depth);
        List<?> items = value == null ? List.of() : Schema.array(value);
        general.checkCollection(items.size());
        output.token('[');
        if (!(isInt32(wire.element) && items instanceof ScalarBlock block && block.writeJsonInt32(output))) {
            boolean subsequent = false;
            for (Object item : items) {
                if (subsequent) output.token(',');
                subsequent = true;
                writeWire(output, wire.element, item, depth + 1);
            }
        }
        output.token(']');
    }

    private static boolean isInt32(Schema.Wire wire) {
        return wire.kind == SCALAR && wire.type.equals("int");
    }

    private void writeDictionary(JsonUtf8Writer output, Schema.Wire wire, Object value, int depth) {
        if (value == null) { output.nullValue(); return; }
        checkDepth(depth);
        Map<?, ?> dictionary = Schema.object(value);
        general.checkCollection(dictionary.size());
        boolean textKeys = wire.key.kind == STRING;
        if (textKeys && dictionary instanceof IdentityHashMap<?, ?>) {
            writeGeneral(output, general.writeWire(wire, value), depth);
            return;
        }
        output.token(textKeys ? '{' : '[');
        boolean subsequent = false;
        for (var entry : dictionary.entrySet()) {
            if (subsequent) output.token(',');
            subsequent = true;
            Object key = entry.getKey();
            if (key == null) throw Schema.fail("Null LuminPack JSON dictionary key");
            if (textKeys) {
                output.string(general.string(key));
                output.token(':');
                writeWire(output, wire.value, entry.getValue(), depth + 1);
            } else {
                checkDepth(depth + 1);
                output.token('[');
                writeWire(output, wire.key, key, depth + 2);
                output.token(',');
                writeWire(output, wire.value, entry.getValue(), depth + 2);
                output.token(']');
            }
        }
        output.token(textKeys ? '}' : ']');
    }

    private void writeTuple(JsonUtf8Writer output, Schema.Wire wire, Object value, int depth) {
        checkDepth(depth);
        List<?> tuple = Schema.array(value);
        if (tuple.size() != wire.elements.size()) throw Schema.fail("Invalid LuminPack JSON tuple width");
        output.token('[');
        for (int i = 0; i < tuple.size(); i++) {
            if (i > 0) output.token(',');
            writeWire(output, wire.elements.get(i), tuple.get(i), depth + 1);
        }
        output.token(']');
    }

    private void writeGeneral(JsonUtf8Writer output, Object value, int depth) {
        var text = new StringBuilder();
        Json.write(text, value, depth, maximumDepth);
        output.raw(text.toString().getBytes(StandardCharsets.UTF_8));
    }

    private void checkDepth(int depth) {
        if (depth >= maximumDepth) throw Schema.fail("LuminPack JSON depth limit exceeded");
    }

    private static long checked(long value, long min, long max) {
        if (value < min || value > max) throw Schema.fail("LuminPack integer exceeds its width");
        return value;
    }
}
