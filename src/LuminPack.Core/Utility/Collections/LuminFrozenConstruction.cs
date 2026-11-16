using System;
using System.Collections.Generic;
#if NET10_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Utility;

/// <summary>Owns temporary mutable construction storage and fixed scalar writes for frozen collections.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminFrozenConstruction
{
    private const int MaximumRetainedCapacity = 4096;
    private const int LastTablePrimeBelowLimit = 4049;
    private const int RoundedRetainedCapacity = 4861;

    /// <summary>Rents an exclusive empty dictionary using the default comparer.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="capacity">The already validated initial count, or zero for one-pass JSON.</param>
    /// <returns>The exclusive mutable construction dictionary.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Dictionary<TKey, TValue> RentDictionary<TKey, TValue>(int capacity) where TKey : notnull
    {
        var value = Cache<Dictionary<TKey, TValue>>.Items.TryTake();
        if (value is null)
            return new Dictionary<TKey, TValue>(capacity);
        if (capacity != 0)
            value.EnsureCapacity(capacity);
        return value;
    }

    /// <summary>Releases all input references and retains only bounded construction capacity.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="value">The exclusively owned construction dictionary.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnDictionary<TKey, TValue>(Dictionary<TKey, TValue> value) where TKey : notnull
    {
        int count = value.Count, capacity = value.EnsureCapacity(0);
        value.Clear();
        if (capacity <= RetainedTableCapacity(count) && capacity <= Math.Max(128, count * 4) &&
            ReferenceEquals(value.Comparer, EqualityComparer<TKey>.Default))
            Cache<Dictionary<TKey, TValue>>.Items.TryStore(value);
    }

    /// <summary>Rents an exclusive empty set using the default comparer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="capacity">The already validated initial count, or zero for one-pass JSON.</param>
    /// <returns>The exclusive mutable construction set.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static HashSet<T> RentSet<T>(int capacity)
    {
        var value = Cache<HashSet<T>>.Items.TryTake();
        if (value is null)
            return new HashSet<T>(capacity);
        if (capacity != 0)
            value.EnsureCapacity(capacity);
        return value;
    }

    /// <summary>Releases all input references and retains only bounded construction capacity.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The exclusively owned construction set.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnSet<T>(HashSet<T> value) => ReturnSet(value, MaximumRetainedCapacity);

    /// <summary>Releases a fixed raw set builder, allowing the framework bucket needed for up to 4096 requested elements.</summary>
    /// <typeparam name="T">The raw callback-free element type.</typeparam>
    /// <param name="value">The exclusively owned construction set.</param>
    /// <param name="requestedCount">The validated count before insertion.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnRawSet<T>(HashSet<T> value, int requestedCount) where T : unmanaged =>
        ReturnSet(value, RetainedTableCapacity(requestedCount));

    /// <summary>Includes the table the framework rounds up for counts still within the retention limit.</summary>
    /// <param name="count">The eligible completed or validated requested count.</param>
    /// <returns>The maximum permitted capacity in the existing cache.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RetainedTableCapacity(int count) => count is > LastTablePrimeBelowLimit and <= MaximumRetainedCapacity
        ? RoundedRetainedCapacity : MaximumRetainedCapacity;

    /// <summary>Clears and releases a bounded private builder.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The exclusively owned set.</param>
    /// <param name="maximumCapacity">The accepted upper bound of retained capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnSet<T>(HashSet<T> value, int maximumCapacity)
    {
        int count = value.Count, capacity = value.EnsureCapacity(0);
        value.Clear();
        if (capacity <= maximumCapacity && capacity <= Math.Max(128, count * 4) && ReferenceEquals(value.Comparer, EqualityComparer<T>.Default))
            Cache<HashSet<T>>.Items.TryStore(value);
    }

    /// <summary>Adds a complete fixed-width payload to a frozen set's exclusive builder without staging an item array.</summary>
    /// <typeparam name="T">A fixed primitive or enum whose hashing and equality have no callbacks.</typeparam>
    /// <param name="reader">The binary state after the count has been read.</param>
    /// <param name="set">The destination builder, sized for the complete element count.</param>
    /// <param name="count">The declared element count.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddRawSetItems<T>(scoped ref LuminPackReader reader, HashSet<T> set, int count) where T : unmanaged
    {
        int width = Unsafe.SizeOf<T>();
        int bytes = checked(count * width);
        reader.EnsureReadable(reader.Position, bytes);
        ref byte source = ref reader.PeekInput(0, out _);
        reader.Advance(bytes);
        if (count == 0)
            return;
        do
        {
            set.Add(LuminPackReader.Read<T>(ref source));
            source = ref Unsafe.Add(ref source, (nint)(uint)width);
        } while (--count != 0);
    }

#if NET10_0_OR_GREATER
    /// <summary>Reuses a previously certified immutable raw set or freezes the checked payload.</summary>
    /// <typeparam name="T">The callback-free raw element type.</typeparam>
    /// <param name="reader">The reader immediately after the complete checked count.</param>
    /// <param name="value">The existing frozen set and the result.</param>
    /// <param name="length">The strictly positive encoded element count.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReadRawSetReuse<T>(scoped ref LuminPackReader reader, scoped ref FrozenSet<T>? value, int length)
        where T : unmanaged
    {
        ReadOnlySpan<byte> input = default;
        if (RawSetCertificate<T>.Available)
        {
            int bytes = checked(length * Unsafe.SizeOf<T>());
            int position = reader.Position;
            ReadOnlySpan<byte> buffer = reader.Buffer;
            ref byte start = ref MemoryMarshal.GetReference(buffer);
            if (bytes > 0 && (uint)position <= (uint)buffer.Length && bytes <= buffer.Length - position &&
                Unsafe.AreSame(ref reader.CurrentReference, ref Unsafe.Add(ref start, position)))
                input = buffer.Slice(position, bytes);

            var existing = value;
            if (!input.IsEmpty && existing is not null && RawSetCertificate<T>.Last is { } previous &&
                previous.TryGetTarget(out var known) && ReferenceEquals(known, existing) &&
                input.SequenceEqual(MemoryMarshal.AsBytes(existing.Items.AsSpan())))
            {
                reader.Advance(bytes);
                return;
            }
        }

        var builder = RentSet<T>(length);
        try
        {
            AddRawSetItems(ref reader, builder, length);
            var result = builder.ToFrozenSet();
            value = result;
            if (!input.IsEmpty && ReferenceEquals(result.Comparer, EqualityComparer<T>.Default) &&
                input.SequenceEqual(MemoryMarshal.AsBytes(result.Items.AsSpan())))
                RawSetCertificate<T>.Remember(result);
        }
        finally
        {
            ReturnRawSet(builder, length);
        }
    }

    /// <summary>Remembers one stock fixed point weakly per raw type and thread.</summary>
    /// <typeparam name="T">The raw element type.</typeparam>
    private static class RawSetCertificate<T> where T : unmanaged
    {
        /// <summary>Whether the framework freeze uses deterministic callback-free hashing in this runtime.</summary>
        internal static readonly bool Available = RuntimeFeature.IsDynamicCodeCompiled &&
            Environment.Version is { Major: 10 or 11, Minor: 0 } &&
            typeof(FrozenSet<T>).Assembly.GetName().Version?.Major == Environment.Version.Major &&
            typeof(T) != typeof(bool) && (typeof(T).IsPrimitive || typeof(T).IsEnum);

        /// <summary>The last certified immutable result for the current thread.</summary>
        [ThreadStatic] internal static WeakReference<FrozenSet<T>>? Last;

        /// <summary>Records a result whose Items exactly match its checked input in order.</summary>
        /// <param name="value">The result of ordinary freezing.</param>
        internal static void Remember(FrozenSet<T> value)
        {
            if (Last is { } previous)
            {
                previous.SetTarget(value);
                return;
            }
            try
            {
                Last = new WeakReference<FrozenSet<T>>(value);
            }
            catch (OutOfMemoryException)
            {
                // The optional certificate does not change allocation failure of the existing read.
            }
        }
    }
#endif

    /// <summary>Writes matching scalar key/value arrays after one complete payload reservation.</summary>
    /// <typeparam name="TKey">The complete raw scalar key representation.</typeparam>
    /// <typeparam name="TValue">The complete raw scalar value representation.</typeparam>
    /// <param name="writer">The output state.</param>
    /// <param name="keys">Keys in frozen enumeration order.</param>
    /// <param name="values">Values in the same order.</param>
    public static void WritePairs<TKey, TValue>(ref LuminPackWriter writer, ReadOnlySpan<TKey> keys, ReadOnlySpan<TValue> values) where TKey : unmanaged where TValue : unmanaged
    {
        if (keys.Length != values.Length)
        {
            LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
            return;
        }
        int keyWidth = Unsafe.SizeOf<TKey>(), valueWidth = Unsafe.SizeOf<TValue>();
        int bytes = checked(keys.Length * checked(keyWidth + valueWidth));
        writer.EnsureAdditionalCapacity(checked(bytes + 4));
        writer.WriteCollectionHeader(keys.Length);
        if (!keys.IsEmpty)
        {
            ref byte destination = ref MemoryMarshal.GetReference(writer.Buffer.Slice(writer.Position, bytes));
            ref TKey firstKey = ref MemoryMarshal.GetReference(keys);
            ref TValue firstValue = ref MemoryMarshal.GetReference(values);
            int index = 0;
            do
            {
                WriteScalar(ref destination, ref Unsafe.Add(ref firstKey, (nint)(uint)index));
                WriteScalar(ref Unsafe.Add(ref destination, (nint)(uint)keyWidth), ref Unsafe.Add(ref firstValue, (nint)(uint)index));
                destination = ref Unsafe.Add(ref destination, (nint)(uint)(keyWidth + valueWidth));
            } while (++index < keys.Length);
        }
        writer.Advance(bytes);
        writer.CheckBuffer();
    }

    /// <summary>Stores one scalar with the existing architecture-specific fixed-access contract.</summary>
    /// <typeparam name="T">The complete unmanaged scalar representation.</typeparam>
    /// <param name="destination">The already reserved destination.</param>
    /// <param name="source">The validated scalar source.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteScalar<T>(ref byte destination, ref T source) where T : unmanaged
    {
        if (IntPtr.Size == 8 || Unsafe.SizeOf<T>() <= 4)
            Unsafe.WriteUnaligned(ref destination, source);
        else
            LuminPackMarshal.CopyBlockUnaligned(ref destination, ref Unsafe.As<T, byte>(ref source), (uint)Unsafe.SizeOf<T>());
    }

    /// <summary>Bounds retained builder count globally without creating a separate array pool.</summary>
    /// <typeparam name="T">The exact closed mutable collection type.</typeparam>
    private static class Cache<T> where T : class
    {
        internal static readonly AtomicSlotPool<T> Items = new(4);
    }
}
