using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides version emitter functionality.</summary>
public static class VersionEmitter
{
    /// <summary>Appends binary writing statements for version values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteObjectHeader(4);");
        sb.AppendLine("            writer.WriteUnmanaged(ref index, value.Major, value.Minor, value.Build, value.Revision);");
        sb.AppendLine("            writer.Advance(16);");
    }

    /// <summary>Appends binary reading statements for version values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadObjectHead())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        // The component load is an unchecked memory kernel, so all four components are validated first.
        sb.AppendLine("            reader.EnsureReadable(reader.Position, 16);");
        sb.AppendLine("            reader.ReadUnmanaged(ref index, out int major, out int minor, out int build, out int revision);");
        sb.AppendLine("            reader.Advance(16);");
        sb.AppendLine();
        sb.AppendLine("            if (revision == -1)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (build == -1)");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = new Version(major, minor);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = new Version(major, minor, build);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new Version(major, minor, build, revision);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends JSON writing statements for version values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteString(value.ToString());");
    }

    /// <summary>Appends JSON reading statements for version values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = global::System.Version.Parse(reader.ReadString());");
    }
}
