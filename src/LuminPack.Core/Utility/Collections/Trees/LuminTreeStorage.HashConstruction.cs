using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LuminPack.Code;
using static LuminPack.Code.LuminFieldLayout;

namespace LuminPack.Utility;

public static partial class LuminTreeStorage
{
    /// <summary>Builds a framework-element hash set including collision buckets from a completed element buffer.</summary>
    /// <typeparam name="T">An element whose default hashing and equality have no user callbacks.</typeparam>
    /// <param name="items">The original element order; its storage is never modified or retained.</param>
    /// <param name="count">The number of complete elements.</param>
    /// <returns>The completed default-comparer set.</returns>
    /// <remarks>Generated callers select only framework scalar or string elements. User callbacks retain their original interleaved construction.</remarks>
    public static ImmutableHashSet<T> CreateFrameworkImmutableHashSet<T>(T[] items, int count)
    {
        if ((uint)count > (uint)items.Length) LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
        if (count == 0) return ImmutableHashSet<T>.Empty;
        if (count == 1) return ImmutableHashSet.Create(items[0]);
        if (ImmutableHashSetLayout<T>.Available && GroupedHashLayout.Available)
            return count == 2 ? BuildFrameworkHashPair(items[0], items[1]) : BuildFrameworkHash(items, count);
        return CreateImmutableHashSet(items, count);
    }

    /// <summary>Builds a framework-element pair without pooled element or ordering storage.</summary>
    /// <typeparam name="T">An element whose default hashing and equality have no user callbacks.</typeparam>
    /// <param name="first">The first decoded element, retained when both elements compare equal.</param>
    /// <param name="second">The second decoded element.</param>
    /// <returns>The completed default-comparer set.</returns>
    public static ImmutableHashSet<T> CreateFrameworkImmutableHashSet<T>(T first, T second)
    {
        if (ImmutableHashSetLayout<T>.Available && GroupedHashLayout.Available)
            return BuildFrameworkHashPair(first, second);
        var builder = LuminImmutableBuilders.RentHashSet<T>();
        try
        {
            builder.Add(first);
            builder.Add(second);
            return builder.ToImmutable();
        }
        finally { LuminImmutableBuilders.ReturnHashSet(builder); }
    }

    /// <summary>Completes a fresh singleton with at most one additional hash or collision node.</summary>
    /// <typeparam name="T">The validated framework element type.</typeparam>
    /// <param name="first">The element used by the singleton factory.</param>
    /// <param name="second">The element to add when distinct.</param>
    /// <returns>The completed result without modifying any published collection or empty node.</returns>
    private static ImmutableHashSet<T> BuildFrameworkHashPair<T>(T first, T second)
    {
        var result = ImmutableHashSet.Create(first);
        var offsets = ImmutableHashSetLayout<T>.Hash;
        object root = Field<object>(result, ImmutableHashSetLayout<T>.Root);
        int firstHash = Field<int>(root, offsets.Tree.Key);
        int secondHash = second is null ? 0 : EqualityComparer<T>.Default.GetHashCode(second);
        ref byte rootBucket = ref Field<byte>(root, offsets.Tree.Value);
        object emptyList = Field<object>(ref rootBucket, offsets.Additional);
        if (firstHash == secondHash)
        {
            if (EqualityComparer<T>.Default.Equals(first, second)) return result;
            object tail = offsets.List.New(emptyList);
            Field<T>(tail, offsets.List.Key) = second;
            Seal(tail, emptyList, emptyList, in offsets.List);
            Field<object>(ref rootBucket, offsets.Additional) = tail;
        }
        else
        {
            object empty = Field<object>(root, offsets.Tree.Left);
            object child = offsets.Tree.New(empty);
            Field<int>(child, offsets.Tree.Key) = secondHash;
            ref byte childBucket = ref Field<byte>(child, offsets.Tree.Value);
            Field<T>(ref childBucket, offsets.First) = second;
            Field<object>(ref childBucket, offsets.Additional) = emptyList;
            Seal(child, empty, empty, in offsets.Tree);
            Seal(root, secondHash < firstHash ? child : empty, secondHash < firstHash ? empty : child, in offsets.Tree);
        }
        Field<int>(result, ImmutableHashSetLayout<T>.Count) = 2;
        return result;
    }

