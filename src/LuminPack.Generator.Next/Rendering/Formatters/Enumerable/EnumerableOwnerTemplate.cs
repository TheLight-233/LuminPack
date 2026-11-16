using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>The names and writer one enumerable sequence owner exposes to its family bodies.</summary>
internal sealed partial class EnumerableOwnerContext
{
    /// <summary>The source writer of the owner file.</summary>
    internal CodeWriter W { get; }

    /// <summary>The closed declared sequence spelling.</summary>
    internal string Sequence { get; }

    /// <summary>The raw scalar element keyword.</summary>
    internal string Scalar { get; }

    /// <summary>The spelling of the unsafe memory helper.</summary>
    internal string Memory { get; }

    /// <summary>The spelling of the memory marshal helper.</summary>
    internal string Marshal { get; }

    /// <summary>Whether the element is a string, which uses the operation's profile.</summary>
    internal bool Text => Scalar == "string";

    /// <summary>The packable element's render model, or null when the element is a raw scalar or a string.</summary>
    internal PayloadRenderModel? Payload { get; }

    /// <summary>Whether the element is written one at a time through the profile instead of as raw storage.</summary>
    internal bool Counted => Text || Payload != null || Forwarded;

    /// <summary>Whether this owner calls the element formatter.</summary>
    internal bool Forwarded { get; }
    /// <summary>Whether this family's default element block uses contiguous storage.</summary>
    internal bool Contiguous { get; }
    /// <summary>Whether the complete element block owns a packed or tagged layout.</summary>
    internal bool ContiguousLayout => Contiguous && _element?.Layout != null;
    private readonly SharedFormatterSet _set;
    /// <summary>Checks the exact runtime API facts of this sequence.</summary>
    /// <param name="capability">The capability supplied to <c>Supports</c>.</param>
    /// <returns>Whether the input satisfies the supports check.</returns>
    internal bool Supports(string capability) => _set.FindFormatter(Sequence)?.Supports(capability) == true;

    /// <summary>Gets whether the referenced Core can prepare a reused hash set for a direct refill.</summary>
    internal bool HashSetRefill => _set.Options?.HashSetRefill == true;

    /// <summary>Gets whether the referenced Core can link linked-list nodes while elements are decoded.</summary>
    internal bool LinkedListAppender => _set.Options?.LinkedListAppender == true;

    /// <summary>Gets whether the referenced Core can refill a queue's backing array in place.</summary>
    internal bool QueueRefill => _set.Options?.QueueRefill == true;

    /// <summary>Gets whether the referenced Core can refill a stack's backing array in place.</summary>
    internal bool StackRefill => _set.Options?.StackRefill == true;

    /// <summary>Gets whether the referenced Core exposes the array behind an existing read-only collection.</summary>
    internal bool ReadOnlyCollectionRefill => _set.Options?.ReadOnlyCollectionRefill == true;
    internal bool RawTreeElements => GenericFormatterReuse.Real(Scalar) != "bool" &&
        (Forwarded ? _element is { Raw: true, RawStorage: true, Layout: null } : !Text && Payload == null);
    private readonly FormatterModel? _element;

    private readonly Dictionary<string, int> _writeNames = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
    private readonly Dictionary<string, int> _readNames = new Dictionary<string, int>(StringComparer.Ordinal);
    private int _ends, _cursors, _spans, _readCursors, _readStrings, _readEnds;

    /// <summary>Emits the counted enumeration write of a nonnull sequence expression through <c>operationState</c>.</summary>
    /// <param name="local">The nonnull sequence expression.</param>
    /// <param name="outlineText">Whether an enclosing operation supplies the outlined string write.</param>
    /// <param name="payloadOnly">Whether the caller owns the header, count and final capacity check.</param>
    internal void CountedWrite(string local, bool outlineText = false, bool payloadOnly = false)
    {
        if (Forwarded)
        {
            if (!payloadOnly)
            {
                W.Line("int countPosition = operationState.BeginCollection();");
                W.Line("int count = 0;");
            }
            if (payloadOnly)
                ImmutableTreeTraversal.Items(W, () => WriteElement("item"));
            else
            {
                W.Line("foreach (var item in " + Source(local) + ")");
                W.Open();
                W.Line("count++;");
                WriteElement("item");
                W.Close();
            }
            if (!payloadOnly)
            {
                W.Line("operationState.EndCollection(countPosition, count);");
                W.Line("operationState.CheckBuffer();");
            }
            return;
        }
        if (Payload == null)
        {
            if (payloadOnly)
            {
                ImmutableTreeTraversal.Items(W, () =>
                {
                    if (Text)
                        W.Line("operationState.Write<TLuminProfile>(item);");
                    else
                    {
                        W.Line("operationState.CheckBuffer();");
                        W.Line("operationState.Write(in item);");
                    }
                });
                return;
            }
            var lines = new List<string>();
            InterfaceTraversal.Counted(lines, Scalar, local, outlineText);
            foreach (string line in lines)
                W.Line(line);
            return;
        }
        if (!payloadOnly)
        {
            W.Line("int countPosition = operationState.BeginCollection();");
            W.Line("int count = 0;");
        }
        if (payloadOnly)
            ImmutableTreeTraversal.Items(W, () => WriteCountedElement("item"));
        else
        {
            W.Line("foreach (var item in " + local + ")");
            W.Open();
            WriteCountedElement("item", incrementCount: true);
            W.Close();
        }
        W.Blank();
        if (!payloadOnly)
        {
            W.Line("operationState.EndCollection(countPosition, count);");
            W.Line("operationState.CheckBuffer();");
        }
    }

