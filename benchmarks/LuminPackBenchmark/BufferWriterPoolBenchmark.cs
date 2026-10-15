using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides buffer writer pool jit probe functionality.</summary>
internal static class BufferWriterPoolJitProbe
{
    /// <summary>Measures run for the configured benchmark input.</summary>
    internal static void Run()
    {
        LegacyRingWriterPool.Return(LegacyRingWriterPool.Rent());
        LuminBufferWriterPool.Return(LuminBufferWriterPool.Rent());

        var legacy = LegacyRoundTrip();
        var dedicated = DedicatedRoundTrip();
        Console.WriteLine($"JIT probe: {legacy.TotalLength + dedicated.TotalLength}");

        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Measures legacy round trip for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter LegacyRoundTrip()
    {
        var writer = LegacyRingWriterPool.Rent();
        LegacyRingWriterPool.Return(writer);
        return writer;
    }

    /// <summary>Measures dedicated round trip for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter DedicatedRoundTrip()
    {
        var writer = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.Return(writer);
        return writer;
    }
}

/// <summary>Provides buffer writer pool hot path benchmark functionality.</summary>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 4, printSource: true, printInstructionAddresses: true)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BufferWriterPoolHotPathBenchmark
{
    private const int Operations = 1_000_000;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
        DoubleTlsWriterPool.ClearThreadLocalCache();

        LegacyRingWriterPool.Return(LegacyRingWriterPool.Rent());
        LuminBufferWriterPool.Return(LuminBufferWriterPool.Rent());

        var first = DoubleTlsWriterPool.Rent();
        var second = DoubleTlsWriterPool.Rent();
        DoubleTlsWriterPool.Return(second);
        DoubleTlsWriterPool.Return(first);
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
        DoubleTlsWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Measures legacy generic shape tls for the configured benchmark input.</summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
    [BenchmarkCategory("TLS")]
    public void LegacyGenericShapeTls()
    {
        for (var i = 0; i < Operations; i++)
        {
            var writer = LegacyRingWriterPool.Rent();
            LegacyRingWriterPool.Return(writer);
        }
    }

    /// <summary>Measures dedicated tls for the configured benchmark input.</summary>
    [Benchmark(OperationsPerInvoke = Operations)]
    [BenchmarkCategory("TLS")]
    public void DedicatedTls()
    {
        for (var i = 0; i < Operations; i++)
        {
            var writer = LuminBufferWriterPool.Rent();
            LuminBufferWriterPool.Return(writer);
        }
    }

    /// <summary>Measures dedicated single tls nested for the configured benchmark input.</summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = Operations * 2)]
    [BenchmarkCategory("Nested")]
    public void DedicatedSingleTlsNested()
    {
        for (var i = 0; i < Operations; i++)
        {
            var first = LuminBufferWriterPool.Rent();
            var second = LuminBufferWriterPool.Rent();
            LuminBufferWriterPool.Return(second);
            LuminBufferWriterPool.Return(first);
        }
    }

    /// <summary>Measures compile time double tls nested for the configured benchmark input.</summary>
    [Benchmark(OperationsPerInvoke = Operations * 2)]
    [BenchmarkCategory("Nested")]
    public void CompileTimeDoubleTlsNested()
    {
        for (var i = 0; i < Operations; i++)
        {
            var first = DoubleTlsWriterPool.Rent();
            var second = DoubleTlsWriterPool.Rent();
            DoubleTlsWriterPool.Return(second);
            DoubleTlsWriterPool.Return(first);
        }
    }
}

/// <summary>Provides buffer writer pool assembly benchmark functionality.</summary>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 5, printSource: true, printInstructionAddresses: true)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class BufferWriterPoolAssemblyBenchmark
{
    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
        LegacyRingWriterPool.Return(LegacyRingWriterPool.Rent());
        LuminBufferWriterPool.Return(LuminBufferWriterPool.Rent());
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Measures legacy tls round trip for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public LuminBufferWriter LegacyTlsRoundTrip()
    {
        var writer = LegacyRingWriterPool.Rent();
        LegacyRingWriterPool.Return(writer);
        return writer;
    }

    /// <summary>Measures dedicated tls round trip for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public LuminBufferWriter DedicatedTlsRoundTrip()
    {
        var writer = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.Return(writer);
        return writer;
    }
}

/// <summary>Provides atomic slot probe benchmark functionality.</summary>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3, printSource: true, printInstructionAddresses: true)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class AtomicSlotProbeBenchmark
{
    /// <summary>Gets or sets the occupied.</summary>
    [Params(0, 8, 16, 24, 32)]
    public int Occupied { get; set; }

    private ProbeSlots _takePreRead = null!;
    private ProbeSlots _takeDirect = null!;
    private ProbeSlots _storePreRead = null!;
    private ProbeSlots _storeDirect = null!;
    private readonly ProbeItem _candidate = new();

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _takePreRead = new ProbeSlots(Occupied);
        _takeDirect = new ProbeSlots(Occupied);
        _storePreRead = new ProbeSlots(Occupied);
        _storeDirect = new ProbeSlots(Occupied);
    }

