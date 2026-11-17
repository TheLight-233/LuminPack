using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>PriorityQueue&lt;TElement, TPriority&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The wire is the node count followed by each element and its priority in the heap's own node order, which the
/// unordered enumeration yields. The read creates a presized queue or clears the reused one and enqueues the nodes in the order
/// stored, which rebuilds the heap array that was written. The JSON form is an array of element and priority arrays.</remarks>
internal static class PriorityQueueOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(Write, Read, "value.Enqueue(key!, itemValue);",
        source: "value.UnorderedItems", keyMember: "Element", valueMember: "Priority", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed priority queue owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Writes heap order without allocating the unordered-items view when storage is recognized.</summary>
    /// <param name="c">The priority-queue formatter context.</param>
    private static void Write(PairedDictionaryContext c)
    {
        if (c.RawPairs && c.Supports("WritePriorityQueueStorage"))
        {
            c.W.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWritePriorityQueueStorage(ref operationState, value))");
            c.W.Line("    goto operationComplete_RootWrite;");
        }
        else if (c.UsesRawOrStringEntries && (c.Key == "string") != (c.Value == "string") &&
                 c.Supports("BinaryTextPair") && c.Supports("PriorityQueueTraversal"))
        {
            WriteMixed(c);
            return;
        }
        else if (!c.UsesRawOrStringEntries && c.Supports("PriorityQueueTraversal"))
        {
            const string access = "global::LuminPack.Code.LuminPackCollectionAccess";
            var w = c.W;
            w.Line("if (value.GetType() == typeof(" + c.Dictionary + ") && " + access +
                ".TryGetPriorityQueueTraversal(value, out var nodes, out int version))");
            w.Open();
            w.Line("operationState.EnsureAdditionalCapacity(sizeof(int));");
            w.Line("operationState.WriteCollectionHeader(nodes.Length);");
            w.Line(access + ".CheckPriorityQueueTraversalVersion(value, version);");
            w.Line("foreach (var pair in nodes)");
            w.Open();
            w.Line("var key = pair.Element;");
            c.WriteMember(c.Key, "key");
            w.Line("var itemValue = pair.Priority;");
            c.WriteMember(c.Value, "itemValue");
            w.Line(access + ".CheckPriorityQueueTraversalVersion(value, version);");
            w.Close();
            w.Line("operationState.CheckBuffer();");
            w.Line("goto operationComplete_RootWrite;");
            w.Close();
        }
        c.CountedWrite();
    }

    /// <summary>Writes mixed heap nodes with one room check per pair on recognized framework storage.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void WriteMixed(PairedDictionaryContext c)
    {
        var w = c.W;
        const string access = "global::LuminPack.Code.LuminPackCollectionAccess";
        const string writer = "global::LuminPack.Core.LuminPackWriter";
        string rawWidth = c.Memory + ".SizeOf<" + (c.Key == "string" ? c.Value : c.Key) + ">()";
        string text = c.Key == "string" ? "element" : "priority";
        w.Line("int position = operationState.BeginCollection();");
        w.Line("int count = 0;");
        w.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile + " &&");
        w.Line("    value.GetType() == typeof(" + c.Dictionary + ") &&");
        w.Line("    " + access + ".TryGetPriorityQueueTraversal(value, out var nodes, out int version))");
        w.Open();
        w.Line("count = nodes.Length;");
        w.Line("if (count != 0)");
        w.Open();
        w.Line("ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(nodes);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line(access + ".CheckPriorityQueueTraversalVersion(value, version);");
        w.Line("var (element, priority) = " + c.Memory + ".Add(ref first, (nint)(uint)index);");
        w.Line("if ((long)operationState.RecordRoom() - " + rawWidth + " >= " + writer + ".Utf16RecordBound(" + text + "))");
        w.Open();
        w.Line("ref byte record = ref operationState.Cursor;");
        if (c.Key == "string")
        {
            w.Line("int written = " + writer + ".WriteReservedUtf16Record(ref record, element);");
            w.Line(writer + ".Write(ref " + c.Memory + ".Add(ref record, (nint)(uint)written), in priority);");
        }
        else
        {
            w.Line(writer + ".Write(ref record, in element);");
            w.Line("int written = " + writer + ".WriteReservedUtf16Record(ref " + c.Memory + ".Add(ref record, " + rawWidth + "), priority);");
        }
        w.Line("operationState.CompleteRun(written + " + rawWidth + ");");
        w.Close();
        w.Line("else");
        w.Indented(() => w.Line("WritePair(ref operationState, element, priority);"));
        w.Close();
        w.Line("while (++index < count);");
        w.Close();
        w.Line(access + ".CheckPriorityQueueTraversalVersion(value, version);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("foreach (var pair in value.UnorderedItems)");
        w.Open();
        w.Line("count++;");
        w.Line("WritePair(ref operationState, pair.Element, pair.Priority);");
        w.Close();
        w.Close();
        w.Line("operationState.EndCollection(position, count);");
        w.Line("operationState.CheckBuffer();");
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void WritePair(scoped ref " + writer + " operationState, " + c.Key + " element, " + c.Value + " priority)");
        w.Open();
        c.WriteMember(c.Key, "element");
        c.WriteMember(c.Value, "priority");
        w.Close();
    }

    /// <summary>Handles read for <c>PriorityQueueOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Dictionary + "(length);"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        if (c.RawPairs && c.Supports("PriorityQueueTraversal") && c.Supports("ReadPriorityQueueStorage"))
        {
            w.Line("if (length != 0 && global::LuminPack.Code.LuminPackCollectionAccess.TryReadPriorityQueueStorage(ref operationState, value, length))");
            w.Line("    goto operationComplete_RootRead;");
        }
        w.Line("if (length == 0) goto operationComplete_RootRead;");
        w.Line("int remaining = length;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " element = default !;");
        w.Line(c.Value + " priority = default !;");
        c.ReadMember(c.Key, "element");
        c.ReadMember(c.Value, "priority");
        w.Line("value.Enqueue(element!, priority);");
        w.Close();
        w.Line("while (--remaining != 0);");
    }
}
