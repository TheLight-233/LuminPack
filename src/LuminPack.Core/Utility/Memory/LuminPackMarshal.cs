using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Attribute;

namespace LuminPack.Code;

/// <summary>Provides lumin pack marshal functionality.</summary>
public static partial class LuminPackMarshal
{

    // List, Stack and Queue storage is reached through the offsets probed by LuminCollectionLayouts; see that type for the
    // probe and its fallback. The offsets are constants in optimized code, as the former compiled views' fields were.

    /// <summary>Gets the array storage of memory whose owner is exactly an array of <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="memory">The memory, including default.</param>
    /// <param name="array">Receives the owning array, or null.</param>
    /// <param name="start">Receives the memory's first index in the array.</param>
    /// <param name="length">Receives the memory's length.</param>
    /// <returns>Whether the memory is a slice of an exact <typeparamref name="T"/> array; managers, strings and covariant
    /// arrays return false.</returns>
    /// <remarks>Reads the memory's owner, index and length through a layout checked once against the public constructors,
    /// instead of the manager and array queries; an unrecognized layout uses those queries.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetExactArray<T>(Memory<T> memory, out T[]? array, out int start, out int length)
    {
        if (MemoryStorage.Available)
        {
            ref var view = ref Unsafe.As<Memory<T>, MemoryView>(ref memory);
            object? owner = view.Owner;
            if (owner is not null && owner.GetType() == typeof(T[]))
            {
                array = Unsafe.As<T[]>(owner);
                // The high bit marks a pre-pinned array.
                start = view.Index & int.MaxValue;
                length = view.Length;
                return true;
            }

            array = null;
            start = length = 0;
            return false;
        }

        if (!MemoryMarshal.TryGetMemoryManager<T, System.Buffers.MemoryManager<T>>(memory, out _) &&
            MemoryMarshal.TryGetArray((ReadOnlyMemory<T>)memory, out var segment) && segment.Array is not null && segment.Array.GetType() == typeof(T[]))
        {
            array = segment.Array;
            start = segment.Offset;
            length = segment.Count;
            return true;
        }

        array = null;
        start = length = 0;
        return false;
    }

    /// <summary>Mirrors the owner, index and length fields of <see cref="Memory{T}"/>.</summary>
    private struct MemoryView
    {
        internal object? Owner;
        internal int Index;
        internal int Length;
    }

    /// <summary>Holds the one-time check of the memory field layout.</summary>
    private static class MemoryStorage
    {
        internal static readonly bool Available = Check();

        /// <summary>Checks the mirrored fields against plain and pre-pinned array memory.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool Check()
        {
            try
            {
                if (Unsafe.SizeOf<Memory<int>>() != Unsafe.SizeOf<MemoryView>()) return false;
                var items = new int[8];
                var plain = new Memory<int>(items, 3, 4);
                var view = Unsafe.As<Memory<int>, MemoryView>(ref plain);
                if (!ReferenceEquals(view.Owner, items) || (view.Index & int.MaxValue) != 3 || view.Length != 4) return false;
#if NET5_0_OR_GREATER
                var pinned = MemoryMarshal.CreateFromPinnedArray(GC.AllocateArray<int>(8, pinned: true), 2, 5);
                var pinnedView = Unsafe.As<Memory<int>, MemoryView>(ref pinned);
                if ((pinnedView.Index & int.MaxValue) != 2 || pinnedView.Length != 5) return false;
#endif
                var empty = default(Memory<int>);
                return Unsafe.As<Memory<int>, MemoryView>(ref empty).Owner is null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Obtains the list span for the supplied input.</summary>
    /// <param name="list">The list used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetListSpan<T>(List<T?>? list)
    {
        if (list is null)
        {
            return Span<T?>.Empty;
        }

#if NET8_0_OR_GREATER
        return CollectionsMarshal.AsSpan(list);
#else
        return ListItems(list).AsSpan(0, ListSize(list));
#endif
    }

    /// <summary>Obtains the list span for the supplied input.</summary>
    /// <param name="list">The list used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetListSpan<T>(List<T?>? list, int length)
    {
        if (list is null)
        {
            return Span<T?>.Empty;
        }

#if NET8_0_OR_GREATER
        // An empty list grows to exactly the count once it is at least the default capacity, so SetCount alone
        // allocates; other growth sets the exact capacity first rather than doubling.
        int capacity = list.Capacity;
        if (capacity < length && (capacity != 0 || length < 4))
            list.Capacity = length;
        CollectionsMarshal.SetCount(list, length);
        return CollectionsMarshal.AsSpan(list).Slice(0, length);
#else
        if (list.Capacity < length)
            list.Capacity = length;
        SetListSize(list, length);
        return ListItems(list).AsSpan(0, length);
#endif
    }

    /// <summary>
    /// Sets the logical size and exposes the backing storage of a newly constructed list.
    /// The caller must own a fresh list whose capacity is at least <paramref name="length"/>.
    /// </summary>
    /// <param name="list">The list used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T[] DangerousGetFreshListArray<T>(List<T> list, int length)
    {
#if NET8_0_OR_GREATER
        CollectionsMarshal.SetCount(list, length);
        return ListItems(list)!;
#else
        ListSize(list) = length;
        return ListItems(list)!;
#endif
    }

    /// <summary>Updates the list size used by the current operation.</summary>
    /// <param name="list">The list used by this operation.</param>
    /// <param name="size">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetListSize<T>(List<T?>? list, int size)
    {
        if (list is null) return;

#if NET8_0_OR_GREATER
        if (list.Count == size)
        {
            // SetCount still invalidates enumerators when the size is unchanged.
            // A valid List already has sufficient capacity for its current count.
            LuminCollectionLayouts.Require(LuminCollectionLayouts.ListVersion, "List<T>");
            LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.ListVersion)++;
            return;
        }
#endif

#if NET8_0_OR_GREATER
        // As in GetListSpan: an empty list's own growth is exact from the default capacity on.
        int capacity = list.Capacity;
        if (capacity < size && (capacity != 0 || size < 4))
            list.Capacity = size;
        CollectionsMarshal.SetCount(list, size);
        return;
#else
        if (list.Capacity < size)
            list.Capacity = size;
        ref int current = ref ListSize(list);
        if (current == size)
            return;

        if (size < current && RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
            Array.Clear(ListItems(list)!, size, current - size);
        current = size;
        LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.ListVersion)++;
#endif
    }

    /// <summary>Gets the backing array of a list.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref T[]? ListItems<T>(List<T> list)
    {
        LuminCollectionLayouts.Require(LuminCollectionLayouts.ListItems, "List<T>");
        return ref LuminFieldLayout.Field<T[]?>(list, LuminCollectionLayouts.ListItems);
    }

    /// <summary>Gets the size of a list.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref int ListSize<T>(List<T> list)
    {
        LuminCollectionLayouts.Require(LuminCollectionLayouts.ListSize, "List<T>");
        return ref LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.ListSize);
    }

    /// <summary>Obtains the stack span for the supplied input.</summary>
    /// <param name="stack">The stack used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetStackSpan<T>(Stack<T?>? stack)
    {
        if (stack is null)
        {
            return Span<T?>.Empty;
        }

        SetStackSize(stack, stack.Count);
        return StackArray(stack).AsSpan(0, stack.Count);
    }

    /// <summary>Obtains the stack span for the supplied input.</summary>
    /// <param name="stack">The stack used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetStackSpan<T>(Stack<T?>? stack, int length)
    {
        if (stack is null)
        {
            return Span<T?>.Empty;
        }

        SetStackSize(stack, length);
        return StackArray(stack).AsSpan(0, length);
    }

    /// <summary>Updates the stack size used by the current operation.</summary>
    /// <param name="stack">The stack used by this operation.</param>
    /// <param name="size">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetStackSize<T>(Stack<T?>? stack, int size)
    {
        if (stack is null) return;

        ref T?[] items = ref StackArray(stack);
        if (items.Length < size)
            items = new T?[size];
        LuminCollectionLayouts.Require(LuminCollectionLayouts.StackSize, "Stack<T>");
        LuminFieldLayout.Field<int>(stack, LuminCollectionLayouts.StackSize) = size;
    }

