using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class BigIntegerFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            #if !UNITY_2021_2_OR_NEWER");
        sb.AppendLine("            Span<byte> temp = stackalloc byte[255];");
        sb.AppendLine("            if (value.TryWriteBytes(temp, out var written))");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteUnmanagedSpan(ref index, temp.Slice(written), out var offset);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(offset);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            #endif");
        sb.AppendLine("            {");
        sb.AppendLine("                var byteArray = value.ToByteArray();");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteUnmanagedArray(ref index, byteArray, out var offset);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(offset);");
        sb.AppendLine("            }");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            ref var src = ref reader.GetSpanReference(length);");
        sb.AppendLine("            value = new BigInteger(MemoryMarshal.CreateReadOnlySpan(ref src, length));");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length);");
    }
}