using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Utility;

/// <summary>Provides lumin union map functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public sealed class LuminUnionMap<TValue>
{
    private const int MIN_CAPACITY = 8;

    /// <summary>Provides entry functionality.</summary>
    internal struct Entry
    {
        /// <summary>Stores the key.</summary>
        public nint Key;
        /// <summary>Stores the value.</summary>
        public TValue Value;
    }

    internal Entry[] _table;
    internal int _capacity;
    internal int _count;
    internal nint _capacityMask;

    /// <summary>Gets the count.</summary>
    public int Count => _count;
    /// <summary>Gets the capacity.</summary>
    public int Capacity => _capacity;
    /// <summary>Gets the is created.</summary>
    public bool IsCreated => _table != null;

    /// <summary>Initializes a new instance of <c>LuminUnionMap</c>.</summary>
    public LuminUnionMap()
    {
        _capacity = MIN_CAPACITY;
        _count = 0;
        InitializeTable();
    }

    /// <summary>Initializes a new instance of <c>LuminUnionMap</c>.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    public LuminUnionMap(int capacity)
    {
        if (capacity < 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(capacity));
        _capacity = CalculateCapacity(capacity);
        _count = 0;
        InitializeTable();
    }

    /// <summary>Calculates the serialized size contribution of capacity.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateCapacity(int capacity)
    {
        if (capacity < MIN_CAPACITY)
            return MIN_CAPACITY;

        capacity--;
        capacity |= capacity >> 1;
        capacity |= capacity >> 2;
        capacity |= capacity >> 4;
        capacity |= capacity >> 8;
        capacity |= capacity >> 16;
        return capacity + 1;
    }

    /// <summary>Performs the initialize table step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeTable()
    {
        _capacityMask = _capacity - 1;
        _table = new Entry[_capacity];
    }

    /// <summary>Looks up a key and reports whether an associated value was found.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in nint key, out TValue value)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), GetIndex(key));
        if (entry.Key != key)
        {
            value = default!;
            return false;
        }
        value = entry.Value;
        return true;
    }

    /// <summary>Obtains the value ref for the supplied input.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in nint key)
        => ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), GetIndex(key)).Value;

    /// <summary>Attempts to get value ref and reports whether it succeeded.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue TryGetValueRef(in nint key)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), GetIndex(key));
        if (entry.Key != key)
            return ref Unsafe.NullRef<TValue>();
        return ref entry.Value;
    }

    /// <summary>Performs the register step for the current operation.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(in nint key, TValue value)
    {
        if (!TryRegister(key, value))
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentException($"An item with the same key has already been added. Key: {key}");
    }

    /// <summary>Attempts to register and reports whether it succeeded.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRegister(in nint key, TValue value)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), GetIndex(key));
        if (entry.Key == default)
        {
            entry.Key = key;
            entry.Value = value;
            _count++;
            if (_count > _capacity) Resize();
            return true;
        }
        else if (entry.Key == key)
        {
            return false;
        }
        return CuckooInsert(key, value);
    }

    /// <summary>Performs the cuckoo insert step for the current operation.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CuckooInsert(in nint key, TValue value)
    {
        nint currentKey = key;
        TValue currentValue = value;

        for (int i = 0; i < _capacity; i++)
        {
            ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), GetIndex(currentKey));
            if (entry.Key == default)
            {
                entry.Key = currentKey;
                entry.Value = currentValue;
                _count++;
                if (_count > _capacity) Resize();
                return true;
            }

            // Displace the current entry.
            (currentKey, entry.Key) = (entry.Key, currentKey);
            (currentValue, entry.Value) = (entry.Value, currentValue);
        }

        // Resize after reaching the maximum number of cuckoo displacements.
        Resize();
        return TryRegister(currentKey, currentValue);
    }

    /// <summary>Obtains the index for the supplied input.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private nint GetIndex(nint key)
    {
        // CoreCLR MethodTables are pointer-aligned. Drop only the guaranteed
        // alignment bits so the low bits of a power-of-two table carry entropy.
        // Keep the 32-bit path conservative for Mono/Unity and netstandard users.
        nuint hash = (nuint)key >> (IntPtr.Size == 8 ? 3 : 2);
        return (nint)(hash & (nuint)_capacityMask);
    }

    /// <summary>Performs the resize step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize() => Resize(_capacity * 2);

    /// <summary>Performs the resize step for the current operation.</summary>
    /// <param name="newCapacity">The new capacity used by this operation.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize(int newCapacity)
    {
        Entry[] oldTable = _table;
        int oldCapacity = _capacity;
        int oldCount = _count;

        _capacity = newCapacity;
        _count = 0;
        InitializeTable();

        if (oldCount > 0)
        {
            for (int i = 0; i < oldCapacity; i++)
            {
#if NET5_0_OR_GREATER
                ref Entry oldEntry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(oldTable), i);
#else
                ref Entry oldEntry = ref oldTable[i];
#endif
                if (oldEntry.Key != default)
                {
                    if (!TryRegister(oldEntry.Key, oldEntry.Value))
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Failed to rehash during resize");
                }
            }
        }
    }
}

