using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides sorted set emitter functionality.</summary>
public static class SortedSetEmitter
{
    /// <summary>Appends binary writing statements for sorted set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Copies the tree once by an in-order walk of its nodes, avoiding the enumerator stack and the per-node delegate of <c>CopyTo</c>.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        // Raw elements are walked straight into the reserved output record.
        if (CollectionEmitterKit.IsRawScalar(element, elementType))
            sb.AppendLine("            global::LuminPack.Utility.LuminTreeStorage.WriteRaw(ref writer, value);");
        else
            CollectionEmitterKit.AppendPooledCopyWrite(sb, element, elementType, "value.Count", "global::LuminPack.Utility.LuminTreeStorage.CopyTo(value, {0})", "            ");
    }

    /// <summary>Appends binary reading statements for sorted set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Accepted framework elements use the existing tree builder for strictly ordered input. Eligible reuse stages through the common element pool and restores failed prefixes; other destinations retain Clear/Add.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            fieldData.CanBuildSortedSet
                ? "value = global::LuminPack.Utility.LuminTreeStorage.CreateSortedSet(__pooled, length);"
                : "value = new global::System.Collections.Generic.SortedSet<" + elementType + ">(new global::System.ArraySegment<" + elementType + ">(__pooled, 0, length));"
        }, "                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        if (fieldData.CanRebuildSortedSet)
        {
            sb.AppendLine("            if (global::LuminPack.Utility.LuminTreeStorage.CanRebuildSortedSet(value))");
            sb.AppendLine("            {");
            sb.AppendLine("                value.Clear();");
            CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
            {
                "if (!global::LuminPack.Utility.LuminTreeStorage.TryRebuildSortedSet(value, __pooled, length))",
                "    for (int index = 0; index < length; index++) value.Add(__pooled[index]);"
            }, "                ", onReadFailure: new[]
            {
                "for (int index = 0; index < __completed; index++) value.Add(__pooled[index]);"
            });
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            value.Clear();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " v = default!;");
        sb.AppendLine("                " + CollectionEmitterKit.ContiguousElementRead(element, elementType, "v"));
        sb.AppendLine("                value.Add(v);");
        sb.AppendLine("            }");
    }
}
