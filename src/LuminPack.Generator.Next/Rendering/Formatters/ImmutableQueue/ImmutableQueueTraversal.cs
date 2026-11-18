#nullable disable
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the shared two-stack traversal used by immutable queue roots and inline members.</summary>
internal static class ImmutableQueueTraversal
{
    /// <summary>Writes a nonnull queue using the caller's isolated operation states.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The scalar element type.</param>
    /// <param name="local">The nonnull queue expression.</param>
    /// <param name="w">The source writer and alias resolver.</param>
    /// <param name="firstAlias">The first stack's Boolean alias.</param>
    /// <param name="secondAlias">The second stack's Boolean alias.</param>
    /// <param name="thirdAlias">The enumerator fallback's Boolean alias.</param>
    /// <param name="checkedAlias">The checked buffer fallback's Boolean alias.</param>
    internal static void Write(List<string> body, string element, string local, CodeWriter w, string firstAlias,
        string secondAlias, string thirdAlias, string checkedAlias)
    {
        bool boolean = element == "bool";
        body.Add("if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetImmutableQueueTraversal(" + local +
            ", out var forwards, out var backwards, out var reverse))");
        body.Add("{");
        body.Add("int countPosition = operationState.BeginCollection();");
        body.Add("int count = 0;");
        body.Add("for (var node = forwards!; !node.IsEmpty; node = node.Pop())");
        body.Add("{");
        body.Add("var v = node.Peek();");
        body.Add("operationState.CheckBuffer();");
        body.Add("count++;");
        body.Add(boolean
            ? "ref readonly bool " + firstAlias + " = ref v;"
            : "operationState.Write(in v);");
        if (boolean)
            body.Add("operationState.Write(in " + firstAlias + ");");
        body.Add("}");
        body.Add("if (reverse)");
        body.Add("{");
        body.Add("operationState.CopyTo(ref operationStateFallback);");
        body.Add("count += WriteReversed(ref operationStateFallback, backwards!);");
        body.Add("operationStateFallback.CopyTo(ref operationState);");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        body.Add("for (var node = backwards!; !node.IsEmpty; node = node.Pop())");
        body.Add("{");
        body.Add("var v2 = node.Peek();");
        body.Add("operationState.CheckBuffer();");
        body.Add("count++;");
        body.Add(boolean
            ? "ref readonly bool " + secondAlias + " = ref v2;"
            : "operationState.Write(in v2);");
        if (boolean)
            body.Add("operationState.Write(in " + secondAlias + ");");
        body.Add("}");
        body.Add("}");
        body.Add("operationState.EndCollection(countPosition, count);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        body.Add("int countPosition2 = operationState.BeginCollection();");
        body.Add("int count2 = 0;");
        body.Add("foreach (var item3 in " + local + ")");
        body.Add("{");
        body.Add("operationState.CheckBuffer();");
        body.Add("count2++;");
        body.Add("var v3 = item3;");
        body.Add(boolean
            ? "ref readonly bool " + thirdAlias + " = ref v3;"
            : "operationState.Write(in v3);");
        if (boolean)
            body.Add("operationState.Write(in " + thirdAlias + ");");
        body.Add("}");
        body.Add("operationState.EndCollection(countPosition2, count2);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static int WriteReversed(ref " + w.A("LuminPackWriter") + " writer2, global::System.Collections.Immutable.ImmutableStack<" +
            element + "> stack)");
        body.Add("{");
        body.Add("var items = global::LuminPack.Code.LuminPackCollectionAccess.RentImmutableQueueReversal(stack, out int count3);");
        body.Add("try");
        body.Add("{");
        body.Add("var reversed = new global::System.ReadOnlySpan<" + element + ">(items, 0, count3);");
        body.Add("long reversedBytes = reversed.Length * (long)" + w.A("Unsafe") + ".SizeOf<" + element + ">();");
        body.Add("if (writer2.RecordRoom() >= reversedBytes)");
        body.Add("{");
        body.Add("ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(reversed);");
        body.Add("writer2.Write(ref " + w.A("Unsafe") + ".As<" + element + ", byte>(ref first), (int)reversedBytes);");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        body.Add("foreach (var item5 in reversed)");
        body.Add("{");
        body.Add("writer2.CheckBuffer();");
        body.Add("var v5 = item5;");
        body.Add(boolean
            ? "ref readonly bool " + checkedAlias + " = ref v5;"
            : "writer2.Write(in v5);");
        if (boolean)
            body.Add("writer2.Write(in " + checkedAlias + ");");
        body.Add("}");
        body.Add("}");
        body.Add("return count3;");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        body.Add("global::System.Buffers.ArrayPool<" + element + ">.Shared.Return(items, " +
            "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + element + ">());");
        body.Add("}");
        body.Add("}");
    }
}
