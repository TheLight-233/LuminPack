using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent dictionary emitter functionality.</summary>
public static class ConcurrentDictionaryEmitter
{
    /// <summary>Appends binary writing statements for concurrent dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Counts during the lock-free enumeration and publishes the count afterwards, because <c>Count</c> acquires every lock.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value",
            new[] { "var key = item.Key;", "writer.WriteValue(in key);", "var itemValue = item.Value;", "writer.WriteValue(in itemValue);" }, "            ");
    }

    /// <summary>Appends binary reading statements for concurrent dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>A fresh dictionary is presized with the default concurrency level, because every table resize recreates all nodes; twice the count keeps every lock below its resize budget.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (!reader.TryReadCollectionHead(out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + ")) { value = null; return; }");
        // A new dictionary is built by its collection constructor, which sizes the table once and adds without taking locks.
        // That constructor rejects duplicate keys, which the original insertions ignore, so they rebuild through TryAdd.
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                var pairs = global::System.Buffers.ArrayPool<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>.Shared.Rent(length);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    for (int i = 0; i < length; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        " + keyType + " k = default!;");
        sb.AppendLine("                        " + valueType + " v = default!;");
        sb.AppendLine("                        reader.ReadValue(ref k);");
        sb.AppendLine("                        reader.ReadValue(ref v);");
        sb.AppendLine("                        pairs[i] = new global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">(k!, v);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentDictionary(pairs, length);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally { global::System.Buffers.ArrayPool<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>.Shared.Return(pairs, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>()); }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            value.Clear();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + keyType + " k = default!;");
        sb.AppendLine("                " + valueType + " v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.TryAdd(k!, v);");
        sb.AppendLine("            }");
    }
}
