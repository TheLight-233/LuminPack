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
        if (capacity < 0)
            LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(capacity));
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
            LuminPackExceptionHelper.ThrowArgumentException($"An item with the same key has already been added. Key: {key}");
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
            if (_count > _capacity)
                Resize();
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
                if (_count > _capacity)
                    Resize();
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
        nuint hash = (nuint)key >> (Unsafe.SizeOf<IntPtr>() == 8 ? 3 : 2);
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
                ref Entry oldEntry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(oldTable), unchecked((nint)(uint)(i)));
#else
                ref Entry oldEntry = ref oldTable[i];
#endif
                if (oldEntry.Key != default)
                {
                    if (!TryRegister(oldEntry.Key, oldEntry.Value))
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Failed to rehash during resize");
                }
            }
        }
    }
}
