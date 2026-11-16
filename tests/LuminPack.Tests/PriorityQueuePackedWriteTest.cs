#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Buffers.Binary;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary heap order and output boundaries for packed priority-queue writes.</summary>
public sealed class PriorityQueuePackedWriteTest
{
    /// <summary>Keeps the same heap bytes through allocating and caller-owned writer overloads.</summary>
    [Fact]
    public void IntegerNodesKeepTheFrameworkOrder()
    {
        int comparisons = 0;
        var comparer = Comparer<int>.Create((left, right) => { comparisons++; return right.CompareTo(left); });
        var source = new PriorityQueue<int, int>(comparer);
        foreach (int count in new[] { 0, 1, 9, 256, 4096 })
        {
            source.Clear();
            for (int index = 0; index < count; index++)
                source.Enqueue(unchecked(index * 486187739), index % 7);
            byte[] expected = new byte[sizeof(int) + count * (sizeof(int) * 2)];
            BinaryPrimitives.WriteInt32LittleEndian(expected, count);
            int at = sizeof(int);
            foreach (var (element, priority) in source.UnorderedItems)
            {
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(at), element);
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(at + sizeof(int)), priority);
                at += sizeof(int) * 2;
            }

            int before = comparisons;
            Assert.Equal(expected, LuminPackSerializer.Serialize(source));
            Assert.Equal(expected, LuminPackSerializer.Serialize<PriorityQueue<int, int>, LuminPackUtf8TokenOptions>(source));
            byte[] buffer = new byte[expected.Length + 2];
            buffer[0] = 0xA5;
            buffer[^1] = 0x5A;
            Span<byte> payload = buffer.AsSpan(1, expected.Length);
            var writer = new LuminPackWriter(ref payload);
            LuminPackSerializer.Serialize(ref writer, source);
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, payload.ToArray());
            Assert.Equal(0xA5, buffer[0]);
            Assert.Equal(0x5A, buffer[^1]);
            Assert.Equal(before, comparisons);
        }
    }

    /// <summary>Declines a short fixed destination without writing the header or advancing the cursor.</summary>
    [Fact]
    public void ShortDestinationKeepsTheOriginalWindow()
    {
        var source = new PriorityQueue<long, ulong>();
        source.Enqueue(long.MinValue, ulong.MaxValue);
        Span<byte> payload = stackalloc byte[sizeof(int) + sizeof(long) - 1];
        payload.Fill(0xA5);
        var writer = new LuminPackWriter(ref payload);

        Assert.False(LuminPackCollectionAccess.TryWritePriorityQueueStorage(ref writer, source));
        Assert.Equal(0, writer.Position);
        Assert.Equal(-1, payload.IndexOfAnyExcept((byte)0xA5));
        byte[] expected = new byte[sizeof(int) + sizeof(long) + sizeof(ulong)];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);
        BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(sizeof(int)), long.MinValue);
        BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(sizeof(int) + sizeof(long)), ulong.MaxValue);
        Assert.Equal(expected, LuminPackSerializer.Serialize(source));
    }

    /// <summary>Retains the scalar wire layout for a padded tuple and emits the original null marker.</summary>
    [Fact]
    public void PaddedPairsUseMemberWidths()
    {
        var source = new PriorityQueue<int, long>();
        source.Enqueue(-121, long.MaxValue);
        Assert.Null(LuminPackCollectionAccess.TryAllocatePriorityQueueRecord(source));
        byte[] expected = new byte[sizeof(int) * 2 + sizeof(long)];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(sizeof(int)), -121);
        BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(sizeof(int) * 2), long.MaxValue);
        Assert.Equal(expected, LuminPackSerializer.Serialize(source));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize((PriorityQueue<int, long>?)null));
    }
}
#endif
