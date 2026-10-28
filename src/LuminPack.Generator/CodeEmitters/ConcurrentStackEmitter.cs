using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent stack emitter functionality.</summary>
public static class ConcurrentStackEmitter
{
    /// <summary>Appends binary writing statements for concurrent stack values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Takes one snapshot traversal and writes it from the bottom element up, the established order, without the linear <c>Count</c> walk.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        sb.AppendLine("            var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentSnapshot(value, out var count);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("            global::System.MemoryExtensions.Reverse(global::System.MemoryExtensions.AsSpan(snapshot, 0, count));");
        CollectionEmitterKit.AppendSpanWrite(sb, element, elementType, "new global::System.ReadOnlySpan<" + elementType + ">(snapshot, 0, count)", "            ");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(snapshot, " + CollectionEmitterKit.ClearExpression(element, elementType) + ");");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary reading statements for concurrent stack values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Pushes the bottom-up elements as one range, which is equivalent to pushing them in order but publishes the chain with a single exchange.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine("            if (value is null) value = new global::System.Collections.Concurrent.ConcurrentStack<" + elementType + ">();");
        sb.AppendLine("            else value.Clear();");
        sb.AppendLine("            if (length == 0) return;");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[] { "value.PushRange(__pooled, 0, length);" }, "            ");
    }

    /// <summary>Restores JSON enumeration order while retaining the existing instance and failed-read prefix.</summary>
    /// <param name="fieldData">The stack metadata.</param>
    /// <param name="sb">The generated statements.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        ListEmitter.GenerateJsonEnumerableDeserializeCode(fieldData, sb);
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentSnapshot(value, out var count);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("                if (count != 0) value.PushRange(snapshot, 0, count);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(snapshot, " +
            CollectionEmitterKit.ClearExpression(EmitterTypeTraits.GetFirstTypeArgument(fieldData), elementType) + ");");
        sb.AppendLine("            }");
    }
}
