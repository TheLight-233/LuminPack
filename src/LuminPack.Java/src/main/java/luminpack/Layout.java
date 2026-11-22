package luminpack;

import java.util.AbstractList;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collection;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.RandomAccess;

import static luminpack.Schema.Wire.*;
import static luminpack.Schema.fail;
import static luminpack.Schema.flag;
import static luminpack.Schema.integer;
import static luminpack.Schema.list;
import static luminpack.Schema.object;
import static luminpack.Schema.string;

/** Compiles exported CLR storage plans into fixed-width little-endian codecs. */
final class Layout {
    private static final Plan NO_PLAN = new Plan(null, new Leaf[0], null, new Model[0], false);

    private Layout() {}

    static boolean readStruct(BinaryDocument document, BinaryCursor.Reader input, Schema.Contract contract,
            Map<String, Object> value, int profile, int depth, boolean packed) {
        Plan plan = structPlan(document, contract);
        if (plan == NO_PLAN) return false;
        Model model = packed ? plan.packed : plan.standalone(document);
        value.putAll(castRecord(readValue(input, plan, model)));
        return true;
    }

    static Map<String, Object> readStruct(BinaryDocument document, BinaryCursor.Reader input, Schema.Contract contract) {
        Plan plan = structPlan(document, contract);
        if (plan == NO_PLAN) return null;
        return castRecord(readValue(input, plan, plan.standalone(document)));
    }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> castRecord(Object value) {
        return (Map<String, Object>) value;
    }

    static boolean writeStruct(BinaryDocument document, BinaryCursor.Writer output, Schema.Contract contract,
            Map<?, ?> value, int profile, int depth, boolean packed) {
        Plan plan = structPlan(document, contract);
        if (plan == NO_PLAN) return false;
        writeValue(document, output, plan, packed ? plan.packed : plan.standalone(document), value);
        return true;
    }

    static List<Object> readAggregate(BinaryDocument document, BinaryCursor.Reader input, Schema.Wire wire,
            int profile, int depth) {
        Plan plan = aggregatePlan(document, wire);
        if (plan != NO_PLAN && wire.kind == TUPLE)
            return castTuple(readValue(input, plan, plan.standalone(document)));
        var values = new ArrayList<Object>(wire.elements.size());
        for (Schema.Wire element : wire.elements) values.add(document.readWire(input, element, profile, depth));
        return values;
    }

    @SuppressWarnings("unchecked")
    private static List<Object> castTuple(Object value) {
        return (List<Object>) value;
    }

    static void writeAggregate(BinaryDocument document, BinaryCursor.Writer output, Schema.Wire wire,
            Object value, int profile, int depth) {
        Plan plan = aggregatePlan(document, wire);
        if (plan != NO_PLAN && wire.kind == TUPLE) {
            writeValue(document, output, plan, plan.standalone(document), value);
            return;
        }
        List<?> values = aggregate(value, wire.elements.size());
        for (int index = 0; index < values.size(); index++)
            document.writeWire(output, wire.elements.get(index), values.get(index), profile, depth);
    }

    static List<Object> readCollection(BinaryDocument document, BinaryCursor.Reader input, Schema.Wire element,
            int count, boolean segmented) {
        if (count == 0) return new ArrayList<>();
        if (element.kind == NULLABLE) {
            NullablePlan plan = nullablePlan(document, element);
            if (plan == null || segmented && !plan.nativeElement) return null;
            Model model = plan.selectRead(input, segmented);
            checkBlock(input, count, model);
            var values = new ArrayList<Object>(count);
            for (int index = 0; index < count; index++)
                values.add(readNullable(document, input, model));
            return values;
        }
        Plan plan = collectionPlan(document, element, segmented);
        if (plan == NO_PLAN) return null;
        Model model = plan.selectRead(document, input);
        checkBlock(input, count, model);
        byte[] storage = input.take(count * model.stride);
        model.validateBooleans(storage, count);
        return new RawRows(plan, model, count, storage);
    }

    static boolean writeCollection(BinaryDocument document, BinaryCursor.Writer output, Schema.Wire element,
            List<?> values, int profile, int depth) {
        return writeCollection(document, output, element, values, profile, depth, false);
    }

