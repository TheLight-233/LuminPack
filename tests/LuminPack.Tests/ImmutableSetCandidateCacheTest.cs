using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

/// <summary>Protects weak immutable candidates without skipping complete input decoding.</summary>
/// <remarks>Each closed set type has one process-wide candidate, so these identity checks run without parallel readers.</remarks>
[Collection(nameof(LuminPackUnitTest.RuntimeStateCollection))]
public sealed class ImmutableSetCandidateCacheTest
{
    /// <summary>Shares equal immutable results even when the caller starts with an empty destination.</summary>
    [Fact]
    public void FreshDestinationsCanShareOnlyValidatedImmutableResults()
    {
        byte[] sortedBytes = LuminPackSerializer.Serialize(ImmutableSortedSet.Create(19001, 19003, 19007));
        ImmutableSortedSet<int> a = null!;
        ImmutableSortedSet<int> b = null!;
        LuminPackSerializer.Deserialize(sortedBytes, ref a);
        LuminPackSerializer.Deserialize(sortedBytes, ref b);
        Assert.Same(a, b);
        byte[] hashBytes = LuminPackSerializer.Serialize(ImmutableHashSet.Create("weak-alpha", "weak-beta"));
        ImmutableHashSet<string> x = null!;
        ImmutableHashSet<string> y = null!;
        LuminPackSerializer.Deserialize(hashBytes, ref x);
        LuminPackSerializer.Deserialize(hashBytes, ref y);
        Assert.Same(x, y);
        Assert.DoesNotContain(19009, a);
        Assert.Contains(19009, b.Add(19009));
        Assert.DoesNotContain("weak-gamma", x);
        Assert.Contains("weak-gamma", y.Add("weak-gamma"));
    }

