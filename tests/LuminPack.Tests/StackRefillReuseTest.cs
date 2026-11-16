#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary stack reuse that reads string and object elements in place over the previous items.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class StackRefillReuseTest
{
    /// <summary>Keeps the stack and string references and allocates nothing for repeated identical input.</summary>
    [Fact]
    public void RepeatedStringReuseKeepsIdentityWithoutAllocation()
    {
        var source = new Stack<string>(Enumerable.Range(0, 300).Select(index => "item-" + index));
        byte[] wire = LuminPackSerializer.Serialize(source);
        var target = new Stack<string>();
        var alias = target;
        LuminPackSerializer.Deserialize(wire, ref target);
        string top = target.Peek();
        LuminPackSerializer.Deserialize(wire, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        LuminPackSerializer.Deserialize(wire, ref target);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(alias, target);
        Assert.Same(top, target.Peek());
        Assert.Equal(source, target);
        Assert.Equal(0, allocated);
    }

    /// <summary>Grows and shrinks the backing array, invalidates enumerators and leaves a usable stack.</summary>
    [Fact]
    public void GrowingAndShrinkingTargetsKeepStackOrder()
    {
        foreach (int existing in new[] { 0, 2, 8 })
        {
            foreach (int incoming in new[] { 1, 3, 8, 20 })
            {
                var target = new Stack<string>(Enumerable.Range(0, existing).Select(index => "old-" + index));
                var enumerator = target.GetEnumerator();
                var source = new Stack<string>(Enumerable.Range(0, incoming).Select(index => "new-" + index));
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);
                Assert.Equal(source, target);
                if (existing != 0)
                    Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
                target.Push("top");
                Assert.Equal("top", target.Pop());
                Assert.Equal("new-" + (incoming - 1), target.Pop());
                Assert.Equal(incoming - 1, target.Count);
            }
        }
    }

    /// <summary>Reuses existing object elements and leaves a reused stack empty when the record is truncated.</summary>
    [Fact]
    public void ObjectElementsAreReusedAndTruncationLeavesAnEmptyStack()
    {
        var source = new Stack<StackRefillItem>(Enumerable.Range(0, 4).Select(index => new StackRefillItem { Id = index }));
        byte[] wire = LuminPackSerializer.Serialize(source);
        var target = new Stack<StackRefillItem>(Enumerable.Range(0, 4).Select(index => new StackRefillItem { Id = -index }));
        var items = target.ToArray();
        LuminPackSerializer.Deserialize(wire, ref target);
        Assert.Equal(new[] { 3, 2, 1, 0 }, target.Select(item => item.Id));
        Assert.Equal(items, target.ToArray());

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        Assert.Empty(target);
        target.Push(new StackRefillItem { Id = 9 });
        Assert.Equal(9, target.Single().Id);
    }
}

/// <summary>An ordinary element reused in place.</summary>
[LuminPackable]
public partial class StackRefillItem
{
    /// <summary>The identifier.</summary>
    public int Id;
}
#endif
