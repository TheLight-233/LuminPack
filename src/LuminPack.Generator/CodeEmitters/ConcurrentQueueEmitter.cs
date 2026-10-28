using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent queue emitter functionality.</summary>
public static class ConcurrentQueueEmitter
{
    /// <summary>Appends binary writing statements for concurrent queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Counts the snapshot enumeration itself instead of reading a separate, possibly different, <c>Count</c>.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value", new[] { "var v = item;", "writer.WriteValue(in v);" }, "            ", EmitterTypeTraits.GetFirstTypeArgument(fieldData));
    }

    /// <summary>Appends binary reading statements for concurrent queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>A fresh queue is built from the counted elements, so its first segment holds them all.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ", scalarElements: true);
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            "value = new global::System.Collections.Concurrent.ConcurrentQueue<" + elementType + ">(new global::System.ArraySegment<" + elementType + ">(__pooled, 0, length));"
        }, "                ", scalarElements: true);
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            value.Clear();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " v = default!;");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.Enqueue(v);");
        sb.AppendLine("            }");
    }
}