    /// <summary>Gets the backing array of a stack.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stack">The stack.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref T[] StackArray<T>(Stack<T> stack)
    {
        LuminCollectionLayouts.Require(LuminCollectionLayouts.StackArray, "Stack<T>");
        return ref LuminFieldLayout.Field<T[]>(stack, LuminCollectionLayouts.StackArray);
    }

    /// <summary>Obtains the queue span for the supplied input.</summary>
    /// <param name="queue">The queue used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetQueueSpan<T>(Queue<T?>? queue)
    {
        if (queue is null)
        {
            return Span<T?>.Empty;
        }

        return QueueArray(queue).AsSpan();
    }

    /// <summary>Obtains the queue span for the supplied input.</summary>
    /// <param name="queue">The queue used by this operation.</param>
    /// <param name="size">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetQueueSpan<T>(Queue<T?>? queue, int size)
    {
        if (queue is null)
        {
            return Span<T?>.Empty;
        }

        ref T?[] items = ref QueueArray(queue);
        if (items.Length < size)
            items = new T?[size];
        return items.AsSpan(0, size);
    }

    /// <summary>Obtains the queue size for the supplied input.</summary>
    /// <param name="queue">The queue used by this operation.</param>
    /// <param name="tail">Receives the tail produced by the operation.</param>
    /// <param name="head">Receives the head produced by the operation.</param>
    /// <param name="size">Receives the size produced by the operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetQueueSize<T>(Queue<T?>? queue, out int tail, out int head, out int size)
    {
        if (queue is null)
        {
            tail = 0;
            head = 0;
            size = 0;

            return;
        }

        LuminCollectionLayouts.Require(LuminCollectionLayouts.QueueSize, "Queue<T>");
        head = LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueHead);
        tail = LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueTail);
        size = LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueSize);

    }

    /// <summary>Updates the queue size used by the current operation.</summary>
    /// <param name="queue">The queue used by this operation.</param>
    /// <param name="tail">The tail used by this operation.</param>
    /// <param name="head">The head used by this operation.</param>
    /// <param name="size">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetQueueSize<T>(Queue<T?>? queue, int tail, int head, int size)
    {
        if (queue is null) return;

        ref T?[] items = ref QueueArray(queue);

        if (items.Length < size)
        {
            items = new T[size];
        }


        LuminCollectionLayouts.Require(LuminCollectionLayouts.QueueSize, "Queue<T>");
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueHead) = head;
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueTail) = tail;
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueSize) = size;

    }

    /// <summary>Gets the backing array of a queue.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="queue">The queue.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref T[] QueueArray<T>(Queue<T> queue)
    {
        LuminCollectionLayouts.Require(LuminCollectionLayouts.QueueArray, "Queue<T>");
        return ref LuminFieldLayout.Field<T[]>(queue, LuminCollectionLayouts.QueueArray);
    }

    /// <summary>Obtains the dictionary view for the supplied input.</summary>
    /// <param name="dict">The dict used by this operation.</param>
    /// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
    /// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="PlatformNotSupportedException">The runtime's dictionary storage is not recognized.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DictionaryView<TKey, TValue?> GetDictionaryView<TKey, TValue>(Dictionary<TKey, TValue?>? dict) where TKey : notnull
    {
        if (!DictionaryView<TKey, TValue?>.Recognized) LuminCollectionLayouts.Require(LuminFieldLayout.Unavailable, "Dictionary<TKey, TValue>");
        return new DictionaryView<TKey, TValue?>(dict!);
    }

    private static readonly bool UsesLegacyHashTableFreeEntryEncoding =
        string.Equals(typeof(object).Assembly.GetName().Name, "mscorlib", StringComparison.Ordinal);

    /// <summary>
    /// Returns whether a Dictionary/HashSet entry represents a live item on the
    /// current runtime. Unity/Mono and .NET Framework mark free entries with a
    /// negative hash code; modern CoreCLR encodes the free list in <paramref name="next"/>.
    /// </summary>
    /// <param name="hashCode">The hash code used by this operation.</param>
    /// <param name="next">The next used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHashTableEntryActive(uint hashCode, int next)
    {
        return UsesLegacyHashTableFreeEntryEncoding
            ? hashCode != uint.MaxValue
            : next >= -1;
    }

    /// <summary>Obtains the hash set view for the supplied input.</summary>
    /// <param name="set">The set used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="PlatformNotSupportedException">The runtime's hash set storage is not recognized.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSetView<T?> GetHashSetView<T>(HashSet<T?>? set)
    {
        if (!HashSetView<T?>.Recognized) LuminCollectionLayouts.Require(LuminFieldLayout.Unavailable, "HashSet<T>");
        return new HashSetView<T?>(set!);
    }

    /// <summary>Writes a hash set of raw elements as the count followed by its live entries in entry order, the enumeration order.</summary>
    /// <param name="writer">The writer receiving the record.</param>
    /// <param name="set">The non-null set.</param>
    /// <typeparam name="T">The raw element type, written as its storage.</typeparam>
    /// <remarks>A set without removed entries holds its live elements in the first <c>Count</c> entries, which are copied without
    /// testing each entry. Each element uses the architecture-aware scalar core, so 32-bit targets keep block copies for values
    /// wider than four bytes. The writes stay inside the reserved record even if the set changes concurrently.</remarks>
    public static void WriteHashSetRaw<T>(ref global::LuminPack.Core.LuminPackWriter writer, HashSet<T> set) where T : unmanaged
    {
        int count = set.Count;
        int bytes = checked(count * Unsafe.SizeOf<T>());
        writer.EnsureAdditionalCapacity(checked(sizeof(int) + bytes));
        ref byte cursor = ref writer.CurrentReference;
        global::LuminPack.Core.LuminPackWriter.Write(ref cursor, in count);
        if (count != 0)
        {
            ref byte destination = ref Unsafe.Add(ref cursor, sizeof(int));
            var view = GetHashSetView(set);
            ref var entry = ref GetArrayReference(view._entries);
            int used = view._count;
            if (used == count)
            {
                for (int i = 0; i < count; i++)
                    global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)i * Unsafe.SizeOf<T>()), in Unsafe.Add(ref entry, (nint)i).Value);
            }
            else
            {
                int written = 0;
                for (int i = 0; i < used && written < count; i++)
                {
                    ref var current = ref Unsafe.Add(ref entry, (nint)i);
                    if (IsHashTableEntryActive(current.HashCode, current.Next))
                        global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)written++ * Unsafe.SizeOf<T>()), in current.Value);
                }
            }
        }
        writer.AdvanceSafe(sizeof(int) + bytes);
    }

    /// <summary>Copies the raw key and value storage of a dense dictionary's first <paramref name="count"/> entries into the reserved record.</summary>
    /// <param name="writer">The writer whose capacity for the pairs is already reserved.</param>
    /// <param name="dictionary">A dictionary without removed entries, so its first entries are its live pairs in enumeration order.</param>
    /// <param name="count">The number of pairs.</param>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    /// <remarks>On 64-bit targets, a key and value stored next to each other in eight bytes move as one load and store, addressed by
    /// index with one advance for the record. Other pairs and 32-bit targets use the architecture-aware scalar core per member.</remarks>
    public static void WriteDenseRawPairs<TKey, TValue>(ref global::LuminPack.Core.LuminPackWriter writer, Dictionary<TKey, TValue> dictionary, int count)
        where TKey : unmanaged where TValue : unmanaged
    {
        if (count == 0) return;
        CopyDenseRawPairs(ref writer.CurrentReference, dictionary, count);
        writer.Advance(count * (Unsafe.SizeOf<TKey>() + Unsafe.SizeOf<TValue>()));
    }

    /// <summary>Copies the raw key and value storage of a dense dictionary's first <paramref name="count"/> entries.</summary>
    /// <param name="destination">Writable room for <paramref name="count"/> packed pairs.</param>
    /// <param name="dictionary">A dictionary without removed entries, so its first entries are its live pairs in enumeration order.</param>
    /// <param name="count">The positive number of pairs.</param>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    internal static void CopyDenseRawPairs<TKey, TValue>(ref byte destination, Dictionary<TKey, TValue> dictionary, int count)
        where TKey : unmanaged where TValue : unmanaged
    {
        var view = GetDictionaryView(dictionary);
        ref var entry = ref GetArrayReference(view._entries);
        int pair = Unsafe.SizeOf<TKey>() + Unsafe.SizeOf<TValue>();
        if (Unsafe.SizeOf<IntPtr>() == 8 && pair == 8 && AdjacentPair<TKey, TValue>.Value)
        {
            int i = 0;
#if NET8_0_OR_GREATER
            // A sixteen-byte entry holding its pair in its upper half: two entries load as two vectors and one unpack of their
            // upper 64-bit lanes stores both pairs. Each load covers exactly one entry of the counted range.
            if (Unsafe.SizeOf<DictionaryView<TKey, TValue>.Entry>() == 16 && AdjacentPair<TKey, TValue>.UpperHalf &&
                (System.Runtime.Intrinsics.X86.Sse2.IsSupported || System.Runtime.Intrinsics.Arm.AdvSimd.Arm64.IsSupported))
            {
                ref byte entries = ref Unsafe.As<DictionaryView<TKey, TValue>.Entry, byte>(ref entry);
                for (; i + 2 <= count; i += 2)
                {
                    var first = System.Runtime.Intrinsics.Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref entries, (nint)i * 16)));
                    var second = System.Runtime.Intrinsics.Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref entries, (nint)i * 16 + 16)));
                    var pairs = System.Runtime.Intrinsics.X86.Sse2.IsSupported
                        ? System.Runtime.Intrinsics.X86.Sse2.UnpackHigh(first, second)
                        : System.Runtime.Intrinsics.Arm.AdvSimd.Arm64.ZipHigh(first, second);
                    System.Runtime.Intrinsics.Vector128.StoreUnsafe(pairs, ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref destination, (nint)i * 8)));
                }
            }
