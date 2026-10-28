using System.Globalization;
using System.Text;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Matches fixed ASCII property contracts without allocating property-name strings.</summary>
internal static class JsonPropertyNameEmitter
{
    /// <summary>Reads the property token and emits a one-based match, or zero for an unknown name.</summary>
    /// <param name="sb">The statements receiving the generated matcher.</param>
    /// <param name="names">The fixed ASCII names in their matching order.</param>
    internal static void Append(StringBuilder sb, params string[] names)
    {
        sb.AppendLine("                int property;");
        sb.AppendLine("                if (reader.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var propertyName = reader.ReadStringUtf8();");
        AppendChoice(sb, names);
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    var propertyName = reader.ReadStringUtf16();");
        AppendChoice(sb, names);
        sb.AppendLine("                }");
        sb.AppendLine("                reader.CompleteValue();");
    }

    /// <summary>Compares token code units using constants supported by both span element widths.</summary>
    /// <param name="sb">The generated branch body.</param>
    /// <param name="names">The fixed ASCII names.</param>
    private static void AppendChoice(StringBuilder sb, string[] names)
    {
        sb.Append("                    property = ");
        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string name = names[nameIndex];
            sb.Append("propertyName.Length == ").Append(name.Length);
            for (int character = 0; character < name.Length; character++)
                sb.Append(" && propertyName[").Append(character).Append("] == ")
                    .Append(((int)name[character]).ToString(CultureInfo.InvariantCulture));
            sb.Append(" ? ").Append(nameIndex + 1).Append(" : ");
        }
        sb.AppendLine("0;");
    }
}
