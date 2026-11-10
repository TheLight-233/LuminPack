using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary formatter owner of <c>List&lt;int&gt;</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's list roots and extension methods. The JSON element calls
/// transfer through the shared scalar owner, which is published with this file.</remarks>
internal static class ListOfIntOwnerFile
{
    private const string ListType = "global::System.Collections.Generic.List<int>";

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set)
    {
        var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        string writer = w.A("LuminPackWriter");
        string reader = w.A("LuminPackReader");
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string memory = w.A("Unsafe");
        string marshal = w.A("MemoryMarshal");
        string formatter = w.A("LuminPackMarshal");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ListOfInt(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + ListType + " input) => LuminRoots_ListOfInt.RootWrite(ref writer, input);");
        w.Blank();

        w.Line("internal static partial class LuminRoots_ListOfInt");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite(scoped ref " + writer + " writer, " + ListType + " input)");
        w.Open();
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line("writer.Write(-1);");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.Write<int>(" + formatter + ".GetListSpan(value), value.Count);");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootRead(scoped ref " + reader + " reader, scoped ref " + ListType + " value)");
        w.Open();
        w.Line("int length = reader.ReadLength(" + memory + ".SizeOf<int>());");
        w.Line("if (length < 0)");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("value ??= new " + ListType + "(length);");
        w.Line("global::System.Span<int> payload = " + marshal + ".CreateSpan<int>(ref " + memory + ".As<byte, int>(ref reader.Cursor), 0);");
        w.Line("payload = " + formatter + ".GetListSpan(value, length);");
        w.Line("if (length != 0)");
        w.Line("    reader.Read(");
        w.Line("        ref " + memory + ".As<int, byte>(ref " + marshal + ".GetReference(payload)),");
        w.Line("        unchecked(length * " + memory + ".SizeOf<int>()));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWriteCompressed(scoped ref " + writer + " writer, " + ListType + " input)");
        w.Open();
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.WriteNullCollectionHeader();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var span = " + formatter + ".GetListSpan(" + memory + ".AsRef(in value));");
        w.Line(writer + ".Write(");
        w.Line("    writer.Reserve(writer.Position, " + writer + ".Size(span, span.Length, true, true)),");
        w.Line("    span,");
        w.Line("    span.Length,");
        w.Line("    true,");
        w.Line("    true,");
        w.Line("    out var spanOffset);");
        w.Line("writer.Advance(spanOffset);");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootReadCompressed(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Open();
        w.Line("if (!reader.PeekCollectionHeader(out var length))");
        w.Open();
        w.Line("value = null;");
        w.Line("reader.EnsureReadable(4);");
        w.Line("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, 4);");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("if (value is null)");
        w.Open();
        w.Line("value = new " + ListType + "(length);");
        w.Close();
        w.Blank();
        w.Line("global::System.Span<int> span = " + marshal + ".CreateSpan<int>(ref " + memory + ".As<byte, int>(ref reader.Cursor), 0);");
        w.Line("span = " + formatter + ".GetListSpan(" + memory + ".AsRef(in value), length);");
        w.Line(reader + ".Read(reader.GetSpan().Slice(reader.Position), ref span, 0, true, true, out var spanOffset);");
        w.Line(formatter + ".SetListSize(" + memory + ".AsRef(in value), length);");
        w.Line("reader.EnsureReadable(spanOffset);");
        w.Line("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, unchecked((nint)(uint)((spanOffset))));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootReadFresh(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Open();
        w.Line("int length = reader.ReadLength(" + memory + ".SizeOf<int>());");
        w.Line("if (length < 0)");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var items = reader.CreateFreshListArray<int>(length, out value);");
        w.Line("global::System.Span<int> payload = " + marshal + ".CreateSpan<int>(ref " + memory + ".As<byte, int>(ref reader.Cursor), 0);");
        w.Line("payload = items.AsSpan();");
        w.Line("if (length != 0)");
        w.Line("    reader.Read(");
        w.Line("        ref " + memory + ".As<int, byte>(ref " + marshal + ".GetReference(payload)),");
        w.Line("        unchecked(length * " + memory + ".SizeOf<int>()));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line("internal static void RootWriteJson<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    " + ListType + " input)");
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
        w.Line("if (value.Count == 0)");
        w.Open();
        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var span = " + formatter + ".GetListSpan(value);");
        w.Line("bool isFirst = true;");
        w.Line("foreach (ref var item in span)");
        w.Open();
        w.Line("if (!isFirst)");
        w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
        w.Line("else");
        w.Line("    isFirst = false;");
        w.Line("writer.SetFirstElement(true);");
        w.Line("global::LuminPack.Binary_" + extension + "_Int.Write<TLuminProfile>(ref writer, item);");
        w.Close();
        w.Blank();
        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line("internal static void RootReadJson<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    where TLuminProfile : struct, " + options);
        w.Open();
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
        w.Line("if (value is null || value.GetType() != typeof(" + ListType + "))");
        w.Open();
        w.Line("value = new " + ListType + "(reader.GetArrayCapacityHint<TLuminProfile>(10));");
        w.Close();
        w.Blank();
        w.Line("int count = 0;");
        w.Line("while (reader.Read<TLuminProfile>())");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
        w.Line("    break;");
        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
        w.Line("    continue;");
        w.Line("int item = count < value.Count ? value[count] : default !;");
        w.Line("global::LuminPack.Binary_" + extension + "_Int.Read<TLuminProfile>(ref reader, ref item);");
        w.Line("if (count < value.Count)");
        w.Line("    value[count] = item;");
        w.Line("else");
        w.Line("    value.Add(item);");
        w.Line("count++;");
        w.Close();
        w.Blank();
        w.Line("if (count < value.Count)");
        w.Line("    value.RemoveRange(count, value.Count - count);");
        w.Close();
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ListOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + ListType + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ListOfInt.RootWrite(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ListOfInt(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value) => LuminRoots_ListOfInt.RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ListOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ListOfInt.RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeWithCompress(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + ListType + " input) => LuminRoots_ListOfInt.RootWriteCompressed(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeWithCompress<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + ListType + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => SerializeWithCompress(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeWithCompress(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    => LuminRoots_ListOfInt.RootReadCompressed(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeWithCompress<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => DeserializeWithCompress(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeFresh(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value) => LuminRoots_ListOfInt.RootReadFresh(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeFresh<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => DeserializeFresh(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ListOfInt(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + ListType + " input)");
        w.Line("    => LuminRoots_ListOfInt.RootWriteJson<" + utf16 + ">(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ListOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + ListType + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ListOfInt.RootWriteJson<TLuminProfile>(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ListOfInt(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    => LuminRoots_ListOfInt.RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ListOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ListOfInt.RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_ListOfInt(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Open();
        w.Line("if (value is null)");
        w.Open();
        w.Line("evaluator.Add(sizeof(int));");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("evaluator.Add(sizeof(int));");
        w.Line("evaluator.Add(checked(value.Count * " + memory + ".SizeOf<int>()));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_ListOfInt<TLuminProfile>(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_ListOfInt(ref evaluator, ref value);");
        w.Close();
        w.Blank();

        w.Line("internal static partial class Binary_" + extension + "_ListOfInt");
        w.Open();
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + ListType + " value) => " + serializer + ".LuminRoots_ListOfInt.RootWrite(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + ListType + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ListOfInt.RootWrite(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Read(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + ListType + " value)");
        w.Line("    => " + serializer + ".LuminRoots_ListOfInt.RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + ListType + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ListOfInt.RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + ListType + " value)");
        w.Line("    => " + serializer + ".LuminRoots_ListOfInt.RootWriteJson<" + utf16 + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in " + ListType + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ListOfInt.RootWriteJson<TOptions>(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + ListType + " value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_ListOfInt.RootReadJson<" + utf16 + ">(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + jsonReader + " state, ref " + ListType + " value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_ListOfInt.RootReadJson<TOptions>(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Close();
        w.Close();
        w.Blank();

        w.Line("namespace LuminPack.Generated");
        w.Open();
        w.Line("internal static partial class " + extension);
        w.Open();
        w.Line(preserve);
        w.Line(inline);
        w.Line("public static void WriteValueWithCompress(");
        w.Line("    ref this " + writer + " writer,");
        w.Line("    scoped in " + ListType + " value)");
        w.Line("    => " + serializer + ".SerializeWithCompress(ref writer, in value);");
        w.Blank();
        w.Line(preserve);
        w.Line(inline);
        w.Line("public static void ReadValueWithCompress(");
        w.Line("    ref this " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value)");
        w.Line("    => " + serializer + ".DeserializeWithCompress(ref reader, ref value);");
        w.Blank();
        w.Line(preserve);
        w.Line(inline);
        w.Line("public static void ReadFreshValue(");
        w.Line("    ref this " + reader + " reader,");
        w.Line("    scoped ref " + ListType + " value) => " + serializer + ".DeserializeFresh(ref reader, ref value);");
        w.Close();
        w.Close();

        return w.Complete(json: false);
    }
}
