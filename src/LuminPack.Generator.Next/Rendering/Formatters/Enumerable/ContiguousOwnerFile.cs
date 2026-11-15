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
        PayloadRenderModel? freshUnion = checkedUnion ?? (element.Definition != FormatterDefinitions.GenericUnion &&
            element.Payload is UnionModel union && set.Options != null && set.Options.Net8
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
            ContiguousShape.Array => "new global::System.ReadOnlySpan<" + element.Type + ">(value)",
            ContiguousShape.ArraySegment => "new global::System.ReadOnlySpan<" + element.Type + ">(value.Array, value.Offset, value.Count)",
            ContiguousShape.ImmutableArray => "value.AsSpan()",
            _ => "value.Span",
        };
        bool measure = element.RawCollection && layout == null && shape is
            (ContiguousShape.Memory or ContiguousShape.ReadOnlyMemory or ContiguousShape.ArraySegment);
        return SpecialOwnerFile.Render(set, operations, suffix, model.Type, reference, BinaryWrite, BinaryRead, JsonWrite, JsonRead, w =>
        {
            if (layout != null)
                FormatterStorageEmitter.Support(w, layout);
        }, compressedEntries: shape is ContiguousShape.Array or ContiguousShape.List, outlineJson: true,
            outlineBinaryWrite: scalarElements || (!raw && layout == null && element.Type != "string"),
            binaryWriteEntries: measure ? MeasureRecord : null,
            jsonWriteRun: !scalarElements && JsonIntegerRunWriter.Supports(set, model.Type, element.Type));

        void MeasureRecord(CodeWriter w)
        {
            string count = shape == ContiguousShape.ArraySegment ? "value.Count" : "value.Length";
            w.Line(w.Preserve);
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line("internal static void MeasureRecord_" + suffix + "<TLuminProfile>(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
            w.Line("    scoped ref " + model.Type + " value)");
            w.Line("    where TLuminProfile : struct, " + w.A("ILuminPackOptions"));
            w.Open();
            w.Line("evaluator.Add(checked(sizeof(int) + " + count + " * " + Unsafe + ".SizeOf<" + element.Type + ">()));");
            w.Close();
            w.Blank();
        }

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
                if (element.Type == "string" && model.Supports("WriteCheckedStringSpan"))
                {
                    w.Line("var items = memory.Span;");
                    w.Line("writer.WriteCheckedStrings<TLuminProfile>(items);");
                    w.Line("count += items.Length;");
                }
                else
                {
                    w.Line("var items = memory.Span;");
                    w.Line("int length = items.Length;");
                    w.Line("if (length != 0)");
                    w.Open();
                    w.Line("ref var firstItem = ref " + MemoryMarshal + ".GetReference(items);");
                    w.Line("int index = 0;");
                    w.Line("do");
                    w.Open();
                    w.Line("var item = " + Unsafe + ".Add(ref firstItem, (nint)(uint)index);");
                    w.Line("count++;");
                    FormatterElementEmitter.Write(w, set, element, "item");
                    w.Close();
                    w.Line("while (++index < length);");
                    w.Close();
                }
                w.Close();
                w.Line("writer.EndCollection(countPosition, count);");
                w.Line("writer.CheckBuffer();");
                return;
            }

            w.Line("global::System.ReadOnlySpan<" + element.Type + "> items = " + span + ";");
            if (raw)
            {
                if (element.RawCollection && element.Definition != GenericIdentity.Nullable)
                    w.Line("writer.Write<" + element.Type + ">(items, items.Length);");
                else
                {
                    w.Line("int bytes = global::LuminPack.Core.LuminPackWriter.Size<" + element.Type + ">(items.Length, true, false);");
                    w.Line("writer.AdvanceSafe(global::LuminPack.Core.LuminPackWriter.Write<" + element.Type + ">(writer.Reserve(writer.Position, bytes), items, items.Length, true, false));");
                }
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
                w.Line("int length = items.Length;");
                w.Line("if (length != 0)");
                w.Open();
                w.Line("ref var firstItem = ref " + MemoryMarshal + ".GetReference(items);");
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                w.Line("ref readonly var item = ref " + Unsafe + ".Add(ref firstItem, (nint)(uint)index);");
                FormatterElementEmitter.Write(w, set, element, "item");
                w.Close();
                w.Line("while (++index < length);");
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
                w.Line("if (freshLength != 0)");
                w.Open();
                w.Line("ref var first = ref " + Marshal + ".GetNotNullArrayReference(freshItems);");
                w.Line("int i = 0;");
                w.Line("do");
                w.Open();
                w.Line(element.Type + " item = default!;");
                UnionEmitter.FreshElementRead(freshUnion, w, "item", "freshEnd");
                w.Line("freshEnd:");
                w.Line(Unsafe + ".Add(ref first, (nint)(uint)i) = item;");
                w.Close();
                w.Line("while (++i < freshLength);");
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
                ReadSlots(w, json: false);
            }

            Publish(w);
        }

        void Prepare(CodeWriter w)
        {
            switch (shape)
            {
                case ContiguousShape.Array:
                    w.Line("if (value is null || value.Length != length || value.GetType() != typeof(" + model.Type + ")) value = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    w.Line("var items = " + Marshal + ".CreateSpan(ref " + Marshal + ".GetNotNullArrayReference(value), length);");
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
                    w.Line("var items = new global::System.Span<" + element.Type + ">(array, start, length);");
                    break;
                case ContiguousShape.ArraySegment:
                    w.Line("var array = value.Array;");
                    w.Line("bool reused = array is not null && array.GetType() == typeof(" + ArrayType(element.Type) + ") && value.Count == length;");
                    w.Line("int start = reused ? value.Offset : 0;");
                    w.Line("if (!reused) array = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    w.Line("var items = new global::System.Span<" + element.Type + ">(array, start, length);");
                    break;
                case ContiguousShape.ImmutableArray:
                    w.Line("var array = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    if (element.Type == "string")
                        w.Line("if (!value.IsDefault) value.AsSpan().Slice(0, global::System.Math.Min(value.Length, length)).CopyTo(array);");
                    w.Line("var items = " + Marshal + ".CreateSpan(ref " + Marshal + ".GetNotNullArrayReference(array), length);");
                    break;
                default:
                    w.Line("var array = " + Marshal + ".AllocateUninitializedArray<" + element.Type + ">(length);");
                    w.Line("var items = " + Marshal + ".CreateSpan(ref " + Marshal + ".GetNotNullArrayReference(array), length);");
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
            bool readBeforeSeparator = shape != ContiguousShape.Memory;
            NullWrite(w, true);
            w.Line("writer.WriteArrayStart<TLuminProfile>();");
            if (shape == ContiguousShape.ReadOnlySequence)
            {
                w.Line("var position = value.Start;");
                bool run = JsonIntegerRunWriter.Supports(set, model.Type, element.Type);
                if (!run)
                    w.Line("bool first = true;");
                w.Line("while (value.TryGet(ref position, out var memory))");
                w.Open();
                if (!JsonIntegerRunWriter.TryEmit(w, set, model.Type, element.Type, "memory.Span", readBeforeSeparator: true))
                    JsonElements("memory.Span");
                w.Close();
            }
            else if (!JsonIntegerRunWriter.TryEmit(w, set, model.Type, element.Type, span, readBeforeSeparator))
            {
                w.Line("bool first = true;");
                JsonElements(span);
            }
            w.Line("writer.WriteArrayEnd<TLuminProfile>();");

            void JsonElements(string items)
            {
                w.Line("var items = " + items + ";");
                w.Line("int length = items.Length;");
                w.Line("if (length != 0)");
                w.Open();
                w.Line("ref var firstItem = ref " + MemoryMarshal + ".GetReference(items);");
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                string load = "var item = " + Unsafe + ".Add(ref firstItem, (nint)(uint)index);";
                // Mutable Memory reads the slot after the separator can call the output owner.
                if (readBeforeSeparator)
                    w.Line(load);
                w.Line("if (!first) writer.WriteByteRaw<TLuminProfile>((byte)',');");
                w.Line("first = false;");
                w.Line("writer.SetFirstElement(true);");
                if (!readBeforeSeparator)
                    w.Line(load);
                w.Line(FormatterElementEmitter.Owner(set, element) + ".Write<TLuminProfile>(ref writer, in item);");
                w.Close();
                w.Line("while (++index < length);");
                w.Close();
            }
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
            bool pooled = shape is ContiguousShape.Array or ContiguousShape.ImmutableArray;
            if (pooled)
            {
                // Exact mutable arrays need the count before reusing element slots. Only the trusted built-in
                // profiles can decode a fresh result before validating the rest of the array.
                bool seedStrings = shape == ContiguousShape.ImmutableArray && element.Type == "string";
                w.Line("if (" + (seedStrings ? "value.IsDefault && (" : "") +
                    "typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackUtf16LengthOptions) ||");
                w.Line("    typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackUtf8LengthOptions) ||");
                w.Line("    typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackUtf16TokenOptions) ||");
                w.Line("    typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackUtf8TokenOptions) ||");
                w.Line("    typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackDefaultOptions))" + (seedStrings ? ")" : ""));
                w.Open();
                if (shape == ContiguousShape.Array)
                {
                    w.Line("if (value is not null && value.GetType() == typeof(" + model.Type + ") &&");
                    w.Line("    reader.CountArrayElements<TLuminProfile>() == value.Length)");
                    w.Open();
                    w.Line("int length = value.Length;");
                    w.Line("var items = " + Marshal + ".CreateSpan(ref " + Marshal + ".GetNotNullArrayReference(value), length);");
                    ReadSlots(w, json: true);
                    w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                }
                ReadPooledJsonArray(w, c);
                w.Line("return;");
                w.Close();
                w.Line("else");
                w.Open();
            }
            w.Line("int length = reader.CountArrayElements<TLuminProfile>();");
            Prepare(w);
            ReadSlots(w, json: true);
            w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
            Publish(w);
            if (pooled)
                w.Close();
        }

        void ReadPooledJsonArray(CodeWriter w, SpecialOwnerContext c)
        {
            string arrayType = ArrayType(element.Type);
            string arrayEnd = c.JsonReader + ".JsonTokenType.ArrayEnd";
            string objectEnd = c.JsonReader + ".JsonTokenType.ObjectEnd";
            string unterminated = "global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"Unterminated JSON array\");";
            w.Line("int arrayDepth = reader.Depth - 1;");
            w.Line("if (!reader.Read<TLuminProfile>()) " + unterminated);
            w.Line("if (reader.CurrentTokenType == " + arrayEnd + ")");
            w.Open();
            w.Line("if (reader.Depth != arrayDepth) " + unterminated);
            w.Line(shape == ContiguousShape.Array ? "value = global::System.Array.Empty<" + element.Type + ">();" :
                "value = " + model.Type + ".Empty;");
            w.Line("return;");
            w.Close();
            w.Line(arrayType + " array;");
            w.Line("var pool = global::System.Buffers.ArrayPool<" + element.Type + ">.Shared;");
            w.Line("var buffer = pool.Rent(" + (shape == ContiguousShape.Array ? "4" : "16") + ");");
            w.Line("bool clear = global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + element.Type + ">();");
            w.Line("int count = 0;");
            w.Line("try");
            w.Open();
            w.Line("int capacity = buffer.Length;");
            w.Line("ref var first = ref " + Marshal + ".GetNotNullArrayReference(buffer);");
            w.Line("do");
            w.Open();
            w.Line("if (reader.CurrentTokenType == " + objectEnd + ") " + unterminated);
            w.Line("if (count == capacity)");
            w.Open();
            w.Line("var larger = pool.Rent(checked(capacity * 2));");
            w.Line("global::System.Array.Copy(buffer, larger, count);");
            w.Line("pool.Return(buffer, clear);");
            w.Line("buffer = larger;");
            w.Line("capacity = larger.Length;");
            w.Line("first = ref " + Marshal + ".GetNotNullArrayReference(larger);");
            w.Close();
            w.Line(element.Type + " item = default!;");
            w.Line(FormatterElementEmitter.Owner(set, element) + ".Read<TLuminProfile>(ref reader, ref item);");
            w.Line(Unsafe + ".Add(ref first, (nint)(uint)count++) = item;");
            w.Line("if (!reader.Read<TLuminProfile>()) " + unterminated);
            w.Close();
            w.Line("while (reader.CurrentTokenType != " + arrayEnd + ");");
            w.Line("if (reader.Depth != arrayDepth) " + unterminated);
            w.Line("array = " + Marshal + ".CreateReadOnlySpan(ref first, count).ToArray();");
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line("pool.Return(buffer, clear);");
            w.Close();
            if (shape == ContiguousShape.Array)
                w.Line("value = array;");
            else
                Publish(w);
        }

        void ReadSlots(CodeWriter w, bool json)
        {
            w.Line("if (length > 0)");
            w.Open();
            w.Line("ref var firstItem = ref " + MemoryMarshal + ".GetReference(items);");
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            const string slot = Unsafe + ".Add(ref firstItem, (nint)(uint)index)";
            if (json)
            {
                w.Line("reader.Read<TLuminProfile>();");
                w.Line(FormatterElementEmitter.Owner(set, element) + ".Read<TLuminProfile>(ref reader, ref " + slot + ");");
            }
            else
                FormatterElementEmitter.Read(w, set, element, slot);
            w.Close();
            w.Line("while (++index < length);");
            w.Close();
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
