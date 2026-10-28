using System.Collections;

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
    public IEnumerable<TElement> this[TKey key] => groupings.TryGetValue(key, out var value) ? value : Enumerable.Empty<TElement>();

    /// <summary>Gets the count.</summary>
    public int Count => groupings.Count;

    /// <summary>Determines whether the collection contains the supplied entry.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool Contains(TKey key) => groupings.ContainsKey(key);

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator() => groupings.Values.GetEnumerator();

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => groupings.Values.GetEnumerator();
}
