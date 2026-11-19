#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Code;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks streaming JSON refill of a reused framework string queue.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class QueueJsonRefillReuseTest
{
    /// <summary>Preserves equal strings while shortening a wrapped ring and appending beyond its old capacity.</summary>
    /// <param name="utf8">Whether to read an explicitly selected built-in UTF-8 profile.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrappedStringQueueReusesSlotsBeforeGrowing(bool utf8)
    {
        var queue = new Queue<string?>(8);
        for (int i = 0; i < 8; i++)
            queue.Enqueue(new string((char)('a' + i), 4));
        for (int i = 0; i < 5; i++)
        {
            queue.Dequeue();
            queue.Enqueue(new string((char)('k' + i), 4));
        }
        var alias = queue;
        var old = alias.GetEnumerator();
        var seeds = queue.ToArray();

        Read("[\"\\u0066fff\",null,\"hhhh\"]", ref queue, utf8);
        Assert.Same(alias, queue);
        Assert.Equal(new string?[] { "ffff", null, "hhhh" }, alias);
        Assert.Same(seeds[0], alias.Peek());
        Assert.Same(seeds[2], alias.Last());
        AssertUnusedSlotsAreClear(alias, 3);
        Assert.Throws<InvalidOperationException>(() => old.MoveNext());

        Read("[\"ffff\",null,\"hhhh\",\"e1\",\"e2\",\"e3\",\"e4\",\"e5\",\"e6\"]", ref queue, utf8);
        Assert.Same(alias, queue);
        Assert.Equal(9, alias.Count);
        Assert.Same(seeds[0], alias.Dequeue());
        Assert.Equal("e6", alias.Last());
        alias.Enqueue("next");
        Assert.Equal("next", alias.Last());
    }

    /// <summary>Publishes the complete prefix and clears unused references even when a later item fails.</summary>
    /// <param name="wire">The JSON array with one malformed item.</param>
    /// <param name="exception">The exception raised by the original element reader.</param>
    /// <param name="completed">The number of successfully read items.</param>
    [Theory]
    [InlineData("[\"keep\",\"cut", typeof(FormatException), 1)]
    [InlineData("[\"keep\",42]", typeof(InvalidOperationException), 1)]
    [InlineData("[\"keep\",false]", typeof(InvalidOperationException), 1)]
    [InlineData("[\"keep\",\"tail\",\"extra\",{}]", typeof(InvalidOperationException), 3)]
    public void InvalidStringPublishesOnlyTheCompletedPrefix(string wire, Type exception, int completed)
    {
        string first = new string("keep".AsSpan());
        var queue = new Queue<string?>([first, "tail"]);
        var alias = queue;
        var old = alias.GetEnumerator();

        Assert.Throws(exception, () => LuminPackSerializer.DeserializeJson(wire, ref queue));
        Assert.Same(alias, queue);
        Assert.Equal(new[] { "keep", "tail", "extra" }.Take(completed), alias);
        Assert.Same(first, alias.Peek());
        AssertUnusedSlotsAreClear(alias, completed);
        Assert.Throws<InvalidOperationException>(() => old.MoveNext());
    }

    /// <summary>Reads an unchanged warmed string queue without allocations.</summary>
    [Fact]
    public void RepeatedStringJsonReadAllocatesNothing()
    {
        var queue = new Queue<string?>(["head", "tail"]);
        var alias = queue;
        const string wire = "[\"head\",\"tail\"]";
        LuminPackSerializer.DeserializeJson(wire, ref queue);
        LuminPackSerializer.DeserializeJson(wire, ref queue);
        long prior = GC.GetAllocatedBytesForCurrentThread();
        LuminPackSerializer.DeserializeJson(wire, ref queue);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - prior;
        Assert.Same(alias, queue);
        Assert.Equal(0L, allocated);
    }

    /// <summary>Keeps the observable Enqueue sequence for a user profile with framework-equivalent values.</summary>
    [Fact]
    public void UserProfileObservesAndCanAppendToThePublishedPrefix()
    {
        string first = new string("keep".AsSpan());
        var queue = new Queue<string?>([first, "old"]);
        try
        {
            QueueJsonObserverProfile.Target = queue;
            LuminPackSerializer.DeserializeJson<Queue<string?>, QueueJsonObserverProfile>("[\"keep\",\"new\"]", ref queue);
            Assert.Null(QueueJsonObserverProfile.Target);
            Assert.Equal(new string?[] { "keep", "observed", "new" }, queue);
            Assert.NotSame(first, queue.Peek());
        }
        finally
        {
            QueueJsonObserverProfile.Target = null;
        }
    }

    /// <summary>Keeps the fallback for derived queues and the replacement semantics of ordinary objects.</summary>
    [Fact]
    public void DerivedAndObjectQueuesKeepOriginalElementBehavior()
    {
        string first = new string("keep".AsSpan());
        var derived = new DerivedStringQueue();
        derived.Enqueue(first);
        Queue<string?> queue = derived;
        LuminPackSerializer.DeserializeJson("[\"keep\"]", ref queue);
        Assert.Same(derived, queue);
        Assert.NotSame(first, queue.Peek());

        var saved = new QueueRefillItem { Id = 11 };
        var records = new Queue<QueueRefillItem>([saved]);
        LuminPackSerializer.DeserializeJson("[{\"Id\":31}]", ref records);
        Assert.Equal(31, records.Single().Id);
        Assert.NotSame(saved, records.Single());
        Assert.Equal(11, saved.Id);
    }

    // Observe storage past the public prefix to catch retained references after shortening or failure.
    private static void AssertUnusedSlotsAreClear(Queue<string?> queue, int completed)
    {
        foreach (string? item in LuminPackMarshal.GetQueueSpan(queue).Slice(completed))
            Assert.Null(item);
    }

    private static void Read(string wire, ref Queue<string?> queue, bool utf8)
    {
        if (utf8)
            LuminPackSerializer.DeserializeJson<Queue<string?>, LuminPackUtf8LengthOptions>(Encoding.UTF8.GetBytes(wire), ref queue);
        else
            LuminPackSerializer.DeserializeJson(wire, ref queue);
    }

    private sealed class DerivedStringQueue : Queue<string?>;
}

