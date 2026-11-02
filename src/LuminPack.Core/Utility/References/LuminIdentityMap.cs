using System.Runtime.CompilerServices;

namespace LuminPack.Utility;

/// <summary>Maps object identities to reference identifiers for one serialization operation.</summary>
/// <remarks>
/// Keys are compared by reference and hashed with <see cref="RuntimeHelpers.GetHashCode(object)"/>, so lookups make no
/// interface calls and never run application equality. Open addressing with linear probing keeps a lookup in one or two
/// cache lines; the table is cleared in place and keeps its capacity between operations.
/// </remarks>
internal sealed class LuminIdentityMap
{
    private const int MinCapacity = 16;
    private ReferenceSlot[] _keys = new ReferenceSlot[MinCapacity];
    private uint[] _values = new uint[MinCapacity];
    private int _count;

    /// <summary>Gets the number of registered identities.</summary>
    internal int Count => _count;

    /// <summary>Finds an identity's identifier, or reserves a slot for a new one.</summary>
    /// <param name="key">The object identity.</param>
    /// <param name="exists">Whether the identity was already registered.</param>
    /// <returns>The identifier slot, which the caller assigns for a new identity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref uint GetValueRefOrAddDefault(object key, out bool exists)
    {
        var keys = _keys;
        int mask = keys.Length - 1;
        int index = Slot(key, mask);
        while (true)
        {
            object? current = keys[index].Value;
            if (ReferenceEquals(current, key))
            {
                exists = true;
                return ref _values[index];
            }
            if (current is null) break;
            index = (index + 1) & mask;
        }
        exists = false;
        // Keep the load factor at or below one half.
        if (_count >= keys.Length >> 1) return ref AddAfterGrowth(key);
        keys[index].Value = key;
        _count++;
        return ref _values[index];
    }

    /// <summary>Registers an identity unless it is already present.</summary>
    /// <param name="key">The object identity.</param>
    /// <param name="value">The identifier.</param>
    internal void TryAdd(object key, uint value)
    {
        ref uint slot = ref GetValueRefOrAddDefault(key, out bool exists);
        if (!exists) slot = value;
    }

    /// <summary>Checks whether an identity is registered.</summary>
    /// <param name="key">The object identity.</param>
    /// <returns>Whether the identity is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Contains(object key)
    {
        var keys = _keys;
        int mask = keys.Length - 1;
        for (int index = Slot(key, mask);; index = (index + 1) & mask)
        {
            object? current = keys[index].Value;
            if (ReferenceEquals(current, key)) return true;
            if (current is null) return false;
        }
    }

    /// <summary>Removes every identity while keeping the capacity.</summary>
    internal void Clear()
    {
        if (_count == 0) return;
        // A table grown by one large graph would make every later small operation clear it completely.
        if (_keys.Length > 4096 && _count <= _keys.Length >> 4)
        {
            _keys = new ReferenceSlot[MinCapacity];
            _values = new uint[MinCapacity];
        }
        else Array.Clear(_keys, 0, _keys.Length);
        _count = 0;
    }

    /// <summary>Spreads the runtime identity hash over the table.</summary>
    /// <param name="key">The object identity.</param>
    /// <param name="mask">The table size minus one.</param>
    /// <returns>The first probe index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Slot(object key, int mask) => (int)((uint)RuntimeHelpers.GetHashCode(key) * 0x9E3779B9u >> 7) & mask;

    /// <summary>Doubles the table and adds a new identity.</summary>
    /// <param name="key">The new object identity.</param>
    /// <returns>The new identity's identifier slot.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ref uint AddAfterGrowth(object key)
    {
        var keys = _keys;
        var values = _values;
        _keys = new ReferenceSlot[keys.Length * 2];
        _values = new uint[keys.Length * 2];
        int mask = _keys.Length - 1;
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i].Value is not { } existing) continue;
            int index = Slot(existing, mask);
            while (_keys[index].Value is not null) index = (index + 1) & mask;
            _keys[index].Value = existing;
            _values[index] = values[i];
        }
        int slot = Slot(key, mask);
        while (_keys[slot].Value is not null) slot = (slot + 1) & mask;
        _keys[slot].Value = key;
        _count++;
        return ref _values[slot];
    }
}
