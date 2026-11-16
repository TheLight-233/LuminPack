using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

// Views of framework collection storage used by source-generated formatters. Each view holds only the collection reference, so
// generated code reinterprets a collection reference as its view; every member reaches the collection's own field through the
// offsets probed by LuminCollectionLayouts instead of a compiled declaration of the expected layout.

/// <summary>Presents the storage fields of a <see cref="global::System.Collections.BitArray"/> at their probed offsets.</summary>
/// <remarks>Holds only the bit array reference, so reinterpreting that reference as the view is valid.</remarks>
public readonly struct BitArrayView
{
    private readonly object _owner;

    /// <summary>Gets the runtime's word-aligned bit storage.</summary>
#if NET10_0_OR_GREATER
    public ref byte[] m_array
#else
    public ref int[] m_array
#endif
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.BitArrayArray, "BitArray");
#if NET10_0_OR_GREATER
            return ref LuminFieldLayout.Field<byte[]>(_owner, LuminCollectionLayouts.BitArrayArray);
#else
            return ref LuminFieldLayout.Field<int[]>(_owner, LuminCollectionLayouts.BitArrayArray);
#endif
        }
    }

    /// <summary>Gets the bit length.</summary>
    public ref int m_length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.BitArrayLength, "BitArray");
            return ref LuminFieldLayout.Field<int>(_owner, LuminCollectionLayouts.BitArrayLength);
        }
    }
}

/// <summary>Provides immutable array view functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
/// <remarks><see cref="ImmutableArray{T}"/> is a struct whose only field is its array, so this
/// declaration is its storage on every runtime.</remarks>
public struct ImmutableArrayView<T>
{
    /// <summary>Stores the array.</summary>
    public T[]? array;
}

/// <summary>Presents the item list of a <see cref="Collection{T}"/>.</summary>
/// <typeparam name="TValue">The element type.</typeparam>
/// <remarks>Holds only the Collection&lt;T&gt; reference, so reinterpreting that reference as the view is valid. <c>items</c> returns a
/// reference to the Collection&lt;T&gt;'s own field at its probed offset; see <see cref="LuminCollectionLayouts"/>.</remarks>
public readonly struct CollectionIListView<TValue>
{
    private readonly object _owner;

    /// <summary>Gets the Collection&lt;T&gt;'s item storage.</summary>
    public ref IList<TValue> items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.CollectionItems, "Collection<T>");
            return ref LuminFieldLayout.Field<IList<TValue>>(_owner, LuminCollectionLayouts.CollectionItems);
        }
    }
}

/// <summary>Presents the item list of a <see cref="Collection{T}"/> as a list.</summary>
/// <typeparam name="TValue">The element type.</typeparam>
/// <remarks>Holds only the Collection&lt;T&gt; reference, so reinterpreting that reference as the view is valid. <c>items</c> returns a
/// reference to the Collection&lt;T&gt;'s own field at its probed offset; see <see cref="LuminCollectionLayouts"/>.</remarks>
public readonly struct CollectionView<TValue>
{
    private readonly object _owner;

    /// <summary>Gets the Collection&lt;T&gt;'s item storage.</summary>
    public ref List<TValue> items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.CollectionItems, "Collection<T>");
            return ref LuminFieldLayout.Field<List<TValue>>(_owner, LuminCollectionLayouts.CollectionItems);
        }
    }
}

/// <summary>Presents the item list of an <see cref="ObservableCollection{T}"/> as a list.</summary>
/// <typeparam name="TValue">The element type.</typeparam>
/// <remarks>Holds only the ObservableCollection&lt;T&gt; reference, so reinterpreting that reference as the view is valid. <c>items</c> returns a
/// reference to the ObservableCollection&lt;T&gt;'s own field at its probed offset; see <see cref="LuminCollectionLayouts"/>.</remarks>
public readonly struct ObservableCollectionView<TValue>
{
    private readonly object _owner;

    /// <summary>Gets the ObservableCollection&lt;T&gt;'s item storage.</summary>
    public ref List<TValue> items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.ObservableCollectionItems, "ObservableCollection<T>");
            return ref LuminFieldLayout.Field<List<TValue>>(_owner, LuminCollectionLayouts.ObservableCollectionItems);
        }
    }
}

/// <summary>Presents the item list of an <see cref="ObservableCollection{T}"/>.</summary>
/// <typeparam name="TValue">The element type.</typeparam>
/// <remarks>Holds only the ObservableCollection&lt;T&gt; reference, so reinterpreting that reference as the view is valid. <c>items</c> returns a
/// reference to the ObservableCollection&lt;T&gt;'s own field at its probed offset; see <see cref="LuminCollectionLayouts"/>.</remarks>
public readonly struct ObservableCollectionIListView<TValue>
{
    private readonly object _owner;

    /// <summary>Gets the ObservableCollection&lt;T&gt;'s item storage.</summary>
    public ref IList<TValue> items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.ObservableCollectionItems, "ObservableCollection<T>");
            return ref LuminFieldLayout.Field<IList<TValue>>(_owner, LuminCollectionLayouts.ObservableCollectionItems);
        }
    }
}

