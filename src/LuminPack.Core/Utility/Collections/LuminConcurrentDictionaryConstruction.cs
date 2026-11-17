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
            for (int i = 0; i < count; i++)
                dictionary.TryAdd(pairs[i].Key, pairs[i].Value);
            return dictionary;
        }
    }

    /// <summary>Starts a fresh concurrent dictionary that is filled pair by pair while the pairs are decoded.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="count">The validated pair count.</param>
    /// <returns>A builder over an unpublished dictionary sized as the collection constructor sizes it.</returns>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static ConcurrentDictionaryBuilder<TKey, TValue> BeginConcurrentDictionary<TKey, TValue>(int count) where TKey : notnull =>
        new(count);

#if NET10_0_OR_GREATER
    /// <summary>Gets whether the validated in-place table fill is available for the closed types.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>Whether the framework table layout passed its probe.</returns>
    internal static bool ConcurrentDictionaryDirect<TKey, TValue>() where TKey : notnull => ConcurrentConstruction<TKey, TValue>.Available;

    /// <summary>Gets the current table object of a dictionary.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="map">The dictionary.</param>
    /// <returns>The framework table object.</returns>
    internal static object ConcurrentDictionaryTables<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map) where TKey : notnull =>
        LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables);

    /// <summary>Gets the node slots of a table.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="tables">The framework table object.</param>
    /// <returns>The bucket node references.</returns>
    internal static Span<object?> ConcurrentDictionaryBuckets<TKey, TValue>(object tables) where TKey : notnull =>
        ConcurrentConstruction<TKey, TValue>.Buckets(tables);

    /// <summary>Gets the per-stripe counts of a table.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="tables">The framework table object.</param>
    /// <returns>The count per lock.</returns>
    internal static int[] ConcurrentDictionaryCounts<TKey, TValue>(object tables) where TKey : notnull =>
        LuminFieldLayout.Field<int[]>(tables, ConcurrentConstruction<TKey, TValue>.Counts);

    /// <summary>Gets the comparer held by a table.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="tables">The framework table object.</param>
    /// <returns>The comparer, or null for the default value-type comparer.</returns>
    internal static IEqualityComparer<TKey>? ConcurrentDictionaryComparer<TKey, TValue>(object tables) where TKey : notnull =>
        LuminFieldLayout.Field<IEqualityComparer<TKey>?>(tables, ConcurrentConstruction<TKey, TValue>.Comparer);

    /// <summary>Allocates a framework node.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="hash">The table's key hash.</param>
    /// <returns>The node.</returns>
    internal static object ConcurrentDictionaryNode<TKey, TValue>(TKey key, TValue value, int hash) where TKey : notnull =>
        ConcurrentConstruction<TKey, TValue>.NewNode(key, value, hash, null);

    /// <summary>Inserts into an occupied bucket of an unpublished dictionary or lets the framework handle a long chain.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="map">The unpublished dictionary.</param>
    /// <param name="tables">The table the hash was computed for.</param>
    /// <param name="key">The key.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="value">The value.</param>
    /// <param name="comparer">The actual table comparer.</param>
    /// <param name="slot">The head of the occupied bucket.</param>
    /// <param name="stripe">The count for the bucket's lock stripe.</param>
    /// <returns>Whether the framework insertion was called and may have replaced the table.</returns>
    internal static bool AddOccupiedConcurrentDictionaryBucket<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map, object tables,
        TKey key, int hash, TValue value, IEqualityComparer<TKey>? comparer, ref object? slot, ref int stripe) where TKey : notnull
    {
        const int collisionThreshold = 100;
        int next = ConcurrentDictionaryStorage<TKey, TValue>.Next;
        if (stripe > collisionThreshold)
        {
            // Check the chain length before comparing keys so framework fallback never repeats key callbacks.
            int collisions = 0;
            for (object? node = slot; node is not null; node = LuminFieldLayout.Field<object?>(node, next))
            {
                if (++collisions <= collisionThreshold)
                    continue;
                ConcurrentConstruction<TKey, TValue>.Insert(map, tables, key, hash, value, false, false, out _);
                return true;
            }
        }

        int hashOffset = ConcurrentConstruction<TKey, TValue>.Hash;
        int keyOffset = ConcurrentDictionaryStorage<TKey, TValue>.Key;
        comparer ??= EqualityComparer<TKey>.Default;
        for (object? node = slot; node is not null; node = LuminFieldLayout.Field<object?>(node, next))
        {
            if (LuminFieldLayout.Field<int>(node, hashOffset) == hash &&
                comparer.Equals(LuminFieldLayout.Field<TKey>(node, keyOffset), key))
                return false;
        }

        slot = ConcurrentConstruction<TKey, TValue>.NewNode(key, value, hash, slot);
        stripe++;
        return false;
    }
