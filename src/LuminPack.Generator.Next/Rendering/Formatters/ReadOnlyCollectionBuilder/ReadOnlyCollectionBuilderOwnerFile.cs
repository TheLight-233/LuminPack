namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ReadOnlyCollectionBuilder&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The write stores the builder's logical backing-array range as one block. The read creates the builder with the stored
/// capacity, or clears and grows a reused one, fills the backing array in place and publishes the size.</remarks>
internal static class ReadOnlyCollectionBuilderOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(WrapperTraversal.WriteBuilderRoot, Read, "value.Add(item);");

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
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "(length);"));
        w.Line("else");
        w.Open();
        w.Line("value.Clear();");
        w.Line("if (value.Capacity < length)");
        w.Indented(() => w.Line("value.Capacity = length;"));
        w.Close();
        w.Blank();
        w.Line("var view = global::LuminPack.Code.LuminPackMarshal.As<" + c.Sequence + ", global::LuminPack.Code.ReadOnlyCollectionBuilderView<" + t + ">>(ref value);");
        w.Line("if (length != 0)");
        w.Open();
        w.Line("global::System.Span<" + t + "> span = global::System.MemoryExtensions.AsSpan(view.items, 0, length);");
        WrapperTraversal.ReadSpan(c, "span");
        w.Close();
        w.Line("view.size = length;");
    }
}
