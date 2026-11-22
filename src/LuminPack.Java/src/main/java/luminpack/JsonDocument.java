package luminpack;

import static luminpack.Schema.Wire.*;
import static luminpack.Schema.fail;
import static luminpack.Schema.object;

import java.math.BigInteger;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.IdentityHashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Projects the same JSON shapes used by the managed and C++ codecs. */
final class JsonDocument {
    private static final Json.Name ID = Json.name("$id");
    private static final Json.Name REFERENCE = Json.name("$ref");
    private static final Json.Name TYPE = Json.name("$type");
    private static final Json.Name VALUE = Json.name("$value");

    private final Schema schema;
    private final LuminPack.Bindings bindings;
    private final int encoding;
    private final Map<Long, Reference> references = new HashMap<>();
    private final IdentityHashMap<Object, Reference> written = new IdentityHashMap<>();

    private record Reference(Schema.Contract type, Object value, long id) {}

    JsonDocument(Schema schema, LuminPack.Bindings bindings, int encoding) {
        this.schema = schema;
        this.bindings = bindings;
        this.encoding = encoding;
    }

    Object read(String input) {
        Object json = Json.parse(input, schema.rootProfile.jsonDepth());
        return schema.rootWire == null ? readContract(schema.root, json) : readWire(schema.rootWire, json);
    }

    String write(Object input) {
        Object json = schema.rootWire == null ? writeContract(schema.root, input) : writeWire(schema.rootWire, input);
        return Json.write(json, schema.rootProfile.jsonDepth());
    }

    Object readContract(Schema.Contract type, Object value) {
        if (type.kind.equals("Custom")) return bindings.custom(type).readJson(value);
        if (value == null) {
            if (type.kind.equals("Struct")) throw fail("Missing LuminPack JSON struct");
            return null;
        }
        Map<?, ?> map = object(value);
        if (type.kind.equals("Union")) return readUnion(type, map);
        var result = new LinkedHashMap<String, Object>(type.members.size());
        for (var entry : map.entrySet()) {
            String name = Schema.string(entry.getKey());
            if (type.profile.track() && name.equals("$id")) {
                long id = BinaryDocument.checkedInteger(entry.getValue(), 0, 0xffff_ffffL);
                if (references.putIfAbsent(id, new Reference(type, result, id)) != null) throw fail("Duplicate LuminPack JSON ID");
            } else if (type.profile.track() && name.equals("$ref")) {
                long id = BinaryDocument.checkedInteger(entry.getValue(), 0, 0xffff_ffffL);
                Reference known = references.get(id);
                if (known == null || known.type != type) throw fail("Invalid LuminPack JSON reference ID or type");
                return known.value;
            } else {
                Schema.Field field = type.names.get(name);
                if (field != null) {
                    Object item = entry.getValue();
                    if (item == null && type.profile.track() && field.wire.kind == SCALAR)
                        item = BinaryDocument.defaultScalar(field.wire.type);
                    result.put(field.name, readWire(field.wire, item));
                }
            }
        }
        BinaryDocument.defaults(type, result);
        return result;
    }

    private Object writeContract(Schema.Contract type, Object value) {
        if (type.kind.equals("Custom")) return bindings.custom(type).writeJson(value);
        if (value == null) {
            if (type.kind.equals("Struct")) throw fail("Missing LuminPack JSON struct");
            return null;
        }
        if (type.kind.equals("Union")) return writeUnion(type, value);
        Map<?, ?> map = object(value);
        var result = new LinkedHashMap<Json.Name, Object>(type.members.size() + 1);
        if (type.profile.track()) {
            Reference known = written.get(value);
            if (known != null) {
                if (known.type != type) throw fail("A LuminPack reference has more than one type");
                return Map.of(REFERENCE, known.id);
            }
            long id = written.size();
            written.put(value, new Reference(type, value, id));
            result.put(ID, id);
        }
        for (Schema.Field field : type.members) result.put(field.jsonName, writeWire(field.wire, map.get(field.name)));
        return result;
    }