    /// <summary>Measures take volatile read then exchange for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public ProbeItem? TakeVolatileReadThenExchange()
    {
        var item = _takePreRead.TryTakePreRead(out var index);
        if (item is not null)
            _takePreRead.Restore(index, item);
        return item;
    }

    /// <summary>Measures take direct exchange for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public ProbeItem? TakeDirectExchange()
    {
        var item = _takeDirect.TryTakeDirect(out var index);
        if (item is not null)
            _takeDirect.Restore(index, item);
        return item;
    }

    /// <summary>Measures store volatile read then cas for the configured benchmark input.</summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [Benchmark]
    public bool StoreVolatileReadThenCas()
    {
        var stored = _storePreRead.TryStorePreRead(_candidate, out var index);
        if (stored)
            _storePreRead.Remove(index);
        return stored;
    }

    /// <summary>Measures store direct cas for the configured benchmark input.</summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [Benchmark]
    public bool StoreDirectCas()
    {
        var stored = _storeDirect.TryStoreDirect(_candidate, out var index);
        if (stored)
            _storeDirect.Remove(index);
        return stored;
    }

    /// <summary>Provides probe item functionality.</summary>
    public sealed class ProbeItem
    {
    }

    /// <summary>Provides probe slots functionality.</summary>
    private sealed class ProbeSlots
    {
        private const int Mask = 31;
        private const int Increment = unchecked((int)0x9E3779B9);
        private readonly ProbeItem?[] _slots = new ProbeItem?[32];
        private int _probe;

        /// <summary>Initializes a new instance of <c>ProbeSlots</c>.</summary>
        /// <param name="occupied">The occupied used by this operation.</param>
        internal ProbeSlots(int occupied)
        {
            for (var i = 0; i < occupied; i++)
                _slots[i] = new ProbeItem();
            _probe = Environment.CurrentManagedThreadId * Increment;
        }

        /// <summary>Attempts to take pre read and reports whether it succeeded.</summary>
        /// <param name="takenIndex">Receives the taken index produced by the operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal ProbeItem? TryTakePreRead(out int takenIndex)
        {
            var start = NextProbe();
            for (var offset = 0; offset < _slots.Length; offset++)
            {
                var index = (start + offset) & Mask;
                ref var slot = ref _slots[index];
                if (Volatile.Read(ref slot) is null)
                    continue;
                var item = Interlocked.Exchange(ref slot, null);
                if (item is not null)
                {
                    takenIndex = index;
                    return item;
                }
            }
            takenIndex = -1;
            return null;
        }

        /// <summary>Attempts to take direct and reports whether it succeeded.</summary>
        /// <param name="takenIndex">Receives the taken index produced by the operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal ProbeItem? TryTakeDirect(out int takenIndex)
        {
            var start = NextProbe();
            for (var offset = 0; offset < _slots.Length; offset++)
            {
                var index = (start + offset) & Mask;
                var item = Interlocked.Exchange(ref _slots[index], null);
                if (item is not null)
                {
                    takenIndex = index;
                    return item;
                }
            }
            takenIndex = -1;
            return null;
        }

        /// <summary>Attempts to store pre read and reports whether it succeeded.</summary>
        /// <param name="item">The entry to process.</param>
        /// <param name="storedIndex">Receives the stored index produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool TryStorePreRead(ProbeItem item, out int storedIndex)
        {
            var start = NextProbe();
            for (var offset = 0; offset < _slots.Length; offset++)
            {
                var index = (start + offset) & Mask;
                ref var slot = ref _slots[index];
                if (Volatile.Read(ref slot) is not null)
                    continue;
                if (Interlocked.CompareExchange(ref slot, item, null) is null)
                {
                    storedIndex = index;
                    return true;
                }
            }
            storedIndex = -1;
            return false;
        }

        /// <summary>Attempts to store direct and reports whether it succeeded.</summary>
        /// <param name="item">The entry to process.</param>
        /// <param name="storedIndex">Receives the stored index produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool TryStoreDirect(ProbeItem item, out int storedIndex)
        {
            var start = NextProbe();
            for (var offset = 0; offset < _slots.Length; offset++)
            {
                var index = (start + offset) & Mask;
                if (Interlocked.CompareExchange(ref _slots[index], item, null) is null)
                {
                    storedIndex = index;
                    return true;
                }
            }
            storedIndex = -1;
            return false;
        }

