using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>ConcurrentQueue&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy formatter generator's concurrent-queue roots and extension methods. The binary write
/// counts the live enumeration, the binary read fills a fresh queue from pooled storage, and both JSON roots transfer their
/// elements through the shared scalar owner. A Boolean element reads through its scalar owner, while every other scalar reads
/// its raw storage.</remarks>
internal static class ConcurrentQueueOwnerFile
{
    /// <summary>Returns the statement that empties a reused queue before its ordered refill.</summary>
    /// <param name="set">The required owners and capabilities.</param>
    /// <param name="queue">The closed queue type.</param>
    /// <returns>The drain helper when the referenced Core exposes it, otherwise the framework clear.</returns>
    internal static string ResetLine(SharedFormatterSet set, string queue) =>
        set.FindFormatter(queue)?.Supports("DrainConcurrentQueue") == true
            ? "global::LuminPack.Code.LuminPackCollectionAccess.DrainConcurrentQueue(value);"
            : "value.Clear();";

    /// <summary>Emits queue construction from an already decoded rented array prefix.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The referenced Core capabilities for this owner.</param>
    /// <param name="queue">The closed concurrent queue type name.</param>
    /// <param name="item">The element type name.</param>
    internal static void WriteFreshConstruction(CodeWriter w, SharedFormatterSet set, string queue, string item)
    {
        if (set.FindFormatter(queue)?.Supports("CreateConcurrentQueue") == true)
        {
            w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentQueue(pooled, length);");
            return;
        }

        w.Line("value = new " + queue + "(length == pooled.Length");
        w.Indented(() =>
        {
            w.Line("? (global::System.Collections.Generic.IEnumerable<" + item + ">)pooled");
            w.Line(": length == 0");
            w.Indented(() =>
            {
                w.Line("? (global::System.Collections.Generic.IEnumerable<" + item + ">)global::System.Array.Empty<" + item + ">()");
                w.Line(": new global::System.ArraySegment<" + item + ">(pooled, 0, length));");
            });
        });
    }

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
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
        string scalar = fact.Scalar!;
        string readable = ReadableTypeName.Of(scalar);
        string queue = fact.DeclaredType;
        string roots = "LuminRoots_ConcurrentQueueOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_ConcurrentQueueOf" + readable;
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
                    w.Line("internal static void Serialize_ConcurrentQueueOf" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("        => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ConcurrentQueueOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWrite(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    " + queue + " input)");
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
                    if (ownerRead)
                    {
                        w.Line("ref readonly " + scalar + " value2 = ref v;");
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
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + queue + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + queue + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWrite(ref state, value);");
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
                    w.Line("internal static void Deserialize_ConcurrentQueueOf" + readable + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("        => " + roots + ".RootRead" + (ownerRead ? "<" + utf16 + ">" : string.Empty) + "(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_ConcurrentQueueOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead" +
                        (ownerRead ? "<TLuminProfile>" : string.Empty) + "(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootRead" + (ownerRead ? "<TLuminProfile>" : string.Empty) + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    if (ownerRead)
                        w.Line("    where TLuminProfile : struct, " + options);
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
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("var pooled = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(length);");
                    w.Line("try");
                    w.Open();
                    w.Line("if (length != 0)");
                    w.Indented(() =>
                    {
                        w.Line("operationState.Read(");
                        w.Indented(() =>
                        {
                            w.Line("ref " + memory + ".As<" + scalar + ", byte>(ref " + marshal + ".GetNotNullArrayReference(pooled)),");
                            w.Line("unchecked(length * " + memory + ".SizeOf<" + scalar + ">()));");
                        });
                    });
                    w.Blank();
                    WriteFreshConstruction(w, set, queue, scalar);
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(pooled, false);");
                    w.Close();
                    w.Blank();
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    if (set.FindFormatter(queue)?.Supports("DrainConcurrentQueue") == true)
                        w.Line(ResetLine(set, queue));
                    else if (!ownerRead && set.FindFormatter(queue)?.Supports("ClearConcurrentQueue") == true)
                    {
                        w.Line("if (length is <= 32 or > 4096 || !global::LuminPack.Code.LuminPackCollectionAccess.TryClearConcurrentQueue(value, length))");
                        w.Indented(() => w.Line("value.Clear();"));
                    }
                    else
                        w.Line("value.Clear();");
                    w.Line("if (length == 0)");
                    w.Indented(() => w.Line("goto operationComplete_RootRead;"));
                    w.Line("int remaining = length;");
                    w.Line("do");
                    w.Open();
                    w.Line(scalar + " v = default !;");
                    if (ownerRead)
                        w.Line(elementOwner + ".Read<TLuminProfile>(ref operationState, ref v);");
                    else
                        w.Line("operationState.Read(ref v);");
                    w.Line("value.Enqueue(v);");
                    w.Close();
                    w.Line("while (--remaining != 0);");
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
                    w.Line("    ref " + queue + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead" + (ownerRead ? "<" + utf16 + ">" : string.Empty) +
                        "(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + queue + " value)");
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
                    w.Line("internal static void SerializeJson_ConcurrentQueueOf" + readable + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_ConcurrentQueueOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
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
                    JsonSequenceElementWriter.Emit(w, set, queue, scalar, elementOwner);
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
                    w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + queue + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWriteJson<TOptions>(ref state, value);");
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
                    w.Line("internal static void DeserializeJson_ConcurrentQueueOf" + readable + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_ConcurrentQueueOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
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
                    w.Line(ResetLine(set, queue));
                    w.Close();
                    w.Blank();
                    w.Line("while (reader.Read<TLuminProfile>())");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                    w.Line("    continue;");
                    w.Line(scalar + " item = default !;");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
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
                    w.Line(serializer + "." + roots + ".RootReadJson<" + utf16 + ">(ref state, ref value);");
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
                    w.Line(serializer + "." + roots + ".RootReadJson<TOptions>(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }
    }
}
