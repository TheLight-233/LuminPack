#if NET9_0_OR_GREATER
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Starts an order-independent refill of a nonempty fixed UTF-16 length record.</summary>
    /// <param name="map">The existing dictionary.</param>
    /// <param name="count">The validated pair count.</param>
    /// <param name="refill">The refill state to release after reading.</param>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <returns>Whether the dictionary supports lookup by decoded key.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryBeginConcurrentDictionaryKeyRefill<TValue>(ConcurrentDictionary<string, TValue> map, int count,
        out ConcurrentDictionaryKeyRefill<TValue> refill) where TValue : class?
    {
        var comparer = map.Comparer;
        if (map.Count == count &&
            (ReferenceEquals(comparer, EqualityComparer<string>.Default) || ReferenceEquals(comparer, StringComparer.Ordinal)) &&
            map.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup))
        {
            refill = new(map, lookup, count);
            return true;
        }

        refill = default;
        return false;
    }
}

/// <summary>Refills an existing string-keyed dictionary while staging the record for a possible set change.</summary>
/// <typeparam name="TValue">The dictionary value type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct ConcurrentDictionaryKeyRefill<TValue> : IDisposable where TValue : class?
{
    [ThreadStatic]
    private static HashSet<string>? _cachedKeys;

    private readonly ConcurrentDictionary<string, TValue> _map;
    private readonly ConcurrentDictionary<string, TValue>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private readonly HashSet<string> _keys;
    private readonly int _count;
    private KeyValuePair<string, TValue>[]? _pairs;
    private int _written;
    private bool _sameKeys;

    /// <summary>Rents the staging storage and the duplicate detector for this read.</summary>
    /// <param name="map">The target dictionary.</param>
    /// <param name="lookup">Its alternate string-key lookup.</param>
    /// <param name="count">The validated pair count.</param>
    internal ConcurrentDictionaryKeyRefill(ConcurrentDictionary<string, TValue> map,
        ConcurrentDictionary<string, TValue>.AlternateLookup<ReadOnlySpan<char>> lookup, int count)
    {
        _map = map;
        _lookup = lookup;
        _count = count;
        _keys = _cachedKeys ?? new HashSet<string>(ReferenceEqualityComparer.Instance);
        _cachedKeys = null;
        _keys.EnsureCapacity(count);
        _pairs = ArrayPool<KeyValuePair<string, TValue>>.Shared.Rent(count);
        _written = 0;
        _sameKeys = true;
    }

    /// <summary>Reads a key and finds its prior value when this is the key's first occurrence.</summary>
    /// <param name="reader">The reader positioned at the fixed UTF-16 key record.</param>
    /// <param name="previous">The candidate value for reuse.</param>
    /// <returns>The decoded key or the existing equal key instance.</returns>
    public string ReadKey(scoped ref LuminPackReader reader, out TValue previous)
    {
        string key = reader.ReadConcurrentDictionaryKey(_lookup, out bool found, out previous);
        if (!found || !_keys.Add(key))
        {
            _sameKeys = false;
            previous = default!;
        }
        return key;
    }

    /// <summary>Stages one pair and publishes a changed value while all keys still match.</summary>
    /// <param name="key">The decoded key.</param>
    /// <param name="value">The decoded value.</param>
    /// <param name="previous">The value supplied by <see cref="ReadKey"/>.</param>
    public void Store(string key, TValue value, TValue previous)
    {
        _pairs![_written++] = new(key, value);
        if (_sameKeys && !ReferenceEquals(value, previous))
            _map[key] = value;
    }

    /// <summary>Replaces the set of keys only when the decoded record changed that set.</summary>
    public void Complete()
    {
        if (_sameKeys && _map.Count == _count)
            return;

        var pairs = _pairs!;
        if (LuminPackCollectionAccess.TryRefillStagedConcurrentDictionary(_map, pairs, _written))
            return;

        _map.Clear();
        for (int i = 0; i < _written; i++)
            _map.TryAdd(pairs[i].Key, pairs[i].Value);
    }

    /// <summary>Returns operation-local staging even when decoding fails.</summary>
    public void Dispose()
    {
        if (_pairs is not { } pairs)
            return;

        _pairs = null;
        _keys.Clear();
        if (_count <= 4096)
            _cachedKeys = _keys;
        ArrayPool<KeyValuePair<string, TValue>>.Shared.Return(pairs, clearArray: true);
    }
}
#endif
