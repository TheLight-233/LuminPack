using System.Buffers.Binary;
using System.Collections.Immutable;
using LuminPack;
using LuminPack.Option;
using Xunit;

/// <summary>Protects complete-record reuse of immutable scalar sets.</summary>
public sealed class ImmutableScalarSetReuseTest
{
    /// <summary>Retains equal immutable sets and replaces changed records under every binary profile.</summary>
    [Fact]
    public void ProfilesRetainOnlyCompleteEqualRecords()
    {
        CheckProfile<LuminPackUtf8LengthOptions>();
        CheckProfile<LuminPackUtf8TokenOptions>();
        CheckProfile<LuminPackUtf16LengthOptions>();
        CheckProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks identities, count transitions and successful independent immutable updates.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    private static void CheckProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        foreach (int count in new[] { 1, 2, 31, 1000 })
        {
            var sorted = Enumerable.Range(-17, count).ToImmutableSortedSet();
            var hashed = sorted.ToImmutableHashSet();
            var sortedTarget = sorted;
            var hashedTarget = hashed;
            byte[] sortedBytes = LuminPackSerializer.Serialize<TOptions>(sorted);
            byte[] hashedBytes = LuminPackSerializer.Serialize<TOptions>(hashed);
            Assert.Equal(sortedBytes.Length, LuminPackSerializer.Deserialize<TOptions>(sortedBytes, ref sortedTarget));
            Assert.Equal(hashedBytes.Length, LuminPackSerializer.Deserialize<TOptions>(hashedBytes, ref hashedTarget));
            Assert.Same(sorted, sortedTarget);
            Assert.Same(hashed, hashedTarget);
            Assert.Equal(sortedBytes, LuminPackSerializer.Serialize<TOptions>(sortedTarget));
            Assert.Equal(hashedBytes, LuminPackSerializer.Serialize<TOptions>(hashedTarget));
            Assert.DoesNotContain(int.MaxValue, sorted);
            Assert.Contains(int.MaxValue, sortedTarget.Add(int.MaxValue));
            Assert.DoesNotContain(int.MaxValue, hashed);
            Assert.Contains(int.MaxValue, hashedTarget.Add(int.MaxValue));

            var changedSorted = sorted.Remove(-17).Add(2000);
            var changedHash = hashed.Remove(-17).Add(2000);
            LuminPackSerializer.Deserialize<TOptions>(LuminPackSerializer.Serialize<TOptions>(changedSorted), ref sortedTarget);
            LuminPackSerializer.Deserialize<TOptions>(LuminPackSerializer.Serialize<TOptions>(changedHash), ref hashedTarget);
            Assert.NotSame(sorted, sortedTarget);
            Assert.NotSame(hashed, hashedTarget);
            Assert.Equal(changedSorted, sortedTarget);
            Assert.True(changedHash.SetEquals(hashedTarget));
            LuminPackSerializer.Deserialize<TOptions>(new byte[4], ref sortedTarget);
            LuminPackSerializer.Deserialize<TOptions>(new byte[4], ref hashedTarget);
            Assert.Same(ImmutableSortedSet<int>.Empty, sortedTarget);
            Assert.Same(ImmutableHashSet<int>.Empty, hashedTarget);
            LuminPackSerializer.Deserialize<TOptions>(new byte[] { 255, 255, 255, 255 }, ref sortedTarget);
            LuminPackSerializer.Deserialize<TOptions>(new byte[] { 255, 255, 255, 255 }, ref hashedTarget);
            Assert.Null(sortedTarget);
            Assert.Null(hashedTarget);
        }
    }