    /// <summary>Orders hashes and constructs only the final nodes, preserving the first element of each equality class.</summary>
    /// <typeparam name="T">The validated framework element type.</typeparam>
    /// <param name="items">The original decoded element storage, never modified or retained.</param>
    /// <param name="count">The positive valid element count.</param>
    /// <returns>The completed immutable hash set.</returns>
    private static ImmutableHashSet<T> BuildFrameworkHash<T>(T[] items, int count)
    {
        long[] keys = ArrayPool<long>.Shared.Rent(count);
        try
        {
            if (TrySortHashes(items, count, keys))
            {
                var result = ImmutableHashSet.Create(items[0]);
                BuildHash<T>(Field<object>(result, ImmutableHashSetLayout<T>.Root), in ImmutableHashSetLayout<T>.Hash, items, keys, count);
                Field<int>(result, ImmutableHashSetLayout<T>.Count) = count;
                return result;
            }

            // TrySortHashes leaves all packed hash/index keys ordered even when hashes collide.
            int[] groups = ArrayPool<int>.Shared.Rent(count);
            try
            {
                int unique = 0, groupCount = 0;
                var comparer = EqualityComparer<T>.Default;
                for (int start = 0; start < count;)
                {
                    int hash = (int)(keys[start] >> 32);
                    int end = start + 1;
                    while (end < count && (int)(keys[end] >> 32) == hash) end++;
                    int first = unique;
                    groups[groupCount++] = first;
                    for (int index = start; index < end; index++)
                    {
                        long key = keys[index];
                        T item = items[(int)(uint)key];
                        int prior = first;
                        while (prior < unique && !comparer.Equals(items[(int)(uint)keys[prior]], item)) prior++;
                        if (prior == unique) keys[unique++] = key;
                    }
                    start = end;
                }
                var result = ImmutableHashSet.Create(items[0]);
                object root = Field<object>(result, ImmutableHashSetLayout<T>.Root);
                var offsets = ImmutableHashSetLayout<T>.Hash;
                object empty = Field<object>(root, offsets.Tree.Left);
                object emptyList = Field<object>(ref Field<byte>(root, offsets.Tree.Value), offsets.Additional);
                FillGroupedHash(root, empty, emptyList, in offsets, items, keys, groups, groupCount, unique, 0, groupCount - 1);
                Field<int>(result, ImmutableHashSetLayout<T>.Count) = unique;
                return result;
            }
            finally { ArrayPool<int>.Shared.Return(groups); }
        }
        finally { ArrayPool<long>.Shared.Return(keys); }
    }

    /// <summary>Completes one unpublished hash node and its balanced child subtrees.</summary>
    /// <typeparam name="T">The framework element type.</typeparam>
    /// <param name="node">The hash node to fill, including the retained singleton root.</param>
    /// <param name="empty">The hash-tree empty sentinel.</param>
    /// <param name="emptyList">The collision-list empty sentinel.</param>
    /// <param name="offsets">The validated hash and list node offsets.</param>
    /// <param name="items">The decoded elements.</param>
    /// <param name="keys">The compacted unique packed hashes and original indices.</param>
    /// <param name="groups">The starting key index of each hash bucket.</param>
    /// <param name="groupCount">The number of distinct hash buckets.</param>
    /// <param name="unique">The number of unique elements.</param>
    /// <param name="start">The inclusive first bucket index.</param>
    /// <param name="end">The inclusive last bucket index.</param>
    private static void FillGroupedHash<T>(object node, object empty, object emptyList, in HashOffsets offsets,
        T[] items, long[] keys, int[] groups, int groupCount, int unique, int start, int end)
    {
        int middle = start + ((end - start) >> 1);
        int first = groups[middle];
        int last = middle + 1 < groupCount ? groups[middle + 1] : unique;
        long key = keys[first];
        Field<int>(node, offsets.Tree.Key) = (int)(key >> 32);
        ref byte bucket = ref Field<byte>(node, offsets.Tree.Value);
        Field<T>(ref bucket, offsets.First) = items[(int)(uint)key];
        Field<object>(ref bucket, offsets.Additional) = LinkHashValues(emptyList, in offsets.List, items, keys, first + 1, last - 1);
        object left = LinkGroupedHash(empty, emptyList, in offsets, items, keys, groups, groupCount, unique, start, middle - 1);
        object right = LinkGroupedHash(empty, emptyList, in offsets, items, keys, groups, groupCount, unique, middle + 1, end);
        Seal(node, left, right, in offsets.Tree);
    }

