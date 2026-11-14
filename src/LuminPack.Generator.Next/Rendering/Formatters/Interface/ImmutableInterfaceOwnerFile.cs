using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the closed immutable list, set, queue and stack interfaces and concrete types whose
/// element is a string, and of the interfaces whose element is a raw scalar.</summary>
/// <remarks>Known framework instances may select an exact traversal. Interface fallbacks count their enumeration.
/// Readers publish the completed framework collection and return any temporary element storage.</remarks>
internal static class ImmutableInterfaceOwnerFile
{
    private const string Namespace = "global::System.Collections.Immutable.";

    private static readonly EnumerableOwnerSpec ListSpec = new EnumerableOwnerSpec(Write, c => Read(c, Builder),
        "staged.Add(item);", BeforeBuilder, AfterBuilder, readInlining: "NoInlining", outlineJson: true);

    private static readonly EnumerableOwnerSpec QueueSpec = new EnumerableOwnerSpec(QueueWrite, c => Read(c, Queue),
        "staged = staged.Enqueue(item);", c => c.W.Line("var staged = " + Namespace + "ImmutableQueue<" + c.Scalar + ">.Empty;"),
        c => c.W.Line("value = staged;"), jsonCount: false, readInlining: "NoInlining", outlineJson: true);

    private static readonly EnumerableOwnerSpec StackSpec = new EnumerableOwnerSpec(StackWrite, c => Read(c, Stack),
        "staged.Add(item);", c => c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();"), AfterStack,
        jsonCount: false, readInlining: "NoInlining", outlineJson: true);

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

    // Use the outer family, since an element can itself be another immutable collection.
    private static string Concrete(EnumerableOwnerContext c)
    {
        int arguments = c.Sequence.IndexOf('<');
        if (arguments < 0)
            return string.Empty;
        return c.Sequence.Substring(0, arguments) switch
        {
            Namespace + "ImmutableSortedSet" => "ImmutableSortedSet",
            Namespace + "ImmutableHashSet" or Namespace + "IImmutableSet" => "ImmutableHashSet",
            Namespace + "ImmutableQueue" or Namespace + "IImmutableQueue" => "ImmutableQueue",
            Namespace + "ImmutableStack" or Namespace + "IImmutableStack" => "ImmutableStack",
            Namespace + "ImmutableList" or Namespace + "IImmutableList" => "ImmutableList",
            _ => string.Empty,
        };
    }

    private static void Write(EnumerableOwnerContext c)
    {
        bool hash = WriteTree("ImmutableHashSet", "CopyImmutableHashTree");
        bool sorted = WriteTree("ImmutableSortedSet", "CopyImmutableSortedTree");
        bool list = WriteTree("ImmutableList", "CopyImmutableListTree");
        if (!hash && !sorted && !list || c.Sequence.StartsWith(Namespace + "IImmutable", System.StringComparison.Ordinal))
            c.CountedWrite("value");

        bool WriteTree(string family, string capability)
        {
            if (!c.Supports(capability))
                return false;
            var w = c.W;
            ImmutableTreeTraversal.Select(w, c.Sequence, c.Scalar, family, source =>
            {
                w.Open();
                if (c.RawTreeElements && c.Supports("WriteRaw" + family))
                    w.Line("global::LuminPack.Utility.LuminTreeStorage.WriteRaw" + (family == "ImmutableList" ? "List" : "") +
                        "(ref operationState, " + source + ");");
                else
                {
                    w.Line("operationState.EnsureAdditionalCapacity(sizeof(int));");
                    w.Line("operationState.WriteCollectionHeader(" + source + ".Count);");
                    ImmutableTreeTraversal.Lease(w, source, c.Scalar, () =>
                    {
                        if (c.Text && c.Supports("ImmutableStringRun"))
                            w.Line("operationState.WriteStrings<TLuminProfile>(" + ImmutableTreeTraversal.CapturedSpan + ");");
                        else
                            c.CountedWrite(source, payloadOnly: true);
                    });
                    w.Line("operationState.CheckBuffer();");
                }
                w.Line("goto operationComplete_RootWrite;");
                w.Close();
            });
            return true;
        }
    }

    internal static bool WriteJsonTree(EnumerableOwnerContext c, SharedFormatterSet set, string owner)
    {
        bool hash = Write("ImmutableHashSet", "CopyImmutableHashTree");
        bool sorted = Write("ImmutableSortedSet", "CopyImmutableSortedTree");
        bool list = Write("ImmutableList", "CopyImmutableListTree");
        return (hash || sorted || list) && !c.Sequence.StartsWith(Namespace + "IImmutable", System.StringComparison.Ordinal);

        bool Write(string family, string capability)
        {
            if (!c.Supports(capability))
                return false;
            var w = c.W;
            ImmutableTreeTraversal.Select(w, c.Sequence, c.Scalar, family, source =>
            {
                w.Open();
                ImmutableTreeTraversal.Lease(w, source, c.Scalar, () =>
                {
                    if (JsonIntegerRunWriter.TryEmit(w, set, c.Sequence, c.Scalar, ImmutableTreeTraversal.CapturedSpan, readBeforeSeparator: true))
                        return;
                    w.Line("bool isFirst = true;");
                    ImmutableTreeTraversal.Items(w, () =>
                        JsonSequenceElementWriter.Emit(w, set, c.Sequence, c.Scalar, owner, utf8Only: true));
                });
                w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                w.Line("return;");
                w.Close();
            });
            return true;
        }
    }

