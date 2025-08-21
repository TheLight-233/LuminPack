
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Utility;

public sealed class ObjectPool<T>
#if NET8_0_OR_GREATER
    where T : class, IPooledObjectPolicy<T>, IDisposable
#else
    where T : class, IDisposable
#endif
    
{
    // 确保字段跨越缓存行边界（64字节）
    private readonly CacheLinePadding1 _padding1;
    private T?[] _items;
    private volatile int _count;
    private readonly CacheLinePadding2 _padding2;
    private readonly int _maxSize;
    private readonly CacheLinePadding3 _padding3;
    
    // 策略
#if !NET8_0_OR_GREATER
    private readonly IPooledObjectPolicy<T> _policy;
#endif
    
    // 线程本地缓存（每个线程独立）
    [ThreadStatic]
    private static T? _threadLocalCache;

    public ObjectPool(
#if !NET8_0_OR_GREATER
        IPooledObjectPolicy<T> policy,
#endif
        int maxSize = 32)
    {
        _items = LuminPackMarshal.AllocateUninitializedArray<T?>(maxSize);
        _count = 0;
        _maxSize = maxSize;
       
#if !NET8_0_OR_GREATER
        _policy = policy;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        
        // 1. 线程本地缓存（零开销路径）
        if (_threadLocalCache != null)
        {
            var local = _threadLocalCache;
            _threadLocalCache = null;
            return local;
        }
        
        // 2. 无锁全局池访问
        int count = _count;
        if (count > 0)
        {
            // 无原子操作读取（依赖内存屏障）
            T? item = _items[count - 1];
            
            // 确保内存可见性
            Interlocked.MemoryBarrier();
            
            // 尝试原子交换
            if (Interlocked.CompareExchange(ref _count, count - 1, count) == count)
            {
                _items[count - 1] = null; // 防止内存泄漏
                return item!;
            }
        }
        
        // 3. 按需创建
#if NET8_0_OR_GREATER
        return T.Create();
#else
        return _policy.Create();
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        // 1. 策略检查
#if NET8_0_OR_GREATER
        if (!T.Return(item))
        {
            item.Dispose();
            return;
        }
#else
        if (!_policy.Return(item))
        {
            item.Dispose();
            return;
        }
#endif
        
        // 2. 优先线程本地缓存
        if (_threadLocalCache == null)
        {
            _threadLocalCache = item;
            return;
        }
        
        // 3. 放回全局池（非原子写入）
        int count = _count;
        if (count < _items.Length)
        {
            _items[count] = item;
            
            // 内存屏障确保写入可见
            Interlocked.MemoryBarrier();
            
            // 原子递增计数
            Interlocked.Increment(ref _count);
            return;
        }
        
        // 4. 池满时丢弃
        item.Dispose();
    }
    
    // 缓存行填充结构（64字节对齐）
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct CacheLinePadding1 {}
    
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct CacheLinePadding2 {}
    
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct CacheLinePadding3 {}
    
    /// <summary>
    /// 清理线程本地缓存
    /// </summary>
    public static void ClearThreadLocalCache()
    {
        _threadLocalCache?.Dispose();
        _threadLocalCache = null;
    }
    
    /// <summary>
    /// 获取当前池中可用对象数量
    /// </summary>
    public int AvailableCount => _count;
    
    /// <summary>
    /// 获取池的最大容量
    /// </summary>
    public int MaxSize => _maxSize;
    
    /// <summary>
    /// 动态调整池大小（线程安全）
    /// </summary>
    public void Resize(int newSize)
    {
        if (newSize < 0) 
            throw new ArgumentOutOfRangeException(nameof(newSize));
        
        lock (this)
        {
            if (newSize > _items.Length)
            {
                // 扩容：创建新数组，但不预填充
                Array.Resize(ref _items, newSize);
            }
            else if (newSize < _items.Length)
            {
                // 缩容：丢弃多余对象
                for (int i = newSize; i < _items.Length; i++)
                {
                    if (i < _count && _items[i] is IDisposable disposable)
                        disposable.Dispose();
                }
                
                // 创建新数组，复制可用对象
                var newItems = LuminPackMarshal.AllocateUninitializedArray<T?>(newSize);
                int itemsToCopy = Math.Min(_count, newSize);
                Array.Copy(_items, newItems, itemsToCopy);
                
                _items = newItems;
                _count = itemsToCopy;
            }
        }
    }
    
    /// <summary>
    /// 获取所有池中对象
    /// </summary>
    public IReadOnlyList<T?> GetAllItems()
    {
        var list = new List<T?>(_count);
        list.AddRange(_items[.._count]);
        return list;
    }
    
    
}