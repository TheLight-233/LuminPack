using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using LuminPack;
using LuminPack.Option;
using Xunit;
using ReuseNode = LuminPackUnitTest.DeserializeReuseTest.ReuseNode;
using StrictUtf8 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf8LengthOptions, LuminPack.Option.LuminPackGeneralFormat, LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;

namespace LuminPackUnitTest;

/// <summary>Checks object framing and compatibility for JSON string-keyed dictionaries.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class JsonDictionaryObjectTest
{
    /// <summary>Checks that interface roots use properties for known dictionaries and pairs for other implementations.</summary>
    [Fact]
    public void InterfaceWritersKeepPairsForUnknownStorage()
    {
        var source = new Dictionary<string, int> { ["name"] = 7 };
        IDictionary<string, int> writable = source;
        IReadOnlyDictionary<string, int> readable = source;
        Assert.Equal("""{"name":7}""", LuminPackSerializer.SerializeJson(writable));
        string json = LuminPackSerializer.SerializeJson(readable);
        Assert.Equal("""{"name":7}""", json);

        IReadOnlyDictionary<string, int> copy = null!;
        LuminPackSerializer.DeserializeJson(json, ref copy);
        Assert.Equal(7, copy["name"]);

        writable = new SortedList<string, int> { ["name"] = 7 };
        Assert.Equal("""[["name",7]]""", LuminPackSerializer.SerializeJson(writable));
        readable = new NullKeyDictionary();
        Assert.Equal("""[[null,7]]""", LuminPackSerializer.SerializeJson(readable));
    }

    /// <summary>Checks that escaped object keys and matching values retain identity across staging growth.</summary>
    [Fact]
    public void ObjectReadsGrowStagingAndReuseMatchingKeysAndValues()
    {
        string key = new("stable".ToCharArray());
        var item = new ReuseNode();
        var original = new Dictionary<string, ReuseNode>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = item,
            ["removed"] = new()
        };
        var destination = original;
        string moreEntries = string.Join(",", Enumerable.Range(0, 80).Select(index => $"\"extra{index}\":null"));
        string json = """{"sta\u0062le":{"Number":41},""" + moreEntries + ""","STABLE":{"Number":42}}""";
        LuminPackSerializer.DeserializeJson<Dictionary<string, ReuseNode>, StrictUtf8>(Encoding.UTF8.GetBytes(json), ref destination);

        Assert.Same(original, destination);
        Assert.Same(StringComparer.OrdinalIgnoreCase, destination.Comparer);
        Assert.Equal(81, destination.Count);
        Assert.False(destination.ContainsKey("removed"));
        Assert.Same(key, destination.Keys.Single(candidate => StringComparer.OrdinalIgnoreCase.Equals(key, candidate)));
        Assert.Same(item, destination["Stable"]);
        Assert.Equal(42, item.Number);
        Assert.Null(destination["extra79"]);
    }

    /// <summary>Checks an empty object clears a large reused map without staging its old entries.</summary>
    [Fact]
    public void EmptyObjectClearsWithoutSnapshotAllocation()
    {
        byte[] pairArray = "[]"u8.ToArray();
        byte[] propertyObject = "{ \n }"u8.ToArray();
        ConcurrentDictionary<string, int> pairTarget = new SnapshotFallbackDictionary();
        ConcurrentDictionary<string, int> objectTarget = new SnapshotFallbackDictionary();
        var original = objectTarget;
        for (int index = 0; index < 32; index++)
        {
            pairTarget["warmup"] = index;
            objectTarget["warmup"] = index;
            Clear(pairArray, ref pairTarget);
            Clear(propertyObject, ref objectTarget);
        }
        for (int index = 0; index < 4096; index++)
        {
            string key = index.ToString();
            pairTarget[key] = index;
            objectTarget[key] = index;
        }

        long pairAllocation = Clear(pairArray, ref pairTarget);
        long objectAllocation = Clear(propertyObject, ref objectTarget);

        Assert.Equal(pairAllocation, objectAllocation);
        Assert.Same(original, objectTarget);
        Assert.Empty(objectTarget);
    }

    /// <summary>Measures one strict JSON clear on the current thread.</summary>
    /// <param name="input">An empty JSON dictionary.</param>
    /// <param name="target">The existing dictionary.</param>
    /// <returns>The bytes allocated by the read and clear.</returns>
    private static long Clear(byte[] input, ref ConcurrentDictionary<string, int> target)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        LuminPackSerializer.DeserializeJson<ConcurrentDictionary<string, int>, StrictUtf8>(input, ref target);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>Checks object framing errors leave the old entries in place.</summary>
    /// <param name="json">A malformed object after a valid first entry.</param>
    [Theory]
    [InlineData("""{"incoming":{"Number":7},"truncated":}""")]
    [InlineData("""{"incoming":{"Number":7}""")]
    [InlineData("""{"incoming":{"Number":7},"bad" null}""")]
    public void InvalidObjectsDoNotPublishTheStagedEntries(string json)
    {
        var old = new ReuseNode { Number = 137 };
        var original = new Dictionary<string, ReuseNode> { ["old"] = old };
        var destination = original;
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        Assert.Throws<FormatException>(() =>
            LuminPackSerializer.DeserializeJson<Dictionary<string, ReuseNode>, StrictUtf8>(bytes, ref destination));

        Assert.Same(original, destination);
        var entry = Assert.Single(destination);
        Assert.Equal("old", entry.Key);
        Assert.Same(old, entry.Value);
        Assert.Equal(137, old.Number);
    }

    /// <summary>Uses the framework enumerator when an entry snapshot is requested.</summary>
    private sealed class SnapshotFallbackDictionary() : ConcurrentDictionary<string, int>(1, 4096);

    /// <summary>Provides an externally implemented read-only map that enumerates a null key.</summary>
    private sealed class NullKeyDictionary : IReadOnlyDictionary<string, int>
    {
        /// <inheritdoc/>
        public int Count => 1;
        /// <inheritdoc/>
        public IEnumerable<string> Keys => [null!];
        /// <inheritdoc/>
        public IEnumerable<int> Values => [7];
        /// <inheritdoc/>
        public int this[string key] => key is null ? 7 : throw new KeyNotFoundException();
        /// <inheritdoc/>
        public bool ContainsKey(string key) => key is null;
        /// <inheritdoc/>
        public bool TryGetValue(string key, out int value)
        {
            value = key is null ? 7 : default;
            return key is null;
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            yield return new(null!, 7);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
