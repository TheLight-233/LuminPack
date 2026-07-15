namespace LuminPack.Internal;

internal static class EnumerableEx
{
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