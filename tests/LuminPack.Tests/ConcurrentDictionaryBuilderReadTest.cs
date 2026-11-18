#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Collections.Concurrent;
using LuminPack;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks fresh concurrent dictionaries filled pair by pair while the pairs are decoded.</summary>
public sealed class ConcurrentDictionaryBuilderReadTest
{
    /// <summary>Produces the collection constructor's contents for small and large inputs.</summary>
    [Fact]
    public void BuilderMatchesTheCollectionConstructor()
    {
        foreach (int count in new[] { 1, 2, 30, 31, 32, 33, 100, 257, 4096 })
        {
            var pairs = Enumerable.Range(0, count).Select(index => new KeyValuePair<int, string>(index * 7 - 50, "v" + index)).ToArray();
            var expected = new ConcurrentDictionary<int, string>(Environment.ProcessorCount, pairs, null);
            var builder = LuminPackCollectionAccess.BeginConcurrentDictionary<int, string>(count);
            foreach (var pair in pairs)
                builder.Add(pair.Key, pair.Value);
            var actual = builder.Complete();
            // The streaming builder defers stripe growth while unpublished. The public constructor can grow earlier.
            Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
            actual.TryAdd(int.MaxValue, "late");
            Assert.True(actual.TryRemove(pairs[0].Key, out _));
            Assert.Equal(count, actual.Count);

            var strings = Enumerable.Range(0, count).Select(index => new KeyValuePair<string, int>("k" + index, index)).ToArray();
            var expectedStrings = new ConcurrentDictionary<string, int>(Environment.ProcessorCount, strings, null);
            ConcurrentDictionary<string, int>? read = null;
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(expectedStrings), ref read);
            Assert.Equal(expectedStrings.OrderBy(pair => pair.Key, StringComparer.Ordinal), read!.OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal("k" + (count - 1), read.Keys.Single(key => key == "k" + (count - 1)));
        }
    }

    /// <summary>A repeated key keeps its first value and a null key throws.</summary>
    [Fact]
    public void DuplicatesKeepTheFirstValueAndNullKeysThrow()
    {
        foreach (int count in new[] { 4, 64 })
        {
            var builder = LuminPackCollectionAccess.BeginConcurrentDictionary<string, int>(count);
            for (int index = 0; index < count; index++)
                builder.Add("k" + (index % (count / 2)), index);
            var map = builder.Complete();
            Assert.Equal(count / 2, map.Count);
            Assert.Equal(0, map["k0"]);
        }
        Assert.Throws<ArgumentNullException>(() =>
        {
            var builder = LuminPackCollectionAccess.BeginConcurrentDictionary<string, int>(40);
            builder.Add(null!, 1);
        });
    }
}
#endif
