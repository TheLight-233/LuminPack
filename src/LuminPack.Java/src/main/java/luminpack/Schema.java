package luminpack;

import java.math.BigDecimal;
import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/** Immutable types and member descriptors resolved outside document operations. */
final class Schema {
    final Contract root;
    final Wire rootWire;
    final Profile rootProfile;
    final int binaryProfile;
    final int nativeBytes;
    final int nativeScalarAlignment;
    final boolean completeBinaryInput;
    final boolean directJson;
    final Map<String, Contract> contractsByIdentity;
    final Map<String, Map<?, ?>> valueLayouts = new HashMap<>();

    Schema(String manifest) {
        Map<?, ?> document = object(Json.parse(manifest, 512));
        if (integer(document.get("version"), 0) != 1) throw fail("Unsupported LuminPack manifest version");
        var contracts = new HashMap<String, Contract>();
        for (Object entry : list(document.get("contracts"))) {
            var contract = new Contract(object(entry));
            if (contracts.putIfAbsent(contract.id, contract) != null) throw fail("Duplicate contract " + contract.id);
            contracts.putIfAbsent(contract.type, contract);
        }
        root = contracts.get(normalize(string(document.get("root"))));
        rootWire = document.get("rootWire") instanceof Map<?, ?> shape ? new Wire(shape) : null;
        if (rootWire == null && root == null) throw fail("Unknown LuminPack root type");
        rootProfile = rootWire == null ? root.profile :
            Profile.from(document.get("rootProfile") instanceof Map<?, ?> profile ? profile : Map.of());
        binaryProfile = integer(document.get("binaryProfile"), rootProfile.binary());
        nativeBytes = integer(document.get("nativeIntegerBytes"), Long.BYTES);
        if (nativeBytes != 4 && nativeBytes != 8) throw fail("Invalid native integer width");
        nativeScalarAlignment = integer(document.get("nativeScalarAlignment"), Long.BYTES);
        if (nativeScalarAlignment != 4 && nativeScalarAlignment != 8) throw fail("Invalid native scalar alignment");
        completeBinaryInput = Boolean.TRUE.equals(document.get("requireCompleteBinaryInput"));
        for (Object entry : list(document.get("valueLayouts"))) {
            Map<?, ?> layout = object(entry);
            valueLayouts.put(normalize(string(layout.get("type"))), layout);
        }
        // Resolve reachable contracts eagerly. Recursive references terminate at the existing declaration.
        if (rootWire == null) root.resolve(contracts);
        else rootWire.resolve(contracts);
        directJson = contracts.values().stream().filter(contract -> contract.resolved)
            .noneMatch(contract -> contract.profile.track() || contract.kind.equals("Custom"));
        contractsByIdentity = Map.copyOf(contracts);
    }

    record Profile(int binary, boolean declared, int json, boolean track, int input, int collection,
                   int string, int depth, int jsonDepth) {
        static Profile from(Map<?, ?> metadata) {
            return new Profile(integer(metadata.get("binary"), 2), flag(metadata, "declared"),
                integer(metadata.get("jsonEncoding"), 1), flag(metadata, "trackReferences"),
                integer(metadata.get("maxInput"), Integer.MAX_VALUE), integer(metadata.get("maxCollection"), Integer.MAX_VALUE),
                integer(metadata.get("maxString"), Integer.MAX_VALUE), integer(metadata.get("maxDepth"), 512),
                integer(metadata.get("jsonDepth"), 1024));
        }

        int binary(int inherited) { return declared ? binary : inherited; }
    }

    static final class Contract {
        final String id;
        final String type;
        final String kind;
        final Profile profile;
        final int binaryMemberCount;
        final boolean versioned;
        final boolean wideTags;
        final boolean unionRegistration;
        final boolean customJson;
        final List<Field> members = new ArrayList<>();
        final Field[] jsonFields;
        final Map<String, Field> names = new HashMap<>();
        final Map<Integer, Field> versionNames = new HashMap<>();
        final Field[] versionFields;
        final long[] versionDescriptors;
        final long versionHash;
        final Map<Long, UnionCase> cases = new HashMap<>();
        final Map<?, ?> metadata;
        volatile Layout.Plan layoutPlan;
        boolean resolved;

