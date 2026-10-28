using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>Provides object pool regression test functionality.</summary>
internal static class ObjectPoolRegressionTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(ThreadCacheIsIsolatedPerPool), ThreadCacheIsIsolatedPerPool);
        RunCase(results, nameof(CentralRingNeverPublishesNullOrDoubleRents), CentralRingNeverPublishesNullOrDoubleRents);
        RunCase(results, nameof(SingleSlotAndFullPoolBehaveCorrectly), SingleSlotAndFullPoolBehaveCorrectly);
        RunCase(results, nameof(QuiescentResizeUpdatesCapacityAndRetainsItems), QuiescentResizeUpdatesCapacityAndRetainsItems);
    }

    /// <summary>Verifies thread cache is isolated per pool.</summary>
    private static void ThreadCacheIsIsolatedPerPool()
    {
        ObjectPool<PoolItem>.ClearThreadLocalCache();
        var firstPool = new ObjectPool<PoolItem>(4);
        var secondPool = new ObjectPool<PoolItem>(4);
        var first = new PoolItem { Owner = 1 };
        var second = new PoolItem { Owner = 2 };

        firstPool.Return(first);
        secondPool.Return(second);

        var fromSecond = secondPool.Rent();
        var fromFirst = firstPool.Rent();
        Assert(ReferenceEquals(fromSecond, second) && ReferenceEquals(fromFirst, first),
            "Thread-local caches leaked objects between ObjectPool<T> instances.");

        ObjectPool<PoolItem>.ClearThreadLocalCache();
    }

    /// <summary>Verifies central ring never publishes null or double rents.</summary>
    private static void CentralRingNeverPublishesNullOrDoubleRents()
    {
        ObjectPool<PoolItem>.ClearThreadLocalCache();
        var pool = new ObjectPool<PoolItem>(32);
        var workers = Math.Max(4, Environment.ProcessorCount);
        const int iterations = 100_000;
        long nullRents = 0;
        long doubleRents = 0;

        Parallel.For(0, workers, _ =>
        {
            try
            {
                for (var i = 0; i < iterations; i++)
                {
                    var first = pool.Rent();
                    var second = pool.Rent();
                    if (first is null || second is null)
                    {
                        Interlocked.Increment(ref nullRents);
                        continue;
                    }

                    if (Interlocked.CompareExchange(ref first.InUse, 1, 0) != 0)
                        Interlocked.Increment(ref doubleRents);
                    if (Interlocked.CompareExchange(ref second.InUse, 1, 0) != 0)
                        Interlocked.Increment(ref doubleRents);

                    pool.Return(first);
                    pool.Return(second);
                }
            }
            finally
            {
                ObjectPool<PoolItem>.ClearThreadLocalCache();
            }
        });

        Assert(nullRents == 0, $"The central pool published {nullRents} uninitialized slots.");
        Assert(doubleRents == 0, $"The same pooled object was rented concurrently {doubleRents} times.");
    }

    /// <summary>Verifies single slot and full pool behave correctly.</summary>
    private static void SingleSlotAndFullPoolBehaveCorrectly()
    {
        ObjectPool<PoolItem>.ClearThreadLocalCache();
        PoolItem.DisposeCount = 0;
        var pool = new ObjectPool<PoolItem>(1);
        pool.Return(new PoolItem()); // TLS
        pool.Return(new PoolItem()); // one-slot central ring
        pool.Return(new PoolItem()); // full: dispose

        Assert(pool.AvailableCount == 1, "A capacity-one central ring reported an invalid count.");
        Assert(Volatile.Read(ref PoolItem.DisposeCount) == 1, "A full pool did not dispose the rejected item.");
        Assert(pool.Rent() is not null && pool.Rent() is not null, "A capacity-one pool lost a retained item.");

        ObjectPool<PoolItem>.ClearThreadLocalCache();
    }

    /// <summary>Verifies quiescent resize updates capacity and retains items.</summary>
    private static void QuiescentResizeUpdatesCapacityAndRetainsItems()
    {
        ObjectPool<PoolItem>.ClearThreadLocalCache();
        var pool = new ObjectPool<PoolItem>(4);
        pool.Return(new PoolItem());
        pool.Return(new PoolItem());
        pool.Return(new PoolItem());
        pool.Resize(2);

        Assert(pool.MaxSize == 2, "Resize did not update MaxSize.");
        Assert(pool.AvailableCount == 2, "Resize did not retain the expected central items.");
        Assert(pool.GetAllItems().Length == 2, "GetAllItems returned an invalid lock-free snapshot.");

        ObjectPool<PoolItem>.ClearThreadLocalCache();
    }

    /// <summary>Provides pool item functionality.</summary>
    private sealed class PoolItem : IPooledObjectPolicy<PoolItem>, IDisposable
    {
        internal static int DisposeCount;
        internal int Owner;
        internal int InUse;

        /// <summary>Creates a sample instance for the benchmark.</summary>
        /// <returns>The result of the operation.</returns>
        public static PoolItem Create() => new();

        /// <summary>Returns an instance to the pool for reuse.</summary>
        /// <param name="obj">The obj used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        public static bool Return(PoolItem obj)
        {
            Volatile.Write(ref obj.InUse, 0);
            return true;
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        public void Dispose() => Interlocked.Increment(ref DisposeCount);
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