/// <summary>Presents the wrapped list of a <see cref="ReadOnlyCollection{T}"/> as a list.</summary>
/// <typeparam name="TValue">The element type.</typeparam>
/// <remarks>Holds only the ReadOnlyCollection&lt;T&gt; reference, so reinterpreting that reference as the view is valid. <c>items</c> returns a
/// reference to the ReadOnlyCollection&lt;T&gt;'s own field at its probed offset; see <see cref="LuminCollectionLayouts"/>.</remarks>
public readonly struct ReadOnlyCollectionView<TValue>
{
    private readonly object _owner;

    /// <summary>Gets the ReadOnlyCollection&lt;T&gt;'s item storage.</summary>
    public ref List<TValue> items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.ReadOnlyCollectionList, "ReadOnlyCollection<T>");
            return ref LuminFieldLayout.Field<List<TValue>>(_owner, LuminCollectionLayouts.ReadOnlyCollectionList);
        }
    }
}

/// <summary>Presents the wrapped list of a <see cref="ReadOnlyCollection{T}"/>.</summary>
/// <typeparam name="TValue">The element type.</typeparam>
/// <remarks>Holds only the ReadOnlyCollection&lt;T&gt; reference, so reinterpreting that reference as the view is valid. <c>items</c> returns a
/// reference to the ReadOnlyCollection&lt;T&gt;'s own field at its probed offset; see <see cref="LuminCollectionLayouts"/>.</remarks>
public readonly struct ReadOnlyCollectionIListView<TValue>
{
    private readonly object _owner;

    /// <summary>Gets the ReadOnlyCollection&lt;T&gt;'s item storage.</summary>
    public ref IList<TValue> items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.ReadOnlyCollectionList, "ReadOnlyCollection<T>");
            return ref LuminFieldLayout.Field<IList<TValue>>(_owner, LuminCollectionLayouts.ReadOnlyCollectionList);
        }
    }
}

/// <summary>Presents the wrapped collection of a <see cref="ReadOnlyObservableCollection{T}"/>.</summary>
/// <typeparam name="TCollection">The element type.</typeparam>
/// <remarks>Holds only the ReadOnlyObservableCollection&lt;T&gt; reference, so reinterpreting that reference as the view is valid. <c>items</c> returns a
/// reference to the ReadOnlyObservableCollection&lt;T&gt;'s own field at its probed offset; see <see cref="LuminCollectionLayouts"/>.</remarks>
public readonly struct ReadOnlyObservableCollectionView<TCollection>
{
    private readonly object _owner;

    /// <summary>Gets the ReadOnlyObservableCollection&lt;T&gt;'s item storage.</summary>
    public ref ObservableCollection<TCollection> items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.ReadOnlyObservableCollectionList, "ReadOnlyObservableCollection<T>");
            return ref LuminFieldLayout.Field<ObservableCollection<TCollection>>(_owner, LuminCollectionLayouts.ReadOnlyObservableCollectionList);
        }
    }
}

/// <summary>Presents the storage fields of a <see cref="global::System.Runtime.CompilerServices.ReadOnlyCollectionBuilder{T}"/>.</summary>
/// <typeparam name="TValue">The element type.</typeparam>
/// <remarks>Holds only the builder reference, so reinterpreting that reference as the view is valid.</remarks>
public readonly struct ReadOnlyCollectionBuilderView<TValue>
{
    private readonly object _owner;

    /// <summary>Gets the backing array.</summary>
    public ref TValue[] items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.BuilderItems, "ReadOnlyCollectionBuilder<T>");
            return ref LuminFieldLayout.Field<TValue[]>(_owner, LuminCollectionLayouts.BuilderItems);
        }
    }

    /// <summary>Gets the size.</summary>
    public ref int size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            LuminCollectionLayouts.Require(LuminCollectionLayouts.BuilderSize, "ReadOnlyCollectionBuilder<T>");
            return ref LuminFieldLayout.Field<int>(_owner, LuminCollectionLayouts.BuilderSize);
        }
    }
}

