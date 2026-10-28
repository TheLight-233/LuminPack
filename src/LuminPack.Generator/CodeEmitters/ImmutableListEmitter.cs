using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable list emitter functionality.</summary>
public static class ImmutableListEmitter
{
    /// <summary>Appends binary writing statements for immutable list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        // The reader selects raw storage for exactly these elements, so a Nullable, KeyValuePair or specialized-mode value
        // is written, like the counted loop below, by its own formatter.
        if (CollectionEmitterKit.IsRawScalar(element, elementType))
        {
            // Raw elements: the tree's own in-order copy, then the count and its payload as one contiguous record,
            // which are exactly the bytes of the counted per-element loop.
            sb.AppendLine("            int count = value.Count;");
            sb.AppendLine("            var items = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(count);");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                value.CopyTo(items);");
            sb.AppendLine("                writer.Write<" + elementType + ">(new global::System.ReadOnlySpan<" + elementType + ">(items, 0, count), count);");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(items, false);");
            sb.AppendLine("            }");
            return;
        }
        if (element?.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String ||
            EmitterTypeTraits.UsesOrdinaryObjectHeader(element) && !EmitterTypeTraits.ContainsUndeterminedTypeParameter(element))
        {
            // Immutable membership permits one public copy before the unchanged element operations.
            // Reference rentals use the existing shared pool and are fully cleared on every exit.
            sb.AppendLine("            int count = value.Count;");
            sb.AppendLine("            writer.EnsureAdditionalCapacity(4);");
            sb.AppendLine("            writer.Write(count);");
            sb.AppendLine("            if (count == 0) { writer.CheckBuffer(); return; }");
            sb.AppendLine("            var items = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(count);");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                value.CopyTo(items);");
            sb.AppendLine("                for (int i = 0; i < count; i++)");
            sb.AppendLine("                {");
            if (element?.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_String)
                sb.AppendLine("                    writer.CheckBuffer();");
            sb.AppendLine("                    writer.WriteValue(in items[i]!);");
            sb.AppendLine("                }");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("            }");
            sb.AppendLine("            finally { global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(items, true); }");
            return;
        }
        sb.AppendLine("            writer.EnsureAdditionalCapacity(4);");
        sb.AppendLine("            writer.Write(value.Count);");
        sb.AppendLine("            foreach (var item in value) { writer.CheckBuffer(); var v = item; writer.WriteValue(in v); }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for immutable list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Builds the balanced tree from the complete element block in one pass instead of one builder insertion per element.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ", scalarElements: true);
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Immutable.ImmutableList<" + elementType + ">.Empty; return; }");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            "#if NET9_0_OR_GREATER",
            "value = global::System.Collections.Immutable.ImmutableList.Create(new global::System.ReadOnlySpan<" + elementType + ">(__pooled, 0, length));",
            "#else",
            "value = global::System.Collections.Immutable.ImmutableList.CreateRange(new global::System.ArraySegment<" + elementType + ">(__pooled, 0, length));",
            "#endif"
        }, "            ", scalarElements: true);
    }
}
