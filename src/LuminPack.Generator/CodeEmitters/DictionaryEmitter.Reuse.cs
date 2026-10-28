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
    /// <param name="unionSlotCases">Certified narrow Union cases refilled inside existing slots, or null.</param>
    /// <param name="scalarSlots">Whether existing string or known scalar values are refilled inside their slots.</param>
    /// <param name="rawPair">Whether the writer stores each entry as the raw native storage of its key and value.</param>
    /// <param name="stableStringKeys">Whether canonical metadata and the referenced runtime permit complete string-key staging.</param>
    private static void AppendReusableDictionaryRead(StringBuilder sb, string keyType, string valueType, bool nonNullableValueKey,
        Microsoft.CodeAnalysis.ITypeSymbol valueSymbol, bool canReuseSlots, System.Collections.Generic.List<(int Id, string Type)> unionSlotCases = null, bool scalarSlots = false,
        bool rawPair = false, bool stableStringKeys = false)
    {
        sb.AppendLine("            if (value is not null)");
        sb.AppendLine("            {");
        if (scalarSlots)
        {
            sb.AppendLine("#if NET8_0_OR_GREATER");
            sb.AppendLine($"                if (global::System.Object.ReferenceEquals(value.Comparer, global::System.Collections.Generic.EqualityComparer<{keyType}>.Default))");
            sb.AppendLine("                {");
            sb.AppendLine("                    ReadExistingDictionaryScalarSlots(ref reader, value, length);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("#endif");
        }
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
        if (unionSlotCases != null)
        {
            sb.AppendLine("#if NET8_0_OR_GREATER");
            sb.AppendLine($"                if (global::System.Object.ReferenceEquals(value.Comparer, global::System.Collections.Generic.EqualityComparer<{keyType}>.Default))");
            sb.AppendLine("                {");
            sb.AppendLine("                    ReadExistingDictionaryUnionSlots(ref reader, value, length);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("#endif");
        }
        sb.AppendLine("                ReadExistingDictionary(ref reader, value, length);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        if (canReuseSlots) AppendDictionarySlotRead(sb, keyType, valueType, valueSymbol);
        if (unionSlotCases != null) AppendDictionaryUnionSlotRead(sb, keyType, valueType, valueSymbol, unionSlotCases);
        if (scalarSlots) AppendDictionaryScalarSlotRead(sb, keyType, valueType, valueSymbol);
        AppendStagedDictionaryRead(sb, keyType, valueType, nonNullableValueKey, valueSymbol, false, rawPair, stableStringKeys);
    }

    /// <summary>Refills an existing default-comparer dictionary of raw unmanaged pairs through the fresh bucket build.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The unmanaged key type.</param>
    /// <param name="valueType">The unmanaged value type.</param>
    /// <remarks>Raw keys and values carry no identity and execute no user code, so storing every decoded pair into the
    /// cleared instance and rebuilding its buckets yields the state of a fresh read inside the reused instance: the same
    /// pairs, the same enumeration order and the retained capacity. Duplicate keys still fail through <c>Add</c>.</remarks>
    private static void AppendRawDictionaryRefill(StringBuilder sb, string keyType, string valueType)
    {
        sb.AppendLine("            if (value is not null && global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8 &&");
        sb.AppendLine($"                global::System.Object.ReferenceEquals(value.Comparer, global::System.Collections.Generic.EqualityComparer<{keyType}>.Default))");
        sb.AppendLine("            {");
        sb.AppendLine("                int refillVersion = LuminPackMarshal.GetDictionaryView(value)._version;");
        sb.AppendLine("                var refillView = LuminPackMarshal.PrepareDictionaryRefill(value, length);");
        sb.AppendLine("                ref var refillRef = ref LuminPackMarshal.GetNotNullArrayReference(refillView._entries!);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref refillRef, (nint)(uint)i);");
        AppendRawPair(sb, "entry.Key", "entry.Value", valueType, "                    ", write: false);
        sb.AppendLine("                }");
        sb.AppendLine("                if (!LuminPackMarshal.TryRebuildFreshDictionaryBuckets(refillView, length))");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var pairs = new global::System.Collections.Generic.KeyValuePair<{keyType}, {valueType}>[length];");
        sb.AppendLine("                    for (int i = 0; i < length; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref refillRef, (nint)(uint)i);");
        sb.AppendLine($"                        pairs[i] = new global::System.Collections.Generic.KeyValuePair<{keyType}, {valueType}>(entry.Key, entry.Value);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    value.Clear();");
        sb.AppendLine("                    foreach (var pair in pairs) value.Add(pair.Key, pair.Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                    refillView._version = unchecked(refillVersion + length + 1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
    }

    /// <summary>Emits the original staged read or its cold continuation after a slot prefix.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The original key type.</param>
    /// <param name="valueType">The original value type.</param>
    /// <param name="nonNullableValueKey">Whether the original key type cannot represent null.</param>
    /// <param name="valueSymbol">The canonical dictionary value contract.</param>
    /// <param name="parsedPrefix">Whether keys and existing values have already been read before construction.</param>
    /// <param name="rawPair">Whether each entry was written as the raw storage of its key and value, which is also how it is read here.</param>
    /// <param name="stableStringKeys">Whether the existing reuse helper may attempt the complete staged string-key kernel.</param>
    private static void AppendStagedDictionaryRead(StringBuilder sb, string keyType, string valueType, bool nonNullableValueKey,
        Microsoft.CodeAnalysis.ITypeSymbol valueSymbol, bool parsedPrefix, bool rawPair = false, bool stableStringKeys = false)
    {
        string pair = $"global::System.Collections.Generic.KeyValuePair<{keyType}, {valueType}>";
        string dictionary = $"global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>";
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        string name = parsedPrefix ? "ReadRemainingDictionary" : "ReadExistingDictionary";
        string prefixParameters = parsedPrefix ? $", {keyType}[] seen, int parsed" : "";
        sb.AppendLine($"            static void {name}(ref global::LuminPack.Core.LuminPackReader reader, {dictionary} destination, int count{prefixParameters})");
        sb.AppendLine("            {");
        if (stableStringKeys && !parsedPrefix)
        {
            sb.AppendLine("                bool stableKeysHandled;");
            sb.AppendLine($"                stableKeysHandled = global::LuminPack.Code.LuminPackMarshal.TryReadStableStringKeys<{valueType}>(ref reader, destination, count);");
            sb.AppendLine("                if (stableKeysHandled)");
            sb.AppendLine("                    return;");
        }
        // Built-in values replace all their storage; a custom comparer can still observe the original lookup.
        bool rawIntValues = (keyType is "int" or "System.Int32" or "global::System.Int32") && valueSymbol != null &&
            valueSymbol.SpecialType != Microsoft.CodeAnalysis.SpecialType.None && EmitterTypeTraits.CanUseUnmanagedSerialization(valueSymbol);
        if (rawIntValues)
            sb.AppendLine("                bool preserveValues = !global::System.Object.ReferenceEquals(destination.Comparer, global::System.Collections.Generic.EqualityComparer<int>.Default);");
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
        if (rawPair && !rawIntValues && !parsedPrefix)
        {
            // The writer stored the pair as raw native storage (Nullable, KeyValuePair and other framed values included), and
            // the fresh read decodes it the same way; both members replace their complete storage.
            sb.AppendLine($"                        {valueType} item = default!;");
            AppendRawPair(sb, "key", "item", valueType, "                        ", write: false);
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
            return;
        }
        if (parsedPrefix)
        {
            sb.AppendLine("                        if (i == parsed) key = seen[i];");
            sb.AppendLine("                        else reader.ReadValue(ref key!);");
        }
        else sb.AppendLine("                        reader.ReadValue(ref key!);");
        sb.AppendLine($"                        {valueType} item = " + (rawIntValues ? "preserveValues && " : "") + (nonNullableValueKey ? "" : "key is { } && ") + "destination.TryGetValue(key, out var existing) ? existing : default!;");
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
