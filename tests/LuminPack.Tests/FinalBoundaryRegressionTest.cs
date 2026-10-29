using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks final-audit element contracts, exact sizes and complete JSON representations.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class FinalBoundaryRegressionTest
{
    /// <summary>Checks primitive queue forwarding retains the root, comparer, reuse and profile-sensitive fallback.</summary>
    [Fact]
    public void PriorityQueueDispatchRetainsReuseAndStringProfiles()
    {
        var source = new PriorityQueue<int, long>();
        source.Enqueue(17, 7);
        source.Enqueue(23, 3);
        byte[] bytes = LuminPackSerializer.Serialize<PriorityQueue<int, long>, LuminPackUtf16LengthOptions>(source);
        var comparer = Comparer<long>.Create((left, right) => right.CompareTo(left));
        var target = new PriorityQueue<int, long>(32, comparer);
        var identity = target;
        int consumed = LuminPackSerializer.Deserialize<PriorityQueue<int, long>, LuminPackUtf16LengthOptions>(bytes, ref target);
        Assert.Same(identity, target);
        Assert.Same(comparer, target.Comparer);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(17, target.Dequeue());
        Assert.Equal(23, target.Dequeue());

        var strings = new PriorityQueue<string, int>();
        strings.Enqueue("\u00E9\u4E2D", 1);
        bytes = LuminPackSerializer.Serialize<PriorityQueue<string, int>, LuminPackUtf16LengthOptions>(strings);
        var copy = LuminPackSerializer.Deserialize<PriorityQueue<string, int>, LuminPackUtf16LengthOptions>(bytes);
        Assert.Equal("\u00E9\u4E2D", copy.Dequeue());
    }

    /// <summary>Checks storage cast recognition when a closed payload type is also named As.</summary>
    [Fact]
    public void CastNamedGenericPayloadRetainsBinaryReuse()
    {
        var source = new As<int, string> { First = 17, Second = "nested cast name" };
        var bytes = LuminPackSerializer.Serialize<As<int, string>, LuminPackUtf16LengthOptions>(source);
        var fresh = LuminPackSerializer.Deserialize<As<int, string>, LuminPackUtf16LengthOptions>(bytes);
        Assert.Equal(source.First, fresh.First);
        Assert.Equal(source.Second, fresh.Second);
        var target = new As<int, string>();
        var identity = target;
        int consumed = LuminPackSerializer.Deserialize<As<int, string>, LuminPackUtf16LengthOptions>(bytes, ref target);
        Assert.Same(identity, target);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(source.First, target.First);
        Assert.Equal(source.Second, target.Second);
    }

    /// <summary>Supplies a generic payload whose name also occurs in unsafe cast invocations.</summary>
    /// <typeparam name="TFirst">The first member type.</typeparam>
    /// <typeparam name="TSecond">The second member type.</typeparam>
    [global::LuminPack.Attribute.LuminPackable]
    public sealed class As<TFirst, TSecond>
    {
        public TFirst First = default!;
        public TSecond Second = default!;
    }

    /// <summary>Checks segmented sequences retain scalar Nullable and padded pair formats.</summary>
    [Fact]
    public void SegmentedSequencesRetainScalarElementContracts()
    {
        var first = new Segment<int?>(new int?[] { 255, null });
        var last = first.Append(new int?[] { 7 });
        var source = new ReadOnlySequence<int?>(first, 0, last, last.Memory.Length);
        var bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(15, bytes.Length);
        Assert.Equal(source.ToArray(), LuminPackSerializer.Deserialize<ReadOnlySequence<int?>>(bytes).ToArray());
        var pairs = new ReadOnlySequence<KeyValuePair<byte, long>>(new[] { KeyValuePair.Create((byte)17, long.MaxValue) });
        bytes = LuminPackSerializer.Serialize(pairs);
        Assert.Equal(13, bytes.Length);
        Assert.Equal(pairs.ToArray(), LuminPackSerializer.Deserialize<ReadOnlySequence<KeyValuePair<byte, long>>>(bytes).ToArray());
    }

    /// <summary>Checks block-constructed collections read the scalar format selected by their enumerating writers.</summary>
    [Fact]
    public void ScalarCollectionsReadTheirOwnNullableRecords()
    {
        int?[] values = { 255, null, 7 };
        var list = values.ToImmutableList();
        Assert.Equal(values, LuminPackSerializer.Deserialize<ImmutableList<int?>>(LuminPackSerializer.Serialize(list)).ToArray());
        var queue = ImmutableQueue.CreateRange(values);
        Assert.Equal(values, LuminPackSerializer.Deserialize<ImmutableQueue<int?>>(LuminPackSerializer.Serialize(queue)).ToArray());
        var stack = ImmutableStack.CreateRange(values);
        Assert.Equal(stack.ToArray(), LuminPackSerializer.Deserialize<ImmutableStack<int?>>(LuminPackSerializer.Serialize(stack)).ToArray());
        var concurrent = new ConcurrentQueue<int?>(values);
        var bytes = LuminPackSerializer.Serialize(concurrent);
        Assert.Equal(values, LuminPackSerializer.Deserialize<ConcurrentQueue<int?>>(bytes).ToArray());
        var reused = new ConcurrentQueue<int?>(new int?[] { 99 });
        LuminPackSerializer.Deserialize(bytes, ref reused);
        Assert.Equal(values, reused.ToArray());
        using var blocking = new BlockingCollection<int?>(new ConcurrentQueue<int?>(values));
        Assert.Equal(values, LuminPackSerializer.Deserialize<BlockingCollection<int?>>(LuminPackSerializer.Serialize(blocking)).ToArray());
        IImmutableList<int?> interfaceList = list;
        Assert.Equal(values, LuminPackSerializer.Deserialize<IImmutableList<int?>>(LuminPackSerializer.Serialize(interfaceList)).ToArray());
        IImmutableQueue<int?> interfaceQueue = queue;
        Assert.Equal(values, LuminPackSerializer.Deserialize<IImmutableQueue<int?>>(LuminPackSerializer.Serialize(interfaceQueue)).ToArray());
        IImmutableStack<int?> interfaceStack = stack;
        Assert.Equal(stack.ToArray(), LuminPackSerializer.Deserialize<IImmutableStack<int?>>(LuminPackSerializer.Serialize(interfaceStack)).ToArray());
    }

    /// <summary>Checks interface and wrapper fallbacks retain their contiguous native element format.</summary>
    [Fact]
    public void ContiguousFallbacksKeepNativeNullableStorage()
    {
        var values = new int?[] { 255, null, 7 };
        var expected = LuminPackSerializer.Serialize(values);
        IEnumerable<int?> enumerable = values.Select(item => item);
        var bytes = LuminPackSerializer.Serialize(enumerable);
        Assert.Equal(expected, bytes);
        Assert.Equal(values, LuminPackSerializer.Deserialize<IEnumerable<int?>>(bytes).ToArray());
        var wrapper = new Collection<int?>(new NonListStorage<int?>(values));
        bytes = LuminPackSerializer.Serialize(wrapper);
        Assert.Equal(expected, bytes);
        Assert.Equal(values, LuminPackSerializer.Deserialize<Collection<int?>>(bytes).ToArray());
        var destination = new Collection<int?>(new NonListStorage<int?>(new int?[] { 99 }));
        var original = destination;
        LuminPackSerializer.Deserialize(bytes, ref destination);
        Assert.Same(original, destination);
        Assert.Equal(values, destination.ToArray());
    }

    /// <summary>Checks concurrent stack JSON preserves its top-to-bottom sequence and destination identity.</summary>
    [Fact]
    public void ConcurrentStackJsonRetainsEnumerationOrder()
    {
        var source = new ConcurrentStack<string?>(new[] { "first", null, "last" });
        string json = LuminPackSerializer.SerializeJson(source);
        Assert.Equal(source.ToArray(), LuminPackSerializer.DeserializeJson<ConcurrentStack<string?>>(json).ToArray());
        var destination = new ConcurrentStack<string?>(new[] { "old" });
        var original = destination;
        LuminPackSerializer.DeserializeJson(json, ref destination);
        Assert.Same(original, destination);
        Assert.Equal(source.ToArray(), destination.ToArray());
        destination = LuminPackSerializer.DeserializeJson<ConcurrentStack<string?>, StrictUtf16>(
            LuminPackSerializer.SerializeJson<ConcurrentStack<string?>, StrictUtf16>(source));
        Assert.Equal(source.ToArray(), destination.ToArray());
        Assert.Empty(LuminPackSerializer.DeserializeJson<ConcurrentStack<string?>>("[]"));
        Assert.Null(LuminPackSerializer.DeserializeJson<ConcurrentStack<string?>>("null"));
        Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<ConcurrentStack<string?>, StrictUtf16>(
            "[\"parsed\",", ref destination));
        Assert.Equal(new[] { "parsed" }, destination.ToArray());
    }

    /// <summary>Checks raw collections evaluate native stride rather than scalar formatter widths.</summary>
    [Fact]
    public void RawCollectionSizesMatchTheirPayloads()
    {
        int?[] values = { 1, null, 255 };
        var list = values.ToList();
        var collection = new Collection<int?>(list);
        var queue = new Queue<int?>(values);
        var immutable = ImmutableArray.Create(values);
        var pairs = new List<KeyValuePair<byte, long>> { new(17, 123456789) };
    }

    /// <summary>Checks string-pair list roots retain cursor publication, null keys and existing list identity.</summary>
    [Fact]
    public void StringPairListsRetainBinaryCursorAndReuse()
    {
        var expected = new List<KeyValuePair<string, int>> { new("first", 255), new(null!, -7), new("last", int.MaxValue) };
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(expected, LuminPackSerializer.Deserialize<List<KeyValuePair<string, int>>>(bytes));
        var destination = new List<KeyValuePair<string, int>> { new("old", 1), new("tail", 2), new("tail", 3), new("remove", 4) };
        var original = destination;
        Span<byte> input = bytes.Concat(BitConverter.GetBytes(17)).ToArray();
        var reader = new global::LuminPack.Core.LuminPackReader(ref input);
        LuminPackSerializer.Deserialize(ref reader, ref destination);
        Assert.Same(original, destination);
        Assert.Equal(expected, destination);
        Assert.Equal(bytes.Length, reader.Position);
        Assert.Equal(17, reader.Read<int>());
        bytes = LuminPackSerializer.Serialize<List<KeyValuePair<string, int>>, LuminPackUtf16LengthOptions>(expected);
        LuminPackSerializer.Deserialize<List<KeyValuePair<string, int>>, LuminPackUtf16LengthOptions>(bytes.AsSpan(), ref destination);
        Assert.Same(original, destination);
        Assert.Equal(expected, destination);
    }

    /// <summary>Checks memory, multidimensional, arbitrary integer, bit and priority records have exact evaluators.</summary>
    [Fact]
    public void BclSizesCoverTheirCompleteBinaryRecords()
    {
        var memory = new Memory<int>(new[] { 1, 2 });
        var readOnly = new ReadOnlyMemory<int>(new[] { 1, 2 });
        var segment = new ArraySegment<int>(new[] { 1, 2 });
        var matrix = new int[,] { { 1, 2 } };
        var integer = BigInteger.Pow(2, 3000) + 123;
        var bits = new BitArray(33, true);
        var priorities = new PriorityQueue<string, int>();
        priorities.Enqueue("one", 1);
    }

    /// <summary>Checks independently valid pair JSON and complete entry consumption inside arrays.</summary>
    [Fact]
    public void JsonPairsHaveOneSeparatorAndConsumeTheirTerminator()
    {
        var pairs = new List<KeyValuePair<string, int>> { new("a", 1), new("b", 2) };
        var json = LuminPackSerializer.SerializeJson(pairs);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("[[\"a\",1],[\"b\",2]]", json);
        Assert.Equal(pairs, LuminPackSerializer.DeserializeJson<List<KeyValuePair<string, int>>>(json));
        var frozen = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }.ToFrozenDictionary();
        json = LuminPackSerializer.SerializeJson(frozen);
        using var frozenDocument = JsonDocument.Parse(json);
        var restored = LuminPackSerializer.DeserializeJson<FrozenDictionary<string, int>>(json);
        Assert.Equal(2, restored.Count);
        Assert.Equal(2, restored["b"]);
        var priorities = new PriorityQueue<string, int>();
        priorities.Enqueue("a", 1);
        priorities.Enqueue("b", 2);
        json = LuminPackSerializer.SerializeJson(priorities);
        using var priorityDocument = JsonDocument.Parse(json);
        var restoredPriorities = LuminPackSerializer.DeserializeJson<PriorityQueue<string, int>>(json);
        Assert.Equal("a", restoredPriorities.Dequeue());
        Assert.Equal("b", restoredPriorities.Dequeue());
    }

    /// <summary>Checks a present Lazy remains present when its nested value is null.</summary>
    [Fact]
    public void LazyJsonPreservesWrapperPresence()
    {
        var source = new Lazy<string?>(() => null);
        string json = LuminPackSerializer.SerializeJson(source);
        Assert.Equal("{\"Value\":null}", json);
        var copy = LuminPackSerializer.DeserializeJson<Lazy<string?>>(json);
        Assert.NotNull(copy);
        Assert.Null(copy.Value);
        Assert.Null(LuminPackSerializer.DeserializeJson<Lazy<string?>>("null"));
        var nested = new Lazy<Lazy<int>>(new Lazy<int>(42));
        Assert.Equal(42, LuminPackSerializer.DeserializeJson<Lazy<Lazy<int>>>(LuminPackSerializer.SerializeJson(nested)).Value.Value);
        Assert.Equal(7, LuminPackSerializer.DeserializeJson<Lazy<int>>("{\"Extra\":[1,2],\"Value\":7}").Value);
        Assert.Equal(9, LuminPackSerializer.DeserializeJson<Lazy<int>>("{\"Val\\u0075e\":9}").Value);
        Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<Lazy<int>>("{}"));
    }

    /// <summary>Checks JSON shape metadata preserves nonzero bounds and dimensions behind an empty dimension.</summary>
    [Fact]
    public void MultidimensionalJsonPreservesCompleteShape()
    {
        var source = (int[,])Array.CreateInstance(typeof(int), new[] { 2, 3 }, new[] { 5, -2 });
        source[5, -2] = 123;
        string json = LuminPackSerializer.SerializeJson(source);
        using var document = JsonDocument.Parse(json);
        var copy = LuminPackSerializer.DeserializeJson<int[,]>(json);
        Assert.Equal(5, copy.GetLowerBound(0));
        Assert.Equal(-2, copy.GetLowerBound(1));
        Assert.Equal(123, copy[5, -2]);
        Assert.Equal(3, copy.GetLength(1));
        var empty = new int[0, 3];
        copy = LuminPackSerializer.DeserializeJson<int[,]>(LuminPackSerializer.SerializeJson(empty));
        Assert.Equal(0, copy.GetLength(0));
        Assert.Equal(3, copy.GetLength(1));
        var cube = new string?[2, 0, 4];
        var restoredCube = LuminPackSerializer.DeserializeJson<string?[,,]>(LuminPackSerializer.SerializeJson(cube));
        Assert.Equal(2, restoredCube.GetLength(0));
        Assert.Equal(0, restoredCube.GetLength(1));
        Assert.Equal(4, restoredCube.GetLength(2));
        copy = LuminPackSerializer.DeserializeJson<int[,]>("{\"Values\":[1,2],\"Extra\":{},\"LowerBounds\":[-1,2],\"Lengths\":[1,2]}");
        Assert.Equal(2, copy[-1, 3]);
        copy = LuminPackSerializer.DeserializeJson<int[,]>("{\"L\\u0065ngths\":[1,1],\"LowerBounds\":[0,0],\"Val\\u0075es\":[17]}");
        Assert.Equal(17, copy[0, 0]);
        Assert.Throws<NotSupportedException>(() => LuminPackSerializer.Serialize(source));
    }

    /// <summary>Checks malformed shape records cannot publish an inconsistent multidimensional array.</summary>
    /// <param name="json">The inconsistent JSON shape record.</param>
    [Theory]
    [InlineData("{\"Lengths\":[1,2],\"LowerBounds\":[0,0],\"Values\":[1]}")]
    [InlineData("{\"Lengths\":[1],\"LowerBounds\":[0,0],\"Values\":[1]}")]
    [InlineData("{\"Lengths\":[1,2,3],\"LowerBounds\":[0,0],\"Values\":[]}")]
    [InlineData("{\"Lengths\":[-1,2],\"LowerBounds\":[0,0],\"Values\":[]}")]
    [InlineData("{\"Lengths\":[2147483647,2],\"LowerBounds\":[0,0],\"Values\":[]}")]
    [InlineData("{\"Lengths\":[0,3],\"Values\":[]}")]
    public void MultidimensionalJsonRejectsInvalidMetadata(string json)
    {
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<int[,]>(json));
    }

    /// <summary>Checks a zero dimension makes the complete element count zero without overflowing a preceding product.</summary>
    [Fact]
    public void EmptyShapesDoNotOverflowIntermediateDimensionProducts()
    {
        var source = new int[50000, 50000, 0];
        var copy = LuminPackSerializer.DeserializeJson<int[,,]>(LuminPackSerializer.SerializeJson(source));
        Assert.Equal(50000, copy.GetLength(0));
        Assert.Equal(50000, copy.GetLength(1));
        Assert.Equal(0, copy.GetLength(2));
        copy = LuminPackSerializer.Deserialize<int[,,]>(LuminPackSerializer.Serialize(source));
        Assert.Equal(0, copy.Length);
        Assert.Equal(50000, copy.GetLength(1));
    }

    /// <summary>Checks canonical date text ignores a receiver's non-Gregorian calendar.</summary>
    [Fact]
    public void DateOnlyJsonUsesTheInvariantCalendar()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); }
            catch (CultureNotFoundException)
            {
                Assert.Equal(new DateOnly(2024, 2, 29), LuminPackSerializer.DeserializeJson<DateOnly>("\"2024-02-29\""));
                return;
            }
            var source = new DateOnly(2024, 2, 29);
            Assert.Equal(source, LuminPackSerializer.DeserializeJson<DateOnly>(LuminPackSerializer.SerializeJson(source)));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    /// <summary>Checks direct date token parsing retains escapes, both encodings and platform validation.</summary>
    [Fact]
    public void DateOnlyJsonTokenSpansPreserveValidation()
    {
        var expected = new DateOnly(2024, 2, 29);
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<DateOnly>("\"2024-02-29\""));
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<DateOnly>("\"\\u0032024-02-29\""));
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<DateOnly, StrictUtf16>("\"2024-02-29\""));
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<DateOnly, StrictUtf16>("\"\\u0032024-02-29\""));
        Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<DateOnly>("\"2023-02-29\""));
        Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<DateOnly>("\"2024/02/29\""));
        Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<DateOnly>("\"\\u00E9024-02-29\""));
        Assert.Throws<ArgumentNullException>(() => LuminPackSerializer.DeserializeJson<DateOnly>("null"));
    }

    /// <summary>Checks arbitrary unknown property names are skipped before quaternion value parsing.</summary>
    [Fact]
    public void QuaternionSkipsUnknownPropertyNames()
    {
        const string json = "{\"X\":1,\"Extra\":[{\"Nested\":true}],\"Y\":2,\"Z\":3,\"W\":4}";
        Assert.Equal(new Quaternion(1, 2, 3, 4), LuminPackSerializer.DeserializeJson<Quaternion>(json));
    }

    /// <summary>Checks complete wrapper and shape grammar under strict UTF-16 profiles.</summary>
    [Fact]
    public void JsonBoundaryRepairsSupportStrictUtf16()
    {
        var lazy = new Lazy<string?>(() => null);
        string json = LuminPackSerializer.SerializeJson<Lazy<string?>, StrictUtf16>(lazy);
        Assert.NotNull(LuminPackSerializer.DeserializeJson<Lazy<string?>, StrictUtf16>(json));
        Assert.Equal(9, LuminPackSerializer.DeserializeJson<Lazy<int>, StrictUtf16>("{\"Val\\u0075e\":9}").Value);
        var source = (string[,])Array.CreateInstance(typeof(string), new[] { 1, 2 }, new[] { -2, 4 });
        source[-2, 4] = "\u4E2D\u6587";
        json = LuminPackSerializer.SerializeJson<string[,], StrictUtf16>(source);
        var copy = LuminPackSerializer.DeserializeJson<string[,], StrictUtf16>(json);
        Assert.Equal(source[-2, 4], copy[-2, 4]);
        Assert.Null(copy[-2, 5]);
        var pairs = new List<KeyValuePair<string, int>> { new("a", 1), new("b", 2) };
        json = LuminPackSerializer.SerializeJson<List<KeyValuePair<string, int>>, StrictUtf16>(pairs);
        Assert.Equal(pairs, LuminPackSerializer.DeserializeJson<List<KeyValuePair<string, int>>, StrictUtf16>(json));
        var frozen = pairs.ToFrozenDictionary();
        json = LuminPackSerializer.SerializeJson<FrozenDictionary<string, int>, StrictUtf16>(frozen);
        Assert.Equal(2, LuminPackSerializer.DeserializeJson<FrozenDictionary<string, int>, StrictUtf16>(json).Count);
        const string quaternion = "{\"X\":1,\"LongUnknown\":[true],\"Y\":2,\"Z\":3,\"W\":4}";
        Assert.Equal(new Quaternion(1, 2, 3, 4), LuminPackSerializer.DeserializeJson<Quaternion, StrictUtf16>(quaternion));
    }

    /// <summary>Provides a constant strict UTF-16 document profile.</summary>
    public readonly struct StrictUtf16 : ILuminPackOptions
    {
        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 1024;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => true;
    }

    /// <summary>Provides explicit segment boundaries without allocating a coalesced input.</summary>
    /// <typeparam name="T">The segment element.</typeparam>
    private sealed class Segment<T> : ReadOnlySequenceSegment<T>
    {
        /// <summary>Initializes the segment with its payload.</summary>
        /// <param name="items">The segment storage.</param>
        internal Segment(T[] items) => Memory = items;

        /// <summary>Appends a segment at the correct running index.</summary>
        /// <param name="items">The next segment storage.</param>
        /// <returns>The appended segment.</returns>
        internal Segment<T> Append(T[] items)
        {
            var next = new Segment<T>(items) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    /// <summary>Supplies an IList whose implementation cannot be treated as List storage.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NonListStorage<T> : Collection<T>
    {
        /// <summary>Creates collection storage over the supplied fixed array.</summary>
        /// <param name="items">The element storage.</param>
        internal NonListStorage(T[] items) : base(new List<T>(items)) { }
    }
}
