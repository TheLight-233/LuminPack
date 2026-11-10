using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>ImmutableStack&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy formatter generator's immutable-stack roots and extension methods. The binary write
/// counts the live enumeration in push order; the binary read takes the pooled block, rebuilds the stack from the last element
/// back, and both JSON roots transfer their elements through the shared scalar owner. A Boolean element writes through a
/// read-only alias, while every scalar reads its raw storage.</remarks>
internal static class ImmutableStackOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact)
    {
        var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        // The legacy immutable-stack roots spell the state types through the imported core namespace.
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
        string scalar = fact.Scalar!;
        string readable = ReadableTypeName.Of(scalar);
        string sequence = fact.DeclaredType;
        string roots = "LuminRoots_ImmutableStackOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_ImmutableStackOf" + readable;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ImmutableStackOf" + readable + "(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("        => " + roots + ".RootWrite(ref writer, input);");
        w.Blank();

        w.Line("internal static partial class " + roots);
        w.Open();

        w.Line(preserve);
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("internal static void RootWrite(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    " + sequence + " input)");
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
        w.Line("operationState.CheckBuffer();");
        w.Line("count++;");
        w.Line("var v = item;");
        // A Boolean element writes through a read-only alias, as the legacy element write does.
        if (scalar == "bool")
        {
            w.Line("ref readonly bool value2 = ref v;");
            w.Line("operationState.Write(in value2);");
        }
        else
            w.Line("operationState.Write(in v);");
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

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootRead(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Open();
        w.Line(reader + " operationState = default;");
        w.Line("reader.CopyTo(ref operationState);");
        w.Line("int length = operationState.ReadLength(" + memory + ".SizeOf<" + scalar + ">());");
        w.Line("if (length < 0)");
        w.Open();
        w.Line("value = null;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + sequence + ".Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Line("var pooled = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(length);");
        w.Line("try");
        w.Open();
        w.Line("global::System.Span<" + scalar + "> items = " + marshal + ".CreateSpan<" + scalar + ">(");
        w.Indented(() =>
        {
            w.Line("ref " + memory + ".As<byte, " + scalar + ">(ref operationState.Cursor),");
            w.Line("0);");
        });
        w.Line("items = pooled.AsSpan(0, length);");
        w.Line("if (length != 0)");
        w.Indented(() =>
        {
            w.Line("operationState.Read(");
            w.Indented(() =>
            {
                w.Line("ref " + memory + ".As<" + scalar + ", byte>(ref " + marshal + ".GetReference(items)),");
                w.Line("unchecked(length * " + memory + ".SizeOf<" + scalar + ">()));");
            });
        });
        w.Blank();
        w.Line("var stack = " + sequence + ".Empty;");
        w.Line("for (int index = length - 1; index >= 0; index--)");
        w.Line("    stack = stack.Push(pooled[index]);");
        w.Line("value = stack;");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(pooled, false);");
        w.Close();
        w.Blank();
        w.Line("operationComplete_RootRead:");
        w.Indented(() => w.Line(";"));
        w.Line("operationState.CopyTo(ref reader);");
        w.Close();
        w.Blank();

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
        w.Line(scalar + " item = default !;");
        w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
        w.Line("buffer[count++] = item;");
        w.Close();
        w.Line("while (reader.Read<TLuminProfile>());");
        w.Line("if (count == 0)");
        w.Open();
        w.Line("value = " + sequence + ".Empty;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var stack = " + sequence + ".Empty;");
        w.Line("for (int i = count - 1; i >= 0; i--)");
        w.Line("    stack = stack.Push(buffer[i]);");
        w.Line("value = stack;");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(buffer, clearArray: false);");
        w.Close();
        w.Close();
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ImmutableStackOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableStackOf" + readable + "(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("        => " + roots + ".RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableStackOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableStackOf" + readable + "(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableStackOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableStackOf" + readable + "(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableStackOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + owner);
        w.Open();
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + sequence + " value)");
        w.Line("        => " + serializer + "." + roots + ".RootWrite(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + sequence + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWrite(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Read(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + sequence + " value)");
        w.Line("        => " + serializer + "." + roots + ".RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + sequence + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootRead(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + sequence + " value)");
        w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + sequence + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWriteJson<TOptions>(ref state, value);");
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
        w.Close();
        w.Close();

        return w.Complete(json: false);
    }
}
