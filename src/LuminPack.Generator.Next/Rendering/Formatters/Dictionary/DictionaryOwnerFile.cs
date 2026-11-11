using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
/// <remarks>The key and value are each a raw scalar or a string. The wire is the entry count followed by each key and its value;
/// two raw scalars store packed raw pairs. The binary write copies the dense entry storage when the runtime layout is the 64-bit
/// one and the dictionary has no removed entries, and enumerates otherwise. The binary read reuses a non-null destination
/// through a staged refill, or builds a fresh dictionary directly in its entry storage and rebuilds its buckets, falling back to
/// <c>Add</c>. The JSON form is an array of key and value arrays.</remarks>
internal static class DictionaryOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations)
    {
        var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        const string writer = "LuminPackWriter";
        const string reader = "LuminPackReader";
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string code = w.A("LuminPackCode");
        string memory = w.A("Unsafe");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        string key = fact.Key, value = fact.Value;
        bool rawKey = key != "string", rawValue = value != "string", pair = rawKey && rawValue;
        string dictionary = fact.DeclaredType;
        string type = ReadableTypeName.Of(GeneratedNames.PayloadKey(dictionary));
        string roots = "LuminRoots_" + type;
        string owner = "Binary_" + extension + "_" + type;
        string keyOwner = "global::LuminPack." + GeneratedNames.Owner(extension, key);
        string valueOwner = "global::LuminPack." + GeneratedNames.Owner(extension, value);
        string entry = "global::System.Collections.Generic.KeyValuePair<" + key + ", " + value + ">";
        string minimum = (rawKey ? memory + ".SizeOf<" + key + ">()" : "1") + " + " + (rawValue ? memory + ".SizeOf<" + value + ">()" : "1");
        const string generic = "    where TLuminProfile : struct, ";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        // Writes one entry member to the isolated operation state.
        void WriteMember(string state, string member, string expression)
        {
            if (!(member == "string"))
            {
                w.Line(state + ".CheckBuffer();");
                w.Line(state + ".Write(in " + expression + ");");
            }
            else
                w.Line(state + ".Write<TLuminProfile>(" + expression + ");");
        }

        // Reads one entry member from the isolated operation state into an assignable location.
        void ReadMember(string state, string member, string destination)
        {
            if (member == "string")
                w.Line(state + ".Read<TLuminProfile>(ref " + destination + ");");
            else
                w.Line(state + ".Read(ref " + memory + ".As<" + member + ", byte>(ref " + destination + "), " + memory + ".SizeOf<" + member + ">());");
        }

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + type + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + dictionary + " input)");
                    w.Line("        => " + roots + ".RootWrite<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + dictionary + " input)");
                    w.Line(generic + options + " => " + roots + ".RootWrite<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWrite<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    " + dictionary + " input)");
                    w.Line(generic + options);
                    w.Open();
                    w.Line(writer + " operationState = default;");
                    w.Line("writer.CopyTo(ref operationState);");
                    w.Line("var value = input;");
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("operationState.EnsureAdditionalCapacity(4);");
                    w.Line(memory + ".WriteUnaligned(ref operationState.Cursor, " + code + ".NullCollection);");
                    w.Line("operationState.Advance(4);");
                    w.Line("goto operationComplete_RootWrite;");
                    w.Close();
                    w.Blank();
                    w.Line("int count = value.Count;");
                    if (pair)
                        w.Line("operationState.EnsureAdditionalCapacity(checked(sizeof(int) + count * (" + memory + ".SizeOf<" + key + ">() + " +
                            memory + ".SizeOf<" + value + ">())));");
                    w.Line("int countPosition = operationState.BeginCollection();");
                    w.Line("if (count != 0 && !WriteDense<TLuminProfile>(ref operationState, value, count))");
                    w.Indented(() => w.Line("WriteSparse<TLuminProfile>(ref operationState, value);"));
                    w.Line("operationState.EndCollection(countPosition, count);");
                    w.Line("operationState.CheckBuffer();");
                    w.Blank();
                    w.Line("operationComplete_RootWrite:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref writer);");
                    w.Close();
                    w.Blank();

                    // The dense path models the 64-bit runtime layout; any other layout enumerates in the sparse path.
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("private static bool WriteDense<TLuminProfile>(ref " + writer + " operationState, " + dictionary + " value, int count)");
                    w.Line(generic + options);
                    w.Open();
                    w.Line("if (" + memory + ".SizeOf<global::System.IntPtr>() != 8)");
                    w.Indented(() => w.Line("return false;"));
                    w.Line("var dictionaryView = global::LuminPack.Code.LuminPackMarshal.GetDictionaryView(value);");
                    w.Line("if (dictionaryView._count != count)");
                    w.Indented(() => w.Line("return false;"));
                    w.Line("int version = dictionaryView._version;");
                    if (pair)
                        w.Line("global::LuminPack.Code.LuminPackMarshal.WriteDenseRawPairs(ref operationState, value, count);");
                    else
                    {
                        w.Line("ref var entryRef = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(dictionaryView._entries!);");
                        w.Line("for (int index = 0; index < count; index++)");
                        w.Open();
                        w.Line("ref var entry = ref entryRef;");
                        WriteMember("operationState", key, "entry.Key");
                        WriteMember("operationState", value, "entry.Value");
                        w.Line("entryRef = ref " + memory + ".Add(ref entryRef, 1);");
                        w.Close();
                    }
                    w.Line("if (dictionaryView._version != version || dictionaryView._count != count || dictionaryView._freeCount != 0)");
                    w.Indented(() => w.Line(exception + ".ThrowCollectionModifiedDuringSerialization();"));
                    w.Line("return true;");
                    w.Close();
                    w.Blank();
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("private static void WriteSparse<TLuminProfile>(ref " + writer + " operationState, " + dictionary + " source)");
                    w.Line(generic + options);
                    w.Open();
                    w.Line("foreach (var item in source)");
                    w.Open();
                    w.Line(key + " key = item.Key;");
                    WriteMember("operationState", key, "key");
                    w.Line(value + " itemValue = item.Value;");
                    WriteMember("operationState", value, "itemValue");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + dictionary + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + dictionary + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWrite<TOptions>(ref state, value);");
                    w.Blank();
                    break;
            }
        }

        void EmitBinaryRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + type + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + dictionary + " value)");
                    w.Line("        => " + roots + ".RootRead<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + dictionary + " value)");
                    w.Line(generic + options + " => " + roots + ".RootRead<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootRead<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + dictionary + " value)");
                    w.Line(generic + options);
                    w.Open();
                    w.Line(reader + " operationState = default;");
                    w.Line("reader.CopyTo(ref operationState);");
                    w.Line("int length = operationState.ReadLength(" + minimum + ");");
                    w.Line("if (length < 0)");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    w.Line("if (length == 0)");
                    w.Open();
                    w.Line("value ??= new " + dictionary + "(0);");
                    w.Line("value.Clear();");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    w.Line("if (value is not null)");
                    w.Open();
                    if (pair)
                    {
                        // Raw keys and values carry no identity and run no user code, so an empty default-comparer destination is refilled
                        // through the fresh bucket build and keeps its capacity and enumeration order.
                        w.Line("if (value.Count == 0 && " + memory + ".SizeOf<global::System.IntPtr>() == 8 &&");
                        w.Line("    global::System.Object.ReferenceEquals(value.Comparer, global::System.Collections.Generic.EqualityComparer<" + key + ">.Default))");
                        w.Open();
                        w.Line("int refillVersion = global::LuminPack.Code.LuminPackMarshal.GetDictionaryView(value)._version;");
                        w.Line("var refillView = global::LuminPack.Code.LuminPackMarshal.PrepareDictionaryRefill(value, length);");
                        w.Line("ref var refillRef = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(refillView._entries!);");
                        w.Line("for (int index = 0; index < length; index++)");
                        w.Open();
                        w.Line("ref var refillEntry = ref " + memory + ".Add(ref refillRef, (nint)(uint)index);");
                        ReadMember("operationState", key, "refillEntry.Key");
                        ReadMember("operationState", value, "refillEntry.Value");
                        w.Close();
                        w.Line("if (!global::LuminPack.Code.LuminPackMarshal.TryRebuildFreshDictionaryBuckets(refillView, length))");
                        w.Open();
                        w.Line("var pairs = new " + entry + "[length];");
                        w.Line("for (int index = 0; index < length; index++)");
                        w.Open();
                        w.Line("ref var refillEntry = ref " + memory + ".Add(ref refillRef, (nint)(uint)index);");
                        w.Line("pairs[index] = new " + entry + "(refillEntry.Key, refillEntry.Value);");
                        w.Close();
                        w.Line("value.Clear();");
                        w.Line("foreach (var item in pairs)");
                        w.Indented(() => w.Line("value.Add(item.Key, item.Value);"));
                        w.Close();
                        w.Line("else");
                        w.Indented(() => w.Line("refillView._version = unchecked(refillVersion + length + 1);"));
                        w.Line("goto operationComplete_RootRead;");
                        w.Close();
                    }
                    w.Line("ReadExisting<TLuminProfile>(ref operationState, value, length);");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    w.Line("if (" + memory + ".SizeOf<global::System.IntPtr>() != 8)");
                    w.Open();
                    w.Line("var fallback = new " + dictionary + "(length);");
                    w.Line("for (int index = 0; index < length; index++)");
                    w.Open();
                    w.Line(key + " key = default !;");
                    w.Line(value + " itemValue = default !;");
                    ReadMember("operationState", key, "key");
                    ReadMember("operationState", value, "itemValue");
                    w.Line("fallback.Add(key, itemValue);");
                    w.Close();
                    w.Line("value = fallback;");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    w.Line("var result = new " + dictionary + "(length);");
                    w.Line("var dictionaryView = global::LuminPack.Code.LuminPackMarshal.GetDictionaryView(result);");
                    w.Line("var entries = dictionaryView._entries!;");
                    w.Line("for (int index = 0; index < length; index++)");
                    w.Open();
                    w.Line("ref var entry = ref entries[index];");
                    if (pair)
                    {
                        ReadMember("operationState", key, "entry.Key");
                        ReadMember("operationState", value, "entry.Value");
                    }
                    else
                    {
                        // A fresh entry starts with default values; read into locals, then store.
                        w.Line(key + " key = default !;");
                        w.Line(value + " itemValue = default !;");
                        ReadMember("operationState", key, "key");
                        ReadMember("operationState", value, "itemValue");
                        w.Line("entry.Key = key;");
                        w.Line("entry.Value = itemValue;");
                    }
                    w.Close();
                    w.Blank();
                    w.Line("if (!global::LuminPack.Code.LuminPackMarshal.TryRebuildFreshDictionaryBuckets(dictionaryView, length))");
                    w.Open();
                    w.Line("var rebuilt = new " + dictionary + "(length);");
                    w.Line("for (int index = 0; index < length; index++)");
                    w.Open();
                    w.Line("ref var entry = ref entries[index];");
                    w.Line("rebuilt.Add(entry.Key, entry.Value);");
                    w.Close();
                    w.Line("result = rebuilt;");
                    w.Close();
                    w.Line("value = result;");
                    w.Blank();
                    w.Line("operationComplete_RootRead:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref reader);");
                    w.Close();
                    w.Blank();

                    // An existing dictionary is replaced through a staged read, so a failed read leaves its entries unchanged.
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("private static void ReadExisting<TLuminProfile>(ref " + reader + " operationState, " + dictionary +
                        " destination, int count)");
                    w.Line(generic + options);
                    w.Open();
                    w.Line("var items = global::System.Buffers.ArrayPool<" + entry + ">.Shared.Rent(count);");
                    w.Line("try");
                    w.Open();
                    w.Line("for (int index = 0; index < count; index++)");
                    w.Open();
                    w.Line(key + " key = default !;");
                    w.Line(value + " item = default !;");
                    ReadMember("operationState", key, "key");
                    ReadMember("operationState", value, "item");
                    w.Line("items[index] = new " + entry + "(key, item);");
                    w.Close();
                    w.Line("destination.Clear();");
                    w.Line("for (int index = 0; index < count; index++)");
                    w.Indented(() => w.Line("destination.Add(items[index].Key, items[index].Value);"));
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + entry + ">.Shared.Return(items,");
                    w.Indented(() => w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + entry + ">());"));
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + dictionary + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead<" + utf16 + ">(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + dictionary + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootRead<TOptions>(ref state, ref value);");
                    w.Blank();
                    break;
            }
        }

        void EmitJsonWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + type + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + dictionary + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + dictionary + " input)");
                    w.Line(generic + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    " + dictionary + " input)");
                    w.Line(generic + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value == null)");
                    w.Open();
                    w.Line("writer.WriteNull<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteArrayStart<TLuminProfile>();");
                    w.Line("bool isFirst = true;");
                    w.Line("foreach (var item in value)");
                    w.Open();
                    w.Line("if (!isFirst)");
                    w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("else");
                    w.Line("    isFirst = false;");
                    w.Line("writer.SetFirstElement(true);");
                    w.Line("writer.WriteArrayStart<TLuminProfile>();");
                    w.Line("var key = item.Key;");
                    w.Line("writer.SetFirstElement(true);");
                    w.Line(keyOwner + ".Write<TLuminProfile>(ref writer, key);");
                    w.Line("writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("var itemValue = item.Value;");
                    w.Line("writer.SetFirstElement(true);");
                    w.Line(valueOwner + ".Write<TLuminProfile>(ref writer, itemValue);");
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + dictionary + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + dictionary + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWriteJson<TOptions>(ref state, value);");
                    w.Blank();
                    break;
            }
        }

        void EmitJsonRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + type + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + dictionary + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + dictionary + " value)");
                    w.Line(generic + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + dictionary + " value)");
                    w.Line(generic + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("if (value is not null && value.GetType() != typeof(" + dictionary + "))");
                    w.Indented(() => w.Line("value = null;"));
                    w.Line("if (value is null)");
                    w.Indented(() => w.Line("value = new " + dictionary + "();"));
                    w.Line("else");
                    w.Indented(() => w.Line("value.Clear();"));
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Line("while (reader.Read<TLuminProfile>())");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    // An entry that is not an array is skipped whole, which keeps the outer array boundary.
                    w.Line("if (reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.ArrayStart)");
                    w.Open();
                    w.Line("reader.Skip<TLuminProfile>();");
                    w.Line("continue;");
                    w.Close();
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Line(key + " key = default !;");
                    w.Line(value + " itemValue = default !;");
                    w.Line("if (reader.Read<TLuminProfile>() && reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Open();
                    w.Line(keyOwner + ".Read<TLuminProfile>(ref reader, ref key);");
                    w.Line("if (reader.Read<TLuminProfile>() && reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Open();
                    w.Line(valueOwner + ".Read<TLuminProfile>(ref reader, ref itemValue);");
                    // A missing value has already consumed the entry end; drain only after an actual value.
                    w.Line("while (reader.Read<TLuminProfile>())");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    w.Line("reader.Skip<TLuminProfile>();");
                    w.Close();
                    w.Close();
                    w.Close();
                    w.Line("value[key] = itemValue;");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + dictionary + " value)");
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + "." + roots + ".RootReadJson<" + utf16 + ">(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + jsonReader + " state,");
                    w.Line("    ref " + dictionary + " value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + "." + roots + ".RootReadJson<TOptions>(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }
    }
}
