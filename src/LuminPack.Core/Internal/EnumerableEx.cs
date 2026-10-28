namespace LuminPack.Internal;

/// <summary>Provides enumerable ex functionality.</summary>
public static class EnumerableEx
{
    /// <summary>Obtains a collection count without enumerating its elements when the shape supports it.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="count">Receives the count produced by the operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool TryGetNonEnumeratedCountEx<T>(this IEnumerable<T> value, out int count)
    {
#if NET7_0_OR_GREATER
        if (value.TryGetNonEnumeratedCount(out count))
        {
            return true;
        }
#else
        count = 0;
        if (value is ICollection<T> collection)
        {
            count = collection.Count;
            return true;
        }
#endif

        if (value is IReadOnlyCollection<T> readOnlyCollection)
        {
            count = readOnlyCollection.Count;
            return true;
        }

        return false;
    }
}
