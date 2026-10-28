using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using static LuminPack.Code.LuminFieldLayout;

namespace LuminPack.Code;

/// <summary>Holds the run-time field offsets of the framework collections whose storage generated formatters access.</summary>
/// <remarks>
/// <para>Each family is probed by <see cref="LuminFieldLayout"/> on two instantiations with different element storage. When
/// the metadata is available, the probed offsets are used, so a runtime whose collections declare their fields in another
/// order is still read correctly. When it is unavailable, for example after trimming, the offsets of the compiled views of
/// the expected layout are used, which is the established behavior. A family whose probe contradicts itself is unavailable,
/// and its accessors throw <see cref="PlatformNotSupportedException"/> instead of reading other storage.</para>
/// <para>The offsets are <see langword="static readonly"/> fields of this non-generic class, so optimized code, including shared
/// generic code, addresses each field with a constant displacement, as a field of a declared type does.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminCollectionLayouts
{
    // List<T>
    private static readonly int[] s_list = Family(typeof(List<int>), typeof(List<object>), CompiledList(), "_items", "_size", "_version");
    /// <summary>Gets the offset of <c>List&lt;T&gt;._items</c>.</summary>
    public static readonly int ListItems = s_list[0];
    /// <summary>Gets the offset of <c>List&lt;T&gt;._size</c>.</summary>
    public static readonly int ListSize = s_list[1];
    /// <summary>Gets the offset of <c>List&lt;T&gt;._version</c>.</summary>
    public static readonly int ListVersion = s_list[2];

    // Stack<T>
    private static readonly int[] s_stack = Family(typeof(Stack<int>), typeof(Stack<object>), CompiledStack(), "_array|_items", "_size", "_version");
    /// <summary>Gets the offset of the backing array of <c>Stack&lt;T&gt;</c>.</summary>
    public static readonly int StackArray = s_stack[0];
    /// <summary>Gets the offset of the size of <c>Stack&lt;T&gt;</c>.</summary>
    public static readonly int StackSize = s_stack[1];

    // Queue<T>
    private static readonly int[] s_queue = Family(typeof(Queue<int>), typeof(Queue<object>), CompiledQueue(), "_array|_items", "_head", "_tail", "_size", "_version");
    /// <summary>Gets the offset of the backing array of <c>Queue&lt;T&gt;</c>.</summary>
    public static readonly int QueueArray = s_queue[0];
    /// <summary>Gets the offset of the head index of <c>Queue&lt;T&gt;</c>.</summary>
    public static readonly int QueueHead = s_queue[1];
    /// <summary>Gets the offset of the tail index of <c>Queue&lt;T&gt;</c>.</summary>
    public static readonly int QueueTail = s_queue[2];
    /// <summary>Gets the offset of the size of <c>Queue&lt;T&gt;</c>.</summary>
    public static readonly int QueueSize = s_queue[3];

    // HashSet<T>
    private static readonly int[] s_hashSet = Family(typeof(HashSet<int>), typeof(HashSet<object>), CompiledHashSet(),
        "_buckets", "_entries|_slots", "_count", "_freeList", "?_freeCount", "_version", "_comparer", "?_fastModMultiplier", "?_lastIndex");
    /// <summary>Gets the offset of the buckets of <c>HashSet&lt;T&gt;</c>.</summary>
    public static readonly int HashSetBuckets = s_hashSet[0];
    /// <summary>Gets the offset of the entries of <c>HashSet&lt;T&gt;</c>.</summary>
    public static readonly int HashSetEntries = s_hashSet[1];
    /// <summary>Gets the offset of the entry count of <c>HashSet&lt;T&gt;</c>.</summary>
    public static readonly int HashSetCount = s_hashSet[2];
    /// <summary>Gets the offset of the free-list head of <c>HashSet&lt;T&gt;</c>.</summary>
    public static readonly int HashSetFreeList = s_hashSet[3];
    /// <summary>Gets the offset of the free count of <c>HashSet&lt;T&gt;</c>, or <see cref="LuminFieldLayout.Unavailable"/>.</summary>
    public static readonly int HashSetFreeCount = s_hashSet[4];
    /// <summary>Gets the offset of the version of <c>HashSet&lt;T&gt;</c>.</summary>
    public static readonly int HashSetVersion = s_hashSet[5];
    /// <summary>Gets the offset of the comparer of <c>HashSet&lt;T&gt;</c>.</summary>
    public static readonly int HashSetComparer = s_hashSet[6];
    /// <summary>Gets the offset of the cached bucket multiplier of <c>HashSet&lt;T&gt;</c>, or <see cref="LuminFieldLayout.Unavailable"/>.</summary>
    public static readonly int HashSetFastMod = s_hashSet[7];
    /// <summary>Gets the offset of the last index of the legacy <c>HashSet&lt;T&gt;</c>, or <see cref="LuminFieldLayout.Unavailable"/>.</summary>
    public static readonly int HashSetLastIndex = s_hashSet[8];

    // Dictionary<TKey, TValue>
    private static readonly int[] s_dictionary = Family(typeof(Dictionary<int, int>), typeof(Dictionary<object, object>), CompiledDictionary(),
        "_buckets", "_entries", "_count", "_freeList", "_freeCount", "_version", "_comparer", "?_fastModMultiplier");
    /// <summary>Gets the offset of the buckets of <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int DictionaryBuckets = s_dictionary[0];
    /// <summary>Gets the offset of the entries of <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int DictionaryEntries = s_dictionary[1];
    /// <summary>Gets the offset of the entry count of <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int DictionaryCount = s_dictionary[2];
    /// <summary>Gets the offset of the free-list head of <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int DictionaryFreeList = s_dictionary[3];
    /// <summary>Gets the offset of the free count of <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int DictionaryFreeCount = s_dictionary[4];
    /// <summary>Gets the offset of the version of <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int DictionaryVersion = s_dictionary[5];
    /// <summary>Gets the offset of the comparer of <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int DictionaryComparer = s_dictionary[6];
    /// <summary>Gets the offset of the cached bucket multiplier of <c>Dictionary&lt;TKey, TValue&gt;</c>, or <see cref="LuminFieldLayout.Unavailable"/>.</summary>
    public static readonly int DictionaryFastMod = s_dictionary[7];

    // LinkedList<T> and LinkedListNode<T>
    private static readonly int[] s_linkedList = Family(typeof(LinkedList<int>), typeof(LinkedList<object>), Unknown(3), "head", "count", "version");
    private static readonly int[] s_linkedNode = Family(typeof(LinkedListNode<int>), typeof(LinkedListNode<object>), Unknown(3), "list", "next", "prev");
    /// <summary>Gets the offset of the head of <c>LinkedList&lt;T&gt;</c>.</summary>
    public static readonly int LinkedListHead = s_linkedList[0];
    /// <summary>Gets the offset of the count of <c>LinkedList&lt;T&gt;</c>.</summary>
    public static readonly int LinkedListCount = s_linkedList[1];
    /// <summary>Gets the offset of the version of <c>LinkedList&lt;T&gt;</c>.</summary>
    public static readonly int LinkedListVersion = s_linkedList[2];
    /// <summary>Gets the offset of the owning list of <c>LinkedListNode&lt;T&gt;</c>.</summary>
    public static readonly int LinkedNodeList = s_linkedNode[0];
    /// <summary>Gets the offset of the next node of <c>LinkedListNode&lt;T&gt;</c>.</summary>
    public static readonly int LinkedNodeNext = s_linkedNode[1];
    /// <summary>Gets the offset of the previous node of <c>LinkedListNode&lt;T&gt;</c>.</summary>
    public static readonly int LinkedNodePrevious = s_linkedNode[2];

    // SortedList<TKey, TValue>
    private static readonly int[] s_sortedList = Family(typeof(SortedList<int, int>), typeof(SortedList<object, object>), Unknown(3), "keys", "values", "_size");
    /// <summary>Gets the offset of the sorted key array of <c>SortedList&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int SortedListKeys = s_sortedList[0];
    /// <summary>Gets the offset of the value array of <c>SortedList&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int SortedListValues = s_sortedList[1];
    /// <summary>Gets the offset of the size of <c>SortedList&lt;TKey, TValue&gt;</c>.</summary>
    public static readonly int SortedListSize = s_sortedList[2];

#if !NETSTANDARD2_1
    // PriorityQueue<TElement, TPriority>
    private static readonly int[] s_priorityQueue = Family(typeof(PriorityQueue<int, int>), typeof(PriorityQueue<object, object>), Unknown(2), "_nodes", "_size");
    /// <summary>Gets the offset of the heap array of <c>PriorityQueue&lt;TElement, TPriority&gt;</c>.</summary>
    public static readonly int PriorityQueueNodes = s_priorityQueue[0];
    /// <summary>Gets the offset of the size of <c>PriorityQueue&lt;TElement, TPriority&gt;</c>.</summary>
    public static readonly int PriorityQueueSize = s_priorityQueue[1];

#endif
    // BitArray
    private static readonly int[] s_bitArray = Family(typeof(BitArray), typeof(BitArray), CompiledBitArray(), "_array|m_array", "_bitLength|m_length", "_version");
    /// <summary>Gets the offset of the word storage of <see cref="BitArray"/>.</summary>
    public static readonly int BitArrayArray = s_bitArray[0];
    /// <summary>Gets the offset of the bit length of <see cref="BitArray"/>.</summary>
    public static readonly int BitArrayLength = s_bitArray[1];

    // ObjectModel wrappers and ReadOnlyCollectionBuilder<T>
    private static readonly int[] s_collection = Family(typeof(Collection<int>), typeof(Collection<object>), CompiledSingle(), "items");
    /// <summary>Gets the offset of <c>Collection&lt;T&gt;.items</c>, inherited by <see cref="ObservableCollection{T}"/>.</summary>
    public static readonly int CollectionItems = s_collection[0];
    private static readonly int[] s_observable = Family(typeof(ObservableCollection<int>), typeof(ObservableCollection<object>), new[] { CollectionItems }, "items");
    /// <summary>Gets the offset of the inherited item list of <see cref="ObservableCollection{T}"/>.</summary>
    public static readonly int ObservableCollectionItems = s_observable[0];
    private static readonly int[] s_readOnly = Family(typeof(ReadOnlyCollection<int>), typeof(ReadOnlyCollection<object>), CompiledSingle(), "list");
    /// <summary>Gets the offset of <c>ReadOnlyCollection&lt;T&gt;.list</c>.</summary>
    public static readonly int ReadOnlyCollectionList = s_readOnly[0];
    private static readonly int[] s_readOnlyObservable = Family(typeof(ReadOnlyObservableCollection<int>), typeof(ReadOnlyObservableCollection<object>),
        new[] { ReadOnlyCollectionList }, "list");
    /// <summary>Gets the offset of the inherited list of <see cref="ReadOnlyObservableCollection{T}"/>.</summary>
    public static readonly int ReadOnlyObservableCollectionList = s_readOnlyObservable[0];
    private static readonly int[] s_builder = Family(typeof(System.Runtime.CompilerServices.ReadOnlyCollectionBuilder<int>), typeof(System.Runtime.CompilerServices.ReadOnlyCollectionBuilder<object>), CompiledBuilder(), "_items", "_size");
    /// <summary>Gets the offset of the backing array of <c>ReadOnlyCollectionBuilder&lt;T&gt;</c>.</summary>
    public static readonly int BuilderItems = s_builder[0];
    /// <summary>Gets the offset of the size of <c>ReadOnlyCollectionBuilder&lt;T&gt;</c>.</summary>
    public static readonly int BuilderSize = s_builder[1];

    /// <summary>Throws when a family's offsets contradict each other on this runtime.</summary>
    /// <param name="offset">An offset of the family.</param>
    /// <param name="family">The collection family, for the message.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Require(int offset, string family)
    {
        if (offset < 0) ThrowUnsupported(family);
    }

    /// <summary>Performs the throw unsupported step.</summary>
    /// <param name="family">The collection family, for the message.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnsupported(string family)
        => LuminPackExceptionHelper.ThrowPlatformNotSupportedException("The storage layout of " + family + " on this runtime is not recognized.");

    /// <summary>Probes one family on two instantiations and selects the probed or the compiled offsets.</summary>
    /// <param name="first">An instantiation of the family.</param>
    /// <param name="second">An instantiation with other element storage.</param>
    /// <param name="compiled">The offsets of the compiled views, in field order.</param>
    /// <param name="fields">Each field's alternative names separated by '|'; a leading '?' marks a field that may be absent.</param>
    /// <returns>The offsets in field order; an absent optional field is <see cref="LuminFieldLayout.Unavailable"/>.</returns>
    private static int[] Family(Type? first, Type? second, int[] compiled, params string[] fields)
    {
        var probed = new int[fields.Length];
        bool anyProbed = false, consistent = true;
        for (int i = 0; i < fields.Length; i++)
        {
            bool optional = fields[i][0] == '?';
            int found = Unavailable;
            foreach (string name in fields[i].TrimStart('?').Split('|'))
            {
                int a = ObjectField(first, name), b = ObjectField(second, name);
                if (a < 0 && b < 0) continue;
                found = a == b ? a : int.MinValue;
                break;
            }
            if (found == int.MinValue || found < 0 && !optional) consistent = false;
            if (found >= 0) anyProbed = true;
            probed[i] = found < 0 ? Unavailable : found;
        }
        if (anyProbed && consistent) return probed;
        if (!anyProbed)
        {
            // No metadata: the compiled views of the expected layout remain in effect.
            var result = new int[fields.Length];
            for (int i = 0; i < result.Length; i++) result[i] = i < compiled.Length ? compiled[i] : Unavailable;
            return result;
        }
        var unavailable = new int[fields.Length];
        for (int i = 0; i < unavailable.Length; i++) unavailable[i] = Unavailable;
        return unavailable;
    }

    /// <summary>Gets the offsets of a family without a compiled view, which is unavailable when it cannot be probed.</summary>
    /// <param name="count">The number of items.</param>
    /// <returns>The result.</returns>
    private static int[] Unknown(int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = Unavailable;
        return result;
    }

    /// <summary>Performs the compiled list step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledList()
    {
        var view = new ListMirror();
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._items)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._size)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._version)) };
    }

    /// <summary>Performs the compiled stack step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledStack()
    {
        var view = new StackMirror();
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._items)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._size)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._version)) };
    }

    /// <summary>Performs the compiled queue step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledQueue()
    {
        var view = new QueueMirror();
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._items)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._head)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._tail)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._size)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._version)) };
    }

    /// <summary>Performs the compiled hash set step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledHashSet()
    {
        var view = new HashSetMirror();
#if NETSTANDARD2_1
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._buckets)), OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._entries)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._count)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._freeList)), Unavailable,
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._version)), OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._comparer)), Unavailable,
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._lastIndex)) };
#else
        if (Unsafe.SizeOf<IntPtr>() == 4)
        {
            var narrow = new HashSet32Mirror();
            return new[] { OffsetOf(narrow, ref Unsafe.As<object?, byte>(ref narrow._buckets)), OffsetOf(narrow, ref Unsafe.As<object?, byte>(ref narrow._entries)),
                OffsetOf(narrow, ref Unsafe.As<int, byte>(ref narrow._count)), OffsetOf(narrow, ref Unsafe.As<int, byte>(ref narrow._freeList)),
                OffsetOf(narrow, ref Unsafe.As<int, byte>(ref narrow._freeCount)), OffsetOf(narrow, ref Unsafe.As<int, byte>(ref narrow._version)),
                OffsetOf(narrow, ref Unsafe.As<object?, byte>(ref narrow._comparer)), Unavailable, Unavailable };
        }
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._buckets)), OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._entries)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._count)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._freeList)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._freeCount)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._version)),
            OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._comparer)), OffsetOf(view, ref Unsafe.As<object?, byte>(ref view.sync)), Unavailable };
