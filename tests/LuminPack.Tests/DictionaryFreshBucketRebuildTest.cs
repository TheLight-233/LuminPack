using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Preserves dictionary hash, equality and GC behavior while rebuilding fresh entries.</summary>
public class DictionaryFreshBucketRebuildTest
{
    /// <summary>Retains colliding value keys through compacting GC and the long-chain Add fallback.</summary>
    /// <param name="count">The number of distinct keys sharing one hash code.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(104)]
    public void CollidingValueKeysRetainLookupAndMutationBehavior(int count)
    {
        var source = Enumerable.Range(0, count).ToDictionary(index => new CollisionKey { Value = index }, index => index * 7);
        byte[] payload = LuminPackSerializer.Serialize(source);
        try
        {
            CollisionKey.BeforeHash = CompactHeap;
            var copy = LuminPackSerializer.Deserialize<Dictionary<CollisionKey, int>>(payload);
            CollisionKey.BeforeHash = null;
            Assert.Equal(source.ToArray(), copy.ToArray());
            foreach (var pair in source) Assert.Equal(pair.Value, copy[pair.Key]);
            copy.Add(new CollisionKey { Value = count }, 701);
            Assert.Equal(701, copy[new CollisionKey { Value = count }]);
            Assert.True(copy.Remove(new CollisionKey { Value = count }));
            Assert.Equal(payload, LuminPackSerializer.Serialize(copy));
        }
        finally { CollisionKey.BeforeHash = null; }
    }

    /// <summary>Rejects equal struct keys instead of publishing a dictionary with duplicate entries.</summary>
    [Fact]
    public void EqualValueKeysStillRejectDuplicateInput()
    {
        var source = new Dictionary<CollisionKey, int>
        {
            [new CollisionKey { Value = 11 }] = 71,
            [new CollisionKey { Value = 23 }] = 73
        };
        byte[] payload = LuminPackSerializer.Serialize(source);
        Assert.Equal(20, payload.Length);
        payload.AsSpan(4, 4).CopyTo(payload.AsSpan(12, 4));
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize<Dictionary<CollisionKey, int>>(payload));
    }

    /// <summary>Retains reference-key equality and managed entry roots during hash callbacks.</summary>
    [Fact]
    public void CollidingReferenceKeysSurviveCompactingCollection()
    {
        var source = Enumerable.Range(0, 32).ToDictionary(index => new ReferenceKey { Value = index }, index => index * 11);
        byte[] payload = LuminPackSerializer.Serialize(source);
        try
        {
            ReferenceKey.BeforeHash = CompactHeap;
            var copy = LuminPackSerializer.Deserialize<Dictionary<ReferenceKey, int>>(payload);
            ReferenceKey.BeforeHash = null;
            Assert.Equal(source.Select(pair => (pair.Key.Value, pair.Value)), copy.Select(pair => (pair.Key.Value, pair.Value)));
            foreach (var pair in source) Assert.Equal(pair.Value, copy[new ReferenceKey { Value = pair.Key.Value }]);
            Assert.Equal(payload, LuminPackSerializer.Serialize(copy));
        }
        finally { ReferenceKey.BeforeHash = null; }
    }

    /// <summary>Preserves the public dictionary null-key failure when a fresh key formatter returns null.</summary>
    [Fact]
    public void NullReferenceKeyStillFails()
    {
        byte[] payload = [1, 0, 0, 0, 255, 17, 0, 0, 0];
        Assert.Throws<ArgumentNullException>(() => LuminPackSerializer.Deserialize<Dictionary<ReferenceKey, int>>(payload));
    }

    /// <summary>Collects while the bucket builder holds managed interior references into its entry array.</summary>
    private static void CompactHeap()
    {
        CollisionKey.BeforeHash = null;
        ReferenceKey.BeforeHash = null;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    /// <summary>Provides raw value storage with caller-defined hashing and equality.</summary>
    [LuminPackable]
    public partial struct CollisionKey : IEquatable<CollisionKey>
    {
        public int Value;
        public static Action? BeforeHash { get; set; }

        /// <summary>Creates collisions without treating distinct values as equal.</summary>
        /// <returns>The deliberately shared hash code.</returns>
        public override int GetHashCode()
        {
            BeforeHash?.Invoke();
            return 7;
        }

        /// <summary>Compares the serialized identity of two value keys.</summary>
        /// <param name="other">The key to compare.</param>
        /// <returns>Whether both keys have the same identity.</returns>
        public bool Equals(CollisionKey other) => Value == other.Value;

        /// <summary>Compares a boxed key using the same value equality.</summary>
        /// <param name="obj">The possible key.</param>
        /// <returns>Whether the object is an equal key.</returns>
        public override bool Equals(object? obj) => obj is CollisionKey other && Equals(other);
    }

    /// <summary>Provides reference keys whose hash callback can relocate the containing entry array.</summary>
    [LuminPackable]
    public sealed partial class ReferenceKey : IEquatable<ReferenceKey>
    {
        public int Value;
        public static Action? BeforeHash { get; set; }

        /// <summary>Creates collisions while allowing a controlled GC callback.</summary>
        /// <returns>The deliberately shared hash code.</returns>
        public override int GetHashCode()
        {
            BeforeHash?.Invoke();
            return 7;
        }

        /// <summary>Compares serialized identities rather than object references.</summary>
        /// <param name="other">The key to compare.</param>
        /// <returns>Whether both keys have the same identity.</returns>
        public bool Equals(ReferenceKey? other) => other is not null && Value == other.Value;

        /// <summary>Compares an object using the typed key contract.</summary>
        /// <param name="obj">The possible key.</param>
        /// <returns>Whether the object is an equal key.</returns>
        public override bool Equals(object? obj) => obj is ReferenceKey other && Equals(other);
    }
}
