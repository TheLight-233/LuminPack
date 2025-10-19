using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class NullableFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine($"            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine($"            if (!value.HasValue)");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine($"                writer.Advance(1);");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{data.TypeName}>())");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                {data.TypeName} val = value.Value;");
        sb.AppendLine($"                writer.DangerousWriteUnmanaged(ref index, val);");
        sb.AppendLine($"                writer.Advance(Unsafe.SizeOf<{data.TypeName}>());");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            {data.TypeName} valRef = value.Value;");
        sb.AppendLine($"            writer.WriteValue(valRef);");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine($"            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine($"            if (reader.PeekIsNullObject(ref index))");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                value = null;");
        sb.AppendLine($"                reader.Advance(1);");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{data.TypeName}>())");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                {data.TypeName} val;");
        sb.AppendLine($"                reader.DangerousReadUnmanaged(ref index, out val);");
        sb.AppendLine($"                value = val;");
        sb.AppendLine($"                reader.Advance(Unsafe.SizeOf<{data.TypeName}>());");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            {data.TypeName} valRef = reader.ReadValue<{data.TypeName}>();");
        sb.AppendLine($"            value = valRef;");
    }
    
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine($"            if (!value.HasValue)");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                evaluator += 1;");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{data.TypeName}>())");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                evaluator += Unsafe.SizeOf<{data.TypeName}>();");
        sb.AppendLine($"                return;");
        sb.AppendLine($"            }}");
        sb.AppendLine($"");
        sb.AppendLine($"            {data.TypeName} val = value.Value;");
        sb.AppendLine($"            var eva = evaluator.GetEvaluator<{data.TypeName}>();");
        sb.AppendLine($"            eva.CalculateOffset(ref evaluator, ref val);");
    }
}