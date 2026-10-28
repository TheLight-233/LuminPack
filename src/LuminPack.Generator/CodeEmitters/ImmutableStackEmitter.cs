using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable stack emitter functionality.</summary>
public static class ImmutableStackEmitter
{
    /// <summary>Appends binary writing statements for immutable stack values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Counts the single traversal and publishes the count afterwards instead of staging the elements in a temporary buffer.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value", new[] { "var v = item;", "writer.WriteValue(in v);" }, "            ", element);
    }

    /// <summary>Appends binary reading statements for immutable stack values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Reads the top-first elements and pushes them from the last one, so the first element ends on top.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ", scalarElements: true);
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Immutable.ImmutableStack<" + elementType + ">.Empty; return; }");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[] { "var __stack = global::System.Collections.Immutable.ImmutableStack<" + elementType + ">.Empty;", "for (int __index = length - 1; __index >= 0; __index--) __stack = __stack.Push(__pooled[__index]);", "value = __stack;" }, "            ", scalarElements: true);
    }
}