    private Object readUnion(Schema.Contract union, Map<?, ?> envelope) {
        long hash = BinaryDocument.checkedInteger(envelope.get("$type"), 0, 0xffff_ffffL);
        if (!envelope.containsKey("$value")) throw fail("Missing LuminPack JSON Union payload");
        Schema.UnionCase item = union.cases.get(hash);
        Object value = envelope.get("$value");
        if (item == null) {
            if (!Schema.flag(union.metadata, "unionRegistration") || hash > 65535) throw fail("Unknown LuminPack JSON Union case");
            return new LuminPack.RegisteredUnionValue((int) hash, bindings.registered(union, (int) hash).readJson(value));
        }
        Object payload = readContract(item.contract(), value);
        return payload == null ? null : new LuminPack.UnionValue(hash, payload);
    }

    Object writeUnion(Schema.Contract union, Object value) {
        var result = new LinkedHashMap<Json.Name, Object>(2);
        if (value instanceof LuminPack.RegisteredUnionValue registered) {
            if (!Schema.flag(union.metadata, "unionRegistration")) throw fail("LuminPack Union registration is disabled");
            result.put(TYPE, registered.tag());
            result.put(VALUE, bindings.registered(union, registered.tag()).writeJson(registered.value()));
            return result;
        }
        if (!(value instanceof LuminPack.UnionValue item)) throw fail("Expected a LuminPack JSON Union value");
        Schema.UnionCase active = union.cases.get(item.hash());
        if (active == null) throw fail("Unknown LuminPack JSON Union case");
        if (item.value() == null && active.contract().versioned) return null;
        result.put(TYPE, item.hash());
        result.put(VALUE, writeContract(active.contract(), item.value()));
        return result;
    }

    Object readWire(Schema.Wire wire, Object value) {
        return switch (wire.kind) {
            case SCALAR -> scalar(wire.type, value);
            case STRING -> string(value);
            case CONTRACT -> readContract(wire.contract, value);
            case SEQUENCE -> sequence(wire, value, false);
            case DICTIONARY -> readDictionary(wire, value);
            case NULLABLE -> value == null ? null : readWire(wire.element, value);
            case RECTANGULAR -> rectangle(wire, value, false);
            case PAIR, TUPLE -> tuple(wire, value, false);
            default -> throw fail("Unsupported LuminPack JSON wire descriptor");
        };
    }

    Object writeWire(Schema.Wire wire, Object value) {
        return switch (wire.kind) {
            case SCALAR -> {
                Object scalar = value == null ? BinaryDocument.defaultScalar(wire.type) : scalar(wire.type, value);
                yield scalar instanceof Character character ? character.toString() : scalar;
            }
            case STRING -> string(value);
            case CONTRACT -> writeContract(wire.contract, value);
            case SEQUENCE -> sequence(wire, value, true);
            case DICTIONARY -> writeDictionary(wire, value);
            case NULLABLE -> value == null ? null : writeWire(wire.element, value);
            case RECTANGULAR -> rectangle(wire, value, true);
            case PAIR, TUPLE -> tuple(wire, value, true);
            default -> throw fail("Unsupported LuminPack JSON wire descriptor");
        };
    }

    private Object sequence(Schema.Wire wire, Object value, boolean write) {
        if (value == null) return wire.memory() ? List.of() : null;
        return sequenceElements(wire.element, Schema.array(value), write);
    }

    private List<Object> sequenceElements(Schema.Wire element, List<?> values, boolean write) {
        checkCollection(values.size());
        var result = new ArrayList<Object>(values.size());
        for (Object item : values) result.add(write ? writeWire(element, item) : readWire(element, item));
        return result;
    }

    private Object readDictionary(Schema.Wire wire, Object value) {
        if (value == null) return null;
        var result = new LinkedHashMap<Object, Object>();
        if (wire.key.kind == STRING && value instanceof Map<?, ?> object) {
            checkCollection(object.size());
            for (var item : object.entrySet()) result.put(string(item.getKey()), readWire(wire.value, item.getValue()));
        } else {
            List<?> pairs = Schema.array(value);
            checkCollection(pairs.size());
            for (Object item : pairs) {
                List<?> pair = Schema.array(item);
                if (pair.size() != 2) throw fail("Invalid LuminPack JSON dictionary entry");
                Object key = readWire(wire.key, pair.get(0));
                if (key == null) throw fail("Null LuminPack JSON dictionary key");
                result.put(BinaryDocument.normalizeKey(key), readWire(wire.value, pair.get(1)));
            }
        }
        return result;
    }

