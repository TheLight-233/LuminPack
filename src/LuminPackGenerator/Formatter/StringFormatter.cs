using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class StringFormatter
{
	public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
	{
		sb.AppendLine("            int offset = writer.WriteString(value) + writer.StringRecordLength();");
		sb.AppendLine("            writer.Advance(offset);");
		sb.AppendLine("            writer.CheckBuffer();");
	}

	public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
	{
		sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
		sb.AppendLine();
		sb.AppendLine("            value = reader.ReadStringAndAdvance(ref index);");
	}
}
