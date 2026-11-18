using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
/// <remarks>The key and value are each a raw scalar or a string. The wire is the entry count followed by each key and its value;
/// two raw scalars store packed raw pairs. The binary write copies the dense entry storage when the runtime layout is the 64-bit
/// one and the dictionary has no removed entries, and enumerates otherwise. The binary read reuses a non-null destination
/// through a staged refill, or builds a fresh dictionary directly in its entry storage and rebuilds its buckets, falling back to
/// <c>Add</c>. The JSON form is an array of key and value arrays.</remarks>
internal static class DictionaryOwnerFile
{
    // The sparse scalar template is used only for these native nullable payloads.
    /// <summary>Handles raw nullable for <c>DictionaryOwnerFile</c>.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>Whether the input satisfies the raw nullable check.</returns>
    private static bool RawNullable(string member) => member.EndsWith("?", System.StringComparison.Ordinal) &&
        member.Substring(0, member.Length - 1) is "bool" or "byte" or "sbyte" or "short" or "ushort" or "char" or "int" or "uint" or "float";

    /// <summary>Selects sparse nullable entries, retained raw/string kernels or recursive value-owner operations.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="fact">The fact supplied to <c>RenderDefault</c>.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <returns>The generated C# source.</returns>
    internal static string RenderDefault(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations)
    {
        var model = fact.Contract!;
        var key = model.Arguments[0];
        var value = model.Arguments[1];
        if (GenericIdentity.IsRaw(key.Type) && key.Type != "bool" && value.NativeNullableElement &&
            RawNullable(value.Arguments[0].Type + "?"))
            return Render(set, new DictionaryOwnerFact(fact.Family, model.Type, key.Type, value.Arguments[0].Type + "?"), operations);
        // A packable payload is a native entry only when its own formatter is a plain storage copy.
        bool Native(Model.FormatterModel side) => side.Raw && (side.Payload == null || side.RawStorage);
        bool direct = ((Native(key) || key.Type == "string") && (Native(value) || value.Type == "string") ||
            key.RawStorage && value.RawStorage) && key.Type != "bool" && value.Type != "bool";
        return direct ? Render(set, new DictionaryOwnerFact(fact.Family, model.Type, key.Type, value.Type), operations) :
            PayloadDictionaryOwnerFile.Render(set, fact, operations);
    }

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
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
        // A nullable raw value makes the entries sparse: it is stored with the core store of the whole nullable storage.
        bool sparse = value.EndsWith("?", System.StringComparison.Ordinal);
        bool rawKey = key != "string", rawValue = value != "string", pair = rawKey && rawValue && !sparse;
        bool CallbackFree(string item) => CallbackFreeType(GenericFormatterReuse.Real(item));
        bool CallbackFreeType(string item) => GenericIdentity.IsRaw(item) || FrameworkScalarBody.Contains(item) ||
            set.FindFormatter(item)?.Underlying != null || set.Scalars.Any(scalar => scalar.StartsWith(item + "=", System.StringComparison.Ordinal));
        bool observeRawLookup = pair && CallbackFree(key) && CallbackFree(value);
        string dictionary = fact.DeclaredType;
        var contractModel = set.FindFormatter(dictionary);
        bool rawPairWrite = pair && contractModel?.Supports("BinaryRawPairs") == true;
        bool packedPairRead = pair && contractModel?.Supports("ReadPackedRawPairs") == true;
        bool expandPairs = pair && contractModel?.Supports("ExpandDenseRawPairs") == true;
        bool textPairWrite = !sparse && rawKey != rawValue && contractModel?.Supports("BinaryTextPair") == true;
        bool stringPairCursor = !rawKey && contractModel?.Supports("BinaryStringPairCursor") == true;
        bool localRead = set.Options?.Net8 == true && !pair;
        bool enumeratedStringKey = key == "string" && contractModel?.Supports("EnumeratedStringKey") == true;
        bool completeRawValue = enumeratedStringKey && rawValue && !sparse && CallbackFree(value);
        string rawWidth = memory + ".SizeOf<" + (rawKey ? key : value) + ">()";
        string keyWidth = memory + ".SizeOf<" + key + ">()";
        string pairWidth = "(" + keyWidth + " + " + memory + ".SizeOf<" + value + ">())";
        const string runtimeWriter = "global::LuminPack.Core.LuminPackWriter";
        string type = ReadableTypeName.Of(GeneratedNames.PayloadKey(dictionary));
        string roots = "LuminRoots_" + type;
        string owner = "Binary_" + extension + "_" + type;
        string keyOwner = "global::LuminPack." + GeneratedNames.Owner(extension, key);
        string valueOwner = "global::LuminPack." + GeneratedNames.Owner(extension,
            sparse ? "System.Nullable<" + value.Substring(0, value.Length - 1) + ">" : value);
        string entry = "global::System.Collections.Generic.KeyValuePair<" + key + ", " + value + ">";
        string minimum = (rawKey ? memory + ".SizeOf<" + key + ">()" : "1") + " + " + (rawValue && (!sparse || RawNullable(value)) ? memory + ".SizeOf<" + value + ">()" : "1");
        const string generic = "    where TLuminProfile : struct, ";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        // Writes one entry member to the isolated operation state.
        void WriteMember(string state, string member, string expression)
        {
            if (member.EndsWith("?", System.StringComparison.Ordinal))
            {
                if (contractModel?.Arguments[1] is { } nullable)
                    FormatterElementEmitter.WriteCollectionElement(w, set, nullable, expression, state);
                else
                    w.Line(valueOwner + ".Write<TLuminProfile>(ref " + state + ", " + expression + ");");
            }
            else if (!(member == "string"))
            {
                w.Line(state + ".CheckBuffer();");
                w.Line(state + ".EnsureAdditionalCapacity(" + memory + ".SizeOf<" + member + ">());");
                w.Line(state + ".Advance(global::LuminPack.Core.LuminPackWriter.Write(ref " + state + ".Cursor, in " + expression + "));");
            }
            else
                w.Line(state + ".Write<TLuminProfile>(" + expression + ");");
        }

