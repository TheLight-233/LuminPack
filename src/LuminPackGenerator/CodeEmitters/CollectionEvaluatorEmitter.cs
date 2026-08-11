using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>
/// Shared size evaluators for closed collection types. These preserve the old parser
/// behavior while dispatching each element through the source-generated formatter cache.
/// </summary>
public static class CollectionEvaluatorEmitter
{
    public static void GenerateArrayCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = fieldData.TypeName.Substring(0, fieldData.TypeName.LastIndexOf("[]", System.StringComparison.Ordinal));
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");

        ITypeSymbol elementSymbol = fieldData.TypeSymbol is IArrayTypeSymbol array
            ? array.ElementType
            : null;
        if (EmitterTypeTraits.TryGetIsUnmanaged(elementSymbol, out bool isUnmanaged))
        {
            if (isUnmanaged)
            {
                AppendUnmanagedArrayOffset(sb, elementType);
            }
            else
            {
                AppendManagedArrayOffset(sb);
            }
            return;
        }

        sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">())");
        sb.AppendLine("            {");
        AppendUnmanagedArrayOffset(sb, elementType, "    ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        AppendManagedArrayOffset(sb);
    }

    private static void AppendUnmanagedArrayOffset(StringBuilder sb, string elementType, string indentSuffix = "")
    {
        sb.AppendLine("            " + indentSuffix + "evaluator += checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
    }

    private static void AppendManagedArrayOffset(StringBuilder sb)
    {
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            foreach (ref var item in value.AsSpan())");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator.CalculateOffset(ref item);");
        sb.AppendLine("            }");
    }

    public static void GenerateEnumerableCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
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
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " current = item;");
        sb.AppendLine("                evaluator.CalculateOffset(ref current);");
        sb.AppendLine("            }");
    }

    public static void GenerateDictionaryCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        string valueType = CodeEmitterRegistry.GetSecondGeneric(fieldData.TypeName);
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
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
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
