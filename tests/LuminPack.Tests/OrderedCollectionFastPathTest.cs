using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks wire order, storage publication and streaming callbacks in ordered collection formatters.</summary>
public sealed class OrderedCollectionFastPathTest
{
    /// <summary>Validates bottom-first storage and reuse for a stack filled into preallocated storage.</summary>
    [Fact]
    public void RawStackKeepsWireOrderAndReuse()
    {
        var input = new Stack<long>([long.MinValue, -1, 0, long.MaxValue]);
        byte[] wire = LuminPackSerializer.Serialize(input);
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(wire));
        Assert.Equal(long.MinValue, BinaryPrimitives.ReadInt64LittleEndian(wire.AsSpan(4)));
        Assert.Equal(long.MaxValue, BinaryPrimitives.ReadInt64LittleEndian(wire.AsSpan(28)));

        var destination = new Stack<long>(16);
        destination.Push(100);
        var enumerator = destination.GetEnumerator();
        Stack<long>? alias = destination;
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref alias));
        Assert.Same(destination, alias);
        Assert.Equal(input.ToArray(), alias!.ToArray());
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        destination.Push(12);
        Assert.Equal(12, destination.Pop());

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref alias));
        Assert.Equal(input.ToArray(), destination.ToArray());
    }

    /// <summary>Keeps a reused string stack empty when conversion fails before the full payload is available.</summary>
    [Fact]
    public void FailedStringStackReadLeavesClearedTarget()
    {
        var input = new Stack<string?>(["first", null, "last-record"]);
        byte[] wire = LuminPackSerializer.Serialize(input);
        var previous = new Stack<string?>(8);
        previous.Push("old");
        Stack<string?>? result = previous;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref result));
        Assert.Same(previous, result);
        Assert.Empty(result!);
        LuminPackSerializer.Deserialize(wire, ref result);
        Assert.Same(previous, result);
        Assert.Equal(input.ToArray(), result!.ToArray());
    }

    /// <summary>Exercises a wrapped queue and reverse stack that both cross many physical output windows.</summary>
    [Fact]
    public void IntegerQueueAndStackKeepOrderAcrossWindows()
    {
        int[] input = Enumerable.Range(0, 4000).Select(i => i % 2 == 0 ? int.MinValue + i : int.MaxValue - i).ToArray();
        var queue = new Queue<int>(input);
        for (int i = 0; i < 1100; i++)
            queue.Enqueue(queue.Dequeue());
        var stack = new Stack<int>(input);
        var output = new WindowOutput(61);
        LuminPackSerializer.SerializeJson<Queue<int>, LuminPackUtf8LengthOptions>(queue, output);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(queue), output.Text);
        output = new WindowOutput(61);
        LuminPackSerializer.SerializeJson<Queue<int>, LuminPackUtf16LengthOptions>(queue, output);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(queue), output.GetText(Encoding.Unicode));
        output = new WindowOutput(61);
        LuminPackSerializer.SerializeJson<Stack<int>, LuminPackUtf8LengthOptions>(stack, output);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(stack), output.Text);
    }

    /// <summary>Exercises collection mutation at the same streaming boundary used by the public enumerators.</summary>
    [Fact]
    public void OrderedJsonStillDetectsMutationInOutputCallback()
    {
        var queue = new Queue<int>(Enumerable.Repeat(int.MinValue, 20));
        Assert.Throws<InvalidOperationException>(() => WriteQueue<LuminPackUtf8LengthOptions>(queue, new WindowOutput(12, () => queue.Enqueue(17))));
        Assert.Throws<InvalidOperationException>(() => WriteQueue<LuminPackUtf16LengthOptions>(queue, new WindowOutput(12, () => queue.Enqueue(17))));
        var stack = new Stack<int>(Enumerable.Repeat(int.MinValue, 20));
        Assert.Throws<InvalidOperationException>(() => WriteStack(stack, new WindowOutput(12, () => stack.Push(17))));
        var heap = new PriorityQueue<int, long>();
        heap.Enqueue(int.MinValue, long.MinValue);
        heap.Enqueue(17, 3);
        Assert.Throws<InvalidOperationException>(() => WriteHeap(heap, new WindowOutput(12, () => heap.Enqueue(90, 90))));

        static void WriteQueue<TOptions>(Queue<int> value, WindowOutput output) where TOptions : struct, ILuminPackOptions
        {
            using var owner = new LuminPack.Utility.LuminBufferWriter(true);
            var writer = new LuminPackJsonWriter(output, owner.WriterState, 12);
            try { LuminPackSerializer.SerializeJson<Queue<int>, TOptions>(ref writer, value); }
            finally { writer.Dispose(); }
        }

        static void WriteStack(Stack<int> value, WindowOutput output)
        {
            using var owner = new LuminPack.Utility.LuminBufferWriter(true);
            var writer = new LuminPackJsonWriter(output, owner.WriterState, 12);
            try { LuminPackSerializer.SerializeJson<Stack<int>, LuminPackUtf8LengthOptions>(ref writer, value); }
            finally { writer.Dispose(); }
        }

        static void WriteHeap(PriorityQueue<int, long> value, WindowOutput output)
        {
            using var owner = new LuminPack.Utility.LuminBufferWriter(true);
            var writer = new LuminPackJsonWriter(output, owner.WriterState, 12);
            try { LuminPackSerializer.SerializeJson<PriorityQueue<int, long>, LuminPackUtf8LengthOptions>(ref writer, value); }
            finally { writer.Dispose(); }
        }
    }

    /// <summary>Replays a non-heap suffix and preserves custom comparer behavior.</summary>
    [Fact]
    public void PriorityReadKeepsInsertionOrderAndComparer()
    {
        long[] priorities = [1, 2, 2, 3, 4, -10, 5, 9, -11, 2, 2];
        byte[] wire = new byte[4 + 12 * priorities.Length];
        BinaryPrimitives.WriteInt32LittleEndian(wire, priorities.Length);
        var stock = new PriorityQueue<int, long>(priorities.Length);
        var comparer = Comparer<long>.Create((left, right) => right.CompareTo(left));
        var reverse = new PriorityQueue<int, long>(priorities.Length, comparer);
        var expectedReverse = new PriorityQueue<int, long>(priorities.Length, comparer);
        for (int i = 0; i < priorities.Length; i++)
        {
            Span<byte> pair = wire.AsSpan(4 + 12 * i);
            BinaryPrimitives.WriteInt32LittleEndian(pair, i);
            BinaryPrimitives.WriteInt64LittleEndian(pair[4..], priorities[i]);
            stock.Enqueue(i, priorities[i]);
            expectedReverse.Enqueue(i, priorities[i]);
        }

        PriorityQueue<int, long>? result = null;
        LuminPackSerializer.Deserialize(wire, ref result);
        Assert.Equal(stock.UnorderedItems.ToArray(), result!.UnorderedItems.ToArray());
        byte[] canonical = LuminPackSerializer.Serialize(stock);
        LuminPackSerializer.Deserialize(canonical, ref result);
        Assert.Equal(stock.UnorderedItems.ToArray(), result!.UnorderedItems.ToArray());
        result = reverse;
        LuminPackSerializer.Deserialize(wire, ref result);
        Assert.Same(reverse, result);
        Assert.Equal(expectedReverse.UnorderedItems.ToArray(), result!.UnorderedItems.ToArray());
    }

    /// <summary>Checks complete, partial and ignored JSON pairs against the existing priority queue grammar.</summary>
    [Fact]
    public void PriorityJsonEntryPreservesFallbackAndPublishedPrefix()
    {
        byte[] json = "[[5,-9223372036854775808, [0]],[],[4],[2,9223372036854775807],null,[3,1]]"u8.ToArray();
        var expected = new PriorityQueue<int, long>();
        expected.Enqueue(5, long.MinValue);
        expected.Enqueue(0, 0);
        expected.Enqueue(4, 0);
        expected.Enqueue(2, long.MaxValue);
        expected.Enqueue(3, 1);
        PriorityQueue<int, long>? actual = null;
        LuminPackSerializer.DeserializeJson<PriorityQueue<int, long>, LuminPackUtf8LengthOptions>(json, ref actual);
        Assert.Equal(expected.UnorderedItems.ToArray(), actual!.UnorderedItems.ToArray());

        byte[] overflow = "[[5,1],[7,9223372036854775808],[2,3]]"u8.ToArray();
        var prior = actual;
        Assert.ThrowsAny<Exception>(() =>
            LuminPackSerializer.DeserializeJson<PriorityQueue<int, long>, LuminPackUtf8LengthOptions>(overflow, ref actual));
        Assert.Same(prior, actual);
        Assert.Equal((5, 1L), Assert.Single(actual!.UnorderedItems));
    }

    /// <summary>Provides bounded output windows and observes the first commit.</summary>
    private sealed class WindowOutput(int window, Action? mutate = null) : IBufferWriter<byte>
    {
        private readonly List<byte> _bytes = [];
        private byte[] _buffer = [];
        private bool _first = true;

        /// <summary>Gets the accumulated UTF-8 output.</summary>
        internal string Text => GetText(Encoding.UTF8);

        /// <summary>Gets the accumulated text in its selected encoding.</summary>
        /// <param name="encoding">The encoding used by the writer.</param>
        /// <returns>The decoded output.</returns>
        internal string GetText(Encoding encoding) => encoding.GetString(_bytes.ToArray());

        /// <inheritdoc/>
        public void Advance(int count)
        {
            _bytes.AddRange(_buffer.AsSpan(0, count).ToArray());
            if (_first)
            {
                _first = false;
                mutate?.Invoke();
            }
        }

        /// <inheritdoc/>
        public Memory<byte> GetMemory(int sizeHint = 0) => _buffer = new byte[Math.Max(window, sizeHint)];

        /// <inheritdoc/>
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }
}
