using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        LinkedListLinking.Available && list.Count == 0 && list.GetType() == typeof(LinkedList<T>)
            ? new LinkedListAppender<T>(list)
            : default;
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
        LuminFieldLayout.Field<object?>(node, LuminCollectionLayouts.LinkedNodeList) = _list;
        if (_last is null)
            _first = node;
        else
        {
            LuminFieldLayout.Field<object?>(node, LuminCollectionLayouts.LinkedNodePrevious) = _last;
            LuminFieldLayout.Field<object?>(_last, LuminCollectionLayouts.LinkedNodeNext) = node;
        }
        _last = node;
    }

    /// <summary>Closes the ring and publishes the head, count and version.</summary>
    /// <param name="count">The number of appended nodes.</param>
    public void Publish(int count)
    {
        if (_first is null || _last is null)
            return;
        LuminFieldLayout.Field<object?>(_last, LuminCollectionLayouts.LinkedNodeNext) = _first;
        LuminFieldLayout.Field<object?>(_first, LuminCollectionLayouts.LinkedNodePrevious) = _last;
        LuminFieldLayout.Field<object?>(_list!, LuminCollectionLayouts.LinkedListHead) = _first;
        LuminFieldLayout.Field<int>(_list!, LuminCollectionLayouts.LinkedListCount) = count;
        LuminFieldLayout.Field<int>(_list!, LuminCollectionLayouts.LinkedListVersion)++;
    }
}
