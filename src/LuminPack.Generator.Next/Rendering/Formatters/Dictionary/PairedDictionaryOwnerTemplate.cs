using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>The names and helpers one paired dictionary owner exposes to its family bodies.</summary>
internal sealed class PairedDictionaryContext
{
    /// <summary>The source writer of the owner file.</summary>
    internal CodeWriter W { get; }

    /// <summary>The closed declared dictionary spelling.</summary>
    internal string Dictionary { get; }

    /// <summary>The key keyword, or <c>string</c>.</summary>
    internal string Key { get; }

    /// <summary>The value keyword, or <c>string</c>.</summary>
    internal string Value { get; }

    /// <summary>The spelling of the unsafe memory helper.</summary>
    internal string Memory { get; }

    /// <summary>The closed key and value pair spelling.</summary>
    internal string Pair { get; }

    /// <summary>The assembly extension container the owners belong to.</summary>
    internal string Extension { get; }

    /// <summary>The expression that enumerates the entries of <c>value</c>.</summary>
    internal string Source { get; }

    /// <summary>The entry member that holds the key.</summary>
    internal string KeyMember { get; }

    /// <summary>The entry member that holds the value.</summary>
    internal string ValueMember { get; }

    /// <summary>The packable value's render model, or null when the value is a raw scalar or a string.</summary>
    internal PayloadRenderModel? Payload { get; }

    private readonly Dictionary<string, int> _writeNames = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
    private readonly Dictionary<string, int> _readNames = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly bool _forwardedValue;
    private int _ends, _cursors, _spans, _readCursors, _readStrings, _readEnds;

    internal PairedDictionaryContext(CodeWriter writer, string dictionary, string key, string value, string memory, string extension,
        string source = "value", string keyMember = "Key", string valueMember = "Value", PayloadRenderModel? payload = null,
        bool forwardedValue = false)
    {
        Payload = payload;
        _forwardedValue = forwardedValue;
        Source = source;
        KeyMember = keyMember;
        ValueMember = valueMember;
        Extension = extension;
        W = writer;
        Dictionary = dictionary;
        Key = key;
        Value = value;
        Memory = memory;
        Pair = "global::System.Collections.Generic.KeyValuePair<" + key + ", " + value + ">";
    }

    /// <summary>Writes one entry member to the isolated operation state.</summary>
    /// <param name="type">The member keyword or <c>string</c>.</param>
    /// <param name="expression">The member value expression.</param>
    internal void WriteMember(string type, string expression)
    {
        if (Payload != null && type == Payload.GlobalName)
        {
            // The value's record body is expanded in place; the expression is an addressable local.
            var body = new List<string>();
            string elementExit = "end" + _ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _ends++;
            body.Add("operationState.CheckBuffer();");
            BinaryEmitter.AddSequenceElementWrite(body, Payload, expression, elementExit, "operationState", 0, W, _writeNames, ref _ends,
                ref _cursors, ref _spans);
            foreach (string line in body)
                W.Line(line);
        }
        else if (_forwardedValue && type == Value)
        {
            W.Line("operationState.CheckBuffer();");
            W.Line(GenericOwnerFile.Owner(Extension, Value) + ".Write<TLuminProfile>(ref operationState, in " + expression + ");");
        }
        else if (type != "string")
        {
            W.Line("operationState.CheckBuffer();");
            W.Line("operationState.Write(in " + expression + ");");
        }
        else
            W.Line("operationState.Write<TLuminProfile>(" + expression + ");");
    }

    /// <summary>Reads one entry member from the isolated operation state into an assignable location.</summary>
    /// <param name="type">The member keyword or <c>string</c>.</param>
    /// <param name="destination">The assignable destination.</param>
    internal void ReadMember(string type, string destination)
    {
        if (Payload != null && type == Payload.GlobalName)
        {
            // The value's record body is expanded in place on the caller's reader, which operationState aliases.
            W.Open();
            W.Line(type + " payloadItem = default !;");
            var body = new List<string>();
            BinaryEmitter.AddSequenceElementRead(body, Payload, "payloadItem", null, W, _readNames, ref _readCursors, ref _readStrings,
                ref _readEnds);
            foreach (string line in body)
                W.Line(line);
            W.Line(destination + " = payloadItem;");
            W.Close();
        }
        else if (_forwardedValue && type == Value)
            ForwardedElementRead.Emit(W, Value, "operationState", destination,
                GenericOwnerFile.Owner(Extension, Value) + ".Read<TLuminProfile>(ref operationState, ref " + destination + ");");
        else if (type == "string")
            W.Line("operationState.Read<TLuminProfile>(ref " + destination + ");");
        else
            W.Line("operationState.Read(ref " + Memory + ".As<" + type + ", byte>(ref " + destination + "), " + Memory + ".SizeOf<" + type + ">());");
    }