/// <summary>Presents parallel pooled key and value arrays to the bulk <see cref="SortedList{TKey, TValue}"/> constructor.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <remarks>The constructor reads <c>Count</c>, <c>Keys</c> and <c>Values</c>; enumeration and pair copies are also available. Lookup and mutation are unsupported.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LuminSortedListSource<TKey, TValue> : IDictionary<TKey, TValue>
{
    private readonly TKey[] _keys;
    private readonly TValue[] _values;
    private readonly int _count;

    /// <summary>Initializes a new instance of <c>LuminSortedListSource</c>.</summary>
    /// <param name="keys">The key storage; the first <paramref name="count"/> entries are used.</param>
    /// <param name="values">The value storage; the first <paramref name="count"/> entries are used.</param>
    /// <param name="count">The number of pairs.</param>
    public LuminSortedListSource(TKey[] keys, TValue[] values, int count)
    {
        _keys = keys;
        _values = values;
        _count = count;
    }

    /// <summary>Gets the number of pairs.</summary>
    public int Count => _count;

    /// <summary>Gets the keys as a collection the constructor copies from.</summary>
    public ICollection<TKey> Keys => new global::System.ArraySegment<TKey>(_keys, 0, _count);

    /// <summary>Gets the values as a collection the constructor copies from.</summary>
    public ICollection<TValue> Values => new global::System.ArraySegment<TValue>(_values, 0, _count);

    /// <summary>Gets whether the source is read-only.</summary>
    public bool IsReadOnly => true;

    /// <summary>Unsupported.</summary>
    /// <param name="key">The key.</param>
    public TValue this[TKey key] { get => LuminPackExceptionHelper.ThrowNotSupportedException<TValue>(); set => LuminPackExceptionHelper.ThrowNotSupportedException(); }

    /// <summary>Unsupported.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void Add(TKey key, TValue value) => LuminPackExceptionHelper.ThrowNotSupportedException();

    /// <summary>Unsupported.</summary>
    /// <param name="item">The pair.</param>
    public void Add(KeyValuePair<TKey, TValue> item) => LuminPackExceptionHelper.ThrowNotSupportedException();

    /// <summary>Unsupported.</summary>
    public void Clear() => LuminPackExceptionHelper.ThrowNotSupportedException();

    /// <summary>Unsupported.</summary>
    /// <param name="item">The pair.</param>
    /// <returns>Never returns.</returns>
    public bool Contains(KeyValuePair<TKey, TValue> item) => LuminPackExceptionHelper.ThrowNotSupportedException<bool>();

    /// <summary>Unsupported.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Never returns.</returns>
    public bool ContainsKey(TKey key) => LuminPackExceptionHelper.ThrowNotSupportedException<bool>();

    /// <summary>Copies the pairs in storage order.</summary>
    /// <param name="array">The destination.</param>
    /// <param name="arrayIndex">The destination index.</param>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        for (int i = 0; i < _count; i++)
            array[arrayIndex + i] = new KeyValuePair<TKey, TValue>(_keys[i], _values[i]);
    }

    /// <summary>Enumerates the pairs in storage order, for runtimes whose bulk constructor enumerates the source.</summary>
    /// <returns>The pair enumerator.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
            yield return new KeyValuePair<TKey, TValue>(_keys[i], _values[i]);
    }

    /// <summary>Unsupported.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Never returns.</returns>
    public bool Remove(TKey key) => LuminPackExceptionHelper.ThrowNotSupportedException<bool>();

    /// <summary>Unsupported.</summary>
    /// <param name="item">The pair.</param>
    /// <returns>Never returns.</returns>
    public bool Remove(KeyValuePair<TKey, TValue> item) => LuminPackExceptionHelper.ThrowNotSupportedException<bool>();

    /// <summary>Unsupported.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">Never assigned.</param>
    /// <returns>Never returns.</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        value = default!;
        return LuminPackExceptionHelper.ThrowNotSupportedException<bool>(); }

    /// <summary>Enumerates the pairs in storage order.</summary>
    /// <returns>The pair enumerator.</returns>
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Layout-only collection helpers used by static collection formatters.
/// They intentionally know nothing about serialization; generated formatters
/// perform every element read/write through static extensions.
/// </summary>
public static partial class LuminPackCollectionAccess
{
    private const uint MaxDepth = 3;

    /// <summary>Obtains the backing <see cref="List{T}"/> of a wrapper, following nested ObjectModel wrappers.</summary>
    /// <param name="value">The wrapper whose storage is inspected.</param>
    /// <param name="depth">The number of wrappers already followed.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The innermost list, or null when the storage is another list type or nested too deeply.</returns>
    public static List<T>? GetUnderlyingIList<T>(
        Collection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth)
            return null;

