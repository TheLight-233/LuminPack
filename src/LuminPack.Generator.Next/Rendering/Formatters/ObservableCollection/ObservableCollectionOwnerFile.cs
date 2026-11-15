using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ObservableCollection&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The write is the shared wrapper write. The read creates or clears the collection and fills its backing list storage
/// in place.</remarks>
internal static class ObservableCollectionOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(WrapperTraversal.WriteRoot, Read, "value.Add(item);",
        jsonBefore: c => WrapperTraversal.PrepareJson(c, () =>
        {
            c.W.Line("value = new " + c.Sequence + "();");
            WrapperTraversal.SizeObservableJson(c, "value");
        }),
        jsonAddBody: c => WrapperTraversal.AppendObservable(c, "value"), readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Handles read for <c>ObservableCollectionOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        string t = c.Scalar;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        w.Line("var view = global::LuminPack.Code.LuminPackMarshal.As<" + c.Sequence + ", global::LuminPack.Code.ObservableCollectionView<" + t + ">>(ref value);");
        w.Line("var span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(view.items!, length);");
        WrapperTraversal.ReadSpan(c, "span");
    }
}
