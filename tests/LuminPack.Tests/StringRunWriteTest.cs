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

    /// <summary>Checks the cursor, published slots and retained references at every truncation of a mixed string run.</summary>
    [Fact]
    public void StringSpanReadsPublishTheSamePrefixAsSingleRecords()
    {
        string?[] values = ["four", "se\u00ffven!", "12345678", "123456789", new string('\u00e9', 16), "keep",
            "replace", new string('x', 17), "abc", "\u4e2d\u6587", null, "", "z"];
        byte[] records = Records(values).ToArray();
        var initial = new string?[values.Length];
        initial[5] = new string("keep".ToCharArray());
        initial[6] = "previous";
        for (int length = 0; length <= records.Length; length++)
        {
            string?[] expected = (string?[])initial.Clone();
            string?[] actual = (string?[])initial.Clone();
            ReadOnlySpan<byte> input = records.AsSpan(0, length);
            var single = new LuminPackReader(ref input);
            var batch = new LuminPackReader(ref input);
            Exception? singleFailure = null;
            Exception? batchFailure = null;
            try
            {
                for (int i = 0; i < expected.Length; i++)
                    single.Read<LuminPackUtf16LengthOptions>(ref expected[i]);
            }
            catch (Exception ex) { singleFailure = ex; }
            try { batch.ReadStrings<LuminPackUtf16LengthOptions>(actual); }
            catch (Exception ex) { batchFailure = ex; }
            Assert.Equal(length == records.Length, singleFailure is null);
            Assert.Equal(singleFailure?.GetType(), batchFailure?.GetType());
            Assert.Equal(single.Position, batch.Position);
            Assert.Equal(expected, actual);
            Assert.Same(initial[5], actual[5]);
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

    /// <summary>Checks both sorted dictionary writers against individual string records across buffer growth.</summary>
    [Fact]
    public void TreeStringPairsMatchPerElementRecordsAcrossGrowth()
    {
        var sorted = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        string?[] values = Mixed(180);
        for (int i = 0; i < values.Length; i++)
            sorted.Add(i.ToString("D3") + (i == 40 ? new string('\u0100', 70_000) : ""), values[i]);
        var immutable = sorted.ToImmutableSortedDictionary(StringComparer.Ordinal);
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackUtf16TokenOptions>();
        Check<LuminPackUtf16VarIntOptions>();
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf8TokenOptions>();
        Check<LuminPackUtf8VarIntOptions>();

        void Check<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            byte[] expected = Counted(sorted.Count, sorted.SelectMany(pair =>
                LuminPackSerializer.Serialize<string, TOptions>(pair.Key).Concat(LuminPackSerializer.Serialize<string, TOptions>(pair.Value))));
            Assert.Equal(expected, LuminPackSerializer.Serialize<SortedDictionary<string, string?>, TOptions>(sorted));
            Assert.Equal(expected, LuminPackSerializer.Serialize<ImmutableSortedDictionary<string, string>, TOptions>(immutable));
        }
    }

    /// <summary>Checks every short fixed destination, including a compact pair that fits only through scalar writes.</summary>
    [Fact]
    public void CapturedStringPairsKeepFixedBufferBoundaries()
    {
        KeyValuePair<string, string?>[] pairs = [new("", null), new("key", "compact"), new("raw", "\u0100")];
        for (int room = 0; room <= 64; room++)
        {
            var expected = Run(false, room);
            var actual = Run(true, room);
            Assert.Equal(expected.failure, actual.failure);
            Assert.Equal(expected.position, actual.position);
            Assert.Equal(expected.bytes, actual.bytes);
        }

        (Type? failure, int position, byte[] bytes) Run(bool batch, int room)
        {
            byte[] bytes = Enumerable.Repeat((byte)0xA5, room + 8).ToArray();
            Span<byte> destination = bytes.AsSpan(4, room);
            var writer = new LuminPackWriter(ref destination);
            try
            {
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (batch)
                    {
                        i += writer.WriteStringPairPrefix<LuminPackUtf16LengthOptions>(pairs.AsSpan(i));
                        if (i == pairs.Length) break;
                    }
                    writer.CheckBuffer();
                    writer.Write<LuminPackUtf16LengthOptions>(pairs[i].Key);
                    writer.Write<LuminPackUtf16LengthOptions>(pairs[i].Value);
                }
            }
            catch (Exception ex) { return (ex.GetType(), writer.Position, bytes); }
            return (null, writer.Position, bytes);
        }
    }

    /// <summary>Checks dense and sparse dictionary records and cursor publication across retained-buffer growth.</summary>
    [Fact]
    public void DictionaryStringPairsKeepRecordsAndFollowingCursor()
    {
        string?[] values = Mixed(200);
        var strings = new Dictionary<string, string?>(StringComparer.Ordinal);
        var scalars = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < values.Length; index++)
        {
            string key = index.ToString("D3") + (index == 41 ? new string('\u0100', 70_000) : "\u00e9");
            strings.Add(key, values[index]);
            scalars.Add(key, index);
        }

        Check();
        strings.Remove("015\u00e9");
        scalars.Remove("015\u00e9");
        Check();

        void Check()
        {
            const int tail = 91;
            byte[] tailBytes = LuminPackSerializer.Serialize(tail);
            byte[] expectedStrings = Counted(strings.Count, strings.SelectMany(pair => Records([pair.Key, pair.Value])));
            byte[] expectedScalars = Counted(scalars.Count, scalars.SelectMany(pair =>
                LuminPackSerializer.Serialize(pair.Key).Concat(LuminPackSerializer.Serialize(pair.Value))));
            IReadOnlyDictionary<string, string?> textInterface = strings;
            Assert.Equal(expectedStrings, LuminPackSerializer.Serialize(textInterface));
            using var textBuffer = new LuminBufferWriter(true);
            using var scalarBuffer = new LuminBufferWriter(true);
            var textWriter = new LuminPackWriter(textBuffer);
            var scalarWriter = new LuminPackWriter(scalarBuffer);
            LuminPackSerializer.Serialize(ref textWriter, in strings);
            LuminPackSerializer.Serialize(ref scalarWriter, in scalars);
            textWriter.Write(tail);
            scalarWriter.Write(tail);
            Assert.Equal(expectedStrings.Concat(tailBytes), textWriter.GetSpan().ToArray());
            Assert.Equal(expectedScalars.Concat(tailBytes), scalarWriter.GetSpan().ToArray());
        }
    }

    /// <summary>Checks the wire order of both parts of a wrapped queue and the bottom-to-top stack storage.</summary>
    [Fact]
    public void WrappedQueueAndStackMatchIndividualStringRecords()
    {
        string?[] values = Mixed(96);
        var queue = new Queue<string?>(values);
        for (int i = 0; i < 31; i++) queue.Enqueue(queue.Dequeue());
        string?[] ordered = values.Skip(31).Concat(values.Take(31)).ToArray();
        var stack = new Stack<string?>(ordered);
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackUtf16TokenOptions>();
        Check<LuminPackUtf16VarIntOptions>();
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf8TokenOptions>();
        Check<LuminPackUtf8VarIntOptions>();

        void Check<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            byte[] records = ordered.SelectMany(text => LuminPackSerializer.Serialize<string, TOptions>(text)).ToArray();
            byte[] stackBytes = Counted(ordered.Length, records);
            Assert.Equal(stackBytes, LuminPackSerializer.Serialize<Stack<string?>, TOptions>(stack));
            Assert.Equal(Counted(ordered.Length, new byte[8].Concat(stackBytes)), LuminPackSerializer.Serialize<Queue<string?>, TOptions>(queue));
        }
    }

    /// <summary>Checks that every bounded prefix writes exactly the complete records accepted by individual raw bounds.</summary>
    [Fact]
    public void StringPrefixRespectsTheRemainingFixedBuffer()
    {
        string?[] values = [new string('a', 17), new string('\u0100', 32), null, "", "a", "compact", "réservé", "\u0100\u0301", "end"];
        int rawBound = values.Sum(value => sizeof(int) + (value?.Length ?? 0) * sizeof(char));
        for (int room = 0; room <= rawBound; room++)
        {
            byte[] actual = Enumerable.Repeat((byte)0xA5, room + 8).ToArray();
            byte[] expected = (byte[])actual.Clone();
            Span<byte> full = actual.AsSpan(3, room + 2);
            Span<byte> reference = expected.AsSpan(3, room + 2);
            var batch = new LuminPackWriter(ref full) { Position = 2 };
            var single = new LuminPackWriter(ref reference) { Position = 2 };
            int count = 0;
            foreach (string? value in values)
            {
                if (sizeof(int) + (value?.Length ?? 0) * sizeof(char) > reference.Length - single.Position) break;
                single.CheckBuffer();
                single.Write<LuminPackUtf16LengthOptions>(value);
                count++;
            }
            Assert.Equal(count, batch.WriteStringPrefix<LuminPackUtf16LengthOptions>(values));
            Assert.Equal(single.Position, batch.Position);
            Assert.Equal(expected, actual);
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

    /// <summary>Checks key and raw scalar framing on both sides of a growth-forcing key.</summary>
    [Fact]
    public void SortedListScalarPairsMatchPerElementRecordsAcrossGrowth()
    {
        var list = new SortedList<string, long>(StringComparer.Ordinal) { [""] = long.MinValue };
        for (int i = 0; i < 96; i++)
        {
            string key = i.ToString("D3") + (i % 3 == 0 ? new string('\u0100', i == 48 ? 70_000 : 12) : "\u00e9");
            list.Add(key, long.MaxValue - i);
        }
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackUtf16TokenOptions>();
        Check<LuminPackUtf16VarIntOptions>();
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf8TokenOptions>();
        Check<LuminPackUtf8VarIntOptions>();

        void Check<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            byte[] expected = Counted(list.Count, list.SelectMany(pair =>
                LuminPackSerializer.Serialize<string, TOptions>(pair.Key).Concat(LuminPackSerializer.Serialize<long, TOptions>(pair.Value))));
            Assert.Equal(expected, LuminPackSerializer.Serialize<SortedList<string, long>, TOptions>(list));
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

        // A fresh queue has an uncached back stack. Serialization borrows traversal storage until a caller enumerates it.
        string?[] items = queue.ToArray().Append("late").ToArray();
        var fresh = ImmutableQueue.CreateRange(queue.ToArray()).Enqueue("late");
        Assert.False(LuminPackCollectionAccess.TryGetImmutableQueueStacks(fresh, out _, out _));
        byte[] freshExpected = Counted(items.Length, Records(items));
        Assert.Equal(freshExpected, LuminPackSerializer.Serialize(fresh));
        Assert.False(LuminPackCollectionAccess.TryGetImmutableQueueStacks(fresh, out _, out _));
        Assert.Equal(items, fresh.ToArray());
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