    static boolean writeCollection(BinaryDocument document, BinaryCursor.Writer output, Schema.Wire element,
            List<?> values, int profile, int depth, boolean segmented) {
        if (values.isEmpty()) return true;
        if (element.kind == NULLABLE) {
            NullablePlan plan = nullablePlan(document, element);
            if (plan == null || segmented && !plan.nativeElement) return false;
            Model model = plan.selectWrite(output, segmented);
            for (Object value : values) writeNullable(document, output, model, value);
            return true;
        }
        Plan plan = collectionPlan(document, element, segmented);
        if (plan == NO_PLAN) return false;
        Model model = plan.selectWrite(document, output);
        if (values instanceof RawRows raw && raw.write(document, output, plan, model)) return true;
        for (Object value : values) writeValue(document, output, plan, model, value);
        return true;
    }

    static Object readDictionaryValue(BinaryDocument document, BinaryCursor.Reader input, Schema.Wire wire,
            int profile, int depth) {
        NullablePlan plan = wire.kind == NULLABLE ? nullablePlan(document, wire) : null;
        if (plan != null && plan.nativeElement)
            return readNullable(document, input, plan.untagged);
        return document.readWire(input, wire, profile, depth);
    }

    static void writeDictionaryValue(BinaryDocument document, BinaryCursor.Writer output, Schema.Wire wire,
            Object value, int profile, int depth) {
        NullablePlan plan = wire.kind == NULLABLE ? nullablePlan(document, wire) : null;
        if (plan != null && plan.nativeElement) writeNullable(document, output, plan.untagged, value);
        else document.writeWire(output, wire, value, profile, depth);
    }

    static int rawWidth(BinaryDocument document, Schema.Wire wire) {
        if (wire.kind == SCALAR) return document.scalarWidth(wire.type);
        Plan plan = collectionPlan(document, wire, false);
        if (plan == NO_PLAN || plan.tagged) return -1;
        if (wire.kind == CONTRACT && !"RawUnmanaged".equals(wire.contract.metadata.get("storageKind"))) return -1;
        if ((wire.kind == PAIR || wire.kind == TUPLE) && !flag(wire.metadata, "rawCollection")) return -1;
        return plan.standalone(document).stride;
    }

    private static void checkBlock(BinaryCursor.Reader input, int count, Model model) {
        if (count > input.remaining() / model.stride)
            throw fail("Truncated LuminPack storage block");
    }

    private static Plan collectionPlan(BinaryDocument document, Schema.Wire element, boolean segmented) {
        return switch (element.kind) {
            case CONTRACT -> segmented ? NO_PLAN : structPlan(document, element.contract);
            case PAIR -> pairCollection(document, element, segmented);
            case TUPLE -> segmented && (!flag(element.metadata, "rawCollection") ||
                    element.metadata.get("layout") instanceof Map<?, ?>) ? NO_PLAN : aggregatePlan(document, element);
            default -> NO_PLAN;
        };
    }

    private static Plan pairCollection(BinaryDocument document, Schema.Wire pair, boolean segmented) {
        boolean raw = flag(pair.metadata, "rawCollection");
        if (segmented && (!raw || pair.metadata.get("layout") instanceof Map<?, ?>)) return NO_PLAN;
        Plan plan = aggregatePlan(document, pair);
        if (plan != NO_PLAN || !raw) return plan;
        Schema.Wire key = pair.elements.get(0);
        Schema.Wire value = pair.elements.get(1);
        if (key.kind != SCALAR || value.kind != SCALAR || key.type.equals("bool") || value.type.equals("bool") ||
                document.scalarWidth(key.type) != document.scalarWidth(value.type))
            throw fail("Missing raw LuminPack pair storage proof");
        return NO_PLAN;
    }

    private static Plan structPlan(BinaryDocument document, Schema.Contract contract) {
        if (contract.versioned || !contract.kind.equals("Struct")) return NO_PLAN;
        Plan cached = contract.layoutPlan;
        if (cached != null) return cached;
        Plan plan = compileStruct(document, contract);
        contract.layoutPlan = plan;
        return plan;
    }

