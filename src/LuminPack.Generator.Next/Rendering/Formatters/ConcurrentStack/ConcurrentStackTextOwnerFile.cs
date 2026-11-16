using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ConcurrentStack&lt;string&gt;</c> and owns its inline text write.</summary>
/// <remarks>The write walks one leased snapshot backward in the bottom-up order the record stores; the read
/// reads the strings into a pooled block and pushes it back with one range push. The JSON read stages the strings in their top-first
/// order and publishes them through the collection-access helper.</remarks>
internal static class ConcurrentStackTextOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "staged.Add(item);", Before, After, jsonAddBody: JsonAdd,
        concurrentStackStaging: true, readInlining: "NoInlining", outlineJson: true);

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
        body.Add("int countPosition = operationState.BeginCollection();");
        body.Add("if (count != 0)");
        body.Add("{");
        body.Add("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(snapshot);");
        body.Add("int remaining = count;");
        body.Add("do");
        body.Add("{");
        body.Add("operationState.Write<TLuminProfile>(global::System.Runtime.CompilerServices.Unsafe.Add(ref first, (nint)(uint)--remaining));");
        body.Add("}");
        body.Add("while (remaining != 0);");
        body.Add("}");
        body.Add("operationState.EndCollection(countPosition, count);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        body.Add("global::System.Buffers.ArrayPool<string>.Shared.Return(snapshot, true);");
        body.Add("}");
    }

    /// <summary>Handles write for <c>ConcurrentStackTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(EnumerableOwnerContext c)
    {
        if (c.Payload != null || c.Forwarded)
        {
            c.StackPayloadWrite("value", true);
            return;
        }
        var lines = new List<string>();
        WriteLines(lines, "value");
        foreach (string line in lines)
            c.W.Line(line);
    }

    /// <summary>Handles read for <c>ConcurrentStackTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
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
        c.PooledRead(() => w.Line(c.Supports("PushConcurrentStackRange")
            ? "global::LuminPack.Code.LuminPackCollectionAccess.PushConcurrentStackRange(value, pooled, length);"
            : "value.PushRange(pooled, 0, length);"));
    }

    /// <summary>Handles before for <c>ConcurrentStackTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Before(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        if (c.Supports("StageConcurrentStack"))
            w.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();");
    }

    /// <summary>Handles after for <c>ConcurrentStackTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void After(EnumerableOwnerContext c)
    {
        var w = c.W;
        if (c.Supports("StageConcurrentStack"))
        {
            w.Line("global::LuminPack.Code.LuminPackCollectionAccess.PublishConcurrentStack(value, staged.ToArray(), staged.Count, true);");
            return;
        }
        w.Line("var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentSnapshot(value, out var count);");
        w.Line("try");
        w.Open();
        w.Line("value.Clear();");
        w.Line(c.Supports("PushConcurrentStackRange")
            ? "if (count != 0) global::LuminPack.Code.LuminPackCollectionAccess.PushConcurrentStackRange(value, snapshot, count);"
            : "if (count != 0) value.PushRange(snapshot, 0, count);");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + c.Scalar + ">.Shared.Return(snapshot, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + c.Scalar + ">());");
        w.Close();
    }

    /// <summary>Publishes callback-bearing elements immediately and stages only certified framework values.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void JsonAdd(EnumerableOwnerContext c) => c.W.Line(c.Supports("StageConcurrentStack") ? "staged.Add(item);" : "value.Push(item);");
}
