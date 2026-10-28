using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides sorted list emitter functionality.</summary>
public static class SortedListEmitter
{
    /// <summary>Appends binary writing statements for sorted list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>Copies the parallel key and value arrays with two array copies instead of enumerating boxed pairs.</remarks>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        sb.AppendLine("            int count = value.Count;");
        sb.AppendLine("            writer.EnsureAdditionalCapacity(4);");
        sb.AppendLine("            writer.Write(count);");
        // The list's own sorted key and value arrays are written in place; unrecognized storage copies through the public lists.
        sb.AppendLine("            if (count != 0 && global::LuminPack.Code.LuminPackCollectionAccess.TryGetSortedListStorage(value, out var storedKeys, out var storedValues))");
        sb.AppendLine("            {");
        var arguments = (fieldData.TypeSymbol as Microsoft.CodeAnalysis.INamedTypeSymbol)?.TypeArguments;
        bool strings = arguments is { Length: 2 } && arguments.Value[0].SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String &&
            arguments.Value[1].SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String;
        if (strings)
        {
            // String pairs keep the per-pair check and record order inside the writer's batched kernel.
            sb.AppendLine("                writer.WriteStringPairs(new global::System.ReadOnlySpan<string?>(storedKeys, 0, count), new global::System.ReadOnlySpan<string?>(storedValues, 0, count));");
        }
        else
        {
            sb.AppendLine("                for (int i = 0; i < count; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    writer.CheckBuffer();");
            sb.AppendLine("                    writer.WriteValue(storedKeys![i]);");
            sb.AppendLine("                    writer.WriteValue(storedValues![i]);");
            sb.AppendLine("                }");
        }
        sb.AppendLine("            }");
        sb.AppendLine("            else if (count != 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                var keys = global::System.Buffers.ArrayPool<" + keyType + ">.Shared.Rent(count);");
        sb.AppendLine("                var values = global::System.Buffers.ArrayPool<" + valueType + ">.Shared.Rent(count);");
        sb.AppendLine("                value.Keys.CopyTo(keys, 0);");
        sb.AppendLine("                value.Values.CopyTo(values, 0);");
        if (strings)
        {
            sb.AppendLine("                writer.WriteStringPairs(new global::System.ReadOnlySpan<string?>(keys, 0, count), new global::System.ReadOnlySpan<string?>(values, 0, count));");
        }
        else
        {
            sb.AppendLine("                for (int i = 0; i < count; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    writer.CheckBuffer();");
            sb.AppendLine("                    writer.WriteValue(keys[i]);");
            sb.AppendLine("                    writer.WriteValue(values[i]);");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                global::System.Buffers.ArrayPool<" + keyType + ">.Shared.Return(keys, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + keyType + ">());");
        sb.AppendLine("                global::System.Buffers.ArrayPool<" + valueType + ">.Shared.Return(values, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + valueType + ">());");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for sorted list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <remarks>A fresh list retains bulk construction and pooled cleanup. Reuse keeps its instance and comparer; string-keyed reuse reads every pair first and stores ordered keys in bulk, other reuse appends per item with a certified ordered append when available and the original Add otherwise.</remarks>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (!reader.TryReadCollectionHead(out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + ")) { value = null; return; }");
        var element = (fieldData.TypeSymbol as Microsoft.CodeAnalysis.INamedTypeSymbol)?.TypeArguments[1];
        var key = (fieldData.TypeSymbol as Microsoft.CodeAnalysis.INamedTypeSymbol)?.TypeArguments[0];
        bool stringKey = key?.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String;
        // Framework scalar keys have no comparison callbacks and compare equal exactly when their prior value may be reused.
        bool scalarKey = key is not null && (key.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum ||
            key.SpecialType is >= Microsoft.CodeAnalysis.SpecialType.System_Boolean and <= Microsoft.CodeAnalysis.SpecialType.System_Double);
        if ((stringKey || scalarKey) &&
            (element?.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String || EmitterTypeTraits.UsesOrdinaryObjectHeader(element)))
            AppendMatchingPairRead(sb, keyType, stringKey, valueType, element);
        sb.AppendLine("            if (value is null && length > 1)");
        sb.AppendLine("            {");
        sb.AppendLine("                var keys = global::System.Buffers.ArrayPool<" + keyType + ">.Shared.Rent(length);");
        sb.AppendLine("                " + valueType + "[]? values = null;");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    values = global::System.Buffers.ArrayPool<" + valueType + ">.Shared.Rent(length);");
        sb.AppendLine("                    for (int i = 0; i < length; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        " + keyType + " k = default!;");
        sb.AppendLine("                        " + valueType + " v = default!;");
        sb.AppendLine("                        reader.ReadValue(ref k);");
        sb.AppendLine("                        reader.ReadValue(ref v);");
        var keySymbol = (fieldData.TypeSymbol as Microsoft.CodeAnalysis.INamedTypeSymbol)?.TypeArguments[0];
        if (keySymbol?.IsReferenceType == true || keySymbol is Microsoft.CodeAnalysis.ITypeParameterSymbol ||
            keySymbol is Microsoft.CodeAnalysis.INamedTypeSymbol { OriginalDefinition.SpecialType: Microsoft.CodeAnalysis.SpecialType.System_Nullable_T })
            sb.AppendLine("                        if (k is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(\"key\");");
        sb.AppendLine("                        keys[i] = k!;");
        sb.AppendLine("                        values[i] = v;");
        sb.AppendLine("                    }");
        // Keys already in order are stored directly; other input uses the bulk construction, which sorts and rejects duplicates.
        sb.AppendLine("                    value = new global::System.Collections.Generic.SortedList<" + keyType + ", " + valueType + ">(length);");
        sb.AppendLine("                    if (!global::LuminPack.Code.LuminPackCollectionAccess.TryFillSortedList(value, keys, values, length))");
        sb.AppendLine("                        value = new global::System.Collections.Generic.SortedList<" + keyType + ", " + valueType + ">(new global::LuminPack.Code.LuminSortedListSource<" + keyType + ", " + valueType + ">(keys, values, length));");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::System.Buffers.ArrayPool<" + keyType + ">.Shared.Return(keys, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + keyType + ">());");
        sb.AppendLine("                    if (values is not null) global::System.Buffers.ArrayPool<" + valueType + ">.Shared.Return(values, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + valueType + ">());");
        sb.AppendLine("                }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (value is null) value = new global::System.Collections.Generic.SortedList<" + keyType + ", " + valueType + ">(length);");
        sb.AppendLine("            else value.Clear();");
        bool orderedStringKey = fieldData.CanAppendSortedListOrdered;
        if (orderedStringKey)
        {
            sb.AppendLine("            var appendDestination = value;");
            sb.AppendLine("            var appendCulture = global::System.Globalization.CultureInfo.CurrentCulture;");
            sb.AppendLine("            bool canAppendOrdered = global::LuminPack.Code.LuminPackCollectionAccess.CanAppendSortedListOrdered(appendDestination);");
        }
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + keyType + " k = default!;");
        sb.AppendLine("                " + valueType + " v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
        if (orderedStringKey)
            sb.AppendLine("                if (!canAppendOrdered || !global::System.Object.ReferenceEquals(value, appendDestination) || !global::System.Object.ReferenceEquals(global::System.Globalization.CultureInfo.CurrentCulture, appendCulture) || !global::LuminPack.Code.LuminPackCollectionAccess.TryAppendSortedListOrdered(value, k!, v, true)) value.Add(k!, v);");
        else
            sb.AppendLine("                value.Add(k!, v);");
        sb.AppendLine("            }");
    }

    /// <summary>Reuses prior values only when the same-position key matches: ordinally for strings, exactly for scalars.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="keyType">The string or framework scalar key spelling.</param>
    /// <param name="stringKey">Whether the key is a string, whose prior storage is also a read candidate.</param>
    /// <param name="valueType">The string or ordinary object value spelling.</param>
    /// <param name="element">The canonical value formatter contract.</param>
    private static void AppendMatchingPairRead(StringBuilder sb, string keyType, bool stringKey, string valueType, Microsoft.CodeAnalysis.ITypeSymbol element)
    {
        string list = "global::System.Collections.Generic.SortedList<" + keyType + ", " + valueType + ">";
        sb.AppendLine("            if (value is not null && value.GetType() == typeof(" + list + ") && length != 0 && value.Count != 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                ReadMatchingPairs(ref reader, ref value, length);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static void ReadMatchingPairs(ref global::LuminPack.Core.LuminPackReader reader, ref " + list + " value, int length)");
        sb.AppendLine("            {");
        // The original Clear/Add publication is kept: the priors are staged in the pooled pair arrays, the list is cleared,
        // and every pair is read before any is added. A failure adds the completed prefix, as the original insertion
        // left it; a list still empty afterwards takes keys already in order with one comparison per pair.
        sb.AppendLine("                int __reuseCount = global::System.Math.Min(length, value.Count);");
        sb.AppendLine("                var keys = global::System.Buffers.ArrayPool<" + keyType + ">.Shared.Rent(length);");
        sb.AppendLine("                " + valueType + "[]? values = null;");
        sb.AppendLine("                int __completed = 0;");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    values = global::System.Buffers.ArrayPool<" + valueType + ">.Shared.Rent(length);");
        sb.AppendLine("                    if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetSortedListStorage(value, out var __priorKeys, out var __priorValues))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        global::System.Array.Copy(__priorKeys!, keys, __reuseCount);");
        sb.AppendLine("                        global::System.Array.Copy(__priorValues!, values, __reuseCount);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var __keyList = value.Keys;");
        sb.AppendLine("                        var __valueList = value.Values;");
        sb.AppendLine("                        for (int index = 0; index < __reuseCount; index++) { keys[index] = __keyList[index]; values[index] = __valueList[index]; }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    value.Clear();");
        sb.AppendLine("                    for (int i = 0; i < length; i++)");
        sb.AppendLine("                    {");
        if (stringKey)
        {
            sb.AppendLine("                        string? priorKey = i < __reuseCount ? keys[i] : null;");
            sb.AppendLine("                        string key = priorKey!;");
            sb.AppendLine("                        reader.ReadValue(ref key);");
            sb.AppendLine("                        " + valueType + " item = priorKey is not null && global::System.String.Equals(key, priorKey, global::System.StringComparison.Ordinal) ? values[i] : default!;");
        }
        else
        {
            sb.AppendLine("                        " + keyType + " key = default;");
            sb.AppendLine("                        reader.ReadValue(ref key);");
            sb.AppendLine("                        " + valueType + " item = i < __reuseCount && global::System.Collections.Generic.EqualityComparer<" + keyType + ">.Default.Equals(key, keys[i]) ? values[i] : default!;");
        }
        EmitterTypeTraits.AppendReusableElementRead(sb, element, "item", "                        ");
        if (stringKey)
            sb.AppendLine("                        if (key is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(\"key\");");
        sb.AppendLine("                        keys[i] = key;");
        sb.AppendLine("                        values[i] = item;");
        sb.AppendLine("                        __completed = i + 1;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    __completed = -1;");
        sb.AppendLine("                    if (!global::LuminPack.Code.LuminPackCollectionAccess.TryFillSortedList(value, keys, values, length))");
        sb.AppendLine("                        for (int i = 0; i < length; i++) value.Add(keys[i], values[i]);");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (global::System.Exception __error) when (__completed >= 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    for (int i = 0; i < __completed; i++) value.Add(keys[i], values![i]);");
        sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.Rethrow(__error);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::System.Buffers.ArrayPool<" + keyType + ">.Shared.Return(keys, true);");
        sb.AppendLine("                    if (values is not null) global::System.Buffers.ArrayPool<" + valueType + ">.Shared.Return(values, true);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }
}
