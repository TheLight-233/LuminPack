using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects the snapshot and publication rules of reused JSON sorted lists.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedListJsonReuseTest
{
    /// <summary>Reads unchanged keys and values without allocating when the runtime exposes ordered storage.</summary>
    /// <param name="utf16">Whether to use the UTF-16 JSON profile.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactSortedListSnapshotsWithoutAllocating(bool utf16)
    {
        if (utf16)
            CheckAllocation<LuminPackUtf16LengthOptions>();
        else
            CheckAllocation<LuminPackUtf8LengthOptions>();
    }

    /// <summary>Captures keys before the comparer can remove a later item, then looks up each value in the live list.</summary>
    /// <param name="utf16">Whether to use the UTF-16 JSON profile.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotKeepsKeysAndLiveValuesDistinct(bool utf16)
    {
        if (utf16)
            CheckReentrancy<LuminPackUtf16LengthOptions>();
        else
            CheckReentrancy<LuminPackUtf8LengthOptions>();
    }

    /// <summary>Preserves the sorted list and the prefix already read into matching values if a later value fails.</summary>
    /// <param name="utf16">Whether to use the UTF-16 JSON profile.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidValueRetainsKeysAndReadPrefix(bool utf16)
    {
        if (utf16)
            CheckFailure<LuminPackUtf16LengthOptions>();
        else
            CheckFailure<LuminPackUtf8LengthOptions>();
    }

    /// <summary>Rejects a truncated strict document before refilling the existing list.</summary>
    [Fact]
    public void StrictTruncationRetainsExistingEntries()
    {
        CheckTruncation<LuminPackOptions<LuminPackUtf8LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>();
        CheckTruncation<LuminPackOptions<LuminPackUtf16LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>();
    }

    /// <summary>Rejects equivalent keys at insertion after publishing the same prefix.</summary>
    [Fact]
    public void EquivalentKeysRetainTheInsertionFailurePrefix()
    {
        string key = new('a', 4);
        var first = new SortedJsonReplayValue { Number = 9 };
        var second = new SortedJsonReplayValue { Number = 10 };
        var values = new SortedList<string, SortedJsonReplayValue>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = first, ["bbbb"] = second
        };
        var old = values;
        byte[] bytes = "[[\"aaaa\",{\"Number\":137}],[\"AAAA\",{\"Number\":271}]]"u8.ToArray();
        Assert.Throws<ArgumentException>(() =>
            LuminPackSerializer.DeserializeJson<SortedList<string, SortedJsonReplayValue>, LuminPackUtf8LengthOptions>(bytes, ref values));
        Assert.Same(old, values);
        Assert.Same(StringComparer.OrdinalIgnoreCase, values.Comparer);
        Assert.Same(key, Assert.Single(values.Keys));
        Assert.Same(first, Assert.Single(values.Values));
        Assert.Equal(271, first.Number);
        Assert.Equal(10, second.Number);
    }

    /// <summary>Measures allocations while matching scalar and reference values to old entries.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    private static void CheckAllocation<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var key = new string('a', 4);
        var values = new SortedList<string, int>(StringComparer.OrdinalIgnoreCase) { [key] = 7, ["bbbb"] = 8 };
        var old = values;
        var reference = new SortedJsonReplayValue { Number = 7 };
        var objectValues = new SortedList<string, SortedJsonReplayValue>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = reference,
            ["bbbb"] = new SortedJsonReplayValue { Number = 8 }
        };
        if (!LuminPackCollectionAccess.TryGetSortedListTraversal(values, out _, out _, out _) ||
            !LuminPackCollectionAccess.TryGetSortedListTraversal(objectValues, out _, out _, out _))
            return;
        byte[] bytes = Encode<TOptions>("[[\"aaaa\",7],[\"bbbb\",8]]");
        byte[] objectBytes = Encode<TOptions>("[[\"aaaa\",{\"Number\":7}],[\"bbbb\",{\"Number\":8}]]");
        for (int index = 0; index < 16; index++)
        {
            LuminPackSerializer.DeserializeJson<SortedList<string, int>, TOptions>(bytes, ref values);
            LuminPackSerializer.DeserializeJson<SortedList<string, SortedJsonReplayValue>, TOptions>(objectBytes, ref objectValues);
        }

        const int count = 128;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < count; index++)
            LuminPackSerializer.DeserializeJson<SortedList<string, int>, TOptions>(bytes, ref values);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < count; index++)
            LuminPackSerializer.DeserializeJson<SortedList<string, SortedJsonReplayValue>, TOptions>(objectBytes, ref objectValues);
        long objectAllocated = GC.GetAllocatedBytesForCurrentThread() - start;

        Assert.Equal(0, allocated);
        Assert.Equal(0, objectAllocated);
        Assert.Same(old, values);
        Assert.Same(key, values.Keys[0]);
        Assert.Same(StringComparer.OrdinalIgnoreCase, values.Comparer);
        Assert.Same(reference, objectValues.Values[0]);
    }

    /// <summary>Checks a removal from the existing list during the first value lookup.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    private static void CheckReentrancy<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        string firstKey = new('a', 4);
        string secondKey = new('b', 4);
        var first = new SortedJsonReplayValue { Number = 9 };
        var second = new SortedJsonReplayValue { Number = 10 };
        SortedList<string, SortedJsonReplayValue> values = null!;
        bool armed = false;
        bool removed = false;
        var comparer = Comparer<string>.Create((left, right) =>
        {
            if (armed && !removed && StringComparer.Ordinal.Equals(right, firstKey))
            {
                removed = true;
                Assert.True(values.Remove(secondKey));
            }
            return StringComparer.OrdinalIgnoreCase.Compare(left, right);
        });
        values = new SortedList<string, SortedJsonReplayValue>(comparer) { [firstKey] = first, [secondKey] = second };
        var old = values;
        byte[] bytes = Encode<TOptions>("[[\"aaaa\",{\"Number\":137}],[\"bbbb\",{\"Number\":271}]]");
        armed = true;
        LuminPackSerializer.DeserializeJson<SortedList<string, SortedJsonReplayValue>, TOptions>(bytes, ref values);
        Assert.True(removed);
        Assert.Same(old, values);
        Assert.Same(comparer, values.Comparer);
        Assert.Same(firstKey, values.Keys[0]);
        Assert.Same(secondKey, values.Keys[1]);
        Assert.Same(first, values.Values[0]);
        Assert.NotSame(second, values.Values[1]);
        Assert.Equal(10, second.Number);
        Assert.Equal(271, values.Values[1].Number);
    }

    /// <summary>Checks an invalid second value after the first existing value has been read.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    private static void CheckFailure<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var first = new SortedJsonReplayValue { Number = 9 };
        var second = new SortedJsonReplayValue { Number = 10 };
        var values = new SortedList<string, SortedJsonReplayValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["aaaa"] = first, ["bbbb"] = second
        };
        var old = values;
        byte[] bytes = Encode<TOptions>("[[\"aaaa\",{\"Number\":137}],[\"bbbb\",{\"Number\":\"invalid\"}]]");
        Assert.Throws<InvalidOperationException>(() =>
            LuminPackSerializer.DeserializeJson<SortedList<string, SortedJsonReplayValue>, TOptions>(bytes, ref values));
        Assert.Same(old, values);
        Assert.Same(first, values.Values[0]);
        Assert.Same(second, values.Values[1]);
        Assert.Equal(137, first.Number);
        Assert.Equal(10, second.Number);
    }

    /// <summary>Checks that strict structural validation runs before matching nested values.</summary>
    /// <typeparam name="TOptions">The active strict JSON profile.</typeparam>
    private static void CheckTruncation<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var first = new SortedJsonReplayValue { Number = 9 };
        var second = new SortedJsonReplayValue { Number = 10 };
        var values = new SortedList<string, SortedJsonReplayValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["aaaa"] = first, ["bbbb"] = second
        };
        var old = values;
        byte[] bytes = Encode<TOptions>("[[\"aaaa\",{\"Number\":137}],[\"bbbb\"");
        Assert.NotNull(Record.Exception(() =>
            LuminPackSerializer.DeserializeJson<SortedList<string, SortedJsonReplayValue>, TOptions>(bytes, ref values)));
        Assert.Same(old, values);
        Assert.Same(first, values.Values[0]);
        Assert.Same(second, values.Values[1]);
        Assert.Equal(9, first.Number);
        Assert.Equal(10, second.Number);
    }

    /// <summary>Encodes a JSON document for the selected profile.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    /// <param name="text">The input JSON.</param>
    /// <returns>The encoded document.</returns>
    private static byte[] Encode<TOptions>(string text) where TOptions : struct, ILuminPackOptions =>
        (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(text);
}

/// <summary>Provides a reference value for sorted-list reuse and failed-read assertions.</summary>
[LuminPackable]
public sealed class SortedJsonReplayValue
{
    /// <summary>Stores a visible part of the read prefix.</summary>
    public int Number;
}
