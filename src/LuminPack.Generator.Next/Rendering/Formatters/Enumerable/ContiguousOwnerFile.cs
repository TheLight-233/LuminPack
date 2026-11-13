using System;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>The storage API of a contiguous framework container.</summary>
internal enum ContiguousShape
{
    Array,
    List,
    ImmutableArray,
    Memory,
    ReadOnlyMemory,
    ArraySegment,
    ReadOnlySequence,
}

/// <summary>Renders contiguous containers using their element storage contract and static element formatter.</summary>
internal static class ContiguousOwnerFile
{
    private const string Marshal = "global::LuminPack.Code.LuminPackMarshal";
    private const string WriterType = "global::LuminPack.Core.LuminPackWriter";
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";
    private const string MemoryMarshal = "global::System.Runtime.InteropServices.MemoryMarshal";
    /// <summary>Renders one closed contiguous family without constraining its element category.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <param name = "shape">The shape used by this operation.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    /// <param name="checkedUnion">The fresh Union element read under the enclosing root's limits, if supplied.</param>
    internal static string Render(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations, ContiguousShape shape,
        PayloadRenderModel? checkedUnion = null)
    {
        FormatterModel element = model.Arguments[0];
        string suffix = ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type));
        bool reference = shape is ContiguousShape.Array or ContiguousShape.List;
        // A Union element of a list keeps the original fresh loop, which needs the root's case arms.
        PayloadRenderModel? freshUnion = checkedUnion ?? (element.Payload is UnionModel union && set.Options != null && set.Options.Net8
            ? RenderModelFactory.Create(union, set.Options) : null);
        if (freshUnion is { UnionRoot: false })
            freshUnion = null;
        bool scalarElements = shape == ContiguousShape.ReadOnlySequence;
        // A sequence of reference-free elements writes its count first and copies each segment's storage, as the original did.
        bool rawSequence = scalarElements && element.RawCollection && element.Layout == null;
        // A nullable raw scalar is stored natively too: the original contiguous writers copied the complete Nullable storage
        // through the tagged block writer, which records the layout where models differ.
        bool raw = (element.RawCollection || element.Definition == GenericIdentity.Nullable && element.Arguments.Length == 1 &&
            element.Arguments[0].RawCollection && element.Layout == null) && !scalarElements;
        FormatterStorageModel? layout = scalarElements ? null : element.Layout;
        string empty = reference ? "null" : "default";
        string absent = shape switch
        {
            ContiguousShape.Array or ContiguousShape.List => "value is null",
            ContiguousShape.ImmutableArray => "value.IsDefault",
            ContiguousShape.ArraySegment => "value.Array is null",
            _ => "false",
        };
        string span = shape switch
        {
            ContiguousShape.List => Marshal + ".GetListSpan(value)",
            ContiguousShape.Array or ContiguousShape.ImmutableArray or ContiguousShape.ArraySegment => "value.AsSpan()",
            _ => "value.Span",
        };
        return SpecialOwnerFile.Render(set, operations, suffix, model.Type, reference, BinaryWrite, BinaryRead, JsonWrite, JsonRead, w =>
        {
            if (layout != null)
                FormatterStorageEmitter.Support(w, layout);
        }, compressedEntries: shape is ContiguousShape.Array or ContiguousShape.List);
        void NullWrite(CodeWriter w, bool json)
        {
            if (absent == "false")
                return;
            w.Line("if (" + absent + ")");
            w.Open();
            w.Line(json ? "writer.WriteNull<TLuminProfile>();" : "writer.EnsureAdditionalCapacity(4);");
            if (!json)
                w.Line("writer.WriteNullCollectionHeader();");
            w.Line("return;");
            w.Close();
        }

        void BinaryWrite(CodeWriter w, SpecialOwnerContext c)
        {
            NullWrite(w, false);
            if (rawSequence)
            {
                RawSequenceWrite(w);
                return;
            }
            if (shape == ContiguousShape.ReadOnlySequence)
            {
                w.Line("int countPosition = writer.BeginCollection();");
                w.Line("int count = 0;");
                w.Line("var position = value.Start;");
                w.Line("while (value.TryGet(ref position, out var memory))");
                w.Open();
                w.Line("foreach (var item in memory.Span)");
                w.Open();
                w.Line("count++;");
                FormatterElementEmitter.Write(w, set, element, "item");
                w.Close();
                w.Close();
                w.Line("writer.EndCollection(countPosition, count);");
                w.Line("writer.CheckBuffer();");
                return;
            }

            w.Line("global::System.ReadOnlySpan<" + element.Type + "> items = " + span + ";");
            if (raw)
            {
                w.Line("int bytes = global::LuminPack.Core.LuminPackWriter.Size<" + element.Type + ">(items.Length, true, false);");
                w.Line("writer.AdvanceSafe(global::LuminPack.Core.LuminPackWriter.Write<" + element.Type + ">(writer.Reserve(writer.Position, bytes), items, items.Length, true, false));");
                return;
            }

            w.Line("writer.EnsureAdditionalCapacity(4);");
            w.Line("writer.WriteCollectionHeader(items.Length);");
            if (layout != null)
            {
                w.Line("writer.EnsureAdditionalCapacity(" + FormatterStorageEmitter.Width(layout, "items.Length") + ");");
                FormatterStorageEmitter.Payload(w, layout, Unsafe + ".AsRef(in " + MemoryMarshal + ".GetReference(items))", "items.Length", true);
            }
            else if (element.Type == "string")
                w.Line("writer.WriteStrings<TLuminProfile>(items);");
            else
            {
                w.Line("foreach (ref readonly var item in items)");
                w.Open();
                FormatterElementEmitter.Write(w, set, element, "item");
                w.Close();
            }

            w.Line("writer.CheckBuffer();");
        }

        void RawSequenceWrite(CodeWriter w)
        {
            string t = element.Type;
            w.Line(WriterType + " operationState = default;");
            w.Line(WriterType + " operationStateFallback = default;");
            w.Line("writer.CopyTo(ref operationState);");
            w.Line("int count = checked((int)value.Length);");
            w.Line("operationState.EnsureAdditionalCapacity(4);");
            w.Line("operationState.Write(count);");
            w.Line("if (value.IsSingleSegment)");
            w.Open();
            w.Line("global::System.ReadOnlySpan<" + t + "> span = value.FirstSpan;");
            w.Line("if (!span.IsEmpty)");
            w.Open();
            w.Line("if (" + MemoryMarshal + ".AsBytes(span).Overlaps(operationState.Buffer))");
            w.Open();
            w.Line("operationState.CopyTo(ref operationStateFallback);");
            w.Line("WriteOverlappingSequenceSegment(ref operationStateFallback, span);");
            w.Line("operationStateFallback.CopyTo(ref operationState);");
            w.Close();
            w.Line("else");
            w.Open();
            w.Line("int bytes = checked(span.Length * " + Unsafe + ".SizeOf<" + t + ">());");
            w.Line("operationState.EnsureAdditionalCapacity(bytes);");
            w.Line("operationState.Write(ref " + Unsafe + ".As<" + t + ", byte>(ref " + MemoryMarshal + ".GetReference(span)), bytes);");
            w.Close();
            w.Close();
            w.Close();
            w.Line("else");
            w.Open();
            w.Line("operationState.CopyTo(ref operationStateFallback);");
            w.Line("WriteRawSequenceSegments(ref operationStateFallback, value);");
            w.Line("operationStateFallback.CopyTo(ref operationState);");
            w.Close();
            w.Line("operationState.CheckBuffer();");
            w.Line("operationState.CopyTo(ref writer);");
            w.Blank();
            // An overlapping source is written element by element, so a reallocation never reads released storage.
            w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            w.Line("static void WriteOverlappingSequenceSegment(ref " + WriterType + " target, global::System.ReadOnlySpan<" + t + "> items)");
            w.Open();
            w.Line(WriterType + " state = default;");
            w.Line("target.CopyTo(ref state);");
            w.Line("long capacity = items.Length * ((long)" + Unsafe + ".SizeOf<" + t + ">());");
            w.Line("if (state.TryEnsureCapacity(capacity))");
            w.Open();
            w.Line("foreach (var item in items)");
            w.Open();
            w.Line("var copy = item;");
            w.Line("state.Write(in copy);");
            w.Close();
            w.Close();
            w.Line("else");
            w.Open();
            w.Line("foreach (var item in items)");
            w.Open();
            w.Line("state.CheckBuffer();");
            w.Line("var copy = item;");
            w.Line("state.Write(in copy);");
            w.Close();
            w.Close();
            w.Line("state.CopyTo(ref target);");
            w.Close();
            w.Blank();
            w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            w.Line("static void WriteRawSequenceSegments(ref " + WriterType + " target, " + model.Type + " sequence)");
            w.Open();
            w.Line("foreach (var memory in sequence)");
            w.Open();
            w.Line("global::System.ReadOnlySpan<" + t + "> segment = memory.Span;");
            w.Line("if (segment.IsEmpty)");
            w.Line("    continue;");
            w.Line("if (" + MemoryMarshal + ".AsBytes(segment).Overlaps(target.Buffer))");
            w.Open();
            w.Line("WriteOverlappingSequenceSegment(ref target, segment);");
            w.Line("continue;");
            w.Close();
            w.Line("int segmentBytes = checked(segment.Length * " + Unsafe + ".SizeOf<" + t + ">());");
            w.Line("target.EnsureAdditionalCapacity(segmentBytes);");
            w.Line("target.Write(ref " + Unsafe + ".As<" + t + ", byte>(ref " + MemoryMarshal + ".GetReference(segment)), segmentBytes);");
            w.Close();
            w.Close();
        }

        void BinaryRead(CodeWriter w, SpecialOwnerContext c)
        {
            if (rawSequence)
            {
                string t = element.Type;
                w.Line("int length = reader.ReadLength(" + Unsafe + ".SizeOf<" + t + ">());");
                w.Line("if (length < 0)");
                w.Open();
                w.Line("value = default;");
                w.Line("return;");
                w.Close();
                w.Line("var array = " + Marshal + ".AllocateUninitializedArray<" + t + ">(length);");
                w.Line("if (length != 0)");
                w.Line("    reader.Read(ref " + Unsafe + ".As<" + t + ", byte>(ref " + MemoryMarshal + ".GetArrayDataReference(array)), unchecked(length * " +
                    Unsafe + ".SizeOf<" + t + ">()));");
                w.Line("value = new " + model.Type + "(array);");
                return;
            }
            string minimum = raw ? Unsafe + ".SizeOf<" + element.Type + ">()" : layout != null ? "0" : FormatterElementEmitter.Minimum(element);
            if (shape == ContiguousShape.List && freshUnion != null)
            {
                // The original fresh Union loop: a new list reads into its fresh storage array and expands the dispatch per element.
                w.Line("if (value is null)");
                w.Open();
                w.Line("int freshLength = reader.ReadLength(" + minimum + ");");
                w.Line("if (freshLength < 0)");
                w.Indented(() => w.Line("return;"));
                w.Line("var freshItems = reader.CreateFreshListArray<" + element.Type + ">(freshLength, out value);");
                w.Line("ref var first = ref " + MemoryMarshal + ".GetArrayDataReference(freshItems);");
                w.Line("for (int i = 0; i < freshLength; i++)");
                w.Open();
                w.Line(element.Type + " item = default!;");
                UnionEmitter.FreshElementRead(freshUnion, w, "item", "freshEnd");
                w.Line("freshEnd:");
                w.Line(Unsafe + ".Add(ref first, i) = item;");
                w.Close();
                w.Line("return;");
                w.Close();
                w.Blank();
            }
            w.Line("int length = reader.ReadLength(" + minimum + ");");
            w.Line("if (length < 0)");
            w.Open();
            w.Line("value = " + empty + ";");
            w.Line("return;");
            w.Close();
            if (layout != null)
                FormatterStorageEmitter.Validate(w, layout, "length");
            Prepare(w);
            if (raw)
            {
                w.Line("if (length != 0) reader.Read(ref " + Unsafe + ".As<" + element.Type + ", byte>(ref " + MemoryMarshal + ".GetReference(items)), checked(length * " + Unsafe + ".SizeOf<" + element.Type + ">()));");
                if (element.Type == "bool")
                    w.Line("global::LuminPack.Security.LuminPackInputPolicy.CheckBooleans(items);");
            }
            else if (layout != null)
                FormatterStorageEmitter.Payload(w, layout, MemoryMarshal + ".GetReference(items)", "length", false);
            else if (element.Type == "string")
                w.Line("reader.ReadStrings<TLuminProfile>(items);");
            else
            {
                w.Line("for (int index = 0; index < length; index++)");
                w.Open();
                FormatterElementEmitter.Read(w, set, element, "items[index]");
                w.Close();
            }

            Publish(w);
        }

        void Prepare(CodeWriter w)
        {
            switch (shape)
            {
                case ContiguousShape.Array:
                    w.Line("if (value is null || value.Length != length || value.GetType() != typeof(" + model.Type + ")) value = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    w.Line("var items = value.AsSpan();");
                    break;
                case ContiguousShape.List:
                    w.Line("value ??= new " + model.Type + "(length);");
                    w.Line("var items = " + Marshal + ".GetListSpan(value, length);");
                    break;
                case ContiguousShape.Memory:
                    w.Line("bool reused = " + Marshal + ".TryGetExactArray(value, out var array, out var start, out var storageLength) && storageLength == length;");
                    w.Line("if (!reused)");
                    w.Open();
                    w.Line("array = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    w.Line("start = 0;");
                    w.Close();
                    w.Line("var items = array.AsSpan(start, length);");
                    break;
                case ContiguousShape.ArraySegment:
                    w.Line("var array = value.Array;");
                    w.Line("bool reused = array is not null && array.GetType() == typeof(" + ArrayType(element.Type) + ") && value.Count == length;");
                    w.Line("int start = reused ? value.Offset : 0;");
                    w.Line("if (!reused) array = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    w.Line("var items = array.AsSpan(start, length);");
                    break;
                case ContiguousShape.ImmutableArray:
                    w.Line("var array = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    if (element.Type == "string")
                        w.Line("if (!value.IsDefault) value.AsSpan().Slice(0, global::System.Math.Min(value.Length, length)).CopyTo(array);");
                    w.Line("var items = array.AsSpan();");
                    break;
                default:
                    w.Line("var array = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    w.Line("var items = array.AsSpan();");
                    break;
            }
        }

        void Publish(CodeWriter w)
        {
            switch (shape)
            {
                case ContiguousShape.ImmutableArray:
                    w.Line("#if NET8_0_OR_GREATER");
                    w.Line("value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(array);");
                    w.Line("#else");
                    w.Line("value = global::System.Collections.Immutable.ImmutableArray.Create<" + element.Type + ">();");
                    w.Line(Marshal + ".As<" + model.Type + ", global::LuminPack.Code.ImmutableArrayView<" + element.Type + ">>(ref value).array = array;");
                    w.Line("#endif");
                    break;
                case ContiguousShape.Memory:
                case ContiguousShape.ArraySegment:
                    w.Line("if (!reused) value = new " + model.Type + "(array!, start, length);");
                    break;
                case ContiguousShape.ReadOnlyMemory:
                case ContiguousShape.ReadOnlySequence:
                    w.Line("value = new " + model.Type + "(array);");
                    break;
            }
        }

        void JsonWrite(CodeWriter w, SpecialOwnerContext c)
        {
            NullWrite(w, true);
            w.Line("writer.WriteArrayStart<TLuminProfile>();");
            w.Line("bool first = true;");
            if (shape == ContiguousShape.ReadOnlySequence)
            {
                w.Line("var position = value.Start;");
                w.Line("while (value.TryGet(ref position, out var memory))");
                w.Open();
                w.Line("foreach (var item in memory.Span)");
            }
            else
                w.Line("foreach (var item in " + span + ")");
            w.Open();
            w.Line("if (!first) writer.WriteByteRaw<TLuminProfile>((byte)',');");
            w.Line("first = false;");
            w.Line("writer.SetFirstElement(true);");
            w.Line(FormatterElementEmitter.Owner(set, element) + ".Write<TLuminProfile>(ref writer, in item);");
            w.Close();
            if (shape == ContiguousShape.ReadOnlySequence)
                w.Close();
            w.Line("writer.WriteArrayEnd<TLuminProfile>();");
        }

        void JsonRead(CodeWriter w, SpecialOwnerContext c)
        {
            w.Line("if (reader.IsNull())");
            w.Open();
            w.Line("value = " + empty + ";");
            w.Line("return;");
            w.Close();
            w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
            if (shape == ContiguousShape.List)
            {
                // The original list read streams its elements once: no lookahead count, existing elements are read in place and
                // the tail is trimmed.
                w.Line("if (value is null || value.GetType() != typeof(" + model.Type + "))");
                w.Open();
                w.Line("value = new " + model.Type + "(reader.GetArrayCapacityHint<TLuminProfile>(10));");
                w.Close();
                w.Blank();
                w.Line("int count = 0;");
                w.Line("while (reader.Read<TLuminProfile>())");
                w.Open();
                w.Line("if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)");
                w.Indented(() => w.Line("break;"));
                w.Line("if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)");
                w.Indented(() => w.Line("continue;"));
                w.Line(element.Type + " item = count < value.Count ? value[count] : default!;");
                w.Line(FormatterElementEmitter.Owner(set, element) + ".Read<TLuminProfile>(ref reader, ref item);");
                w.Line("if (count < value.Count)");
                w.Indented(() => w.Line("value[count] = item;"));
                w.Line("else");
                w.Indented(() => w.Line("value.Add(item);"));
                w.Line("count++;");
                w.Close();
                w.Blank();
                w.Line("if (count < value.Count)");
                w.Indented(() => w.Line("value.RemoveRange(count, value.Count - count);"));
                return;
            }
            w.Line("int length = reader.CountArrayElements<TLuminProfile>();");
            Prepare(w);
            w.Line("for (int index = 0; index < length; index++)");
            w.Open();
            w.Line("reader.Read<TLuminProfile>();");
            w.Line(FormatterElementEmitter.Owner(set, element) + ".Read<TLuminProfile>(ref reader, ref items[index]);");
            w.Close();
            w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
            Publish(w);
        }
    }

    /// <summary>Inserts the outer vector rank ahead of any array ranks in the element type.</summary>
    /// <param name = "element">The element used by this operation.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string ArrayType(string element) => WithRank(element, "[]");
    /// <summary>Inserts an outer array rank ahead of any ranks in its element.</summary>
    /// <param name = "element">The element used by this operation.</param>
    /// <param name = "rank">The rank used by this operation.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string WithRank(string element, string rank)
    {
        int depth = 0;
        for (int i = 0; i < element.Length; i++)
        {
            if (element[i] == '<')
                depth++;
            if (element[i] == '>')
                depth--;
            if (element[i] == '[' && depth == 0)
                return element.Insert(i, rank);
        }

        return element + rank;
    }
}
