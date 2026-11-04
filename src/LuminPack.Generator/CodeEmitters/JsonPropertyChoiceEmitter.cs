using System.Linq;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Matches two fixed ASCII property names through the reader's decoded token spans.</summary>
internal static class JsonPropertyChoiceEmitter
{
    /// <summary>Consumes the selected string and keeps only the choice before the next token invalidates its span.</summary>
    /// <param name="field">The referenced reader capabilities.</param>
    /// <param name="sb">The operation receiving generated statements.</param>
    /// <param name="first">The first ASCII property name.</param>
    /// <param name="second">The second ASCII property name.</param>
    internal static void Append(LuminLocalFieldData field, StringBuilder sb, string first, string second)
    {
        if (field.CanReadJsonStringChoice)
        {
            string Bytes(string text) => "new byte[] { " + string.Join(", ", text.Select(c => ((int)c).ToString(System.Globalization.CultureInfo.InvariantCulture))) + " }";
            sb.AppendLine("                    int propertyChoice = reader.ReadStringChoice(" + Bytes(first) + ", \"" + first + "\".AsSpan(), " + Bytes(second) + ", \"" + second + "\".AsSpan());");
        }
        else
        {
            sb.AppendLine("                    string propertyName = reader.ReadString();");
            sb.AppendLine("                    int propertyChoice = propertyName == \"" + first + "\" ? 1 : propertyName == \"" + second + "\" ? 2 : 0;");
        }
    }
}
