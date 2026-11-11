using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>ImmutableSortedSet&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy formatter generator's immutable-sorted-set roots and extension methods. The binary write
/// stores the tree storage through the shared raw kernel; the read rebuilds the set from the pooled block and reuses the last
/// candidate when its comparer, count and bytes match; the JSON roots transfer their elements through the shared scalar owner and
/// build the decoded values through the ordered-set fast path or a rented sorted-set builder.</remarks>
internal static class ImmutableSortedSetOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact)
    {
        var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        // The legacy immutable-sorted-set roots spell the state types through the imported core namespace.
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
        string roots = "LuminRoots_ImmutableSortedSetOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_ImmutableSortedSetOf" + readable;
        string candidate = "LuminImmutableCandidate<" + sequence + ">";
        string tree = "global::LuminPack.Utility.LuminTreeStorage";

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_ImmutableSortedSetOf" + readable + "(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("        => " + roots + ".RootWrite(ref writer, input);");
        w.Blank();

        w.Line("internal static partial class " + roots);
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    " + sequence + " input)");
        w.Open();
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line(memory + ".WriteUnaligned(ref writer.Cursor, " + code + ".NullCollection);");
        w.Line("writer.Advance(4);");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line(tree + ".WriteRaw(ref writer, value);");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line("internal static void RootRead(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
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
        w.Line("value = " + sequence + ".Empty;");
        w.Line("return;");
        w.Close();
        w.Line("var pooled = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(length);");
        w.Line("try");
        w.Open();
        w.Line("global::System.Span<" + scalar + "> items = " + marshal + ".CreateSpan<" + scalar + ">(ref " + memory +
            ".As<byte, " + scalar + ">(ref reader.Cursor), 0);");
        w.Line("items = pooled.AsSpan(0, length);");
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
        w.Blank();
        w.Line("bool fromCache = value is null;");
        w.Line("var candidate = value;");
        w.Line("if (candidate is null)");
        w.Line("    " + candidate + ".Last.TryGetTarget(out candidate);");
        w.Line("if (candidate is not null &&");
        w.Line("    candidate.Count == length &&");
        w.Line("    object.ReferenceEquals(candidate.KeyComparer, global::System.Collections.Generic.Comparer<" + scalar +
            ">.Default) &&");
        w.Line("    (!fromCache || CandidateHasFirst(candidate, pooled[0])))");
        w.Open();
        w.Line("var prior = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(length);");
        w.Line("try");
        w.Open();
        w.Line(tree + ".CopyTo(candidate, prior);");
        w.Line("if (" + marshal + ".AsBytes(prior.AsSpan(");
        w.Indented(() =>
        {
            w.Line("0,");
            w.Line("length)).SequenceEqual(" + marshal + ".AsBytes(pooled.AsSpan(0, length))))");
        });
        w.Open();
        w.Line(candidate + ".Last.SetTarget(candidate);");
        w.Line("value = candidate;");
        w.Line("return;");
        w.Close();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(prior, false);");
        w.Close();
        w.Close();
        w.Blank();
        w.Line("var completed = " + tree + ".CreateImmutableSortedSet(pooled, length);");
        w.Line(candidate + ".Last.SetTarget(completed);");
        w.Line("value = completed;");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(pooled, false);");
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static bool CandidateHasFirst(" + sequence + " candidate2, " + scalar + " wireFirst)");
        w.Open();
        w.Line("var stored = candidate2.Min;");
        w.Line("return " + marshal + ".AsBytes(" + marshal + ".CreateReadOnlySpan(");
        w.Indented(() =>
        {
            w.Line("ref stored,");
            w.Line("1)).SequenceEqual(" + marshal + ".AsBytes(" + marshal + ".CreateReadOnlySpan(ref wireFirst, 1)));");
        });
        w.Close();
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
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
        w.Line("WriteKnownElements0(ref writer, value);");
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void WriteKnownElements0(");
        w.Line("    ref " + jsonWriter + " writer2,");
        w.Line("    " + sequence + " source)");
        w.Open();
        w.Line("int count = source.Count;");
        w.Line("if (count == 0)");
        w.Line("    return;");
        w.Line("var items = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(count);");
        w.Line("try");
        w.Open();
        w.Line(tree + ".CopyTo(source, new global::System.Span<" + scalar + ">(items, 0, count));");
        w.Line("bool isFirst = true;");
        w.Line("foreach (var item in new global::System.ReadOnlySpan<" + scalar + ">(items, 0, count))");
        w.Open();
        w.Line("if (!isFirst)");
        w.Line("    writer2.WriteByteRaw<TLuminProfile>((byte)',');");
        w.Line("else");
        w.Line("    isFirst = false;");
        w.Line("writer2.SetFirstElement(true);");
        w.Line("var temp = item;");
        w.Line(elementOwner + ".Write<TLuminProfile>(ref writer2, temp);");
        w.Close();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(");
        w.Indented(() =>
        {
            w.Line("items,");
            w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + scalar + ">());");
        });
        w.Close();
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
        w.Line("if (count == 1)");
        w.Open();
        w.Line("value = global::System.Collections.Immutable.ImmutableSortedSet.Create<" + scalar + ">(buffer[0]);");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("if (" + tree + ".CreateOrderedSetIfSupported(buffer, count) is { } ordered)");
        w.Open();
        w.Line("value = ordered;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var builder = global::LuminPack.Utility.LuminImmutableBuilders.RentSortedSet<" + scalar + ">();");
        w.Line("try");
        w.Open();
        w.Line("for (int i = 0; i < count; i++)");
        w.Open();
        w.Line("builder.Add(buffer[i]);");
        w.Close();
        w.Blank();
        w.Line("value = builder.ToImmutable();");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::LuminPack.Utility.LuminImmutableBuilders.ReturnSortedSet(builder);");
        w.Close();
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
        w.Line("internal static void Serialize_ImmutableSortedSetOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableSortedSetOf" + readable + "(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("        => " + roots + ".RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_ImmutableSortedSetOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + reader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableSortedSetOf" + readable + "(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_ImmutableSortedSetOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in " + sequence + " input)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableSortedSetOf" + readable + "(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_ImmutableSortedSetOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_ImmutableSortedSetOf" + readable + "(");
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
        w.Line("internal static void MeasureRecord_ImmutableSortedSetOf" + readable + "<TLuminProfile>(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref " + sequence + " value)");
        w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_ImmutableSortedSetOf" + readable +
            "(ref evaluator, ref value);");
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
        w.Line("internal static void Read(this ref " + reader + " state, ref " + sequence + " value)");
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + roots + ".RootRead(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref " + sequence + " value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + roots + ".RootRead(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Blank();
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
