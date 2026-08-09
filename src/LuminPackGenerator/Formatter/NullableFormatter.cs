using System.Text;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Formatter;

public static class NullableFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        string elementType = GetElementType(data);
        sb.AppendLine($"            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine($"            if (!value.HasValue)");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine($"                writer.Advance(1);");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                {elementType} val = value.Value;");
        sb.AppendLine($"                writer.DangerousWriteUnmanaged(ref index, val);");
        sb.AppendLine($"                writer.Advance(Unsafe.SizeOf<{elementType}>());");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            {elementType} valRef = value.Value;");
        sb.AppendLine($"            writer.WriteValue(in valRef);");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        string elementType = GetElementType(data);
        sb.AppendLine($"            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine($"            if (reader.PeekIsNullObject(ref index))");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                value = null;");
        sb.AppendLine($"                reader.Advance(1);");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                {elementType} val;");
        sb.AppendLine($"                reader.DangerousReadUnmanaged(ref index, out val);");
        sb.AppendLine($"                value = val;");
        sb.AppendLine($"                reader.Advance(Unsafe.SizeOf<{elementType}>());");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            {elementType} valRef = default!;");
        sb.AppendLine($"            reader.ReadValue(ref valRef);");
        sb.AppendLine($"            value = valRef;");
    }

    public static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
    {
		string elementType = GetElementType(data);
        sb.AppendLine($"            if (!value.HasValue)");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                evaluator += 1;");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
		sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine($"            {{");
		sb.AppendLine($"                evaluator += Unsafe.SizeOf<{elementType}>();");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
		sb.AppendLine($"            {elementType} val = value.Value;");
		sb.AppendLine("            evaluator.CalculateOffset(ref val);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        var elementType = GetElementType(data);
        sb.AppendLine("            if (!value.HasValue)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var item = value.Value;");
        sb.AppendLine("            writer.WriteValue(in item);");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        var elementType = GetElementType(data);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            " + elementType + " item = default;");
        sb.AppendLine("            reader.ReadValue(ref item);");
        sb.AppendLine("            value = item;");
    }

    private static string GetElementType(LuminLocalFieldData data)
    {
        if (data.TypeSymbol is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            return FormatterTypeName.Get(named.TypeArguments[0]);
        }

        return FormatterDiscovery.GetFirstGeneric(data.TypeName);
    }
}
