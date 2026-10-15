using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides dictionary emitter functionality.</summary>
public static class DictionaryEmitter

{
    /// <summary>Appends JSON writing statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDictionarySerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteArrayStart();");
        sb.AppendLine("                    var key = item.Key;");
        sb.AppendLine("                    writer.WriteValue(in key);");
        sb.AppendLine("                    var itemValue = item.Value;");
        sb.AppendLine("                    writer.WriteValue(in itemValue);");
        sb.AppendLine("                    writer.WriteArrayEnd();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDictionaryDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string type = fieldData.TypeName;
        string baseType = GetDictionaryBaseType(type);
        string keyType = GetFirstGeneric(type);
        string valueType = GetSecondGeneric(type);
        bool interfaceType = baseType is "global::System.Collections.Generic.IDictionary" or "global::System.Collections.Generic.IReadOnlyDictionary";

        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (interfaceType)
        {
            sb.AppendLine("            var dictionary = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">();");
        }
        else
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("                value = new " + type + "();");
            sb.AppendLine("            else");
            sb.AppendLine("                value.Clear();");
        }
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.ReadValue(ref key);");
        sb.AppendLine("                    if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        reader.ReadValue(ref itemValue);");
        sb.AppendLine("                }");
        sb.AppendLine("                while (reader.Read())");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        if (interfaceType)
        {
            sb.AppendLine("                dictionary.Add(key, itemValue);");
        }
        else if (baseType == "global::System.Collections.Concurrent.ConcurrentDictionary")
        {
            sb.AppendLine("                value.TryAdd(key, itemValue);");
        }
        else
        {
            sb.AppendLine("                value.Add(key, itemValue);");
        }
        sb.AppendLine("            }");
        if (interfaceType)
        {
            sb.AppendLine("            value = dictionary;");
        }
    }

    /// <summary>Obtains the dictionary base type for the supplied input.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetDictionaryBaseType(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }

