using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static partial class DictionaryEmitter
{
    /// <summary>Identifies a dictionary value whose existing slot can be refilled without any user code.</summary>
    /// <param name="value">The dictionary value type.</param>
    /// <returns>Whether the value is a string or a known raw scalar.</returns>
    internal static bool IsScalarSlotValue(ITypeSymbol value) =>
        value.SpecialType == SpecialType.System_String ||
        value.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T && value.IsUnmanagedType &&
        (value.TypeKind == TypeKind.Enum || EmitterTypeTraits.IsKnownScalar(value)) && EmitterTypeTraits.FormatterIsRawStorage(value);

    /// <summary>Refills existing string or scalar values in place, continuing through the staged read when a key is missing.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The callback-free fixed-width key type.</param>
    /// <param name="valueType">The string or scalar value type.</param>
    /// <param name="valueSymbol">The value contract.</param>
    /// <param name="rawPair">Whether the complete interleaved payload was already bounds checked.</param>
    private static void AppendDictionaryScalarSlotRead(StringBuilder sb, string keyType, string valueType, ITypeSymbol valueSymbol, bool rawPair)
    {
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"            static void ReadExistingDictionaryScalarSlots(ref global::LuminPack.Core.LuminPackReader reader, global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> destination, int count)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType}[] seen = destination.Count == count ? null : global::System.Buffers.ArrayPool<{keyType}>.Shared.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    var order = destination.GetEnumerator();");
        if (rawPair)
        {
            AppendDenseScalarSlotPrefix(sb, keyType);
            sb.AppendLine("                    if (matched == count) return;");
            sb.AppendLine("                    if (matched > 0) seen = RecordScalarPrefix(destination, count, matched);");
        }
        sb.AppendLine("                    for (int i = " + (rawPair ? "matched" : "0") + "; i < count; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        {keyType} key = default;");
        sb.AppendLine("                        reader.ReadValue(ref key);");
        sb.AppendLine("                        if (seen is null && (!order.MoveNext() || !global::System.Collections.Generic.EqualityComparer<" + keyType + ">.Default.Equals(order.Current.Key, key))) seen = RecordScalarPrefix(destination, count, i);");
        sb.AppendLine("                        if (seen is not null) seen[i] = key;");
        sb.AppendLine("                        ref var slot = ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(destination, key);");
        sb.AppendLine("                        if (global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref slot))");
        sb.AppendLine("                        {");
        sb.AppendLine("                            if (seen is null) { seen = RecordScalarPrefix(destination, count, i); seen[i] = key; }");
        sb.AppendLine("                            ReadRemainingDictionary(ref reader, destination, count, seen, i);");
        sb.AppendLine("                            return;");
        sb.AppendLine("                        }");
        EmitterTypeTraits.AppendReusableElementRead(sb, valueSymbol, "slot", "                        ");
        sb.AppendLine("                    }");
        sb.AppendLine("                    if (seen is not null)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        var keys = new global::System.Collections.Generic.HashSet<{keyType}>(global::System.Math.Min(count, destination.Count));");
        sb.AppendLine("                        for (int i = 0; i < count; i++) if (!keys.Add(seen[i])) destination.Add(seen[i], destination[seen[i]]);");
        // The modern Dictionary enumerator remains valid when Remove deletes its current entry.
        sb.AppendLine("                        foreach (var pair in destination) if (!keys.Contains(pair.Key)) destination.Remove(pair.Key);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine($"                    if (seen is not null) global::System.Buffers.ArrayPool<{keyType}>.Shared.Return(seen);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"            static {keyType}[] RecordScalarPrefix(global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> destination, int count, int prefix)");
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

    /// <summary>Refills contiguous occupied slots until the first key that requires a lookup.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The callback-free raw key type.</param>
    private static void AppendDenseScalarSlotPrefix(StringBuilder sb, string keyType)
    {
        sb.AppendLine("                    int matched = 0;");
        sb.AppendLine("                    if (seen is null && global::System.IntPtr.Size == 8)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var view = LuminPackMarshal.GetDictionaryView(destination);");
        sb.AppendLine("                        if (view._freeCount == 0 && view._count == count)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            ref var first = ref LuminPackMarshal.GetNotNullArrayReference(view._entries!);");
        sb.AppendLine("                            for (; matched < count; matched++)");
        sb.AppendLine("                            {");
        sb.AppendLine("                                ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, (nint)(uint)matched);");
        sb.AppendLine("                                ref byte cursor = ref reader.GetCurrentSpanReference();");
        sb.AppendLine($"                                int keyBytes = global::LuminPack.Core.LuminPackReader.Read(ref cursor, out {keyType} key);");
        sb.AppendLine($"                                if (!global::System.Collections.Generic.EqualityComparer<{keyType}>.Default.Equals(key, entry.Key)) break;");
        sb.AppendLine("                                reader.Advance(keyBytes);");
        sb.AppendLine("                                int valueBytes = global::LuminPack.Core.LuminPackReader.Read(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref cursor, (nint)(uint)keyBytes), out entry.Value);");
        sb.AppendLine("                                reader.Advance(valueBytes);");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
    }
}
