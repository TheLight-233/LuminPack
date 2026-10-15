using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>Provides lumin buffer writer pool regression test functionality.</summary>
internal static class LuminBufferWriterPoolRegressionTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(TlsRoundTripResetsAndReusesWriter), TlsRoundTripResetsAndReusesWriter);
        RunCase(results, nameof(TlsHotPathAllocatesNothing), TlsHotPathAllocatesNothing);
        RunCase(results, nameof(CentralSlotsAreBoundedAndUnordered), CentralSlotsAreBoundedAndUnordered);
        RunCase(results, nameof(OversizedWriterIsDisposedInsteadOfCached), OversizedWriterIsDisposedInsteadOfCached);
        RunCase(results, nameof(AtomicSlotsNeverDuplicateAnItem), AtomicSlotsNeverDuplicateAnItem);
        RunCase(results, nameof(WriterPoolNeverDoubleRentsOrReturnsDisposedWriter), WriterPoolNeverDoubleRentsOrReturnsDisposedWriter);
    }

    /// <summary>Verifies tls round trip resets and reuses writer.</summary>
    private static void TlsRoundTripResetsAndReusesWriter()
    {
        ClearPool();
        var writer = LuminBufferWriterPool.Rent();
        writer.SetWrittenCount(123);
        LuminBufferWriterPool.Return(writer);

        var reused = LuminBufferWriterPool.Rent();
        Assert(ReferenceEquals(writer, reused), "TLS did not return the same writer.");
        Assert(reused.CurrentIndex == 0, "Returned writer was not reset before caching.");
        Assert(reused.UseFirstBuffer, "TLS returned a disposed writer.");

        LuminBufferWriterPool.Return(reused);
        ClearPool();
    }

    /// <summary>Verifies tls hot path allocates nothing.</summary>
    private static void TlsHotPathAllocatesNothing()
    {
        ClearPool();
        var writer = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.Return(writer);

        for (var i = 0; i < 20_000; i++)
        {
            writer = LuminBufferWriterPool.Rent();
            LuminBufferWriterPool.Return(writer);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000_000; i++)
        {
            writer = LuminBufferWriterPool.Rent();
            LuminBufferWriterPool.Return(writer);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert(allocated == 0, $"TLS Rent/Return allocated {allocated} bytes.");
        ClearPool();
    }

    /// <summary>Verifies central slots are bounded and unordered.</summary>
    private static void CentralSlotsAreBoundedAndUnordered()
    {
        ClearPool();
#if LUMINPACK_BUFFERWRITER_SECOND_TLS
        const int tlsSlotCount = 2;
#else
        const int tlsSlotCount = 1;
#endif
        var writers = new LuminBufferWriter[LuminBufferWriterPool.MaxPoolSize + tlsSlotCount + 1];
        for (var i = 0; i < writers.Length; i++)
        {
            writers[i] = new LuminBufferWriter(true);
            LuminBufferWriterPool.Return(writers[i]);
        }

        Assert(LuminBufferWriterPool.ApproximateCentralCount == LuminBufferWriterPool.MaxPoolSize,
            "Central atomic slots did not stop at their fixed capacity.");
        Assert(!writers[^1].UseFirstBuffer, "Writer rejected by the full central pool was not disposed.");

        LuminBufferWriterPool.ClearThreadLocalCache();
        var expected = new HashSet<LuminBufferWriter>(writers.Skip(tlsSlotCount).Take(LuminBufferWriterPool.MaxPoolSize),
            ReferenceComparer.Instance);
        var taken = new HashSet<LuminBufferWriter>(ReferenceComparer.Instance);

        while (LuminBufferWriterPool.ApproximateCentralCount != 0)
        {
            var writer = LuminBufferWriterPool.Rent();
            Assert(expected.Contains(writer), "Central pool returned an object that was never stored.");
            Assert(taken.Add(writer), "Central pool returned the same writer more than once.");
            writer.Dispose();
        }

        Assert(taken.Count == LuminBufferWriterPool.MaxPoolSize, "Central pool lost a stored writer.");
        ClearPool();
    }

    /// <summary>Verifies oversized writer is disposed instead of cached.</summary>
    private static void OversizedWriterIsDisposedInsteadOfCached()
    {
        ClearPool();
        var oversized = new LuminBufferWriter(true);
        oversized.EnsureCapacity(LuminBufferWriterPool.MaxPooledBufferSize);
        LuminBufferWriterPool.Return(oversized);

        Assert(!oversized.UseFirstBuffer, "Oversized writer was cached instead of disposed.");
        Assert(LuminBufferWriterPool.ApproximateCentralCount == 0,
            "Oversized writer reached the central pool.");

        var replacement = LuminBufferWriterPool.Rent();
        Assert(!ReferenceEquals(oversized, replacement), "Disposed oversized writer was rented again.");
        Assert(replacement.UseFirstBuffer, "Replacement writer is disposed.");
        LuminBufferWriterPool.Return(replacement);
        ClearPool();
    }

    /// <summary>Verifies atomic slots never duplicate an item.</summary>
    private static void AtomicSlotsNeverDuplicateAnItem()
    {
        var pool = new AtomicSlotPool<SlotItem>(32);
        var workers = Math.Min(32, Math.Max(4, Environment.ProcessorCount));
        const int iterations = 200_000;
        var duplicateRents = 0;

        Parallel.For(0, workers, _ =>
        {
            for (var i = 0; i < iterations; i++)
            {
                var item = pool.TryTake() ?? new SlotItem();
                if (Interlocked.CompareExchange(ref item.InUse, 1, 0) != 0)
                    Interlocked.Increment(ref duplicateRents);

                Volatile.Write(ref item.InUse, 0);
                pool.TryStore(item);
            }
        });

        Assert(duplicateRents == 0, $"Atomic slots double-rented an item {duplicateRents} times.");
        Assert(pool.ApproximateCount <= pool.Capacity, "Atomic slots exceeded fixed capacity.");
        pool.Drain(static _ => { });
    }

    /// <summary>Writes r pool never double rents or returns disposed writer to the current output.</summary>
    private static void WriterPoolNeverDoubleRentsOrReturnsDisposedWriter()
    {
        ClearPool();
        var workers = Math.Min(32, Math.Max(4, Environment.ProcessorCount));
        const int iterations = 100_000;
        var active = new ConcurrentDictionary<LuminBufferWriter, byte>(ReferenceComparer.Instance);
        var duplicateRents = 0;
        var disposedRents = 0;

        Parallel.For(0, workers, workerIndex =>
        {
            try
            {
                for (var i = 0; i < iterations; i++)
                {
                    var first = LuminBufferWriterPool.Rent();
                    var second = LuminBufferWriterPool.Rent();

                    if (!active.TryAdd(first, 0))
                        Interlocked.Increment(ref duplicateRents);
                    if (!active.TryAdd(second, 0))
                        Interlocked.Increment(ref duplicateRents);
                    if (!first.UseFirstBuffer || !second.UseFirstBuffer)
                        Interlocked.Increment(ref disposedRents);

                    first.SetWrittenCount(1);
                    second.SetWrittenCount(1);

                    active.TryRemove(second, out _);
                    LuminBufferWriterPool.Return(second);
                    active.TryRemove(first, out _);
                    LuminBufferWriterPool.Return(first);
                }
            }
            finally
            {
                LuminBufferWriterPool.ClearThreadLocalCache();
            }
        });

        Assert(duplicateRents == 0, $"Writer pool double-rented a writer {duplicateRents} times.");
        Assert(disposedRents == 0, $"Writer pool rented disposed writers {disposedRents} times.");
        Assert(active.IsEmpty, "Writer remained marked active after the stress test.");
        Assert(LuminBufferWriterPool.ApproximateCentralCount <= LuminBufferWriterPool.MaxPoolSize,
            "Writer central pool exceeded its fixed capacity.");
        ClearPool();
    }

    /// <summary>Verifies clear pool.</summary>
    private static void ClearPool()
    {
        LuminBufferWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearCentralCache();
    }

    /// <summary>Provides slot item functionality.</summary>
    private sealed class SlotItem
    {
        internal int InUse;
    }

    /// <summary>Provides reference comparer functionality.</summary>
    private sealed class ReferenceComparer : IEqualityComparer<LuminBufferWriter>
    {
        internal static readonly ReferenceComparer Instance = new();

        /// <summary>Determines whether the supplied value is equal to the current value.</summary>
        /// <param name="x">The x used by this operation.</param>
        /// <param name="y">The y used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        public bool Equals(LuminBufferWriter? x, LuminBufferWriter? y) => ReferenceEquals(x, y);

        /// <summary>Computes the hash code used by the configured lookup or equality strategy.</summary>
        /// <param name="obj">The obj used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public int GetHashCode(LuminBufferWriter obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>Verifies run case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
