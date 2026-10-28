using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides queue emitter functionality.</summary>
public static class QueueEmitter

{
    /// <summary>Appends binary writing statements for queue values.</summary>
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

        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                index += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var size = value.Count;");
        sb.AppendLine("            if (size == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, 0);");
        sb.AppendLine("                index += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
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
            sb.AppendLine($"            var payloadLength = global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>()");
            sb.AppendLine("                ? 0");
            sb.AppendLine($"                : checked(size * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        }
        sb.AppendLine();
        sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) * 4 + payloadLength));");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, size);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine("            writer.WriteUnmanaged(0);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine("            writer.WriteUnmanaged(0);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine("            writer.WriteUnmanaged(size);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine();
        if (useUnmanagedFastPath)
        {
            AppendQueueUnmanagedSerializeCode(sb, elementType, "            ");
            return;
        }

        if (requiresRuntimeClassification)
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            AppendQueueUnmanagedSerializeCode(sb, elementType, "                ");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            global::LuminPack.Code.LuminPackMarshal.GetQueueSize(value, out _, out var referenceHead, out _);");
        sb.AppendLine("            var referenceStorage = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value);");
        sb.AppendLine("            var referenceFirstLength = global::System.Math.Min(size, referenceStorage.Length - referenceHead);");
        sb.AppendLine("            var first = referenceStorage.Slice(referenceHead, referenceFirstLength);");
        sb.AppendLine("            var second = referenceStorage.Slice(0, size - referenceFirstLength);");
        sb.AppendLine("            foreach (ref var item in first)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("            foreach (ref var item in second)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("#else");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                var current = item;");
        sb.AppendLine("                writer.WriteValue(in current);");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary writing statements for queue values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void AppendQueueUnmanagedSerializeCode(
        StringBuilder sb, string elementType, string indent)
    {
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine(indent + "global::LuminPack.Code.LuminPackMarshal.GetQueueSize(value, out _, out var head, out _);");
        sb.AppendLine(indent + "var storage = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value);");
        sb.AppendLine(indent + "var firstLength = global::System.Math.Min(size, storage.Length - head);");
        sb.AppendLine(indent + "var firstSegment = storage.Slice(head, firstLength);");
        sb.AppendLine(indent + "var secondSegment = storage.Slice(0, size - firstLength);");
        sb.AppendLine(indent + "ref var destination = ref writer.GetSpanReference(index);");
        sb.AppendLine(indent + $"var firstBytes = checked(firstSegment.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        sb.AppendLine(indent + "if (firstBytes != 0)");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + $"    ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref firstSegment.GetPinnableReference()!);");
        sb.AppendLine(indent + "    global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)firstBytes);");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "var secondBytes = payloadLength - firstBytes;");
        sb.AppendLine(indent + "if (secondBytes != 0)");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + $"    ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref secondSegment.GetPinnableReference()!);");
        sb.AppendLine(indent + "    global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref destination, (nint)(uint)firstBytes), ref source, (uint)secondBytes);");
        sb.AppendLine(indent + "}");
        sb.AppendLine("#else");
        sb.AppendLine(indent + $"var buffer = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(size);");
        sb.AppendLine(indent + "try");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    value.CopyTo(buffer, 0);");
        sb.AppendLine(indent + "    ref var destination = ref writer.GetSpanReference(index);");
        sb.AppendLine(indent + $"    ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref buffer[0]);");
        sb.AppendLine(indent + "    global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)payloadLength);");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "finally");
        sb.AppendLine(indent + "{");
        // This path is reached only after proving that T has no managed references.
        sb.AppendLine(indent + $"    global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(buffer, clearArray: false);");
        sb.AppendLine(indent + "}");
        sb.AppendLine("#endif");
        sb.AppendLine(indent + "index += payloadLength;");
        sb.AppendLine(indent + "return;");
    }

    /// <summary>Appends binary reading statements for queue values.</summary>
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
        sb.AppendLine($"                value = new global::System.Collections.Generic.Queue<{elementType}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("                value.EnsureCapacity(length);");
        sb.AppendLine("#endif");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 0)");
        sb.AppendLine("                return;");
        sb.AppendLine();
        // The checked Advance validates the three storage words before the cursor moves; they are then loaded from the
        // captured position.
        sb.AppendLine("            ref byte storage = ref reader.GetCurrentSpanReference();");
        sb.AppendLine("            reader.Advance(12);");
        sb.AppendLine("            global::LuminPack.Core.LuminPackReader.Read(ref storage, out int storedHead);");
        sb.AppendLine("            global::LuminPack.Core.LuminPackReader.Read(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref storage, 4), out int storedTail);");
        sb.AppendLine("            global::LuminPack.Core.LuminPackReader.Read(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref storage, 8), out int storedSize);");
        sb.AppendLine();
        sb.AppendLine("            if (storedSize != length || storedHead < 0 || storedTail < 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidRange(storedSize, length);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            var span = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value, length);");
        sb.AppendLine("            var normalizedTail = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value).Length == length ? 0 : length;");
        sb.AppendLine();
        if (useUnmanagedFastPath)
        {
            AppendQueueUnmanagedDeserializeNet8Code(sb, elementType, "            ");
        }
        else
        {
            if (requiresRuntimeClassification)
            {
                sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
                sb.AppendLine("            {");
                AppendQueueUnmanagedDeserializeNet8Code(sb, elementType, "                ");
                sb.AppendLine("            }");
                sb.AppendLine();
            }
            sb.AppendLine("            for (int i = 0; i < length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.ReadValue(ref span[i]);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            global::LuminPack.Code.LuminPackMarshal.SetQueueSize(value, normalizedTail, 0, length);");
        }
        sb.AppendLine("#else");
        if (useUnmanagedFastPath)
        {
            AppendQueueUnmanagedDeserializeLegacyCode(sb, elementType, "            ");
        }
        else
        {
            if (requiresRuntimeClassification)
            {
                sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
                sb.AppendLine("            {");
                AppendQueueUnmanagedDeserializeLegacyCode(sb, elementType, "                ");
                sb.AppendLine("            }");
            }
            sb.AppendLine("            for (var i = 0; i < length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {elementType} item = default!;");
            sb.AppendLine("                reader.ReadValue(ref item);");
            sb.AppendLine("                value.Enqueue(item);");
            sb.AppendLine("            }");
        }
        sb.AppendLine("#endif");
    }

    /// <summary>Appends binary reading statements for queue values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void AppendQueueUnmanagedDeserializeNet8Code(
        StringBuilder sb, string elementType, string indent)
    {
        sb.AppendLine(indent + "ref var dest = ref global::LuminPack.Code.LuminPackMarshal.GetReference(ref span.GetPinnableReference());");
        sb.AppendLine(indent + $"var srcLength = checked(length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        // The checked Advance validates the payload before the cursor moves; the copy reads the captured position.
        sb.AppendLine(indent + "ref byte payload = ref reader.GetSpanReference(index);");
        sb.AppendLine(indent + "reader.Advance(srcLength);");
        sb.AppendLine(indent + "global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref dest, ref payload, (uint)srcLength);");
        sb.AppendLine(indent + "global::LuminPack.Code.LuminPackMarshal.SetQueueSize(value, normalizedTail, 0, length);");
        sb.AppendLine(indent + "return;");
    }

    /// <summary>Appends binary reading statements for queue values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void AppendQueueUnmanagedDeserializeLegacyCode(
        StringBuilder sb, string elementType, string indent)
    {
        sb.AppendLine(indent + $"var itemSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>();");
        sb.AppendLine(indent + "for (var i = 0; i < length; i++)");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + $"    {elementType} item = default!;");
        // The checked Advance validates the element before the cursor moves; the copy reads the captured position.
        sb.AppendLine(indent + "    ref byte element = ref reader.GetSpanReference(index);");
        sb.AppendLine(indent + "    reader.Advance(itemSize);");
        sb.AppendLine(indent + $"    global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref item), ref element, (uint)itemSize);");
        sb.AppendLine(indent + "    value.Enqueue(item);");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "return;");
    }
}