#endif

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
            if (pairs[i].Key is not null && !keys.Add(pairs[i].Key))
                return true;
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
        internal static readonly int Counts;
        internal static readonly int Comparer;
        internal static readonly int Budget;
        internal static readonly int Hash;
        internal static readonly bool Available;

        /// <summary>Checks field types, node allocation and insertion without running user key or value methods.</summary>
        static ConcurrentConstruction()
        {
            if (!RuntimeFeature.IsDynamicCodeCompiled || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
                Environment.Version.Major is not (10 or 11) ||
                ConcurrentBucketStorage<TKey, TValue>.Node is not { } nodeType ||
                !ConcurrentDictionaryStorage<TKey, TValue>.Available)
                return;
            try
            {
                var mapType = typeof(ConcurrentDictionary<TKey, TValue>);
                var tableType = LuminFieldLayout.Find(mapType, "_tables")?.FieldType;
                if (LuminFieldLayout.Find(tableType, "_countPerLock")?.FieldType != typeof(int[]) ||
                    LuminFieldLayout.Find(tableType, "_comparer")?.FieldType != typeof(IEqualityComparer<TKey>) ||
                    LuminFieldLayout.Find(mapType, "_budget")?.FieldType != typeof(int) ||
                    LuminFieldLayout.Find(nodeType, "_hashcode")?.FieldType != typeof(int))
                    return;
                Counts = LuminFieldLayout.ObjectField(tableType, "_countPerLock");
                Comparer = LuminFieldLayout.ObjectField(tableType, "_comparer", EqualityComparer<TKey>.Default);
                Budget = LuminFieldLayout.ObjectField(mapType, "_budget");
                Hash = LuminFieldLayout.ObjectField(nodeType, "_hashcode");
                if ((Counts | Comparer | Budget | Hash) < 0)
                    return;
                var map = new ConcurrentDictionary<TKey, TValue>(1, 37);
                var tables = LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables);
                var buckets = Buckets(tables);
                // Supply the hash and use an empty bucket. The framework never hashes or compares the probe's default key.
                if (!Insert(map, tables, default!, -1, default!, false, false, out _) || map.Count != 1)
                    return;
                object? inserted = buckets[(int)(uint.MaxValue % (uint)buckets.Length)];
                object linked = NewNode(default!, default!, -2, inserted);
                Available = inserted?.GetType() == linked.GetType() &&
                    LuminFieldLayout.Field<int>(linked, Hash) == -2 &&
                    ReferenceEquals(LuminFieldLayout.Field<object?>(linked, ConcurrentDictionaryStorage<TKey, TValue>.Next), inserted) &&
                    LuminFieldLayout.Field<int[]>(tables, Counts)[0] == 1 && LuminFieldLayout.Field<int>(map, Budget) == buckets.Length &&
                    !string.IsNullOrEmpty(DuplicateKeyMessage(null));
            }
            catch { }
        }

        /// <summary>Exposes the single managed node reference proved by the traversal layout.</summary>
        /// <param name="tables">The dictionary's current tables.</param>
        /// <returns>The node slots of its buckets.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Span<object?> Buckets(object tables)
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
                    if (key is null)
                        LuminPackExceptionHelper.ThrowArgumentNullException("key");
                    // The table owns the actual comparer, including the framework's internal string comparer.
                    int hash = comparer is null ? key.GetHashCode() : comparer.GetHashCode(key);
                    uint bucket = (uint)hash;
                    if (bucket >= (uint)buckets.Length)
                        bucket = LuminPackMarshal.FastMod(bucket, (uint)buckets.Length, bucketMultiplier);
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
                    if (!ReferenceEquals(tables, LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables)))
                        break;
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
        internal static extern object NewNode(TKey key, TValue value, int hash, [UnsafeAccessorType(NodeType)] object? next);

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
        internal static extern bool Insert(ConcurrentDictionary<TKey, TValue> map, [UnsafeAccessorType(TablesType)] object tables,
            TKey key, int? hash, TValue value, bool update, bool acquireLock, out TValue result);
    }

    /// <summary>Obtains the framework constructor's localized message before running the original duplicate filter.</summary>
    /// <param name="owner">Identifies the concurrent collections resource type.</param>
    /// <returns>The current duplicate source key message.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ConcurrentDictionary_SourceContainsDuplicateKeys")]
    private static extern string DuplicateKeyMessage([UnsafeAccessorType("System.SR, System.Collections.Concurrent")] object? owner);
#endif
}

