using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>
/// Shared binary statement shapes for collection emitters. Every shape writes the established
/// count header followed by the elements in traversal order, so the wire format is unchanged;
/// only the traversal and copy strategy differ.
/// </summary>
internal static class CollectionEmitterKit
{
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";
    private const string MemoryMarshal = "global::System.Runtime.InteropServices.MemoryMarshal";

    /// <summary>Determines whether elements are written as their complete raw storage.</summary>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <returns>Whether a span of elements can be copied as one block.</returns>
    /// <remarks>Matches the List emitter: proven unmanaged storage without a Custom or portable contract, or a known scalar when the symbol is unavailable.</remarks>
    internal static bool IsRaw(ITypeSymbol element, string elementType)
    {
        return EmitterTypeTraits.TryGetIsUnmanaged(element, out bool unmanaged)
            ? unmanaged
            : CodeEmitterRegistry.KnownValueTypes.Contains(elementType);
    }

    /// <summary>Checks whether a scalar element formatter has the same bytes as its complete native storage.</summary>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element spelling.</param>
    /// <returns>Whether a scalar-written element block can use the raw reader.</returns>
    internal static bool IsRawScalar(ITypeSymbol element, string elementType)
    {
        if (!IsRaw(element, elementType)) return false;
        // A proven unmanaged element is raw only when its own formatter is a plain storage copy: Nullable, KeyValuePair and
        // specialized-mode structs are framed by their formatter. An unclassifiable element reached IsRaw as a known scalar.
        if (element is not null && EmitterTypeTraits.TryGetIsUnmanaged(element, out bool unmanaged) && unmanaged)
            return EmitterTypeTraits.FormatterIsRawStorage(element);
        return !elementType.StartsWith("global::System.Collections.Generic.KeyValuePair<", System.StringComparison.Ordinal);
    }

    /// <summary>Writes one local using the same element representation as a contiguous sequence.</summary>
    /// <param name="element">The collection element symbol.</param>
    /// <param name="elementType">The generated element spelling.</param>
    /// <param name="local">The readable local holding the element.</param>
    /// <returns>A complete statement retaining raw, portable or Custom element selection.</returns>
    internal static string ContiguousElementWrite(ITypeSymbol element, string elementType, string local)
        => IsRaw(element, elementType)
            ? "writer.Write(ref " + Unsafe + ".As<" + elementType + ", byte>(ref " + Unsafe + ".AsRef(in " + local + ")), " + Unsafe + ".SizeOf<" + elementType + ">());"
            : "writer.WriteValue(in " + local + ");";

    /// <summary>Reads one local using the selected contiguous element contract.</summary>
    /// <param name="element">The collection element symbol.</param>
    /// <param name="elementType">The generated element spelling.</param>
    /// <param name="local">The destination local.</param>
    /// <returns>A fixed block copy for raw storage, or the original formatter operation.</returns>
    internal static string ContiguousElementRead(ITypeSymbol element, string elementType, string local)
        => IsRaw(element, elementType)
            ? "reader.Read(ref " + Unsafe + ".As<" + elementType + ", byte>(ref " + local + "), " + Unsafe + ".SizeOf<" + elementType + ">());"
            : "reader.ReadValue(ref " + local + ");";

    /// <summary>Determines whether an element write performs its own buffer completion check.</summary>
    /// <param name="element">The element symbol.</param>
    /// <returns>Whether the per-element buffer check can be omitted.</returns>
    private static bool ChecksItself(ITypeSymbol element) => element?.SpecialType == SpecialType.System_String;

