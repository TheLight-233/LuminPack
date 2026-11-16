using System;
using System.Collections.Generic;
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
        if (capacity <= MaximumRetainedCapacity && capacity <= Math.Max(128, count * 4) && ReferenceEquals(value.Comparer, EqualityComparer<TKey>.Default))
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
    public static void ReturnSet<T>(HashSet<T> value)
    {
        int count = value.Count, capacity = value.EnsureCapacity(0);
        value.Clear();
        if (capacity <= MaximumRetainedCapacity && capacity <= Math.Max(128, count * 4) && ReferenceEquals(value.Comparer, EqualityComparer<T>.Default))
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
