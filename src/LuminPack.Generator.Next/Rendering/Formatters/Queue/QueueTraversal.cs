#nullable disable
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the shared storage write used by queue roots and inline members.</summary>
internal static class QueueTraversal
{
    /// <summary>Writes a nonnull queue through the caller's isolated operation state.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The scalar element type, or <c>string</c>.</param>
    /// <param name="local">The nonnull queue expression.</param>
    /// <param name="memory">The spelling of the unsafe memory helper.</param>
    /// <param name="marshal">The spelling of the memory marshal helper.</param>
    /// <remarks>The wire is the element count, the head, tail and size storage words (zero, zero, count), then the live elements
    /// in dequeue order, which the circular storage splits into at most two copied segments. Runtimes without storage access copy
    /// the queue into one pooled array instead. A string element has no raw storage, so the strings are written one at a time in
    /// dequeue order.</remarks>
    internal static void Write(List<string> body, string element, string local, string memory, string marshal)
    {
        if (element == "string")
        {
            WriteText(body, local);
            return;
        }
        body.Add("int queueSize = " + local + ".Count;");
        body.Add("int countPosition = operationState.BeginCollection();");
        body.Add("if (queueSize != 0)");
        body.Add("{");
        body.Add("int queuePayload = checked(queueSize * " + memory + ".SizeOf<" + element + ">());");
        body.Add("operationState.EnsureAdditionalCapacity(checked(sizeof(int) * 3 + queuePayload));");
        WriteStorageWords(body, memory);
        body.Add("#if NET8_0_OR_GREATER");
        body.Add("global::LuminPack.Code.LuminPackMarshal.GetQueueSize(" + local + ", out _, out var queueHead, out _);");
        body.Add("var queueStorage = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(" + local + ");");
        body.Add("var queueFirstLength = global::System.Math.Min(queueSize, queueStorage.Length - queueHead);");
        body.Add("var queueFirst = queueStorage.Slice(queueHead, queueFirstLength);");
        body.Add("var queueSecond = queueStorage.Slice(0, queueSize - queueFirstLength);");
        body.Add("int queueFirstBytes = checked(queueFirst.Length * " + memory + ".SizeOf<" + element + ">());");
        body.Add("if (queueFirstBytes != 0)");
        body.Add("{");
        body.Add("global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(");
        body.Add("    ref operationState.Cursor,");
        body.Add("    ref " + memory + ".As<" + element + ", byte>(ref " + marshal + ".GetReference(queueFirst)),");
        body.Add("    (uint)queueFirstBytes);");
        body.Add("}");
        body.Add("int queueSecondBytes = queuePayload - queueFirstBytes;");
        body.Add("if (queueSecondBytes != 0)");
        body.Add("{");
        body.Add("global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(");
        body.Add("    ref " + memory + ".Add(ref operationState.Cursor, (nint)(uint)queueFirstBytes),");
        body.Add("    ref " + memory + ".As<" + element + ", byte>(ref " + marshal + ".GetReference(queueSecond)),");
        body.Add("    (uint)queueSecondBytes);");
        body.Add("}");
        body.Add("#else");
        body.Add("var queueBuffer = global::System.Buffers.ArrayPool<" + element + ">.Shared.Rent(queueSize);");
        body.Add("try");
        body.Add("{");
        body.Add(local + ".CopyTo(queueBuffer, 0);");
        body.Add("global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(");
        body.Add("    ref operationState.Cursor,");
        body.Add("    ref " + memory + ".As<" + element + ", byte>(ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(queueBuffer)),");
        body.Add("    (uint)queuePayload);");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        // The pooled copy holds raw scalars only, so it carries no managed references to clear.
        body.Add("global::System.Buffers.ArrayPool<" + element + ">.Shared.Return(queueBuffer, false);");
        body.Add("}");
        body.Add("#endif");
        body.Add("operationState.Advance(queuePayload);");
        body.Add("}");
        body.Add("");
        body.Add("operationState.EndCollection(countPosition, queueSize);");
        body.Add("operationState.CheckBuffer();");
    }

    // A string element has no raw storage: after the storage words the strings are written in dequeue order.
    private static void WriteText(List<string> body, string local)
    {
        body.Add("int queueSize = " + local + ".Count;");
        body.Add("int countPosition = operationState.BeginCollection();");
        body.Add("if (queueSize != 0)");
        body.Add("{");
        body.Add("operationState.EnsureAdditionalCapacity(sizeof(int) * 3);");
        WriteStorageWords(body, "global::System.Runtime.CompilerServices.Unsafe");
        body.Add("foreach (var queueItem in " + local + ")");
        body.Add("operationState.Write<TLuminProfile>(queueItem);");
        body.Add("}");
        body.Add("");
        body.Add("operationState.EndCollection(countPosition, queueSize);");
        body.Add("operationState.CheckBuffer();");
    }

    /// <summary>Stores the three already-reserved wire words using the architecture-aware scalar writer.</summary>
    private static void WriteStorageWords(List<string> body, string memory)
    {
        const string writer = "global::LuminPack.Core.LuminPackWriter";
        body.Add("ref byte queueWords = ref operationState.Cursor;");
        body.Add(writer + ".Write(ref queueWords, 0);");
        body.Add(writer + ".Write(ref " + memory + ".Add(ref queueWords, sizeof(int)), 0);");
        body.Add(writer + ".Write(ref " + memory + ".Add(ref queueWords, 2 * sizeof(int)), queueSize);");
        body.Add("operationState.Advance(3 * sizeof(int));");
    }
}
