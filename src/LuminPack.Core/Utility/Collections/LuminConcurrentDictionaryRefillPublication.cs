using System.Collections.Concurrent;
#if NET10_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
#endif

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Tries to replace an existing dictionary with a complete staged record under the framework's locks.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="map">The existing dictionary.</param>
    /// <param name="pairs">The staging array, in decoded order.</param>
    /// <param name="count">The initialized staging prefix.</param>
    /// <returns>Whether the new table was published. A false result leaves the usual clear and add path available.</returns>
    internal static bool TryRefillStagedConcurrentDictionary<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map,
        KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
#if NET10_0_OR_GREATER
        if (count >= 64 && (uint)count <= (uint)pairs.Length && typeof(TKey) == typeof(string) &&
            map.GetType() == typeof(ConcurrentDictionary<TKey, TValue>) && ConcurrentDictionaryRefillPublication<TKey, TValue>.Available)
            return ConcurrentDictionaryRefillPublication<TKey, TValue>.TryReplace(map, pairs.AsSpan(0, count));
#endif
        return false;
    }

#if NET10_0_OR_GREATER
    /// <summary>Publishes a complete staged string-key record through the binary concurrent refill kernel.</summary>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="map">The dictionary whose comparer and lock protocol are retained.</param>
    /// <param name="pairs">The decoded key/value pairs in input order.</param>
    /// <param name="count">The initialized prefix of the staging array.</param>
    /// <returns>Whether publication succeeded. A false result leaves the normal clear and insert path available.</returns>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static bool TryPublishStagedStringDictionary<TValue>(ConcurrentDictionary<string, TValue> map,
        KeyValuePair<string, TValue>[] pairs, int count) => TryRefillStagedConcurrentDictionary(map, pairs, count);

    /// <summary>Checks and extends the framework table publication protocol for an already decoded record.</summary>
    /// <typeparam name="TKey">The exact string key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    private static class ConcurrentDictionaryRefillPublication<TKey, TValue> where TKey : notnull
    {
        private delegate object CreateTable(Array buckets, object[] locks, int[] counts, IEqualityComparer<TKey> comparer);

        private static readonly int Locks;
        private static readonly CreateTable? Create;
        internal static readonly bool Available;

        /// <summary>Binds the framework's tables constructor and lock protocol for the closed dictionary type.</summary>
        static ConcurrentDictionaryRefillPublication()
        {
            if (typeof(TKey) != typeof(string) || !RuntimeFeature.IsDynamicCodeSupported || !ConcurrentConstruction<TKey, TValue>.Available)
                return;

            try
            {
                Type map = typeof(ConcurrentDictionary<TKey, TValue>);
                var tables = LuminFieldLayout.Find(map, "_tables");
                if (tables is null || tables.IsInitOnly || Array.IndexOf(tables.GetRequiredCustomModifiers(), typeof(IsVolatile)) < 0)
                    return;
                var tableType = tables.FieldType;
                var locks = LuminFieldLayout.Find(tableType, "_locks");
                var buckets = LuminFieldLayout.Find(tableType, "_buckets");
                if (locks is not { IsInitOnly: true } || locks.FieldType != typeof(object[]) ||
                    buckets is not { IsInitOnly: true } || !buckets.FieldType.IsSZArray)
                    return;
                Locks = LuminFieldLayout.ObjectField(tableType, "_locks");
                if (Locks < 0)
                    return;
                Create = Bind(tableType, buckets.FieldType);
                Available = Create is not null && Probe();
            }
            catch
            {
                // Keep the public refill on framework versions whose private bindings or probes fail.
            }
        }

        /// <summary>Creates a typed call to the framework tables constructor.</summary>
        /// <param name="tables">The verified table object type.</param>
        /// <param name="buckets">The verified private framework bucket array type.</param>
        /// <returns>The bound constructor, or null if the signature has changed.</returns>
        [RequiresDynamicCode("The verified tables constructor is called from a DynamicMethod.")]
        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Missing private metadata leaves the constructor unbound.")]
        private static CreateTable? Bind(Type tables, Type buckets)
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var constructor = tables.GetConstructor(instance, [buckets, typeof(object[]), typeof(int[]), typeof(IEqualityComparer<TKey>)]);
            if (constructor is null)
                return null;

            var method = new DynamicMethod("LuminConcurrentDictionaryRefillTables", typeof(object),
                [typeof(Array), typeof(object[]), typeof(int[]), typeof(IEqualityComparer<TKey>)],
                typeof(ConcurrentDictionaryRefillPublication<TKey, TValue>).Module, skipVisibility: true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, buckets);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate<CreateTable>();
        }

        /// <summary>Builds unpublished storage and attempts publication against the original table.</summary>
        /// <param name="map">The existing exact framework dictionary.</param>
        /// <param name="pairs">The complete decoded record.</param>
        /// <returns>Whether the replacement was published.</returns>
        internal static bool TryReplace(ConcurrentDictionary<TKey, TValue> map, ReadOnlySpan<KeyValuePair<TKey, TValue>> pairs)
        {
            object original = ReadTable(map);
            object[] locks = LuminFieldLayout.Field<object[]>(original, Locks);
            var comparer = ConcurrentDictionaryComparer<TKey, TValue>(original);
            var publicComparer = map.Comparer;
            if (!ReferenceEquals(publicComparer, EqualityComparer<TKey>.Default) &&
                !ReferenceEquals(publicComparer, StringComparer.Ordinal) && !ReferenceEquals(publicComparer, StringComparer.OrdinalIgnoreCase))
                return false;

            // Checking the actual comparer preserves an existing rehash and keeps user comparer callbacks on the public path.
            var candidate = new ConcurrentDictionary<TKey, TValue>(locks.Length, Math.Max(pairs.Length, 31), publicComparer);
            object candidateTable = ConcurrentDictionaryTables(candidate);
            if (comparer is null || !ReferenceEquals(comparer, ConcurrentDictionaryComparer<TKey, TValue>(candidateTable)))
                return false;

            var builder = new ConcurrentDictionaryBuilder<TKey, TValue>(candidate);
            foreach (ref readonly var pair in pairs)
            {
                // The public refill clears first and exposes the prefix before a null key fails.
                if (pair.Key is null)
                    return false;
                builder.Add(pair.Key, pair.Value);
            }

            candidateTable = ConcurrentDictionaryTables(candidate);
            if (!ReferenceEquals(comparer, ConcurrentDictionaryComparer<TKey, TValue>(candidateTable)))
                return false;
            Array buckets = LuminFieldLayout.Field<Array>(candidateTable, ConcurrentBucketStorage<TKey, TValue>.Buckets);
            int[] counts = ConcurrentDictionaryCounts<TKey, TValue>(candidateTable);
            if (counts.Length != locks.Length)
                return false;

            object replacement = Create!(buckets, locks, counts, comparer);
            return TryPublish(map, original, replacement, locks.Length, buckets.Length);
        }

        /// <summary>Reads the current table with the same acquire ordering as the framework.</summary>
        /// <param name="map">The published dictionary.</param>
        /// <returns>The table observed by this operation.</returns>
        private static object ReadTable(ConcurrentDictionary<TKey, TValue> map) =>
            Volatile.Read(ref LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables));

        /// <summary>Checks the current table under all stripe locks and publishes the replacement with release ordering.</summary>
        /// <param name="map">The existing dictionary.</param>
        /// <param name="original">The table whose comparer and locks were used to build the replacement.</param>
        /// <param name="replacement">The prepared table with the original locks.</param>
        /// <param name="lockCount">The number of original stripe locks.</param>
        /// <param name="bucketCount">The new bucket capacity.</param>
        /// <returns>Whether the prepared table still matched the existing dictionary.</returns>
        private static bool TryPublish(ConcurrentDictionary<TKey, TValue> map, object original, object replacement, int lockCount, int bucketCount)
        {
            int acquired = 0;
            try
            {
                AcquireLocks(map, ref acquired);
                if (acquired != lockCount || !ReferenceEquals(original, ReadTable(map)))
                    return false;

                LuminFieldLayout.Field<int>(map, ConcurrentConstruction<TKey, TValue>.Budget) = Math.Max(1, bucketCount / lockCount);
                Volatile.Write(ref LuminFieldLayout.Field<object>(map, ConcurrentBucketStorage<TKey, TValue>.Tables), replacement);
                return true;
            }
            finally
            {
                ReleaseLocks(map, acquired);
            }
        }

        /// <summary>Checks lock coverage, default comparer behavior and the table's public mutation and enumeration.</summary>
        /// <returns>Whether the probed constructor and locks match this framework.</returns>
        private static bool Probe()
        {
            var comparer = (IEqualityComparer<TKey>)(object)StringComparer.OrdinalIgnoreCase;
            var map = new ConcurrentDictionary<TKey, TValue>(2, 37, comparer);
            var oldKey = (TKey)(object)"original";
            map.TryAdd(oldKey, default!);
            using var prior = map.GetEnumerator();
            if (!prior.MoveNext())
                return false;

            object original = ReadTable(map);
            object[] locks = LuminFieldLayout.Field<object[]>(original, Locks);
            int acquired = 0;
            try
            {
                AcquireLocks(map, ref acquired);
                if (acquired != locks.Length)
                    return false;
                foreach (object stripe in locks)
                    if (!Monitor.IsEntered(stripe))
                        return false;
            }
            finally
            {
                ReleaseLocks(map, acquired);
            }

            KeyValuePair<TKey, TValue>[] pairs = [new((TKey)(object)"first", default!), new((TKey)(object)"FIRST", default!),
                new((TKey)(object)"second", default!)];
            if (!TryReplace(map, pairs))
                return false;
            object replacement = ReadTable(map);
            int newLength = LuminFieldLayout.Field<Array>(replacement, ConcurrentBucketStorage<TKey, TValue>.Buckets).Length;
            return !ReferenceEquals(original, replacement) && ReferenceEquals(locks, LuminFieldLayout.Field<object[]>(replacement, Locks)) &&
                LuminFieldLayout.Field<int>(map, ConcurrentConstruction<TKey, TValue>.Budget) == Math.Max(1, newLength / locks.Length) &&
                ReferenceEquals(map.Comparer, comparer) && map.Count == 2 && !map.ContainsKey(oldKey) && ReferenceEquals(prior.Current.Key, oldKey) &&
                map.TryGetValue((TKey)(object)"FIRST", out _) && map.TryAdd((TKey)(object)"third", default!) && map.TryRemove((TKey)(object)"second", out _);
        }

        /// <summary>Acquires every stripe lock in framework order.</summary>
        /// <param name="map">The existing framework dictionary.</param>
        /// <param name="acquired">Receives the count of successfully acquired locks.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AcquireAllLocks")]
        private static extern void AcquireLocks(ConcurrentDictionary<TKey, TValue> map, ref int acquired);

        /// <summary>Releases the acquired stripe locks using the framework's retained lock array.</summary>
        /// <param name="map">The existing framework dictionary.</param>
        /// <param name="acquired">The count of locks to release.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReleaseLocks")]
        private static extern void ReleaseLocks(ConcurrentDictionary<TKey, TValue> map, int acquired);
    }
#endif
}