    /// <summary>Writes one captured item through the counted sequence's existing element operation.</summary>
    /// <param name="item">The captured value.</param>
    /// <param name="incrementCount">Whether this operation counts the item before writing it.</param>
    internal void WriteCountedElement(string item, bool incrementCount = false)
    {
        if (Payload == null)
        {
            if (Forwarded)
                WriteElement(item);
            else if (Text)
                W.Line("operationState.Write<TLuminProfile>(" + item + ");");
            else
            {
                W.Line("operationState.CheckBuffer();");
                W.Line("operationState.Write(in " + item + ");");
            }
            return;
        }
        var body = new List<string>();
        string elementExit = "end" + _ends.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ends++;
        body.Add("operationState.CheckBuffer();");
        if (incrementCount)
            body.Add("count++;");
        body.Add("var v = " + item + ";");
        BinaryEmitter.AddSequenceElementWrite(body, Payload, "v", elementExit, "operationState", 0, W, _writeNames, ref _ends,
            ref _cursors, ref _spans);
        foreach (string line in body)
            W.Line(line);
    }

    /// <summary>Emits the bottom-first packable write of a nonnull stack through <c>operationState</c>.</summary>
    /// <param name="local">The nonnull stack expression.</param>
    /// <param name="concurrent">Whether the stack is a concurrent stack.</param>
    internal void StackPayloadWrite(string local, bool concurrent)
    {
        if (Forwarded)
        {
            ForwardedStackWrite(local, concurrent);
            return;
        }
        var body = new List<string>();
        OrderedPayloadTraversal.WriteStack(body, Payload!, local, concurrent, W, _writeNames, ref _ends, ref _cursors, ref _spans);
        foreach (string line in body)
            W.Line(line);
    }

    /// <summary>Emits the packable write of a nonnull queue through <c>operationState</c>.</summary>
    /// <param name="local">The nonnull queue expression.</param>
    internal void QueuePayloadWrite(string local)
    {
        if (Forwarded)
        {
            ForwardedQueueWrite(local);
            return;
        }
        var body = new List<string>();
        OrderedPayloadTraversal.WriteQueue(body, Payload!, local, W, _writeNames, ref _ends, ref _cursors, ref _spans);
        foreach (string line in body)
            W.Line(line);
    }

    /// <summary>Emits the read of one element into an assignable location.</summary>
    /// <param name="destination">The assignable destination.</param>
    /// <param name="fresh">Whether the destination is a newly initialized default local.</param>
    internal void ReadElement(string destination, bool fresh = false)
    {
        if (Scalar == "bool")
        {
            W.Line(destination + " = global::LuminPack.Security.LuminPackInputPolicy.ReadBoolean(ref operationState);");
            return;
        }
        if (Forwarded)
        {
            if (Contiguous && _element!.RawCollection)
                W.Line("operationState.Read(ref " + Memory + ".As<" + Scalar + ", byte>(ref " + destination + "), " + Memory + ".SizeOf<" + Scalar + ">());");
            else
            {
                if (NativeNullableElement)
                    W.Line("operationState.Read(ref " + Memory + ".As<" + Scalar + ", byte>(ref " + destination + "), " + Memory + ".SizeOf<" + Scalar + ">());");
                else
                    FormatterElementEmitter.Read(W, _set, _element!, destination, "operationState", fresh: fresh);
            }
            return;
        }
        if (Payload != null)
        {
            // The element body is expanded in place on the caller's reader, which operationState aliases.
            W.Open();
            W.Line(Scalar + " payloadItem = default !;");
            var body = new List<string>();
            BinaryEmitter.AddSequenceElementRead(body, Payload, "payloadItem", null, W, _readNames, ref _readCursors, ref _readStrings,
                ref _readEnds);
            foreach (string line in body)
                W.Line(line);
            W.Line(destination + " = payloadItem;");
            W.Close();
        }
        else if (Text)
            W.Line("operationState.Read<TLuminProfile>(ref " + destination + ");");
        else
            W.Line("operationState.Read(ref " + Memory + ".As<" + Scalar + ", byte>(ref " + destination + "), " + Memory + ".SizeOf<" + Scalar + ">());");
    }

    /// <summary>Emits the read of <c>length</c> packable elements into the existing elements of a span.</summary>
    /// <param name="span">The destination span expression, whose elements are the destination's existing elements.</param>
    /// <remarks>A null marker clears the slot; otherwise the record body is expanded into the existing element, which is constructed
    /// only when absent.</remarks>
    /// <param name="array">An array expression read through its unchecked first-element reference, as the original array read
    /// did, instead of the span.</param>
    internal void ReadBlockInPlace(string span, string? array = null)
    {
        if (Payload == null)
        {
            ReadBlock(span);
            return;
        }
        W.Line("if (length != 0)");
        W.Open();
        string first = array == null ? Marshal + ".GetReference(" + span + ")" :
            "global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(" + array + "!)";
        W.Line("ref var first = ref " + first + ";");
        W.Line("int blockIndex = 0;");
        W.Line("do");
        W.Open();
        W.Line("ref var slot = ref " + Memory + ".Add(ref first, (nint)(uint)blockIndex);");
        W.Open();
        W.Line("ref " + Scalar + " payloadItem = ref slot!;");
        var body = new List<string>();
        BinaryEmitter.AddSequenceElementRead(body, Payload, "payloadItem", null, W, _readNames, ref _readCursors, ref _readStrings,
            ref _readEnds, clearOnNull: true);
        foreach (string line in body)
            W.Line(line);
        W.Close();
        W.Close();
        W.Line("while (++blockIndex < length);");
        W.Close();
    }

