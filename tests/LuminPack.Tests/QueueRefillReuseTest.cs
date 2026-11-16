#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary queue reuse that reads counted elements in place over the previous items.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class QueueRefillReuseTest
{
    /// <summary>Keeps the queue and string references and allocates nothing for repeated identical input.</summary>
    [Fact]
    public void RepeatedStringReuseKeepsIdentityWithoutAllocation()
    {
        var source = new Queue<string>(Enumerable.Range(0, 300).Select(index => "item-" + index));
        byte[] wire = LuminPackSerializer.Serialize(source);
        var target = new Queue<string>();
        var alias = target;
        LuminPackSerializer.Deserialize(wire, ref target);
        string first = target.Peek();
        LuminPackSerializer.Deserialize(wire, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        LuminPackSerializer.Deserialize(wire, ref target);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(alias, target);
        Assert.Same(first, target.Peek());
        Assert.Equal(source, target);
        Assert.Equal(0, allocated);
    }

    /// <summary>Linearizes offset and wrapped rings, grows and shrinks, and leaves a usable queue.</summary>
    [Fact]
    public void OffsetAndWrappedRingsRefillInOrder()
    {
        foreach (int incoming in new[] { 1, 3, 5, 8, 20 })
        {
            foreach (int shift in new[] { 0, 2, 5 })
            {
                var target = new Queue<string>(8);
                for (int index = 0; index < 8; index++)
                    target.Enqueue("old-" + index);
                for (int index = 0; index < shift; index++)
                {
                    target.Dequeue();
                    target.Enqueue("wrap-" + index);
                }
                var enumerator = target.GetEnumerator();
                var source = new Queue<string>(Enumerable.Range(0, incoming).Select(index => "new-" + index));
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);
                Assert.Equal(source, target);
                Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
                target.Enqueue("tail");
                Assert.Equal("new-0", target.Dequeue());
                Assert.Equal(source.Skip(1).Append("tail"), target);
            }
        }
    }

    /// <summary>Reuses existing object elements and clears a reused queue when the record is truncated.</summary>
    [Fact]
    public void ObjectElementsAreReusedAndTruncationLeavesAnEmptyQueue()
    {
        var source = new Queue<QueueRefillItem>(Enumerable.Range(0, 4).Select(index => new QueueRefillItem { Id = index }));
        byte[] wire = LuminPackSerializer.Serialize(source);
        var target = new Queue<QueueRefillItem>(Enumerable.Range(0, 4).Select(index => new QueueRefillItem { Id = -index }));
        var items = target.ToArray();
        LuminPackSerializer.Deserialize(wire, ref target);
        Assert.Equal(new[] { 0, 1, 2, 3 }, target.Select(item => item.Id));
        Assert.Equal(items, target.ToArray());

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        Assert.Empty(target);
        target.Enqueue(new QueueRefillItem { Id = 9 });
        Assert.Equal(9, target.Single().Id);
    }
}

/// <summary>An ordinary element reused in place.</summary>
[LuminPackable]
public partial class QueueRefillItem
{
    /// <summary>The identifier.</summary>
    public int Id;
}
#endif
