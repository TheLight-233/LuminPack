using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ObservableCollection&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The write is the shared wrapper write. The read can fill a fresh or unobserved backing list in place. An observed
/// or derived destination is populated through the public insertion operation.</remarks>
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
        w.Open();
        if (c.Supports("UnobservedObservableList"))
            ReadObserved(c);
        if (c.NullableBlock != null)
        {
            w.Line("if (length != 0)");
            w.Open();
            // Clear can invoke user code. Transfer the validated input into the lease before that callback boundary.
            c.PooledRead(() =>
            {
                w.Line("value.Clear();");
                w.Line("var reusedView = global::LuminPack.Code.LuminPackMarshal.As<" + c.Sequence +
                    ", global::LuminPack.Code.ObservableCollectionView<" + t + ">>(ref value);");
                w.Line("items.CopyTo(global::LuminPack.Code.LuminPackMarshal.GetListSpan(reusedView.items!, length));");
            });
            w.Line("goto operationComplete_RootRead;");
            w.Close();
        }
        w.Line("value.Clear();");
        w.Close();
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        w.Line("var view = global::LuminPack.Code.LuminPackMarshal.As<" + c.Sequence + ", global::LuminPack.Code.ObservableCollectionView<" + t + ">>(ref value);");
        w.Line("var span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(view.items!, length);");
        WrapperTraversal.ReadSpan(c, "span");
    }

    /// <summary>Reads into an observed or derived destination using the framework's notification and reentrancy rules.</summary>
    /// <param name="c">The binary owner context.</param>
    private static void ReadObserved(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (length != 0 && global::LuminPack.Code.LuminPackCollectionAccess.GetUnobservedObservableList(value) is null)");
        w.Open();
        if (c.ContiguousLayout || c.NativeNullableElement)
        {
            c.PooledRead(() =>
            {
                w.Line("value.Clear();");
                w.Line("foreach (var item in items) value.Add(item);");
            });
        }
        else
        {
            w.Line("value.Clear();");
            w.Line("for (int remaining = length; remaining != 0; remaining--)");
            w.Open();
            w.Line(c.Scalar + " item = default!;");
            c.ReadElement("item", fresh: true);
            w.Line("value.Add(item);");
            w.Close();
        }
        w.Line("goto operationComplete_RootRead;");
        w.Close();
    }
}