    /// <summary>Validates every profile before sharing immutable contents originating in another profile.</summary>
    [Fact]
    public void CrossProfileCandidatesStillDecodeTheCompleteRecord()
    {
        var source = ImmutableSortedSet.Create("", "cross-alpha", "\u4e2d\u6587-\ud83d\ude03");
        ImmutableSortedSet<string> first = null!;
        ImmutableSortedSet<string> second = null!;
        ImmutableSortedSet<string> third = null!;
        ImmutableSortedSet<string> fourth = null!;
        LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(source), ref first);
        LuminPackSerializer.Deserialize<LuminPackUtf8TokenOptions>(LuminPackSerializer.Serialize<LuminPackUtf8TokenOptions>(source), ref second);
        LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(source), ref third);
        LuminPackSerializer.Deserialize<LuminPackUtf16TokenOptions>(LuminPackSerializer.Serialize<LuminPackUtf16TokenOptions>(source), ref fourth);
        Assert.Same(first, second);
        Assert.Same(first, third);
        Assert.Same(first, fourth);
        Assert.Equal(source, fourth);
    }

    /// <summary>Reads every changed byte and keeps prior immutable contents independent.</summary>
    [Fact]
    public void ChangedInputCannotHitAnAddressOrCountShortcut()
    {
        byte[] bytes = LuminPackSerializer.Serialize(ImmutableSortedSet.Create(20101, 20103, 20107));
        ImmutableSortedSet<int> first = null!;
        LuminPackSerializer.Deserialize(bytes, ref first);
        bytes[4] ^= 1;
        ImmutableSortedSet<int> changed = null!;
        LuminPackSerializer.Deserialize(bytes, ref changed);
        Assert.NotSame(first, changed);
        Assert.Contains(20101, first);
        Assert.DoesNotContain(20101, changed);
        Assert.Contains(20100, changed);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(changed));
        byte[] alternate = LuminPackSerializer.Serialize(ImmutableSortedSet.Create(20201, 20203, 20207));
        ImmutableSortedSet<int> next = null!;
        LuminPackSerializer.Deserialize(alternate, ref next);
        Assert.NotSame(changed, next);
        Assert.Equal(new[] { 20201, 20203, 20207 }, next);
        Assert.Contains(20100, changed);
    }

    /// <summary>Never publishes a cached candidate or replaces its slot after an incomplete record.</summary>
    [Fact]
    public void FailedFreshReadStaysNullAndKeepsTheLastCompleteCandidate()
    {
        byte[] bytes = LuminPackSerializer.Serialize(ImmutableHashSet.Create(30101, 30103, 30107));
        ImmutableHashSet<int> first = null!;
        LuminPackSerializer.Deserialize(bytes, ref first);
        for (int prefix = 0; prefix < bytes.Length; prefix++)
        {
            ImmutableHashSet<int> failed = null!;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, prefix), ref failed));
            Assert.Null(failed);
        }
        ImmutableHashSet<int> again = null!;
        LuminPackSerializer.Deserialize(bytes, ref again);
        Assert.Same(first, again);
        byte[] strings = LuminPackSerializer.Serialize(ImmutableSortedSet.Create("failure-alpha", "failure-beta"));
        ImmutableSortedSet<string> text = null!;
        LuminPackSerializer.Deserialize(strings, ref text);
        ImmutableSortedSet<string> failedText = null!;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(strings.AsSpan(0, strings.Length - 1), ref failedText));
        Assert.Null(failedText);
        ImmutableSortedSet<string> textAgain = null!;
        LuminPackSerializer.Deserialize(strings, ref textAgain);
        Assert.Same(text, textAgain);
    }

    /// <summary>Allows the collector to reclaim a last candidate once every caller has released it.</summary>
    [Fact]
    public void WeakCandidateDoesNotRetainTheResult()
    {
        WeakReference<ImmutableSortedSet<int>> weak = CreateUnrootedResult();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
        Assert.False(weak.TryGetTarget(out _));
    }

    /// <summary>Creates a complete result without leaving a caller-owned strong reference on the test stack.</summary>
    /// <returns>A weak observation independent of the generated candidate slot.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<ImmutableSortedSet<int>> CreateUnrootedResult()
    {
        byte[] bytes = LuminPackSerializer.Serialize(ImmutableSortedSet.Create(4493321, 4493323, 4493327));
        ImmutableSortedSet<int> value = null!;
        LuminPackSerializer.Deserialize(bytes, ref value);
        return new WeakReference<ImmutableSortedSet<int>>(value);
    }

    /// <summary>Revalidates cached sorted strings against the current culture before sharing a tree.</summary>
    [Fact]
    public void FreshCultureChangeRebuildsCachedOrdering()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            byte[] bytes = LuminPackSerializer.Serialize(ImmutableSortedSet.Create("a", "\u00e4", "z"));
            ImmutableSortedSet<string> first = null!;
            LuminPackSerializer.Deserialize(bytes, ref first);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            ImmutableSortedSet<string> next = null!;
            LuminPackSerializer.Deserialize(bytes, ref next);
            Assert.NotSame(first, next);
            Assert.Equal(new[] { "a", "z", "\u00e4" }, next);
            Assert.True(next.Contains("\u00e4"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    /// <summary>Shares no mutable elements when an immutable container has an ordinary object formatter.</summary>
    [Fact]
    public void MutableElementContractsKeepFreshConstruction()
    {
        byte[] bytes = LuminPackSerializer.Serialize(ImmutableSortedSet.Create(new MutableElement { Number = 40101 }, new MutableElement { Number = 40103 }));
        ImmutableSortedSet<MutableElement> a = null!;
        ImmutableSortedSet<MutableElement> b = null!;
        LuminPackSerializer.Deserialize(bytes, ref a);
        LuminPackSerializer.Deserialize(bytes, ref b);
        Assert.NotSame(a, b);
        Assert.NotSame(a.First(), b.First());
        Assert.Equal(a.Select(item => item.Number), b.Select(item => item.Number));
    }

    /// <summary>Keeps complete values independent while multiple readers replace the same weak slot.</summary>
    /// <returns>A task completing after all concurrent readers finish.</returns>
    [Fact]
    public async Task ConcurrentCandidateReplacementKeepsEveryRecord()
    {
        var first = Enumerable.Range(50100, 37).ToImmutableHashSet();
        var second = Enumerable.Range(60100, 37).ToImmutableHashSet();
        byte[] a = LuminPackSerializer.Serialize(first);
        byte[] b = LuminPackSerializer.Serialize(second);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            for (int iteration = 0; iteration < 100; iteration++)
            {
                bool useFirst = ((iteration + worker) & 1) == 0;
                ImmutableHashSet<int> value = null!;
                LuminPackSerializer.Deserialize(useFirst ? a : b, ref value);
                Assert.True((useFirst ? first : second).SetEquals(value));
            }
        })));
    }

    /// <summary>Represents an ordinary mutable object excluded from candidate sharing.</summary>
    [LuminPackable]
    public sealed class MutableElement : IComparable<MutableElement>
    {
        public int Number;

        /// <summary>Orders decoded objects by their numeric field.</summary>
        /// <param name="other">The other object.</param>
        /// <returns>The numeric ordering.</returns>
        public int CompareTo(MutableElement? other) => other is null ? 1 : Number.CompareTo(other.Number);
    }
}
