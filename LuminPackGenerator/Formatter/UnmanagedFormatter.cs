using System.Text;

namespace LuminPack.SourceGenerator.Formatter;

public static class UnmanagedFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);");
        sb.AppendLine("        writer.Advance(Unsafe.SizeOf<T>());");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        value = Unsafe.ReadUnaligned<T>(ref reader.GetCurrentSpanReference());");
        sb.AppendLine("        reader.Advance(Unsafe.SizeOf<T>());");
    }
}