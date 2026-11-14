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
        RunCase(results, nameof(SharedCacheNeverPublishesNullOrDoubleRents), SharedCacheNeverPublishesNullOrDoubleRents);
        RunCase(results, nameof(SingleSlotAndFullPoolBehaveCorrectly), SingleSlotAndFullPoolBehaveCorrectly);
        RunCase(results, nameof(QuiescentResizeUpdatesCapacityAndRetainsItems), QuiescentResizeUpdatesCapacityAndRetainsItems);
        RunCase(results, nameof(ZeroSharedCapacityKeepsOnlyOneThreadItem), ZeroSharedCapacityKeepsOnlyOneThreadItem);
        RunCase(results, nameof(RejectedItemsNeverReachEitherCache), RejectedItemsNeverReachEitherCache);
        RunCase(results, nameof(ClearDetachesBeforeDisposalCallbacks), ClearDetachesBeforeDisposalCallbacks);
        RunCase(results, nameof(ResizePublishesBeforeDisposalAndAggregatesFailures), ResizePublishesBeforeDisposalAndAggregatesFailures);
        RunCase(results, nameof(TerminatedThreadDoesNotRootItsCachedItem), TerminatedThreadDoesNotRootItsCachedItem);
        RunCase(results, nameof(InvalidArgumentsLeavePoolUsable), InvalidArgumentsLeavePoolUsable);
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

    /// <summary>Verifies shared storage never publishes null or double rents.</summary>
    private static void SharedCacheNeverPublishesNullOrDoubleRents()
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
        pool.Return(new PoolItem()); // one shared slot
        pool.Return(new PoolItem()); // full: dispose

        Assert(pool.AvailableCount == 1, "A capacity-one shared cache reported an invalid count.");
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

    /// <summary>Checks that zero capacity disables shared retention without changing the TLS contract.</summary>
    private static void ZeroSharedCapacityKeepsOnlyOneThreadItem()
    {
        ObjectPool<CallbackItem>.ClearThreadLocalCache();
        var pool = new ObjectPool<CallbackItem>(0);
        var retained = new CallbackItem();
        var rejected = new CallbackItem();
        pool.Return(retained);
        pool.Return(rejected);
        Assert(pool.AvailableCount == 0 && pool.GetAllItems().Length == 0, "Zero capacity retained shared items.");
        Assert(rejected.Disposals == 1 && ReferenceEquals(pool.Rent(), retained), "Zero capacity changed TLS or disposal behavior.");
        retained.Dispose();
    }

    /// <summary>Checks policy rejection before either cache publishes an instance.</summary>
    private static void RejectedItemsNeverReachEitherCache()
    {
        ObjectPool<CallbackItem>.ClearThreadLocalCache();
        var pool = new ObjectPool<CallbackItem>(3);
        var item = new CallbackItem { Accept = false };
        pool.Return(item);
        Assert(item.Disposals == 1 && pool.AvailableCount == 0, "Policy rejection did not dispose the item.");
        var fresh = pool.Rent();
        Assert(!ReferenceEquals(item, fresh), "A policy-rejected item was rented again.");
        fresh.Dispose();
    }

    /// <summary>Checks that a reentrant or throwing disposer cannot leave a disposed item in TLS.</summary>
    private static void ClearDetachesBeforeDisposalCallbacks()
    {
        ObjectPool<CallbackItem>.ClearThreadLocalCache();
        var pool = new ObjectPool<CallbackItem>(1);
        var replacement = new CallbackItem();
        var item = new CallbackItem
        {
            OnDispose = () =>
            {
                ObjectPool<CallbackItem>.ClearThreadLocalCache();
                pool.Return(replacement);
                throw new InvalidOperationException("Disposal probe.");
            }
        };
        pool.Return(item);
        try
        {
            ObjectPool<CallbackItem>.ClearThreadLocalCache();
            Assert(false, "The disposal exception was lost.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "Disposal probe.") { }
        Assert(item.Disposals == 1 && ReferenceEquals(pool.Rent(), replacement), "Clear lost a reentrant return or disposed twice.");
        replacement.Dispose();
    }

    /// <summary>Checks resize ownership when excess disposers reenter and throw.</summary>
    private static void ResizePublishesBeforeDisposalAndAggregatesFailures()
    {
        ObjectPool<CallbackItem>.ClearThreadLocalCache();
        var pool = new ObjectPool<CallbackItem>(4);
        var local = new CallbackItem();
        pool.Return(local);
        var shared = Enumerable.Range(0, 4).Select(_ => new CallbackItem()).ToArray();
        foreach (var item in shared)
        {
            item.OnDispose = () =>
            {
                Assert(pool.MaxSize == 1, "Dispose observed the old resize storage.");
                var rented = pool.Rent();
                pool.Return(rented);
                throw new InvalidOperationException("Resize disposal probe.");
            };
            pool.Return(item);
        }
        try
        {
            pool.Resize(1);
            Assert(false, "Resize lost disposal failures.");
        }
        catch (AggregateException ex)
        {
            Assert(ex.InnerExceptions.Count == 3, "Resize stopped before draining every excess item.");
            Assert(ex.InnerExceptions.All(error => error.Message == "Resize disposal probe."), "A reentrant disposer observed invalid state.");
        }
        Assert(shared.Sum(item => item.Disposals) == 3 && pool.AvailableCount == 1, "Resize lost or disposed retained storage.");
        var survivor = pool.GetAllItems().Single()!;
        Assert(survivor.Disposals == 0, "Resize retained a disposed object.");
        survivor.OnDispose = null;
        pool.Resize(0);
        ObjectPool<CallbackItem>.ClearThreadLocalCache();
        Assert(shared.All(item => item.Disposals == 1) && local.Disposals == 1, "Resize cleanup did not release each item once.");
    }

    /// <summary>Checks that managed TLS does not permanently root an item after its thread terminates.</summary>
    private static void TerminatedThreadDoesNotRootItsCachedItem()
    {
        var weak = CreateTerminatedThreadCache();
        for (var i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert(!weak.IsAlive, "A terminated thread still rooted its cached object.");
    }

    /// <summary>Creates a thread-local item without retaining a strong reference in the test frame.</summary>
    /// <returns>A weak reference to the returned item.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateTerminatedThreadCache()
    {
        WeakReference? weak = null;
        var thread = new Thread(() =>
        {
            var pool = new ObjectPool<CallbackItem>(0);
            var item = pool.Rent();
            weak = new WeakReference(item);
            pool.Return(item);
        });
        thread.Start();
        Assert(thread.Join(TimeSpan.FromSeconds(10)), "The TLS probe thread did not terminate.");
        return weak!;
    }

    /// <summary>Checks argument validation before a pool or policy can be mutated.</summary>
    private static void InvalidArgumentsLeavePoolUsable()
    {
        ObjectPool<CallbackItem>.ClearThreadLocalCache();
        try { _ = new ObjectPool<CallbackItem>(-1); Assert(false, "Negative capacity was accepted."); }
        catch (ArgumentOutOfRangeException ex) { Assert(ex.ParamName == "maxSize", "Incorrect capacity parameter."); }
        var pool = new ObjectPool<CallbackItem>(3);
        try { pool.Return(null!); Assert(false, "Null return was accepted."); }
        catch (ArgumentNullException ex) { Assert(ex.ParamName == "item", "Incorrect return parameter."); }
        try { pool.Resize(-1); Assert(false, "Negative resize was accepted."); }
        catch (ArgumentOutOfRangeException ex) { Assert(ex.ParamName == "newSize", "Incorrect resize parameter."); }
        var item = pool.Rent();
        pool.Return(item);
        Assert(ReferenceEquals(pool.Rent(), item) && pool.MaxSize == 3, "Validation mutated pool state.");
        item.Dispose();
    }

    /// <summary>Provides observable policy and disposal callbacks.</summary>
    private sealed class CallbackItem : IPooledObjectPolicy<CallbackItem>, IDisposable
    {
        internal bool Accept = true;
        internal int Disposals;
        internal Action? OnDispose;

        /// <summary>Creates an instance for a cold rental.</summary>
        /// <returns>A new, exclusively owned instance.</returns>
        public static CallbackItem Create() => new();

        /// <summary>Applies the configured retention decision.</summary>
        /// <param name="obj">The returned instance.</param>
        /// <returns>The instance's retention decision.</returns>
        public static bool Return(CallbackItem obj) => obj.Accept;

        /// <summary>Records disposal before invoking the configured callback.</summary>
        public void Dispose()
        {
            Disposals++;
            OnDispose?.Invoke();
        }
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