    /// <summary>Appends binary writing statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        bool unmanagedPair = fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol dictionaryType &&
                             dictionaryType.TypeArguments.Length == 2 &&
                             dictionaryType.TypeArguments[0].IsUnmanagedType &&
                             dictionaryType.TypeArguments[1].IsUnmanagedType;
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            int count = value.Count;");
        if (unmanagedPair)
        {
            sb.AppendLine($"            writer.EnsureAdditionalCapacity(checked(sizeof(int) + count * (global::System.Runtime.CompilerServices.Unsafe.SizeOf<{keyType}>() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<{valueType}>())));");
        }
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine("            writer.Advance(4);");
        if (!unmanagedPair)
        {
            sb.AppendLine("            if (count != 0)");
            sb.AppendLine("            {");
            // DictionaryView models the modern 64-bit runtime layout. Keep the
            // public enumerator fallback entirely outside the view path on x86.
            sb.AppendLine("                if (global::System.IntPtr.Size != 8)");
            sb.AppendLine("                {");
            sb.AppendLine("                    WriteSparseDictionary(ref writer, value);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                var dictionaryView = LuminPackMarshal.GetDictionaryView(value);");
            sb.AppendLine("                if (dictionaryView._count != count)");
            sb.AppendLine("                {");
            sb.AppendLine("                    WriteSparseDictionary(ref writer, value);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                int version = dictionaryView._version;");
            sb.AppendLine("                ref var entryRef = ref LuminPackMarshal.GetArrayReference(dictionaryView._entries);");
            sb.AppendLine("                for (nint i = 0; i < count; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, i);");
            sb.AppendLine("                    " + keyType + " key = entry.Key;");
            sb.AppendLine("                    writer.WriteValue(in key);");
            sb.AppendLine("                    " + valueType + " itemValue = entry.Value;");
            sb.AppendLine("                    writer.WriteValue(in itemValue);");
            sb.AppendLine("                }");
            sb.AppendLine("                if (dictionaryView._version != version || dictionaryView._count != count || dictionaryView._freeCount != 0)");
            sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.CheckBuffer();");
            sb.AppendLine();
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.AppendLine($"            static void WriteSparseDictionary(ref global::LuminPack.Core.LuminPackWriter writer, global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> source)");
            sb.AppendLine("            {");
            sb.AppendLine("                foreach (var item in source)");
            sb.AppendLine("                {");
            sb.AppendLine("                    " + keyType + " key = item.Key;");
            sb.AppendLine("                    writer.WriteValue(in key);");
            sb.AppendLine("                    " + valueType + " itemValue = item.Value;");
            sb.AppendLine("                    writer.WriteValue(in itemValue);");
            sb.AppendLine("                }");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("            }");
            return;
        }
        if (unmanagedPair)
        {
            // CoreCLR's 32-bit Dictionary omits the 64-bit cached FastMod field. The
            // view deliberately models the 64-bit layout, so never read state fields
            // after _entries unless the runtime layout is the 64-bit one.
            sb.AppendLine("            if (global::System.IntPtr.Size == 8)");
            sb.AppendLine("            {");
            sb.AppendLine("                var dictionaryView = LuminPackMarshal.GetDictionaryView(value);");
            sb.AppendLine("                if (dictionaryView._count == count)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int version = dictionaryView._version;");
            sb.AppendLine("                    ref var entryRef = ref LuminPackMarshal.GetArrayReference(dictionaryView._entries);");
            sb.AppendLine("                    for (nint i = 0; i < count; i++)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, i);");
            sb.AppendLine("                        int entryOffset = index;");
            sb.AppendLine("                        entryOffset += writer.WriteUnmanaged(ref entryOffset, entry.Key);");
            sb.AppendLine("                        entryOffset += writer.WriteUnmanaged(ref entryOffset, entry.Value);");
            sb.AppendLine("                        writer.Advance(entryOffset - index);");
            sb.AppendLine("                    }");
            sb.AppendLine("                    if (dictionaryView._version != version || dictionaryView._count != count || dictionaryView._freeCount != 0)");
            sb.AppendLine("                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        if (unmanagedPair)
        {
            sb.AppendLine("                int entryOffset = index;");
            sb.AppendLine("                entryOffset += writer.WriteUnmanaged(ref entryOffset, item.Key);");
            sb.AppendLine("                entryOffset += writer.WriteUnmanaged(ref entryOffset, item.Value);");
            sb.AppendLine("                writer.Advance(entryOffset - index);");
        }
        else
        {
            sb.AppendLine("                " + keyType + " key = item.Key;");
            sb.AppendLine("                writer.WriteValue(in key);");
            sb.AppendLine("                " + valueType + " itemValue = item.Value;");
            sb.AppendLine("                writer.WriteValue(in itemValue);");
        }
        sb.AppendLine("            }");
        if (!unmanagedPair)
        {
            sb.AppendLine("            writer.CheckBuffer();");
        }
    }

    /// <summary>Appends binary reading statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: false);
    }

