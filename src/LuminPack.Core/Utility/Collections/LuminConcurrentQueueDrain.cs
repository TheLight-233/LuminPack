using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Empties a reused concurrent queue for an ordered refill while keeping its current tail segment.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <param name="queue">The existing queue.</param>
    /// <remarks><c>Clear</c> freezes the tail and publishes a new 32-slot segment, so refilling a large queue allocates the
    /// whole growth chain again. Dequeuing every item leaves the last segment open for enqueues, and a refill that fits
    /// its capacity allocates no segment. On verified runtimes each segment's published items are dequeued together: one
    /// compare-exchange of the segment head claims exactly the slots that consecutive dequeues would claim, and the slots
    /// are then released as each dequeue releases its own. Items still being enqueued and segment transitions go through
    /// the framework dequeue, so concurrent operations keep their framework guarantees.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void DrainConcurrentQueue<T>(ConcurrentQueue<T> queue)
    {
#if NET10_0_OR_GREATER
        if (ConcurrentQueueSegments<T>.Available && queue.GetType() == typeof(ConcurrentQueue<T>))
        {
            do
                ConcurrentQueueSegments<T>.DequeueAll(Volatile.Read(ref LuminFieldLayout.Field<object>(queue, ConcurrentQueueSegments<T>.Head)));
            while (queue.TryDequeue(out _));
            return;
        }
#endif
        while (queue.TryDequeue(out _))
        {
        }
    }

    /// <summary>Empties a reused concurrent queue and enqueues decoded items in order.</summary>
    /// <typeparam name="T">The raw element type.</typeparam>
    /// <param name="queue">The existing queue.</param>
    /// <param name="items">The decoded items.</param>
    /// <param name="count">The number of items to enqueue.</param>
    /// <remarks>The queue is drained as <see cref="DrainConcurrentQueue{T}"/> does. On verified runtimes the free slots of the
    /// tail segment are claimed together by one compare-exchange of its tail, exactly the slots consecutive enqueues would
    /// claim, and each item is published as an enqueue publishes it; a full segment continues with the framework enqueue,
    /// which grows the queue as before.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RefillConcurrentQueue<T>(ConcurrentQueue<T> queue, T[] items, int count) where T : unmanaged
    {
        if ((uint)count > (uint)items.Length)
            LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
        DrainConcurrentQueue(queue);
        int index = 0;
#if NET10_0_OR_GREATER
        if (ConcurrentQueueSegments<T>.Available && queue.GetType() == typeof(ConcurrentQueue<T>))
        {
            while (index < count)
            {
                index += ConcurrentQueueSegments<T>.EnqueueRange(
                    Volatile.Read(ref LuminFieldLayout.Field<object>(queue, ConcurrentQueueSegments<T>.Tail)), items, index, count - index);
                if (index < count)
                    queue.Enqueue(items[index++]);
            }
            return;
        }
#endif
        for (; index < count; index++)
            queue.Enqueue(items[index]);
    }

