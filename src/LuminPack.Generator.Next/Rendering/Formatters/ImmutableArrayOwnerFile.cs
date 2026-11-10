using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Collections.Immutable.ImmutableArray&lt;int&gt;</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's immutable-array roots, extension methods and evaluator
/// template. A default array is the null record; any other array transfers its storage span, and the JSON roots transfer
/// their elements through the shared scalar owner, which is published with this file.</remarks>
internal static class ImmutableArrayOwnerFile
{
    private const string Array = "global::System.Collections.Immutable.ImmutableArray<int>";

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set)
    {
        var w = new CodeWriter(set.AvailableAliases);
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
        string owner = "Binary_" + extension + "_ImmutableArrayOfInt";

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ImmutableArrayOfInt(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("        => LuminRoots_ImmutableArrayOfInt.RootWrite(ref writer, in input);");
        w.Blank();

        w.Line("internal static partial class LuminRoots_ImmutableArrayOfInt");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Open();
        w.Line("var value = input;");
        w.Line("if (value.IsDefault)");
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line(memory + ".WriteUnaligned(ref writer.Cursor, " + code + ".NullCollection);");
        w.Line("writer.Advance(4);");
        w.Line("return;");
        w.Close();
        w.Line("global::System.ReadOnlySpan<int> items = value.AsSpan();");
        w.Line("writer.Write<int>(items, items.Length);");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootRead(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Open();
        w.Line(reader + " operationState = default;");
        w.Line("reader.CopyTo(ref operationState);");
        w.Line("int[]? array = null;");
        w.Line("if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<int>())");
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
        w.Line("array = global::System.Array.Empty<int>();");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("array = " + packMarshal + ".AllocateUninitializedArray<int>(length);");
        w.Line("ref var first = ref " + packMarshal + ".GetNotNullArrayReference(array!);");
        w.Line("for (nint i = 0; i < length; i++)");
        w.Open();
        w.Line("operationState.Read(ref " + memory + ".Add(ref first, unchecked(i))!);");
        w.Close();
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
        w.Line("value = " + Array + ".Empty;");
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

        w.Line(preserve);
        w.Line("internal static void RootWriteJson<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + Array + " input)");
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
        w.Line("var temp = item;");
        w.Line("global::LuminPack.Binary_" + extension + "_Int.Write<TLuminProfile>(ref writer, temp);");
        w.Close();
        w.Blank();
        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line("internal static void RootReadJson<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
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
        w.Line("value = " + Array + ".Empty;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("const int initialCapacity = 16;");
        w.Line("var buffer = global::System.Buffers.ArrayPool<int>.Shared.Rent(initialCapacity);");
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
        w.Line("var newBuffer = global::System.Buffers.ArrayPool<int>.Shared.Rent(buffer.Length * 2);");
        w.Line("global::System.Array.Copy(buffer, newBuffer, count);");
        w.Line("global::System.Buffers.ArrayPool<int>.Shared.Return(buffer, clearArray: false);");
        w.Line("buffer = newBuffer;");
        w.Close();
        w.Blank();
        w.Line("int item = default !;");
        w.Line("global::LuminPack.Binary_" + extension + "_Int.Read<TLuminProfile>(ref reader, ref item);");
        w.Line("buffer[count++] = item;");
        w.Close();
        w.Line("while (reader.Read<TLuminProfile>());");
        w.Line("if (count == 0)");
        w.Open();
        w.Line("value = " + Array + ".Empty;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var finalArray = new int[count];");
        w.Line("global::System.Array.Copy(buffer, finalArray, count);");
        w.Line("value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(finalArray);");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<int>.Shared.Return(buffer, clearArray: false);");
        w.Close();
        w.Close();
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ImmutableArrayOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfInt.RootWrite(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableArrayOfInt(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("        => LuminRoots_ImmutableArrayOfInt.RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableArrayOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfInt.RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableArrayOfInt(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("        => LuminRoots_ImmutableArrayOfInt.RootWriteJson<" + utf16 + ">(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableArrayOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfInt.RootWriteJson<TLuminProfile>(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableArrayOfInt(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("        => LuminRoots_ImmutableArrayOfInt.RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableArrayOfInt<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfInt.RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_ImmutableArrayOfInt(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref " + Array + " value)");
        w.Open();
        w.Line("evaluator.Add(sizeof(int));");
        w.Line("if (value.IsDefault)");
        w.Line("    return;");
        w.Line("evaluator.Add(checked(value.Length * " + memory + ".SizeOf<int>()));");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_ImmutableArrayOfInt<TLuminProfile>(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_ImmutableArrayOfInt(ref evaluator, ref value);");
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + owner);
        w.Open();
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ImmutableArrayOfInt.RootWrite(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ImmutableArrayOfInt.RootWrite(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Read(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + Array + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ImmutableArrayOfInt.RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + Array + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ImmutableArrayOfInt.RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ImmutableArrayOfInt.RootWriteJson<" + utf16 + ">(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ImmutableArrayOfInt.RootWriteJson<TOptions>(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + Array + " value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_ImmutableArrayOfInt.RootReadJson<" + utf16 + ">(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(");
        w.Line("    this ref " + jsonReader + " state,");
        w.Line("    ref " + Array + " value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_ImmutableArrayOfInt.RootReadJson<TOptions>(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Close();
        w.Close();

        return w.Complete(json: false);
    }
}
