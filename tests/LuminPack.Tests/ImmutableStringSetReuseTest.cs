using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using LuminPack;
using LuminPack.Option;
using Xunit;

/// <summary>Protects immutable string candidate reuse without changing complete string decoding.</summary>
public sealed class ImmutableStringSetReuseTest
{
    /// <summary>Retains equal containers and immutable strings under all four binary profiles.</summary>
    [Fact]
    public void EveryProfileRetainsEqualStorage()
    {
        CheckProfile<LuminPackUtf8LengthOptions>();
        CheckProfile<LuminPackUtf8TokenOptions>();
        CheckProfile<LuminPackUtf16LengthOptions>();
        CheckProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks equal records, changed membership and original strings in a replacement set.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    private static void CheckProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        string retained = new string('x', 37);
        var sorted = ImmutableSortedSet.CreateRange(new[] { "", "alpha", "beta", "\u4e2d\u6587-\ud83d\ude03", retained });
        var hashed = sorted.ToImmutableHashSet();
        var a = sorted;
        var b = hashed;
        byte[] sortedBytes = LuminPackSerializer.Serialize<TOptions>(sorted);
        byte[] hashBytes = LuminPackSerializer.Serialize<TOptions>(hashed);
        Assert.Equal(sortedBytes.Length, LuminPackSerializer.Deserialize<TOptions>(sortedBytes, ref a));
        Assert.Equal(hashBytes.Length, LuminPackSerializer.Deserialize<TOptions>(hashBytes, ref b));
        Assert.Same(sorted, a);
        Assert.Same(hashed, b);
        Assert.Equal(sortedBytes, LuminPackSerializer.Serialize<TOptions>(a));
        Assert.Equal(hashBytes, LuminPackSerializer.Serialize<TOptions>(b));
        var changed = sorted.Remove("alpha").Add("alphb");
        LuminPackSerializer.Deserialize<TOptions>(LuminPackSerializer.Serialize<TOptions>(changed), ref a);
        LuminPackSerializer.Deserialize<TOptions>(LuminPackSerializer.Serialize<TOptions>(changed.ToImmutableHashSet()), ref b);
        Assert.NotSame(sorted, a);
        Assert.NotSame(hashed, b);
        Assert.Equal(changed, a);
        Assert.True(changed.SetEquals(b));
        Assert.Contains("alpha", sorted);
        Assert.Contains("alpha", hashed);
        Assert.DoesNotContain("alpha", a);
        Assert.DoesNotContain("alpha", b);
        LuminPackSerializer.Deserialize<TOptions>(new byte[4], ref a);
        LuminPackSerializer.Deserialize<TOptions>(new byte[4], ref b);
        Assert.Same(ImmutableSortedSet<string>.Empty, a);
        Assert.Same(ImmutableHashSet<string>.Empty, b);
        LuminPackSerializer.Deserialize<TOptions>(new byte[] { 255, 255, 255, 255 }, ref a);
        LuminPackSerializer.Deserialize<TOptions>(new byte[] { 255, 255, 255, 255 }, ref b);
        Assert.Null(a);
        Assert.Null(b);
    }

