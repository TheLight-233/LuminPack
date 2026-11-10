using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Collections.Immutable.ImmutableArray&lt;string&gt;</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's string-element immutable-array roots and extension methods. A
/// default array is the null record; any other array transfers its storage span, a reference-containing array reuses the
/// elements the target already holds and reads the rest, and the JSON roots transfer their elements through the shared string
/// owner, which is published with this file. The roots are generic over the operation's profile because the element string
/// record needs it.</remarks>
internal static class ImmutableArrayStringOwnerFile
{
    private const string Array = "global::System.Collections.Immutable.ImmutableArray<string>";

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set)
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
        string element = "global::LuminPack.Binary_" + extension + "_String";
        string owner = "Binary_" + extension + "_ImmutableArrayOfString";

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ImmutableArrayOfString(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("        => LuminRoots_ImmutableArrayOfString.RootWrite<" + utf16 + ">(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ImmutableArrayOfString<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfString.RootWrite<TLuminProfile>(ref writer, in input);");
        w.Blank();

        w.Line("internal static partial class LuminRoots_ImmutableArrayOfString");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("    where TLuminProfile : struct, " + options);
        w.Open();
        w.Line("var value = input;");
        w.Line("if (value.IsDefault)");
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line(memory + ".WriteUnaligned(ref writer.Cursor, " + code + ".NullCollection);");
        w.Line("writer.Advance(4);");
        w.Line("return;");
        w.Close();
        w.Line("global::System.ReadOnlySpan<string> items = value.AsSpan();");
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line("writer.Write(items.Length);");
        w.Line("writer.WriteStrings<TLuminProfile>(items);");
        w.Line("writer.CheckBuffer();");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("internal static void RootRead<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("    where TLuminProfile : struct, " + options);
        w.Open();
        w.Line(reader + " operationState = default;");
        w.Line("reader.CopyTo(ref operationState);");
        w.Line("string[]? array = null;");
        w.Line("if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<string>())");
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
        w.Line("array = global::System.Array.Empty<string>();");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("array = " + packMarshal + ".AllocateUninitializedArray<string>(length);");
        w.Line("if (!value.IsDefault && value.Length != 0)");
        w.Open();
        w.Line("value.AsSpan().Slice(0, global::System.Math.Min(value.Length, length)).CopyTo(array);");
        w.Close();
        w.Blank();
        w.Line("operationState.ReadStrings<TLuminProfile>(array.AsSpan());");
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
        w.Line(element + ".Write<TLuminProfile>(ref writer, temp);");
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
        w.Line("var buffer = global::System.Buffers.ArrayPool<string>.Shared.Rent(initialCapacity);");
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
        w.Line("var newBuffer = global::System.Buffers.ArrayPool<string>.Shared.Rent(buffer.Length * 2);");
        w.Line("global::System.Array.Copy(buffer, newBuffer, count);");
        w.Line("global::System.Buffers.ArrayPool<string>.Shared.Return(buffer, clearArray: true);");
        w.Line("buffer = newBuffer;");
        w.Close();
        w.Blank();
        w.Line("string item = default !;");
        w.Line(element + ".Read<TLuminProfile>(ref reader, ref item);");
        w.Line("buffer[count++] = item;");
        w.Close();
        w.Line("while (reader.Read<TLuminProfile>());");
        w.Line("if (count == 0)");
        w.Open();
        w.Line("value = " + Array + ".Empty;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var finalArray = new string[count];");
        w.Line("global::System.Array.Copy(buffer, finalArray, count);");
        w.Line("value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(finalArray);");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<string>.Shared.Return(buffer, clearArray: true);");
        w.Close();
        w.Close();
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableArrayOfString(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("        => LuminRoots_ImmutableArrayOfString.RootRead<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableArrayOfString<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfString.RootRead<TLuminProfile>(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableArrayOfString(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("        => LuminRoots_ImmutableArrayOfString.RootWriteJson<" + utf16 + ">(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableArrayOfString<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + Array + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfString.RootWriteJson<TLuminProfile>(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableArrayOfString(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("        => LuminRoots_ImmutableArrayOfString.RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableArrayOfString<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + Array + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ImmutableArrayOfString.RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + owner);
        w.Open();
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ImmutableArrayOfString.RootWrite<" + utf16 + ">(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ImmutableArrayOfString.RootWrite<TOptions>(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Read(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + Array + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ImmutableArrayOfString.RootRead<" + utf16 + ">(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + Array + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ImmutableArrayOfString.RootRead<TOptions>(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ImmutableArrayOfString.RootWriteJson<" + utf16 + ">(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + Array + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ImmutableArrayOfString.RootWriteJson<TOptions>(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + Array + " value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_ImmutableArrayOfString.RootReadJson<" + utf16 + ">(ref state, ref value);");
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
        w.Line(serializer + ".LuminRoots_ImmutableArrayOfString.RootReadJson<TOptions>(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Close();
        w.Close();

        return w.Complete(json: false);
    }
}
