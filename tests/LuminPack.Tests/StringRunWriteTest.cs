using System.Buffers.Binary;
using System.Collections.Immutable;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that batched string runs, string pairs and immutable-queue stack walks write exactly what one record
/// write per element does, across storage growth.</summary>
public sealed class StringRunWriteTest
{
    /// <summary>Returns strings covering null, empty, compact, raw, short-kernel and growth-forcing records.</summary>
    /// <param name="count">The number of strings.</param>
    /// <returns>The strings.</returns>
    private static string?[] Mixed(int count)
    {
        var values = new string?[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = (i % 11) switch
            {
                0 => null,
                1 => "",
                2 => "v" + i,
                3 => "\u00E9t\u00E9-" + i,
                4 => "\u4E2D\u6587-" + i,
                5 => new string('x', 4 + i % 13),
                6 => new string('y', 300 + i % 50),
                7 => new string('\u0100', 1 + i % 20),
                8 => i % 97 == 8 ? new string('z', 70_000) : "w",
                9 => new string('q', 16),
                _ => new string('ÿ', 17 + i % 40),
            };
        }
        return values;
    }

    /// <summary>Concatenates the root record of each string, which is also its element record.</summary>
    /// <param name="values">The strings.</param>
    /// <returns>The records.</returns>
    private static IEnumerable<byte> Records(IEnumerable<string?> values) => values.SelectMany(value => LuminPackSerializer.Serialize(value));

