using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>
/// Checks that every write path, fresh read, reuse read and size evaluator of one collection family agree on the representation
/// of elements whose own formatter differs from their raw storage: Nullable, KeyValuePair, specialized-mode and portable structs.
/// </summary>
[Collection(nameof(RuntimeStateCollection))]
public partial class ReuseFreshParityTest
{
    /// <summary>Checks Nullable int elements, the reported Dictionary&lt;int, int?&gt; reuse failure and its relatives.</summary>
    [Fact]
    public void NullableIntElementsAgreeInEveryFamily() => RunNullableInt(i => i % 3 == 1 ? null : i, i => i, comparable: true);

    /// <summary>Checks Nullable long elements.</summary>
    [Fact]
    public void NullableLongElementsAgreeInEveryFamily() => RunNullableLong(i => i % 3 == 1 ? null : i * 100000000000L, i => i * 7L, comparable: true);

    /// <summary>Checks padded KeyValuePair elements, which their formatter writes member by member.</summary>
    [Fact]
    public void KeyValuePairElementsAgreeInEveryFamily()
        => RunKeyValuePair(i => new(i, i * 5000000000L), i => new(i, i), comparable: false);

    /// <summary>Checks version-tolerant structs whose CLR storage is unmanaged.</summary>
    [Fact]
    public void VersionTolerantStructElementsAgreeInEveryFamily()
        => RunVersioned(i => new() { A = i, B = i * 2 }, i => new() { A = i, B = i }, comparable: false);

    /// <summary>Checks structs with padding and ValueTuple storage.</summary>
    [Fact]
    public void PaddedStorageElementsAgreeInEveryFamily()
    {
        RunPadded(i => new() { Flag = (byte)i, Wide = i * 99L }, i => new() { Flag = (byte)i, Wide = i }, comparable: false);
        RunIntByteTuple(i => (i, (byte)(i + 1)), i => (i, (byte)i), comparable: false);
        RunByteIntTuple(i => ((byte)i, i + 1), i => ((byte)i, i), comparable: false);
        RunByteLongTuple(i => ((byte)i, i * 99L), i => ((byte)i, i), comparable: false);
    }

    /// <summary>Checks opted-in portable structs.</summary>
    [Fact]
    public void PortableStructElementsAgreeInEveryFamily()
        => RunPortable(i => new() { Flag = (byte)i, Wide = i * 99L }, i => new() { Flag = (byte)i, Wide = i }, comparable: false);

    /// <summary>Checks a member dictionary of Nullable values inside a reused object.</summary>
    [Fact]
    public void MemberDictionaryOfNullablesReusesTheWriterContract()
    {
        var source = new NullableDictionaryHolder { Values = new() { [1] = 10, [2] = null }, Tail = 77 };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var target = new NullableDictionaryHolder { Values = new() { [1] = 5, [9] = 9 }, Tail = 1 };
        var members = target.Values;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(members, target!.Values);
        Assert.Equal(source.Values.OrderBy(pair => pair.Key), target.Values!.OrderBy(pair => pair.Key));
        Assert.Equal(77, target.Tail);
    }

    /// <summary>Checks a custom comparer survives reuse of a raw-pair dictionary.</summary>
    [Fact]
    public void RawPairDictionaryKeepsItsComparerOnReuse()
    {
        var source = new Dictionary<int, int?> { [1] = 10, [2] = null, [3] = 30 };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var comparer = new ReverseComparer();
        var target = new Dictionary<int, int?>(comparer) { [1] = 5, [2] = 6 };
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(comparer, target.Comparer);
        Assert.Equal(source.OrderBy(pair => pair.Key), target.OrderBy(pair => pair.Key));
    }

    /// <summary>Reads a binary record into an existing or default destination.</summary>
    /// <typeparam name="T">The collection type.</typeparam>
    /// <param name="bytes">The record.</param>
    /// <param name="value">The destination.</param>
    private delegate void ReadBinary<T>(byte[] bytes, ref T value);

    /// <summary>Reads a JSON document into an existing or default destination.</summary>
    /// <typeparam name="T">The collection type.</typeparam>
    /// <param name="json">The document.</param>
    /// <param name="value">The destination.</param>
    private delegate void ReadJson<T>(string json, ref T value);

    /// <summary>Projects any sequence to an ordered array view.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The sequence.</param>
    /// <returns>The elements in enumeration order.</returns>
    private static IEnumerable Ordered<T>(IEnumerable<T> items) => items.ToArray();

