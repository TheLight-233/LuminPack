using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the collection write and read fast paths against the established count-plus-elements wire.</summary>
public class CollectionFastPathTest
{
    private static readonly int[] Lengths = { 0, 1, 2, 31, 32, 33, 64, 200 };

    /// <summary>Creates distinct integers.</summary>
    /// <param name="length">The element count.</param>
    /// <returns>The integers.</returns>
    private static int[] Ints(int length) => Enumerable.Range(0, length).Select(i => i * 7919 % 100003 - 50000).ToArray();

    /// <summary>Creates strings including null entries.</summary>
    /// <param name="length">The element count.</param>
    /// <returns>The strings.</returns>
    private static string?[] Strings(int length) => Enumerable.Range(0, length).Select(i => i % 7 == 3 ? null : "item_" + i + (i % 5 == 0 ? "\u00E9" : "")).ToArray();

    /// <summary>Checks every sequence interface writes the bytes of the equivalent list, whatever its runtime implementation.</summary>
    [Fact]
    public void SequenceInterfacesMatchTheListWire()
    {
        foreach (int length in Lengths)
        {
            var ints = Ints(length);
            byte[] expected = LuminPackSerializer.Serialize(ints.ToList());
            foreach (IEnumerable<int> source in new IEnumerable<int>[] { ints.ToList(), ints.ToArray(), new LinkedList<int>(ints), Yield(ints) })
            {
                Assert.Equal(expected, LuminPackSerializer.Serialize<IEnumerable<int>>(source));
                if (source is ICollection<int> collection)
                {
                    Assert.Equal(expected, LuminPackSerializer.Serialize<ICollection<int>>(collection));
                    Assert.Equal(expected, LuminPackSerializer.Serialize<IReadOnlyCollection<int>>((IReadOnlyCollection<int>)collection));
                }
                if (source is IList<int> list)
                {
                    Assert.Equal(expected, LuminPackSerializer.Serialize<IList<int>>(list));
                    Assert.Equal(expected, LuminPackSerializer.Serialize<IReadOnlyList<int>>((IReadOnlyList<int>)list));
                }
            }
            Assert.Equal(ints, LuminPackSerializer.Deserialize<IEnumerable<int>>(expected));
            Assert.Equal(ints, LuminPackSerializer.Deserialize<IList<int>>(expected));
            Assert.Equal(ints, LuminPackSerializer.Deserialize<IReadOnlyCollection<int>>(expected));

            var strings = Strings(length);
            byte[] expectedStrings = LuminPackSerializer.Serialize(strings.ToList());
            Assert.Equal(expectedStrings, LuminPackSerializer.Serialize<IEnumerable<string?>>(strings.ToArray()));
            Assert.Equal(expectedStrings, LuminPackSerializer.Serialize<IList<string?>>(strings.ToList()));
            Assert.Equal(expectedStrings, LuminPackSerializer.Serialize<ICollection<string?>>(strings.ToList()));
            Assert.Equal(strings, LuminPackSerializer.Deserialize<IList<string?>>(expectedStrings));
            Assert.Equal(strings, LuminPackSerializer.Deserialize<IReadOnlyList<string?>>(expectedStrings));
        }

        // Correctness fix: an empty list of references used to be written as a null collection.
        var empty = LuminPackSerializer.Deserialize<ICollection<string>>(LuminPackSerializer.Serialize<ICollection<string>>(new List<string>()));
        Assert.NotNull(empty);
        Assert.Empty(empty);

        static IEnumerable<int> Yield(int[] values) { foreach (int value in values) yield return value; }
    }