        var view = LuminPackMarshal.As<Collection<T>, CollectionIListView<T>>(ref value);
        return Resolve(view.items, ref depth);
    }

    /// <summary>Obtains the backing <see cref="List{T}"/> of a wrapper, following nested ObjectModel wrappers.</summary>
    /// <param name="value">The wrapper whose storage is inspected.</param>
    /// <param name="depth">The number of wrappers already followed.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The innermost list, or null when the storage is another list type or nested too deeply.</returns>
    public static List<T>? GetUnderlyingIList<T>(
        ReadOnlyCollection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth)
            return null;

        var view = LuminPackMarshal.As<ReadOnlyCollection<T>, ReadOnlyCollectionIListView<T>>(ref value);
        return Resolve(view.items, ref depth);
    }

    /// <summary>Obtains the backing <see cref="List{T}"/> of a wrapper, following nested ObjectModel wrappers.</summary>
    /// <param name="value">The wrapper whose storage is inspected.</param>
    /// <param name="depth">The number of wrappers already followed.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The innermost list, or null when the storage is another list type or nested too deeply.</returns>
    public static List<T>? GetUnderlyingIList<T>(
        ObservableCollection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth)
            return null;

        var view = LuminPackMarshal.As<ObservableCollection<T>, ObservableCollectionIListView<T>>(ref value);
        return Resolve(view.items, ref depth);
    }

    /// <summary>Obtains the backing <see cref="List{T}"/> of a wrapper, following nested ObjectModel wrappers.</summary>
    /// <param name="value">The wrapper whose storage is inspected.</param>
    /// <param name="depth">The number of wrappers already followed.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The innermost list, or null when the storage is another list type or nested too deeply.</returns>
    public static List<T>? GetUnderlyingIList<T>(
        ReadOnlyObservableCollection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth)
            return null;

        var observable = LuminPackMarshal.As<ReadOnlyObservableCollection<T>, ReadOnlyObservableCollectionView<T>>(ref value);
        var view = LuminPackMarshal.As<ObservableCollection<T>, ObservableCollectionIListView<T>>(ref observable.items);
        return Resolve(view.items, ref depth);
    }

    /// <summary>Gets the key and value storage of a sorted list, whose first <c>Count</c> entries are its pairs in order.</summary>
    /// <param name="list">The non-null list.</param>
    /// <param name="keys">Receives the key storage, or null when the storage is not recognized.</param>
    /// <param name="values">Receives the value storage, or null when the storage is not recognized.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>Whether the storage was located; otherwise callers copy through the public key and value lists.</returns>
    public static bool TryGetSortedListStorage<TKey, TValue>(SortedList<TKey, TValue> list,
        out TKey[]? keys, out TValue[]? values) where TKey : notnull
    {
        if ((LuminCollectionLayouts.SortedListKeys | LuminCollectionLayouts.SortedListValues) < 0 || !SortedListStorage.Available)
        {
            keys = null;
            values = null;
            return false;
        }
        keys = LuminFieldLayout.Field<TKey[]>(list, LuminCollectionLayouts.SortedListKeys);
        values = LuminFieldLayout.Field<TValue[]>(list, LuminCollectionLayouts.SortedListValues);
        // A storage array shorter than the count cannot belong to the list; the public path reports any inconsistency.
        return keys.Length >= list.Count && values.Length >= list.Count;
    }

    /// <summary>Captures the validated ranges and mutation version of a sorted-list traversal.</summary>
    /// <param name="list">The non-null source collection.</param>
    /// <param name="keys">The complete key range, or an empty span when storage is unavailable.</param>
    /// <param name="values">The corresponding value range, or an empty span when storage is unavailable.</param>
    /// <param name="version">The mutation version to check before each pair and after the final pair.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>Whether checked storage traversal can replace the public enumerator.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryGetSortedListTraversal<TKey, TValue>(SortedList<TKey, TValue> list,
        out global::System.ReadOnlySpan<TKey> keys, out global::System.ReadOnlySpan<TValue> values, out int version) where TKey : notnull
    {
        keys = default;
        values = default;
        version = 0;
        if (!SortedListAppendStorage.Available || !TryGetSortedListStorage(list, out var keyArray, out var valueArray))
            return false;
        int count = list.Count;
        keys = new global::System.ReadOnlySpan<TKey>(keyArray, 0, count);
        values = new global::System.ReadOnlySpan<TValue>(valueArray, 0, count);
        version = LuminFieldLayout.Field<int>(list, SortedListAppendStorage.Version);
        return true;
    }

    /// <summary>Checks the version captured by a successful sorted-list traversal probe.</summary>
    /// <param name="list">The same collection passed to the traversal probe.</param>
    /// <param name="version">The captured mutation version.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckSortedListTraversalVersion<TKey, TValue>(SortedList<TKey, TValue> list, int version)
        where TKey : notnull
    {
        if (LuminFieldLayout.Field<int>(list, SortedListAppendStorage.Version) != version)
            LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
    }

    /// <summary>Gets the two stacks whose consecutive enumeration is the enumeration of an immutable queue.</summary>
    /// <param name="queue">The non-null queue.</param>
    /// <param name="forwards">Receives the stack of the oldest elements, or null when the storage is not recognized.</param>
    /// <param name="backwards">Receives the queue's cached reversal of its newer elements, or null when the storage is not recognized.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>Whether both stacks were located; otherwise callers enumerate the queue, which also caches the reversal.</returns>
    /// <remarks>The queue's enumerator walks the same two stacks, but through a shared generic state machine that costs
    /// several times a stack enumeration per element.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryGetImmutableQueueStacks<T>(ImmutableQueue<T> queue,
        out ImmutableStack<T>? forwards, out ImmutableStack<T>? backwards)
    {
        forwards = null;
        backwards = null;
        if (!ImmutableQueueStorage.Available)
            return false;
        var front = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.Forwards);
        var reversed = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.BackwardsReversed);
        if (front is null)
            return false;
        if (reversed is null)
        {
            // The reversal is created on first enumeration; an empty back stack needs none.
            var back = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.Backwards);
            if (back is null || !back.IsEmpty)
                return false;
            reversed = back;
        }
        forwards = front;
        backwards = reversed;
        return true;
    }

    /// <summary>Locates the stacks of <see cref="ImmutableQueue{T}"/> by name and type, and
    /// checks them against the public enumeration.</summary>
    private static class ImmutableQueueStorage
    {
        internal static readonly int Forwards = Offset("_forwards");
        internal static readonly int Backwards = Offset("_backwards");
        internal static readonly int BackwardsReversed = Offset("_backwardsReversed");
        internal static readonly bool Available = Forwards >= 0 && Backwards >= 0 && BackwardsReversed >= 0 && Check();

        /// <summary>Finds a stack field at the same offset in two instantiations.</summary>
        /// <param name="name">The field name.</param>
        /// <returns>The offset, or <see cref="LuminFieldLayout.Unavailable"/>.</returns>
        private static int Offset(string name)
        {
            try
            {
                const global::System.Reflection.BindingFlags flags = global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.NonPublic;
                if (typeof(ImmutableQueue<int>).GetField(name, flags)?.FieldType != typeof(ImmutableStack<int>) ||
                    typeof(ImmutableQueue<object>).GetField(name, flags)?.FieldType != typeof(ImmutableStack<object>))
                    return LuminFieldLayout.Unavailable;
                int first = LuminFieldLayout.ObjectField(typeof(ImmutableQueue<int>), name);
                int second = LuminFieldLayout.ObjectField(typeof(ImmutableQueue<object>), name);
                return first >= 0 && first == second ? first : LuminFieldLayout.Unavailable;
            }
            catch
            {
                return LuminFieldLayout.Unavailable;
            }
        }

        /// <summary>Checks that the stacks enumerate as the queue does, before and after the reversal is cached.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Check()
        {
            try
            {
                var front = ImmutableQueue<int>.Empty.Enqueue(1);
                if (!Matches(front, "1"))
                    return false;
                var queue = front.Enqueue(2).Enqueue(3);
                foreach (int _ in queue) { }
                return Matches(queue, "123") && Matches(queue.Dequeue(), "23");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Compares the stack walk with the expected digits.</summary>
        /// <param name="queue">The queue.</param>
        /// <param name="expected">The expected elements as digits.</param>
        /// <returns>Whether the walk produced exactly the digits.</returns>
        private static bool Matches(ImmutableQueue<int> queue, string expected)
        {
            if (!TryGetImmutableQueueStacksUnchecked(queue, out var forwards, out var backwards))
                return false;
            var text = new global::System.Text.StringBuilder();
            foreach (int item in forwards!)
                text.Append(item);
            foreach (int item in backwards!)
                text.Append(item);
            return text.ToString() == expected && string.Concat(queue) == expected;
        }

        /// <summary>Reads the stacks during the check, before availability is known.</summary>
        /// <param name="queue">The queue.</param>
        /// <param name="forwards">The front stack.</param>
        /// <param name="backwards">The reversed back stack.</param>
        /// <returns>Whether both stacks are present.</returns>
        private static bool TryGetImmutableQueueStacksUnchecked(ImmutableQueue<int> queue,
            out ImmutableStack<int>? forwards, out ImmutableStack<int>? backwards)
        {
            forwards = LuminFieldLayout.Field<ImmutableStack<int>?>(queue, Forwards);
            backwards = LuminFieldLayout.Field<ImmutableStack<int>?>(queue, BackwardsReversed);
            if (backwards is null)
            {
                var back = LuminFieldLayout.Field<ImmutableStack<int>?>(queue, Backwards);
                if (back is { IsEmpty: true })
                    backwards = back;
            }
            return forwards is not null && backwards is not null;
        }
    }

    /// <summary>Checks an exact sorted-list destination before the generated operation starts its original traversal.</summary>
    /// <typeparam name="TKey">The caller-approved framework key type.</typeparam>
    /// <typeparam name="TValue">The original value contract.</typeparam>
    /// <param name="list">The original destination.</param>
    /// <returns>Whether its default comparer and storage permit ordered appends.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanAppendSortedListOrdered<TKey, TValue>(SortedList<TKey, TValue> list) where TKey : notnull
        => SortedListAppendStorage.Available && list.GetType() == typeof(SortedList<TKey, TValue>) &&
           ReferenceEquals(list.Comparer, Comparer<TKey>.Default) &&
           (typeof(TKey) != typeof(string) || global::System.Globalization.CultureInfo.CurrentCulture.GetType() == typeof(global::System.Globalization.CultureInfo));

    /// <summary>Appends an increasing key without a binary search when the existing capacity and storage are valid.</summary>
    /// <typeparam name="TKey">The caller-approved framework key type without user comparison callbacks.</typeparam>
    /// <typeparam name="TValue">The original value contract.</typeparam>
    /// <param name="list">The exact default-comparer destination.</param>
    /// <param name="key">The key already parsed before its value.</param>
    /// <param name="value">The already parsed value.</param>
    /// <param name="validatedDestination">Whether the caller checked eligibility and retained that same destination reference.</param>
    /// <returns>Whether the pair was appended; otherwise the caller must retain its original Add operation.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryAppendSortedListOrdered<TKey, TValue>(SortedList<TKey, TValue> list, TKey key, TValue value, bool validatedDestination = false)
        where TKey : notnull
    {
        if ((!validatedDestination && !CanAppendSortedListOrdered(list)) || key is null)
            return false;
        int count = list.Count;
        var keys = LuminFieldLayout.Field<TKey[]>(list, LuminCollectionLayouts.SortedListKeys);
        var values = LuminFieldLayout.Field<TValue[]>(list, LuminCollectionLayouts.SortedListValues);
        if ((uint)count >= (uint)keys.Length || (uint)count >= (uint)values.Length)
            return false;
        if (count != 0 && Comparer<TKey>.Default.Compare(keys[count - 1], key) >= 0)
            return false;
        keys[count] = key;
        values[count] = value;
        LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.SortedListSize) = count + 1;
        ref int version = ref LuminFieldLayout.Field<int>(list, SortedListAppendStorage.Version);
        version = unchecked(version + 1);
        return true;
    }

    /// <summary>Locates and validates the version field independently from the unchanged sorted-list view gate.</summary>
    private static class SortedListAppendStorage
    {
        internal static readonly int Version = LuminFieldLayout.ObjectField(typeof(SortedList<int, int>), "version");
        internal static readonly bool Available = Version >= 0 && SortedListStorage.Available && Probe();

        /// <summary>Checks exact version increments and generic-family offset agreement through the public operations.</summary>
        /// <returns>Whether direct appends can preserve enumerator invalidation.</returns>
        private static bool Probe()
        {
            try
            {
                if (Version != LuminFieldLayout.ObjectField(typeof(SortedList<string, object>), "version"))
                    return false;
                var list = new SortedList<int, string>();
                int version = LuminFieldLayout.Field<int>(list, Version);
                list.Add(1, "a");
                if (LuminFieldLayout.Field<int>(list, Version) != unchecked(version + 1))
                    return false;
                list.Clear();
                return LuminFieldLayout.Field<int>(list, Version) == unchecked(version + 2);
            }
            catch
            {
                return false; }
        }
    }

    /// <summary>Fills a new, empty sorted list with the default comparer from read pairs that are already in key order.</summary>
    /// <param name="list">A list created with a capacity of at least <paramref name="count"/> and the default comparer.</param>
    /// <param name="keys">The keys, in the order they were read.</param>
    /// <param name="values">The values, in the order they were read.</param>
    /// <param name="count">The number of pairs.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>Whether the pairs were stored; false leaves the list empty for the bulk construction, which sorts and rejects duplicates.</returns>
    public static bool TryFillSortedList<TKey, TValue>(SortedList<TKey, TValue> list, TKey[] keys, TValue[] values, int count)
        where TKey : notnull
    {
        if (LuminCollectionLayouts.SortedListSize < 0 || !SortedListStorage.Available || list.Count != 0 || list.Capacity < count ||
            !ReferenceEquals(list.Comparer, Comparer<TKey>.Default))
            return false;
        var comparer = Comparer<TKey>.Default;
        if (keys[0] is null)
            return false;
        for (int i = 1; i < count; i++)
            if (keys[i] is null || comparer.Compare(keys[i - 1], keys[i]) >= 0)
                return false;
        global::System.Array.Copy(keys, LuminFieldLayout.Field<TKey[]>(list, LuminCollectionLayouts.SortedListKeys), count);
        global::System.Array.Copy(values, LuminFieldLayout.Field<TValue[]>(list, LuminCollectionLayouts.SortedListValues), count);
        LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.SortedListSize) = count;
        // Enumerators observe the same version advance as one Add per pair.
        if (SortedListAppendStorage.Version >= 0)
        {
            ref int version = ref LuminFieldLayout.Field<int>(list, SortedListAppendStorage.Version);
            version = unchecked(version + count);
        }
        return true;
    }