        /// <summary>Measures restore for the configured benchmark input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="item">The entry to process.</param>
        internal void Restore(int index, ProbeItem item) => Volatile.Write(ref _slots[index], item);
        /// <summary>Removes the entry matching the supplied key or value.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        internal void Remove(int index) => Volatile.Write(ref _slots[index], null);

        /// <summary>Measures next probe for the configured benchmark input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NextProbe()
        {
            _probe = unchecked(_probe + Increment);
            return _probe;
        }
    }
}

/// <summary>Provides buffer writer pool contention benchmark functionality.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BufferWriterPoolContentionBenchmark
{
    private const int TotalPairs = 1_048_576;

    /// <summary>Gets or sets the thread count.</summary>
    [Params(2, 4, 8, 16, 32)]
    public int ThreadCount { get; set; }

    private Thread[] _workers = null!;
    private Barrier _barrier = null!;
    private CountdownEvent _ready = null!;
    private volatile bool _stop;
    private int _mode;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _barrier = new Barrier(ThreadCount + 1);
        _ready = new CountdownEvent(ThreadCount);
        _workers = new Thread[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            _workers[i] = new Thread(Worker) { IsBackground = true };
            _workers[i].Start();
        }
        _ready.Wait();
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _stop = true;
        _barrier.SignalAndWait();
        foreach (var worker in _workers)
            worker.Join();
        _barrier.Dispose();
        _ready.Dispose();
    }

    /// <summary>Measures legacy ring nested for the configured benchmark input.</summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = TotalPairs)]
    public void LegacyRingNested() => Run(0);

    /// <summary>Measures atomic slots nested for the configured benchmark input.</summary>
    [Benchmark(OperationsPerInvoke = TotalPairs)]
    public void AtomicSlotsNested() => Run(1);

    /// <summary>Measures run for the configured benchmark input.</summary>
    /// <param name="mode">The mode used by this operation.</param>
    private void Run(int mode)
    {
        Volatile.Write(ref _mode, mode);
        _barrier.SignalAndWait();
        _barrier.SignalAndWait();
    }

    /// <summary>Measures worker for the configured benchmark input.</summary>
    private void Worker()
    {
        WarmWorkerCaches();
        _ready.Signal();

        while (true)
        {
            _barrier.SignalAndWait();
            if (_stop)
                break;

            var iterations = TotalPairs / (ThreadCount * 2);
            if (Volatile.Read(ref _mode) == 0)
            {
                for (var i = 0; i < iterations; i++)
                {
                    var first = LegacyRingWriterPool.Rent();
                    var second = LegacyRingWriterPool.Rent();
                    LegacyRingWriterPool.Return(second);
                    LegacyRingWriterPool.Return(first);
                }
            }
            else
            {
                for (var i = 0; i < iterations; i++)
                {
                    var first = LuminBufferWriterPool.Rent();
                    var second = LuminBufferWriterPool.Rent();
                    LuminBufferWriterPool.Return(second);
                    LuminBufferWriterPool.Return(first);
                }
            }

            _barrier.SignalAndWait();
        }

        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Measures warm worker caches for the configured benchmark input.</summary>
    private static void WarmWorkerCaches()
    {
        for (var i = 0; i < 20_000; i++)
        {
            var legacyFirst = LegacyRingWriterPool.Rent();
            var legacySecond = LegacyRingWriterPool.Rent();
            LegacyRingWriterPool.Return(legacySecond);
            LegacyRingWriterPool.Return(legacyFirst);

            var atomicFirst = LuminBufferWriterPool.Rent();
            var atomicSecond = LuminBufferWriterPool.Rent();
            LuminBufferWriterPool.Return(atomicSecond);
            LuminBufferWriterPool.Return(atomicFirst);
        }
    }
}

/// <summary>Provides double tls writer pool functionality.</summary>
internal static class DoubleTlsWriterPool
{
    [ThreadStatic] private static LuminBufferWriter? t_first;
    [ThreadStatic] private static LuminBufferWriter? t_second;

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LuminBufferWriter Rent()
    {
        var writer = t_first;
        if (writer is not null)
        {
            t_first = null;
            return writer;
        }
        writer = t_second;
        if (writer is not null)
        {
            t_second = null;
            return writer;
        }
        return new LuminBufferWriter(true);
    }

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(LuminBufferWriter writer)
    {
        if (writer.TotalLength >= LuminBufferWriterPool.MaxPooledBufferSize)
        {
            writer.Dispose();
            return;
        }

        writer.ResetCore();
        if (t_first is null)
        {
            t_first = writer;
            return;
        }
        if (t_second is null)
        {
            t_second = writer;
            return;
        }
        writer.Dispose();
    }

