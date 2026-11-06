using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Copies one frozen bag snapshot into a native shared-pool lease before invoking any formatter.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    /// <param name="bag">The source bag.</param>
    /// <param name="count">The exact snapshot count.</param>
    /// <param name="pooled">Whether the caller returns the array to the shared pool.</param>
    /// <returns>The complete snapshot in public ToArray order.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T[] RentConcurrentBagSnapshot<T>(ConcurrentBag<T> bag, out int count, out bool pooled)
    {
        pooled = false;
        if (!ConcurrentBagStorage<T>.Available)
        {
            T[] fallback = bag.ToArray();
            count = fallback.Length;
            return fallback;
        }
        using var frozen = new FrozenBag<T>(bag);
        count = frozen.Count;
        if (count == 0)
            return Array.Empty<T>();
        T[] result = ArrayPool<T>.Shared.Rent(count);
        try
        {
            int index = 0;
            for (object? queue = frozen.Head; queue is not null; queue = LuminFieldLayout.Field<object?>(queue, ConcurrentBagStorage<T>.Next))
            {
                T[] items = LuminFieldLayout.Field<T[]>(queue, ConcurrentBagStorage<T>.Array);
                int size = QueueCount<T>(queue), mask = LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Mask);
                int head = LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.HeadIndex);
                for (int offset = size - 1; offset >= 0; offset--)
                    result[index++] = items[unchecked(head + offset) & mask];
            }
            pooled = true;
            return result;
        }
        catch (Exception error)
        {
            ArrayPool<T>.Shared.Return(result, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            LuminPackExceptionHelper.Rethrow(error);
            return null!;
        }
    }

    /// <summary>Writes callback-free raw bag elements directly into reserved output while holding the original snapshot freeze.</summary>
    /// <typeparam name="T">The raw unmanaged storage contract selected by the generator.</typeparam>
    /// <param name="writer">The destination state.</param>
    /// <param name="bag">The non-null source bag.</param>
    /// <returns>Whether the validated layout completed the original binary representation.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryWriteConcurrentBagRaw<T>(ref LuminPackWriter writer, ConcurrentBag<T> bag) where T : unmanaged
    {
        if (!ConcurrentBagStorage<T>.Available)
            return false;
        using var frozen = new FrozenBag<T>(bag);
        int width = Unsafe.SizeOf<T>(), bytes = checked(frozen.Count * width);
        writer.EnsureAdditionalCapacity(checked(bytes + 4));
        writer.WriteCollectionHeader(frozen.Count);
        ref byte destination = ref MemoryMarshal.GetReference(writer.Buffer.Slice(writer.Position, bytes));
        int written = 0;
        for (object? queue = frozen.Head; queue is not null; queue = LuminFieldLayout.Field<object?>(queue, ConcurrentBagStorage<T>.Next))
        {
            T[] items = LuminFieldLayout.Field<T[]>(queue, ConcurrentBagStorage<T>.Array);
            int size = QueueCount<T>(queue), mask = LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Mask);
            int head = LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.HeadIndex);
            for (int offset = size - 1; offset >= 0; offset--)
            {
                ref T item = ref items[unchecked(head + offset) & mask];
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref destination, written), ref Unsafe.As<T, byte>(ref item), (uint)width);
                written += width;
            }
        }
        writer.Advance(bytes);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Uses the stable counters rather than a tail index temporarily changed by a peek.</summary>
    /// <typeparam name="T">The closed queue layout.</typeparam>
    /// <param name="queue">A frozen work queue.</param>
    /// <returns>The actual element count.</returns>
    private static int QueueCount<T>(object queue) => checked(LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.AddTake) - LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Steal));

    /// <summary>Owns the original global and local freeze locks until a complete snapshot is captured.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    private struct FrozenBag<T> : IDisposable
    {
        private readonly object _gate;
        private bool _taken;
        internal object? Head;
        internal int Count;

        /// <summary>Stops creation of local queues, freezes their owners, and waits for in-flight operations.</summary>
        /// <param name="bag">The bag with validated metadata.</param>
        internal FrozenBag(ConcurrentBag<T> bag)
        {
            _gate = LuminFieldLayout.Field<object>(bag, ConcurrentBagStorage<T>.Gate);
            _taken = false;
            Head = null;
            Count = 0;
            try
            {
                Monitor.Enter(_gate, ref _taken);
                Head = LuminFieldLayout.Field<object?>(bag, ConcurrentBagStorage<T>.Queues);
                for (object? queue = Head; queue is not null; queue = LuminFieldLayout.Field<object?>(queue, ConcurrentBagStorage<T>.Next))
                    Monitor.Enter(queue, ref LuminFieldLayout.Field<bool>(queue, ConcurrentBagStorage<T>.Frozen));
                Interlocked.MemoryBarrier();
                for (object? queue = Head; queue is not null; queue = LuminFieldLayout.Field<object?>(queue, ConcurrentBagStorage<T>.Next))
                {
                    var spin = new SpinWait();
                    while (Volatile.Read(ref LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Operation)) != 0)
                        spin.SpinOnce();
                    Count = checked(Count + QueueCount<T>(queue));
                }
            }
            catch (Exception error)
            {
                Dispose();
                LuminPackExceptionHelper.Rethrow(error);
            }
        }

        /// <summary>Releases every acquired local lock before the global lock, including partial freezes.</summary>
        public void Dispose()
        {
            if (!_taken)
                return;
            for (object? queue = Head; queue is not null; queue = LuminFieldLayout.Field<object?>(queue, ConcurrentBagStorage<T>.Next))
            {
                ref bool frozen = ref LuminFieldLayout.Field<bool>(queue, ConcurrentBagStorage<T>.Frozen);
                if (!frozen)
                    continue;
                Volatile.Write(ref frozen, false);
                Monitor.Exit(queue);
            }
            Monitor.Exit(_gate);
            _taken = false;
        }
    }

    /// <summary>Validates the public snapshot's synchronization and work-queue storage fields.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    private static class ConcurrentBagStorage<T>
    {
        internal static readonly int Gate, Queues, Next, Array, HeadIndex, Mask, AddTake, Steal, Frozen, Operation;
        internal static readonly bool Available;

        /// <summary>Rejects missing or differently typed metadata before any internal field is read.</summary>
        static ConcurrentBagStorage()
        {
            try
            {
                Type bag = typeof(ConcurrentBag<T>);
                var queue = LuminFieldLayout.Find(bag, "_workStealingQueues")?.FieldType;
                var gate = LuminFieldLayout.Find(bag, "_locals")?.FieldType;
                if (queue is null || queue.IsValueType || gate is null || !gate.IsGenericType || gate.GetGenericTypeDefinition() != typeof(ThreadLocal<>))
                    return;
                if (LuminFieldLayout.Find(queue, "_nextQueue")?.FieldType != queue || LuminFieldLayout.Find(queue, "_array")?.FieldType != typeof(T[]) || LuminFieldLayout.Find(queue, "_frozen")?.FieldType != typeof(bool))
                    return;
                foreach (string name in new[] { "_headIndex", "_mask", "_addTakeCount", "_stealCount" })
                    if (LuminFieldLayout.Find(queue, name)?.FieldType != typeof(int))
                        return;
                var operation = LuminFieldLayout.Find(queue, "_currentOp")?.FieldType;
                if (operation != typeof(int) && (operation is null || !operation.IsEnum || Enum.GetUnderlyingType(operation) != typeof(int) || !Enum.IsDefined(operation, "None") || Convert.ToInt32(Enum.Parse(operation, "None")) != 0))
                    return;
                Gate = LuminFieldLayout.ObjectField(bag, "_locals");
                Queues = LuminFieldLayout.ObjectField(bag, "_workStealingQueues");
                Next = LuminFieldLayout.ObjectField(queue, "_nextQueue");
                Array = LuminFieldLayout.ObjectField(queue, "_array");
                HeadIndex = LuminFieldLayout.ObjectField(queue, "_headIndex");
                Mask = LuminFieldLayout.ObjectField(queue, "_mask");
                AddTake = LuminFieldLayout.ObjectField(queue, "_addTakeCount");
                Steal = LuminFieldLayout.ObjectField(queue, "_stealCount");
                Frozen = LuminFieldLayout.ObjectField(queue, "_frozen");
                Operation = LuminFieldLayout.ObjectField(queue, "_currentOp");
                Available = Gate >= 0 && Queues >= 0 && Next >= 0 && Array >= 0 && HeadIndex >= 0 && Mask >= 0 && AddTake >= 0 && Steal >= 0 && Frozen >= 0 && Operation >= 0;
            }
            catch { }
        }
    }
}