    /// <summary>Appends binary reading statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateFreshDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: true);
    }

    /// <summary>Appends binary reading statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="freshValue">The fresh value used by this operation.</param>
    private static void GenerateDeserializeCodeCore(
        LuminLocalFieldData fieldData, StringBuilder sb, bool freshValue)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        bool unmanagedPair = fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol dictionaryType &&
                             dictionaryType.TypeArguments.Length == 2 &&
                             dictionaryType.TypeArguments[0].IsUnmanagedType &&
                             dictionaryType.TypeArguments[1].IsUnmanagedType;

        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            if (length == 0)");
        sb.AppendLine("            {");
        if (freshValue)
        {
            sb.AppendLine($"                value = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(0);");
        }
        else
        {
            sb.AppendLine($"                value ??= new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(0);");
            sb.AppendLine("                value.Clear();");
        }
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (global::System.IntPtr.Size != 8)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var fallback = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        if (unmanagedPair)
        {
            sb.AppendLine($"                    {keyType} key;");
            sb.AppendLine($"                    {valueType} itemValue;");
            sb.AppendLine("                    int entryOffset = index;");
            sb.AppendLine("                    entryOffset += reader.ReadUnmanaged(ref entryOffset, out key);");
            sb.AppendLine("                    entryOffset += reader.ReadUnmanaged(ref entryOffset, out itemValue);");
            sb.AppendLine("                    reader.Advance(entryOffset - index);");
        }
        else
        {
            sb.AppendLine($"                    {keyType} key = default!;");
            sb.AppendLine($"                    {valueType} itemValue = default!;");
            sb.AppendLine("                    reader.ReadValue(ref key!);");
            sb.AppendLine("                    reader.ReadValue(ref itemValue!);");
        }
        sb.AppendLine("                    fallback.Add(key, itemValue);");
        sb.AppendLine("                }");
        sb.AppendLine("                value = fallback;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var result = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine("            var dictionaryView = LuminPackMarshal.GetDictionaryView(result);");
        sb.AppendLine("            ref var entryRef = ref LuminPackMarshal.GetArrayReference(dictionaryView._entries);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, (nint)(uint)i);");
        if (unmanagedPair)
        {
            sb.AppendLine("                int entryOffset = index;");
            sb.AppendLine("                entryOffset += reader.ReadUnmanaged(ref entryOffset, out entry.Key);");
            sb.AppendLine("                entryOffset += reader.ReadUnmanaged(ref entryOffset, out entry.Value);");
            sb.AppendLine("                reader.Advance(entryOffset - index);");
        }
        else
        {
            sb.AppendLine("                reader.ReadValue(ref entry.Key!);");
            sb.AppendLine("                reader.ReadValue(ref entry.Value!);");
        }
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (!LuminPackMarshal.TryRebuildFreshDictionaryBuckets(dictionaryView, length))");
        sb.AppendLine("            {");
        sb.AppendLine($"                var fallback = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, (nint)(uint)i);");
        sb.AppendLine("                    fallback.Add(entry.Key, entry.Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                result = fallback;");
        sb.AppendLine("            }");
        sb.AppendLine("            value = result;");
    }

    /// <summary>Appends JSON writing statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (global::System.IntPtr.Size == 8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    nuint dictIndex = 0;");
        sb.AppendLine("                    var dictView = LuminPackMarshal.GetDictionaryView(value);");
        sb.AppendLine("                    ref var arrayRef = ref LuminPackMarshal.GetArrayReference(dictView._entries);");
        sb.AppendLine("                    while (dictIndex < (uint)dictView._count)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref arrayRef, dictIndex++);");
        sb.AppendLine("                        if (LuminPackMarshal.IsHashTableEntryActive(entry.HashCode, entry.Next))");
        sb.AppendLine("                        {");
        AppendJsonDictionaryEntrySerialize(sb, "entry.Key", "entry.Value", "                            ");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    foreach (var entry in value)");
        sb.AppendLine("                    {");
        AppendJsonDictionaryEntrySerialize(sb, "entry.Key", "entry.Value", "                        ");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON writing statements for dictionary values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="keyExpression">The key expression used by this operation.</param>
    /// <param name="valueExpression">The value expression used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void AppendJsonDictionaryEntrySerialize(
        StringBuilder sb,
        string keyExpression,
        string valueExpression,
        string indent)
    {
        sb.AppendLine(indent + "writer.WriteArrayStart();");
        sb.AppendLine(indent + "var key = " + keyExpression + ";");
        sb.AppendLine(indent + "writer.WriteValue(in key);");
        sb.AppendLine(indent + "var itemValue = " + valueExpression + ";");
        sb.AppendLine(indent + "writer.WriteValue(in itemValue);");
        sb.AppendLine(indent + "writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("                value = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">();");
        sb.AppendLine("            else");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.ReadValue(ref key);");
        sb.AppendLine("                    if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        reader.ReadValue(ref itemValue);");
        sb.AppendLine("                }");
        sb.AppendLine("                while (reader.Read())");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        sb.AppendLine("                value[key] = itemValue;");
        sb.AppendLine("            }");
    }
}
