using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Emits serialization code for the span-backed parser inventory.</summary>
public static class MemoryEmitter
{
    /// <summary>Appends binary writing statements for memory values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), list: false, write: true, GetBaseType(fieldData.TypeName))) return;
        string baseType = GetBaseType(fieldData.TypeName);
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        if (baseType != "global::System.Buffers.ReadOnlySequence")
        {
            // Contiguous storage: one raw block for raw elements, otherwise an indexed loop without enumerator state.
            if (baseType == "global::System.ArraySegment")
                sb.AppendLine("            if (value.Array is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
            CollectionEmitterKit.AppendSpanWrite(sb, element, elementType, baseType == "global::System.ArraySegment" ? "value.AsSpan()" : "value.Span", "            ");
            return;
        }
        if (EmitterTypeTraits.IsKnownScalar(element))
        {
            AppendRawSequenceWrite(sb, elementType);
            return;
        }
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, checked((int)value.Length));");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var memory in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var item in memory.Span)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    writer.WriteValue(in temp);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for memory values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), list: false, write: false, GetBaseType(fieldData.TypeName))) return;
        string baseType = GetBaseType(fieldData.TypeName);
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        bool scalarElements = baseType == "global::System.Buffers.ReadOnlySequence";
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = default;", "            ", scalarElements);
        if ((baseType == "global::System.Memory" || baseType == "global::System.ArraySegment") &&
            (element?.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String || EmitterTypeTraits.UsesOrdinaryObjectHeader(element) ||
             EmitterTypeTraits.IsKnownScalar(element)))
        {
            AppendReferenceStorageRead(sb, element, elementType, baseType);
            return;
        }
        sb.AppendLine("            var array = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + elementType + ">(length);");
        CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "array.AsSpan()", "            ", scalarElements: scalarElements);
        AppendArrayAssignment(sb, baseType, elementType);
    }

    /// <summary>Reads through exact mutable array storage while preserving its slice and selected element contract.</summary>
    /// <param name="sb">The generated reading statements.</param>
    /// <param name="element">The closed string, ordinary object or known raw scalar contract.</param>
    /// <param name="elementType">The generated element spelling.</param>
    /// <param name="baseType">The mutable memory or array-segment family.</param>
    private static void AppendReferenceStorageRead(StringBuilder sb, Microsoft.CodeAnalysis.ITypeSymbol element, string elementType, string baseType)
    {
        sb.AppendLine("            " + elementType + "[] array;");
        sb.AppendLine("            int __start = 0;");
        sb.AppendLine("            bool __reused = true;");
        if (baseType == "global::System.Memory")
        {
            sb.AppendLine("            if (global::LuminPack.Code.LuminPackMarshal.TryGetExactArray(value, out var __storageArray, out var __storageStart, out var __storageLength) && __storageLength == length)");
            sb.AppendLine("            {");
            sb.AppendLine("                array = __storageArray!;");
            sb.AppendLine("                __start = __storageStart;");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            var __storage = value;");
            sb.AppendLine("            if (__storage.Count == length && __storage.Array is not null && __storage.Array.GetType() == typeof(" + elementType + "[]))");
            sb.AppendLine("            {");
            sb.AppendLine("                array = __storage.Array;");
            sb.AppendLine("                __start = __storage.Offset;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            else { array = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + elementType + ">(length); __reused = false; }");
        if (EmitterTypeTraits.IsKnownScalar(element))
        {
            CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "array.AsSpan(__start, length)", "            ");
        }
        else
        {
            sb.AppendLine("            ref var __first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(array.AsSpan(__start, length));");
            sb.AppendLine("            for (int __i = 0; __i < length; __i++)");
            sb.AppendLine("            {");
            EmitterTypeTraits.AppendReusableElementRead(sb, element,
                "global::System.Runtime.CompilerServices.Unsafe.Add(ref __first, (nint)(uint)__i)", "                ");
            sb.AppendLine("            }");
        }
        // Reused storage already describes exactly this array slice, so only a new array is published.
        sb.AppendLine("            if (!__reused) value = new " + baseType + "<" + elementType + ">(array, __start, length);");
    }

    /// <summary>Appends JSON writing statements for memory values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseType(fieldData.TypeName);
        if (baseType == "global::System.ArraySegment")
        {
            sb.AppendLine("            if (value.Array is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNull();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            bool isFirst = true;");
        if (baseType == "global::System.Buffers.ReadOnlySequence")
        {
            sb.AppendLine("            foreach (var memory in value)");
            sb.AppendLine("            {");
            sb.AppendLine("                foreach (var item in memory.Span)");
            sb.AppendLine("                {");
            AppendJsonItemWrite(sb, "                    ");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        else if (baseType == "global::System.Memory")
        {
            sb.AppendLine("            foreach (ref var item in value.Span)");
            sb.AppendLine("            {");
            AppendJsonItemWrite(sb, "                ");
            sb.AppendLine("            }");
        }
        else if (baseType == "global::System.ReadOnlyMemory")
        {
            sb.AppendLine("            foreach (var item in value.Span)");
            sb.AppendLine("            {");
            AppendJsonItemWrite(sb, "                ");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            foreach (var item in value)");
            sb.AppendLine("            {");
            AppendJsonItemWrite(sb, "                ");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for memory values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseType(fieldData.TypeName);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        AppendNullAssignment(sb, baseType);
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            var items = new global::System.Collections.Generic.List<" + elementType + ">();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                items.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine("            var array = items.ToArray();");
        AppendArrayAssignment(sb, baseType, elementType);
    }

    /// <summary>Appends JSON writing statements for memory values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void AppendJsonItemWrite(StringBuilder sb, string indent)
    {
        sb.AppendLine(indent + "if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine(indent + "else isFirst = false;");
        sb.AppendLine(indent + "writer.SetFirstElement(true);");
        sb.AppendLine(indent + "var temp = item;");
        sb.AppendLine(indent + "writer.WriteValue(in temp);");
    }

    /// <summary>Appends binary size evaluation statements for memory values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="baseType">The base type used by this operation.</param>
    private static void AppendNullAssignment(StringBuilder sb, string baseType)
    {
        sb.AppendLine(baseType == "global::System.ArraySegment" ? "                value = default;" : "                value = default;");
    }

    /// <summary>Appends binary size evaluation statements for memory values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="baseType">The base type used by this operation.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    private static void AppendArrayAssignment(StringBuilder sb, string baseType, string elementType)
    {
        if (baseType == "global::System.ArraySegment")
            sb.AppendLine("            value = new global::System.ArraySegment<" + elementType + ">(array);");
        else if (baseType == "global::System.Buffers.ReadOnlySequence")
            sb.AppendLine("            value = new global::System.Buffers.ReadOnlySequence<" + elementType + ">(array);");
        else
            sb.AppendLine("            value = array;");
    }

    /// <summary>Evaluates the exact contiguous or segmented binary element contract.</summary>
    /// <param name="fieldData">The memory type metadata.</param>
    /// <param name="sb">The emitted statements.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseType(fieldData.TypeName);
        sb.AppendLine("            evaluator += 4;");
        if (baseType == "global::System.ArraySegment") sb.AppendLine("            if (value.Array is null) return;");
        if (baseType == "global::System.Buffers.ReadOnlySequence")
        {
            sb.AppendLine("            _ = checked((int)value.Length);");
            sb.AppendLine("            foreach (var memory in value)");
            sb.AppendLine("            foreach (var element in memory.Span)");
        }
        else
        {
            string span = baseType == "global::System.ArraySegment" ? "value.AsSpan()" : "value.Span";
            sb.AppendLine("            var payload = " + span + ";");
            if (CollectionEvaluatorEmitter.AppendContiguousPayloadSize(fieldData, sb, "payload.Length", allowTag: true)) return;
            sb.AppendLine("            foreach (var element in payload)");
        }
        sb.AppendLine("            {");
        sb.AppendLine("                var item = element;");
        sb.AppendLine("                evaluator.CalculateOffset(ref item);");
        sb.AppendLine("            }");
    }

    /// <summary>Obtains the base type for the supplied input.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetBaseType(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }

    /// <summary>Emits checked raw segments with original element traversal for aliased storage.</summary>
    /// <param name="sb">The generated complete sequence operation.</param>
    /// <param name="elementType">The canonically proven closed framework raw scalar.</param>
    private static void AppendRawSequenceWrite(StringBuilder sb, string elementType)
    {
        sb.AppendLine("            int count = checked((int)value.Length);");
        sb.AppendLine("            writer.EnsureAdditionalCapacity(4);");
        sb.AppendLine("            writer.Write(count);");
        sb.AppendLine("            if (value.IsSingleSegment) WriteRawSequenceSegment(ref writer, value.FirstSpan);");
        sb.AppendLine("            else WriteRawSequenceSegments(ref writer, value);");
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            static void WriteRawSequenceSegment(ref global::LuminPack.Core.LuminPackWriter writer, global::System.ReadOnlySpan<" + elementType + "> span)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (span.IsEmpty) return;");
        sb.AppendLine("                if (global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(span).Overlaps(writer.Buffer))");
        sb.AppendLine("                {");
        sb.AppendLine("                    WriteOverlappingSequenceSegment(ref writer, span);");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("                int bytes = checked(span.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(bytes);");
        sb.AppendLine("                writer.Write(ref global::System.Runtime.CompilerServices.Unsafe.As<" + elementType + ", byte>(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span)), bytes);");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static void WriteOverlappingSequenceSegment(ref global::LuminPack.Core.LuminPackWriter writer, global::System.ReadOnlySpan<" + elementType + "> span)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var item in span)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                    var element = item;");
        sb.AppendLine("                    writer.Write(in element);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            }");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static void WriteRawSequenceSegments(ref global::LuminPack.Core.LuminPackWriter writer, global::System.Buffers.ReadOnlySequence<" + elementType + "> value)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var memory in value) WriteRawSequenceSegment(ref writer, memory.Span);");
        sb.AppendLine("            }");
    }


}
