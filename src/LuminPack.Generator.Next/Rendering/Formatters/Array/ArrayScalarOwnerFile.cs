using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary formatter owner of a closed scalar array.</summary>
/// <remarks>The members mirror the legacy formatter generator's array roots and extension methods. The JSON element calls
/// transfer through the shared scalar owner, which is published with this file.</remarks>
internal static class ArrayScalarOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed collection and its raw scalar element.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
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

        string scalar = fact.Scalar!;
        bool nullable = GenericIdentity.NullableScalar(scalar) != null;
        string collection = fact.DeclaredType;
        string readable = ReadableTypeName.Of(GeneratedNames.PayloadKey(collection));
        string elementOwner = "global::LuminPack." + GeneratedNames.Owner(extension, scalar);

        return FormatterOwnerFile.Render(w, operations, "internal static partial class LuminRoots_" + readable, "internal static partial class Binary_" + extension + "_" + readable,
            "internal static partial class " + extension, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + collection + " input) => LuminRoots_" + readable + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + readable + "<TLuminProfile>(scoped ref " + writer + " writer, scoped in " + collection + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeWithCompress(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + collection + " input) => LuminRoots_" + readable + ".RootWriteCompressed(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeWithCompress<TLuminProfile>(scoped ref " + writer + " writer, scoped in " + collection + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => SerializeWithCompress(ref writer, in input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_" + readable + "(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + collection + " value)");
                    w.Open();
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("evaluator.Add(sizeof(int));");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("evaluator.Add(checked(sizeof(int) + value.Length * " + memory + ".SizeOf<" + scalar + ">()));");
                    w.Close();
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref " + collection + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_" + readable + "(ref evaluator, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWrite(scoped ref " + writer + " writer, " + collection + " input)");
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("writer.EnsureAdditionalCapacity(4);");
                    w.Line("writer.Write(-1);");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    if (nullable)
                    {
                        w.Line("var payload = value.AsSpan();");
                        w.Line("int bytes = " + writer + ".Size<" + scalar + ">(value.Length, true, false);");
                        w.Line("writer.AdvanceSafe(" + writer + ".Write<" + scalar +
                            ">(writer.Reserve(writer.Position, bytes), payload, value.Length, true, false));");
                    }
                    else
                        w.Line("writer.Write<" + scalar + ">(value.AsSpan(), value.Length);");
                    w.Close();
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWriteCompressed(scoped ref " + writer + " writer, " + collection + " input)");
                    w.Open();
                    if (nullable)
                        w.Line("RootWrite(ref writer, input);");
                    else
                    {
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
                    }
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + collection + " value) => " + serializer + ".LuminRoots_" + readable + ".RootWrite(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_" + readable + ".RootWrite(ref state, value);");
                    w.Blank();
                    break;
                case FormatterSection.Extensions:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("public static void WriteValueWithCompress(");
                    w.Line("    ref this " + writer + " writer,");
                    w.Line("    scoped in " + collection + " value) => " + serializer + ".SerializeWithCompress(ref writer, in value);");
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
                    w.Line("    scoped ref " + collection + " value) => LuminRoots_" + readable + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + readable + "<TLuminProfile>(scoped ref " + reader + " reader, scoped ref " + collection + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeWithCompress(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + collection + " value) => LuminRoots_" + readable + ".RootReadCompressed(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeWithCompress<TLuminProfile>(scoped ref " + reader + " reader, scoped ref " + collection + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => DeserializeWithCompress(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootRead(scoped ref " + reader + " reader, scoped ref " + collection + " value)");
                    w.Open();
                    w.Line("int length = reader.ReadLength(" + memory + ".SizeOf<" + scalar + ">());");
                    w.Line("if (length < 0)");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("if (length == 0)");
                    w.Open();
                    w.Line("value = global::System.Array.Empty<" + scalar + ">();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("if (value is null || value.Length != length)");
                    w.Line("    value = " + w.A("LuminPackMarshal") + ".AllocateUninitializedArray<" + scalar + ">(length);");
                    w.Line("global::System.Span<" + scalar + "> payload = " + marshal + ".CreateSpan<" + scalar + ">(ref " + memory + ".As<byte, " + scalar + ">(ref reader.Cursor), 0);");
                    w.Line("payload = value.AsSpan();");
                    w.Line("if (length != 0)");
                    w.Line("    reader.Read(");
                    w.Line("        ref " + memory + ".As<" + scalar + ", byte>(ref " + marshal + ".GetReference(payload)),");
                    w.Line("        unchecked(length * " + memory + ".SizeOf<" + scalar + ">()));");
                    w.Close();
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootReadCompressed(scoped ref " + reader + " reader, scoped ref " + collection + " value)");
                    w.Open();
                    if (nullable)
                        w.Line("RootRead(ref reader, ref value);");
                    else
                    {
                        w.Line(reader + ".Read(reader.GetSpan().Slice(reader.Position), ref value, 0, true, true, out var length);");
                        w.Line("reader.EnsureReadable(length);");
                        w.Line("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, unchecked((nint)(uint)((length))));");
                    }
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + collection + " value) => " + serializer + ".LuminRoots_" + readable + ".RootRead(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_" + readable + ".RootRead(ref state, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Extensions:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("public static void ReadValueWithCompress(");
                    w.Line("    ref this " + reader + " reader,");
                    w.Line("    scoped ref " + collection + " value) => " + serializer + ".DeserializeWithCompress(ref reader, ref value);");
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
                    w.Line("    scoped in " + collection + " input) => LuminRoots_" + readable + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + readable + "<TLuminProfile>(scoped ref " + jsonWriter + " writer, scoped in " + collection + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(scoped ref " + jsonWriter + " writer, " + collection + " input)");
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
                    w.Line(elementOwner + ".Write<TLuminProfile>(ref writer, item);");
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
                    w.Line("    in " + collection + " value) => " + serializer + ".LuminRoots_" + readable + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in " + collection + " value)");
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
                    w.Line("    scoped ref " + collection + " value) => LuminRoots_" + readable + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + readable + "<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref " + collection + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_" + readable + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line("internal static void RootReadJson<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref " + collection + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Line("if (value is not null && value.GetType() == typeof(" + collection + ") && reader.CountArrayElements<TLuminProfile>() == value.Length)");
                    w.Open();
                    w.Line("for (int i = 0; i < value.Length; i++)");
                    w.Open();
                    w.Line("reader.Read<TLuminProfile>();");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref value[i]);");
                    w.Close();
                    w.Blank();
                    w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line(collection + "? buffer = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(4);");
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
                    w.Line("var newBuffer = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(newCapacity);");
                    w.Line("buffer.AsSpan(0, count).CopyTo(newBuffer);");
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(buffer, clearArray: false);");
                    w.Line("buffer = newBuffer;");
                    w.Line("capacity = newBuffer.Length;");
                    w.Close();
                    w.Blank();
                    w.Line(scalar + " item = default !;");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    w.Line("buffer[count++] = item;");
                    w.Close();
                    w.Blank();
                    w.Line("value = buffer.AsSpan(0, count).ToArray();");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("if (buffer != null)");
                    w.Line("    global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(buffer, clearArray: false);");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + collection + " value)");
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + ".LuminRoots_" + readable + ".RootReadJson<" + utf16 + ">(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + jsonReader + " state, ref " + collection + " value)");
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