/// <summary>Fills an unpublished concurrent dictionary one decoded pair at a time.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <remarks>On validated .NET 10 and 11 x64 layouts the pre-sized table defers per-lock growth until public insertion.
/// Chains longer than 100 use the framework insertion with the computed hash to preserve collision defenses. A repeated key
/// keeps its first value. Other runtimes use <c>TryAdd</c>. A failed read publishes nothing.</remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public struct ConcurrentDictionaryBuilder<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _map;
#if NET10_0_OR_GREATER
    private readonly bool _direct;
    private object? _tables;
    private int[]? _counts;
    private IEqualityComparer<TKey>? _comparer;
    private ulong _bucketMultiplier;
    private ulong _stripeMultiplier;
#endif

    /// <summary>Creates the unpublished dictionary.</summary>
    /// <param name="count">The validated pair count.</param>
    internal ConcurrentDictionaryBuilder(int count)
        : this(new ConcurrentDictionary<TKey, TValue>(Environment.ProcessorCount, Math.Max(count, 31)))
    {
    }

    /// <summary>Fills a prepared, empty and unpublished dictionary.</summary>
    /// <param name="map">The prepared dictionary with its final comparer and concurrency level.</param>
    internal ConcurrentDictionaryBuilder(ConcurrentDictionary<TKey, TValue> map)
    {
        _map = map;
#if NET10_0_OR_GREATER
        _direct = LuminPackCollectionAccess.ConcurrentDictionaryDirect<TKey, TValue>();
        _tables = null;
        _counts = null;
        _comparer = null;
        _bucketMultiplier = 0;
        _stripeMultiplier = 0;
        if (_direct)
            Refresh();
#endif
    }

#if NET10_0_OR_GREATER
    /// <summary>Captures the dictionary's current table, counts and comparer.</summary>
    private void Refresh()
    {
        _tables = LuminPackCollectionAccess.ConcurrentDictionaryTables(_map);
        _counts = LuminPackCollectionAccess.ConcurrentDictionaryCounts<TKey, TValue>(_tables);
        _comparer = LuminPackCollectionAccess.ConcurrentDictionaryComparer<TKey, TValue>(_tables);
        _bucketMultiplier = LuminPackMarshal.GetFastModMultiplier((uint)LuminPackCollectionAccess.ConcurrentDictionaryBuckets<TKey, TValue>(_tables).Length);
        _stripeMultiplier = LuminPackMarshal.GetFastModMultiplier((uint)_counts.Length);
    }
#endif

    /// <summary>Adds one decoded pair, keeping the first value of a repeated key.</summary>
    /// <param name="key">The decoded key; a null key throws.</param>
    /// <param name="value">The decoded value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        if (key is null)
            LuminPackExceptionHelper.ThrowArgumentNullException("key");
#if NET10_0_OR_GREATER
        if (_direct)
        {
            int hash = _comparer is null ? key.GetHashCode() : _comparer.GetHashCode(key);
            var buckets = LuminPackCollectionAccess.ConcurrentDictionaryBuckets<TKey, TValue>(_tables!);
            uint bucket = (uint)hash;
            if (bucket >= (uint)buckets.Length)
                bucket = LuminPackMarshal.FastMod(bucket, (uint)buckets.Length, _bucketMultiplier);
            ref object? slot = ref buckets[(int)bucket];
            ref int stripe = ref _counts![LuminPackMarshal.FastMod(bucket, (uint)_counts.Length, _stripeMultiplier)];
            if (slot is null)
            {
                slot = LuminPackCollectionAccess.ConcurrentDictionaryNode(key, value, hash);
                stripe++;
                return;
            }
            AddOccupied(key, hash, value, ref slot, ref stripe);
            return;
        }
#endif
        _map.TryAdd(key, value);
    }

#if NET10_0_OR_GREATER
    /// <summary>Resolves a collision and refreshes the captured table after framework growth.</summary>
    /// <param name="key">The incoming key.</param>
    /// <param name="hash">Its hash under the active comparer.</param>
    /// <param name="value">The incoming value.</param>
    /// <param name="slot">The head of the occupied bucket.</param>
    /// <param name="stripe">The count owned by the corresponding lock.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddOccupied(TKey key, int hash, TValue value, ref object? slot, ref int stripe)
    {
        if (LuminPackCollectionAccess.AddOccupiedConcurrentDictionaryBucket(_map, _tables!, key, hash, value, _comparer, ref slot, ref stripe) &&
            !ReferenceEquals(_tables, LuminPackCollectionAccess.ConcurrentDictionaryTables(_map)))
            Refresh();
    }
#endif

    /// <summary>Returns the completed dictionary.</summary>
    /// <returns>The filled dictionary.</returns>
    public readonly ConcurrentDictionary<TKey, TValue> Complete() => _map;
}
