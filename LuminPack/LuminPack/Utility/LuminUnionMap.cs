using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Utility;

[StructLayout(LayoutKind.Sequential)]
public sealed unsafe class LuminUnionMap<TValue> : IDisposable
    where TValue : unmanaged
{
    private const int MIN_CAPACITY = 8;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Entry
    {
        public IntPtr Key;
        public TValue Value;
    }

    internal Entry* _table1;
    internal int _capacity;
    internal int _count;
    internal nint _capacityMask;

    public int Count => _count;
    public int Capacity => _capacity;
    public bool IsCreated => _table1 != null;

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
        _capacityMask = (_capacity - 1);
        
        nuint tableSize = (nuint)(_capacity * sizeof(Entry));
        
#if NET5_0_OR_GREATER
        _table1 = (Entry*)NativeMemory.AllocZeroed(tableSize);
#else
        _table1 = (Entry*)Marshal.AllocHGlobal((nint)tableSize);
        Unsafe.InitBlock(_table1, 0, (uint)tableSize);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in nint key, out TValue value)
    {
        ref var entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask);

        if (entry.Key != key)
        {
            value = default;
            return false;
        }
        
        value = entry.Value;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in nint key)
    {
        return ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask).Value;
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
        ref Entry entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask);
        if (entry.Key == IntPtr.Zero)
        {
            entry.Key = key;
            entry.Value = value;
            _count++;
            
            if (_count > _capacity)
                Resize();
                
            return true;
        }
        else if (entry.Key == key)
        {
            return false;
        }

        // 使用布谷鸟踢出
        return CuckooInsert(key, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CuckooInsert(in nint key, TValue value)
    {
        nint currentKey = key;
        TValue currentValue = value;
        
        for (int i = 0; i < _capacity; i++)
        {
            ref Entry entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), currentKey & _capacityMask);
            
            if (entry.Key == IntPtr.Zero)
            {
                entry.Key = currentKey;
                entry.Value = currentValue;
                _count++;
                
                if (_count > _capacity)
                    Resize();
                    
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
    public void Dispose()
    {
        if (_table1 != null)
        {
#if NET5_0_OR_GREATER
            NativeMemory.Free(_table1);
#else
            Marshal.FreeHGlobal((IntPtr)_table1);
#endif
            _table1 = null;
        }
            
        _count = 0;
        _capacity = 0;
        _capacityMask = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize() => Resize(_capacity * 2);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize(int newCapacity)
    {
        Entry* oldTable = _table1;
        int oldCapacity = _capacity;
        int oldCount = _count;

        _capacity = newCapacity;
        _count = 0;
        InitializeTable();

        if (oldCount > 0)
        {
            for (int i = 0; i < oldCapacity; i++)
            {
                ref Entry oldEntry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(oldTable), i);
                if (oldEntry.Key != IntPtr.Zero)
                {
                    if (!TryRegister(oldEntry.Key, oldEntry.Value))
                        throw new InvalidOperationException("Failed to rehash during resize");
                }
            }
        }

        if (oldTable != null)
        {
#if NET5_0_OR_GREATER
            NativeMemory.Free(oldTable);
#else
            Marshal.FreeHGlobal((IntPtr)oldTable);
#endif
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public sealed unsafe class LuminFrozenUnionMap<TValue> : IDisposable
    where TValue : unmanaged
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Entry
    {
        public IntPtr Key;
        public TValue Value;
    }

    internal Entry* _table;
    private int _capacity;
    private nint _capacityMask;
    private nint _seed;
    private const int MAX_SEED_SEARCH = 100_000;
    
    public int Capacity => _capacity;

    public static LuminFrozenUnionMap<TValue> CreateFrom(LuminUnionMap<TValue> source, int maxSeedSearch = MAX_SEED_SEARCH)
    {
        if (source == null || source._count == 0)
            throw new ArgumentException("Source map cannot be null or empty");

        var frozen = new LuminFrozenUnionMap<TValue>();

        Span<int> multipliers = stackalloc int[]{ 2, 3, 4, 5, 6, 8, 10, 12, 16 };
        
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
        bool* used = stackalloc bool[_capacity];
        
        Random rand = new Random(Environment.TickCount);
        nint startSeed = rand.Next(1, 1000);
        
        for (nint seed = startSeed; seed < startSeed + maxSeedSearch; seed++)
        {
            Unsafe.InitBlock(used, 0, (uint)_capacity);
            bool collision = false;
        
            for (int i = 0; i < source._capacity; i++)
            {
                ref Entry entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(source._table1), i);
                if (entry.Key == IntPtr.Zero) continue;

                nint idx = Hash(entry.Key, seed) & _capacityMask;
                if (Unsafe.Add(ref Unsafe.AsRef<bool>(used), idx))
                {
                    collision = true;
                    break;
                }
                Unsafe.Add(ref Unsafe.AsRef<bool>(used), idx) = true;
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
        nuint tableSize = (nuint)(_capacity * sizeof(Entry));
        
#if NET5_0_OR_GREATER
        _table = (Entry*)NativeMemory.AllocZeroed(tableSize);
#else
        _table = (Entry*)Marshal.AllocHGlobal((nint)tableSize);
        Unsafe.InitBlock(_table, 0, (uint)tableSize);
#endif
    }

    private void PopulateFrom(LuminUnionMap<TValue> source)
    {
        for (int i = 0; i < source._capacity; i++)
        {
            ref Entry srcEntry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(source._table1), i);
            if (srcEntry.Key == IntPtr.Zero) continue;

            nint idx = Hash(srcEntry.Key, _seed) & _capacityMask;
            ref Entry destEntry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table), idx);
            destEntry.Key = srcEntry.Key;
            destEntry.Value = srcEntry.Value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(in nint key)
    {
        nint idx = Hash(key, _seed) & _capacityMask;
        return ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table), idx).Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in nint key, out TValue value)
    {
        nint idx = Hash(key, _seed) & _capacityMask;
        ref Entry entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table), idx);
        
        if (entry.Key != key)
        {
            value = default;
            return false;
        }
        
        value = entry.Value;
        return true;
    }
    
    public bool TryRegister(nint key, TValue value)
    {
        using var tempMap = new LuminUnionMap<TValue>(_capacity);
        
        for (int i = 0; i < _capacity; i++)
        {
            ref Entry entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table), i);
            if (entry.Key != IntPtr.Zero)
            {
                if (!tempMap.TryRegister(entry.Key, entry.Value))
                    return false;
            }
        }
        
        if (!tempMap.TryRegister(key, value))
            return false;
        
        var newFrozen = CreateFrom(tempMap);
        
        this.Dispose();
        this._table = newFrozen._table;
        this._capacity = newFrozen._capacity;
        this._capacityMask = newFrozen._capacityMask;
        this._seed = newFrozen._seed;
        
        newFrozen._table = null;
        newFrozen.Dispose();
        
        return true;
    }

    public void Dispose()
    {
        if (_table != null)
        {
#if NET5_0_OR_GREATER
            NativeMemory.Free(_table);
#else
            Marshal.FreeHGlobal((nint)_table);
#endif
            _table = null;
        }
    }
}