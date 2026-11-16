#if LUMINPACK_NEXT_GENERATOR
using System.Collections.Concurrent;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks raw binary queue reuse across the replacement segment capacity boundaries.</summary>
public sealed class ConcurrentQueueReuseTest
{
    /// <summary>Preserves the existing queue and captured snapshot after validating the incoming payload.</summary>
    /// <param name="count">The incoming element count.</param>
    [Theory]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void QueueReusePreservesIdentityAndExistingSnapshot(int count)
    {
        int[] oldItems = [19, 23, 47];
        int[] incoming = Enumerable.Range(0, count).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(new ConcurrentQueue<int>(incoming));
        var alias = new ConcurrentQueue<int>(oldItems);
        var target = alias;
        using var snapshot = alias.GetEnumerator();

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Same(alias, target);
        Assert.Equal(oldItems, alias.ToArray());

        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(alias, target);
        Assert.Equal(incoming, alias.ToArray());
        foreach (int item in oldItems)
        {
            Assert.True(snapshot.MoveNext());
            Assert.Equal(item, snapshot.Current);
        }
        Assert.False(snapshot.MoveNext());
    }
}
#endif
