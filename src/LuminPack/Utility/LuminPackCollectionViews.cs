namespace LuminPack.Code;

// Layout views copied from the former collection parsers. They are kept in the
// runtime assembly because source-generated extensions need the same layouts
// without depending on runtime formatter registration.
/// <summary>Provides bit array view functionality.</summary>
public sealed class BitArrayView
{
    /// <summary>Stores the m array.</summary>
    public int[] m_array;
    /// <summary>Stores the m length.</summary>
    public int m_length;
    /// <summary>Stores the version.</summary>
    public int _version;
}

/// <summary>Provides immutable array view functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
public struct ImmutableArrayView<T>
{
    /// <summary>Stores the array.</summary>
    public T[]? array;
}

/// <summary>Provides collection i list view functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public class CollectionIListView<TValue>
{
    /// <summary>Stores the items.</summary>
    public global::System.Collections.Generic.IList<TValue> items;
}

/// <summary>Provides collection view functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public class CollectionView<TValue>
{
    /// <summary>Stores the items.</summary>
    public global::System.Collections.Generic.List<TValue> items;
}

/// <summary>Provides observable collection view functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public class ObservableCollectionView<TValue>
{
    /// <summary>Stores the items.</summary>
    public global::System.Collections.Generic.List<TValue> items;
}

/// <summary>Provides observable collection i list view functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public class ObservableCollectionIListView<TValue>
{
    /// <summary>Stores the items.</summary>
    public global::System.Collections.Generic.IList<TValue> items;
}

/// <summary>Provides read only collection view functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public class ReadOnlyCollectionView<TValue>
{
    /// <summary>Stores the items.</summary>
    public global::System.Collections.Generic.List<TValue> items;
}

/// <summary>Provides read only collection i list view functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public class ReadOnlyCollectionIListView<TValue>
{
    /// <summary>Stores the items.</summary>
    public global::System.Collections.Generic.IList<TValue> items;
}

/// <summary>Provides read only observable collection view functionality.</summary>
/// <typeparam name="TCollection">The type represented by <c>TCollection</c>.</typeparam>
public class ReadOnlyObservableCollectionView<TCollection>
{
    /// <summary>Stores the items.</summary>
    public global::System.Collections.ObjectModel.ObservableCollection<TCollection> items;
}

/// <summary>Provides read only collection builder view functionality.</summary>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
public class ReadOnlyCollectionBuilderView<TValue>
{
    /// <summary>Stores the items.</summary>
    public TValue[] items;
    /// <summary>Stores the size.</summary>
    public int size;
}

/// <summary>
/// Layout-only collection helpers used by static collection formatters.
/// They intentionally know nothing about serialization; generated formatters
/// perform every element read/write through static extensions.
/// </summary>
public static class LuminPackCollectionAccess
{
    private const uint MaxDepth = 3;

