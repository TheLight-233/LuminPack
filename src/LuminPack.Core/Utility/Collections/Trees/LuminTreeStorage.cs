using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using static LuminPack.Code.LuminFieldLayout;

namespace LuminPack.Utility;

/// <summary>Copies and builds tree-backed framework collections through their node storage.</summary>
/// <remarks>
/// <para>Generated formatters call these members. Each copy yields exactly the collection's enumeration order, so the written
/// bytes are unchanged; each build produces a tree that answers every operation as the collection's own construction does.</para>
/// <para>The fields of each collection and node type are located at run time by <see cref="LuminFieldLayout"/> for the closed
/// types in use, then the family is checked once through its public API. A field that cannot be located, a failed check, a
/// derived or view collection, input that is not strictly ordered, equal hash codes and duplicates use the public API, which
/// keeps every exception of the original paths. Writes into destinations stay bounds-checked.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminTreeStorage
{
    /// <summary>Copies a sorted set's items in order.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="set">The set to copy.</param>
    /// <param name="destination">Storage for at least <c>set.Count</c> elements.</param>
    public static void CopyTo<T>(SortedSet<T> set, Span<T> destination)
    {
        if (set.GetType() == typeof(SortedSet<T>) && SortedSetLayout<T>.Available)
        {
            var nodes = SortedSetLayout<T>.Nodes;
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                WalkRedBlackValue(Field<object?>(set, SortedSetLayout<T>.Root), in nodes,
                    ref MemoryMarshal.GetReference(destination), destination.Length, 0);
                return;
            }
            int index = 0;
            WalkRedBlack(Field<object?>(set, SortedSetLayout<T>.Root), in nodes, destination, ref index);
            return;
        }
        int position = 0;
        foreach (var item in set) destination[position++] = item;
    }

    /// <summary>Copies a sorted dictionary's pairs in key order.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary to copy.</param>
    /// <param name="destination">Storage for at least <c>dictionary.Count</c> pairs.</param>
    public static void CopyTo<TKey, TValue>(SortedDictionary<TKey, TValue> dictionary, Span<KeyValuePair<TKey, TValue>> destination) where TKey : notnull
    {
        if (dictionary.GetType() == typeof(SortedDictionary<TKey, TValue>) && SortedDictionaryLayout<TKey, TValue>.Available)
        {
            var nodes = SortedSetLayout<KeyValuePair<TKey, TValue>>.Nodes;
            object set = Field<object>(dictionary, SortedDictionaryLayout<TKey, TValue>.Set);
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey, TValue>>())
            {
                WalkRedBlackValue(Field<object?>(set, SortedSetLayout<KeyValuePair<TKey, TValue>>.Root), in nodes,
                    ref MemoryMarshal.GetReference(destination), destination.Length, 0);
                return;
            }
            int index = 0;
            WalkRedBlack(Field<object?>(set, SortedSetLayout<KeyValuePair<TKey, TValue>>.Root), in nodes, destination, ref index);
            return;
        }
        int position = 0;
        foreach (var pair in dictionary) destination[position++] = pair;
    }

    /// <summary>Copies an immutable sorted set's items in order.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="set">The set to copy.</param>
    /// <param name="destination">Storage for at least <c>set.Count</c> elements.</param>
    public static void CopyTo<T>(ImmutableSortedSet<T> set, Span<T> destination)
    {
        if (ImmutableSortedSetLayout<T>.Available)
        {
            var nodes = ImmutableSortedSetLayout<T>.Nodes;
            int index = 0;
            WalkAvl<T>(Field<object>(set, ImmutableSortedSetLayout<T>.Root), in nodes, destination, ref index);
            return;
        }
        int position = 0;
        foreach (var item in set) destination[position++] = item;
    }

    /// <summary>Copies an immutable sorted dictionary's pairs in key order.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary to copy.</param>
    /// <param name="destination">Storage for at least <c>dictionary.Count</c> pairs.</param>
    public static void CopyTo<TKey, TValue>(ImmutableSortedDictionary<TKey, TValue> dictionary, Span<KeyValuePair<TKey, TValue>> destination) where TKey : notnull
    {
        if (ImmutableSortedDictionaryLayout<TKey, TValue>.Available)
        {
            var nodes = ImmutableSortedDictionaryLayout<TKey, TValue>.Nodes;
            int index = 0;
            WalkPairs<TKey, TValue>(Field<object>(dictionary, ImmutableSortedDictionaryLayout<TKey, TValue>.Root), in nodes, destination, ref index);
            return;
        }
        int position = 0;
        foreach (var pair in dictionary) destination[position++] = pair;
    }

    /// <summary>Copies an immutable hash set's items in its enumeration order.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="set">The set to copy.</param>
    /// <param name="destination">Storage for at least <c>set.Count</c> elements.</param>
    public static void CopyTo<T>(ImmutableHashSet<T> set, Span<T> destination)
    {
        if (ImmutableHashSetLayout<T>.Available)
        {
            var hash = ImmutableHashSetLayout<T>.Hash;
            WalkHash(Field<object>(set, ImmutableHashSetLayout<T>.Root), in hash,
                ref MemoryMarshal.GetReference(destination), destination.Length, 0);
            return;
        }
        int position = 0;
        foreach (var item in set) destination[position++] = item;
    }

    /// <summary>Copies an immutable dictionary's pairs in its enumeration order.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary to copy.</param>
    /// <param name="destination">Storage for at least <c>dictionary.Count</c> pairs.</param>
    public static void CopyTo<TKey, TValue>(ImmutableDictionary<TKey, TValue> dictionary, Span<KeyValuePair<TKey, TValue>> destination) where TKey : notnull
    {
        if (ImmutableDictionaryLayout<TKey, TValue>.Available)
        {
            var hash = ImmutableDictionaryLayout<TKey, TValue>.Hash;
            WalkHash(Field<object>(dictionary, ImmutableDictionaryLayout<TKey, TValue>.Root), in hash,
                ref MemoryMarshal.GetReference(destination), destination.Length, 0);
            return;
        }
        int position = 0;
        foreach (var pair in dictionary) destination[position++] = pair;
    }

    /// <summary>Writes a sorted set of raw elements as the count followed by the elements in order.</summary>
    /// <typeparam name="T">The raw element type, written as its storage.</typeparam>
    /// <param name="writer">The writer receiving the record.</param>
    /// <param name="set">The non-null set.</param>
    /// <remarks>The node walk stores into the reserved output directly where a typed store of the element is permitted.</remarks>
    public static void WriteRaw<T>(ref LuminPackWriter writer, SortedSet<T> set) where T : unmanaged
    {
        int count = set.Count;
        if (TryReserveRaw<T>(ref writer, count, out var destination))
        {
            CopyTo(set, destination);
            CompleteRaw<T>(ref writer, count);
            return;
        }
        var pooled = System.Buffers.ArrayPool<T>.Shared.Rent(count);
        CopyTo(set, pooled.AsSpan(0, count));
        writer.Write<T>(new ReadOnlySpan<T>(pooled, 0, count), count);
        System.Buffers.ArrayPool<T>.Shared.Return(pooled);
    }

    /// <summary>Writes an immutable sorted set of raw elements as the count followed by the elements in order.</summary>
    /// <typeparam name="T">The raw element type, written as its storage.</typeparam>
    /// <param name="writer">The writer receiving the record.</param>
    /// <param name="set">The non-null set.</param>
    /// <remarks>The node walk stores into the reserved output directly where a typed store of the element is permitted.</remarks>
    public static void WriteRaw<T>(ref LuminPackWriter writer, ImmutableSortedSet<T> set) where T : unmanaged
    {
        int count = set.Count;
        if (TryReserveRaw<T>(ref writer, count, out var destination))
        {
            CopyTo(set, destination);
            CompleteRaw<T>(ref writer, count);
            return;
        }
        var pooled = System.Buffers.ArrayPool<T>.Shared.Rent(count);
        CopyTo(set, pooled.AsSpan(0, count));
        writer.Write<T>(new ReadOnlySpan<T>(pooled, 0, count), count);
        System.Buffers.ArrayPool<T>.Shared.Return(pooled);
    }

    /// <summary>Writes an immutable hash set of raw elements as the count followed by the elements in enumeration order.</summary>
    /// <typeparam name="T">The raw element type, written as its storage.</typeparam>
    /// <param name="writer">The writer receiving the record.</param>
    /// <param name="set">The non-null set.</param>
    /// <remarks>The hash-tree walk stores into the reserved output directly where a typed store of the element is permitted.</remarks>
    public static void WriteRaw<T>(ref LuminPackWriter writer, ImmutableHashSet<T> set) where T : unmanaged
    {
        int count = set.Count;
        if (TryReserveRaw<T>(ref writer, count, out var destination))
        {
            CopyTo(set, destination);
            CompleteRaw<T>(ref writer, count);
            return;
        }
        var pooled = System.Buffers.ArrayPool<T>.Shared.Rent(count);
        CopyTo(set, pooled.AsSpan(0, count));
        writer.Write<T>(new ReadOnlySpan<T>(pooled, 0, count), count);
        System.Buffers.ArrayPool<T>.Shared.Return(pooled);
    }

    /// <summary>Reserves a raw record and writes its count, exposing the element block as typed storage.</summary>
    /// <remarks>On 32-bit targets only elements of at most four bytes use typed stores; larger ones keep the block copy.</remarks>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="writer">The writer receiving the record.</param>
    /// <param name="count">The number of items.</param>
    /// <param name="destination">Storage receiving the items in enumeration order.</param>
    /// <returns>Whether the operation used the located storage.</returns>
    private static bool TryReserveRaw<T>(ref LuminPackWriter writer, int count, out Span<T> destination) where T : unmanaged
    {
#if NET8_0_OR_GREATER
        if (Unsafe.SizeOf<IntPtr>() == 8 || Unsafe.SizeOf<T>() <= 4)
        {
            writer.EnsureAdditionalCapacity(checked(sizeof(int) + count * Unsafe.SizeOf<T>()));
            LuminPackWriter.Write<int>(ref writer.Cursor, count);
            destination = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(
                ref Unsafe.As<byte, T>(ref Unsafe.Add(ref writer.Cursor, sizeof(int))), count);
            return true;
        }
#endif
        destination = default;
        return false;
    }

    /// <summary>Advances past a reserved raw record through the established completed-position check.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="writer">The writer receiving the record.</param>
    /// <param name="count">The number of items.</param>
    private static void CompleteRaw<T>(ref LuminPackWriter writer, int count) where T : unmanaged
        => writer.AdvanceSafe(sizeof(int) + count * Unsafe.SizeOf<T>());

    /// <summary>Builds a fresh default-comparer set from strictly ascending elements without a second array.</summary>
    /// <typeparam name="T">The framework element storage selected by the generator.</typeparam>
    /// <param name="items">The fully parsed elements, which remain owned by the caller.</param>
    /// <param name="count">The valid element count.</param>
    /// <returns>A fresh default-comparer set with the original constructor's element semantics.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static SortedSet<T> CreateSortedSet<T>(T[] items, int count)
    {
        if ((uint)count > (uint)items.Length) LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
        if (count == 0) return new SortedSet<T>();
#if NET8_0_OR_GREATER
        // Native publication retains the public construction rather than rooting a new dynamic node allocator.
        if (!RuntimeFeature.IsDynamicCodeSupported) return new SortedSet<T>(new ArraySegment<T>(items, 0, count));
#endif
        if (SortedSetLayout<T>.Available && (count == 1 || IsStrictlyAscending(items, count)))
        {
            var result = new SortedSet<T>();
            var offsets = SortedSetLayout<T>.Nodes;
            object? root = BuildRedBlack(in offsets, items, count);
            Field<object?>(result, SortedSetLayout<T>.Root) = root;
            Field<int>(result, SortedSetLayout<T>.Count) = count;
            return result;
        }
        return new SortedSet<T>(new ArraySegment<T>(items, 0, count));
    }

    /// <summary>Checks whether an exact empty-or-mutable default-comparer set supports ordered reconstruction.</summary>
    /// <typeparam name="T">The statically approved framework element type.</typeparam>
    /// <param name="set">The original destination.</param>
    /// <returns>Whether direct ordered reconstruction is available without replacing the destination.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool CanRebuildSortedSet<T>(SortedSet<T> set)
    {
#if NET8_0_OR_GREATER
        if (!RuntimeFeature.IsDynamicCodeSupported) return false;
#endif
        return set.GetType() == typeof(SortedSet<T>) && ReferenceEquals(set.Comparer, Comparer<T>.Default) &&
            (typeof(T) != typeof(string) || System.Globalization.CultureInfo.CurrentCulture.GetType() == typeof(System.Globalization.CultureInfo)) && SortedSetLayout<T>.Available;
    }

    /// <summary>Links ordered elements into an already cleared destination, retaining its comparer and identity.</summary>
    /// <typeparam name="T">The statically approved framework element type.</typeparam>
    /// <param name="set">The cleared exact default-comparer set.</param>
    /// <param name="items">The complete parsed element block, owned by the caller.</param>
    /// <param name="count">The valid element count.</param>
    /// <returns>Whether direct construction succeeded; false leaves the set empty for original Add semantics.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryRebuildSortedSet<T>(SortedSet<T> set, T[] items, int count)
    {
        if ((uint)count > (uint)items.Length) LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
        if (set.Count != 0 || !CanRebuildSortedSet(set)) return false;
        if (count == 0) return true;
        if (count > 1 && !IsStrictlyAscending(items, count)) return false;
        var offsets = SortedSetLayout<T>.Nodes;
        object? root = BuildRedBlack(in offsets, items, count);
        Field<object?>(set, SortedSetLayout<T>.Root) = root;
        Field<int>(set, SortedSetLayout<T>.Count) = count;
        Field<int>(set, SortedSetLayout<T>.Version) = unchecked(Field<int>(set, SortedSetLayout<T>.Version) + count);
        return true;
    }

    /// <summary>Builds an immutable sorted set with the default comparer from items.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The items, in the order they were read.</param>
    /// <param name="count">The number of items, at least one.</param>
    /// <returns>A set equal to the one its sorted construction produces from the same items.</returns>
    /// <remarks>Strictly ascending items link a balanced tree of frozen nodes in one pass; any other input uses the sorted
    /// construction, which sorts and removes duplicates.</remarks>
    public static ImmutableSortedSet<T> CreateImmutableSortedSet<T>(T[] items, int count)
    {
#if NET10_0_OR_GREATER
        if (count > 1 && BclTrees.ImmutableSortedSet<T>.Available && IsStrictlyAscending(items, count))
            return BclTrees.ImmutableSortedSet<T>.Build(items, count);
#endif
        if (count > 1 && ImmutableSortedSetLayout<T>.Available && IsStrictlyAscending(items, count))
        {
            var template = ImmutableSortedSet.Create(items[0]);
            var offsets = ImmutableSortedSetLayout<T>.Nodes;
            object node = Field<object>(template, ImmutableSortedSetLayout<T>.Root);
            object root = LinkItems(Field<object>(node, offsets.Left), in offsets, items, 0, count - 1);
            object result = Clone(template);
            Field<object>(result, ImmutableSortedSetLayout<T>.Root) = root;
            return Unsafe.As<ImmutableSortedSet<T>>(result);
        }
        return ImmutableSortedSet.CreateRange(new ArraySegment<T>(items, 0, count));
    }

    /// <summary>Links an AVL subtree of new frozen item nodes with subtree counts.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="empty">The collection's shared empty node.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="items">The items, in the order they were read or stored.</param>
    /// <param name="start">The first index of the range.</param>
    /// <param name="end">The last index of the range.</param>
    /// <returns>The root of the linked subtree.</returns>
    private static object LinkItems<T>(object empty, in NodeOffsets offsets, T[] items, int start, int end)
    {
        if (start > end) return empty;
        int middle = start + ((end - start) >> 1);
        object node = offsets.New();
        Field<T>(node, offsets.Key) = items[middle];
        object left = LinkItems(empty, in offsets, items, start, middle - 1);
        object right = LinkItems(empty, in offsets, items, middle + 1, end);
        Seal(node, left, right, in offsets);
        return node;
    }

    /// <summary>Checks that items ascend strictly under the default comparer, the order of a sorted set's own writes.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The items, in the order they were read or stored.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>Whether the condition holds.</returns>
    private static bool IsStrictlyAscending<T>(T[] items, int count)
    {
        var comparer = Comparer<T>.Default;
        for (int i = 1; i < count; i++)
            if (comparer.Compare(items[i - 1], items[i]) >= 0) return false;
        return true;
    }

    /// <summary>Builds an immutable sorted dictionary with the default comparers from pairs.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of pairs, at least one.</param>
    /// <returns>A dictionary equal to the one the builder produces from the same pairs.</returns>
    /// <remarks>Strictly ascending keys link a balanced tree of frozen nodes in one pass; any other input adds the pairs through
    /// the builder, which throws for duplicate keys as before.</remarks>
    public static ImmutableSortedDictionary<TKey, TValue> CreateImmutableSortedDictionary<TKey, TValue>(KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
        if (ImmutableSortedDictionaryLayout<TKey, TValue>.Available && IsStrictlyAscending(pairs, count))
            return BuildSortedDictionary(pairs, count);
        var builder = ImmutableSortedDictionary.CreateBuilder<TKey, TValue>();
        for (int i = 0; i < count; i++) builder.Add(pairs[i].Key, pairs[i].Value);
        return builder.ToImmutable();
    }

    /// <summary>Fills a new, empty sorted dictionary with the default comparer from pairs.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">An empty dictionary; one with another comparer is filled pair by pair.</param>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of pairs.</param>
    /// <remarks>Strictly ascending keys link the red-black tree that the sorted-array construction of <see cref="SortedSet{T}"/>
    /// builds; any other input is added pair by pair, which throws for duplicate keys as before.</remarks>
    public static void FillSortedDictionary<TKey, TValue>(SortedDictionary<TKey, TValue> dictionary, KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
        if (count > 1 && dictionary.GetType() == typeof(SortedDictionary<TKey, TValue>) && dictionary.Count == 0 &&
            ReferenceEquals(dictionary.Comparer, Comparer<TKey>.Default) && SortedDictionaryLayout<TKey, TValue>.Available && IsStrictlyAscending(pairs, count))
        {
            LinkSortedDictionary(dictionary, pairs, count);
            return;
        }
        for (int i = 0; i < count; i++) dictionary.Add(pairs[i].Key, pairs[i].Value);
    }

    /// <summary>Builds an immutable hash set with the default comparer from items.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The items, in the order they were read.</param>
    /// <param name="count">The number of items, at least one.</param>
    /// <returns>A set equal to the one the builder produces from the same items.</returns>
    /// <remarks>Items with distinct hash codes are linked into the hash tree in one pass; equal hash codes, which include
    /// duplicates, add the items through the builder.</remarks>
    public static ImmutableHashSet<T> CreateImmutableHashSet<T>(T[] items, int count)
    {
        if (count > 1 && ImmutableHashSetLayout<T>.Available && TrySortHashes(items, count, out var keys))
        {
            var template = ImmutableHashSet.Create(items[0]);
            object root = BuildHash<T>(Field<object>(template, ImmutableHashSetLayout<T>.Root), in ImmutableHashSetLayout<T>.Hash, items, keys, count);
            System.Buffers.ArrayPool<long>.Shared.Return(keys);
            object result = Clone(template);
            Field<object>(result, ImmutableHashSetLayout<T>.Root) = root;
            Field<int>(result, ImmutableHashSetLayout<T>.Count) = count;
            return Unsafe.As<ImmutableHashSet<T>>(result);
        }
        var builder = ImmutableHashSet.CreateBuilder<T>();
        for (int i = 0; i < count; i++) builder.Add(items[i]);
        return builder.ToImmutable();
    }

    /// <summary>Builds an immutable dictionary with the default comparers from pairs.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of pairs, at least one.</param>
    /// <returns>A dictionary equal to the one the builder produces from the same pairs.</returns>
    /// <remarks>Keys with distinct hash codes are linked into the hash tree in one pass; equal hash codes, which include
    /// duplicate keys, add the pairs through the builder, which throws for duplicates as before.</remarks>
    public static ImmutableDictionary<TKey, TValue> CreateImmutableDictionary<TKey, TValue>(KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
        if (count > 1 && ImmutableDictionaryLayout<TKey, TValue>.Available && TrySortHashes(pairs, count, out var keys))
        {
            var template = ImmutableDictionary<TKey, TValue>.Empty.Add(pairs[0].Key, pairs[0].Value);
            object root = BuildHash<KeyValuePair<TKey, TValue>>(Field<object>(template, ImmutableDictionaryLayout<TKey, TValue>.Root),
                in ImmutableDictionaryLayout<TKey, TValue>.Hash, pairs, keys, count);
            System.Buffers.ArrayPool<long>.Shared.Return(keys);
            object result = Clone(template);
            Field<object>(result, ImmutableDictionaryLayout<TKey, TValue>.Root) = root;
            Field<int>(result, ImmutableDictionaryLayout<TKey, TValue>.Count) = count;
            return Unsafe.As<ImmutableDictionary<TKey, TValue>>(result);
        }
        var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>();
        for (int i = 0; i < count; i++) builder.Add(pairs[i].Key, pairs[i].Value);
        return builder.ToImmutable();
    }

    // Each walk keeps its offsets in locals: a store through the destination could otherwise alias the offsets and force reloads.

    /// <summary>Copies a red-black subtree in order.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="node">The current node; the shared empty node has no children.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="destination">Storage receiving the items in enumeration order.</param>
    /// <param name="index">The next destination index, advanced past each copied item.</param>
    private static void WalkRedBlack<T>(object? node, in NodeOffsets offsets, Span<T> destination, ref int index)
    {
        int left = offsets.Left, right = offsets.Right, key = offsets.Key;
        while (node is not null)
        {
            object? child = Field<object?>(node, left);
            if (child is not null) WalkRedBlack(child, in offsets, destination, ref index);
            destination[index++] = Field<T>(node, key);
            node = Field<object?>(node, right);
        }
    }

    /// <summary>Copies a red-black subtree in order without passing an address-taken index across recursion.</summary>
    /// <typeparam name="T">The element storage.</typeparam>
    /// <param name="node">The current node, or null.</param>
    /// <param name="offsets">The verified immutable node offsets.</param>
    /// <param name="destination">The tracked first element of the complete destination.</param>
    /// <param name="length">The complete destination length.</param>
    /// <param name="index">The next writable element index.</param>
    /// <returns>The index after this subtree.</returns>
    private static int WalkRedBlackValue<T>(object? node, in NodeOffsets offsets, ref T destination, int length, int index)
    {
        int left = offsets.Left, right = offsets.Right, key = offsets.Key;
        while (node is not null)
        {
            object? child = Field<object?>(node, left);
            if (child is not null) index = WalkRedBlackValue(child, in offsets, ref destination, length, index);
            MemoryMarshal.CreateSpan(ref destination, length)[index++] = Field<T>(node, key);
            node = Field<object?>(node, right);
        }
        return index;
    }

    /// <summary>Copies an AVL subtree in order; the shared empty node has no children.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="node">The current node; the shared empty node has no children.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="destination">Storage receiving the items in enumeration order.</param>
    /// <param name="index">The next destination index, advanced past each copied item.</param>
    private static void WalkAvl<T>(object node, in NodeOffsets offsets, Span<T> destination, ref int index)
    {
        int left = offsets.Left, right = offsets.Right, key = offsets.Key;
        object? child;
        while ((child = Field<object?>(node, left)) is not null)
        {
            if (Field<object?>(child, left) is not null) WalkAvl(child, in offsets, destination, ref index);
            destination[index++] = Field<T>(node, key);
            node = Field<object>(node, right);
        }
    }

    /// <summary>Copies an immutable sorted dictionary subtree in order.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="node">The current node; the shared empty node has no children.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="destination">Storage receiving the items in enumeration order.</param>
    /// <param name="index">The next destination index, advanced past each copied item.</param>
    private static void WalkPairs<TKey, TValue>(object node, in NodeOffsets offsets, Span<KeyValuePair<TKey, TValue>> destination, ref int index)
    {
        int left = offsets.Left, right = offsets.Right, key = offsets.Key, value = offsets.Value;
        object? child;
        while ((child = Field<object?>(node, left)) is not null)
        {
            if (Field<object?>(child, left) is not null) WalkPairs(child, in offsets, destination, ref index);
            destination[index++] = new KeyValuePair<TKey, TValue>(Field<TKey>(node, key), Field<TValue>(node, value));
            node = Field<object>(node, right);
        }
    }

    /// <summary>Copies a hash tree in order of hash code, each bucket's first value before its additional values.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="node">The current node; the shared empty node has no children.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="destination">The first destination element.</param>
    /// <param name="length">The complete checked destination length.</param>
    /// <param name="index">The next destination index.</param>
    /// <returns>The next destination index after copying this subtree.</returns>
    private static int WalkHash<T>(object node, in HashOffsets offsets, ref T destination, int length, int index)
    {
        int left = offsets.Tree.Left, right = offsets.Tree.Right;
        object? child;
        while ((child = Field<object?>(node, left)) is not null)
        {
            if (Field<object?>(child, left) is object grandchild)
            {
                // Empty nodes have null child links. A leaf contributes one bucket without another recursive frame.
                if (Field<object?>(grandchild, left) is null && Field<object?>(Field<object>(child, right), left) is null)
                    index = CopyHashBucket(child, in offsets, ref destination, length, index);
                else
                    index = WalkHash(child, in offsets, ref destination, length, index);
            }
            index = CopyHashBucket(node, in offsets, ref destination, length, index);
            node = Field<object>(node, right);
        }
        return index;
    }

    /// <summary>Copies one hash bucket using the same checked element store and collision subtree.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="node">The current nonempty hash node.</param>
    /// <param name="offsets">The node and collision layouts.</param>
    /// <param name="destination">The first destination element.</param>
    /// <param name="length">The complete checked destination length.</param>
    /// <param name="index">The next destination index.</param>
    /// <returns>The next destination index after copying the bucket.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CopyHashBucket<T>(object node, in HashOffsets offsets, ref T destination, int length, int index)
    {
        ref byte bucket = ref Field<byte>(node, offsets.Tree.Value);
        MemoryMarshal.CreateSpan(ref destination, length)[index++] = Field<T>(ref bucket, offsets.First);
        object list = Field<object>(ref bucket, offsets.Additional);
        if (Field<object?>(list, offsets.List.Left) is not null)
            index = WalkHashAdditional(list, in offsets.List, ref destination, length, index);
        return index;
    }

    /// <summary>Copies a collision subtree without exposing the main hash traversal's accumulator.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="node">The collision subtree.</param>
    /// <param name="offsets">The collision node layout.</param>
    /// <param name="destination">The first destination element.</param>
    /// <param name="length">The complete checked destination length.</param>
    /// <param name="index">The next destination index.</param>
    /// <returns>The next destination index after copying the subtree.</returns>
    /// <remarks>Keeps the address-taken collision accumulator outside the recursive hash traversal.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int WalkHashAdditional<T>(object node, in NodeOffsets offsets, ref T destination, int length, int index)
    {
        WalkAvl(node, in offsets, MemoryMarshal.CreateSpan(ref destination, length), ref index);
        return index;
    }

    /// <summary>Checks that keys ascend strictly under the default comparer, the order of a sorted dictionary's own writes.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>Whether the condition holds.</returns>
    private static bool IsStrictlyAscending<TKey, TValue>(KeyValuePair<TKey, TValue>[] pairs, int count)
    {
        if (pairs[0].Key is null) return false;
        var comparer = Comparer<TKey>.Default;
        for (int i = 1; i < count; i++)
            if (pairs[i].Key is null || comparer.Compare(pairs[i - 1].Key, pairs[i].Key) >= 0) return false;
        return true;
    }

    /// <summary>Links a balanced tree of frozen nodes over strictly ascending pairs.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>The root of the built tree.</returns>
    private static ImmutableSortedDictionary<TKey, TValue> BuildSortedDictionary<TKey, TValue>(KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
#if NET10_0_OR_GREATER
        if (BclTrees.ImmutableSortedDictionary<TKey, TValue>.Available)
            return BclTrees.ImmutableSortedDictionary<TKey, TValue>.Build(pairs, count);
#endif
        var template = ImmutableSortedDictionary<TKey, TValue>.Empty.Add(pairs[0].Key, pairs[0].Value);
        var offsets = ImmutableSortedDictionaryLayout<TKey, TValue>.Nodes;
        object node = Field<object>(template, ImmutableSortedDictionaryLayout<TKey, TValue>.Root);
        object root = LinkPairs(node, Field<object>(node, offsets.Left), in offsets, pairs, 0, count - 1);
        object result = Clone(template);
        Field<object>(result, ImmutableSortedDictionaryLayout<TKey, TValue>.Root) = root;
        Field<int>(result, ImmutableSortedDictionaryLayout<TKey, TValue>.Count) = count;
        return Unsafe.As<ImmutableSortedDictionary<TKey, TValue>>(result);
    }

    /// <summary>Links an AVL subtree of new frozen pair nodes.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="template">A node of the collection's own node type.</param>
    /// <param name="empty">The collection's shared empty node.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="start">The first index of the range.</param>
    /// <param name="end">The last index of the range.</param>
    /// <returns>The root of the linked subtree.</returns>
    private static object LinkPairs<TKey, TValue>(object template, object empty, in NodeOffsets offsets, KeyValuePair<TKey, TValue>[] pairs, int start, int end)
    {
        if (start > end) return empty;
        int middle = start + ((end - start) >> 1);
        object node = offsets.New();
        Field<TKey>(node, offsets.Key) = pairs[middle].Key;
        Field<TValue>(node, offsets.Value) = pairs[middle].Value;
        object left = LinkPairs(template, empty, in offsets, pairs, start, middle - 1);
        object right = LinkPairs(template, empty, in offsets, pairs, middle + 1, end);
        Seal(node, left, right, in offsets);
        return node;
    }

    /// <summary>Links new single-item hash buckets, ordered by hash code, into an AVL tree of frozen nodes.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="template">A node of the collection's own node type.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="items">The items, in the order they were read or stored.</param>
    /// <param name="keys">The ordered hash keys, each holding a hash code above an item index.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>The root of the built tree.</returns>
    private static object BuildHash<T>(object template, in HashOffsets offsets, T[] items, long[] keys, int count)
    {
        object empty = Field<object>(template, offsets.Tree.Left);
        object emptyList = Field<object>(ref Field<byte>(template, offsets.Tree.Value), offsets.Additional);
        return LinkHash(empty, emptyList, in offsets, items, keys, 0, count - 1);
    }

    /// <summary>Creates and links the bucket nodes of a range of ordered hash keys.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="empty">The collection's shared empty node.</param>
    /// <param name="emptyList">The shared empty list node of single-item buckets.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="items">The items, in the order they were read or stored.</param>
    /// <param name="keys">The ordered hash keys, each holding a hash code above an item index.</param>
    /// <param name="start">The first index of the range.</param>
    /// <param name="end">The last index of the range.</param>
    /// <returns>The root of the linked subtree.</returns>
    private static object LinkHash<T>(object empty, object emptyList, in HashOffsets offsets, T[] items, long[] keys, int start, int end)
    {
        if (start > end) return empty;
        int middle = start + ((end - start) >> 1);
        object node = offsets.Tree.New();
        Field<int>(node, offsets.Tree.Key) = (int)(keys[middle] >> 32);
        ref byte bucket = ref Field<byte>(node, offsets.Tree.Value);
        Field<T>(ref bucket, offsets.First) = items[(int)(uint)keys[middle]];
        Field<object>(ref bucket, offsets.Additional) = emptyList;
        object left = LinkHash(empty, emptyList, in offsets, items, keys, start, middle - 1);
        object right = LinkHash(empty, emptyList, in offsets, items, keys, middle + 1, end);
        Seal(node, left, right, in offsets.Tree);
        return node;
    }

    /// <summary>Publishes an AVL node's children, its height and its frozen state.</summary>
    /// <param name="node">The current node; the shared empty node has no children.</param>
    /// <param name="left">The left subtree.</param>
    /// <param name="right">The right subtree.</param>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    private static void Seal(object node, object left, object right, in NodeOffsets offsets)
    {
        Field<object>(node, offsets.Left) = left;
        Field<object>(node, offsets.Right) = right;
        Field<byte>(node, offsets.Height) = (byte)(1 + Math.Max(Field<byte>(left, offsets.Height), Field<byte>(right, offsets.Height)));
        Field<bool>(node, offsets.Frozen) = true;
        if (offsets.Counted) Field<int>(node, offsets.Count) = 1 + Field<int>(left, offsets.Count) + Field<int>(right, offsets.Count);
    }

    /// <summary>Replaces an empty sorted dictionary's tree with the one the sorted-array construction builds.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of items.</param>
    private static void LinkSortedDictionary<TKey, TValue>(SortedDictionary<TKey, TValue> dictionary, KeyValuePair<TKey, TValue>[] pairs, int count) where TKey : notnull
    {
        dictionary.Add(pairs[0].Key, pairs[0].Value);
        object set = Field<object>(dictionary, SortedDictionaryLayout<TKey, TValue>.Set);
        var offsets = SortedSetLayout<KeyValuePair<TKey, TValue>>.Nodes;
        ref object? root = ref Field<object?>(set, SortedSetLayout<KeyValuePair<TKey, TValue>>.Root);
        root = BuildRedBlack(in offsets, pairs, count);
        Field<int>(set, SortedSetLayout<KeyValuePair<TKey, TValue>>.Count) = count;
        Field<int>(set, SortedSetLayout<KeyValuePair<TKey, TValue>>.Version)++;
    }

    private const byte Black = 0;
    private const byte Red = 1;

    /// <summary>Builds the red-black tree of strictly ascending items, through the set's own sorted-array construction where
    /// it is bound.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="items">The items, in order.</param>
    /// <param name="count">The number of items, at least one.</param>
    /// <returns>The root of the built tree.</returns>
    private static object? BuildRedBlack<T>(in NodeOffsets offsets, T[] items, int count)
    {
#if NET10_0_OR_GREATER
        if (BclTrees.SortedSet<T>.Available)
            return BclTrees.SortedSet<T>.Build(items, count);
#endif
        return LinkRedBlack(in offsets, items, 0, count - 1, null);
    }

    /// <summary>Builds the red-black tree that <see cref="SortedSet{T}"/> constructs from a sorted array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="items">The items, in the order they were read or stored.</param>
    /// <param name="start">The first index of the range.</param>
    /// <param name="end">The last index of the range.</param>
    /// <param name="redNode">A red node attached as the leftmost child of the range, or null.</param>
    /// <returns>The root of the linked subtree.</returns>
    private static object? LinkRedBlack<T>(in NodeOffsets offsets, T[] items, int start, int end, object? redNode)
    {
        int size = end - start + 1;
        object root;
        switch (size)
        {
            case 0:
                return null;
            case 1:
                root = NewNode(in offsets, items[start], Black);
                if (redNode is not null) Field<object?>(root, offsets.Left) = redNode;
                break;
            case 2:
                root = NewNode(in offsets, items[start], Black);
                Field<object?>(root, offsets.Right) = NewNode(in offsets, items[end], Red);
                if (redNode is not null) Field<object?>(root, offsets.Left) = redNode;
                break;
            case 3:
                root = NewNode(in offsets, items[start + 1], Black);
                object left = NewNode(in offsets, items[start], Black);
                Field<object?>(root, offsets.Left) = left;
                Field<object?>(root, offsets.Right) = NewNode(in offsets, items[end], Black);
                if (redNode is not null) Field<object?>(left, offsets.Left) = redNode;
                break;
            default:
                int middle = (start + end) / 2;
                root = NewNode(in offsets, items[middle], Black);
                Field<object?>(root, offsets.Left) = LinkRedBlack(in offsets, items, start, middle - 1, redNode);
                Field<object?>(root, offsets.Right) = size % 2 == 0
                    ? LinkRedBlack(in offsets, items, middle + 2, end, NewNode(in offsets, items[middle + 1], Red))
                    : LinkRedBlack(in offsets, items, middle + 1, end, null);
                break;
        }
        return root;
    }

    /// <summary>Creates a red-black node of the collection's own node type.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="offsets">The field offsets of the collection's node type.</param>
    /// <param name="item">The node's item.</param>
    /// <param name="color">The red-black color.</param>
    /// <returns>The new node.</returns>
    private static object NewNode<T>(in NodeOffsets offsets, T item, byte color)
    {
        object node = offsets.New();
        Field<T>(node, offsets.Key) = item;
        Field<object?>(node, offsets.Left) = null;
        Field<object?>(node, offsets.Right) = null;
        Field<byte>(node, offsets.Color) = color;
        return node;
    }

    /// <summary>Orders items by their default hash codes, with the item index in the low half.</summary>
    /// <returns>Whether every hash code is distinct, so each bucket holds one item.</returns>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The items, in the order they were read or stored.</param>
    /// <param name="count">The number of items.</param>
    /// <param name="keys">The ordered hash keys, each holding a hash code above an item index.</param>
    private static bool TrySortHashes<T>(T[] items, int count, out long[] keys)
    {
        keys = System.Buffers.ArrayPool<long>.Shared.Rent(count);
        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            keys[i] = ((long)(item is null ? 0 : comparer.GetHashCode(item)) << 32) | (uint)i;
        }
        return SortDistinct(keys, count);
    }

    /// <summary>Orders pairs by their keys' default hash codes, with the pair index in the low half.</summary>
    /// <returns>Whether every key hash code is distinct, so each bucket holds one pair.</returns>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="pairs">The pairs, in the order they were read.</param>
    /// <param name="count">The number of items.</param>
    /// <param name="keys">The ordered hash keys, each holding a hash code above an item index.</param>
    private static bool TrySortHashes<TKey, TValue>(KeyValuePair<TKey, TValue>[] pairs, int count, out long[] keys)
    {
        keys = System.Buffers.ArrayPool<long>.Shared.Rent(count);
        var comparer = EqualityComparer<TKey>.Default;
        for (int i = 0; i < count; i++)
        {
            var key = pairs[i].Key;
            if (key is null)
            {
                System.Buffers.ArrayPool<long>.Shared.Return(keys);
                return false;
            }
            keys[i] = ((long)comparer.GetHashCode(key) << 32) | (uint)i;
        }
        return SortDistinct(keys, count);
    }

    /// <summary>Orders hash keys, skipping the sort for the hash order a LuminPack write produces.</summary>
    /// <returns>Whether every hash code is distinct; otherwise the pooled keys have been returned.</returns>
    /// <param name="keys">The ordered hash keys, each holding a hash code above an item index.</param>
    /// <param name="count">The number of items.</param>
    private static bool SortDistinct(long[] keys, int count)
    {
        int i = 1;
        while (i < count && (int)(keys[i - 1] >> 32) < (int)(keys[i] >> 32)) i++;
        if (i == count) return true;
        Array.Sort(keys, 0, count);
        for (i = 1; i < count; i++)
            if ((int)(keys[i - 1] >> 32) == (int)(keys[i] >> 32))
            {
                System.Buffers.ArrayPool<long>.Shared.Return(keys);
                return false;
            }
        return true;
    }

    /// <summary>Copies an object of any type, allocating the object's own type.</summary>
    /// <param name="source">The object to copy.</param>
    /// <returns>The copy.</returns>
    private static object Clone(object source)
    {
        object copy = Unsafe.As<Cloner>(source).Copy();
        if (copy.GetType() != source.GetType()) LuminPackExceptionHelper.ThrowMessage("A storage copy produced another type.");
        return copy;
    }

    /// <summary>Runs <see cref="object.MemberwiseClone"/>, which copies by the actual type of the object it is called on.</summary>
    /// <remarks>Unsealed and not inlined, so no exact type of the receiver is assumed.</remarks>
    private class Cloner
    {
        /// <summary>Copies the receiver by its actual type.</summary>
        /// <returns>The copy.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal object Copy() => MemberwiseClone();
    }

    /// <summary>Field offsets of a tree node.</summary>
    private readonly struct NodeOffsets
    {
        internal readonly int Key, Value, Left, Right, Height, Frozen, Color, Count;
        internal readonly bool Counted;
        internal readonly Type? Type;
        private readonly Func<object>? _factory;

        /// <summary>Locates the named fields of a node type.</summary>
        /// <param name="node">The node type, or null when its metadata is unavailable.</param>
        /// <param name="key">The item or key field.</param>
        /// <param name="value">The value field of a pair node, or null.</param>
        /// <param name="left">The left child field.</param>
        /// <param name="right">The right child field.</param>
        /// <param name="height">The AVL height field, or null.</param>
        /// <param name="frozen">The frozen flag field, or null.</param>
        /// <param name="color">The red-black color field, or null.</param>
        /// <param name="count">The subtree count field, or null.</param>
        /// <param name="factory">Allocates the node type directly, or null to use an uninitialized-object request.</param>
        internal NodeOffsets(Type? node, string key, string? value, string left, string right, string? height, string? frozen, string? color,
            string? count = null, Func<object>? factory = null)
        {
            Type = node;
            Counted = count is not null;
            Count = count is null ? 0 : ObjectField(node, count);
            Key = ObjectField(node, key);
            Value = value is null ? 0 : ObjectField(node, value);
            Left = ObjectField(node, left);
            Right = ObjectField(node, right);
            Height = height is null ? 0 : ObjectField(node, height);
            Frozen = frozen is null ? 0 : ObjectField(node, frozen);
            Color = color is null ? 0 : ObjectField(node, color);
            _factory = Verify(node, factory);
        }

        /// <summary>Keeps a direct allocation delegate only when it produces exactly the located node type.</summary>
        /// <param name="node">The located node type.</param>
        /// <param name="factory">The candidate delegate.</param>
        /// <returns>The delegate, or null when it is missing or fails.</returns>
        private static Func<object>? Verify(Type? node, Func<object>? factory)
        {
            if (node is null || factory is null) return null;
            try { return factory().GetType() == node ? factory : null; }
            catch { return null; }
        }

        internal bool Located => (Key | Value | Left | Right | Height | Frozen | Color | Count) >= 0 && Type is { IsAbstract: false, IsValueType: false };

        /// <summary>Allocates a node of the collection's own node type; every field the trees use is assigned afterwards.</summary>
        /// <returns>The new node.</returns>
        internal object New() => _factory is not null ? _factory() : RuntimeHelpers.GetUninitializedObject(Type!);
    }

