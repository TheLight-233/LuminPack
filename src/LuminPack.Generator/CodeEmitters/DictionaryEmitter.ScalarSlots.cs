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
        CodeEmitterRegistry.KnownValueTypes.Contains(LuminPack.Code.Core.FormatterTypeName.Get(value)) && EmitterTypeTraits.CanUseUnmanagedSerialization(value);

    /// <summary>Refills existing string or scalar values in place, continuing through the staged read when a key is missing.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The int key type.</param>
    /// <param name="valueType">The string or scalar value type.</param>
    /// <param name="valueSymbol">The value contract.</param>
    private static void AppendDictionaryScalarSlotRead(StringBuilder sb, string keyType, string valueType, ITypeSymbol valueSymbol)
    {
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"            static void ReadExistingDictionaryScalarSlots(ref global::LuminPack.Core.LuminPackReader reader, global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> destination, int count)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType}[] seen = destination.Count == count ? null : global::System.Buffers.ArrayPool<{keyType}>.Shared.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    var order = destination.GetEnumerator();");
        sb.AppendLine("                    for (int i = 0; i < count; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        {keyType} key = default;");
        sb.AppendLine("                        reader.ReadValue(ref key);");
        sb.AppendLine("                        if (seen is null && (!order.MoveNext() || order.Current.Key != key)) seen = RecordScalarPrefix(destination, count, i);");
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
}