    /// <summary>Emits the staged read of <c>length</c> entries into a pooled pair array.</summary>
    /// <param name="consume">The statements that consume the pooled <c>pairs</c> and <c>length</c>.</param>
    internal void ReadPairs(Action consume)
    {
        W.Line("var pairs = global::System.Buffers.ArrayPool<" + Pair + ">.Shared.Rent(length);");
        W.Line("try");
        W.Open();
        W.Line("for (int index = 0; index < length; index++)");
        W.Open();
        W.Line(Key + " k = default !;");
        W.Line(Value + " v = default !;");
        ReadMember(Key, "k");
        ReadMember(Value, "v");
        W.Line("pairs[index] = new " + Pair + "(k!, v);");
        W.Close();
        consume();
        W.Close();
        W.Line("finally");
        W.Open();
        W.Line("global::System.Buffers.ArrayPool<" + Pair + ">.Shared.Return(pairs,");
        W.Indented(() => W.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + Pair + ">());"));
        W.Close();
    }

    /// <summary>Emits the counted enumeration write of a nonnull dictionary named <c>value</c>.</summary>
    internal void CountedWrite()
    {
        W.Line("int countPosition = operationState.BeginCollection();");
        W.Line("int count = 0;");
        W.Line("foreach (var item in " + Source + ")");
        W.Open();
        W.Line("count++;");
        W.Line(Key + " key = item." + KeyMember + ";");
        WriteMember(Key, "key");
        W.Line(Value + " itemValue = item." + ValueMember + ";");
        WriteMember(Value, "itemValue");
        W.Close();
        W.Line("operationState.EndCollection(countPosition, count);");
        W.Line("operationState.CheckBuffer();");
    }

    /// <summary>Emits the write that copies a tree-backed dictionary's pairs once before writing them in order.</summary>
    internal void TreeWrite()
    {
        W.Line("int count = value.Count;");
        W.Line("int countPosition = operationState.BeginCollection();");
        W.Line("if (count != 0)");
        W.Open();
        W.Line("var pairs = global::System.Buffers.ArrayPool<" + Pair + ">.Shared.Rent(count);");
        W.Line("try");
        W.Open();
        W.Line("global::LuminPack.Utility.LuminTreeStorage.CopyTo(value, pairs);");
        W.Line("for (int index = 0; index < count; index++)");
        W.Open();
        W.Line(Key + " key = pairs[index].Key;");
        WriteMember(Key, "key");
        W.Line(Value + " itemValue = pairs[index].Value;");
        WriteMember(Value, "itemValue");
        W.Close();
        W.Close();
        W.Line("finally");
        W.Open();
        W.Line("global::System.Buffers.ArrayPool<" + Pair + ">.Shared.Return(pairs,");
        W.Indented(() => W.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + Pair + ">());"));
        W.Close();
        W.Close();
        W.Line("operationState.EndCollection(countPosition, count);");
        W.Line("operationState.CheckBuffer();");
    }
}

/// <summary>Describes how one paired dictionary family differs from the shared owner template.</summary>
internal sealed class PairedDictionarySpec
{
    /// <summary>Emits the binary write of a nonnull dictionary named <c>value</c> on <c>operationState</c>.</summary>
    internal Action<PairedDictionaryContext> WriteBody { get; }

    /// <summary>Emits the binary read after a non-negative <c>length</c>, or the whole read when it owns the header.</summary>
    internal Action<PairedDictionaryContext> ReadBody { get; }

    /// <summary>The statement that stores the staged JSON entry <c>key</c> and <c>itemValue</c>.</summary>
    internal string JsonAdd { get; }

    /// <summary>Emits the JSON destination preparation in place of the default create-or-clear.</summary>
    internal Action<PairedDictionaryContext>? JsonBefore { get; }

    /// <summary>Emits the statements that publish the staged JSON entries.</summary>
    internal Action<PairedDictionaryContext>? JsonAfter { get; }

    /// <summary>Whether the binary read reads the count header itself.</summary>
    internal bool ReadsHeader { get; }

    /// <summary>The expression that enumerates the entries of <c>value</c>.</summary>
    internal string Source { get; }