/// <summary>
/// Single-slot union map for non-pointer keys such as encoded union tags.
/// MethodTable-keyed maps should use <see cref="LuminUnionMap{TValue}"/>,
/// which drops guaranteed pointer-alignment bits before indexing.
/// </summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public sealed class LuminRawUnionMap<TValue>
{
    private const int MIN_CAPACITY = 8;

    /// <summary>Provides entry functionality.</summary>
    private struct Entry
    {
        /// <summary>Stores the key.</summary>
        public nint Key;
        /// <summary>Stores the value.</summary>
        public TValue Value;
    }

    private Entry[] _table;
    private int _capacity;
    private int _count;
    private nint _capacityMask;

    /// <summary>Gets the count.</summary>
    public int Count => _count;
    /// <summary>Gets the capacity.</summary>
    public int Capacity => _capacity;

    /// <summary>Initializes a new instance of <c>LuminRawUnionMap</c>.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    public LuminRawUnionMap(int capacity)
    {
        if (capacity < 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(capacity));
        _capacity = CalculateCapacity(capacity);
        InitializeTable();
    }

    /// <summary>Looks up a key and reports whether an associated value was found.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in nint key, out TValue value)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), key & _capacityMask);
        if (entry.Key != key)
        {
            value = default!;
            return false;
        }

        value = entry.Value;
        return true;
    }

    /// <summary>Attempts to register and reports whether it succeeded.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRegister(in nint key, TValue value)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), key & _capacityMask);
        if (entry.Key == default)
        {
            entry.Key = key;
            entry.Value = value;
            _count++;
            return true;
        }

        if (entry.Key == key)
            return false;

        return CuckooInsert(key, value);
    }

    /// <summary>Performs the cuckoo insert step for the current operation.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CuckooInsert(in nint key, TValue value)
    {
        nint currentKey = key;
        TValue currentValue = value;

        for (int i = 0; i < _capacity; i++)
        {
            ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), currentKey & _capacityMask);
            if (entry.Key == default)
            {
                entry.Key = currentKey;
                entry.Value = currentValue;
                _count++;
                return true;
            }

            (currentKey, entry.Key) = (entry.Key, currentKey);
            (currentValue, entry.Value) = (entry.Value, currentValue);
        }

        Resize();
        return TryRegister(currentKey, currentValue);
    }

    /// <summary>Performs the resize step for the current operation.</summary>
    private void Resize()
    {
        Entry[] oldTable = _table;
        int oldCapacity = _capacity;
        _capacity *= 2;
        _count = 0;
        InitializeTable();

        for (int i = 0; i < oldCapacity; i++)
        {
#if NET5_0_OR_GREATER
            ref Entry oldEntry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(oldTable), i);
#else
            ref Entry oldEntry = ref oldTable[i];
#endif
            if (oldEntry.Key != default && !TryRegister(oldEntry.Key, oldEntry.Value))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Failed to rehash raw union map during resize");
        }
    }

    /// <summary>Performs the initialize table step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeTable()
    {
        _capacityMask = _capacity - 1;
        _table = new Entry[_capacity];
    }

    /// <summary>Calculates the serialized size contribution of capacity.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateCapacity(int capacity)
    {
        if (capacity < MIN_CAPACITY)
            return MIN_CAPACITY;

        capacity--;
        capacity |= capacity >> 1;
        capacity |= capacity >> 2;
        capacity |= capacity >> 4;
        capacity |= capacity >> 8;
        capacity |= capacity >> 16;
        return capacity + 1;
    }
}

