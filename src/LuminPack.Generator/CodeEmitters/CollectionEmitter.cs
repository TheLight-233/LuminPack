using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides collection emitter functionality.</summary>
public static class CollectionEmitter

{
    /// <summary>Appends binary writing statements for collection values.</summary>
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

    /// <summary>Appends binary reading statements for collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.ObjectModel.Collection<{elementType}>(new global::System.Collections.Generic.List<{elementType}>(length));");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var list = LuminPackMarshal.As<global::System.Collections.ObjectModel.Collection<{elementType}>, CollectionView<{elementType}>>(ref value);");
        sb.AppendLine();
        // An existing wrapper may hold any IList; only an exact List matches the storage view.
        sb.AppendLine("            if (list.items is null || list.items.GetType() != typeof(global::System.Collections.Generic.List<" + InterfaceCollectionEmitterHelper.RuntimeName(EmitterTypeTraits.GetFirstTypeArgument(fieldData), elementType) + ">))");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    " + elementType + " item = default!;");
        sb.AppendLine("                    " + CollectionEmitterKit.ContiguousElementRead(element, elementType, "item"));
        sb.AppendLine("                    value.Add(item);");
        sb.AppendLine("                }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(list.items!, length);");
        CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "span", "            ");
    }
}
