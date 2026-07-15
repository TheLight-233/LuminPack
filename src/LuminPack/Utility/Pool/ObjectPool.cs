
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

public sealed class ObjectPool<T>
#if NET8_0_OR_GREATER
    where T : class, IPooledObjectPolicy<T>, IDisposable
#else
    where T : class, IDisposable
#endif
{
    private T?[] _items;
    private int _count;
    private readonly int _maxSize;
    
#if !NET8_0_OR_GREATER
    private readonly IPooledObjectPolicy<T> _policy;
#endif

    [ThreadStatic]
    private static T? _threadLocalCache;

    public ObjectPool(
#if !NET8_0_OR_GREATER
        IPooledObjectPolicy<T> policy,
#endif
        int maxSize = 16)
    {
        _items = new T?[maxSize];
        _count = 0;
        _maxSize = maxSize;
       
#if !NET8_0_OR_GREATER
        _policy = policy;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (_threadLocalCache != null)
        {
            var local = _threadLocalCache;
            _threadLocalCache = null;
            return local;
        }

        while (true)
        {
            int count = Volatile.Read(ref _count);
            if (count <= 0) break;

            if (Interlocked.CompareExchange(ref _count, count - 1, count) == count)
            {
                var item = _items[count - 1];
                _items[count - 1] = null;
                return item!;
            }
        }

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

        if (_threadLocalCache == null)
        {
            _threadLocalCache = item;
            return;
        }

        while (true)
        {
            int count = Volatile.Read(ref _count);
            if (count >= _maxSize) break;

            if (Interlocked.CompareExchange(ref _count, count + 1, count) == count)
            {
                _items[count] = item;
                return;
            }
        }

        item.Dispose();
    }

    public static void ClearThreadLocalCache()
    {
        _threadLocalCache?.Dispose();
        _threadLocalCache = null;
    }

    public int AvailableCount => Volatile.Read(ref _count);
    public int MaxSize => _maxSize;

    public void Resize(int newSize)
    {
        if (newSize < 0) throw new ArgumentOutOfRangeException(nameof(newSize));

        lock (this)
        {
            if (newSize > _items.Length)
            {
                Array.Resize(ref _items, newSize);
            }
            else if (newSize < _items.Length)
            {
                for (int i = newSize; i < _items.Length; i++)
                {
                    if (i < _count && _items[i] is IDisposable disposable)
                        disposable.Dispose();
                }

                var newItems = new T[newSize];
                int itemsToCopy = Math.Min(_count, newSize);
                Array.Copy(_items, newItems, itemsToCopy);
                _items = newItems;
                _count = itemsToCopy;
            }
        }
    }

    public T?[] GetAllItems()
    {
        var result = new T?[_count];
        Array.Copy(_items, result, _count);
        return result;
    }

}