    /// <summary>Prefixes a count header.</summary>
    /// <param name="count">The count.</param>
    /// <param name="payload">The element records.</param>
    /// <returns>The complete collection record.</returns>
    private static byte[] Counted(int count, IEnumerable<byte> payload)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, count);
        return header.Concat(payload).ToArray();
    }

    /// <summary>Checks lists, arrays and immutable arrays of strings whose records cross several growth boundaries.</summary>
    [Fact]
    public void StringSpansMatchPerElementRecords()
    {
        foreach (int count in new[] { 0, 1, 7, 64, 3000 })
        {
            string?[] values = Mixed(count);
            byte[] expected = Counted(count, Records(values));
            Assert.Equal(expected, LuminPackSerializer.Serialize(values.ToList()));
            Assert.Equal(expected, LuminPackSerializer.Serialize(values));
            Assert.Equal(expected, LuminPackSerializer.Serialize(ImmutableArray.Create(values)));

            List<string?>? list = null;
            LuminPackSerializer.Deserialize(expected, ref list);
            Assert.Equal(values, list);
        }
    }

    /// <summary>Checks that every binary profile keeps its own records through the batched entry point.</summary>
    [Fact]
    public void EveryProfileMatchesSingleRecordWrites()
    {
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackUtf16TokenOptions>();
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf8TokenOptions>();

        static void Check<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            string?[] values = Mixed(500);
            var batched = LuminBufferWriterPool.Rent();
            var single = LuminBufferWriterPool.Rent();
            try
            {
                var first = new LuminPackWriter(batched);
                first.WriteStrings<TOptions>(values);
                first.WriteStringPairs<TOptions>(values, Enumerable.Reverse(values).ToArray());
                var second = new LuminPackWriter(single);
                foreach (string? value in values) second.Write<TOptions>(value);
                string?[] reversed = Enumerable.Reverse(values).ToArray();
                for (int i = 0; i < values.Length; i++)
                {
                    second.CheckBuffer();
                    second.Write<TOptions>(values[i]);
                    second.Write<TOptions>(reversed[i]);
                }
                Assert.Equal(second.GetSpan().ToArray(), first.GetSpan().ToArray());
            }
            finally
            {
                LuminBufferWriterPool.Return(batched);
                LuminBufferWriterPool.Return(single);
            }
        }
    }

    /// <summary>Checks string-keyed sorted lists, whose pairs alternate key and value records.</summary>
    [Fact]
    public void SortedListPairsMatchPerElementRecords()
    {
        foreach (int count in new[] { 0, 1, 50, 2500 })
        {
            string?[] values = Mixed(count);
            var list = new SortedList<string, string?>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++) list.Add("k" + i.ToString("D5") + (i % 5 == 0 ? new string('è', i % 30) : ""), values[i]);
            byte[] expected = Counted(count, list.SelectMany(pair => Records(new[] { pair.Key, pair.Value })));
            Assert.Equal(expected, LuminPackSerializer.Serialize(list));

            SortedList<string, string?>? read = null;
            LuminPackSerializer.Deserialize(expected, ref read);
            Assert.Equal(list, read!);
        }
    }

    /// <summary>Checks immutable queues before and after their back stack is reversed, and through their interface.</summary>
    [Fact]
    public void ImmutableQueueStacksMatchTheEnumeration()
    {
        var queue = ImmutableQueue<string?>.Empty;
        foreach (string? value in Mixed(400)) queue = queue.Enqueue(value);
        queue = queue.Dequeue().Enqueue("tail-1").Enqueue(null).Enqueue("tail-2");
        byte[] expected = Counted(queue.Count(), Records(queue));

        // Counting above enumerated the queue, which cached its reversed back stack; a new queue has none until its
        // first enumeration, which the fallback write performs.
        string?[] items = queue.ToArray().Append("late").ToArray();
        var fresh = ImmutableQueue.CreateRange(queue.ToArray()).Enqueue("late");
        Assert.False(LuminPackCollectionAccess.TryGetImmutableQueueStacks(fresh, out _, out _));
        byte[] freshExpected = Counted(items.Length, Records(items));
        Assert.Equal(freshExpected, LuminPackSerializer.Serialize(fresh));
        Assert.True(LuminPackCollectionAccess.TryGetImmutableQueueStacks(fresh, out _, out _));
        Assert.Equal(freshExpected, LuminPackSerializer.Serialize(fresh));

        Assert.Equal(expected, LuminPackSerializer.Serialize(queue));
        IImmutableQueue<string?> asInterface = queue;
        Assert.Equal(expected, LuminPackSerializer.Serialize(asInterface));
        ImmutableQueue<string?>? read = null;
        LuminPackSerializer.Deserialize(expected, ref read);
        Assert.Equal(queue, read!);

        var numbers = ImmutableQueue<int>.Empty;
        for (int i = 0; i < 1000; i++) numbers = i % 3 == 0 && !numbers.IsEmpty ? numbers.Dequeue().Enqueue(i) : numbers.Enqueue(i);
        ImmutableQueue<int>? readNumbers = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(numbers), ref readNumbers);
        Assert.Equal(numbers, readNumbers!);
        Assert.Equal(new[] { "a" }, RoundTrip(ImmutableQueue<string>.Empty.Enqueue("a")));
        Assert.Empty(RoundTrip(ImmutableQueue<string>.Empty));

        static ImmutableQueue<string> RoundTrip(ImmutableQueue<string> value)
        {
            ImmutableQueue<string>? result = null;
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(value), ref result);
            return result!;
        }
    }

    /// <summary>Checks that reused memory keeps its exact storage, and other lengths publish new storage.</summary>
    [Fact]
    public void ReusedMemoryKeepsItsStorage()
    {
        byte[] bytes = LuminPackSerializer.Serialize((Memory<int>)Enumerable.Range(0, 100).ToArray());
        int[] backing = new int[120];
        Memory<int> target = backing.AsMemory(10, 100);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.True(target.Span.SequenceEqual(Enumerable.Range(0, 100).ToArray()));
        Assert.True(System.Runtime.InteropServices.MemoryMarshal.TryGetArray<int>(target, out var segment));
        Assert.Same(backing, segment.Array);
        Assert.Equal(10, segment.Offset);
        Assert.Equal(0, backing[9]);
        Assert.Equal(0, backing[110]);

        Memory<int> shorter = new int[99];
        LuminPackSerializer.Deserialize(bytes, ref shorter);
        Assert.Equal(100, shorter.Length);
        Assert.True(shorter.Span.SequenceEqual(Enumerable.Range(0, 100).ToArray()));
    }
}
