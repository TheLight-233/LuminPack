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
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Sizes a fresh queue from the structural hint and clears a reused queue before its element loop.</summary>
    /// <param name="c">The queue owner's JSON read context.</param>
    private static void PrepareJson(EnumerableOwnerContext c)
    {
        string capacity = c.Supports("QueueJsonCapacity") ? "reader.GetArrayCapacityHint<TLuminProfile>(10)" : "";
        c.W.Line("if (value is null)");
        c.W.Indented(() => c.W.Line("value = new " + c.Sequence + "(" + capacity + ");"));
        c.W.Line("else");
        c.W.Indented(() => c.W.Line("value.Clear();"));
    }

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

    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        string scalar = c.Scalar;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "(length);"));
        w.Line("else");
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
        if (c.Counted)
        {
            // A string or packable element is read and enqueued one at a time.
            w.Line("int remaining = length;");
            w.Line("do");
            w.Open();
            w.Line(scalar + " item = default;");
            c.ReadElement("item");
            w.Line("value.Enqueue(item);");
            w.Close();
            w.Line("while (--remaining != 0);");
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
        w.Line("int remaining = length;");
        w.Line("do");
        w.Open();
        w.Line(scalar + " item = default;");
        w.Line("operationState.Read(ref " + c.Memory + ".As<" + scalar + ", byte>(ref item), " + c.Memory + ".SizeOf<" + scalar + ">());");
        w.Line("value.Enqueue(item);");
        w.Close();
        w.Line("while (--remaining != 0);");
        w.Line("#endif");
    }
}
