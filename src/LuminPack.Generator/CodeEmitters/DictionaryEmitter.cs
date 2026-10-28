using System.Linq;
using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides dictionary emitter functionality.</summary>
public static partial class DictionaryEmitter

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
        AppendJsonDictionaryEntrySerialize(sb, "item.Key", "item.Value", "                    ",
            EmitterTypeTraits.HasJsonEntryWriter(GetFirstGeneric(fieldData.TypeName), GetSecondGeneric(fieldData.TypeName)));
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
        AppendJsonEntryRead(sb, keyType, valueType);
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

    /// <summary>Returns whether a type is a constructed <see cref="System.Nullable{T}"/>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Whether the type is a nullable value type.</returns>
    private static bool IsNullableValue(Microsoft.CodeAnalysis.ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Nullable_T;

    /// <summary>Appends binary writing statements for dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        var dictionaryType = fieldData.TypeSymbol as Microsoft.CodeAnalysis.INamedTypeSymbol;
        bool rawKey = dictionaryType?.TypeArguments.Length == 2 &&
                      EmitterTypeTraits.CanUseUnmanagedSerialization(dictionaryType.TypeArguments[0]);
        bool rawValue = dictionaryType?.TypeArguments.Length == 2 &&
                        EmitterTypeTraits.CanUseUnmanagedSerialization(dictionaryType.TypeArguments[1]);
        bool unmanagedPair = rawKey && rawValue;
        bool directRawKey = !unmanagedPair && rawKey && HasRawEntryContract(dictionaryType.TypeArguments[0]);
        bool directRawValue = !unmanagedPair && rawValue && HasRawEntryContract(dictionaryType.TypeArguments[1]);
        bool localUnionCursor = directRawKey && fieldData.UnionCursorSuffix != null;
        bool stringKey = dictionaryType?.TypeArguments.Length == 2 &&
                         dictionaryType.TypeArguments[0].SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String;
        bool stringValue = dictionaryType?.TypeArguments.Length == 2 &&
                           dictionaryType.TypeArguments[1].SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String;
        bool stableStringHeaders = (stringKey || stringValue) &&
                                   (stringKey || directRawKey) && (stringValue || directRawValue);
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
            sb.AppendLine("                if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() != 8)");
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
            if (localUnionCursor) sb.AppendLine("                int nextIndex = index;");
            if (stableStringHeaders) sb.AppendLine("                int stringRecordLength = writer.StringRecordLength();");
            sb.AppendLine("                ref var entryRef = ref LuminPackMarshal.GetNotNullArrayReference(dictionaryView._entries!);");
            sb.AppendLine("                for (int i = 0; i < count; i++)");
            sb.AppendLine("                {");
            bool boundedEntries = !localUnionCursor && stableStringHeaders;
            if (!stringKey && !boundedEntries)
                sb.AppendLine(localUnionCursor ? "                    nextIndex = writer.CheckBuffer(nextIndex);" : "                    writer.CheckBuffer();");
            sb.AppendLine("                    ref var entry = ref entryRef;");
            if (localUnionCursor)
            {
                sb.AppendLine($"                    global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref writer.GetSpanReference(nextIndex), ref global::System.Runtime.CompilerServices.Unsafe.As<{keyType}, byte>(ref entry.Key), (uint)global::System.Runtime.CompilerServices.Unsafe.SizeOf<{keyType}>());");
                sb.AppendLine($"                    nextIndex += global::System.Runtime.CompilerServices.Unsafe.SizeOf<{keyType}>();");
                sb.AppendLine($"                    {valueType} itemValue = entry.Value;");
                sb.AppendLine("                    if (itemValue is null) { writer.GetSpanReference(nextIndex) = 255; nextIndex++; }");
                // Approved exact fixed-case tests: each runs the body its case override runs; every other runtime type keeps the cursor slot.
                foreach (var (id, caseType) in fieldData.UnionCursorCases ?? new System.Collections.Generic.List<(int Id, string Type)>())
                    sb.AppendLine($"                    else if (itemValue.GetType() == typeof({caseType})) nextIndex = global::LuminPack.LuminPackSerializer.SerializeUnionAt(ref writer, global::System.Runtime.CompilerServices.Unsafe.As<{caseType}>(itemValue), nextIndex, (byte){id});");
                sb.AppendLine($"                    else nextIndex = itemValue.__LuminPackUnionSerializeCursor_{fieldData.UnionCursorSuffix}(ref writer, nextIndex);");
            }
            else if (boundedEntries)
                AppendBoundedEntryWrite(sb, keyType, valueType, stringKey, stringValue);
            else
            {
                AppendDenseBinaryElementWrite(sb, keyType, "Key", "key", directRawKey, stableStringHeaders && stringKey);
                AppendDenseBinaryElementWrite(sb, valueType, "Value", "itemValue", directRawValue, stableStringHeaders && stringValue);
            }
            sb.AppendLine("                    entryRef = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, 1);");
            sb.AppendLine("                }");
            if (localUnionCursor) sb.AppendLine("                writer.CurrentIndex = nextIndex;");
            sb.AppendLine("                if (dictionaryView._version != version || dictionaryView._count != count || dictionaryView._freeCount != 0)");
            sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.CheckBuffer();");
            sb.AppendLine();
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.AppendLine($"            static void WriteSparseDictionary(ref global::LuminPack.Core.LuminPackWriter writer, global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> source)");
            sb.AppendLine("            {");
            if (stableStringHeaders) sb.AppendLine("                int stringRecordLength = writer.StringRecordLength();");
            sb.AppendLine("                foreach (var item in source)");
            sb.AppendLine("                {");
            if (!stringKey) sb.AppendLine("                    writer.CheckBuffer();");
            sb.AppendLine("                    " + keyType + " key = item.Key;");
            AppendBinaryElementWrite(sb, "key", "                    ", stableStringHeaders && stringKey);
            sb.AppendLine("                    " + valueType + " itemValue = item.Value;");
            AppendBinaryElementWrite(sb, "itemValue", "                    ", stableStringHeaders && stringValue);
            sb.AppendLine("                }");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("            }");
            return;
        }
        // The dense copy kernel's unmanaged constraint excludes Nullable<T>, which Roslyn still reports as unmanaged storage.
        if (unmanagedPair && !IsNullableValue(dictionaryType.TypeArguments[0]) && !IsNullableValue(dictionaryType.TypeArguments[1]))
        {
            // CoreCLR's 32-bit Dictionary omits the 64-bit cached FastMod field. The
            // view deliberately models the 64-bit layout, so never read state fields
            // after _entries unless the runtime layout is the 64-bit one.
            sb.AppendLine("            if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
            sb.AppendLine("            {");
            sb.AppendLine("                var dictionaryView = LuminPackMarshal.GetDictionaryView(value);");
            sb.AppendLine("                if (dictionaryView._count == count)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int version = dictionaryView._version;");
            // The dense entries' raw key and value storage is copied by index into the reserved record.
            sb.AppendLine("                    global::LuminPack.Code.LuminPackMarshal.WriteDenseRawPairs(ref writer, value, count);");
            sb.AppendLine("                    if (dictionaryView._version != version || dictionaryView._count != count || dictionaryView._freeCount != 0)");
            sb.AppendLine("                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
        if (unmanagedPair)
        {
            // Sparse dictionaries and 32-bit targets enumerate in an outlined function, so the dense path's frame holds no enumerator.
            sb.AppendLine("            WriteSparseRawDictionary(ref writer, value);");
            sb.AppendLine();
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.AppendLine($"            static void WriteSparseRawDictionary(ref global::LuminPack.Core.LuminPackWriter writer, global::System.Collections.Generic.Dictionary<{keyType}, {valueType}> source)");
            sb.AppendLine("            {");
            sb.AppendLine("                foreach (var item in source)");
            sb.AppendLine("                {");
            AppendRawPair(sb, "item.Key", "item.Value", valueType, "                    ", write: true);
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            return;
        }
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + keyType + " key = item.Key;");
        sb.AppendLine("                writer.WriteValue(in key);");
        sb.AppendLine("                " + valueType + " itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(in itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Checks whether a dictionary writes each entry as the raw native storage of its key followed by its value.</summary>
    /// <param name="dictionaryType">The closed dictionary or dictionary-interface type.</param>
    /// <returns>Whether both members are unmanaged without a Custom or portable contract.</returns>
    /// <remarks>Every reader, reuse path, interface projection and size evaluator of a dictionary must make this same choice, since
    /// Nullable, KeyValuePair and specialized-mode members are framed differently by their own formatters.</remarks>
    internal static bool UsesRawPair(Microsoft.CodeAnalysis.ITypeSymbol dictionaryType)
        => dictionaryType is Microsoft.CodeAnalysis.INamedTypeSymbol { TypeArguments.Length: 2 } named &&
           EmitterTypeTraits.CanUseUnmanagedSerialization(named.TypeArguments[0]) &&
           EmitterTypeTraits.CanUseUnmanagedSerialization(named.TypeArguments[1]);

    /// <summary>Reads or writes a packed raw entry using one current reference and one cursor advance.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="key">The raw key storage expression.</param>
    /// <param name="value">The raw value storage expression.</param>
    /// <param name="valueType">The complete value storage type.</param>
    /// <param name="indent">The current statement indentation.</param>
    /// <param name="write">Whether the entry is an output rather than an input.</param>
    internal static void AppendRawPair(StringBuilder sb, string key, string value, string valueType, string indent, bool write)
    {
        string state = write ? "writer" : "reader", operation = write ? "Write" : "Read", argument = write ? "" : "out ";
        string core = "global::LuminPack.Core.LuminPack" + (operation == "Write" ? "Writer" : "Reader") + "." + operation;
        sb.AppendLine(indent + "ref byte entryCursor = ref " + state + ".GetCurrentSpanReference();");
        sb.AppendLine(indent + "int keyBytes = " + core + "(ref entryCursor, " + argument + key + ");");
        sb.AppendLine(indent + core + "(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryCursor, keyBytes), " + argument + value + ");");
        sb.AppendLine(indent + state + ".Advance(keyBytes + global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + valueType + ">());");
    }

    /// <summary>Excludes specialized root framing from the direct mixed-entry storage optimization.</summary>
    /// <param name="type">A member already proven unmanaged and free of nested Custom contracts.</param>
    /// <returns>Whether its selected root operation uses ordinary raw storage.</returns>
    private static bool HasRawEntryContract(Microsoft.CodeAnalysis.ITypeSymbol type)
        => EmitterTypeTraits.FormatterIsRawStorage(type);

    /// <summary>Copies a proven raw entry member directly or evaluates its selected formatter in order.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="type">The concrete member type.</param>
    /// <param name="member">The entry field name.</param>
    /// <param name="local">The evaluated formatter argument name.</param>
    /// <param name="raw">Whether the member's complete contract is unmanaged storage.</param>
    /// <param name="cachedStringHeader">Whether the selected string framing is stable.</param>
    private static void AppendDenseBinaryElementWrite(StringBuilder sb, string type, string member, string local,
        bool raw, bool cachedStringHeader)
    {
        const string indent = "                    ";
        if (raw)
        {
            // This helper is emitted only inside the dense 64-bit storage-view branch.
            // Copy each member separately: entry metadata and inter-member gaps are not wire data.
            sb.AppendLine(indent + "global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref writer.GetCurrentSpanReference(),");
            sb.AppendLine(indent + $"    ref global::System.Runtime.CompilerServices.Unsafe.As<{type}, byte>(ref entry.{member}), (uint)global::System.Runtime.CompilerServices.Unsafe.SizeOf<{type}>());");
            sb.AppendLine(indent + $"writer.Advance(global::System.Runtime.CompilerServices.Unsafe.SizeOf<{type}>());");
            return;
        }
        sb.AppendLine(indent + type + " " + local + " = entry." + member + ";");
        AppendBinaryElementWrite(sb, local, indent, cachedStringHeader);
    }

    /// <summary>Writes one dense entry of raw and string members after a single room check.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="keyType">The key type.</param>
    /// <param name="valueType">The value type.</param>
    /// <param name="stringKey">Whether the key is a string; otherwise it is raw storage.</param>
    /// <param name="stringValue">Whether the value is a string; otherwise it is raw storage.</param>
    /// <remarks>Under the UTF-16 length profile the entry's raw-form bound comes from one read of each string. When the room the
    /// writer reports for an unchecked record covers it, the collection threshold cannot act and both members are stored into
    /// that room; otherwise the buffer check and the member writes run in their original order. Other profiles keep that order.</remarks>
    private static void AppendBoundedEntryWrite(StringBuilder sb, string keyType, string valueType, bool stringKey, bool stringValue)
    {
        const string indent = "                    ";
        const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        string Bound(bool text, string type, string local) => text
            ? "global::LuminPack.Core.LuminPackWriter.Utf16RecordBound(" + local + ")"
            : unsafeType + ".SizeOf<" + type + ">()";
        string Raw(string type, string member, string at) =>
            "global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref " + at + ", ref " + unsafeType + ".As<" + type + ", byte>(ref entry." + member + "), (uint)" + unsafeType + ".SizeOf<" + type + ">());";
        if (stringKey) sb.AppendLine(indent + keyType + " key = entry.Key;");
        if (stringValue) sb.AppendLine(indent + valueType + " itemValue = entry.Value;");
        sb.AppendLine(indent + "if (writer.UsesUtf16LengthRecords() && writer.RecordRoom() >= (long)" + Bound(stringKey, keyType, "key") + " + " + Bound(stringValue, valueType, "itemValue") + ")");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    ref byte destination = ref writer.GetCurrentSpanReference();");
        if (stringKey)
            sb.AppendLine(indent + "    int written = global::LuminPack.Core.LuminPackWriter.WriteReservedUtf16Record(ref destination, key);");
        else
        {
            sb.AppendLine(indent + "    " + Raw(keyType, "Key", "destination"));
            sb.AppendLine(indent + "    int written = " + unsafeType + ".SizeOf<" + keyType + ">();");
        }
        if (stringValue)
            sb.AppendLine(indent + "    written += global::LuminPack.Core.LuminPackWriter.WriteReservedUtf16Record(ref " + unsafeType + ".Add(ref destination, written), itemValue);");
        else
        {
            sb.AppendLine(indent + "    " + Raw(valueType, "Value", unsafeType + ".Add(ref destination, written)"));
            sb.AppendLine(indent + "    written += " + unsafeType + ".SizeOf<" + valueType + ">();");
        }
        sb.AppendLine(indent + "    writer.CompleteRun(written);");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "else");
        sb.AppendLine(indent + "{");
        // The original order: the collection buffer check, then each member's own write.
        var original = new StringBuilder();
        if (!stringKey) original.AppendLine(indent + "writer.CheckBuffer();");
        if (stringKey) AppendBinaryElementWrite(original, "key", indent, true);
        else AppendDenseBinaryElementWrite(original, keyType, "Key", "key", true, false);
        if (stringValue) AppendBinaryElementWrite(original, "itemValue", indent, true);
        else AppendDenseBinaryElementWrite(original, valueType, "Value", "itemValue", true, false);
        foreach (var line in original.ToString().Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None))
            if (line.Length != 0) sb.AppendLine("    " + line);
        sb.AppendLine(indent + "}");
    }

    /// <summary>Reuses the selected string formatter when a dictionary has no state-changing element operation.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="value">The evaluated element local.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="cachedStringHeader">Whether string recording mode is stable throughout this loop.</param>
    private static void AppendBinaryElementWrite(StringBuilder sb, string value, string indent, bool cachedStringHeader)
    {
        if (!cachedStringHeader)
        {
            sb.AppendLine(indent + "writer.WriteValue(in " + value + ");");
            return;
        }
        sb.AppendLine(indent + "{");
        StringEmitter.AppendSerializeCode(sb, value, "stringRecordLength", indent + "    ");
        sb.AppendLine(indent + "}");
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
                             EmitterTypeTraits.CanUseUnmanagedSerialization(dictionaryType.TypeArguments[0]) &&
                             EmitterTypeTraits.CanUseUnmanagedSerialization(dictionaryType.TypeArguments[1]);

        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        if (unmanagedPair)
        {
            sb.AppendLine("            int inputLength = reader.GetSpan().Length;");
        }
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
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
        if (unmanagedPair)
        {
            sb.AppendLine($"            int payloadLength = checked(length * (global::System.Runtime.CompilerServices.Unsafe.SizeOf<{keyType}>() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<{valueType}>()));");
            sb.AppendLine("            if ((uint)payloadLength > (uint)(inputLength - index))");
            sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + payloadLength));");
        }
        // Raw pairs rebuild the buckets in incoming order, ahead of the scalar slot refill (owner-approved 2026-10-27).
        if (!freshValue && unmanagedPair)
            AppendRawDictionaryRefill(sb, keyType, valueType);
        if (!freshValue)
            AppendReusableDictionaryRead(sb, keyType, valueType,
                fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol collection &&
                collection.TypeArguments[0].IsValueType &&
                collection.TypeArguments[0].OriginalDefinition.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_Nullable_T,
                (fieldData.TypeSymbol as Microsoft.CodeAnalysis.INamedTypeSymbol)?.TypeArguments[1], fieldData.CanReuseDictionarySlots, fieldData.UnionSlotCases, fieldData.CanReuseDictionaryScalarSlots,
                unmanagedPair, fieldData.CanReadStableStringKeys);
        sb.AppendLine("            if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() != 8)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var fallback = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        if (unmanagedPair)
        {
            sb.AppendLine($"                    {keyType} key;");
            sb.AppendLine($"                    {valueType} itemValue;");
            AppendRawPair(sb, "key", "itemValue", valueType, "                    ", write: false);
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
        sb.AppendLine("            var entries = dictionaryView._entries!;");
        if (unmanagedPair)
            sb.AppendLine("            ref var entryRef = ref LuminPackMarshal.GetNotNullArrayReference(entries);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        if (unmanagedPair)
        {
            sb.AppendLine("                ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, (nint)(uint)i);");

            AppendRawPair(sb, "entry.Key", "entry.Value", valueType, "                ", write: false);
        }
        else
        {
            // A fresh entry starts with default values. Read into local roots so
            // expanded Union arms do not keep a heap destination byref alive.
            sb.AppendLine($"                {keyType} key = default!;");
            sb.AppendLine($"                {valueType} itemValue = default!;");
            sb.AppendLine("                reader.ReadValue(ref key!);");
            sb.AppendLine("                reader.ReadValue(ref itemValue!);");
            sb.AppendLine("                ref var entry = ref entries[i];");
            sb.AppendLine("                entry.Key = key;");
            sb.AppendLine("                entry.Value = itemValue;");
        }
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (!LuminPackMarshal.TryRebuildFreshDictionaryBuckets(dictionaryView, length))");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var fallbackEntries = ref LuminPackMarshal.GetNotNullArrayReference(entries);");
        sb.AppendLine($"                var fallback = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref fallbackEntries, (nint)(uint)i);");
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
        bool fused = EmitterTypeTraits.HasJsonEntryWriter(keyType, valueType);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    nuint dictIndex = 0;");
        sb.AppendLine("                    var dictView = LuminPackMarshal.GetDictionaryView(value);");
        sb.AppendLine("                    ref var arrayRef = ref LuminPackMarshal.GetArrayReference(dictView._entries);");
        sb.AppendLine("                    while (dictIndex < (uint)dictView._count)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref arrayRef, dictIndex++);");
        sb.AppendLine("                        if (LuminPackMarshal.IsHashTableEntryActive(entry.HashCode, entry.Next))");
        sb.AppendLine("                        {");
        AppendJsonDictionaryEntrySerialize(sb, "entry.Key", "entry.Value", "                            ", fused);
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    foreach (var entry in value)");
        sb.AppendLine("                    {");
        AppendJsonDictionaryEntrySerialize(sb, "entry.Key", "entry.Value", "                        ", fused);
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
    /// <param name="fused">Whether the scalar key and value use the fused entry writers.</param>
    private static void AppendJsonDictionaryEntrySerialize(
        StringBuilder sb,
        string keyExpression,
        string valueExpression,
        string indent,
        bool fused)
    {
        if (fused)
        {
            // The pair array, its separators and both scalars use two capacity reservations instead of six.
            sb.AppendLine(indent + "writer.WriteEntryKey(" + keyExpression + ");");
            sb.AppendLine(indent + "writer.WriteEntryValue(" + valueExpression + ");");
            return;
        }

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
        AppendJsonEntryRead(sb, keyType, valueType);
        sb.AppendLine("                value[key] = itemValue;");
        sb.AppendLine("            }");
    }
    /// <summary>Appends the token reads of one dictionary entry array after its opening bracket.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="keyType">The generated key type name.</param>
    /// <param name="valueType">The generated value type name.</param>
    /// <remarks>Scalar entries first try the fused trusted UTF-8 reader, which consumes nothing when it declines.</remarks>
    internal static void AppendJsonEntryRead(StringBuilder sb, string keyType, string valueType)
    {
        string indent = "                ";
        if (EmitterTypeTraits.HasJsonEntryWriter(keyType, valueType))
        {
            sb.AppendLine(indent + "if (!reader.TryReadEntry(ref key, ref itemValue))");
            sb.AppendLine(indent + "{");
            indent += "    ";
        }
        sb.AppendLine(indent + "if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    reader.ReadValue(ref key);");
        sb.AppendLine(indent + "    if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine(indent + "        reader.ReadValue(ref itemValue);");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "while (reader.Read())");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine(indent + "        break;");
        // Read classifies a string or number without consuming it; skipping each extra element keeps the drain finite.
        sb.AppendLine(indent + "    reader.Skip();");
        sb.AppendLine(indent + "}");
        if (indent.Length > 16)
            sb.AppendLine("                }");
    }
}
