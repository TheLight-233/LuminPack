using System.Buffers.Binary;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects priority-queue capacity reservations and validation before pooled staging.</summary>
public sealed class PriorityQueueCapacityTest
{
    /// <summary>Preserves field framing, heap semantics and truncation boundaries in every binary profile.</summary>
    [Fact]
    public void ProfilesKeepIndependentElementAndPriorityStorage()
    {
        CheckProfile<LuminPackUtf8LengthOptions>();
        CheckProfile<LuminPackUtf8TokenOptions>();
        CheckProfile<LuminPackUtf16LengthOptions>();
        CheckProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Rejects a superficially plausible count before renting a much larger raw-node array.</summary>
    [Fact]
    public void RawPayloadIsValidatedBeforeRentingNodes()
    {
        PriorityQueue<Guid, long>? warmup = null;
        LuminPackSerializer.Deserialize(new byte[4], ref warmup);
        var record = new byte[100_004];
        BinaryPrimitives.WriteInt32LittleEndian(record, 100_000);
        PriorityQueue<Guid, long>? target = null;
        Exception? failure = null;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            LuminPackSerializer.Deserialize(record, ref target);
        }
        catch (Exception error)
        {
            failure = error;
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.NotNull(failure);
        Assert.Null(target);
        Assert.True(allocated < 64_000, $"Invalid input allocated {allocated} bytes before rejection.");
    }

    /// <summary>Keeps large owned-buffer growth and the original per-field bytes, without tuple padding.</summary>
    [Fact]
    public void LargeQueuesKeepTheirBytesAcrossBufferGrowth()
    {
        foreach (int count in new[] { 0, 1, 8, 1000, 40_000 })
        {
            var source = new PriorityQueue<byte, long>();
            foreach (int index in Enumerable.Range(0, count).Reverse()) source.Enqueue((byte)index, index * 7919L);
            byte[] expected = new byte[4 + count * 9];
            BinaryPrimitives.WriteInt32LittleEndian(expected, count);
            int offset = 4;
            foreach (var pair in source.UnorderedItems)
            {
                expected[offset++] = pair.Element;
                BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(offset), pair.Priority);
                offset += 8;
            }
            Assert.Equal(expected, LuminPackSerializer.Serialize(source));
            using var buffer = new LuminBufferWriter(true);
            LuminPackSerializer.Serialize(123, buffer);
            LuminPackSerializer.Serialize(source, buffer);
            byte[] actual = buffer.ToArrayAndReset();
            Assert.Equal(123, BinaryPrimitives.ReadInt32LittleEndian(actual));
            Assert.Equal(expected, actual.AsSpan(4).ToArray());
        }
    }

    /// <summary>Keeps exact and insufficient caller-owned spans within their original validated windows.</summary>
    [Fact]
    public void CallerOwnedSpanReservationKeepsItsGuards()
    {
        var source = new PriorityQueue<byte, long>();
        for (int index = 0; index < 37; index++) source.Enqueue((byte)index, index);
        byte[] expected = LuminPackSerializer.Serialize(source);
        var exact = Enumerable.Repeat((byte)0xAC, expected.Length + 2).ToArray();
        Assert.Equal(expected.Length, WriteInto(source, exact, expected.Length));
        Assert.Equal(expected, exact.AsSpan(1, expected.Length).ToArray());
        Assert.Equal((byte)0xAC, exact[0]);
        Assert.Equal((byte)0xAC, exact[^1]);
        var shortBuffer = Enumerable.Repeat((byte)0xAC, expected.Length + 2).ToArray();
        Assert.ThrowsAny<Exception>(() => WriteInto(source, shortBuffer, expected.Length - 1));
        Assert.Equal((byte)0xAC, shortBuffer[0]);
        Assert.All(shortBuffer.AsSpan(expected.Length).ToArray(), item => Assert.Equal((byte)0xAC, item));
    }