    /// <summary>The entry member that holds the key.</summary>
    internal string KeyMember { get; }

    /// <summary>The entry member that holds the value.</summary>
    internal string ValueMember { get; }

    internal PairedDictionarySpec(Action<PairedDictionaryContext> writeBody, Action<PairedDictionaryContext> readBody, string jsonAdd,
        Action<PairedDictionaryContext>? jsonBefore = null, Action<PairedDictionaryContext>? jsonAfter = null, bool readsHeader = false,
        string source = "value", string keyMember = "Key", string valueMember = "Value")
    {
        Source = source;
        KeyMember = keyMember;
        ValueMember = valueMember;
        WriteBody = writeBody;
        ReadBody = readBody;
        JsonAdd = jsonAdd;
        JsonBefore = jsonBefore;
        JsonAfter = jsonAfter;
        ReadsHeader = readsHeader;
    }
}

/// <summary>Renders the shared binary and JSON formatter owner of a closed dictionary of raw scalar or string entries.</summary>
/// <remarks>The wire is the entry count followed by each key and its value; the JSON form is an array of key and value arrays.
/// The binary algorithms and the JSON destination handling are supplied by the family through
/// <see cref="PairedDictionarySpec"/>.</remarks>
internal static class PairedDictionaryOwnerTemplate
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <param name="spec">The family algorithms.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations, PairedDictionarySpec spec)
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
        string key = fact.Key, value = fact.Value, dictionary = fact.DeclaredType;
        string type = ReadableTypeName.Of(GeneratedNames.PayloadKey(dictionary));
        string roots = "LuminRoots_" + type;
        string owner = "Binary_" + extension + "_" + type;
        string keyOwner = "global::LuminPack." + GeneratedNames.Owner(extension, key);
        string valueOwner = fact.Element != null ? "global::LuminPack." + fact.Element.Owner : "global::LuminPack." + GeneratedNames.Owner(extension, value);
        string minimum = (key != "string" ? memory + ".SizeOf<" + key + ">()" : "1") + " + " +
            (value != "string" && fact.Element == null && !fact.ForwardedValue ? memory + ".SizeOf<" + value + ">()" : "1");
        const string generic = "    where TLuminProfile : struct, ";
        var context = new PairedDictionaryContext(w, dictionary, key, value, memory, extension, spec.Source, spec.KeyMember, spec.ValueMember,
            fact.Element, fact.ForwardedValue);

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

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
                    spec.WriteBody(context);
                    w.Blank();
                    w.Line("operationComplete_RootWrite:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref writer);");
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
                    if (fact.Element != null || fact.ForwardedValue)
                        w.Line("ref var operationState = ref reader;");
                    else
                    {
                        w.Line(reader + " operationState = default;");
                        w.Line("reader.CopyTo(ref operationState);");
                    }
                    if (!spec.ReadsHeader)
                    {
                        w.Line("int length = operationState.ReadLength(" + minimum + ");");
                        w.Line("if (length < 0)");
                        w.Open();
                        w.Line("value = null;");
                        w.Line("goto operationComplete_RootRead;");
                        w.Close();
                        w.Blank();
                    }
                    spec.ReadBody(context);
                    w.Blank();
                    w.Line("operationComplete_RootRead:");
                    w.Indented(() => w.Line(";"));
                    if (fact.Element == null && !fact.ForwardedValue)
                        w.Line("operationState.CopyTo(ref reader);");
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
                    w.Line("foreach (var item in " + spec.Source + ")");
                    w.Open();
                    w.Line("if (!isFirst)");
                    w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("else");
                    w.Line("    isFirst = false;");
                    w.Line("writer.SetFirstElement(true);");
                    w.Line("writer.WriteArrayStart<TLuminProfile>();");
                    w.Line("var key = item." + spec.KeyMember + ";");
                    w.Line("writer.SetFirstElement(true);");
                    w.Line(keyOwner + ".Write<TLuminProfile>(ref writer, key);");
                    w.Line("writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("var itemValue = item." + spec.ValueMember + ";");
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
                    if (spec.JsonBefore != null)
                        spec.JsonBefore(context);
                    else
                    {
                        w.Line("if (value is null)");
                        w.Indented(() => w.Line("value = new " + dictionary + "();"));
                        w.Line("else");
                        w.Indented(() => w.Line("value.Clear();"));
                    }
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
                    w.Line(spec.JsonAdd);
                    w.Close();
                    spec.JsonAfter?.Invoke(context);
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
