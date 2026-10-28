using System.Collections.Concurrent;
using System.Collections.Immutable;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that a forged collection count is rejected before any allocation sized from it.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class ForgedCountAllocationTest
{
    /// <summary>The largest allocation a rejected record may cause on the reading thread.</summary>
    private const long AllowedBytes = 1L << 20;

    /// <summary>Checks sequence collections of managed elements.</summary>
    [Fact]
    public void ForgedSequenceCountsAreRejectedBeforeAllocating()
    {
        Reject<List<string>>(LuminPackSerializer.Serialize(new List<string> { "a" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<string[]>(LuminPackSerializer.Serialize(new[] { "a" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<HashSet<string>>(LuminPackSerializer.Serialize(new HashSet<string> { "a" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<Queue<string>>(LuminPackSerializer.Serialize(new Queue<string>(new[] { "a" })), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<Stack<string>>(LuminPackSerializer.Serialize(new Stack<string>(new[] { "a" })), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<LinkedList<string>>(LuminPackSerializer.Serialize(new LinkedList<string>(new[] { "a" })), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<ImmutableList<string>>(LuminPackSerializer.Serialize(ImmutableList.Create("a")), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<ImmutableArray<string>>(LuminPackSerializer.Serialize(ImmutableArray.Create("a")), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<Memory<string>>(LuminPackSerializer.Serialize(new Memory<string>(new[] { "a" })), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<ConcurrentQueue<string>>(LuminPackSerializer.Serialize(new ConcurrentQueue<string>(new[] { "a" })), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<ConcurrentBag<string>>(LuminPackSerializer.Serialize(new ConcurrentBag<string>(new[] { "a" })), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
    }

    /// <summary>Checks keyed collections, whose presized storage is the largest.</summary>
    [Fact]
    public void ForgedDictionaryCountsAreRejectedBeforeAllocating()
    {
        Reject<Dictionary<string, string>>(LuminPackSerializer.Serialize(new Dictionary<string, string> { ["a"] = "b" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<Dictionary<int, string>>(LuminPackSerializer.Serialize(new Dictionary<int, string> { [1] = "b" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<IDictionary<int, string>>(LuminPackSerializer.Serialize((IDictionary<int, string>)new Dictionary<int, string> { [1] = "b" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<SortedList<int, string>>(LuminPackSerializer.Serialize(new SortedList<int, string> { [1] = "b" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<SortedDictionary<int, string>>(LuminPackSerializer.Serialize(new SortedDictionary<int, string> { [1] = "b" }), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Reject<ConcurrentDictionary<int, string>>(LuminPackSerializer.Serialize(new ConcurrentDictionary<int, string>(new Dictionary<int, string> { [1] = "b" })), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        var queue = new PriorityQueue<string, int>();
        queue.Enqueue("a", 1);
        Reject<PriorityQueue<string, int>>(LuminPackSerializer.Serialize(queue), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
    }

    /// <summary>Checks that a multidimensional raw array validates its complete element payload before allocating.</summary>
    [Fact]
    public void RawMultidimensionalPayloadIsValidatedBeforeAllocating()
    {
        byte[] valid = LuminPackSerializer.Serialize(new long[2, 2] { { 1, 2 }, { 3, 4 } });
        // Keep the header and dimensions but drop most of the element block: the count still fits one byte per element.
        byte[] truncated = valid.AsSpan(0, valid.Length - 24).ToArray();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.ThrowsAny<Exception>(() => { long[,]? target = null; LuminPackSerializer.Deserialize(truncated, ref target); });
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < AllowedBytes);
    }

    /// <summary>Forges the first count of a record and asserts a cheap rejection.</summary>
    /// <typeparam name="T">The collection type.</typeparam>
    /// <param name="valid">A valid record whose first four bytes are the collection count.</param>
    /// <param name="read">The concrete reader of <typeparamref name="T"/>.</param>
    private static void Reject<T>(byte[] valid, ReadInto<T> read)
    {
        foreach (int count in new[] { 50_000_000, int.MaxValue })
        {
            byte[] forged = (byte[])valid.Clone();
            BitConverter.GetBytes(count).CopyTo(forged, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.ThrowsAny<Exception>(() => { T target = default!; read(forged, ref target); });
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < AllowedBytes, typeof(T).Name + " allocated " + allocated + " bytes for a forged count of " + count + ".");
        }
    }
}
