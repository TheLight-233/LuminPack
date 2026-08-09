using System;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

/// <summary>Static formatter for System.Tuple and System.ValueTuple arities one through seven.</summary>
public static class TupleFormatter
{
    public static bool IsTuple(string typeName) => typeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal) ||
        typeName.StartsWith("global::System.ValueTuple<", StringComparison.Ordinal);

    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        int count = GetGenericArguments(fieldData.TypeName).Length;
        if (referenceTuple)
        {
            sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
            sb.AppendLine("                writer.Advance(1);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        for (int i = 1; i <= count; i++)
            sb.AppendLine("            writer.WriteValue(value.Item" + i + ");");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        string[] args = GetGenericArguments(fieldData.TypeName);
        if (referenceTuple)
        {
            sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
            sb.AppendLine("            if (!reader.TryReadObjectHead(ref index))");
            sb.AppendLine("            {");
            sb.AppendLine("                value = null;");
            sb.AppendLine("                reader.Advance(1);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        for (int i = 0; i < args.Length; i++)
        {
            sb.AppendLine("            " + args[i] + " item" + i + " = default!;");
            sb.AppendLine("            reader.ReadValue(ref item" + i + ");");
        }
        sb.AppendLine("            value = new " + fieldData.TypeName + "(" + JoinItems(args.Length) + ");");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        int count = GetGenericArguments(fieldData.TypeName).Length;
        if (referenceTuple)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNull();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            writer.WriteArrayStart();");
        for (int i = 1; i <= count; i++)
        {
            if (i > 1) sb.AppendLine("            writer.WriteByteRaw((byte)',');");
            sb.AppendLine("            writer.SetFirstElement(true);");
            sb.AppendLine("            var item" + i + " = value.Item" + i + ";");
            sb.AppendLine("            writer.WriteValue(in item" + i + ");");
        }
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        string[] args = GetGenericArguments(fieldData.TypeName);
        if (referenceTuple)
        {
            sb.AppendLine("            if (reader.IsNull())");
            sb.AppendLine("            {");
            sb.AppendLine("                value = null;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        for (int i = 0; i < args.Length; i++)
        {
            sb.AppendLine("            reader.Read();");
            sb.AppendLine("            " + args[i] + " item" + i + " = default!;");
            sb.AppendLine("            reader.ReadValue(ref item" + i + ");");
        }
        sb.AppendLine("            reader.Read();");
        sb.AppendLine("            value = new " + fieldData.TypeName + "(" + JoinItems(args.Length) + ");");
    }

    private static string JoinItems(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("item").Append(i);
        }
        return sb.ToString();
    }
}