#if NET10_0_OR_GREATER
    /// <summary>Calls the framework's own construction of sorted trees, whose nodes it allocates directly.</summary>
    /// <remarks>The members are bound by the runtime, including under ahead-of-time compilation. Each family checks its
    /// binding once with a one-element build; a framework version without the member keeps the field-linked build.</remarks>
    private static class BclTrees
    {
        private const string Immutable = "System.Collections.Immutable";

        /// <summary>Builds the red-black tree of a sorted array exactly as <see cref="SortedSet{T}"/>'s constructor does.</summary>
        /// <typeparam name="T">The element type.</typeparam>
        internal static class SortedSet<T>
        {
            private const string Node = "System.Collections.Generic.SortedSet`1+Node[[!0]], System.Collections";

            /// <summary>Runs the set's sorted-array construction.</summary>
            /// <param name="owner">Unused; the member is static.</param>
            /// <param name="items">The ordered items.</param>
            /// <param name="startIndex">The first index.</param>
            /// <param name="endIndex">The last index.</param>
            /// <param name="redNode">A red node to attach as the leftmost child, or null.</param>
            /// <returns>The root node.</returns>
            [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ConstructRootFromSortedArray")]
            [return: UnsafeAccessorType(Node)]
            private static extern object? Construct(System.Collections.Generic.SortedSet<T>? owner, T[] items, int startIndex, int endIndex,
                [UnsafeAccessorType(Node)] object? redNode);

            internal static readonly bool Available = Probe();

            /// <summary>Builds the tree of strictly ascending items.</summary>
            /// <param name="items">The items.</param>
            /// <param name="count">The number of items, at least one.</param>
            /// <returns>The root node.</returns>
            internal static object? Build(T[] items, int count) => Construct(null, items, 0, count - 1, null);

            /// <summary>Checks the binding with a one-item build.</summary>
            /// <returns>Whether the construction returns the set's own node type.</returns>
            private static bool Probe()
            {
                try { return Construct(null, new T[1], 0, 0, null)?.GetType() == FieldType(typeof(System.Collections.Generic.SortedSet<T>), "root"); }
                catch { return false; }
            }
        }

        /// <summary>Links frozen nodes through the node constructor, which computes each height and count itself.</summary>
        /// <typeparam name="T">The element type.</typeparam>
        internal static class ImmutableSortedSet<T>
        {
            private const string Node = "System.Collections.Immutable.ImmutableSortedSet`1+Node[[!0]], " + Immutable;

            /// <summary>Calls the node constructor.</summary>
            /// <param name="key">The item.</param>
            /// <param name="left">The left subtree.</param>
            /// <param name="right">The right subtree.</param>
            /// <param name="frozen">Whether the node is frozen.</param>
            /// <returns>The node.</returns>
            [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
            [return: UnsafeAccessorType(Node)]
            private static extern object NewNode(T key, [UnsafeAccessorType(Node)] object left, [UnsafeAccessorType(Node)] object right, bool frozen);

            /// <summary>Calls the set constructor over a root.</summary>
            /// <param name="root">The root node.</param>
            /// <param name="comparer">The comparer.</param>
            /// <returns>The set.</returns>
            [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
            private static extern System.Collections.Immutable.ImmutableSortedSet<T> Wrap([UnsafeAccessorType(Node)] object root, IComparer<T> comparer);

            private static readonly object? Empty = ImmutableSortedSetLayout<T>.Root >= 0
                ? Field<object>(System.Collections.Immutable.ImmutableSortedSet<T>.Empty, ImmutableSortedSetLayout<T>.Root) : null;

            internal static readonly bool Available = Probe();

            /// <summary>Builds a default-comparer set of strictly ascending items.</summary>
            /// <param name="items">The items.</param>
            /// <param name="count">The number of items, at least one.</param>
            /// <returns>The set.</returns>
            internal static System.Collections.Immutable.ImmutableSortedSet<T> Build(T[] items, int count) => Wrap(Link(items, 0, count - 1), Comparer<T>.Default);

            /// <summary>Links a balanced subtree.</summary>
            /// <param name="items">The items.</param>
            /// <param name="start">The first index.</param>
            /// <param name="end">The last index.</param>
            /// <returns>The subtree root.</returns>
            private static object Link(T[] items, int start, int end)
            {
                if (start > end) return Empty!;
                int middle = start + ((end - start) >> 1);
                return NewNode(items[middle], Link(items, start, middle - 1), Link(items, middle + 1, end), true);
            }

            /// <summary>Checks the binding with a one-item build.</summary>
            /// <returns>Whether the built set answers as one.</returns>
            private static bool Probe()
            {
                if (Empty is null) return false;
                try
                {
                    // Nodes reject a null key, so a reference family relies on the value-type check of the same members.
                    if (default(T) is null) return Wrap(Empty, Comparer<T>.Default).Count == 0 && ImmutableSortedSet<int>.Available;
                    return Build(new T[1], 1).Count == 1;
                }
                catch { return false; }
            }
        }

        /// <summary>Links frozen pair nodes through the node constructor, which computes each height itself.</summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        internal static class ImmutableSortedDictionary<TKey, TValue> where TKey : notnull
        {
            private const string Node = "System.Collections.Immutable.ImmutableSortedDictionary`2+Node[[!0],[!1]], " + Immutable;

            /// <summary>Calls the node constructor.</summary>
            /// <param name="key">The key.</param>
            /// <param name="value">The value.</param>
            /// <param name="left">The left subtree.</param>
            /// <param name="right">The right subtree.</param>
            /// <param name="frozen">Whether the node is frozen.</param>
            /// <returns>The node.</returns>
            [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
            [return: UnsafeAccessorType(Node)]
            private static extern object NewNode(TKey key, TValue value, [UnsafeAccessorType(Node)] object left, [UnsafeAccessorType(Node)] object right, bool frozen);

            /// <summary>Calls the dictionary constructor over a root.</summary>
            /// <param name="root">The root node.</param>
            /// <param name="count">The number of pairs.</param>
            /// <param name="keyComparer">The key comparer.</param>
            /// <param name="valueComparer">The value comparer.</param>
            /// <returns>The dictionary.</returns>
            [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
            private static extern System.Collections.Immutable.ImmutableSortedDictionary<TKey, TValue> Wrap([UnsafeAccessorType(Node)] object root, int count,
                IComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer);

            private static readonly object? Empty = ImmutableSortedDictionaryLayout<TKey, TValue>.Root >= 0
                ? Field<object>(System.Collections.Immutable.ImmutableSortedDictionary<TKey, TValue>.Empty, ImmutableSortedDictionaryLayout<TKey, TValue>.Root) : null;

            internal static readonly bool Available = Probe();

            /// <summary>Builds a default-comparer dictionary of pairs with strictly ascending keys.</summary>
            /// <param name="pairs">The pairs.</param>
            /// <param name="count">The number of pairs, at least one.</param>
            /// <returns>The dictionary.</returns>
            internal static System.Collections.Immutable.ImmutableSortedDictionary<TKey, TValue> Build(KeyValuePair<TKey, TValue>[] pairs, int count) =>
                Wrap(Link(pairs, 0, count - 1), count, Comparer<TKey>.Default, EqualityComparer<TValue>.Default);

            /// <summary>Links a balanced subtree.</summary>
            /// <param name="pairs">The pairs.</param>
            /// <param name="start">The first index.</param>
            /// <param name="end">The last index.</param>
            /// <returns>The subtree root.</returns>
            private static object Link(KeyValuePair<TKey, TValue>[] pairs, int start, int end)
            {
                if (start > end) return Empty!;
                int middle = start + ((end - start) >> 1);
                return NewNode(pairs[middle].Key, pairs[middle].Value, Link(pairs, start, middle - 1), Link(pairs, middle + 1, end), true);
            }

            /// <summary>Checks the binding with a one-pair build.</summary>
            /// <returns>Whether the built dictionary answers as one.</returns>
            private static bool Probe()
            {
                if (Empty is null) return false;
                try
                {
                    // Nodes reject a null key, so a reference family relies on the value-type check of the same members.
                    if (default(TKey) is null) return Wrap(Empty, 0, Comparer<TKey>.Default, EqualityComparer<TValue>.Default).Count == 0 && ImmutableSortedDictionary<int, int>.Available;
                    return Build(new KeyValuePair<TKey, TValue>[1], 1).Count == 1;
                }
                catch { return false; }
            }
        }
    }
#endif

#if NET10_0_OR_GREATER
    /// <summary>Allocates hash tree and list nodes through their own constructors, which costs about half as much as an
    /// uninitialized-object request.</summary>
    /// <remarks>The accessors are bound by the runtime, including under ahead-of-time compilation; a member that a
    /// framework version lacks fails the one-time check in <see cref="NodeOffsets"/>, which then keeps the
    /// uninitialized-object request.</remarks>
    private static class NodeAllocators
    {
        private const string Immutable = "System.Collections.Immutable";

        /// <summary>Allocates immutable list nodes, which hold the additional values of a hash bucket.</summary>
        /// <typeparam name="T">The element type.</typeparam>
        internal static class ImmutableListNode<T>
        {
            /// <summary>Calls the parameterless node constructor.</summary>
            /// <returns>The new node.</returns>
            [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
            [return: UnsafeAccessorType("System.Collections.Immutable.ImmutableList`1+Node[[!0]], " + Immutable)]
            internal static extern object New();
        }

        /// <summary>Allocates the hash tree nodes of an immutable hash set.</summary>
        /// <typeparam name="T">The element type.</typeparam>
        internal static class ImmutableHashSetNode<T>
        {
            /// <summary>Calls the parameterless node constructor.</summary>
            /// <returns>The new node.</returns>
            [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
            [return: UnsafeAccessorType("System.Collections.Immutable.SortedInt32KeyNode`1[[System.Collections.Immutable.ImmutableHashSet`1+HashBucket[[!0]], " + Immutable + "]], " + Immutable)]
            internal static extern object New();
        }
    }
#endif

    /// <summary>Field offsets of a hash tree, its buckets and their additional-value lists.</summary>
    private readonly struct HashOffsets
    {
        internal readonly NodeOffsets Tree, List;
        internal readonly int First, Additional;

        /// <summary>Locates the hash tree, bucket and list fields of a hash collection.</summary>
        /// <param name="collection">The closed collection type.</param>
        /// <param name="tree">Allocates hash tree nodes directly, or null.</param>
        /// <param name="list">Allocates additional-value list nodes directly, or null.</param>
        internal HashOffsets(Type? collection, Func<object>? tree, Func<object>? list)
        {
            var node = FieldType(collection, "_root");
            var bucket = FieldType(node, "_value");
            var listNode = FieldType(bucket, "_additionalElements");
            Tree = new NodeOffsets(node, "_key", "_value", "_left", "_right", "_height", "_frozen", null, factory: tree);
            List = AvlList(listNode, list);
            First = ValueField(bucket, "_firstValue");
            Additional = ValueField(bucket, "_additionalElements");
        }

        internal bool Located => Tree.Located && List.Located && (First | Additional) >= 0;
    }

    /// <summary>Gets the offsets of an immutable AVL node with a subtree count.</summary>
    /// <param name="node">The current node; the shared empty node has no children.</param>
    /// <returns>The result.</returns>
    /// <param name="factory">Allocates the node type directly, or null.</param>
    private static NodeOffsets AvlList(Type? node, Func<object>? factory) => new(node, "_key", null, "_left", "_right", "_height", "_frozen", null, "_count", factory);

    /// <summary>Runs a family check, treating any failure as an unrecognized layout.</summary>
    /// <param name="check">The check to run.</param>
    /// <returns>Whether the storage behaves as the public API does.</returns>
    private static bool Check(Func<bool> check)
    {
        try { return check(); }
        catch { return false; }
    }

    private static class SortedSetLayout<T>
    {
        internal static readonly int Root = ObjectField(typeof(SortedSet<T>), "root");
        internal static readonly int Count = ObjectField(typeof(SortedSet<T>), "count");
        internal static readonly int Version = ObjectField(typeof(SortedSet<T>), "version");
        internal static readonly NodeOffsets Nodes = new(FieldType(typeof(SortedSet<T>), "root"),
            "<Item>k__BackingField", null, "<Left>k__BackingField", "<Right>k__BackingField", null, null, "<Color>k__BackingField");
        internal static readonly bool Available = (Root | Count | Version) >= 0 && Nodes.Located && Check(Probe);

        /// <summary>Checks this family's storage through its public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Probe()
        {
            var set = new SortedSet<int> { 5, 1, 4, 2, 3 };
            if (SortedSetLayout<int>.Root < 0 || !SortedSetLayout<int>.Nodes.Located || Field<int>(set, SortedSetLayout<int>.Count) != 5) return false;
            var copy = new int[5];
            int index = 0;
            WalkRedBlack<int>(Field<object?>(set, SortedSetLayout<int>.Root), in SortedSetLayout<int>.Nodes, copy, ref index);
            return index == 5 && copy.AsSpan().SequenceEqual(new[] { 1, 2, 3, 4, 5 });
        }
    }

    private static class SortedDictionaryLayout<TKey, TValue> where TKey : notnull
    {
        internal static readonly int Set = ObjectField(typeof(SortedDictionary<TKey, TValue>), "_set");
        internal static readonly bool Available = Set >= 0 && SortedSetLayout<KeyValuePair<TKey, TValue>>.Available &&
            FieldType(typeof(SortedDictionary<TKey, TValue>), "_set")?.BaseType == typeof(SortedSet<KeyValuePair<TKey, TValue>>) && Check(Probe);

        /// <summary>Checks this family's storage through its public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Probe()
        {
            var filled = new SortedDictionary<int, int>();
            var pairs = new KeyValuePair<int, int>[9];
            for (int i = 0; i < pairs.Length; i++) pairs[i] = new(i * 2, i);
            if (SortedDictionaryLayout<int, int>.Set < 0 || !SortedSetLayout<KeyValuePair<int, int>>.Available) return false;
            LinkSortedDictionary(filled, pairs, pairs.Length);
            var copy = new KeyValuePair<int, int>[9];
            int index = 0;
            WalkRedBlack<KeyValuePair<int, int>>(Field<object?>(Field<object>(filled, SortedDictionaryLayout<int, int>.Set), SortedSetLayout<KeyValuePair<int, int>>.Root),
                in SortedSetLayout<KeyValuePair<int, int>>.Nodes, copy, ref index);
            return index == 9 && copy.SequenceEqual(pairs) && filled.Count == 9 && filled.SequenceEqual(pairs) && filled[16] == 8 &&
                filled.Remove(8) && !filled.ContainsKey(8) && filled.TryAdd(9, 1) && filled.TryAdd(-1, 0) && filled.Keys.First() == -1 && filled.Count == 10;
        }
    }

    private static class ImmutableSortedSetLayout<T>
    {
        internal static readonly int Root = ObjectField(typeof(ImmutableSortedSet<T>), "_root");
        internal static readonly NodeOffsets Nodes = AvlList(FieldType(typeof(ImmutableSortedSet<T>), "_root"), null);
        internal static readonly bool Available = Root >= 0 && Nodes.Located && Check(Probe);

        /// <summary>Checks this family's storage through its public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Probe()
        {
            if (ImmutableSortedSetLayout<int>.Root < 0 || !ImmutableSortedSetLayout<int>.Nodes.Located) return false;
            var set = ImmutableSortedSet.Create(5, 1, 4, 2, 3);
            var copy = new int[5];
            int index = 0;
            WalkAvl<int>(Field<object>(set, ImmutableSortedSetLayout<int>.Root), in ImmutableSortedSetLayout<int>.Nodes, copy, ref index);
            if (index != 5 || !copy.AsSpan().SequenceEqual(new[] { 1, 2, 3, 4, 5 })) return false;
            var items = Enumerable.Range(-9, 19).ToArray();
            var template = ImmutableSortedSet.Create(items[0]);
            var offsets = ImmutableSortedSetLayout<int>.Nodes;
            object node = Field<object>(template, ImmutableSortedSetLayout<int>.Root);
            var built = Unsafe.As<ImmutableSortedSet<int>>(Clone(template));
            Field<object>(built, ImmutableSortedSetLayout<int>.Root) = LinkItems(Field<object>(node, offsets.Left), in offsets, items, 0, items.Length - 1);
            var modified = built.Remove(0).Add(100).Add(-100);
            return built.Count == 19 && built.SequenceEqual(items) && built[7] == -2 && built.IndexOf(9) == 18 && built.Min == -9 && built.Max == 9 &&
                modified.Count == 20 && modified[0] == -100 && !modified.Contains(0) && built.Contains(0) && built.Reverse().First() == 9;
        }
    }

    private static class ImmutableSortedDictionaryLayout<TKey, TValue> where TKey : notnull
    {
        internal static readonly int Root = ObjectField(typeof(ImmutableSortedDictionary<TKey, TValue>), "_root");
        internal static readonly int Count = ObjectField(typeof(ImmutableSortedDictionary<TKey, TValue>), "_count");
        internal static readonly NodeOffsets Nodes = new(FieldType(typeof(ImmutableSortedDictionary<TKey, TValue>), "_root"),
            "_key", "_value", "_left", "_right", "_height", "_frozen", null);
        internal static readonly bool Available = (Root | Count) >= 0 && Nodes.Located && Check(Probe);

        /// <summary>Checks this family's storage through its public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Probe()
        {
            if ((ImmutableSortedDictionaryLayout<int, int>.Root | ImmutableSortedDictionaryLayout<int, int>.Count) < 0 ||
                !ImmutableSortedDictionaryLayout<int, int>.Nodes.Located)
                return false;
            var pairs = new KeyValuePair<int, int>[10];
            for (int i = 0; i < pairs.Length; i++) pairs[i] = new(i, i * 3);
            var built = BuildSortedDictionary(pairs, pairs.Length);
            var copy = new KeyValuePair<int, int>[10];
            int index = 0;
            WalkPairs<int, int>(Field<object>(built, ImmutableSortedDictionaryLayout<int, int>.Root), in ImmutableSortedDictionaryLayout<int, int>.Nodes, copy, ref index);
            var modified = built.Remove(4).Add(4, 1).Add(10, 30).Add(-1, 0);
            return index == 10 && copy.SequenceEqual(pairs) && built.Count == 10 && built.SequenceEqual(pairs) && built[7] == 21 &&
                modified.Count == 12 && modified[4] == 1 && built[4] == 12 && modified.Keys.SequenceEqual(Enumerable.Range(-1, 12));
        }
    }

    private static class ImmutableHashSetLayout<T>
    {
        internal static readonly int Root = ObjectField(typeof(ImmutableHashSet<T>), "_root");
        internal static readonly int Count = ObjectField(typeof(ImmutableHashSet<T>), "_count");
#if NET10_0_OR_GREATER
        internal static readonly HashOffsets Hash = new(typeof(ImmutableHashSet<T>), NodeAllocators.ImmutableHashSetNode<T>.New, NodeAllocators.ImmutableListNode<T>.New);
#else
        internal static readonly HashOffsets Hash = new(typeof(ImmutableHashSet<T>), null, null);
#endif
        internal static readonly bool Available = (Root | Count) >= 0 && Hash.Located && Check(Probe);

        /// <summary>Checks this family's storage through its public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Probe()
        {
            if ((ImmutableHashSetLayout<int>.Root | ImmutableHashSetLayout<int>.Count) < 0 || !ImmutableHashSetLayout<int>.Hash.Located) return false;
            var source = ImmutableHashSet.CreateRange(new[] { 3, -7, 11, 0, 42, int.MinValue, 5 });
            var copy = new int[source.Count];
            int index = 0;
            index = WalkHash(Field<object>(source, ImmutableHashSetLayout<int>.Root), in ImmutableHashSetLayout<int>.Hash,
                ref copy[0], copy.Length, index);
            if (index != source.Count || !copy.SequenceEqual(source)) return false;
            var items = Enumerable.Range(-20, 41).ToArray();
            if (!TrySortHashes(items, items.Length, out var keys)) return false;
            var template = ImmutableHashSet.Create(items[0]);
            object root = BuildHash<int>(Field<object>(template, ImmutableHashSetLayout<int>.Root), in ImmutableHashSetLayout<int>.Hash, items, keys, items.Length);
            var built = Unsafe.As<ImmutableHashSet<int>>(Clone(template));
            Field<object>(built, ImmutableHashSetLayout<int>.Root) = root;
            Field<int>(built, ImmutableHashSetLayout<int>.Count) = items.Length;
            var modified = built.Remove(0).Add(100).Add(-20);
            return built.Count == 41 && built.SetEquals(items) && built.SequenceEqual(ImmutableHashSet.CreateRange(items)) && built.Contains(-20) &&
                !built.Contains(21) && modified.Count == 41 && modified.Contains(100) && !modified.Contains(0) && built.Contains(0);
        }
    }

    private static class ImmutableDictionaryLayout<TKey, TValue> where TKey : notnull
    {
        internal static readonly int Root = ObjectField(typeof(ImmutableDictionary<TKey, TValue>), "_root");
        internal static readonly int Count = ObjectField(typeof(ImmutableDictionary<TKey, TValue>), "_count");
#if NET10_0_OR_GREATER
        // The runtime cannot bind a type-name accessor for this two-parameter bucket node, so its tree nodes keep the
        // uninitialized-object request.
        internal static readonly HashOffsets Hash = new(typeof(ImmutableDictionary<TKey, TValue>), null,
            NodeAllocators.ImmutableListNode<KeyValuePair<TKey, TValue>>.New);
#else
        internal static readonly HashOffsets Hash = new(typeof(ImmutableDictionary<TKey, TValue>), null, null);
#endif
        internal static readonly bool Available = (Root | Count) >= 0 && Hash.Located && Check(Probe);

        /// <summary>Checks this family's storage through its public API.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Probe()
        {
            if ((ImmutableDictionaryLayout<int, int>.Root | ImmutableDictionaryLayout<int, int>.Count) < 0 || !ImmutableDictionaryLayout<int, int>.Hash.Located)
                return false;
            var source = ImmutableDictionary.CreateRange(new[] { new KeyValuePair<int, int>(9, 1), new(-3, 2), new(77, 3), new(0, 4) });
            var copy = new KeyValuePair<int, int>[source.Count];
            int index = 0;
            index = WalkHash(Field<object>(source, ImmutableDictionaryLayout<int, int>.Root), in ImmutableDictionaryLayout<int, int>.Hash,
                ref copy[0], copy.Length, index);
            if (index != source.Count || !copy.SequenceEqual(source)) return false;
            var pairs = Enumerable.Range(-15, 31).Select(i => new KeyValuePair<int, int>(i, i * 5)).ToArray();
            if (!TrySortHashes(pairs, pairs.Length, out var keys)) return false;
            var template = ImmutableDictionary<int, int>.Empty.Add(pairs[0].Key, pairs[0].Value);
            object root = BuildHash<KeyValuePair<int, int>>(Field<object>(template, ImmutableDictionaryLayout<int, int>.Root), in ImmutableDictionaryLayout<int, int>.Hash, pairs, keys, pairs.Length);
            var built = Unsafe.As<ImmutableDictionary<int, int>>(Clone(template));
            Field<object>(built, ImmutableDictionaryLayout<int, int>.Root) = root;
            Field<int>(built, ImmutableDictionaryLayout<int, int>.Count) = pairs.Length;
            var modified = built.Remove(3).SetItem(4, -1).Add(99, 0);
            return built.Count == 31 && built[-15] == -75 && built[15] == 75 && !built.ContainsKey(16) &&
                built.SequenceEqual(ImmutableDictionary.CreateRange(pairs)) && modified.Count == 31 && modified[4] == -1 && built[4] == 20 && !modified.ContainsKey(3);
        }
    }


}
