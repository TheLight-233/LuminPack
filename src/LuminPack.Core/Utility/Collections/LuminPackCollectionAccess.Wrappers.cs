using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Writes a wrapped string sequence with the same capture and count publication as its enumerator.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="writer">The original collection writer.</param>
    /// <param name="wrapper">The wrapper to inspect.</param>
    /// <returns>Whether the complete collection was written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteWrappedStrings<TOptions>(scoped ref LuminPackWriter writer, IEnumerable<string?> wrapper)
        where TOptions : struct, ILuminPackOptions
    {
        if (!LuminPackMarshal.ConstantStringProfile<TOptions>() || !writer.UsesUtf16LengthRecords<TOptions>())
            return false;
        object? storage = GetWrapperStorage(wrapper);
        if (storage is string?[] array)
        {
            int position = writer.BeginCollection();
            writer.WriteStrings<TOptions>(array);
            writer.EndCollection(position, array.Length);
            writer.CheckBuffer();
            return true;
        }
        if (!TryGetVersionedWrapperStorage<string?>(storage, out int itemsOffset, out int versionOffset))
            return false;

        int header = writer.BeginCollection();
        int count = GetVersionedWrapperCount<string?>(storage!);
        ref int version = ref LuminFieldLayout.Field<int>(storage!, versionOffset);
        ref var arrayField = ref LuminFieldLayout.Field<string?[]>(storage!, itemsOffset);
        int captured = version, index = 0;
        while (index < count)
        {
            var items = arrayField.AsSpan(0, count);
            index += writer.WriteStringPrefix<TOptions>(items.Slice(index));
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            if (index == count)
                break;
            writer.Write<TOptions>(items[index++]);
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
        }
        writer.EndCollection(header, count);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Writes integers directly from the storage selected by an exact wrapper's enumeration.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <typeparam name="T">The integer element type.</typeparam>
    /// <param name="writer">The writer immediately after opening the JSON array.</param>
    /// <param name="wrapper">The wrapper to inspect.</param>
    /// <returns>Whether a checked storage traversal was selected.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteWrappedIntegers<TOptions, T>(scoped ref LuminPackJsonWriter writer, IEnumerable<T> wrapper)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        if (!LuminPackMarshal.ConstantStringProfile<TOptions>() ||
            default(TOptions).StringEncoding != LuminPackStringEncoding.UTF8 ||
            (typeof(T) != typeof(int) && typeof(T) != typeof(long)))
            return false;

        object? storage = GetWrapperStorage(wrapper);
        if (storage is T[] array)
        {
            if (typeof(T) == typeof(int))
                writer.WriteIntegerRun<TOptions>(Unsafe.As<int[]>(array), readBeforeSeparator: true);
            else
                writer.WriteIntegerRun<TOptions>(Unsafe.As<long[]>(array), readBeforeSeparator: true);
            return true;
        }
        if (!TryGetVersionedWrapperStorage<T>(storage, out int itemsOffset, out int versionOffset))
            return false;

        ref int version = ref LuminFieldLayout.Field<int>(storage!, versionOffset);
        ref var items = ref LuminFieldLayout.Field<T[]>(storage!, itemsOffset);
        WriteWrappedIntegerStorage<TOptions, T>(ref writer, ref items, GetVersionedWrapperCount<T>(storage!), ref version, version);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteWrappedIntegerStorage<TOptions, T>(scoped ref LuminPackJsonWriter writer,
        scoped ref T[] arrayField, int count, scoped ref int version, int captured)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        int index = 0;
        while (index < count)
        {
            // Capacity may replace the array without changing the enumerator's version.
            var items = arrayField.AsSpan(index, count - index);
            int written = writer.WriteOrderedIntegerWindow<T>(items, reverse: false);
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            index += written;
            if (index == count)
                return;
            WriteOrderedIntegerBoundary<TOptions, T>(ref writer, items[written]);
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetVersionedWrapperCount<T>(object storage) =>
        storage is List<T> list ? list.Count : ((ReadOnlyCollectionBuilder<T>)storage).Count;

    private static object? GetWrapperStorage<T>(IEnumerable<T> wrapper) =>
        wrapper is ReadOnlyCollectionBuilder<T> ? wrapper : GetSequenceStorage(wrapper);

    private static bool TryGetVersionedWrapperStorage<T>(object? storage, out int itemsOffset, out int versionOffset)
    {
        if (storage is List<T>)
        {
            itemsOffset = LuminCollectionLayouts.ListItems;
            versionOffset = LuminCollectionLayouts.ListVersion;
        }
        else if (storage is ReadOnlyCollectionBuilder<T>)
        {
            itemsOffset = LuminCollectionLayouts.BuilderItems;
            versionOffset = BuilderVersion<T>.Offset;
        }
        else
        {
            itemsOffset = versionOffset = -1;
            return false;
        }
        return (itemsOffset | versionOffset) >= 0;
    }

    private static class BuilderVersion<T>
    {
        internal static readonly int Offset = Probe();

        private static int Probe()
        {
            try
            {
                int offset = LuminFieldLayout.ObjectField(typeof(ReadOnlyCollectionBuilder<T>), "_version", 0x65A2);
                if (offset < 0 || LuminCollectionLayouts.BuilderItems < 0)
                    return -1;

                var builder = new ReadOnlyCollectionBuilder<T>();
                ref int version = ref LuminFieldLayout.Field<int>(builder, offset);
                int initial = version;
                builder.Add(default!);
                var items = LuminFieldLayout.Field<T[]>(builder, LuminCollectionLayouts.BuilderItems);
                if (version == initial || items.Length != builder.Capacity)
                    return -1;

                int added = version;
                builder.Capacity++;
                if (version != added)
                    return -1;
                builder.Clear();
                return version != added ? offset : -1;
            }
            catch
            {
                return -1;
            }
        }
    }
}
