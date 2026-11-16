using System.Collections.Concurrent;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects the streamed value lookups and initial key snapshot of JSON concurrent dictionary reuse.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ConcurrentJsonSnapshotTest
{
    /// <summary>Separates the snapshot and refill allocations for exact and derived dictionaries.</summary>
    /// <param name="utf16">Whether to use the UTF-16 JSON profile.</param>
    /// <param name="derived">Whether the destination retains public enumeration.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void WarmSnapshotOnlyAllocatesForTraversalAndRefill(bool utf16, bool derived)
    {
        if (utf16)
            CheckAllocation<LuminPackUtf16LengthOptions>(derived);
        else
            CheckAllocation<LuminPackUtf8LengthOptions>(derived);
    }

    /// <summary>Keeps the old string seed when a comparer removes a key before its value lookup.</summary>
    [Fact]
    public void ReentrantComparerKeepsOldKeyAndLooksUpTheLiveValue()
    {
        CheckReentrancy<LuminPackUtf8LengthOptions>();
        CheckReentrancy<LuminPackUtf16LengthOptions>();
    }

    /// <summary>Preserves the destination and the read prefix if a later value cannot be decoded.</summary>
    [Fact]
    public void InvalidValueKeepsTheOldDictionaryAndTheReadPrefix()
    {
        CheckFailure<LuminPackUtf8LengthOptions>();
        CheckFailure<LuminPackUtf16LengthOptions>();
    }

    /// <summary>Compares the read against the expected traversal and clear/refill costs.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    /// <param name="derived">Whether to expect public enumeration.</param>
    private static void CheckAllocation<TOptions>(bool derived) where TOptions : struct, ILuminPackOptions
    {
        const int repeats = 128;
        string oldKey = new('a', 4);
        var manual = new ConcurrentDictionary<string, int>(1, 31, StringComparer.OrdinalIgnoreCase) { [oldKey] = 7 };
        ConcurrentDictionary<string, int> values = derived
            ? new DerivedConcurrentDictionary()
            : new ConcurrentDictionary<string, int>(1, 31, StringComparer.OrdinalIgnoreCase);
        values[oldKey] = 7;
        var old = values;
        byte[] bytes = Encode<TOptions>("[[\"aaaa\",7]]");
        for (int index = 0; index < 16; index++)
        {
            LuminPackSerializer.DeserializeJson<ConcurrentDictionary<string, int>, TOptions>(bytes, ref values);
            Capture(manual, derived);
            manual.Clear();
            manual.TryAdd(oldKey, 7);
        }

        int captured = 0;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < repeats; index++)
            captured += Capture(manual, derived);
        long snapshot = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < repeats; index++)
            LuminPackSerializer.DeserializeJson<ConcurrentDictionary<string, int>, TOptions>(bytes, ref values);
        long actual = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < repeats; index++)
        {
            manual.Clear();
            manual.TryAdd(oldKey, 7);
        }
        long refill = GC.GetAllocatedBytesForCurrentThread() - start;

        Assert.Equal(repeats * 7, captured);
        Assert.Equal(refill + snapshot, actual);
        Assert.Same(old, values);
        Assert.Same(oldKey, Assert.Single(values).Key);
        Assert.Same(StringComparer.OrdinalIgnoreCase, values.Comparer);
    }

    /// <summary>Measures the same traversal available to the selected dictionary type.</summary>
    /// <param name="value">The dictionary holding the expected entry.</param>
    /// <param name="derived">Whether to select public enumeration.</param>
    /// <returns>The sum of values seen in this traversal.</returns>
    private static int Capture(ConcurrentDictionary<string, int> value, bool derived)
    {
        int sum = 0;
        if (derived)
        {
            foreach (var entry in value)
                sum += entry.Value;
        }
        else
        {
            foreach (var entry in LuminPackCollectionAccess.EnumerateConcurrentDictionary(value))
                sum += entry.Value;
        }
        return sum;
    }

    /// <summary>Proves key capture finishes before any live value lookup can invoke the comparer.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    private static void CheckReentrancy<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        string firstKey = new('a', 4);
        string secondKey = new('b', 4);
        var first = new ConcurrentSnapshotValue { Number = 9 };
        var second = new ConcurrentSnapshotValue { Number = 10 };
        var comparer = new ReentrantComparer();
        var values = new ConcurrentDictionary<string, ConcurrentSnapshotValue>(1, 31, comparer)
        {
            [firstKey] = first, [secondKey] = second
        };
        var old = values;
        Assert.Equal(new[] { firstKey, secondKey }, values.Select(pair => pair.Key));
        byte[] bytes = Encode<TOptions>("[[\"aaaa\",{\"Number\":137}],[\"bbbb\",{\"Number\":271}]]");
        bool removed = false;
        comparer.BeforeHash = key =>
        {
            if (!removed && key == firstKey)
            {
                removed = true;
                Assert.True(values.TryRemove(secondKey, out _));
            }
        };
        LuminPackSerializer.DeserializeJson<ConcurrentDictionary<string, ConcurrentSnapshotValue>, TOptions>(bytes, ref values);
        Assert.True(removed);
        Assert.Same(old, values);
        Assert.Same(comparer, values.Comparer);
        var result = values.ToArray();
        Assert.Same(firstKey, result[0].Key);
        Assert.Same(first, result[0].Value);
        Assert.Same(secondKey, result[1].Key);
        Assert.NotSame(second, result[1].Value);
        Assert.Equal(10, second.Number);
        Assert.Equal(271, result[1].Value.Number);
    }

    /// <summary>Checks the existing root and values after a malformed second value.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    private static void CheckFailure<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var first = new ConcurrentSnapshotValue { Number = 9 };
        var second = new ConcurrentSnapshotValue { Number = 10 };
        var values = new ConcurrentDictionary<string, ConcurrentSnapshotValue>(1, 31, new ReentrantComparer())
        {
            ["aaaa"] = first, ["bbbb"] = second
        };
        var old = values;
        byte[] bytes = Encode<TOptions>("[[\"aaaa\",{\"Number\":137}],[\"bbbb\",{\"Number\":\"invalid\"}]]");
        Assert.Throws<InvalidOperationException>(() =>
            LuminPackSerializer.DeserializeJson<ConcurrentDictionary<string, ConcurrentSnapshotValue>, TOptions>(bytes, ref values));
        Assert.Same(old, values);
        Assert.Same(first, values["aaaa"]);
        Assert.Same(second, values["bbbb"]);
        Assert.Equal(137, first.Number);
        Assert.Equal(10, second.Number);
    }

    /// <summary>Encodes JSON input for the selected reader profile.</summary>
    /// <typeparam name="TOptions">The active JSON profile.</typeparam>
    /// <param name="text">The input JSON.</param>
    /// <returns>The encoded document.</returns>
    private static byte[] Encode<TOptions>(string text) where TOptions : struct, ILuminPackOptions =>
        (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(text);

    private sealed class ReentrantComparer : IEqualityComparer<string>
    {
        /// <summary>Invokes an observer before a live lookup computes its hash.</summary>
        internal Action<string>? BeforeHash;

        /// <inheritdoc/>
        public bool Equals(string? left, string? right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);

        /// <inheritdoc/>
        public int GetHashCode(string key)
        {
            BeforeHash?.Invoke(key);
            return key[0] is 'a' or 'A' ? 1 : 2;
        }
    }

    private sealed class DerivedConcurrentDictionary() : ConcurrentDictionary<string, int>(1, 31, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Provides an object whose in-place read can be observed through an old alias.</summary>
[LuminPackable]
public sealed class ConcurrentSnapshotValue
{
    /// <summary>Stores the value read through the existing dictionary entry.</summary>
    public int Number;
}