#endif
    }

    /// <summary>Performs the compiled dictionary step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledDictionary()
    {
        var view = new DictionaryMirror();
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._buckets)), OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._entries)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._count)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._freeList)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._freeCount)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._version)),
            OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._comparer)),
#if NETSTANDARD2_1
            Unavailable };
#else
            Unsafe.SizeOf<IntPtr>() == 8 ? OffsetOf(view, ref Unsafe.As<ulong, byte>(ref view._fastModMultiplier)) : Unavailable };
#endif
    }

    /// <summary>Performs the compiled bit array step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledBitArray()
    {
        var view = new BitArrayMirror();
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view.m_array)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view.m_length)),
            OffsetOf(view, ref Unsafe.As<int, byte>(ref view._version)) };
    }

    /// <summary>Performs the compiled single step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledSingle()
    {
        var view = new SingleMirror();
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view.items)) };
    }

    /// <summary>Performs the compiled builder step.</summary>
    /// <returns>The result.</returns>
    private static int[] CompiledBuilder()
    {
        var view = new ListMirror();
        return new[] { OffsetOf(view, ref Unsafe.As<object?, byte>(ref view._items)), OffsetOf(view, ref Unsafe.As<int, byte>(ref view._size)) };
    }

    // The compiled views of the expected layouts, used only to measure fallback offsets.

    [Preserve]
    private sealed class ListMirror
    {
        public object? _items;
        public int _size;
        public int _version;
    }

    [Preserve]
    private sealed class StackMirror
    {
        public object? _items;
#if NETSTANDARD2_1
        private IntPtr _ptr;
#endif
        public int _size;
        public int _version;
    }

    [Preserve]
    private sealed class QueueMirror
    {
        public object? _items;
#if NETSTANDARD2_1
        private IntPtr _ptr;
#endif
        public int _head;
        public int _tail;
        public int _size;
        public int _version;
    }

    [Preserve]
    private sealed class HashSetMirror
    {
        public object? _buckets;
        public object? _entries;
        public int _count;
#if NETSTANDARD2_1
        public int _lastIndex;
        public int _freeList;
        public int _version;
#else
        public int _version;
        public int _freeList;
        public int _freeCount;
#endif
        public object? _comparer;
        // The legacy reference slot shares the space of the cached 64-bit multiplier.
        public object? sync;
    }