#if !NETSTANDARD2_1
    /// <summary>Gets the heap array of a priority queue, whose first <paramref name="count"/> nodes are the order <c>UnorderedItems</c> enumerates.</summary>
    /// <param name="queue">The non-null queue.</param>
    /// <param name="nodes">Receives the heap array, or null when the storage is not recognized.</param>
    /// <param name="count">Receives the number of nodes.</param>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <typeparam name="TPriority">The priority type.</typeparam>
    /// <returns>Whether the storage was located; otherwise callers enumerate <c>UnorderedItems</c>.</returns>
    public static bool TryGetPriorityQueueNodes<TElement, TPriority>(PriorityQueue<TElement, TPriority> queue,
        out (TElement Element, TPriority Priority)[]? nodes, out int count)
    {
        count = queue.Count;
        if ((LuminCollectionLayouts.PriorityQueueNodes | LuminCollectionLayouts.PriorityQueueSize) < 0 || !PriorityQueueStorage.Available)
        {
            nodes = null;
            return false;
        }
        nodes = LuminFieldLayout.Field<(TElement, TPriority)[]>(queue, LuminCollectionLayouts.PriorityQueueNodes);
        return nodes.Length >= count;
    }

    /// <summary>Captures a checked heap traversal with the public enumerator's mutation version.</summary>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <typeparam name="TPriority">The priority type.</typeparam>
    /// <param name="queue">The non-null priority queue.</param>
    /// <param name="nodes">The complete active node range, or an empty span when unavailable.</param>
    /// <param name="version">The version to check before each pair and after the last pair.</param>
    /// <returns>Whether generated code can retain public enumeration without creating the unordered-items view.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryGetPriorityQueueTraversal<TElement, TPriority>(PriorityQueue<TElement, TPriority> queue,
        out global::System.ReadOnlySpan<(TElement Element, TPriority Priority)> nodes, out int version)
    {
        nodes = default;
        version = 0;
        if (!PriorityQueueTraversal.Available || !TryGetPriorityQueueNodes(queue, out var storage, out int count))
            return false;
        nodes = new global::System.ReadOnlySpan<(TElement, TPriority)>(storage, 0, count);
        version = LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.PriorityQueueVersion);
        return true;
    }

    /// <summary>Checks the version captured by a successful priority-queue traversal probe.</summary>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <typeparam name="TPriority">The priority type.</typeparam>
    /// <param name="queue">The same queue passed to the traversal probe.</param>
    /// <param name="version">The captured mutation version.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckPriorityQueueTraversalVersion<TElement, TPriority>(PriorityQueue<TElement, TPriority> queue, int version)
    {
        if (LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.PriorityQueueVersion) != version)
            LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
    }

    /// <summary>Validates the optional version slot separately from the established binary node access.</summary>
    private static class PriorityQueueTraversal
    {
        internal static readonly bool Available = Check();

        /// <summary>Verifies public mutations against both representative generic layouts.</summary>
        /// <returns>Whether the located version tracks the public queue operations.</returns>
        private static bool Check()
        {
            try
            {
                return LuminCollectionLayouts.PriorityQueueVersion >= 0 && Check<int, int>() && Check<object, object>(); }
            catch
            {
                return false; }
        }

        /// <summary>Checks version changes and stable capacity queries on one queue instantiation.</summary>
        /// <typeparam name="TElement">The probe element type.</typeparam>
        /// <typeparam name="TPriority">The probe priority type.</typeparam>
        /// <returns>Whether the slot matches the public enumerator's mutation points.</returns>
        private static bool Check<TElement, TPriority>()
        {
            var queue = new PriorityQueue<TElement, TPriority>();
            ref int version = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.PriorityQueueVersion);
            int before = version;
            queue.Enqueue(default!, default!);
            if (version == before)
                return false;
            before = version;
            queue.EnsureCapacity(0);
            if (version != before)
                return false;
            queue.Dequeue();
            if (version == before)
                return false;
            before = version;
            queue.Clear();
            return version != before;
        }
    }

    /// <summary>Holds the one-time check of priority-queue storage.</summary>
    private static class PriorityQueueStorage
    {
        internal static readonly bool Available = Check();

        /// <summary>Checks the storage through the public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Check()
        {
            try
            {
                if ((LuminCollectionLayouts.PriorityQueueNodes | LuminCollectionLayouts.PriorityQueueSize) < 0)
                    return false;
                var queue = new PriorityQueue<string, int>();
                queue.Enqueue("c", 3);
                queue.Enqueue("a", 1);
                queue.Enqueue("b", 2);
                var nodes = LuminFieldLayout.Field<(string, int)[]>(queue, LuminCollectionLayouts.PriorityQueueNodes);
                int index = 0;
                foreach (var item in queue.UnorderedItems)
                    if (nodes[index++] != item)
                        return false;
                return index == 3 && LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.PriorityQueueSize) == 3;
            }
            catch
            {
                return false;
            }
        }
    }

