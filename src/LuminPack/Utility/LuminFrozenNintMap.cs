using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LuminPack.Code;

namespace LuminPack.Utility;

/// <summary>
/// Read-only frozen map keyed directly on a native integer (<see cref="nint"/>) with no
/// <c>TKey</c> generic and no <c>IEqualityComparer</c> virtual call. The hash of a nint key
/// is itself (low pointer-alignment bits dropped, like <see cref="LuminUnionMap{TValue}"/>),
/// so the lookup is pure arithmetic + one probe.
/// </summary>
/// <typeparam name="TValue"></typeparam>
public sealed class LuminFrozenNintMap<TValue>
{
    private readonly nint[] _keys;
    private readonly TValue[] _values;
    private readonly nint _capacityMask;

    public int Count { get; }

    private LuminFrozenNintMap(nint[] keys, TValue[] values, nint capacityMask, int count)
    {
        _keys = keys;
        _values = values;
        _capacityMask = capacityMask;
        Count = count;
    }

    /// <summary>
    /// Builds an immutable open-addressing table. The supplied pairs are collected into a
    /// table whose capacity is the next power of two above <c>2 * count</c>, so the load
    /// factor stays at or below 0.5 and lookups resolve in O(1) expected probes.
    /// </summary>
    public static LuminFrozenNintMap<TValue> Create(IEnumerable<KeyValuePair<nint, TValue>> entries)
    {
        if (entries == null) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(entries));

        List<KeyValuePair<nint, TValue>> pairs = entries is List<KeyValuePair<nint, TValue>> list
            ? list
            : new List<KeyValuePair<nint, TValue>>(entries);

        int count = pairs.Count;
        if (count == 0)
        {
            nint[] emptyKeys = Array.Empty<nint>();
            return new LuminFrozenNintMap<TValue>(emptyKeys, Array.Empty<TValue>(), 0, 0);
        }

        int capacity = 1;
        while (capacity < count << 2) capacity <<= 1;

        nint mask = capacity - 1;
        nint[] keys = new nint[capacity];
        TValue[] values = new TValue[capacity];

        for (int i = 0; i < count; i++)
        {
            nint key = pairs[i].Key;
            if (key == default)
                LuminPackExceptionHelper.ThrowArgumentException("Frozen nint map keys cannot be zero.");

            int idx = Index(key, mask);
            while (keys[idx] != default)
                idx = (idx + 1) & (int)mask;

            keys[idx] = key;
            values[idx] = pairs[i].Value;
        }

        return new LuminFrozenNintMap<TValue>(keys, values, mask, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(nint key, nint mask)
    {
        // CoreCLR MethodTables are pointer-aligned. Drop only the guaranteed alignment
        // bits so the low bits of a power-of-two table carry entropy. Keep the 32-bit
        // path conservative for Mono/Unity and netstandard users.
        nuint hash = (nuint)key >> (IntPtr.Size == 8 ? 3 : 2);
        return (int)(hash & (nuint)mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in nint key, out TValue value)
    {
        int idx = Index(key, _capacityMask);
        while (true)
        {
            if (_keys[idx] == key)
            {
                value = _values[idx];
                return true;
            }
            if (_keys[idx] == default)
            {
                value = default!;
                return false;
            }
            idx = (idx + 1) & (int)_capacityMask;
        }
    }

    public TValue this[in nint key]
    {
        get
        {
            if (TryGetValue(in key, out TValue value))
                return value;
            LuminPackExceptionHelper.ThrowKeyNotFoundException();
            return default!;
        }
    }
}