/// <summary>Returns the built-in UTF-16 length settings while observing queue publication.</summary>
internal readonly struct QueueJsonObserverProfile : ILuminPackOptions
{
    /// <inheritdoc/>
    public int RecordBound(string? value) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).RecordBound(value);

    /// <inheritdoc/>
    public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Write(ref writer, value);

    /// <inheritdoc/>
    public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Read(ref reader, current);

    /// <inheritdoc/>
    public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    public int Measure(string? value) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Measure(value);

    /// <inheritdoc/>
    public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    public bool SupportsUtf16Runs() =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encoding;

    /// <inheritdoc/>
    public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Recording;

    /// <inheritdoc/>
    public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).BinaryProfile;

    internal static Queue<string?>? Target;

    public LuminPackStringEncoding StringEncoding
    {
        get
        {
            if (Target is { Count: 1 } queue && queue.Peek() == "keep")
            {
                Target = null;
                queue.Enqueue("observed");
            }
            return default(LuminPackUtf16LengthOptions).StringEncoding;
        }
    }

    public LuminPackStringRecording StringRecording => default(LuminPackUtf16LengthOptions).StringRecording;
    public StandardFormat StandardFormat => default(LuminPackUtf16LengthOptions).StandardFormat;
    public int MaxJsonDepth => default(LuminPackUtf16LengthOptions).MaxJsonDepth;
    public bool ValidateJsonSyntax => default(LuminPackUtf16LengthOptions).ValidateJsonSyntax;
}
#endif
