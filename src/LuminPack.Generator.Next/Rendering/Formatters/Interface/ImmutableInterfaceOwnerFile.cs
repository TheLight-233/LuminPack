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
        "staged.Add(item);", BeforeBuilder, AfterBuilder);

    private static readonly EnumerableOwnerSpec QueueSpec = new EnumerableOwnerSpec(QueueWrite, c => Read(c, Queue),
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

    /// <summary>Writes a concrete string queue through its two stacks, as the original traversal did, so a queue whose back stack
    /// is not cached is not materialized by serialization; other queues keep the counted enumeration.</summary>
    private static void QueueWrite(EnumerableOwnerContext c)
    {
        if (!c.Text || !c.Sequence.StartsWith("global::System.Collections.Immutable.ImmutableQueue<", System.StringComparison.Ordinal))
        {
            c.CountedWrite("value");
            return;
        }
        var w = c.W;
        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetImmutableQueueTraversal(value, out var forwards, out var backwards, out var reverse))");
        w.Open();
        w.Line("global::LuminPack.Core.LuminPackWriter operationStateFallback = default;");
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("int count = 0;");
        w.Line("foreach (var item in forwards!)");
        w.Open();
        w.Line("count++;");
        w.Line("operationState.Write<TLuminProfile>(item);");
        w.Close();
        w.Line("if (reverse)");
        w.Open();
        w.Line("operationState.CopyTo(ref operationStateFallback);");
        w.Line("count += WriteReversed(ref operationStateFallback, backwards!);");
        w.Line("operationStateFallback.CopyTo(ref operationState);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("foreach (var item2 in backwards!)");
        w.Open();
        w.Line("count++;");
        w.Line("operationState.Write<TLuminProfile>(item2);");
        w.Close();
        w.Close();
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
        w.Close();
        w.Line("else");
        w.Open();
        c.CountedWrite("value");
        w.Close();
        w.Blank();
        w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        w.Line("static int WriteReversed(ref global::LuminPack.Core.LuminPackWriter writer2, global::System.Collections.Immutable.ImmutableStack<string> stack)");
        w.Open();
        w.Line("var items = global::LuminPack.Code.LuminPackCollectionAccess.RentImmutableQueueReversal(stack, out int count3);");
        w.Line("try");
        w.Open();
        w.Line("foreach (var item4 in new global::System.ReadOnlySpan<string>(items, 0, count3))");
        w.Indented(() => w.Line("writer2.Write<TLuminProfile>(item4);"));
        w.Line("return count3;");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<string>.Shared.Return(items, true);");
        w.Close();
        w.Close();
    }

    private static void Read(EnumerableOwnerContext c, System.Action<EnumerableOwnerContext> build)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + Namespace + Concrete(c) + "<" + c.Scalar + ">.Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        if (c.Text && (c.Sequence.StartsWith(Namespace + "ImmutableHashSet<", System.StringComparison.Ordinal) ||
            c.Sequence.StartsWith(Namespace + "ImmutableSortedSet<", System.StringComparison.Ordinal)))
        {
            CandidateTextSetRead(c);
            return;
        }
        c.PooledRead(() => build(c));
    }

    /// <summary>Reads a concrete string set through the weak candidate cache, as the original read did: the destination or the
    /// last completed set is kept when every decoded string is the same instance it already holds (and a sorted set is still in
    /// its comparer's order); otherwise the decoded strings build a new set, which becomes the next candidate.</summary>
    private static void CandidateTextSetRead(EnumerableOwnerContext c)
    {
        var w = c.W;
        bool sorted = c.Sequence.StartsWith(Namespace + "ImmutableSortedSet<", System.StringComparison.Ordinal);
        string candidate = "LuminImmutableCandidate<" + c.Sequence + ">";
        string comparer = sorted ? "global::System.Collections.Generic.Comparer<string>.Default" : "global::System.Collections.Generic.EqualityComparer<string>.Default";
        string tree = "global::LuminPack.Utility.LuminTreeStorage";
        w.Line("var items = global::System.Buffers.ArrayPool<string>.Shared.Rent(length);");
        w.Line("try");
        w.Open();
        w.Line("bool fromCache = value is null;");
        w.Line("var candidate = value;");
        w.Line("if (candidate is null)");
        w.Line("    " + candidate + ".Last.TryGetTarget(out candidate);");
        w.Line("bool same = candidate is not null &&");
        w.Line("    candidate.Count == length &&");
        w.Line("    object.ReferenceEquals(candidate.KeyComparer, " + comparer + ");");
        w.Line("bool hasPrior = same;");
        w.Line("if (hasPrior && !fromCache)");
        w.Line("    " + tree + ".CopyTo(candidate!, items);");
        w.Line("for (int i = 0; i < length; i++)");
        w.Open();
        w.Line("string prior = hasPrior ? fromCache && i == 0 ? CandidateFirst(candidate!) : items[i] : default!;");
        w.Line("string selected = prior;");
        w.Line("operationState.Read<TLuminProfile>(ref selected);");
        w.Line("if (!object.ReferenceEquals(prior, selected))");
        w.Open();
        w.Line("same = false;");
        w.Line("if (fromCache)");
        w.Line("    hasPrior = false;");
        w.Close();
        w.Line("else if (hasPrior && fromCache && i == 0)");
        w.Line("    " + tree + ".CopyTo(candidate!, items);");
        w.Line("items[i] = selected;");
        w.Close();
        w.Blank();
        if (sorted)
        {
            w.Line("if (same)");
            w.Open();
            w.Line("var comparer = " + comparer + ";");
            w.Line("for (int i2 = 1; i2 < length; i2++)");
            w.Line("    if (comparer.Compare(items[i2 - 1], items[i2]) >= 0)");
            w.Line("    {");
            w.Line("        same = false;");
            w.Line("        break;");
            w.Line("    }");
            w.Close();
            w.Blank();
        }
        w.Line("if (same)");
        w.Open();
        w.Line(candidate + ".Last.SetTarget(candidate!);");
        w.Line("value = candidate!;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("var completed = " + tree + (sorted ? ".CreateImmutableSortedSet(items, length);" : ".CreateFrameworkImmutableHashSet(items, length);"));
        w.Line(candidate + ".Last.SetTarget(completed);");
        w.Line("value = completed;");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<string>.Shared.Return(items, true);");
        w.Close();
        w.Blank();
        w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        w.Line("static string CandidateFirst(" + c.Sequence + " candidate2)");
        w.Open();
        w.Line("using var entries = candidate2.GetEnumerator();");
        w.Line("return entries.MoveNext() ? entries.Current : default!;");
        w.Close();
    }

    private static void Builder(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Blank();
        if (Concrete(c) == "ImmutableList")
        {
            w.Line(c.Supports("ImmutableSpan") ?
                "value = global::System.Collections.Immutable.ImmutableList.Create(new global::System.ReadOnlySpan<" + c.Scalar + ">(pooled, 0, length));" :
                "value = global::System.Collections.Immutable.ImmutableList.CreateRange(new global::System.ArraySegment<" + c.Scalar + ">(pooled, 0, length));");
            return;
        }
        string family = Concrete(c).Substring(9);
        ImmutableBuilderLifetime.Begin(w, family, c.Scalar, c.Supports("ImmutableBuilder"), "builder");
        w.Line("for (int index = 0; index < length; index++)");
        w.Indented(() => w.Line("builder.Add(pooled[index]);"));
        w.Line("value = builder.ToImmutable();");
        ImmutableBuilderLifetime.End(w, family, c.Supports("ImmutableBuilder"), "builder");
    }

    private static void Queue(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Blank();
        w.Line(c.Supports("ImmutableSpan") ?
            "value = global::System.Collections.Immutable.ImmutableQueue.Create(new global::System.ReadOnlySpan<" + c.Scalar + ">(pooled, 0, length));" :
            "value = global::System.Collections.Immutable.ImmutableQueue.CreateRange(new global::System.ArraySegment<" + c.Scalar + ">(pooled, 0, length));");
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

    /// <summary>Starts the selected immutable builder's JSON lifetime.</summary>
    private static void BeforeBuilder(EnumerableOwnerContext c) =>
        ImmutableBuilderLifetime.Begin(c.W, Concrete(c).Substring(9), c.Scalar, c.Supports("ImmutableBuilder"), "staged");

    /// <summary>Publishes the immutable JSON value and returns an optional rental.</summary>
    private static void AfterBuilder(EnumerableOwnerContext c)
    {
        c.W.Line("value = staged.ToImmutable();");
        ImmutableBuilderLifetime.End(c.W, Concrete(c).Substring(9), c.Supports("ImmutableBuilder"), "staged");
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
