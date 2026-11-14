using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
internal sealed partial class EnumerableOwnerContext
{
    private const string PackMarshal = "global::LuminPack.Code.LuminPackMarshal";
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
    internal void WriteElement(string expression)
    {
        // A nullable reference-free scalar element is native storage, as the original element loops copied it.
        if (NativeNullableElement)
        {
            W.Line("operationState.CheckBuffer();");
            W.Line("operationState.Write(ref global::System.Runtime.CompilerServices.Unsafe.As<" + _element!.Type + ", byte>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in " +
                expression + ")), global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + _element.Type + ">());");
            return;
        }
        FormatterElementEmitter.Write(W, _set, _element!, expression, "operationState");
    }

    /// <summary>Whether the forwarded element is a nullable reference-free scalar without a storage layout.</summary>
    internal bool NativeNullableElement => _element is { Definition: GenericIdentity.Nullable, Layout: null } element &&
        element.Arguments.Length == 1 && element.Arguments[0].RawCollection;
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
        W.Line("var target = " + PackMarshal + ".CreateSpan(ref " + PackMarshal + ".GetNotNullArrayReference(selected), length);");
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
        WriteSpan("new global::System.ReadOnlySpan<" + Scalar + ">(builderView.items, 0, " + local + ".Count)");
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
        if (Text && Supports("WriteCheckedStringSpan"))
        {
            W.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile + ")");
            W.Open();
            W.Line("operationState.EnsureAdditionalCapacity(4);");
            W.Line("operationState.WriteCollectionHeader(count);");
            W.Line("operationState.WriteCheckedStrings<TLuminProfile>(new global::System.ReadOnlySpan<string>(snapshot, 0, count));");
            W.Line("operationState.CheckBuffer();");
            W.Close();
            W.Line("else");
            W.Open();
        }
        WriteSpan("new global::System.ReadOnlySpan<" + Scalar + ">(snapshot, 0, count)");
        if (Text && Supports("WriteCheckedStringSpan"))
            W.Close();
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
        if (Text && Supports("WriteCheckedStringSpan"))
        {
            W.Line("operationState.EnsureAdditionalCapacity(4);");
            W.Line("operationState.WriteCollectionHeader(count);");
            W.Line("operationState.WriteCheckedStrings<TLuminProfile>(" + PackMarshal + ".CreateReadOnlySpan(ref " + PackMarshal + ".GetNotNullArrayReference(snapshot), count));");
            W.Line("operationState.CheckBuffer();");
        }
        else
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
        if (!concurrent && Text && Supports("WriteStringStackStorage"))
        {
            W.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteStringStackStorage<TLuminProfile>(ref operationState, " + local + ", true))");
            W.Line("    goto operationComplete_RootWrite;");
        }
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
        if (Text && Contiguous)
        {
            W.Line("operationState.EnsureAdditionalCapacity(sizeof(int));");
            W.Line("operationState.WriteCollectionHeader(count);");
            bool checkedRun = concurrent && Supports("WriteCheckedStringSpan");
            if (checkedRun)
            {
                W.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile + ")");
                W.Open();
                W.Line("var items = new global::System.Span<string>(snapshot, 0, count);");
                W.Line("global::System.MemoryExtensions.Reverse(items);");
                W.Line("operationState.WriteCheckedStrings<TLuminProfile>(items);");
                W.Close();
                W.Line("else");
            }
            W.Line("if (count != 0)");
            W.Open();
            W.Line("ref var first = ref " + PackMarshal + ".GetNotNullArrayReference(snapshot);");
            W.Line("int remaining = count;");
            W.Line("do");
            W.Open();
            W.Line("operationState.CheckBuffer();");
            W.Line("operationState.Write<TLuminProfile>(" + Memory + ".Add(ref first, (nint)(uint)--remaining));");
            W.Close();
            W.Line("while (remaining != 0);");
            W.Close();
            W.Line("operationState.CheckBuffer();");
        }
        else if (Contiguous)
        {
            W.Line("var active = " + PackMarshal + ".CreateSpan(ref " + PackMarshal + ".GetNotNullArrayReference(snapshot), count);");
            W.Line("global::System.MemoryExtensions.Reverse(active);");
            WriteSpan("active");
        }
        else
        {
            W.Line("int countPosition = operationState.BeginCollection();");
            W.Line("if (count != 0)");
            W.Open();
            W.Line("ref var first = ref " + PackMarshal + ".GetNotNullArrayReference(snapshot);");
            W.Line("int remaining = count;");
            W.Line("do");
            W.Open();
            W.Line("ref var item = ref " + Memory + ".Add(ref first, (nint)(uint)--remaining);");
            WriteElement("item");
            W.Close();
            W.Line("while (remaining != 0);");
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
