using System;
using System.Text;
using Microsoft.CodeAnalysis;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent bag emitter functionality.</summary>
public static class ConcurrentBagEmitter
{
    /// <summary>Formats one complete leased snapshot after the bag's freeze locks have been released.</summary>
    /// <param name="fieldData">The analyzed bag and formatter contract.</param>
    /// <param name="sb">The generated statements.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.WriteNull(); return; }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentConcurrentBagSnapshot(value, out int count, out bool pooled);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                bool isFirst = true;");
        sb.AppendLine("                var elements = new global::System.ReadOnlySpan<" + elementType + ">(snapshot, 0, count);");
        sb.AppendLine("                foreach (var item in elements)");
        sb.AppendLine("                {");
        JsonCollectionElementEmitter.Append(sb, "item", "                    ");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            finally { if (pooled) global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(snapshot, " + CollectionEmitterKit.ClearExpression(EmitterTypeTraits.GetFirstTypeArgument(fieldData), elementType) + "); }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends binary writing statements for concurrent bag values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Freezes the bag once for direct raw output or a complete snapshot; unsupported layouts retain <c>ToArray</c>.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        if (fieldData.CanWriteConcurrentBagRaw && !IsNullableElement(element, elementType) && CollectionEmitterKit.IsRaw(element, elementType))
            sb.AppendLine("            if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteConcurrentBagRaw(ref writer, value)) return;");
        if (fieldData.CanSnapshotConcurrentBag)
        {
            sb.AppendLine("            var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentConcurrentBagSnapshot(value, out int count, out bool pooled);");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            CollectionEmitterKit.AppendSpanWrite(sb, element, elementType, "new global::System.ReadOnlySpan<" + elementType + ">(snapshot, 0, count)", "                ");
            sb.AppendLine("            }");
            sb.AppendLine("            finally { if (pooled) global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(snapshot, " + CollectionEmitterKit.ClearExpression(element, elementType) + "); }");
            return;
        }
        CollectionEmitterKit.AppendSpanWrite(sb, element, elementType, "value.ToArray()", "            ");
    }

    /// <summary>Determines whether the element is a nullable value type that cannot use the raw bag path.</summary>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <returns>Whether the element is nullable.</returns>
    private static bool IsNullableElement(ITypeSymbol element, string elementType)
        => element is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
            || elementType.EndsWith("?", StringComparison.Ordinal);

    /// <summary>Appends binary reading statements for concurrent bag values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        // The writer copies a contiguous snapshot, so the elements use the contiguous element contract.
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine("            if (value is null) value = new global::System.Collections.Concurrent.ConcurrentBag<" + elementType + ">();");
        sb.AppendLine("            else value.Clear();");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            "for (int i = 0; i < length; i++) value.Add(__pooled[i]);"
        }, "            ");
    }
}
