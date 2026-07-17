namespace LuminPack.Utility;

using System.Runtime.CompilerServices;

#if NET8_0_OR_GREATER
public interface IPooledObjectPolicy<T> where T : class
{
    static abstract T Create();
    static abstract bool Return(T obj);
}
#else
public interface IPooledObjectPolicy<T> where T : class
{
    T Create();
    bool Return(T obj);
}
#endif

/// <summary>
/// A low-contention, thread-safe object pool.
/// </summary>
/// <remarks>
/// The layout borrows the important ideas from mimalloc: a thread-owned fast path,
/// a thread-striped shared page, and retirement of old pages during resize. The hot
/// thread-local rent/return pair performs no atomic operation or allocation.
/// </remarks>
public sealed class ObjectPool<T>
#if NET8_0_OR_GREATER
    where T : class, IPooledObjectPolicy<T>, IDisposable
#else
    where T : class, IDisposable
#endif
{
    // These fields are per closed T, so the owner is required to prevent two
    // ObjectPool<T> instances on the same thread from exchanging objects.
    [ThreadStatic]
    private static ObjectPool<T>? s_threadLocalOwner;

    [ThreadStatic]
    private static T? s_threadLocalItem;

    [ThreadStatic]
    private static int s_rentSlotCursor;

    [ThreadStatic]
    private static int s_storeSlotCursor;

    private readonly object _resizeLock = new();
    private PoolState _state;

#if !NET8_0_OR_GREATER
    private readonly IPooledObjectPolicy<T> _policy;
#endif

    public ObjectPool(
#if !NET8_0_OR_GREATER
        IPooledObjectPolicy<T> policy,
#endif
        int maxSize = 16)
    {
        if (maxSize < 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize));

#if !NET8_0_OR_GREATER
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
#endif
        _state = new PoolState(maxSize);
    }

    /// <summary>
    /// Gets an object from the calling thread's cache, the shared page, or the policy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (ReferenceEquals(s_threadLocalOwner, this))
        {
            T? item = s_threadLocalItem;
            if (item is not null)
            {
                s_threadLocalItem = null;
                return item;
            }
        }
        else
        {
            SwitchThreadLocalOwner();
        }

        PoolState state = Volatile.Read(ref _state);
        if (state.TryRent(ref s_rentSlotCursor, out T? sharedItem))
            return sharedItem;

        // A resize may have retired the captured page while it was being probed.
        PoolState current = Volatile.Read(ref _state);
        if (!ReferenceEquals(state, current) &&
            current.TryRent(ref s_rentSlotCursor, out sharedItem))
            return sharedItem;

#if NET8_0_OR_GREATER
        return T.Create();
#else
        return _policy.Create();
#endif
    }

    /// <summary>
    /// Resets and returns an object to the calling thread or the shared page.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
#if NET8_0_OR_GREATER
        if (!T.Return(item))
#else
        if (!_policy.Return(item))
