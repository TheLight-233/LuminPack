using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>LinkedList&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy linked-list operations. The binary write walks the nodes directly instead of through
/// the version-checked enumerator and counts them. The binary read can link each decoded element directly or stage elements
/// before filling the list when the layout is unavailable. A nonempty destination refills its existing nodes first.</remarks>
internal static class LinkedListOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "value.AddLast(item);",
        readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec, nonEmptyRawHeaderBytes: sizeof(int));

    /// <summary>Handles write for <c>LinkedListOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(EnumerableOwnerContext c)
    {
        var w = c.W;
        if (c.Payload != null)
        {
            c.CountedWrite("value");
            return;
        }
        if ((c.Text || !c.Forwarded) && c.Supports("LinkedListRingWriter"))
        {
            string write = c.Text ? "TryWriteLinkedListStrings<TLuminProfile>" : "TryWriteLinkedListRaw";
            w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess." + write + "(ref operationState, value))");
            w.Indented(() => w.Line("goto operationComplete_RootWrite;"));
        }
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("int count = 0;");
        w.Line("var node = value.First;");
        if (!c.Text && !c.Forwarded && c.Supports("LinkedListRawWindow"))
        {
            w.Line("int expectedCount = value.Count;");
            w.Line("int width = " + c.Memory + ".SizeOf<" + c.Scalar + ">();");
            w.Line("if (node is not null && expectedCount > 0 && operationState.Position >= 0 &&");
            w.Line("    operationState.RecordRoom() >= (long)expectedCount * width)");
            w.Open();
            w.Line("ref byte cursor = ref operationState.Cursor;");
            w.Line("int remaining = expectedCount;");
            w.Line("do");
            w.Open();
            w.Line("var item = node.Value;");
            w.Line("global::LuminPack.Core.LuminPackWriter.Write(ref cursor, in item);");
            w.Line("cursor = ref " + c.Memory + ".Add(ref cursor, (nint)(uint)width);");
            w.Line("node = node.Next;");
            w.Close();
            w.Line("while (--remaining != 0 && node is not null);");
            w.Line("count = expectedCount - remaining;");
            w.Line("operationState.CompleteRun(count * width);");
            w.Close();
        }
        w.Line("if (node is not null)");
        w.Open();
        w.Line("do");
        w.Open();
        if (!c.Text)
            w.Line("operationState.CheckBuffer();");
        w.Line("count++;");
        w.Line("var v = node.Value;");
        // A Boolean element writes through a read-only alias, as the legacy element write does.
        if (c.Forwarded)
            c.WriteElement("v");
        else if (c.Text)
            w.Line("operationState.Write<TLuminProfile>(v);");
        else if (c.Scalar == "bool")
        {
            w.Line("ref readonly bool value2 = ref v;");
            w.Line("operationState.Write(in value2);");
        }
        else
            w.Line("operationState.Write(in v);");
        w.Line("node = node.Next;");
        w.Close();
        w.Line("while (node is not null);");
        w.Close();
        w.Blank();
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
    }

    /// <summary>Handles read for <c>LinkedListOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        // A reused list reads each element into its existing node in order, as a reused List<T> reads into its slots, then
        // appends missing nodes or removes surplus ones. A failed read exposes the completed prefix.
        w.Line("if (value is not null && length != 0 && value.First is { } reuseNode)");
        w.Open();
        w.Line("int reuseIndex = 0;");
        w.Line("do");
        w.Open();
        w.Line("var reuseItem = reuseNode.Value;");
        c.ReadElement("reuseItem");
        w.Line("reuseNode.Value = reuseItem;");
        w.Line("reuseNode = reuseNode.Next!;");
        w.Close();
        w.Line("while (++reuseIndex < length && reuseNode is not null);");
        w.Line("for (; reuseIndex < length; reuseIndex++)");
        w.Open();
        w.Line(c.Scalar + " addedItem = default!;");
        c.ReadElement("addedItem", fresh: true);
        w.Line("value.AddLast(addedItem);");
        w.Close();
        w.Line("while (reuseNode is not null)");
        w.Open();
        w.Line("var nextNode = reuseNode.Next;");
        w.Line("value.Remove(reuseNode);");
        w.Line("reuseNode = nextNode!;");
        w.Close();
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        if (c.LinkedListAppender)
        {
            // Decode and link in one pass; the list publishes its nodes once, after the last element.
            w.Line("var appender = global::LuminPack.Code.LuminPackCollectionAccess.BeginLinkedList(value);");
            w.Line("if (appender.Direct)");
            w.Open();
            if (!c.Counted)
            {
                w.Line("int width = " + c.Memory + ".SizeOf<" + c.Scalar + ">();");
                w.Line("ref byte input = ref operationState.PeekInput(0, out _);");
                w.Line("operationState.Advance(checked(length * width));");
            }
            w.Line("int remaining = length;");
            w.Line("do");
            w.Open();
            if (c.Counted)
            {
                w.Line(c.Scalar + " current = default!;");
                c.ReadElement("current", fresh: true);
                w.Line("appender.Append(current);");
            }
            else
            {
                w.Line("appender.Append(global::LuminPack.Core.LuminPackReader.Read<" + c.Scalar + ">(ref input));");
                w.Line("input = ref " + c.Memory + ".Add(ref input, (nint)(uint)width);");
            }
            w.Close();
            w.Line("while (--remaining != 0);");
            w.Line("appender.Publish(length);");
            w.Line("goto operationComplete_RootRead;");
            w.Close();
            w.Blank();
        }
        c.PooledRead(() =>
        {
            w.Blank();
            w.Line("global::LuminPack.Code.LuminPackCollectionAccess.FillLinkedList(value, pooled, length);");
        });
    }
}
