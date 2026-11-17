#if NET6_0_OR_GREATER
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Starts reading a record over an existing concurrent dictionary, reusing its keys and values in place.</summary>
    /// <param name="map">The existing dictionary.</param>
    /// <param name="count">The validated nonzero pair count.</param>
    /// <param name="reuseValues">Whether existing values are worth reading in place after the order no longer matches.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>The refill state.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ConcurrentDictionaryRefill<TKey, TValue> BeginConcurrentDictionaryRefill<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> map, int count, bool reuseValues) where TKey : notnull => new(map, count, reuseValues);
}

/// <summary>Reads pairs over an existing concurrent dictionary with the framework’s synchronization.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <remarks>When the record has the dictionary's count (and, for string keys, at most 64 pairs), each pair is first matched
/// against the dictionary's own enumeration order, which is the order a write of an unchanged dictionary produces: the candidate key seeds the key read and its value
/// the value read, and an unchanged pair needs no lookup or store. A changed value is stored with the indexer, which keeps
/// the node of an atomically written value. At the first mismatch, or with a different count, values that are worth
/// reusing stage the matched pairs and every following pair, looking up existing values so they are still read in place;
/// <see cref="Complete"/> then publishes the staged pairs in record order. Other values clear the
/// dictionary right away, add back the matched pairs and add the following pairs as they are read, as a fresh refill does.
/// Either way a repeated key keeps its first value. A failed read leaves the pairs updated so far.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct ConcurrentDictionaryRefill<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _map;
    private readonly int _count;
    private readonly bool _reuseValues;
    private bool _diverged;
    private IEnumerator<KeyValuePair<TKey, TValue>>? _cursor;
    private KeyValuePair<TKey, TValue>[]? _staged;
    private KeyValuePair<TKey, TValue> _candidate;
    private bool _hasCandidate;
    private int _matched;
    private int _stagedCount;

    /// <summary>Starts the refill.</summary>
    /// <param name="map">The existing dictionary.</param>
    /// <param name="count">The validated nonzero pair count.</param>
    /// <param name="reuseValues">Whether existing values are read in place after the order no longer matches.</param>
    internal ConcurrentDictionaryRefill(ConcurrentDictionary<TKey, TValue> map, int count, bool reuseValues)
    {
        _map = map;
        _count = count;
        _reuseValues = reuseValues;
        _diverged = false;
        _cursor = null;
        _staged = null;
        _candidate = default;
        _hasCandidate = false;
        _matched = 0;
        _stagedCount = 0;
        // Larger string-keyed dictionaries rarely enumerate in the order of a record written elsewhere, and a late mismatch
        // costs more than the plain refill, so they skip the ordered match.
        if ((count <= OrderedStringKeys || typeof(TKey) != typeof(string)) && map.Count == count)
            _cursor = map.GetEnumerator();
        else
            Diverge();
    }

    /// <summary>The largest string-keyed record that is matched against the dictionary's order.</summary>
    private const int OrderedStringKeys = 64;

    /// <summary>Gets whether pairs are still matched against the dictionary's enumeration order.</summary>
    public readonly bool Ordered => _cursor is not null;

    /// <summary>Advances to the next existing pair in enumeration order.</summary>
    /// <returns>The candidate key, or the default value once the order no longer matches.</returns>
    public TKey NextKey()
    {
        if (_cursor is not null && _cursor.MoveNext())
        {
            _candidate = _cursor.Current;
            _hasCandidate = true;
            return _candidate.Key;
        }
        _hasCandidate = false;
        return default!;
    }

    /// <summary>Finds the existing value of a read key.</summary>
    /// <param name="key">The key just read.</param>
    /// <param name="value">The existing value to read over, or the default value.</param>
    /// <returns>Whether the key matched the dictionary's next pair in order.</returns>
    public bool Find(TKey key, out TValue value)
    {
        if (_hasCandidate && _map.Comparer.Equals(key, _candidate.Key))
        {
            value = _candidate.Value;
            return true;
        }
        if (!_diverged)
            Diverge();
        if (_staged is null || !_map.TryGetValue(key, out value!))
            value = default!;
        return false;
    }

    /// <summary>Stores a read value.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value just read.</param>
    /// <param name="found">Whether the key matched the dictionary's next pair in order.</param>
    /// <param name="previous">The existing value passed to the value read.</param>
    public void Store(TKey key, TValue value, bool found, TValue previous)
    {
        if (!found)
        {
            if (_staged is not null)
                _staged[_stagedCount++] = new KeyValuePair<TKey, TValue>(key, value);
            else
                _map.TryAdd(key, value);
            return;
        }
        _matched++;
        if (typeof(TValue).IsValueType ? !EqualityComparer<TValue>.Default.Equals(value, previous) : !ReferenceEquals(value, previous))
            _map[key] = value;
    }

    /// <summary>Publishes the staged pairs, if the record left the dictionary's order.</summary>
    public void Complete()
    {
        var staged = _staged;
        if (staged is null)
            return;
        try
        {
            if (LuminPackCollectionAccess.TryRefillStagedConcurrentDictionary(_map, staged, _stagedCount))
                return;

            _map.Clear();
            for (int i = 0; i < _stagedCount; i++)
                _map.TryAdd(staged[i].Key, staged[i].Value);
        }
        finally
        {
            _staged = null;
            ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(staged, RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey, TValue>>());
        }
    }

    /// <summary>Leaves the dictionary's order, keeping the pairs matched so far.</summary>
    private void Diverge()
    {
        // Later pairs never match a candidate again, even when the caller stops asking for the next key.
        _diverged = true;
        _hasCandidate = false;
        _cursor = null;
        var staged = ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(_reuseValues ? _count : Math.Max(_matched, 1));
        int matched = 0;
        if (_matched != 0)
        {
            // The matched pairs are the dictionary's first enumerated pairs, with their values already updated.
            foreach (var pair in _map)
            {
                staged[matched++] = pair;
                if (matched == _matched)
                    break;
            }
        }
        if (_reuseValues)
        {
            _staged = staged;
            _stagedCount = matched;
            return;
        }
        try
        {
            _map.Clear();
            for (int i = 0; i < matched; i++)
                _map.TryAdd(staged[i].Key, staged[i].Value);
        }
        finally
        {
            ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(staged, RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey, TValue>>());
        }
    }
}
#endif
