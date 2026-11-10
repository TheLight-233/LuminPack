using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary formatter owner of <c>int[]</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's array roots and extension methods. The JSON element calls
/// transfer through the shared scalar owner, which is published with this file.</remarks>
internal static class IntArrayOwnerFile
{
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
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_IntArray(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in int[] input) => LuminRoots_IntArray.RootWrite(ref writer, input);");
        w.Blank();

        w.Line("internal static partial class LuminRoots_IntArray");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite(scoped ref " + writer + " writer, int[] input)");
        w.Open();
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line("writer.Write(-1);");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.Write<int>(value.AsSpan(), value.Length);");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootRead(scoped ref " + reader + " reader, scoped ref int[] value)");
        w.Open();
        w.Line("int length = reader.ReadLength(" + memory + ".SizeOf<int>());");
        w.Line("if (length < 0)");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::System.Array.Empty<int>();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("if (value is null || value.Length != length)");
        w.Line("    value = " + w.A("LuminPackMarshal") + ".AllocateUninitializedArray<int>(length);");
        w.Line("global::System.Span<int> payload = " + marshal + ".CreateSpan<int>(ref " + memory + ".As<byte, int>(ref reader.Cursor), 0);");
        w.Line("payload = value.AsSpan();");
        w.Line("if (length != 0)");
        w.Line("    reader.Read(");
        w.Line("        ref " + memory + ".As<int, byte>(ref " + marshal + ".GetReference(payload)),");
        w.Line("        unchecked(length * " + memory + ".SizeOf<int>()));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWriteCompressed(scoped ref " + writer + " writer, int[] input)");
        w.Open();
        w.Line("var value = input;");
        w.Line(writer + ".Write(");
        w.Line("    writer.Reserve(writer.Position, " + writer + ".Size(value.AsSpan(), (value is null ? -1 : value.Length), true, true)),");
        w.Line("    value.AsSpan(),");
        w.Line("    (value is null ? -1 : value.Length),");
        w.Line("    true,");
        w.Line("    true,");
        w.Line("    out var length);");
        w.Line("writer.Advance(length);");
        w.Line("writer.CheckBuffer();");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootReadCompressed(scoped ref " + reader + " reader, scoped ref int[] value)");
        w.Open();
        w.Line(reader + ".Read(reader.GetSpan().Slice(reader.Position), ref value, 0, true, true, out var length);");
        w.Line("reader.EnsureReadable(length);");
        w.Line("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, unchecked((nint)(uint)((length))));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line("internal static void RootWriteJson<TLuminProfile>(scoped ref " + jsonWriter + " writer, int[] input)");
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
        w.Line("bool isFirst = true;");
        w.Line("foreach (ref var item in value.AsSpan())");
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
        w.Line("internal static void RootReadJson<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref int[] value)");
        w.Line("    where TLuminProfile : struct, " + options);
        w.Open();
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
        w.Line("if (value is not null && value.GetType() == typeof(int[]) && reader.CountArrayElements<TLuminProfile>() == value.Length)");
        w.Open();
        w.Line("for (int i = 0; i < value.Length; i++)");
        w.Open();
        w.Line("reader.Read<TLuminProfile>();");
        w.Line("global::LuminPack.Binary_" + extension + "_Int.Read<TLuminProfile>(ref reader, ref value[i]);");
        w.Close();
        w.Blank();
        w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("int[]? buffer = global::System.Buffers.ArrayPool<int>.Shared.Rent(4);");
        w.Line("int count = 0;");
        w.Line("int capacity = buffer.Length;");
        w.Line("try");
        w.Open();
        w.Line("while (true)");
        w.Open();
        w.Line("if (!reader.Read<TLuminProfile>())");
        w.Line("    break;");
        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
        w.Line("    break;");
        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
        w.Line("    continue;");
        w.Line("if (count >= capacity)");
        w.Open();
        w.Line("int newCapacity = capacity * 2;");
        w.Line("var newBuffer = global::System.Buffers.ArrayPool<int>.Shared.Rent(newCapacity);");
        w.Line("buffer.AsSpan(0, count).CopyTo(newBuffer);");
        w.Line("global::System.Buffers.ArrayPool<int>.Shared.Return(buffer, clearArray: false);");
        w.Line("buffer = newBuffer;");
        w.Line("capacity = newBuffer.Length;");
        w.Close();
        w.Blank();
        w.Line("int item = default !;");
        w.Line("global::LuminPack.Binary_" + extension + "_Int.Read<TLuminProfile>(ref reader, ref item);");
        w.Line("buffer[count++] = item;");
        w.Close();
        w.Blank();
        w.Line("value = buffer.AsSpan(0, count).ToArray();");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("if (buffer != null)");
        w.Line("    global::System.Buffers.ArrayPool<int>.Shared.Return(buffer, clearArray: false);");
        w.Close();
        w.Close();
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_IntArray<TLuminProfile>(scoped ref " + writer + " writer, scoped in int[] input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_IntArray.RootWrite(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_IntArray(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref int[] value) => LuminRoots_IntArray.RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_IntArray<TLuminProfile>(scoped ref " + reader + " reader, scoped ref int[] value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_IntArray.RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeWithCompress(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in int[] input) => LuminRoots_IntArray.RootWriteCompressed(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeWithCompress<TLuminProfile>(scoped ref " + writer + " writer, scoped in int[] input)");
        w.Line("    where TLuminProfile : struct, " + options + " => SerializeWithCompress(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeWithCompress(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref int[] value) => LuminRoots_IntArray.RootReadCompressed(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeWithCompress<TLuminProfile>(scoped ref " + reader + " reader, scoped ref int[] value)");
        w.Line("    where TLuminProfile : struct, " + options + " => DeserializeWithCompress(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_IntArray(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in int[] input) => LuminRoots_IntArray.RootWriteJson<" + utf16 + ">(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_IntArray<TLuminProfile>(scoped ref " + jsonWriter + " writer, scoped in int[] input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_IntArray.RootWriteJson<TLuminProfile>(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_IntArray(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref int[] value) => LuminRoots_IntArray.RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_IntArray<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref int[] value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_IntArray.RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_IntArray(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref int[] value)");
        w.Open();
        w.Line("if (value is null)");
        w.Open();
        w.Line("evaluator.Add(sizeof(int));");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("evaluator.Add(checked(sizeof(int) + value.Length * " + memory + ".SizeOf<int>()));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_IntArray<TLuminProfile>(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref int[] value)");
        w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_IntArray(ref evaluator, ref value);");
        w.Close();
        w.Blank();

        w.Line("internal static partial class Binary_" + extension + "_IntArray");
        w.Open();
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in int[] value) => " + serializer + ".LuminRoots_IntArray.RootWrite(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in int[] value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_IntArray.RootWrite(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Read(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref int[] value) => " + serializer + ".LuminRoots_IntArray.RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref int[] value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_IntArray.RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in int[] value) => " + serializer + ".LuminRoots_IntArray.RootWriteJson<" + utf16 + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in int[] value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_IntArray.RootWriteJson<TOptions>(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Read(this ref " + jsonReader + " state, ref int[] value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_IntArray.RootReadJson<" + utf16 + ">(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + jsonReader + " state, ref int[] value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_IntArray.RootReadJson<TOptions>(ref state, ref value);");
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
        w.Line("    scoped in int[] value) => " + serializer + ".SerializeWithCompress(ref writer, in value);");
        w.Blank();
        w.Line(preserve);
        w.Line(inline);
        w.Line("public static void ReadValueWithCompress(");
        w.Line("    ref this " + reader + " reader,");
        w.Line("    scoped ref int[] value) => " + serializer + ".DeserializeWithCompress(ref reader, ref value);");
        w.Close();
        w.Close();

        return w.Complete(json: false);
    }
}
