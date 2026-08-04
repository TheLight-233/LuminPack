using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using LuminPack.Utility;

namespace LuminPackBenchmark;

internal static class BufferWriterPoolJitProbe
{
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter LegacyRoundTrip()
    {
        var writer = LegacyRingWriterPool.Rent();
        LegacyRingWriterPool.Return(writer);
        return writer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter DedicatedRoundTrip()
    {
        var writer = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.Return(writer);
        return writer;
    }
}

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 4, printSource: true, printInstructionAddresses: true)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 5)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BufferWriterPoolHotPathBenchmark
{
    private const int Operations = 1_000_000;

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

    [GlobalCleanup]
    public void Cleanup()
    {
        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
        DoubleTlsWriterPool.ClearThreadLocalCache();
    }

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

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 5, printSource: true, printInstructionAddresses: true)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class BufferWriterPoolAssemblyBenchmark
{
    [GlobalSetup]
    public void Setup()
    {
        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
        LegacyRingWriterPool.Return(LegacyRingWriterPool.Rent());
        LuminBufferWriterPool.Return(LuminBufferWriterPool.Rent());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        LegacyRingWriterPool.ClearThreadLocalCache();
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    [Benchmark(Baseline = true)]
    public LuminBufferWriter LegacyTlsRoundTrip()
    {
        var writer = LegacyRingWriterPool.Rent();
        LegacyRingWriterPool.Return(writer);
        return writer;
    }

    [Benchmark]
    public LuminBufferWriter DedicatedTlsRoundTrip()
    {
        var writer = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.Return(writer);
        return writer;
    }
}

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3, printSource: true, printInstructionAddresses: true)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class AtomicSlotProbeBenchmark
{
    [Params(0, 8, 16, 24, 32)]
    public int Occupied { get; set; }

    private ProbeSlots _takePreRead = null!;
    private ProbeSlots _takeDirect = null!;
    private ProbeSlots _storePreRead = null!;
    private ProbeSlots _storeDirect = null!;
    private readonly ProbeItem _candidate = new();

    [GlobalSetup]
    public void Setup()
    {
        _takePreRead = new ProbeSlots(Occupied);
        _takeDirect = new ProbeSlots(Occupied);
        _storePreRead = new ProbeSlots(Occupied);
        _storeDirect = new ProbeSlots(Occupied);
    }

    [Benchmark(Baseline = true)]
    public ProbeItem? TakeVolatileReadThenExchange()
    {
        var item = _takePreRead.TryTakePreRead(out var index);
        if (item is not null)
            _takePreRead.Restore(index, item);
        return item;
    }

    [Benchmark]
    public ProbeItem? TakeDirectExchange()
    {
        var item = _takeDirect.TryTakeDirect(out var index);
        if (item is not null)
            _takeDirect.Restore(index, item);
        return item;
    }

    [Benchmark]
    public bool StoreVolatileReadThenCas()
    {
        var stored = _storePreRead.TryStorePreRead(_candidate, out var index);
        if (stored)
            _storePreRead.Remove(index);
        return stored;
    }

    [Benchmark]
    public bool StoreDirectCas()
    {
        var stored = _storeDirect.TryStoreDirect(_candidate, out var index);
        if (stored)
            _storeDirect.Remove(index);
        return stored;
    }

    public sealed class ProbeItem
    {
    }

    private sealed class ProbeSlots
    {
        private const int Mask = 31;
        private const int Increment = unchecked((int)0x9E3779B9);
        private readonly ProbeItem?[] _slots = new ProbeItem?[32];
        private int _probe;

        internal ProbeSlots(int occupied)
        {
            for (var i = 0; i < occupied; i++)
                _slots[i] = new ProbeItem();
            _probe = Environment.CurrentManagedThreadId * Increment;
        }

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

        internal void Restore(int index, ProbeItem item) => Volatile.Write(ref _slots[index], item);
        internal void Remove(int index) => Volatile.Write(ref _slots[index], null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NextProbe()
        {
            _probe = unchecked(_probe + Increment);
            return _probe;
        }
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BufferWriterPoolContentionBenchmark
{
    private const int TotalPairs = 1_048_576;

    [Params(2, 4, 8, 16, 32)]
    public int ThreadCount { get; set; }

    private Thread[] _workers = null!;
    private Barrier _barrier = null!;
    private CountdownEvent _ready = null!;
    private volatile bool _stop;
    private int _mode;

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

    [Benchmark(Baseline = true, OperationsPerInvoke = TotalPairs)]
    public void LegacyRingNested() => Run(0);

    [Benchmark(OperationsPerInvoke = TotalPairs)]
    public void AtomicSlotsNested() => Run(1);

    private void Run(int mode)
    {
        Volatile.Write(ref _mode, mode);
        _barrier.SignalAndWait();
        _barrier.SignalAndWait();
    }

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

internal static class DoubleTlsWriterPool
{
    [ThreadStatic] private static LuminBufferWriter? t_first;
    [ThreadStatic] private static LuminBufferWriter? t_second;

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

    internal static void ClearThreadLocalCache()
    {
        var first = t_first;
        var second = t_second;
        t_first = t_second = null;
        first?.Dispose();
        second?.Dispose();
    }
}

internal static class LegacyRingWriterPool
{
    private const int Capacity = LuminBufferWriterPool.MaxPoolSize;
    private static readonly LegacyRing s_ring = new(Capacity);
    private static bool s_multiplePools;

    [ThreadStatic] private static LuminBufferWriter? t_first;

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminBufferWriter RentSlow() => s_ring.TryTake() ?? new LuminBufferWriter(true);

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReturnSlow(LuminBufferWriter writer)
    {
        if (!s_ring.TryStore(writer))
            writer.Dispose();
    }

    internal static void ClearThreadLocalCache()
    {
        var item = t_first;
        t_first = null;
        item?.Dispose();
    }

    private sealed class LegacyRing
    {
        private readonly Slot[] _slots = new Slot[Capacity];
        private PaddedCounter _enqueue;
        private PaddedCounter _dequeue;

        internal LegacyRing(int capacity)
        {
            for (var i = 0; i < capacity; i++)
                _slots[i].Sequence = i;
        }

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
