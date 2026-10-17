using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

public static partial class ListEmitter

{

    /// <summary>Appends binary writing statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine();

        if (useUnmanagedFastPath)
        {
            sb.AppendLine("            writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("            writer.Advance(spanOffset);");
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }

        if (!elementClassificationKnown && !elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("                writer.Advance(spanOffset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else if (!elementClassificationKnown)
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("                writer.Advance(spanOffset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (ref var item in span)");
        sb.AppendLine("            {");
        if (EmitterTypeTraits.GetFirstTypeArgument(fieldData)?.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_String)
            sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                writer.WriteValue(in item!);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }
    // Compressed serialization variants.

    /// <summary>Appends binary writing statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine();

        if (useUnmanagedFastPath)
        {
            if (elementType.Contains("?"))
            {
                // Nullable unmanaged collections have historically kept a raw
                // payload even when the member requests compression.
                sb.AppendLine($"            writer.EnsureAdditionalCapacity(checked(sizeof(int) + span.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>()));");
                sb.AppendLine("            writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
                sb.AppendLine("            writer.Advance(spanOffset);");
                sb.AppendLine("            writer.CheckBuffer();");
                return;
            }

            sb.AppendLine("            writer.DangerousWriteUnmanagedSpanWithCompress(ref index, span, out var spanOffset);");
            sb.AppendLine("            writer.Advance(spanOffset);");
            return;
        }

        if (!elementClassificationKnown && !elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedSpanWithCompress(ref index, span, out var spanOffset);");
            sb.AppendLine("                writer.Advance(spanOffset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else if (!elementClassificationKnown)
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("                writer.Advance(spanOffset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Reference elements: propagate WithCompress only when leaf element is a KnownValueType.
        string elementCallSerialize = KnownValueTypes.Contains(elementType) || IsLeafTypeKnownValueType(elementType)
            ? "writer.WriteValueWithCompress(item!);"
            : "writer.WriteValue(in item!);";
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (ref var item in span)");
        sb.AppendLine("            {");
        if (EmitterTypeTraits.GetFirstTypeArgument(fieldData)?.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_String)
            sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine($"                {elementCallSerialize}");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }
}
