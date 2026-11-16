#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks linked-list reads that link nodes while decoding and publish them once.</summary>
public sealed class LinkedListAppendReadTest
{
    /// <summary>Fresh reads build the same circular list as <c>AddLast</c>; reused reads keep the existing nodes in order.</summary>
    [Fact]
    public void StringListsMatchAddLast()
    {
        foreach (int count in new[] { 1, 2, 3, 8, 257 })
        {
            var source = new LinkedList<string?>(Enumerable.Range(0, count).Select(index => index % 7 == 3 ? null : "item-" + index));
            byte[] wire = LuminPackSerializer.Serialize(source);
            LinkedList<string?>? fresh = null;
            LuminPackSerializer.Deserialize(wire, ref fresh);
            Check(source, fresh!);
            var reused = new LinkedList<string?>(new[] { "old", "older" });
            var alias = reused;
            var stale = reused.First!;
            LuminPackSerializer.Deserialize(wire, ref reused);
            Assert.Same(alias, reused);
            Assert.Same(stale, reused.First);
            Assert.Same(reused, stale.List);
            Check(source, reused);
            reused.AddFirst("head");
            reused.RemoveLast();
            Assert.Equal(count, reused.Count);
            Assert.Equal("head", reused.First!.Value);
        }
    }

    /// <summary>A truncated record leaves a fresh-or-cleared list empty and a reused list with its completed prefix.</summary>
    [Fact]
    public void TruncatedReadsKeepAConsistentList()
    {
        var source = new LinkedList<string?>(new[] { "a", "b", "c" });
        byte[] wire = LuminPackSerializer.Serialize(source);
        var empty = new LinkedList<string?>();
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref empty));
        Assert.Empty(empty);

        var target = new LinkedList<string?>(new[] { "old" });
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        Assert.Equal(new[] { "a", "b" }, target);
        target.AddLast("x");
        Assert.Equal(new[] { "a", "b", "x" }, target);
        Assert.Equal(new[] { "x", "b", "a" }, target.Reverse());
    }

    /// <summary>Reuses object elements and nodes in place, growing and shrinking the list.</summary>
    [Fact]
    public void ReusedListsKeepNodesAndElements()
    {
        foreach (int existing in new[] { 1, 3, 6 })
        {
            foreach (int incoming in new[] { 1, 3, 6 })
            {
                var target = new LinkedList<LinkedListReuseItem?>(Enumerable.Range(0, existing).Select(index => new LinkedListReuseItem { Id = -index }));
                var nodes = new List<LinkedListNode<LinkedListReuseItem?>>();
                for (var node = target.First; node is not null; node = node.Next)
                    nodes.Add(node);
                var items = target.ToArray();
                var source = new LinkedList<LinkedListReuseItem?>(Enumerable.Range(0, incoming).Select(index => index == 1 ? null : new LinkedListReuseItem { Id = index }));
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);
                Assert.Equal(source.Select(item => item?.Id), target.Select(item => item?.Id));
                int shared = Math.Min(existing, incoming);
                var current = target.First;
                for (int index = 0; index < shared; index++, current = current!.Next)
                {
                    Assert.Same(nodes[index], current);
                    if (index != 1)
                        Assert.Same(items[index], current!.Value);
                }
                for (int index = shared; index < existing; index++)
                    Assert.Null(nodes[index].List);
                Assert.Equal(target.Reverse().Select(item => item?.Id), source.Reverse().Select(item => item?.Id));
            }
        }
    }

    /// <summary>Compares content, links, ownership and both traversal directions.</summary>
    /// <param name="expected">The source list.</param>
    /// <param name="actual">The read list.</param>
    private static void Check(LinkedList<string?> expected, LinkedList<string?> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Reverse(), actual.Reverse());
        for (var node = actual.First; node is not null; node = node.Next)
            Assert.Same(actual, node.List);
        Assert.Null(actual.First!.Previous);
        Assert.Null(actual.Last!.Next);
    }
}
/// <summary>An ordinary element reused in place by linked-list reads.</summary>
[LuminPackable]
public partial class LinkedListReuseItem
{
    /// <summary>The identifier.</summary>
    public int Id;
}
#endif
