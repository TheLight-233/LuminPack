using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>ImmutableQueue&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy formatter generator's immutable-queue roots and extension methods. The binary write
/// counts the two-stack traversal and stages the backwards stack through a reversal lease when the queue keeps it reversed, the
/// binary read fills a fresh queue from one pooled block, and both JSON roots transfer their elements through the shared scalar
/// owner.</remarks>
internal static class ImmutableQueueOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
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
        string queue = fact.DeclaredType;
        string factory = "global::System.Collections.Immutable.ImmutableQueue";
        string stack = "global::System.Collections.Immutable.ImmutableStack<" + scalar + ">";
        string roots = "LuminRoots_ImmutableQueueOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_ImmutableQueueOf" + readable;
        bool boolean = scalar == "bool";
        string firstAlias = "value2", secondAlias = "value3", thirdAlias = "value4", reversedAlias = "value5", checkedAlias = "value6";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ImmutableQueueOf" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("        => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ImmutableQueueOf" + readable + "<TLuminProfile>(");
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
                    w.Line(writer + " operationStateFallback = default;");
                    w.Line("writer.CopyTo(ref operationState);");
                    w.Line("var value = input;");
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("operationState.EnsureAdditionalCapacity(4);");
                    w.Line(memory + ".WriteUnaligned(ref operationState.Cursor, " + code + ".NullCollection);");
                    w.Line("operationState.Advance(4);");
                    w.Line("goto operationComplete_RootWrite;");
                    w.Close();
                    w.Blank();
                    var traversal = new global::System.Collections.Generic.List<string>();
                    ImmutableQueueTraversal.Write(traversal, scalar, "value", w, firstAlias, secondAlias, thirdAlias,
                        reversedAlias, checkedAlias);
                    foreach (string line in traversal)
                        w.Line(line);
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
                    w.Line("internal static void Deserialize_ImmutableQueueOf" + readable + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("        => " + roots + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_ImmutableQueueOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootRead(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Open();
                    w.Line("int length = reader.ReadLength(" + memory + ".SizeOf<" + scalar + ">());");
                    w.Line("if (length < 0)");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Line("if (length == 0)");
                    w.Open();
                    w.Line("value = " + queue + ".Empty;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("var pooled = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(length);");
                    w.Line("try");
                    w.Open();
                    w.Line("global::System.Span<" + scalar + "> items = pooled.AsSpan(0, length);");
                    w.Line("if (length != 0)");
                    w.Indented(() =>
                    {
                        w.Line("reader.Read(");
                        w.Indented(() =>
                        {
                            w.Line("ref " + memory + ".As<" + scalar + ", byte>(ref " + marshal + ".GetReference(items)),");
                            w.Line("unchecked(length * " + memory + ".SizeOf<" + scalar + ">()));");
                        });
                    });
                    w.Line(set.FindFormatter(queue)?.Supports("ImmutableSpan") == true ?
                        "value = " + factory + ".Create(new global::System.ReadOnlySpan<" + scalar + ">(pooled, 0, length));" :
                        "value = " + factory + ".CreateRange(new global::System.ArraySegment<" + scalar + ">(pooled, 0, length));");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(pooled, false);");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + queue + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + queue + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootRead(ref state, ref value);");
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
                    w.Line("internal static void SerializeJson_ImmutableQueueOf" + readable + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_ImmutableQueueOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + queue + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
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
                    w.Line("bool isFirst = true;");
                    w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetImmutableQueueTraversal(value, out var forwards, out var backwards, out var reverse))");
                    w.Open();
                    w.Line("foreach (var item in forwards!)");
                    EmitJsonElement("item", "temp");
                    w.Line("if (reverse)");
                    w.Indented(() => w.Line("WriteReversed(ref writer, backwards!, ref isFirst);"));
                    w.Line("else");
                    w.Open();
                    w.Line("foreach (var item2 in backwards!)");
                    EmitJsonElement("item2", "temp2");
                    w.Close();
                    w.Close();
                    w.Line("else");
                    w.Open();
                    w.Line("foreach (var item3 in value)");
                    EmitJsonElement("item3", "temp3");
                    w.Close();
                    w.Blank();
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("static void WriteReversed(ref " + jsonWriter + " writer2, " + stack + " stack, ref bool isFirst2)");
                    w.Open();
                    w.Line("var items = global::LuminPack.Code.LuminPackCollectionAccess.RentImmutableQueueReversal(stack, out int count);");
                    w.Line("try");
                    w.Open();
                    w.Line("foreach (var item4 in new global::System.ReadOnlySpan<" + scalar + ">(items, 0, count))");
                    w.Open();
                    w.Line("if (!isFirst2)");
                    w.Line("    writer2.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("else");
                    w.Line("    isFirst2 = false;");
                    w.Line("writer2.SetFirstElement(true);");
                    w.Line("var temp4 = item4;");
                    w.Line(elementOwner + ".Write<TLuminProfile>(ref writer2, temp4);");
                    w.Close();
                    w.Close();
                    w.Line("finally { global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(items, " +
                        "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + scalar + ">()); }");
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

        void EmitJsonElement(string variable, string temp)
        {
            w.Open();
            w.Line("if (!isFirst)");
            w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
            w.Line("else");
            w.Line("    isFirst = false;");
            w.Line("writer.SetFirstElement(true);");
            w.Line("var " + temp + " = " + variable + ";");
            w.Line(elementOwner + ".Write<TLuminProfile>(ref writer, " + temp + ");");
            w.Close();
        }

        void EmitJsonRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_ImmutableQueueOf" + readable + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_ImmutableQueueOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + queue + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
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
                    w.Line("if (!reader.Read<TLuminProfile>() || reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Open();
                    w.Line("value = " + queue + ".Empty;");
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
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(buffer, false);");
                    w.Line("buffer = newBuffer;");
                    w.Close();
                    w.Line(scalar + " item = default !;");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    w.Line("buffer[count++] = item;");
                    w.Close();
                    w.Line("while (reader.Read<TLuminProfile>());");
                    w.Line("if (count == 0)");
                    w.Open();
                    w.Line("value = " + queue + ".Empty;");
                    w.Line("return;");
                    w.Close();
                    w.Line(set.FindFormatter(queue)?.Supports("ImmutableSpan") == true ?
                        "value = " + factory + ".Create(new global::System.ReadOnlySpan<" + scalar + ">(buffer, 0, count));" :
                        "value = " + factory + ".CreateRange(new global::System.ArraySegment<" + scalar + ">(buffer, 0, count));");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(buffer, false);");
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
