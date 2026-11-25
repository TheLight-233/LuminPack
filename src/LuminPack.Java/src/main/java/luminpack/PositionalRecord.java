package luminpack;

import java.util.AbstractMap;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;

/** A fully populated record. The schema owns field names once for all instances. */
final class PositionalRecord extends AbstractMap<String, Object> {
    static final class Fields {
        final String[] names;
        private final Map<String, Integer> positions;

        Fields(String[] names) {
            this.names = names;
            positions = names.length > 8 ? new HashMap<>() : null;
            if (positions != null)
                for (int i = 0; i < names.length; i++) positions.put(names[i], i);
        }

        int index(Object key) {
            if (positions != null) return positions.getOrDefault(key, -1);
            for (int i = 0; i < names.length; i++)
                if (names[i].equals(key)) return i;
            return -1;
        }
    }

    private final Fields fields;
    private volatile Object storage;

    PositionalRecord(Fields fields, Object[] values) {
        this.fields = fields;
        storage = values;
    }

    static Object[] slots(Map<?, ?> map, Fields fields) {
        if (map instanceof PositionalRecord record && record.fields == fields && record.storage instanceof Object[] values)
            return values;
        return null;
    }

    @Override public Object get(Object key) {
        Object current = storage;
        if (current instanceof Map<?, ?> map) return map.get(key);
        int index = fields.index(key);
        return index < 0 ? null : ((Object[]) current)[index];
    }

    @Override public boolean containsKey(Object key) {
        Object current = storage;
        return current instanceof Map<?, ?> map ? map.containsKey(key) : fields.index(key) >= 0;
    }

    @Override public int size() {
        Object current = storage;
        return current instanceof Map<?, ?> map ? map.size() : fields.names.length;
    }

    // LinkedHashMap owns mutations and live views after the first structural operation.
    @SuppressWarnings("unchecked")
    private synchronized Map<String, Object> mutable() {
        if (storage instanceof Map<?, ?> map) return (Map<String, Object>) map;
        Object[] values = (Object[]) storage;
        var map = new LinkedHashMap<String, Object>(values.length * 4 / 3 + 1);
        for (int i = 0; i < values.length; i++) map.put(fields.names[i], values[i]);
        storage = map;
        return map;
    }

    @Override public Set<Entry<String, Object>> entrySet() { return mutable().entrySet(); }
    @Override public Object put(String key, Object value) { return mutable().put(key, value); }
    @Override public Object remove(Object key) { return mutable().remove(key); }
    @Override public void clear() { mutable().clear(); }
}