    /// <summary>Offers the aligned chunk starting at an element index to a Union partition kernel at the top of an element loop.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="indent">The loop body indentation.</param>
    /// <param name="index">The loop's element index, an <see cref="int"/> or <see cref="nint"/>.</param>
    /// <param name="length">The element count expression.</param>
    /// <param name="call">The kernel invocation over <c>chunkLength</c> elements starting at <paramref name="index"/>.</param>
    /// <remarks>A declined chunk keeps the loop's ordinary element statements; an accepted chunk advances the index past it.</remarks>
    internal static void AppendPartitionChunk(StringBuilder sb, string indent, string index, string length, string call)
    {
        sb.AppendLine($"{indent}if (({index} & (global::LuminPack.Code.LuminUnionPartition.ChunkLength - 1)) == 0 && {length} - {index} >= global::LuminPack.Code.LuminUnionPartition.MinimumLength)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    int chunkLength = (int)global::System.Math.Min(global::LuminPack.Code.LuminUnionPartition.ChunkLength, {length} - {index});");
        sb.AppendLine($"{indent}    if ({call}) {{ {index} += chunkLength - 1; continue; }}");
        sb.AppendLine($"{indent}}}");
    }

    /// <summary>Writes a count header and the elements of a span.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <param name="span">An expression producing a <c>Span</c> or <c>ReadOnlySpan</c> of the elements.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="scalarElements">Whether the reader dispatches individual element formatters, as <see cref="AppendSpanRead"/> selects.</param>
    /// <param name="partition">The eligible Union root whose aligned chunks are offered to its partition kernel, or null.</param>
    internal static void AppendSpanWrite(StringBuilder sb, ITypeSymbol element, string elementType, string span, string indent, bool scalarElements = false,
        LuminPack.Code.Core.UnionPartitionPlan partition = null)
    {
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    global::System.ReadOnlySpan<" + elementType + "> __items = " + span + ";");
        if (scalarElements ? IsRawScalar(element, elementType) : IsRaw(element, elementType))
        {
            var raw = new StringBuilder();
            EmitterTypeTraits.AppendRawCollectionWrite(raw, element, elementType, "__items", "__items.Length");
            foreach (var line in raw.ToString().Split('\n'))
                if (line.Trim().Length != 0) sb.AppendLine(indent + "    " + line.Trim());
        }
        else if (PortableStructPlan.TryCreateContiguous(element) is { } portable)
        {
            sb.AppendLine(indent + "    int __portableCount = __items.Length;");
            sb.AppendLine(indent + "    writer.EnsureAdditionalCapacity(checked(4 + " + PortableStructEmitter.PayloadSize(portable, "__portableCount") + "));");
            sb.AppendLine(indent + "    writer.Write(__portableCount);");
            PortableStructEmitter.AppendPayload(sb, portable, elementType, MemoryMarshal + ".GetReference(__items)", "__portableCount", write: true);
            sb.AppendLine(indent + "    writer.CheckBuffer();");
        }
        else if (element?.SpecialType == SpecialType.System_String)
        {
            sb.AppendLine(indent + "    writer.EnsureAdditionalCapacity(4);");
            sb.AppendLine(indent + "    writer.Write(__items.Length);");
            sb.AppendLine(indent + "    writer.WriteStrings(__items);");
            sb.AppendLine(indent + "    writer.CheckBuffer();");
        }
        else
        {
            sb.AppendLine(indent + "    writer.EnsureAdditionalCapacity(4);");
            sb.AppendLine(indent + "    writer.Write(__items.Length);");
            sb.AppendLine(indent + "    for (int __i = 0; __i < __items.Length; __i++)");
            sb.AppendLine(indent + "    {");
            if (partition != null)
                AppendPartitionChunk(sb, indent + "        ", "__i", "__items.Length",
                    $"{partition.Root}.__LuminPackUnionPartitionWrite_{partition.Suffix}(ref writer, __items.Slice(__i, chunkLength))");
            if (!ChecksItself(element)) sb.AppendLine(indent + "        writer.CheckBuffer();");
            sb.AppendLine(indent + "        writer.WriteValue(in __items[__i]!);");
            sb.AppendLine(indent + "    }");
            sb.AppendLine(indent + "    writer.CheckBuffer();");
        }
        sb.AppendLine(indent + "}");
    }