    /// <summary>Stages input before mutating a reused heap and retains its original comparer.</summary>
    [Fact]
    public void ReuseKeepsComparerIdentityAndFailureBoundary()
    {
        var source = new PriorityQueue<int, int>();
        source.Enqueue(19, 7);
        source.Enqueue(23, 3);
        source.Enqueue(31, 5);
        byte[] complete = LuminPackSerializer.Serialize(source);
        var comparer = Comparer<int>.Create((left, right) => right.CompareTo(left));
        var target = new PriorityQueue<int, int>(comparer);
        target.Enqueue(77, 100);
        var identity = target;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(complete.AsSpan(0, complete.Length - 1), ref target));
        Assert.Same(identity, target);
        Assert.Equal(77, target.Peek());
        Assert.Equal(complete.Length, LuminPackSerializer.Deserialize(complete, ref target));
        Assert.Same(identity, target);
        Assert.Same(comparer, target.Comparer);
        Assert.Equal(new[] { 19, 31, 23 }, Enumerable.Range(0, 3).Select(_ => target.Dequeue()).ToArray());
        Assert.Equal(4, LuminPackSerializer.Deserialize(new byte[] { 255, 255, 255, 255 }, ref target));
        Assert.Null(target);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(new byte[] { 254, 255, 255, 255 }, ref target));
        Assert.Null(target);
    }

    /// <summary>Retains string and nullable formatting, null children, and JSON queue reconstruction.</summary>
    [Fact]
    public void VariableAndNullableElementsKeepTheirOwnContracts()
    {
        var source = new PriorityQueue<string?, int>();
        source.Enqueue(null, -1);
        source.Enqueue("\u4e2d\u6587", 2);
        source.Enqueue("\u00e9clair", 1);
        byte[] binary = LuminPackSerializer.Serialize(source);
        PriorityQueue<string?, int>? copy = null;
        LuminPackSerializer.Deserialize(binary, ref copy);
        Assert.Equal(source.UnorderedItems.OrderBy(item => item.Priority), copy!.UnorderedItems.OrderBy(item => item.Priority));
        string json = LuminPackSerializer.SerializeJson(source);
        copy = null;
        LuminPackSerializer.DeserializeJson(json, ref copy);
        Assert.Equal(source.UnorderedItems.OrderBy(item => item.Priority), copy!.UnorderedItems.OrderBy(item => item.Priority));
        var nullable = new PriorityQueue<int?, int>();
        nullable.Enqueue(null, -1);
        nullable.Enqueue(7, 2);
        byte[] record = LuminPackSerializer.Serialize(nullable);
        PriorityQueue<int?, int>? restored = null;
        LuminPackSerializer.Deserialize(record, ref restored);
        Assert.Null(restored!.Dequeue());
        Assert.Equal(7, restored.Dequeue());
    }

    /// <summary>Checks complete and every truncated raw record under one compile-time profile.</summary>
    /// <typeparam name="TOptions">The complete binary profile.</typeparam>
    private static void CheckProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new PriorityQueue<Guid, long>();
        source.Enqueue(new Guid(19, 17, 29, 3, 5, 7, 11, 13, 17, 19, 23), 7);
        source.Enqueue(Guid.Empty, -11);
        byte[] record = LuminPackSerializer.Serialize<TOptions>(source);
        Assert.Equal(4 + 24 * source.Count, record.Length);
        PriorityQueue<Guid, long>? copy = null;
        byte[] withTail = record.Concat(new byte[] { 0xAC }).ToArray();
        Assert.Equal(record.Length, LuminPackSerializer.Deserialize<TOptions>(withTail, ref copy));
        Assert.Equal(Guid.Empty, copy!.Dequeue());
        Assert.Equal(source.UnorderedItems.Single(item => item.Priority == 7).Element, copy.Dequeue());
        for (int prefix = 0; prefix < record.Length; prefix++)
        {
            PriorityQueue<Guid, long>? target = null;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<TOptions>(record.AsSpan(0, prefix), ref target));
            Assert.Null(target);
        }
    }

    /// <summary>Invokes the concrete queue operation over an unaligned bounded caller span.</summary>
    /// <param name="source">The queue whose captured range is reserved.</param>
    /// <param name="storage">The guarded caller storage.</param>
    /// <param name="length">The accessible output window.</param>
    /// <returns>The actual completed record width.</returns>
    private static int WriteInto(PriorityQueue<byte, long> source, byte[] storage, int length)
    {
        var span = storage.AsSpan(1, length);
        var writer = new LuminPackWriter(ref span);
        writer.Write(source);
        return writer.Position;
    }
}