    private static Plan compileStruct(BinaryDocument document, Schema.Contract contract) {
        if (contract.metadata.get("storage") instanceof Map<?, ?> metadata)
            return compile(document, contract, null, metadata, flag(metadata, "tagged"));
        if (contract.metadata.get("nativeStorage") instanceof Map<?, ?> metadata) {
            Plan plan = compile(document, contract, null, metadata, false);
            int strategy = integer(contract.metadata.get("nativeStrategy"), 2);
            if (plan.models.length < 1 || plan.models.length > 2 || strategy != 2 && plan.models.length != 1)
                throw fail("Invalid untagged LuminPack structure model");
            return plan;
        }
        Object kind = contract.metadata.get("storageKind");
        if ("PortableUnmanaged".equals(kind)) throw fail("Missing portable CLR layout for " + contract.id);
        if (!"RawUnmanaged".equals(kind)) return NO_PLAN;
        Map<?, ?> layout = document.valueLayout(contract.type);
        if (layout == null) throw fail("Missing dense CLR layout for " + contract.id);
        List<?> declared = list(layout.get("fields"));
        if (declared.size() != contract.members.size())
            throw fail("Missing CLR fields for " + contract.id);
        var leaves = new ArrayList<Leaf>(contract.members.size());
        var fields = new String[contract.members.size()];
        Node[] children = new Node[fields.length];
        int width = -1;
        for (int index = 0; index < fields.length; index++) {
            Schema.Field field = contract.members.get(index);
            if (!field.name.equals(declared.get(index)) || field.compressed ||
                    field.wire.kind != SCALAR || field.wire.type.equals("bool"))
                throw fail("Missing a dense LuminPack storage proof for " + contract.id);
            int fieldWidth = document.scalarWidth(field.wire.type);
            if (width < 0) width = fieldWidth;
            else if (width != fieldWidth) throw fail("Missing CLR offsets for " + contract.id);
            fields[index] = field.name;
            children[index] = leaf(field.wire.type, fieldWidth, leaves);
        }
        if (leaves.isEmpty()) throw fail("Empty LuminPack storage for " + contract.id);
        return create(new RecordNode(fields, children), leaves, Math.multiplyExact(width, fields.length),
            List.of(), List.of(), false);
    }

    private static Plan aggregatePlan(BinaryDocument document, Schema.Wire wire) {
        Plan cached = wire.layoutPlan;
        if (cached != null) return cached;
        Plan plan = NO_PLAN;
        if (wire.metadata.get("layout") instanceof Map<?, ?> layout) {
            plan = compile(document, null, wire, layout, flag(layout, "tagged"));
        } else if (wire.metadata.get("rawCompoundStorage") instanceof Map<?, ?> storage) {
            plan = compile(document, null, wire, storage, false);
            if (plan.models.length != 1)
                throw fail("Missing native LuminPack aggregate model");
        } else if (wire.kind == TUPLE && flag(wire.metadata, "raw") && !flag(wire.metadata, "denseTupleStorage")) {
            throw fail("Missing raw LuminPack tuple storage proof");
        }
        wire.layoutPlan = plan;
        return plan;
    }

    private static Plan compile(BinaryDocument document, Schema.Contract contract, Schema.Wire wire,
            Map<?, ?> metadata, boolean tagged) {
        var leaves = new ArrayList<Leaf>();
        Node shape = contract == null ? shape(document, wire, metadata, leaves, 0) :
            record(document, contract, metadata, leaves, 0);
        return create(shape, leaves, integer(metadata.get("wireSize"), -1), list(metadata.get("modelSizes")),
            list(metadata.get("modelOffsets")), tagged);
    }

