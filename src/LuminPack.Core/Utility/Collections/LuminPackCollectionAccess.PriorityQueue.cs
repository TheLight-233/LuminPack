#if !NETSTANDARD2_1
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Writes fixed heap pairs in their public enumeration order into one proven binary window.</summary>
    /// <typeparam name="TElement">The reference-free element.</typeparam>
    /// <typeparam name="TPriority">The reference-free priority.</typeparam>
    /// <param name="writer">The writer at the collection header.</param>
    /// <param name="queue">The source queue.</param>
    /// <returns>Whether the complete collection was written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWritePriorityQueueStorage<TElement, TPriority>(scoped ref LuminPackWriter writer, PriorityQueue<TElement, TPriority> queue)
        where TElement : unmanaged where TPriority : unmanaged
    {
        int elementBytes = Unsafe.SizeOf<TElement>();
        int pairBytes = elementBytes + Unsafe.SizeOf<TPriority>();
        if (queue.GetType() != typeof(PriorityQueue<TElement, TPriority>) ||
            writer.RecordRoom() < sizeof(int) + (long)queue.Count * pairBytes ||
            !TryGetPriorityQueueTraversal(queue, out var nodes, out int version))
            return false;
        long payloadBytes = (long)nodes.Length * pairBytes;
        if (writer.RecordRoom() < sizeof(int) + payloadBytes || MemoryMarshal.AsBytes(nodes).Overlaps(writer.Buffer))
            return false;
        int header = writer.BeginCollection();
        if (!nodes.IsEmpty)
        {
            ref byte output = ref writer.Cursor;
            ref var first = ref MemoryMarshal.GetReference(nodes);
            int index = 0;
            do
            {
                ref readonly var node = ref Unsafe.Add(ref first, (nint)(uint)index);
                LuminPackWriter.Write(ref output, in node.Element);
                LuminPackWriter.Write(ref Unsafe.Add(ref output, (nint)(uint)elementBytes), in node.Priority);
                output = ref Unsafe.Add(ref output, (nint)(uint)pairBytes);
            } while (++index < nodes.Length);
        }
        writer.CompleteRun((int)payloadBytes);
        CheckPriorityQueueTraversalVersion(queue, version);
        writer.EndCollection(header, nodes.Length);
        return true;
    }

    /// <summary>Fills a default-comparer heap directly while each pair already belongs in the next heap slot.</summary>
    /// <typeparam name="TElement">The reference-free element.</typeparam>
    /// <typeparam name="TPriority">The callback-free priority.</typeparam>
    /// <param name="reader">The reader after its validated count header.</param>
    /// <param name="queue">The empty destination.</param>
    /// <param name="count">The checked pair count.</param>
    /// <returns>Whether the complete input was consumed.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryReadPriorityQueueStorage<TElement, TPriority>(scoped ref LuminPackReader reader,
        PriorityQueue<TElement, TPriority> queue, int count) where TElement : unmanaged where TPriority : unmanaged
    {
        if (typeof(TElement) == typeof(bool) ||
            (typeof(TPriority) != typeof(int) && typeof(TPriority) != typeof(long) &&
             typeof(TPriority) != typeof(uint) && typeof(TPriority) != typeof(ulong)) ||
            count <= 0 || queue.GetType() != typeof(PriorityQueue<TElement, TPriority>) || queue.Count != 0 ||
            !ReferenceEquals(queue.Comparer, Comparer<TPriority>.Default) || !PriorityQueueTraversal.Available ||
            !PriorityQueueStorage.Available || !PriorityQueueParents.Available)
            return false;
        var nodes = LuminFieldLayout.Field<(TElement Element, TPriority Priority)[]>(queue, LuminCollectionLayouts.PriorityQueueNodes);
        if (count > nodes.Length)
            return false;
        ref var first = ref LuminPackMarshal.GetNotNullArrayReference(nodes);
        if (MemoryMarshal.AsBytes(LuminPackMarshal.CreateReadOnlySpan(ref first, count)).Overlaps(reader.Buffer))
            return false;

        int elementBytes = Unsafe.SizeOf<TElement>();
        int pairBytes = elementBytes + Unsafe.SizeOf<TPriority>();
        int payloadBytes = checked(count * pairBytes);
        ref byte input = ref reader.PeekInput(payloadBytes, out bool available);
        if (!available)
            return false;

        var comparer = Comparer<TPriority>.Default;
        int index = 0;
        do
        {
            TPriority priority = LuminPackReader.Read<TPriority>(ref Unsafe.Add(ref input, (nint)(uint)elementBytes));
            if (index != 0 && comparer.Compare(priority, Unsafe.Add(ref first, (nint)(uint)((index - 1) >> 2)).Priority) < 0)
                break;
            ref var slot = ref Unsafe.Add(ref first, (nint)(uint)index);
            slot.Element = LuminPackReader.Read<TElement>(ref input);
            slot.Priority = priority;
            input = ref Unsafe.Add(ref input, (nint)(uint)pairBytes);
        } while (++index < count);

        // Publish the exact prefix ordinary insertion would produce. A non-heap suffix still uses Enqueue.
        reader.Advance(index * pairBytes);
        LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.PriorityQueueSize) = index;
        ref int version = ref LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.PriorityQueueVersion);
        version = unchecked(version + index);
        if (index == count)
            return true;
        do
        {
            TElement element = default;
            TPriority priority = default;
            reader.Read(ref Unsafe.As<TElement, byte>(ref element), elementBytes);
            reader.Read(ref Unsafe.As<TPriority, byte>(ref priority), pairBytes - elementBytes);
            queue.Enqueue(element, priority);
        } while (++index < count);
        return true;
    }

    /// <summary>Probes the parent and tie behavior used by the default framework heap.</summary>
    private static class PriorityQueueParents
    {
        internal static readonly bool Available = Check();

        /// <summary>Returns the check for <c>PriorityQueueParents</c>.</summary>
        /// <returns>Whether the input satisfies the check check.</returns>
        private static bool Check()
        {
            try
            {
                var queue = new PriorityQueue<int, int>(8);
                foreach (int priority in new[] { 0, 4, 4, 4, 4, 1, 1, 4 })
                    queue.Enqueue(queue.Count, priority);
                if (!TryGetPriorityQueueNodes(queue, out var nodes, out int count) || count != 8)
                    return false;
                return nodes![1].Element == 5 && nodes[2].Element == 2 && nodes[4].Element == 4 &&
                    nodes[5].Element == 1 && nodes[6].Element == 6 && nodes[7].Element == 7;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
