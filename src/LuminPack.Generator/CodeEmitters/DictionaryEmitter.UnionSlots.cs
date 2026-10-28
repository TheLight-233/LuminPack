using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static partial class DictionaryEmitter
{
    /// <summary>Refills existing exact Union case instances in place, using staged construction for every other value.</summary>
    /// <param name="sb">The builder receiving the generated local operation.</param>
    /// <param name="keyType">The canonical int key spelling.</param>
    /// <param name="valueType">The Union root spelling.</param>
    /// <param name="valueSymbol">The canonical value framing contract.</param>
    /// <param name="cases">Narrow tags and their certified fixed case types.</param>
    private static void AppendDictionaryUnionSlotRead(StringBuilder sb, string keyType, string valueType, ITypeSymbol valueSymbol,
        List<(int Id, string Type)> cases)
    {
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"            static void ReadExistingDictionaryUnionSlots(ref global::LuminPack.Core.LuminPackReader reader, global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> destination, int count)");
        sb.AppendLine("            {");
        // Matching incoming order is proven against the unmutated dictionary; keys are recorded only once that proof fails.
        sb.AppendLine($"                {keyType}[] seen = destination.Count == count ? null : global::System.Buffers.ArrayPool<{keyType}>.Shared.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    var order = destination.GetEnumerator();");
        sb.AppendLine("                    for (int i = 0; i < count; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        {keyType} key = default;");
        sb.AppendLine("                        reader.ReadValue(ref key);");
        sb.AppendLine("                        if (seen is null && (!order.MoveNext() || order.Current.Key != key)) seen = RecordPrefix(destination, count, i);");
        sb.AppendLine("                        if (seen is not null) seen[i] = key;");
        sb.AppendLine("                        ref var slot = ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(destination, key);");
        // A single load lets the JIT prove the case instance nonnull, removing the original construction branch.
        sb.AppendLine("                        if (global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref slot) || slot is not { } item) goto remaining;");
        sb.AppendLine("                        switch (reader.Cursor)");
        sb.AppendLine("                        {");
        foreach (var (id, type) in cases)
        {
            sb.AppendLine($"                            case {id}:");
            sb.AppendLine("                            {");
            sb.AppendLine($"                                if (item.GetType() != typeof({type})) goto remaining;");
            sb.AppendLine("                                reader.Advance(1);");
            sb.AppendLine($"                                var member = global::System.Runtime.CompilerServices.Unsafe.As<{type}>(item);");
            sb.AppendLine("                                reader.ReadPolymorphismValue(ref member);");
            sb.AppendLine("                                continue;");
            sb.AppendLine("                            }");
        }
        sb.AppendLine("                        }");
        sb.AppendLine("                        remaining:");
        sb.AppendLine("                        if (seen is null) { seen = RecordPrefix(destination, count, i); seen[i] = key; }");
        sb.AppendLine("                        ReadRemainingDictionary(ref reader, destination, count, seen, i);");
        sb.AppendLine("                        return;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    if (seen is not null)");
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
        sb.AppendLine($"                    if (seen is not null) global::System.Buffers.ArrayPool<{keyType}>.Shared.Return(seen);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"            static {keyType}[] RecordPrefix(global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> destination, int count, int prefix)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var keys = global::System.Buffers.ArrayPool<{keyType}>.Shared.Rent(count);");
        sb.AppendLine("                int position = 0;");
        sb.AppendLine("                foreach (var pair in destination)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (position == prefix) break;");
        sb.AppendLine("                    keys[position++] = pair.Key;");
        sb.AppendLine("                }");
        sb.AppendLine("                return keys;");
        sb.AppendLine("            }");
        AppendStagedDictionaryRead(sb, keyType, valueType, true, valueSymbol, true);
        sb.AppendLine("#endif");
    }
}
