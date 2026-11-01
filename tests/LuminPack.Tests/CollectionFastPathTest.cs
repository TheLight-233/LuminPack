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
                Assert.Equal(expected, LuminPackSerializer.Serialize((IEnumerable<int>)(source)));
                if (source is ICollection<int> collection)
                {
                    Assert.Equal(expected, LuminPackSerializer.Serialize((ICollection<int>)(collection)));
                    Assert.Equal(expected, LuminPackSerializer.Serialize((IReadOnlyCollection<int>)collection));
                }
                if (source is IList<int> list)
                {
                    Assert.Equal(expected, LuminPackSerializer.Serialize((IList<int>)(list)));
                    Assert.Equal(expected, LuminPackSerializer.Serialize((IReadOnlyList<int>)list));
                }
            }
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<IEnumerable<int>>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IEnumerable<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<IList<int>>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IList<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<IReadOnlyCollection<int>>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IReadOnlyCollection<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

            var strings = Strings(length);
            byte[] expectedStrings = LuminPackSerializer.Serialize(strings.ToList());
            Assert.Equal(expectedStrings, LuminPackSerializer.Serialize((IEnumerable<string?>)(strings.ToArray())));
            Assert.Equal(expectedStrings, LuminPackSerializer.Serialize((IList<string?>)(strings.ToList())));
            Assert.Equal(expectedStrings, LuminPackSerializer.Serialize((ICollection<string?>)(strings.ToList())));
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.Read<IList<string?>>(expectedStrings, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IList<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.Read<IReadOnlyList<string?>>(expectedStrings, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IReadOnlyList<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
        }

        // Correctness fix: an empty list of references used to be written as a null collection.
        var empty = global::LuminPackUnitTest.ConcreteReads.Read<ICollection<string>>(LuminPackSerializer.Serialize((ICollection<string>)(new List<string>())), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ICollection<string> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
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
            Assert.Equal(expected, LuminPackSerializer.Serialize((IDictionary<int, int>)(dictionary)));
            Assert.Equal(expected, LuminPackSerializer.Serialize((IReadOnlyDictionary<int, int>)(dictionary)));
            var sorted = new SortedDictionary<int, int>(dictionary);
            Assert.Equal(LuminPackSerializer.Serialize(sorted), LuminPackSerializer.Serialize((IDictionary<int, int>)(sorted)));
            Assert.Equal(dictionary, global::LuminPackUnitTest.ConcreteReads.Read<IReadOnlyDictionary<int, int>>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IReadOnlyDictionary<int, int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

            var set = new HashSet<int>(ints);
            Assert.Equal(LuminPackSerializer.Serialize(set), LuminPackSerializer.Serialize((ISet<int>)(set)));
            Assert.Equal(LuminPackSerializer.Serialize(set), LuminPackSerializer.Serialize((IReadOnlySet<int>)(set)));
            var sortedSet = new SortedSet<int>(ints);
            Assert.Equal(LuminPackSerializer.Serialize(sortedSet), LuminPackSerializer.Serialize((ISet<int>)(sortedSet)));
            Assert.True(set.SetEquals(global::LuminPackUnitTest.ConcreteReads.Read<ISet<int>>(LuminPackSerializer.Serialize((ISet<int>)(set)), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ISet<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!));
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
            Assert.Equal(dictionary.OrderBy(p => p.Key), global::LuminPackUnitTest.ConcreteReads.Read<ConcurrentDictionary<int, int>>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ConcurrentDictionary<int, int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!.OrderBy(p => p.Key));
            var reused = new ConcurrentDictionary<int, int>(StringComparerLike.Instance);
            reused[123456] = 1;
            LuminPackSerializer.Deserialize(bytes, ref reused);
            Assert.Same(StringComparerLike.Instance, reused!.Comparer);
            Assert.Equal(dictionary.OrderBy(p => p.Key), reused.OrderBy(p => p.Key));

            var queue = new ConcurrentQueue<int>(ints);
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<ConcurrentQueue<int>>(LuminPackSerializer.Serialize(queue), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ConcurrentQueue<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

            var stack = new ConcurrentStack<int>(ints);
            var stackCopy = global::LuminPackUnitTest.ConcreteReads.Read<ConcurrentStack<int>>(LuminPackSerializer.Serialize(stack), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ConcurrentStack<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!;
            Assert.Equal(stack.ToArray(), stackCopy.ToArray());
            Assert.Equal(LuminPackSerializer.Serialize(stack.Reverse().ToList()), LuminPackSerializer.Serialize(stack));

            var strings = new ConcurrentStack<string?>(Strings(length));
            Assert.Equal(strings.ToArray(), global::LuminPackUnitTest.ConcreteReads.Read<ConcurrentStack<string?>>(LuminPackSerializer.Serialize(strings), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ConcurrentStack<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!.ToArray());

            var bag = new ConcurrentBag<int>(ints);
            Assert.Equal(ints.OrderBy(i => i), global::LuminPackUnitTest.ConcreteReads.Read<ConcurrentBag<int>>(LuminPackSerializer.Serialize(bag), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ConcurrentBag<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!.OrderBy(i => i));

            var blocking = new BlockingCollection<int>();
            foreach (int value in ints) blocking.Add(value);
            var blockingCopy = global::LuminPackUnitTest.ConcreteReads.Read<BlockingCollection<int>>(LuminPackSerializer.Serialize(blocking), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BlockingCollection<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!;
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
            Assert.Equal(set, global::LuminPackUnitTest.ConcreteReads.Read<SortedSet<int>>(setBytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref SortedSet<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            var descending = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a))) { 1, 2 };
            LuminPackSerializer.Deserialize(setBytes, ref descending);
            Assert.Equal(set.Reverse(), descending);

            var list = new SortedList<int, string?>(ints.ToDictionary(i => i, i => i % 3 == 0 ? null : i.ToString()));
            byte[] listBytes = LuminPackSerializer.Serialize(list);
            var listCopy = global::LuminPackUnitTest.ConcreteReads.Read<SortedList<int, string?>>(listBytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref SortedList<int, string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!;
            Assert.Equal(list.Keys, listCopy.Keys);
            Assert.Equal(list.Values, listCopy.Values);
            listCopy.Add(int.MaxValue, "tail");
            Assert.Equal(int.MaxValue, listCopy.Keys[^1]);

            var dictionary = new SortedDictionary<int, int>(ints.ToDictionary(i => i, i => -i));
            Assert.Equal(dictionary, global::LuminPackUnitTest.ConcreteReads.Read<SortedDictionary<int, int>>(LuminPackSerializer.Serialize(dictionary), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref SortedDictionary<int, int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
        }

        // Duplicate keys still reject the payload on the bulk path.
        byte[] duplicate = BitConverter.GetBytes(2).Concat(BitConverter.GetBytes(5)).Concat(BitConverter.GetBytes(1))
            .Concat(BitConverter.GetBytes(5)).Concat(BitConverter.GetBytes(2)).ToArray();
        Assert.ThrowsAny<ArgumentException>(() => global::LuminPackUnitTest.ConcreteReads.Read<SortedList<int, int>>(duplicate, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref SortedList<int, int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
    }

    /// <summary>Checks immutable collections and their interfaces round trip with reference and raw elements.</summary>
    [Fact]
    public void ImmutableCollectionsRoundTrip()
    {
        foreach (int length in Lengths)
        {
            var ints = Ints(length);
            var strings = Strings(length);
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<ImmutableList<int>>(LuminPackSerializer.Serialize(ints.ToImmutableList()), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableList<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.Read<ImmutableList<string?>>(LuminPackSerializer.Serialize(strings.ToImmutableList()), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableList<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.Read<IImmutableList<string?>>(LuminPackSerializer.Serialize((IImmutableList<string?>)(strings.ToImmutableList())), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IImmutableList<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(ints.OrderBy(i => i), global::LuminPackUnitTest.ConcreteReads.Read<ImmutableSortedSet<int>>(LuminPackSerializer.Serialize(ints.ToImmutableSortedSet()), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableSortedSet<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            var sortedDictionary = ints.ToImmutableSortedDictionary(i => i, i => i + 1);
            Assert.Equal(sortedDictionary, global::LuminPackUnitTest.ConcreteReads.Read<ImmutableSortedDictionary<int, int>>(LuminPackSerializer.Serialize(sortedDictionary), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableSortedDictionary<int, int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

            var array = strings.ToImmutableArray();
            Assert.Equal(LuminPackSerializer.Serialize(strings.ToList()), LuminPackSerializer.Serialize(array));
            var arrayCopy = global::LuminPackUnitTest.ConcreteReads.Read<ImmutableArray<string?>>(LuminPackSerializer.Serialize(array), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableArray<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
            Assert.False(arrayCopy.IsDefault);
            Assert.Equal(strings, arrayCopy);
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<ImmutableArray<int>>(LuminPackSerializer.Serialize(ints.ToImmutableArray()), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableArray<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

            var queue = ImmutableQueue.CreateRange(strings);
            byte[] queueBytes = LuminPackSerializer.Serialize(queue);
            Assert.Equal(LuminPackSerializer.Serialize(strings.ToList()), queueBytes);
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.Read<ImmutableQueue<string?>>(queueBytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableQueue<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.Read<IImmutableQueue<string?>>(LuminPackSerializer.Serialize((IImmutableQueue<string?>)(queue)), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IImmutableQueue<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

            var stack = ImmutableStack.CreateRange(ints);
            byte[] stackBytes = LuminPackSerializer.Serialize(stack);
            Assert.Equal(LuminPackSerializer.Serialize(stack.ToList()), stackBytes);
            Assert.Equal(stack, global::LuminPackUnitTest.ConcreteReads.Read<ImmutableStack<int>>(stackBytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableStack<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
            Assert.Equal(stack, global::LuminPackUnitTest.ConcreteReads.Read<IImmutableStack<int>>(LuminPackSerializer.Serialize((IImmutableStack<int>)(stack)), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref IImmutableStack<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
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
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<Memory<int>>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Memory<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).ToArray());
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<ArraySegment<int>>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ArraySegment<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).ToArray());
            var strings = Strings(length);
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.Read<ReadOnlyMemory<string?>>(LuminPackSerializer.Serialize(new ReadOnlyMemory<string?>(strings)), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ReadOnlyMemory<string?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).ToArray());

            var nested = new ReadOnlyCollection<int>(new ObservableCollection<int>(ints));
            Assert.Equal(expected, LuminPackSerializer.Serialize(nested));
            Assert.Equal(expected, LuminPackSerializer.Serialize(new ReadOnlyObservableCollection<int>(new ObservableCollection<int>(ints))));
            Assert.Equal(expected, LuminPackSerializer.Serialize(new Collection<int>(ints.ToList())));
            Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.Read<ReadOnlyCollection<int>>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ReadOnlyCollection<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

            // Correctness fix: a reused wrapper over another IList kind is refilled through its own Add.
            var reused = new Collection<int>(new ReadOnlyFreeList(ints.Length + 1));
            LuminPackSerializer.Deserialize(expected, ref reused);
            Assert.Equal(ints, reused);
        }
        Assert.Null(global::LuminPackUnitTest.ConcreteReads.Read<ArraySegment<int>>(LuminPackSerializer.Serialize(default(ArraySegment<int>)), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ArraySegment<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Array);
        Assert.True(global::LuminPackUnitTest.ConcreteReads.Read<ImmutableArray<string>>(LuminPackSerializer.Serialize(default(ImmutableArray<string>)), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableArray<string> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).IsDefault);
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
        var queue = global::LuminPackUnitTest.ConcreteReads.Read<ImmutableQueue<PooledElement?>>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableQueue<PooledElement?> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!;
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
