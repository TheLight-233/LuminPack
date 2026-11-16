using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>ConcurrentBag&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy formatter generator's concurrent-bag roots and extension methods. The binary write
/// tries the validated raw freeze and otherwise writes one leased snapshot, the binary read adds one pooled block, and both JSON
/// roots transfer their elements through the shared scalar owner.</remarks>
internal static class ConcurrentBagOwnerFile
{
    /// <summary>Writes the established bag snapshot with locks released before element formatting.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The available formatter capabilities.</param>
    /// <param name="bag">The closed bag type.</param>
    /// <param name="scalar">The element type spelling.</param>
    /// <param name="owner">The existing element formatter.</param>
    /// <param name="pooledSnapshot">Whether Core exposes the checked snapshot lease.</param>
    internal static void WriteJsonSnapshot(CodeWriter w, SharedFormatterSet set, string bag, string scalar, string owner, bool pooledSnapshot)
    {
        if (pooledSnapshot)
            w.Line("var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentConcurrentBagSnapshot(value, out int count, out bool pooled);");
        else
        {
            w.Line("var snapshot = value.ToArray();");
            w.Line("int count = snapshot.Length;");
            w.Line("bool pooled = false;");
        }
        w.Line("try");
        w.Open();
        w.Line("if (count != 0)");
        w.Open();
        w.Line("bool isFirst = true;");
        w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(snapshot);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("var item = " + w.A("Unsafe") + ".Add(ref first, (nint)(uint)index);");
        JsonSequenceElementWriter.Emit(w, set, bag, scalar, owner);
        w.Close();
        w.Line("while (++index < count);");
        w.Close();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("if (pooled)");
        w.Line("    global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(snapshot, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + scalar + ">());");
        w.Close();
    }

    /// <summary>Adds the original staged values in input order, with optional sizing after the first Add.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="memory">The source-ready Unsafe name.</param>
    /// <param name="presize">Whether the referenced Core offers the exact sizing helper.</param>
    internal static void AddStagedItems(CodeWriter w, string memory, bool presize)
    {
        w.Line("int index = 0;");
        if (presize)
        {
            w.Line("if (length >= 64 && fresh is not null && global::System.Object.ReferenceEquals(value, fresh))");
            w.Open();
            w.Line("value.Add(first);");
            w.Line("global::LuminPack.Code.LuminPackCollectionAccess.PresizeFreshConcurrentBag(fresh, length);");
            w.Line("index = 1;");
            w.Close();
        }
        w.Line("do");
        w.Open();
        w.Line("value.Add(" + memory + ".Add(ref first, (nint)(uint)index));");
        w.Close();
        w.Line("while (++index < length);");
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
        string bag = fact.DeclaredType;
        string roots = "LuminRoots_ConcurrentBagOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_ConcurrentBagOf" + readable;

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ConcurrentBagOf" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + bag + " input)");
                    w.Line("        => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ConcurrentBagOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + bag + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWrite(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    " + bag + " input)");
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
                    if (set.FindFormatter(bag)?.Supports("BagRawWrite") == true)
                    {
                        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteConcurrentBagRaw(ref operationState, value))");
                        w.Line("    goto operationComplete_RootWrite;");
                    }
                    w.Blank();
                    if (set.FindFormatter(bag)?.Supports("BagSnapshot") == true)
                        w.Line("var snapshot = global::LuminPack.Code.LuminPackCollectionAccess.RentConcurrentBagSnapshot(value, out int count, out bool pooled);");
                    else
                    {
                        w.Line("var snapshot = value.ToArray();");
                        w.Line("int count = snapshot.Length;");
                        w.Line("bool pooled = false;");
                    }
                    w.Line("try");
                    w.Open();
                    w.Line("global::System.ReadOnlySpan<" + scalar + "> items = new global::System.ReadOnlySpan<" + scalar +
                        ">(snapshot, 0, count);");
                    w.Line("operationState.Write<" + scalar + ">(items, items.Length);");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("if (pooled)");
                    w.Line("    global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(snapshot, false);");
                    w.Close();
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
                    w.Line("    in " + bag + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + bag + " value)");
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
                    w.Line("internal static void Deserialize_ConcurrentBagOf" + readable + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + bag + " value)");
                    w.Line("        => " + roots + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_ConcurrentBagOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + bag + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootRead(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + bag + " value)");
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
                    bool presize = set.FindFormatter(bag)?.Supports("BagFreshSizing") == true;
                    if (presize)
                        w.Line(bag + "? fresh = null;");
                    w.Line("if (value is null)");
                    w.Indented(() => w.Line("value = " + (presize ? "fresh = " : "") + "new " + bag + "();"));
                    w.Line("else");
                    w.Indented(() => w.Line("value.Clear();"));
                    w.Blank();
                    w.Line("if (length == 0)");
                    w.Indented(() => w.Line("goto operationComplete_RootRead;"));
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
                    AddStagedItems(w, memory, presize);
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
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + bag + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + bag + " value)");
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
                    w.Line("internal static void SerializeJson_ConcurrentBagOf" + readable + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + bag + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_ConcurrentBagOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + bag + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    " + bag + " input)");
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
                    WriteJsonSnapshot(w, set, bag, scalar, elementOwner, set.FindFormatter(bag)?.Supports("BagSnapshot") == true);
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
                    w.Line("    in " + bag + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + bag + " value)");
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
                    w.Line("internal static void DeserializeJson_ConcurrentBagOf" + readable + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + bag + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_ConcurrentBagOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + bag + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + bag + " value)");
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
                    w.Line("value = new " + bag + "();");
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
                    w.Line(scalar + " item = default !;");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    w.Line("value.Add(item);");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + bag + " value)");
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
                    w.Line("    ref " + bag + " value)");
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
