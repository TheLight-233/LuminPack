#nullable disable
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the packable-element writes of the families whose stored order differs from their enumeration, shared by owner
/// roots and inline members.</summary>
internal static class OrderedPayloadTraversal
{
    /// <summary>Writes a nonnull stack through the caller's isolated operation state, bottom element first.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The element's render model.</param>
    /// <param name="local">The nonnull stack expression.</param>
    /// <param name="concurrent">Whether the stack is a concurrent stack, whose snapshot is leased by the collection-access helper.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    /// <remarks>Both stacks copy their top-first snapshot once and walk it from the last slot, which is the stored order.</remarks>
    internal static void WriteStack(List<string> body, PayloadRenderModel element, string local, bool concurrent, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        string type = element.GlobalName;
        if (concurrent)
            body.Add("var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentSnapshot(" + local + ", out var count);");
        else
        {
            body.Add("int count = " + local + ".Count;");
            body.Add("var snapshot = global::System.Buffers.ArrayPool<" + type + ">.Shared.Rent(count);");
        }
        body.Add("try");
        body.Add("{");
        if (!concurrent)
            body.Add(local + ".CopyTo(snapshot, 0);");
        body.Add("int countPosition = operationState.BeginCollection();");
        body.Add("if (count != 0)");
        body.Add("{");
        body.Add("ref var snapshotFirst = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(snapshot);");
        body.Add("int remaining = count;");
        body.Add("do");
        body.Add("{");
        string elementExit = "end" + ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ends++;
        body.Add("operationState.CheckBuffer();");
        body.Add("var v = " + w.A("Unsafe") + ".Add(ref snapshotFirst, (nint)(uint)--remaining);");
        BinaryEmitter.AddSequenceElementWrite(body, element, "v", elementExit, "operationState", 0, w, names, ref ends, ref cursors, ref spans);
        body.Add("}");
        body.Add("while (remaining != 0);");
        body.Add("}");
        body.Add("operationState.EndCollection(countPosition, count);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        body.Add("global::System.Buffers.ArrayPool<" + type + ">.Shared.Return(snapshot, true);");
        body.Add("}");
    }

    /// <summary>Writes a nonnull stack of strings through the caller's isolated operation state, bottom string first.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="local">The nonnull stack expression.</param>
    internal static void WriteStackText(List<string> body, string local)
    {
        body.Add("int count = " + local + ".Count;");
        body.Add("var snapshot = global::System.Buffers.ArrayPool<string>.Shared.Rent(count);");
        body.Add("try");
        body.Add("{");
        body.Add(local + ".CopyTo(snapshot, 0);");
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

    /// <summary>Writes a nonnull queue through the caller's isolated operation state: the storage words, then the elements.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The element's render model.</param>
    /// <param name="local">The nonnull queue expression.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    internal static void WriteQueue(List<string> body, PayloadRenderModel element, string local, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        body.Add("int queueSize = " + local + ".Count;");
        body.Add("int countPosition = operationState.BeginCollection();");
        body.Add("if (queueSize != 0)");
        body.Add("{");
        body.Add("operationState.EnsureAdditionalCapacity(sizeof(int) * 3);");
        body.Add("int queueZero = 0;");
        body.Add("operationState.Write(in queueZero);");
        body.Add("operationState.Write(in queueZero);");
        body.Add("operationState.Write(in queueSize);");
        body.Add("foreach (var queueItem in " + local + ")");
        body.Add("{");
        string elementExit = "end" + ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ends++;
        body.Add("operationState.CheckBuffer();");
        body.Add("var v = queueItem;");
        BinaryEmitter.AddSequenceElementWrite(body, element, "v", elementExit, "operationState", 0, w, names, ref ends, ref cursors, ref spans);
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("operationState.EndCollection(countPosition, queueSize);");
        body.Add("operationState.CheckBuffer();");
    }
}

/// <summary>Owns the inline record writes of packable elements for the stack and queue families.</summary>
internal static class OrderedPayloadMemberWriter
{
    /// <summary>Expands a stack member's packable-element write in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The stack member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    internal static void WriteStack(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans) =>
        Emit(body, member, owner, w, names, ref ends, ref cursors, ref spans, 0);

    /// <summary>Expands a stack member's string write in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The stack member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    internal static void WriteStackText(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans) =>
        Emit(body, member, owner, w, names, ref ends, ref cursors, ref spans, 3);

    /// <summary>Expands a concurrent stack member's packable-element write in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The concurrent stack member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    internal static void WriteConcurrentStack(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans) =>
        Emit(body, member, owner, w, names, ref ends, ref cursors, ref spans, 1);

    /// <summary>Expands a queue member's packable-element write in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The queue member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    internal static void WriteQueue(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans) =>
        Emit(body, member, owner, w, names, ref ends, ref cursors, ref spans, 2);

    private static void Emit(List<string> body, RenderMember member, string owner, CodeWriter w, Dictionary<string, int> names,
        ref int ends, ref int cursors, ref int spans, int mode)
    {
        string local = BinaryEmitter.RoleName(names, "value");
        string exit = "end" + ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + local + " is null)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add(w.A("Unsafe") + ".WriteUnaligned(ref writer.Cursor, " + w.A("LuminPackCode") + ".NullCollection);");
        body.Add("writer.Advance(4);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add(w.A("LuminPackWriter") + " operationState = default;");
        body.Add("writer.CopyTo(ref operationState);");
        if (mode == 3)
            OrderedPayloadTraversal.WriteStackText(body, local);
        else if (mode == 2)
            OrderedPayloadTraversal.WriteQueue(body, member.Nested, local, w, names, ref ends, ref cursors, ref spans);
        else
            OrderedPayloadTraversal.WriteStack(body, member.Nested, local, mode == 1, w, names, ref ends, ref cursors, ref spans);
        body.Add("operationState.CopyTo(ref writer);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
