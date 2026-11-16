#if NET10_0_OR_GREATER
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Presizes the first local work queue without changing its indices, counts, or item.</summary>
    /// <typeparam name="T">The original bag element type.</typeparam>
    /// <param name="bag">The exact fresh bag after the first ordinary Add.</param>
    /// <param name="count">The total successfully decoded item count.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void PresizeFreshConcurrentBag<T>(ConcurrentBag<T> bag, int count)
    {
        if (count is < 64 or > 4096 || bag.GetType() != typeof(ConcurrentBag<T>) || !ConcurrentBagSizing<T>.Available)
            return;

        using var frozen = new FrozenBag<T>(bag);
        if (frozen.Head is not { } queue || frozen.Count != 1 ||
            LuminFieldLayout.Field<object?>(queue, ConcurrentBagStorage<T>.Next) is not null ||
            LuminFieldLayout.Field<int>(queue, ConcurrentBagSizing<T>.OwnerThread) != Environment.CurrentManagedThreadId ||
            LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.HeadIndex) != 0 ||
            LuminFieldLayout.Field<int>(queue, ConcurrentBagSizing<T>.TailIndex) != 1 ||
            LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.AddTake) != 1 ||
            LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Steal) != 0)
            return;

        ref T[] array = ref LuminFieldLayout.Field<T[]>(queue, ConcurrentBagStorage<T>.Array);
        ref int mask = ref LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Mask);
        // The framework grows on occupancy equal to its mask, leaving a spare slot.
        int capacity = (int)BitOperations.RoundUpToPowerOf2((uint)count + 1);
        if (array.Length != ConcurrentBagSizing<T>.InitialCapacity || mask != array.Length - 1 || capacity <= array.Length * 2)
            return;

        var sized = new T[capacity];
        sized[0] = array[0];
        array = sized;
        mask = capacity - 1;
    }

    /// <summary>Recognizes the addressable fields and the growth rule of a real local queue.</summary>
    /// <typeparam name="T">The original bag element type.</typeparam>
    private static class ConcurrentBagSizing<T>
    {
        internal static readonly int OwnerThread, TailIndex, InitialCapacity;
        internal static readonly bool Available;

        /// <summary>Probes the local queue layout and verifies its growth threshold.</summary>
        static ConcurrentBagSizing()
        {
            if (!RuntimeFeature.IsDynamicCodeCompiled || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
                Environment.Version.Major is not (10 or 11) || !ConcurrentBagStorage<T>.Available)
                return;

            try
            {
                var queueType = LuminFieldLayout.Find(typeof(ConcurrentBag<T>), "_workStealingQueues")?.FieldType;
                if (LuminFieldLayout.Find(queueType, "_tailIndex")?.FieldType != typeof(int) ||
                    LuminFieldLayout.Find(queueType, "_ownerThreadId")?.FieldType != typeof(int) ||
                    LuminFieldLayout.Find(queueType, "_array") is not { IsInitOnly: false } ||
                    LuminFieldLayout.Find(queueType, "_mask") is not { IsInitOnly: false })
                    return;
                TailIndex = LuminFieldLayout.ObjectField(queueType, "_tailIndex");
                OwnerThread = LuminFieldLayout.ObjectField(queueType, "_ownerThreadId");
                if ((TailIndex | OwnerThread) < 0)
                    return;

                var bag = new ConcurrentBag<T>();
                bag.Add(default!);
                var queue = LuminFieldLayout.Field<object>(bag, ConcurrentBagStorage<T>.Queues);
                InitialCapacity = LuminFieldLayout.Field<T[]>(queue, ConcurrentBagStorage<T>.Array).Length;
                if (InitialCapacity != 32 ||
                    LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Mask) != InitialCapacity - 1 ||
                    LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.HeadIndex) != 0 ||
                    LuminFieldLayout.Field<int>(queue, TailIndex) != 1 ||
                    LuminFieldLayout.Field<int>(queue, OwnerThread) != Environment.CurrentManagedThreadId)
                    return;

                for (int count = 1; count < InitialCapacity; count++)
                    bag.Add(default!);
                Available = LuminFieldLayout.Field<T[]>(queue, ConcurrentBagStorage<T>.Array).Length == InitialCapacity * 2 &&
                    LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.Mask) == InitialCapacity * 2 - 1 &&
                    LuminFieldLayout.Field<int>(queue, ConcurrentBagStorage<T>.HeadIndex) == 0 &&
                    LuminFieldLayout.Field<int>(queue, TailIndex) == InitialCapacity && bag.Count == InitialCapacity;
            }
            catch { }
        }
    }
}
#endif
