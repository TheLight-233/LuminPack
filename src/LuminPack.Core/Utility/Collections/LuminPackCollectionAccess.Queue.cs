using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Writes a nonempty raw queue as one complete record within the current binary output window.</summary>
    /// <typeparam name="T">The reference-free queue element.</typeparam>
    /// <param name="writer">The writer positioned at the queue's count header.</param>
    /// <param name="queue">The source queue.</param>
    /// <param name="count">The captured element count.</param>
    /// <returns>Whether the entire record was written without growing the buffer.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteQueueRecord<T>(scoped ref LuminPackWriter writer, Queue<T> queue, int count) where T : unmanaged
    {
        const int headerBytes = 4 * sizeof(int);
        int elementBytes = Unsafe.SizeOf<T>();
        long recordBytes = headerBytes + (long)count * elementBytes;
        if (writer.Position < 0 || writer.RecordRoom() < recordBytes || !TryGetQueueArray(queue, count, out var array, out int head) ||
            queue.Count != count)
            return false;

        int firstCount = Math.Min(count, array.Length - head);
        int secondCount = count - firstCount;
        ref T start = ref LuminPackMarshal.GetNotNullArrayReference(array);
        var first = MemoryMarshal.AsBytes(LuminPackMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref start, (nint)(uint)head), firstCount));
        var second = MemoryMarshal.AsBytes(LuminPackMarshal.CreateReadOnlySpan(ref start, secondCount));
        if (first.Overlaps(writer.Buffer) || second.Overlaps(writer.Buffer))
            return false;

        ref byte output = ref writer.CurrentReference;
        LuminPackWriter.Write(ref output, count);
        LuminPackWriter.Write(ref Unsafe.Add(ref output, sizeof(int)), 0);
        LuminPackWriter.Write(ref Unsafe.Add(ref output, 2 * sizeof(int)), 0);
        LuminPackWriter.Write(ref Unsafe.Add(ref output, 3 * sizeof(int)), count);
        LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref output, headerBytes), ref MemoryMarshal.GetReference(first), (uint)first.Length);
        if (!second.IsEmpty)
            LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref output, (nint)(uint)(headerBytes + first.Length)),
                ref MemoryMarshal.GetReference(second), (uint)second.Length);
        writer.CompleteRun((int)recordBytes);
        return true;
    }

    /// <summary>Writes the active strings directly from a recognized ring buffer in dequeue order.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="writer">The writer positioned at the queue's count header.</param>
    /// <param name="queue">The source queue.</param>
    /// <param name="checkEachRecord">Whether the original traversal checked the growth threshold before each string.</param>
    /// <returns>Whether the checked UTF-16 length traversal was available.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteStringQueueStorage<TOptions>(scoped ref LuminPackWriter writer, Queue<string?> queue, bool checkEachRecord)
        where TOptions : struct, ILuminPackOptions
    {
        int count = queue.Count;
        if (!writer.UsesUtf16LengthRecords<TOptions>() || !TryGetQueueArray(queue, count, out var array, out int head))
            return false;
        ref int version = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueVersion);
        int captured = version;
        int header = writer.BeginCollection();
        writer.EnsureAdditionalCapacity(3 * sizeof(int));
        {
            ref byte output = ref MemoryMarshal.GetReference(writer.Buffer.Slice(writer.Position));
            LuminPackWriter.Write(ref output, 0);
            LuminPackWriter.Write(ref Unsafe.Add(ref output, sizeof(int)), 0);
            LuminPackWriter.Write(ref Unsafe.Add(ref output, 2 * sizeof(int)), count);
            writer.CompleteRun(3 * sizeof(int));
        }
        int first = Math.Min(count, array.Length - head);
        ref string? start = ref LuminPackMarshal.GetNotNullArrayReference(array);
        WriteQueueStrings<TOptions>(ref writer, LuminPackMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref start, (nint)(uint)head), first), checkEachRecord);
        if (first != count)
            WriteQueueStrings<TOptions>(ref writer, LuminPackMarshal.CreateReadOnlySpan(ref start, count - first), checkEachRecord);
        if (version != captured)
            LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
        writer.EndCollection(header, count);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Shares the selected string-run operation across the two ring segments.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="writer">The writer positioned after the queue storage words.</param>
    /// <param name="items">The active queue segment.</param>
    /// <param name="checkEachRecord">Whether to preserve the explicit per-record threshold check.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteQueueStrings<TOptions>(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> items, bool checkEachRecord)
        where TOptions : struct, ILuminPackOptions
    {
        if (checkEachRecord)
            writer.WriteCheckedStrings<TOptions>(items);
        else
            writer.WriteStrings<TOptions>(items);
    }

    /// <summary>Writes the active queue segments in dequeue order, checking the original version across output callbacks.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <typeparam name="T">The exact framework integer type.</typeparam>
    /// <param name="writer">The writer after the opening bracket.</param>
    /// <param name="queue">The nonempty queue.</param>
    /// <returns>Whether the complete run was written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteIntegerQueueJson<TOptions, T>(scoped ref LuminPackJsonWriter writer, Queue<T> queue)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        if (default(TOptions).StringEncoding != LuminPackStringEncoding.UTF8 ||
            (typeof(T) != typeof(int) && typeof(T) != typeof(long)))
            return false;
        int count = queue.Count;
        if (!TryGetQueueArray(queue, count, out var array, out int head))
            return false;
        ref int version = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueVersion);
        int captured = version;
        int first = Math.Min(count, array.Length - head);
        ref T start = ref LuminPackMarshal.GetNotNullArrayReference(array);
        WriteIntegerQueueItems<TOptions, T>(ref writer, LuminPackMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref start, (nint)(uint)head), first), ref version, captured);
        if (first != count)
            WriteIntegerQueueItems<TOptions, T>(ref writer, LuminPackMarshal.CreateReadOnlySpan(ref start, count - first), ref version, captured);
        return true;
    }

    /// <summary>Empties an exact framework queue for an in-place refill and returns its first slots seeded with the old items.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <param name="queue">The fresh or reused queue.</param>
    /// <param name="count">The validated incoming element count.</param>
    /// <returns>The first <paramref name="count"/> slots of the backing array, holding the previous items in queue order as
    /// reuse seeds and default values after them; an empty span when the layout is unsupported and the caller must enqueue.</returns>
    /// <remarks>The previous items are moved to the front of the ring, surplus slots are cleared, the array grows to
    /// <paramref name="count"/> when it is smaller, and the queue is published empty with a new version. Until
    /// <see cref="CompleteQueueRefill{T}"/> runs the queue stays empty, so a failed read leaves it empty as <c>Clear</c> does.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Span<T> PrepareQueueRefill<T>(Queue<T> queue, int count)
    {
        if (count <= 0 || !QueueStorage.Available || (LuminCollectionLayouts.QueueTail | LuminCollectionLayouts.QueueSize) < 0 ||
            queue.GetType() != typeof(Queue<T>))
            return default;
        ref int size = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueSize);
        ref int head = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueHead);
        ref int tail = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueTail);
        ref T[] array = ref LuminFieldLayout.Field<T[]>(queue, LuminCollectionLayouts.QueueArray);
        int live = size;
        var items = array;
        if ((uint)live > (uint)items.Length || live != 0 && (uint)head >= (uint)items.Length)
            return default;
        if (live != 0 && head != 0)
        {
            if (head + live <= items.Length)
            {
                Array.Copy(items, head, items, 0, live);
                Array.Clear(items, Math.Max(live, head), head + live - Math.Max(live, head));
            }
            else
            {
                // A wrapped ring: rotate the whole array left by the head in place. The free slots between the tail and the
                // head hold default values, so the live items end up first and in queue order.
                items.AsSpan(0, head).Reverse();
                items.AsSpan(head).Reverse();
                items.AsSpan().Reverse();
            }
        }
        if (items.Length < count)
        {
            var grown = new T[count];
            Array.Copy(items, grown, live);
            items = grown;
        }
        else if (live > count)
            Array.Clear(items, count, live - count);
        array = items;
        head = 0;
        tail = 0;
        size = 0;
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueVersion)++;
        return new Span<T>(items, 0, count);
    }

    /// <summary>Publishes the slots filled after <see cref="PrepareQueueRefill{T}"/> as the queue contents.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <param name="queue">The prepared queue.</param>
    /// <param name="count">The number of filled slots.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void CompleteQueueRefill<T>(Queue<T> queue, int count)
    {
        var items = LuminFieldLayout.Field<T[]>(queue, LuminCollectionLayouts.QueueArray);
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueHead) = 0;
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueTail) = count == items.Length ? 0 : count;
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueSize) = count;
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueVersion)++;
    }

    /// <summary>Retrieves the checked active range of an exact framework queue.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <param name="queue">The source queue.</param>
    /// <param name="count">The captured number of active elements.</param>
    /// <param name="array">The physical ring array.</param>
    /// <param name="head">The first live element's physical index.</param>
    /// <returns>Whether the captured storage and range are usable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetQueueArray<T>(Queue<T> queue, int count, out T[] array, out int head)
    {
        if (!QueueStorage.Available || queue.GetType() != typeof(Queue<T>) || count <= 0)
        {
            array = Array.Empty<T>();
            head = 0;
            return false;
        }
        array = LuminFieldLayout.Field<T[]>(queue, LuminCollectionLayouts.QueueArray);
        head = LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueHead);
        return (uint)count <= (uint)array.Length && (uint)head < (uint)array.Length;
    }

    /// <summary>Shares one integer loop across both physical queue segments.</summary>
    /// <typeparam name="TOptions">The compiled UTF-8 profile.</typeparam>
    /// <typeparam name="T">The exact framework integer type.</typeparam>
    /// <param name="writer">The open JSON array writer.</param>
    /// <param name="items">The current segment.</param>
    /// <param name="version">The collection's live mutation counter.</param>
    /// <param name="captured">Its value when enumeration began.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteIntegerQueueItems<TOptions, T>(scoped ref LuminPackJsonWriter writer, scoped ReadOnlySpan<T> items,
        scoped ref int version, int captured) where TOptions : struct, ILuminPackOptions where T : unmanaged =>
        WriteOrderedIntegerItems<TOptions, T>(ref writer, items, ref version, captured, reverse: false);

    /// <summary>Writes an active array segment across windows in the same order as the collection enumerator.</summary>
    /// <typeparam name="TOptions">The compiled UTF-8 profile.</typeparam>
    /// <typeparam name="T">The exact framework integer type.</typeparam>
    /// <param name="writer">The open JSON array writer.</param>
    /// <param name="items">The remaining values in storage order.</param>
    /// <param name="version">The collection's live mutation counter.</param>
    /// <param name="captured">Its value when enumeration began.</param>
    /// <param name="reverse">Whether enumeration visits the highest slot first.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteOrderedIntegerItems<TOptions, T>(scoped ref LuminPackJsonWriter writer, scoped ReadOnlySpan<T> items,
        scoped ref int version, int captured, bool reverse) where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        while (true)
        {
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            if (items.IsEmpty)
                return;
            int count = writer.WriteOrderedIntegerWindow<TOptions, T>(items, reverse);
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            if (count == items.Length)
                return;
            T item = reverse ? items[items.Length - count - 1] : items[count];
            WriteOrderedIntegerBoundary<TOptions, T>(ref writer, item);
            items = reverse ? items.Slice(0, items.Length - count - 1) : items.Slice(count + 1);
        }
    }

    /// <summary>Writes the item captured before a separator can commit a window and invoke the caller.</summary>
    /// <typeparam name="TOptions">The compiled UTF-8 profile.</typeparam>
    /// <typeparam name="T">The selected scalar.</typeparam>
    /// <param name="writer">The open JSON array writer.</param>
    /// <param name="item">The captured element.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteOrderedIntegerBoundary<TOptions, T>(scoped ref LuminPackJsonWriter writer, T item)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        if (!writer.IsFirstElement)
            writer.WriteByteRaw<TOptions>(LuminPackJsonWriter.Comma);
        writer.SetFirstElement(true);
        if (typeof(T) == typeof(int))
            writer.Write<TOptions>(Unsafe.As<T, int>(ref item));
        else
            writer.Write<TOptions>(Unsafe.As<T, long>(ref item));
    }

    /// <summary>Checks the backing array, head and version against the framework's circular queue operations.</summary>
    private static class QueueStorage
    {
        internal static readonly bool Available = Check();

        /// <summary>Returns the check for <c>QueueStorage</c>.</summary>
        /// <returns>Whether the input satisfies the check check.</returns>
        private static bool Check()
        {
            if ((LuminCollectionLayouts.QueueArray | LuminCollectionLayouts.QueueHead | LuminCollectionLayouts.QueueVersion) < 0)
                return false;
            try
            {
                return Check(19, 37) && Check("first", "second");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Handles check for <c>QueueStorage</c>.</summary>
        /// <typeparam name="T">The value type processed by the operation.</typeparam>
        /// <param name="first">The first supplied to <c>Check</c>.</param>
        /// <param name="second">The second supplied to <c>Check</c>.</param>
        /// <returns>Whether the input satisfies the check check.</returns>
        private static bool Check<T>(T first, T second)
        {
            var queue = new Queue<T>(2);
            ref int version = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueVersion);
            int initial = version;
            queue.Enqueue(first);
            queue.Enqueue(second);
            queue.Dequeue();
            queue.Enqueue(first);
            int head = LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueHead);
            var array = LuminFieldLayout.Field<T[]>(queue, LuminCollectionLayouts.QueueArray);
            var comparer = EqualityComparer<T>.Default;
            return head == 1 && array.Length == 2 && version == unchecked(initial + 4) &&
                comparer.Equals(array[0], first) && comparer.Equals(array[1], second);
        }
    }
}
