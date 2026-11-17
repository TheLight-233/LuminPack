using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Publishes staged callback-free JSON elements once, or restores the original failed-read prefix.</summary>
    /// <typeparam name="T">The framework element contract selected by the generator.</typeparam>
    /// <param name="stack">The original fresh or cleared destination.</param>
    /// <param name="items">The staged values in JSON order.</param>
    /// <param name="count">The number of completed elements.</param>
    /// <param name="complete">Whether to restore final enumeration order after successful reading.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void PublishConcurrentStack<T>(ConcurrentStack<T> stack, T[] items, int count, bool complete)
    {
        if (complete)
        {
            items.AsSpan(0, count).Reverse();
            stack.Clear();
        }
        if (count != 0)
            PushConcurrentStackRange(stack, items, count);
    }

    /// <summary>Captures the same immutable node chain as the public stack enumerator.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    /// <param name="stack">The non-null source stack.</param>
    /// <returns>A value traversal, or the public traversal on an unsupported layout.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ConcurrentStackTraversal<T> EnumerateConcurrentStack<T>(ConcurrentStack<T> stack) => new(stack);

    /// <summary>Writes the raw record of a nonnull concurrent stack: its count, then its items bottom first.</summary>
    /// <typeparam name="T">The raw element storage.</typeparam>
    /// <param name="writer">The binary writer.</param>
    /// <param name="stack">The nonnull source stack.</param>
    /// <remarks>The record is the one a reversed snapshot writes. The head is read once, as the public enumerator reads it,
    /// and the captured chain never changes: pushes link a node before publishing it, and pops and <c>Clear</c> only replace
    /// the head. One pass stores the items top first in the room the writer already proves for an unchecked record, then
    /// reverses them in place, with no pooled snapshot or second copy. A chain longer than that room, a larger-than-four-byte
    /// element on a 32-bit process (whose in-place reversal would use typed unaligned access) and an unsupported layout write
    /// the reversed public snapshot with the original reservation.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void WriteConcurrentStack<T>(ref LuminPackWriter writer, ConcurrentStack<T> stack) where T : unmanaged
    {
        if (ConcurrentStackStorage<T>.Available && (IntPtr.Size == 8 || Unsafe.SizeOf<T>() <= 4))
        {
            int room = writer.RecordRoom() - sizeof(int);
            if (room >= 0)
            {
                int capacity = room / Unsafe.SizeOf<T>();
                int next = ConcurrentStackStorage<T>.Next, value = ConcurrentStackStorage<T>.Value;
                ref T items = ref Unsafe.As<byte, T>(ref Unsafe.Add(ref writer.CurrentReference, sizeof(int)));
                object? node = Volatile.Read(ref LuminFieldLayout.Field<object?>(stack, ConcurrentStackStorage<T>.Head));
                int count = 0;
                while (node is not null && count < capacity)
                {
                    LuminPackWriter.Write(ref Unsafe.As<T, byte>(ref Unsafe.Add(ref items, count++)), LuminFieldLayout.Field<T>(node, value));
                    node = LuminFieldLayout.Field<object?>(node, next);
                }
                if (node is null)
                {
                    MemoryExtensions.Reverse(LuminPackMarshal.CreateSpan(ref items, count));
                    LuminPackWriter.Write(ref writer.CurrentReference, count);
                    writer.CompleteRun(sizeof(int) + count * Unsafe.SizeOf<T>());
                    return;
                }
            }
        }
        WriteConcurrentStackSnapshot(ref writer, stack);
    }

    /// <summary>Writes the reversed snapshot of a stack, reserving its record as the contiguous writer does.</summary>
    /// <typeparam name="T">The raw element storage.</typeparam>
    /// <param name="writer">The binary writer.</param>
    /// <param name="stack">The nonnull source stack.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteConcurrentStackSnapshot<T>(ref LuminPackWriter writer, ConcurrentStack<T> stack) where T : unmanaged
    {
        T[] snapshot = RentSnapshot(stack, out int count);
        try
        {
            var items = snapshot.AsSpan(0, count);
            items.Reverse();
            writer.Write<T>(items, count);
        }
        finally
        {
            System.Buffers.ArrayPool<T>.Shared.Return(snapshot, false);
        }
    }

    /// <summary>Traverses concurrent buckets with the runtime's original capture and link-read timing.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The non-null source dictionary.</param>
    /// <returns>A value traversal, or the public traversal on an unsupported layout.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ConcurrentDictionaryTraversal<TKey, TValue> EnumerateConcurrentDictionary<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary) where TKey : notnull => new(dictionary);

    /// <summary>Retains a stack snapshot without an iterator object.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ConcurrentStackTraversal<T> : IDisposable
    {
        private object? _next;
        private readonly IEnumerator<T>? _fallback;
        private readonly int _valueOffset;
        private readonly int _nextOffset;
        /// <summary>Gets the captured current value.</summary>
        public T Current { get; private set; }

        /// <summary>Acquires the head with the same ordering as the public enumerator.</summary>
        /// <param name="stack">The source stack.</param>
        internal ConcurrentStackTraversal(ConcurrentStack<T> stack)
        {
            Current = default!;
            _next = null;
            _fallback = null;
            _valueOffset = 0;
            _nextOffset = 0;
            if (ConcurrentStackStorage<T>.Available)
            {
                _next = Volatile.Read(ref LuminFieldLayout.Field<object?>(stack, ConcurrentStackStorage<T>.Head));
                _valueOffset = ConcurrentStackStorage<T>.Value;
                _nextOffset = ConcurrentStackStorage<T>.Next;
            }
            else
                _fallback = stack.GetEnumerator();
        }

        /// <summary>Returns the already captured enumeration state.</summary>
        /// <returns>The captured traversal.</returns>
        public readonly ConcurrentStackTraversal<T> GetEnumerator() => this;

        /// <summary>Advances through nodes that remain immutable after publication.</summary>
        /// <returns>Whether another captured value exists.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_fallback is not null)
            {
                if (!_fallback.MoveNext())
                    return false;
                Current = _fallback.Current;
                return true;
            }
            object? node = _next;
            if (node is null)
                return false;
            Current = LuminFieldLayout.Field<T>(node, _valueOffset);
            _next = LuminFieldLayout.Field<object?>(node, _nextOffset);
            return true;
        }

        /// <summary>Releases only a public fallback iterator when one was required.</summary>
        public readonly void Dispose() => _fallback?.Dispose();
    }

    /// <summary>Retains the runtime's weakly consistent dictionary traversal without an iterator object.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ConcurrentDictionaryTraversal<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, TValue> _dictionary;
        private readonly IEnumerator<KeyValuePair<TKey, TValue>>? _fallback;
        private Array? _buckets;
        private object? _node;
        private int _index;
        /// <summary>Gets the pair captured before either component is serialized.</summary>
        public KeyValuePair<TKey, TValue> Current { get; private set; }

        /// <summary>Prepares the original lazy bucket capture or its unsupported-layout fallback.</summary>
        /// <param name="dictionary">The source dictionary.</param>
        internal ConcurrentDictionaryTraversal(ConcurrentDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _fallback = ConcurrentDictionaryStorage<TKey, TValue>.Available && ConcurrentDictionaryTiming.Available ? null : dictionary.GetEnumerator();
            _buckets = null;
            _node = null;
            _index = 0;
            Current = default;
        }

        /// <summary>Returns the original lazy traversal state.</summary>
        /// <returns>The prepared traversal.</returns>
        public readonly ConcurrentDictionaryTraversal<TKey, TValue> GetEnumerator() => this;

        /// <summary>Reads each bucket and link with acquire ordering, retaining runtime-specific yield timing.</summary>
        /// <returns>Whether a complete next pair was captured.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_fallback is not null)
            {
                if (!_fallback.MoveNext())
                    return false;
                Current = _fallback.Current;
                return true;
            }
            Array? buckets = _buckets;
            if (buckets is null)
            {
                object tables = Volatile.Read(ref LuminFieldLayout.Field<object>(_dictionary, ConcurrentBucketStorage<TKey, TValue>.Tables));
                _buckets = buckets = LuminFieldLayout.Field<Array>(tables, ConcurrentBucketStorage<TKey, TValue>.Buckets);
            }
            object? node = _node;
            if (!ConcurrentDictionaryTiming.ReadNextBeforeYield && node is not null)
                node = Volatile.Read(ref LuminFieldLayout.Field<object?>(node, ConcurrentDictionaryStorage<TKey, TValue>.Next));
            while (node is null)
            {
                if ((uint)_index >= (uint)buckets.Length)
                {
                    _node = null;
                    return false; }
                ref byte first = ref Unsafe.As<byte[]>(buckets)[0];
                ref object? slot = ref Unsafe.As<byte, object?>(ref Unsafe.AddByteOffset(ref first, (nint)_index++ * IntPtr.Size));
                node = Volatile.Read(ref slot);
            }
            Current = new KeyValuePair<TKey, TValue>(LuminFieldLayout.Field<TKey>(node, ConcurrentDictionaryStorage<TKey, TValue>.Key), LuminFieldLayout.Field<TValue>(node, ConcurrentDictionaryStorage<TKey, TValue>.Value));
            _node = ConcurrentDictionaryTiming.ReadNextBeforeYield
                ? Volatile.Read(ref LuminFieldLayout.Field<object?>(node, ConcurrentDictionaryStorage<TKey, TValue>.Next)) : node;
            return true;
        }

        /// <summary>Releases only a public fallback iterator when one was required.</summary>
        public readonly void Dispose() => _fallback?.Dispose();
    }

    /// <summary>Locates exact closed stack fields without constructing user elements.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    private static class ConcurrentStackStorage<T>
    {
        internal static readonly int Head;
        internal static readonly int Next;
        internal static readonly int Value;
        internal static readonly bool Available;

        /// <summary>Rejects missing metadata and field-type mismatches before publishing offsets.</summary>
        static ConcurrentStackStorage()
        {
            try
            {
                var head = LuminFieldLayout.Find(typeof(ConcurrentStack<T>), "_head");
                var node = head?.FieldType;
                if (node is null || node.IsValueType || LuminFieldLayout.Find(node, "_next")?.FieldType != node || LuminFieldLayout.Find(node, "_value")?.FieldType != typeof(T))
                    return;
                Head = LuminFieldLayout.ObjectField(typeof(ConcurrentStack<T>), "_head");
                Next = LuminFieldLayout.ObjectField(node, "_next");
                Value = LuminFieldLayout.ObjectField(node, "_value");
                Available = Head >= 0 && Next >= 0 && Value >= 0;
            }
            catch { }
        }
    }

    /// <summary>Validates the dictionary's bucket references independently of the key and value layouts.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    private static class ConcurrentBucketStorage<TKey, TValue> where TKey : notnull
    {
        internal static readonly int Tables;
        internal static readonly int Buckets;
        internal static readonly Type? Node;

        /// <summary>Checks that every slot holds exactly one managed reference to the expected framework node.</summary>
        static ConcurrentBucketStorage()
        {
            try
            {
                var tables = LuminFieldLayout.Find(typeof(ConcurrentDictionary<TKey, TValue>), "_tables")?.FieldType;
                var buckets = LuminFieldLayout.Find(tables, "_buckets")?.FieldType;
                var element = buckets?.GetElementType();
                if (tables is null || buckets is null || !buckets.IsSZArray || element is null)
                    return;
                Type node = element;
                if (element.IsValueType)
                {
                    var link = LuminFieldLayout.Find(element, "_node");
                    if (link is null || link.FieldType.IsValueType || LuminFieldLayout.ValueField(element, "_node") != 0 || LuminFieldLayout.ArrayStride(element) != IntPtr.Size)
                        return;
                    node = link.FieldType;
                }
                if (node.IsValueType || LuminFieldLayout.Find(node, "_next")?.FieldType != node ||
                    LuminFieldLayout.Find(node, "_key")?.FieldType != typeof(TKey) || LuminFieldLayout.Find(node, "_value")?.FieldType != typeof(TValue))
                    return;
                Tables = LuminFieldLayout.ObjectField(typeof(ConcurrentDictionary<TKey, TValue>), "_tables");
                Buckets = LuminFieldLayout.ObjectField(tables, "_buckets");
                if (Tables >= 0 && Buckets >= 0)
                    Node = node;
            }
            catch { }
        }
    }

    /// <summary>Locates the node members needed to traverse a published dictionary.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    private static class ConcurrentDictionaryStorage<TKey, TValue> where TKey : notnull
    {
        internal static readonly int Next;
        internal static readonly int Key;
        internal static readonly int Value;
        internal static readonly bool Available;

        /// <summary>Rejects node fields whose closed storage offsets cannot be proved.</summary>
        static ConcurrentDictionaryStorage()
        {
            if (ConcurrentBucketStorage<TKey, TValue>.Node is not { } node)
                return;
            Next = LuminFieldLayout.ObjectField(node, "_next");
            Key = LuminFieldLayout.ObjectField(node, "_key");
            Value = LuminFieldLayout.ObjectField(node, "_value");
            Available = Next >= 0 && Key >= 0 && Value >= 0;
        }
    }

    /// <summary>Distinguishes framework iterator versions by their observable capture timing.</summary>
    private static class ConcurrentDictionaryTiming
    {
        internal static readonly bool ReadNextBeforeYield;
        internal static readonly bool Available;

        /// <summary>Checks lazy table capture and whether a removed next node was already captured.</summary>
        static ConcurrentDictionaryTiming()
        {
            var growing = new ConcurrentDictionary<int, int>(1, 1);
            using (var empty = growing.GetEnumerator())
            {
                for (int i = 0; i < 127; i++)
                    growing.TryAdd(i, i);
                int count = 0;
                while (empty.MoveNext())
                    count++;
                if (count != growing.Count)
                    return;
            }
            var dictionary = new ConcurrentDictionary<int, int>(1, 31, new CollisionComparer());
            dictionary.TryAdd(1, 1);
            dictionary.TryAdd(2, 2);
            using var iterator = dictionary.GetEnumerator();
            if (!iterator.MoveNext())
                return;
            dictionary.TryRemove(iterator.Current.Key == 1 ? 2 : 1, out _);
            ReadNextBeforeYield = iterator.MoveNext();
            Available = true;
        }

        /// <summary>Places the probe keys in one chain without depending on bucket counts.</summary>
        private sealed class CollisionComparer : IEqualityComparer<int>
        {
            /// <summary>Compares probe keys.</summary>
            /// <param name="x">The first key.</param>
            /// <param name="y">The second key.</param>
            /// <returns>Whether the keys match.</returns>
            public bool Equals(int x, int y) => x == y;
            /// <summary>Selects the same bucket for both probe keys.</summary>
            /// <param name="value">The probe key.</param>
            /// <returns>The probe bucket hash.</returns>
            public int GetHashCode(int value) => 0;
        }
    }
}
