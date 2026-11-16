using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
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
