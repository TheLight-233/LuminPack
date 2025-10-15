using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class BitArrayFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            ref var view = ref LuminPackMarshal.As<global::System.Collections.BitArray, BitArrayView>(ref Unsafe.AsRef(in value));");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteUnmanagedArray(ref index, view.m_array, out var offset);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(offset);");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadObjectHead(ref index))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanaged(out int length);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var bitArray = new global::System.Collections.BitArray(length, false);");
        sb.AppendLine();
        sb.AppendLine("            ref var view = ref LuminPackMarshal.As<global::System.Collections.BitArray, BitArrayView>(ref bitArray);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanagedArray(ref index, ref view.m_array!, length, out var offset);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(offset);");
        sb.AppendLine();
        sb.AppendLine("            value = bitArray;");
    }
}