        Contract(Map<?, ?> metadata) {
            this.metadata = metadata;
            id = normalize(string(metadata.get("id")));
            type = normalize(string(metadata.get("type")));
            kind = string(metadata.get("kind"));
            profile = Profile.from(object(metadata.get("profile")));
            versioned = flag(metadata, "versioned");
            wideTags = flag(metadata, "unionWideTags");
            unionRegistration = flag(metadata, "unionRegistration");
            customJson = flag(metadata, "customJson");
            for (Object item : list(metadata.get("members"))) {
                Field field = new Field(object(item));
                members.add(field);
                if (names.putIfAbsent(field.name, field) != null) throw fail("Duplicate member " + field.name);
            }
            binaryMemberCount = integer(metadata.get("binaryMemberCount"), members.size());
            if (metadata.containsKey("binaryMemberCount") && binaryMemberCount != members.size() - 1)
                throw fail("Invalid LuminPack collection base member count");
            jsonFields = members.toArray(Field[]::new);
            versionFields = versioned ? members.toArray(Field[]::new) : null;
            versionDescriptors = versioned ? new long[versionFields.length] : null;
            for (int index = 0; index < members.size(); index++) {
                Field field = members.get(index);
                for (String alias : field.aliases) names.putIfAbsent(alias, field);
                if (versioned) {
                    if (field.descriptor == null) throw fail("Missing Version descriptor for " + id + '.' + field.name);
                    versionDescriptors[index] = field.descriptor;
                    versionNames.putIfAbsent(field.descriptor.intValue(), field);
                    for (int key : field.nameKeys) {
                        Field old = versionNames.putIfAbsent(key, field);
                        if (old != null && old != field) throw fail("Conflicting Version aliases in " + id);
                    }
                }
            }
            versionHash = versioned ? BinaryDocument.schemaHash(versionDescriptors) : 0;
        }

        void resolve(Map<String, Contract> contracts) {
            if (resolved) return;
            resolved = true;
            if (!list(metadata.get("typeParameters")).isEmpty()) throw fail("Open LuminPack type " + id);
            if (!List.of("Record", "Struct", "Union", "Custom").contains(kind)) throw fail("Unsupported contract kind " + kind);
            if (binaryMemberCount < 0 || binaryMemberCount > 249) throw fail("LuminPack records support up to 249 members");
            for (Field field : members) {
                if (field.wire == null) throw fail("Missing wire descriptor for " + id + '.' + field.name);
                field.wire.resolve(contracts);
            }
            for (Object item : list(metadata.get("cases"))) {
                Map<?, ?> row = object(item);
                long hash = longValue(row.get("hash"));
                long tag = row.get("tag") == null ? -1 : longValue(row.get("tag"));
                Contract target = contracts.get(normalize(string(row.get("type"))));
                if (target == null) target = contracts.get(normalize(string(row.get("id"))));
                if (target == null || hash <= 0 || hash >= 0xffff_ffffL || cases.putIfAbsent(hash, new UnionCase(target, tag)) != null)
                    throw fail("Invalid LuminPack Union case in " + id);
                target.resolve(contracts);
            }
        }

        int maximumRegisteredTag() { return wideTags ? 65535 : 255; }

        void checkRegistrationTag(int tag) {
            if (tag < 0 || tag > maximumRegisteredTag()) throw fail("Registered LuminPack Union tag is out of range");
            if (cases.containsKey((long) tag) || cases.values().stream().anyMatch(item -> item.tag == tag))
                throw fail("This LuminPack Union tag belongs to a generated case");
        }
    }

    record UnionCase(Contract contract, long tag) {}

    static final class Field {
        final String name;
        final Json.Name jsonName;
        final byte[] firstJsonHead;
        final byte[] nextJsonHead;
        final String[] aliases;
        final int[] nameKeys;
        final Profile profile;
        final Wire wire;
        final Long descriptor;
        final Object fixedNullable;
        final String legacy;
        final boolean compressed;

        Field(Map<?, ?> metadata) {
            name = string(metadata.get("name"));
            jsonName = Json.name(name);
            firstJsonHead = jsonName.token().getBytes(StandardCharsets.UTF_8);
            nextJsonHead = ("," + jsonName.token()).getBytes(StandardCharsets.UTF_8);
            aliases = list(metadata.get("oldNames")).stream().map(Schema::string).toArray(String[]::new);
            nameKeys = list(metadata.get("nameKeys")).stream().mapToInt(key -> (int) longValue(key)).toArray();
            profile = Profile.from(object(metadata.get("profile")));
            wire = metadata.get("wire") == null ? null : new Wire(object(metadata.get("wire")));
            descriptor = metadata.get("descriptor") instanceof String hex ? Long.parseUnsignedLong(hex, 16) : null;
            fixedNullable = metadata.get("fixedNullable");
            legacy = metadata.get("legacyVersionContract") instanceof String text ? text : null;
            compressed = flag(metadata, "compressed");
        }

