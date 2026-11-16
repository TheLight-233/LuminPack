using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static partial class DictionaryEmitter
{
    /// <summary>Adds a reusable dictionary path while retaining the original fresh bucket-building operation.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="field">The dictionary's canonical types and available reuse paths.</param>
    /// <param name="keyType">The original key type.</param>
    /// <param name="valueType">The original value type.</param>
    /// <param name="rawPair">Whether the writer stores each entry as the raw native storage of its key and value.</param>
    private static void AppendReusableDictionaryRead(StringBuilder sb, LuminLocalFieldData field, string keyType, string valueType, bool rawPair)
    {
        var dictionary = field.TypeSymbol as INamedTypeSymbol;
        var valueSymbol = dictionary?.TypeArguments[1];
        var keySymbol = dictionary?.TypeArguments[0];
        bool nonNullableValueKey = keySymbol?.IsValueType == true && keySymbol.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
        sb.AppendLine("            if (value is not null)");
        sb.AppendLine("            {");
        if (field.CanReuseDictionaryScalarSlots)
        {
            sb.AppendLine("#if NET8_0_OR_GREATER");
            sb.AppendLine($"                if (global::System.Object.ReferenceEquals(value.Comparer, global::System.Collections.Generic.EqualityComparer<{keyType}>.Default))");
            sb.AppendLine("                {");
            sb.AppendLine("                    ReadExistingDictionaryScalarSlots(ref reader, value, length);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("#endif");
        }
        if (field.CanReuseDictionarySlots)
        {
            sb.AppendLine("#if NET8_0_OR_GREATER");
            sb.AppendLine($"                if (global::System.Object.ReferenceEquals(value.Comparer, global::System.Collections.Generic.EqualityComparer<{keyType}>.Default))");
            sb.AppendLine("                {");
            sb.AppendLine("                    ReadExistingDictionarySlots(ref reader, value, length);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("#endif");
        }
        if (field.UnionSlotCases != null)
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
        if (field.CanReuseDictionarySlots)
            AppendDictionarySlotRead(sb, keyType, valueType, valueSymbol);
        if (field.UnionSlotCases != null)
            AppendDictionaryUnionSlotRead(sb, keyType, valueType, valueSymbol, field.UnionSlotCases);
        if (field.CanReuseDictionaryScalarSlots)
            AppendDictionaryScalarSlotRead(sb, keyType, valueType, valueSymbol, rawPair);
        AppendStagedDictionaryRead(sb, keyType, valueType, nonNullableValueKey, valueSymbol, false, rawPair, field.CanReuseEnumeratedStringKey,
            rawPair && field.HasCallbackFreeDictionaryKey && IsScalarSlotValue(valueSymbol));
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
        string pair = $"global::System.Collections.Generic.KeyValuePair<{keyType}, {valueType}>";
        string dictionary = $"global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>";
        string pool = $"global::System.Buffers.ArrayPool<{pair}>.Shared";
        string storage = $"global::LuminPack.Code.LuminPackMarshal.DictionaryView<{keyType}, {valueType}>.Entry";
        sb.AppendLine("            if (value is { Count: 0 } && global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8 &&");
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
        sb.AppendLine("                    ReplayRejectedRefill(value, ref refillRef, length);");
        sb.AppendLine("                else");
        sb.AppendLine("                    refillView._version = unchecked(refillVersion + length + 1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"            static void ReplayRejectedRefill({dictionary} destination, ref {storage} firstEntry, int count)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var pairs = {pool}.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    for (int i = 0; i < count; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntry, (nint)(uint)i);");
        sb.AppendLine($"                        pairs[i] = new {pair}(entry.Key, entry.Value);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    destination.Clear();");
        // A rejected rebuild leaves provisional buckets even when Clear sees zero published entries.
        sb.AppendLine("                    var buckets = LuminPackMarshal.GetDictionaryView(destination)._buckets;");
        sb.AppendLine("                    global::System.Array.Clear(buckets, 0, buckets.Length);");
        sb.AppendLine("                    for (int i = 0; i < count; i++) destination.Add(pairs[i].Key, pairs[i].Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {pool}.Return(pairs, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pair}>());");
        sb.AppendLine("                }");
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
    /// <param name="enumeratedStringKey">Whether matching string keys may reuse the enumerated value after decoding.</param>
    /// <param name="observeRawLookup">Whether the staged scalar path exposes the custom comparer lookup between fields.</param>
    private static void AppendStagedDictionaryRead(StringBuilder sb, string keyType, string valueType, bool nonNullableValueKey,
        Microsoft.CodeAnalysis.ITypeSymbol valueSymbol, bool parsedPrefix, bool rawPair = false, bool enumeratedStringKey = false, bool observeRawLookup = false)
    {
        string pair = $"global::System.Collections.Generic.KeyValuePair<{keyType}, {valueType}>";
        string dictionary = $"global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>";
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        string name = parsedPrefix ? "ReadRemainingDictionary" : "ReadExistingDictionary";
        string prefixParameters = parsedPrefix ? $", {keyType}[] seen, int parsed" : "";
        sb.AppendLine($"            static void {name}(ref global::LuminPack.Core.LuminPackReader reader, {dictionary} destination, int count{prefixParameters})");
        sb.AppendLine("            {");
        if (observeRawLookup)
            sb.AppendLine($"                bool observeLookup = !global::System.Object.ReferenceEquals(destination.Comparer, global::System.Collections.Generic.EqualityComparer<{keyType}>.Default);");
        sb.AppendLine($"                var items = global::System.Buffers.ArrayPool<{pair}>.Shared.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        if (enumeratedStringKey)
            sb.AppendLine("                    bool useEnumeratedValue = reader.CanReuseEnumeratedStringKey(destination.Comparer);");
        if (keyType is "string" or "System.String" or "global::System.String")
            sb.AppendLine("                    var previous = destination.GetEnumerator();");
        if (parsedPrefix)
        {
            sb.AppendLine($"                    for (int i = 0; i < parsed; i++) items[i] = new {pair}(seen[i], destination[seen[i]]);");
        }
        sb.AppendLine("                    for (int i = " + (parsedPrefix ? "parsed" : "0") + "; i < count; i++)");
        sb.AppendLine("                    {");
        if (enumeratedStringKey)
        {
            sb.AppendLine("                        var previousPair = previous.MoveNext() ? previous.Current : default;");
            sb.AppendLine($"                        {keyType} key = previousPair.Key;");
        }
        else
            sb.AppendLine($"                        {keyType} key = " + (keyType is "string" or "System.String" or "global::System.String" ? "previous.MoveNext() ? previous.Current.Key : default!;" : "default!;"));
        if (rawPair && !parsedPrefix)
        {
            // The writer stored the pair as raw native storage (Nullable, KeyValuePair and other framed values included), and
            // the fresh read decodes it the same way; both members replace their complete storage.
            sb.AppendLine($"                        {valueType} item = default!;");
            sb.AppendLine("                        int keyBytes = global::LuminPack.Core.LuminPackReader.Read(ref reader.GetCurrentSpanReference(), out key);");
            sb.AppendLine("                        reader.Advance(keyBytes);");
            if (observeRawLookup)
                sb.AppendLine("                        if (observeLookup) destination.TryGetValue(key, out _);");
            sb.AppendLine("                        int valueBytes = global::LuminPack.Core.LuminPackReader.Read(ref reader.GetCurrentSpanReference(), out item);");
            sb.AppendLine("                        reader.Advance(valueBytes);");
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
        else
            sb.AppendLine("                        reader.ReadValue(ref key!);");
        string lookup = (nonNullableValueKey ? "" : "key is { } && ") + "destination.TryGetValue(key, out var existing) ? existing : default!";
        // Only a callback-free key read can still use the pair observed by MoveNext. Value callbacks run after selection.
        if (enumeratedStringKey)
            lookup = "useEnumeratedValue && key is not null && global::System.Object.ReferenceEquals(key, previousPair.Key) ? previousPair.Value : " + lookup;
        sb.AppendLine($"                        {valueType} item = {lookup};");
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
