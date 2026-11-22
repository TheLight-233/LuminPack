package luminpack;

import java.lang.invoke.MethodHandles;
import java.lang.invoke.VarHandle;
import java.math.BigInteger;
import java.nio.ByteOrder;
import java.util.AbstractList;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Objects;
import java.util.RandomAccess;

/** A checked little-endian block with lazy boxing and ordinary writable List behavior. */
final class ScalarBlock extends AbstractList<Object> implements RandomAccess {
    private static final VarHandle INT16 = MethodHandles.byteArrayViewVarHandle(short[].class, ByteOrder.LITTLE_ENDIAN);
    private static final VarHandle INT32 = MethodHandles.byteArrayViewVarHandle(int[].class, ByteOrder.LITTLE_ENDIAN);
    private static final VarHandle INT64 = MethodHandles.byteArrayViewVarHandle(long[].class, ByteOrder.LITTLE_ENDIAN);

    private final String type;
    private final int width;
    private byte[] storage;
    private ArrayList<Object> elements;

    ScalarBlock(String type, int width, byte[] storage) {
        this.type = type;
        this.width = width;
        this.storage = storage;
    }

    static boolean supported(Schema.Wire wire) {
        return wire.kind == Schema.Wire.SCALAR && !wire.type.equals("bool");
    }

    byte[] raw(String requiredType, int requiredWidth) {
        return width == requiredWidth && type.equals(requiredType) ? storage : null;
    }

    @Override public int size() { return elements == null ? storage.length / width : elements.size(); }

    @Override public Object get(int index) {
        if (elements != null) return elements.get(index);
        Objects.checkIndex(index, size());
        int offset = index * width;
        return switch (type) {
            case "byte" -> storage[offset] & 255;
            case "sbyte" -> (int) storage[offset];
            case "short" -> (int) (short) INT16.get(storage, offset);
            case "ushort" -> (short) INT16.get(storage, offset) & 65535;
            case "char" -> (char) (short) INT16.get(storage, offset);
            case "int" -> (int) INT32.get(storage, offset);
            case "uint" -> Integer.toUnsignedLong((int) INT32.get(storage, offset));
            case "long" -> (long) INT64.get(storage, offset);
            case "ulong" -> BinaryDocument.unsigned64((long) INT64.get(storage, offset));
            case "nint" -> width == Long.BYTES ? (long) INT64.get(storage, offset) : (int) INT32.get(storage, offset);
            case "nuint" -> width == Long.BYTES ? BinaryDocument.unsigned64((long) INT64.get(storage, offset)) :
                BigInteger.valueOf(Integer.toUnsignedLong((int) INT32.get(storage, offset)));
            case "float" -> Float.intBitsToFloat((int) INT32.get(storage, offset));
            case "double" -> Double.longBitsToDouble((long) INT64.get(storage, offset));
            default -> throw new IllegalStateException("Unsupported LuminPack scalar block.");
        };
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
        int size = size();
        var result = new ArrayList<Object>(size);
        for (int index = 0; index < size; index++) result.add(get(index));
        elements = result;
        storage = null;
        return result;
    }
}