    private static Node shape(BinaryDocument document, Schema.Wire wire, Map<?, ?> metadata,
            List<Leaf> leaves, int depth) {
        if (depth > 128) throw fail("LuminPack storage plan is nested too deeply");
        List<?> fields = list(metadata.get("fields"));
        return switch (wire.kind) {
            case SCALAR -> {
                int width = document.scalarWidth(wire.type);
                if (!fields.isEmpty() || integer(metadata.get("wireSize"), -1) != width)
                    throw fail("LuminPack scalar and CLR storage width differ");
                yield leaf(wire.type, width, leaves);
            }
            case CONTRACT -> record(document, wire.contract, metadata, leaves, depth + 1);
            case PAIR, TUPLE -> {
                Node[] children = new Node[fields.size()];
                if (children.length != wire.elements.size()) throw fail("LuminPack aggregate storage arity differs");
                for (int index = 0; index < children.length; index++) {
                    Map<?, ?> field = object(fields.get(index));
                    if (flag(field, "volatile") || !aggregateName(wire.kind, index).equals(field.get("name")))
                        throw fail("Invalid LuminPack aggregate storage field");
                    children[index] = shape(document, wire.elements.get(index), object(field.get("storage")), leaves, depth + 1);
                }
                yield new TupleNode(children);
            }
            default -> throw fail("Unsupported LuminPack storage leaf");
        };
    }

    private static Node record(BinaryDocument document, Schema.Contract contract, Map<?, ?> metadata,
            List<Leaf> leaves, int depth) {
        if (!contract.kind.equals("Struct") || contract.versioned) throw fail("Invalid LuminPack storage record");
        List<?> fields = list(metadata.get("fields"));
        if (fields.size() != contract.members.size()) throw fail("LuminPack storage has inaccessible structure fields");
        String[] names = new String[fields.size()];
        Node[] children = new Node[fields.size()];
        for (int index = 0; index < fields.size(); index++) {
            Map<?, ?> row = object(fields.get(index));
            String name = string(row.get("name"));
            Schema.Field member = contract.names.get(name);
            if (member == null || !member.name.equals(name) || member.compressed || flag(row, "volatile"))
                throw fail("Invalid LuminPack storage member " + name);
            for (int previous = 0; previous < index; previous++)
                if (names[previous].equals(name)) throw fail("Duplicate LuminPack storage member " + name);
            names[index] = name;
            children[index] = shape(document, member.wire, object(row.get("storage")), leaves, depth + 1);
        }
        return new RecordNode(names, children);
    }

    private static String aggregateName(int kind, int index) {
        return kind == PAIR ? (index == 0 ? "key" : "value") : (index == 7 ? "Rest" : "Item" + (index + 1));
    }

    private static ScalarNode leaf(String type, int width, List<Leaf> leaves) {
        Leaf leaf = new Leaf(leaves.size(), type, width);
        leaves.add(leaf);
        return new ScalarNode(leaf);
    }

    private static Plan create(Node shape, List<Leaf> leaves, int width, List<?> sizes, List<?> offsets, boolean tagged) {
        if (width < 1 || leaves.isEmpty() || sizes.size() != offsets.size() || sizes.size() > 250 ||
                tagged && sizes.size() < 2)
            throw fail("Invalid LuminPack storage descriptors");
        Leaf[] items = leaves.toArray(Leaf[]::new);
        int[] packed = new int[items.length];
        int next = 0;
        for (int index = 0; index < packed.length; index++) {
            packed[index] = next;
            next = Math.addExact(next, items[index].width);
        }
        if (next != width) throw fail("LuminPack fields do not match their packed width");
        Model[] models = new Model[sizes.size()];
        for (int index = 0; index < sizes.size(); index++)
            models[index] = model(items, width, integer(sizes.get(index), -1), list(offsets.get(index)));
        return new Plan(shape, items, new Model(width, items, packed), models, tagged);
    }

    private static Model model(Leaf[] leaves, int width, int stride, List<?> offsets) {
        if (offsets.size() != width || stride < width)
            throw fail("Invalid LuminPack storage stride");
        int[] result = new int[leaves.length];
        int next = 0;
        for (int index = 0; index < leaves.length; index++) {
            int offset = integer(offsets.get(next), -1);
            int length = leaves[index].width;
            if (offset < 0 || offset > stride - length)
                throw fail("Invalid LuminPack storage offset");
            for (int at = 0; at < length; at++)
                if (integer(offsets.get(next++), -1) != offset + at)
                    throw fail("LuminPack scalar storage is not contiguous");
            result[index] = offset;
        }
        return new Model(stride, leaves, result);
    }

