using System.Buffers;
using System.Collections;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects exact Dictionary storage selected through mutable and read-only interface contracts.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class DictionaryInterfaceReuseTest
{
    /// <summary>Retains containers, keys, values and nested storage in every binary profile.</summary>
    [Fact]
    public void ExactDictionariesReuseReferenceValuesInEveryProfile()
    {
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf8TokenOptions>();
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Uses the selected concrete comparer when finding an existing value under changed key text.</summary>
    [Fact]
    public void ExistingComparerControlsValueMatching()
    {
        var backing = new Dictionary<string, ReferenceReuseValue>(StringComparer.OrdinalIgnoreCase) { ["a"] = Make(719) };
        var previous = backing["a"];
        IDictionary<string, ReferenceReuseValue> target = backing;
        IDictionary<string, ReferenceReuseValue> source = new Dictionary<string, ReferenceReuseValue> { ["A"] = Make(137) };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(backing, target);
        Assert.Same(StringComparer.OrdinalIgnoreCase, backing.Comparer);
        Assert.Same(previous, target["a"]);
        Assert.Equal(137, previous.Number);
        Assert.Equal("A", Assert.Single(target.Keys));
    }

    /// <summary>Preserves the original fresh fallback for derived and unrecognized implementations.</summary>
    [Fact]
    public void UnknownAndDerivedTargetsRemainUnobserved()
    {
        IDictionary<string, ReferenceReuseValue> source = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(137) };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        IReadOnlyDictionary<string, ReferenceReuseValue> unknown = new UnobservedDictionary();
        LuminPackSerializer.Deserialize(bytes, ref unknown);
        Assert.IsType<Dictionary<string, ReferenceReuseValue>>(unknown);
        var derived = new DerivedDictionary { ["a"] = Make(719) };
        IDictionary<string, ReferenceReuseValue> target = derived;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.IsType<Dictionary<string, ReferenceReuseValue>>(target);
        Assert.NotSame(derived, target);
        Assert.Equal(719, derived["a"].Number);
    }

    /// <summary>Keeps callback receiver identity without constructing replacement ordinary values.</summary>
    [Fact]
    public void ReusedValuesDoNotRunTheirConstructorsAgain()
    {
        IDictionary<int, DictionaryInterfaceConstructedValue> source = new Dictionary<int, DictionaryInterfaceConstructedValue> { [17] = new() { Number = 137 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var first = new DictionaryInterfaceConstructedValue { Number = 719 };
        IDictionary<int, DictionaryInterfaceConstructedValue> target = new Dictionary<int, DictionaryInterfaceConstructedValue> { [17] = first };
        int before = DictionaryInterfaceConstructedValue.Constructions;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(first, target[17]);
        Assert.Equal(137, first.Number);
        Assert.Equal(before, DictionaryInterfaceConstructedValue.Constructions);
    }

    /// <summary>Compares every failed prefix with the selected concrete reader, including aliased nested mutations.</summary>
    [Fact]
    public void TruncatedReadsRetainTheConcreteFailureBoundary()
    {
        IDictionary<string, ReferenceReuseValue> source = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(137), ["b"] = Make(271) };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        for (int prefix = 0; prefix < bytes.Length; prefix++)
        {
            var expected = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(719), ["b"] = Make(981) };
            var backing = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(719), ["b"] = Make(981) };
            IReadOnlyDictionary<string, ReferenceReuseValue> target = backing;
            byte[] input = bytes.AsSpan(0, prefix).ToArray();
            Exception? a = Record.Exception(() => LuminPackSerializer.Deserialize(input, ref expected));
            Exception? b = Record.Exception(() => LuminPackSerializer.Deserialize(input, ref target));
            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.Equal(a.GetType(), b.GetType());
            Assert.Equal(a.Message, b.Message);
            Assert.Same(backing, target);
            Assert.Equal(expected.Select(pair => pair.Value.Number), target.Select(pair => pair.Value.Number));
            Assert.Equal(expected.Select(pair => pair.Value.Child.Text), target.Select(pair => pair.Value.Child.Text));
        }
    }

    /// <summary>Keeps explicit reader cursor publication identical to the existing concrete operation on failure.</summary>
    [Fact]
    public unsafe void ExplicitReaderFailuresKeepTheConcreteCursor()
    {
        var source = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(137), ["b"] = Make(271) };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        fixed (byte* pinned = bytes)
        {
            for (int prefix = 0; prefix < bytes.Length; prefix++)
            {
                ReadOnlySpan<byte> input = bytes.AsSpan(0, prefix);
                var expectedReader = new LuminPackReader(ref input);
                var actualReader = new LuminPackReader(ref input);
                var expected = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(719), ["b"] = Make(981) };
                IDictionary<string, ReferenceReuseValue> actual = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(719), ["b"] = Make(981) };
                Exception? a = null, b = null;
                try { expectedReader.Read(ref expected); } catch (Exception error) { a = error; }
                try { actualReader.Read(ref actual); } catch (Exception error) { b = error; }
                Assert.NotNull(a); Assert.NotNull(b); Assert.Equal(a.GetType(), b.GetType());
                Assert.Equal(expectedReader.Position, actualReader.Position);
                Assert.Equal(expected.Select(pair => pair.Value.Number), actual.Select(pair => pair.Value.Number));
            }
        }
    }

    /// <summary>Retains ordinary aliases when distinct target keys already share one value.</summary>
    [Fact]
    public void ExistingValueAliasesRemainObservable()
    {
        IDictionary<string, ReferenceReuseValue> source = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(137), ["b"] = Make(271) };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var shared = Make(719);
        IReadOnlyDictionary<string, ReferenceReuseValue> target = new Dictionary<string, ReferenceReuseValue> { ["a"] = shared, ["b"] = shared };
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(shared, target["a"]); Assert.Same(shared, target["b"]); Assert.Equal(271, shared.Number);
    }

    /// <summary>Publishes a captured concrete dictionary only after its callbacks and complete parse succeed.</summary>
    /// <param name="fail">Whether the replacing callback throws.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackReplacementPublishesOnlyOnSuccess(bool fail)
    {
        var backing = new Dictionary<string, ReferenceReuseCallback> { ["a"] = new() { Number = 719 }, ["b"] = new() { Number = 981 } };
        IReadOnlyDictionary<string, ReferenceReuseCallback> target = backing;
        IReadOnlyDictionary<string, ReferenceReuseCallback> source = new Dictionary<string, ReferenceReuseCallback> { ["a"] = new() { Number = 137 }, ["b"] = new() { Number = 271 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var replacement = new Dictionary<string, ReferenceReuseCallback>();
        var failure = new InvalidOperationException("The callback replaced its interface target.");
        ReferenceReuseCallback.OnRead = value =>
        {
            if (value.Number != 137) return;
            Assert.Same(backing["a"], value);
            target = replacement;
            if (fail) throw failure;
        };
        try
        {
            Exception? actual = Record.Exception(() => LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Same(fail ? failure : null, actual);
            Assert.Same(fail ? replacement : backing, target);
            Assert.Equal(137, backing["a"].Number);
            Assert.Equal(fail ? 981 : 271, backing["b"].Number);
        }
        finally { ReferenceReuseCallback.OnRead = null; }
    }

    /// <summary>Preserves caller exception identity when an existing custom comparer rejects lookup.</summary>
    [Fact]
    public void CustomComparerFailureKeepsTheSelectedTarget()
    {
        IDictionary<string, ReferenceReuseValue> source = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(137) };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var comparer = new FailingComparer();
        var first = Make(719);
        var backing = new Dictionary<string, ReferenceReuseValue>(comparer) { ["a"] = first };
        IReadOnlyDictionary<string, ReferenceReuseValue> target = backing;
        comparer.Fail = true;
        Exception? actual = Record.Exception(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(comparer.Failure, actual);
        Assert.Same(backing, target);
        Assert.Equal(719, first.Number);
    }

    /// <summary>Clears the existing common pair rental after success and callback failure.</summary>
    /// <param name="fail">Whether the selected element callback throws.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingSharedPairRentalIsCleared(bool fail)
    {
        IDictionary<string, ReferenceReuseCallback> source = new Dictionary<string, ReferenceReuseCallback> { ["a"] = new() { Number = 137 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        IReadOnlyDictionary<string, ReferenceReuseCallback> target = new Dictionary<string, ReferenceReuseCallback> { ["a"] = new() { Number = 719 } };
        var pool = ArrayPool<KeyValuePair<string, ReferenceReuseCallback>>.Shared;
        var rental = pool.Rent(1);
        Array.Fill(rental, new KeyValuePair<string, ReferenceReuseCallback>("poison", new() { Number = -1 }));
        pool.Return(rental, false);
        var failure = new InvalidOperationException("The selected value failed.");
        ReferenceReuseCallback.OnRead = _ => { if (fail) throw failure; };
        try
        {
            Exception? actual = Record.Exception(() => LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Same(fail ? failure : null, actual);
            var returned = pool.Rent(1);
            try { Assert.Same(rental, returned); Assert.All(returned, pair => { Assert.Null(pair.Key); Assert.Null(pair.Value); }); }
            finally { pool.Return(returned, true); }
        }
        finally { ReferenceReuseCallback.OnRead = null; }
    }

    /// <summary>Retains both dictionary interface contracts without requiring caller-authored concrete adapters.</summary>
    /// <typeparam name="TOptions">The complete binary string profile.</typeparam>
    private static void Check<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        IDictionary<string, ReferenceReuseValue> source = new Dictionary<string, ReferenceReuseValue> { ["a"] = Make(137), ["b"] = null! };
        byte[] bytes = LuminPackSerializer.Serialize<TOptions>(source);
        var backing = new Dictionary<string, ReferenceReuseValue> { [new string('a', 1)] = Make(719), ["b"] = Make(981), ["removed"] = Make(3) };
        IDictionary<string, ReferenceReuseValue> target = backing;
        var first = backing["a"]; var child = first.Child; var tags = first.Tags; var name = first.Name; var key = backing.Keys.First();
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TOptions>(bytes, ref target));
        Assert.Same(backing, target); Assert.Same(first, target["a"]); Assert.Same(child, first.Child); Assert.Same(tags, first.Tags); Assert.Same(name, first.Name); Assert.Same(key, backing.Keys.First());
        Assert.Equal(137, first.Number); Assert.Null(target["b"]); Assert.False(target.ContainsKey("removed"));
        IReadOnlyDictionary<string, ReferenceReuseValue> readOnly = backing;
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref readOnly);
        Assert.Same(backing, readOnly); Assert.Same(first, readOnly["a"]);
        IDictionary<string, string> text = new Dictionary<string, string> { ["a"] = new('x', 7) };
        var textRoot = text; string previous = text["a"];
        bytes = LuminPackSerializer.Serialize<TOptions>(text);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref text);
        Assert.Same(textRoot, text); Assert.Same(previous, text["a"]);
        source = new Dictionary<string, ReferenceReuseValue>(); bytes = LuminPackSerializer.Serialize<TOptions>(source);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref target); Assert.Same(backing, target); Assert.Empty(target);
        source = null!; bytes = LuminPackSerializer.Serialize<TOptions>(source);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref target); Assert.Null(target);
    }

    /// <summary>Creates independent ordinary nested storage with stable text.</summary>
    /// <param name="number">The mutable number in the value.</param>
    /// <returns>The independently allocated object graph.</returns>
    private static ReferenceReuseValue Make(int number) => new() { Number = number, Name = new('x', 7), Child = new() { Text = "child" }, Tags = ["a", "b"] };

    private sealed class DerivedDictionary : Dictionary<string, ReferenceReuseValue>;

    private sealed class FailingComparer : IEqualityComparer<string>
    {
        internal bool Fail;
        internal readonly Exception Failure = new InvalidOperationException("The existing comparer failed.");

        /// <summary>Uses ordinary ordinal string equality.</summary>
        /// <param name="x">The first string.</param>
        /// <param name="y">The second string.</param>
        /// <returns>Whether the strings are ordinally equal.</returns>
        public bool Equals(string? x, string? y) => StringComparer.Ordinal.Equals(x, y);

        /// <summary>Throws the selected caller exception after setup completes.</summary>
        /// <param name="value">The lookup key.</param>
        /// <returns>The ordinal hash when failure is disabled.</returns>
        public int GetHashCode(string value) => Fail ? throw Failure : StringComparer.Ordinal.GetHashCode(value);
    }

    private sealed class UnobservedDictionary : IReadOnlyDictionary<string, ReferenceReuseValue>
    {
        public ReferenceReuseValue this[string key] => throw new InvalidOperationException("The old dictionary must not be observed.");
        public IEnumerable<string> Keys => throw new InvalidOperationException("The old dictionary must not be observed.");
        public IEnumerable<ReferenceReuseValue> Values => throw new InvalidOperationException("The old dictionary must not be observed.");
        public int Count => throw new InvalidOperationException("The old dictionary must not be observed.");

        /// <summary>Rejects unexpected lookup on an excluded destination.</summary>
        /// <param name="key">The requested key.</param>
        /// <returns>No key lookup is supported.</returns>
        public bool ContainsKey(string key) => throw new InvalidOperationException("The old dictionary must not be observed.");

        /// <summary>Rejects unexpected lookup on an excluded destination.</summary>
        /// <param name="key">The requested key.</param>
        /// <param name="value">The unavailable value.</param>
        /// <returns>No value lookup is supported.</returns>
        public bool TryGetValue(string key, out ReferenceReuseValue value) => throw new InvalidOperationException("The old dictionary must not be observed.");

        /// <summary>Rejects unexpected enumeration of an excluded destination.</summary>
        /// <returns>No destination enumerator is available.</returns>
        public IEnumerator<KeyValuePair<string, ReferenceReuseValue>> GetEnumerator() => throw new InvalidOperationException("The old dictionary must not be observed.");

        /// <summary>Rejects unexpected nongeneric enumeration.</summary>
        /// <returns>No destination enumerator is available.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>Counts user construction independently of object field population.</summary>
[LuminPackable]
public sealed class DictionaryInterfaceConstructedValue
{
    public static int Constructions;
    public int Number;

    /// <summary>Records construction of a genuinely new value.</summary>
    public DictionaryInterfaceConstructedValue() => Constructions++;
}