        // Reads one entry member from the isolated operation state into an assignable location.
        void ReadMember(string state, string member, string destination)
        {
            if (member.EndsWith("?", System.StringComparison.Ordinal))
            {
                if (contractModel?.Arguments[1] is { } nullable)
                    FormatterElementEmitter.ReadCollectionElement(w, set, nullable, destination, state);
                else
                    w.Line(valueOwner + ".Read<TLuminProfile>(ref " + state + ", ref " + destination + ");");
            }
            else if (member == "string")
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
                        if (stringPairCursor)
                        {
                            w.Line("bool bounded = " + ParallelDictionaryWrite.FixedUtf16LengthProfile + ";");
                            w.Line("ref byte cursor = ref operationState.Cursor;");
                            w.Line("int room = bounded ? operationState.RecordRoom(ref cursor) : 0;");
                        }
                        w.Line("int index = 0;");
                        w.Line("do");
                        w.Open();
                        w.Line("ref var entry = ref " + memory + ".Add(ref entryRef, (nint)(uint)index);");
                        if (stringPairCursor)
                            EmitDenseStringPair();
                        else if (textPairWrite)
                            w.Line("WriteTextPair<TLuminProfile>(ref operationState, in entry.Key, in entry.Value);");
                        else
                        {
                            WriteMember("operationState", key, "entry.Key");
                            WriteMember("operationState", value, "entry.Value");
                        }
                        w.Close();
                        w.Line("while (++index < count);");
                        if (stringPairCursor)
                            w.Line("if (bounded) operationState.CommitRecords(ref cursor);");
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
                    if (rawPairWrite)
                    {
                        w.Line(value + " itemValue = item.Value;");
                        w.Line("if (operationState.RecordRoom() >= " + pairWidth + ")");
                        w.Open();
                        w.Line("ref byte record = ref operationState.Cursor;");
                        w.Line(runtimeWriter + ".Write(ref record, in key);");
                        w.Line(runtimeWriter + ".Write(ref " + memory + ".Add(ref record, " + keyWidth + "), in itemValue);");
                        w.Line("operationState.CompleteRun(" + pairWidth + ");");
                        w.Close();
                        w.Line("else");
                        w.Indented(() => w.Line("WriteSparsePair<TLuminProfile>(ref operationState, key, itemValue);"));
                    }
                    else if (textPairWrite)
                    {
                        w.Line("var itemValue = item.Value;");
                        w.Line("WriteTextPair<TLuminProfile>(ref operationState, in key, in itemValue);");
                    }
                    else
                    {
                        WriteMember("operationState", key, "key");
                        w.Line(value + " itemValue = item.Value;");
                        WriteMember("operationState", value, "itemValue");
                    }
                    w.Close();
                    w.Close();
                    w.Blank();
                    if (rawPairWrite)
                    {
                        w.Line(w.MethodImpl("NoInlining"));
                        w.Line("private static void WriteSparsePair<TLuminProfile>(ref " + writer + " operationState, " + key + " key, " + value + " itemValue)");
                        w.Line(generic + options);
                        w.Open();
                        WriteMember("operationState", key, "key");
                        WriteMember("operationState", value, "itemValue");
                        w.Close();
                        w.Blank();
                    }
                    if (textPairWrite)
                        EmitTextPair();
                    if (stringPairCursor && !textPairWrite)
                        EmitTextPairFallback();
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

        void EmitDenseStringPair()
        {
            w.Line("if (bounded)");
            w.Open();
            w.Line("string key = entry.Key;");
            w.Line(value + " itemValue = entry.Value;");
            ParallelDictionaryWrite.WriteStringKeyPairIfFits(w, memory, rawValue ? rawWidth : null);
            w.Line("operationState.CommitRecords(ref cursor);");
            w.Close();
            w.Line("WriteTextPairSlow<TLuminProfile>(ref operationState, in entry.Key, in entry.Value);");
            w.Line("if (bounded)");
            w.Open();
            w.Line("cursor = ref operationState.Cursor;");
            w.Line("room = operationState.RecordRoom(ref cursor);");
            w.Close();
        }

        void EmitTextPair()
        {
            string text = rawKey ? "itemValue" : "key";
            string scalar = rawKey ? "key" : "itemValue";
            w.Line(inline);
            w.Line("private static void WriteTextPair<TLuminProfile>(scoped ref " + writer + " operationState, in " + key + " key, in " + value + " itemValue)");
            w.Line(generic + options);
            w.Open();
            w.Line("string text;");
            w.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile + " &&");
            w.Line("    (long)operationState.RecordRoom() - " + rawWidth + " >= " + runtimeWriter + ".Utf16RecordBound(text = " + text + "))");
            w.Open();
            w.Line("ref byte cursor = ref operationState.Cursor;");
            if (rawKey)
            {
                w.Line(runtimeWriter + ".Write(ref cursor, in " + scalar + ");");
                w.Line("int written = " + runtimeWriter + ".WriteReservedUtf16Record(ref " + memory + ".Add(ref cursor, " + rawWidth + "), text);");
            }
            else
            {
                w.Line("int written = " + runtimeWriter + ".WriteReservedUtf16Record(ref cursor, text);");
                w.Line(runtimeWriter + ".Write(ref " + memory + ".Add(ref cursor, (nint)(uint)written), in " + scalar + ");");
            }
            w.Line("operationState.CompleteRun(written + " + rawWidth + ");");
            w.Line("return;");
            w.Close();
            w.Line("WriteTextPairSlow<TLuminProfile>(ref operationState, in key, in itemValue);");
            w.Close();
            w.Blank();
            EmitTextPairFallback();
        }

