using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Writes a native linked-list record directly from a verified circular node layout.</summary>
    /// <typeparam name="T">The native element type.</typeparam>
    /// <param name="writer">The current binary writer.</param>
    /// <param name="list">The source list.</param>
    /// <returns>Whether the record was written. False leaves the writer unchanged.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryWriteLinkedListRaw<T>(scoped ref LuminPackWriter writer, LinkedList<T> list) where T : unmanaged
    {
        int count = list.Count;
        int width = Unsafe.SizeOf<T>();
        if (!LinkedListLinking.Available || list.GetType() != typeof(LinkedList<T>) || list.First is not { } node ||
            count == 0 || writer.Position < 0 || writer.RecordRoom() < sizeof(int) + (long)count * width)
            return false;

        int header = writer.BeginCollection();
        int written = 0;
        ref byte cursor = ref writer.CurrentReference;
        do
        {
            var item = node.Value;
            LuminPackWriter.Write(ref cursor, in item);
            cursor = ref Unsafe.Add(ref cursor, (nint)(uint)width);
            node = NextNode(node);
        }
        while (++written < count && node is not null);
        writer.CompleteRun(written * width);
        writer.EndCollection(header, written);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Writes a linked list of strings in ring order under a callback-free string profile.</summary>
    /// <typeparam name="TProfile">The compiled string profile.</typeparam>
    /// <param name="writer">The current binary writer.</param>
    /// <param name="list">The source list.</param>
    /// <returns>Whether the record was written. False leaves the writer unchanged.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryWriteLinkedListStrings<TProfile>(scoped ref LuminPackWriter writer, LinkedList<string?> list)
        where TProfile : struct, ILuminPackOptions
    {
        // A caller-owned profile can run user code during an element write. Leave that path on the public node walk.
        if (!(typeof(TProfile) == typeof(LuminPackUtf16LengthOptions) || typeof(TProfile) == typeof(LuminPackUtf8LengthOptions) ||
              typeof(TProfile) == typeof(LuminPackUtf16TokenOptions) || typeof(TProfile) == typeof(LuminPackUtf8TokenOptions) ||
              typeof(TProfile) == typeof(LuminPackUtf16VarIntOptions) || typeof(TProfile) == typeof(LuminPackUtf8VarIntOptions) ||
              typeof(TProfile) == typeof(LuminPackDefaultOptions) || typeof(TProfile) == typeof(LuminPackDeclaredOptions)) ||
            !LinkedListLinking.Available || list.GetType() != typeof(LinkedList<string>) || list.First is not { } node)
            return false;

        int count = list.Count;
        if (count == 0)
            return false;
        int header = writer.BeginCollection();
        int written = 0;
        do
        {
            writer.Write<TProfile>(node.Value);
            node = NextNode(node);
        }
        while (++written < count && node is not null);
        writer.EndCollection(header, written);
        writer.CheckBuffer();
        return true;
    }

    /// <summary>Writes fixed integer groups from a verified linked list without enumerator state.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <typeparam name="T">The native Int32 or Int64 element.</typeparam>
    /// <param name="writer">The writer within an open JSON array.</param>
    /// <param name="list">The source list.</param>
    /// <returns>Whether the list was traversed and written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteIntegerLinkedListJson<TOptions, T>(scoped ref LuminPackJsonWriter writer, LinkedList<T> list)
        where TOptions : struct, ILuminPackOptions where T : unmanaged
    {
        bool isInt = typeof(T) == typeof(int);
        if (!LuminPackMarshal.ConstantStringProfile<TOptions>() ||
            (!isInt && typeof(T) != typeof(long)) ||
            !LinkedListReadLayout<T>.CanLink || list.GetType() != typeof(LinkedList<T>) || list.First is not { } node)
            return false;

        var encoding = default(TOptions).StringEncoding;
        if (encoding is not (LuminPackStringEncoding.UTF8 or LuminPackStringEncoding.UTF16))
            return false;

        bool utf8 = encoding == LuminPackStringEncoding.UTF8;
        int maximum = (isInt ? 12 : 21) * (utf8 ? 1 : 2);
        ref int version = ref LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.LinkedListVersion);
        int captured = version;
        int remaining = list.Count;
        Span<T> staged = stackalloc T[256];
        do
        {
            int position = writer.Position;
            int room = writer.Buffer.Length - position;
            int count = position >= 0 && room >= maximum && (utf8 || CultureInfo.CurrentCulture.GetType() == typeof(CultureInfo))
                ? Math.Min(Math.Min(remaining, staged.Length), room / maximum) : 0;
            if (count == 0)
            {
                T item = node.Value;
                WriteOrderedIntegerBoundary<TOptions, T>(ref writer, item);
                if (captured != version)
                    LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
                node = NextNode(node)!;
                remaining--;
                continue;
            }
            // All staged elements fit even at their full scalar reserve widths, so the run cannot call the output owner.
            for (int index = 0; index < count; index++)
            {
                staged[index] = node.Value;
                node = NextNode(node)!;
            }
            var items = staged.Slice(0, count);
            if (isInt)
                writer.WriteIntegerRun<TOptions>(MemoryMarshal.Cast<T, int>(items), readBeforeSeparator: true);
            else
                writer.WriteIntegerRun<TOptions>(MemoryMarshal.Cast<T, long>(items), readBeforeSeparator: true);
            if (captured != version)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            remaining -= count;
        }
        while (remaining != 0);
        return true;
    }

    /// <summary>Reads the next node from the verified ring.</summary>
    /// <typeparam name="T">The linked-list element type.</typeparam>
    /// <param name="node">The current node.</param>
    /// <returns>The next node, which may point back to the head.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LinkedListNode<T>? NextNode<T>(LinkedListNode<T> node) =>
        LuminFieldLayout.Field<LinkedListNode<T>?>(node, LuminCollectionLayouts.LinkedNodeNext);

    /// <summary>Starts linking decoded items into an empty linked list as they are read.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The fresh or cleared destination.</param>
    /// <returns>A direct appender for an empty list of the exact type on a probed layout; otherwise an inactive appender,
    /// and the caller uses <see cref="FillLinkedList{T}"/>.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LinkedListAppender<T> BeginLinkedList<T>(LinkedList<T> list) =>
        list.Count == 0 && list.GetType() == typeof(LinkedList<T>) && LinkedListReadLayout<T>.CanAppendDirectly
            ? new LinkedListAppender<T>(list)
            : default;

    /// <summary>Caches the closed framework layout checks required by the two read paths.</summary>
    /// <typeparam name="T">The linked-list element type.</typeparam>
    private static class LinkedListReadLayout<T>
    {
        internal static readonly bool CanLink = CheckPhysicalLayout();
        internal static readonly bool CanAppendDirectly = CanLink && LinkedNodeView<T>.MatchesOffsets();

        /// <summary>Checks the exact list and node fields against the offsets verified by the ring probe.</summary>
        /// <returns>Whether raw field access can build a ring for this element type.</returns>
        private static bool CheckPhysicalLayout()
        {
            if (!LinkedListLinking.Available)
                return false;
            var list = typeof(LinkedList<T>);
            var node = typeof(LinkedListNode<T>);
            return LuminFieldLayout.ObjectField(list, "head") == LuminCollectionLayouts.LinkedListHead &&
                   LuminFieldLayout.ObjectField(list, "count") == LuminCollectionLayouts.LinkedListCount &&
                   LuminFieldLayout.ObjectField(list, "version") == LuminCollectionLayouts.LinkedListVersion &&
                   LuminFieldLayout.ObjectField(node, "list") == LuminCollectionLayouts.LinkedNodeList &&
                   LuminFieldLayout.ObjectField(node, "next") == LuminCollectionLayouts.LinkedNodeNext &&
                   LuminFieldLayout.ObjectField(node, "prev") == LuminCollectionLayouts.LinkedNodePrevious;
        }
    }
}

