using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides read only collection emitter functionality.</summary>
public static class ReadOnlyCollectionEmitter

{
    /// <summary>Appends binary writing statements for read only collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var depth = 0;");
        sb.AppendLine();
        sb.AppendLine("            var list = global::LuminPack.Code.LuminPackCollectionAccess.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        CollectionEmitterHelper.AppendStaticListSerialize(sb, fieldData);
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    " + CollectionEmitterKit.ContiguousElementWrite(EmitterTypeTraits.GetFirstTypeArgument(fieldData), elementType, "temp"));
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary reading statements for read only collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine($"            var array = LuminPackMarshal.AllocateUninitializedArray<{elementType}>(length);");
        CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "array.AsSpan()", "            ");
        sb.AppendLine($"            value = new global::System.Collections.ObjectModel.ReadOnlyCollection<{elementType}>(array);");
    }
}
