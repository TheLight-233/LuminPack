#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.Code;

namespace LuminPack.Utility
{
    /// <summary>
    /// Bounded, unordered shared cache. A miss is not an atomic empty/full snapshot.
    /// Hints only select a scan start; ownership lives exclusively in the slots.
    /// </summary>
    /// <typeparam name="T">The reference type retained by this cache.</typeparam>
    internal sealed class AtomicSlotPool<T> where T : class
    {
        private readonly int _capacity;
        private T? _firstItem;
        private readonly T?[] _slots;
        private int _takeHint;
        private int _storeHint;

        /// <summary>Allocates fixed shared storage, with zero disabling retention.</summary>
        /// <param name="capacity">The maximum number of retained references.</param>
        internal AtomicSlotPool(int capacity)
        {
            if (capacity < 0)
                LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(capacity));

            _capacity = capacity;
            _slots = capacity > 1 ? new T?[capacity - 1] : Array.Empty<T?>();
        }

        /// <summary>Gets the maximum number of retained references.</summary>
        internal int Capacity => _capacity;

        /// <summary>Transfers exclusive ownership of an observed item to the caller.</summary>
        /// <returns>An item removed atomically from storage, or null after a bounded scan.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T? TryTake()
        {
            if (Volatile.Read(ref _firstItem) is not null)
            {
                var item = Interlocked.Exchange(ref _firstItem, null);
                if (item is not null)
                    return item;
            }

            return TakeSlow();
        }

        /// <summary>Scans overflow slots once, starting after the previous successful take.</summary>
        /// <returns>An exclusively owned item, or null if no take succeeded.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T? TakeSlow()
        {
            var slots = _slots;
            var length = slots.Length;
            var index = Volatile.Read(ref _takeHint);

            for (var remaining = length; remaining != 0; remaining--)
            {
                ref var slot = ref slots[index];
                if (Volatile.Read(ref slot) is not null)
                {
                    var item = Interlocked.Exchange(ref slot, null);
                    if (item is not null)
                    {
                        Volatile.Write(ref _takeHint, NextIndex(index, length));
                        return item;
                    }
                }

                index = NextIndex(index, length);
            }

            return null;
        }

        /// <summary>Publishes an exclusively owned item if an observed slot is available.</summary>
        /// <param name="item">The non-null item to transfer to the cache.</param>
        /// <returns>True if ownership transferred; otherwise the caller still owns the item.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryStore(T item)
        {
            if (item is null)
                LuminPackExceptionHelper.ThrowArgumentNullException(nameof(item));
            if (_capacity == 0)
                return false;

            if (Volatile.Read(ref _firstItem) is null &&
                Interlocked.CompareExchange(ref _firstItem, item, null) is null)
                return true;

            return StoreSlow(item);
        }

        /// <summary>Scans overflow slots once, starting after the previous successful store.</summary>
        /// <param name="item">The exclusively owned item to publish.</param>
        /// <returns>True if a slot accepted ownership.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool StoreSlow(T item)
        {
            var slots = _slots;
            var length = slots.Length;
            var index = Volatile.Read(ref _storeHint);

            for (var remaining = length; remaining != 0; remaining--)
            {
                ref var slot = ref slots[index];
                if (Volatile.Read(ref slot) is null &&
                    Interlocked.CompareExchange(ref slot, item, null) is null)
                {
                    Volatile.Write(ref _storeHint, NextIndex(index, length));
                    return true;
                }

                index = NextIndex(index, length);
            }

            return false;
        }

        /// <summary>Advances a valid overflow index without division.</summary>
        /// <param name="index">The current index.</param>
        /// <param name="length">The positive overflow length.</param>
        /// <returns>The next index, wrapping at the array length.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NextIndex(int index, int length)
            => index + 1 == length ? 0 : index + 1;

        /// <summary>Counts observed occupied slots without acquiring their ownership.</summary>
        internal int ApproximateCount
        {
            get
            {
                var count = Volatile.Read(ref _firstItem) is null ? 0 : 1;
                for (var i = 0; i < _slots.Length; i++)
                    if (Volatile.Read(ref _slots[i]) is not null)
                        count++;
                return count;
            }
        }

        /// <summary>
        /// Diagnostic references, not leases. Concurrent snapshots may contain
        /// duplicates or stale/disposed objects; never mutate/return/dispose them.
        /// </summary>
        /// <returns>Observed references without transferring ownership.</returns>
        internal T?[] Snapshot()
        {
            if (_capacity == 0)
                return Array.Empty<T?>();

            var result = new T?[_capacity];
            var count = 0;
            var first = Volatile.Read(ref _firstItem);
            if (first is not null)
                result[count++] = first;

            for (var i = 0; i < _slots.Length; i++)
            {
                var item = Volatile.Read(ref _slots[i]);
                if (item is not null)
                    result[count++] = item;
            }

            if (count != result.Length)
                Array.Resize(ref result, count);
            return result;
        }

        /// <summary>
        /// Transfers observed items to consume. Requires quiescence to empty the
        /// entire cache. A throwing callback stops this pass without losing the
        /// items still in slots.
        /// </summary>
        /// <param name="consume">Receives ownership of each item removed from storage.</param>
        internal void Drain(Action<T> consume)
        {
            if (consume is null)
                LuminPackExceptionHelper.ThrowArgumentNullException(nameof(consume));

            var first = Interlocked.Exchange(ref _firstItem, null);
            if (first is not null)
                consume(first);

            for (var i = 0; i < _slots.Length; i++)
            {
                var item = Interlocked.Exchange(ref _slots[i], null);
                if (item is not null)
                    consume(item);
            }
        }
    }
}