#endif
            for (; i < count; i++)
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nint)i * 8),
                    Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<TKey, byte>(ref Unsafe.Add(ref entry, (nint)i).Key)));
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                ref var current = ref Unsafe.Add(ref entry, (nint)i);
                ref byte target = ref Unsafe.Add(ref destination, (nint)i * pair);
                global::LuminPack.Core.LuminPackWriter.Write(ref target, in current.Key);
                global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref target, unchecked((nint)(uint)(Unsafe.SizeOf<TKey>()))), in current.Value);
            }
        }
    }

    /// <summary>Whether an entry stores its value directly after its key.</summary>
    private static class AdjacentPair<TKey, TValue>
    {
        internal static readonly bool Value = Measure();

        /// <summary>Whether the pair occupies the upper eight bytes of a sixteen-byte entry.</summary>
        internal static readonly bool UpperHalf = MeasureUpperHalf();

        /// <summary>Measures the adjacency of the entry's key and value.</summary>
        /// <returns>Whether the condition holds.</returns>
        private static bool Measure()
        {
            DictionaryView<TKey, TValue>.Entry entry = default;
            return Unsafe.ByteOffset(ref Unsafe.As<TKey, byte>(ref entry.Key), ref Unsafe.As<TValue, byte>(ref entry.Value)) == (nint)Unsafe.SizeOf<TKey>();
        }

        /// <summary>Measures whether the pair occupies the entry's upper half.</summary>
        /// <returns>Whether the condition holds.</returns>
        private static bool MeasureUpperHalf()
        {
            DictionaryView<TKey, TValue>.Entry entry = default;
            return Unsafe.ByteOffset(ref Unsafe.As<DictionaryView<TKey, TValue>.Entry, byte>(ref entry), ref Unsafe.As<TKey, byte>(ref entry.Key)) == (nint)8;
        }
    }

    /// <summary>Gets the occupied entry range through the current runtime's HashSet storage layout.</summary>
    /// <param name="view">The existing storage view, including sparse entry slots.</param>
    /// <typeparam name="T">The set element type.</typeparam>
    /// <returns>The number of occupied or free entry slots to inspect, not the public live count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetHashSetEntryCount<T>(HashSetView<T> view) => view._count;

    /// <summary>Obtains the non null pinnable reference for the supplied input.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetNonNullPinnableReference<T>(Span<T> span)
    {
        return ref span.Length == 0
            ? ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), (nint)1)
            : ref Unsafe.AsRef(in span.GetPinnableReference());
    }

    /// <summary>Obtains the non null pinnable reference for the supplied input.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetNonNullPinnableReference<T>(ReadOnlySpan<T> span)
    {
        return ref span.Length == 0
            ? ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), (nint)1)
            : ref Unsafe.AsRef(in span.GetPinnableReference());
    }

    /// <summary>Gets the numeric address of a reference without an unsafe context.</summary>
    /// <param name="reference">A reference into stationary storage: the stack, pinned arrays or native memory.</param>
    /// <typeparam name="T">The referenced type.</typeparam>
    /// <returns>The address, which stays meaningful only while the storage cannot move.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static nint AddressOf<T>(ref T reference) => Unsafe.ByteOffset(ref Unsafe.NullRef<T>(), ref reference);

    /// <summary>Reinterprets an address produced by <see cref="AddressOf{T}"/> as a reference.</summary>
    /// <param name="address">The nonzero address of live stationary storage.</param>
    /// <typeparam name="T">The referenced type.</typeparam>
    /// <returns>A reference to the storage at <paramref name="address"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T RefAt<T>(nint address) => ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), address);

    /// <summary>
    /// Reinterprets the span as bytes.
    /// </summary>
    /// <param name="span"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsRawBytes<T>(this Span<T> span) where T : unmanaged
    {
        return MemoryMarshal.AsBytes(span);
    }

    /// <summary>
    /// Reinterprets memory through the supplied generic types.
    /// </summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="TIn">The type represented by <c>TIn</c>.</typeparam>
    /// <typeparam name="TOut">The type represented by <c>TOut</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TOut> ReinterpretCast<TIn, TOut>(this Span<TIn> span)
        where TIn : unmanaged
        where TOut : unmanaged
    {
        return MemoryMarshal.Cast<TIn, TOut>(span);
    }

    /// <summary>
    /// Returns a reference to the underlying span storage.
    /// </summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T* GetUnderlyingPointer<T>(this Span<T> span) where T : unmanaged
    {
        fixed (T* ptr = &span.GetPinnableReference())
        {
            return ptr;
        }
    }

    /// <summary>
    /// Create a span from a reference without additional safety checks.
    /// </summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> CreateSpanFromRef<T>(ref T data, int length)
    {
        return MemoryMarshal.CreateSpan(ref data, length);
    }

    /// <summary>
    /// Copies a struct's storage representation directly to a byte array.
    /// </summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] StructureToByteArray<T>(ref T value) where T : unmanaged
    {
        byte[] result = new byte[Unsafe.SizeOf<T>()];
        CopyBlockUnaligned(ref MemoryMarshal.GetReference(result.AsSpan()), ref Unsafe.As<T, byte>(ref value), (uint)result.Length);
        return result;
    }

    /// <summary>
    /// Compares complete byte spans without pinning their storage.
    /// </summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool MemoryCompare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b) => a.SequenceEqual(b);

    /// <summary>Creates span from the supplied input.</summary>
    /// <param name="reference">The reference used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> CreateSpan<T>(scoped ref T reference, int length)
    {
        return MemoryMarshal.CreateSpan(ref reference, length);
    }

    /// <summary>Creates read only span from the supplied input.</summary>
    /// <param name="reference">The reference used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> CreateReadOnlySpan<T>(scoped ref T reference, int length)
    {
        return MemoryMarshal.CreateSpan(ref reference, length);
    }

    /// <summary>Performs the as step for the current operation.</summary>
    /// <param name="form">The form used by this operation.</param>
    /// <typeparam name="TForm">The type represented by <c>TForm</c>.</typeparam>
    /// <typeparam name="TTo">The type represented by <c>TTo</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TTo As<TForm, TTo>(ref TForm form)
    {
        return ref Unsafe.As<TForm, TTo>(ref form);
    }

    /// <summary>Performs the as memory step for the current operation.</summary>
    /// <param name="memory">The memory used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(ReadOnlyMemory<T> memory)
    {
        return Unsafe.As<ReadOnlyMemory<T>, Memory<T>>(ref memory);
    }

    /// <summary>Obtains the not null array reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetNotNullArrayReference<T>(T[] array)
    {
#if NET8_0_OR_GREATER
        return ref MemoryMarshal.GetArrayDataReference(array);
#else
        return ref DangerousGetArrayDataReference(array!);
#endif
    }

    /// <summary>Obtains the not null array reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte GetNotNullArrayReference<T>(Array array)
    {
#if NET8_0_OR_GREATER
        return ref MemoryMarshal.GetArrayDataReference(array);
#else
        return ref DangerousGetArrayDataReference<T>(array!);
#endif
    }

    /// <summary>Obtains the array reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetArrayReference<T>(T[]? array)
    {
        if (array is null)
            return ref Unsafe.NullRef<T>();

#if NET8_0_OR_GREATER
        return ref MemoryMarshal.GetArrayDataReference(array);
#else
        return ref DangerousGetArrayDataReference(array!);
#endif

    }

    /// <summary>Obtains the array reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte GetArrayReference<T>(Array? array)
    {
        if (array is null)
            return ref Unsafe.NullRef<byte>();

#if NET8_0_OR_GREATER
        return ref MemoryMarshal.GetArrayDataReference(array);
#else
        return ref DangerousGetArrayDataReference<T>(array!);
#endif
    }

    /// <summary>Obtains the array data reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte GetArrayDataReference<T>(T[] array)
    {
        ref var elementRef = ref GetArrayReference(array);
        return ref Unsafe.As<T, byte>(ref elementRef);
    }

    /// <summary>Obtains the array data reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte GetArrayDataReference<T>(Array array)
    {
        ref var elementRef = ref GetArrayReference<T>(array);
        return ref elementRef;
    }

    /// <summary>Obtains the reference for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte GetReference<T>(ref T value)
    {
        return ref Unsafe.As<T, byte>(ref value);
    }

    /// <summary>Performs the dangerous get array data reference step for the current operation.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T DangerousGetArrayDataReference<T>(T[] array)
    {
#if NETSTANDARD2_1
        // Use the runtime's own array header; null yields a null reference without a separate validation load.
        return ref MemoryMarshal.GetReference(new ReadOnlySpan<T>(array));
#else
        return ref GetArrayReference(array);
#endif
    }

    /// <summary>Performs the dangerous get array data reference step for the current operation.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte DangerousGetArrayDataReference<T>(Array array)
    {
#if NETSTANDARD2_1
        ref var src = ref Unsafe.As<T, byte>(ref Unsafe.As<byte, T>(ref Unsafe.As<LuminRawArrayData>(array).Data));
        return ref Unsafe.Add(ref src, unchecked((nint)(uint)(Unsafe.SizeOf<UIntPtr>())));
#else
        return ref GetArrayReference<T>(array);
#endif
    }


    /// <summary>Calculates the serialized size contribution of string byte count.</summary>
    /// <param name="s">The s used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateStringByteCount(string s)
    {
        if (s.Length > 5)
            return Encoding.UTF8.GetByteCount(s);

        int total = 0;
        for (int index = 0; index < s.Length; index++)
        {
            char c = s[index];
            if (c <= 0x7F) total += 1;
            else if (c <= 0x7FF) total += 2;
            else if (char.IsHighSurrogate(c) && index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]))
            {
                index++;
                total += 4;
            }
            else total += 3;
        }
        return total;
    }

    /// <summary>Performs the allocate uninitialized object step for the current operation.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AllocateUninitializedObject<T>()
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    /// <summary>Performs the allocate uninitialized array step for the current operation.</summary>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] AllocateUninitializedArray<T>(int length)
    {
#if NET8_0_OR_GREATER
        return GC.AllocateUninitializedArray<T>(length);
#else
        return new T[length];
#endif
    }

    /// <summary>
    /// Encodes the string as UTF-8 into the destination span.
    /// </summary>
    /// <param name="source">The input string.</param>
    /// <param name="destination">The destination span.</param>
    /// <returns>The number of bytes written, or -1 if the destination is too small.</returns>
    public static int GetBytes(ReadOnlySpan<char> source, Span<byte> destination)
        => EncodeCore(source, destination);

    /// <summary>Encodes UTF-8 while preserving completed prefixes and legacy replacement boundaries.</summary>
    /// <param name="source">The complete input characters.</param>
    /// <param name="destination">The writable output range.</param>
    /// <returns>The completed byte count, or minus one for insufficient capacity.</returns>
    /// <remarks>Every store is covered by a preceding remaining-capacity check, so the references stay inside both spans.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EncodeCore(ReadOnlySpan<char> source, Span<byte> destination)
    {
        ref char src = ref MemoryMarshal.GetReference(source);
        ref byte dst = ref MemoryMarshal.GetReference(destination);
        nint inputLength = source.Length, outputLength = destination.Length;
        nint input = 0, output = 0;

        // ASCII prefix.
        while (input < inputLength && output < outputLength)
        {
            char ascii = Unsafe.Add(ref src, input);
            if (ascii > 0x7F) break;
            Unsafe.Add(ref dst, output++) = (byte)ascii;
            input++;
        }

        while (input < inputLength)
        {
            if (output >= outputLength) return -1;
            uint c = Unsafe.Add(ref src, input++);
            if (c <= 0x7F)
            {
                Unsafe.Add(ref dst, output++) = (byte)c;
                continue;
            }
            if (c <= 0x7FF)
            {
                if (outputLength - output < 2) return -1;
                Unsafe.Add(ref dst, output) = (byte)(0xC0 | (c >> 6));
                Unsafe.Add(ref dst, output + 1) = (byte)(0x80 | (c & 0x3F));
                output += 2;
                continue;
            }
            if (char.IsHighSurrogate((char)c))
            {
                if (input >= inputLength || outputLength - output < 4)
                    return WriteReplacementChar(ref dst, output, outputLength);
                uint low = Unsafe.Add(ref src, input++);
                if (!char.IsLowSurrogate((char)low)) return WriteReplacementChar(ref dst, output, outputLength);
                uint codePoint = ((c - 0xD800U) << 10) + (low - 0xDC00U) + 0x10000U;
                Unsafe.Add(ref dst, output) = (byte)(0xF0 | (codePoint >> 18));
                Unsafe.Add(ref dst, output + 1) = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                Unsafe.Add(ref dst, output + 2) = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                Unsafe.Add(ref dst, output + 3) = (byte)(0x80 | (codePoint & 0x3F));
                output += 4;
                continue;
            }
            if (outputLength - output < 3) return -1;
            Unsafe.Add(ref dst, output) = (byte)(0xE0 | (c >> 12));
            Unsafe.Add(ref dst, output + 1) = (byte)(0x80 | ((c >> 6) & 0x3F));
            Unsafe.Add(ref dst, output + 2) = (byte)(0x80 | (c & 0x3F));
            output += 3;
        }
        return (int)output;
    }

    /// <summary>Writes the legacy replacement sequence within the complete validated output range.</summary>
    /// <param name="destination">The first byte of the writable output range.</param>
    /// <param name="offset">The prefix already written.</param>
    /// <param name="length">The complete output length.</param>
    /// <returns>The completed byte count, or minus one for insufficient capacity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteReplacementChar(ref byte destination, nint offset, nint length)
    {
        if (length - offset < 3) return -1;
        Unsafe.Add(ref destination, offset) = 0xEF;
        Unsafe.Add(ref destination, offset + 1) = 0xBF;
        Unsafe.Add(ref destination, offset + 2) = 0xBD;
        return (int)(offset + 3);
    }

    /// <summary>Obtains the method table for the supplied input.</summary>
    /// <param name="obj">The obj used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr GetMethodTable(object obj)
    {
#if NET8_0_OR_GREATER
        return Unsafe.Add(ref Unsafe.As<byte, IntPtr>(ref Unsafe.As<RawData>(obj).Data), -1);
#else
        return GetMethodTable(obj.GetType());
#endif
    }

    /// <summary>Obtains the method table for the supplied input.</summary>
    /// <param name="obj">The obj used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr GetMethodTable<T>(T obj) where T : class
    {
#if NET8_0_OR_GREATER
        return Unsafe.Add(ref Unsafe.As<byte, IntPtr>(ref Unsafe.As<RawData>(obj).Data), -1);
#else
        return GetMethodTable(obj.GetType());
#endif

    }

    /// <summary>Obtains the method table for the supplied input.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr GetMethodTable(Type type)
    {
        return type.TypeHandle.Value;
    }

    /// <summary>Performs the align of step for the current operation.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignOf<T>() where T : unmanaged => (nuint)Unsafe.SizeOf<AlignHelper<T>>() - (nuint)Unsafe.SizeOf<T>();

    /// <summary>Provides raw data functionality.</summary>
    private sealed class RawData
    {
        /// <summary>Stores the data.</summary>
        public byte Data;
    }

    /// <summary>Provides method table functionality.</summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct MethodTable
    {
        /// <summary>Stores the component size.</summary>
        [FieldOffset(0)]
        public ushort ComponentSize;
        /// <summary>Stores the flags.</summary>
        [FieldOffset(0)]
        public uint Flags;
        /// <summary>Stores the base size.</summary>
        [FieldOffset(4)]
        public uint BaseSize;
        /// <summary>Stores the interface count.</summary>
        [FieldOffset(14)]
        public ushort InterfaceCount;
        /// <summary>Stores the parent method table.</summary>
        [FieldOffset(16 /*0x10*/)]
        public unsafe MethodTable* ParentMethodTable;
        /// <summary>Stores the auxiliary data.</summary>
        [FieldOffset(32 /*0x20*/)]
        public unsafe IntPtr* AuxiliaryData;
        /// <summary>Stores the element type.</summary>
        [FieldOffset(48 /*0x30*/)]
        public unsafe void* ElementType;
        /// <summary>Stores the interface map.</summary>
        [FieldOffset(56)]
        public unsafe MethodTable** InterfaceMap;

        /// <summary>Gets the has component size.</summary>
        public bool HasComponentSize => (this.Flags & 2147483648U /*0x80000000*/) > 0U;

        /// <summary>Gets the contains gc pointers.</summary>
        public bool ContainsGCPointers => (this.Flags & 16777216U /*0x01000000*/) > 0U;

        /// <summary>Gets the non trivial interface cast.</summary>
        public bool NonTrivialInterfaceCast => (this.Flags & 1347158016U /*0x504C0000*/) > 0U;

        /// <summary>Gets the has type equivalence.</summary>
        public bool HasTypeEquivalence => (this.Flags & 33554432U /*0x02000000*/) > 0U;

        /// <summary>Gets the has finalizer.</summary>
        public bool HasFinalizer => (this.Flags & 1048576U /*0x100000*/) > 0U;

        /// <summary>Performs the are same type step for the current operation.</summary>
        /// <param name="mt1">The mt1 used by this operation.</param>
        /// <param name="mt2">The mt2 used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        internal static unsafe bool AreSameType(MethodTable* mt1, MethodTable* mt2) => mt1 == mt2;

        /// <summary>Gets the has default constructor.</summary>
        public bool HasDefaultConstructor
        {
            get => ((int)this.Flags & -2147483136 /*0x80000200*/) == 512 /*0x0200*/;
        }

        /// <summary>Gets the is multi dimensional array.</summary>
        public bool IsMultiDimensionalArray
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.BaseSize > (uint)(3 * 8);
        }

        /// <summary>Gets the multi dimensional array rank.</summary>
        public int MultiDimensionalArrayRank
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (int)((this.BaseSize - (uint)(3 * 8)) / 8U);
            }
        }

        /// <summary>Gets the is interface.</summary>
        public bool IsInterface => ((int)this.Flags & 983040 /*0x0F0000*/) == 786432 /*0x0C0000*/;

        /// <summary>Gets the is value type.</summary>
        public bool IsValueType => ((int)this.Flags & 786432 /*0x0C0000*/) == 262144 /*0x040000*/;

        /// <summary>Gets the is nullable.</summary>
        public bool IsNullable => ((int)this.Flags & 983040 /*0x0F0000*/) == 327680 /*0x050000*/;

        /// <summary>Gets the is by ref like.</summary>
        public bool IsByRefLike => ((int)this.Flags & -2147479552 /*0x80001000*/) == 4096 /*0x1000*/;

        /// <summary>Gets the is primitive.</summary>
        public bool IsPrimitive
        {
            get
            {
                bool isPrimitive;
                switch (this.Flags & 983040U /*0x0F0000*/)
                {
                    case 393216 /*0x060000*/:
                    case 458752 /*0x070000*/:
                        isPrimitive = true;
                        break;
                    default:
                        isPrimitive = false;
                        break;
                }
                return isPrimitive;
            }
        }

        /// <summary>Gets the is true primitive.</summary>
        public bool IsTruePrimitive => ((int)this.Flags & 983040 /*0x0F0000*/) == 458752 /*0x070000*/;

        /// <summary>Gets the has instantiation.</summary>
        public bool HasInstantiation
        {
            get => ((int)this.Flags & int.MinValue) == 0 && (this.Flags & 48U /*0x30*/) > 0U;
        }

        /// <summary>Gets the is generic type definition.</summary>
        public bool IsGenericTypeDefinition
        {
            get => ((int)this.Flags & -2147483600 /*0x80000030*/) == 48 /*0x30*/;
        }

        /// <summary>Gets the is constructed generic type.</summary>
        public bool IsConstructedGenericType
        {
            get
            {
                uint num = this.Flags & 2147483696U /*0x80000030*/;
                return num == 16U /*0x10*/ || num == 32U /*0x20*/;
            }
        }

        /// <summary>Gets the contains generic variables.</summary>
        public bool ContainsGenericVariables => (this.Flags & 536870912U /*0x20000000*/) > 0U;

        /// <summary>Performs the p invoke step for the current operation.</summary>
        /// <param name="__pMTa_native">The p m ta native used by this operation.</param>
        /// <param name="__pMTb_native">The p m tb native used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [DllImport("QCall", EntryPoint = "MethodTable_AreTypesEquivalent")]
        public static extern unsafe int __PInvoke(MethodTable* __pMTa_native, MethodTable* __pMTb_native);

        /// <summary>Obtains the num instance field bytes for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern uint GetNumInstanceFieldBytes();

    }

    /// <summary>Performs the round up to power of2 step for the current operation.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RoundUpToPowerOf2(uint value)
    {
        if (value == 0) return 1;
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    /// <summary>Computes the reciprocal multiplier used by the fast remainder calculation.</summary>
    /// <param name="divisor">The divisor used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetFastModMultiplier(uint divisor)
    {
        return ulong.MaxValue / divisor + 1;
    }

    /// <summary>Calculates the remainder using the precomputed reciprocal multiplier.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="divisor">The divisor used by this operation.</param>
    /// <param name="multiplier">The multiplier used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FastMod(uint value, uint divisor, ulong multiplier)
    {
        // Equivalent to value % divisor.
        // The divisor is invariant throughout Rebuild, so compute the multiplier once.
        return (uint)(((((multiplier * value) >> 32) + 1) * divisor) >> 32);
    }

    /// <summary>Obtains the bucket index for the supplied input.</summary>
    /// <param name="hashCode">The hash code used by this operation.</param>
    /// <param name="bucketCount">The bucket count used by this operation.</param>
    /// <param name="fastModMultiplier">The fast mod multiplier used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBucketIndex(
        uint hashCode,
        uint bucketCount,
        ulong fastModMultiplier)
    {
        // A hash already inside the bucket range is its own remainder.
        if (hashCode < bucketCount)
            return hashCode;

        // Unsafe.SizeOf<IntPtr>() is a platform constant for JIT and AOT compilation,
        // The JIT normally folds this condition outside the loop.
        if (Unsafe.SizeOf<IntPtr>() == 8)
            return FastMod(hashCode, bucketCount, fastModMultiplier);

        return hashCode % bucketCount;
    }

    /// <summary>
    /// Rebuilds Dictionary buckets and count after deserialization writes directly to entries.
    /// Bypasses Dictionary.Add hashing, duplicate-key checks, and capacity growth.
    /// Only valid after input validation has established that no duplicate keys exist.
    /// </summary>
    /// <param name="view">The view used by this operation.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
    /// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RebuildDictionaryBuckets<TKey, TValue>(
        DictionaryView<TKey, TValue?> view,
        int count)
        where TKey : notnull
    {
#if !NETSTANDARD2_1
        if (Unsafe.SizeOf<IntPtr>() != 8)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowPlatformNotSupportedException(
                "Direct Dictionary bucket rebuilding requires the modern 64-bit Dictionary layout.");
#endif

        ref var entriesRef = ref GetArrayReference(view._entries);
        ref var bucketsRef = ref GetArrayReference(view._buckets);

        uint bucketCount = (uint)view._buckets.Length;

        // Use FastMod on 64-bit platforms.
        // Perform one integer division outside the loop.
#if NETSTANDARD2_1
        ulong fastModMultiplier = Unsafe.SizeOf<IntPtr>() == 8
            ? GetFastModMultiplier(bucketCount)
            : 0;
#else
        // Dictionary already computed this value when it allocated _buckets.
        ulong fastModMultiplier = view._fastModMultiplier;
#endif

        /*
         * For unmanaged keys with the default comparer, Dictionary can directly invoke
         * TKey.GetHashCode()。
         *
         * For closed unmanaged generic types:
         *
         * Dictionary<int, ...>
         * Dictionary<long, ...>
         * Dictionary<float, ...>
         * Dictionary<MyEnum, ...>
         *
         * The JIT can fold typeof(TKey).IsValueType and
         * devirtualize and inline constrained GetHashCode calls.
         *
         * This avoids IEqualityComparer<TKey>.GetHashCode interface calls in hot loops.
         */
        if (typeof(TKey).IsValueType && view._comparer is null)
        {
            for (int i = 0; i < count; i++)
            {
                ref var entry =
                    ref Unsafe.Add(
                        ref entriesRef,
                        (nint)(uint)i);

                uint hashCode =
                    unchecked((uint)entry.Key.GetHashCode());

#if NETSTANDARD2_1
                // Preserve the existing NETSTANDARD2_1 and Unity behavior.
                hashCode &= 0x7FFFFFFF;
#endif

                entry.HashCode = hashCode;

                uint bucketIndex =
                    GetBucketIndex(
                        hashCode,
                        bucketCount,
                        fastModMultiplier);

                ref int bucket =
                    ref Unsafe.Add(
                        ref bucketsRef,
                        (nint)bucketIndex);

                entry.Next = bucket - 1;
                bucket = i + 1;
            }

            view._count = count;
            return;
        }

        /*
         * Reference types and custom comparers must preserve comparer semantics.
         *
         * In particular, never bypass the configured comparer for string or other reference keys.
         */
        var comparer =
            view._comparer ??
            EqualityComparer<TKey>.Default;

        for (int i = 0; i < count; i++)
        {
            ref var entry =
                ref Unsafe.Add(
                    ref entriesRef,
                    (nint)(uint)i);

#if NETSTANDARD2_1
            uint hashCode =
                unchecked(
                    (uint)(
                        comparer.GetHashCode(entry.Key) &
                        0x7FFFFFFF));
#else
            uint hashCode =
                unchecked(
                    (uint)comparer.GetHashCode(entry.Key));
#endif

            entry.HashCode = hashCode;

            uint bucketIndex =
                GetBucketIndex(
                    hashCode,
                    bucketCount,
                    fastModMultiplier);

            ref int bucket =
                ref Unsafe.Add(
                    ref bucketsRef,
                    (nint)bucketIndex);

            entry.Next = bucket - 1;
            bucket = i + 1;
        }

        view._count = count;
    }

    /// <summary>Empties a dictionary for a sequential refill of <paramref name="count"/> entries, keeping its storage.</summary>
    /// <param name="dictionary">The default-comparer dictionary to refill.</param>
    /// <param name="count">The number of entries the caller stores next.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>A view whose entries have room for <paramref name="count"/> pairs and whose buckets are empty.</returns>
    /// <remarks>When the pairs hold no references, entries past the new count are never read and need no clearing, so
    /// only the buckets are reset; otherwise, or when the storage is too small, the public Clear and EnsureCapacity apply.
    /// The caller then stores the pairs and runs <see cref="TryRebuildFreshDictionaryBuckets{TKey, TValue}"/>.</remarks>
    public static DictionaryView<TKey, TValue?> PrepareDictionaryRefill<TKey, TValue>(Dictionary<TKey, TValue?> dictionary, int count)
        where TKey : notnull
    {
        var view = GetDictionaryView(dictionary);
        var entries = view._entries;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>() || RuntimeHelpers.IsReferenceOrContainsReferences<TValue>() ||
            entries is null || entries.Length < count)
        {
            dictionary.Clear();
            dictionary.EnsureCapacity(count);
            return GetDictionaryView(dictionary);
        }

        view._buckets.AsSpan().Clear();
        view._count = 0;
        view._freeList = -1;
        view._freeCount = 0;
        return view;
    }

    /// <summary>
    /// Builds the buckets of a newly allocated dictionary whose entries were populated sequentially.
    /// Returns <see langword="false"/> when the normal <see cref="Dictionary{TKey,TValue}.Add"/>
    /// path is required to preserve null-key, duplicate-key, or collision-randomization behavior.
    /// </summary>
    /// <param name="view">The view used by this operation.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
    /// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryRebuildFreshDictionaryBuckets<TKey, TValue>(
        DictionaryView<TKey, TValue?> view,
        int count)
        where TKey : notnull
    {
#if !NETSTANDARD2_1
        // CoreCLR x86 omits Dictionary._fastModMultiplier. DictionaryView models
        // the modern 64-bit layout, so every field after _entries is shifted on
        // x86. Return before reading any such field; generated callers already
        // rebuild through the public Dictionary.Add path when this returns false.
        if (Unsafe.SizeOf<IntPtr>() != 8)
            return false;
#endif

        ref var entriesRef = ref GetArrayReference(view._entries);
        ref var bucketsRef = ref GetArrayReference(view._buckets);
        uint bucketCount = (uint)view._buckets.Length;
#if NETSTANDARD2_1
        ulong fastModMultiplier = Unsafe.SizeOf<IntPtr>() == 8
            ? GetFastModMultiplier(bucketCount)
            : 0;
#else
        // Dictionary already computed this value when it allocated _buckets.
        ulong fastModMultiplier = view._fastModMultiplier;
#endif

        // Generated callers construct the dictionary with the default comparer.
        // Value-type keys can therefore use their constrained hash/equality path even
        // on runtimes that materialize EqualityComparer<TKey>.Default in _comparer.
        if (typeof(TKey).IsValueType)
        {
            ref var entry = ref entriesRef;
            for (int i = 0; i < count; i++)
            {
                if (entry.Key is null)
                    return false;

                uint hashCode = unchecked((uint)entry.Key.GetHashCode());
#if NETSTANDARD2_1
                hashCode &= 0x7FFFFFFF;
#endif
                uint bucketIndex = GetBucketIndex(hashCode, bucketCount, fastModMultiplier);
                ref int bucket = ref Unsafe.Add(ref bucketsRef, (nint)bucketIndex);
                int previous = bucket - 1;
                int collisionCount = 0;

                while ((uint)previous < (uint)i)
                {
                    ref var candidate = ref Unsafe.Add(ref entriesRef, (nint)(uint)previous);
                    // Keep the default comparer in the collision path so primitive
                    // specializations can fold equality without retaining its instance.
                    if (candidate.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(candidate.Key, entry.Key))
                        return false;

                    previous = candidate.Next;
                    if (++collisionCount > 100)
                        return false;
                }

                entry.HashCode = hashCode;
                entry.Next = bucket - 1;
                bucket = i + 1;
                entry = ref Unsafe.Add(ref entry, 1);
            }
        }
        else
        {
            var comparer = view._comparer ?? EqualityComparer<TKey>.Default;
            ref var entry = ref entriesRef;
            for (int i = 0; i < count; i++)
            {
                if (entry.Key is null)
                    return false;

#if NETSTANDARD2_1
                uint hashCode = unchecked((uint)(comparer.GetHashCode(entry.Key) & 0x7FFFFFFF));
#else
                uint hashCode = unchecked((uint)comparer.GetHashCode(entry.Key));
#endif
                uint bucketIndex = GetBucketIndex(hashCode, bucketCount, fastModMultiplier);
                ref int bucket = ref Unsafe.Add(ref bucketsRef, (nint)bucketIndex);
                int previous = bucket - 1;
                int collisionCount = 0;

                while ((uint)previous < (uint)i)
                {
                    ref var candidate = ref Unsafe.Add(ref entriesRef, (nint)(uint)previous);
                    if (candidate.HashCode == hashCode && comparer.Equals(candidate.Key, entry.Key))
                        return false;

                    previous = candidate.Next;
                    if (++collisionCount > 100)
                        return false;
                }

                entry.HashCode = hashCode;
                entry.Next = bucket - 1;
                bucket = i + 1;
                entry = ref Unsafe.Add(ref entry, 1);
            }
        }

        view._count = count;
        view._version = count;
        return true;
    }

    /// <summary>
    /// Rebuilds HashSet buckets and count after deserialization writes directly to entries.
    /// Bypasses HashSet.Add hashing, duplicate-value checks, and capacity growth.
    /// The payload must contain unique values under the destination comparer; this helper does not deduplicate.
    /// </summary>
    /// <param name="view">The view used by this operation.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RebuildHashSetBuckets<T>(
        HashSetView<T?> view,
        int count)
    {
        ref var entriesRef = ref GetArrayReference(view._entries);
        ref var bucketsRef = ref GetArrayReference(view._buckets);

        uint bucketCount = (uint)view._buckets.Length;

        ulong fastModMultiplier =
            Unsafe.SizeOf<IntPtr>() == 8
                ? GetFastModMultiplier(bucketCount)
                : 0;

        /*
         * Match Dictionary behavior:
         * With unmanaged keys and the default comparer, use constrained T.GetHashCode().
         *
         * Avoid IEqualityComparer<T>.GetHashCode
         * interface calls for each element.
         */
        if (typeof(T).IsValueType && view._comparer is null)
        {
            for (int i = 0; i < count; i++)
            {
                ref var entry =
                    ref Unsafe.Add(
                        ref entriesRef,
                        (nint)(uint)i);

                uint hashCode =
                    unchecked(
                        (uint)entry.Value!.GetHashCode());

#if NETSTANDARD2_1
                hashCode &= 0x7FFFFFFF;
#endif

                entry.HashCode = hashCode;

                uint bucketIndex =
                    GetBucketIndex(
                        hashCode,
                        bucketCount,
                        fastModMultiplier);

                ref int bucket =
                    ref Unsafe.Add(
                        ref bucketsRef,
                        (nint)bucketIndex);

                entry.Next = bucket - 1;
                bucket = i + 1;
            }

            view._count = count;

#if NETSTANDARD2_1
            view._lastIndex = count;
#endif

            return;
        }

        var comparer =
            view._comparer ??
            EqualityComparer<T?>.Default;

        for (int i = 0; i < count; i++)
        {
            ref var entry =
                ref Unsafe.Add(
                    ref entriesRef,
                    (nint)(uint)i);

#if NETSTANDARD2_1
            uint hashCode =
                entry.Value is null ? 0u : unchecked(
                    (uint)(
                        comparer.GetHashCode(entry.Value!) &
                        0x7FFFFFFF));
#else
            uint hashCode =
                entry.Value is null ? 0u : unchecked(
                    (uint)comparer.GetHashCode(entry.Value!));
#endif

            entry.HashCode = hashCode;

            uint bucketIndex =
                GetBucketIndex(
                    hashCode,
                    bucketCount,
                    fastModMultiplier);

            ref int bucket =
                ref Unsafe.Add(
                    ref bucketsRef,
                    (nint)bucketIndex);

            entry.Next = bucket - 1;
            bucket = i + 1;
        }

        view._count = count;

#if NETSTANDARD2_1
        view._lastIndex = count;
#endif
    }

    /// <summary>Presents the storage fields of a <see cref="Dictionary{TKey, TValue}"/> at their probed offsets.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <remarks>The view holds only the dictionary reference, so reinterpreting a dictionary reference as the view is valid.
    /// Each member returns a reference to the dictionary's own field; see <see cref="LuminCollectionLayouts"/>.</remarks>
    public readonly struct DictionaryView<TKey, TValue>
    {
        private readonly object _dictionary;

        /// <summary>Whether the runtime's entry storage matches <see cref="Entry"/>.</summary>
        internal static readonly bool Recognized = LuminCollectionLayouts.DictionaryEntries >= 0 && CheckEntries();

        /// <summary>Creates a view of a dictionary.</summary>
        /// <param name="dictionary">The dictionary.</param>
        internal DictionaryView(object dictionary) => _dictionary = dictionary;

        /// <summary>Gets the buckets.</summary>
        public ref int[] _buckets
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int[]>(_dictionary, LuminCollectionLayouts.DictionaryBuckets);
        }

        /// <summary>Gets the entries, viewed as <see cref="Entry"/> storage.</summary>
        public ref Entry[] _entries
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<Entry[]>(_dictionary, LuminCollectionLayouts.DictionaryEntries);
        }

        /// <summary>Gets the cached bucket multiplier, which modern 64-bit runtimes store.</summary>
        public ref ulong _fastModMultiplier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                LuminCollectionLayouts.Require(LuminCollectionLayouts.DictionaryFastMod, "Dictionary<TKey, TValue>");
                return ref LuminFieldLayout.Field<ulong>(_dictionary, LuminCollectionLayouts.DictionaryFastMod);
            }
        }

        /// <summary>Gets the entry count.</summary>
        public ref int _count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int>(_dictionary, LuminCollectionLayouts.DictionaryCount);
        }

        /// <summary>Gets the free-list head.</summary>
        public ref int _freeList
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int>(_dictionary, LuminCollectionLayouts.DictionaryFreeList);
        }

        /// <summary>Gets the free count.</summary>
        public ref int _freeCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int>(_dictionary, LuminCollectionLayouts.DictionaryFreeCount);
        }

        /// <summary>Gets the version.</summary>
        public ref int _version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int>(_dictionary, LuminCollectionLayouts.DictionaryVersion);
        }

        /// <summary>Gets the comparer.</summary>
        public ref IEqualityComparer<TKey> _comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<IEqualityComparer<TKey>>(_dictionary, LuminCollectionLayouts.DictionaryComparer);
        }

        /// <summary>Checks the probed entry fields and stride against <see cref="Entry"/>.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool CheckEntries()
        {
            Entry entry = default;
            ref byte start = ref Unsafe.As<Entry, byte>(ref entry);
            return LuminEntryLayout<Entry>.Matches(LuminFieldLayout.FieldType(typeof(Dictionary<TKey, TValue>), "_entries")?.GetElementType(),
                ("hashCode|HashCode", (int)Unsafe.ByteOffset(ref start, ref Unsafe.As<uint, byte>(ref entry.HashCode))),
                ("next|Next", (int)Unsafe.ByteOffset(ref start, ref Unsafe.As<int, byte>(ref entry.Next))),
                ("key|Key", (int)Unsafe.ByteOffset(ref start, ref Unsafe.As<TKey, byte>(ref entry.Key))),
                ("value|Value", (int)Unsafe.ByteOffset(ref start, ref Unsafe.As<TValue, byte>(ref entry.Value))));
        }

        /// <summary>Provides entry functionality.</summary>
        public struct Entry
        {
            /// <summary>Stores the hash code.</summary>
            public uint HashCode;
            /// <summary>Stores the next.</summary>
            public int Next;
            /// <summary>Stores the key.</summary>
            public TKey Key;
            /// <summary>Stores the value.</summary>
            public TValue Value;
        }
    }

    /// <summary>Presents the storage fields of a <see cref="HashSet{T}"/> at their probed offsets.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>The view holds only the set reference, so reinterpreting a set reference as the view is valid. Each member
    /// returns a reference to the set's own field; see <see cref="LuminCollectionLayouts"/>.</remarks>
    public readonly struct HashSetView<T>
    {
        private readonly object _set;

        /// <summary>Whether the runtime's entry storage matches <see cref="Entry"/>.</summary>
        internal static readonly bool Recognized = LuminCollectionLayouts.HashSetEntries >= 0 && CheckEntries();

        /// <summary>Creates a view of a set.</summary>
        /// <param name="set">The set.</param>
        internal HashSetView(object set) => _set = set;

        /// <summary>Gets the buckets.</summary>
        public ref int[] _buckets
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int[]>(_set, LuminCollectionLayouts.HashSetBuckets);
        }

        /// <summary>Gets the entries, viewed as <see cref="Entry"/> storage.</summary>
        public ref Entry[] _entries
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<Entry[]>(_set, LuminCollectionLayouts.HashSetEntries);
        }

        /// <summary>Gets the number of used entry slots.</summary>
        public ref int _count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int>(_set, LuminCollectionLayouts.HashSetCount);
        }

        /// <summary>Gets the free-list head.</summary>
        public ref int _freeList
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int>(_set, LuminCollectionLayouts.HashSetFreeList);
        }

        /// <summary>Gets the free count, which modern runtimes store.</summary>
        public ref int _freeCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                LuminCollectionLayouts.Require(LuminCollectionLayouts.HashSetFreeCount, "HashSet<T>");
                return ref LuminFieldLayout.Field<int>(_set, LuminCollectionLayouts.HashSetFreeCount);
            }
        }

        /// <summary>Gets the last used index, which the legacy set stores.</summary>
        public ref int _lastIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                LuminCollectionLayouts.Require(LuminCollectionLayouts.HashSetLastIndex, "HashSet<T>");
                return ref LuminFieldLayout.Field<int>(_set, LuminCollectionLayouts.HashSetLastIndex);
            }
        }

        /// <summary>Gets the version.</summary>
        public ref int _version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<int>(_set, LuminCollectionLayouts.HashSetVersion);
        }

        /// <summary>Gets the comparer.</summary>
        public ref IEqualityComparer<T> _comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LuminFieldLayout.Field<IEqualityComparer<T>>(_set, LuminCollectionLayouts.HashSetComparer);
        }

        /// <summary>Checks the probed entry fields and stride against <see cref="Entry"/>.</summary>
        /// <returns>Whether the storage behaves as the public API does.</returns>
        private static bool CheckEntries()
        {
            Entry entry = default;
            ref byte start = ref Unsafe.As<Entry, byte>(ref entry);
            return LuminEntryLayout<Entry>.Matches(LuminFieldLayout.FieldType(typeof(HashSet<T>), "_entries")?.GetElementType() ??
                                                   LuminFieldLayout.FieldType(typeof(HashSet<T>), "_slots")?.GetElementType(),
                ("HashCode|hashCode", (int)Unsafe.ByteOffset(ref start, ref Unsafe.As<uint, byte>(ref entry.HashCode))),
                ("Next|next", (int)Unsafe.ByteOffset(ref start, ref Unsafe.As<int, byte>(ref entry.Next))),
                ("Value|value", (int)Unsafe.ByteOffset(ref start, ref Unsafe.As<T, byte>(ref entry.Value))));
        }

        /// <summary>Provides entry functionality.</summary>
        public struct Entry
        {
            /// <summary>Stores the hash code.</summary>
            public uint HashCode;
            /// <summary>Stores the next.</summary>
            public int Next;
            /// <summary>Stores the value.</summary>
            public T Value;
        }
    }

    /// <summary>Provides align helper functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    private struct AlignHelper<T> where T : unmanaged
    {
        private byte _dummy;
        private T _data;
    }

    /// <summary>Writes the original raw hash-set record with grouped small dense stores where 64-bit access permits.</summary>
    /// <typeparam name="T">The raw element storage.</typeparam>
    /// <param name="writer">The complete output state.</param>
    /// <param name="set">The original non-null set.</param>
    /// <remarks>Only one- or two-byte dense elements are grouped; other widths, sparse entries and 32-bit targets retain scalar storage.</remarks>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public static void WriteSmallHashSetRaw<T>(ref global::LuminPack.Core.LuminPackWriter writer, HashSet<T> set) where T : unmanaged
    {
        int count = set.Count;
        int bytes = checked(count * Unsafe.SizeOf<T>());
        writer.EnsureAdditionalCapacity(checked(sizeof(int) + bytes));
        ref byte cursor = ref writer.CurrentReference;
        global::LuminPack.Core.LuminPackWriter.Write(ref cursor, in count);
        if (count != 0)
        {
            ref byte destination = ref Unsafe.Add(ref cursor, sizeof(int));
            var view = GetHashSetView(set);
            ref var entry = ref GetArrayReference(view._entries);
            int used = view._count;
            if (used == count)
            {
                int index = 0;
                if (Unsafe.SizeOf<IntPtr>() == 8 && Unsafe.SizeOf<T>() == 1)
                {
                    for (; index <= count - 4; index += 4)
                    {
                        uint packed = Unsafe.As<T, byte>(ref Unsafe.Add(ref entry, (nint)(uint)index).Value) |
                            (uint)Unsafe.As<T, byte>(ref Unsafe.Add(ref entry, (nint)(uint)(index + 1)).Value) << 8 |
                            (uint)Unsafe.As<T, byte>(ref Unsafe.Add(ref entry, (nint)(uint)(index + 2)).Value) << 16 |
                            (uint)Unsafe.As<T, byte>(ref Unsafe.Add(ref entry, (nint)(uint)(index + 3)).Value) << 24;
                        global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)index), in packed);
                    }
                }
                else if (Unsafe.SizeOf<IntPtr>() == 8 && Unsafe.SizeOf<T>() == 2)
                {
                    for (; index <= count - 4; index += 4)
                    {
                        ulong packed = Unsafe.As<T, ushort>(ref Unsafe.Add(ref entry, (nint)(uint)index).Value) |
                            (ulong)Unsafe.As<T, ushort>(ref Unsafe.Add(ref entry, (nint)(uint)(index + 1)).Value) << 16 |
                            (ulong)Unsafe.As<T, ushort>(ref Unsafe.Add(ref entry, (nint)(uint)(index + 2)).Value) << 32 |
                            (ulong)Unsafe.As<T, ushort>(ref Unsafe.Add(ref entry, (nint)(uint)(index + 3)).Value) << 48;
                        global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)index * 2), in packed);
                    }
                }
                for (; index < count; index++)
                    global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)index * Unsafe.SizeOf<T>()), in Unsafe.Add(ref entry, (nint)(uint)index).Value);
            }
            else
            {
                int written = 0;
                for (int i = 0; i < used && written < count; i++)
                {
                    ref var current = ref Unsafe.Add(ref entry, (nint)(uint)i);
                    if (IsHashTableEntryActive(current.HashCode, current.Next))
                        global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)written++ * Unsafe.SizeOf<T>()), in current.Value);
                }
            }
        }
        writer.AdvanceSafe(sizeof(int) + bytes);
    }

}