    internal static bool ReadJsonTree(EnumerableOwnerContext c, string owner, string reader)
    {
        bool sorted = c.Supports("BuildOrderedImmutableSortedSet");
        string family = Concrete(c);
        bool linear = family == "ImmutableStack" || c.Supports("ImmutableSpan") && family is "ImmutableList" or "ImmutableQueue";
        if (!sorted && !linear && !c.Supports("BuildFrameworkImmutableHashSet"))
            return false;

        var w = c.W;
        string concrete = Namespace + family + "<" + c.Scalar + ">";
        string pool = "global::System.Buffers.ArrayPool<" + c.Scalar + ">.Shared";
        string clear = "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + c.Scalar + ">()";
        bool guardedCulture = sorted && c.Text;
        if (guardedCulture)
        {
            // A custom profile or culture can run user code while the builder compares successive strings.
            const string options = "global::LuminPack.Option.";
            const string culture = "global::System.Globalization.CultureInfo";
            w.Line("if ((typeof(TLuminProfile) == typeof(" + options + "LuminPackDefaultOptions) ||");
            w.Line("     typeof(TLuminProfile) == typeof(" + options + "LuminPackUtf16LengthOptions) ||");
            w.Line("     typeof(TLuminProfile) == typeof(" + options + "LuminPackUtf8LengthOptions)) &&");
            w.Line("    " + culture + ".CurrentCulture.GetType() == typeof(" + culture + "))");
            w.Open();
        }
        w.Line(c.Scalar + "[] items = null;");
        w.Line("int count = 0;");
        w.Line("try");
        w.Open();
        w.Line("if (reader.Read<TLuminProfile>())");
        w.Open();
        w.Line("do");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ArrayEnd)");
        w.Line("    break;");
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ObjectEnd)");
        w.Line("    continue;");
        w.Line(c.Scalar + " item = default!;");
        w.Line(owner + ".Read<TLuminProfile>(ref reader, ref item);");
        w.Line("if (items is null)");
        w.Line("    items = " + pool + ".Rent(16);");
        w.Line("else if (count == items.Length)");
        w.Open();
        w.Line("var larger = " + pool + ".Rent(checked(count * 2));");
        w.Line("global::System.Array.Copy(items, larger, count);");
        w.Line(pool + ".Return(items, " + clear + ");");
        w.Line("items = larger;");
        w.Close();
        w.Line(c.Memory + ".Add(ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items), (nint)(uint)count++) = item;");
        w.Close();
        w.Line("while (reader.Read<TLuminProfile>());");
        w.Close();
        w.Line("if (count == 0)");
        w.Open();
        w.Line("value = " + concrete + ".Empty;");
        w.Line("return;");
        w.Close();
        if (sorted)
        {
            w.Line("var ordered = global::LuminPack.Utility.LuminTreeStorage.CreateOrderedSetIfSupported<" + c.Scalar + ">(items!, count);");
            w.Line("if (ordered is not null)");
            w.Open();
            w.Line("value = ordered;");
            w.Line("return;");
            w.Close();
            ImmutableBuilderLifetime.Begin(w, "SortedSet", c.Scalar, c.Supports("ImmutableBuilder"), "sortedBuilder");
            w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items!);");
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            w.Line("sortedBuilder.Add(" + c.Memory + ".Add(ref first, (nint)(uint)index));");
            w.Close();
            w.Line("while (++index < count);");
            w.Line("value = sortedBuilder.ToImmutable();");
            ImmutableBuilderLifetime.End(w, "SortedSet", c.Supports("ImmutableBuilder"), "sortedBuilder");
        }
        else if (linear)
        {
            w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items!);");
            if (family == "ImmutableStack")
                BuildStack(c, "count");
            else
                w.Line("value = " + Namespace + family + ".Create(" + c.Marshal + ".CreateReadOnlySpan(ref first, count));");
        }
        else
            w.Line("value = global::LuminPack.Utility.LuminTreeStorage.CreateFrameworkImmutableHashSet(items!, count);");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("if (items is not null) " + pool + ".Return(items, " + clear + ");");
        w.Close();
        if (guardedCulture)
        {
            w.Line("return;");
            w.Close();
        }
        return !guardedCulture;
    }

    // The exact framework queue exposes its validated stacks. An arbitrary implementation retains its own enumeration.
    private static void QueueWrite(EnumerableOwnerContext c)
    {
        var w = c.W;
        bool traversal = c.Supports("ImmutableQueueTraversal") || c.Text && c.Sequence.StartsWith(Namespace + "ImmutableQueue<", System.StringComparison.Ordinal);
        if (traversal)
        {
            ImmutableTreeTraversal.Select(w, c.Sequence, c.Scalar, "ImmutableQueue", source =>
            {
                w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetImmutableQueueTraversal(" +
                    source + ", out var forwards, out var backwards, out var reverse))");
                w.Open();
                w.Line("int header = operationState.BeginCollection();");
                w.Line("int written = WriteStack(ref operationState, forwards!);");
                w.Line("written += reverse ? WriteReversed(ref operationState, backwards!) : WriteStack(ref operationState, backwards!);");
                w.Line("operationState.EndCollection(header, written);");
                w.Line("operationState.CheckBuffer();");
                w.Line("goto operationComplete_RootWrite;");
                w.Close();
            });
        }
        c.CountedWrite("value");
        if (traversal)
            ImmutableLinearTraversal.WriteQueueLocals(c);
    }

    private static void StackWrite(EnumerableOwnerContext c)
    {
        bool isInterface = c.Sequence.StartsWith(Namespace + "IImmutableStack<", System.StringComparison.Ordinal);
        if (isInterface)
        {
            ImmutableTreeTraversal.Select(c.W, c.Sequence, c.Scalar, "ImmutableStack", source =>
            {
                c.CountedWrite(source);
                c.W.Line("goto operationComplete_RootWrite;");
            });
        }
        if (isInterface)
            c.W.Open();
        c.CountedWrite("value");
        if (isInterface)
            c.W.Close();
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
        if (c.Sequence.StartsWith(Namespace + "IImmutableSet<", System.StringComparison.Ordinal) &&
            c.Supports("BuildFrameworkImmutableHashSet"))
        {
            w.Line("if (length == 1)");
            w.Open();
            w.Line(c.Scalar + " item = default!;");
            c.ReadElement("item");
            w.Line("value = global::System.Collections.Immutable.ImmutableHashSet.Create(item);");
            w.Line("goto operationComplete_RootRead;");
            w.Close();
            c.PooledRead(() => w.Line("value = global::LuminPack.Utility.LuminTreeStorage.CreateFrameworkImmutableHashSet(pooled, length);"),
                rawBlock: c.RawTreeElements);
            return;
        }
        string family = Concrete(c);
        if (family == "ImmutableStack" && c.RawTreeElements)
        {
            w.Line("int width = " + c.Memory + ".SizeOf<" + c.Scalar + ">();");
            w.Line("int offset = checked(length * width);");
            w.Line("ref byte start = ref operationState.Cursor;");
            w.Line("operationState.Advance(offset);");
            w.Line("var stack = " + Namespace + "ImmutableStack<" + c.Scalar + ">.Empty;");
            w.Line("do");
            w.Open();
            w.Line("offset -= width;");
            w.Line("stack = stack.Push(global::LuminPack.Core.LuminPackReader.Read<" + c.Scalar + ">(ref " +
                c.Memory + ".Add(ref start, (nint)(uint)offset)));");
            w.Close();
            w.Line("while (offset != 0);");
            w.Line("value = stack;");
            return;
        }
        c.PooledRead(() => build(c), rawBlock: family == "ImmutableQueue" && c.RawTreeElements);
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
        w.Line("ref string first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items);");
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
        w.Line("int i = 0;");
        w.Line("do");
        w.Open();
        w.Line("ref string slot = ref " + c.Memory + ".Add(ref first, (nint)(uint)i);");
        w.Line("string prior = hasPrior ? fromCache && i == 0 ? CandidateFirst(candidate!) : slot : default!;");
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
        w.Line("slot = selected;");
        w.Close();
        w.Line("while (++i < length);");
        w.Blank();
        if (sorted)
        {
            w.Line("if (same && length > 1)");
            w.Open();
            w.Line("var comparer = " + comparer + ";");
            w.Line("int i2 = 1;");
            w.Line("do");
            w.Open();
            w.Line("if (comparer.Compare(" + c.Memory + ".Add(ref first, (nint)(uint)(i2 - 1)),");
            w.Line("                     " + c.Memory + ".Add(ref first, (nint)(uint)i2)) >= 0)");
            w.Open();
            w.Line("same = false;");
            w.Line("break;");
            w.Close();
            w.Close();
            w.Line("while (++i2 < length);");
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
        w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("builder.Add(" + c.Memory + ".Add(ref first, (nint)(uint)index));");
        w.Close();
        w.Line("while (++index < length);");
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
        w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled);");
        BuildStack(c, "length");
    }

    private static void BuildStack(EnumerableOwnerContext c, string count)
    {
        var w = c.W;
        w.Line("var stack = " + Namespace + "ImmutableStack<" + c.Scalar + ">.Empty;");
        w.Line("int index = " + count + ";");
        w.Line("do");
        w.Open();
        w.Line("stack = stack.Push(" + c.Memory + ".Add(ref first, (nint)(uint)--index));");
        w.Close();
        w.Line("while (index != 0);");
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
        w.Line("int index = staged.Count;");
        w.Line("if (index != 0)");
        w.Open();
        w.Line("do");
        w.Open();
        w.Line("stack = stack.Push(staged[--index]);");
        w.Close();
        w.Line("while (index != 0);");
        w.Close();
        w.Line("value = stack;");
    }
}