    /// <summary>Emits the read of <c>length</c> elements into a span.</summary>
    /// <param name="span">The destination span expression.</param>
    /// <param name="foreign">Whether to enable foreign for this operation.</param>
    internal void ReadBlock(string span, bool foreign = false)
    {
        if (!foreign && Text && Supports("ReadInterfaceStrings"))
        {
            W.Line("operationState.ReadStrings<TLuminProfile>(" + span + ");");
            return;
        }
        if (Forwarded && Contiguous && (_element!.RawCollection || _element.Layout != null))
        {
            ReadContiguous(span);
            return;
        }
        if (foreign && Text)
        {
            W.Line("ref var blockSlot = ref " + Marshal + ".GetReference(" + span + ");");
            W.Line("for (int blockRemaining = " + span + ".Length; blockRemaining != 0; blockRemaining--)");
            W.Open();
            W.Line("blockSlot = operationState.Read<TLuminProfile>((string?)null);");
            W.Line("blockSlot = ref " + Memory + ".Add(ref blockSlot, 1);");
            W.Close();
            return;
        }
        if (Payload != null || Forwarded || Text)
        {
            W.Line("if (length != 0)");
            W.Open();
            W.Line("ref var firstBlockItem = ref " + Marshal + ".GetReference(" + span + ");");
            W.Line("int blockIndex = 0;");
            W.Line("do");
            W.Open();
            W.Line("ref var blockSlot = ref " + Memory + ".Add(ref firstBlockItem, (nint)(uint)blockIndex);");
            if (foreign)
            {
                W.Line(Scalar + " blockItem = default!;");
                ReadElement("blockItem", fresh: true);
                W.Line("blockSlot = blockItem;");
            }
            else
                ReadElement("blockSlot");
            W.Close();
            W.Line("while (++blockIndex < length);");
            W.Close();
            return;
        }
        W.Line("operationState.Read(");
        W.Indented(() =>
        {
            W.Line("ref " + Memory + ".As<" + Scalar + ", byte>(ref " + Marshal + ".GetReference(" + span + ")),");
            W.Line("unchecked(length * " + Memory + ".SizeOf<" + Scalar + ">()));");
        });
    }

    /// <summary>Processes the current formatter contract.</summary>
    /// <param name="writer">The source writer or operation writer.</param>
    /// <param name="sequence">The sequence used by this operation.</param>
    /// <param name="scalar">The scalar used by this operation.</param>
    /// <param name="memory">The memory used by this operation.</param>
    /// <param name="marshal">The marshal used by this operation.</param>
    /// <param name="payload">The payload used by this operation.</param>
    /// <param name="set">The shared formatter publication facts.</param>
    /// <param name="element">The element used by this operation.</param>
    /// <param name="contiguous">The contiguous used by this operation.</param>
    internal EnumerableOwnerContext(CodeWriter writer, string sequence, string scalar, string memory, string marshal,
        PayloadRenderModel? payload = null, SharedFormatterSet set = default, FormatterModel? element = null, bool contiguous = false)
    {
        _set = set;
        _element = element;
        Forwarded = element != null;
        Contiguous = contiguous;
        Payload = payload;
        W = writer;
        Sequence = sequence;
        Scalar = scalar;
        Memory = memory;
        Marshal = marshal;
    }

    /// <summary>Emits the pooled block read of <c>length</c> raw elements and hands the pooled array to the family.</summary>
    /// <param name="consume">The statements that consume the pooled array and <c>length</c>.</param>
    /// <param name="onReadFailure">The action used to process on read failure.</param>
    /// <param name="rawBlock">Whether to enable raw block for this operation.</param>
    internal void PooledRead(Action consume, Action? onReadFailure = null, bool rawBlock = false)
    {
        W.Line("var pooled = global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Rent(length);");
        W.Line("try");
        W.Open();
        if (rawBlock)
        {
            W.Line("ref var firstItem = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled);");
            W.Line("operationState.Read(ref " + Memory + ".As<" + Scalar + ", byte>(ref firstItem), checked(length * " +
                Memory + ".SizeOf<" + Scalar + ">()));");
        }
        else if (onReadFailure == null)
        {
            W.Line("var items = new global::System.Span<" + Scalar + ">(pooled, 0, length);");
            ReadBlock("items", foreign: true);
        }
        else
        {
            W.Line("int completed = 0;");
            W.Line("try");
            W.Open();
            W.Line("if (length != 0)");
            W.Open();
            W.Line("ref var firstItem = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled);");
            W.Line("do");
            W.Open();
            W.Line(Scalar + " current = default!;");
            ReadElement("current", fresh: true);
            W.Line(Memory + ".Add(ref firstItem, (nint)(uint)completed) = current;");
            W.Close();
            W.Line("while (++completed < length);");
            W.Close();
            W.Close();
            W.Line("catch (global::System.Exception error)");
            W.Open();
            onReadFailure();
            W.Line("global::LuminPack.Code.LuminPackExceptionHelper.Rethrow(error);");
            W.Close();
        }
        consume();
        W.Close();
        W.Line("finally");
        W.Open();
        // A pooled string block holds references, so it is cleared when it returns.
        W.Line("global::System.Buffers.ArrayPool<" + Scalar + ">.Shared.Return(pooled, " + (Forwarded ? "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + Scalar + ">()" : Counted ? "true" : "false") + ");");
        W.Close();
    }
}

/// <summary>Describes how one enumerable sequence family differs from the shared owner template.</summary>
internal sealed class EnumerableOwnerSpec
{
    /// <summary>Emits the binary write of a non-null sequence value named <c>value</c> on <c>operationState</c>.</summary>
    internal Action<EnumerableOwnerContext> WriteBody { get; }

