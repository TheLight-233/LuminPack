namespace LuminPack.Utility;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NET8_0_OR_GREATER
/// <summary>Provides i pooled object policy functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
public interface IPooledObjectPolicy<T> where T : class
{
    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    static abstract T Create();
    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="obj">The obj used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    static abstract bool Return(T obj);
}
#else
/// <summary>Provides i pooled object policy functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
public interface IPooledObjectPolicy<T> where T : class
{
    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    T Create();
    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="obj">The obj used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    bool Return(T obj);
}
#endif

/// <summary>
/// A bounded object pool with a one-item thread cache and a lock-free MPMC ring.
/// Central slots use sequence numbers so an item is published only after its
/// value is visible and cannot be reused until the consumer releases the slot.
/// </summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
public sealed class ObjectPool<T>
#if NET8_0_OR_GREATER
    where T : class, IPooledObjectPolicy<T>, IDisposable
#else
    where T : class, IDisposable
#endif
{
    private static int s_nextPoolId;
    private static int s_poolCount;
    private static bool s_multiplePools;

    [ThreadStatic]
    private static T? s_threadLocalItem;

    [ThreadStatic]
    private static int s_threadLocalPoolId;

    private readonly int _poolId;
    private RingBuffer _ring;
    private int _maxSize;

#if !NET8_0_OR_GREATER
    private readonly IPooledObjectPolicy<T> _policy;
#endif

#if !NET8_0_OR_GREATER
    /// <summary>Initializes a pool with the supplied policy and maximum retained capacity.</summary>
    /// <param name="policy">The policy used by this operation.</param>
    /// <param name="maxSize">The max size used by this operation.</param>
#else
    /// <summary>Initializes a pool with the maximum retained capacity.</summary>
    /// <param name="maxSize">The maximum number of instances retained by the shared pool.</param>
#endif
    public ObjectPool(
#if !NET8_0_OR_GREATER
        IPooledObjectPolicy<T>? policy,
#endif
        int maxSize = 16)
    {
        if (maxSize < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(maxSize));

        _poolId = Interlocked.Increment(ref s_nextPoolId);
        if (_poolId == 0)
            _poolId = Interlocked.Increment(ref s_nextPoolId);
        if (Interlocked.Increment(ref s_poolCount) > 1)
            Volatile.Write(ref s_multiplePools, true);

        _maxSize = maxSize;
        _ring = new RingBuffer(Math.Max(1, maxSize));

#if !NET8_0_OR_GREATER
        if (policy is null)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(policy));

        _policy = policy;
#endif
    }

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        var local = s_threadLocalItem;
        if (local is not null &&
            (!Volatile.Read(ref s_multiplePools) || s_threadLocalPoolId == _poolId))
        {
            s_threadLocalItem = null;
            return local;
        }

        return RentSlow();
    }

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private T RentSlow()
    {
        if (Volatile.Read(ref _ring).TryDequeue(out var item))
            return item;

#if NET8_0_OR_GREATER
        return T.Create();
#else
        return _policy.Create();
#endif
    }

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="item">The entry to process.</param>
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

        if (s_threadLocalItem is null &&
            (!Volatile.Read(ref s_multiplePools) || s_threadLocalPoolId == _poolId))
        {
            s_threadLocalItem = item;
            return;
        }

        ReturnSlow(item);
    }

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="item">The entry to process.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReturnSlow(T item)
    {
        if (s_threadLocalItem is null)
        {
            s_threadLocalPoolId = _poolId;
            s_threadLocalItem = item;
            return;
        }

        if (Volatile.Read(ref _maxSize) != 0 && Volatile.Read(ref _ring).TryEnqueue(item))
            return;

        item.Dispose();
    }

    /// <summary>Performs the clear thread local cache step for the current operation.</summary>
    public static void ClearThreadLocalCache()
    {
        s_threadLocalItem?.Dispose();
        s_threadLocalItem = null;
        s_threadLocalPoolId = 0;
    }

    /// <summary>Gets the available count.</summary>
    public int AvailableCount => Volatile.Read(ref _ring).Count;
    /// <summary>Gets the max size.</summary>
    public int MaxSize => Volatile.Read(ref _maxSize);

    /// <summary>
    /// Resizes the central ring. As with most lock-free bounded queues, resizing
    /// is a quiescent operation and must not run concurrently with Rent/Return.
    /// The hot Rent/Return paths never acquire a lock.
    /// </summary>
    /// <param name="newSize">The new size used by this operation.</param>
    public void Resize(int newSize)
    {
        if (newSize < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(newSize));

        var oldRing = Volatile.Read(ref _ring);
        var newRing = new RingBuffer(Math.Max(1, newSize));
        var retained = 0;

        while (oldRing.TryDequeue(out var item))
        {
            if (retained < newSize && newRing.TryEnqueue(item))
            {
                retained++;
            }
            else
            {
                item.Dispose();
            }
        }

        Volatile.Write(ref _maxSize, newSize);
        Volatile.Write(ref _ring, newRing);
    }

    /// <summary>Obtains the all items for the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    public T?[] GetAllItems() => Volatile.Read(ref _ring).Snapshot();

    /// <summary>Provides ring buffer functionality.</summary>
    private sealed class RingBuffer
    {
        private readonly Slot[] _slots;
        private readonly int _capacity;
        private readonly int _indexMask;
        private T? _singleItem;
        private PaddedCounter _enqueuePosition;
        private PaddedCounter _dequeuePosition;

        /// <summary>Initializes a new instance of <c>RingBuffer</c>.</summary>
        /// <param name="capacity">The requested storage capacity.</param>
        internal RingBuffer(int capacity)
        {
            _capacity = capacity;
            _indexMask = (capacity & (capacity - 1)) == 0 ? capacity - 1 : -1;
            _slots = new Slot[capacity];

            for (var i = 0; i < capacity; i++)
                _slots[i].Sequence = i;
        }

        /// <summary>Gets the count.</summary>
        internal int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_capacity == 1)
                    return Volatile.Read(ref _singleItem) is null ? 0 : 1;

                var count = Volatile.Read(ref _enqueuePosition.Value) -
                            Volatile.Read(ref _dequeuePosition.Value);
                if (count <= 0)
                    return 0;
                return count >= _capacity ? _capacity : (int)count;
            }
        }

        /// <summary>Attempts to enqueue and reports whether it succeeded.</summary>
        /// <param name="item">The entry to process.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryEnqueue(T item)
        {
            if (_capacity == 1)
                return Interlocked.CompareExchange(ref _singleItem, item, null) is null;

            var position = Volatile.Read(ref _enqueuePosition.Value);

            while (true)
            {
                ref var slot = ref _slots[GetIndex(position)];
                var sequence = Volatile.Read(ref slot.Sequence);
                var difference = sequence - position;

                if (difference == 0)
                {
                    if (Interlocked.CompareExchange(
                            ref _enqueuePosition.Value, position + 1, position) == position)
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

                position = Volatile.Read(ref _enqueuePosition.Value);
            }
        }

        /// <summary>Attempts to dequeue and reports whether it succeeded.</summary>
        /// <param name="item">Receives the item produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryDequeue(out T item)
        {
            if (_capacity == 1)
            {
                item = Interlocked.Exchange(ref _singleItem, null)!;
                return item is not null;
            }

            var position = Volatile.Read(ref _dequeuePosition.Value);

            while (true)
            {
                ref var slot = ref _slots[GetIndex(position)];
                var sequence = Volatile.Read(ref slot.Sequence);
                var difference = sequence - (position + 1);

                if (difference == 0)
                {
                    if (Interlocked.CompareExchange(
                            ref _dequeuePosition.Value, position + 1, position) == position)
                    {
                        item = slot.Item!;
                        slot.Item = null;
                        Volatile.Write(ref slot.Sequence, position + _capacity);
                        return item is not null;
                    }
                }
                else if (difference < 0)
                {
                    item = null!;
                    return false;
                }

                position = Volatile.Read(ref _dequeuePosition.Value);
            }
        }

        /// <summary>Performs the snapshot step for the current operation.</summary>
        /// <returns>The result of the operation.</returns>
        internal T?[] Snapshot()
        {
            if (_capacity == 1)
            {
                var single = Volatile.Read(ref _singleItem);
                return single is null ? Array.Empty<T?>() : [single];
            }

            var start = Volatile.Read(ref _dequeuePosition.Value);
            var end = Volatile.Read(ref _enqueuePosition.Value);
            var maximum = (int)Math.Min(Math.Max(end - start, 0), _capacity);
            if (maximum == 0)
                return Array.Empty<T?>();

            var result = new T?[maximum];
            var count = 0;
            for (var position = start; position < end && count < maximum; position++)
            {
                ref var slot = ref _slots[GetIndex(position)];
                if (Volatile.Read(ref slot.Sequence) != position + 1)
                    continue;

                var item = slot.Item;
                if (item is not null)
                    result[count++] = item;
            }

            if (count != result.Length)
                Array.Resize(ref result, count);
            return result;
        }

        /// <summary>Obtains the index for the supplied input.</summary>
        /// <param name="position">The position used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetIndex(long position) => _indexMask >= 0
            ? (int)position & _indexMask
            : (int)(position % _capacity);
    }

    // Sequence and item live on their own cache line. Adjacent producers and
    // consumers therefore do not bounce an unrelated slot between CPU cores.
    /// <summary>Provides slot functionality.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Slot
    {
#pragma warning disable CS0169
        private long _pad0, _pad1, _pad2, _pad3, _pad4, _pad5, _pad6;
        internal long Sequence;
        internal T? Item;
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
