using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the standalone owners of <c>Memory&lt;T&gt;</c>, <c>ReadOnlyMemory&lt;T&gt;</c>, <c>ArraySegment&lt;T&gt;</c> and
/// <c>ReadOnlySequence&lt;T&gt;</c> of raw scalars or strings.</summary>
/// <remarks>The wire is the list wire: the element count followed by the elements. A raw element span is written as one block, a
/// string span element by element, and a sequence segment by segment. An array segment without an array stores the null collection
/// header. The read allocates an uninitialized array, fills it in place and wraps it; the JSON read stages the elements in a
/// list.</remarks>
internal static class MemoryOwnerFile
{
    /// <summary>Renders the owner.</summary>
    /// <param name="definition">The memory definition.</param>
    /// <param name="element">The element keyword or <c>string</c>.</param>
    /// <param name="declared">The fully qualified closed type.</param>
    /// <param name="shortName">The readable owner suffix.</param>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(string definition, string element, string declared, string shortName, SharedFormatterSet set,
        GeneratedOperations operations)
    {
        string extension = set.Extension!;
        string owner = "global::LuminPack." + GeneratedNames.Owner(extension, element);
        return SpecialOwnerFile.Render(set, operations, shortName, declared, false,
            (w, c) => Write(w, c, definition, element),
            (w, c) => Read(w, c, definition, element, declared),
            (w, c) => JsonWrite(w, c, definition, element, owner, set, declared),
            (w, c) => JsonRead(w, c, element, declared, owner),
            outlineJson: true, outlineBinaryWrite: definition == GenericIdentity.ReadOnlySequence || element == "string");
    }

    private static string Span(string definition, string element) => definition switch
    {
        GenericIdentity.ArraySegment => "new global::System.ReadOnlySpan<" + element + ">(value.Array, value.Offset, value.Count)",
        _ => "value.Span",
    };

    private static void WriteElement(CodeWriter w, SpecialOwnerContext c, string element, string local)
    {
        if (element == "string")
            w.Line("writer.Write<" + c.Profile + ">(" + local + ");");
        else
        {
            w.Line("writer.CheckBuffer();");
            w.Line("writer.Write(in " + local + ");");
        }
    }

    private static void Write(CodeWriter w, SpecialOwnerContext c, string definition, string element)
    {
        if (definition == GenericIdentity.ArraySegment)
        {
            w.Line("if (value.Array is null)");
            w.Open();
            w.Line("writer.EnsureAdditionalCapacity(4);");
            w.Line("global::System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref writer.Cursor, global::LuminPack.Code.LuminPackCode.NullCollection);");
            w.Line("writer.Advance(4);");
            w.Line("return;");
            w.Close();
            w.Blank();
        }

        if (definition == GenericIdentity.ReadOnlySequence)
        {
            w.Line("int countPosition = writer.BeginCollection();");
            w.Line("int count = 0;");
            w.Line("var position = value.Start;");
            w.Line("while (value.TryGet(ref position, out var memory))");
            w.Open();
            w.Line("foreach (var item in memory.Span)");
            w.Open();
            w.Line("count++;");
            w.Line(element + " current = item;");
            WriteElement(w, c, element, "current");
            w.Close();
            w.Close();
            w.Line("writer.EndCollection(countPosition, count);");
            w.Line("writer.CheckBuffer();");
            return;
        }

        if (element != "string")
        {
            w.Line("global::System.ReadOnlySpan<" + element + "> items = " + Span(definition, element) + ";");
            w.Line("writer.Write<" + element + ">(items, items.Length);");
            w.Line("writer.CheckBuffer();");
            return;
        }

        w.Line("global::System.ReadOnlySpan<string> span = " + Span(definition, element) + ";");
        w.Line("int countPosition = writer.BeginCollection();");
        w.Line("if (!span.IsEmpty)");
        w.Open();
        w.Line("ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("writer.Write<" + c.Profile + ">(global::System.Runtime.CompilerServices.Unsafe.Add(ref first, (nint)(uint)index));");
        w.Close();
        w.Line("while (++index < span.Length);");
        w.Close();
        w.Line("writer.EndCollection(countPosition, span.Length);");
        w.Line("writer.CheckBuffer();");
    }

    private static void Read(CodeWriter w, SpecialOwnerContext c, string definition, string element, string declared)
    {
        string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        w.Line("int length = reader.ReadLength(" + (element == "string" ? "1" : unsafeType + ".SizeOf<" + element + ">()") + ");");
        w.Line("if (length < 0)");
        w.Open();
        w.Line("value = default;");
        w.Line("return;");
        w.Close();
        w.Blank();
        const string marshal = "global::LuminPack.Code.LuminPackMarshal";
        w.Line("var array = " + marshal + ".AllocateUninitializedArray<" + element + ">(length);");
        if (element == "string")
        {
            w.Line("if (length != 0)");
            w.Open();
            w.Line("ref var first = ref " + marshal + ".GetNotNullArrayReference(array);");
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            w.Line("reader.Read<" + c.Profile + ">(ref " + unsafeType + ".Add(ref first, (nint)(uint)index));");
            w.Close();
            w.Line("while (++index < length);");
            w.Close();
        }
        else
        {
            w.Line("if (length != 0)");
            w.Indented(() => w.Line("reader.Read(ref " + unsafeType + ".As<" + element + ", byte>(ref " + marshal + ".GetNotNullArrayReference(array)), unchecked(length * " +
                unsafeType + ".SizeOf<" + element + ">()));"));
        }
        w.Line("value = new " + declared + "(array);");
    }

    private static void JsonWrite(CodeWriter w, SpecialOwnerContext c, string definition, string element, string owner,
        SharedFormatterSet set, string declared)
    {
        if (definition == GenericIdentity.ArraySegment)
        {
            w.Line("if (value.Array is null)");
            w.Open();
            w.Line("writer.WriteNull<" + c.Profile + ">();");
            w.Line("return;");
            w.Close();
            w.Blank();
        }
        w.Line("writer.WriteArrayStart<" + c.Profile + ">();");
        bool run = JsonIntegerRunWriter.Supports(set, declared, element);
        if (!run)
            w.Line("bool isFirst = true;");
        if (definition == GenericIdentity.ReadOnlySequence)
        {
            w.Line("var position = value.Start;");
            w.Line("while (value.TryGet(ref position, out var memory))");
            w.Open();
            WriteItems("memory.Span");
            w.Close();
        }
        else
        {
            WriteItems(Span(definition, element));
        }
        w.Line("writer.WriteArrayEnd<" + c.Profile + ">();");

        void WriteItems(string items)
        {
            if (!JsonIntegerRunWriter.TryEmit(w, set, declared, element, items, readBeforeSeparator: true, c.Profile))
            {
                w.Line("foreach (var item in " + items + ")");
                JsonElement(w, c, element, owner);
            }
        }
    }

    private static void JsonElement(CodeWriter w, SpecialOwnerContext c, string element, string owner)
    {
        w.Open();
        w.Line("if (!isFirst)");
        w.Line("    writer.WriteByteRaw<" + c.Profile + ">((byte)',');");
        w.Line("else");
        w.Line("    isFirst = false;");
        w.Line("writer.SetFirstElement(true);");
        w.Line(element + " temp = item;");
        w.Line(owner + ".Write<" + c.Profile + ">(ref writer, temp);");
        w.Close();
    }

    private static void JsonRead(CodeWriter w, SpecialOwnerContext c, string element, string declared, string owner)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = default;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("reader.TryConsumeArrayStart<" + c.Profile + ">();");
        w.Line("var staged = new global::System.Collections.Generic.List<" + element + ">();");
        w.Line("while (reader.Read<" + c.Profile + ">())");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ArrayEnd)");
        w.Line("    break;");
        w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ObjectEnd)");
        w.Line("    continue;");
        w.Line(element + " item = default !;");
        w.Line(owner + ".Read<" + c.Profile + ">(ref reader, ref item);");
        w.Line("staged.Add(item);");
        w.Close();
        w.Line("value = new " + declared + "(staged.ToArray());");
    }
}