#if !NETSTANDARD2_1
    [Preserve]
    private sealed class HashSet32Mirror
    {
        public object? _buckets;
        public object? _entries;
        public int _count;
        public int _freeList;
        public int _freeCount;
        public int _version;
        public object? _comparer;
    }
#endif

    [Preserve]
    private sealed class DictionaryMirror
    {
        public object? _buckets;
        public object? _entries;
#if !NETSTANDARD2_1
        public ulong _fastModMultiplier;
#endif
        public int _count;
        public int _freeList;
        public int _freeCount;
        public int _version;
        public object? _comparer;
        public object? _keys;
        public object? _values;
#if NETSTANDARD2_1
        public object? sync;
#endif
    }

    [Preserve]
    private sealed class BitArrayMirror
    {
        public object? m_array;
        public int m_length;
        public int _version;
    }

    [Preserve]
    private sealed class SingleMirror
    {
        public object? items;
    }
}

/// <summary>Checks the element storage of hash-table entries against the compiled entry views.</summary>
/// <typeparam name="TEntry">The compiled entry view.</typeparam>
internal static class LuminEntryLayout<TEntry> where TEntry : struct
{
    /// <summary>Checks a probed entry type: each named field must be at the compiled view's offset and the stride equal to its size.</summary>
    /// <param name="entry">The runtime's entry type, or null when its metadata is unavailable.</param>
    /// <param name="fields">Each field's alternative names and its offset in the compiled view.</param>
    /// <returns>False only when the probe succeeded and contradicts the compiled view.</returns>
    internal static bool Matches(Type? entry, params (string Names, int Compiled)[] fields)
    {
        if (entry is null) return true;
        int stride = ArrayStride(entry);
        if (stride >= 0 && stride != Unsafe.SizeOf<TEntry>()) return false;
        foreach (var (names, compiled) in fields)
            foreach (string name in names.Split('|'))
            {
                int offset = ValueField(entry, name);
                if (offset < 0) continue;
                if (offset != compiled) return false;
                break;
            }
        return true;
    }
}
