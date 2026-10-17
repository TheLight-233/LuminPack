using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides read only collection builder emitter functionality.</summary>
public static class ReadOnlyCollectionBuilderEmitter

{
    /// <summary>Appends binary writing statements for read only collection builder values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                var index = writer.CurrentIndex;");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var list = LuminPackMarshal.As<global::System.Collections.Immutable.ReadOnlyCollectionBuilder<{elementType}>, global::System.Collections.Generic.List<{elementType}>>(ref value);");
        sb.AppendLine();
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(ref list);");
        sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(checked(sizeof(int) + span.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">()));");
        sb.AppendLine("                writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
        sb.AppendLine("                writer.Advance(spanOffset);");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                if (span.IsEmpty)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                    writer.Advance(sizeof(int));");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("                    writer.Advance(sizeof(int));");
        sb.AppendLine("                    foreach (var item in span)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        writer.CheckBuffer();");
        sb.AppendLine("                        var temp = item;");
        sb.AppendLine("                        writer.WriteValue(in temp!);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary reading statements for read only collection builder values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Immutable.ReadOnlyCollectionBuilder<{elementType}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else if (value.Count == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var list = LuminPackMarshal.As<global::System.Collections.Immutable.ReadOnlyCollectionBuilder<{elementType}>, global::System.Collections.Generic.List<{elementType}>>(ref value);");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(ref list, length);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">())");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.DangerousReadUnmanagedSpan(ref index, ref span, length, out var spanOffset);");
        sb.AppendLine("                reader.Advance(spanOffset);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                span = global::System.Array.Empty<" + elementType + ">();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (span.Length != length)");
        sb.AppendLine("            {");
        sb.AppendLine("                span = LuminPackMarshal.AllocateUninitializedArray<" + elementType + ">(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
        sb.AppendLine("            for (nint i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends JSON writing statements for read only collection builder values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var list = LuminPackMarshal.As<global::System.Collections.Immutable.ReadOnlyCollectionBuilder<" + elementType + ">, global::System.Collections.Generic.List<" + elementType + ">(ref value);");
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(list);");
        sb.AppendLine("            bool isFirst = true;");
        sb.AppendLine("            foreach (ref var item in span)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                else isFirst = false;");
        sb.AppendLine("                writer.SetFirstElement(true);");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for read only collection builder values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.Immutable.ReadOnlyCollectionBuilder<" + elementType + ">();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                value.Add(item);");
        sb.AppendLine("            }");
    }
}