    /// <summary>Uses raw equality so equal-comparing floating-point values still replace different bits.</summary>
    [Fact]
    public void FloatingPointBitsAreNotComparerEquality()
    {
        var positive = ImmutableSortedSet.Create(0.0f, 2.0f);
        var negative = ImmutableSortedSet.Create(BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)), 2.0f);
        var target = positive;
        byte[] bytes = LuminPackSerializer.Serialize(negative);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.NotSame(positive, target);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(target));
        var nanA = ImmutableHashSet.Create(BitConverter.Int32BitsToSingle(unchecked((int)0x7FC00001)), 3.0f);
        var nanB = ImmutableHashSet.Create(BitConverter.Int32BitsToSingle(unchecked((int)0x7FC00002)), 3.0f);
        var hashTarget = nanA;
        byte[] nanBytes = LuminPackSerializer.Serialize(nanB);
        LuminPackSerializer.Deserialize(nanBytes, ref hashTarget);
        Assert.NotSame(nanA, hashTarget);
        Assert.Equal(nanBytes, LuminPackSerializer.Serialize(hashTarget));
        var same = hashTarget;
        LuminPackSerializer.Deserialize(nanBytes, ref hashTarget);
        Assert.Same(same, hashTarget);
    }

    /// <summary>Rejects every truncated record without publishing or mutating an immutable destination.</summary>
    [Fact]
    public void IncompleteAndForgedCountsKeepTheDestination()
    {
        var sorted = ImmutableSortedSet.Create(3, 5, 7);
        var hashed = ImmutableHashSet.Create(3, 5, 7);
        byte[] bytes = LuminPackSerializer.Serialize(sorted);
        for (int length = 0; length < bytes.Length; length++)
        {
            var a = sorted;
            var b = hashed;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref a));
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref b));
            Assert.Same(sorted, a);
            Assert.Same(hashed, b);
        }
        foreach (int count in new[] { -2, int.MinValue, int.MaxValue })
        {
            byte[] forged = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(forged, count);
            var a = sorted;
            var b = hashed;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(forged, ref a));
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(forged, ref b));
            Assert.Same(sorted, a);
            Assert.Same(hashed, b);
        }
    }

    /// <summary>Replaces custom-comparer destinations without calling their comparison or hashing operations.</summary>
    [Fact]
    public void CustomComparersKeepTheOriginalDefaultReplacement()
    {
        var comparer = new ObservedComparer();
        var sorted = ImmutableSortedSet.Create<int>(comparer, 3, 5, 7);
        var hashed = ImmutableHashSet.Create<int>(comparer, 3, 5, 7);
        comparer.Reject = true;
        byte[] bytes = LuminPackSerializer.Serialize(new[] { 3, 5, 7 });
        var a = sorted;
        var b = hashed;
        LuminPackSerializer.Deserialize(bytes, ref a);
        LuminPackSerializer.Deserialize(bytes, ref b);
        Assert.NotSame(sorted, a);
        Assert.NotSame(hashed, b);
        Assert.Same(Comparer<int>.Default, a.KeyComparer);
        Assert.Same(EqualityComparer<int>.Default, b.KeyComparer);
        Assert.Equal(new[] { 3, 5, 7 }, a);
        Assert.True(b.SetEquals(a));
    }

    /// <summary>Keeps duplicate removal and ordering when an input record is not the existing enumeration.</summary>
    [Fact]
    public void UnorderedAndDuplicateRecordsUseOriginalConstruction()
    {
        var sorted = ImmutableSortedSet.Create(-1, 3, 5, 9);
        var hashed = sorted.ToImmutableHashSet();
        byte[] bytes = LuminPackSerializer.Serialize(new[] { 5, 3, 3, 9, -1 });
        var a = sorted;
        var b = hashed;
        LuminPackSerializer.Deserialize(bytes, ref a);
        LuminPackSerializer.Deserialize(bytes, ref b);
        Assert.NotSame(sorted, a);
        Assert.NotSame(hashed, b);
        Assert.Equal(sorted, a);
        Assert.True(hashed.SetEquals(b));
    }

    /// <summary>Preserves complete wider framework storage without typed aggregate reads.</summary>
    [Fact]
    public void WideFrameworkScalarsKeepTheirBits()
    {
        var longs = ImmutableSortedSet.Create(long.MinValue, -1L, long.MaxValue);
        var longTarget = longs;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(longs), ref longTarget);
        Assert.Same(longs, longTarget);
        var guids = ImmutableHashSet.Create(Guid.Empty, new Guid("e50e71a5-4367-4ba1-98d5-a0d6e3e89a19"));
        var guidTarget = guids;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(guids), ref guidTarget);
        Assert.Same(guids, guidTarget);
        var decimals = ImmutableSortedSet.Create(decimal.MinValue, 1.000m, decimal.MaxValue);
        var decimalTarget = decimals;
        byte[] bytes = LuminPackSerializer.Serialize(decimals);
        LuminPackSerializer.Deserialize(bytes, ref decimalTarget);
        Assert.Same(decimals, decimalTarget);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(decimalTarget));
    }

    /// <summary>Keeps narrow raw character and Boolean framing independent of the string profile.</summary>
    [Fact]
    public void NarrowScalarsKeepEveryProfile()
    {
        CheckNarrowProfile<LuminPackUtf8LengthOptions>();
        CheckNarrowProfile<LuminPackUtf8TokenOptions>();
        CheckNarrowProfile<LuminPackUtf16LengthOptions>();
        CheckNarrowProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks full narrow records and unchanged immutable identities under one binary profile.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    private static void CheckNarrowProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var characters = ImmutableHashSet.Create('\0', 'x', '\u4e2d', '\ud800', '\uffff');
        var sorted = characters.ToImmutableSortedSet();
        var booleans = ImmutableHashSet.Create(false, true);
        var characterTarget = characters;
        var sortedTarget = sorted;
        var booleanTarget = booleans;
        byte[] bytes = LuminPackSerializer.Serialize<TOptions>(characters);
        Assert.Equal(4 + characters.Count * sizeof(char), bytes.Length);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref characterTarget);
        LuminPackSerializer.Deserialize<TOptions>(LuminPackSerializer.Serialize<TOptions>(sorted), ref sortedTarget);
        LuminPackSerializer.Deserialize<TOptions>(LuminPackSerializer.Serialize<TOptions>(booleans), ref booleanTarget);
        Assert.Same(characters, characterTarget);
        Assert.Same(sorted, sortedTarget);
        Assert.Same(booleans, booleanTarget);
        Assert.Equal(bytes, LuminPackSerializer.Serialize<TOptions>(characterTarget));
    }

    /// <summary>Detects accidental observations of an excluded destination comparer.</summary>
    private sealed class ObservedComparer : IComparer<int>, IEqualityComparer<int>
    {
        internal bool Reject;

        /// <summary>Compares values unless the read boundary must leave this comparer unobserved.</summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>The normal numeric ordering.</returns>
        public int Compare(int x, int y) => Reject ? throw new InvalidOperationException("Excluded comparer observed.") : x.CompareTo(y);

        /// <summary>Checks equality unless the read boundary must leave this comparer unobserved.</summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>Whether the values match.</returns>
        public bool Equals(int x, int y) => Reject ? throw new InvalidOperationException("Excluded comparer observed.") : x == y;

        /// <summary>Hashes a value unless the read boundary must leave this comparer unobserved.</summary>
        /// <param name="obj">The value.</param>
        /// <returns>The numeric hash.</returns>
        public int GetHashCode(int obj) => Reject ? throw new InvalidOperationException("Excluded comparer observed.") : obj.GetHashCode();
    }
}
