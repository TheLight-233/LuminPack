using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif
using System.Text;
using LuminPack.Attribute;

namespace LuminPack.Code;

/// <summary>Provides lumin pack marshal functionality.</summary>
public static class LuminPackMarshal
{

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
        ref ListView<T?> local = ref As<List<T?>, ListView<T?>>(ref list);
        return local._items.AsSpan(0, local._size);
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

        if (list.Capacity < length)
        {
            list.Capacity = length;
        }


#if NET8_0_OR_GREATER
        CollectionsMarshal.SetCount(list, length);
        return CollectionsMarshal.AsSpan(list).Slice(0, length);
#else
        SetListSize(list, length);
        ref ListView<T?> local = ref As<List<T?>, ListView<T?>>(ref list);
        return local._items.AsSpan(0, length);
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
        ref ListView<T> local = ref As<List<T>, ListView<T>>(ref list);
        local._size = length;
        return local._items;
    }

    /// <summary>Updates the list size used by the current operation.</summary>
    /// <param name="list">The list used by this operation.</param>
    /// <param name="size">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetListSize<T>(List<T?>? list, int size)
    {
        if (list is null) return;

        if (list.Capacity < size)
            list.Capacity = size;

#if NET8_0_OR_GREATER
        CollectionsMarshal.SetCount(list, size);
        return;
#else
        ref ListView<T?> local = ref As<List<T?>, ListView<T?>>(ref list);
        if (local._size == size)
            return;

        if (size < local._size && RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
            Array.Clear(local._items, size, local._size - size);
        local._size = size;
        local._version++;
#endif
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
        ref var view = ref As<Stack<T?>, StackView<T?>>(ref stack);
        return view._items.AsSpan(0, stack.Count);
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
        ref var view = ref As<Stack<T?>, StackView<T?>>(ref stack);
        return view._items.AsSpan(0, length);
    }

    /// <summary>Updates the stack size used by the current operation.</summary>
    /// <param name="stack">The stack used by this operation.</param>
    /// <param name="size">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetStackSize<T>(Stack<T?>? stack, int size)
    {
        if (stack is null) return;

        ref var view = ref As<Stack<T?>, StackView<T?>>(ref stack);
        if (view._items.Length < size)
            view._items = new T?[size];
        view._size = size;
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

        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);
        return local._items.AsSpan();
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

        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);
        if (local._items.Length < size)
            local._items = new T?[size];
        return local._items.AsSpan(0, size);
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

        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);

        head = local._head;
        tail = local._tail;
        size = local._size;

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

        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);

        if (local._items.Length < size)
        {
            local._items = new T[size];
        }


        local._head = head;
        local._tail = tail;
        local._size = size;

    }

    /// <summary>Obtains the dictionary view for the supplied input.</summary>
    /// <param name="dict">The dict used by this operation.</param>
    /// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
    /// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DictionaryView<TKey, TValue?> GetDictionaryView<TKey, TValue>(Dictionary<TKey, TValue?>? dict) where TKey : notnull
    {
        return Unsafe.As<Dictionary<TKey, TValue?>, DictionaryView<TKey, TValue?>>(ref dict!);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSetView<T?> GetHashSetView<T>(HashSet<T?>? set)
    {
        return Unsafe.As<HashSet<T?>, HashSetView<T?>>(ref set!);
    }

    /// <summary>Obtains the non null pinnable reference for the supplied input.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T GetNonNullPinnableReference<T>(Span<T> span)
    {
        return ref span.Length == 0
            ? ref Unsafe.AsRef<T>((void*)new IntPtr(1))
            : ref Unsafe.AsRef(in span.GetPinnableReference());
    }

    /// <summary>Obtains the non null pinnable reference for the supplied input.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T GetNonNullPinnableReference<T>(ReadOnlySpan<T> span)
    {
        return ref span.Length == 0
            ? ref Unsafe.AsRef<T>((void*)new IntPtr(1))
            : ref Unsafe.AsRef(in span.GetPinnableReference());
    }

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
    public static unsafe byte[] StructureToByteArray<T>(ref T value) where T : unmanaged
    {
        byte[] result = new byte[sizeof(T)];
        fixed (byte* dest = result)
        {
            Unsafe.Write(dest, value);
        }
        return result;
    }

    /// <summary>
    /// Compares memory blocks through pointers.
    /// </summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static unsafe bool MemoryCompare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;
        if (a.Length == 0) return true;

        fixed (byte* aPtr = a, bPtr = b)
        {
            long* longA = (long*)aPtr;
            long* longB = (long*)bPtr;
            int longCount = a.Length / sizeof(long);

            // Compare long-sized blocks first.
            for (int i = 0; i < longCount; i++)
            {
                if (longA[i] != longB[i]) return false;
            }

            // Process the remaining bytes.
            byte* remA = (byte*)&longA[longCount];
            byte* remB = (byte*)&longB[longCount];
            for (int i = 0; i < a.Length % sizeof(long); i++)
            {
                if (remA[i] != remB[i]) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Copies unmanaged memory.
    /// </summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="source">The input data to process.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void RawCopy<T>(void* destination, void* source, int count) where T : unmanaged
    {
        Buffer.MemoryCopy(source, destination, count * sizeof(T), count * sizeof(T));
    }

    /// <summary>
    /// Obtains a reference to the underlying array storage.
    /// </summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T* GetArrayPointer<T>(T[] array) where T : unmanaged
    {
        fixed (T* ptr = array)
        {
            return ptr;
        }
    }

    /// <summary>
    /// Create a span from a pointer after validating the length.
    /// </summary>
    /// <param name="ptr">The ptr used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<T> CreateSpan<T>(T* ptr, int length) where T : unmanaged
    {
        if (ptr == null && length != 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentException("Null pointer with non-zero length");

        return new Span<T>(ptr, length);
    }

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

    /// <summary>
    /// Initializes unmanaged memory.
    /// </summary>
    /// <param name="ptr">The ptr used by this operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void InitMemory<T>(T* ptr, T value, int count) where T : unmanaged
    {
        for (int i = 0; i < count; i++)
        {
            ptr[i] = value;
        }
    }

    /// <summary>
    /// Convert the memory address with validation.
    /// </summary>
    /// <param name="ptr">The ptr used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T AsRef<T>(IntPtr ptr) where T : unmanaged
    {
        if (ptr == IntPtr.Zero)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(ptr));

        return ref Unsafe.AsRef<T>(ptr.ToPointer());
    }

    /// <summary>
    /// Processes memory in blocks with optional SIMD acceleration.
    /// </summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="byteLength">The byte length used by this operation.</param>
    /// <param name="blockSize">The block size used by this operation.</param>
    /// <param name="blockProcessor">The block processor used by this operation.</param>
    public static unsafe void ProcessMemoryBlocks(
        void* source,
        void* destination,
        int byteLength,
        int blockSize,
        Action<IntPtr, IntPtr, int> blockProcessor)
    {
        if (byteLength < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(byteLength));
        if (blockSize <= 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(blockSize));
        if (blockProcessor is null)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(blockProcessor));

        byte* src = (byte*)source;
        byte* dest = (byte*)destination;
        int remaining = byteLength;

        while (remaining >= blockSize)
        {
            blockProcessor((IntPtr)src, (IntPtr)dest, blockSize);
            src += blockSize;
            dest += blockSize;
            remaining -= blockSize;
        }

        if (remaining > 0)
        {
            blockProcessor((IntPtr)src, (IntPtr)dest, remaining);
        }
    }

    /// <summary>
    /// Swaps memory without a temporary buffer.
    /// </summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <param name="byteLength">The byte length used by this operation.</param>
    public static unsafe void SwapMemory(void* a, void* b, int byteLength)
    {
        byte* pa = (byte*)a;
        byte* pb = (byte*)b;
        long* la = (long*)a;
        long* lb = (long*)b;
        int longCount = byteLength / sizeof(long);
        int rem = byteLength % sizeof(long);

        // Compare long-sized blocks for improved throughput.
        for (int i = 0; i < longCount; i++)
        {
            (la[i], lb[i]) = (lb[i], la[i]);
        }

        // Process the remaining bytes.
        if (rem > 0)
        {
            pa += longCount * sizeof(long);
            pb += longCount * sizeof(long);
            for (int i = 0; i < rem; i++)
            {
                (pa[i], pb[i]) = (pb[i], pa[i]);
            }
        }
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
    public static unsafe ref T GetNotNullArrayReference<T>(T[] array)
    {
#if NET8_0_OR_GREATER
        return ref Unsafe.As<byte, T>(ref Unsafe.AddByteOffset(ref Unsafe.As<RawData>(array).Data, (nuint)(Unsafe.AsRef<MethodTable>(GetMethodTable((object)array).ToPointer()).BaseSize - 2 * sizeof(IntPtr))));
#else
        return ref DangerousGetArrayDataReference(array!);
#endif
    }

    /// <summary>Obtains the not null array reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref byte GetNotNullArrayReference<T>(Array array)
    {
#if NET8_0_OR_GREATER
        return ref Unsafe.AddByteOffset(ref Unsafe.As<RawData>(array).Data, (nuint)(Unsafe.AsRef<MethodTable>(GetMethodTable((object)array).ToPointer()).BaseSize - 2 * sizeof(IntPtr)));
#else
        return ref DangerousGetArrayDataReference<T>(array!);
#endif
    }

    /// <summary>Obtains the array reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T GetArrayReference<T>(T[]? array)
    {
        if (array is null)
            return ref Unsafe.NullRef<T>();

#if NET8_0_OR_GREATER
        return ref Unsafe.As<byte, T>(ref Unsafe.AddByteOffset(ref Unsafe.As<RawData>(array).Data, (nuint)(Unsafe.AsRef<MethodTable>(GetMethodTable((object)array).ToPointer()).BaseSize - 2 * sizeof(IntPtr))));
#else
        return ref DangerousGetArrayDataReference(array!);
#endif

    }

    /// <summary>Obtains the array reference for the supplied input.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref byte GetArrayReference<T>(Array? array)
    {
        if (array is null)
            return ref Unsafe.NullRef<byte>();

#if NET8_0_OR_GREATER
        return ref Unsafe.AddByteOffset(ref Unsafe.As<RawData>(array).Data, (nuint)(Unsafe.AsRef<MethodTable>(GetMethodTable((object)array).ToPointer()).BaseSize - 2 * sizeof(IntPtr)));
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
    public static unsafe ref byte GetArrayDataReference<T>(Array array)
    {
        ref var elementRef = ref GetArrayReference<T>(array);
        return ref Unsafe.AsRef<byte>(Unsafe.AsPointer(ref elementRef));
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
        ref var src = ref Unsafe.As<T, byte>(ref Unsafe.As<byte, T>(ref Unsafe.As<LuminRawArrayData>(array).Data));
        return ref Unsafe.As<byte, T>(ref Unsafe.Add(ref src, Unsafe.SizeOf<UIntPtr>()));
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
        return ref Unsafe.Add(ref src, Unsafe.SizeOf<UIntPtr>());
#else
        return ref GetArrayReference<T>(array);
#endif
    }


    /// <summary>Calculates the serialized size contribution of string byte count.</summary>
    /// <param name="s">The s used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int CalculateStringByteCount(string s)
    {
        if (s.Length > 5)
            return Encoding.UTF8.GetByteCount(s);

        int total = 0;
        fixed (char* ptr = s)
        {
            char* p = ptr;
            int remaining = s.Length;
            while (remaining > 0)
            {
                char c = *p++;
                remaining--;

                if (c <= 0x7F) total += 1;
                else if (c <= 0x7FF) total += 2;
                else if (char.IsHighSurrogate(c))
                {
                    if (remaining > 0 && char.IsLowSurrogate(*p))
                    {
                        p++;
                        remaining--;
                        total += 4;
                    }
                    else total += 3; // Handle invalid surrogate pairs.
                }
                else total += 3;
            }
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
    public static unsafe int GetBytes(ReadOnlySpan<char> source, Span<byte> destination)
    {
        fixed (char* srcPtr = &source.GetPinnableReference())
        fixed (byte* destPtr = &destination.GetPinnableReference())
        {
            return EncodeCore(srcPtr, source.Length, destPtr, destination.Length);
        }
    }

    /// <summary>Performs the encode core step for the current operation.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcLength">The src length used by this operation.</param>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="destMaxLength">The dest max length used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int EncodeCore(char* src, int srcLength, byte* dest, int destMaxLength)
    {
        byte* destStart = dest;
        char* srcEnd = src + srcLength;
        char* srcCurrent = src;

        // Process the ASCII prefix through the fast path.
        ProcessAsciiPrefix(ref srcCurrent, srcEnd, ref dest, destMaxLength);

        // Process the remaining characters.
        while (srcCurrent < srcEnd)
        {
            if (dest >= destStart + destMaxLength) return -1; // Insufficient destination capacity.

            uint c = *srcCurrent++;

            // ASCII characters in 0x00-0x7F.
            if (c <= 0x7F)
            {
                *dest++ = (byte)c;
                continue;
            }

            // Two-byte encoding for 0x80-0x7FF.
            if (c <= 0x7FF)
            {
                if (dest + 1 >= destStart + destMaxLength) return -1;
                *dest++ = (byte)(0xC0 | (c >> 6));
                *dest++ = (byte)(0x80 | (c & 0x3F));
                continue;
            }

            // Process surrogate pairs in the range 0x10000-0x10FFFF.
            if (char.IsHighSurrogate((char)c))
            {
                if (srcCurrent >= srcEnd || dest + 3 >= destStart + destMaxLength)
                    return WriteReplacementChar(ref dest, destStart, destMaxLength);

                uint lowSurrogate = *srcCurrent++;
                if (!char.IsLowSurrogate((char)lowSurrogate))
                    return WriteReplacementChar(ref dest, destStart, destMaxLength);

                uint codePoint = ((c - 0xD800U) << 10) + (lowSurrogate - 0xDC00U) + 0x10000U;
                *dest++ = (byte)(0xF0 | (codePoint >> 18));
                *dest++ = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                *dest++ = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                *dest++ = (byte)(0x80 | (codePoint & 0x3F));
                continue;
            }

            // Three-byte encoding for 0x800-0xFFFF.
            if (dest + 2 >= destStart + destMaxLength) return -1;
            *dest++ = (byte)(0xE0 | (c >> 12));
            *dest++ = (byte)(0x80 | ((c >> 6) & 0x3F));
            *dest++ = (byte)(0x80 | (c & 0x3F));
        }

        return (int)(dest - destStart);
    }

    /// <summary>Records ascii prefix in the current field metadata.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcEnd">The src end used by this operation.</param>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="destMaxLength">The dest max length used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessAsciiPrefix(ref char* src, char* srcEnd, ref byte* dest, int destMaxLength)
    {
        byte* destEnd = dest + destMaxLength;

        // Process contiguous ASCII in blocks.
        while (src < srcEnd && dest < destEnd)
        {
            if (*src > 0x7F) break;
            *dest++ = (byte)*src++;
        }
    }

    /// <summary>Writes replacement char to the current output.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="destStart">The dest start used by this operation.</param>
    /// <param name="destMaxLength">The dest max length used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int WriteReplacementChar(ref byte* dest, byte* destStart, int destMaxLength)
    {
        if (dest + 2 >= destStart + destMaxLength) return -1;
        *dest++ = 0xEF; // U+FFFD replacement character.
        *dest++ = 0xBF;
        *dest++ = 0xBD;
        return (int)(dest - destStart);
    }

    private const int MediumDataThreshold = 16 * 1024; // 16 KB
    private const int LargeDataThreshold = 64 * 1024; // 64 KB

#if NET8_0_OR_GREATER
    private static readonly bool UseAvx2 = System.Runtime.Intrinsics.X86.Avx2.IsSupported;
    private static readonly bool UseSse2 = System.Runtime.Intrinsics.X86.Sse2.IsSupported;
    private static readonly bool UseAdvSimd = System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported;

    #region NET8_0_OR_GREATER SIMD

    /// <summary>Performs the avx2 copy block step for the current operation.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="byteCount">The number of bytes or elements to process.</param>
    private static unsafe void Avx2CopyBlock(void* dest, void* src, uint byteCount)
    {
        byte* d = (byte*)dest;
        byte* s = (byte*)src;
        uint head = (uint)((nuint)d & 31);
        if (head != 0)
        {
            uint fix = Math.Min(32 - head, byteCount);
            Unsafe.CopyBlockUnaligned(d, s, fix);
            d += fix; s += fix; byteCount -= fix;
        }

        uint blocks = byteCount / 256;
        for (uint i = 0; i < blocks; i++)
        {
            var v0 = Avx.LoadVector256(s);
            var v1 = Avx.LoadVector256(s + 32);
            var v2 = Avx.LoadVector256(s + 64);
            var v3 = Avx.LoadVector256(s + 96);
            var v4 = Avx.LoadVector256(s + 128);
            var v5 = Avx.LoadVector256(s + 160);
            var v6 = Avx.LoadVector256(s + 192);
            var v7 = Avx.LoadVector256(s + 224);


            Avx.StoreAligned(d, v0);
            Avx.StoreAligned(d + 32, v1);
            Avx.StoreAligned(d + 64, v2);
            Avx.StoreAligned(d + 96, v3);
            Avx.StoreAligned(d + 128, v4);
            Avx.StoreAligned(d + 160, v5);
            Avx.StoreAligned(d + 192, v6);
            Avx.StoreAligned(d + 224, v7);

            // Prefetch the next cache line.
            if (i + 1 < blocks)
                Sse.Prefetch0(s + 256);

            s += 256; d += 256;

        }

        uint tail = byteCount % 256;
        if (tail != 0) Unsafe.CopyBlockUnaligned(d, s, tail);
    }

    /// <summary>Performs the sse2 copy block step for the current operation.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="byteCount">The number of bytes or elements to process.</param>
    private static unsafe void Sse2CopyBlock(void* dest, void* src, uint byteCount)
    {
        byte* d = (byte*)dest;
        byte* s = (byte*)src;

        uint head = (uint)((nuint)d & 15);
        if (head != 0)
        {
            uint fix = Math.Min(16 - head, byteCount);
            Unsafe.CopyBlockUnaligned(d, s, fix);
            d += fix; s += fix; byteCount -= fix;
        }

        uint blocks = byteCount / 256;
        for (uint i = 0; i < blocks; i++)
        {
            var v0 = Sse2.LoadVector128(s);
            var v1 = Sse2.LoadVector128(s + 16);
            var v2 = Sse2.LoadVector128(s + 32);
            var v3 = Sse2.LoadVector128(s + 48);
            var v4 = Sse2.LoadVector128(s + 64);
            var v5 = Sse2.LoadVector128(s + 80);
            var v6 = Sse2.LoadVector128(s + 96);
            var v7 = Sse2.LoadVector128(s + 112);
            var v8 = Sse2.LoadVector128(s + 128);
            var v9 = Sse2.LoadVector128(s + 144);
            var v10 = Sse2.LoadVector128(s + 160);
            var v11 = Sse2.LoadVector128(s + 176);
            var v12 = Sse2.LoadVector128(s + 192);
            var v13 = Sse2.LoadVector128(s + 208);
            var v14 = Sse2.LoadVector128(s + 224);
            var v15 = Sse2.LoadVector128(s + 240);

            Sse2.StoreAligned(d, v0);
            Sse2.StoreAligned(d + 16, v1);
            Sse2.StoreAligned(d + 32, v2);
            Sse2.StoreAligned(d + 48, v3);
            Sse2.StoreAligned(d + 64, v4);
            Sse2.StoreAligned(d + 80, v5);
            Sse2.StoreAligned(d + 96, v6);
            Sse2.StoreAligned(d + 112, v7);
            Sse2.StoreAligned(d + 128, v8);
            Sse2.StoreAligned(d + 144, v9);
            Sse2.StoreAligned(d + 160, v10);
            Sse2.StoreAligned(d + 176, v11);
            Sse2.StoreAligned(d + 192, v12);
            Sse2.StoreAligned(d + 208, v13);
            Sse2.StoreAligned(d + 224, v14);
            Sse2.StoreAligned(d + 240, v15);

            // Prefetch the next cache line.
            if (i + 1 < blocks)
                Sse.Prefetch0(s + 256);

            s += 256; d += 256;
        }

        uint tail = byteCount % 256;
        if (tail != 0) Unsafe.CopyBlockUnaligned(d, s, tail);
    }

    /// <summary>Performs the adv copy block step for the current operation.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="byteCount">The number of bytes or elements to process.</param>
    private static unsafe void AdvCopyBlock(void* dest, void* src, uint byteCount)
    {
        byte* d = (byte*)dest;
        byte* s = (byte*)src;

        uint head = (uint)((nuint)d & 15);
        if (head != 0)
        {
            uint fix = Math.Min(16 - head, byteCount);
            Unsafe.CopyBlockUnaligned(d, s, fix);
            d += fix; s += fix; byteCount -= fix;
        }

        uint blocks = byteCount / 256;
        for (uint i = 0; i < blocks; i++)
        {
            var v0 = AdvSimd.LoadVector128(s);
            var v1 = AdvSimd.LoadVector128(s + 16);
            var v2 = AdvSimd.LoadVector128(s + 32);
            var v3 = AdvSimd.LoadVector128(s + 48);
            var v4 = AdvSimd.LoadVector128(s + 64);
            var v5 = AdvSimd.LoadVector128(s + 80);
            var v6 = AdvSimd.LoadVector128(s + 96);
            var v7 = AdvSimd.LoadVector128(s + 112);
            var v8 = AdvSimd.LoadVector128(s + 128);
            var v9 = AdvSimd.LoadVector128(s + 144);
            var v10 = AdvSimd.LoadVector128(s + 160);
            var v11 = AdvSimd.LoadVector128(s + 176);
            var v12 = AdvSimd.LoadVector128(s + 192);
            var v13 = AdvSimd.LoadVector128(s + 208);
            var v14 = AdvSimd.LoadVector128(s + 224);
            var v15 = AdvSimd.LoadVector128(s + 240);

            AdvSimd.Store(d, v0);
            AdvSimd.Store(d + 16, v1);
            AdvSimd.Store(d + 32, v2);
            AdvSimd.Store(d + 48, v3);
            AdvSimd.Store(d + 64, v4);
            AdvSimd.Store(d + 80, v5);
            AdvSimd.Store(d + 96, v6);
            AdvSimd.Store(d + 112, v7);
            AdvSimd.Store(d + 128, v8);
            AdvSimd.Store(d + 144, v9);
            AdvSimd.Store(d + 160, v10);
            AdvSimd.Store(d + 176, v11);
            AdvSimd.Store(d + 192, v12);
            AdvSimd.Store(d + 208, v13);
            AdvSimd.Store(d + 224, v14);
            AdvSimd.Store(d + 240, v15);

            s += 256; d += 256;
        }

        uint tail = byteCount % 256;
        if (tail != 0) Unsafe.CopyBlockUnaligned(d, s, tail);
    }

    #endregion

#endif

    /// <summary>Copies block unaligned to the supplied destination.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="byteCount">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void CopyBlockUnaligned(ref byte destination, ref byte src, uint byteCount)
    {
        CopyBlockUnaligned(Unsafe.AsPointer(ref destination), Unsafe.AsPointer(ref src), byteCount);
    }


    /// <summary>Copies block unaligned to the supplied destination.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="source">The input data to process.</param>
    /// <param name="byteCount">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void CopyBlockUnaligned(
        void* destination,
        void* source,
        uint byteCount)
    {

#if NET8_0_OR_GREATER
        if (byteCount < MediumDataThreshold)
        {
            Unsafe.CopyBlockUnaligned(destination, source, byteCount);
            return;
        }
        else
        {

            switch (UseAvx2, UseSse2, UseAdvSimd)
            {
                case (true, _, _):
                    Avx2CopyBlock(destination, source, byteCount);
                    return;
                case (_, true, _):
                    Sse2CopyBlock(destination, source, byteCount);
                    return;
                case (_, _, true):
                    AdvCopyBlock(destination, source, byteCount);
                    return;
                default:
                    Unsafe.CopyBlockUnaligned(destination, source, byteCount);
                    return;
            }
        }
#else

        Unsafe.CopyBlockUnaligned(destination, source, byteCount);
        return;
#endif

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

    /// <summary>Provides list view functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [Preserve]
    internal sealed class ListView<T>
    {
        /// <summary>Stores the items.</summary>
        public T[]? _items;
        /// <summary>Stores the size.</summary>
        public int _size;
        /// <summary>Stores the version.</summary>
        public int _version;
    }

    /// <summary>Provides stack view functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [Preserve]
    internal sealed class StackView<T>
    {
        /// <summary>Stores the items.</summary>
        public T[]? _items;
#if NETSTANDARD2_1
        private IntPtr _ptr;
#endif
        /// <summary>Stores the size.</summary>
        public int _size;
        /// <summary>Stores the version.</summary>
        public int _version;
    }

    /// <summary>Provides queue view functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [Preserve]
    internal sealed class QueueView<T>
    {
        /// <summary>Stores the items.</summary>
        public T[]? _items;
#if NETSTANDARD2_1
        private IntPtr _ptr;
#endif
        /// <summary>Stores the head.</summary>
        public int _head;
        /// <summary>Stores the tail.</summary>
        public int _tail;
        /// <summary>Stores the size.</summary>
        public int _size;
        /// <summary>Stores the version.</summary>
        public int _version;

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
        // IntPtr.Size is a platform constant for JIT and AOT compilation,
        // The JIT normally folds this condition outside the loop.
        if (IntPtr.Size == 8)
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
        if (IntPtr.Size != 8)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowPlatformNotSupportedException(
                "Direct Dictionary bucket rebuilding requires the modern 64-bit Dictionary layout.");
#endif

        ref var entriesRef = ref GetArrayReference(view._entries);
        ref var bucketsRef = ref GetArrayReference(view._buckets);

        uint bucketCount = (uint)view._buckets.Length;

        // Use FastMod on 64-bit platforms.
        // Perform one integer division outside the loop.
#if NETSTANDARD2_1
        ulong fastModMultiplier = IntPtr.Size == 8
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
        if (IntPtr.Size != 8)
            return false;
#endif

        ref var entriesRef = ref GetArrayReference(view._entries);
        ref var bucketsRef = ref GetArrayReference(view._buckets);
        uint bucketCount = (uint)view._buckets.Length;
#if NETSTANDARD2_1
        ulong fastModMultiplier = IntPtr.Size == 8
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
            var comparer = EqualityComparer<TKey>.Default;
            for (int i = 0; i < count; i++)
            {
                ref var entry = ref Unsafe.Add(ref entriesRef, (nint)(uint)i);
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
                    if (candidate.HashCode == hashCode && comparer.Equals(candidate.Key, entry.Key))
                        return false;

                    previous = candidate.Next;
                    if (++collisionCount > 100)
                        return false;
                }

                entry.HashCode = hashCode;
                entry.Next = bucket - 1;
                bucket = i + 1;
            }
        }
        else
        {
            var comparer = view._comparer ?? EqualityComparer<TKey>.Default;
            for (int i = 0; i < count; i++)
            {
                ref var entry = ref Unsafe.Add(ref entriesRef, (nint)(uint)i);
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
            }
        }

        view._count = count;
        view._version = count;
        return true;
    }

    /// <summary>
    /// Rebuilds HashSet buckets and count after deserialization writes directly to entries.
    /// Bypasses HashSet.Add hashing, duplicate-value checks, and capacity growth.
    /// Only valid after input validation has established that no duplicate values exist.
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
            IntPtr.Size == 8
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
                unchecked(
                    (uint)(
                        comparer.GetHashCode(entry.Value!) &
                        0x7FFFFFFF));
#else
            uint hashCode =
                unchecked(
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

    /// <summary>Provides dictionary view functionality.</summary>
    /// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
    /// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
    [Preserve]
    public sealed class DictionaryView<TKey, TValue>
    {
        /// <summary>Stores the buckets.</summary>
        public int[] _buckets;
        /// <summary>Stores the entries.</summary>
        public Entry[] _entries;
#if !NETSTANDARD2_1
        // Modern Dictionary stores its cached FastMod multiplier before the
        // integer state. Keep this declaration before _count so automatic
        // layout maps it to the same 64-bit slot.
        /// <summary>Stores the fast mod multiplier.</summary>
        public ulong _fastModMultiplier;
#endif
        /// <summary>Stores the count.</summary>
        public int _count;
        /// <summary>Stores the free list.</summary>
        public int _freeList;
        /// <summary>Stores the free count.</summary>
        public int _freeCount;
        /// <summary>Stores the version.</summary>
        public int _version;
        /// <summary>Stores the comparer.</summary>
        public IEqualityComparer<TKey> _comparer;
        /// <summary>Stores the keys.</summary>
        public Dictionary<TKey, TValue>.KeyCollection _keys;
        /// <summary>Stores the values.</summary>
        public Dictionary<TKey, TValue>.ValueCollection _values;
#if NETSTANDARD2_1
        // Retain the Mono/.NET Standard reference slot and the old multiplier
        // calculation path; Unity validation is intentionally deferred.
        private object sync;
#endif

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

    /// <summary>Provides hash set view functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [Preserve]
    public sealed class HashSetView<T>
    {
        /// <summary>Stores the buckets.</summary>
        public int[] _buckets;
        /// <summary>Stores the entries.</summary>
        public Entry[] _entries;
        /// <summary>Stores the count.</summary>
        public int _count;
#if NETSTANDARD2_1
        /// <summary>Stores the last index.</summary>
        public int _lastIndex;
        /// <summary>Stores the free list.</summary>
        public int _freeList;
        /// <summary>Stores the version.</summary>
        public int _version;
#else
        /// <summary>Stores the version.</summary>
        public int _version;
        /// <summary>Stores the free list.</summary>
        public int _freeList;
        /// <summary>Stores the free count.</summary>
        public int _freeCount;
#endif
        /// <summary>Stores the comparer.</summary>
        public IEqualityComparer<T> _comparer;
        private object sync;

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
}