    /// <summary>Returns the empty sentinel or a fresh completed hash subtree.</summary>
    /// <typeparam name="T">The framework element type.</typeparam>
    /// <param name="empty">The hash-tree empty sentinel.</param>
    /// <param name="emptyList">The collision-list empty sentinel.</param>
    /// <param name="offsets">The validated hash and list node offsets.</param>
    /// <param name="items">The decoded elements.</param>
    /// <param name="keys">The compacted unique packed hashes and original indices.</param>
    /// <param name="groups">The starting key index of each hash bucket.</param>
    /// <param name="groupCount">The number of distinct hash buckets.</param>
    /// <param name="unique">The number of unique elements.</param>
    /// <param name="start">The inclusive first bucket index.</param>
    /// <param name="end">The inclusive last bucket index.</param>
    /// <returns>The immutable subtree root.</returns>
    private static object LinkGroupedHash<T>(object empty, object emptyList, in HashOffsets offsets,
        T[] items, long[] keys, int[] groups, int groupCount, int unique, int start, int end)
    {
        if (start > end) return empty;
        object node = offsets.Tree.New(empty);
        FillGroupedHash(node, empty, emptyList, in offsets, items, keys, groups, groupCount, unique, start, end);
        return node;
    }

    /// <summary>Builds the collision list with the topology of sequential AVL appends.</summary>
    /// <typeparam name="T">The framework element type.</typeparam>
    /// <param name="empty">The collision-list empty sentinel.</param>
    /// <param name="offsets">The validated list node offsets.</param>
    /// <param name="items">The decoded elements.</param>
    /// <param name="keys">The compacted unique elements in original bucket order.</param>
    /// <param name="start">The inclusive first key index.</param>
    /// <param name="end">The inclusive last key index.</param>
    /// <returns>The immutable list root, retaining the BCL's root-promotion behavior after removal.</returns>
    private static object LinkHashValues<T>(object empty, in NodeOffsets offsets, T[] items, long[] keys, int start, int end)
    {
        if (start > end) return empty;
        // Sequential AVL appends leave a complete left subtree. Its size changes
        // at 3, 6, 12, ... nodes, when the root rotates. Preserve that topology:
        // HashBucket.Remove promotes this list's root into the first-value slot.
        int width = 1;
        int limit = (end - start + 1) / 3;
        while (width <= limit) width <<= 1;
        int middle = start + width - 1;
        object node = offsets.New(empty);
        Field<T>(node, offsets.Key) = items[(int)(uint)keys[middle]];
        object left = LinkHashValues(empty, in offsets, items, keys, start, middle - 1);
        object right = LinkHashValues(empty, in offsets, items, keys, middle + 1, end);
        Seal(node, left, right, in offsets);
        return node;
    }

    /// <summary>Validates collision construction and mutation once, in addition to each closed element layout.</summary>
    private static class GroupedHashLayout
    {
        internal static readonly bool Available = Check(Probe);

        /// <summary>Compares collision lists, pairs and persistent mutations with public factory results.</summary>
        /// <returns>Whether the direct construction preserves the framework's observable behavior.</returns>
        private static bool Probe()
        {
            if (!ImmutableHashSetLayout<long>.Available) return false;
            long[] items = Enumerable.Range(0, 65).Select(i => ((long)i << 32) | (uint)i).Concat(new long[] { -1, 0, 7, 11, 23, 7 }).ToArray();
            var reference = ImmutableHashSet.CreateRange(items);
            var result = BuildFrameworkHash(items, items.Length);
            if (result.Count != reference.Count || !result.SequenceEqual(reference)) return false;
            foreach (long item in items)
            {
                if (!result.Contains(item) || !result.Remove(item).SequenceEqual(reference.Remove(item))) return false;
            }
            foreach (var pair in new[] { new long[] { 0, 0 }, new long[] { 0, 0x100000001 }, new long[] { 3, 7 }, new long[] { 7, 3 }, new long[] { -1, 0 } })
            {
                var expected = ImmutableHashSet.CreateRange(pair);
                var actual = BuildFrameworkHashPair(pair[0], pair[1]);
                if (!actual.SequenceEqual(expected) || !actual.Remove(pair[0]).SequenceEqual(expected.Remove(pair[0]))) return false;
            }
            var changed = result.Add(17).Remove(0).Add(0);
            return changed.SequenceEqual(reference.Add(17).Remove(0).Add(0)) && result.SequenceEqual(reference);
        }
    }
}
