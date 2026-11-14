using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>LinkedList&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy linked-list operations. The binary write walks the nodes directly instead of through
/// the version-checked enumerator and counts them; the binary read takes one pooled block and links its nodes in one pass
/// through the collection-access helper, reusing a non-null destination after clearing it.</remarks>
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
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Write(EnumerableOwnerContext c)
    {
        var w = c.W;
        if (c.Payload != null)
        {
            c.CountedWrite("value");
            return;
        }
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("int count = 0;");
        w.Line("var node = value.First;");
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

    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        c.PooledRead(() =>
        {
            w.Blank();
            w.Line("global::LuminPack.Code.LuminPackCollectionAccess.FillLinkedList(value, pooled, length);");
        });
    }
}
