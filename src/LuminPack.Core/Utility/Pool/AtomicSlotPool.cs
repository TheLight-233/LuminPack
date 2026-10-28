namespace LuminPack.Utility;

using System.Runtime.CompilerServices;

/// <summary>
/// Fixed-capacity, unordered storage for shared object-pool overflow.
/// Every slot independently transitions between null and an object.
/// </summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
internal sealed class AtomicSlotPool<T> where T : class
{
    private const int ProbeIncrement = unchecked((int)0x9E3779B9);

    [ThreadStatic]
    private static int t_probe;

    private readonly T?[] _slots;
    private readonly int _indexMask;

    /// <summary>Initializes a new instance of <c>AtomicSlotPool</c>.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    internal AtomicSlotPool(int capacity)
    {
        if (capacity <= 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(capacity));

        _slots = new T?[capacity];
        _indexMask = (capacity & (capacity - 1)) == 0 ? capacity - 1 : -1;
    }

    /// <summary>Gets the capacity.</summary>
    internal int Capacity => _slots.Length;

    /// <summary>
    /// Returns an approximate concurrent snapshot of occupied central slots.
    /// </summary>
    internal int ApproximateCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (Volatile.Read(ref _slots[i]) is not null)
                    count++;
            }

            return count;
        }
    }

    /// <summary>Attempts to take and reports whether it succeeded.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T? TryTake()
    {
        var start = NextProbe();

        for (var offset = 0; offset < _slots.Length; offset++)
        {
            ref var slot = ref _slots[GetIndex(start + offset)];
            if (Volatile.Read(ref slot) is null)
                continue;

            var item = Interlocked.Exchange(ref slot, null);
            if (item is not null)
                return item;
        }

        return null;
    }

    /// <summary>Attempts to store and reports whether it succeeded.</summary>
    /// <param name="item">The entry to process.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryStore(T item)
    {
        var start = NextProbe();

        for (var offset = 0; offset < _slots.Length; offset++)
        {
            ref var slot = ref _slots[GetIndex(start + offset)];
            if (Volatile.Read(ref slot) is not null)
                continue;

            if (Interlocked.CompareExchange(ref slot, item, null) is null)
                return true;
        }

        return false;
    }

    /// <summary>Performs the drain step for the current operation.</summary>
    /// <param name="consume">The consume used by this operation.</param>
    internal void Drain(Action<T> consume)
    {
        if (consume is null)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(consume));

        for (var i = 0; i < _slots.Length; i++)
        {
            var item = Interlocked.Exchange(ref _slots[i], null);
            if (item is not null)
                consume(item);
        }
    }

    /// <summary>Performs the next probe step for the current operation.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int NextProbe()
    {
        var probe = t_probe;
        if (probe == 0)
            probe = unchecked(Environment.CurrentManagedThreadId * ProbeIncrement);

        probe = unchecked(probe + ProbeIncrement);
        t_probe = probe;
        return probe;
    }

    /// <summary>Obtains the index for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(int value) => _indexMask >= 0
        ? value & _indexMask
        : (int)((uint)value % (uint)_slots.Length);
}
