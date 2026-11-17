using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

public static partial class LuminPackMarshal
{
    /// <summary>Encodes a string set in enumeration order, using one cursor update for each proven output window.</summary>
    /// <typeparam name="TOptions">A built-in UTF-16 length profile selected by the caller.</typeparam>
    /// <param name="writer">The output state before the header.</param>
    /// <param name="set">The set being written.</param>
    /// <returns>Whether the built-in profile's exact framework set was written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteStringSet<TOptions>(ref LuminPackWriter writer, HashSet<string?> set)
        where TOptions : struct, ILuminPackOptions
    {
        if (set.GetType() != typeof(HashSet<string>) || !writer.UsesUtf16LengthRecords<TOptions>())
            return false;
        if (!TryWriteDenseStringSet<TOptions>(ref writer, set))
            WriteEnumeratedStringSet<TOptions>(ref writer, set);
        return true;
    }

    /// <summary>Encodes dense entries without loading hash codes or following the free list.</summary>
    /// <typeparam name="TOptions">The built-in UTF-16 length profile.</typeparam>
    /// <param name="writer">The output state before the header.</param>
    /// <param name="set">The exact framework set.</param>
    /// <returns>Whether the dense layout and entry range were proved and written.</returns>
    [MethodImpl(global::LuminPack.Utility.LuminPackJit.Optimized)]
    private static bool TryWriteDenseStringSet<TOptions>(ref LuminPackWriter writer, HashSet<string?> set)
        where TOptions : struct, ILuminPackOptions
    {
        int count = set.Count;
        if (IntPtr.Size != 8 || count < 16 ||
            (LuminCollectionLayouts.HashSetCount | LuminCollectionLayouts.HashSetFreeCount | LuminCollectionLayouts.HashSetVersion) < 0 ||
            !HashSetView<string?>.Recognized)
            return false;
        var view = new HashSetView<string?>(set);
        var entries = view._entries;
        if (view._count != count || view._freeCount != 0 || entries is null || count > entries.Length)
            return false;

        int version = view._version;
        int header = writer.BeginCollection();
        ref var first = ref GetNotNullArrayReference(entries);
        int index = 0;
        do
        {
            int room = writer.RecordRoom();
            int written = 0;
            ref byte start = ref writer.CurrentReference;
            while (index < count)
            {
                // A typed reference load keeps the string tracked by the GC across encoding calls.
                string? item = Unsafe.Add(ref first, (nint)(uint)index).Value;
                if (LuminPackWriter.Utf16RecordBound(item) > (long)room - written)
                    break;
                written += LuminPackWriter.WriteReservedUtf16Record(ref Unsafe.Add(ref start, (nint)(uint)written), item);
                index++;
            }
            writer.CompleteRun(written);
            if (index < count)
                writer.Write<TOptions>(Unsafe.Add(ref first, (nint)(uint)index++).Value);
        } while (index < count);

        if (view._version != version || view._count != count || view._freeCount != 0 || !ReferenceEquals(view._entries, entries))
            LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
        writer.EndCollection(header, count);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Encodes strings in bounded runs using the original enumerator for sparse or unknown layouts.</summary>
    /// <typeparam name="TOptions">The built-in UTF-16 length profile.</typeparam>
    /// <param name="writer">The output state before the header.</param>
    /// <param name="set">The exact framework set.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteEnumeratedStringSet<TOptions>(ref LuminPackWriter writer, HashSet<string?> set)
        where TOptions : struct, ILuminPackOptions
    {
        int header = writer.BeginCollection();
        int count = 0;
        var items = set.GetEnumerator();
        bool more = items.MoveNext();
        while (more)
        {
            int room = writer.RecordRoom();
            int written = 0;
            ref byte start = ref writer.CurrentReference;
            do
            {
                string? item = items.Current;
                if (LuminPackWriter.Utf16RecordBound(item) > (long)room - written)
                    break;
                written += LuminPackWriter.WriteReservedUtf16Record(ref Unsafe.Add(ref start, (nint)(uint)written), item);
                count++;
                more = items.MoveNext();
            } while (more);
            writer.CompleteRun(written);
            if (more)
            {
                writer.Write<TOptions>(items.Current);
                count++;
                more = items.MoveNext();
            }
        }
        writer.EndCollection(header, count);
        writer.CheckBuffer();
    }
}
