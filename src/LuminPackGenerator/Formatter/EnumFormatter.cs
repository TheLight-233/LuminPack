using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

/// <summary>
/// Static counterpart of the enum branch in the unmanaged formatter.
/// The JSON representation deliberately remains the integer token format.
/// </summary>
public static class EnumFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            global::System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);");
        sb.AppendLine("            writer.Advance(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">());");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            reader.EnsureReadable(reader.GetCurrentSpanOffset(), global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">());");
        sb.AppendLine("            value = global::System.Runtime.CompilerServices.Unsafe.ReadUnaligned<" + data.TypeName + ">(ref reader.GetCurrentSpanReference());");
        sb.AppendLine("            reader.Advance(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">());");
    }

    public static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            evaluator += global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">();");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            var local = value;");
        sb.AppendLine("            var intValue = global::System.Runtime.CompilerServices.Unsafe.As<" + data.TypeName + ", int>(ref local);");
        sb.AppendLine("            writer.WriteInt(intValue);");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            var intValue = reader.ReadInt();");
        sb.AppendLine("            value = global::System.Runtime.CompilerServices.Unsafe.As<int, " + data.TypeName + ">(ref intValue);");
    }
}
