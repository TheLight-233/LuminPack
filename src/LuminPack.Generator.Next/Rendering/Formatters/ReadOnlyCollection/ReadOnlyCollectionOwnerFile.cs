using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ReadOnlyCollection&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The write is the shared wrapper write. The binary reader can refill an existing array-backed wrapper or allocate an array and wrap it. The JSON read stages
/// its elements in a list that the new wrapper then holds.</remarks>
internal static class ReadOnlyCollectionOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(WrapperTraversal.WriteRoot, Read, "staged.Add(item);",
        c => c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">(" + WrapperTraversal.JsonCapacity(c) + ");"),
        c => c.W.Line("value = new " + c.Sequence + "(staged);"), readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec, inlineRawRead: true);

    /// <summary>Handles read for <c>ReadOnlyCollectionOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        string t = c.Scalar;

        if (!c.Counted && c.Supports("ReadArrayBackedWrapper"))
        {
            w.Line("if (value is not null && length != 0 && global::LuminPack.Code.LuminPackCollectionAccess.TryReadArrayBackedWrapper(ref operationState, value, length))");
            w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        }
        w.Line("var array = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + t + ">(length);");
        w.Line("if (length != 0)");
        w.Open();
        w.Line("var span = global::System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref global::LuminPack.Code.LuminPackMarshal.GetArrayReference(array), length);");
        WrapperTraversal.ReadSpan(c, "span");
        w.Close();
        w.Line("value = new " + c.Sequence + "(array);");
    }
}