    private static Model readModel(BinaryCursor.Reader input, Model[] models) {
        int tag = input.readU8();
        if (tag == 0 || tag > models.length) throw fail("Unknown LuminPack storage layout tag");
        return models[tag - 1];
    }

    private static Model abiModel(BinaryDocument document, Model[] models) {
        if (models.length == 1 || models.length == 2 && document.scalarWidth("nint") == Long.BYTES)
            return models[0];
        if (models.length == 2) return models[1];
        throw fail("Missing architecture-specific LuminPack storage");
    }

    private static NullablePlan nullablePlan(BinaryDocument document, Schema.Wire wire) {
        int encoding = integer(wire.metadata.get("nullableEncoding"), 0);
        if (encoding == 0) return null;
        NullablePlan cached = wire.nullablePlan;
        if (cached != null) return cached;
        if (encoding < 0 || encoding > 2 || wire.element.kind != SCALAR)
            throw fail("Unsupported LuminPack nullable storage");
        Map<?, ?> layout = object(wire.metadata.get("nullableLayout"));
        List<?> sizes = list(layout.get("modelSizes"));
        List<?> offsets = list(layout.get("modelOffsets"));
        int width = document.scalarWidth(wire.element.type);
        if (sizes.isEmpty() || sizes.size() > 2 || sizes.size() != offsets.size() ||
                integer(layout.get("wireSize"), -1) != width + 1)
            throw fail("Invalid LuminPack nullable storage descriptors");
        var leaves = new Leaf[] {new Leaf(0, "bool", 1), new Leaf(1, wire.element.type, width)};
        Model[] models = new Model[sizes.size()];
        for (int index = 0; index < models.length; index++)
            models[index] = model(leaves, width + 1, integer(sizes.get(index), -1), list(offsets.get(index)));
        boolean nativeElement = flag(wire.metadata, "nativeNullableElement");
        if (nativeElement && encoding == 1 && models.length != 1)
            throw fail("Ambiguous untagged LuminPack nullable storage");
        Model untagged = encoding == 2 ? nullableAbiModel(document, models, width) : models[0];
        NullablePlan plan = new NullablePlan(models, untagged, encoding, nativeElement);
        wire.nullablePlan = plan;
        return plan;
    }

    private static Model nullableAbiModel(BinaryDocument document, Model[] models, int width) {
        // The flag precedes the payload and the selected CLR layout obeys its scalar alignment.
        int alignment = Math.min(width, document.nativeScalarAlignment());
        for (Model model : models) {
            if (model.offsets[0] == 0 && model.offsets[1] == alignment && model.stride == alignment + width)
                return model;
        }
        throw fail("No LuminPack native nullable model matches the requested scalar alignment");
    }

    private static Object readNullable(BinaryDocument document, BinaryCursor.Reader input, Model model) {
        if (model.stride > input.remaining()) throw fail("Truncated LuminPack nullable value");
        int start = input.position();
        Object payload = null;
        boolean present = false;
        for (Leaf leaf : model.order) {
            input.skip(model.offsets[leaf.index] - (input.position() - start));
            if (leaf.index == 0) present = (boolean) document.readScalar(input, "bool");
            else if (present || model.offsets[0] > model.offsets[1] || leaf.type.equals("bool"))
                payload = document.readScalar(input, leaf.type);
            else input.skip(leaf.width);
        }
        input.skip(model.stride - (input.position() - start));
        return present ? payload : null;
    }

    private static void writeNullable(BinaryDocument document, BinaryCursor.Writer output, Model model, Object value) {
        if (value == null) {
            zeroes(output, model.stride);
            return;
        }
        int start = output.position();
        for (Leaf leaf : model.order) {
            zeroes(output, model.offsets[leaf.index] - (output.position() - start));
            if (leaf.index == 0) output.writeU8(1);
            else document.writeScalar(output, leaf.type, value);
        }
        zeroes(output, model.stride - (output.position() - start));
    }

    private static Object readValue(BinaryCursor.Reader input, Plan plan, Model model) {
        if (model.stride > input.remaining()) throw fail("Truncated LuminPack storage value");
        if (model.sequential) return plan.shape.readSequential(input);
        Slot slot = readSlot(input, plan, model);
        return plan.shape.read(slot.values, slot.padding);
    }

