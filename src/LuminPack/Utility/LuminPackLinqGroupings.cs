using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LuminPack.Utility;

public sealed class LuminPackGrouping<TKey, TElement> : IGrouping<TKey, TElement>
{
    readonly TKey key;
    readonly IEnumerable<TElement> elements;

    public LuminPackGrouping(TKey key, IEnumerable<TElement> elements)
    {
        this.key = key;
        this.elements = elements;
    }

    public TKey Key => key;

    public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => elements.GetEnumerator();
}

public sealed class LuminPackLookup<TKey, TElement> : ILookup<TKey, TElement>
    where TKey : notnull
{
    readonly Dictionary<TKey, IGrouping<TKey, TElement>> groupings;

    public LuminPackLookup(Dictionary<TKey, IGrouping<TKey, TElement>> groupings)
    {
        this.groupings = groupings;
    }

    public IEnumerable<TElement> this[TKey key] => groupings.TryGetValue(key, out var value) ? value : Enumerable.Empty<TElement>();

    public int Count => groupings.Count;

    public bool Contains(TKey key) => groupings.ContainsKey(key);

    public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator() => groupings.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => groupings.Values.GetEnumerator();
}
