using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Creates a concurrent dictionary from read pairs with the original concurrency level and comparer.</summary>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of pairs.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>A dictionary holding the first pair of each key, as the original insertions with <c>TryAdd</c> produce.</returns>
    /// <remarks>The framework constructor and the validated unpublished-table fill both size the table once.
    /// Duplicate input is replayed through TryAdd to keep the first value. A null key throws.</remarks>
    public static ConcurrentDictionary<TKey, TValue> CreateConcurrentDictionary<TKey, TValue>(
        KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
        var source = new ArraySegment<KeyValuePair<TKey, TValue>>(pairs, 0, count);
        try
        {
#if NET10_0_OR_GREATER
            if (count >= 32 && ConcurrentConstruction<TKey, TValue>.Available)
                return ConcurrentConstruction<TKey, TValue>.Build(source.AsSpan());
#endif
            return new ConcurrentDictionary<TKey, TValue>(Environment.ProcessorCount, source, null);
        }
        catch (ArgumentException) when (HasDuplicateKey(pairs, count))
        {
            var dictionary = new ConcurrentDictionary<TKey, TValue>(Environment.ProcessorCount, Math.Max(count, 1));
            for (int i = 0; i < count; i++) dictionary.TryAdd(pairs[i].Key, pairs[i].Value);
            return dictionary;
        }
    }

    /// <summary>Checks read pairs for a repeated key under the default comparer.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>The result.</returns>
    private static bool HasDuplicateKey<TKey, TValue>(KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
        var keys = new HashSet<TKey>();
        for (int i = 0; i < count; i++)
            if (pairs[i].Key is not null && !keys.Add(pairs[i].Key)) return true;
        return false;
    }

