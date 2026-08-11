using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static class StringEmitter
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

	public static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
	{
		sb.AppendLine("            evaluator += evaluator.GetStringLength(ref value);");
	}

	public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
	{
		sb.AppendLine("            writer.WriteString(value);");
	}

	public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
	{
		sb.AppendLine("            if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.Null)");
		sb.AppendLine("            {");
		sb.AppendLine("                value = null;");
		sb.AppendLine("                return;");
		sb.AppendLine("            }");
		sb.AppendLine();
		sb.AppendLine("            if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
		sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(\"Expected string token\");");
		sb.AppendLine();
		sb.AppendLine("            value = reader.ReadString();");
	}
}
