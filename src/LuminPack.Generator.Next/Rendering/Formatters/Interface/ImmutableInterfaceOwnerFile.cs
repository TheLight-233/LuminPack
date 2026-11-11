using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the closed immutable sequence interfaces of raw scalars.</summary>
/// <remarks>The write counts the enumeration of the value. The read returns the empty instance of the concrete immutable type for
/// an empty payload, otherwise reads one pooled block and builds the list or set through its builder, enqueues each element of the
/// queue in order and pushes the stack from its last element back. The JSON read stages elements the same way.</remarks>
internal static class ImmutableInterfaceOwnerFile
{
    private const string Namespace = "global::System.Collections.Immutable.";

    private static readonly EnumerableOwnerSpec ListSpec = new EnumerableOwnerSpec(Write, c => Read(c, "ImmutableList", Builder),
        "staged.Add(item);", c => c.W.Line("var staged = " + Namespace + "ImmutableList.CreateBuilder<" + c.Scalar + ">();"),
        c => c.W.Line("value = staged.ToImmutable();"));

    private static readonly EnumerableOwnerSpec SetSpec = new EnumerableOwnerSpec(Write, c => Read(c, "ImmutableHashSet", Builder),
        "staged.Add(item);", c => c.W.Line("var staged = " + Namespace + "ImmutableHashSet.CreateBuilder<" + c.Scalar + ">();"),
        c => c.W.Line("value = staged.ToImmutable();"));

    private static readonly EnumerableOwnerSpec QueueSpec = new EnumerableOwnerSpec(Write, c => Read(c, "ImmutableQueue", Queue),
        "staged = staged.Enqueue(item);", c => c.W.Line("var staged = " + Namespace + "ImmutableQueue<" + c.Scalar + ">.Empty;"),
        c => c.W.Line("value = staged;"), jsonCount: false);

    private static readonly EnumerableOwnerSpec StackSpec = new EnumerableOwnerSpec(Write, c => Read(c, "ImmutableStack", Stack),
        "staged.Add(item);", c => c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();"), AfterStack,
        jsonCount: false);

    /// <summary>Renders an <c>IImmutableList&lt;T&gt;</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderList(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, ListSpec);

    /// <summary>Renders an <c>IImmutableSet&lt;T&gt;</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderSet(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, SetSpec);

    /// <summary>Renders an <c>IImmutableQueue&lt;T&gt;</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderQueue(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, QueueSpec);

    /// <summary>Renders an <c>IImmutableStack&lt;T&gt;</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderStack(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, StackSpec);

    private static void Write(EnumerableOwnerContext c)
    {
        var lines = new List<string>();
        InterfaceTraversal.Counted(lines, c.Scalar, "value");
        foreach (string line in lines)
            c.W.Line(line);
    }

    private static void Read(EnumerableOwnerContext c, string concrete, System.Action<EnumerableOwnerContext> build)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + Namespace + concrete + "<" + c.Scalar + ">.Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        c.PooledRead(() => build(c));
    }

    private static void Builder(EnumerableOwnerContext c)
    {
        var w = c.W;
        string concrete = c.Sequence.Contains("IImmutableSet") ? "ImmutableHashSet" : "ImmutableList";
        w.Blank();
        w.Line("var builder = " + Namespace + concrete + ".CreateBuilder<" + c.Scalar + ">();");
        w.Line("for (int index = 0; index < length; index++)");
        w.Indented(() => w.Line("builder.Add(pooled[index]);"));
        w.Line("value = builder.ToImmutable();");
    }

    private static void Queue(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Blank();
        w.Line("var queue = " + Namespace + "ImmutableQueue<" + c.Scalar + ">.Empty;");
        w.Line("for (int index = 0; index < length; index++)");
        w.Indented(() => w.Line("queue = queue.Enqueue(pooled[index]);"));
        w.Line("value = queue;");
    }

    private static void Stack(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Blank();
        w.Line("var stack = " + Namespace + "ImmutableStack<" + c.Scalar + ">.Empty;");
        w.Line("for (int index = length - 1; index >= 0; index--)");
        w.Indented(() => w.Line("stack = stack.Push(pooled[index]);"));
        w.Line("value = stack;");
    }

    private static void AfterStack(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("var stack = " + Namespace + "ImmutableStack<" + c.Scalar + ">.Empty;");
        w.Line("for (int index = staged.Count - 1; index >= 0; index--)");
        w.Indented(() => w.Line("stack = stack.Push(staged[index]);"));
        w.Line("value = stack;");
    }
}
