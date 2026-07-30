namespace LuminPack.Utility;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
/// A bounded object pool with a one-item thread cache and a lock-free MPMC ring.
/// Central slots use sequence numbers so an item is published only after its
/// value is visible and cannot be reused until the consumer releases the slot.
/// </summary>
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

    public ObjectPool(
#if !NET8_0_OR_GREATER
        IPooledObjectPolicy<T> policy,
#endif
        int maxSize = 16)
    {
        if (maxSize < 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize));

        _poolId = Interlocked.Increment(ref s_nextPoolId);
        if (_poolId == 0)
            _poolId = Interlocked.Increment(ref s_nextPoolId);
        if (Interlocked.Increment(ref s_poolCount) > 1)
            Volatile.Write(ref s_multiplePools, true);

        _maxSize = maxSize;
        _ring = new RingBuffer(Math.Max(1, maxSize));

#if !NET8_0_OR_GREATER
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
#endif
    }

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

    public static void ClearThreadLocalCache()
    {
        s_threadLocalItem?.Dispose();
        s_threadLocalItem = null;
        s_threadLocalPoolId = 0;
    }

    public int AvailableCount => Volatile.Read(ref _ring).Count;
    public int MaxSize => Volatile.Read(ref _maxSize);

    /// <summary>
    /// Resizes the central ring. As with most lock-free bounded queues, resizing
    /// is a quiescent operation and must not run concurrently with Rent/Return.
    /// The hot Rent/Return paths never acquire a lock.
    /// </summary>
    public void Resize(int newSize)
    {
        if (newSize < 0)
            throw new ArgumentOutOfRangeException(nameof(newSize));

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

    public T?[] GetAllItems() => Volatile.Read(ref _ring).Snapshot();

    private sealed class RingBuffer
    {
        private readonly Slot[] _slots;
        private readonly int _capacity;
        private readonly int _indexMask;
        private T? _singleItem;
        private PaddedCounter _enqueuePosition;
        private PaddedCounter _dequeuePosition;

        internal RingBuffer(int capacity)
        {
            _capacity = capacity;
            _indexMask = (capacity & (capacity - 1)) == 0 ? capacity - 1 : -1;
            _slots = new Slot[capacity];

            for (var i = 0; i < capacity; i++)
                _slots[i].Sequence = i;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetIndex(long position) => _indexMask >= 0
            ? (int)position & _indexMask
            : (int)(position % _capacity);
    }

    // Sequence and item live on their own cache line. Adjacent producers and
    // consumers therefore do not bounce an unrelated slot between CPU cores.
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
