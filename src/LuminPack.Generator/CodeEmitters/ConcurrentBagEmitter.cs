using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent bag emitter functionality.</summary>
public static class ConcurrentBagEmitter
{
    /// <summary>Appends binary writing statements for concurrent bag values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Freezes the bag once through <c>ToArray</c>; <c>Count</c> and the enumerator would each freeze it again.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        CollectionEmitterKit.AppendSpanWrite(sb, element, elementType, "value.ToArray()", "            ");
    }

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
