using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>The names and writer one enumerable sequence owner exposes to its family bodies.</summary>
internal sealed class EnumerableOwnerContext
{
    /// <summary>The source writer of the owner file.</summary>
    internal CodeWriter W { get; }

    /// <summary>The closed declared sequence spelling.</summary>
    internal string Sequence { get; }

    /// <summary>The raw scalar element keyword.</summary>
    internal string Scalar { get; }

    /// <summary>The spelling of the unsafe memory helper.</summary>
    internal string Memory { get; }

    /// <summary>The spelling of the memory marshal helper.</summary>
    internal string Marshal { get; }

    /// <summary>Whether the element is a string, which uses the operation's profile.</summary>
    internal bool Text => Scalar == "string";

    /// <summary>The packable element's render model, or null when the element is a raw scalar or a string.</summary>
    internal PayloadRenderModel? Payload { get; }

    /// <summary>Whether the element is written one at a time through the profile instead of as raw storage.</summary>
    internal bool Counted => Text || Payload != null;

    private readonly Dictionary<string, int> _writeNames = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
    private readonly Dictionary<string, int> _readNames = new Dictionary<string, int>(StringComparer.Ordinal);
    private int _ends, _cursors, _spans, _readCursors, _readStrings, _readEnds;

    /// <summary>Emits the counted enumeration write of a nonnull sequence expression through <c>operationState</c>.</summary>
    /// <param name="local">The nonnull sequence expression.</param>
    internal void CountedWrite(string local)
    {
        if (Payload == null)
        {
            var lines = new List<string>();
            InterfaceTraversal.Counted(lines, Scalar, local);
            foreach (string line in lines)
                W.Line(line);
            return;
        }
        W.Line("int countPosition = operationState.BeginCollection();");
        W.Line("int count = 0;");
        W.Line("foreach (var item in " + local + ")");
        W.Open();
        var body = new List<string>();
        string elementExit = "end" + _ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ends++;
        body.Add("operationState.CheckBuffer();");
        body.Add("count++;");
        body.Add("var v = item;");
        BinaryEmitter.AddSequenceElementWrite(body, Payload, "v", elementExit, "operationState", 0, W, _writeNames, ref _ends,
            ref _cursors, ref _spans);
        foreach (string line in body)
            W.Line(line);
        W.Close();
        W.Blank();
        W.Line("operationState.EndCollection(countPosition, count);");
        W.Line("operationState.CheckBuffer();");
    }

    /// <summary>Emits the bottom-first packable write of a nonnull stack through <c>operationState</c>.</summary>
    /// <param name="local">The nonnull stack expression.</param>
    /// <param name="concurrent">Whether the stack is a concurrent stack.</param>
    internal void StackPayloadWrite(string local, bool concurrent)
    {
        var body = new List<string>();
        OrderedPayloadTraversal.WriteStack(body, Payload!, local, concurrent, W, _writeNames, ref _ends, ref _cursors, ref _spans);
        foreach (string line in body)
            W.Line(line);
    }

    /// <summary>Emits the packable write of a nonnull queue through <c>operationState</c>.</summary>
    /// <param name="local">The nonnull queue expression.</param>
    internal void QueuePayloadWrite(string local)
    {
        var body = new List<string>();
        OrderedPayloadTraversal.WriteQueue(body, Payload!, local, W, _writeNames, ref _ends, ref _cursors, ref _spans);
        foreach (string line in body)
            W.Line(line);
    }

    /// <summary>Emits the read of one element into an assignable location.</summary>
    /// <param name="destination">The assignable destination.</param>
    internal void ReadElement(string destination)
    {
        if (Payload != null)
        {
            // The element body is expanded in place on the caller's reader, which operationState aliases.
            W.Open();
            W.Line(Scalar + " payloadItem = default !;");
            var body = new List<string>();
            BinaryEmitter.AddSequenceElementRead(body, Payload, "payloadItem", null, W, _readNames, ref _readCursors, ref _readStrings,
                ref _readEnds);
            foreach (string line in body)
                W.Line(line);
            W.Line(destination + " = payloadItem;");
            W.Close();
        }
        else if (Text)
            W.Line("operationState.Read<TLuminProfile>(ref " + destination + ");");
        else
            W.Line("operationState.Read(ref " + Memory + ".As<" + Scalar + ", byte>(ref " + destination + "), " + Memory + ".SizeOf<" + Scalar + ">());");
    }

