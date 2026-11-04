using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Shares scalar JSON enumeration framing without expanding integer formatting into each loop.</summary>
internal static class JsonCollectionElementEmitter
{
    /// <summary>Writes one captured element and updates the enclosing loop's first-element flag.</summary>
    /// <param name="field">The collection contract and runtime capabilities.</param>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The captured current element expression.</param>
    /// <param name="indent">The enclosing indentation.</param>
    /// <param name="isolateInteger">Whether this traversal isolates integer formatting from its loop.</param>
    internal static void Append(LuminLocalFieldData field, StringBuilder sb, string element, string indent, bool isolateInteger = true)
    {
        bool isolate = isolateInteger && field.CanWriteJsonEnumerationElement;
        if (isolate)
        {
            sb.AppendLine(indent + "if (writer.UsesUtf8Enumeration())");
            sb.AppendLine(indent + "{");
            sb.AppendLine(indent + "    writer.WriteEnumerationElement(" + element + ", !isFirst);");
            sb.AppendLine(indent + "    isFirst = false;");
            sb.AppendLine(indent + "}");
            sb.AppendLine(indent + "else");
            sb.AppendLine(indent + "{");
        }
        string body = isolate ? indent + "    " : indent;
        sb.AppendLine(body + "if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine(body + "else isFirst = false;");
        sb.AppendLine(body + "writer.SetFirstElement(true);");
        sb.AppendLine(body + "var temp = " + element + ";");
        sb.AppendLine(body + "writer.WriteValue(in temp);");
        if (isolate) sb.AppendLine(indent + "}");
    }
}
