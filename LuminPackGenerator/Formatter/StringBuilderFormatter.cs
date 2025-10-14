using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class StringBuilderFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET7_0_OR_GREATER");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Length);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var chunk in value.GetChunks())");
        sb.AppendLine("            {");
        sb.AppendLine("                int index1 = checked(chunk.Length * 2);");
        sb.AppendLine("                ref var p = ref Unsafe.Add(ref Unsafe.AsRef<byte>(writer._bufferStart.ToPointer()), (nint)index1);");
        sb.AppendLine("                ref var src = ref LuminPackMarshal.As<char, byte>(ref MemoryMarshal.GetReference(chunk.Span));");
        sb.AppendLine("                Unsafe.CopyBlockUnaligned(ref p, ref src, (uint)chunk.Length * 2);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(chunk.Length * 2);");
        sb.AppendLine("            }");
        sb.AppendLine("#else");
        sb.AppendLine("            writer.WriteUtf16WithLength(index, value.ToString());");
        sb.AppendLine("#endif");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new StringBuilder(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("                value.EnsureCapacity(length);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var size = checked(length * 2);");
        sb.AppendLine("            ref var p = ref reader.GetSpanReference(size);");
        sb.AppendLine("            var src = LuminPackMarshal.CreateSpan(ref Unsafe.As<byte, char>(ref p), length);");
        sb.AppendLine("            value.Append(src);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(size);");
    }
}