    /// <summary>Emits the read of <c>length</c> elements into a span.</summary>
    /// <param name="span">The destination span expression.</param>
    internal void ReadBlock(string span)
    {
        if (Payload != null)
        {
            W.Line("for (int blockIndex = 0; blockIndex < length; blockIndex++)");
            W.Open();
            ReadElement(span + "[blockIndex]");
            W.Close();
            return;
        }
        if (Text)
        {
            W.Line("for (int blockIndex = 0; blockIndex < length; blockIndex++)");
            W.Indented(() => W.Line("operationState.Read<TLuminProfile>(ref " + span + "[blockIndex]);"));
            return;
        }
        W.Line("operationState.Read(");
        W.Indented(() =>
        {
            W.Line("ref " + Memory + ".As<" + Scalar + ", byte>(ref " + Marshal + ".GetReference(" + span + ")),");
            W.Line("unchecked(length * " + Memory + ".SizeOf<" + Scalar + ">()));");
        });
    }

    internal EnumerableOwnerContext(CodeWriter writer, string sequence, string scalar, string memory, string marshal,
        PayloadRenderModel? payload = null)
    {
        Payload = payload;
        W = writer;
        Sequence = sequence;
        Scalar = scalar;
        Memory = memory;
        Marshal = marshal;
    }

    /// <summary>Emits the pooled block read of <c>length</c> raw elements and hands the pooled array to the family.</summary>
    /// <param name="consume">The statements that consume the pooled array and <c>length</c>.</param>
    internal void PooledRead(Action consume)
    {
        W.Line("var pooled = global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Rent(length);");
        W.Line("try");
        W.Open();
        W.Line("global::System.Span<" + Scalar + "> items = pooled.AsSpan(0, length);");
        ReadBlock("items");
        consume();
        W.Close();
        W.Line("finally");
        W.Open();
        // A pooled string block holds references, so it is cleared when it returns.
        W.Line("global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Return(pooled, " + (Counted ? "true" : "false") + ");");
        W.Close();
    }
}

/// <summary>Describes how one enumerable sequence family differs from the shared owner template.</summary>
internal sealed class EnumerableOwnerSpec
{
    /// <summary>Emits the binary write of a non-null sequence value named <c>value</c> on <c>operationState</c>.</summary>
    internal Action<EnumerableOwnerContext> WriteBody { get; }

    /// <summary>Emits the binary read after a non-negative <c>length</c> and before the exit label.</summary>
    internal Action<EnumerableOwnerContext> ReadBody { get; }

    /// <summary>The statement that appends the staged JSON element <c>item</c> to <c>value</c>.</summary>
    internal string JsonAdd { get; }

    /// <summary>Emits the JSON destination preparation in place of the default create-or-clear, when the sequence is staged.</summary>
    internal Action<EnumerableOwnerContext>? JsonBefore { get; }

    /// <summary>Emits the statements that publish the staged JSON elements after the element loop.</summary>
    internal Action<EnumerableOwnerContext>? JsonAfter { get; }

    /// <summary>Whether the sequence exposes a count that short-circuits an empty JSON array.</summary>
    internal bool JsonCount { get; }

    internal EnumerableOwnerSpec(Action<EnumerableOwnerContext> writeBody, Action<EnumerableOwnerContext> readBody, string jsonAdd,
        Action<EnumerableOwnerContext>? jsonBefore = null, Action<EnumerableOwnerContext>? jsonAfter = null, bool jsonCount = true)
    {
        JsonCount = jsonCount;
        WriteBody = writeBody;
        ReadBody = readBody;
        JsonAdd = jsonAdd;
        JsonBefore = jsonBefore;
        JsonAfter = jsonAfter;
    }
}