#endif
        {
            item.Dispose();
            return;
        }

        if (!ReferenceEquals(s_threadLocalOwner, this))
            SwitchThreadLocalOwner();

        if (s_threadLocalItem is null)
        {
            s_threadLocalItem = item;
            return;
        }

        StoreSharedOrDispose(item);
    }

    /// <summary>
    /// Disposes the item cached by the current thread, if any.
    /// </summary>
    public static void ClearThreadLocalCache()
    {
        T? item = s_threadLocalItem;
        s_threadLocalItem = null;
        s_threadLocalOwner = null;
        s_rentSlotCursor = 0;
        s_storeSlotCursor = 0;
        item?.Dispose();
    }

    /// <summary>
    /// Gets the number of objects currently visible in the shared page.
    /// Thread-local items are intentionally excluded.
    /// </summary>
    public int AvailableCount
    {
        get => Volatile.Read(ref _state).CountItems();
    }

    public int MaxSize => Volatile.Read(ref _state).Capacity;

    /// <summary>
    /// Changes the shared capacity. Concurrent rent and return operations remain valid.
    /// Excess objects are disposed when shrinking.
    /// </summary>
    public void Resize(int newSize)
    {
        if (newSize < 0)
            throw new ArgumentOutOfRangeException(nameof(newSize));

        lock (_resizeLock)
        {
            PoolState oldState = Volatile.Read(ref _state);
            if (oldState.Capacity == newSize)
                return;

            PoolState newState = new(newSize);
            oldState = Interlocked.Exchange(ref _state, newState);
            Volatile.Write(ref oldState.Retired, 1);
            oldState.DrainTo(this);
        }
    }

    /// <summary>
    /// Returns a best-effort snapshot of the objects in the shared page.
    /// </summary>
    public T?[] GetAllItems() => Volatile.Read(ref _state).Snapshot();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SwitchThreadLocalOwner()
    {
        ObjectPool<T>? previousOwner = s_threadLocalOwner;
        T? displacedItem = s_threadLocalItem;

        s_threadLocalItem = null;
        s_threadLocalOwner = this;
        int threadHash = unchecked(Environment.CurrentManagedThreadId * (int)0x9E3779B9);
        s_rentSlotCursor = threadHash;
        s_storeSlotCursor = threadHash;

        // The policy already accepted this object when it entered the old pool.
        if (displacedItem is not null && previousOwner is not null)
            previousOwner.StoreSharedOrDispose(displacedItem);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StoreSharedOrDispose(T item)
    {
        while (true)
        {
            PoolState state = Volatile.Read(ref _state);
            StoreResult result = state.TryStore(ref s_storeSlotCursor, item);

            if (result == StoreResult.Stored)
                return;

            if (result == StoreResult.Full)
            {
                item.Dispose();
                return;
            }

            // The page was retired by Resize. Retry directly against the new page.
        }
    }

    private enum StoreResult : byte
    {
        Stored,
        Full,
        Retired
    }

    private sealed class PoolState
    {
        internal readonly int Capacity;
        private readonly T?[] _slots;
        private readonly int _indexMask;
        internal int Retired;

        internal PoolState(int capacity)
        {
            Capacity = capacity;
            _slots = new T?[capacity];
            _indexMask = capacity > 0 && (capacity & (capacity - 1)) == 0
                ? capacity - 1
                : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryRent(ref int cursor, out T? item)
        {
            T?[] slots = _slots;
            int length = slots.Length;
            if (length == 0)
            {
                item = null;
                return false;
            }

            int start = GetStartIndex(cursor, length);
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= length)
                    index -= length;

                item = Interlocked.Exchange(ref slots[index], null);
                if (item is not null)
                {
                    cursor = index + 1;
                    return true;
                }
            }

            item = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StoreResult TryStore(ref int cursor, T item)
        {
            if (Volatile.Read(ref Retired) != 0)
                return StoreResult.Retired;

            T?[] slots = _slots;
            int length = slots.Length;
            if (length == 0)
                return StoreResult.Full;

            int start = GetStartIndex(cursor, length);
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= length)
                    index -= length;

                if (Interlocked.CompareExchange(ref slots[index], item, null) is not null)
                    continue;

                cursor = index + 1;

                if (Volatile.Read(ref Retired) == 0)
                    return StoreResult.Stored;

                // A resize raced with publication. Reclaim the item when possible;
                // otherwise the resizer/renter already took responsibility for it.
                if (ReferenceEquals(Interlocked.CompareExchange(ref slots[index], null, item), item))
                    return StoreResult.Retired;

                return StoreResult.Stored;
            }

            return Volatile.Read(ref Retired) == 0
                ? StoreResult.Full
                : StoreResult.Retired;
        }

        internal void DrainTo(ObjectPool<T> owner)
        {
            T?[] slots = _slots;
            for (int i = 0; i < slots.Length; i++)
            {
                T? item = Interlocked.Exchange(ref slots[i], null);
                if (item is not null)
                    owner.StoreSharedOrDispose(item!);
            }
        }

        internal int CountItems()
        {
            int count = 0;
            T?[] slots = _slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (Volatile.Read(ref slots[i]) is not null)
                    count++;
            }
            return count;
        }

        internal T?[] Snapshot()
        {
            if (Capacity == 0)
                return Array.Empty<T?>();

            T?[] result = new T?[Capacity];
            int count = 0;
            T?[] slots = _slots;
            for (int i = 0; i < slots.Length; i++)
            {
                T? item = Volatile.Read(ref slots[i]);
                if (item is not null)
                    result[count++] = item;
            }

            if (count != result.Length)
                Array.Resize(ref result, count);

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetStartIndex(int cursor, int length)
        {
            int mask = _indexMask;
            return mask >= 0 ? cursor & mask : (cursor & int.MaxValue) % length;
        }
    }
}