/// <summary>Links new nodes, created by the public node constructor, in read order and publishes them to the list once.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>Nodes stay unreachable from the list until <see cref="Publish"/>, so a failed read leaves the list empty, as the
/// staged fill does. The result is the circular list that <c>AddLast</c> builds.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct LinkedListAppender<T>
{
    private readonly LinkedList<T>? _list;
    private LinkedListNode<T>? _first;
    private LinkedListNode<T>? _last;

    /// <summary>Creates an active appender for an empty list of the exact type.</summary>
    /// <param name="list">The destination.</param>
    internal LinkedListAppender(LinkedList<T> list)
    {
        _list = list;
        _first = null;
        _last = null;
    }

    /// <summary>Gets whether nodes are linked directly.</summary>
    public readonly bool Direct => _list is not null;

    /// <summary>Links a new node holding <paramref name="item"/> after the previous one.</summary>
    /// <param name="item">The decoded item.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T item)
    {
        var node = new LinkedListNode<T>(item);
        var view = Unsafe.As<LinkedNodeView<T>>(node);
        view.List = _list;
        if (_last is null)
            _first = node;
        else
        {
            view.Previous = _last;
            Unsafe.As<LinkedNodeView<T>>(_last).Next = node;
        }
        _last = node;
    }

    /// <summary>Closes the ring and publishes the head, count and version.</summary>
    /// <param name="count">The number of appended nodes.</param>
    public void Publish(int count)
    {
        if (_first is null || _last is null)
            return;
        Unsafe.As<LinkedNodeView<T>>(_last).Next = _first;
        Unsafe.As<LinkedNodeView<T>>(_first).Previous = _last;
        LuminFieldLayout.Field<object?>(_list!, LuminCollectionLayouts.LinkedListHead) = _first;
        LuminFieldLayout.Field<int>(_list!, LuminCollectionLayouts.LinkedListCount) = count;
        LuminFieldLayout.Field<int>(_list!, LuminCollectionLayouts.LinkedListVersion)++;
    }
}

/// <summary>Mirrors the link fields of an exact closed linked-list node after checking their offsets.</summary>
/// <typeparam name="T">The linked-list element type.</typeparam>
[StructLayout(LayoutKind.Auto)]
internal sealed class LinkedNodeView<T>
{
    internal LinkedList<T>? List;
    internal LinkedListNode<T>? Next;
    internal LinkedListNode<T>? Previous;
    internal T Item = default!;

    /// <summary>Checks each stored reference against the previously verified physical offsets.</summary>
    /// <returns>Whether the typed stores target the verified slots.</returns>
    internal static bool MatchesOffsets()
    {
        var view = new LinkedNodeView<T>();
        return LuminFieldLayout.OffsetOf(view, ref Unsafe.As<LinkedList<T>?, byte>(ref view.List)) == LuminCollectionLayouts.LinkedNodeList &&
               LuminFieldLayout.OffsetOf(view, ref Unsafe.As<LinkedListNode<T>?, byte>(ref view.Next)) == LuminCollectionLayouts.LinkedNodeNext &&
               LuminFieldLayout.OffsetOf(view, ref Unsafe.As<LinkedListNode<T>?, byte>(ref view.Previous)) == LuminCollectionLayouts.LinkedNodePrevious;
    }
}
