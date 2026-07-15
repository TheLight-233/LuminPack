using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class UnmanagedFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);");
        sb.AppendLine($"            writer.Advance(Unsafe.SizeOf<{data.TypeName}>());");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine($"            value = Unsafe.ReadUnaligned<{data.TypeName}>(ref reader.GetCurrentSpanReference());");
        sb.AppendLine($"            reader.Advance(Unsafe.SizeOf<{data.TypeName}>());");
    }
}