/// <summary>Renders the shared binary and JSON formatter owner of a counted enumerable sequence of raw scalars.</summary>
/// <remarks>The wire is the element count followed by the raw elements in enumeration order. The JSON roots enumerate the
/// sequence into an array, and read by clearing or creating the sequence and appending each element through the shared
/// scalar owner. The binary algorithms are supplied by the family through <see cref="EnumerableOwnerSpec"/>.</remarks>
internal static class EnumerableOwnerTemplate
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <param name="spec">The family algorithms.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations, EnumerableOwnerSpec spec)
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
        string marshal = w.A("MemoryMarshal");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        PayloadRenderModel? payload = fact.Element;
        // A packable element is spelled by its global name and expanded in place by the element's record body.
        string scalar = payload != null ? payload.GlobalName : fact.Scalar!;
        string readable = ReadableTypeName.Of(scalar);
        string sequence = fact.DeclaredType;
        // The closed owner identity derives from the declared sequence, so the nested static calls agree with the model.
        string type = ReadableTypeName.Of(GeneratedNames.PayloadKey(sequence));
        string roots = "LuminRoots_" + type;
        string elementOwner = payload != null ? "global::LuminPack." + payload.Owner : "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_" + type;
        // A string element needs the profile, so its binary roots are generic in it.
        bool text = scalar == "string" || payload != null;
        string minimum = text ? "1" : memory + ".SizeOf<" + scalar + ">()";
        var context = new EnumerableOwnerContext(w, sequence, scalar, memory, marshal, payload);

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
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWrite" + (text ? "<" + utf16 + ">" : "") + "(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite" + (text ? "<TLuminProfile>" : "") + "(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWrite" + (text ? "<TLuminProfile>" : "") + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    " + sequence + " input)" + (text ? "" : ""));
                    if (text)
                        w.Line("    where TLuminProfile : struct, " + options);
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
                    w.Line("    in " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite" + (text ? "<" + utf16 + ">" : "") + "(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWrite" + (text ? "<TOptions>" : "") + "(ref state, value);");
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
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootRead" + (text ? "<" + utf16 + ">" : "") + "(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead" + (text ? "<TLuminProfile>" : "") + "(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootRead" + (text ? "<TLuminProfile>" : "") + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    if (text)
                        w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    if (payload != null)
                        w.Line("ref var operationState = ref reader;");
                    else
                    {
                        w.Line(reader + " operationState = default;");
                        w.Line("reader.CopyTo(ref operationState);");
                    }
                    w.Line("int length = operationState.ReadLength(" + minimum + ");");
                    w.Line("if (length < 0)");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    spec.ReadBody(context);
                    w.Blank();
                    w.Line("operationComplete_RootRead:");
                    w.Indented(() => w.Line(";"));
                    if (payload == null)
                        w.Line("operationState.CopyTo(ref reader);");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead" + (text ? "<" + utf16 + ">" : "") + "(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootRead" + (text ? "<TOptions>" : "") + "(ref state, ref value);");
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
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value == null)");
                    w.Open();
                    w.Line("writer.WriteNull<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteArrayStart<TLuminProfile>();");
                    if (spec.JsonCount)
                    {
                        w.Line("if (value.Count == 0)");
                        w.Open();
                        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                        w.Line("return;");
                        w.Close();
                    }
                    w.Blank();
                    w.Line("bool isFirst = true;");
                    w.Line("foreach (var item in value)");
                    w.Open();
                    w.Line("if (!isFirst)");
                    w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("else");
                    w.Line("    isFirst = false;");
                    w.Line("writer.SetFirstElement(true);");
                    w.Line("var temp = item;");
                    w.Line(elementOwner + ".Write<TLuminProfile>(ref writer, temp);");
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
                    w.Line("    in " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + sequence + " value)");
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
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Blank();
                    if (spec.JsonBefore != null)
                        spec.JsonBefore(context);
                    else
                    {
                        w.Line("if (value is null)");
                        w.Indented(() => w.Line("value = new " + sequence + "();"));
                        w.Line("else");
                        w.Indented(() => w.Line("value.Clear();"));
                    }
                    w.Blank();
                    w.Line("while (reader.Read<TLuminProfile>())");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                    w.Line("    continue;");
                    w.Line(scalar + " item = default !;");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    w.Line(spec.JsonAdd);
                    w.Close();
                    spec.JsonAfter?.Invoke(context);
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + sequence + " value)");
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
                    w.Line("    ref " + sequence + " value)");
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
