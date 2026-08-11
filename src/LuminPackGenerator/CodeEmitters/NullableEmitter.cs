using System.Text;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static class NullableEmitter
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

        if (EmitterTypeTraits.TryGetIsUnmanaged(GetElementTypeSymbol(data), out bool isUnmanaged))
        {
            if (isUnmanaged)
            {
                AppendUnmanagedSerialize(sb, elementType);
            }
            else
            {
                AppendManagedSerialize(sb, elementType);
            }
            return;
        }

        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine($"            {{");
        AppendUnmanagedSerialize(sb, elementType, "    ");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        AppendManagedSerialize(sb, elementType);
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

        if (EmitterTypeTraits.TryGetIsUnmanaged(GetElementTypeSymbol(data), out bool isUnmanaged))
        {
            if (isUnmanaged)
            {
                AppendUnmanagedDeserialize(sb, elementType);
            }
            else
            {
                AppendManagedDeserialize(sb, elementType);
            }
            return;
        }

        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine($"            {{");
        AppendUnmanagedDeserialize(sb, elementType, "    ");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        AppendManagedDeserialize(sb, elementType);
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

		if (EmitterTypeTraits.TryGetIsUnmanaged(GetElementTypeSymbol(data), out bool isUnmanaged))
		{
			if (isUnmanaged)
			{
				AppendUnmanagedCalculateOffset(sb, elementType);
			}
			else
			{
				AppendManagedCalculateOffset(sb, elementType);
			}
			return;
		}

		sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine($"            {{");
		AppendUnmanagedCalculateOffset(sb, elementType, "    ");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
		AppendManagedCalculateOffset(sb, elementType);
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

        return CodeEmitterRegistry.GetFirstGeneric(data.TypeName);
    }

    private static ITypeSymbol GetElementTypeSymbol(LuminLocalFieldData data)
    {
        return data.TypeSymbol is INamedTypeSymbol
        {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
            TypeArguments.Length: 1
        } named
            ? named.TypeArguments[0]
            : null;
    }

    private static void AppendUnmanagedSerialize(StringBuilder sb, string elementType, string indentSuffix = "")
    {
        string indent = "            " + indentSuffix;
        sb.AppendLine($"{indent}{elementType} val = value.Value;");
        sb.AppendLine($"{indent}writer.DangerousWriteUnmanaged(ref index, val);");
        sb.AppendLine($"{indent}writer.Advance(Unsafe.SizeOf<{elementType}>());");
    }

    private static void AppendManagedSerialize(StringBuilder sb, string elementType)
    {
        sb.AppendLine($"            {elementType} valRef = value.Value;");
        sb.AppendLine("            writer.WriteValue(in valRef);");
    }

    private static void AppendUnmanagedDeserialize(StringBuilder sb, string elementType, string indentSuffix = "")
    {
        string indent = "            " + indentSuffix;
        sb.AppendLine($"{indent}{elementType} val;");
        sb.AppendLine($"{indent}reader.DangerousReadUnmanaged(ref index, out val);");
        sb.AppendLine($"{indent}value = val;");
        sb.AppendLine($"{indent}reader.Advance(Unsafe.SizeOf<{elementType}>());");
    }

    private static void AppendManagedDeserialize(StringBuilder sb, string elementType)
    {
        sb.AppendLine($"            {elementType} valRef = default!;");
        sb.AppendLine("            reader.ReadValue(ref valRef);");
        sb.AppendLine("            value = valRef;");
    }

    private static void AppendUnmanagedCalculateOffset(StringBuilder sb, string elementType, string indentSuffix = "")
    {
        sb.AppendLine($"            {indentSuffix}evaluator += Unsafe.SizeOf<{elementType}>();");
    }

    private static void AppendManagedCalculateOffset(StringBuilder sb, string elementType)
    {
		sb.AppendLine($"            {elementType} val = value.Value;");
		sb.AppendLine("            evaluator.CalculateOffset(ref val);");
    }
}