        void EmitTextPairFallback()
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("private static void WriteTextPairSlow<TLuminProfile>(scoped ref " + writer + " operationState, in " + key + " key, in " + value + " itemValue)");
            w.Line(generic + options);
            w.Open();
            WriteMember("operationState", key, "key");
            WriteMember("operationState", value, "itemValue");
            w.Close();
            w.Blank();
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
                    // The original Dictionary, SortedDictionary and SortedList reads took TryReadCollectionHead: one byte per entry.
                        w.Line("int length = operationState.ReadLength(" + (minimum.Split('+').Any(part => part.Trim() != "0") && (dictionary.StartsWith("global::System.Collections.Generic.Dictionary<", System.StringComparison.Ordinal) ||
                            dictionary.StartsWith("global::System.Collections.Generic.SortedDictionary<", System.StringComparison.Ordinal) ||
                            dictionary.StartsWith("global::System.Collections.Generic.SortedList<", System.StringComparison.Ordinal)) ? "1" : minimum) + ");");
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
                        ValidateRawPairs("refillSource", "refillBytes");
                        w.Line("int refillVersion = global::LuminPack.Code.LuminPackMarshal.GetDictionaryView(value)._version;");
                        w.Line("var refillView = global::LuminPack.Code.LuminPackMarshal.PrepareDictionaryRefill(value, length);");
                        w.Line("ref var refillRef = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(refillView._entries!);");
                        FillRawEntries("refillView", "refillRef", "refillSource", "refillBytes", "refillEntry", "refillRemaining");
                        w.Line("if (!global::LuminPack.Code.LuminPackMarshal.TryRebuildFreshDictionaryBuckets(refillView, length))");
                        w.Indented(() => w.Line("ReplayRejectedRefill(value, ref refillRef, length);"));
                        w.Line("else");
                        w.Indented(() => w.Line("refillView._version = unchecked(refillVersion + length + 1);"));
                        w.Line("goto operationComplete_RootRead;");
                        w.Close();
                    }
                    if (set.FindFormatter(dictionary) is { } contract)
                        DictionarySlotReuseEmitter.Emit(w, set, contract, "operationState", "value", "length", pair);
                    w.Line("ReadExisting<TLuminProfile>(ref operationState, value, length);");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    w.Line("if (" + memory + ".SizeOf<global::System.IntPtr>() != 8)");
                    w.Open();
                    w.Line("var fallback = new " + dictionary + "(length);");
                    w.Line("int fallbackRemaining = length;");
                    w.Line("do");
                    w.Open();
                    w.Line(key + " key = default !;");
                    w.Line(value + " itemValue = default !;");
                    ReadMember("operationState", key, "key");
                    ReadMember("operationState", value, "itemValue");
                    w.Line("fallback.Add(key, itemValue);");
                    w.Close();
                    w.Line("while (--fallbackRemaining != 0);");
                    w.Line("value = fallback;");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    if (pair)
                        ValidateRawPairs("source", "byteCount");
                    w.Line("var result = new " + dictionary + "(length);");
                    w.Line("var dictionaryView = global::LuminPack.Code.LuminPackMarshal.GetDictionaryView(result);");
                    w.Line("ref var firstEntry = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(dictionaryView._entries!);");
                    if (pair)
                        FillRawEntries("dictionaryView", "firstEntry", "source", "byteCount", "entry", "remaining");
                    else
                    {
                        w.Line("ref var entry = ref firstEntry;");
                        w.Line("int remaining = length;");
                        w.Line("do");
                        w.Open();
                        // A fresh entry starts with default values; read into locals, then store.
                        w.Line(key + " key = default !;");
                        w.Line(value + " itemValue = default !;");
                        ReadMember("operationState", key, "key");
                        ReadMember("operationState", value, "itemValue");
                        w.Line("entry.Key = key;");
                        w.Line("entry.Value = itemValue;");
                        w.Line("entry = ref " + memory + ".Add(ref entry, 1);");
                        w.Close();
                        w.Line("while (--remaining != 0);");
                    }
                    w.Blank();
                    w.Line("if (!global::LuminPack.Code.LuminPackMarshal.TryRebuildFreshDictionaryBuckets(dictionaryView, length))");
                    w.Open();
                    w.Line("var rebuilt = new " + dictionary + "(length);");
                    w.Line("int rebuildIndex = 0;");
                    w.Line("do");
                    w.Open();
                    w.Line("ref var item = ref " + memory + ".Add(ref firstEntry, (nint)(uint)rebuildIndex);");
                    w.Line("rebuilt.Add(item.Key, item.Value);");
                    w.Close();
                    w.Line("while (++rebuildIndex < length);");
                    w.Line("result = rebuilt;");
                    w.Close();
                    w.Line("value = result;");
                    w.Blank();
                    w.Line("operationComplete_RootRead:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref reader);");
                    w.Close();
                    w.Blank();

                    if (pair)
                        EmitRejectedRefill();

                    // An existing dictionary is replaced through a staged read, so a failed read leaves its entries unchanged.
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("private static void ReadExisting<TLuminProfile>(ref " + reader + (localRead ? " target, " : " operationState, ") + dictionary +
                        " destination, int count)");
                    w.Line(generic + options);
                    w.Open();
                    if (localRead)
                        w.Line("var operationState = target;");
                    w.Line("var items = global::System.Buffers.ArrayPool<" + entry + ">.Shared.Rent(count);");
                    w.Line("try");
                    w.Open();
                    if (key == "string")
                    {
                        w.Line("var previous = destination.GetEnumerator();");
                        if (enumeratedStringKey)
                            w.Line("bool useEnumeratedValue = operationState.CanReuseEnumeratedStringKey<TLuminProfile>(destination.Comparer);");
                    }
                    if (observeRawLookup)
                        w.Line("bool observeLookup = !global::System.Object.ReferenceEquals(destination.Comparer, global::System.Collections.Generic.EqualityComparer<" + key + ">.Default);");
                    w.Line("ref var firstPair = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items);");
                    w.Line("ref var slot = ref firstPair;");
                    w.Line("int remaining = count;");
                    if (packedPairRead)
                    {
                        string observedLookup = observeRawLookup ? "observeLookup || " : "";
                        w.Line("if (" + observedLookup + "!global::LuminPack.Utility.LuminTreeStorage.TryReadPackedPairs(ref operationState, items, count))");
                        w.Open();
                    }
                    w.Line("do");
                    w.Open();
                    if (key == "string")
                        w.Line("var prior = previous.MoveNext() ? previous.Current : default;");
                    w.Line(key == "string" ? "string key = prior.Key;" : key + " key = default!;");
                    ReadMember("operationState", key, "key");
                    if (pair)
                    {
                        if (observeRawLookup)
                            w.Line("if (observeLookup) destination.TryGetValue(key, out _);");
                        w.Line(value + " item = default!;");
                    }
                    else if (completeRawValue)
                    {
                        // The default string comparer has no observers and the read overwrites the entire scalar.
                        w.Line(value + " item = default;");
                        w.Line("if (!useEnumeratedValue && key is not null) destination.TryGetValue(key, out item);");
                    }
                    else
                        w.Line(value + " item = " + (enumeratedStringKey ?
                            "useEnumeratedValue && global::System.Object.ReferenceEquals(key, prior.Key) ? prior.Value : " : "") +
                            // A non-nullable value key needs no null check before the lookup.
                            (key == "string" || key == "object" || key.EndsWith("?", System.StringComparison.Ordinal) ? "key is not null && " : "") +
                            "destination.TryGetValue(key, out var existing) ? existing : default!;");
                    ReadMember("operationState", value, "item");
                    w.Line("slot = new " + entry + "(key, item);");
                    w.Line("slot = ref " + memory + ".Add(ref slot, 1);");
                    w.Close();
                    w.Line("while (--remaining != 0);");
                    if (packedPairRead)
                        w.Close();
                    w.Line("destination.Clear();");
                    w.Line("slot = ref firstPair;");
                    w.Line("remaining = count;");
                    w.Line("do");
                    w.Open();
                    w.Line("destination.Add(slot.Key, slot.Value);");
                    w.Line("slot = ref " + memory + ".Add(ref slot, 1);");
                    w.Close();
                    w.Line("while (--remaining != 0);");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + entry + ">.Shared.Return(items,");
                    w.Indented(() => w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + entry + ">());"));
                    w.Close();
                    if (localRead)
                        w.Line("operationState.CopyTo(ref target);");
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

        // Bound the full key/value block before loading an address-only field or mutating refill storage.
        void ValidateRawPairs(string input, string byteCount)
        {
            // An oversized claim cannot fit after the collection header, so Advance rejects the saturated width.
            w.Line("int " + byteCount + " = (int)global::System.Math.Min((long)length * " + pairWidth + ", int.MaxValue);");
            w.Line("ref byte " + input + " = ref operationState.Cursor;");
            w.Line("operationState.Advance(" + byteCount + ");");
        }

        // Both callers have established room for length entries and a complete source block.
        void FillRawEntries(string view, string first, string input, string byteCount, string entryCursor, string remaining)
        {
            const string read = "global::LuminPack.Core.LuminPackReader.Read";
            if (expandPairs)
            {
                const string marshal = "global::LuminPack.Code.LuminPackMarshal";
                w.Line("if (!" + marshal + ".TryExpandDenseRawPairs(" + marshal + ".CreateReadOnlySpan(ref " + input +
                    ", " + byteCount + "), " + view + ", length))");
                w.Open();
            }
            w.Line("ref var " + entryCursor + " = ref " + first + ";");
            w.Line("int " + remaining + " = length;");
            w.Line("do");
            w.Open();
            w.Line(read + "(ref " + input + ", out " + entryCursor + ".Key);");
            w.Line(read + "(ref " + memory + ".Add(ref " + input + ", " + keyWidth + "), out " + entryCursor + ".Value);");
            w.Line(entryCursor + " = ref " + memory + ".Add(ref " + entryCursor + ", 1);");
            w.Line(input + " = ref " + memory + ".Add(ref " + input + ", " + pairWidth + ");");
            w.Close();
            w.Line("while (--" + remaining + " != 0);");
            if (expandPairs)
                w.Close();
        }

        /// <summary>Emits the cold refill replay through the original dictionary's public insertion path.</summary>
        void EmitRejectedRefill()
        {
            const string marshal = "global::LuminPack.Code.LuminPackMarshal";
            string pool = "global::System.Buffers.ArrayPool<" + entry + ">.Shared";
            string storage = marshal + ".DictionaryView<" + key + ", " + value + ">.Entry";
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("private static void ReplayRejectedRefill(" + dictionary + " destination, ref " + storage + " firstEntry, int count)");
            w.Open();
            w.Line("var pairs = " + pool + ".Rent(count);");
            w.Line("try");
            w.Open();
            w.Line("for (int index = 0; index < count; index++)");
            w.Open();
            w.Line("ref var item = ref " + memory + ".Add(ref firstEntry, (nint)(uint)index);");
            w.Line("pairs[index] = new " + entry + "(item.Key, item.Value);");
            w.Close();
            w.Line("destination.Clear();");
            // A rejected rebuild leaves provisional buckets even when Clear sees zero published entries.
            w.Line("var buckets = " + marshal + ".GetDictionaryView(destination)._buckets;");
            w.Line("global::System.Array.Clear(buckets, 0, buckets.Length);");
            w.Line("for (int index = 0; index < count; index++)");
            w.Indented(() => w.Line("destination.Add(pairs[index].Key, pairs[index].Value);"));
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line(pool + ".Return(pairs, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + entry + ">());");
            w.Close();
            w.Close();
            w.Blank();
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
                    w.Line(w.MethodImpl("NoInlining"));
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
                    var pairWriter = new DictionaryJsonPairWriter(w, keyOwner, valueOwner, key, value,
                        pair && contractModel?.Supports("JsonIntegerPair") == true, capturedPair: true);
                    pairWriter.Begin();
                    w.Line("foreach (var item in value)");
                    w.Open();
                    pairWriter.Write("item.Key", "item.Value");
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
                    w.Line(w.MethodImpl("NoInlining"));
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
                    DictionaryJsonReuseEmitter.Emit(w, key, value, keyOwner, valueOwner, "value[key] = itemValue;", jsonReader);
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
