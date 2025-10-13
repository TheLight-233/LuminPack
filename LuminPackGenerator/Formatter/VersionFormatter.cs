using System.Text;

namespace LuminPack.SourceGenerator.Formatter;

public static class VersionFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteUnmanaged(ref index, value.Major, value.Minor, value.Build, value.Revision);");
        sb.AppendLine("        writer.Advance(16);");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (!reader.TryReadObjectHead(ref index))");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        reader.ReadUnmanaged(ref index, out int major, out int minor, out int build, out int revision);");
        sb.AppendLine();
        sb.AppendLine("        if (revision == -1)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (build == -1)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new Version(major, minor);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new Version(major, minor, build);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            value = new Version(major, minor, build, revision);");
        sb.AppendLine("        }");
    }
}