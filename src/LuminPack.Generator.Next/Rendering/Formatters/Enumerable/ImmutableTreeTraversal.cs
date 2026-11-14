using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Shares pooled, enumeration-ordered storage for immutable tree writers.</summary>
internal static class ImmutableTreeTraversal
{
    private const string Namespace = "global::System.Collections.Immutable.";

    /// <summary>Emits exact framework dispatch when the declared tree is an interface.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="sequence">The declared tree.</param>
    /// <param name="arguments">The generic arguments between angle brackets.</param>
    /// <param name="family">The open name of the concrete tree.</param>
    /// <param name="emit">The code consuming the typed tree expression.</param>
    internal static void Select(CodeWriter w, string sequence, string arguments, string family, Action<string> emit)
    {
        string concrete = Namespace + family + "<" + arguments + ">";
        if (sequence.StartsWith(Namespace + "IImmutable", StringComparison.Ordinal))
        {
            w.Line("if (value is " + concrete + " tree" + family + ")");
            w.Open();
            emit("tree" + family);
            w.Close();
        }
        else
            emit("value");
    }

    /// <summary>Emits a bounded lease and its cleanup around a captured tree sequence.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="source">The concrete immutable tree expression.</param>
    /// <param name="item">The captured element or pair type.</param>
    /// <param name="consume">The statements consuming the complete element span.</param>
    internal static void Lease(CodeWriter w, string source, string item, Action consume)
    {
        w.Line("int capturedCount = " + source + ".Count;");
        w.Line("if (capturedCount != 0)");
        w.Open();
        string pool = "global::System.Buffers.ArrayPool<" + item + ">.Shared";
        w.Line("var captured = " + pool + ".Rent(capturedCount);");
        w.Line("try");
        w.Open();
        w.Line("ref var capturedFirst = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(captured);");
        w.Line("global::LuminPack.Utility.LuminTreeStorage.CopyTo(" + source + ",");
        w.Line("    global::System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref capturedFirst, capturedCount));");
        consume();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line(pool + ".Return(captured, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + item + ">());");
        w.Close();
        w.Close();
    }

    // Lease emits this body only for a positive count on a pool rental of at least that length.
    internal static void Items(CodeWriter w, Action consume)
    {
        w.Line("int capturedIndex = 0;");
        w.Line("do");
        w.Open();
        w.Line("var item = global::System.Runtime.CompilerServices.Unsafe.Add(ref capturedFirst, (nint)(uint)capturedIndex);");
        consume();
        w.Close();
        w.Line("while (++capturedIndex < capturedCount);");
    }

    internal const string CapturedSpan = "global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref capturedFirst, capturedCount)";
}