#endif
    /// <summary>Holds the one-time check of sorted-list storage.</summary>
    private static class SortedListStorage
    {
        internal static readonly bool Available = Check();

        /// <summary>Checks the storage through the public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Check()
        {
            try
            {
                if ((LuminCollectionLayouts.SortedListKeys | LuminCollectionLayouts.SortedListValues | LuminCollectionLayouts.SortedListSize) < 0)
                    return false;
                var list = new SortedList<int, string> { [3] = "c", [1] = "a", [2] = "b" };
                var keys = LuminFieldLayout.Field<int[]>(list, LuminCollectionLayouts.SortedListKeys);
                var values = LuminFieldLayout.Field<string[]>(list, LuminCollectionLayouts.SortedListValues);
                if (keys.Length < 3 || keys[0] != 1 || keys[2] != 3 || values[0] != "a" || values[2] != "c" ||
                    LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.SortedListSize) != 3)
                    return false;
                var filled = new SortedList<int, string>(4);
                global::System.Array.Copy(new[] { 1, 5, 9 }, LuminFieldLayout.Field<int[]>(filled, LuminCollectionLayouts.SortedListKeys), 3);
                global::System.Array.Copy(new[] { "x", "y", "z" }, LuminFieldLayout.Field<string[]>(filled, LuminCollectionLayouts.SortedListValues), 3);
                LuminFieldLayout.Field<int>(filled, LuminCollectionLayouts.SortedListSize) = 3;
                filled.Add(7, "w");
                return filled.Count == 4 && filled.IndexOfKey(7) == 2 && filled[9] == "z" && filled.Keys[3] == 9 && filled.Remove(1) && filled.Values[0] == "y";
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Appends read items to an empty linked list.</summary>
    /// <param name="list">The list, empty unless a reused instance is filled.</param>
    /// <param name="items">The items, in the order they were read.</param>
    /// <param name="count">The number of items.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>An empty list of the exact type links nodes created by the public constructor in one pass through the probed
    /// node and list fields, producing the circular list <c>AddLast</c> produces; any other list appends item by item.</remarks>
    public static void FillLinkedList<T>(LinkedList<T> list, T[] items, int count)
    {
        if (count == 0)
            return;
        if (!LinkedListLinking.Available || list.Count != 0 || list.GetType() != typeof(LinkedList<T>))
        {
            for (int i = 0; i < count; i++)
                list.AddLast(items[i]);
            return;
        }
        Link(list, items, count);
    }

    /// <summary>Links new nodes into an empty list of the exact type.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="items">The items, in the order they were read or stored.</param>
    /// <param name="count">The number of items.</param>
    private static void Link<T>(LinkedList<T> list, T[] items, int count)
    {
        // Check the last source slot using the array's ordinary index exception.
        _ = items[count > 0 ? count - 1 : 0];
        ref T source = ref LuminPackMarshal.GetNotNullArrayReference(items);
        var first = new LinkedListNode<T>(source);
        LuminFieldLayout.Field<object>(first, LuminCollectionLayouts.LinkedNodeList) = list;
        var previous = first;
        for (int i = 1; i < count; i++)
        {
            var node = new LinkedListNode<T>(Unsafe.Add(ref source, (nint)(uint)i));
            LuminFieldLayout.Field<object>(node, LuminCollectionLayouts.LinkedNodeList) = list;
            LuminFieldLayout.Field<object>(node, LuminCollectionLayouts.LinkedNodePrevious) = previous;
            LuminFieldLayout.Field<object>(previous, LuminCollectionLayouts.LinkedNodeNext) = node;
            previous = node;
        }
        LuminFieldLayout.Field<object>(previous, LuminCollectionLayouts.LinkedNodeNext) = first;
        LuminFieldLayout.Field<object>(first, LuminCollectionLayouts.LinkedNodePrevious) = previous;
        LuminFieldLayout.Field<object>(list, LuminCollectionLayouts.LinkedListHead) = first;
        LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.LinkedListCount) = count;
        LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.LinkedListVersion)++;
    }

    /// <summary>Holds the one-time check of direct linked-list construction.</summary>
    private static class LinkedListLinking
    {
        internal static readonly bool Available = Check();

        /// <summary>Checks the storage through the public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Check()
        {
            try
            {
                if ((LuminCollectionLayouts.LinkedListHead | LuminCollectionLayouts.LinkedListCount | LuminCollectionLayouts.LinkedListVersion |
                     LuminCollectionLayouts.LinkedNodeList | LuminCollectionLayouts.LinkedNodeNext | LuminCollectionLayouts.LinkedNodePrevious) < 0)
                    return false;
                var list = new LinkedList<int>();
                Link(list, new[] { 1, 2, 3, 4 }, 4);
                var node = list.Find(3);
                list.Remove(2);
                list.AddFirst(0);
                list.AddLast(5);
                return node is not null && node.List == list && node.Previous!.Value == 1 && list.Count == 5 &&
                       global::System.Linq.Enumerable.SequenceEqual(list, new[] { 0, 1, 3, 4, 5 }) && list.Last!.Previous!.Value == 4 &&
                       global::System.Linq.Enumerable.SequenceEqual(global::System.Linq.Enumerable.Reverse(list), new[] { 5, 4, 3, 1, 0 });
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Copies one traversal of a concurrent stack into pooled storage, from the top element down.</summary>
    /// <param name="stack">The stack to traverse; its enumerator observes one consistent snapshot.</param>
    /// <param name="count">Receives the number of copied elements.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>Storage rented from <see cref="global::System.Buffers.ArrayPool{T}.Shared"/>; the caller returns it.</returns>
    /// <remarks>Replaces a separate <c>Count</c>, which walks the complete stack, followed by a second traversal.</remarks>
    public static T[] RentSnapshot<T>(global::System.Collections.Concurrent.ConcurrentStack<T> stack, out int count)
    {
        return RentSnapshotCore(stack, out count);
    }

    /// <summary>Copies the current items of a stack bottom first into the leading slots of a read buffer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stack">The existing stack.</param>
    /// <param name="destination">The read buffer, which holds at least <paramref name="length"/> slots.</param>
    /// <param name="length">The validated record length.</param>
    /// <remarks>A record stores the stack bottom first, so the existing items line up with the incoming ones from the bottom
    /// and can be read in place; slots beyond the existing items are cleared.</remarks>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public static void SeedConcurrentStack<T>(global::System.Collections.Concurrent.ConcurrentStack<T> stack, T[] destination, int length)
    {
        T[] snapshot = RentSnapshotCore(stack, out int count);
        int seeded = count < length ? count : length;
        for (int i = 0; i < seeded; i++)
            destination[i] = snapshot[count - 1 - i];
        if (seeded < length)
            global::System.Array.Clear(destination, seeded, length - seeded);
        global::System.Buffers.ArrayPool<T>.Shared.Return(snapshot, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }

    /// <summary>Leases a top-first snapshot of a stack.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stack">The stack.</param>
    /// <param name="count">The number of items copied.</param>
    /// <returns>The leased array.</returns>
    private static T[] RentSnapshotCore<T>(global::System.Collections.Concurrent.ConcurrentStack<T> stack, out int count)
    {
        T[] items = global::System.Buffers.ArrayPool<T>.Shared.Rent(16);
        int written = 0;
        foreach (T item in EnumerateConcurrentStack(stack))
        {
            if (written == items.Length)
                items = Grow(items, written);
            items[written++] = item;
        }
        count = written;
        return items;
    }

    /// <summary>Moves pooled snapshot storage into a pooled array of twice the length.</summary>
    /// <param name="items">The full pooled storage.</param>
    /// <param name="count">The number of valid elements.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The larger pooled storage holding the same elements.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T[] Grow<T>(T[] items, int count)
    {
        T[] larger = global::System.Buffers.ArrayPool<T>.Shared.Rent(checked(count * 2));
        global::System.Array.Copy(items, larger, count);
        global::System.Buffers.ArrayPool<T>.Shared.Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        return larger;
    }

    /// <summary>Classifies wrapper storage, testing the exact list type before the nested wrapper types.</summary>
    /// <param name="items">The storage held by a wrapper.</param>
    /// <param name="depth">The number of wrappers already followed.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The innermost list, or null when the storage is another list type or nested too deeply.</returns>
    private static List<T>? Resolve<T>(IList<T>? items, ref int depth)
    {
        if (items is null)
            return null;
        if (items.GetType() == typeof(List<T>))
            return Unsafe.As<List<T>>(items);
        switch (items)
        {
            case List<T> list: return list;
            case ReadOnlyObservableCollection<T> readOnlyObservable:
                depth++;
                return GetUnderlyingIList(readOnlyObservable, ref depth);
            case ObservableCollection<T> observable:
                depth++;
                return GetUnderlyingIList(observable, ref depth);
            case ReadOnlyCollection<T> readOnly:
                depth++;
                return GetUnderlyingIList(readOnly, ref depth);
            case Collection<T> collection:
                depth++;
                return GetUnderlyingIList(collection, ref depth);
            default:
                return null;
        }
    }
}
