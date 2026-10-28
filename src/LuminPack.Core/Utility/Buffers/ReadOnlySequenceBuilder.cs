using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Utility;

/// <summary>Provides read only sequence builder pool functionality.</summary>
internal static class ReadOnlySequenceBuilderPool
{
    const int MaxPoolSize = 32;
    static readonly ConcurrentQueue<ReadOnlySequenceBuilder> queue = new();
    static int count;

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    public static ReadOnlySequenceBuilder Rent()
    {
        if (queue.TryDequeue(out var builder))
        {
            Interlocked.Decrement(ref count);
            return builder;
        }
        return new ReadOnlySequenceBuilder();
    }

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="builder">The builder used by this operation.</param>
    public static void Return(ReadOnlySequenceBuilder builder)
    {
        builder.Reset();
        if (Interlocked.Increment(ref count) <= MaxPoolSize)
        {
            queue.Enqueue(builder);
            return;
        }

        Interlocked.Decrement(ref count);
    }
}

/// <summary>Provides read only sequence builder functionality.</summary>
internal sealed class ReadOnlySequenceBuilder
{
    readonly Stack<Segment> segmentPool;
    readonly List<Segment> list;

    /// <summary>Initializes a new instance of <c>ReadOnlySequenceBuilder</c>.</summary>
    public ReadOnlySequenceBuilder()
    {
        list = new();
        segmentPool = new Stack<Segment>();
    }

    /// <summary>Adds the supplied entry to the collection.</summary>
    /// <param name="buffer">The buffer used by this operation.</param>
    /// <param name="returnToPool">The return to pool used by this operation.</param>
    public void Add(ReadOnlyMemory<byte> buffer, bool returnToPool)
    {
        if (!segmentPool.TryPop(out var segment))
        {
            segment = new Segment();
        }

        segment.SetBuffer(buffer, returnToPool);
        list.Add(segment);
    }

    /// <summary>Attempts to get single memory and reports whether it succeeded.</summary>
    /// <param name="memory">Receives the memory produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool TryGetSingleMemory(out ReadOnlyMemory<byte> memory)
    {
        if (list.Count == 1)
        {
            memory = list[0].Memory;
            return true;
        }
        memory = default;
        return false;
    }

    /// <summary>Performs the build step for the current operation.</summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlySequence<byte> Build()
    {
        if (list.Count == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        if (list.Count == 1)
        {
            return new ReadOnlySequence<byte>(list[0].Memory);
        }

        long running = 0;

        var span = LuminPackMarshal.GetListSpan(list);
        for (int i = 0; i < span.Length; i++)
        {
            var next = i < span.Length - 1 ? span[i + 1] : null;
            span[i].SetRunningIndexAndNext(running, next);
            running += span[i].Memory.Length;
        }
        var firstSegment = span[0];
        var lastSegment = span[span.Length - 1];

        return new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
    }

    /// <summary>Clears state retained by the previous operation so it can be reused.</summary>
    public void Reset()
    {
        var span = LuminPackMarshal.GetListSpan(list);

        foreach (var item in span)
        {
            item.Reset();
            segmentPool.Push(item);
        }
        list.Clear();
    }

    /// <summary>Provides segment functionality.</summary>
    class Segment : ReadOnlySequenceSegment<byte>
    {
        bool returnToPool;

        /// <summary>Initializes a new instance of <c>Segment</c>.</summary>
        public Segment()
        {
            returnToPool = false;
        }

        /// <summary>Updates the buffer used by the current operation.</summary>
        /// <param name="buffer">The buffer used by this operation.</param>
        /// <param name="returnToPool">The return to pool used by this operation.</param>
        public void SetBuffer(ReadOnlyMemory<byte> buffer, bool returnToPool)
        {
            Memory = buffer;
            this.returnToPool = returnToPool;
        }

        /// <summary>Clears state retained by the previous operation so it can be reused.</summary>
        public void Reset()
        {
            if (returnToPool)
            {
                if (MemoryMarshal.TryGetArray(Memory, out var segment) && segment.Array != null)
                {
                    ArrayPool<byte>.Shared.Return(segment.Array, clearArray: false);
                }
            }
            Memory = default;
            RunningIndex = 0;
            Next = null;
        }

        /// <summary>Updates the running index and next used by the current operation.</summary>
        /// <param name="runningIndex">The running index used by this operation.</param>
        /// <param name="nextSegment">The next segment used by this operation.</param>
        public void SetRunningIndexAndNext(long runningIndex, Segment? nextSegment)
        {
            RunningIndex = runningIndex;
            Next = nextSegment;
        }
    }
}
