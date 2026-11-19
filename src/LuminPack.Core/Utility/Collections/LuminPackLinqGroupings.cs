using System.Collections;
using System.ComponentModel;

namespace LuminPack.Utility;

/// <summary>Provides lumin pack grouping functionality.</summary>
/// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
/// <typeparam name="TElement">The type represented by <c>TElement</c>.</typeparam>
public sealed class LuminPackGrouping<TKey, TElement> : IGrouping<TKey, TElement>
{
    readonly TKey key;
    readonly IEnumerable<TElement> elements;

    /// <summary>Initializes a new instance of <c>LuminPackGrouping</c>.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="elements">The elements used by this operation.</param>
    public LuminPackGrouping(TKey key, IEnumerable<TElement> elements)
    {
        this.key = key;
        this.elements = elements;
    }

    /// <summary>Gets the key.</summary>
    public TKey Key => key;

    /// <summary>Gets the same sequence used by the public enumerator.</summary>
    internal IEnumerable<TElement> Elements => elements;

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => elements.GetEnumerator();
}

/// <summary>Provides lumin pack lookup functionality.</summary>
/// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
/// <typeparam name="TElement">The type represented by <c>TElement</c>.</typeparam>
public sealed class LuminPackLookup<TKey, TElement> : ILookup<TKey, TElement>
    where TKey : notnull
{
    readonly Dictionary<TKey, IGrouping<TKey, TElement>> groupings;

    /// <summary>Initializes a new instance of <c>LuminPackLookup</c>.</summary>
    /// <param name="groupings">The groupings used by this operation.</param>
    public LuminPackLookup(Dictionary<TKey, IGrouping<TKey, TElement>> groupings)
    {
        this.groupings = groupings;
    }

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    public IEnumerable<TElement> this[TKey key] => key is not null && groupings.TryGetValue(key, out var value) ? value : Enumerable.Empty<TElement>();

    /// <summary>Gets the count.</summary>
    public int Count => groupings.Count;

    /// <summary>Determines whether the collection contains the supplied entry.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool Contains(TKey key) => key is not null && groupings.ContainsKey(key);

    /// <summary>Creates a lookup with one null-key group and dictionary storage for the other groups.</summary>
    /// <param name="groupings">The non-null groups, in wire order.</param>
    /// <param name="nullGroup">The group whose key is null.</param>
    /// <param name="nullGroupIndex">The position of the null-key group in the complete sequence.</param>
    /// <returns>The complete lookup, with groups in wire order.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ILookup<TKey, TElement> CreateWithNullKey(Dictionary<TKey, IGrouping<TKey, TElement>> groupings,
        IGrouping<TKey, TElement> nullGroup, int nullGroupIndex) => new NullKeyLookup(groupings, nullGroup, nullGroupIndex);

    /// <summary>Creates an unboxed entry enumerator for generated traversal without allocating a values view.</summary>
    /// <returns>The dictionary enumerator whose values are the lookup's groups in enumeration order.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Dictionary<TKey, IGrouping<TKey, TElement>>.Enumerator GetEntryEnumerator() => groupings.GetEnumerator();

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator() => groupings.Values.GetEnumerator();

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => groupings.Values.GetEnumerator();

    /// <summary>Keeps the null-key group without changing the normal lookup's storage.</summary>
    private sealed class NullKeyLookup(Dictionary<TKey, IGrouping<TKey, TElement>> groups,
        IGrouping<TKey, TElement> nullGroup, int nullGroupIndex) : ILookup<TKey, TElement>
    {
        public int Count => groups.Count + 1;

        public IEnumerable<TElement> this[TKey key] => key is null
            ? nullGroup
            : groups.TryGetValue(key, out var group) ? group : Enumerable.Empty<TElement>();

        /// <inheritdoc/>
        public bool Contains(TKey key) => key is null || groups.ContainsKey(key);

        /// <inheritdoc/>
        public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
        {
            int index = 0;
            foreach (var group in groups)
            {
                if (index++ == nullGroupIndex)
                    yield return nullGroup;
                yield return group.Value;
            }
            if (index == nullGroupIndex)
                yield return nullGroup;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
