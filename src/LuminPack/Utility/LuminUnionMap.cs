using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Utility;

public sealed class LuminUnionMap<TValue>
{
    private const int MIN_CAPACITY = 8;

    internal struct Entry
    {
        public nint Key;
        public TValue Value;
    }

    internal Entry[] _table;
    internal int _capacity;
    internal int _count;
    internal nint _capacityMask;

    public int Count => _count;
    public int Capacity => _capacity;
    public bool IsCreated => _table != null;

    public LuminUnionMap()
    {
        _capacity = MIN_CAPACITY;
        _count = 0;
        InitializeTable();
    }

    public LuminUnionMap(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = CalculateCapacity(capacity);
        _count = 0;
        InitializeTable();
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeTable()
    {
        _capacityMask = _capacity - 1;
        _table = new Entry[_capacity];
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in nint key)
        => ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), key & _capacityMask).Value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue TryGetValueRef(in nint key)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), key & _capacityMask);
        if (entry.Key != key)
            return ref Unsafe.NullRef<TValue>();
        return ref entry.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(in nint key, TValue value)
    {
        if (!TryRegister(key, value))
            throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRegister(in nint key, TValue value)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), key & _capacityMask);
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
                if (_count > _capacity) Resize();
                return true;
            }

            // 踢出当前条目
            (currentKey, entry.Key) = (entry.Key, currentKey);
            (currentValue, entry.Value) = (entry.Value, currentValue);
        }

        // 超过最大踢出次数，扩容
        Resize();
        return TryRegister(currentKey, currentValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize() => Resize(_capacity * 2);

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
                        throw new InvalidOperationException("Failed to rehash during resize");
                }
            }
        }
    }
}

public sealed class LuminFrozenUnionMap<TValue>
{
    internal struct Entry
    {
        public nint Key;
        public TValue Value;
    }

    internal Entry[] _table;
    private int _capacity;
    private nint _capacityMask;
    private nint _seed;
    private const int MAX_SEED_SEARCH = 100_000;

    public int Capacity => _capacity;
    
    private LuminFrozenUnionMap() { }

    public static LuminFrozenUnionMap<TValue> CreateFrom(LuminUnionMap<TValue> source, int maxSeedSearch = MAX_SEED_SEARCH)
    {
        if (source == null || source._count == 0)
            throw new ArgumentException("Source map cannot be null or empty");

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

        throw new InvalidOperationException($"无法找到完美哈希种子: Keys={source._count}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint Hash(nint key, nint seed)
    {
        key *= seed;
        return key ^ (key >> 27);
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateCapacity(int count)
    {
        int capacity = 1;
        while (capacity < count) capacity <<= 1;
        return capacity;
    }

    private void AllocateTable()
    {
        _table = new Entry[_capacity];
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in nint key)
    {
        var idx = Hash(key, _seed) & _capacityMask;
        return ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), idx).Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue TryGetValueRef(in nint key)
    {
        var idx = Hash(key, _seed) & _capacityMask;
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), idx);
        if (entry.Key != key)
            return ref Unsafe.NullRef<TValue>();
        return ref entry.Value;
    }

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