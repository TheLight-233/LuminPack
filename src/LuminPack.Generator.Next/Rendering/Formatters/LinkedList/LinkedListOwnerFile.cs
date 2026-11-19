using System;
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
        bool outline = c.Text && c.LinkedListAppender;
        // A failed read into existing nodes exposes the completed prefix.
        w.Line("if (value is not null && length != 0 && value.First is { } reuseNode)");
        w.Open();
        if (outline)
            w.Line("ReadExisting(ref operationState, value, reuseNode, length);");
        else
            ReadExisting(c);
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
        if (!outline)
        {
            ReadStaged(c);
            return;
        }

        w.Line("ReadStaged(ref operationState, value, length);");
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void ReadExisting(scoped ref global::LuminPack.Core.LuminPackReader operationState, " +
            c.Sequence + " value, global::System.Collections.Generic.LinkedListNode<" + c.Scalar + "> reuseNode, int length)");
        w.Open();
        ReadExisting(c);
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void ReadStaged(scoped ref global::LuminPack.Core.LuminPackReader operationState, " +
            c.Sequence + " value, int length)");
        w.Open();
        ReadStaged(c);
        w.Close();
    }

    /// <summary>Emits the existing-node read, including adding and removing nodes to match the input length.</summary>
    /// <param name="c">The linked-list formatter context.</param>
    private static void ReadExisting(EnumerableOwnerContext c)
    {
        var w = c.W;
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
        RemoveUnusedNodes(w);
    }

    /// <summary>Checks whether the element parser is built in and cannot observe the destination while refilling.</summary>
    /// <param name="c">The linked-list formatter context.</param>
    /// <returns>Whether JSON can refill existing nodes.</returns>
    internal static bool CanReuseJsonNodes(EnumerableOwnerContext c)
    {
        string element = GenericFormatterReuse.Real(c.Scalar);
        return c.Text || !c.Forwarded && c.Payload == null &&
            (GenericIdentity.IsRaw(element) || element is "nint" or "nuint");
    }

    /// <summary>Uses node refill only when neither the collection nor its JSON profile can invoke user code.</summary>
    /// <param name="c">The linked-list formatter context.</param>
    internal static void SelectJsonReuse(EnumerableOwnerContext c)
    {
        const string options = "global::LuminPack.Option.";
        const string match = "typeof(TLuminProfile) == typeof(" + options;
        const string strict = options + "LuminPackGeneralFormat, " + options + "LuminPackDefaultDepth, " + options + "LuminPackStrictJson>";
        var w = c.W;
        w.Line("if (value?.First is { } reuseNode && value.GetType() == typeof(" + c.Sequence + ") &&");
        w.Line("    (" + match + "LuminPackUtf16LengthOptions) || " + match + "LuminPackUtf8LengthOptions) ||");
        w.Line("     " + match + "LuminPackDefaultOptions) || " + match + "LuminPackUtf16TokenOptions) ||");
        w.Line("     " + match + "LuminPackUtf8TokenOptions) || " + match + "LuminPackUtf16VarIntOptions) ||");
        w.Line("     " + match + "LuminPackUtf8VarIntOptions) ||");
        w.Line("     " + match + "LuminPackOptions<" + options + "LuminPackUtf16LengthOptions, " + strict + ") ||");
        w.Line("     " + match + "LuminPackOptions<" + options + "LuminPackUtf8LengthOptions, " + strict + ")))");
        w.Open();
        w.Line("ReadExistingJson<TLuminProfile>(ref reader, value, reuseNode);");
        w.Line("return;");
        w.Close();
        w.Blank();
    }

    /// <summary>Emits JSON node refill around the family's streaming scalar or text element parser.</summary>
    /// <param name="c">The linked-list formatter context.</param>
    /// <param name="reader">The generated reader type name.</param>
    /// <param name="options">The generated profile constraint type name.</param>
    /// <param name="readItems">The ordinary loop, with a callback after each completed value.</param>
    internal static void ReadExistingJson(EnumerableOwnerContext c, string reader, string options, Action<Action, bool> readItems)
    {
        var w = c.W;
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("private static void ReadExistingJson<TLuminProfile>(scoped ref " + reader + " reader, " + c.Sequence + " value,");
        w.Line("    global::System.Collections.Generic.LinkedListNode<" + c.Scalar + "> reuseNode)");
        w.Line("    where TLuminProfile : struct, " + options);
        w.Open();
        // The ordinary JSON reader clears even when the final size matches. Reattach the first node
        // to invalidate preexisting framework enumerators without losing that node.
        w.Line("value.Remove(reuseNode);");
        w.Line("value.AddFirst(reuseNode);");
        w.Line("try");
        w.Open();
        readItems(() =>
        {
            w.Line("if (reuseNode is null)");
            w.Indented(() => w.Line("value.AddLast(item);"));
            w.Line("else");
            w.Open();
            w.Line("reuseNode.Value = item;");
            w.Line("reuseNode = reuseNode.Next!;");
            w.Close();
        }, false);
        w.Close();
        w.Line("finally");
        w.Open();
        // On failure, keep exactly the completed prefix, as the ordinary JSON reader does.
        RemoveUnusedNodes(w);
        w.Close();
        w.Close();
        w.Blank();
    }

    /// <summary>Emits detaching each node after the last value read.</summary>
    /// <param name="w">The writer in the linked-list read body.</param>
    private static void RemoveUnusedNodes(CodeWriter w)
    {
        w.Line("while (reuseNode is not null)");
        w.Open();
        w.Line("var nextNode = reuseNode.Next;");
        w.Line("value.Remove(reuseNode);");
        w.Line("reuseNode = nextNode!;");
        w.Close();
    }

    /// <summary>Emits the destination fill through pooled staging when direct linking is unavailable.</summary>
    /// <param name="c">The linked-list formatter context.</param>
    private static void ReadStaged(EnumerableOwnerContext c)
    {
        c.PooledRead(() =>
        {
            c.W.Blank();
            c.W.Line("global::LuminPack.Code.LuminPackCollectionAccess.FillLinkedList(value, pooled, length);");
        });
    }
}
