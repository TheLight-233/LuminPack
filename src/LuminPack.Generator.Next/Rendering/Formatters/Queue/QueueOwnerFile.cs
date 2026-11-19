using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>Queue&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy queue operations. The binary write stores the count, the head, tail and size
/// words, then the live elements in dequeue order; the binary read validates the three words against the count before it fills
/// the circular storage in place and republishes its size, or enqueues each element on runtimes without storage access. A
/// non-null destination is cleared and reused.</remarks>
internal static class QueueOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "value.Enqueue(item);",
        jsonBefore: PrepareJson, outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec, nonEmptyRawHeaderBytes: 4 * sizeof(int));

    /// <summary>Sizes a fresh queue or prepares a reused queue for its streaming element loop.</summary>
    /// <param name="c">The queue owner's JSON read context.</param>
    private static void PrepareJson(EnumerableOwnerContext c)
    {
        var w = c.W;
        string capacity = c.Supports("QueueJsonCapacity") ? "reader.GetArrayCapacityHint<TLuminProfile>(10)" : "";
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "(" + capacity + ");"));
        if (c.Text && c.QueueRefill)
        {
            const string profile = "global::LuminPack.Option.";
            w.Line("else if ((typeof(TLuminProfile) == typeof(" + profile + "LuminPackUtf16LengthOptions) ||");
            w.Line("    typeof(TLuminProfile) == typeof(" + profile + "LuminPackUtf8LengthOptions) ||");
            w.Line("    typeof(TLuminProfile) == typeof(" + profile + "LuminPackDefaultOptions)) && value.Count != 0)");
            w.Open();
            const string access = "global::LuminPack.Code.LuminPackCollectionAccess.";
            w.Line("var slots = " + access + "PrepareQueueRefill(value, value.Count);");
            w.Line("if (slots.IsEmpty)");
            w.Indented(() => w.Line("value.Clear();"));
            w.Line("else if (ReadQueueSlots(ref reader, value, slots))");
            w.Indented(() => w.Line("return;"));
            w.Close();
        }
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        if (c.Text && c.QueueRefill)
            EmitJsonSlotReader(c);
    }

    /// <summary>Reads the borrowed string window and publishes its successful prefix before the ordinary overflow loop.</summary>
    /// <param name="c">The queue owner's JSON read context.</param>
    private static void EmitJsonSlotReader(EnumerableOwnerContext c)
    {
        var w = c.W;
        string reader = w.A("LuminPackJsonReader");
        string serializer = w.A("LuminPackSerializer");
        const string access = "global::LuminPack.Code.LuminPackCollectionAccess.";
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static bool ReadQueueSlots(scoped ref " + reader + " reader, " + c.Sequence + " value, scoped global::System.Span<string> slots)");
        w.Open();
        w.Line("int filled = 0;");
        w.Line("try");
        w.Open();
        w.Line("while (filled < slots.Length)");
        w.Open();
        w.Line("if (!reader.Read<TLuminProfile>() || reader.CurrentTokenType == " + reader + ".JsonTokenType.ArrayEnd)");
        w.Indented(() => w.Line("return true;"));
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ObjectEnd)");
        w.Indented(() => w.Line("continue;"));
        w.Line(serializer + ".LuminRoots_String.RootReadJson<TLuminProfile>(ref reader, ref slots[filled]);");
        w.Line("reader.CompleteValue();");
        w.Line("filled++;");
        w.Close();
        w.Line("return false;");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("slots.Slice(filled).Clear();");
        w.Line(access + "CompleteQueueRefill(value, filled);");
        w.Close();
        w.Close();
    }

    /// <summary>Handles write for <c>QueueOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(EnumerableOwnerContext c)
    {
        if (c.Text && c.Supports("WriteStringQueueStorage"))
        {
            c.W.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteStringQueueStorage<TLuminProfile>(ref operationState, value, " +
                (c.Forwarded ? "true" : "false") + "))");
            c.W.Line("    goto operationComplete_RootWrite;");
        }
        if (c.Payload != null || c.Forwarded)
        {
            c.QueuePayloadWrite("value");
            return;
        }
        var traversal = new List<string>();
        QueueTraversal.Write(traversal, c.Scalar, "value", c.Memory, c.Marshal, c.Supports("WriteQueueRecord"));
        foreach (string line in traversal)
            c.W.Line(line);
    }

    /// <summary>Handles read for <c>QueueOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        string scalar = c.Scalar;
        // Counted elements of a reused queue are read in place over its previous items, published once at the end.
        bool refill = c.Counted && c.QueueRefill;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "(length);"));
        w.Line(refill ? "else if (length == 0)" : "else");
        w.Open();
        w.Line("value.Clear();");
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("value.EnsureCapacity(length);");
        w.Line("#endif");
        w.Close();
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        // A truncated header still consumes its complete words before reporting the failed read.
        w.Line("ref byte storageWords = ref operationState.PeekInput(3 * sizeof(int), out bool completeHeader);");
        w.Line("int storedHead, storedTail, storedSize;");
        w.Line("if (completeHeader)");
        w.Open();
        w.Line("operationState.Advance(3 * sizeof(int));");
        const string reader = "global::LuminPack.Core.LuminPackReader";
        w.Line("storedHead = " + reader + ".Read<int>(ref storageWords);");
        w.Line("storedTail = " + reader + ".Read<int>(ref " + c.Memory + ".Add(ref storageWords, sizeof(int)));");
        w.Line("storedSize = " + reader + ".Read<int>(ref " + c.Memory + ".Add(ref storageWords, 2 * sizeof(int)));");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("storedHead = storedTail = storedSize = 0;");
        w.Line("operationState.Read(ref " + c.Memory + ".As<int, byte>(ref storedHead), sizeof(int));");
        w.Line("operationState.Read(ref " + c.Memory + ".As<int, byte>(ref storedTail), sizeof(int));");
        w.Line("operationState.Read(ref " + c.Memory + ".As<int, byte>(ref storedSize), sizeof(int));");
        w.Close();
        w.Line("if (storedSize != length || storedHead < 0 || storedTail < 0)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidRange(storedSize, length);"));
        w.Blank();
        if (refill)
        {
            if (c.NativeNullableElement)
                c.ValidateNativeNullableElements();
            w.Line("var slots = global::LuminPack.Code.LuminPackCollectionAccess.PrepareQueueRefill(value, length);");
            w.Line("if (slots.Length == length)");
            w.Open();
            if (c.NativeNullableElement)
                c.ReadBlock("slots", nativeValidated: true);
            else
            {
                w.Line("ref var firstSlot = ref " + c.Marshal + ".GetReference(slots);");
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                w.Line("ref var slot = ref " + c.Memory + ".Add(ref firstSlot, (nint)(uint)index);");
                c.ReadElement("slot");
                w.Close();
                w.Line("while (++index < length);");
            }
            w.Line("global::LuminPack.Code.LuminPackCollectionAccess.CompleteQueueRefill(value, length);");
            w.Line("goto operationComplete_RootRead;");
            w.Close();
            w.Line("value.Clear();");
        }
        if (c.Counted)
        {
            // A string or packable element is read and enqueued one at a time.
            if (refill && !c.NativeNullableElement)
            {
                w.Line("ReadQueueFallback(ref operationState, value, length);");
                w.Blank();
                w.Line(w.MethodImpl("NoInlining"));
                w.Line("static void ReadQueueFallback(scoped ref global::LuminPack.Core.LuminPackReader reader, " + c.Sequence + " value, int length)");
                w.Open();
                w.Line("ref var operationState = ref reader;");
                EnqueueItems();
                w.Close();
            }
            else
                EnqueueItems();
            return;
        }
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("var span = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value, length);");
        w.Line("int normalizedTail = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value).Length == length ? 0 : length;");
        w.Line("operationState.Read(");
        w.Indented(() =>
        {
            w.Line("ref " + c.Memory + ".As<" + scalar + ", byte>(ref " + c.Marshal + ".GetReference(span)),");
            w.Line("unchecked(length * " + c.Memory + ".SizeOf<" + scalar + ">()));");
        });
        w.Line("global::LuminPack.Code.LuminPackMarshal.SetQueueSize(value, normalizedTail, 0, length);");
        w.Line("#else");
        EnqueueItems();
        w.Line("#endif");

        void EnqueueItems()
        {
            w.Line("int remaining = length;");
            w.Line("do");
            w.Open();
            w.Line(scalar + " item = default;");
            if (c.Counted)
                c.ReadElement("item");
            else
                w.Line("operationState.Read(ref " + c.Memory + ".As<" + scalar + ", byte>(ref item), " + c.Memory + ".SizeOf<" + scalar + ">());");
            w.Line("value.Enqueue(item);");
            w.Close();
            w.Line("while (--remaining != 0);");
        }
    }
}
