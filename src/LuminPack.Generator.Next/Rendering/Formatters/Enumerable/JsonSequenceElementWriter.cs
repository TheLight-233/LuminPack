using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Writes an element of a concrete sequence in its existing traversal.</summary>
internal static class JsonSequenceElementWriter
{
    /// <summary>Combines the separator and scalar record when the compiled runtime exposes that operation.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The formatter capability facts.</param>
    /// <param name="sequence">The closed or shared sequence spelling.</param>
    /// <param name="scalar">The element's storage type spelling.</param>
    /// <param name="owner">The existing JSON element formatter.</param>
    /// <param name="writer">The JSON writer local.</param>
    /// <param name="first">The first-element flag local.</param>
    /// <param name="item">The current element local.</param>
    /// <param name="utf8Only">Whether to preserve individual UTF-16 writer calls for this traversal.</param>
    internal static void Emit(CodeWriter w, SharedFormatterSet set, string sequence, string scalar, string owner,
        string writer = "writer", string first = "isFirst", string item = "item", bool utf8Only = false)
    {
        bool fusedInteger = GenericFormatterReuse.Real(scalar) is "int" or "long" &&
            set.FindFormatter(sequence)?.Supports("JsonIntegerElement") == true;
        if (fusedInteger)
        {
            if (utf8Only)
            {
                w.Line("if (default(TLuminProfile).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
                w.Open();
            }
            string element = item;
            if (GenericFormatterReuse.IsUnmanagedPlaceholder(scalar))
            {
                w.Line("var scalarItem = " + item + ";");
                element = w.A("Unsafe") + ".As<" + scalar + ", " + GenericFormatterReuse.Real(scalar) + ">(ref scalarItem)";
            }
            w.Line(writer + ".WriteEnumerationElement<TLuminProfile>(" + element + ", !" + first + ");");
            w.Line(first + " = false;");
            if (!utf8Only)
                return;
            w.Close();
            w.Line("else");
            w.Open();
        }

        w.Line("if (!" + first + ")");
        w.Line("    " + writer + ".WriteByteRaw<TLuminProfile>((byte)',');");
        w.Line("else");
        w.Line("    " + first + " = false;");
        w.Line(writer + ".SetFirstElement(true);");
        w.Line("var temp = " + item + ";");
        w.Line(owner + ".Write<TLuminProfile>(ref " + writer + ", temp);");
        if (utf8Only && fusedInteger)
            w.Close();
    }
}