    /// <summary>Obtains the underlying i list for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    public static global::System.Collections.Generic.List<T>? GetUnderlyingIList<T>(
        global::System.Collections.ObjectModel.Collection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth) return null;

        var view = LuminPackMarshal.As<global::System.Collections.ObjectModel.Collection<T>, CollectionIListView<T>>(ref value);
        switch (view.items)
        {
            case global::System.Collections.Generic.List<T> list: return list;
            case global::System.Collections.ObjectModel.ReadOnlyObservableCollection<T> readOnlyObservable:
                depth++;
                GetUnderlyingIList(readOnlyObservable, ref depth);
                break;
            case global::System.Collections.ObjectModel.ObservableCollection<T> observable:
                depth++;
                GetUnderlyingIList(observable, ref depth);
                break;
            case global::System.Collections.ObjectModel.ReadOnlyCollection<T> readOnly:
                depth++;
                GetUnderlyingIList(readOnly, ref depth);
                break;
            case global::System.Collections.ObjectModel.Collection<T> collection:
                depth++;
                GetUnderlyingIList(collection, ref depth);
                break;
        }

        return null;
    }

    /// <summary>Obtains the underlying i list for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    public static global::System.Collections.Generic.List<T>? GetUnderlyingIList<T>(
        global::System.Collections.ObjectModel.ReadOnlyCollection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth) return null;

        var view = LuminPackMarshal.As<global::System.Collections.ObjectModel.ReadOnlyCollection<T>, ReadOnlyCollectionIListView<T>>(ref value);
        switch (view.items)
        {
            case global::System.Collections.Generic.List<T> list: return list;
            case global::System.Collections.ObjectModel.ReadOnlyObservableCollection<T> readOnlyObservable:
                depth++;
                GetUnderlyingIList(readOnlyObservable, ref depth);
                break;
            case global::System.Collections.ObjectModel.ObservableCollection<T> observable:
                depth++;
                GetUnderlyingIList(observable, ref depth);
                break;
            case global::System.Collections.ObjectModel.ReadOnlyCollection<T> readOnly:
                depth++;
                GetUnderlyingIList(readOnly, ref depth);
                break;
            case global::System.Collections.ObjectModel.Collection<T> collection:
                depth++;
                GetUnderlyingIList(collection, ref depth);
                break;
        }

        return null;
    }

    /// <summary>Obtains the underlying i list for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    public static global::System.Collections.Generic.List<T>? GetUnderlyingIList<T>(
        global::System.Collections.ObjectModel.ObservableCollection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth) return null;

        var view = LuminPackMarshal.As<global::System.Collections.ObjectModel.ObservableCollection<T>, ObservableCollectionIListView<T>>(ref value);
        switch (view.items)
        {
            case global::System.Collections.Generic.List<T> list: return list;
            case global::System.Collections.ObjectModel.ReadOnlyObservableCollection<T> readOnlyObservable:
                depth++;
                GetUnderlyingIList(readOnlyObservable, ref depth);
                break;
            case global::System.Collections.ObjectModel.ObservableCollection<T> observable:
                depth++;
                GetUnderlyingIList(observable, ref depth);
                break;
            case global::System.Collections.ObjectModel.ReadOnlyCollection<T> readOnly:
                depth++;
                GetUnderlyingIList(readOnly, ref depth);
                break;
            case global::System.Collections.ObjectModel.Collection<T> collection:
                depth++;
                GetUnderlyingIList(collection, ref depth);
                break;
        }

        return null;
    }

    /// <summary>Obtains the underlying i list for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    public static global::System.Collections.Generic.List<T>? GetUnderlyingIList<T>(
        global::System.Collections.ObjectModel.ReadOnlyObservableCollection<T>? value, ref int depth)
    {
        if (depth >= MaxDepth) return null;

        var observable = LuminPackMarshal.As<global::System.Collections.ObjectModel.ReadOnlyObservableCollection<T>, ReadOnlyObservableCollectionView<T>>(ref value);
        var view = LuminPackMarshal.As<global::System.Collections.ObjectModel.ObservableCollection<T>, ObservableCollectionIListView<T>>(ref observable.items);
        switch (view.items)
        {
            case global::System.Collections.Generic.List<T> list: return list;
            case global::System.Collections.ObjectModel.ReadOnlyObservableCollection<T> readOnlyObservable:
                depth++;
                GetUnderlyingIList(readOnlyObservable, ref depth);
                break;
            case global::System.Collections.ObjectModel.ObservableCollection<T> observableCollection:
                depth++;
                GetUnderlyingIList(observableCollection, ref depth);
                break;
            case global::System.Collections.ObjectModel.ReadOnlyCollection<T> readOnly:
                depth++;
                GetUnderlyingIList(readOnly, ref depth);
                break;
            case global::System.Collections.ObjectModel.Collection<T> collection:
                depth++;
                GetUnderlyingIList(collection, ref depth);
                break;
        }

        return null;
    }

}
