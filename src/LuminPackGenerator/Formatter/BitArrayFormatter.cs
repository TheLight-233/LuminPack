using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class BitArrayFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
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
        sb.AppendLine("            ref var view = ref global::LuminPack.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Parsers.BitArrayParser.BitArrayView>(ref Unsafe.AsRef(in value));");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, view.m_length);");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.WriteUnmanagedArrayWithOutHeader(ref index, view.m_array, view.m_array.Length, out var offset);");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.Advance(4 + offset);");
        sb.AppendLine("            writer.CheckBuffer();");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
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
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.BitArray(length, false);");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            ");
        sb.AppendLine("            ref var view = ref global::LuminPack.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Parsers.BitArrayParser.BitArrayView>(ref value);");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.ReadUnmanagedArray(ref index, ref view.m_array!, view.m_array.Length, out var offset);");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(offset);");
    }
}