using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class UnmanagedArrayFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteUnmanagedArray(ref offset, value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(length);");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanagedArray(ref offset, ref value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length);");
    }

    // ── Compress variants ────────────────────────────────────────────────────

    public static void GenerateSerializeCodeWithCompress(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            writer.DangerousWriteUnmanagedArrayWithCompress(ref offset, value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(length);");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCodeWithCompress(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            reader.DangerousReadUnmanagedArrayWithCompress(ref offset, ref value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length);");
    }
}