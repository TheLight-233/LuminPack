using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Emits the BCL builder using its array-and-count storage view and complete element contracts.</summary>
public static class ReadOnlyCollectionBuilderEmitter
{
    /// <summary>Writes the builder's logical backing-array range.</summary>
    /// <param name="fieldData">The closed builder metadata.</param>
    /// <param name="sb">The generated operation body.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        sb.AppendLine("            var view = LuminPackMarshal.As<" + fieldData.TypeName + ", ReadOnlyCollectionBuilderView<" + elementType + ">>(ref value);");
        CollectionEmitterKit.AppendSpanWrite(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), elementType,
            "view.items.AsSpan(0, value.Count)", "            ");
    }

    /// <summary>Reads into a fresh or reused builder while retaining its actual count and capacity.</summary>
    /// <param name="fieldData">The closed builder metadata.</param>
    /// <param name="sb">The generated operation body.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine("            if (value is null) value = new " + fieldData.TypeName + "(length);");
        sb.AppendLine("            else { value.Clear(); if (value.Capacity < length) value.Capacity = length; }");
        sb.AppendLine("            var view = LuminPackMarshal.As<" + fieldData.TypeName + ", ReadOnlyCollectionBuilderView<" + elementType + ">>(ref value);");
        CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "view.items.AsSpan(0, length)", "            ");
        sb.AppendLine("            view.size = length;");
    }

    /// <summary>Writes the existing JSON array contract without reinterpreting the builder as a List.</summary>
    /// <param name="fieldData">The closed builder metadata.</param>
    /// <param name="sb">The generated operation body.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.WriteNull(); return; }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            var view = LuminPackMarshal.As<" + fieldData.TypeName + ", ReadOnlyCollectionBuilderView<" + elementType + ">>(ref value);");
        sb.AppendLine("            int count = value.Count;");
        sb.AppendLine("            for (int i = 0; i < count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (i != 0) writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                writer.SetFirstElement(true);");
        sb.AppendLine("                writer.WriteValue(in view.items[i]);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Reads the JSON array through the builder's existing mutable Add contract.</summary>
    /// <param name="fieldData">The closed builder metadata.</param>
    /// <param name="sb">The generated operation body.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
        => ListEmitter.GenerateJsonEnumerableDeserializeCode(fieldData, sb);
}