#if NET10_0_OR_GREATER
    /// <summary>Locates the ring storage of the framework queue segments of one closed element type.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <remarks>Each slot's sequence number is the position that may next use it: a free slot holds its enqueue position, a
    /// published item its position plus one, and a released slot its position plus the segment length. Head and tail are
    /// claimed with compare-exchange and sequence numbers are read and written with acquire and release semantics, as the
    /// framework segment does.</remarks>
    private static class ConcurrentQueueSegments<T>
    {
        internal static readonly int Head, Tail;
        private static readonly int Slots, Mask, Positions, PositionHead, PositionTail, Preserved, Item, Sequence, Stride;

        /// <summary>Whether this runtime's segment layout was located and checked through the public queue.</summary>
        internal static readonly bool Available;

        /// <summary>Locates the segment storage and checks it once through the public queue.</summary>
        static ConcurrentQueueSegments()
        {
            if (Environment.Version is not { Major: 10 or 11 } || Type.GetType("Mono.RuntimeStructs") is not null)
                return;
            try
            {
                var queue = typeof(ConcurrentQueue<T>);
                var segment = LuminFieldLayout.FieldType(queue, "_head");
                var positions = LuminFieldLayout.FieldType(segment, "_headAndTail");
                var slots = LuminFieldLayout.FieldType(segment, "_slots");
                var slot = slots?.GetElementType();
                if (segment is null || segment != LuminFieldLayout.FieldType(queue, "_tail") || queue.Assembly != typeof(object).Assembly ||
                    positions is not { IsValueType: true } || slots is not { IsSZArray: true } || slot is not { IsValueType: true } ||
                    LuminFieldLayout.FieldType(slot, "Item") != typeof(T) || LuminFieldLayout.FieldType(slot, "SequenceNumber") != typeof(int) ||
                    LuminFieldLayout.FieldType(segment, "_slotsMask") != typeof(int) ||
                    LuminFieldLayout.FieldType(segment, "_preservedForObservation") != typeof(bool) ||
                    LuminFieldLayout.FieldType(positions, "Head") != typeof(int) || LuminFieldLayout.FieldType(positions, "Tail") != typeof(int))
                    return;
                Head = LuminFieldLayout.ObjectField(queue, "_head");
                Tail = LuminFieldLayout.ObjectField(queue, "_tail");
                Slots = LuminFieldLayout.ObjectField(segment, "_slots");
                Mask = LuminFieldLayout.ObjectField(segment, "_slotsMask");
                Positions = LuminFieldLayout.ObjectField(segment, "_headAndTail");
                Preserved = LuminFieldLayout.ObjectField(segment, "_preservedForObservation");
                PositionHead = LuminFieldLayout.ValueField(positions, "Head");
                PositionTail = LuminFieldLayout.ValueField(positions, "Tail");
                Item = LuminFieldLayout.ValueField(slot, "Item");
                Sequence = LuminFieldLayout.ValueField(slot, "SequenceNumber");
                Stride = LuminFieldLayout.ArrayStride(slot);
                if ((Head | Tail | Slots | Mask | Positions | Preserved | PositionHead | PositionTail | Item | Sequence | Stride) < 0 ||
                    Head == Tail || PositionHead == PositionTail)
                    return;
                Available = Probe();
            }
            catch
            {
                Available = false;
            }
        }

        /// <summary>Checks both bulk operations against the public queue across wraparound and segment growth.</summary>
        /// <returns>Whether every observation matches the framework queue.</returns>
        private static bool Probe()
        {
            // Reference elements cannot be constructed generically for the check, so they keep the framework operations.
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                return false;
            var items = new T[100];
            for (int i = 0; i < items.Length; i++)
                Bytes(ref items[i]).Fill((byte)(i + 1));
            var queue = new ConcurrentQueue<T>();
            object first = LuminFieldLayout.Field<object>(queue, Head);
            for (int round = 0; round < 3; round++)
            {
                // Twenty items from position 20 wrap the 32-slot segment; the last round grows the queue past it.
                int count = round == 2 ? 100 : 20;
                for (int i = 0; i < 20; i++)
                    queue.Enqueue(items[99 - i]);
                DequeueAll(LuminFieldLayout.Field<object>(queue, Head));
                if (!queue.IsEmpty || queue.Count != 0)
                    return false;
                int index = EnqueueRange(LuminFieldLayout.Field<object>(queue, Tail), items, 0, count);
                if (index != Math.Min(count, 32) || !ReferenceEquals(first, LuminFieldLayout.Field<object>(queue, Tail)))
                    return false;
                while (index < count)
                {
                    queue.Enqueue(items[index++]);
                    index += EnqueueRange(LuminFieldLayout.Field<object>(queue, Tail), items, index, count - index);
                }
                // Enumerating or copying would preserve the segment, so the contents are checked by dequeuing them.
                if (queue.Count != count)
                    return false;
                for (int i = 0; i < count; i++)
                    if (!queue.TryDequeue(out T? item) || !Bytes(ref item!).SequenceEqual(Bytes(ref items[i])))
                        return false;
            }
            return queue.IsEmpty;
        }

        /// <summary>Views the storage of one reference-free element.</summary>
        /// <param name="item">The element.</param>
        /// <returns>Its bytes.</returns>
        private static Span<byte> Bytes(ref T item) => MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref item), Unsafe.SizeOf<T>());

        /// <summary>Gets one ring slot.</summary>
        /// <param name="slots">The segment's slot array.</param>
        /// <param name="index">The masked slot index.</param>
        /// <returns>The first byte of the slot.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref byte Slot(Array slots, int index) =>
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), (nint)(uint)index * Stride);

        /// <summary>Dequeues every published item of one segment as consecutive dequeues would.</summary>
        /// <param name="segment">A segment of a queue of the located type.</param>
        internal static void DequeueAll(object segment)
        {
            var slots = LuminFieldLayout.Field<Array>(segment, Slots);
            int mask = LuminFieldLayout.Field<int>(segment, Mask);
            ref int head = ref LuminFieldLayout.Field<int>(ref LuminFieldLayout.Field<byte>(segment, Positions), PositionHead);
            while (true)
            {
                int current = Volatile.Read(ref head);
                int count = 0;
                // A dequeue at position p claims its slot only once the slot's sequence number is p + 1, the published item.
                while (count <= mask && Volatile.Read(ref LuminFieldLayout.Field<int>(ref Slot(slots, (current + count) & mask), Sequence)) == current + count + 1)
                    count++;
                if (count == 0)
                    return;
                if (Interlocked.CompareExchange(ref head, current + count, current) != current)
                    continue;
                // As in each dequeue, a segment preserved for an enumeration or snapshot keeps its slots.
                if (Volatile.Read(ref LuminFieldLayout.Field<bool>(segment, Preserved)))
                    return;
                int length = mask + 1;
                for (int i = 0; i < count; i++)
                {
                    ref byte slot = ref Slot(slots, (current + i) & mask);
                    if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                        LuminFieldLayout.Field<T>(ref slot, Item) = default!;
                    Volatile.Write(ref LuminFieldLayout.Field<int>(ref slot, Sequence), current + i + length);
                }
                return;
            }
        }

        /// <summary>Enqueues items into the free slots of one segment as consecutive enqueues would.</summary>
        /// <param name="segment">The tail segment of a queue of the located type.</param>
        /// <param name="items">The items.</param>
        /// <param name="start">The first item to enqueue.</param>
        /// <param name="count">The number of items left.</param>
        /// <returns>The number of items enqueued, zero when the segment is full or frozen.</returns>
        internal static int EnqueueRange(object segment, T[] items, int start, int count)
        {
            var slots = LuminFieldLayout.Field<Array>(segment, Slots);
            int mask = LuminFieldLayout.Field<int>(segment, Mask);
            ref int tail = ref LuminFieldLayout.Field<int>(ref LuminFieldLayout.Field<byte>(segment, Positions), PositionTail);
            while (true)
            {
                int current = Volatile.Read(ref tail);
                int claimed = 0;
                // An enqueue at position p claims its slot only once the slot's sequence number is p; a frozen segment's tail
                // is offset past every sequence number, so nothing is claimed.
                while (claimed < count && claimed <= mask &&
                       Volatile.Read(ref LuminFieldLayout.Field<int>(ref Slot(slots, (current + claimed) & mask), Sequence)) == current + claimed)
                    claimed++;
                if (claimed == 0)
                    return 0;
                if (Interlocked.CompareExchange(ref tail, current + claimed, current) != current)
                    continue;
                for (int i = 0; i < claimed; i++)
                {
                    ref byte slot = ref Slot(slots, (current + i) & mask);
                    LuminFieldLayout.Field<T>(ref slot, Item) = items[start + i];
                    Volatile.Write(ref LuminFieldLayout.Field<int>(ref slot, Sequence), current + i + 1);
                }
                return claimed;
            }
        }
    }
#endif
}
