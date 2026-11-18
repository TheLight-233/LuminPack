using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
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

    /// <summary>Checks that exact-type dispatch still calls a derived collection's interface enumerator.</summary>
    [Fact]
    public void DerivedSequenceInterfaceKeepsItsEnumeration()
    {
        IList<int> source = new ReorderedList { 1, 2, 3 };
        var expected = new[] { 3, 2, 1 };
        Assert.Equal(LuminPackSerializer.Serialize(expected), LuminPackSerializer.Serialize(source));
        string json = LuminPackSerializer.SerializeJson<IList<int>, LuminPackUtf16LengthOptions>(source);
        Assert.Equal("[3,2,1]", json);
    }

    /// <summary>Checks that integer pair fusion preserves a sparse dictionary's enumeration order and native widths.</summary>
    [Fact]
    public void SparseDictionaryInterfaceKeepsLongPairWire()
    {
        var dictionary = new Dictionary<int, long> { [7] = long.MinValue, [12] = 3, [-1] = long.MaxValue };
        dictionary.Remove(12);
        IReadOnlyDictionary<int, long> source = dictionary;
        Assert.Equal(LuminPackSerializer.Serialize(dictionary), LuminPackSerializer.Serialize(source));
        Assert.Equal("[[7,-9223372036854775808],[-1,9223372036854775807]]",
            LuminPackSerializer.SerializeJson<IReadOnlyDictionary<int, long>, LuminPackUtf16LengthOptions>(source));
    }

    /// <summary>Checks the raw pair block before publishing a new dictionary or refilling an empty one.</summary>
    /// <param name="reuse">Whether the caller supplies an empty destination with retained entry storage.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TruncatedRawDictionaryLeavesReaderAndTargetUnchanged(bool reuse)
    {
        var source = new Dictionary<int, long> { [-1] = long.MinValue, [7] = long.MaxValue };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        ReadOnlySpan<byte> input = bytes.AsSpan(0, bytes.Length - 1);
        var reader = new LuminPackReader(ref input);
        Dictionary<int, long>? target = reuse ? new Dictionary<int, long>(8) : null;
        var original = target;
        try
        {
            LuminPackSerializer.Deserialize(ref reader, ref target);
            Assert.Fail("A truncated fixed-width pair block was accepted.");
        }
        catch (Exception error) when (error.GetType().Name == "LuminPackException")
        {
            Assert.Same(original, target);
            if (target is not null)
                Assert.Empty(target);
            Assert.Equal(0, reader.Position);
        }
    }

    /// <summary>Retains an empty destination and its packed pair order across a successful refill.</summary>
    /// <param name="count">The payload size, covering small and dense blocks.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(48)]
    public void RawDictionaryRefillRetainsDestinationAndPairOrder(int count)
    {
        var source = Enumerable.Range(0, count).Reverse().ToDictionary(key => key, key => key * -7);
        byte[] bytes = LuminPackSerializer.Serialize(source);
        ReadOnlySpan<byte> input = bytes;
        var reader = new LuminPackReader(ref input);
        var original = new Dictionary<int, int>(count);
        var target = original;
        LuminPackSerializer.Deserialize(ref reader, ref target);

        Assert.Same(original, target);
        Assert.Equal(source.ToArray(), target.ToArray());
        Assert.Equal(bytes.Length, reader.Position);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(target));
    }

    /// <summary>Checks integer JSON run separators across empty segments and output growth.</summary>
    [Fact]
    public void JsonIntegerSequenceMatchesContiguousWire()
    {
        int[] items = Enumerable.Range(0, 9000).Select(index => int.MinValue + index).ToArray();
        var first = new IntegerSegment(ReadOnlyMemory<int>.Empty);
        var last = first.Append(items.AsMemory(0, 4500)).Append(ReadOnlyMemory<int>.Empty).Append(items.AsMemory(4500));
        var sequence = new ReadOnlySequence<int>(first, 0, last, last.Memory.Length);
        var expected = new ArrayBufferWriter<byte>();
        var actual = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<int[], LuminPackUtf8LengthOptions>(items, expected);
        LuminPackSerializer.SerializeJson<ReadOnlySequence<int>, LuminPackUtf8LengthOptions>(sequence, actual);
        Assert.Equal(expected.WrittenSpan.ToArray(), actual.WrittenSpan.ToArray());
        Assert.Equal(LuminPackSerializer.SerializeJson<int[], LuminPackUtf16LengthOptions>(items),
            LuminPackSerializer.SerializeJson<ReadOnlySequence<int>, LuminPackUtf16LengthOptions>(sequence));
    }

    /// <summary>Checks row-major integer JSON with an array that has nonzero lower bounds.</summary>
    [Fact]
    public void JsonIntegerMatrixKeepsLowerBoundsAndStorageOrder()
    {
        var matrix = (int[,])Array.CreateInstance(typeof(int), [2, 2], [-2, 4]);
        matrix[-2, 4] = int.MinValue;
        matrix[-2, 5] = 7;
        matrix[-1, 4] = int.MaxValue;
        matrix[-1, 5] = -19;
        var output = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<int[,], LuminPackUtf8LengthOptions>(matrix, output);
        Assert.Equal("{\"Lengths\":[2,2],\"LowerBounds\":[-2,4],\"Values\":[-2147483648,7,2147483647,-19]}",
            System.Text.Encoding.UTF8.GetString(output.WrittenSpan));
        int[,]? restored = null;
        LuminPackSerializer.DeserializeJson<int[,], LuminPackUtf8LengthOptions>(output.WrittenSpan, ref restored);
        Assert.NotNull(restored);
        Assert.Equal(-2, restored.GetLowerBound(0));
        Assert.Equal(4, restored.GetLowerBound(1));
        Assert.Equal(new[] { int.MinValue, 7, int.MaxValue, -19 }, restored.Cast<int>());
    }

    /// <summary>Checks source reads on either side of a streaming separator flush.</summary>
    [Fact]
    public void JsonIntegerRunsPreserveStreamingCaptureOrder()
    {
        const string prefix = "[-2147483648,";
        int[] array = [int.MinValue, 7];
        var list = new List<int>(array);
        var memory = new[] { int.MinValue, 7 }.AsMemory();
        int[] segmentItems = [int.MinValue, 7];
        var sequence = new ReadOnlySequence<int>(segmentItems);

        var output = new MutatingJsonOutput(() => array[1] = 1234);
        var writer = new LuminPackJsonWriter(output, default!, 12);
        LuminPackSerializer.SerializeJson<int[], LuminPackUtf8LengthOptions>(ref writer, array);
        writer.FlushToBufferWriter();
        Assert.Equal(prefix + "1234]", output.Text);

        output = new MutatingJsonOutput(() => list[1] = 1234);
        writer = new LuminPackJsonWriter(output, default!, 12);
        LuminPackSerializer.SerializeJson<List<int>, LuminPackUtf8LengthOptions>(ref writer, list);
        writer.FlushToBufferWriter();
        Assert.Equal(prefix + "1234]", output.Text);

        output = new MutatingJsonOutput(() => memory.Span[1] = 1234);
        writer = new LuminPackJsonWriter(output, default!, 12);
        LuminPackSerializer.SerializeJson<Memory<int>, LuminPackUtf8LengthOptions>(ref writer, memory);
        writer.FlushToBufferWriter();
        Assert.Equal(prefix + "1234]", output.Text);

        output = new MutatingJsonOutput(() => segmentItems[1] = 1234);
        writer = new LuminPackJsonWriter(output, default!, 12);
        LuminPackSerializer.SerializeJson<ReadOnlySequence<int>, LuminPackUtf8LengthOptions>(ref writer, sequence);
        writer.FlushToBufferWriter();
        Assert.Equal(prefix + "7]", output.Text);
    }

    /// <summary>Checks collection mutation during output and completed elements after a failed read into a reused set.</summary>
    [Fact]
    public void SortedSetJsonPreservesMutationAndReuseBoundaries()
    {
        var source = new SortedSet<int> { int.MinValue, 7, 9 };
        var output = new MutatingJsonOutput(() => source.Remove(9));
        Assert.Throws<InvalidOperationException>(() =>
        {
            var writer = new LuminPackJsonWriter(output, default!, 12);
            LuminPackSerializer.SerializeJson<SortedSet<int>, LuminPackUtf8LengthOptions>(ref writer, source);
            writer.FlushToBufferWriter();
        });
        Assert.StartsWith("[-2147483648", output.Text);

        int[] prefix = Enumerable.Range(0, 128).ToArray();
        string json = "[" + string.Join(',', prefix);
        var target = new SortedSet<int> { -1 };
        var original = target;
        var invalid = System.Text.Encoding.UTF8.GetBytes(json + ",-]");
        Assert.NotNull(Record.Exception(() =>
            LuminPackSerializer.DeserializeJson<SortedSet<int>, LuminPackUtf8LengthOptions>(invalid, ref target)));
        Assert.Same(original, target);
        Assert.Equal(prefix, target);

        var unsorted = System.Text.Encoding.UTF8.GetBytes(json + ",3,-1]");
        LuminPackSerializer.DeserializeJson<SortedSet<int>, LuminPackUtf8LengthOptions>(unsorted, ref target);
        Assert.Same(original, target);
        Assert.Equal(prefix.Prepend(-1), target);
    }

    /// <summary>Uses an exact first window and observes its commit before the separator.</summary>
    private sealed class MutatingJsonOutput(Action onFirstAdvance) : IBufferWriter<byte>
    {
        private readonly List<byte> _bytes = new();
        private byte[] _window = [];
        private bool _first = true;

        /// <summary>Gets the complete UTF-8 output.</summary>
        internal string Text => System.Text.Encoding.UTF8.GetString(_bytes.ToArray());

        /// <inheritdoc />
        public void Advance(int count)
        {
            _bytes.AddRange(_window.AsSpan(0, count).ToArray());
            if (_first)
            {
                _first = false;
                onFirstAdvance();
            }
        }

        /// <inheritdoc />
        public Memory<byte> GetMemory(int sizeHint = 0) => _window = new byte[Math.Max(12, sizeHint)];

        /// <inheritdoc />
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }

    /// <summary>Builds the segmented integer input for the JSON boundary test.</summary>
    private sealed class IntegerSegment : ReadOnlySequenceSegment<int>
    {
        /// <summary>Starts a segment with the given memory.</summary>
        /// <param name="memory">The values in this segment.</param>
        internal IntegerSegment(ReadOnlyMemory<int> memory) => Memory = memory;

        /// <summary>Appends the next segment.</summary>
        /// <param name="memory">The next values.</param>
        /// <returns>The appended segment.</returns>
        internal IntegerSegment Append(ReadOnlyMemory<int> memory)
        {
            var next = new IntegerSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    /// <summary>Reimplements generic enumeration without changing the concrete list's storage.</summary>
    private sealed class ReorderedList : List<int>, IList<int>
    {
        /// <inheritdoc />
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            for (int index = Count - 1; index >= 0; index--)
                yield return this[index];
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

    /// <summary>Checks local bag order and reuse across the first and last preallocation boundaries.</summary>
    /// <param name="count">The number of staged string records.</param>
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void ConcurrentBagReadPreservesLocalOrderAndExistingTarget(int count)
    {
        var source = new ConcurrentBag<string?>(Enumerable.Range(0, count).Select(index =>
            index % 8 == 0 ? null : $"item-{index:D6}"));
        string?[] expected = source.ToArray();
        Array.Reverse(expected);
        byte[] payload = LuminPackSerializer.Serialize(source);
        ConcurrentBag<string?>? target = null;
        Assert.Equal(payload.Length, LuminPackSerializer.Deserialize(payload.AsSpan(), ref target));
        Assert.Equal(expected, target!.ToArray());
        Assert.True(target.TryTake(out string? item));
        Assert.Equal(expected[0], item);

        var retained = target;
        target.Add("stale");
        Assert.Equal(payload.Length, LuminPackSerializer.Deserialize(payload.AsSpan(), ref target));
        Assert.Same(retained, target);
        Assert.Equal(expected, target!.ToArray());

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(payload.AsSpan(0, payload.Length - 1), ref target));
        Assert.Same(retained, target);
        Assert.Empty(target!);
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

    /// <summary>Checks that the raw stack validates its complete input before replacing the target or advancing the caller.</summary>
    [Fact]
    public void TruncatedRawImmutableStackLeavesReaderAndTargetUnchanged()
    {
        var source = ImmutableStack.CreateRange(new[] { long.MinValue, -1L, long.MaxValue });
        byte[] bytes = LuminPackSerializer.Serialize(source);
        ReadOnlySpan<byte> input = bytes.AsSpan(0, bytes.Length - 1);
        var reader = new LuminPackReader(ref input);
        var previous = ImmutableStack.Create(11L);
        var target = previous;

        try
        {
            LuminPackSerializer.Deserialize(ref reader, ref target);
            Assert.Fail("A truncated fixed-width stack payload was accepted.");
        }
        catch (Exception exception) when (exception.GetType().Name == "LuminPackException")
        {
            Assert.Same(previous, target);
            Assert.Equal(0, reader.Position);
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

    /// <summary>Checks wrapper enumeration, mutation at an output boundary and observable reuse notifications.</summary>
    [Fact]
    public void WrapperFastPathsPreservePublicBehavior()
    {
        var reversed = new Collection<string>(new ReverseEnumerableList { "a", "b" });
        Assert.Equal(LuminPackSerializer.Serialize(new[] { "b", "a" }), LuminPackSerializer.Serialize(reversed));

        var items = new List<int> { int.MinValue, 7, 9 };
        var wrapper = new ReadOnlyCollection<int>(items);
        var output = new MutatingJsonOutput(() => items.Capacity += 16);
        var writer = new LuminPackJsonWriter(output, default!, 12);
        LuminPackSerializer.SerializeJson<ReadOnlyCollection<int>, LuminPackUtf8LengthOptions>(ref writer, wrapper);
        writer.FlushToBufferWriter();
        Assert.Equal("[-2147483648,7,9]", output.Text);
        Assert.Throws<InvalidOperationException>(() =>
        {
            var mutatedOutput = new MutatingJsonOutput(() => items.Add(11));
            var mutatedWriter = new LuminPackJsonWriter(mutatedOutput, default!, 12);
            LuminPackSerializer.SerializeJson<ReadOnlyCollection<int>, LuminPackUtf8LengthOptions>(ref mutatedWriter, wrapper);
            mutatedWriter.FlushToBufferWriter();
        });

        var observable = new ObservableCollection<int> { -1 };
        var original = observable;
        var actions = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        observable.CollectionChanged += (_, e) => actions.Add(e.Action);
        LuminPackSerializer.DeserializeJson("[1,2]", ref observable);
        Assert.Same(original, observable);
        Assert.Equal(new[] { 1, 2 }, observable);
        Assert.Equal(new[]
        {
            System.Collections.Specialized.NotifyCollectionChangedAction.Reset,
            System.Collections.Specialized.NotifyCollectionChangedAction.Add,
            System.Collections.Specialized.NotifyCollectionChangedAction.Add
        }, actions);
    }

    /// <summary>Checks that an array-backed wrapper keeps the array wire without allocating an enumerator.</summary>
    [Fact]
    public void ArrayBackedObjectWrapperWritesWithoutAllocations()
    {
        var items = new PooledElement[] { new() { Value = 17 }, null!, new() { Value = -31 } };
        var wrapper = new ReadOnlyCollection<PooledElement>(items);
        using var buffer = new LuminBufferWriter(true);
        for (int i = 0; i < 32; i++)
        {
            buffer.ResetCore();
            LuminPackSerializer.Serialize(wrapper, buffer);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 32; i++)
        {
            buffer.ResetCore();
            LuminPackSerializer.Serialize(wrapper, buffer);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(LuminPackSerializer.Serialize(items), buffer.GetSpan().ToArray());
    }

    private sealed class ReverseEnumerableList : List<string>, IList<string>
    {
        /// <summary>Enumerates the wrapped collection in its configured order.</summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<string> IEnumerable<string>.GetEnumerator() => Enumerable.Reverse(ToArray()).GetEnumerator();
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
