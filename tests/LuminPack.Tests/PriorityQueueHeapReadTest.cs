#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Buffers.Binary;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary priority-queue reads that accept a verified heap prefix before ordinary insertion.</summary>
public sealed class PriorityQueueHeapReadTest
{
    /// <summary>Reads heap-ordered and broken payloads into fresh and reused queues exactly like ordered framework insertion.</summary>
    [Fact]
    public void HeapPrefixesMatchFrameworkInsertion()
    {
        foreach (int count in new[] { 1, 2, 4, 5, 8, 9, 16, 17, 33, 256, 4097 })
        {
            foreach (bool ties in new[] { false, true })
            {
                var breaks = new List<int> { -1, count - 1, count / 2 };
                for (int at = 1; at <= Math.Min(count - 1, 12); at++)
                    breaks.Add(at);
                foreach (int broken in breaks.Distinct())
                {
                    if (broken == 0)
                        continue;
                    var pairs = new (int Element, int Priority)[count];
                    for (int index = 0; index < count; index++)
                        pairs[index] = (unchecked(index * 31 + 7), ties ? index / 5 : index);
                    if (broken > 0)
                        pairs[broken].Priority = -1 - broken;
                    Check(pairs);
                }
            }
        }
    }

    /// <summary>Compares fresh and reused reads with the framework queue built by enqueueing the payload pairs in order.</summary>
    /// <param name="pairs">The pairs in wire order.</param>
    private static void Check((int Element, int Priority)[] pairs)
    {
        byte[] wire = new byte[sizeof(int) + pairs.Length * 8];
        BinaryPrimitives.WriteInt32LittleEndian(wire, pairs.Length);
        var expected = new PriorityQueue<int, int>();
        for (int index = 0; index < pairs.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(sizeof(int) + index * 8), pairs[index].Element);
            BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(sizeof(int) + index * 8 + 4), pairs[index].Priority);
            expected.Enqueue(pairs[index].Element, pairs[index].Priority);
        }

        PriorityQueue<int, int>? fresh = null;
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref fresh));
        Assert.Equal(expected.UnorderedItems.ToArray(), fresh!.UnorderedItems.ToArray());

        var reused = new PriorityQueue<int, int>(pairs.Length);
        reused.Enqueue(-5, int.MinValue);
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref reused));
        Assert.Equal(expected.UnorderedItems.ToArray(), reused.UnorderedItems.ToArray());
        var drained = new List<int>();
        var reference = new List<int>();
        while (reused.TryDequeue(out int element, out _) && expected.TryDequeue(out int other, out _))
        {
            drained.Add(element);
            reference.Add(other);
        }
        Assert.Equal(reference, drained);
    }
}
#endif
