using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

/// <summary>
/// Shared size evaluators for closed collection types. These preserve the old parser
/// behavior while dispatching each element through the source-generated formatter cache.
/// </summary>
public static class CollectionEvaluatorFormatter
{
    public static void GenerateArrayCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = fieldData.TypeName.Substring(0, fieldData.TypeName.LastIndexOf("[]", System.StringComparison.Ordinal));
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">())");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            foreach (ref var item in value.AsSpan())");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator.CalculateOffset(ref item);");
        sb.AppendLine("            }");
    }

    public static void GenerateEnumerableCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = FormatterDiscovery.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " current = item;");
        sb.AppendLine("                evaluator.CalculateOffset(ref current);");
        sb.AppendLine("            }");
    }

    public static void GenerateValueEnumerableCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = FormatterDiscovery.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " current = item;");
        sb.AppendLine("                evaluator.CalculateOffset(ref current);");
        sb.AppendLine("            }");
    }

    public static void GenerateDictionaryCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = FormatterDiscovery.GetFirstGeneric(fieldData.TypeName);
        string valueType = FormatterDiscovery.GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + keyType + " key = item.Key;");
        sb.AppendLine("                evaluator.CalculateOffset(ref key);");
        sb.AppendLine("                " + valueType + " itemValue = item.Value;");
        sb.AppendLine("                evaluator.CalculateOffset(ref itemValue);");
        sb.AppendLine("            }");
    }

    public static void GenerateQueueCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = FormatterDiscovery.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null || value.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            evaluator += sizeof(int) * 4;");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " current = item;");
        sb.AppendLine("                evaluator.CalculateOffset(ref current);");
        sb.AppendLine("            }");
    }
}
