using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects exclusive empty-builder ownership and published immutable graphs.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ImmutableBuilderReuseTest
{
    /// <summary>Returns all builder families without allocating another shell after warmup.</summary>
    [Fact]
    public void WarmEmptyRentalsDoNotAllocate()
    {
        for (int i = 0; i < 32; i++) RentEveryFamily();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) RentEveryFamily();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Clears defaults and separates reentrant rentals without mutating frozen results.</summary>
    [Fact]
    public void DictionaryOwnershipAndDefaultsSurviveReuse()
    {
        var builder = LuminImmutableBuilders.RentDictionary<string, object>();
        var payload = new object();
        builder.Add("first", payload);
        var result = builder.ToImmutable();
        LuminImmutableBuilders.ReturnDictionary(builder);
        var reused = LuminImmutableBuilders.RentDictionary<string, object>();
        Assert.Same(builder, reused);
        Assert.Empty(reused);
        reused.KeyComparer = StringComparer.OrdinalIgnoreCase;
        reused.Add("second", payload);
        var nested = LuminImmutableBuilders.RentDictionary<string, object>();
        Assert.NotSame(reused, nested);
        Assert.Empty(nested);
        LuminImmutableBuilders.ReturnDictionary(nested);
        LuminImmutableBuilders.ReturnDictionary(reused);
        reused = LuminImmutableBuilders.RentDictionary<string, object>();
        Assert.Same(EqualityComparer<string>.Default, reused.KeyComparer);
        Assert.Same(EqualityComparer<object>.Default, reused.ValueComparer);
        Assert.Empty(reused);
        Assert.Single(result);
        Assert.Same(payload, result["first"]);
        LuminImmutableBuilders.ReturnDictionary(reused);
    }

    /// <summary>Leaves already frozen collection roots independent from the next rental.</summary>
    [Fact]
    public void OtherFamiliesKeepPublishedResults()
    {
        var sorted = LuminImmutableBuilders.RentSortedDictionary<int, int>();
        sorted.Add(1, 1);
        var sortedResult = sorted.ToImmutable();
        LuminImmutableBuilders.ReturnSortedDictionary(sorted);
        sorted = LuminImmutableBuilders.RentSortedDictionary<int, int>();
        sorted.Add(2, 2);
        Assert.False(sortedResult.ContainsKey(2));
        LuminImmutableBuilders.ReturnSortedDictionary(sorted);
        var hash = LuminImmutableBuilders.RentHashSet<int>();
        hash.Add(1);
        var hashResult = hash.ToImmutable();
        LuminImmutableBuilders.ReturnHashSet(hash);
        hash = LuminImmutableBuilders.RentHashSet<int>();
        hash.Add(2);
        Assert.Equal(new[] { 1 }, hashResult);
        LuminImmutableBuilders.ReturnHashSet(hash);
        var set = LuminImmutableBuilders.RentSortedSet<int>();
        set.Add(1);
        var setResult = set.ToImmutable();
        LuminImmutableBuilders.ReturnSortedSet(set);
        set = LuminImmutableBuilders.RentSortedSet<int>();
        set.Add(2);
        Assert.Equal(new[] { 1 }, setResult);
        LuminImmutableBuilders.ReturnSortedSet(set);
        var list = LuminImmutableBuilders.RentList<int>();
        list.Add(1);
        var listResult = list.ToImmutable();
        LuminImmutableBuilders.ReturnList(list);
        list = LuminImmutableBuilders.RentList<int>();
        list.Add(2);
        Assert.Equal(new[] { 1 }, listResult);
        LuminImmutableBuilders.ReturnList(list);
    }

    /// <summary>Drops the builder's element and immutable-result references before retention.</summary>
    [Fact]
    public void ReturnedBuilderDoesNotKeepPayloadAlive()
    {
        var weak = ReturnPayload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.IsAlive);
    }

    /// <summary>Exercises each exact empty ownership pair without allocating a delegate.</summary>
    private static void RentEveryFamily()
    {
        LuminImmutableBuilders.ReturnDictionary(LuminImmutableBuilders.RentDictionary<int, int>());
        LuminImmutableBuilders.ReturnSortedDictionary(LuminImmutableBuilders.RentSortedDictionary<int, int>());
        LuminImmutableBuilders.ReturnHashSet(LuminImmutableBuilders.RentHashSet<int>());
        LuminImmutableBuilders.ReturnSortedSet(LuminImmutableBuilders.RentSortedSet<int>());
        LuminImmutableBuilders.ReturnList(LuminImmutableBuilders.RentList<int>());
    }

    /// <summary>Releases all strong references on a separate stack frame.</summary>
    /// <returns>The payload observation retained by the test.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ReturnPayload()
    {
        var payload = new object();
        var weak = new WeakReference(payload);
        var builder = LuminImmutableBuilders.RentDictionary<int, object>();
        builder.Add(1, payload);
        builder.ToImmutable();
        LuminImmutableBuilders.ReturnDictionary(builder);
        return weak;
    }
}