    private Object writeDictionary(Schema.Wire wire, Object value) {
        if (value == null) return null;
        Map<?, ?> map = object(value);
        checkCollection(map.size());
        if (wire.key.kind == STRING) {
            var result = new LinkedHashMap<String, Object>(map.size());
            for (var entry : map.entrySet()) {
                String key = string(entry.getKey());
                if (key == null) throw fail("Null LuminPack JSON dictionary key");
                result.put(key, writeWire(wire.value, entry.getValue()));
            }
            return result;
        }
        var result = new ArrayList<List<Object>>(map.size());
        for (var entry : map.entrySet()) {
            if (entry.getKey() == null) throw fail("Null LuminPack JSON dictionary key");
            var pair = new ArrayList<Object>(2);
            pair.add(writeWire(wire.key, entry.getKey()));
            pair.add(writeWire(wire.value, entry.getValue()));
            result.add(pair);
        }
        return result;
    }

    private Object tuple(Schema.Wire wire, Object value, boolean write) {
        List<?> items = Schema.array(value);
        if (items.size() != wire.elements.size()) throw fail("Invalid LuminPack JSON tuple width");
        var result = new ArrayList<Object>(items.size());
        for (int index = 0; index < items.size(); index++) {
            Schema.Wire child = wire.elements.get(index);
            result.add(write ? writeWire(child, items.get(index)) : readWire(child, items.get(index)));
        }
        return result;
    }

    private Object rectangle(Schema.Wire wire, Object value, boolean write) {
        if (value == null) return null;
        Map<?, ?> object = object(value);
        List<?> lengths = Schema.array(object.get("Lengths"));
        List<?> bounds = Schema.array(object.get("LowerBounds"));
        List<?> values = Schema.array(object.get("Values"));
        if (lengths.size() != wire.rank || bounds.size() != wire.rank || BinaryDocument.rectangularCount(lengths) != values.size())
            throw fail("Invalid LuminPack JSON rectangular shape");
        for (Object bound : bounds) Math.toIntExact(Schema.longValue(bound));
        return BinaryDocument.rectangle(lengths, bounds, sequenceElements(wire.element, values, write));
    }

    Object scalar(String type, Object value) {
        return switch (type) {
            case "bool" -> BinaryDocument.checkedBoolean(value);
            case "char" -> {
                if (value instanceof Character item) yield item;
                String text = string(value);
                if (text == null || (encoding == LuminPack.UTF8 && text.length() > 2)) throw fail("Invalid LuminPack JSON character");
                yield text.isEmpty() ? '\0' : text.charAt(0);
            }
            case "byte" -> (int) BinaryDocument.checkedInteger(value, 0, 255);
            case "sbyte" -> (int) BinaryDocument.checkedInteger(value, Byte.MIN_VALUE, Byte.MAX_VALUE);
            case "short" -> (int) BinaryDocument.checkedInteger(value, Short.MIN_VALUE, Short.MAX_VALUE);
            case "ushort" -> (int) BinaryDocument.checkedInteger(value, 0, 65535);
            case "int" -> (int) BinaryDocument.checkedInteger(value, Integer.MIN_VALUE, Integer.MAX_VALUE);
            case "uint" -> BinaryDocument.checkedInteger(value, 0, 0xffff_ffffL);
            case "long" -> Schema.longValue(value);
            case "nint" -> schema.nativeBytes == 8 ? Schema.longValue(value) : (long) (int) Schema.longValue(value);
            case "nuint" -> {
                BinaryDocument.checkedUnsigned64(value);
                if (schema.nativeBytes == 4) yield BigInteger.valueOf(Integer.toUnsignedLong(Schema.bigInteger(value).intValue()));
                yield Schema.bigInteger(value);
            }
            case "ulong" -> { BinaryDocument.checkedUnsigned64(value); yield Schema.bigInteger(value); }
            case "float" -> {
                float number = BinaryDocument.number(value).floatValue();
                if (!Float.isFinite(number)) throw fail("Invalid LuminPack JSON float");
                yield number;
            }
            case "double" -> {
                double number = BinaryDocument.number(value).doubleValue();
                if (!Double.isFinite(number)) throw fail("Invalid LuminPack JSON double");
                yield number;
            }
            default -> throw fail("Unsupported LuminPack JSON scalar " + type);
        };
    }

    String string(Object value) {
        if (value == null) return null;
        String text = Schema.string(value);
        if (text.length() > schema.rootProfile.string()) throw fail("LuminPack JSON string limit exceeded");
        return text;
    }

    void checkCollection(int count) {
        if (count > schema.rootProfile.collection()) throw fail("LuminPack JSON collection limit exceeded");
    }
}
