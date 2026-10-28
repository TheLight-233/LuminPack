using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable sorted set emitter functionality.</summary>
public static class ImmutableSortedSetEmitter
{
    /// <summary>Appends binary writing statements for immutable sorted set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        // The in-order node walk yields the enumeration order, so the elements are written as one contiguous span.
        // Raw elements are walked straight into the reserved output record.
        if (CollectionEmitterKit.IsRawScalar(element, elementType))
            sb.AppendLine("            global::LuminPack.Utility.LuminTreeStorage.WriteRaw(ref writer, value);");
        else
            CollectionEmitterKit.AppendPooledCopyWrite(sb, element, elementType, "value.Count", "global::LuminPack.Utility.LuminTreeStorage.CopyTo(value, {0})", "            ", scalarElements: true);
    }

    /// <summary>Appends binary reading statements for immutable sorted set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Builds the set from the complete element block: strictly ascending elements link the tree in one pass, other input uses the sorted construction.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (ImmutableStringSetEmitter.TryGenerateDeserializeCode(fieldData, sb, sorted: true)) return;
        if (ImmutableScalarSetEmitter.TryGenerateDeserializeCode(fieldData, sb, sorted: true)) return;
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ", scalarElements: true);
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Immutable.ImmutableSortedSet<" + elementType + ">.Empty; return; }");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            "value = global::LuminPack.Utility.LuminTreeStorage.CreateImmutableSortedSet(__pooled, length);"
        }, "            ", scalarElements: true);
    }
}
