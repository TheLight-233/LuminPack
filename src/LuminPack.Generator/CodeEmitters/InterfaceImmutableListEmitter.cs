using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides interface immutable list emitter functionality.</summary>
public static class InterfaceImmutableListEmitter
{
    /// <summary>Appends binary writing statements for interface immutable list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        sb.AppendLine("            if (value.GetType() == typeof(global::System.Collections.Immutable.ImmutableList<" + InterfaceCollectionEmitterHelper.RuntimeName(element, elementType) + ">))");
        sb.AppendLine("            {");
        sb.AppendLine("                var list = global::System.Runtime.CompilerServices.Unsafe.As<global::System.Collections.Immutable.ImmutableList<" + elementType + ">>(value);");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(4);");
        sb.AppendLine("                writer.Write(list.Count);");
        sb.AppendLine("                foreach (var item in list) { writer.CheckBuffer(); var v = item; writer.WriteValue(in v); }");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value", new[] { "var v = item;", "writer.WriteValue(in v);" }, "            ", element);
    }

    /// <summary>Appends binary reading statements for interface immutable list values.</summary>
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
            "value = global::System.Collections.Immutable.ImmutableList.CreateRange(new global::System.ArraySegment<" + elementType + ">(__pooled, 0, length));"
        }, "            ", scalarElements: true);
    }
}
