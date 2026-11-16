#if NET10_0_OR_GREATER
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Clears an exact concurrent queue and sizes its replacement segment for a checked raw payload.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <param name="queue">The existing queue.</param>
    /// <param name="count">The element count after validating the complete raw input range.</param>
    /// <returns>Whether the supported queue reset succeeded. Otherwise the caller uses the framework clear.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryClearConcurrentQueue<T>(ConcurrentQueue<T> queue, int count) where T : unmanaged
    {
        if (count is <= 32 or > 4096 || queue.GetType() != typeof(ConcurrentQueue<T>) || !SizedConcurrentQueue<T>.Available)
            return false;
        SizedConcurrentQueue<T>.Clear(queue, (int)BitOperations.RoundUpToPowerOf2((uint)count));
        return true;
    }

    /// <summary>Binds the checked CoreLib queue reset for one closed element type.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    private static class SizedConcurrentQueue<T> where T : unmanaged
    {
        private const string SegmentType = "System.Collections.Concurrent.ConcurrentQueueSegment`1[[!0]], System.Private.CoreLib";
        private static readonly int Gate;
        private static readonly int Head;
        private static readonly int Tail;

        /// <summary>Whether this runtime and closed type passed the reset probe.</summary>
        internal static readonly bool Available;

        /// <summary>Checks the stock reset and proves that both accessors match the expected segment storage.</summary>
        static SizedConcurrentQueue()
        {
            if (!RuntimeFeature.IsDynamicCodeCompiled || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
                Environment.Version is not { Major: 10 or 11, Minor: 0 })
                return;

            try
            {
                var queueType = typeof(ConcurrentQueue<T>);
                var gate = LuminFieldLayout.Find(queueType, "_crossSegmentLock");
                var head = LuminFieldLayout.Find(queueType, "_head");
                var tail = LuminFieldLayout.Find(queueType, "_tail");
                if (queueType.Assembly != typeof(object).Assembly || gate?.FieldType != typeof(object) || !gate.IsInitOnly ||
                    head is null || tail is null || head.FieldType != tail.FieldType ||
                    !head.FieldType.IsGenericType || head.FieldType.Assembly != queueType.Assembly ||
                    head.FieldType.GetGenericTypeDefinition().FullName != "System.Collections.Concurrent.ConcurrentQueueSegment`1" ||
                    head.FieldType.GetGenericArguments()[0] != typeof(T) || !VolatileField(head) || !VolatileField(tail))
                    return;

                var segment = head.FieldType;
                var slots = LuminFieldLayout.Find(segment, "_slots");
                var frozen = LuminFieldLayout.Find(segment, "_frozenForEnqueues");
                if (slots is null || !slots.IsInitOnly || !slots.FieldType.IsSZArray || frozen?.FieldType != typeof(bool))
                    return;

                Gate = LuminFieldLayout.ObjectField(queueType, "_crossSegmentLock");
                Head = LuminFieldLayout.ObjectField(queueType, "_head");
                Tail = LuminFieldLayout.ObjectField(queueType, "_tail");
                if ((Gate | Head | Tail) < 0 || Gate == Head || Gate == Tail || Head == Tail)
                    return;

                var probe = new ConcurrentQueue<T>();
                object sync = LuminFieldLayout.Field<object>(probe, Gate);
                object original = LuminFieldLayout.Field<object>(probe, Head);
                if (!ReferenceEquals(original, LuminFieldLayout.Field<object>(probe, Tail)) ||
                    slots.GetValue(original) is not Array { Length: 32 } || frozen.GetValue(original) is not false)
                    return;

                probe.Enqueue(default!);
                probe.Clear();
                object stock = LuminFieldLayout.Field<object>(probe, Head);
                if (!ReferenceEquals(sync, LuminFieldLayout.Field<object>(probe, Gate)) || ReferenceEquals(original, stock) ||
                    !ReferenceEquals(stock, LuminFieldLayout.Field<object>(probe, Tail)) ||
                    slots.GetValue(stock) is not Array { Length: 32 } || frozen.GetValue(original) is not true ||
                    frozen.GetValue(stock) is not false || !probe.IsEmpty)
                    return;

                Clear(probe, 64);
                object sized = LuminFieldLayout.Field<object>(probe, Head);
                if (!ReferenceEquals(sync, LuminFieldLayout.Field<object>(probe, Gate)) || ReferenceEquals(stock, sized) ||
                    !ReferenceEquals(sized, LuminFieldLayout.Field<object>(probe, Tail)) ||
                    slots.GetValue(sized) is not Array { Length: 64 } || frozen.GetValue(stock) is not true ||
                    frozen.GetValue(sized) is not false || !probe.IsEmpty)
                    return;

                probe.Enqueue(default!);
                Available = probe.TryDequeue(out _) && probe.IsEmpty;
            }
            catch
            {
                // A different runtime or missing private metadata leaves the framework route available.
            }
        }

        /// <summary>Checks the acquire and release semantics required for the stored field.</summary>
        /// <param name="field">The runtime field.</param>
        /// <returns>Whether the runtime field is declared volatile.</returns>
        private static bool VolatileField(FieldInfo field) =>
            !field.IsInitOnly && Array.IndexOf(field.GetRequiredCustomModifiers(), typeof(IsVolatile)) >= 0;

        /// <summary>Replays the stock Clear operation with the checked initial capacity.</summary>
        /// <param name="queue">An exact queue of the validated type.</param>
        /// <param name="capacity">The power-of-two capacity of the replacement segment.</param>
        internal static void Clear(ConcurrentQueue<T> queue, int capacity)
        {
            lock (LuminFieldLayout.Field<object>(queue, Gate))
            {
                ref object tail = ref LuminFieldLayout.Field<object>(queue, Tail);
                Freeze(Volatile.Read(ref tail));
                object replacement = CreateSegment(capacity);
                Volatile.Write(ref LuminFieldLayout.Field<object>(queue, Head), replacement);
                Volatile.Write(ref tail, replacement);
            }
        }

        /// <summary>Freezes the original tail without changing any captured segment contents.</summary>
        /// <param name="segment">The current tail of the validated closed queue type.</param>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EnsureFrozenForEnqueues")]
        private static extern void Freeze([UnsafeAccessorType(SegmentType)] object segment);

        /// <summary>Invokes the framework segment constructor.</summary>
        /// <param name="capacity">The power-of-two capacity of the segment.</param>
        /// <returns>A fresh framework segment initialized for normal enqueues.</returns>
        [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
        [return: UnsafeAccessorType(SegmentType)]
        private static extern object CreateSegment(int capacity);
    }
}
#endif
