using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>ImmutableHashSet&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy formatter generator's immutable-hash-set roots and extension methods. The binary write
/// stores the tree storage through the shared raw kernel; the read rebuilds the set from the pooled block and reuses the last
/// candidate when its comparer, count and bytes match; the JSON roots transfer their elements through the shared scalar owner.</remarks>
internal static class ImmutableHashSetOwnerFile
{
    /// <summary>Renders the shared weak candidate cache one compilation needs when a set owner is published.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <returns>The complete source.</returns>
    /// <remarks>The cache is a private nested type of <c>LuminPackSerializer</c>, so a set owner's root container reaches it
    /// through the partial class. It is emitted once per compilation, beside the owners that reference it.</remarks>
    internal static string RenderCache(SharedFormatterSet set)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();
        w.Line("// One weak immutable candidate per closed generated set; every input is still fully decoded.");
        w.Line("private static class LuminImmutableCandidate<TValue> where TValue : class");
        w.Open();
        w.Line("internal static readonly global::System.WeakReference<TValue> Last = new global::System.WeakReference<TValue>(null!);");
        w.Close();
        w.Close();
        w.Close();
        return w.Complete(json: false);
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
        // The legacy immutable-hash-set roots spell the state types through the imported core namespace.
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
        string roots = "LuminRoots_ImmutableHashSetOf" + readable;
        string elementOwner = "global::LuminPack.Binary_" + extension + "_" + readable;
        string owner = "Binary_" + extension + "_ImmutableHashSetOf" + readable;
        string candidate = "LuminImmutableCandidate<" + sequence + ">";
        string tree = "global::LuminPack.Utility.LuminTreeStorage";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ImmutableHashSetOf" + readable + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_ImmutableHashSetOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_ImmutableHashSetOf" + readable + "(");
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
                    w.Line("internal static void MeasureRecord_ImmutableHashSetOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_ImmutableHashSetOf" + readable +
                        "(ref evaluator, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
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
                    w.Line("internal static void Deserialize_ImmutableHashSetOf" + readable + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootRead(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_ImmutableHashSetOf" + readable + "<TLuminProfile>(");
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
                    // Other raw elements may execute user code in the default hash comparer.
                    bool wireComparison = GenericIdentity.IsRaw(scalar);
                    if (wireComparison)
                    {
                        w.Line("int byteCount = checked(length * " + memory + ".SizeOf<" + scalar + ">());");
                        w.Line("int start = reader.Position;");
                        w.Line("reader.Advance(byteCount);");
                        w.Line("var input = reader.Buffer.Slice(start, byteCount);");
                    }
                    else
                    {
                        w.Line("reader.Read(ref " + memory + ".As<" + scalar + ", byte>(ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled)), checked(length * " + memory + ".SizeOf<" + scalar + ">()));");
                        w.Line("var input = " + marshal + ".AsBytes(new global::System.ReadOnlySpan<" + scalar + ">(pooled, 0, length));");
                    }
                    w.Line("var candidate = value;");
                    w.Line("if (candidate is null)");
                    w.Line("    " + candidate + ".Last.TryGetTarget(out candidate);");
                    w.Line("if (candidate is not null &&");
                    w.Line("    candidate.Count == length &&");
                    w.Line("    object.ReferenceEquals(candidate.KeyComparer, global::System.Collections.Generic.EqualityComparer<" + scalar +
                        ">.Default) &&");
                    w.Line(wireComparison
                        ? "    CandidateHasFirst(candidate, " + reader + ".Read<" + scalar + ">(ref " + marshal + ".GetReference(input))))"
                        : "    (value is not null || CandidateHasFirst(candidate, pooled[0])))");
                    w.Open();
                    if (!wireComparison)
                    {
                        w.Line("var prior = global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Rent(length);");
                        w.Line("try");
                        w.Open();
                    }
                    string prior = wireComparison ? "pooled" : "prior";
                    w.Line(tree + ".CopyTo(candidate, " + prior + ");");
                    w.Line("var previousItems = new global::System.ReadOnlySpan<" + scalar + ">(" + prior + ", 0, length);");
                    w.Line("if (" + marshal + ".AsBytes(previousItems).SequenceEqual(input))");
                    w.Open();
                    w.Line(candidate + ".Last.SetTarget(candidate);");
                    w.Line("value = candidate;");
                    w.Line("return;");
                    w.Close();
                    if (!wireComparison)
                    {
                        w.Close();
                        w.Line("finally");
                        w.Open();
                        w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(prior, false);");
                        w.Close();
                    }
                    w.Close();
                    w.Blank();
                    if (wireComparison)
                        w.Line("global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref " + memory + ".As<" + scalar + ", byte>(ref " +
                            "global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled)), ref " + marshal + ".GetReference(input), (uint)byteCount);");
                    w.Line("var completed = " + tree + ".CreateFrameworkImmutableHashSet(pooled, length);");
                    w.Line(candidate + ".Last.SetTarget(completed);");
                    w.Line("value = completed;");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("global::System.Buffers.ArrayPool<" + scalar + ">.Shared.Return(pooled, false);");
                    w.Close();
                    w.Blank();
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("static bool CandidateHasFirst(");
                    w.Indented(() =>
                    {
                        w.Line(sequence + " candidate2,");
                        w.Line(scalar + " wireFirst) => candidate2.Contains(wireFirst);");
                    });
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
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
                    w.Line("internal static void SerializeJson_ImmutableHashSetOf" + readable + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_ImmutableHashSetOf" + readable + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
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
                    JsonSequenceElementWriter.Emit(w, set, sequence, scalar, elementOwner, writer: "writer2");
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
                    w.Line("internal static void DeserializeJson_ImmutableHashSetOf" + readable + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_ImmutableHashSetOf" + readable + "<TLuminProfile>(");
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
                    w.Line("value = global::System.Collections.Immutable.ImmutableHashSet.Create<" + scalar + ">(buffer[0]);");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("value = " + tree + ".CreateFrameworkImmutableHashSet(buffer, count);");
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
