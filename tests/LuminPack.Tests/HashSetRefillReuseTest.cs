#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that binary set reads refill an existing default-comparer hash set in place.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class HashSetRefillReuseTest
{
    /// <summary>Reuses the exact set, keeps its arrays and replaces the contents.</summary>
    [Fact]
    public void ScalarSetsRefillWithoutAllocation()
    {
        foreach (int count in new[] { 1, 8, 256, 4096 })
        {
            var source = new HashSet<int>(Enumerable.Range(0, count).Select(index => index * 7919));
            byte[] wire = LuminPackSerializer.Serialize(source);
            var target = new HashSet<int>(Enumerable.Range(-count, count));
            var alias = target;
            LuminPackSerializer.Deserialize(wire, ref target);
            LuminPackSerializer.Deserialize(wire, ref target);
            long before = GC.GetAllocatedBytesForCurrentThread();
            LuminPackSerializer.Deserialize(wire, ref target);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Same(alias, target);
            Assert.True(source.SetEquals(target));
            Assert.Equal(count, target.Count);
            Assert.Equal(0, allocated);
        }
    }

    /// <summary>Grows a smaller target, refills string and interface targets, and replaces sets with a custom comparer.</summary>
    [Fact]
    public void StringAndInterfaceTargetsKeepSetSemantics()
    {
        var source = new HashSet<string>(Enumerable.Range(0, 300).Select(index => "Item-" + index));
        byte[] wire = LuminPackSerializer.Serialize(source);

        var small = new HashSet<string> { "old" };
        var smallAlias = small;
        LuminPackSerializer.Deserialize(wire, ref small);
        Assert.Same(smallAlias, small);
        Assert.True(source.SetEquals(small));
        Assert.Contains("Item-299", small);
        Assert.DoesNotContain("old", small);

        ISet<string> asInterface = new HashSet<string> { "old" };
        var interfaceAlias = asInterface;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize((ISet<string>)source), ref asInterface);
        Assert.Same(interfaceAlias, asInterface);
        Assert.True(source.SetEquals(asInterface));

        var custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "old" };
        var customAlias = custom;
        var mixed = new HashSet<string> { "a", "A" };
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(mixed), ref custom);
        Assert.NotSame(customAlias, custom);
        Assert.Equal(2, custom.Count);
        Assert.Same(EqualityComparer<string>.Default, custom.Comparer);
    }
}
#endif
