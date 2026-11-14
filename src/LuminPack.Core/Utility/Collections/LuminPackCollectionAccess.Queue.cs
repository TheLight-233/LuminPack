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
        WriteQueueStrings<TOptions>(ref writer, array.AsSpan(head, first), checkEachRecord);
        if (first != count)
            WriteQueueStrings<TOptions>(ref writer, array.AsSpan(0, count - first), checkEachRecord);
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
        WriteIntegerQueueItems<TOptions, T>(ref writer, array.AsSpan(head, first), ref version, captured);
        if (first != count)
            WriteIntegerQueueItems<TOptions, T>(ref writer, array.AsSpan(0, count - first), ref version, captured);
        return true;
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
            int count = writer.WriteOrderedIntegerWindow(items, reverse);
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
