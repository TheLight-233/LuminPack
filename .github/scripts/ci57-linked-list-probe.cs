using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LuminPack.Code;

// The main int/string matrix case uses this builder capability.
var direct = typeof(LuminPackCollectionAccess).GetMethod("ConcurrentDictionaryDirect", BindingFlags.NonPublic | BindingFlags.Static)!;
Console.WriteLine(JsonSerializer.Serialize(new { Kind = "Probe", Processors = Environment.ProcessorCount,
    Direct = direct.MakeGenericMethod(typeof(int), typeof(string)).Invoke(null, null) }));
Run("string", "sample");
Run("object", new Element());

static void Run<T>(string type, T item) where T : class
{
    foreach (int n in new[] { 3, 130, 4096 })
    {
        Func<LinkedList<T>>[] builders = [Current, Public, Typed];
        string[] names = ["stock", "AddLast", "typed"];
        foreach (var build in builders)
        {
            var list = build();
            int reverseCount = 0;
            for (var node = list.Last; node is not null; node = node.Previous)
            {
                if (++reverseCount > n || !ReferenceEquals(node.List, list) || !ReferenceEquals(node.Value, item))
                    throw new InvalidOperationException("Invalid reverse links.");
            }
            if (list.Count != n || reverseCount != n || list.First?.Previous != null || list.Last?.Next != null ||
                list.Any(x => !ReferenceEquals(x, item)))
                throw new InvalidOperationException("Invalid linked list.");
        }
        for (int round = -1; round < 9; round++)
        for (int j = 0; j < 3; j++)
        {
            int id = (j + round + builders.Length) % builders.Length;
            int calls = Math.Max(64, 240_000 / n);
            var build = builders[id];
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int k = 0; k < calls; k++) GC.KeepAlive(build());
            double ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds / calls;
            double bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)calls;
            if (round >= 0) Console.WriteLine(JsonSerializer.Serialize(new { type, n, round, method = names[id], ns, bytes }));
        }
        LinkedList<T> Current()
        {
            var list = new LinkedList<T>();
            var appender = LuminPackCollectionAccess.BeginLinkedList(list);
            if (!appender.Direct) throw new InvalidOperationException();
            for (int i = 0; i < n; i++) appender.Append(item);
            appender.Publish(n);
            return list;
        }
        LinkedList<T> Public()
        {
            var list = new LinkedList<T>();
            for (int i = 0; i < n; i++) list.AddLast(item);
            return list;
        }
        LinkedList<T> Typed()
        {
            var list = new LinkedList<T>();
            var first = Link<T>.New(list, item);
            var last = first;
            for (int i = 1; i < n; i++)
            {
                var node = Link<T>.New(list, item);
                Link<T>.Prev(node) = last;
                Link<T>.Next(last) = node;
                last = node;
            }
            Link<T>.Next(last) = first;
            Link<T>.Prev(first) = last;
            Link<T>.Head(list) = first;
            Link<T>.Count(list) = n;
            Link<T>.Version(list)++;
            return list;
        }
    }
}
sealed class Element;
static class Link<T>
{
    /// <summary>Constructs a node already owned by the list.</summary>
    /// <param name="list">The owning list.</param>
    /// <param name="item">The stored value.</param>
    /// <returns>The new node.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)] internal static extern LinkedListNode<T> New(LinkedList<T> list, T item);
    /// <summary>Accesses the circular link used by the typed builder.</summary>
    /// <param name="node">The node being linked.</param>
    /// <returns>A reference to the successor field.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "next")] internal static extern ref LinkedListNode<T>? Next(LinkedListNode<T> node);
    /// <summary>Accesses the circular link used by the typed builder.</summary>
    /// <param name="node">The node being linked.</param>
    /// <returns>A reference to the predecessor field.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "prev")] internal static extern ref LinkedListNode<T>? Prev(LinkedListNode<T> node);
    /// <summary>Accesses the list field published by the typed builder.</summary>
    /// <param name="list">The list being built.</param>
    /// <returns>A reference to the head node field.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "head")] internal static extern ref LinkedListNode<T>? Head(LinkedList<T> list);
    /// <summary>Accesses the list field published by the typed builder.</summary>
    /// <param name="list">The list being built.</param>
    /// <returns>A reference to the element count field.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "count")] internal static extern ref int Count(LinkedList<T> list);
    /// <summary>Accesses the list field published by the typed builder.</summary>
    /// <param name="list">The list being built.</param>
    /// <returns>A reference to the mutation version field.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "version")] internal static extern ref int Version(LinkedList<T> list);
}