    /// <summary>Round-trips one value through a fresh read, a reused read and the JSON document.</summary>
    /// <typeparam name="T">The collection type.</typeparam>
    /// <param name="name">The case name used in failure text.</param>
    /// <param name="source">The serialized value.</param>
    /// <param name="stale">Creates a destination holding stale content.</param>
    /// <param name="view">Projects the value to an enumerable, when it is not one itself.</param>
    /// <param name="write">The concrete binary writer of <typeparamref name="T"/>.</param>
    /// <param name="read">The concrete binary reader of <typeparamref name="T"/>.</param>
    /// <param name="writeJson">The concrete JSON writer of <typeparamref name="T"/>.</param>
    /// <param name="readJson">The concrete JSON reader of <typeparamref name="T"/>.</param>
    private static void Check<T>(string name, T source, Func<T> stale, Func<T, IEnumerable>? view, Func<T, byte[]> write,
        ReadBinary<T> read, Func<T, string> writeJson, ReadJson<T> readJson)
    {
        view ??= item => (IEnumerable)item!;
        byte[] bytes = write(source);
        string expected = Describe(view(source));
        T fresh = default!;
        read(bytes, ref fresh);
        Assert.True(expected == Describe(view(fresh)), name + ": a fresh read decoded a different value.");
        T reused = stale();
        read(bytes, ref reused);
        Assert.True(expected == Describe(view(reused)), name + ": a reuse read decoded a different value.");
        string json = writeJson(source);
        T jsonFresh = default!;
        readJson(json, ref jsonFresh);
        Assert.True(expected == Describe(view(jsonFresh)), name + ": a fresh JSON read decoded a different value.");
        T jsonReused = stale();
        readJson(json, ref jsonReused);
        Assert.True(expected == Describe(view(jsonReused)), name + ": a reused JSON read decoded a different value.");
    }

    /// <summary>Renders a collection's content, ordered only where the collection itself is ordered.</summary>
    /// <param name="collection">The collection to describe.</param>
    /// <returns>A deterministic text form.</returns>
    private static string Describe(IEnumerable collection)
    {
        var items = new List<string>();
        foreach (object? item in collection) items.Add(item is IEnumerable inner and not string ? "[" + Describe(inner) + "]" : item?.ToString() ?? "null");
        string type = collection.GetType().Name;
        bool ordered = collection is IDictionary or Array || type.StartsWith("List", StringComparison.Ordinal) || type.StartsWith("Immutable", StringComparison.Ordinal) ||
            type.StartsWith("Sorted", StringComparison.Ordinal) || type.StartsWith("ConcurrentQueue", StringComparison.Ordinal);
        if (!ordered) items.Sort(StringComparer.Ordinal);
        return string.Join("|", items);
    }

    /// <summary>Creates a sequence of values from consecutive seeds.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="factory">Creates one element.</param>
    /// <param name="start">The first seed.</param>
    /// <param name="count">The number of elements.</param>
    /// <returns>The elements.</returns>
    private static IEnumerable<T> Seq<T>(Func<int, T> factory, int start, int count = 5) => Enumerable.Range(start, count).Select(factory);

    /// <summary>Creates a dictionary from consecutive seeds.</summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="V">The value type.</typeparam>
    /// <param name="keys">Creates one key.</param>
    /// <param name="values">Creates one value.</param>
    /// <param name="start">The first seed.</param>
    /// <param name="count">The number of entries.</param>
    /// <returns>The dictionary.</returns>
    private static Dictionary<K, V> Dict<K, V>(Func<int, K> keys, Func<int, V> values, int start, int count = 5) where K : notnull
        => Enumerable.Range(start, count).ToDictionary(keys, values);

    /// <summary>Creates a two-column grid from consecutive seeds.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="factory">Creates one element.</param>
    /// <param name="rows">The row count.</param>
    /// <returns>The grid.</returns>
    private static T[,] Grid<T>(Func<int, T> factory, int rows = 2)
    {
        var grid = new T[rows, 2];
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < 2; column++)
                grid[row, column] = factory(row * 2 + column);
        return grid;
    }

    /// <summary>An equality comparer distinct from the default instance.</summary>
    private sealed class ReverseComparer : IEqualityComparer<int>
    {
        /// <summary>Compares two keys.</summary>
        /// <param name="x">The first key.</param>
        /// <param name="y">The second key.</param>
        /// <returns>Whether the keys are equal.</returns>
        public bool Equals(int x, int y) => x == y;

        /// <summary>Hashes a key.</summary>
        /// <param name="obj">The key.</param>
        /// <returns>The hash code.</returns>
        public int GetHashCode(int obj) => ~obj;
    }
}

/// <summary>An unmanaged struct whose natural layout contains padding.</summary>
[LuminPackable]
public partial struct ParityPadded
{
    /// <summary>The narrow member preceding padding.</summary>
    public byte Flag;
    /// <summary>The wide member.</summary>
    public long Wide;

    /// <summary>Renders the members for content comparison.</summary>
    /// <returns>The text form.</returns>
    public override string ToString() => Flag + ":" + Wide;
}

/// <summary>An unmanaged struct that uses the version-tolerant record.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public partial struct ParityVersioned
{
    /// <summary>The first member.</summary>
    public int A;
    /// <summary>The second member.</summary>
    public int B;

    /// <summary>Renders the members for content comparison.</summary>
    /// <returns>The text form.</returns>
    public override string ToString() => A + "," + B;
}

/// <summary>An unmanaged struct stored as its declaration-ordered portable fields.</summary>
[LuminPackable]
[LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct ParityPortable
{
    /// <summary>The narrow member preceding padding.</summary>
    public byte Flag;
    /// <summary>The wide member.</summary>
    public long Wide;

    /// <summary>Renders the members for content comparison.</summary>
    /// <returns>The text form.</returns>
    public override string ToString() => Flag + ":" + Wide;
}

/// <summary>Holds a dictionary of Nullable values.</summary>
[LuminPackable]
public partial class NullableDictionaryHolder
{
    /// <summary>The values.</summary>
    public Dictionary<int, int?>? Values;
    /// <summary>A member following the dictionary.</summary>
    public int Tail;
}
