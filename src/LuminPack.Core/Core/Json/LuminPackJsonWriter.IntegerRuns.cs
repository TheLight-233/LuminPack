using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Option;

namespace LuminPack.Core;

#if NET8_0_OR_GREATER
public ref partial struct LuminPackJsonWriter
#else
public unsafe ref partial struct LuminPackJsonWriter
#endif
{
    /// <summary>Writes consecutive integer elements after the caller has opened an array.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="items">The values in wire order.</param>
    /// <param name="readBeforeSeparator">Whether the original traversal copies an item before writing its separator.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteIntegerRun<TOptions>(scoped ReadOnlySpan<int> items, bool readBeforeSeparator)
        where TOptions : struct, ILuminPackOptions
    {
        if (items.Length == 1 && IsFirstElement)
            Write<TOptions>(items[0]);
        else if (!items.IsEmpty)
            WriteIntegerRunCore<TOptions, int>(items, readBeforeSeparator);
    }

    /// <summary>Writes consecutive long elements after the caller has opened an array.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="items">The values in wire order.</param>
    /// <param name="readBeforeSeparator">Whether the original traversal copies an item before writing its separator.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteIntegerRun<TOptions>(scoped ReadOnlySpan<long> items, bool readBeforeSeparator)
        where TOptions : struct, ILuminPackOptions
    {
        if (items.Length == 1 && IsFirstElement)
            Write<TOptions>(items[0]);
        else if (!items.IsEmpty)
            WriteIntegerRunCore<TOptions, long>(items, readBeforeSeparator);
    }

    /// <summary>Writes an ordered integer run that fits the current window.</summary>
    /// <typeparam name="T">The exact framework integer type.</typeparam>
    /// <param name="items">The remaining values in storage order.</param>
    /// <param name="reverse">Whether the collection enumerates its storage backward.</param>
    /// <returns>The number of values written without calling the output owner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int WriteOrderedIntegerWindow<T>(scoped ReadOnlySpan<T> items, bool reverse) where T : unmanaged
    {
        if (items.IsEmpty)
            return 0;
        ref T first = ref MemoryMarshal.GetReference(items);
        ref byte start = ref Current;
#if NET8_0_OR_GREATER
        long room = (long)Unsafe.ByteOffset(ref start, ref _end);
#else
        long room = (long)unchecked((nint)_end - (nint)Cursor);
#endif
        int digits = typeof(T) == typeof(int) ? 11 : 20;
        int maximum = digits + 1;
        if (room < maximum || IntegerRunOverlaps(items, ref start, room))
            return 0;
        int written = 0;
        int separator = IsFirstElement ? 0 : 1;
        int step = reverse ? -1 : 1;
        int index = reverse ? items.Length - 1 : 0;
        do
        {
            T item = Unsafe.Add(ref first, (nint)(uint)index);
            ref byte output = ref Unsafe.Add(ref start, (nint)(uint)written);
            output = Comma;
            ref byte number = ref Unsafe.Add(ref output, separator);
            written += separator + (typeof(T) == typeof(int)
                ? LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref number, 11), Unsafe.As<T, int>(ref item))
                : LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref number, 20), Unsafe.As<T, long>(ref item)));
            separator = 1;
            index += step;
        } while ((uint)index < (uint)items.Length && room - written >= maximum);
        Advance(written);
        IsFirstElement = false;
        return reverse ? items.Length - 1 - index : index;
    }

    /// <summary>Writes a run using a local output offset until the next element needs a capacity check.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <typeparam name="T">An integer type selected by the overload.</typeparam>
    /// <param name="items">The values in wire order.</param>
    /// <param name="readBeforeSeparator">Whether to capture the next item before a separate capacity check.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteIntegerRunCore<TOptions, T>(scoped ReadOnlySpan<T> items, bool readBeforeSeparator)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
        int scale = utf8 ? 1 : 2;
        int digits = typeof(T) == typeof(int) ? 11 : 20;
        int maximum = (digits + 1) * scale;
        int index = 0;
        ref T first = ref MemoryMarshal.GetReference(items);
        if (IsFirstElement)
        {
            WriteIntegerRunValue<TOptions, T>(first);
            index = 1;
        }
        while (index < items.Length)
        {
            // Each format call uses at most its fixed bound, including any overstore inside the numeric kernel.
            ref byte start = ref Current;
#if NET8_0_OR_GREATER
            long room = (long)Unsafe.ByteOffset(ref start, ref _end);
#else
            long room = (long)unchecked((nint)_end - (nint)Cursor);
#endif
            int written = 0;
            // A derived culture can call user code during formatting. Overlap also needs the scalar load order.
            if (room >= maximum && (utf8 || CultureInfo.CurrentCulture.GetType() == typeof(CultureInfo)) &&
                !IntegerRunOverlaps(items.Slice(index), ref start, room))
            {
                do
                {
                    T item = Unsafe.Add(ref first, (nint)(uint)index++);
                    ref byte output = ref Unsafe.Add(ref start, (nint)(uint)written);
                    if (utf8)
                    {
                        output = Comma;
                        var destination = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, 1), digits);
                        written += 1 + (typeof(T) == typeof(int)
                            ? LuminPackJsonNumberFormatter.WriteInt32(destination, Unsafe.As<T, int>(ref item))
                            : LuminPackJsonNumberFormatter.WriteLong(destination, Unsafe.As<T, long>(ref item)));
                    }
                    else
                    {
                        Unsafe.WriteUnaligned(ref output, ',');
                        ref char next = ref Unsafe.As<byte, char>(ref Unsafe.Add(ref output, 2));
                        var destination = MemoryMarshal.CreateSpan(ref next, digits);
                        int count;
                        if (typeof(T) == typeof(int))
                            Unsafe.As<T, int>(ref item).TryFormat(destination, out count);
                        else
                            Unsafe.As<T, long>(ref item).TryFormat(destination, out count);
                        written += (1 + count) << 1;
                    }
                } while (index < items.Length && room - written >= maximum);
                Advance(written);
            }
            if (index == items.Length)
                return;
            WriteIntegerRunElement<TOptions, T>(in items[index++], readBeforeSeparator);
        }
    }

    /// <summary>Checks whether writing the current window can change a remaining input value.</summary>
    /// <typeparam name="T">The integer storage type.</typeparam>
    /// <param name="items">The remaining values.</param>
    /// <param name="start">The writable cursor.</param>
    /// <param name="room">The remaining writable byte count.</param>
    /// <returns>Whether the input and output ranges intersect.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IntegerRunOverlaps<T>(ReadOnlySpan<T> items, ref byte start, long room) where T : unmanaged
    {
        ref byte input = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(items));
        long offset = (long)Unsafe.ByteOffset(ref start, ref input);
        return offset < room && offset > -(long)items.Length * Unsafe.SizeOf<T>();
    }

    /// <summary>Writes a scalar at a window or callback boundary in the original traversal order.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <typeparam name="T">The integer storage type.</typeparam>
    /// <param name="item">The source element.</param>
    /// <param name="readBeforeSeparator">Whether to copy the item before reporting a completed output window.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteIntegerRunElement<TOptions, T>(scoped in T item, bool readBeforeSeparator)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        T next = readBeforeSeparator ? item : default;
        if (!IsFirstElement)
            WriteByteRaw<TOptions>(Comma);
        IsFirstElement = true;
        if (!readBeforeSeparator)
            next = item;
        WriteIntegerRunValue<TOptions, T>(next);
    }

    /// <summary>Dispatches to the existing scalar writer for the selected integer type.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <typeparam name="T">The integer storage type.</typeparam>
    /// <param name="next">The copied value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteIntegerRunValue<TOptions, T>(T next)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        if (typeof(T) == typeof(int))
            Write<TOptions>(Unsafe.As<T, int>(ref next));
        else
            Write<TOptions>(Unsafe.As<T, long>(ref next));
    }
}
