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
        CollectionEmitterHelper.AppendStaticListSerialize(sb);
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    writer.WriteValue(in temp);");
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

        sb.AppendLine($"            {elementType}[]? array = null;");
        sb.AppendLine("            ref var readIndex = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.DangerousReadUnmanagedArray(ref readIndex, ref array!, out var readOffset);");
        sb.AppendLine("                reader.Advance(readOffset);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!reader.TryReadCollectionHead(ref readIndex, out var length))");
        sb.AppendLine("                {");
        sb.AppendLine("                    array = null;");
        sb.AppendLine("                    reader.Advance(4);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.Advance(4);");
        sb.AppendLine("                    if (length is 0)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        array = global::System.Array.Empty<{elementType}>();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        array = LuminPackMarshal.AllocateUninitializedArray<{elementType}>(length);");
        sb.AppendLine("                        ref var first = ref LuminPackMarshal.GetNotNullArrayReference(array!);");
        sb.AppendLine("                        for (nint i = 0; i < length; i++)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (array is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.ObjectModel.ReadOnlyCollection<{elementType}>(array);");
        sb.AppendLine("            }");
    }
}
