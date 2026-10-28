using System.Buffers;
using System.Reflection;
using System.Text.Json;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects same-position ordinal-key matching without mutating sorted keys.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedListReferenceReuseTest
{
    /// <summary>Retains keys, values and nested storage while keeping all four binary profiles.</summary>
    [Fact]
    public void MatchingKeysRetainNestedStorageInEveryProfile()
    {
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf8TokenOptions>();
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Case-insensitive comparer equivalence does not substitute for exact serialized key identity.</summary>
    [Fact]
    public void DifferentOrdinalKeysKeepFreshValues()
    {
        var target = Make(StringComparer.OrdinalIgnoreCase);
        var previous = target["a"];
        var source = new SortedList<string, ReferenceReuseValue> { ["A"] = new() { Number = 137 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(StringComparer.OrdinalIgnoreCase, target.Comparer);
        Assert.NotSame(previous, target["a"]);
        Assert.Equal(137, target["A"].Number);
        Assert.Equal(0, previous.Number);
    }

    /// <summary>Reinserting a future mutable value never changes the ordering of immutable keys.</summary>
    [Fact]
    public void CallbackFutureValueInsertionRetainsValidKeysAndAliases()
    {
        var target = new SortedList<string, ReferenceReuseCallback>
        {
            ["a"] = new() { Number = 719 }, ["b"] = new() { Number = 981 }, ["c"] = new() { Number = 1981 }
        };
        var future = target["c"];
        var source = new SortedList<string, ReferenceReuseCallback>
        {
            ["a"] = new() { Number = 137 }, ["b"] = new() { Number = 271 }, ["c"] = new() { Number = 3 }
        };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        ReferenceReuseCallback.OnRead = item => { if (item.Number == 137) target.Add("future", future); };
        try
        {
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.Equal(new[] { "a", "b", "c", "future" }, target.Keys);
            Assert.Same(future, target["c"]);
            Assert.Same(future, target["future"]);
            Assert.Equal(3, future.Number);
        }
        finally { ReferenceReuseCallback.OnRead = null; }
    }

    /// <summary>Clears the pooled key and value staging arrays on both normal and failed reads.</summary>
    /// <param name="truncated">Whether the selected value fails before publication.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedPairStorageAlwaysClears(bool truncated)
    {
        var source = Make();
        byte[] bytes = LuminPackSerializer.Serialize(source);
        if (truncated) bytes = bytes.AsSpan(0, bytes.Length - 1).ToArray();
        var target = Make();
        var keys = ArrayPool<string>.Shared.Rent(3);
        var values = ArrayPool<ReferenceReuseValue>.Shared.Rent(3);
        Array.Fill(keys, "poison");
        Array.Fill(values, new ReferenceReuseValue { Number = -719 });
        ArrayPool<string>.Shared.Return(keys, false);
        ArrayPool<ReferenceReuseValue>.Shared.Return(values, false);
        Exception? failure = Record.Exception(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Equal(truncated, failure is not null);
        var returnedKeys = ArrayPool<string>.Shared.Rent(3);
        var returnedValues = ArrayPool<ReferenceReuseValue>.Shared.Rent(3);
        try
        {
            Assert.Same(keys, returnedKeys);
            Assert.Same(values, returnedValues);
            Assert.All(returnedKeys, Assert.Null);
            Assert.All(returnedValues, Assert.Null);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(returnedKeys, true);
            ArrayPool<ReferenceReuseValue>.Shared.Return(returnedValues, true);
        }
    }

    /// <summary>Derived sorted lists keep their original fresh-value operation.</summary>
    [Fact]
    public void DerivedListsKeepFreshValues()
    {
        SortedList<string, ReferenceReuseValue> target = new DerivedList();
        foreach (var pair in Make()) target.Add(pair.Key, pair.Value);
        var first = target["a"];
        byte[] bytes = LuminPackSerializer.Serialize(Make());
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.IsType<DerivedList>(target);
        Assert.NotSame(first, target["a"]);
    }

    /// <summary>Checks matching objects, immutable text and every incomplete selected record.</summary>
    /// <typeparam name="TOptions">The complete binary string profile.</typeparam>
    private static void Check<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = Make();
        byte[] bytes = LuminPackSerializer.Serialize<TOptions>(source);
        var target = Make();
        var root = target;
        var first = target["a"];
        var child = first.Child;
        var tags = first.Tags;
        var name = first.Name;
        string key = target.Keys[0];
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref target);
        Assert.Same(root, target);
        Assert.Same(first, target["a"]);
        Assert.Same(child, first.Child);
        Assert.Same(tags, first.Tags);
        Assert.Same(name, first.Name);
        Assert.Same(key, target.Keys[0]);
        var text = new SortedList<string, string> { ["a"] = new('x', 7), ["b"] = new('y', 9) };
        key = text.Keys[0];
        name = text["a"];
        byte[] textBytes = LuminPackSerializer.Serialize<TOptions>(text);
        LuminPackSerializer.Deserialize<TOptions>(textBytes, ref text);
        Assert.Same(key, text.Keys[0]);
        Assert.Same(name, text["a"]);
        for (int prefix = 0; prefix < bytes.Length; prefix++)
        {
            var expected = Make();
            var actual = Make();
            byte[] input = bytes.AsSpan(0, prefix).ToArray();
            Exception? before = Record.Exception(() => ReadExpected<TOptions>(input, ref expected));
            Exception? after = Record.Exception(() => LuminPackSerializer.Deserialize<TOptions>(input, ref actual));
            Assert.Equal(before?.GetType(), after?.GetType());
            Assert.Equal(Snapshot(expected), Snapshot(actual));
            Assert.Equal(Version(expected), Version(actual));
        }
    }

    /// <summary>Reads the independent captured-prefix model through the original typed state and append operations.</summary>
    /// <typeparam name="TOptions">The complete binary string profile.</typeparam>
    /// <param name="bytes">The selected input window.</param>
    /// <param name="value">The existing sorted list.</param>
    private static unsafe void ReadExpected<TOptions>(byte[] bytes, ref SortedList<string, ReferenceReuseValue> value)
        where TOptions : struct, ILuminPackOptions
    {
        fixed (byte* pinned = bytes)
        {
            ReadOnlySpan<byte> input = new(pinned, bytes.Length);
            var reader = new LuminPackReader(ref input);
            reader.TryReadCollectionHead(out int length);
            var prior = value.Take(Math.Min(length, value.Count)).ToArray();
            value.Clear();
            var selected = value;
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool append = LuminPack.Code.LuminPackCollectionAccess.CanAppendSortedListOrdered(value);
            for (int index = 0; index < length; index++)
            {
                var previous = index < prior.Length ? prior[index] : default;
                string key = previous.Key;
                LuminPackSerializer.Deserialize<TOptions>(ref reader, ref key);
                ReferenceReuseValue item = key is not null && string.Equals(key, previous.Key, StringComparison.Ordinal) ? previous.Value : null!;
                if (reader.PeekIsNullObject()) item = null!;
                LuminPackSerializer.Deserialize<TOptions>(ref reader, ref item);
                if (!append || !ReferenceEquals(value, selected) || !ReferenceEquals(culture, System.Globalization.CultureInfo.CurrentCulture) ||
                    !LuminPack.Code.LuminPackCollectionAccess.TryAppendSortedListOrdered(value, key!, item, true)) value.Add(key!, item);
            }
        }
    }

    /// <summary>Creates independent existing values ordered by stable immutable string keys.</summary>
    /// <param name="comparer">The retained key comparer, or the default.</param>
    /// <returns>The independent sorted map.</returns>
    private static SortedList<string, ReferenceReuseValue> Make(IComparer<string>? comparer = null)
    {
        var result = new SortedList<string, ReferenceReuseValue>(comparer);
        for (int index = 0; index < 3; index++) result.Add(((char)('a' + index)).ToString(), new()
        {
            Number = index, Name = "value" + index, Child = new() { Text = "child" }, Tags = ["a", "b"]
        });
        return result;
    }

    /// <summary>Reads the original framework mutation version for publication checks.</summary>
    /// <param name="value">The selected map.</param>
    /// <returns>The current mutation version.</returns>
    private static int Version(SortedList<string, ReferenceReuseValue> value) => (int)typeof(SortedList<string, ReferenceReuseValue>)
        .GetField("version", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;

    /// <summary>Captures the complete decoded graph and key order.</summary>
    /// <param name="value">The map to inspect.</param>
    /// <returns>The public field/property snapshot.</returns>
    private static string Snapshot(SortedList<string, ReferenceReuseValue> value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { IncludeFields = true });

    private sealed class DerivedList : SortedList<string, ReferenceReuseValue>;
}
