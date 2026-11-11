using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of an <c>ImmutableArray&lt;T&gt;</c> whose element is a packable class.</summary>
/// <remarks>The members mirror the legacy formatter generator's payload-element immutable-array roots and extension methods.
/// A default array is the null record; any other array transfers its storage span, a reference-containing array expands each
/// element's record body in place, and the JSON roots expand the element's object body and read each element through the
/// element's own formatter owner.</remarks>
internal static class ImmutableArrayPayloadOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The payload-element sequence owner this file publishes.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        // The legacy immutable-array roots spell the writer through the imported core namespace and the reader through its alias.
        const string writer = "LuminPackWriter";
        string reader = w.A("LuminPackReader");
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string code = w.A("LuminPackCode");
        string memory = w.A("Unsafe");
        // The legacy immutable-array roots spell the pack marshal through the imported code namespace.
        const string packMarshal = "LuminPackMarshal";
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        string array = fact.DeclaredType;
        PayloadRenderModel element = fact.Element!;
        string elementType = element.GlobalName;
        string elementOwner = element.Owner;
        string readable = ReadableTypeName.Of(GeneratedNames.PayloadKey(array));
        string owner = "Binary_" + extension + "_" + readable;

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
                    w.Line("    scoped in " + array + " input)");
                    w.Line("        => LuminRoots_" + readable + ".RootWrite<" + utf16 + ">(ref writer, in input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + array + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootWrite<TLuminProfile>(ref writer, in input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWrite<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + array + " input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line(writer + " operationState = default;");
                    w.Line("writer.CopyTo(ref operationState);");
                    w.Line("var value = input;");
                    w.Line("if (value.IsDefault)");
                    w.Open();
                    w.Line("operationState.EnsureAdditionalCapacity(4);");
                    w.Line(memory + ".WriteUnaligned(ref operationState.Cursor, " + code + ".NullCollection);");
                    w.Line("operationState.Advance(4);");
                    w.Line("goto operationComplete_RootWrite;");
                    w.Close();
                    w.Line("global::System.ReadOnlySpan<" + elementType + "> items = value.AsSpan();");
                    w.Line("operationState.EnsureAdditionalCapacity(4);");
                    w.Line("operationState.Write(items.Length);");
                    w.Line("for (int i = 0; i < items.Length; i++)");
                    w.Open();
                    w.Line("operationState.CheckBuffer();");
                    var writeBody = new List<string>();
                    int ends = 0, cursors = 0, spans = 0;
                    var names = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
                    string elementExit = "end" + ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ends++;
                    BinaryEmitter.AddSequenceElementWrite(writeBody, element, "items[i]!", elementExit, "operationState", 0, w, names,
                        ref ends, ref cursors, ref spans);
                    foreach (string line in writeBody)
                        w.Line(line);
                    w.Close();
                    w.Blank();
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
                    w.Line("    in " + array + " value)");
                    w.Line("        => " + serializer + ".LuminRoots_" + readable + ".RootWrite<" + utf16 + ">(ref state, in value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + array + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_" + readable + ".RootWrite<TOptions>(ref state, in value);");
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
                    w.Line("    scoped ref " + array + " value)");
                    w.Line("        => LuminRoots_" + readable + ".RootRead<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + array + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootRead<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootRead<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + array + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line(elementType + "[]? array = null;");
                    w.Line("if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">())");
                    w.Open();
                    w.Line(reader + ".Read(reader.GetSpan().Slice(reader.Position), ref array!, 0, true, false, out var readOffset);");
                    w.Line("reader.EnsureReadable(readOffset);");
                    w.Line("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, unchecked((nint)(uint)((readOffset))));");
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("if (!reader.PeekCollectionHeader(out var length))");
                    w.Open();
                    w.Line("array = null;");
                    w.Line("reader.EnsureReadable(4);");
                    w.Line("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, 4);");
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("reader.EnsureReadable(4);");
                    w.Line("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, 4);");
                    w.Line("if (length is 0)");
                    w.Open();
                    w.Line("array = global::System.Array.Empty<" + elementType + ">();");
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("array = " + packMarshal + ".AllocateUninitializedArray<" + elementType + ">(length);");
                    w.Line("ref var first = ref " + packMarshal + ".GetNotNullArrayReference(array!);");
                    w.Line("for (nint i = 0; i < length; i++)");
                    w.Open();
                    var readNames = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1 };
                    int readCursors = 0, readStrings = 0, readEnds = 0;
                    string elementLocal = "value" + (readNames["value"] + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    readNames["value"] = readNames["value"] + 1;
                    var readBody = new List<string>();
                    BinaryEmitter.AddSequenceElementRead(readBody, element, elementLocal,
                        "ref " + elementType + " " + elementLocal + " = ref " + memory + ".Add(ref first, unchecked(i))!;",
                        w, readNames, ref readCursors, ref readStrings, ref readEnds);
                    foreach (string line in readBody)
                        w.Line(line);
                    w.Close();
                    w.Close();
                    w.Close();
                    w.Close();
                    w.Blank();
                    w.Line("if (array is null)");
                    w.Open();
                    w.Line("value = default;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("if (array.Length == 0)");
                    w.Open();
                    w.Line("value = " + array + ".Empty;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(array);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + array + " value)");
                    w.Line("        => " + serializer + ".LuminRoots_" + readable + ".RootRead<" + utf16 + ">(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + array + " value)");
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
                    w.Line("    scoped in " + array + " input)");
                    w.Line("        => LuminRoots_" + readable + ".RootWriteJson<" + utf16 + ">(ref writer, in input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + array + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootWriteJson<TLuminProfile>(ref writer, in input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + array + " input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value.IsDefault)");
                    w.Open();
                    w.Line("writer.WriteNull<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteArrayStart<TLuminProfile>();");
                    w.Line("if (value.Length == 0)");
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
                    w.Line("    in " + array + " value)");
                    w.Line("        => " + serializer + ".LuminRoots_" + readable + ".RootWriteJson<" + utf16 + ">(ref state, in value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + array + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_" + readable + ".RootWriteJson<TOptions>(ref state, in value);");
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
                    w.Line("    scoped ref " + array + " value)");
                    w.Line("        => LuminRoots_" + readable + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + array + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + array + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = default;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Line("if (!reader.Read<TLuminProfile>() || reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Open();
                    w.Line("value = " + array + ".Empty;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("const int initialCapacity = 16;");
                    w.Line("var buffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(initialCapacity);");
                    w.Line("var count = 0;");
                    w.Line("try");
                    w.Open();
                    w.Line("do");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                    w.Line("    continue;");
                    w.Line("if (count >= buffer.Length)");
                    w.Open();
                    w.Line("var newBuffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(buffer.Length * 2);");
                    w.Line("global::System.Array.Copy(buffer, newBuffer, count);");
                    w.Line("global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: true);");
                    w.Line("buffer = newBuffer;");
                    w.Close();
                    w.Blank();
                    w.Line(elementType + " item = default !;");
                    w.Line("global::LuminPack." + elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    w.Line("buffer[count++] = item;");
                    w.Close();
                    w.Line("while (reader.Read<TLuminProfile>());");
                    w.Line("if (count == 0)");
                    w.Open();
                    w.Line("value = " + array + ".Empty;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("var finalArray = new " + elementType + "[count];");
                    w.Line("global::System.Array.Copy(buffer, finalArray, count);");
                    w.Line("value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(finalArray);");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: true);");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + array + " value)");
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
                    w.Line("    ref " + array + " value)");
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
