using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static partial class DictionaryEmitter
{
    /// <summary>Emits the approved modern int-keyed refill with staged construction on a missing or null slot.</summary>
    /// <param name="sb">The builder receiving the generated local operation.</param>
    /// <param name="keyType">The canonical int key spelling.</param>
    /// <param name="valueType">The proven leaf reference type spelling.</param>
    /// <param name="valueSymbol">The canonical value framing contract.</param>
    private static void AppendDictionarySlotRead(StringBuilder sb, string keyType, string valueType, ITypeSymbol valueSymbol)
    {
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"            static void ReadExistingDictionarySlots(ref global::LuminPack.Core.LuminPackReader reader, global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> destination, int count)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var seen = global::System.Buffers.ArrayPool<{keyType}>.Shared.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    for (int i = 0; i < count; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        {keyType} key = default;");
        sb.AppendLine("                        reader.ReadValue(ref key);");
        sb.AppendLine("                        seen[i] = key;");
        sb.AppendLine("                        ref var item = ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(destination, key);");
        sb.AppendLine("                        if (global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref item) || item is null)");
        sb.AppendLine("                        {");
        // Preserve the parsed prefix before a constructor or pool rent can change any dictionary entry.
        // The retained slot reference is never used after entering that cold operation.
        sb.AppendLine("                            ReadRemainingDictionary(ref reader, destination, count, seen, i);");
        sb.AppendLine("                            return;");
        sb.AppendLine("                        }");
        EmitterTypeTraits.AppendReusableElementRead(sb, valueSymbol, "item", "                        ");
        sb.AppendLine("                    }");
        sb.AppendLine("                    bool same = destination.Count == count;");
        sb.AppendLine("                    int position = 0;");
        sb.AppendLine("                    if (same) foreach (var pair in destination) if (pair.Key != seen[position++]) { same = false; break; }");
        sb.AppendLine("                    if (!same)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        var keys = new global::System.Collections.Generic.HashSet<{keyType}>();");
        sb.AppendLine("                        for (int i = 0; i < count; i++) if (!keys.Add(seen[i])) destination.Add(seen[i], destination[seen[i]]);");
        sb.AppendLine($"                        var stale = new global::System.Collections.Generic.List<{keyType}>();");
        sb.AppendLine("                        foreach (var pair in destination) if (!keys.Contains(pair.Key)) stale.Add(pair.Key);");
        sb.AppendLine("                        foreach (var key in stale) destination.Remove(key);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine($"                    global::System.Buffers.ArrayPool<{keyType}>.Shared.Return(seen);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        AppendStagedDictionaryRead(sb, keyType, valueType, true, valueSymbol, true);
        sb.AppendLine("#endif");
    }
}