    /// <summary>Writes a counted collection by copying it into pooled contiguous storage first.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <param name="count">An expression producing the exact element count.</param>
    /// <param name="copyTo">A format string copying the elements into array <c>{0}</c> from index zero.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="scalarElements">Whether the reader dispatches individual element formatters, as <see cref="AppendSpanRead"/> selects.</param>
    /// <remarks>Used for collections whose bulk copy is a tree walk or array copy while their enumerator pays a per-element version check, stack or interface call.</remarks>
    internal static void AppendPooledCopyWrite(StringBuilder sb, ITypeSymbol element, string elementType, string count, string copyTo, string indent, bool scalarElements = false)
    {
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    int __count = " + count + ";");
        sb.AppendLine(indent + "    if (__count == 0) { writer.EnsureAdditionalCapacity(4); writer.Write(0); }");
        sb.AppendLine(indent + "    else");
        sb.AppendLine(indent + "    {");
        sb.AppendLine(indent + "        var __pooled = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(__count);");
        sb.AppendLine(indent + "        try");
        sb.AppendLine(indent + "        {");
        sb.AppendLine(indent + "            " + string.Format(copyTo, "__pooled") + ";");
        AppendSpanWrite(sb, element, elementType, "new global::System.ReadOnlySpan<" + elementType + ">(__pooled, 0, __count)", indent + "            ", scalarElements);
        sb.AppendLine(indent + "        }");
        sb.AppendLine(indent + "        finally");
        sb.AppendLine(indent + "        {");
        sb.AppendLine(indent + "            global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(__pooled, " + ClearExpression(element, elementType) + ");");
        sb.AppendLine(indent + "        }");
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "}");
    }

    /// <summary>Writes an enumeration with a count header published after the traversal.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="item">The loop variable declaration, such as <c>var item</c>.</param>
    /// <param name="source">The enumerated expression.</param>
    /// <param name="body">The element write statements, each without indentation.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="element">The element of a single-element body, whose own write may already complete the buffer check.</param>
    /// <remarks>Counts the elements actually written, so concurrent collections need neither a lock-taking count nor a consistency check.</remarks>
    internal static void AppendCountedWrite(StringBuilder sb, string item, string source, string[] body, string indent, ITypeSymbol element = null)
    {
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    int __countPosition = writer.BeginCollection();");
        sb.AppendLine(indent + "    int __count = 0;");
        sb.AppendLine(indent + "    foreach (" + item + " in " + source + ")");
        sb.AppendLine(indent + "    {");
        if (!ChecksItself(element)) sb.AppendLine(indent + "        writer.CheckBuffer();");
        sb.AppendLine(indent + "        __count++;");
        foreach (var line in body) sb.AppendLine(indent + "        " + line);
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "    writer.EndCollection(__countPosition, __count);");
        sb.AppendLine(indent + "    writer.CheckBuffer();");
        sb.AppendLine(indent + "}");
    }

    /// <summary>Writes an immutable queue through its two located stacks, or through its enumerator otherwise.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The element symbol.</param>
    /// <param name="queue">An expression producing the exact non-null queue.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <remarks>The stacks are the ones the queue's enumerator walks, in the same order and with the same count.</remarks>
    internal static void AppendImmutableQueueWrite(StringBuilder sb, ITypeSymbol element, string queue, string indent)
    {
        string[] body = { "var v = item;", "writer.WriteValue(in v);" };
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetImmutableQueueStacks(" + queue + ", out var __forwards, out var __backwards))");
        sb.AppendLine(indent + "    {");
        sb.AppendLine(indent + "        int __countPosition = writer.BeginCollection();");
        sb.AppendLine(indent + "        int __count = 0;");
        foreach (string stack in new[] { "__forwards!", "__backwards!" })
        {
            sb.AppendLine(indent + "        foreach (var item in " + stack + ")");
            sb.AppendLine(indent + "        {");
            if (!ChecksItself(element)) sb.AppendLine(indent + "            writer.CheckBuffer();");
            sb.AppendLine(indent + "            __count++;");
            foreach (var line in body) sb.AppendLine(indent + "            " + line);
            sb.AppendLine(indent + "        }");
        }
        sb.AppendLine(indent + "        writer.EndCollection(__countPosition, __count);");
        sb.AppendLine(indent + "        writer.CheckBuffer();");
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "    else");
        AppendCountedWrite(sb, "var item", queue, body, indent + "    ", element);
        sb.AppendLine(indent + "}");
    }

    /// <summary>Declares <c>length</c> from the collection header, validating a raw payload before any allocation.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <param name="nullStatement">The statement assigning the null result, without the return.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="scalarElements">Whether the writer dispatches individual element formatters.</param>
    internal static void AppendReadLength(StringBuilder sb, ITypeSymbol element, string elementType, string nullStatement, string indent, bool scalarElements = false)
    {
        string minimum = MinimumElementSize(element, elementType, scalarElements);
        sb.AppendLine(indent + "int length = reader.ReadLength(" + minimum + ");");
        sb.AppendLine(indent + "if (length < 0) { " + nullStatement + " return; }");
    }

    /// <summary>Gets a proven minimum element width for validation before allocating a collection.</summary>
    /// <param name="element">The canonical element symbol.</param>
    /// <param name="elementType">The generated element spelling.</param>
    /// <param name="scalarElements">Whether the writer selects individual element formatters.</param>
    /// <returns>A native or portable width expression, including zero for packed empty elements; otherwise one.</returns>
    /// <remarks>A forged count is therefore rejected before an allocation sized from it, instead of after reserving memory for
    /// elements the input cannot contain.</remarks>
    internal static string MinimumElementSize(ITypeSymbol element, string elementType, bool scalarElements)
        => (scalarElements ? IsRawScalar(element, elementType) : IsRaw(element, elementType)) ? Unsafe + ".SizeOf<" + elementType + ">()" :
            PortableStructPlan.TryCreateContiguous(element)?.WireSize.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1";

    /// <summary>Selects a zero-width header only when every element component is proven packed and empty.</summary>
    /// <param name="collection">The closed array or collection symbol whose header is being emitted.</param>
    /// <returns>An explicit zero-width argument, or an empty suffix retaining the ordinary validation.</returns>
    internal static string HeaderMinimumArgument(ITypeSymbol collection)
    {
        if (collection is IArrayTypeSymbol array)
            return PortableStructPlan.TryCreateContiguous(array.ElementType) is { WireSize: 0 } ? ", 0" : "";
        if (collection is not INamedTypeSymbol named || named.TypeArguments.Length == 0) return "";
        foreach (var element in named.TypeArguments)
            if (PortableStructPlan.TryCreateContiguous(element) is not { WireSize: 0 }) return "";
        return ", 0";
    }

    /// <summary>Reads <c>length</c> elements into an already allocated destination.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <param name="span">An expression producing the writable destination span of exactly <c>length</c> elements.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="foreign">Whether the destination may hold stale values that must not be merged into, such as shared pool storage.</param>
    /// <param name="scalarElements">Whether the writer dispatches individual element formatters.</param>
    /// <remarks>The raw form relies on <see cref="AppendReadLength"/> having validated the complete payload.</remarks>
    internal static void AppendSpanRead(StringBuilder sb, ITypeSymbol element, string elementType, string span, string indent, bool foreign = false, bool scalarElements = false)
    {
        sb.AppendLine(indent + "{");
        if (scalarElements ? IsRawScalar(element, elementType) : IsRaw(element, elementType))
        {
            sb.AppendLine(indent + "    global::System.Span<" + elementType + "> __items = " + span + ";");
            sb.AppendLine(indent + "    if (length != 0) reader.Read(ref " + Unsafe + ".As<" + elementType + ", byte>(ref " + MemoryMarshal +
                ".GetReference(__items)), unchecked(length * " + Unsafe + ".SizeOf<" + elementType + ">()));");
        }
        else if (PortableStructPlan.TryCreateContiguous(element) is { } portable)
        {
            PortableStructEmitter.AppendPayload(sb, portable, elementType, MemoryMarshal + ".GetReference<" + elementType + ">(" + span + ")", "length", write: false);
        }
        else
        {
            sb.AppendLine(indent + "    ref var __first = ref " + MemoryMarshal + ".GetReference<" + elementType + ">(" + span + ");");
            if (foreign)
            {
                sb.AppendLine(indent + "    for (int __i = 0; __i < length; __i++)");
                sb.AppendLine(indent + "    {");
                sb.AppendLine(indent + "        " + elementType + " __item = default!;");
                sb.AppendLine(indent + "        reader.ReadValue(ref __item);");
                sb.AppendLine(indent + "        " + Unsafe + ".Add(ref __first, __i) = __item;");
                sb.AppendLine(indent + "    }");
            }
            else
                sb.AppendLine(indent + "    for (int __i = 0; __i < length; __i++) reader.ReadValue(ref " + Unsafe + ".Add(ref __first, __i)!);");
        }
        sb.AppendLine(indent + "}");
    }

    /// <summary>Reads <c>length</c> elements into pooled storage, consumes it, and returns it to the pool.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <param name="consume">Statements using the pooled array <c>__pooled</c>, whose first <c>length</c> elements are valid.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="scalarElements">Whether the writer dispatches individual element formatters.</param>
    /// <param name="onReadFailure">Optional statements using <c>__pooled</c> and <c>__completed</c> to restore the original completed prefix before rethrowing.</param>
    internal static void AppendPooledRead(StringBuilder sb, ITypeSymbol element, string elementType, string[] consume, string indent,
        bool scalarElements = false, string[] onReadFailure = null)
    {
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    var __pooled = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(length);");
        sb.AppendLine(indent + "    try");
        sb.AppendLine(indent + "    {");
        if (onReadFailure == null)
            AppendSpanRead(sb, element, elementType, "__pooled.AsSpan(0, length)", indent + "        ", foreign: true, scalarElements: scalarElements);
        else
        {
            // The same shared element pool serves fresh and reusable consumers. Individual reads retain the
            // original formatter and record exactly the prefix completed before an element failure.
            sb.AppendLine(indent + "        int __completed = 0;");
            sb.AppendLine(indent + "        try");
            sb.AppendLine(indent + "        {");
            sb.AppendLine(indent + "            for (; __completed < length; __completed++)");
            sb.AppendLine(indent + "            {");
            sb.AppendLine(indent + "                " + elementType + " __item = default!;");
            sb.AppendLine(indent + "                " + (scalarElements ? "reader.ReadValue(ref __item);" : ContiguousElementRead(element, elementType, "__item")));
            sb.AppendLine(indent + "                __pooled[__completed] = __item;");
            sb.AppendLine(indent + "            }");
            sb.AppendLine(indent + "        }");
            sb.AppendLine(indent + "        catch (global::System.Exception __error)");
            sb.AppendLine(indent + "        {");
            foreach (var line in onReadFailure) sb.AppendLine(indent + "            " + line);
            sb.AppendLine(indent + "            global::LuminPack.Code.LuminPackExceptionHelper.Rethrow(__error);");
            sb.AppendLine(indent + "        }");
        }
        foreach (var line in consume) sb.AppendLine(indent + "        " + line);
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "    finally");
        sb.AppendLine(indent + "    {");
        sb.AppendLine(indent + "        global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(__pooled, " + ClearExpression(element, elementType) + ");");
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "}");
    }

    /// <summary>Gets the pooled-array clearing argument for the element type.</summary>
    /// <param name="element">The element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <returns>A constant when the element classification is known; otherwise a runtime test.</returns>
    internal static string ClearExpression(ITypeSymbol element, string elementType)
    {
        if (element?.IsUnmanagedType == true) return "false";
        if (element?.IsReferenceType == true) return "true";
        return "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">()";
    }
}
