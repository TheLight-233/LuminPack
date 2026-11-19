#if NET10_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Collections.Concurrent;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;
using StrictUtf8 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf8LengthOptions, LuminPack.Option.LuminPackGeneralFormat, LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;

namespace LuminPackUnitTest;

/// <summary>Checks staged JSON replacement through the public concurrent dictionary reader.</summary>
public sealed class ConcurrentDictionaryJsonPublicationTest
{
    /// <summary>Keeps the dictionary alias, comparer and first occurrence on both sides of the staging threshold.</summary>
    /// <param name="objectForm">Whether the record uses property names or arrays of pairs.</param>
    /// <param name="entries">The total number of pairs, including repeated keys.</param>
    [Theory]
    [InlineData(true, 63)]
    [InlineData(true, 64)]
    [InlineData(false, 63)]
    [InlineData(false, 64)]
    public void StagedStringKeysRetainFirstOccurrence(bool objectForm, int entries)
    {
        var map = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["removed"] = -1 };
        var alias = map;
        IEnumerable<(string Key, int Value)> pairs = new[] { ("keep", 1), ("KEEP", 2), ("keep", 3) }
            .Concat(Enumerable.Range(3, entries - 3).Select(index => ($"key{index}", index)));

        Read(Record(objectForm, pairs), ref map);

        Assert.Same(alias, map);
        Assert.Same(StringComparer.OrdinalIgnoreCase, alias.Comparer);
        Assert.False(alias.ContainsKey("removed"));
        Assert.Equal(1, alias["KEEP"]);
        Assert.Equal(entries - 2, alias.Count);
        for (int index = 3; index < entries; index++)
            Assert.Equal(index, alias[$"key{index}"]);
        Assert.True(alias.TryAdd("later", 100));
        Assert.True(alias.TryRemove("KEEP", out int previous));
        Assert.Equal(1, previous);
    }

    /// <summary>Retains the original clear and insert behavior for a user comparer or a derived dictionary.</summary>
    /// <param name="derived">Whether the existing map is a derived type with a framework comparer.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomComparerAndDerivedMapCanReuse(bool derived)
    {
        var custom = new ObservedComparer();
        ConcurrentDictionary<string, int> map = derived ? new DerivedMap() : new ConcurrentDictionary<string, int>(custom);
        map["removed"] = -1;
        var alias = map;
        custom.Target = alias;
        var pairs = new[] { ("match", 1), ("MATCH", 2) }
            .Concat(Enumerable.Range(2, 62).Select(index => ($"key{index}", index)));

        Read(Record(true, pairs), ref map);

        Assert.Same(alias, map);
        Assert.Same(derived ? StringComparer.OrdinalIgnoreCase : custom, alias.Comparer);
        Assert.False(alias.ContainsKey("removed"));
        Assert.Equal(63, alias.Count);
        Assert.Equal(1, alias["MATCH"]);
        if (!derived)
            Assert.True(custom.ObservedEmpty);
    }

    /// <summary>Preserves the first failing operation when an array pair supplies, or omits, a null key.</summary>
    /// <param name="explicitNull">Whether null is passed to the lookup during staging.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullKeyKeepsOriginalFailureState(bool explicitNull)
    {
        var map = new ConcurrentDictionary<string, int>(StringComparer.Ordinal) { ["old"] = 137 };
        var alias = map;
        string prefix = Record(false, Enumerable.Range(0, 63).Select(index => ($"key{index}", index)));
        string wire = prefix[..^1] + (explicitNull ? ",[null,7]]" : ",[]]");

        Assert.Throws<ArgumentNullException>(() => Read(wire, ref map));

        Assert.Same(alias, map);
        if (explicitNull)
            Assert.Equal(new KeyValuePair<string, int>("old", 137), Assert.Single(alias));
        else
        {
            Assert.False(alias.ContainsKey("old"));
            Assert.Equal(63, alias.Count);
            Assert.Equal(62, alias["key62"]);
        }
    }

    /// <summary>Does not publish staged keys when JSON fails after a publishable number of complete entries.</summary>
    /// <param name="objectForm">Whether the malformed record uses property names or arrays of pairs.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TruncationLeavesTheOldMap(bool objectForm)
    {
        var map = new ConcurrentDictionary<string, int>(StringComparer.Ordinal) { ["old"] = 137 };
        var alias = map;
        string prefix = Record(objectForm, Enumerable.Range(0, 64).Select(index => ($"key{index}", index)));
        string wire = prefix[..^1] + (objectForm ? ",\"last\":" : ",[\"last\",");

        Assert.Throws<FormatException>(() => Read(wire, ref map));

        Assert.Same(alias, map);
        Assert.Equal(new KeyValuePair<string, int>("old", 137), Assert.Single(alias));
    }

    /// <summary>Publishes the same object references reused while reading the staged entries.</summary>
    [Fact]
    public void StagedValuesKeepObjectIdentity()
    {
        var kept = new ConcurrentJsonRefillValue { Id = -1 };
        var map = new ConcurrentDictionary<string, ConcurrentJsonRefillValue?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keep"] = kept,
            ["removed"] = new ConcurrentJsonRefillValue()
        };
        var alias = map;
        string prefix = string.Join(",", Enumerable.Range(0, 80).Select(index => $"\"key{index}\":null"));
        string wire = "{\"KEEP\":{\"Id\":137}," + prefix + "}";

        LuminPackSerializer.DeserializeJson(wire, ref map);

        Assert.Same(alias, map);
        Assert.Same(kept, alias["keep"]);
        Assert.Equal(137, kept.Id);
        Assert.Equal(81, alias.Count);
        Assert.Null(alias["key79"]);
        Assert.False(alias.ContainsKey("removed"));
    }

    private static string Record(bool objectForm, IEnumerable<(string Key, int Value)> pairs) =>
        objectForm
            ? "{" + string.Join(",", pairs.Select(pair => $"\"{pair.Key}\":{pair.Value}")) + "}"
            : "[" + string.Join(",", pairs.Select(pair => $"[\"{pair.Key}\",{pair.Value}]")) + "]";

    private static void Read(string wire, ref ConcurrentDictionary<string, int> map) =>
        LuminPackSerializer.DeserializeJson<ConcurrentDictionary<string, int>, StrictUtf8>(Encoding.UTF8.GetBytes(wire), ref map);

    private sealed class DerivedMap() : ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private sealed class ObservedComparer : IEqualityComparer<string>
    {
        internal ConcurrentDictionary<string, int>? Target;
        internal bool ObservedEmpty;

        public bool Equals(string? left, string? right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);

        public int GetHashCode(string key)
        {
            ObservedEmpty |= Target is { IsEmpty: true };
            return StringComparer.OrdinalIgnoreCase.GetHashCode(key);
        }
    }
}

/// <summary>A mutable value decoded over an existing concurrent dictionary entry.</summary>
[LuminPackable]
internal sealed partial class ConcurrentJsonRefillValue
{
    /// <summary>The identifier.</summary>
    public int Id;
}
#endif
