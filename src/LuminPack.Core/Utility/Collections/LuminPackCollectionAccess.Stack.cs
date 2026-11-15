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
    /// <summary>Writes a nonempty raw stack as one bottom-first binary record within the current output window.</summary>
    /// <typeparam name="T">The reference-free stack element.</typeparam>
    /// <param name="writer">The writer positioned at the collection header.</param>
    /// <param name="stack">The nonempty source stack.</param>
    /// <param name="count">The captured element count.</param>
    /// <returns>Whether the complete record was written without growing the buffer.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteStackRecord<T>(scoped ref LuminPackWriter writer, Stack<T> stack, int count) where T : unmanaged
    {
        long bytes = sizeof(int) + (long)count * Unsafe.SizeOf<T>();
        if (writer.Position < 0 || writer.RecordRoom() < bytes || !TryGetStackArray(stack, count, out var array))
            return false;
        int recordBytes = (int)bytes;
        int payloadBytes = recordBytes - sizeof(int);
        ref byte source = ref Unsafe.As<T, byte>(ref LuminPackMarshal.GetNotNullArrayReference(array));
        if (LuminPackMarshal.CreateReadOnlySpan(ref source, payloadBytes).Overlaps(writer.Buffer))
            return false;

        ref byte output = ref writer.CurrentReference;
        LuminPackWriter.Write(ref output, count);
        LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref output, sizeof(int)), ref source, (uint)payloadBytes);
        writer.CompleteRun(recordBytes);
        return true;
    }

    /// <summary>Copies bottom-first stack storage into an already started binary record.</summary>
    /// <typeparam name="T">The reference-free stack element.</typeparam>
    /// <param name="writer">The writer positioned after the collection header.</param>
    /// <param name="stack">The nonempty source stack.</param>
    /// <param name="count">The captured element count.</param>
    /// <returns>Whether the complete, non-overlapping output window was written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteStackStorage<T>(scoped ref LuminPackWriter writer, Stack<T> stack, int count) where T : unmanaged
    {
        long bytes = (long)count * Unsafe.SizeOf<T>();
        if (writer.RecordRoom() < bytes || !TryGetStackArray(stack, count, out var array))
            return false;
        var payload = MemoryMarshal.AsBytes(LuminPackMarshal.CreateReadOnlySpan(
            ref LuminPackMarshal.GetNotNullArrayReference(array), count));
        if (payload.Overlaps(writer.Buffer))
            return false;
        writer.Write(ref MemoryMarshal.GetReference(payload), (int)bytes);
        return true;
    }

    /// <summary>Writes bottom-first string records directly from the stack storage.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="writer">The writer positioned at the collection header.</param>
    /// <param name="stack">The nonempty source stack.</param>
    /// <param name="checkEachRecord">Whether the original traversal checked the growth threshold before each string.</param>
    /// <returns>Whether the supported profile and layout were available.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteStringStackStorage<TOptions>(scoped ref LuminPackWriter writer, Stack<string?> stack, bool checkEachRecord)
        where TOptions : struct, ILuminPackOptions
    {
        int count = stack.Count;
        if (!writer.UsesUtf16LengthRecords<TOptions>() || !TryGetStackArray(stack, count, out var array))
            return false;
        int position = writer.BeginCollection();
        var slots = LuminPackMarshal.CreateReadOnlySpan(ref LuminPackMarshal.GetNotNullArrayReference(array), count);
        if (checkEachRecord)
            writer.WriteCheckedStrings<TOptions>(slots);
        else
            writer.WriteStrings<TOptions>(slots);
        writer.EndCollection(position, count);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Fills an empty stack whose existing storage already fits the complete bottom-first record.</summary>
    /// <typeparam name="T">The reference-free stack element.</typeparam>
    /// <param name="reader">The reader positioned after the collection header.</param>
    /// <param name="stack">The exact target already constructed or cleared by the caller.</param>
    /// <param name="count">The collection count validated by the reader.</param>
    /// <returns>Whether the destination could be filled and published directly.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadStackStorage<T>(scoped ref LuminPackReader reader, Stack<T> stack, int count) where T : unmanaged
    {
        if (stack.Count != 0 || count <= 0 || !StackStorage.Available || stack.GetType() != typeof(Stack<T>))
            return false;
        var array = LuminFieldLayout.Field<T[]>(stack, LuminCollectionLayouts.StackArray);
        if ((uint)count > (uint)array.Length)
            return false;
        var payload = MemoryMarshal.AsBytes(LuminPackMarshal.CreateSpan(
            ref LuminPackMarshal.GetNotNullArrayReference(array), count));
        if (payload.Overlaps(reader.Buffer))
            return false;
        reader.Read(ref MemoryMarshal.GetReference(payload), payload.Length);
        PublishStackSize(stack, count);
        return true;
    }

    /// <summary>Reads a complete bottom-first string payload into an empty stack before publishing its count.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="reader">The reader after its collection header.</param>
    /// <param name="stack">The already constructed or cleared target.</param>
    /// <param name="count">The checked collection count.</param>
    /// <returns>Whether the existing storage can receive the complete string transfer.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryReadStringStackStorage<TOptions>(scoped ref LuminPackReader reader, Stack<string?> stack, int count)
        where TOptions : struct, ILuminPackOptions
    {
        if (stack.Count != 0 || count <= 0 || !StackStorage.Available || stack.GetType() != typeof(Stack<string?>))
            return false;
        var array = LuminFieldLayout.Field<string?[]>(stack, LuminCollectionLayouts.StackArray);
        if ((uint)count > (uint)array.Length)
            return false;
        var slots = LuminPackMarshal.CreateSpan(ref LuminPackMarshal.GetNotNullArrayReference(array), count);
        try
        {
            // Stack.Clear guarantees empty active slots. Decode independently of hidden inactive references.
            slots.Clear();
            reader.ReadStrings<TOptions>(slots);
        }
        catch (Exception exception)
        {
            slots.Clear();
            LuminPackExceptionHelper.Rethrow(exception);
        }
        PublishStackSize(stack, count);
        return true;
    }

    /// <summary>Exposes exact, validated stack storage in bottom-first order for a callback-free binary writer.</summary>
    /// <typeparam name="T">The stack element.</typeparam>
    /// <param name="stack">The stack to inspect.</param>
    /// <param name="items">The active read-only storage on success.</param>
    /// <returns>Whether the nonempty stack has recognized storage.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetBottomFirstStackItems<T>(Stack<T> stack, out ReadOnlySpan<T> items)
    {
        int count = stack.Count;
        if (TryGetStackArray(stack, count, out var array))
        {
            items = LuminPackMarshal.CreateReadOnlySpan(ref LuminPackMarshal.GetNotNullArrayReference(array), count);
            return true;
        }

        items = default;
        return false;
    }

    /// <summary>Publishes a completely decoded bottom-first binary range into the selected stack.</summary>
    /// <typeparam name="T">The stack element.</typeparam>
    /// <param name="stack">The selected target after all element callbacks have completed.</param>
    /// <param name="items">The completed elements in wire order.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RestoreStackFromBottom<T>(Stack<T> stack, scoped ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
            return;

        if (StackStorage.Available && stack.GetType() == typeof(Stack<T>) && stack.Count == 0)
        {
            var array = LuminFieldLayout.Field<T[]>(stack, LuminCollectionLayouts.StackArray);
            if (items.Length <= array.Length)
            {
                items.CopyTo(array);
                PublishStackSize(stack, items.Length);
                return;
            }
        }

        foreach (T item in items)
            stack.Push(item);
    }

    /// <summary>Restores a fresh stack from a completely decoded top-first JSON range.</summary>
    /// <typeparam name="T">The stack element.</typeparam>
    /// <param name="stack">The fresh empty result with storage reserved for the range.</param>
    /// <param name="items">The staged elements in pop order.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RestoreStackFromTop<T>(Stack<T> stack, scoped ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
            return;
        ref T source = ref MemoryMarshal.GetReference(items);
        int count = items.Length;
        if (StackStorage.Available && stack.GetType() == typeof(Stack<T>) && stack.Count == 0)
        {
            var array = LuminFieldLayout.Field<T[]>(stack, LuminCollectionLayouts.StackArray);
            if (count <= array.Length && !items.Overlaps(new ReadOnlySpan<T>(array)))
            {
                ref T target = ref LuminPackMarshal.GetNotNullArrayReference(array);
                int index = 0;
                do
                {
                    Unsafe.Add(ref target, (nint)(uint)index) = Unsafe.Add(ref source, (nint)(uint)(count - index - 1));
                } while (++index < count);
                PublishStackSize(stack, count);
                return;
            }
        }
        int remaining = count;
        do
        {
            stack.Push(Unsafe.Add(ref source, (nint)(uint)--remaining));
        } while (remaining != 0);
    }

    /// <summary>Publishes a complete empty-target fill with the version change of one push per item.</summary>
    /// <typeparam name="T">The stack element.</typeparam>
    /// <param name="stack">The validated target stack.</param>
    /// <param name="count">The number of initialized slots.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PublishStackSize<T>(Stack<T> stack, int count)
    {
        LuminFieldLayout.Field<int>(stack, LuminCollectionLayouts.StackSize) = count;
        ref int version = ref LuminFieldLayout.Field<int>(stack, LuminCollectionLayouts.StackVersion);
        version = unchecked(version + count);
    }

    /// <summary>Writes integer stack storage in pop order, checking the original version across output callbacks.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <typeparam name="T">The exact framework integer type.</typeparam>
    /// <param name="writer">The writer after the opening bracket.</param>
    /// <param name="stack">The nonempty stack.</param>
    /// <returns>Whether the UTF-8 profile and exact stack storage were available.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteIntegerStackJson<TOptions, T>(scoped ref LuminPackJsonWriter writer, Stack<T> stack)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        if (default(TOptions).StringEncoding != LuminPackStringEncoding.UTF8 ||
            (typeof(T) != typeof(int) && typeof(T) != typeof(long)))
            return false;
        int count = stack.Count;
        if (!TryGetStackArray(stack, count, out var array))
            return false;
        ref int version = ref LuminFieldLayout.Field<int>(stack, LuminCollectionLayouts.StackVersion);
        var items = LuminPackMarshal.CreateReadOnlySpan(ref LuminPackMarshal.GetNotNullArrayReference(array), count);
        WriteOrderedIntegerItems<TOptions, T>(ref writer, items, ref version, version, reverse: true);
        return true;
    }

    /// <summary>Obtains exact, validated stack storage with the caller's captured active length.</summary>
    /// <typeparam name="T">The stack element.</typeparam>
    /// <param name="stack">The stack to inspect.</param>
    /// <param name="count">The captured active length.</param>
    /// <param name="array">The active storage on success.</param>
    /// <returns>Whether the backing array can be used.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetStackArray<T>(Stack<T> stack, int count, out T[] array)
    {
        if (count <= 0 || !StackStorage.Available || stack.GetType() != typeof(Stack<T>) || stack.Count != count)
        {
            array = Array.Empty<T>();
            return false;
        }
        array = LuminFieldLayout.Field<T[]>(stack, LuminCollectionLayouts.StackArray);
        return (uint)count <= (uint)array.Length;
    }

    /// <summary>Checks the array, size and version slots against public stack operations.</summary>
    private static class StackStorage
    {
        internal static readonly bool Available = Check();

        /// <summary>Returns the check for <c>StackStorage</c>.</summary>
        /// <returns>Whether the input satisfies the check check.</returns>
        private static bool Check()
        {
            if ((LuminCollectionLayouts.StackArray | LuminCollectionLayouts.StackSize | LuminCollectionLayouts.StackVersion) < 0)
                return false;
            try
            {
                return Check(19, 37) && Check("bottom", "top");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Handles check for <c>StackStorage</c>.</summary>
        /// <typeparam name="T">The value type processed by the operation.</typeparam>
        /// <param name="bottom">The bottom supplied to <c>Check</c>.</param>
        /// <param name="top">The top supplied to <c>Check</c>.</param>
        /// <returns>Whether the input satisfies the check check.</returns>
        private static bool Check<T>(T bottom, T top)
        {
            var stack = new Stack<T>(4);
            ref int size = ref LuminFieldLayout.Field<int>(stack, LuminCollectionLayouts.StackSize);
            ref int version = ref LuminFieldLayout.Field<int>(stack, LuminCollectionLayouts.StackVersion);
            int original = version;
            stack.Push(bottom);
            stack.Push(top);
            var items = LuminFieldLayout.Field<T[]>(stack, LuminCollectionLayouts.StackArray);
            var comparer = EqualityComparer<T>.Default;
            if (items.Length != 4 || size != 2 || version != unchecked(original + 2) ||
                !comparer.Equals(items[0], bottom) || !comparer.Equals(items[1], top))
                return false;
            stack.Clear();
            return size == 0 && version == unchecked(original + 3);
        }
    }
}
