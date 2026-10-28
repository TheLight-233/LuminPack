using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable hash set emitter functionality.</summary>
public static class ImmutableHashSetEmitter
{
    /// <summary>Appends binary writing statements for immutable hash set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>The hash-tree walk yields the enumeration order, so the elements are written as one contiguous span.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        // Raw elements are walked straight into the reserved output record.
        if (CollectionEmitterKit.IsRawScalar(element, elementType))
            sb.AppendLine("            global::LuminPack.Utility.LuminTreeStorage.WriteRaw(ref writer, value);");
        else
            CollectionEmitterKit.AppendPooledCopyWrite(sb, element, elementType, "value.Count", "global::LuminPack.Utility.LuminTreeStorage.CopyTo(value, {0})", "            ", scalarElements: true);
    }

    /// <summary>Appends binary reading statements for immutable hash set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (ImmutableStringSetEmitter.TryGenerateDeserializeCode(fieldData, sb, sorted: false)) return;
        if (ImmutableScalarSetEmitter.TryGenerateDeserializeCode(fieldData, sb, sorted: false)) return;
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet.Create<{elementType}>(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        // Distinct hash codes link the hash tree in one pass; anything else goes through the builder.
        sb.AppendLine("            var pooled = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    " + elementType + " item = default!;");
        sb.AppendLine("                    reader.ReadValue(ref item);");
        sb.AppendLine("                    pooled[i] = item;");
        sb.AppendLine("                }");
        sb.AppendLine("                value = global::LuminPack.Utility.LuminTreeStorage.CreateImmutableHashSet(pooled, length);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally { global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(pooled, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">()); }");
    }
}
