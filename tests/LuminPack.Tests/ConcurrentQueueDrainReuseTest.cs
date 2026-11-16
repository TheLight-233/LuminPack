#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Collections.Concurrent;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that reused concurrent queues refill their retained segment instead of allocating a new growth chain.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ConcurrentQueueDrainReuseTest
{
    /// <summary>Repeated binary reuse of the same shape keeps identity, order and allocates nothing for scalars.</summary>
    [Fact]
    public void RepeatedScalarReuseAllocatesNoSegments()
    {
        foreach (int count in new[] { 8, 256, 4096 })
        {
            var source = new ConcurrentQueue<int>(Enumerable.Range(0, count));
            byte[] wire = LuminPackSerializer.Serialize(source);
            var target = new ConcurrentQueue<int>(Enumerable.Range(100, count));
            var alias = target;
            LuminPackSerializer.Deserialize(wire, ref target);
            LuminPackSerializer.Deserialize(wire, ref target);
            long before = GC.GetAllocatedBytesForCurrentThread();
            LuminPackSerializer.Deserialize(wire, ref target);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Same(alias, target);
            Assert.Equal(source.ToArray(), target.ToArray());
            Assert.Equal(0, allocated);
        }
    }

    /// <summary>String and JSON reuse keep order, identity and an earlier snapshot.</summary>
    [Fact]
    public void StringReuseKeepsOrderAndSnapshots()
    {
        var source = new ConcurrentQueue<string>(Enumerable.Range(0, 300).Select(index => "item-" + index));
        var target = new ConcurrentQueue<string>(new[] { "old", "older" });
        var snapshot = target.ToArray();
        using var enumerator = target.GetEnumerator();
        var alias = target;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);
        Assert.Same(alias, target);
        Assert.Equal(source.ToArray(), target.ToArray());
        Assert.Equal(new[] { "old", "older" }, snapshot);
        var json = new ConcurrentQueue<string>(new[] { "old" });
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(source), ref json);
        Assert.Equal(source.ToArray(), json.ToArray());
    }
}
#endif
