using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects ordered mutable reconstruction against the original per-element public operations.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedReuseConstructionTest
{
    /// <summary>Compares every selected truncated string record, destination identity and version against Clear/Add.</summary>
    [Fact]
    public void SetProfilesAndFailedPrefixesMatchOriginalInsertion()
    {
        CheckSets<LuminPackUtf8LengthOptions>();
        CheckSets<LuminPackUtf8TokenOptions>();
        CheckSets<LuminPackUtf16LengthOptions>();
        CheckSets<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Keeps pair publication, capacity, comparer calls and callbacks while retaining every failed prefix.</summary>
    [Fact]
    public void ListProfilesCallbacksAndFailedPrefixesMatchOriginalInsertion()
    {
        CheckLists<LuminPackUtf8LengthOptions>();
        CheckLists<LuminPackUtf8TokenOptions>();
        CheckLists<LuminPackUtf16LengthOptions>();
        CheckLists<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Builds ordered user-comparable elements with one comparison between each adjacent pair.</summary>
    [Fact]
    public void UserComparisonCallbacksKeepTheirInvocationSequence()
    {
        var source = new SortedSet<SortedReuseComparedValue>
        {
            new() { Number = 3 }, new() { Number = 1 }, new() { Number = 2 }
        };
        byte[] payload = LuminPackSerializer.Serialize(source);
        var target = new SortedSet<SortedReuseComparedValue>();
        SortedReuseComparedValue.Comparisons.Clear();
        LuminPackSerializer.Deserialize(payload, ref target);
        string[] calls = SortedReuseComparedValue.Comparisons.ToArray();
        SortedReuseComparedValue.Comparisons.Clear();
        var expected = new SortedSet<SortedReuseComparedValue>();
        foreach (var item in source) expected.Add(new() { Number = item.Number });
        // Ordered input is checked in enumeration order before the tree is linked; the set answers as one built by Add.
        Assert.Equal(new[] { "1:2", "2:3" }, calls);
        Assert.Equal(expected.Select(item => item.Number), target.Select(item => item.Number));
        Assert.True(target.Contains(new() { Number = 2 }) && target.Add(new() { Number = 0 }) && target.Min!.Number == 0);
        SortedReuseComparedValue.Comparisons.Clear();
    }

    /// <summary>Retains ref-target replacement, user failures and observable culture comparisons after callbacks.</summary>
    [Fact]
    public void ListCallbacksKeepTargetReplacementAndCultureBoundaries()
    {
        var source = new SortedList<string, SortedReuseCallbackValue>
        {
            ["a"] = new() { Number = 1 }, ["b"] = new() { Number = 2 }, ["c"] = new() { Number = 3 }
        };
        byte[] payload = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(source);
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (string action in new[] { "replace", "throw", "culture" })
            {
                var expected = new SortedList<string, SortedReuseCallbackValue>(16);
                var actual = new SortedList<string, SortedReuseCallbackValue>(16);
                var beforeOriginal = expected;
                var afterOriginal = actual;
                var beforeCulture = new ObservedCulture();
                var afterCulture = new ObservedCulture();
                var failure = new InvalidOperationException("A user callback failed.");
                Exception? before = null, after = null;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                SortedReuseCallbackValue.OnRead = item =>
                {
                    if (item.Number != 2) return;
                    if (action == "replace") expected = new(StringComparer.Ordinal);
                    if (action == "throw") throw failure;
                    if (action == "culture") CultureInfo.CurrentCulture = beforeCulture;
                };
                try { ReadListOriginal<LuminPackUtf16LengthOptions>(payload, ref expected); }
                catch (Exception error) { before = error; }
                int beforeCalls = beforeCulture.Calls;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                SortedReuseCallbackValue.OnRead = item =>
                {
                    if (item.Number != 2) return;
                    if (action == "replace") actual = new(StringComparer.Ordinal);
                    if (action == "throw") throw failure;
                    if (action == "culture") CultureInfo.CurrentCulture = afterCulture;
                };
                try { LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(payload, ref actual); }
                catch (Exception error) { after = error; }
                Assert.Same(before, after);
                Assert.Equal(beforeCalls, afterCulture.Calls);
                Assert.Equal(expected.Keys, actual.Keys);
                Assert.Equal(expected.Values.Select(item => item.Number), actual.Values.Select(item => item.Number));
                Assert.Equal(Version(expected), Version(actual));
                Assert.Equal(ReferenceEquals(beforeOriginal, expected), ReferenceEquals(afterOriginal, actual));
            }
        }
        finally
        {
            SortedReuseCallbackValue.OnRead = null;
            CultureInfo.CurrentCulture = saved;
        }
    }

    /// <summary>Checks the same string prefix under default and explicit comparison contracts.</summary>
    /// <typeparam name="TOptions">The complete binary profile.</typeparam>
    private static void CheckSets<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        foreach (string?[] values in new string?[][]
        {
            Array.Empty<string?>(), new string?[] { null }, new[] { "a", "b", "c" }, new[] { "c", "a", "b" },
            new[] { "a", "a", "b" }, new string?[] { null, "a", null }, new[] { "I", "i", "\u0130", "\u0131", "\u00e4" }
        })
        {
            byte[] payload = LuminPackSerializer.Serialize<TOptions>(values);
            foreach (bool custom in new[] { false, true })
            for (int prefix = 0; prefix <= payload.Length; prefix++)
            {
                IComparer<string?> comparer = custom ? StringComparer.OrdinalIgnoreCase : Comparer<string?>.Default;
                var expected = new SortedSet<string?>(comparer) { "old" };
                var actual = new SortedSet<string?>(comparer) { "old" };
                var original = actual;
                byte[] input = payload.AsSpan(0, prefix).ToArray();
                Exception? before = null, after = null;
                try { ReadSetOriginal<TOptions>(input, expected); } catch (Exception error) { before = error; }
                try { LuminPackSerializer.Deserialize<TOptions>(input, ref actual); } catch (Exception error) { after = error; }
                Assert.Equal(before?.GetType(), after?.GetType());
                Assert.Equal(before?.Message, after?.Message);
                Assert.Same(original, actual);
                Assert.Same(comparer, actual.Comparer);
                Assert.Equal(expected, actual);
                Assert.Equal(Version(expected), Version(actual));
            }
        }
    }

    /// <summary>Runs the former reused-set algorithm over stationary input.</summary>
    /// <typeparam name="TOptions">The complete binary profile.</typeparam>
    /// <param name="payload">The complete or truncated record.</param>
    /// <param name="target">The existing destination.</param>
    private static unsafe void ReadSetOriginal<TOptions>(byte[] payload, SortedSet<string?> target)
        where TOptions : struct, ILuminPackOptions
    {
        fixed (byte* pointer = payload)
        {
            ReadOnlySpan<byte> span = new(pointer, payload.Length);
            var reader = new LuminPackReader(ref span);
            int count = reader.ReadLength(1);
            target.Clear();
            for (int index = 0; index < count; index++) target.Add(reader.Read<TOptions>((string?)null));
        }
    }

    /// <summary>Exercises a reused reference-valued map against direct public insertion.</summary>
    /// <typeparam name="TOptions">The complete binary profile.</typeparam>
    private static void CheckLists<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new SortedList<string, SortedReuseCallbackValue>
        {
            ["a"] = new() { Number = 1 }, ["b"] = new() { Number = 2 }, ["c"] = new() { Number = 3 }
        };
        byte[] payload = LuminPackSerializer.Serialize<TOptions>(source);
        foreach (bool custom in new[] { false, true })
        foreach (bool mutate in new[] { false, true })
        foreach (int capacity in new[] { 0, 16 })
        for (int prefix = 0; prefix <= payload.Length; prefix++)
        {
            var beforeComparer = new CountingComparer();
            var afterComparer = new CountingComparer();
            var expected = new SortedList<string, SortedReuseCallbackValue>(capacity, custom ? beforeComparer : Comparer<string>.Default);
            var actual = new SortedList<string, SortedReuseCallbackValue>(capacity, custom ? afterComparer : Comparer<string>.Default);
            var original = actual;
            byte[] input = payload.AsSpan(0, prefix).ToArray();
            var beforeEvents = new List<string>();
            var afterEvents = new List<string>();
            Exception? before = null, after = null;
            try
            {
                SortedReuseCallbackValue.OnRead = item =>
                {
                    beforeEvents.Add(item.Number + ":" + expected.Count);
                    if (mutate) expected["external" + item.Number] = new() { Number = -item.Number };
                };
                try { ReadListOriginal<TOptions>(input, ref expected); } catch (Exception error) { before = error; }
                SortedReuseCallbackValue.OnRead = item =>
                {
                    afterEvents.Add(item.Number + ":" + actual.Count);
                    if (mutate) actual["external" + item.Number] = new() { Number = -item.Number };
                };
                try { LuminPackSerializer.Deserialize<TOptions>(input, ref actual); } catch (Exception error) { after = error; }
            }
            finally { SortedReuseCallbackValue.OnRead = null; }
            Assert.Equal(before?.GetType(), after?.GetType());
            Assert.Equal(before?.Message, after?.Message);
            Assert.Equal(beforeEvents, afterEvents);
            Assert.Equal(expected.Keys, actual.Keys);
            Assert.Equal(expected.Values.Select(item => item.Number), actual.Values.Select(item => item.Number));
            Assert.Equal(expected.Capacity, actual.Capacity);
            Assert.Equal(Version(expected), Version(actual));
            Assert.Equal(beforeComparer.Calls, afterComparer.Calls);
            Assert.Same(original, actual);
        }
    }

    /// <summary>Runs the former reused-map algorithm with the unchanged concrete element reader.</summary>
    /// <typeparam name="TOptions">The complete binary profile.</typeparam>
    /// <param name="payload">The complete or truncated record.</param>
    /// <param name="target">The existing destination.</param>
    private static unsafe void ReadListOriginal<TOptions>(byte[] payload, ref SortedList<string, SortedReuseCallbackValue> target)
        where TOptions : struct, ILuminPackOptions
    {
        fixed (byte* pointer = payload)
        {
            ReadOnlySpan<byte> span = new(pointer, payload.Length);
            var reader = new LuminPackReader(ref span);
            int count = reader.ReadLength(1);
            target.Clear();
            for (int index = 0; index < count; index++)
            {
                string? key = reader.Read<TOptions>((string?)null);
                SortedReuseCallbackValue value = null!;
                LuminPackSerializer.Deserialize<TOptions>(ref reader, ref value);
                target.Add(key!, value);
            }
        }
    }

    /// <summary>Reads the same private version field used by the validated runtime storage gate.</summary>
    /// <param name="value">The original framework collection.</param>
    /// <returns>Its version when the test runtime exposes that field.</returns>
    private static int Version(object value)
        => (int)value.GetType().GetField("version", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;

    private sealed class CountingComparer : IComparer<string>
    {
        public int Calls;

        /// <summary>Counts every user comparison before retaining default ordering.</summary>
        /// <param name="left">The first key.</param>
        /// <param name="right">The second key.</param>
        /// <returns>The default comparison result.</returns>
        public int Compare(string? left, string? right)
        {
            Calls++;
            return Comparer<string>.Default.Compare(left, right);
        }
    }

    private sealed class ObservedCulture : CultureInfo
    {
        public int Calls;

        /// <summary>Creates an observable culture with ordinary English string ordering.</summary>
        public ObservedCulture() : base("en-US") { }

        /// <inheritdoc/>
        public override CompareInfo CompareInfo
        {
            get
            {
                Calls++;
                return base.CompareInfo;
            }
        }
    }
}

/// <summary>Observes each original value publication boundary.</summary>
[LuminPackable]
public sealed class SortedReuseCallbackValue
{
    public int Number;
    public static Action<SortedReuseCallbackValue>? OnRead;

    /// <summary>Invokes the selected test observer after reading the value.</summary>
    [LuminPackOnDeserialized]
    public void CompleteRead() => OnRead?.Invoke(this);
}

/// <summary>Supplies an observable user comparison contract that must retain ordinary set insertion.</summary>
[LuminPackable]
public sealed class SortedReuseComparedValue : IComparable<SortedReuseComparedValue>
{
    public int Number;
    public static readonly List<string> Comparisons = new();

    /// <summary>Records the comparison sequence.</summary>
    /// <param name="other">The other value.</param>
    /// <returns>The integer-field comparison.</returns>
    public int CompareTo(SortedReuseComparedValue? other)
    {
        Comparisons.Add(Number + ":" + other?.Number);
        return other is null ? 1 : Number.CompareTo(other.Number);
    }
}