    /// <summary>Measures clear thread local cache for the configured benchmark input.</summary>
    internal static void ClearThreadLocalCache()
    {
        var first = t_first;
        var second = t_second;
        t_first = t_second = null;
        first?.Dispose();
        second?.Dispose();
    }
}

/// <summary>Provides legacy ring writer pool functionality.</summary>
internal static class LegacyRingWriterPool
{
    private const int Capacity = LuminBufferWriterPool.MaxPoolSize;
    private static readonly LegacyRing s_ring = new(Capacity);
    private static bool s_multiplePools;

    [ThreadStatic] private static LuminBufferWriter? t_first;

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LuminBufferWriter Rent()
    {
        var writer = t_first;
        if (writer is not null && !Volatile.Read(ref s_multiplePools))
        {
            t_first = null;
            return writer;
        }
        return RentSlow();
    }

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter RentSlow() => s_ring.TryTake() ?? new LuminBufferWriter(true);

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(LuminBufferWriter writer)
    {
        if (writer.TotalLength >= LuminBufferWriterPool.MaxPooledBufferSize)
        {
            writer.Dispose();
            return;
        }

        writer.ResetCore();
        if (t_first is null && !Volatile.Read(ref s_multiplePools))
        {
            t_first = writer;
            return;
        }
        ReturnSlow(writer);
    }

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReturnSlow(LuminBufferWriter writer)
    {
        if (!s_ring.TryStore(writer))
            writer.Dispose();
    }

    /// <summary>Measures clear thread local cache for the configured benchmark input.</summary>
    internal static void ClearThreadLocalCache()
    {
        var item = t_first;
        t_first = null;
        item?.Dispose();
    }

    /// <summary>Provides legacy ring functionality.</summary>
    private sealed class LegacyRing
    {
        private readonly Slot[] _slots = new Slot[Capacity];
        private PaddedCounter _enqueue;
        private PaddedCounter _dequeue;

        /// <summary>Initializes a new instance of <c>LegacyRing</c>.</summary>
        /// <param name="capacity">The requested storage capacity.</param>
        internal LegacyRing(int capacity)
        {
            for (var i = 0; i < capacity; i++)
                _slots[i].Sequence = i;
        }

        /// <summary>Attempts to store and reports whether it succeeded.</summary>
        /// <param name="item">The entry to process.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        internal bool TryStore(LuminBufferWriter item)
        {
            var position = Volatile.Read(ref _enqueue.Value);
            while (true)
            {
                ref var slot = ref _slots[(int)position & (Capacity - 1)];
                var difference = Volatile.Read(ref slot.Sequence) - position;
                if (difference == 0)
                {
                    if (Interlocked.CompareExchange(ref _enqueue.Value, position + 1, position) == position)
                    {
                        slot.Item = item;
                        Volatile.Write(ref slot.Sequence, position + 1);
                        return true;
                    }
                }
                else if (difference < 0)
                {
                    return false;
                }
                position = Volatile.Read(ref _enqueue.Value);
            }
        }

        /// <summary>Attempts to take and reports whether it succeeded.</summary>
        /// <returns>The result of the operation.</returns>
        internal LuminBufferWriter? TryTake()
        {
            var position = Volatile.Read(ref _dequeue.Value);
            while (true)
            {
                ref var slot = ref _slots[(int)position & (Capacity - 1)];
                var difference = Volatile.Read(ref slot.Sequence) - (position + 1);
                if (difference == 0)
                {
                    if (Interlocked.CompareExchange(ref _dequeue.Value, position + 1, position) == position)
                    {
                        var item = slot.Item;
                        slot.Item = null;
                        Volatile.Write(ref slot.Sequence, position + Capacity);
                        return item;
                    }
                }
                else if (difference < 0)
                {
                    return null;
                }
                position = Volatile.Read(ref _dequeue.Value);
            }
        }
    }

    /// <summary>Provides slot functionality.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Slot
    {
#pragma warning disable CS0169
        private long _pad0, _pad1, _pad2, _pad3, _pad4, _pad5, _pad6;
        internal long Sequence;
        internal LuminBufferWriter? Item;
        private long _pad8, _pad9, _pad10, _pad11, _pad12, _pad13, _pad14;
#pragma warning restore CS0169
    }

    /// <summary>Provides padded counter functionality.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PaddedCounter
    {
#pragma warning disable CS0169
        private long _pad0, _pad1, _pad2, _pad3, _pad4, _pad5, _pad6;
        internal long Value;
        private long _pad8, _pad9, _pad10, _pad11, _pad12, _pad13, _pad14;
#pragma warning restore CS0169
    }
}