    /// <summary>Checks dictionary and set interfaces write the bytes of their concrete counterparts.</summary>
    [Fact]
    public void DictionaryAndSetInterfacesMatchTheConcreteWire()
    {
        foreach (int length in Lengths)
        {
            var ints = Ints(length);
            var dictionary = ints.ToDictionary(i => i, i => i * 3);
            byte[] expected = LuminPackSerializer.Serialize(dictionary);
            Assert.Equal(expected, LuminPackSerializer.Serialize<IDictionary<int, int>>(dictionary));
            Assert.Equal(expected, LuminPackSerializer.Serialize<IReadOnlyDictionary<int, int>>(dictionary));
            var sorted = new SortedDictionary<int, int>(dictionary);
            Assert.Equal(LuminPackSerializer.Serialize(sorted), LuminPackSerializer.Serialize<IDictionary<int, int>>(sorted));
            Assert.Equal(dictionary, LuminPackSerializer.Deserialize<IReadOnlyDictionary<int, int>>(expected));

            var set = new HashSet<int>(ints);
            Assert.Equal(LuminPackSerializer.Serialize(set), LuminPackSerializer.Serialize<ISet<int>>(set));
            Assert.Equal(LuminPackSerializer.Serialize(set), LuminPackSerializer.Serialize<IReadOnlySet<int>>(set));
            var sortedSet = new SortedSet<int>(ints);
            Assert.Equal(LuminPackSerializer.Serialize(sortedSet), LuminPackSerializer.Serialize<ISet<int>>(sortedSet));
            Assert.True(set.SetEquals(LuminPackSerializer.Deserialize<ISet<int>>(LuminPackSerializer.Serialize<ISet<int>>(set))!));
        }
    }

    /// <summary>Checks concurrent collections publish the counted traversal and rebuild with the established order.</summary>
    [Fact]
    public void ConcurrentCollectionsRoundTrip()
    {
        foreach (int length in Lengths)
        {
            var ints = Ints(length);
            var dictionary = new ConcurrentDictionary<int, int>(ints.ToDictionary(i => i, i => i ^ 0x55));
            byte[] bytes = LuminPackSerializer.Serialize(dictionary);
            Assert.Equal(length, BitConverter.ToInt32(bytes, 0));
            Assert.Equal(4 + length * 8, bytes.Length);
            Assert.Equal(dictionary.OrderBy(p => p.Key), LuminPackSerializer.Deserialize<ConcurrentDictionary<int, int>>(bytes)!.OrderBy(p => p.Key));
            var reused = new ConcurrentDictionary<int, int>(StringComparerLike.Instance);
            reused[123456] = 1;
            LuminPackSerializer.Deserialize(bytes, ref reused);
            Assert.Same(StringComparerLike.Instance, reused!.Comparer);
            Assert.Equal(dictionary.OrderBy(p => p.Key), reused.OrderBy(p => p.Key));

            var queue = new ConcurrentQueue<int>(ints);
            Assert.Equal(ints, LuminPackSerializer.Deserialize<ConcurrentQueue<int>>(LuminPackSerializer.Serialize(queue)));

            var stack = new ConcurrentStack<int>(ints);
            var stackCopy = LuminPackSerializer.Deserialize<ConcurrentStack<int>>(LuminPackSerializer.Serialize(stack))!;
            Assert.Equal(stack.ToArray(), stackCopy.ToArray());
            Assert.Equal(LuminPackSerializer.Serialize(stack.Reverse().ToList()), LuminPackSerializer.Serialize(stack));

            var strings = new ConcurrentStack<string?>(Strings(length));
            Assert.Equal(strings.ToArray(), LuminPackSerializer.Deserialize<ConcurrentStack<string?>>(LuminPackSerializer.Serialize(strings))!.ToArray());

            var bag = new ConcurrentBag<int>(ints);
            Assert.Equal(ints.OrderBy(i => i), LuminPackSerializer.Deserialize<ConcurrentBag<int>>(LuminPackSerializer.Serialize(bag))!.OrderBy(i => i));

            var blocking = new BlockingCollection<int>();
            foreach (int value in ints) blocking.Add(value);
            var blockingCopy = LuminPackSerializer.Deserialize<BlockingCollection<int>>(LuminPackSerializer.Serialize(blocking))!;
            Assert.Equal(length, blockingCopy.Count);
            blockingCopy.Add(int.MinValue);
            Assert.Equal(ints.Append(int.MinValue), blockingCopy.Take(length + 1).ToArray());
        }
    }

