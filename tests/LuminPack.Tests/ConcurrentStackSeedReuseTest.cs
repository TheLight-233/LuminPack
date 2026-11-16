#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Collections.Concurrent;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary concurrent stack reuse that reads object elements over the existing items before pushing them back.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ConcurrentStackSeedReuseTest
{
    /// <summary>Existing objects line up from the bottom across count changes and keep their identity.</summary>
    [Fact]
    public void ObjectsAreReusedFromTheBottom()
    {
        foreach (int existing in new[] { 0, 2, 5 })
        {
            foreach (int incoming in new[] { 1, 3, 5, 9 })
            {
                var source = new ConcurrentStack<ConcurrentStackSeedItem>(
                    Enumerable.Range(0, incoming).Select(index => new ConcurrentStackSeedItem { Id = index }));
                var target = new ConcurrentStack<ConcurrentStackSeedItem>(
                    Enumerable.Range(0, existing).Select(index => new ConcurrentStackSeedItem { Id = -index - 1 }));
                var oldBottomFirst = target.Reverse().ToArray();
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);
                Assert.Equal(source.Select(item => item.Id), target!.Select(item => item.Id));
                var newBottomFirst = target.Reverse().ToArray();
                for (int index = 0; index < Math.Min(existing, incoming); index++)
                    Assert.Same(oldBottomFirst[index], newBottomFirst[index]);
                target.Push(new ConcurrentStackSeedItem { Id = 99 });
                Assert.True(target.TryPop(out var pushed) && pushed.Id == 99);
            }
        }
    }

    /// <summary>A truncated record leaves an empty usable stack.</summary>
    [Fact]
    public void TruncatedRecordLeavesAnEmptyStack()
    {
        var source = new ConcurrentStack<ConcurrentStackSeedItem>(Enumerable.Range(0, 3).Select(index => new ConcurrentStackSeedItem { Id = index }));
        byte[] wire = LuminPackSerializer.Serialize(source);
        var target = new ConcurrentStack<ConcurrentStackSeedItem>(new[] { new ConcurrentStackSeedItem(), new ConcurrentStackSeedItem() });
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        Assert.Empty(target);
        target.Push(new ConcurrentStackSeedItem { Id = 7 });
        Assert.Equal(7, Assert.Single(target).Id);
    }
}

/// <summary>An ordinary element reused in place.</summary>
[LuminPackable]
public partial class ConcurrentStackSeedItem
{
    /// <summary>The identifier.</summary>
    public int Id;
}
#endif
