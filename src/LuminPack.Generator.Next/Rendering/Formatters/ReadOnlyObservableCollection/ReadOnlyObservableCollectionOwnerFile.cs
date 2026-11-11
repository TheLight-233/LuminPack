namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ReadOnlyObservableCollection&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The write is the shared wrapper write. The read fills the exactly sized list of a new observable collection in place
/// and wraps it; the JSON read stages its elements in that observable collection.</remarks>
internal static class ReadOnlyObservableCollectionOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(WrapperTraversal.WriteRoot, Read, "staged.Add(item);",
        c => c.W.Line("var staged = new global::System.Collections.ObjectModel.ObservableCollection<" + c.Scalar + ">();"),
        c => c.W.Line("value = new " + c.Sequence + "(staged);"));

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        string t = c.Scalar;
        string observable = "global::System.Collections.ObjectModel.ObservableCollection<" + t + ">";
        w.Line("var observable = new " + observable + "();");
        w.Line("var view = global::LuminPack.Code.LuminPackMarshal.As<" + observable + ", global::LuminPack.Code.ObservableCollectionView<" + t + ">>(ref observable);");
        w.Line("var span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(view.items!, length);");
        w.Line("if (length != 0)");
        w.Open();
        WrapperTraversal.ReadSpan(c, "span");
        w.Close();
        w.Line("value = new " + c.Sequence + "(observable);");
    }
}
