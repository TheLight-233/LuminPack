using System;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

/// <summary>Static formatter for the span-backed parser inventory.</summary>
public static class MemoryFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseType(fieldData.TypeName);
		string length = baseType == "global::System.ArraySegment" ? "value.Count" :
			baseType == "global::System.Buffers.ReadOnlySequence" ? "checked((int)value.Length)" : "value.Length";
        if (baseType == "global::System.ArraySegment")
        {
            sb.AppendLine("            if (value.Array is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                ref var index = ref writer.GetCurrentSpanOffset();");
            sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
            sb.AppendLine("                writer.Advance(4);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, " + length + ");");
        sb.AppendLine("            writer.Advance(4);");
        if (baseType == "global::System.Buffers.ReadOnlySequence")
        {
            sb.AppendLine("            foreach (var memory in value)");
            sb.AppendLine("            {");
            sb.AppendLine("                foreach (var item in memory.Span)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var temp = item;");
            sb.AppendLine("                    writer.WriteValue(temp);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        else if (baseType == "global::System.Memory")
        {
            sb.AppendLine("            foreach (ref var item in value.Span)");
            sb.AppendLine("            {");
            sb.AppendLine("                var temp = item;");
            sb.AppendLine("                writer.WriteValue(temp);");
            sb.AppendLine("            }");
        }
        else if (baseType == "global::System.ReadOnlyMemory")
        {
            sb.AppendLine("            foreach (var item in value.Span)");
            sb.AppendLine("            {");
            sb.AppendLine("                var temp = item;");
            sb.AppendLine("                writer.WriteValue(temp);");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            foreach (var item in value)");
            sb.AppendLine("            {");
            sb.AppendLine("                var temp = item;");
            sb.AppendLine("                writer.WriteValue(temp);");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseType(fieldData.TypeName);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        AppendNullAssignment(sb, baseType);
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            var array = new " + elementType + "[length];");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.ReadValue(ref array[i]);");
        sb.AppendLine("            }");
        AppendArrayAssignment(sb, baseType, elementType);
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseType(fieldData.TypeName);
        if (baseType == "global::System.ArraySegment")
        {
            sb.AppendLine("            if (value.Array is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNull();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            bool isFirst = true;");
        if (baseType == "global::System.Buffers.ReadOnlySequence")
        {
            sb.AppendLine("            foreach (var memory in value)");
            sb.AppendLine("            {");
            sb.AppendLine("                foreach (var item in memory.Span)");
            sb.AppendLine("                {");
            AppendJsonItemWrite(sb, "                    ");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        else if (baseType == "global::System.Memory")
        {
            sb.AppendLine("            foreach (ref var item in value.Span)");
            sb.AppendLine("            {");
            AppendJsonItemWrite(sb, "                ");
            sb.AppendLine("            }");
        }
        else if (baseType == "global::System.ReadOnlyMemory")
        {
            sb.AppendLine("            foreach (var item in value.Span)");
            sb.AppendLine("            {");
            AppendJsonItemWrite(sb, "                ");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            foreach (var item in value)");
            sb.AppendLine("            {");
            AppendJsonItemWrite(sb, "                ");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseType(fieldData.TypeName);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        AppendNullAssignment(sb, baseType);
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            var items = new global::System.Collections.Generic.List<" + elementType + ">();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref item);");
        sb.AppendLine("                items.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine("            var array = items.ToArray();");
        AppendArrayAssignment(sb, baseType, elementType);
    }

    private static void AppendJsonItemWrite(StringBuilder sb, string indent)
    {
        sb.AppendLine(indent + "if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine(indent + "else isFirst = false;");
        sb.AppendLine(indent + "writer.SetFirstElement(true);");
        sb.AppendLine(indent + "var temp = item;");
        sb.AppendLine(indent + "global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in temp);");
    }

    private static void AppendNullAssignment(StringBuilder sb, string baseType)
    {
        sb.AppendLine(baseType == "global::System.ArraySegment" ? "                value = default;" : "                value = default;");
    }

    private static void AppendArrayAssignment(StringBuilder sb, string baseType, string elementType)
    {
        if (baseType == "global::System.ArraySegment")
            sb.AppendLine("            value = new global::System.ArraySegment<" + elementType + ">(array);");
        else if (baseType == "global::System.Buffers.ReadOnlySequence")
            sb.AppendLine("            value = new global::System.Buffers.ReadOnlySequence<" + elementType + ">(array);");
        else
            sb.AppendLine("            value = array;");
    }

    private static string GetBaseType(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }
}
