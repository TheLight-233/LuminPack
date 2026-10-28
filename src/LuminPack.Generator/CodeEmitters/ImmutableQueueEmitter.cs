using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable queue emitter functionality.</summary>
public static class ImmutableQueueEmitter
{
    /// <summary>Appends binary writing statements for immutable queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Counts the single traversal and publishes the count afterwards instead of staging the elements in a temporary buffer.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        CollectionEmitterKit.AppendImmutableQueueWrite(sb, element, "value", "            ");
    }

    /// <summary>Appends binary reading statements for immutable queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Enqueues the complete element block in order.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ", scalarElements: true);
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Immutable.ImmutableQueue<" + elementType + ">.Empty; return; }");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            "#if NET9_0_OR_GREATER",
            "value = global::System.Collections.Immutable.ImmutableQueue.Create(new global::System.ReadOnlySpan<" + elementType + ">(__pooled, 0, length));",
            "#else",
            "value = global::System.Collections.Immutable.ImmutableQueue.CreateRange(new global::System.ArraySegment<" + elementType + ">(__pooled, 0, length));",
            "#endif"
        }, "            ", scalarElements: true);
    }
}
