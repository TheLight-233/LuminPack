using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable sorted dictionary emitter functionality.</summary>
public static class ImmutableSortedDictionaryEmitter
{
    /// <summary>Appends binary writing statements for immutable sorted dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Copies the pairs once through the in-order tree copy instead of the pooled-stack enumerator.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        sb.AppendLine("            int count = value.Count;");
        sb.AppendLine("            writer.EnsureAdditionalCapacity(4);");
        sb.AppendLine("            writer.Write(count);");
        sb.AppendLine("            if (count != 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                var pairs = global::System.Buffers.ArrayPool<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>.Shared.Rent(count);");
        sb.AppendLine("                global::LuminPack.Utility.LuminTreeStorage.CopyTo(value, pairs);");
        sb.AppendLine("                for (int i = 0; i < count; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                    writer.WriteValue(pairs[i].Key);");
        sb.AppendLine("                    writer.WriteValue(pairs[i].Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                global::System.Buffers.ArrayPool<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>.Shared.Return(pairs, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>());");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for immutable sorted dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (!reader.TryReadCollectionHead(out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + ")) { value = null; return; }");
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Immutable.ImmutableSortedDictionary<" + keyType + ", " + valueType + ">.Empty; return; }");
        // Strictly ascending keys link the tree in one pass; any other input goes through the builder.
        sb.AppendLine("            var pairs = global::System.Buffers.ArrayPool<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    " + keyType + " k = default!;");
        sb.AppendLine("                    " + valueType + " v = default!;");
        sb.AppendLine("                    reader.ReadValue(ref k);");
        sb.AppendLine("                    reader.ReadValue(ref v);");
        sb.AppendLine("                    pairs[i] = new global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">(k!, v);");
        sb.AppendLine("                }");
        sb.AppendLine("                value = global::LuminPack.Utility.LuminTreeStorage.CreateImmutableSortedDictionary(pairs, length);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally { global::System.Buffers.ArrayPool<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>.Shared.Return(pairs, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>()); }");
    }
}
