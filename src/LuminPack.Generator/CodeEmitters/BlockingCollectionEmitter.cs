using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides blocking collection emitter functionality.</summary>
public static class BlockingCollectionEmitter
{
    /// <summary>Appends binary writing statements for blocking collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Counts the non-consuming enumeration of the underlying collection, which may differ from a separately read count.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value", new[] { "var v = item;", "writer.WriteValue(in v);" }, "            ", EmitterTypeTraits.GetFirstTypeArgument(fieldData));
    }

    /// <summary>Appends binary reading statements for blocking collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>
    /// Wraps a queue that already holds the elements: an unbounded collection over a <c>ConcurrentQueue</c>, the default
    /// backing store, whose initial count equals the elements, instead of one semaphore release per added element.
    /// </remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ", scalarElements: true);
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            "value = new global::System.Collections.Concurrent.BlockingCollection<" + elementType + ">(new global::System.Collections.Concurrent.ConcurrentQueue<" + elementType + ">(new global::System.ArraySegment<" + elementType + ">(__pooled, 0, length)));"
        }, "            ", scalarElements: true);
    }
}
