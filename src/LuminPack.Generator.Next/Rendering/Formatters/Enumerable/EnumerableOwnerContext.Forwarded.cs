using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
internal sealed partial class EnumerableOwnerContext
{
    /// <summary>Preserves optional concurrent traversal without changing its capture boundary.</summary>
    /// <param name = "local">The local used by this operation.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal string Source(string local)
    {
        FormatterModel? model = _set.FindFormatter(Sequence);
        if (model?.Supports("ConcurrentTraversal") != true)
            return local;
        string family = model.Definition.Substring(model.Definition.LastIndexOf('.') + 1);
        return "global::LuminPack.Code.LuminPackCollectionAccess.Enumerate" + family + "(" + local + ")";
    }

    /// <summary>Writes a default element through the element's static owner.</summary>
    /// <param name = "expression">The addressable value expression.</param>
    internal void WriteElement(string expression) => FormatterElementEmitter.Write(W, _set, _element!, expression, "operationState");
    /// <summary>Retains exact mutable List and array targets selected by the legacy interface reader.</summary>
    internal void ReadInterfaceTargets()
    {
        if (!Supports("InterfaceTargetReuse"))
            return;
        string list = "global::System.Collections.Generic.List<" + Scalar + ">";
        string array = ContiguousOwnerFile.ArrayType(Scalar);
        W.Line("if (value is not null && value.GetType() == typeof(" + list + "))");
        W.Open();
        W.Line("var selected = (" + list + ")value;");
        W.Line("var target = global::LuminPack.Code.LuminPackMarshal.GetListSpan(selected, length);");
        ReadBlock("target");
        W.Line("value = selected;");
        W.Line("goto operationComplete_RootRead;");
        W.Close();
        W.Line("if (value is not null && value.GetType() == typeof(" + array + "))");
        W.Open();
        W.Line("var selected = (" + array + ")value;");
        W.Line("if (selected.Length != length) selected = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + Scalar + ">(length);");
        W.Line("var target = selected.AsSpan();");
        ReadBlock("target");
        W.Line("value = selected;");
        W.Line("goto operationComplete_RootRead;");
        W.Close();
    }

    /// <summary>Writes a full count and payload using the element's contiguous representation.</summary>
    /// <param name = "expression">The addressable value expression.</param>
    internal void WriteSpan(string expression)
    {
        W.Line("global::System.ReadOnlySpan<" + Scalar + "> blockItems = " + expression + ";");
        if (_element!.RawCollection)
        {
            W.Line("int blockBytes = global::LuminPack.Core.LuminPackWriter.Size<" + Scalar + ">(blockItems.Length, true, false);");
            W.Line("operationState.AdvanceSafe(global::LuminPack.Core.LuminPackWriter.Write<" + Scalar + ">(operationState.Reserve(operationState.Position, blockBytes), blockItems, blockItems.Length, true, false));");
            return;
        }

        W.Line("operationState.EnsureAdditionalCapacity(4);");
        W.Line("operationState.WriteCollectionHeader(blockItems.Length);");
        if (_element.Layout != null)
        {
            W.Line("operationState.EnsureAdditionalCapacity(" + FormatterStorageEmitter.Width(_element.Layout, "blockItems.Length") + ");");
            FormatterStorageEmitter.Payload(W, _element.Layout, "global::System.Runtime.CompilerServices.Unsafe.AsRef(in global::System.Runtime.InteropServices.MemoryMarshal.GetReference(blockItems))", "blockItems.Length", true, "operationState");
        }
        else
        {
            W.Line("foreach (ref readonly var item in blockItems)");
            W.Open();
            WriteElement("item");
            W.Close();
        }

        W.Line("operationState.CheckBuffer();");
    }

    /// <summary>Reads a validated contiguous block before its family publishes the destination.</summary>
    /// <param name = "span">The element storage span expression.</param>
    private void ReadContiguous(string span)
    {
        if (_element!.Layout != null)
        {
            FormatterStorageEmitter.Payload(W, _element.Layout, "global::System.Runtime.InteropServices.MemoryMarshal.GetReference(" + span + ")", "length", false, "operationState");
            return;
        }

        W.Line("if (length != 0) operationState.Read(ref " + Memory + ".As<" + Scalar + ", byte>(ref " + Marshal + ".GetReference(" + span + ")), checked(length * " + Memory + ".SizeOf<" + Scalar + ">()));");
        if (Scalar == "bool")
            W.Line("global::LuminPack.Security.LuminPackInputPolicy.CheckBooleans(" + span + ");");
    }

    /// <summary>Preserves the wrapper's underlying-list probe and its scalar fallback.</summary>
    /// <param name = "local">The local used by this operation.</param>
    internal void WrapperWrite(string local)
    {
        W.Line("int wrapperDepth = 0;");
        W.Line("var wrapperList = global::LuminPack.Code.LuminPackCollectionAccess.GetUnderlyingIList(" + local + ", ref wrapperDepth);");
        W.Line("if (wrapperList != null)");
        W.Open();
        WriteSpan("global::LuminPack.Code.LuminPackMarshal.GetListSpan(wrapperList, wrapperList.Count)");
        W.Close();
        W.Line("else");
        W.Open();
        W.Line("int countPosition = operationState.BeginCollection();");
        W.Line("int count = 0;");
        W.Line("foreach (var item in " + local + ")");
        W.Open();
        W.Line("count++;");
        if (_element!.RawCollection)
        {
            W.Line("operationState.CheckBuffer();");
            W.Line("operationState.Write(ref " + Memory + ".As<" + Scalar + ", byte>(ref " + Memory + ".AsRef(in item)), " + Memory + ".SizeOf<" + Scalar + ">());");
        }
        else
            WriteElement("item");
        W.Close();
        W.Line("operationState.EndCollection(countPosition, count);");
        W.Line("operationState.CheckBuffer();");
        W.Close();
    }

    /// <summary>Writes only the collection builder's logical backing-array range.</summary>
    /// <param name = "local">The local used by this operation.</param>
    internal void BuilderWrite(string local)
    {
        W.Line("var builderView = global::LuminPack.Code.LuminPackMarshal.As<" + Sequence + ", global::LuminPack.Code.ReadOnlyCollectionBuilderView<" + Scalar + ">>(ref " + Memory + ".AsRef(in " + local + "));");
        WriteSpan("global::System.MemoryExtensions.AsSpan(builderView.items, 0, " + local + ".Count)");
    }

    /// <summary>Writes a stable concurrent-bag snapshot through its contiguous element contract.</summary>
    /// <param name = "local">The local used by this operation.</param>
    internal void BagSnapshotWrite(string local)
    {
        if (Supports("BagSnapshot"))
            W.Line("var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentConcurrentBagSnapshot(" + local + ", out int count, out bool pooled);");
        else
        {
            W.Line("var snapshot = " + local + ".ToArray();");
            W.Line("int count = snapshot.Length;");
            W.Line("bool pooled = false;");
        }

        W.Line("try");
        W.Open();
        WriteSpan("new global::System.ReadOnlySpan<" + Scalar + ">(snapshot, 0, count)");
        W.Close();
        W.Line("finally");
        W.Open();
        W.Line("if (pooled) global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Return(snapshot, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + Scalar + ">());");
        W.Close();
    }

    /// <summary>Retains the ordered tree copy before writing a contiguous element block.</summary>
    /// <param name = "local">The local used by this operation.</param>
    internal void TreeSnapshotWrite(string local)
    {
        W.Line("int count = " + local + ".Count;");
        W.Line("var snapshot = global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Rent(count);");
        W.Line("try");
        W.Open();
        W.Line("global::LuminPack.Utility.LuminTreeStorage.CopyTo(" + local + ", snapshot);");
        WriteSpan("new global::System.ReadOnlySpan<" + Scalar + ">(snapshot, 0, count)");
        W.Close();
        W.Line("finally");
        W.Open();
        W.Line("global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Return(snapshot, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + Scalar + ">());");
        W.Close();
    }

    /// <summary>Preserves the queue's three storage words and live dequeue order for arbitrary elements.</summary>
    /// <param name = "local">The local used by this operation.</param>
    private void ForwardedQueueWrite(string local)
    {
        W.Line("int queueSize = " + local + ".Count;");
        W.Line("int countPosition = operationState.BeginCollection();");
        W.Line("if (queueSize != 0)");
        W.Open();
        W.Line("operationState.EnsureAdditionalCapacity(sizeof(int) * 3);");
        W.Line("operationState.Write(0);");
        W.Line("operationState.Write(0);");
        W.Line("operationState.Write(queueSize);");
        W.Line("foreach (var item in " + local + ")");
        W.Open();
        WriteElement("item");
        W.Close();
        W.Close();
        W.Line("operationState.EndCollection(countPosition, queueSize);");
        W.Line("operationState.CheckBuffer();");
    }

    /// <summary>Writes the leased stack snapshot bottom first and returns every reference-bearing slot to its pool safely.</summary>
    /// <param name = "local">The local used by this operation.</param>
    /// <param name = "concurrent">The concurrent used by this operation.</param>
    private void ForwardedStackWrite(string local, bool concurrent)
    {
        if (concurrent)
            W.Line("var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentSnapshot(" + local + ", out var count);");
        else
        {
            W.Line("int count = " + local + ".Count;");
            W.Line("var snapshot = global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Rent(count);");
        }

        W.Line("try");
        W.Open();
        if (!concurrent)
            W.Line(local + ".CopyTo(snapshot, 0);");
        if (Contiguous)
        {
            W.Line("global::System.MemoryExtensions.Reverse(global::System.MemoryExtensions.AsSpan(snapshot, 0, count));");
            WriteSpan("new global::System.ReadOnlySpan<" + Scalar + ">(snapshot, 0, count)");
        }
        else
        {
            W.Line("int countPosition = operationState.BeginCollection();");
            W.Line("for (int index = count - 1; index >= 0; index--)");
            W.Open();
            WriteElement("snapshot[index]");
            W.Close();
            W.Line("operationState.EndCollection(countPosition, count);");
            W.Line("operationState.CheckBuffer();");
        }

        W.Close();
        W.Line("finally");
        W.Open();
        W.Line("global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Return(snapshot, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + Scalar + ">());");
        W.Close();
    }
}
