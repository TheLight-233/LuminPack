using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Captures the version needed to traverse a collection builder without allocating an enumerator.</summary>
    /// <typeparam name="T">The builder element type.</typeparam>
    /// <param name="builder">The source builder.</param>
    /// <param name="traversal">The captured traversal when the layout is recognized.</param>
    /// <returns>Whether the generated caller can use the value traversal.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryGetBuilderTraversal<T>(ReadOnlyCollectionBuilder<T>? builder, out BuilderTraversal<T> traversal)
    {
        int offset = BuilderVersion<T>.Offset;
        if (offset >= 0 && builder is not null)
        {
            traversal = new BuilderTraversal<T>(builder, offset);
            return true;
        }
        traversal = default;
        return false;
    }

    /// <summary>Reads the next builder item at the original version and capture boundary.</summary>
    /// <typeparam name="T">The builder element type.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct BuilderTraversal<T>
    {
        private readonly ReadOnlyCollectionBuilder<T>? _builder;
        private readonly int _versionOffset;
        private readonly int _version;
        private int _index;

        /// <summary>Initializes a <c>BuilderTraversal</c> from the supplied data.</summary>
        /// <param name="builder">The builder receiving the result.</param>
        /// <param name="versionOffset">The version offset supplied to <c>BuilderTraversal</c>.</param>
        internal BuilderTraversal(ReadOnlyCollectionBuilder<T> builder, int versionOffset)
        {
            _builder = builder;
            _versionOffset = versionOffset;
            _version = LuminFieldLayout.Field<int>(builder, versionOffset);
            _index = 0;
        }

        /// <summary>Checks the version, then captures an element from the builder's current storage.</summary>
        /// <param name="item">The captured item, when available.</param>
        /// <returns>Whether another element was captured.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out T item)
        {
            var builder = _builder;
            if (builder is null)
            {
                item = default!;
                return false;
            }
            if (LuminFieldLayout.Field<int>(builder, _versionOffset) != _version)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            if (_index >= builder.Count)
            {
                item = default!;
                return false;
            }

            // A capacity change may replace the array without changing the version.
            item = builder[_index++];
            return true;
        }
    }

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
        object? storage = GetObservableStringList(wrapper) ?? GetWrapperStorage(wrapper);
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
            var remaining = new ReadOnlySpan<string?>(arrayField, index, count - index);
            int written = writer.WriteStringPrefix<TOptions>(remaining);
            index += written;
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            if (index == count)
                break;
            string? value = remaining[written];
            index++;
            writer.Write<TOptions>(value);
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
        }
        writer.EndCollection(header, count);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Finds the exact list behind a framework observable wrapper through the known layouts.</summary>
    /// <param name="wrapper">The wrapped string sequence.</param>
    /// <returns>The exact backing list, or null when the general storage probe is needed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<string?>? GetObservableStringList(IEnumerable<string?>? wrapper)
    {
        var type = wrapper?.GetType();
        if (type == typeof(ReadOnlyObservableCollection<string?>) && LuminCollectionLayouts.ReadOnlyObservableCollectionList >= 0)
        {
            wrapper = LuminFieldLayout.Field<IList<string?>?>(wrapper!, LuminCollectionLayouts.ReadOnlyObservableCollectionList);
            type = wrapper?.GetType();
        }
        if (type != typeof(ObservableCollection<string?>) || LuminCollectionLayouts.ObservableCollectionItems < 0)
            return null;

        var items = LuminFieldLayout.Field<IList<string?>?>(wrapper!, LuminCollectionLayouts.ObservableCollectionItems);
        return items?.GetType() == typeof(List<string?>) ? Unsafe.As<List<string?>>(items!) : null;
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
            (default(TOptions).JsonByteCount(1) == sizeof(char) &&
                CultureInfo.CurrentCulture.GetType() != typeof(CultureInfo)) ||
            (typeof(T) != typeof(int) && typeof(T) != typeof(long)))
            return false;

        if (wrapper is ReadOnlyCollectionBuilder<T> builder)
        {
            int builderItemsOffset = LuminCollectionLayouts.BuilderItems;
            int builderVersionOffset = BuilderVersion<T>.Offset;
            if ((builderItemsOffset | builderVersionOffset) < 0)
                return false;

            ref int builderVersion = ref LuminFieldLayout.Field<int>(builder, builderVersionOffset);
            ref var builderItems = ref LuminFieldLayout.Field<T[]>(builder, builderItemsOffset);
            WriteWrappedIntegerStorage<TOptions, T>(ref writer, ref builderItems, builder.Count, ref builderVersion, builderVersion);
            return true;
        }

        object? storage = GetSequenceStorage(wrapper);
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

    /// <summary>Writes wrapped integer storage.</summary>
    /// <typeparam name="TOptions">The compiled serialization options.</typeparam>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="writer">The writer receiving the output.</param>
    /// <param name="arrayField">The array field supplied to <c>WriteWrappedIntegerStorage</c>.</param>
    /// <param name="count">The number of items to process.</param>
    /// <param name="version">The version supplied to <c>WriteWrappedIntegerStorage</c>.</param>
    /// <param name="captured">The captured supplied to <c>WriteWrappedIntegerStorage</c>.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteWrappedIntegerStorage<TOptions, T>(scoped ref LuminPackJsonWriter writer,
        scoped ref T[] arrayField, int count, scoped ref int version, int captured)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        int index = 0;
        if (count == 0)
            return;
        do
        {
            // Capacity may replace the array without changing the enumerator's version.
            var items = new ReadOnlySpan<T>(arrayField, index, count - index);
            int written = writer.WriteOrderedIntegerWindow<TOptions, T>(items, reverse: false);
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            if (written == items.Length)
                return;
            WriteOrderedIntegerBoundary<TOptions, T>(ref writer, items[written]);
            if (version != captured)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            index += written + 1;
        } while (index < count);
    }

    /// <summary>Gets versioned wrapper count.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="storage">The storage supplied to <c>GetVersionedWrapperCount</c>.</param>
    /// <returns>The versioned wrapper count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetVersionedWrapperCount<T>(object storage) =>
        storage is List<T> list ? list.Count : ((ReadOnlyCollectionBuilder<T>)storage).Count;

    /// <summary>Gets wrapper storage.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="wrapper">The wrapper supplied to <c>GetWrapperStorage</c>.</param>
    /// <returns>The wrapper storage, or null when unavailable.</returns>
    private static object? GetWrapperStorage<T>(IEnumerable<T> wrapper) =>
        wrapper is ReadOnlyCollectionBuilder<T> ? wrapper : GetSequenceStorage(wrapper);

    /// <summary>Attempts to get versioned wrapper storage.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="storage">The storage supplied to <c>TryGetVersionedWrapperStorage</c>.</param>
    /// <param name="itemsOffset">Receives the items offset when the operation succeeds.</param>
    /// <param name="versionOffset">Receives the version offset when the operation succeeds.</param>
    /// <returns>Whether the operation succeeded.</returns>
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

        /// <summary>Returns the probe for <c>BuilderVersion</c>.</summary>
        /// <returns>The resulting <c>int</c>.</returns>
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
