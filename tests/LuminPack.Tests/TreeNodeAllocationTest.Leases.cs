using System.Buffers;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Versioning;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public sealed partial class TreeNodeAllocationTest
{
    /// <summary>Returns the original lease before a collision fallback invokes the public builder.</summary>
    /// <param name="dictionary">Whether to construct key/value buckets.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HashCollisionFallbackReleasesItsLeaseBeforeCallbacks(bool dictionary)
    {
        var items = new[] { new HashLeaseKey { Value = 7 }, new HashLeaseKey { Value = 11 } };
        var pairs = items.Select(item => new KeyValuePair<HashLeaseKey, int>(item, item.Value)).ToArray();
        HashLeaseKey.Failure = null;
        Func<object> build = dictionary
            ? () => LuminTreeStorage.CreateImmutableDictionary(pairs, pairs.Length)
            : () => LuminTreeStorage.CreateImmutableHashSet(items, items.Length);
        GC.KeepAlive(build());
        long[] expected = ArrayPool<long>.Shared.Rent(16);
        ArrayPool<long>.Shared.Return(expected);
        bool observed = false;
        try
        {
            HashLeaseKey.Calls = 0;
            HashLeaseKey.Collision = true;
            HashLeaseKey.ObserveHash = () =>
            {
                if (HashLeaseKey.Calls != 3) return;
                long[] first = ArrayPool<long>.Shared.Rent(16);
                long[] second = ArrayPool<long>.Shared.Rent(16);
                try
                {
                    Assert.Same(expected, first);
                    Assert.NotSame(first, second);
                    observed = true;
                }
                finally
                {
                    if (!ReferenceEquals(first, second)) ArrayPool<long>.Shared.Return(second);
                    ArrayPool<long>.Shared.Return(first);
                }
            };
            GC.KeepAlive(build());
            Assert.True(observed);
        }
        finally
        {
            HashLeaseKey.Collision = false;
            HashLeaseKey.ObserveHash = null;
        }
    }

    /// <summary>Returns scratch storage used by a successful family layout self-check.</summary>
    /// <param name="dictionary">Whether to exercise the dictionary layout check.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HashLayoutProbeReturnsItsLease(bool dictionary)
    {
        string name = dictionary ? "ImmutableDictionaryLayout`2" : "ImmutableHashSetLayout`1";
        Type[] arguments = dictionary ? [typeof(int), typeof(int)] : [typeof(int)];
        Assert.True((bool)Layout(name, arguments, "Available"));
        var type = typeof(LuminTreeStorage).GetNestedType(name, BindingFlags.NonPublic)!.MakeGenericType(arguments);
        var probe = type.GetMethod("Probe", BindingFlags.Static | BindingFlags.NonPublic)!;
        int count = dictionary ? 31 : 41;
        long[] expected = ArrayPool<long>.Shared.Rent(count);
        ArrayPool<long>.Shared.Return(expected);
        long[]? actual = null;
        try
        {
            Assert.True((bool)probe.Invoke(null, null)!);
            actual = ArrayPool<long>.Shared.Rent(count);
            Assert.Same(expected, actual);
        }
        finally
        {
            if (actual is not null) ArrayPool<long>.Shared.Return(actual);
            if (!ReferenceEquals(expected, actual)) ArrayPool<long>.Shared.Return(expected);
        }
    }

    /// <summary>Returns hash ordering storage after scan failures and any later singleton-factory callback.</summary>
    /// <param name="dictionary">Whether to construct key/value buckets.</param>
    /// <param name="failAt">The hash callback that throws the original exception.</param>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 3)]
    public void HashConstructionReturnsItsLeaseOnCallbackFailure(bool dictionary, int failAt)
    {
        var items = new[] { new HashLeaseKey { Value = 7 }, new HashLeaseKey { Value = 11 } };
        var pairs = items.Select(item => new KeyValuePair<HashLeaseKey, int>(item, item.Value)).ToArray();
        HashLeaseKey.Failure = null;
        Func<object> build = dictionary
            ? () => LuminTreeStorage.CreateImmutableDictionary(pairs, pairs.Length)
            : () => LuminTreeStorage.CreateImmutableHashSet(items, items.Length);
        GC.KeepAlive(build());
        long[] expected = ArrayPool<long>.Shared.Rent(16);
        ArrayPool<long>.Shared.Return(expected);
        long[]? actual = null;
        var failure = new InvalidOperationException("Selected hash failure");
        try
        {
            HashLeaseKey.Calls = 0;
            HashLeaseKey.FailAt = failAt;
            HashLeaseKey.Failure = failure;
            Exception? observed = null;
            try { GC.KeepAlive(build()); } catch (Exception error) { observed = error; }
            actual = ArrayPool<long>.Shared.Rent(16);
            bool constructedDirectly = false;
#if NET10_0_OR_GREATER
            constructedDirectly = dictionary && failAt == 3 &&
                Layout("ImmutableDictionaryConstructors`2", new[] { typeof(HashLeaseKey), typeof(int) }, "Wrap") is not null;
#endif
            if (constructedDirectly)
            {
                Assert.Null(observed);
                Assert.Equal(items.Length, HashLeaseKey.Calls);
            }
            else
            {
                Assert.Same(failure, observed);
                Assert.Contains(nameof(HashLeaseKey.GetHashCode), observed!.StackTrace);
                Assert.Equal(failAt, HashLeaseKey.Calls);
            }
            Assert.Same(expected, actual);
        }
        finally
        {
            HashLeaseKey.Failure = null;
            if (actual is not null) ArrayPool<long>.Shared.Return(actual);
            if (!ReferenceEquals(expected, actual)) ArrayPool<long>.Shared.Return(expected);
        }
    }

    /// <summary>Keeps original hash exceptions, unpublished targets and cleared element leases in both document formats.</summary>
    /// <param name="dictionary">Whether to read a dictionary or a set.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public unsafe void FailedHashReadsReturnNestedLeases(bool dictionary)
    {
        HashLeaseKey.Failure = null;
        var items = new[] { new HashLeaseKey { Value = 7 }, new HashLeaseKey { Value = 11 } };
        var set = ImmutableHashSet.CreateRange(items);
        var map = items.ToImmutableDictionary(item => item, item => item.Value);
        using var output = new LuminBufferWriter(true);
        for (int format = 0; format < 3; format++)
        {
            output.ResetWrittenCount();
            if (dictionary)
            {
                if (format == 0) LuminPackSerializer.Serialize(map, output);
                else if (format == 1) LuminPackSerializer.SerializeJson<ImmutableDictionary<HashLeaseKey, int>, LuminPackUtf8LengthOptions>(map, output);
                else LuminPackSerializer.SerializeJson<ImmutableDictionary<HashLeaseKey, int>, LuminPackUtf16LengthOptions>(map, output);
            }
            else
            {
                if (format == 0) LuminPackSerializer.Serialize(set, output);
                else if (format == 1) LuminPackSerializer.SerializeJson<ImmutableHashSet<HashLeaseKey>, LuminPackUtf8LengthOptions>(set, output);
                else LuminPackSerializer.SerializeJson<ImmutableHashSet<HashLeaseKey>, LuminPackUtf16LengthOptions>(set, output);
            }
            byte[] bytes = output.GetSpan().ToArray();
            ImmutableHashSet<HashLeaseKey>? readSet = null;
            ImmutableDictionary<HashLeaseKey, int>? readMap = null;
            fixed (byte* pin = bytes) Read();
            var failure = new InvalidOperationException("Selected decoded hash failure");
            long[] expectedHashes = ArrayPool<long>.Shared.Rent(16);
            var expectedItems = ArrayPool<HashLeaseKey>.Shared.Rent(16);
            var expectedPairs = ArrayPool<KeyValuePair<HashLeaseKey, int>>.Shared.Rent(16);
            ArrayPool<long>.Shared.Return(expectedHashes);
            ArrayPool<HashLeaseKey>.Shared.Return(expectedItems, true);
            ArrayPool<KeyValuePair<HashLeaseKey, int>>.Shared.Return(expectedPairs, true);
            long[]? actualHashes = null;
            HashLeaseKey[]? actualItems = null;
            KeyValuePair<HashLeaseKey, int>[]? actualPairs = null;
            try
            {
                readSet = null; readMap = null;
                HashLeaseKey.Calls = 0; HashLeaseKey.FailAt = 1; HashLeaseKey.Failure = failure;
                Exception? observed = null;
                try { fixed (byte* pin = bytes) Read(); } catch (Exception error) { observed = error; }
                actualHashes = ArrayPool<long>.Shared.Rent(16);
                actualItems = ArrayPool<HashLeaseKey>.Shared.Rent(16);
                actualPairs = ArrayPool<KeyValuePair<HashLeaseKey, int>>.Shared.Rent(16);
                Assert.Same(failure, observed);
                Assert.Null(readSet); Assert.Null(readMap);
                Assert.Same(expectedHashes, actualHashes);
                Assert.Same(expectedItems, actualItems);
                Assert.Same(expectedPairs, actualPairs);
                Assert.All(actualItems, item => Assert.Null(item));
                Assert.All(actualPairs, pair => Assert.Null(pair.Key));
            }
            finally
            {
                HashLeaseKey.Failure = null;
                if (actualHashes is not null) ArrayPool<long>.Shared.Return(actualHashes);
                if (actualItems is not null) ArrayPool<HashLeaseKey>.Shared.Return(actualItems, true);
                if (actualPairs is not null) ArrayPool<KeyValuePair<HashLeaseKey, int>>.Shared.Return(actualPairs, true);
                if (!ReferenceEquals(expectedHashes, actualHashes)) ArrayPool<long>.Shared.Return(expectedHashes);
                if (!ReferenceEquals(expectedItems, actualItems)) ArrayPool<HashLeaseKey>.Shared.Return(expectedItems, true);
                if (!ReferenceEquals(expectedPairs, actualPairs)) ArrayPool<KeyValuePair<HashLeaseKey, int>>.Shared.Return(expectedPairs, true);
            }

            void Read()
            {
                if (dictionary)
                {
                    if (format == 0) LuminPackSerializer.Deserialize(bytes, ref readMap!);
                    else if (format == 1) LuminPackSerializer.DeserializeJson<ImmutableDictionary<HashLeaseKey, int>, LuminPackUtf8LengthOptions>(bytes, ref readMap!);
                    else LuminPackSerializer.DeserializeJson<ImmutableDictionary<HashLeaseKey, int>, LuminPackUtf16LengthOptions>(bytes, ref readMap!);
                }
                else
                {
                    if (format == 0) LuminPackSerializer.Deserialize(bytes, ref readSet!);
                    else if (format == 1) LuminPackSerializer.DeserializeJson<ImmutableHashSet<HashLeaseKey>, LuminPackUtf8LengthOptions>(bytes, ref readSet!);
                    else LuminPackSerializer.DeserializeJson<ImmutableHashSet<HashLeaseKey>, LuminPackUtf16LengthOptions>(bytes, ref readSet!);
                }
            }
        }
    }

    /// <summary>Returns compatibility staging when a fixed writer cannot reserve its raw record.</summary>
    /// <param name="family">The sorted mutable, sorted immutable or hashed immutable family.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedRawTreeWriteReturnsItsLease(int family)
    {
        long[] items = [7, 11];
        var sorted = new SortedSet<long>(items);
        var immutableSorted = ImmutableSortedSet.CreateRange(items);
        var hash = ImmutableHashSet.CreateRange(items);
        LuminTreeStorage.CopyTo(sorted, items.AsSpan());
        LuminTreeStorage.CopyTo(immutableSorted, items.AsSpan());
        LuminTreeStorage.CopyTo(hash, items.AsSpan());
        long[] expected = ArrayPool<long>.Shared.Rent(16);
        expected[0] = long.MinValue;
        ArrayPool<long>.Shared.Return(expected);
        long[]? actual = null;
        try
        {
            Span<byte> bytes = stackalloc byte[1];
            var writer = new LuminPackWriter(ref bytes);
            Exception? failure = null;
            try
            {
                if (family == 0) LuminTreeStorage.WriteRaw(ref writer, sorted);
                else if (family == 1) LuminTreeStorage.WriteRaw(ref writer, immutableSorted);
                else LuminTreeStorage.WriteRaw(ref writer, hash);
            }
            catch (Exception error) { failure = error; }
            actual = ArrayPool<long>.Shared.Rent(16);
            Assert.NotNull(failure);
            Assert.Same(expected, actual);
            bool portable = typeof(LuminPackWriter).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName.StartsWith(".NETStandard", StringComparison.Ordinal);
            if (portable || !Environment.Is64BitProcess) Assert.NotEqual(long.MinValue, expected[0]);
        }
        finally
        {
            if (actual is not null) ArrayPool<long>.Shared.Return(actual);
            if (!ReferenceEquals(expected, actual)) ArrayPool<long>.Shared.Return(expected);
        }
    }

    /// <summary>Provides a stable hash whose chosen invocation throws the caller's exception.</summary>
    [LuminPackable]
    public sealed class HashLeaseKey : IEquatable<HashLeaseKey>
    {
        public int Value;
        public static Exception? Failure;
        public static int Calls, FailAt;
        public static bool Collision;
        public static Action? ObserveHash;

        /// <inheritdoc/>
        public bool Equals(HashLeaseKey? other) => other is not null && Value == other.Value;

        /// <inheritdoc/>
        public override bool Equals(object? other) => other is HashLeaseKey key && Equals(key);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            Calls++;
            ObserveHash?.Invoke();
            if (Calls == FailAt && Failure is not null) throw Failure;
            return Collision ? 0 : Value;
        }
    }
}
