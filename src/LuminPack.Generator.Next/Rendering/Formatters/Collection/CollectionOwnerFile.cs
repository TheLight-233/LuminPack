using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>Collection&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The write is the shared wrapper write. The read creates the collection over an exactly sized list, or keeps the
/// items of a reused exact collection like a reused list and clears any other one, then fills the list storage in place, or
/// adds each element when the wrapper holds another list type.</remarks>
internal static class CollectionOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(WrapperTraversal.WriteRoot, Read, "value.Add(item);",
        jsonBefore: c => WrapperTraversal.PrepareJson(c, () =>
        {
            string capacity = WrapperTraversal.JsonCapacity(c);
            string items = capacity.Length == 0 ? "" : "new global::System.Collections.Generic.List<" + c.Scalar + ">(" + capacity + ")";
            c.W.Line("value = new " + c.Sequence + "(" + items + ");");
        }), readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec, inlineRawRead: true);

    /// <summary>Handles read for <c>CollectionOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        string t = c.Scalar;
        string exactList = "typeof(global::System.Collections.Generic.List<" + t + ">)";
        string viewType = "global::LuminPack.Code.CollectionView<" + t + ">";
        if (c.NullableBlock != null)
        {
            w.Line("if (value is not null && length != 0 && (value.GetType() != typeof(" + c.Sequence + ") ||");
            w.Indented(() => w.Line("global::LuminPack.Code.LuminPackMarshal.As<" + c.Sequence + ", " + viewType +
                ">(ref value).items?.GetType() != " + exactList + "))"));
            w.Open();
            // A foreign backing or derived collection can run callbacks during its clear and insertion operations.
            c.PooledRead(() =>
            {
                w.Line("if (value.GetType() != typeof(" + c.Sequence + ")) value.Clear();");
                w.Line("var backing = global::LuminPack.Code.LuminPackMarshal.As<" + c.Sequence + ", " + viewType + ">(ref value).items;");
                w.Line("if (backing?.GetType() == " + exactList + ")");
                w.Indented(() => w.Line("items.CopyTo(global::LuminPack.Code.LuminPackMarshal.GetListSpan((global::System.Collections.Generic.List<" +
                    t + ">)backing!, length));"));
                w.Line("else");
                w.Open();
                w.Line("if (value.Count != 0) value.Clear();");
                w.Line("for (int index = 0; index < length; index++) value.Add(pooled[index]);");
                w.Close();
            });
            w.Line("goto operationComplete_RootRead;");
            w.Close();
        }
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "(new global::System.Collections.Generic.List<" + t + ">(length));"));
        // An exact collection's Clear only clears its list, so the list's items stay as reuse seeds like a reused list;
        // a derived collection keeps its overridden ClearItems.
        w.Line("else if (length == 0 || value.GetType() != typeof(" + c.Sequence + "))");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        w.Line("var view = global::LuminPack.Code.LuminPackMarshal.As<" + c.Sequence + ", " + viewType + ">(ref value);");
        if (c.NullableBlock != null)
        {
            w.Line("var slots = global::LuminPack.Code.LuminPackMarshal.GetListSpan(view.items!, length);");
            c.ReadBlockInPlace("slots");
            return;
        }
        // An existing wrapper may hold any list; only an exact List matches the storage view.
        w.Line("if (view.items is null || view.items.GetType() != " + exactList + ")");
        w.Open();
        w.Line("if (value.Count != 0)");
        w.Indented(() => w.Line("value.Clear();"));
        if (c.ContiguousLayout)
        {
            c.PooledRead(() => w.Line("for (int index = 0; index < length; index++) value.Add(pooled[index]);"));
        }
        else
        {
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            w.Line(t + " item = default;");
            c.ReadElement("item");
            w.Line("value.Add(item);");
            w.Close();
            w.Line("while (++index < length);");
        }
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        if (c.ContiguousLayout)
            w.Open();
        w.Line("var span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(view.items!, length);");
        c.ReadBlockInPlace("span");
        if (c.ContiguousLayout)
            w.Close();
    }
}