    private static Slot readSlot(BinaryCursor.Reader input, Plan plan, Model model) {
        if (model.stride > input.remaining()) throw fail("Truncated LuminPack storage value");
        Object[] values = new Object[plan.leaves.length];
        int start = input.position();
        byte[] padding = null;
        boolean retain = plan.retainPadding;
        for (Leaf leaf : model.order) {
            int offset = model.offsets[leaf.index];
            if (retain) padding = readPadding(input, start, offset, model.stride, padding);
            else input.skip(offset - (input.position() - start));
            values[leaf.index] = BinaryDocument.readScalar(input, leaf.type, leaf.width);
        }
        if (retain) padding = readPadding(input, start, model.stride, model.stride, padding);
        else input.skip(model.stride - (input.position() - start));
        return new Slot(values, padding == null ? null : new Padding(model, padding));
    }

    private static byte[] readPadding(BinaryCursor.Reader input, int start, int end, int stride, byte[] padding) {
        for (int offset = input.position() - start; offset < end; offset++) {
            int value = input.readU8();
            if (value != 0) {
                if (padding == null) padding = new byte[stride];
                padding[offset] = (byte) value;
            }
        }
        return padding;
    }

    private static void writeValue(BinaryDocument document, BinaryCursor.Writer output, Plan plan, Model model,
            Object value) {
        if (model.sequential) {
            plan.shape.writeSequential(document, output, value);
            return;
        }
        Object[] values = new Object[plan.leaves.length];
        plan.shape.collect(value, values);
        int start = output.position();
        Padding padding = plan.retainPadding && value instanceof Padded padded ? padded.padding() : null;
        byte[] retained = padding != null && model.sameOffsets(padding.model) ? padding.bytes : null;
        for (Leaf leaf : model.order) {
            writePadding(output, output.position() - start, model.offsets[leaf.index], retained);
            document.writeScalar(output, leaf.type, values[leaf.index]);
        }
        writePadding(output, output.position() - start, model.stride, retained);
    }

    private static void writePadding(BinaryCursor.Writer output, int start, int end, byte[] retained) {
        if (retained == null) zeroes(output, end - start);
        else for (int offset = start; offset < end; offset++) output.writeU8(retained[offset]);
    }

    private static void zeroes(BinaryCursor.Writer output, int count) {
        while (count >= Long.BYTES) {
            output.writeI64(0);
            count -= Long.BYTES;
        }
        while (count-- > 0) output.writeU8(0);
    }

    private static List<?> aggregate(Object value, int count) {
        List<?> values = Schema.array(value);
        if (values.size() != count) throw fail("Unexpected LuminPack tuple width");
        return values;
    }

    private record Leaf(int index, String type, int width) {}

    private static final class Model {
        final int stride;
        final int[] offsets;
        final Leaf[] order;
        final int[] booleanOffsets;
        final boolean sequential;

        Model(int stride, Leaf[] leaves, int[] offsets) {
            this.stride = stride;
            this.offsets = offsets;
            order = leaves.clone();
            Arrays.sort(order, Comparator.comparingInt(leaf -> offsets[leaf.index]));
            booleanOffsets = Arrays.stream(order).filter(leaf -> leaf.type.equals("bool"))
                .mapToInt(leaf -> offsets[leaf.index]).toArray();
            int end = 0;
            int next = 0;
            boolean dense = true;
            for (Leaf leaf : order) {
                if (offsets[leaf.index] < end) throw fail("Overlapping LuminPack storage fields");
                dense &= leaf.index == next && offsets[leaf.index] == end;
                end = offsets[leaf.index] + leaf.width;
                next++;
            }
            sequential = dense && end == stride;
        }

        boolean sameOffsets(Model other) {
            return other == this || stride == other.stride && Arrays.equals(offsets, other.offsets);
        }

        void validateBooleans(byte[] source, int count) {
            for (int offset : booleanOffsets) {
                for (int row = 0, position = offset; row < count; row++, position += stride) {
                    if ((source[position] & ~1) != 0) throw fail("Invalid LuminPack Boolean marker");
                }
            }
        }
    }

