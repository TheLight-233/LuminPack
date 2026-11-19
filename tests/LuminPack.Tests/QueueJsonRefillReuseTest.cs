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
