using System.Runtime.InteropServices;

namespace LuminPack.Utility;

using System.Collections;
using System.Runtime.CompilerServices;


/// <summary>
/// Two-table Cuckoo Hash: open addressing + cuckoo kicking, O(1) read/write, zero chaining, ref returns and auto-resize.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public sealed class LuminCircleReferenceMap<TKey, TValue> : IDisposable, IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    private const int MAX_KICK_COUNT = 16;
    private const int MIN_CAPACITY = 8;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Entry
    {
        public uint HashCode;
        public TKey Key;
        public TValue Value;
        public bool IsOccupied;
    }

    private Entry[] _table1;
    private Entry[] _table2;
    private int _capacity;
    private int _count;
    private int _capacityMask;
    private int _version;
    private readonly IEqualityComparer<TKey> _keyComparer;
    private readonly IEqualityComparer<TValue> _valueComparer;

    public int Count => _count;
    public int Capacity => _capacity;
    public bool IsCreated => _table1 != null;
    public IEqualityComparer<TKey> KeyComparer => _keyComparer;
    public IEqualityComparer<TValue> ValueComparer => _valueComparer;

    public ref TValue this[in TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var hashCode = GetHashCode(key);
                
            var index1 = hashCode & _capacityMask;
            ref Entry entry1 = ref _table1[index1];
            if (entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
                return ref entry1.Value;
                
            var index2 = (hashCode >> 8) & _capacityMask;
            ref Entry entry2 = ref _table2[index2];
            if (entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
                return ref entry2.Value;
            throw new KeyNotFoundException();
        }
    }

    public LuminCircleReferenceMap(int capacity, IEqualityComparer<TKey> keyComparer = null, IEqualityComparer<TValue> valueComparer = null)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
        _capacity = CalculateCapacity(capacity);
        _count = 0;
        _version = 0;
        InitializeTables();
    }

    public LuminCircleReferenceMap() : this(4) { }

    public LuminCircleReferenceMap(IEqualityComparer<TKey> keyComparer) : this(4, keyComparer) { }

    public LuminCircleReferenceMap(in LuminCircleReferenceMap<TKey, TValue> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
            
        _keyComparer = source._keyComparer;
        _valueComparer = source._valueComparer;
        _capacity = source._capacity;
        _count = source._count;
        _capacityMask = source._capacityMask;
        _version = source._version;
        _table1 = new Entry[_capacity];
        _table2 = new Entry[_capacity];
            
        Array.Copy(source._table1, _table1, _capacity);
        Array.Copy(source._table2, _table2, _capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetHashCode(in TKey key)
    {
        var hashCode = _keyComparer.GetHashCode(key);
        hashCode ^= hashCode >> 16;
        return hashCode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateCapacity(int capacity)
    {
        if (capacity < MIN_CAPACITY) return MIN_CAPACITY;
        capacity--;
        capacity |= capacity >> 1;
        capacity |= capacity >> 2;
        capacity |= capacity >> 4;
        capacity |= capacity >> 8;
        capacity |= capacity >> 16;
        return capacity + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeTables()
    {
        _capacityMask = _capacity - 1;
        _table1 = new Entry[_capacity];
        _table2 = new Entry[_capacity];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in TKey key, in TValue value)
    {
        if (!TryAdd(key, value))
            throw new ArgumentException("An item with the same key has already been added.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(in TKey key, in TValue value)
    {
        var hashCode = GetHashCode(key);
            
        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (!entry1.IsOccupied)
        {
            entry1.HashCode = (uint)hashCode;
            entry1.Key = key;
            entry1.Value = value;
            entry1.IsOccupied = true;
            _count++;
            _version++;
            return true;
        }
        else if (entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
            return false;
            
        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        if (!entry2.IsOccupied)
        {
            entry2.HashCode = (uint)hashCode;
            entry2.Key = key;
            entry2.Value = value;
            entry2.IsOccupied = true;
            _count++;
            _version++;
            return true;
        }
        else if (entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
            return false;
            
        bool res = CuckooInsert(hashCode, key, value);
        if (res) _version++;
        return res;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CuckooInsert(int hashCode, in TKey key, in TValue value)
    {
        int currentHashCode = hashCode;
        TKey currentKey = key;
        TValue currentValue = value;
        int tableIndex = 0;
            
        for (int i = 0; i < MAX_KICK_COUNT; i++)
        {
            if (tableIndex == 0)
            {
                var index = currentHashCode & _capacityMask;
                ref Entry entry = ref _table1[index];
                if (!entry.IsOccupied)
                {
                    entry.HashCode = (uint)currentHashCode;
                    entry.Key = currentKey;
                    entry.Value = currentValue;
                    entry.IsOccupied = true;
                    _count++;
                    return true;
                }
                    
                (currentHashCode, entry.HashCode) = ((int)entry.HashCode, (uint)currentHashCode);
                (currentKey, entry.Key) = (entry.Key, currentKey);
                (currentValue, entry.Value) = (entry.Value, currentValue);
                tableIndex = 1;
            }
            else
            {
                var index = (currentHashCode >> 8) & _capacityMask;
                ref Entry entry = ref _table2[index];
                if (!entry.IsOccupied)
                {
                    entry.HashCode = (uint)currentHashCode;
                    entry.Key = currentKey;
                    entry.Value = currentValue;
                    entry.IsOccupied = true;
                    _count++;
                    return true;
                }
                    
                (currentHashCode, entry.HashCode) = ((int)entry.HashCode, (uint)currentHashCode);
                (currentKey, entry.Key) = (entry.Key, currentKey);
                (currentValue, entry.Value) = (entry.Value, currentValue);
                tableIndex = 0;
            }
        }
        Resize();
        return TryAdd(currentKey, currentValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRefOrAddDefault(TKey key, out bool exists)
    {
        var hashCode = GetHashCode(key);
            
        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
        {
            exists = true;
            return ref entry1.Value;
        }
        var index2 = (hashCode >> 8) & _capacityMask;
            
        ref Entry entry2 = ref _table2[index2];
        if (entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
        {
            exists = true;
            return ref entry2.Value;
        }
        exists = false;
            
        if (!entry1.IsOccupied)
        {
            entry1.HashCode = (uint)hashCode;
            entry1.Key = key;
            entry1.Value = default;
            entry1.IsOccupied = true;
            _count++;
            _version++;
            return ref entry1.Value;
        }
        else if (!entry2.IsOccupied)
        {
            entry2.HashCode = (uint)hashCode;
            entry2.Key = key;
            entry2.Value = default;
            entry2.IsOccupied = true;
            _count++;
            _version++;
            return ref entry2.Value;
        }
        else
        {
            if (!TryAdd(key, default))
                throw new InvalidOperationException("Failed to add key to dictionary");
            return ref GetValueRefOrAddDefault(key, out _);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in TKey key, out TValue value)
    {
        var hashCode = GetHashCode(key);
            
        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
        {
            value = entry1.Value;
            return true;
        }
            
        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        if (entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
        {
            value = entry2.Value;
            return true;
        }
        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in TKey key)
    {
        var hashCode = GetHashCode(key);
            
        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
            return ref entry1.Value;
            
        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        if (entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
            return ref entry2.Value;
            
        throw new KeyNotFoundException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(in TKey key)
    {
        var hashCode = GetHashCode(key);
            
        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
            return true;
            
        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        return entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsValue(in TValue value)
    {
        for (int i = 0; i < _capacity; i++)
        {
            ref Entry entry1 = ref _table1[i];
            if (entry1.IsOccupied && _valueComparer.Equals(entry1.Value, value))
                return true;
                
            ref Entry entry2 = ref _table2[i];
            if (entry2.IsOccupied && _valueComparer.Equals(entry2.Value, value))
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key)
    {
        var hashCode = GetHashCode(key);
            
        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
        {
            entry1.IsOccupied = false;
            _count--;
            _version++;
            return true;
        }
            
        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        if (entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
        {
            entry2.IsOccupied = false;
            _count--;
            _version++;
            return true;
        }
            
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_count > 0)
        {
            Array.Clear(_table1, 0, _capacity);
            Array.Clear(_table2, 0, _capacity);
            _count = 0;
            _version++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (capacity > _capacity)
            Resize(CalculateCapacity(capacity));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess()
    {
        int newCapacity = CalculateCapacity(_count);
            
        if (newCapacity < _capacity)
            Resize(newCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        _table1 = null;
        _table2 = null;
        _count = 0;
        _capacity = 0;
        _capacityMask = 0;
        _version = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize() => Resize(_capacity * 2);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize(int newCapacity)
    {
        Entry[] oldTable1 = _table1;
        Entry[] oldTable2 = _table2;
        int oldCapacity = _capacity;
        int oldCount = _count;
        _capacity = newCapacity;
        _count = 0;
        InitializeTables();
        if (oldCount > 0)
        {
            for (int i = 0; i < oldCapacity; i++)
            {
                ref Entry oldEntry1 = ref oldTable1[i];
                if (oldEntry1.IsOccupied)
                    if (!TryAdd(oldEntry1.Key, oldEntry1.Value))
                        throw new InvalidOperationException("Failed to rehash during resize");
                ref Entry oldEntry2 = ref oldTable2[i];
                if (oldEntry2.IsOccupied)
                    if (!TryAdd(oldEntry2.Key, oldEntry2.Value))
                        throw new InvalidOperationException("Failed to rehash during resize");
            }
        }
    }

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly LuminCircleReferenceMap<TKey, TValue> _dict;
        private int _index;
        private int _table;
        private KeyValuePair<TKey, TValue> _current;
        private readonly int _version;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(LuminCircleReferenceMap<TKey, TValue> circleReferenceMap)
        {
            _dict = circleReferenceMap;
            _index = -1;
            _table = 0;
            _current = default;
            _version = circleReferenceMap._version;
        }

        public KeyValuePair<TKey, TValue> Current => _current;
        object IEnumerator.Current => Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified");
            while (_table < 2)
            {
                var table = _table == 0 ? _dict._table1 : _dict._table2;
                while (++_index < _dict._capacity)
                {
                    ref Entry entry = ref table[_index];
                    if (entry.IsOccupied)
                    {
                        _current = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
                        return true;
                    }
                }
                _table++;
                _index = -1;
            }
            _current = default;
            return false;
        }

        public void Reset()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified");
            _index = -1;
            _table = 0;
            _current = default;
        }

        public void Dispose() { }
    }
}