using System.Collections.Generic;
using System.ComponentModel;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Returns an empty default-comparer set with room for a checked refill, reusing an existing exact set.</summary>
    /// <typeparam name="T">The set element type.</typeparam>
    /// <param name="existing">The reused destination, or null.</param>
    /// <param name="count">The validated element count of the incoming record.</param>
    /// <returns><paramref name="existing"/> after <c>Clear</c> and <c>EnsureCapacity</c> when it is an exact
    /// <see cref="HashSet{T}"/> with the default comparer, otherwise a new set sized for <paramref name="count"/>.</returns>
    /// <remarks>Clearing keeps the entry and bucket arrays, so a refill that fits the retained capacity allocates nothing.
    /// The direct entry readers require an empty set whose comparer matches the writer's default equality. A custom
    /// comparer could merge distinct wire items, so such sets and derived types are replaced as before.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static HashSet<T> PrepareHashSetForRefill<T>(HashSet<T>? existing, int count)
    {
        if (existing is not null && existing.GetType() == typeof(HashSet<T>) &&
            ReferenceEquals(existing.Comparer, EqualityComparer<T>.Default))
        {
            existing.Clear();
            existing.EnsureCapacity(count);
            return existing;
        }
        return new HashSet<T>(count);
    }
}
