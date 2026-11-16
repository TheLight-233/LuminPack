#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.Code;

namespace LuminPack.Utility
{
#if NET8_0_OR_GREATER
    /// <summary>Creates instances and resets them before retention.</summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    internal interface IPooledObjectPolicy<T> where T : class
    {
        /// <summary>Creates an exclusively owned instance on a cache miss.</summary>
        /// <returns>A non-null instance.</returns>
        static abstract T Create();
        /// <summary>Resets an instance before the pool publishes it for reuse.</summary>
        /// <param name="obj">The exclusively owned instance being returned.</param>
        /// <returns>True to retain the instance; false to dispose it.</returns>
        static abstract bool Return(T obj);
    }
#else
    /// <summary>Creates instances and resets them before retention.</summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    internal interface IPooledObjectPolicy<T> where T : class
    {
        /// <summary>Creates an exclusively owned instance on a cache miss.</summary>
        /// <returns>A non-null instance.</returns>
        T Create();
        /// <summary>Resets an instance before the pool publishes it for reuse.</summary>
        /// <param name="obj">The exclusively owned instance being returned.</param>
        /// <returns>True to retain the instance; false to dispose it.</returns>
        bool Return(T obj);
    }
#endif

    /// <summary>
    /// One TLS item per closed T and thread, plus a bounded shared cache.
    /// Rent/Return may run concurrently. Each lease must be returned at most once
    /// to its originating pool and must not be used after Return.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <remarks>Retention order is unspecified. TLS retention lasts until reuse, explicit
    /// clearing on that thread, or thread termination; it does not provide automatic disposal.</remarks>
    internal sealed class ObjectPool<T>
#if NET8_0_OR_GREATER
        where T : class, IPooledObjectPolicy<T>, IDisposable
#else
        where T : class, IDisposable
#endif
    {
        private static int s_nextPoolId;

        [ThreadStatic]
        private static T? s_threadLocalItem;

        [ThreadStatic]
        private static int s_threadLocalPoolId;

        private readonly int _poolId;
        private AtomicSlotPool<T> _shared;

#if !NET8_0_OR_GREATER
        private readonly IPooledObjectPolicy<T> _policy;
#endif

#if !NET8_0_OR_GREATER
        /// <summary>Creates a bounded shared cache with one per-type thread-local fast slot.</summary>
        /// <param name="policy">Creates and resets pooled instances.</param>
        /// <param name="maxSize">
        /// Shared capacity, excluding all TLS items. Zero disables shared
        /// retention, not the TLS cache. Default: 16.
        /// </param>
#else
        /// <summary>Creates a bounded shared cache with one per-type thread-local fast slot.</summary>
        /// <param name="maxSize">
        /// Shared capacity, excluding all TLS items. Zero disables shared
        /// retention, not the TLS cache. Default: 16.
        /// </param>
#endif
        public ObjectPool(
#if !NET8_0_OR_GREATER
            IPooledObjectPolicy<T>? policy,
#endif
            int maxSize = 16)
        {
            if (maxSize < 0)
                LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(maxSize));
#if !NET8_0_OR_GREATER
            if (policy is null)
                LuminPackExceptionHelper.ThrowArgumentNullException(nameof(policy));
            _policy = policy;
#endif
            _poolId = AllocatePoolId();
            _shared = new AtomicSlotPool<T>(maxSize);
        }

        /// <summary>Rents an exclusive instance, creating one only after a cache miss.</summary>
        /// <returns>An instance owned by the caller until it is returned once.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            var item = s_threadLocalItem;
            if (item is not null && s_threadLocalPoolId == _poolId)
            {
                s_threadLocalItem = null;
                return item;
            }

