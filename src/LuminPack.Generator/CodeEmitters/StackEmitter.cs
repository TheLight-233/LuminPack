using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides stack emitter functionality.</summary>
public static class StackEmitter

{
    /// <summary>Appends binary writing statements for stack values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        bool requiresRuntimeClassification = !elementClassificationKnown && !useUnmanagedFastPath;
        string clearArrayExpression = elementClassificationKnown
            ? (elementIsUnmanaged ? "false" : "true")
            : useUnmanagedFastPath
                ? "false"
                : $"global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>()";

        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                index += 4;");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            var span = LuminPackMarshal.GetStackSpan(Unsafe.AsRef(in value));");
        if (useUnmanagedFastPath)
        {
            sb.AppendLine("            writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("            writer.Advance(spanOffset);");
            sb.AppendLine("            writer.CheckBuffer();");
        }
        else if (elementClassificationKnown)
        {
            sb.AppendLine("            writer.WriteCollectionHeader(ref index, span.Length);");
            sb.AppendLine("            writer.Advance(sizeof(int));");
            sb.AppendLine("            foreach (ref var item in span)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                writer.WriteValue(in item!);");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.CheckBuffer();");
        }
        else
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("                writer.Advance(spanOffset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.WriteCollectionHeader(ref index, span.Length);");
            sb.AppendLine("            writer.Advance(sizeof(int));");
            sb.AppendLine("            foreach (ref var item in span)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                writer.WriteValue(in item!);");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.CheckBuffer();");
        }
        sb.AppendLine("#else");
        sb.AppendLine("            var size = value.Count;");
        if (useUnmanagedFastPath)
        {
            sb.AppendLine($"            var payloadLength = checked(size * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        }
        else if (elementClassificationKnown)
        {
            sb.AppendLine("            const int payloadLength = 0;");
        }
        else
        {
            sb.AppendLine($"            var payloadLength = global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>() ? 0 : checked(size * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        }
        sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) + payloadLength));");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, size);");
        sb.AppendLine("            writer.Advance(sizeof(int));");
        sb.AppendLine("            if (size == 0) return;");
        sb.AppendLine($"            var buffer = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(size);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                value.CopyTo(buffer, 0);");
        if (useUnmanagedFastPath || requiresRuntimeClassification)
        {
            string indent = requiresRuntimeClassification ? "                    " : "                ";
            if (requiresRuntimeClassification)
            {
                sb.AppendLine($"                if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
                sb.AppendLine("                {");
            }
            sb.AppendLine(indent + "global::System.Array.Reverse(buffer, 0, size);");
            sb.AppendLine(indent + "ref var destination = ref writer.GetSpanReference(index);");
            sb.AppendLine(indent + $"ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref buffer[0]);");
            sb.AppendLine(indent + "global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)payloadLength);");
            sb.AppendLine(indent + "writer.Advance(payloadLength);");
            sb.AppendLine(indent + "return;");
            if (requiresRuntimeClassification)
            {
                sb.AppendLine("                }");
            }
        }
        sb.AppendLine("                for (var i = size - 1; i >= 0; i--)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                    writer.WriteValue(buffer[i]);");
        sb.AppendLine("                }");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(buffer, {clearArrayExpression});");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
    }

    /// <summary>Appends binary reading statements for stack values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        bool requiresRuntimeClassification = !elementClassificationKnown && !useUnmanagedFastPath;

        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Generic.Stack<{elementType}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            var span = LuminPackMarshal.GetStackSpan(value, length);");
        if (useUnmanagedFastPath)
        {
            sb.AppendLine("            reader.DangerousReadUnmanagedSpan(ref index, ref span, length, out var spanOffset);");
            sb.AppendLine("            reader.Advance(spanOffset);");
        }
        else if (elementClassificationKnown)
        {
            sb.AppendLine("            foreach (ref var item in span)");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.ReadValue(ref item!);");
            sb.AppendLine("            }");
        }
        else
        {
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
        sb.AppendLine("#else");
        if (useUnmanagedFastPath || requiresRuntimeClassification)
        {
            string indent = requiresRuntimeClassification ? "                " : "            ";
            if (requiresRuntimeClassification)
            {
                sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
                sb.AppendLine("            {");
            }
            sb.AppendLine(indent + $"var itemSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>();");
            sb.AppendLine(indent + "for (var i = 0; i < length; i++)");
            sb.AppendLine(indent + "{");
            sb.AppendLine(indent + $"    {elementType} item = default!;");
            // The checked Advance validates the element before the cursor moves; the copy reads the captured position.
            sb.AppendLine(indent + "    ref byte element = ref reader.GetSpanReference(index);");
            sb.AppendLine(indent + "    reader.Advance(itemSize);");
            sb.AppendLine(indent + $"    global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref item), ref element, (uint)itemSize);");
            sb.AppendLine(indent + "    value.Push(item);");
            sb.AppendLine(indent + "}");
            sb.AppendLine(indent + "return;");
            if (requiresRuntimeClassification)
            {
                sb.AppendLine("            }");
            }
        }
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                value.Push(item);");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
    }
}
