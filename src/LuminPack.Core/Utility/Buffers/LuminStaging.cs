using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Utility;

/// <summary>Leases temporary storage to synchronous collection operations on the calling thread.</summary>
/// <typeparam name="T">The staged element type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminStaging<T>
{
    private const int MaxRetainedBytes = 64 * 1024;
    private static readonly int RetainedLimit = Math.Min(4096, MaxRetainedBytes / Unsafe.SizeOf<T>());

    [ThreadStatic]
    private static T[]? _slot;

    /// <summary>Leases an array with at least the requested number of slots.</summary>
    /// <param name="count">The number of slots needed by the operation.</param>
    /// <returns>An array exclusively owned by the caller until it is returned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Rent(int count)
    {
        if (count == 0)
            return Array.Empty<T>();
        if ((uint)count > (uint)RetainedLimit)
            return ArrayPool<T>.Shared.Rent(count);
        var array = _slot;
        if (array is not null && count <= array.Length)
        {
            _slot = null;
            return array;
        }
        return Allocate(count);
    }

    /// <summary>Releases an array and any references published in its used prefix.</summary>
    /// <param name="array">An array obtained from <see cref="Rent"/>.</param>
    /// <param name="count">The full prefix the operation may have written, including partial writes on failure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T[] array, int count)
    {
        if (array.Length == 0)
            return;
        bool references = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        if (array.Length > RetainedLimit)
        {
            ArrayPool<T>.Shared.Return(array, references);
            return;
        }
        if (references && count != 0)
            Array.Clear(array, 0, count);
        if (_slot is null || array.Length >= _slot.Length)
            _slot = array;
    }

    /// <summary>Allocates a retained size bucket without exceeding the per-type byte budget.</summary>
    /// <param name="count">The positive requested length.</param>
    /// <returns>A new array large enough for the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T[] Allocate(int count)
    {
        int capacity = 16;
        while (capacity < count)
            capacity *= 2;
        return new T[Math.Min(capacity, RetainedLimit)];
    }
}