    /// <summary>Emits the binary read after a non-negative <c>length</c> and before the exit label.</summary>
    internal Action<EnumerableOwnerContext> ReadBody { get; }

    /// <summary>The statement that appends the staged JSON element <c>item</c> to <c>value</c>.</summary>
    internal string JsonAdd { get; }
    /// <summary>Emits a family-specific JSON insertion when capture semantics select it.</summary>
    internal Action<EnumerableOwnerContext>? JsonAddBody { get; }

    /// <summary>Emits the JSON destination preparation in place of the default create-or-clear, when the sequence is staged.</summary>
    internal Action<EnumerableOwnerContext>? JsonBefore { get; }

    /// <summary>Emits the statements that publish the staged JSON elements after the element loop.</summary>
    internal Action<EnumerableOwnerContext>? JsonAfter { get; }

    /// <summary>Whether the JSON read keeps a list of the exact type and reads its existing elements in place.</summary>
    internal bool ListReuse { get; }

    /// <summary>Whether the JSON read refills an exact-length array of packable elements in place.</summary>
    internal bool ArrayReuse { get; }

    /// <summary>Whether the JSON read stages a concurrent stack in a pooled block and publishes it even on failure.</summary>
    internal bool ConcurrentStackStaging { get; }

    /// <summary>Whether an interface JSON read of packable elements reuses an existing exact array or list.</summary>
    internal bool InterfaceListReuse { get; }

    /// <summary>Whether the JSON writer can use a concrete set enumerator after an exact type check.</summary>
    internal bool InterfaceSetTraversal { get; }

    /// <summary>Whether the sequence exposes a count that short-circuits an empty JSON array.</summary>
    internal bool JsonCount { get; }

    /// <summary>Whether the owner also declares the compressed entry points a forwarding parent calls, which equal the plain ones.</summary>
    internal bool CompressEntries { get; }

    /// <summary>The inlining hint on the binary read root, or null for the default JIT policy.</summary>
    internal string? ReadInlining { get; }

    /// <summary>Whether the JSON roots are kept out of their callers.</summary>
    internal bool OutlineJson { get; }

    /// <summary>Whether JSON reads resolve the empty value before acquiring staging storage.</summary>
    internal bool JsonEmptyBeforeStaging { get; }

    /// <summary>Whether the JSON read selects a private observable list before the element loop.</summary>
    internal bool JsonObservableBacking { get; }

