using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class StringBuilderFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                ");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Length);");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            ");
        sb.AppendLine("            foreach (var chunk in value.GetChunks())");
        sb.AppendLine("            {");
        sb.AppendLine("                int length = checked(chunk.Length * 2);");
        sb.AppendLine("                ref var p = ref writer.GetSpanReference(index);");
        sb.AppendLine("                ref var src = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(global::System.Runtime.InteropServices.MemoryMarshal.Cast<char, byte>(chunk.Span));");
        sb.AppendLine("                global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref p, ref src, (uint)length);");
        sb.AppendLine("                ");
        sb.AppendLine("                writer.Advance(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine("            ");
        sb.AppendLine("#else");
        sb.AppendLine("            writer.WriteUtf16WithLength(index, value.ToString());");
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine("#endif");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                ");
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine("                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            ");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Text.StringBuilder(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("                value.EnsureCapacity(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            var size = checked(length * 2);");
        sb.AppendLine("            ref var p = ref reader.GetSpanReference(index);");
        sb.AppendLine("            var src = global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref p, size));");
        sb.AppendLine("            value.Append(src);");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(size);");
    }
}