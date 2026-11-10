using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Collections.Concurrent.ConcurrentQueue&lt;string&gt;</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's string-element concurrent-queue roots and extension methods. The
/// binary write counts the live enumeration, the binary read fills a fresh queue from pooled storage, and both JSON roots
/// transfer their elements through the shared string owner, which is published with this file. Every root is generic over the
/// operation's profile because the element string record needs it.</remarks>
internal static class ConcurrentQueueStringOwnerFile
{
    private const string Queue = "global::System.Collections.Concurrent.ConcurrentQueue<string>";

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set)
    {
        var w = new CodeWriter(set.AvailableAliases);
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
        string marshal = w.A("MemoryMarshal");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        string element = "global::LuminPack.Binary_" + extension + "_String";
        string owner = "Binary_" + extension + "_ConcurrentQueueOfString";

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ConcurrentQueueOfString(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Queue + " input)");
        w.Line("        => LuminRoots_ConcurrentQueueOfString.RootWrite<" + utf16 + ">(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ConcurrentQueueOfString<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + Queue + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ConcurrentQueueOfString.RootWrite<TLuminProfile>(ref writer, input);");
        w.Blank();

        w.Line("internal static partial class LuminRoots_ConcurrentQueueOfString");
        w.Open();

        w.Line(preserve);
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("internal static void RootWrite<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    " + Queue + " input)");
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
        w.Line("count++;");
        w.Line("var v = item;");
        w.Line("ref readonly string value2 = ref v;");
        w.Line("operationState.Write<TLuminProfile>(value2);");
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
        w.Line("internal static void RootRead<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Queue + " value)");
        w.Line("    where TLuminProfile : struct, " + options);
        w.Open();
        w.Line(reader + " operationState = default;");
        w.Line("reader.CopyTo(ref operationState);");
        w.Line("int length = operationState.ReadLength(1);");
        w.Line("if (length < 0)");
        w.Open();
        w.Line("value = null;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("if (value is null)");
        w.Open();
        w.Line("var pooled = global::System.Buffers.ArrayPool<string>.Shared.Rent(length);");
        w.Line("try");
        w.Open();
        w.Line("ref var first = ref " + marshal + ".GetReference<string>(pooled.AsSpan(0, length));");
        w.Line("for (int i = 0; i < length; i++)");
        w.Open();
        w.Line("string item = default !;");
        w.Line(element + ".Read<TLuminProfile>(ref operationState, ref item);");
        w.Line(memory + ".Add(ref first, unchecked((nint)(uint)(i))) = item;");
        w.Close();
        w.Blank();
        w.Line("value = new " + Queue + "(length == pooled.Length");
        w.Indented(() =>
        {
            w.Line("? (global::System.Collections.Generic.IEnumerable<string>)pooled");
            w.Line(": length == 0");
            w.Indented(() =>
            {
                w.Line("? (global::System.Collections.Generic.IEnumerable<string>)global::System.Array.Empty<string>()");
                w.Line(": new global::System.ArraySegment<string>(pooled, 0, length));");
            });
        });
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<string>.Shared.Return(pooled, true);");
        w.Close();
        w.Blank();
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("value.Clear();");
        w.Line("for (int i2 = 0; i2 < length; i2++)");
        w.Open();
        w.Line("string v = default !;");
        w.Line(element + ".Read<TLuminProfile>(ref operationState, ref v);");
        w.Line("value.Enqueue(v);");
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
        w.Line("    " + Queue + " input)");
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
        w.Line("    scoped ref " + Queue + " value)");
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
        w.Line("value = new " + Queue + "();");
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
        w.Line("string item = default !;");
        w.Line(element + ".Read<TLuminProfile>(ref reader, ref item);");
        w.Line("value.Enqueue(item);");
        w.Close();
        w.Close();
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ConcurrentQueueOfString(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Queue + " value)");
        w.Line("        => LuminRoots_ConcurrentQueueOfString.RootRead<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ConcurrentQueueOfString<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + Queue + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ConcurrentQueueOfString.RootRead<TLuminProfile>(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ConcurrentQueueOfString(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + Queue + " input)");
        w.Line("        => LuminRoots_ConcurrentQueueOfString.RootWriteJson<" + utf16 + ">(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ConcurrentQueueOfString<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + Queue + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ConcurrentQueueOfString.RootWriteJson<TLuminProfile>(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ConcurrentQueueOfString(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + Queue + " value)");
        w.Line("        => LuminRoots_ConcurrentQueueOfString.RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ConcurrentQueueOfString<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + Queue + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_ConcurrentQueueOfString.RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + owner);
        w.Open();
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + Queue + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ConcurrentQueueOfString.RootWrite<" + utf16 + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in " + Queue + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ConcurrentQueueOfString.RootWrite<TOptions>(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Read(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + Queue + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ConcurrentQueueOfString.RootRead<" + utf16 + ">(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + Queue + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ConcurrentQueueOfString.RootRead<TOptions>(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + Queue + " value)");
        w.Line("        => " + serializer + ".LuminRoots_ConcurrentQueueOfString.RootWriteJson<" + utf16 + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in " + Queue + " value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_ConcurrentQueueOfString.RootWriteJson<TOptions>(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + Queue + " value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_ConcurrentQueueOfString.RootReadJson<" + utf16 + ">(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(");
        w.Line("    this ref " + jsonReader + " state,");
        w.Line("    ref " + Queue + " value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_ConcurrentQueueOfString.RootReadJson<TOptions>(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Close();
        w.Close();

        return w.Complete(json: false);
    }
}