        boolean legacyName(String candidate) {
            if (name.equals(candidate)) return true;
            for (String alias : aliases) if (alias.equals(candidate)) return true;
            return false;
        }
    }

    static final class Wire {
        final int kind;
        final String type;
        final Wire element;
        final Wire key;
        final Wire value;
        final List<Wire> elements;
        final int rank;
        final String family;
        final boolean required;
        final Map<?, ?> metadata;
        volatile Layout.Plan layoutPlan;
        volatile Layout.NullablePlan nullablePlan;
        Contract contract;

        static final int SCALAR = 1, STRING = 2, CONTRACT = 3, SEQUENCE = 4, DICTIONARY = 5, NULLABLE = 6,
            RECTANGULAR = 7, PAIR = 8, TUPLE = 9;

        Wire(Map<?, ?> metadata) {
            this.metadata = metadata;
            kind = switch (string(metadata.get("kind"))) {
                case "scalar" -> SCALAR;
                case "string" -> STRING;
                case "contract" -> CONTRACT;
                case "sequence" -> SEQUENCE;
                case "dictionary" -> DICTIONARY;
                case "nullable" -> NULLABLE;
                case "rectangular" -> RECTANGULAR;
                case "pair" -> PAIR;
                case "tuple" -> TUPLE;
                default -> throw fail("Unsupported wire descriptor " + metadata.get("kind"));
            };
            type = metadata.get("type") instanceof String text ? normalize(text) : "";
            element = metadata.get("element") instanceof Map<?, ?> item ? new Wire(item) : null;
            key = metadata.get("key") instanceof Map<?, ?> item ? new Wire(item) : null;
            value = metadata.get("value") instanceof Map<?, ?> item ? new Wire(item) : null;
            elements = list(metadata.get("elements")).stream().map(item -> new Wire(object(item))).toList();
            rank = integer(metadata.get("rank"), 0);
            required = flag(metadata, "required");
            family = metadata.get("family") instanceof String name ? normalize(name) : "";
        }

        void resolve(Map<String, Contract> contracts) {
            if (kind == CONTRACT) {
                contract = contracts.get(type);
                if (contract == null) throw fail("Missing LuminPack contract " + type);
                contract.resolve(contracts);
            }
            if (element != null) element.resolve(contracts);
            if (key != null) key.resolve(contracts);
            if (value != null) value.resolve(contracts);
            for (Wire child : elements) child.resolve(contracts);
        }

        boolean nullSequenceIsEmpty() {
            if (required) throw fail("Null LuminPack collection base");
            return memory();
        }

        boolean memory() {
            return family.equals("System.Memory") || family.equals("System.ReadOnlyMemory") || family.equals("System.Buffers.ReadOnlySequence");
        }

        boolean segmented() { return family.equals("System.Buffers.ReadOnlySequence"); }
    }

    static String normalize(String value) { return value.replace("global::", "").trim(); }

    static Map<?, ?> object(Object value) {
        if (value instanceof Map<?, ?> result) return result;
        throw fail("Expected an object");
    }

    static List<?> list(Object value) {
        if (value == null) return List.of();
        if (value instanceof List<?> result) return result;
        throw fail("Expected an array");
    }

    static List<?> array(Object value) {
        if (value instanceof List<?> result) return result;
        throw fail("Expected an array");
    }

    static String string(Object value) {
        if (value instanceof String result) return result;
        throw fail("Expected a string");
    }

    static boolean flag(Map<?, ?> metadata, String name) { return Boolean.TRUE.equals(metadata.get(name)); }

    static int integer(Object value, int fallback) { return value == null ? fallback : Math.toIntExact(longValue(value)); }

    static long longValue(Object value) {
        if (value instanceof Integer number) return number;
        if (value instanceof Long number) return number;
        if (value instanceof Byte number) return number;
        if (value instanceof Short number) return number;
        return bigInteger(value).longValueExact();
    }

    static BigInteger bigInteger(Object value) {
        if (value instanceof BigInteger integer) return integer;
        if (value instanceof BigDecimal decimal) return decimal.toBigIntegerExact();
        if (value instanceof Double || value instanceof Float) return new BigDecimal(value.toString()).toBigIntegerExact();
        if (value instanceof Number number) return BigInteger.valueOf(number.longValue());
        throw fail("Expected an integer");
    }

    static IllegalArgumentException fail(String message) { return new IllegalArgumentException(message + '.'); }
}