/// <summary>Provides lumin frozen union map functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public sealed class LuminFrozenUnionMap<TValue>
{
    /// <summary>Provides entry functionality.</summary>
    internal struct Entry
    {
        /// <summary>Stores the key.</summary>
        public nint Key;
        /// <summary>Stores the value.</summary>
        public TValue Value;
    }

    internal Entry[] _table;
    private int _capacity;
    private nint _capacityMask;
    private nint _seed;
    private const int MAX_SEED_SEARCH = 100_000;

    /// <summary>Gets the capacity.</summary>
    public int Capacity => _capacity;

    /// <summary>Initializes a new instance of <c>LuminFrozenUnionMap</c>.</summary>
    private LuminFrozenUnionMap() { }

    /// <summary>Creates from from the supplied input.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="maxSeedSearch">The max seed search used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static LuminFrozenUnionMap<TValue> CreateFrom(LuminUnionMap<TValue> source, int maxSeedSearch = MAX_SEED_SEARCH)
    {
        if (source == null || source._count == 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentException("Source map cannot be null or empty");

        var frozen = new LuminFrozenUnionMap<TValue>();

        Span<int> multipliers = stackalloc int[] { 2, 3, 4, 5, 6, 8, 10, 12, 16 };

        foreach (int multiplier in multipliers)
        {
            frozen._capacity = CalculateCapacity(source._count * multiplier);
            frozen._capacityMask = frozen._capacity - 1;

            if (frozen.TryFindPerfectSeed(source, maxSeedSearch, out frozen._seed))
            {
                frozen.AllocateTable();
                frozen.PopulateFrom(source);
                return frozen;
            }
        }

        global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException($"Could not find a perfect hash seed: Keys={source._count}");
        return frozen;
    }

    /// <summary>Determines whether h are present.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="seed">The seed used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint Hash(nint key, nint seed)
    {
        key *= seed;
        return key ^ (key >> 27);
    }

    /// <summary>Attempts to find perfect seed and reports whether it succeeded.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="maxSeedSearch">The max seed search used by this operation.</param>
    /// <param name="foundSeed">Receives the found seed produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private bool TryFindPerfectSeed(LuminUnionMap<TValue> source, int maxSeedSearch, out nint foundSeed)
    {
        Span<bool> used = stackalloc bool[_capacity];

        Random rand = new Random(Environment.TickCount);
        nint startSeed = rand.Next(1, 1000);

        for (nint seed = startSeed; seed < startSeed + maxSeedSearch; seed++)
        {
            used.Clear();
            bool collision = false;

            for (int i = 0; i < source._capacity; i++)
            {
#if NET5_0_OR_GREATER
                ref LuminUnionMap<TValue>.Entry srcEntry =
                    ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(source._table), i);
#else
                ref LuminUnionMap<TValue>.Entry srcEntry = ref source._table[i];
#endif
                if (srcEntry.Key == default) continue;

                int idx = (int)(Hash(srcEntry.Key, seed) & _capacityMask);
                if (used[idx]) { collision = true; break; }
                used[idx] = true;
            }

            if (!collision)
            {
                foundSeed = seed;
                return true;
            }
        }

        foundSeed = 0;
        return false;
    }

    /// <summary>Calculates the serialized size contribution of capacity.</summary>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateCapacity(int count)
    {
        int capacity = 1;
        while (capacity < count) capacity <<= 1;
        return capacity;
    }

    /// <summary>Performs the allocate table step for the current operation.</summary>
    private void AllocateTable()
    {
        _table = new Entry[_capacity];
    }

    /// <summary>Performs the populate from step for the current operation.</summary>
    /// <param name="source">The input data to process.</param>
    private void PopulateFrom(LuminUnionMap<TValue> source)
    {
        for (int i = 0; i < source._capacity; i++)
        {
#if NET5_0_OR_GREATER
            ref LuminUnionMap<TValue>.Entry srcEntry =
                ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(source._table), i);
#else
            ref LuminUnionMap<TValue>.Entry srcEntry = ref source._table[i];
#endif
            if (srcEntry.Key == default) continue;

            int idx = (int)(Hash(srcEntry.Key, _seed) & _capacityMask);
#if NET5_0_OR_GREATER
            ref Entry destEntry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_table), idx);
#else
            ref Entry destEntry = ref _table[idx];
#endif
            destEntry.Key = srcEntry.Key;
            destEntry.Value = srcEntry.Value;
        }
    }

    /// <summary>Obtains the value ref for the supplied input.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in nint key)
    {
        var idx = Hash(key, _seed) & _capacityMask;
        return ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), idx).Value;
    }

    /// <summary>Attempts to get value ref and reports whether it succeeded.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue TryGetValueRef(in nint key)
    {
        var idx = Hash(key, _seed) & _capacityMask;
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), idx);
        if (entry.Key != key)
            return ref Unsafe.NullRef<TValue>();
        return ref entry.Value;
    }

    /// <summary>Looks up a key and reports whether an associated value was found.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in nint key, out TValue value)
    {
        var idx = Hash(key, _seed) & _capacityMask;
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), idx);
        if (entry.Key != key)
        {
            value = default!;
            return false;
        }
        value = entry.Value;
        return true;
    }

    /// <summary>Attempts to register and reports whether it succeeded.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool TryRegister(nint key, TValue value)
    {
        var tempMap = new LuminUnionMap<TValue>(_capacity);

        for (int i = 0; i < _capacity; i++)
        {
#if NET5_0_OR_GREATER
            ref Entry entry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_table), i);
#else
            ref Entry entry = ref _table[i];
#endif
            if (entry.Key != default)
            {
                if (!tempMap.TryRegister(entry.Key, entry.Value))
                    return false;
            }
        }

        if (!tempMap.TryRegister(key, value))
            return false;

        var newFrozen = CreateFrom(tempMap);

        this._table = newFrozen._table;
        this._capacity = newFrozen._capacity;
        this._capacityMask = newFrozen._capacityMask;
        this._seed = newFrozen._seed;

        newFrozen._table = null!;

        return true;
    }
}
