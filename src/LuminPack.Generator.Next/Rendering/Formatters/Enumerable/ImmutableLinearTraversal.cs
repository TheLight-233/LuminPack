using System;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits exact stack and queue traversal while retaining interface fallbacks.</summary>
internal static class ImmutableLinearTraversal
{
    private const string Namespace = "global::System.Collections.Immutable.";
    private const string Writer = "global::LuminPack.Core.LuminPackWriter";
    private const string CoreAccess = "global::LuminPack.Code.LuminPackCollectionAccess";
    private const string ArrayReference = "global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference";

    /// <summary>Emits the binary operations that consume the validated queue stacks.</summary>
    /// <param name="c">The owning formatter context.</param>
    internal static void WriteQueueLocals(EnumerableOwnerContext c)
    {
        var w = c.W;
        string stack = Namespace + "ImmutableStack<" + c.Scalar + ">";
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static int WriteStack(scoped ref " + Writer + " operationState, " + stack + " stack)");
        w.Open();
        w.Line("int count = 0;");
        w.Line("foreach (var item in stack)");
        w.Open();
        w.Line("count++;");
        c.WriteCountedElement("item");
        w.Close();
        w.Line("return count;");
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static int WriteReversed(scoped ref " + Writer + " operationState, " + stack + " stack)");
        w.Open();
        EmitLease(c, () =>
        {
            if (c.Text && c.Supports("ImmutableStringRun"))
            {
                w.Line("operationState.WriteStrings<TLuminProfile>(" + c.Marshal + ".CreateReadOnlySpan(ref first, count));");
                return;
            }
            if (c.RawTreeElements && c.Supports("ImmutableQueueRawWindow"))
            {
                w.Line("long bytes = (long)count * " + c.Memory + ".SizeOf<" + c.Scalar + ">();");
                w.Line("if (operationState.RecordRoom() >= bytes)");
                w.Open();
                w.Line("operationState.Write(ref " + c.Memory + ".As<" + c.Scalar + ", byte>(ref first), (int)bytes);");
                w.Line("return count;");
                w.Close();
            }
            EmitItems(c, () => c.WriteCountedElement("item"));
        });
        w.Line("return count;");
        w.Close();
    }

    /// <summary>Emits an exact framework JSON traversal before the existing counted fallback.</summary>
    /// <param name="c">The owning formatter context.</param>
    /// <param name="set">The available formatter capabilities.</param>
    /// <param name="owner">The element's JSON formatter.</param>
    /// <param name="writer">The JSON writer type.</param>
    internal static void WriteJson(EnumerableOwnerContext c, SharedFormatterSet set, string owner, string writer)
    {
        bool queue = c.Supports("ImmutableQueueTraversal");
        bool stackInterface = c.Sequence.StartsWith(Namespace + "IImmutableStack<", StringComparison.Ordinal);
        if (!queue && !stackInterface)
            return;

        var w = c.W;
        ImmutableTreeTraversal.Select(w, c.Sequence, c.Scalar, queue ? "ImmutableQueue" : "ImmutableStack", source =>
        {
            if (queue)
            {
                w.Line("if (" + CoreAccess + ".TryGetImmutableQueueTraversal(" + source +
                    ", out var forwards, out var backwards, out var reverse))");
                w.Open();
                w.Line("bool firstElement = true;");
                w.Line("WriteKnownStack(ref writer, forwards!, ref firstElement);");
                w.Line("if (reverse)");
                w.Indented(() => w.Line("WriteKnownReversed(ref writer, backwards!, ref firstElement);"));
                w.Line("else");
                w.Indented(() => w.Line("WriteKnownStack(ref writer, backwards!, ref firstElement);"));
            }
            else
            {
                w.Line("WriteKnownStack(ref writer, " + source + ");");
            }
            w.Line("writer.WriteArrayEnd<TLuminProfile>();");
            w.Line("return;");
            if (queue)
                w.Close();
        });

        string stack = Namespace + "ImmutableStack<" + c.Scalar + ">";
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void WriteKnownStack(scoped ref " + writer + " output, " + stack + " stack" +
            (queue ? ", ref bool firstElement)" : ")"));
        w.Open();
        if (!queue)
            w.Line("bool firstElement = true;");
        w.Line("foreach (var item in stack)");
        w.Open();
        EmitElement();
        w.Close();
        w.Close();
        if (queue)
        {
            w.Blank();
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void WriteKnownReversed(scoped ref " + writer + " output, " + stack + " stack, ref bool firstElement)");
            w.Open();
            EmitLease(c, () =>
            {
                if (JsonIntegerRunWriter.TryEmit(w, set, c.Sequence, c.Scalar,
                    c.Marshal + ".CreateReadOnlySpan(ref first, count)", readBeforeSeparator: true, writer: "output"))
                    w.Line("firstElement = false;");
                else
                    EmitItems(c, EmitElement);
            });
            w.Close();
        }
        w.Blank();

        // Preserve the root's separator state and its original element formatter calls.
        void EmitElement() => JsonSequenceElementWriter.Emit(w, set, c.Sequence, c.Scalar, owner, "output", "firstElement", utf8Only: true);
    }

    private static void EmitLease(EnumerableOwnerContext c, Action consume)
    {
        var w = c.W;
        w.Line("var items = " + CoreAccess + ".RentImmutableQueueReversal(stack, out int count);");
        w.Line("try");
        w.Open();
        w.Line("ref var first = ref " + ArrayReference + "(items);");
        consume();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + c.Scalar + ">.Shared.Return(items, " +
            "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + c.Scalar + ">());");
        w.Close();
    }

    // The reversal helper is called only for an uncached back stack with at least two elements.
    private static void EmitItems(EnumerableOwnerContext c, Action consume)
    {
        var w = c.W;
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("var item = " + c.Memory + ".Add(ref first, (nint)(uint)index);");
        consume();
        w.Close();
        w.Line("while (++index < count);");
    }
}