            return RentSlow();
        }

        /// <summary>Tries shared storage before invoking the creation policy.</summary>
        /// <returns>An exclusively owned instance.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T RentSlow()
        {
            var item = Volatile.Read(ref _shared).TryTake();
            if (item is not null)
                return item;
#if NET8_0_OR_GREATER
            return T.Create();
#else
            return _policy.Create();
#endif
        }

        /// <summary>Resets an instance and transfers ownership to the cache or disposes it.</summary>
        /// <param name="item">The non-null instance rented from this pool.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item)
        {
            if (item is null)
                LuminPackExceptionHelper.ThrowArgumentNullException(nameof(item));
#if NET8_0_OR_GREATER
            if (!T.Return(item))
#else
            if (!_policy.Return(item))
#endif
            {
                DisposeRejected(item);
                return;
            }

            if (s_threadLocalItem is null)
            {
                s_threadLocalPoolId = _poolId;
                s_threadLocalItem = item;
                return;
            }

            ReturnSlow(item);
        }

        /// <summary>Publishes an instance to shared storage or disposes it when no slot accepts it.</summary>
        /// <param name="item">The reset, exclusively owned instance.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReturnSlow(T item)
        {
            if (!Volatile.Read(ref _shared).TryStore(item))
                item.Dispose();
        }

        /// <summary>Disposes a policy-rejected instance outside the hot return path.</summary>
        /// <param name="item">The rejected instance.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisposeRejected(T item) => item.Dispose();

        /// <summary>Clears this T's cache on the calling thread only.</summary>
        public static void ClearThreadLocalCache()
        {
            var item = s_threadLocalItem;
            s_threadLocalItem = null;
            s_threadLocalPoolId = 0;
            item?.Dispose();
        }

        /// <summary>Approximate shared count, excluding TLS items.</summary>
        public int AvailableCount => Volatile.Read(ref _shared).ApproximateCount;

        /// <summary>Shared capacity, excluding TLS items.</summary>
        public int MaxSize => Volatile.Read(ref _shared).Capacity;

        /// <summary>
        /// Diagnostic references, not leases or an atomic snapshot. Never use,
        /// mutate, return, or dispose the returned objects.
        /// </summary>
        /// <returns>Observed shared references; thread-local entries are excluded.</returns>
        public T?[] GetAllItems() => Volatile.Read(ref _shared).Snapshot();

        /// <summary>
        /// Quiescent operation: no other pool operation may overlap Resize.
        /// TLS caches are unchanged. Excess items are disposed after publishing
        /// the new storage, so Dispose callbacks can safely reenter the pool.
        /// Disposal failures are aggregated after the old cache is drained.
        /// </summary>
        /// <param name="newSize">The new nonnegative shared capacity.</param>
        public void Resize(int newSize)
        {
            if (newSize < 0)
                LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(newSize));

            var old = Volatile.Read(ref _shared);
            if (old.Capacity == newSize)
                return;

            var replacement = new AtomicSlotPool<T>(newSize);
            for (var retained = 0; retained < newSize; retained++)
            {
                var item = old.TryTake();
                if (item is null)
                    break;
                // The replacement is private and has room under the contract.
                var stored = replacement.TryStore(item);
                Debug.Assert(stored);
            }

            Volatile.Write(ref _shared, replacement);

            List<Exception>? errors = null;
            while (true)
            {
                var item = old.TryTake();
                if (item is null)
                    break;
                try
                {
                    item.Dispose();
                }
                catch (Exception ex)
                {
                    (errors ??= new List<Exception>()).Add(ex);
                }
            }

            if (errors is not null)
                LuminPackExceptionHelper.ThrowPoolDisposalException(errors);
        }

        /// <summary>Allocates an identity that cannot alias a previously created pool.</summary>
        /// <returns>A positive identity unique among pools of this closed type.</returns>
        private static int AllocatePoolId()
        {
            while (true)
            {
                var current = Volatile.Read(ref s_nextPoolId);
                if (current == int.MaxValue)
                    LuminPackExceptionHelper.ThrowInvalidOperationException("Object pool IDs exhausted for this type.");
                var next = current + 1;
                if (Interlocked.CompareExchange(ref s_nextPoolId, next, current) == current)
                    return next;
            }
        }
    }
}