    /// <summary>Processes the current formatter contract.</summary>
    /// <param name="writeBody">The write body used by this operation.</param>
    /// <param name="readBody">The read body used by this operation.</param>
    /// <param name="jsonAdd">The json add used by this operation.</param>
    /// <param name="jsonBefore">The json before used by this operation.</param>
    /// <param name="jsonAfter">The json after used by this operation.</param>
    /// <param name="jsonCount">The json count used by this operation.</param>
    /// <param name="compressEntries">The compress entries used by this operation.</param>
    /// <param name="jsonAddBody">The json add body used by this operation.</param>
    /// <param name="readInlining">The binary read root's inlining hint.</param>
    /// <param name="outlineJson">Whether to keep the JSON roots out of their callers.</param>
    /// <param name="interfaceSetTraversal">Whether to probe for exact framework sets during JSON writes.</param>
    /// <param name="jsonEmptyBeforeStaging">Whether an initial empty token or accepted end of input returns the empty value directly.</param>
    /// <param name="jsonObservableBacking">Whether to select a private observable list before reading the elements.</param>
    /// <param name="listReuse">Whether to enable list reuse for this operation.</param>
    /// <param name="arrayReuse">Whether to enable array reuse for this operation.</param>
    /// <param name="concurrentStackStaging">Whether to enable concurrent stack staging for this operation.</param>
    /// <param name="interfaceListReuse">Whether to enable interface list reuse for this operation.</param>
    internal EnumerableOwnerSpec(Action<EnumerableOwnerContext> writeBody, Action<EnumerableOwnerContext> readBody, string jsonAdd,
        Action<EnumerableOwnerContext>? jsonBefore = null, Action<EnumerableOwnerContext>? jsonAfter = null, bool jsonCount = true,
        bool compressEntries = false, Action<EnumerableOwnerContext>? jsonAddBody = null, bool listReuse = false, bool arrayReuse = false,
        bool concurrentStackStaging = false, bool interfaceListReuse = false, string? readInlining = "AggressiveInlining",
        bool outlineJson = false, bool interfaceSetTraversal = false, bool jsonEmptyBeforeStaging = false, bool jsonObservableBacking = false)
    {
        ReadInlining = readInlining;
        OutlineJson = outlineJson;
        InterfaceSetTraversal = interfaceSetTraversal;
        InterfaceListReuse = interfaceListReuse;
        ConcurrentStackStaging = concurrentStackStaging;
        ArrayReuse = arrayReuse;
        ListReuse = listReuse;
        JsonAddBody = jsonAddBody;
        CompressEntries = compressEntries;
        JsonCount = jsonCount;
        WriteBody = writeBody;
        ReadBody = readBody;
        JsonAdd = jsonAdd;
        JsonBefore = jsonBefore;
        JsonAfter = jsonAfter;
        JsonEmptyBeforeStaging = jsonEmptyBeforeStaging;
        JsonObservableBacking = jsonObservableBacking;
    }
}

/// <summary>Renders the shared binary and JSON formatter owner of a counted enumerable sequence of raw scalars.</summary>
/// <remarks>The wire is the element count followed by the raw elements in enumeration order. The JSON roots enumerate the
/// sequence into an array, and read by clearing or creating the sequence and appending each element through the shared
/// scalar owner. The binary algorithms are supplied by the family through <see cref="EnumerableOwnerSpec"/>.</remarks>
internal static class EnumerableOwnerTemplate
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <param name="spec">The family algorithms.</param>
    /// <param name="nonEmptyRawHeaderBytes">The complete header width of a measurable nonempty raw collection, or zero.</param>
    /// <param name="inlineRawRead">Whether to inline binary reads of direct raw elements.</param>
    /// <param name="directRawWrite">The raw family's direct write probe before falling back to isolated state.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations, EnumerableOwnerSpec spec,
        int nonEmptyRawHeaderBytes = 0, bool inlineRawRead = false, Action<EnumerableOwnerContext>? directRawWrite = null)
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
        string marshal = w.A("MemoryMarshal");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        PayloadRenderModel? payload = fact.Element;
        // A packable element is spelled by its global name and expanded in place by the element's record body.
        string scalar = fact.ForwardedType ?? (payload != null ? payload.GlobalName : fact.Scalar!);
        string readable = ReadableTypeName.Of(scalar);
        string sequence = fact.DeclaredType;
        // The closed owner identity derives from the declared sequence, so the nested static calls agree with the model.
        string type = ReadableTypeName.Of(GeneratedNames.PayloadKey(sequence));
        string roots = "LuminRoots_" + type;
        string elementOwner = payload != null ? "global::LuminPack." + payload.Owner : GenericOwnerFile.Owner(extension, scalar);
        string owner = "Binary_" + extension + "_" + type;
        // A string element needs the profile, so its binary roots are generic in it.
        bool text = scalar == "string" || payload != null || fact.ForwardedType != null;
        bool measureRaw = !text && nonEmptyRawHeaderBytes != 0;
        string minimum = fact.ForwardedType != null ? (fact.Family.ContiguousElements && set.FindFormatter(scalar)?.RawCollection == true ?
            memory + ".SizeOf<" + scalar + ">()" : fact.Family.ContiguousElements && set.FindFormatter(scalar)?.Layout != null ? "0" :
            FormatterElementEmitter.Minimum(set.FindFormatter(scalar) ?? new FormatterModel(scalar, FormatterDefinitions.PayloadReference))) :
            text ? "1" : memory + ".SizeOf<" + scalar + ">()";
        FormatterModel? element = fact.ForwardedType == null ? null : set.FindFormatter(scalar) ?? new FormatterModel(scalar, FormatterDefinitions.PayloadReference);
        var context = new EnumerableOwnerContext(w, sequence, scalar, memory, marshal, payload, set, element, fact.Family.ContiguousElements);
        bool concurrentStack = sequence.StartsWith("global::System.Collections.Concurrent.ConcurrentStack<", StringComparison.Ordinal);

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots, "internal static partial class " + owner,
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead,
            () =>
            {
                if (element?.Layout != null && context.Contiguous)
                    FormatterStorageEmitter.Support(w, element.Layout);
            });

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + type + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWrite" + (text ? "<" + utf16 + ">" : "") + "(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite" + (text ? "<TLuminProfile>" : "") + "(ref writer, input);");
                    w.Blank();
                    if (measureRaw)
                    {
                        w.Line(preserve);
                        w.Line(inline);
                        w.Line("internal static void MeasureRecord_" + type + "<TLuminProfile>(");
                        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + sequence + " value)");
                        w.Line("    where TLuminProfile : struct, " + options);
                        w.Open();
                        w.Line("int count = value?.Count ?? 0;");
                        string header = nonEmptyRawHeaderBytes == 4 ? "sizeof(int)" :
                            "(count == 0 ? sizeof(int) : " + nonEmptyRawHeaderBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
                        w.Line("evaluator.Add(checked(" + header + " + count * " + memory + ".SizeOf<" + scalar + ">()));");
                        w.Close();
                        w.Blank();
                    }
                    if (spec.CompressEntries)
                    {
                        w.Line(preserve);
                        w.Line(inline);
                        w.Line("internal static void SerializeWithCompress(");
                        w.Line("    scoped ref " + writer + " writer,");
                        w.Line("    scoped in " + sequence + " input)");
                        w.Line("        => " + roots + ".RootWrite" + (text ? "<" + utf16 + ">" : "") + "(ref writer, input);");
                        w.Blank();
                        w.Line(preserve);
                        w.Line(inline);
                        w.Line("internal static void SerializeWithCompress<TLuminProfile>(");
                        w.Line("    scoped ref " + writer + " writer,");
                        w.Line("    scoped in " + sequence + " input)");
                        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite" + (text ? "<TLuminProfile>" : "") + "(ref writer, input);");
                        w.Blank();
                    }
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(measureRaw ? inline : w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWrite" + (text ? "<TLuminProfile>" : "") + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    " + sequence + " input)" + (text ? "" : ""));
                    if (text)
                        w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    if (measureRaw && directRawWrite != null)
                    {
                        directRawWrite(context);
                        w.Line("WriteFallback(ref writer, input);");
                        w.Close();
                        w.Blank();
                        w.Line(w.MethodImpl("NoInlining"));
                        w.Line("private static void WriteFallback(scoped ref " + writer + " writer, " + sequence + " input)");
                        w.Open();
                    }
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
                    spec.WriteBody(context);
                    w.Blank();
                    w.Line("operationComplete_RootWrite:");
                    w.Indented(() => w.Line(";"));
                    w.Line("operationState.CopyTo(ref writer);");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite" + (text ? "<" + utf16 + ">" : "") + "(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + sequence + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWrite" + (text ? "<TOptions>" : "") + "(ref state, value);");
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
                    w.Line("internal static void Deserialize_" + type + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootRead" + (text ? "<" + utf16 + ">" : "") + "(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead" + (text ? "<TLuminProfile>" : "") + "(ref reader, ref value);");
                    w.Blank();
                    if (spec.CompressEntries)
                    {
                        w.Line(preserve);
                        w.Line(inline);
                        w.Line("internal static void DeserializeWithCompress(");
                        w.Line("    scoped ref " + reader + " reader,");
                        w.Line("    scoped ref " + sequence + " value)");
                        w.Line("        => " + roots + ".RootRead" + (text ? "<" + utf16 + ">" : "") + "(ref reader, ref value);");
                        w.Blank();
                        w.Line(preserve);
                        w.Line(inline);
                        w.Line("internal static void DeserializeWithCompress<TLuminProfile>(");
                        w.Line("    scoped ref " + reader + " reader,");
                        w.Line("    scoped ref " + sequence + " value)");
                        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead" + (text ? "<TLuminProfile>" : "") + "(ref reader, ref value);");
                        w.Blank();
                    }
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    // The original scalar sequence interfaces leave the allocating read to the JIT's default budget.
                    if (inlineRawRead && !text)
                        w.Line(inline);
                    else if (spec.ReadInlining != null && (text || !spec.InterfaceListReuse))
                        w.Line(w.MethodImpl(spec.ReadInlining));
                    w.Line("internal static void RootRead" + (text ? "<TLuminProfile>" : "") + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    if (text)
                        w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    if (payload != null || fact.ForwardedType != null)
                        w.Line("ref var operationState = ref reader;");
                    else
                    {
                        w.Line(reader + " operationState = default;");
                        w.Line("reader.CopyTo(ref operationState);");
                    }
                    w.Line("int length = operationState.ReadLength(" + minimum + ");");
                    w.Line("if (length < 0)");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("goto operationComplete_RootRead;");
                    w.Close();
                    w.Blank();
                    if (context.Forwarded && context.Contiguous && element?.Layout != null)
                        FormatterStorageEmitter.Validate(w, element.Layout, "length", "operationState");
                    spec.ReadBody(context);
                    w.Blank();
                    w.Line("operationComplete_RootRead:");
                    w.Indented(() => w.Line(";"));
                    if (payload == null && fact.ForwardedType == null)
                        w.Line("operationState.CopyTo(ref reader);");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead" + (text ? "<" + utf16 + ">" : "") + "(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + sequence + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootRead" + (text ? "<TOptions>" : "") + "(ref state, ref value);");
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
                    w.Line("internal static void SerializeJson_" + type + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + sequence + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    if (spec.OutlineJson && (fact.Family.Kind != CollectionKind.Frozen ||
                        !JsonIntegerRunWriter.Supports(set, sequence, scalar)))
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
                    if (spec.JsonCount)
                    {
                        w.Line(concurrentStack ? "if (value.IsEmpty)" : "if (value.Count == 0)");
                        w.Open();
                        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                        w.Line("return;");
                        w.Close();
                    }
                    w.Blank();
                    if (context.Supports("BagSnapshot"))
                    {
                        ConcurrentBagOwnerFile.WriteJsonSnapshot(w, set, sequence, scalar, elementOwner, pooledSnapshot: true);
                        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                        w.Close();
                        w.Blank();
                        break;
                    }
                    if (fact.Family.Kind == CollectionKind.Queue && context.Supports("OrderedIntegerJson") &&
                        GenericFormatterReuse.Real(scalar) is "int" or "long")
                    {
                        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteIntegerQueueJson<TLuminProfile, " + scalar + ">(ref writer, value))");
                        w.Open();
                        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                        w.Line("return;");
                        w.Close();
                    }
                    if (fact.Family.Kind == CollectionKind.Wrapper)
                    {
                        WrapperTraversal.WriteJsonIntegers(context);
                        WrapperTraversal.WriteJsonStorage(context, elementOwner);
                    }
                    ImmutableLinearTraversal.WriteJson(context, set, elementOwner, jsonWriter);
                    if (ImmutableInterfaceOwnerFile.WriteJsonTree(context, set, elementOwner))
                    {
                        w.Close();
                        w.Blank();
                        break;
                    }
                    bool wroteFrozenRun = fact.Family.Kind == CollectionKind.Frozen &&
                        JsonIntegerRunWriter.TryEmit(w, set, sequence, scalar, "value.Items.AsSpan()", readBeforeSeparator: true);
                    if (spec.InterfaceListReuse || spec.InterfaceSetTraversal)
                        InterfaceJsonTraversal.Write(w, set, sequence, scalar, elementOwner, jsonWriter, spec.InterfaceSetTraversal);
                    else if (!wroteFrozenRun)
                    {
                        bool scopedFallback = context.Supports("CopyImmutableHashTree") || context.Supports("CopyImmutableSortedTree") ||
                            context.Supports("CopyImmutableListTree");
                        if (scopedFallback)
                            w.Open();
                        w.Line("bool isFirst = true;");
                        string source = concurrentStack ? context.Source("value") : "value";
                        w.Line("foreach (var item in " + source + ")");
                        w.Open();
                        if (fact.Family.Kind == CollectionKind.HashSet)
                            JsonSequenceElementWriter.Emit(w, set, sequence, scalar, elementOwner, utf8Only: true);
                        else
                        {
                            w.Line("if (!isFirst)");
                            w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
                            w.Line("else");
                            w.Line("    isFirst = false;");
                            w.Line("writer.SetFirstElement(true);");
                            w.Line("var temp = item;");
                            w.Line(elementOwner + ".Write<TLuminProfile>(ref writer, temp);");
                        }
                        w.Close();
                        if (scopedFallback)
                            w.Close();
                    }
                    w.Blank();
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Close();
                    w.Blank();
                    break;
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
            // The built-in integer formatter starts by replacing the destination from the canonical scalar reader.
            string? integerRead = GenericFormatterReuse.Real(scalar) is "int" or "long"
                ? "reader.Read<" + scalar + ", TLuminProfile>()" : null;
            // The streaming interface list loop has already selected an element token. An ordinary generated record's
            // state entry only selects a token if needed, calls its read root and completes the value.
            string? itemRoot = null;
            if (spec.InterfaceListReuse && element is { OrdinaryObject: true, Definition: FormatterDefinitions.PayloadReference, Payload: RecordModel record } &&
                !record.Identity.IsGeneric && scalar.IndexOf('<') < 0 &&
                RenderModelFactory.Create(record, set.Options, json: true) is { Rejection: null } itemPayload)
                itemRoot = serializer + ".LuminRoots_" + itemPayload.Readable + ".RootReadJson<TLuminProfile>";

            void ReadItem(string seed, bool selected = false)
            {
                if (integerRead != null)
                {
                    w.Line(scalar + " item = " + integerRead + ";");
                    return;
                }
                w.Line(scalar + " item = " + seed + ";");
                if (selected && itemRoot != null)
                {
                    w.Line(itemRoot + "(ref reader, ref item);");
                    w.Line("reader.CompleteValue();");
                }
                else
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
            }

            void AppendJsonItem()
            {
                if (spec.JsonAddBody != null)
                    spec.JsonAddBody(context);
                else
                    w.Line(spec.JsonAdd);
            }

            void ReadJsonItems(Action append, bool hasFirstToken)
            {
                w.Line(hasFirstToken ? "do" : "while (reader.Read<TLuminProfile>())");
                w.Open();
                w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                w.Line("    break;");
                w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                w.Line("    continue;");
                ReadItem("default !");
                append();
                w.Close();
                if (hasFirstToken)
                    w.Line("while (reader.Read<TLuminProfile>());");
            }

            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + type + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + type + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + sequence + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    if (spec.OutlineJson)
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
                    string? immutableIntegerRead = integerRead != null &&
                        set.FindFormatter(scalar) is { Definition: FormatterDefinitions.Scalar, Underlying: null } ? integerRead : null;
                    if (ImmutableInterfaceOwnerFile.ReadJsonTree(context, elementOwner, jsonReader, immutableIntegerRead))
                    {
                        w.Close();
                        w.Blank();
                        break;
                    }
                    if (spec.InterfaceListReuse)
                    {
                        // The original interface read refills an existing array of the same exact length in place and otherwise
                        // streams into an existing list of the exact type, or a new one, whose tail it trims.
                        string list = "global::System.Collections.Generic.List<" + scalar + ">";
                        string array = ContiguousOwnerFile.ArrayType(scalar);
                        string capacity = context.Supports("InterfaceJsonCapacity") ? "reader.GetArrayCapacityHint<TLuminProfile>(10)" : string.Empty;
                        w.Line("if (value is " + array + " existingArray && existingArray.GetType() == typeof(" + array + ") &&");
                        w.Line("    reader.CountArrayElements<TLuminProfile>() == existingArray.Length)");
                        w.Open();
                        ReadJsonArraySlots("existingArray", integerRead);
                        w.Blank();
                        w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
                        w.Line("return;");
                        w.Close();
                        w.Blank();
                        w.Line("var list = value as " + list + ";");
                        w.Line("if (list is null || list.GetType() != typeof(" + list + "))");
                        w.Line("    list = new " + list + "(" + capacity + ");");
                        w.Blank();
                        w.Line("int count = 0;");
                        w.Line("while (reader.Read<TLuminProfile>())");
                        w.Open();
                        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                        w.Line("    break;");
                        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                        w.Line("    continue;");
                        ReadItem("count < list.Count ? list[count] : default!", selected: true);
                        w.Line("if (count < list.Count)");
                        w.Line("    list[count] = item;");
                        w.Line("else");
                        w.Line("    list.Add(item);");
                        w.Line("count++;");
                        w.Close();
                        w.Blank();
                        w.Line("if (count < list.Count)");
                        w.Line("    list.RemoveRange(count, list.Count - count);");
                        w.Line("value = list;");
                        w.Close();
                        w.Blank();
                        break;
                    }
                    if (spec.ConcurrentStackStaging && context.Supports("StageConcurrentStack"))
                    {
                        ConcurrentStackJsonReader.Emit(w, sequence, scalar, elementOwner, jsonReader, clear: true);
                        w.Close();
                        w.Blank();
                        break;
                    }
                    if (fact.Family.Kind == CollectionKind.Stack && context.Supports("RestoreStackFromTop"))
                    {
                        StackJsonStaging.Emit(w, sequence, scalar, elementOwner, jsonReader, clear: true, bulkRestore: true);
                        w.Close();
                        w.Blank();
                        break;
                    }
                    if (spec.ArrayReuse && (context.Payload != null || context.Text))
                    {
                        // The original array read refills an existing array of the same exact length in place and otherwise
                        // stages the elements in a pooled array.
                        string pool = "global::System.Buffers.ArrayPool<" + scalar + ">.Shared";
                        w.Line("if (value is not null && value.GetType() == typeof(" + sequence + ") &&");
                        w.Line("    reader.CountArrayElements<TLuminProfile>() == value.Length)");
                        w.Open();
                        ReadJsonArraySlots("value", null);
                        w.Blank();
                        w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
                        w.Line("return;");
                        w.Close();
                        w.Blank();
                        w.Line(scalar + "[] buffer = " + pool + ".Rent(4);");
                        w.Line("int count = 0;");
                        w.Line("int capacity = buffer.Length;");
                        w.Line("try");
                        w.Open();
                        w.Line("while (true)");
                        w.Open();
                        w.Line("if (!reader.Read<TLuminProfile>())");
                        w.Line("    break;");
                        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                        w.Line("    break;");
                        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                        w.Line("    continue;");
                        w.Line("if (count >= capacity)");
                        w.Open();
                        w.Line("int newCapacity = capacity * 2;");
                        w.Line("var newBuffer = " + pool + ".Rent(newCapacity);");
                        w.Line("global::System.Array.Copy(buffer, newBuffer, count);");
                        w.Line(pool + ".Return(buffer, clearArray: true);");
                        w.Line("buffer = newBuffer;");
                        w.Line("capacity = newBuffer.Length;");
                        w.Close();
                        w.Blank();
                        w.Line(scalar + " item = default!;");
                        w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                        w.Line(memory + ".Add(ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(buffer), (nint)(uint)count++) = item;");
                        w.Close();
                        w.Blank();
                        w.Line("value = global::LuminPack.Code.LuminPackMarshal.CreateReadOnlySpan(ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(buffer), count).ToArray();");
                        w.Close();
                        w.Line("finally");
                        w.Open();
                        w.Line(pool + ".Return(buffer, clearArray: true);");
                        w.Close();
                        w.Close();
                        w.Blank();
                        break;
                    }
                    if (spec.ListReuse)
                    {
                        // The original list read streams its elements once: existing elements are read in place and the tail is trimmed.
                        w.Line("if (value is null || value.GetType() != typeof(" + sequence + "))");
                        w.Open();
                        w.Line("value = new " + sequence + "(reader.GetArrayCapacityHint<TLuminProfile>(10));");
                        w.Close();
                        w.Blank();
                        w.Line("int count = 0;");
                        w.Line("while (reader.Read<TLuminProfile>())");
                        w.Open();
                        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                        w.Line("    break;");
                        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                        w.Line("    continue;");
                        ReadItem("count < value.Count ? value[count] : default!");
                        w.Line("if (count < value.Count)");
                        w.Line("    value[count] = item;");
                        w.Line("else");
                        w.Line("    value.Add(item);");
                        w.Line("count++;");
                        w.Close();
                        w.Blank();
                        w.Line("if (count < value.Count)");
                        w.Line("    value.RemoveRange(count, value.Count - count);");
                        w.Close();
                        w.Blank();
                        break;
                    }
                    w.Blank();
                    if (spec.JsonEmptyBeforeStaging)
                    {
                        w.Line("if (!reader.Read<TLuminProfile>() || reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                        w.Open();
                        w.Line("value = " + sequence + ".Empty;");
                        w.Line("return;");
                        w.Close();
                        w.Blank();
                    }
                    if (spec.JsonBefore != null)
                        spec.JsonBefore(context);
                    else
                    {
                        w.Line("if (value is null)");
                        w.Indented(() => w.Line("value = new " + sequence + "();"));
                        w.Line("else");
                        w.Indented(() => w.Line("value.Clear();"));
                    }
                    w.Blank();
                    bool observableBacking = spec.JsonObservableBacking && !context.Counted && context.Supports("WrapperJsonStorage");
                    if (observableBacking)
                    {
                        w.Line("if (backing is not null)");
                        w.Open();
                        ReadJsonItems(() => w.Line("backing.Add(item);"), false);
                        w.Close();
                        w.Line("else");
                        w.Open();
                        w.Line("ReadPortableJson<TLuminProfile>(ref reader, staged);");
                        w.Close();
                    }
                    else
                        ReadJsonItems(AppendJsonItem, spec.JsonEmptyBeforeStaging);
                    spec.JsonAfter?.Invoke(context);
                    w.Close();
                    w.Blank();
                    if (observableBacking)
                    {
                        w.Line(w.MethodImpl("NoInlining"));
                        w.Line("private static void ReadPortableJson<TLuminProfile>(");
                        w.Line("    scoped ref " + jsonReader + " reader,");
                        w.Line("    global::System.Collections.ObjectModel.ObservableCollection<" + scalar + "> staged)");
                        w.Line("    where TLuminProfile : struct, " + options);
                        w.Open();
                        ReadJsonItems(AppendJsonItem, false);
                        w.Close();
                        w.Blank();
                    }
                    break;
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

        void ReadJsonArraySlots(string array, string? directRead)
        {
            // Each caller has checked exact array identity and length before borrowing its slots.
            w.Line("int length = " + array + ".Length;");
            w.Line("if (length != 0)");
            w.Open();
            w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(" + array + ");");
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            w.Line("reader.Read<TLuminProfile>();");
            string slot = memory + ".Add(ref first, (nint)(uint)index)";
            w.Line(directRead == null ? elementOwner + ".Read<TLuminProfile>(ref reader, ref " + slot + ");" : slot + " = " + directRead + ";");
            w.Close();
            w.Line("while (++index < length);");
            w.Close();
        }
    }
}