#if NET10_0_OR_GREATER
    /// <summary>Validates each closed framework table before filling its empty buckets.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    private static class ConcurrentConstruction<TKey, TValue> where TKey : notnull
    {
        private const string TablesType = "System.Collections.Concurrent.ConcurrentDictionary`2+Tables[[!0],[!1]], System.Collections.Concurrent";
        private const string NodeType = "System.Collections.Concurrent.ConcurrentDictionary`2+Node[[!0],[!1]], System.Collections.Concurrent";
        private static readonly int Counts;
        private static readonly int Comparer;
        private static readonly int Budget;
        internal static readonly bool Available;

        /// <summary>Checks field types, node allocation and insertion without running user key or value methods.</summary>
        static ConcurrentConstruction()
        {
            if (!RuntimeFeature.IsDynamicCodeCompiled || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
                Environment.Version.Major is not (10 or 11) ||
                ConcurrentBucketStorage<TKey, TValue>.Node is null) return;
            try
            {
                var mapType = typeof(ConcurrentDictionary<TKey, TValue>);
                var tableType = LuminFieldLayout.Find(mapType, "_tables")?.FieldType;
                if (LuminFieldLayout.Find(tableType, "_countPerLock")?.FieldType != typeof(int[]) ||
                    LuminFieldLayout.Find(tableType, "_comparer")?.FieldType != typeof(IEqualityComparer<TKey>) ||
                    LuminFieldLayout.Find(mapType, "_budget")?.FieldType != typeof(int)) return;
                Counts = LuminFieldLayout.ObjectField(tableType, "_countPerLock");
                Comparer = LuminFieldLayout.ObjectField(tableType, "_comparer", EqualityComparer<TKey>.Default);
                Budget = LuminFieldLayout.ObjectField(mapType, "_budget");
                if ((Counts | Comparer | Budget) < 0) return;
                var map = new ConcurrentDictionary<TKey, TValue>(1, 37);
                var tables = LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables);
                var buckets = Buckets(tables);
                // Supply the hash and use an empty bucket. The framework never hashes or compares the probe's default key.
                if (!Insert(map, tables, default!, -1, default!, false, false, out _) || map.Count != 1) return;
                object? inserted = buckets[(int)(uint.MaxValue % (uint)buckets.Length)];
                Available = inserted?.GetType() == NewNode(default!, default!, -1, null).GetType() &&
                    LuminFieldLayout.Field<int[]>(tables, Counts)[0] == 1 && LuminFieldLayout.Field<int>(map, Budget) == buckets.Length &&
                    !string.IsNullOrEmpty(DuplicateKeyMessage(null));
            }
            catch { }
        }

        /// <summary>Exposes the single managed node reference proved by the traversal layout.</summary>
        /// <param name="tables">The dictionary's current tables.</param>
        /// <returns>The node slots of its buckets.</returns>
        private static Span<object?> Buckets(object tables)
        {
            var array = LuminFieldLayout.Field<Array>(tables, ConcurrentBucketStorage<TKey, TValue>.Buckets);
            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, object?>(ref Unsafe.As<byte[]>(array)[0]), array.Length);
        }

        /// <summary>Fills a fresh dictionary with the same hashes, node order and growth decisions as its collection constructor.</summary>
        /// <param name="pairs">The validated range of parsed pairs.</param>
        /// <returns>The completed, previously unpublished dictionary.</returns>
        internal static ConcurrentDictionary<TKey, TValue> Build(ReadOnlySpan<KeyValuePair<TKey, TValue>> pairs)
        {
            var map = new ConcurrentDictionary<TKey, TValue>(Environment.ProcessorCount, pairs.Length);
            int i = 0;
            while (i < pairs.Length)
            {
                var tables = LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables);
                var buckets = Buckets(tables);
                var counts = LuminFieldLayout.Field<int[]>(tables, Counts);
                var comparer = LuminFieldLayout.Field<IEqualityComparer<TKey>?>(tables, Comparer);
                int budget = LuminFieldLayout.Field<int>(map, Budget);
                ulong bucketMultiplier = LuminPackMarshal.GetFastModMultiplier((uint)buckets.Length);
                ulong stripeMultiplier = LuminPackMarshal.GetFastModMultiplier((uint)counts.Length);
                while (i < pairs.Length)
                {
                    ref readonly var pair = ref pairs[i++];
                    TKey key = pair.Key;
                    if (key is null) LuminPackExceptionHelper.ThrowArgumentNullException("key");
                    // The table owns the actual comparer, including the framework's internal string comparer.
                    int hash = comparer is null ? key.GetHashCode() : comparer.GetHashCode(key);
                    uint bucket = (uint)hash;
                    if (bucket >= (uint)buckets.Length) bucket = LuminPackMarshal.FastMod(bucket, (uint)buckets.Length, bucketMultiplier);
                    ref object? slot = ref buckets[(int)bucket];
                    ref int stripe = ref counts[LuminPackMarshal.FastMod(bucket, (uint)counts.Length, stripeMultiplier)];
                    if (slot is null && stripe < budget)
                    {
                        slot = NewNode(key, pair.Value, hash, null);
                        stripe++;
                        continue;
                    }
                    // Pass the hash already obtained from the key. The BCL handles equality, rehashing and growth.
                    if (!Insert(map, tables, key, hash, pair.Value, false, false, out _))
                        LuminPackExceptionHelper.ThrowArgumentException(DuplicateKeyMessage(null));
                    if (!ReferenceEquals(tables, LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables))) break;
                    budget = LuminFieldLayout.Field<int>(map, Budget);
                }
            }
            return map;
        }

        /// <summary>Allocates the framework's closed node.</summary>
        /// <param name="key">The parsed key.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="hash">The table's key hash.</param>
        /// <param name="next">The next bucket node.</param>
        /// <returns>The initialized framework node.</returns>
        [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
        [return: UnsafeAccessorType(NodeType)]
        private static extern object NewNode(TKey key, TValue value, int hash, [UnsafeAccessorType(NodeType)] object? next);

        /// <summary>Runs the framework constructor's insertion on an occupied bucket or at a growth boundary.</summary>
        /// <param name="map">The unpublished dictionary.</param>
        /// <param name="tables">The table used to compute the hash.</param>
        /// <param name="key">The parsed key.</param>
        /// <param name="hash">The precomputed hash.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="update">Whether to replace an existing value.</param>
        /// <param name="acquireLock">Whether to take a stripe lock.</param>
        /// <param name="result">The existing or inserted value.</param>
        /// <returns>Whether the key was inserted.</returns>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryAddInternal")]
        private static extern bool Insert(ConcurrentDictionary<TKey, TValue> map, [UnsafeAccessorType(TablesType)] object tables,
            TKey key, int? hash, TValue value, bool update, bool acquireLock, out TValue result);
    }

    /// <summary>Obtains the framework constructor's localized message before running the original duplicate filter.</summary>
    /// <param name="owner">Identifies the concurrent collections resource type.</param>
    /// <returns>The current duplicate source key message.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ConcurrentDictionary_SourceContainsDuplicateKeys")]
    private static extern string DuplicateKeyMessage([UnsafeAccessorType("System.SR, System.Collections.Concurrent")] object? owner);
#endif
}
