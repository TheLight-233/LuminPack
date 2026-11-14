using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>Stack&lt;T&gt;</c>.</summary>
/// <remarks>Binary stores active stack elements bottom first. Unrecognized storage and insufficient output room use the pooled
/// traversal. JSON writes in public pop order and stages the decoded elements before restoring the stack.</remarks>
internal static class StackOwnerFile
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
        // The legacy stack roots spell the state types through the imported core namespace.
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
        string sequence = fact.DeclaredType;
        string roots = "LuminRoots_StackOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_StackOf" + readable;
        var contract = set.FindFormatter(sequence);
        const string access = "global::LuminPack.Code.LuminPackCollectionAccess.";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_StackOf" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_StackOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_StackOf" + readable + "(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Open();
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("evaluator.Add(sizeof(int));");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("evaluator.Add(sizeof(int));");
                    w.Line("evaluator.Add(checked(value.Count * " + memory + ".SizeOf<" + scalar + ">()));");
                    w.Close();
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_StackOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_StackOf" + readable +
                        "(ref evaluator, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
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
                    w.Blank();
                    w.Line("int count = value.Count;");
                    if (contract?.Supports("WriteStackRecord") == true)
                    {
                        w.Line("if (" + access + "TryWriteStackRecord(ref operationState, value, count))");
                        w.Indented(() => w.Line("goto operationComplete_RootWrite;"));
                    }
                    w.Line("int countPosition = operationState.BeginCollection();");
                    w.Line("if (count != 0" + (contract?.Supports("WriteStackStorage") == true ?
                        " && !" + access + "TryWriteStackStorage(ref operationState, value, count)" : "") + ")");
                    w.Indented(() => w.Line("WriteStaged(ref operationState, value, count);"));
                    w.Blank();
                    w.Line("operationState.EndCollection(countPosition, count);");
                    w.Line("operationState.CheckBuffer();");
                    w.Blank();
                    w.Line("operationComplete_RootWrite:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref writer);");
                    w.Blank();
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("static void WriteStaged(scoped ref " + writer + " operationState, " + sequence + " value, int count)");
                    w.Open();
                    w.Line("var buffer = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(count);");
                    w.Line("try");
                    w.Open();
                    w.Line("value.CopyTo(buffer, 0);");
                    // The public enumeration is top first, so the stored block walks the copy from its last slot.
                    w.Line("ref var first = ref " + marshal + ".GetNotNullArrayReference(buffer);");
                    w.Line("int remaining = count;");
                    w.Line("do");
                    w.Open();
                    w.Line("operationState.CheckBuffer();");
                    w.Line("var v = " + memory + ".Add(ref first, (nint)(uint)--remaining);");
                    // A Boolean element writes through a read-only alias, as the legacy element write does.
                    if (scalar == "bool")
                    {
                        w.Line("ref readonly bool value2 = ref v;");
                        w.Line("operationState.Write(in value2);");
                    }
                    else
                        w.Line("operationState.Write(in v);");
                    w.Close();
                    w.Line("while (remaining != 0);");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(");
                    w.Indented(() =>
                    {
                        w.Line("buffer,");
                        w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + scalar + ">());");
                    });
                    w.Close();
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + sequence + " value)");
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
                    w.Line("internal static void Deserialize_StackOf" + readable + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_StackOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
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
                    w.Line("if (value is null)");
                    w.Indented(() => w.Line("value = new " + sequence + "(length);"));
                    w.Line("else");
                    w.Indented(() => w.Line("value.Clear();"));
                    w.Blank();
                    w.Line("if (length == 0" + (contract?.Supports("ReadStackStorage") == true ?
                        " || " + access + "TryReadStackStorage(ref operationState, value, length)" : "") + ")");
                    w.Indented(() => w.Line("goto operationComplete_RootRead;"));
                    w.Line("ReadStaged(ref operationState, value, length);");
                    w.Blank();
                    w.Line("operationComplete_RootRead:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref reader);");
                    w.Blank();
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("static void ReadStaged(scoped ref " + reader + " operationState, " + sequence + " value, int length)");
                    w.Open();
                    w.Line("var pooled = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(length);");
                    w.Line("try");
                    w.Open();
                    w.Line("ref var first = ref " + marshal + ".GetNotNullArrayReference(pooled);");
                    w.Line("operationState.Read(");
                    w.Indented(() =>
                    {
                        w.Line("ref " + memory + ".As<" + scalar + ", byte>(ref first),");
                        w.Line("unchecked(length * " + memory + ".SizeOf<" + scalar + ">()));");
                    });
                    w.Blank();
                    // The stored block is bottom first, so each element is pushed in slot order.
                    w.Line("int index = 0;");
                    w.Line("do");
                    w.Open();
                    w.Line("value.Push(" + memory + ".Add(ref first, (nint)(uint)index));");
                    w.Close();
                    w.Line("while (++index < length);");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(");
                    w.Indented(() =>
                    {
                        w.Line("pooled,");
                        w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + scalar + ">());");
                    });
                    w.Close();
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
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
                    w.Line("internal static void SerializeJson_StackOf" + readable + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_StackOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
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
                    w.Line("if (value.Count == 0)");
                    w.Open();
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    if (contract?.Supports("OrderedIntegerJson") == true && GenericFormatterReuse.Real(scalar) is "int" or "long")
                    {
                        w.Line("if (" + access + "TryWriteIntegerStackJson<TLuminProfile, " + scalar + ">(ref writer, value))");
                        w.Open();
                        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                        w.Line("return;");
                        w.Close();
                    }
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
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + sequence + " value)");
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
                    w.Line("internal static void DeserializeJson_StackOf" + readable + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_StackOf" + readable + "<TLuminProfile>(");
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
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Blank();
                    StackJsonStaging.Emit(w, sequence, scalar, elementOwner, jsonReader, clear: false,
                        bulkRestore: contract?.Supports("RestoreStackFromTop") == true);
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
