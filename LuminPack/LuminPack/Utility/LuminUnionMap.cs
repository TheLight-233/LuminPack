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
        value = Unsafe.Add(ref Unsafe.AsRef<Entry>(_table1), key & _capacityMask).Value;
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
