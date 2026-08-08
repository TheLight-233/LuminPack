namespace LuminPack.Code;

// Layout views copied from the former collection parsers. They are kept in the
// runtime assembly because source-generated extensions need the same layouts
// without depending on runtime formatter registration.
public sealed class BitArrayView
{
    public int[] m_array;
    public int m_length;
    public int _version;
}

public struct ImmutableArrayView<T>
{
    public T[]? array;
}

public class CollectionIListView<TValue>
{
    public global::System.Collections.Generic.IList<TValue> items;
}

public class CollectionView<TValue>
{
    public global::System.Collections.Generic.List<TValue> items;
}

public class ObservableCollectionView<TValue>
{
    public global::System.Collections.Generic.List<TValue> items;
}

public class ObservableCollectionIListView<TValue>
{
    public global::System.Collections.Generic.IList<TValue> items;
}

public class ReadOnlyCollectionView<TValue>
{
    public global::System.Collections.Generic.List<TValue> items;
}

public class ReadOnlyCollectionIListView<TValue>
{
    public global::System.Collections.Generic.IList<TValue> items;
}

public class ReadOnlyObservableCollectionView<TCollection>
{
    public global::System.Collections.ObjectModel.ObservableCollection<TCollection> items;
}

public class ReadOnlyCollectionBuilderView<TValue>
{
    public TValue[] items;
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
