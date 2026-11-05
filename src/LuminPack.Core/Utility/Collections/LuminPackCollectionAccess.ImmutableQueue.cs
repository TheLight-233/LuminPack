using System;
using System.Buffers;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Locates immutable queue stacks without creating the framework's lazy reversal.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="queue">The exact framework queue.</param>
    /// <param name="forwards">Receives the oldest elements in enumeration order.</param>
    /// <param name="backwards">Receives the cached reversal or original newer-element stack.</param>
    /// <param name="reverse">Whether the newer-element stack needs reversed traversal.</param>
    /// <returns>Whether the established queue storage probe supports this traversal.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryGetImmutableQueueTraversal<T>(ImmutableQueue<T> queue, out ImmutableStack<T>? forwards,
        out ImmutableStack<T>? backwards, out bool reverse)
    {
        forwards = null;
        backwards = null;
        reverse = false;
        if (!ImmutableQueueStorage.Available || !ImmutableQueueReversalStorage.Available) return false;
        var front = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.Forwards);
        var back = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.BackwardsReversed);
        if (front is null) return false;
        if (back is null)
        {
            back = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.Backwards);
            if (back is null) return false;
            // A singleton is already in both orders and needs no temporary lease.
            reverse = !back.IsEmpty && !back.Pop().IsEmpty;
        }
        forwards = front;
        backwards = back;
        return true;
    }

    /// <summary>Copies an immutable queue's uncached back stack in enumeration order into a returned lease.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stack">The nonempty newer-element stack, observed without changing the queue.</param>
    /// <param name="count">Receives the valid element count.</param>
    /// <returns>A shared-pool array; the caller returns it and clears references on every exit.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T[] RentImmutableQueueReversal<T>(ImmutableStack<T> stack, out int count)
    {
        count = 0;
        foreach (T item in stack) count = checked(count + 1);
        T[] items = ArrayPool<T>.Shared.Rent(count);
        try
        {
            int index = count;
            foreach (T item in stack) items[--index] = item;
            return items;
        }
        catch (Exception error)
        {
            ArrayPool<T>.Shared.Return(items, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            LuminPackExceptionHelper.Rethrow(error);
            return null!;
        }
    }

    /// <summary>Validates the raw back-stack order independently of the original cached-reversal probe.</summary>
    private static class ImmutableQueueReversalStorage
    {
        internal static readonly bool Available = Check();

        /// <summary>Checks uncached and cached queues across both reference and value element layouts.</summary>
        /// <returns>Whether reversed raw stacks preserve public enumeration.</returns>
        private static bool Check()
        {
            try
            {
                if (!ImmutableQueueStorage.Available) return false;
                var integers = ImmutableQueue<int>.Empty;
                var objects = ImmutableQueue<object>.Empty;
                for (int index = 0; index < 17; index++)
                {
                    integers = integers.Enqueue(index);
                    objects = objects.Enqueue(new object());
                    if (!Matches(integers) || !Matches(objects)) return false;
                    if (index % 3 == 2)
                    {
                        integers = integers.Dequeue();
                        objects = objects.Dequeue();
                        if (!Matches(integers) || !Matches(objects)) return false;
                    }
                }
                return Matches(ImmutableQueue<int>.Empty);
            }
            catch { return false; }
        }

        /// <summary>Compares raw and cached stack orders with the public queue without modifying its membership.</summary>
        /// <typeparam name="T">The element type whose field layout is being checked.</typeparam>
        /// <param name="queue">The queue at the current enqueue/dequeue boundary.</param>
        /// <returns>Whether both observed traversals agree with the public sequence.</returns>
        private static bool Matches<T>(ImmutableQueue<T> queue)
        {
            var front = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.Forwards);
            var back = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.Backwards);
            if (front is null || back is null) return false;
            var expected = front.Concat(back.Reverse()).ToArray();
            if (!queue.SequenceEqual(expected)) return false;
            var cached = LuminFieldLayout.Field<ImmutableStack<T>?>(queue, ImmutableQueueStorage.BackwardsReversed);
            return cached is null ? back.IsEmpty : front.Concat(cached).SequenceEqual(expected);
        }
    }
}