    static final class Plan {
        final Node shape;
        final Leaf[] leaves;
        final int[] widths;
        final Model packed;
        final Model[] models;
        final boolean tagged;
        final boolean retainPadding;

        Plan(Node shape, Leaf[] leaves, Model packed, Model[] models, boolean tagged) {
            this.shape = shape;
            this.leaves = leaves;
            widths = Arrays.stream(leaves).mapToInt(Leaf::width).toArray();
            this.packed = packed;
            this.models = models;
            this.tagged = tagged;
            retainPadding = !tagged || shape instanceof RecordNode;
        }

        Model standalone(BinaryDocument document) {
            return tagged || models.length == 0 ? packed : abiModel(document, models);
        }

        Model selectRead(BinaryDocument document, BinaryCursor.Reader input) {
            return tagged ? readModel(input, models) : standalone(document);
        }

        Model selectWrite(BinaryDocument document, BinaryCursor.Writer output) {
            if (!tagged) return standalone(document);
            // Java has no CLR-compatible object stride. Use the canonical first model.
            output.writeU8(1);
            return models[0];
        }
    }

    /** Owned storage snapshot. A row is materialized once because its maps and tuples are writable. */
    private static final class RawRows extends AbstractList<Object> implements RandomAccess {
        private final Plan plan;
        private final Model model;
        private final int count;
        private byte[] storage;
        private Object[] accessed;
        private ArrayList<Object> elements;

        RawRows(Plan plan, Model model, int count, byte[] storage) {
            this.plan = plan;
            this.model = model;
            this.count = count;
            this.storage = storage;
        }

        @Override public int size() { return elements == null ? count : elements.size(); }

        @Override public Object get(int index) {
            if (elements != null) return elements.get(index);
            Objects.checkIndex(index, count);
            if (accessed == null) accessed = new Object[count];
            Object value = accessed[index];
            if (value == null) {
                var cursor = BinaryCursor.Reader.window(storage, index * model.stride, model.stride);
                value = readValue(cursor, plan, model);
                accessed[index] = value;
            }
            return value;
        }

        boolean write(BinaryDocument document, BinaryCursor.Writer output, Plan required, Model target) {
            if (required != plan || storage == null) return false;
            if (accessed == null) {
                writeRun(output, target, 0, count);
                return true;
            }
            int start = 0;
            for (int row = 0; row < count; row++) {
                Object value = accessed[row];
                if (value == null) continue;
                if (start != row) writeRun(output, target, start, row - start);
                writeValue(document, output, plan, target, value);
                start = row + 1;
            }
            if (start < count) writeRun(output, target, start, count - start);
            return true;
        }

        private void writeRun(BinaryCursor.Writer output, Model target, int start, int length) {
            int offset = start * model.stride;
            if (target.sameOffsets(model) && (plan.retainPadding || model.stride == plan.packed.stride))
                output.writeBytes(storage, offset, length * model.stride);
            else
                output.writeModeledRows(storage, offset, length, model.stride, target.stride,
                    model.offsets, target.offsets, plan.widths);
        }

        @Override public Object set(int index, Object value) { return writable().set(index, value); }

        @Override public void add(int index, Object value) {
            writable().add(index, value);
            modCount++;
        }

        @Override public boolean addAll(int index, Collection<?> values) {
            if (!writable().addAll(index, values)) return false;
            modCount++;
            return true;
        }

        @Override public Object remove(int index) {
            Object previous = writable().remove(index);
            modCount++;
            return previous;
        }

        @Override protected void removeRange(int fromIndex, int toIndex) {
            writable().subList(fromIndex, toIndex).clear();
            modCount++;
        }

        private ArrayList<Object> writable() {
            if (elements != null) return elements;
            var result = new ArrayList<Object>(count);
            for (int index = 0; index < count; index++) result.add(get(index));
            elements = result;
            accessed = null;
            storage = null;
            return result;
        }
    }

    static final class NullablePlan {
        final Model[] models;
        final Model untagged;
        final int encoding;
        final boolean nativeElement;

        NullablePlan(Model[] models, Model untagged, int encoding, boolean nativeElement) {
            this.models = models;
            this.untagged = untagged;
            this.encoding = encoding;
            this.nativeElement = nativeElement;
        }