    /// <summary>Checks sorted collections, including the bulk fresh constructions and the reused-instance paths.</summary>
    [Fact]
    public void SortedCollectionsRoundTrip()
    {
        foreach (int length in Lengths)
        {
            var ints = Ints(length);
            var set = new SortedSet<int>(ints);
            byte[] setBytes = LuminPackSerializer.Serialize(set);
            Assert.Equal(LuminPackSerializer.Serialize(set.ToList()), setBytes);
            Assert.Equal(set, LuminPackSerializer.Deserialize<SortedSet<int>>(setBytes));
            var descending = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a))) { 1, 2 };
            LuminPackSerializer.Deserialize(setBytes, ref descending);
            Assert.Equal(set.Reverse(), descending);

            var list = new SortedList<int, string?>(ints.ToDictionary(i => i, i => i % 3 == 0 ? null : i.ToString()));
            byte[] listBytes = LuminPackSerializer.Serialize(list);
            var listCopy = LuminPackSerializer.Deserialize<SortedList<int, string?>>(listBytes)!;
            Assert.Equal(list.Keys, listCopy.Keys);
            Assert.Equal(list.Values, listCopy.Values);
            listCopy.Add(int.MaxValue, "tail");
            Assert.Equal(int.MaxValue, listCopy.Keys[^1]);

            var dictionary = new SortedDictionary<int, int>(ints.ToDictionary(i => i, i => -i));
            Assert.Equal(dictionary, LuminPackSerializer.Deserialize<SortedDictionary<int, int>>(LuminPackSerializer.Serialize(dictionary)));
        }

        // Duplicate keys still reject the payload on the bulk path.
        byte[] duplicate = BitConverter.GetBytes(2).Concat(BitConverter.GetBytes(5)).Concat(BitConverter.GetBytes(1))
            .Concat(BitConverter.GetBytes(5)).Concat(BitConverter.GetBytes(2)).ToArray();
        Assert.ThrowsAny<ArgumentException>(() => LuminPackSerializer.Deserialize<SortedList<int, int>>(duplicate));
    }

    /// <summary>Checks immutable collections and their interfaces round trip with reference and raw elements.</summary>
    [Fact]
    public void ImmutableCollectionsRoundTrip()
    {
        foreach (int length in Lengths)
        {
            var ints = Ints(length);
            var strings = Strings(length);
            Assert.Equal(ints, LuminPackSerializer.Deserialize<ImmutableList<int>>(LuminPackSerializer.Serialize(ints.ToImmutableList())));
            Assert.Equal(strings, LuminPackSerializer.Deserialize<ImmutableList<string?>>(LuminPackSerializer.Serialize(strings.ToImmutableList())));
            Assert.Equal(strings, LuminPackSerializer.Deserialize<IImmutableList<string?>>(LuminPackSerializer.Serialize<IImmutableList<string?>>(strings.ToImmutableList())));
            Assert.Equal(ints.OrderBy(i => i), LuminPackSerializer.Deserialize<ImmutableSortedSet<int>>(LuminPackSerializer.Serialize(ints.ToImmutableSortedSet())));
            var sortedDictionary = ints.ToImmutableSortedDictionary(i => i, i => i + 1);
            Assert.Equal(sortedDictionary, LuminPackSerializer.Deserialize<ImmutableSortedDictionary<int, int>>(LuminPackSerializer.Serialize(sortedDictionary)));

            var array = strings.ToImmutableArray();
            Assert.Equal(LuminPackSerializer.Serialize(strings.ToList()), LuminPackSerializer.Serialize(array));
            var arrayCopy = LuminPackSerializer.Deserialize<ImmutableArray<string?>>(LuminPackSerializer.Serialize(array));
            Assert.False(arrayCopy.IsDefault);
            Assert.Equal(strings, arrayCopy);
            Assert.Equal(ints, LuminPackSerializer.Deserialize<ImmutableArray<int>>(LuminPackSerializer.Serialize(ints.ToImmutableArray())));

            var queue = ImmutableQueue.CreateRange(strings);
            byte[] queueBytes = LuminPackSerializer.Serialize(queue);
            Assert.Equal(LuminPackSerializer.Serialize(strings.ToList()), queueBytes);
            Assert.Equal(strings, LuminPackSerializer.Deserialize<ImmutableQueue<string?>>(queueBytes));
            Assert.Equal(strings, LuminPackSerializer.Deserialize<IImmutableQueue<string?>>(LuminPackSerializer.Serialize<IImmutableQueue<string?>>(queue)));

            var stack = ImmutableStack.CreateRange(ints);
            byte[] stackBytes = LuminPackSerializer.Serialize(stack);
            Assert.Equal(LuminPackSerializer.Serialize(stack.ToList()), stackBytes);
            Assert.Equal(stack, LuminPackSerializer.Deserialize<ImmutableStack<int>>(stackBytes));
            Assert.Equal(stack, LuminPackSerializer.Deserialize<IImmutableStack<int>>(LuminPackSerializer.Serialize<IImmutableStack<int>>(stack)));
        }
    }

    /// <summary>Checks contiguous wrappers and ObjectModel wrappers, including nested storage and non-list reuse.</summary>
    [Fact]
    public void WrappersRoundTrip()
    {
        foreach (int length in Lengths)
        {
            var ints = Ints(length);
            byte[] expected = LuminPackSerializer.Serialize(ints);
            Assert.Equal(expected, LuminPackSerializer.Serialize(new Memory<int>(ints)));
            Assert.Equal(expected, LuminPackSerializer.Serialize(new ReadOnlyMemory<int>(ints)));
            Assert.Equal(expected, LuminPackSerializer.Serialize(new ArraySegment<int>(ints)));
            Assert.Equal(ints, LuminPackSerializer.Deserialize<Memory<int>>(expected).ToArray());
            Assert.Equal(ints, LuminPackSerializer.Deserialize<ArraySegment<int>>(expected).ToArray());
            var strings = Strings(length);
            Assert.Equal(strings, LuminPackSerializer.Deserialize<ReadOnlyMemory<string?>>(LuminPackSerializer.Serialize(new ReadOnlyMemory<string?>(strings))).ToArray());

            var nested = new ReadOnlyCollection<int>(new ObservableCollection<int>(ints));
            Assert.Equal(expected, LuminPackSerializer.Serialize(nested));
            Assert.Equal(expected, LuminPackSerializer.Serialize(new ReadOnlyObservableCollection<int>(new ObservableCollection<int>(ints))));
            Assert.Equal(expected, LuminPackSerializer.Serialize(new Collection<int>(ints.ToList())));
            Assert.Equal(ints, LuminPackSerializer.Deserialize<ReadOnlyCollection<int>>(expected));

            // Correctness fix: a reused wrapper over another IList kind is refilled through its own Add.
            var reused = new Collection<int>(new ReadOnlyFreeList(ints.Length + 1));
            LuminPackSerializer.Deserialize(expected, ref reused);
            Assert.Equal(ints, reused);
        }
        Assert.Null(LuminPackSerializer.Deserialize<ArraySegment<int>>(LuminPackSerializer.Serialize(default(ArraySegment<int>))).Array);
        Assert.True(LuminPackSerializer.Deserialize<ImmutableArray<string>>(LuminPackSerializer.Serialize(default(ImmutableArray<string>))).IsDefault);
    }

    /// <summary>Checks pooled read storage never merges into stale references left in the shared pool by other code.</summary>
    [Fact]
    public void PooledReadsDoNotMergeIntoStaleElements()
    {
        var marker = new PooledElement { Value = -1 };
        var stale = ArrayPool<PooledElement?>.Shared.Rent(8);
        Array.Fill(stale, marker);
        ArrayPool<PooledElement?>.Shared.Return(stale, clearArray: false);
        var source = Enumerable.Range(0, 8).Select(i => new PooledElement { Value = i }).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(ImmutableQueue.CreateRange(source));
        var queue = LuminPackSerializer.Deserialize<ImmutableQueue<PooledElement?>>(bytes)!;
        Assert.Equal(source.Select(e => e.Value), queue.Select(e => e!.Value));
        Assert.DoesNotContain(queue, e => ReferenceEquals(e, marker));
        Assert.Equal(-1, marker.Value);
    }

    /// <summary>An integer comparer instance used to observe that a reused dictionary keeps its comparer.</summary>
    private sealed class StringComparerLike : IEqualityComparer<int>
    {
        /// <summary>The shared instance.</summary>
        public static readonly StringComparerLike Instance = new();

        /// <summary>Compares two integers.</summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>Whether they are equal.</returns>
        public bool Equals(int x, int y) => x == y;

        /// <summary>Hashes an integer.</summary>
        /// <param name="obj">The value.</param>
        /// <returns>The hash.</returns>
        public int GetHashCode(int obj) => obj;
    }

    /// <summary>A resizable list that is not a <see cref="List{T}"/>, for wrapper reuse.</summary>
    private sealed class ReadOnlyFreeList : System.Collections.ObjectModel.Collection<int>
    {
        /// <summary>Initializes a new instance of <c>ReadOnlyFreeList</c>.</summary>
        /// <param name="capacity">Unused sizing hint.</param>
        public ReadOnlyFreeList(int capacity) : base(new List<int>(capacity)) { }
    }
}

/// <summary>A reference element read through pooled storage.</summary>
[LuminPackable]
public partial class PooledElement
{
    /// <summary>The element value.</summary>
    public int Value;
}
