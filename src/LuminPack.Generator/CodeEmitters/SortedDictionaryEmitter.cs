using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides sorted dictionary emitter functionality.</summary>
public static class SortedDictionaryEmitter
{
    /// <summary>Appends binary writing statements for sorted dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        // The in-order node walk yields the enumeration order; the pairs are copied once instead of through the enumerator stack.
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

    /// <summary>Appends binary reading statements for sorted dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (!reader.TryReadCollectionHead(out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + ")) { value = null; return; }");
        sb.AppendLine("            if (value is null) value = new global::System.Collections.Generic.SortedDictionary<" + keyType + ", " + valueType + ">();");
        sb.AppendLine("            else value.Clear();");
        // Strictly ascending keys under the default comparer link the tree in one pass; any other input is added pair by pair.
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
        sb.AppendLine("                global::LuminPack.Utility.LuminTreeStorage.FillSortedDictionary(value, pairs, length);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally { global::System.Buffers.ArrayPool<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>.Shared.Return(pairs, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">>()); }");
    }
}
