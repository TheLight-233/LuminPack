using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Writes an element of a concrete concurrent sequence in its existing traversal.</summary>
internal static class ConcurrentJsonElementWriter
{
    /// <summary>Combines the separator and scalar record when the compiled runtime exposes that operation.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The formatter capability facts.</param>
    /// <param name="sequence">The closed or shared sequence spelling.</param>
    /// <param name="scalar">The element's storage type spelling.</param>
    /// <param name="owner">The existing JSON element formatter.</param>
    internal static void Emit(CodeWriter w, SharedFormatterSet set, string sequence, string scalar, string owner)
    {
        if (GenericFormatterReuse.Real(scalar) is "int" or "long" &&
            set.FindFormatter(sequence)?.Supports("ConcurrentJsonIntegerElement") == true)
        {
            string element = "item";
            if (GenericFormatterReuse.IsUnmanagedPlaceholder(scalar))
            {
                w.Line("var scalarItem = item;");
                element = w.A("Unsafe") + ".As<" + scalar + ", " + GenericFormatterReuse.Real(scalar) + ">(ref scalarItem)";
            }
            w.Line("writer.WriteEnumerationElement<TLuminProfile>(" + element + ", !isFirst);");
            w.Line("isFirst = false;");
            return;
        }

        w.Line("if (!isFirst)");
        w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
        w.Line("else");
        w.Line("    isFirst = false;");
        w.Line("writer.SetFirstElement(true);");
        w.Line("var temp = item;");
        w.Line(owner + ".Write<TLuminProfile>(ref writer, temp);");
    }
}
