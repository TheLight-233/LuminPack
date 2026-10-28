using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides read only observable collection emitter functionality.</summary>
public static class ReadOnlyObservableCollectionEmitter

{
    /// <summary>Appends binary writing statements for read only observable collection values.</summary>
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

    /// <summary>Appends binary reading statements for read only observable collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        // The elements are read into the exactly sized list the wrapped collection then holds, instead of an array its constructor copies.
        sb.AppendLine($"            var observable = new global::System.Collections.ObjectModel.ObservableCollection<{elementType}>();");
        sb.AppendLine($"            var list = new global::System.Collections.Generic.List<{elementType}>(length);");
        sb.AppendLine("            var listSpan = LuminPackMarshal.GetListSpan(list, length);");
        CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "listSpan", "            ");
        sb.AppendLine($"            LuminPackMarshal.As<global::System.Collections.ObjectModel.ObservableCollection<{elementType}>, ObservableCollectionView<{elementType}>>(ref observable).items = list;");
        sb.AppendLine($"            value = new global::System.Collections.ObjectModel.ReadOnlyObservableCollection<{elementType}>(observable);");
    }
}
