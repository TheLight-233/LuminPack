using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>Stack&lt;T&gt;</c> whose element is a packable class.</summary>
/// <remarks>Binary writes use validated bottom-first storage for callback-free records and a snapshot for other elements.
/// Binary reads stage every element before restoring the stack. JSON stages in top-first order and restores in reverse.</remarks>
internal static class StackPayloadOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read,
        "staged.Add(item);", Before, After, readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The payload-element stack owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Writes proven records from exact stack storage before selecting the original snapshot fallback.</summary>
    /// <param name="c">The generated stack writer context.</param>
    private static void Write(EnumerableOwnerContext c)
    {
        if (c.Supports("GetBottomFirstStackItems") && c.ReservableListElement() is { } element)
        {
            var w = c.W;
            w.Line("if (operationState.Owner is not null && " + ParallelDictionaryWrite.FixedUtf16LengthProfile + " &&");
            w.Line("    global::LuminPack.Code.LuminPackCollectionAccess.TryGetBottomFirstStackItems(value, out var items))");
            w.Open();
            w.Line("int header = operationState.BeginCollection();");
            c.WriteReservedElements(element);
            w.Line("operationState.EndCollection(header, items.Length);");
            w.Line("operationState.CheckBuffer();");
            w.Line("goto operationComplete_RootWrite;");
            w.Close();
            w.Blank();
        }
        c.StackPayloadWrite("value", false);
    }

    /// <summary>Handles read for <c>StackPayloadOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        if (c.StackRefill)
        {
            // The record is bottom first like the backing array, so the previous items of a reused stack seed the same
            // slots in place. Fresh stacks keep their original direct reads.
            w.Line("if (value is not null && length != 0)");
            w.Open();
            w.Line("var slots = global::LuminPack.Code.LuminPackCollectionAccess.PrepareStackRefill(value, length);");
            w.Line("if (slots.Length == length)");
            w.Open();
            if (c.Text)
                w.Line("operationState.ReadStrings<TLuminProfile>(slots);");
            else
            {
                w.Line("ref var firstSlot = ref " + c.Marshal + ".GetReference(slots);");
                w.Line("int slotIndex = 0;");
                w.Line("do");
                w.Open();
                w.Line("ref var slot = ref " + c.Memory + ".Add(ref firstSlot, (nint)(uint)slotIndex);");
                c.ReadElement("slot");
                w.Close();
                w.Line("while (++slotIndex < length);");
            }
            w.Line("global::LuminPack.Code.LuminPackCollectionAccess.CompleteStackRefill(value, length);");
            w.Line("goto operationComplete_RootRead;");
            w.Close();
            w.Close();
        }
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "(length);"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0" + (c.Text && c.Supports("ReadStringStackStorage") ?
            " || global::LuminPack.Code.LuminPackCollectionAccess.TryReadStringStackStorage<TLuminProfile>(ref operationState, value, length)" : "") + ")");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        if (c.Supports("RestoreStackFromBottom"))
        {
            c.PooledRead(() => w.Line("global::LuminPack.Code.LuminPackCollectionAccess.RestoreStackFromBottom(value, " +
                "new global::System.ReadOnlySpan<" + c.Scalar + ">(pooled, 0, length));"));
            return;
        }
        c.PooledRead(() =>
        {
            w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled);");
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            w.Line("value.Push(" + c.Memory + ".Add(ref first, (nint)(uint)index));");
            w.Close();
            w.Line("while (++index < length);");
        });
    }

    /// <summary>Handles before for <c>StackPayloadOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Before(EnumerableOwnerContext c)
        => c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();");

    /// <summary>Handles after for <c>StackPayloadOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void After(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("value = new " + c.Sequence + "(staged.Count);");
        w.Line("int remaining = staged.Count;");
        w.Line("if (remaining != 0)");
        w.Open();
        w.Line("do");
        w.Open();
        w.Line("value.Push(staged[--remaining]);");
        w.Close();
        w.Line("while (remaining != 0);");
        w.Close();
    }
}
