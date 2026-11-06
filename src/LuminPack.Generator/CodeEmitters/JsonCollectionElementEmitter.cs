using System.Text;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Shares JSON enumeration framing for every element formatter.</summary>
internal static class JsonCollectionElementEmitter
{
    /// <summary>Writes one captured element and updates the enclosing loop's first-element flag.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The captured current element expression.</param>
    /// <param name="indent">The enclosing indentation.</param>
    internal static void Append(StringBuilder sb, string element, string indent)
    {
        sb.AppendLine(indent + "if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine(indent + "else isFirst = false;");
        sb.AppendLine(indent + "writer.SetFirstElement(true);");
        sb.AppendLine(indent + "var temp = " + element + ";");
        sb.AppendLine(indent + "writer.WriteValue(in temp);");
    }
}
