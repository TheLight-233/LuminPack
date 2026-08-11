using LuminPack;

namespace LuminPackUnitTest;

internal static class InterfaceCollectionReadRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(IListReferenceElementsRoundTrip), IListReferenceElementsRoundTrip);
        RunCase(results, nameof(IListValueElementsRoundTrip), IListValueElementsRoundTrip);
        RunCase(results, nameof(IListNullAndEmptyStayDistinct), IListNullAndEmptyStayDistinct);
        RunCase(results, nameof(InterfaceDictionariesRoundTrip), InterfaceDictionariesRoundTrip);
    }

    private static void IListReferenceElementsRoundTrip()
    {
        IList<string> source = new List<string> { "alpha", null!, "gamma", "delta" };
        IList<string> value = LuminPackSerializer.Deserialize<IList<string>>(
            LuminPackSerializer.Serialize(source));

        Assert(value is List<string>, "IList<T> no longer materialized as the established List<T> implementation.");
        Assert(value.SequenceEqual(source), "IList<string> lost a reference element during direct backing-array fill.");

        IEnumerator<string> enumerator = value.GetEnumerator();
        Assert(enumerator.MoveNext(), "The deserialized IList<string> unexpectedly enumerated as empty.");
        value.Add("tail");
        AssertThrows<InvalidOperationException>(
            () => enumerator.MoveNext(),
            "A fresh-list backing-array fill left mutation/version tracking invalid.");
    }

    private static void IListValueElementsRoundTrip()
    {
        IList<int> source = new List<int> { 1, 2, 3, 5, 8, 13 };
        IList<int> value = LuminPackSerializer.Deserialize<IList<int>>(
            LuminPackSerializer.Serialize(source));

        Assert(value.SequenceEqual(source), "IList<int> lost a value element during direct backing-array fill.");
    }

    private static void IListNullAndEmptyStayDistinct()
    {
        IList<string> empty = new List<string>();
        IList<string> emptyRoundTrip = LuminPackSerializer.Deserialize<IList<string>>(
            LuminPackSerializer.Serialize(empty));
        Assert(emptyRoundTrip is List<string> { Count: 0 }, "An empty IList<T> was decoded as null or a different implementation.");

        IList<string>? source = null;
        IList<string>? nullRoundTrip = LuminPackSerializer.Deserialize<IList<string>?>(
            LuminPackSerializer.Serialize(source));
        Assert(nullRoundTrip is null, "A null IList<T> was decoded as an empty collection.");
    }

    private static void InterfaceDictionariesRoundTrip()
    {
        IDictionary<string, int> source = new Dictionary<string, int>
        {
            ["alpha"] = 1,
            ["beta"] = 2,
            ["gamma"] = 3,
            ["delta"] = 4
        };
        IDictionary<string, int> dictionary = LuminPackSerializer.Deserialize<IDictionary<string, int>>(
            LuminPackSerializer.Serialize(source));
        Assert(dictionary.Count == source.Count && source.All(pair => dictionary[pair.Key] == pair.Value),
            "IDictionary<TKey,TValue> changed values while using header-sized preallocation.");

        IReadOnlyDictionary<string, int> readOnlySource = new Dictionary<string, int>(source);
        IReadOnlyDictionary<string, int> readOnly = LuminPackSerializer.Deserialize<IReadOnlyDictionary<string, int>>(
            LuminPackSerializer.Serialize(readOnlySource));
        Assert(readOnly.Count == readOnlySource.Count && readOnlySource.All(pair => readOnly[pair.Key] == pair.Value),
            "IReadOnlyDictionary<TKey,TValue> changed values while using header-sized preallocation.");

        IDictionary<string, int> empty = new Dictionary<string, int>();
        IDictionary<string, int> emptyRoundTrip = LuminPackSerializer.Deserialize<IDictionary<string, int>>(
            LuminPackSerializer.Serialize(empty));
        Assert(emptyRoundTrip.Count == 0, "An empty IDictionary<TKey,TValue> did not remain empty.");

        IDictionary<string, int>? nullSource = null;
        IDictionary<string, int>? nullRoundTrip = LuminPackSerializer.Deserialize<IDictionary<string, int>?>(
            LuminPackSerializer.Serialize(nullSource));
        Assert(nullRoundTrip is null, "A null IDictionary<TKey,TValue> was decoded as an empty dictionary.");
    }

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