    /// <summary>Rejects complete-header, partial-payload and malformed string records before publication.</summary>
    [Fact]
    public void TruncationAndMalformedStringsKeepOriginalSets()
    {
        var sorted = ImmutableSortedSet.Create("alpha", "beta", "gamma");
        var hashed = sorted.ToImmutableHashSet();
        byte[] bytes = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(sorted);
        for (int length = 0; length < bytes.Length; length++)
        {
            var a = sorted;
            var b = hashed;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes.AsSpan(0, length), ref a));
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes.AsSpan(0, length), ref b));
            Assert.Same(sorted, a);
            Assert.Same(hashed, b);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        var sortedTarget = sorted;
        var hashTarget = hashed;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes, ref sortedTarget));
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes, ref hashTarget));
        Assert.Same(sorted, sortedTarget);
        Assert.Same(hashed, hashTarget);
    }

    /// <summary>Preserves original default replacement without observing a custom destination comparer.</summary>
    [Fact]
    public void ExcludedComparersStayUnobserved()
    {
        var comparer = new ObservedComparer();
        var sorted = ImmutableSortedSet.Create<string>(comparer, "alpha", "beta");
        var hashed = ImmutableHashSet.Create<string>(comparer, "alpha", "beta");
        comparer.Reject = true;
        byte[] bytes = LuminPackSerializer.Serialize(new[] { "alpha", "beta" });
        var a = sorted;
        var b = hashed;
        LuminPackSerializer.Deserialize(bytes, ref a);
        LuminPackSerializer.Deserialize(bytes, ref b);
        Assert.NotSame(sorted, a);
        Assert.NotSame(hashed, b);
        Assert.Same(Comparer<string>.Default, a.KeyComparer);
        Assert.Same(EqualityComparer<string>.Default, b.KeyComparer);
        Assert.Equal(new[] { "alpha", "beta" }, a);
        Assert.True(a.SetEquals(b));
    }

    /// <summary>Preserves duplicate removal and default ordering for independent input order.</summary>
    [Fact]
    public void ReorderedDuplicateInputKeepsOriginalConstruction()
    {
        var sorted = ImmutableSortedSet.Create("alpha", "beta", "gamma");
        var hashed = sorted.ToImmutableHashSet();
        byte[] bytes = LuminPackSerializer.Serialize(new[] { "gamma", "alpha", "alpha", "beta" });
        var a = sorted;
        var b = hashed;
        LuminPackSerializer.Deserialize(bytes, ref a);
        LuminPackSerializer.Deserialize(bytes, ref b);
        Assert.NotSame(sorted, a);
        Assert.NotSame(hashed, b);
        Assert.Equal(sorted, a);
        Assert.True(hashed.SetEquals(b));
    }

    /// <summary>Preserves the existing token-mode normalization of null string elements.</summary>
    [Fact]
    public void NullElementsKeepExistingProfileNormalization()
    {
        CheckNullProfile<LuminPackUtf8LengthOptions>();
        CheckNullProfile<LuminPackUtf8TokenOptions>();
        CheckNullProfile<LuminPackUtf16LengthOptions>();
        CheckNullProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Compares set decoding with the original independent string-array decoder.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    private static void CheckNullProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        string[] source = [null!, "", "alpha"];
        byte[] bytes = LuminPackSerializer.Serialize<TOptions>(source);
        string[] decoded = null!;
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref decoded);
        var sorted = source.ToImmutableSortedSet();
        var hashed = source.ToImmutableHashSet();
        var a = sorted;
        var b = hashed;
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref a);
        LuminPackSerializer.Deserialize<TOptions>(LuminPackSerializer.Serialize<TOptions>(hashed), ref b);
        Assert.Equal(decoded.ToImmutableSortedSet(), a);
        Assert.True(decoded.ToImmutableHashSet().SetEquals(b));
        if (default(TOptions).StringRecording == LuminPackStringRecording.Length)
        {
            Assert.Same(sorted, a);
            Assert.Same(hashed, b);
        }
        else
        {
            Assert.NotSame(sorted, a);
            Assert.NotSame(hashed, b);
        }
    }

    /// <summary>Clears staged old and newly allocated references when a later element fails.</summary>
    [Fact]
    public void FailedReadClearsTheReferenceRental()
    {
        var sorted = ImmutableSortedSet.Create("alpha", "beta", "gamma");
        byte[] bytes = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(new[] { "alphb", "beta", "gamma" });
        var target = sorted;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Same(sorted, target);
        string[] rental = ArrayPool<string>.Shared.Rent(3);
        try { Assert.All(rental, item => Assert.Null(item)); }
        finally { ArrayPool<string>.Shared.Return(rental, true); }
    }

    /// <summary>Rebuilds unchanged text when the current default comparer no longer accepts the old tree order.</summary>
    [Fact]
    public void CultureChangesRevalidateSortedOrder()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var source = ImmutableSortedSet.Create("a", "\u00e4", "z");
            byte[] bytes = LuminPackSerializer.Serialize(source);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var target = source;
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.NotSame(source, target);
            Assert.Equal(new[] { "a", "z", "\u00e4" }, target);
            Assert.True(target.Contains("\u00e4"));
            Assert.Equal(new[] { "a", "z" }, target.Remove("\u00e4"));
            var selected = target;
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(target), ref target);
            Assert.Same(selected, target);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    /// <summary>Rejects observations after a destination with custom ordering has been built.</summary>
    private sealed class ObservedComparer : IComparer<string>, IEqualityComparer<string>
    {
        internal bool Reject;

        /// <summary>Compares values unless observation is excluded by the read boundary.</summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>The ordinal ordering.</returns>
        public int Compare(string? x, string? y) => Reject ? throw new InvalidOperationException("Excluded comparer observed.") : StringComparer.Ordinal.Compare(x, y);

        /// <summary>Checks equality unless observation is excluded by the read boundary.</summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>Whether the values match ordinally.</returns>
        public bool Equals(string? x, string? y) => Reject ? throw new InvalidOperationException("Excluded comparer observed.") : StringComparer.Ordinal.Equals(x, y);

        /// <summary>Hashes a value unless observation is excluded by the read boundary.</summary>
        /// <param name="obj">The value.</param>
        /// <returns>The ordinal hash.</returns>
        public int GetHashCode(string obj) => Reject ? throw new InvalidOperationException("Excluded comparer observed.") : StringComparer.Ordinal.GetHashCode(obj);
    }
}
