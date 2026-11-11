using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ConcurrentStack&lt;string&gt;</c> and owns its inline text write.</summary>
/// <remarks>The write leases one snapshot, reverses it to the bottom-up order the record stores and writes each string; the read
/// reads the strings into a pooled block and pushes it back with one range push. The JSON read stages the strings in their top-first
/// order and publishes them through the collection-access helper.</remarks>
internal static class ConcurrentStackTextOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "staged.Add(item);", Before, After);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Emits the text write of a nonnull concurrent stack through an isolated operation state.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="local">The nonnull stack expression.</param>
    internal static void WriteLines(List<string> body, string local)
    {
        body.Add("var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentSnapshot(" + local + ", out var count);");
        body.Add("try");
        body.Add("{");
        body.Add("global::System.MemoryExtensions.Reverse(global::System.MemoryExtensions.AsSpan(snapshot, 0, count));");
        body.Add("int countPosition = operationState.BeginCollection();");
        body.Add("for (int index = 0; index < count; index++)");
        body.Add("operationState.Write<TLuminProfile>(snapshot[index]);");
        body.Add("operationState.EndCollection(countPosition, count);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        body.Add("global::System.Buffers.ArrayPool<string>.Shared.Return(snapshot, true);");
        body.Add("}");
    }

    private static void Write(EnumerableOwnerContext c)
    {
        if (c.Payload != null)
        {
            c.StackPayloadWrite("value", true);
            return;
        }
        var lines = new List<string>();
        WriteLines(lines, "value");
        foreach (string line in lines)
            c.W.Line(line);
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
        c.PooledRead(() => w.Line("value.PushRange(pooled, 0, length);"));
    }

    private static void Before(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();");
    }

    private static void After(EnumerableOwnerContext c) =>
        c.W.Line("global::LuminPack.Code.LuminPackCollectionAccess.PublishConcurrentStack(value, staged.ToArray(), staged.Count, true);");
}
