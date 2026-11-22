package luminpack;

import static luminpack.Schema.fail;

import java.util.ArrayList;
import java.util.List;

/** LUM2 framing for scalar, raw-storage and jagged array fields. */
final class CompressedField {
    private CompressedField() {}

    static Object read(BinaryDocument document, BinaryCursor.Reader input, Schema.Wire wire, int profile, int depth) {
        if (wire.kind != Schema.Wire.SEQUENCE) throw fail("LuminPack compression requires a sequence field");
        Schema.Wire element = wire.element;
        if (element.kind == Schema.Wire.SEQUENCE) {
            int count = input.readCollectionCount(Integer.BYTES);
            if (count < 0) return null;
            var rows = new ArrayList<Object>(Math.min(count, 256));
            for (int index = 0; index < count; index++) rows.add(read(document, input, element, profile, depth + 1));
            return rows;
        }

        int count = input.readCollectionCount(0);
        if (count < 0) return null;
        if (count == 0) return new ArrayList<>();
        int bytes = expectedBytes(document, element, count);
        int size = input.readI32();
        byte[] raw = Compression.decompress(input.take(size), bytes);
        if (ScalarBlock.supported(element)) return new ScalarBlock(element.type, document.scalarWidth(element.type), raw);
        var payload = new BinaryCursor.Reader(raw);
        var result = new ArrayList<Object>(Math.min(count, 256));
        for (int index = 0; index < count; index++) result.add(document.readWire(payload, element, profile, depth));
        payload.requireEnd();
        return result;
    }

    static void write(BinaryDocument document, BinaryCursor.Writer output, Schema.Wire wire, Object value, int profile, int depth) {
        if (wire.kind != Schema.Wire.SEQUENCE) throw fail("LuminPack compression requires a sequence field");
        if (value == null) { output.writeI32(-1); return; }
        Schema.Wire element = wire.element;
        List<?> values = Schema.array(value);
        output.writeI32(values.size());
        if (values.isEmpty()) return;
        if (element.kind == Schema.Wire.SEQUENCE) {
            for (Object row : values) write(document, output, element, row, profile, depth + 1);
            return;
        }
        int bytes = expectedBytes(document, element, values.size());
        byte[] raw = values instanceof ScalarBlock block && ScalarBlock.supported(element)
            ? block.raw(element.type, document.scalarWidth(element.type)) : null;
        if (raw == null) {
            var cursor = new BinaryCursor.Writer(bytes);
            for (Object item : values) document.writeWire(cursor, element, item, profile, depth);
            if (cursor.position() != bytes) throw fail("Unexpected LuminPack compressed element width");
            raw = cursor.toByteArray();
        }
        byte[] frame = Compression.compress(raw);
        output.writeI32(frame.length);
        output.writeBytes(frame);
    }

    private static int expectedBytes(BinaryDocument document, Schema.Wire element, int count) {
        int width = Layout.rawWidth(document, element);
        if (width <= 0 || count > Integer.MAX_VALUE / width) throw fail("Invalid LuminPack raw compression layout or size");
        return count * width;
    }
}
