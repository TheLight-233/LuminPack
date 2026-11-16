#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks linked-list reads that link nodes while decoding and publish them once.</summary>
public sealed class LinkedListAppendReadTest
{
    /// <summary>Fresh and reused reads build the same circular list as <c>AddLast</c>.</summary>
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
            Assert.Null(stale.List);
            Check(source, reused);
            reused.AddFirst("head");
            reused.RemoveLast();
            Assert.Equal(count, reused.Count);
            Assert.Equal("head", reused.First!.Value);
        }
    }

    /// <summary>A truncated record leaves a reused list empty and usable.</summary>
    [Fact]
    public void TruncatedReadLeavesTheListEmpty()
    {
        var source = new LinkedList<string?>(new[] { "a", "b", "c" });
        byte[] wire = LuminPackSerializer.Serialize(source);
        var target = new LinkedList<string?>(new[] { "old" });
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        Assert.Empty(target);
        target.AddLast("x");
        Assert.Equal(new[] { "x" }, target);
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
#endif
