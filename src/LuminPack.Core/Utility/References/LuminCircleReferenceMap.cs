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

    /// <summary>Provides entry functionality.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Entry
    {
        /// <summary>Stores the hash code.</summary>
        public uint HashCode;
        /// <summary>Stores the key.</summary>
        public TKey Key;
        /// <summary>Stores the value.</summary>
        public TValue Value;
        /// <summary>Stores the is occupied.</summary>
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

    /// <summary>Gets the count.</summary>
    public int Count => _count;
    /// <summary>Gets the capacity.</summary>
    public int Capacity => _capacity;
    /// <summary>Gets the is created.</summary>
    public bool IsCreated => _table1 != null;
    /// <summary>Gets the key comparer.</summary>
    public IEqualityComparer<TKey> KeyComparer => _keyComparer;
    /// <summary>Gets the value comparer.</summary>
    public IEqualityComparer<TValue> ValueComparer => _valueComparer;

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    public ref TValue this[in TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var hashCode = GetHashCode(key);

            var index1 = hashCode & _capacityMask;
            ref Entry entry1 = ref _table1[index1];
            if (entry1.IsOccupied && entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
                return ref entry1.Value;

            var index2 = (hashCode >> 8) & _capacityMask;
            ref Entry entry2 = ref _table2[index2];
            if (entry2.IsOccupied && entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
                return ref entry2.Value;
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowKeyNotFoundException();
            return ref Unsafe.NullRef<TValue>();
        }
    }

    /// <summary>Initializes a new instance of <c>LuminCircleReferenceMap</c>.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    /// <param name="keyComparer">The key comparer used by this operation.</param>
    /// <param name="valueComparer">The value comparer used by this operation.</param>
    public LuminCircleReferenceMap(int capacity, IEqualityComparer<TKey> keyComparer = null, IEqualityComparer<TValue> valueComparer = null)
    {
        if (capacity < 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(capacity));
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
        _capacity = CalculateCapacity(capacity);
        _count = 0;
        _version = 0;
        InitializeTables();
    }

    /// <summary>Initializes a new instance of <c>LuminCircleReferenceMap</c>.</summary>
    public LuminCircleReferenceMap() : this(4) { }

    /// <summary>Initializes a new instance of <c>LuminCircleReferenceMap</c>.</summary>
    /// <param name="keyComparer">The key comparer used by this operation.</param>
    public LuminCircleReferenceMap(IEqualityComparer<TKey> keyComparer) : this(4, keyComparer) { }

    /// <summary>Initializes a new instance of <c>LuminCircleReferenceMap</c>.</summary>
    /// <param name="source">The input data to process.</param>
    public LuminCircleReferenceMap(in LuminCircleReferenceMap<TKey, TValue> source)
    {
        if (source == null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(source));

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

    /// <summary>Computes the hash code used by the configured lookup or equality strategy.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetHashCode(in TKey key)
    {
        var hashCode = _keyComparer.GetHashCode(key);
        hashCode ^= hashCode >> 16;
        return hashCode;
    }

    /// <summary>Calculates the serialized size contribution of capacity.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Performs the initialize tables step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeTables()
    {
        _capacityMask = _capacity - 1;
        _table1 = new Entry[_capacity];
        _table2 = new Entry[_capacity];
    }

    /// <summary>Adds the supplied entry to the collection.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in TKey key, in TValue value)
    {
        if (!TryAdd(key, value))
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentException("An item with the same key has already been added.");
    }

    /// <summary>Adds the supplied entry to the collection.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(in TKey key, in TValue value)
    {
        var hashCode = GetHashCode(key);

        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (!entry1.IsOccupied)
        {
            ref Entry other = ref _table2[(hashCode >> 8) & _capacityMask];
            if (other.IsOccupied && other.HashCode == hashCode && _keyComparer.Equals(other.Key, key)) return false;
            entry1.HashCode = (uint)hashCode;
            entry1.Key = key;
            entry1.Value = value;
            entry1.IsOccupied = true;
            _count++;
            _version++;
            return true;
        }
        else if (entry1.IsOccupied && entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
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
        else if (entry2.IsOccupied && entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
            return false;

        bool res = CuckooInsert(hashCode, key, value);
        if (res) _version++;
        return res;
    }

    /// <summary>Performs the cuckoo insert step for the current operation.</summary>
    /// <param name="hashCode">The hash code used by this operation.</param>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
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

    /// <summary>Obtains the value ref or add default for the supplied input.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="exists">Receives the exists produced by the operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRefOrAddDefault(TKey key, out bool exists)
    {
        var hashCode = GetHashCode(key);

        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.IsOccupied && entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
        {
            exists = true;
            return ref entry1.Value;
        }
        var index2 = (hashCode >> 8) & _capacityMask;

        ref Entry entry2 = ref _table2[index2];
        if (entry2.IsOccupied && entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
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
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Failed to add key to dictionary");
            return ref GetValueRefOrAddDefault(key, out _);
        }
    }

    /// <summary>Looks up a key and reports whether an associated value was found.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in TKey key, out TValue value)
    {
        var hashCode = GetHashCode(key);

        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.IsOccupied && entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
        {
            value = entry1.Value;
            return true;
        }

        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        if (entry2.IsOccupied && entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
        {
            value = entry2.Value;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>Obtains the value ref for the supplied input.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in TKey key)
    {
        var hashCode = GetHashCode(key);

        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.IsOccupied && entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
            return ref entry1.Value;

        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        if (entry2.IsOccupied && entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
            return ref entry2.Value;

        global::LuminPack.Code.LuminPackExceptionHelper.ThrowKeyNotFoundException();
        return ref Unsafe.NullRef<TValue>();
    }

    /// <summary>Determines whether the collection contains the supplied key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(in TKey key)
    {
        var hashCode = GetHashCode(key);

        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.IsOccupied && entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
            return true;

        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        return entry2.IsOccupied && entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key);
    }

    /// <summary>Determines whether the collection contains the supplied value.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
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

    /// <summary>Removes the entry matching the supplied key or value.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key)
    {
        var hashCode = GetHashCode(key);

        var index1 = hashCode & _capacityMask;
        ref Entry entry1 = ref _table1[index1];
        if (entry1.IsOccupied && entry1.HashCode == hashCode && _keyComparer.Equals(entry1.Key, key))
        {
            entry1 = default;
            _count--;
            _version++;
            return true;
        }

        var index2 = (hashCode >> 8) & _capacityMask;
        ref Entry entry2 = ref _table2[index2];
        if (entry2.IsOccupied && entry2.HashCode == hashCode && _keyComparer.Equals(entry2.Key, key))
        {
            entry2 = default;
            _count--;
            _version++;
            return true;
        }

        return false;
    }

    /// <summary>Clears the entries or buffered contents retained by this instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        // Operation cleanup inlines only the empty check; the table clearing stays out of every entry's cleanup block.
        if (_count > 0) ClearEntries();
    }

    /// <summary>Clears both tables of a nonempty map.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ClearEntries()
    {
        Array.Clear(_table1, 0, _capacity);
        Array.Clear(_table2, 0, _capacity);
        _count = 0;
        _version++;
    }

    /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (capacity > _capacity)
            Resize(CalculateCapacity(capacity));
    }

    /// <summary>Performs the trim excess step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess()
    {
        int newCapacity = CalculateCapacity(_count);

        if (newCapacity < _capacity)
            Resize(newCapacity);
    }

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Releases the resources or pooled state owned by this instance.</summary>
    public void Dispose()
    {
        _table1 = null;
        _table2 = null;
        _count = 0;
        _capacity = 0;
        _capacityMask = 0;
        _version = 0;
    }

    /// <summary>Performs the resize step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize() => Resize(_capacity * 2);

    /// <summary>Performs the resize step for the current operation.</summary>
    /// <param name="newCapacity">The new capacity used by this operation.</param>
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
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Failed to rehash during resize");
                ref Entry oldEntry2 = ref oldTable2[i];
                if (oldEntry2.IsOccupied)
                    if (!TryAdd(oldEntry2.Key, oldEntry2.Value))
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Failed to rehash during resize");
            }
        }
    }

    /// <summary>Provides enumerator functionality.</summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly LuminCircleReferenceMap<TKey, TValue> _dict;
        private int _index;
        private int _table;
        private KeyValuePair<TKey, TValue> _current;
        private readonly int _version;

        /// <summary>Initializes a new instance of <c>Enumerator</c>.</summary>
        /// <param name="circleReferenceMap">The circle reference map used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(LuminCircleReferenceMap<TKey, TValue> circleReferenceMap)
        {
            _dict = circleReferenceMap;
            _index = -1;
            _table = 0;
            _current = default;
            _version = circleReferenceMap._version;
        }

        /// <summary>Gets the current.</summary>
        public KeyValuePair<TKey, TValue> Current => _current;
        /// <summary>Gets the current.</summary>
        object IEnumerator.Current => Current;

        /// <summary>Advances the enumerator to the next entry.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_version != _dict._version)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Collection was modified");
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

        /// <summary>Resets the enumerator to its initial position.</summary>
        public void Reset()
        {
            if (_version != _dict._version)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Collection was modified");
            _index = -1;
            _table = 0;
            _current = default;
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        public void Dispose() { }
    }
}
