using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>ImmutableArray&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy formatter generator's immutable-array roots, extension methods and evaluator
/// template. A default array is the null record; any other array transfers its storage span, and the JSON roots transfer
/// their elements through the shared scalar owner. A Boolean element reads through its scalar owner, while every other
/// scalar reads its raw storage.</remarks>
internal static class ImmutableArrayOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
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
        string scalar = fact.Scalar!;
        string readable = ReadableTypeName.Of(scalar);
        string sequence = fact.DeclaredType;
        string roots = "LuminRoots_ImmutableArrayOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_ImmutableArrayOf" + readable;
        // A Boolean element validates through its scalar owner, so its read root takes the operation profile.
        bool ownerRead = scalar == "bool";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ImmutableArrayOf" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWrite(ref writer, in input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ImmutableArrayOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite(ref writer, in input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_ImmutableArrayOf" + readable + "(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Open();
                    w.Line("evaluator.Add(sizeof(int));");
                    w.Line("if (value.IsDefault)");
                    w.Line("    return;");
                    w.Line("evaluator.Add(checked(value.Length * " + memory + ".SizeOf<" + scalar + ">()));");
                    w.Close();
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_ImmutableArrayOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_ImmutableArrayOf" + readable +
                        "(ref evaluator, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWrite(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value.IsDefault)");
                    w.Open();
                    w.Line("writer.EnsureAdditionalCapacity(4);");
                    w.Line(memory + ".WriteUnaligned(ref writer.Cursor, " + code + ".NullCollection);");
                    w.Line("writer.Advance(4);");
                    w.Line("return;");
                    w.Close();
                    w.Line("global::System.ReadOnlySpan<" + scalar + "> items = value.AsSpan();");
                    w.Line("writer.Write<" + scalar + ">(items, items.Length);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite(ref state, in value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWrite(ref state, in value);");
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
                    w.Line("internal static void Deserialize_ImmutableArrayOf" + readable + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootRead" + (ownerRead ? "<" + utf16 + ">" : string.Empty) + "(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_ImmutableArrayOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead" +
                        (ownerRead ? "<TLuminProfile>" : string.Empty) + "(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootRead" + (ownerRead ? "<TLuminProfile>" : string.Empty) + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    if (ownerRead)
                        w.Line("    where TLuminProfile : struct, " + options);
                    if (!ownerRead && set.FindFormatter(sequence) is { } formatter && formatter.Supports("ReadRawImmutableArray"))
                    {
                        string candidate = formatter.Supports("ReuseRawImmutableArray") ? "value" : string.Empty;
                        w.Line("    => value = reader.ReadRawImmutableArray<" + scalar + ">(" + candidate + ");");
                        w.Blank();
                        break;
                    }
                    w.Open();
                    w.Line(reader + " operationState = default;");
                    w.Line("reader.CopyTo(ref operationState);");
                    w.Line(scalar + "[]? array = null;");
                    w.Line("if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + scalar + ">())");
                    w.Open();
                    w.Line(reader + ".Read(");
                    w.Indented(() =>
                    {
                        w.Line("operationState.GetSpan().Slice(operationState.Position),");
                        w.Line("ref array!,");
                        w.Line("0,");
                        w.Line("true,");
                        w.Line("false,");
                        w.Line("out var readOffset);");
                    });
                    w.Line("operationState.EnsureReadable(readOffset);");
                    w.Line("operationState.Cursor = ref " + memory + ".Add(ref operationState.Cursor, unchecked((nint)(uint)((readOffset))));");
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("if (!operationState.PeekCollectionHeader(out var length))");
                    w.Open();
                    w.Line("array = null;");
                    w.Line("operationState.EnsureReadable(4);");
                    w.Line("operationState.Cursor = ref " + memory + ".Add(ref operationState.Cursor, 4);");
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("operationState.EnsureReadable(4);");
                    w.Line("operationState.Cursor = ref " + memory + ".Add(ref operationState.Cursor, 4);");
                    w.Line("if (length is 0)");
                    w.Open();
                    w.Line("array = global::System.Array.Empty<" + scalar + ">();");
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("array = " + packMarshal + ".AllocateUninitializedArray<" + scalar + ">(length);");
                    w.Line("ref var first = ref " + packMarshal + ".GetNotNullArrayReference(array!);");
                    w.Line("int i = 0;");
                    w.Line("do");
                    w.Open();
                    if (ownerRead)
                    {
                        w.Line(elementOwner + ".Read<TLuminProfile>(");
                        w.Indented(() =>
                        {
                            w.Line("ref operationState,");
                            w.Line("ref " + memory + ".Add(ref first, (nint)(uint)i)!);");
                        });
                    }
                    else
                        w.Line("operationState.Read(ref " + memory + ".Add(ref first, (nint)(uint)i)!);");
                    w.Close();
                    w.Line("while (++i < length);");
                    w.Close();
                    w.Close();
                    w.Close();
                    w.Blank();
                    w.Line("if (array is null)");
                    w.Open();
                    w.Line("value = default;");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    w.Line("if (array.Length == 0)");
                    w.Open();
                    w.Line("value = " + sequence + ".Empty;");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    w.Line("value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(array);");
                    w.Blank();
                    w.Line("operationComplete_RootRead:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref reader);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead" + (ownerRead ? "<" + utf16 + ">" : string.Empty) +
                        "(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootRead" +
                        (ownerRead ? "<TOptions>" : string.Empty) + "(ref state, ref value);");
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
                    w.Line("internal static void SerializeJson_ImmutableArrayOf" + readable + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, in input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_ImmutableArrayOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, in input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
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
                    w.Line("var items = value.AsSpan();");
                    w.Line("if (items.IsEmpty)");
                    w.Open();
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("bool isFirst = true;");
                    w.Line("ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(items);");
                    w.Line("int i = 0;");
                    w.Line("do");
                    w.Open();
                    w.Line("var item = " + memory + ".Add(ref first, (nint)(uint)i);");
                    JsonSequenceElementWriter.Emit(w, set, sequence, scalar, elementOwner);
                    w.Close();
                    w.Line("while (++i < items.Length);");
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
                    w.Line("    in " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, in value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWriteJson<TOptions>(ref state, in value);");
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
                    w.Line("internal static void DeserializeJson_ImmutableArrayOf" + readable + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_ImmutableArrayOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
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
                    w.Line("value = " + sequence + ".Empty;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("const int initialCapacity = 16;");
                    w.Line("var buffer = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(initialCapacity);");
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
                    w.Line("var newBuffer = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(buffer.Length * 2);");
                    w.Line("global::System.Array.Copy(buffer, newBuffer, count);");
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(buffer, clearArray: false);");
                    w.Line("buffer = newBuffer;");
                    w.Close();
                    w.Blank();
                    // Framework integer formatters only replace the item through the same scalar read.
                    if (set.FindFormatter(scalar) is { Definition: FormatterDefinitions.Scalar, Underlying: null } &&
                        GenericFormatterReuse.Real(scalar) is "int" or "long")
                        w.Line(scalar + " item = reader.Read<" + scalar + ", TLuminProfile>();");
                    else
                    {
                        w.Line(scalar + " item = default !;");
                        w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    }
                    w.Line("buffer[count++] = item;");
                    w.Close();
                    w.Line("while (reader.Read<TLuminProfile>());");
                    w.Line("if (count == 0)");
                    w.Open();
                    w.Line("value = " + sequence + ".Empty;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("var finalArray = global::LuminPack.Code.LuminPackMarshal.CreateReadOnlySpan(ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(buffer), count).ToArray();");
                    w.Line("value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(finalArray);");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(buffer, clearArray: false);");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
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
