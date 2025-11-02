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
        public bool IsOccupied;
    }

    internal Entry* _table1;
    internal Entry* _table2;
    private int _capacity;
    private int _count;
    private nint _capacityMask;

    public int Count => _count;
    public int Capacity => _capacity;
    public bool IsCreated => _table1 != null;

    public LuminUnionMap()
    {
        _capacity = MIN_CAPACITY;
        _count = 0;
        InitializeTables();
    }

    public LuminUnionMap(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = CalculateCapacity(capacity);
        _count = 0;
        InitializeTables();
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
    private void InitializeTables()
    {
        _capacityMask = (_capacity - 1);
        
        nuint tableSize = (nuint)(_capacity * sizeof(Entry));
        
#if NET5_0_OR_GREATER
        _table1 = (Entry*)NativeMemory.AllocZeroed(tableSize);
        _table2 = (Entry*)NativeMemory.AllocZeroed(tableSize);
#else
        _table1 = (Entry*)Marshal.AllocHGlobal((nint)tableSize);
        _table2 = (Entry*)Marshal.AllocHGlobal((nint)tableSize);
        Unsafe.InitBlock(_table1, 0, (uint)tableSize);
        Unsafe.InitBlock(_table2, 0, (uint)tableSize);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(nint key, out TValue value)
    {
        ref var entry1 = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask);
        if (entry1.Key == key)
        {
            value = entry1.Value;
            return true;
        }
        
        ref var entry2 = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table2), key & _capacityMask);
        if (entry2.Key == key)
        {
            value = entry2.Value;
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRef(nint key)
    {
        ref Entry entry1 = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask);
        if (entry1.IsOccupied && entry1.Key == key)
            return ref entry1.Value;
        
        ref Entry entry2 = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table2), key & _capacityMask);
        if (entry2.IsOccupied && entry2.Key == key)
            return ref entry2.Value;

        throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(nint key, TValue value)
    {
        if (!TryRegister(key, value))
            throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRegister(nint key, TValue value)
    {
        ref Entry entry1 = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask);
        if (!entry1.IsOccupied)
        {
            entry1.Key = key;
            entry1.Value = value;
            entry1.IsOccupied = true;
            _count++;
            
            if (_count > _capacity)
                Resize();
                
            return true;
        }
        else if (entry1.Key == key)
        {
            return false;
        }
        
        ref Entry entry2 = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table2), key & _capacityMask);
        if (!entry2.IsOccupied)
        {
            entry2.Key = key;
            entry2.Value = value;
            entry2.IsOccupied = true;
            _count++;
            
            if (_count > (_capacity * 3 / 4))
                Resize();
                
            return true;
        }
        else if (entry2.Key == key)
        {
            return false;
        }

        // 两个表都有冲突，使用布谷鸟踢出
        return CuckooInsert(key, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CuckooInsert(nint key, TValue value)
    {
        nint currentKey = key;
        TValue currentValue = value;
        bool useTable1 = true;
        
        for (int i = 0; i < _capacity; i++)
        {
            if (useTable1)
            {
                ref Entry entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask);
                
                if (!entry.IsOccupied)
                {
                    entry.Key = currentKey;
                    entry.Value = currentValue;
                    entry.IsOccupied = true;
                    _count++;
                    
                    if (_count > (_capacity * 3 / 4))
                        Resize();
                        
                    return true;
                }
                
                // 踢出当前条目
                (currentKey, entry.Key) = (entry.Key, currentKey);
                (currentValue, entry.Value) = (entry.Value, currentValue);
                useTable1 = false;
            }
            else
            {
                ref Entry entry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(_table2), key & _capacityMask);
                
                if (!entry.IsOccupied)
                {
                    entry.Key = currentKey;
                    entry.Value = currentValue;
                    entry.IsOccupied = true;
                    _count++;
                    
                    if (_count > _capacity)
                        Resize();
                        
                    return true;
                }
                
                // 踢出当前条目
                (currentKey, entry.Key) = (entry.Key, currentKey);
                (currentValue, entry.Value) = (entry.Value, currentValue);
                useTable1 = true;
            }
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
            NativeMemory.Free(_table2);
#else
            Marshal.FreeHGlobal((IntPtr)_table1);
            Marshal.FreeHGlobal((IntPtr)_table2);
#endif
            _table1 = null;
            _table2 = null;
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
        Entry* oldTable1 = _table1;
        Entry* oldTable2 = _table2;
        int oldCapacity = _capacity;
        int oldCount = _count;

        _capacity = newCapacity;
        _count = 0;
        InitializeTables();

        if (oldCount > 0)
        {
            // 重新插入第一个表的元素
            for (int i = 0; i < oldCapacity; i++)
            {
                ref Entry oldEntry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(oldTable1), i);
                if (oldEntry.IsOccupied)
                {
                    if (!TryRegister(oldEntry.Key, oldEntry.Value))
                        throw new InvalidOperationException("Failed to rehash during resize");
                }
            }

            // 重新插入第二个表的元素
            for (int i = 0; i < oldCapacity; i++)
            {
                ref Entry oldEntry = ref Unsafe.Add(ref Unsafe.AsRef<Entry>(oldTable2), i);
                if (oldEntry.IsOccupied)
                {
                    if (!TryRegister(oldEntry.Key, oldEntry.Value))
                        throw new InvalidOperationException("Failed to rehash during resize");
                }
            }
        }

        if (oldTable1 != null)
        {
#if NET5_0_OR_GREATER
            NativeMemory.Free(oldTable1);
            NativeMemory.Free(oldTable2);
#else
            Marshal.FreeHGlobal((IntPtr)oldTable1);
            Marshal.FreeHGlobal((IntPtr)oldTable2);
#endif
        }
    }
}