        Model selectRead(BinaryCursor.Reader input, boolean segmented) {
            if (encoding == 2 || segmented) return untagged;
            return models.length == 1 ? models[0] : readModel(input, models);
        }

        Model selectWrite(BinaryCursor.Writer output, boolean segmented) {
            if (encoding == 2 || segmented) return untagged;
            if (models.length > 1) output.writeU8(1);
            return models[0];
        }
    }

    private record Slot(Object[] values, Padding padding) {}
    private record Padding(Model model, byte[] bytes) {}
    private interface Padded { Padding padding(); }

    private static final class PaddedRecord extends LinkedHashMap<String, Object> implements Padded {
        private static final long serialVersionUID = 1L;
        private final transient Padding padding;
        PaddedRecord(int capacity, Padding padding) { super(capacity); this.padding = padding; }
        public Padding padding() { return padding; }
    }

    private static final class PaddedTuple extends ArrayList<Object> implements Padded {
        private static final long serialVersionUID = 1L;
        private final transient Padding padding;
        PaddedTuple(int capacity, Padding padding) { super(capacity); this.padding = padding; }
        public Padding padding() { return padding; }
    }

    private interface Node {
        Object read(Object[] values, Padding padding);
        void collect(Object value, Object[] values);
        Object readSequential(BinaryCursor.Reader input);
        void writeSequential(BinaryDocument document, BinaryCursor.Writer output, Object value);

    }

    private record ScalarNode(Leaf leaf) implements Node {
        public Object read(Object[] values, Padding padding) { return values[leaf.index]; }
        public void collect(Object value, Object[] values) { values[leaf.index] = value; }
        public Object readSequential(BinaryCursor.Reader input) {
            return BinaryDocument.readScalar(input, leaf.type, leaf.width);
        }
        public void writeSequential(BinaryDocument document, BinaryCursor.Writer output, Object value) {
            document.writeScalar(output, leaf.type, value);
        }
    }

    private record RecordNode(String[] names, Node[] children) implements Node {
        public Object read(Object[] values, Padding padding) {
            Map<String, Object> value = padding == null ? new LinkedHashMap<>(names.length) :
                new PaddedRecord(names.length, padding);
            readInto(value, values);
            return value;
        }

        private void readInto(Map<String, Object> value, Object[] values) {
            for (int index = 0; index < names.length; index++)
                value.put(names[index], children[index].read(values, null));
        }

        public void collect(Object value, Object[] values) {
            Map<?, ?> fields = object(value);
            for (int index = 0; index < names.length; index++)
                children[index].collect(fields.get(names[index]), values);
        }

        public Object readSequential(BinaryCursor.Reader input) {
            Map<String, Object> value = new LinkedHashMap<>(names.length);
            for (int index = 0; index < names.length; index++)
                value.put(names[index], children[index].readSequential(input));
            return value;
        }

        public void writeSequential(BinaryDocument document, BinaryCursor.Writer output, Object value) {
            Map<?, ?> fields = object(value);
            for (int index = 0; index < names.length; index++)
                children[index].writeSequential(document, output, fields.get(names[index]));
        }
    }

    private record TupleNode(Node[] children) implements Node {
        public Object read(Object[] values, Padding padding) {
            List<Object> value = padding == null ? new ArrayList<>(children.length) :
                new PaddedTuple(children.length, padding);
            for (Node child : children) value.add(child.read(values, null));
            return value;
        }

        public void collect(Object value, Object[] values) {
            List<?> items = aggregate(value, children.length);
            for (int index = 0; index < children.length; index++) children[index].collect(items.get(index), values);
        }

        public Object readSequential(BinaryCursor.Reader input) {
            List<Object> value = new ArrayList<>(children.length);
            for (Node child : children) value.add(child.readSequential(input));
            return value;
        }

        public void writeSequential(BinaryDocument document, BinaryCursor.Writer output, Object value) {
            List<?> items = aggregate(value, children.length);
            for (int index = 0; index < children.length; index++)
                children[index].writeSequential(document, output, items.get(index));
        }
    }
}
