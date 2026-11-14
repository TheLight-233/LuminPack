using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of a <c>ConcurrentQueue&lt;T&gt;</c> whose element is a packable class.</summary>
/// <remarks>The members mirror the legacy formatter generator's payload-element concurrent-queue roots and extension methods.
/// The binary write counts the live enumeration and expands each element's record body through an isolated state; the binary
/// read frames the length, fills a pooled array and expands each element's record body through the caller's reader; the JSON
/// roots expand the element's object body and read each element through the element's own formatter owner.</remarks>
internal static class ConcurrentQueuePayloadOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The payload-element sequence owner this file publishes.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        // The legacy concurrent-queue roots spell the state types through the imported core namespace.
        const string writer = "LuminPackWriter";
        const string reader = "LuminPackReader";
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string code = w.A("LuminPackCode");
        string memory = w.A("Unsafe");
        string marshal = w.A("LuminPackMarshal");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        string queue = fact.DeclaredType;
        PayloadRenderModel element = fact.Element!;
        string elementType = element.GlobalName;
        string elementOwner = element.Owner;
        string owner = "Binary_" + extension + "_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(queue));
        string readable = ReadableTypeName.Of(GeneratedNames.PayloadKey(queue));

        return FormatterOwnerFile.Render(w, operations, "internal static partial class LuminRoots_" + readable, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("        => LuminRoots_" + readable + ".RootWrite<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootWrite<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWrite<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    " + queue + " input)");
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
                    w.Line("int countPosition = operationState.BeginCollection();");
                    w.Line("int count = 0;");
                    w.Line("foreach (var item in value)");
                    w.Open();
                    var writeBody = new List<string>();
                    int ends = 0, cursors = 0, spans = 0;
                    var names = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
                    string elementExit = "end" + ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ends++;
                    writeBody.Add("operationState.CheckBuffer();");
                    writeBody.Add("count++;");
                    writeBody.Add("var v = item;");
                    BinaryEmitter.AddSequenceElementWrite(writeBody, element, "v", elementExit, "operationState", 0, w, names, ref ends,
                        ref cursors, ref spans);
                    foreach (string line in writeBody)
                        w.Line(line);
                    w.Close();
                    w.Blank();
                    w.Line("operationState.EndCollection(countPosition, count);");
                    w.Line("operationState.CheckBuffer();");
                    w.Blank();
                    w.Line("operationComplete_RootWrite:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref writer);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + queue + " value)");
                    w.Line("        => " + serializer + ".LuminRoots_" + readable + ".RootWrite<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + queue + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_" + readable + ".RootWrite<TOptions>(ref state, value);");
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
                    w.Line("internal static void Deserialize_" + readable + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("        => LuminRoots_" + readable + ".RootRead<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootRead<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootRead<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("int length = reader.ReadLength(1);");
                    w.Line("if (length < 0)");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("var pooled = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(length);");
                    w.Line("try");
                    w.Open();
                    w.Line("if (length != 0)");
                    w.Open();
                    w.Line("ref var first = ref " + marshal + ".GetNotNullArrayReference(pooled);");
                    w.Line("int i = 0;");
                    w.Line("do");
                    w.Open();
                    w.Line(elementType + " item = default !;");
                    var readNames = new Dictionary<string, int>(StringComparer.Ordinal);
                    int readCursors = 0, readStrings = 0, readEnds = 0;
                    var readBody = new List<string>();
                    BinaryEmitter.AddSequenceElementRead(readBody, element, "item", null, w, readNames, ref readCursors, ref readStrings, ref readEnds);
                    foreach (string line in readBody)
                        w.Line(line);
                    w.Line(memory + ".Add(ref first, (nint)(uint)i) = item;");
                    w.Close();
                    w.Line("while (++i < length);");
                    w.Close();
                    w.Blank();
                    w.Line("value = new " + queue + "(length == pooled.Length");
                    w.Indented(() =>
                    {
                        w.Line("? (global::System.Collections.Generic.IEnumerable<" + elementType + ">)pooled");
                        w.Line(": length == 0");
                        w.Indented(() =>
                        {
                            w.Line("? (global::System.Collections.Generic.IEnumerable<" + elementType + ">)global::System.Array.Empty<" + elementType + ">()");
                            w.Line(": new global::System.ArraySegment<" + elementType + ">(pooled, 0, length));");
                        });
                    });
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(pooled, true);");
                    w.Close();
                    w.Blank();
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("value.Clear();");
                    w.Line("if (length == 0)");
                    w.Indented(() => w.Line("return;"));
                    w.Line("int remaining = length;");
                    w.Line("do");
                    w.Open();
                    w.Line(elementType + " v = default !;");
                    var secondBody = new List<string>();
                    BinaryEmitter.AddSequenceElementRead(secondBody, element, "v", null, w, readNames, ref readCursors, ref readStrings, ref readEnds);
                    foreach (string line in secondBody)
                        w.Line(line);
                    w.Line("value.Enqueue(v);");
                    w.Close();
                    w.Line("while (--remaining != 0);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + queue + " value)");
                    w.Line("        => " + serializer + ".LuminRoots_" + readable + ".RootRead<" + utf16 + ">(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + queue + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_" + readable + ".RootRead<TOptions>(ref state, ref value);");
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
                    w.Line("internal static void SerializeJson_" + readable + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("        => LuminRoots_" + readable + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    " + queue + " input)");
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
                    w.Line("if (value.IsEmpty)");
                    w.Open();
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("bool isFirst = true;");
                    w.Line("foreach (var item in value)");
                    w.Open();
                    w.Line("if (!isFirst)");
                    w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("else");
                    w.Line("    isFirst = false;");
                    w.Line("writer.SetFirstElement(true);");
                    foreach (string line in JsonEmitter.SequenceElementWrite(element, "item", "end0", w))
                        w.Line(line);
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + queue + " value)");
                    w.Line("        => " + serializer + ".LuminRoots_" + readable + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + queue + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_" + readable + ".RootWriteJson<TOptions>(ref state, value);");
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
                    w.Line("internal static void DeserializeJson_" + readable + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("        => LuminRoots_" + readable + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("value = new " + queue + "();");
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("value.Clear();");
                    w.Close();
                    w.Blank();
                    w.Line("while (reader.Read<TLuminProfile>())");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                    w.Line("    continue;");
                    w.Line(elementType + " item = default !;");
                    w.Line("global::LuminPack." + elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    w.Line("value.Enqueue(item);");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + queue + " value)");
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + ".LuminRoots_" + readable + ".RootReadJson<" + utf16 + ">(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + jsonReader + " state,");
                    w.Line("    ref " + queue + " value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + ".LuminRoots_" + readable + ".RootReadJson<TOptions>(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }
    }
}
