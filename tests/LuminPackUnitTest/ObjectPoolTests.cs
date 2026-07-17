using System.Collections.Concurrent;
using LuminPack.Utility;

namespace LuminPackUnitTest;

internal static class ObjectPoolTests
{
    internal static void RunAll(List<string> results)
    {
        Run(results, nameof(ThreadLocalCacheIsIsolatedPerPool), ThreadLocalCacheIsIsolatedPerPool);
        Run(results, nameof(ResizePreservesCapacityAndLiveObjects), ResizePreservesCapacityAndLiveObjects);
        Run(results, nameof(RejectedObjectsAreDisposed), RejectedObjectsAreDisposed);
        Run(results, nameof(ConcurrentRentNeverPublishesTheSameObjectTwice), ConcurrentRentNeverPublishesTheSameObjectTwice);
    }

    private static void ThreadLocalCacheIsIsolatedPerPool()
    {
        ObjectPool<PoolProbe>.ClearThreadLocalCache();
        var firstPool = new ObjectPool<PoolProbe>(8);
        var secondPool = new ObjectPool<PoolProbe>(8);

        PoolProbe first = firstPool.Rent();
        firstPool.Return(first);

        PoolProbe second = secondPool.Rent();
        Assert(!ReferenceEquals(first, second), "two pools of the same type exchanged a TLS object");
        secondPool.Return(second);

        PoolProbe firstAgain = firstPool.Rent();
        Assert(ReferenceEquals(first, firstAgain), "the first pool did not recover its own TLS object");

        firstPool.Return(firstAgain);
        ObjectPool<PoolProbe>.ClearThreadLocalCache();
        firstPool.Resize(0);
        secondPool.Resize(0);
    }

    private static void ResizePreservesCapacityAndLiveObjects()
    {
        ObjectPool<PoolProbe>.ClearThreadLocalCache();
        var pool = new ObjectPool<PoolProbe>(8);
        var objects = new PoolProbe[6];

        for (int i = 0; i < objects.Length; i++)
            objects[i] = pool.Rent();

        for (int i = 0; i < objects.Length; i++)
            pool.Return(objects[i]);

        Assert(pool.AvailableCount == 5, "unexpected shared count before resize");
        pool.Resize(2);
        Assert(pool.MaxSize == 2, "MaxSize did not follow Resize");
        Assert(pool.AvailableCount == 2, "shrink did not enforce the new capacity");
        Assert(pool.GetAllItems().Length == 2, "snapshot does not match live shared objects");

        pool.Resize(16);
        Assert(pool.MaxSize == 16, "grow did not update capacity");
        Assert(pool.AvailableCount == 2, "grow lost pooled objects");

        ObjectPool<PoolProbe>.ClearThreadLocalCache();
        pool.Resize(0);
    }

    private static void RejectedObjectsAreDisposed()
    {
        ObjectPool<RejectableProbe>.ClearThreadLocalCache();
        var pool = new ObjectPool<RejectableProbe>(4);
        RejectableProbe item = pool.Rent();
        item.CanReturn = false;

        pool.Return(item);

        Assert(item.IsDisposed, "an object rejected by the policy was not disposed");
        Assert(pool.AvailableCount == 0, "a rejected object entered the shared pool");
    }

    private static void ConcurrentRentNeverPublishesTheSameObjectTwice()
    {
        ObjectPool<PoolProbe>.ClearThreadLocalCache();
        var pool = new ObjectPool<PoolProbe>(256);
        var active = new ConcurrentDictionary<int, byte>();
        int duplicateRentals = 0;
        int workerCount = Math.Max(Environment.ProcessorCount, 4);

        Parallel.For(0, workerCount, workerIndex =>
        {
            for (int i = 0; i < 25_000; i++)
            {
                PoolProbe item = pool.Rent();
                if (!active.TryAdd(item.Id, 0))
                    Interlocked.Increment(ref duplicateRentals);

                Thread.SpinWait(4);

                active.TryRemove(item.Id, out _);
                pool.Return(item);
            }

            GC.KeepAlive(workerIndex);
        });

        Assert(duplicateRentals == 0, $"observed {duplicateRentals} duplicate concurrent rentals");
        Assert(active.IsEmpty, "the active rental tracker did not drain");
        Assert(pool.AvailableCount <= pool.MaxSize, "shared capacity was exceeded");

        ObjectPool<PoolProbe>.ClearThreadLocalCache();
        pool.Resize(0);
    }

    private static void Run(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ ObjectPool.{name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ ObjectPool.{name} - ERROR: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class PoolProbe : IDisposable, IPooledObjectPolicy<PoolProbe>
    {
        private static int s_nextId;

        internal int Id { get; } = Interlocked.Increment(ref s_nextId);

        public static PoolProbe Create() => new();

        public static bool Return(PoolProbe obj) => true;

        public void Dispose()
        {
        }
    }

    private sealed class RejectableProbe : IDisposable, IPooledObjectPolicy<RejectableProbe>
    {
        internal bool CanReturn { get; set; } = true;
        internal bool IsDisposed { get; private set; }

        public static RejectableProbe Create() => new();

        public static bool Return(RejectableProbe obj) => obj.CanReturn;

        public void Dispose() => IsDisposed = true;
    }
}
