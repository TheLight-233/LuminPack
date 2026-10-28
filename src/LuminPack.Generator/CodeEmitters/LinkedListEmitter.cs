using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides linked list emitter functionality.</summary>
public static class LinkedListEmitter
{
    /// <summary>Appends binary writing statements for linked list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Walks the nodes directly instead of through the version-checked enumerator.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        sb.AppendLine("            writer.EnsureAdditionalCapacity(4);");
        sb.AppendLine("            writer.Write(value.Count);");
        sb.AppendLine("            for (var node = value.First; node is not null; node = node.Next)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                writer.WriteValue(node.Value);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for linked list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (!reader.TryReadCollectionHead(out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + ")) { value = null; return; }");
        // The element block is read once, raw elements as one copy, and its nodes are linked in one pass.
        sb.AppendLine("            if (value is null) value = new global::System.Collections.Generic.LinkedList<" + elementType + ">();");
        sb.AppendLine("            else value.Clear();");
        sb.AppendLine("            if (length == 0) return;");
        CollectionEmitterKit.AppendPooledRead(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), elementType, new[]
        {
            "global::LuminPack.Code.LuminPackCollectionAccess.FillLinkedList(value, __pooled, length);"
        }, "            ", scalarElements: true);
    }
}
