using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides priority queue emitter functionality.</summary>
public static class PriorityQueueEmitter

{
    /// <summary>Evaluates the count and each independently formatted element and priority.</summary>
    /// <param name="fieldData">The priority queue metadata.</param>
    /// <param name="sb">The emitted statements.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            evaluator += 4;");
        sb.AppendLine("            if (value is null) return;");
        sb.AppendLine("            foreach (var pair in value.UnorderedItems)");
        sb.AppendLine("            {");
        sb.AppendLine("                var element = pair.Element;");
        sb.AppendLine("                var priority = pair.Priority;");
        sb.AppendLine("                evaluator.CalculateOffset(ref element);");
        sb.AppendLine("                evaluator.CalculateOffset(ref priority);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary writing statements for priority queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        var priorityType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        // The heap array holds the nodes in the order UnorderedItems enumerates; unrecognized storage enumerates.
        if (fieldData.CanAccessPriorityQueueNodes)
        {
            sb.AppendLine("            if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetPriorityQueueNodes(value, out var nodes, out int nodeCount))");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = 0; i < nodeCount; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    writer.CheckBuffer();");
            sb.AppendLine("                    writer.WriteValue(nodes![i].Element);");
            sb.AppendLine("                    writer.WriteValue(nodes[i].Priority);");
            sb.AppendLine("                }");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            foreach (var item in value.UnorderedItems)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                writer.WriteValue(item.Element);");
        sb.AppendLine("                writer.WriteValue(item.Priority);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for priority queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        var priorityType = GetSecondGeneric(fieldData.TypeName);

        // Validate the independently formatted fixed fields before renting padded tuple storage.
        // Nullable, portable, tagged and Custom element contracts retain their own framing and checks.
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        var priority = fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol { TypeArguments.Length: 2 } pair ? pair.TypeArguments[1] : null;
        if (CollectionEmitterKit.IsRawScalar(element, elementType) && CollectionEmitterKit.IsRawScalar(priority, priorityType))
        {
            sb.AppendLine("            int length = reader.ReadLength((int)global::System.Math.Min((long)global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + priorityType + ">(), int.MaxValue));");
            sb.AppendLine("            if (length < 0) { value = null; return; }");
        }
        else
        {
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
        }
        // The record holds the heap array in its order, so the bulk construction heapifies it in one pass without moving
        // anything, producing the array the per-item insertions produce; other input still forms a valid heap.
        string node = "(" + elementType + " Element, " + priorityType + " Priority)";
        sb.AppendLine($"            var nodes = global::System.Buffers.ArrayPool<{node}>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                for (var i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {elementType} element = default!;");
        sb.AppendLine($"                    {priorityType} priority = default!;");
        sb.AppendLine("                    reader.ReadValue(ref element);");
        sb.AppendLine("                    reader.ReadValue(ref priority);");
        sb.AppendLine("                    nodes[i] = (element, priority);");
        sb.AppendLine("                }");
        sb.AppendLine($"                var source = new global::System.ArraySegment<{node}>(nodes, 0, length);");
        sb.AppendLine("                if (value is null)");
        sb.AppendLine($"                    value = new global::System.Collections.Generic.PriorityQueue<{elementType}, {priorityType}>(source);");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    value.Clear();");
        sb.AppendLine("                    value.EnqueueRange(source);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine($"            finally {{ global::System.Buffers.ArrayPool<{node}>.Shared.Return(nodes, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{node}>()); }}");
    }

    /// <summary>Appends JSON writing statements for priority queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        sb.AppendLine("            foreach (var item in value.UnorderedItems)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayStart();");
        sb.AppendLine("                var element = item.Element;");
        sb.AppendLine("                writer.WriteValue(in element);");
        sb.AppendLine("                var priority = item.Priority;");
        sb.AppendLine("                writer.WriteValue(in priority);");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for priority queue values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        var priorityType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.Generic.PriorityQueue<" + elementType + ", " + priorityType + ">();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                " + elementType + " element = default!;");
        sb.AppendLine("                reader.ReadValue(ref element);");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                " + priorityType + " priority = default!;");
        sb.AppendLine("                reader.ReadValue(ref priority);");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                value.Enqueue(element, priority);");
        sb.AppendLine("            }");
    }
}
