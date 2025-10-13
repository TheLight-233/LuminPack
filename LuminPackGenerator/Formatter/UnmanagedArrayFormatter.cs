using System.Text;

namespace LuminPack.SourceGenerator.Formatter;

public static class UnmanagedArrayFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteUnmanagedArray(ref offset, value, out var length);");
        sb.AppendLine();
        sb.AppendLine("        writer.Advance(length);");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        reader.ReadUnmanagedArray(ref offset, ref value, out var length);");
        sb.AppendLine();
        sb.AppendLine("        reader.Advance(length);");
    }
}