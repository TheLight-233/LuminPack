using System.Text;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static partial class DictionaryEmitter
{
    /// <summary>Adds a reusable dictionary path while retaining the original fresh bucket-building operation.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The original key type.</param>
    /// <param name="valueType">The original value type.</param>
    /// <param name="nonNullableValueKey">Whether null cannot be represented by the original key type.</param>
    /// <param name="valueSymbol">The canonical dictionary value type.</param>
    /// <param name="canReuseSlots">Whether canonical analysis proves an existing value cannot execute user code.</param>
    private static void AppendReusableDictionaryRead(StringBuilder sb, string keyType, string valueType, bool nonNullableValueKey,
        Microsoft.CodeAnalysis.ITypeSymbol valueSymbol, bool canReuseSlots)
    {
        sb.AppendLine("            if (value is not null)");
        sb.AppendLine("            {");
        if (canReuseSlots)
        {
            sb.AppendLine("#if NET8_0_OR_GREATER");
            sb.AppendLine($"                if (global::System.Object.ReferenceEquals(value.Comparer, global::System.Collections.Generic.EqualityComparer<{keyType}>.Default))");
            sb.AppendLine("                {");
            sb.AppendLine("                    ReadExistingDictionarySlots(ref reader, value, length);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("#endif");
        }
        sb.AppendLine("                ReadExistingDictionary(ref reader, value, length);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        if (canReuseSlots) AppendDictionarySlotRead(sb, keyType, valueType, valueSymbol);
        AppendStagedDictionaryRead(sb, keyType, valueType, nonNullableValueKey, valueSymbol, false);
    }

    /// <summary>Emits the original staged read or its cold continuation after a slot prefix.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The original key type.</param>
    /// <param name="valueType">The original value type.</param>
    /// <param name="nonNullableValueKey">Whether the original key type cannot represent null.</param>
    /// <param name="valueSymbol">The canonical dictionary value contract.</param>
    /// <param name="parsedPrefix">Whether keys and existing values have already been read before construction.</param>
    private static void AppendStagedDictionaryRead(StringBuilder sb, string keyType, string valueType, bool nonNullableValueKey,
        Microsoft.CodeAnalysis.ITypeSymbol valueSymbol, bool parsedPrefix)
    {
        string pair = $"global::System.Collections.Generic.KeyValuePair<{keyType}, {valueType}>";
        string dictionary = $"global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>";
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        string name = parsedPrefix ? "ReadRemainingDictionary" : "ReadExistingDictionary";
        string prefixParameters = parsedPrefix ? $", {keyType}[] seen, int parsed" : "";
        sb.AppendLine($"            static void {name}(ref global::LuminPack.Core.LuminPackReader reader, {dictionary} destination, int count{prefixParameters})");
        sb.AppendLine("            {");
        sb.AppendLine($"                var items = global::System.Buffers.ArrayPool<{pair}>.Shared.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        if (keyType is "string" or "System.String" or "global::System.String")
            sb.AppendLine("                    var previous = destination.GetEnumerator();");
        if (parsedPrefix)
        {
            sb.AppendLine($"                    for (int i = 0; i < parsed; i++) items[i] = new {pair}(seen[i], destination[seen[i]]);");
        }
        sb.AppendLine("                    for (int i = " + (parsedPrefix ? "parsed" : "0") + "; i < count; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        {keyType} key = " + (keyType is "string" or "System.String" or "global::System.String" ? "previous.MoveNext() ? previous.Current.Key : default!;" : "default!;"));
        if (parsedPrefix)
        {
            sb.AppendLine("                        if (i == parsed) key = seen[i];");
            sb.AppendLine("                        else reader.ReadValue(ref key!);");
        }
        else sb.AppendLine("                        reader.ReadValue(ref key!);");
        sb.AppendLine($"                        {valueType} item = " + (nonNullableValueKey ? "" : "key is { } && ") + "destination.TryGetValue(key, out var existing) ? existing : default!;");
        EmitterTypeTraits.AppendReusableElementRead(sb, valueSymbol, "item", "                        ");
        sb.AppendLine($"                        items[i] = new {pair}(key, item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    destination.Clear();");
        sb.AppendLine("                    for (int i = 0; i < count; i++) destination.Add(items[i].Key, items[i].Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine($"                    global::System.Buffers.ArrayPool<{pair}>.Shared.Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pair}>());");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }
}
