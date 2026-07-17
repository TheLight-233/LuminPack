using System.Runtime.CompilerServices;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class ObjectPoolBenchmark
{
    private const int BurstSize = 64;
    private const int BurstIterations = 64;
    private const int ConcurrentWorkers = 8;
    private const int ConcurrentIterations = 4_000;

    private readonly ObjectPool<BenchmarkPooledObject> _pool = new(256);
    private readonly LegacyObjectPool _legacyPool = new(256);
    private readonly BenchmarkPooledObject[] _newBurst = new BenchmarkPooledObject[BurstSize];
    private readonly BenchmarkPooledObject[] _legacyBurst = new BenchmarkPooledObject[BurstSize];

    public static void RunQuick()
    {
        const int hotIterations = 20_000_000;
        const int burstInvocations = 2_000;
        var benchmark = new ObjectPoolBenchmark();
        benchmark.Setup();

        for (int i = 0; i < 100_000; i++)
        {
            benchmark.LegacyThreadLocalRoundTrip();
            benchmark.StripedThreadLocalRoundTrip();
        }

        long checksum = 0;
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < hotIterations; i++)
            checksum ^= RuntimeHelpers.GetHashCode(benchmark.LegacyThreadLocalRoundTrip());
        stopwatch.Stop();
        PrintQuickResult("Legacy TLS round-trip", stopwatch.Elapsed, hotIterations);

        stopwatch.Restart();
        for (int i = 0; i < hotIterations; i++)
            checksum ^= RuntimeHelpers.GetHashCode(benchmark.StripedThreadLocalRoundTrip());
        stopwatch.Stop();
        PrintQuickResult("Striped TLS round-trip", stopwatch.Elapsed, hotIterations);

        stopwatch.Restart();
        for (int i = 0; i < burstInvocations; i++)
            benchmark.LegacySharedBurst();
        stopwatch.Stop();
        PrintQuickResult("Legacy shared burst", stopwatch.Elapsed, burstInvocations * BurstSize * BurstIterations);

        stopwatch.Restart();
        for (int i = 0; i < burstInvocations; i++)
            benchmark.StripedSharedBurst();
        stopwatch.Stop();
        PrintQuickResult("Striped shared burst", stopwatch.Elapsed, burstInvocations * BurstSize * BurstIterations);

        stopwatch.Restart();
        int legacyInvalidRentals = RunLegacyConcurrentBurst(benchmark);
        stopwatch.Stop();
        PrintQuickResult(
            "Legacy concurrent burst",
            stopwatch.Elapsed,
            ConcurrentWorkers * ConcurrentIterations * BurstSize);

        stopwatch.Restart();
        RunStripedConcurrentBurst(benchmark);
        stopwatch.Stop();
        PrintQuickResult(
            "Striped concurrent burst",
            stopwatch.Elapsed,
            ConcurrentWorkers * ConcurrentIterations * BurstSize);

        Console.WriteLine($"Legacy invalid concurrent rentals: {legacyInvalidRentals}");

        GC.KeepAlive(checksum);
    }

    private static void PrintQuickResult(string name, TimeSpan elapsed, long operations)
    {
        double nanoseconds = elapsed.TotalNanoseconds / operations;
        double millionOperationsPerSecond = operations / elapsed.TotalSeconds / 1_000_000d;
        Console.WriteLine($"{name,-28} {nanoseconds,8:F2} ns/op  {millionOperationsPerSecond,8:F2} M ops/s");
    }

    private static int RunLegacyConcurrentBurst(ObjectPoolBenchmark benchmark)
    {
        int invalidRentals = 0;
        Parallel.For(0, ConcurrentWorkers, _ =>
        {
            var items = new BenchmarkPooledObject[BurstSize];
            for (int iteration = 0; iteration < ConcurrentIterations; iteration++)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    BenchmarkPooledObject? item = benchmark._legacyPool.Rent();
                    if (item is null)
                    {
                        Interlocked.Increment(ref invalidRentals);
                        item = new BenchmarkPooledObject();
                    }
                    items[i] = item;
                }

                for (int i = 0; i < items.Length; i++)
                    benchmark._legacyPool.Return(items[i]);
            }
        });
        return invalidRentals;
    }

    private static void RunStripedConcurrentBurst(ObjectPoolBenchmark benchmark)
    {
        Parallel.For(0, ConcurrentWorkers, _ =>
        {
            var items = new BenchmarkPooledObject[BurstSize];
            for (int iteration = 0; iteration < ConcurrentIterations; iteration++)
            {
                for (int i = 0; i < items.Length; i++)
                    items[i] = benchmark._pool.Rent();

                for (int i = 0; i < items.Length; i++)
                    benchmark._pool.Return(items[i]);
            }
        });
    }

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkPooledObject newItem = _pool.Rent();
        _pool.Return(newItem);

        BenchmarkPooledObject legacyItem = _legacyPool.Rent();
        _legacyPool.Return(legacyItem);
    }

    [Benchmark(Baseline = true)]
    public BenchmarkPooledObject LegacyThreadLocalRoundTrip()
    {
        BenchmarkPooledObject item = _legacyPool.Rent();
        _legacyPool.Return(item);
        return item;
    }

    [Benchmark]
    public BenchmarkPooledObject StripedThreadLocalRoundTrip()
    {
        BenchmarkPooledObject item = _pool.Rent();
        _pool.Return(item);
        return item;
    }

    [Benchmark(OperationsPerInvoke = BurstSize * BurstIterations)]
    public void LegacySharedBurst()
    {
        for (int iteration = 0; iteration < BurstIterations; iteration++)
        {
            for (int i = 0; i < BurstSize; i++)
                _legacyBurst[i] = _legacyPool.Rent();

            for (int i = 0; i < BurstSize; i++)
                _legacyPool.Return(_legacyBurst[i]);
        }
    }

    [Benchmark(OperationsPerInvoke = BurstSize * BurstIterations)]
    public void StripedSharedBurst()
    {
        for (int iteration = 0; iteration < BurstIterations; iteration++)
        {
            for (int i = 0; i < BurstSize; i++)
                _newBurst[i] = _pool.Rent();

            for (int i = 0; i < BurstSize; i++)
                _pool.Return(_newBurst[i]);
        }
    }

    public sealed class BenchmarkPooledObject : IDisposable, IPooledObjectPolicy<BenchmarkPooledObject>
    {
        public static BenchmarkPooledObject Create() => new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Return(BenchmarkPooledObject obj) => true;

        public void Dispose()
        {
        }
    }

    // The previous implementation is retained only inside the benchmark so changes
    // can be measured against the exact former algorithm.
    private sealed class LegacyObjectPool
    {
        [ThreadStatic]
        private static BenchmarkPooledObject? s_item;

        private readonly BenchmarkPooledObject?[] _items;
        private readonly int _maxSize;
        private int _count;

        internal LegacyObjectPool(int maxSize)
        {
            _items = new BenchmarkPooledObject[maxSize];
            _maxSize = maxSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal BenchmarkPooledObject Rent()
        {
            BenchmarkPooledObject? local = s_item;
            if (local is not null)
            {
                s_item = null;
                return local;
            }

            while (true)
            {
                int count = Volatile.Read(ref _count);
                if (count <= 0)
                    return new BenchmarkPooledObject();

                if (Interlocked.CompareExchange(ref _count, count - 1, count) == count)
                {
                    BenchmarkPooledObject item = _items[count - 1]!;
                    _items[count - 1] = null;
                    return item;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Return(BenchmarkPooledObject item)
        {
            if (s_item is null)
            {
                s_item = item;
                return;
            }

            while (true)
            {
                int count = Volatile.Read(ref _count);
                if (count >= _maxSize)
                    return;

                if (Interlocked.CompareExchange(ref _count, count + 1, count) == count)
                {
                    _items[count] = item;
                    return;
                }
            }
        }
    }
}
