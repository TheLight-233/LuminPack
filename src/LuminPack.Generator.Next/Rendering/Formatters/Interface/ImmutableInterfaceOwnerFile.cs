using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the closed immutable list, set, queue and stack interfaces and concrete types whose
/// element is a string, and of the interfaces whose element is a raw scalar.</summary>
/// <remarks>The write counts the enumeration of the value. The read returns the empty instance of the concrete immutable type for
/// an empty payload, otherwise reads one pooled block and builds the list or set through its builder, enqueues each element of the
/// queue in order and pushes the stack from its last element back. The JSON read stages elements the same way. The concrete
/// immutable families reuse these owners for their string element, since the string wire is the counted enumeration.</remarks>
internal static class ImmutableInterfaceOwnerFile
{
    private const string Namespace = "global::System.Collections.Immutable.";

    private static readonly EnumerableOwnerSpec ListSpec = new EnumerableOwnerSpec(Write, c => Read(c, Builder),
        "staged.Add(item);", c => c.W.Line("var staged = " + Namespace + Concrete(c) + ".CreateBuilder<" + c.Scalar + ">();"),
        c => c.W.Line("value = staged.ToImmutable();"));

    private static readonly EnumerableOwnerSpec QueueSpec = new EnumerableOwnerSpec(Write, c => Read(c, Queue),
        "staged = staged.Enqueue(item);", c => c.W.Line("var staged = " + Namespace + "ImmutableQueue<" + c.Scalar + ">.Empty;"),
        c => c.W.Line("value = staged;"), jsonCount: false);

    private static readonly EnumerableOwnerSpec StackSpec = new EnumerableOwnerSpec(Write, c => Read(c, Stack),
        "staged.Add(item);", c => c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();"), AfterStack,
        jsonCount: false);

    /// <summary>Renders an immutable list or set owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderList(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, ListSpec);

    /// <summary>Renders an immutable set owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderSet(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, ListSpec);

    /// <summary>Renders an immutable queue owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderQueue(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, QueueSpec);

    /// <summary>Renders an immutable stack owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderStack(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, StackSpec);

    // The concrete immutable family the declared sequence reads into.
    private static string Concrete(EnumerableOwnerContext c) =>
        c.Sequence.Contains("SortedSet") ? "ImmutableSortedSet" :
        c.Sequence.Contains("Set") ? "ImmutableHashSet" :
        c.Sequence.Contains("Queue") ? "ImmutableQueue" :
        c.Sequence.Contains("Stack") ? "ImmutableStack" : "ImmutableList";

    private static void Write(EnumerableOwnerContext c) => c.CountedWrite("value");

    private static void Read(EnumerableOwnerContext c, System.Action<EnumerableOwnerContext> build)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + Namespace + Concrete(c) + "<" + c.Scalar + ">.Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        c.PooledRead(() => build(c));
    }

    private static void Builder(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Blank();
        w.Line("var builder = " + Namespace + Concrete(c) + ".CreateBuilder<" + c.Scalar + ">();");
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
