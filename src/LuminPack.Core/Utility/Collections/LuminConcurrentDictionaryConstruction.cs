#if NET10_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Map = System.Collections.Concurrent.ConcurrentDictionary<int, string>;
using Pair = System.Collections.Generic.KeyValuePair<int, string>;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    private static class IntStringConstruction
    {
        private static readonly int Counts;
        private static readonly int Budget;
        internal static readonly bool Available;

        /// <summary>Checks constructor binding, bucket layout, per-lock counts and lookup against the framework.</summary>
        static IntStringConstruction()
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || !ConcurrentDictionaryStorage<int, string>.Available) return;
            try
            {
                var tables = LuminFieldLayout.Find(typeof(Map), "_tables")?.FieldType;
                if (LuminFieldLayout.Find(tables, "_countPerLock")?.FieldType != typeof(int[]) ||
                    LuminFieldLayout.Find(typeof(Map), "_budget")?.FieldType != typeof(int)) return;
                Counts = LuminFieldLayout.ObjectField(tables, "_countPerLock");
                Budget = LuminFieldLayout.ObjectField(typeof(Map), "_budget");
                if ((Counts | Budget) < 0) return;
                var pairs = new Pair[64];
                for (int i = 0; i < pairs.Length; i++) pairs[i] = new Pair(i - 32, string.Empty);
                var expected = new Map(Environment.ProcessorCount, new ArraySegment<Pair>(pairs), null);
                var actual = Build(pairs, pairs.Length);
                Available = expected.SequenceEqual(actual) && actual.Count == pairs.Length &&
                    expected.All(pair => actual.ContainsKey(pair.Key)) && actual.TryRemove(-32, out _) && actual.TryAdd(-32, string.Empty);
            }
            catch { }
        }

        /// <summary>Fills only the private buckets of a newly constructed framework dictionary.</summary>
        /// <param name="pairs">The already parsed pairs.</param>
        /// <param name="count">The validated pair count, at least 32.</param>
        /// <returns>The completed dictionary with the first value for each key.</returns>
        internal static Map Build(Pair[] pairs, int count)
        {
            var map = new Map(Environment.ProcessorCount, count);
            var tables = LuminFieldLayout.Field<object>(map, ConcurrentDictionaryStorage<int, string>.Tables);
            var array = LuminFieldLayout.Field<Array>(tables, ConcurrentDictionaryStorage<int, string>.Buckets);
            var counts = LuminFieldLayout.Field<int[]>(tables, Counts);
            int budget = LuminFieldLayout.Field<int>(map, Budget);
            // The traversal probe proves each bucket contains one managed reference at offset zero.
            var buckets = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, object?>(ref Unsafe.As<byte[]>(array)[0]), array.Length);
            if (TryFillConsecutive(pairs, count, buckets, counts, budget)) return map;
            uint width = (uint)buckets.Length;
            for (int i = 0; i < count; i++)
            {
                Pair pair = pairs[i];
                uint bucket = (uint)pair.Key;
                if (bucket >= width) bucket %= width;
                ref int stripeCount = ref counts[bucket % (uint)counts.Length];
                ref object? slot = ref buckets[(int)bucket];
                if (slot is not null || stripeCount >= budget)
                {
                    for (; i < count; i++) map.TryAdd(pairs[i].Key, pairs[i].Value);
                    break;
                }
                slot = ConcurrentNodes<int, string>.New(pair.Key, pair.Value, pair.Key, null);
                stripeCount++;
            }
            return map;
        }

        /// <summary>Links a consecutive range of empty buckets and computes their stripe counts once.</summary>
        /// <param name="pairs">The ordered input candidates.</param>
        /// <param name="count">The positive number of pairs.</param>
        /// <param name="buckets">The validated, empty bucket storage.</param>
        /// <param name="counts">The empty per-lock counts.</param>
        /// <param name="budget">The growth threshold per lock.</param>
        /// <returns>Whether the complete input fit without collisions, wrap or growth.</returns>
        private static bool TryFillConsecutive(Pair[] pairs, int count, Span<object?> buckets, int[] counts, int budget)
        {
            uint firstKey = (uint)pairs[0].Key;
            int firstBucket = (int)(firstKey % (uint)buckets.Length);
            if (count > buckets.Length - firstBucket || (uint)(count - 1) > uint.MaxValue - firstKey ||
                1 + (count - 1) / counts.Length > budget) return false;
            for (int i = 1; i < count; i++)
                if ((uint)pairs[i].Key != firstKey + (uint)i) return false;
            var range = buckets.Slice(firstBucket, count);
            for (int i = 0; i < range.Length; i++)
            {
                Pair pair = pairs[i];
                range[i] = ConcurrentNodes<int, string>.New(pair.Key, pair.Value, pair.Key, null);
            }
            Array.Fill(counts, count / counts.Length);
            int stripe = firstBucket % counts.Length;
            int remainder = count % counts.Length;
            for (int i = 0; i < remainder; i++)
            {
                counts[stripe]++;
                if (++stripe == counts.Length) stripe = 0;
            }
            return true;
        }
    }

    private static class ConcurrentNodes<TKey, TValue>
    {
        private const string Node = "System.Collections.Concurrent.ConcurrentDictionary`2+Node[[!0],[!1]], System.Collections.Concurrent";

        /// <summary>Calls the framework node constructor with its closed generic parameter signature.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="hash">The framework key hash.</param>
        /// <param name="next">The next node in the bucket.</param>
        /// <returns>The initialized node.</returns>
        [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
        [return: UnsafeAccessorType(Node)]
        internal static extern object New(TKey key, TValue value, int hash, [UnsafeAccessorType(Node)] object? next);
    }
}
#endif
