#nullable disable
using System.Collections.Generic;
using System.Globalization;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes the collection members whose binary protocols expand in place: stacks, sorted sets, sorted lists,
/// key/value pair lists, nested lists and dictionaries with nullable raw values.</summary>
internal static partial class BinaryEmitter
{
    /// <summary>Whether a member writes its own block in place instead of entering the surrounding reservation.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>Whether the input is collection block.</returns>
    private static bool IsCollectionBlock(RenderMember member) => member.Kind is RenderMemberKind.SortedList or RenderMemberKind.PairList or RenderMemberKind.NestedList or RenderMemberKind.SparseDictionary;

    /// <summary>Writes one collection member in place: its null header, its payload and its exit label.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="model">The render model.</param>
    /// <param name="member">The collection member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth of the member.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    private static void AddCollectionWrite(List<string> body, PayloadRenderModel model, RenderMember member, string owner, int depth,
        CodeWriter w, Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        switch (member.Kind)
        {


            case RenderMemberKind.SortedList:
                AddSortedListWrite(body, model, member, owner, w, names, ref ends);
                break;
            case RenderMemberKind.PairList:
                AddPairListWrite(body, model, member, owner, w, names, ref ends);
                break;
            case RenderMemberKind.NestedList:
                AddNestedListWrite(body, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                break;
            default:
                // A nullable value is framed by its owner (a null marker, or a memberless header and the payload), so the sparse
                // dictionary writes through its shared owner like the other keyed collections.
                body.Add(member.BinaryFormatter + ".Write<" + Profile + ">(ref writer, " + owner + "." + member.Name + ");");
                break;
        }
    }





    /// <summary>Writes a sorted list's stored key and value arrays as paired entries, with an array-pool fallback.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddSortedListWrite</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddSortedListWrite</c>.</param>
    private static void AddSortedListWrite(List<string> body, PayloadRenderModel model, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        var shape = member.Collection;
        string value = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + value + " = ref " + owner + "." + member.Name + ";");
        AddCollectionNull(body, value, exit, w, reserve: true, countMarker: false);
        body.Add("");
        body.Add("int count = " + value + ".Count;");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(count);");
        body.Add("if (count != 0 &&");
        body.Add("\tglobal::LuminPack.Code.LuminPackCollectionAccess.TryGetSortedListStorage(" + value + ", out var storedKeys, out var storedValues))");
        body.Add("{");
        body.Add("var keySpan = new global::System.ReadOnlySpan<" + shape.KeyType + ">(storedKeys, 0, count);");
        body.Add("var valueSpan = new global::System.ReadOnlySpan<" + shape.ValueType + ">(storedValues, 0, count);");
        body.Add("for (int i = 0; i < keySpan.Length; i++)");
        body.Add("{");
        body.Add("writer.CheckBuffer();");
        body.Add("writer.Write(keySpan[i]);");
        AddCollectionValueWrite(body, shape, "writer", "valueSpan[i]", model);
        body.Add("}");
        body.Add("}");
        body.Add("else if (count != 0)");
        body.Add("\tWriteFallback(ref writer, " + value + ", count);");
        body.Add("writer.CheckBuffer();");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void WriteFallback(");
        body.Add("\tref " + w.A("LuminPackWriter") + " writer2,");
        body.Add("\t" + member.TypeName + " value3,");
        body.Add("\tint pairCount)");
        body.Add("{");
        body.Add("var keys = global::System.Buffers.ArrayPool<" + shape.KeyType + ">.Shared.Rent(pairCount);");
        body.Add(shape.ValueText ? "string[]? values = null;" : shape.ValueType + "[]? values = null;");
        body.Add("try");
        body.Add("{");
        body.Add("values = global::System.Buffers.ArrayPool<" + shape.ValueType + ">.Shared.Rent(pairCount);");
        body.Add("value3.Keys.CopyTo(keys, 0);");
        body.Add("value3.Values.CopyTo(values, 0);");
        body.Add("for (int i2 = 0; i2 < pairCount; i2++)");
        body.Add("{");
        body.Add("writer2.CheckBuffer();");
        body.Add("writer2.Write(keys[i2]);");
        AddCollectionValueWrite(body, shape, "writer2", "values[i2]", model);
        body.Add("}");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        body.Add("global::System.Buffers.ArrayPool<" + shape.KeyType + ">.Shared.Return(");
        body.Add("\tkeys,");
        body.Add("\tglobal::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + shape.KeyType + ">());");
        body.Add("if (values != null)");
        body.Add("\tglobal::System.Buffers.ArrayPool<" + shape.ValueType + ">.Shared.Return(");
        body.Add("\t\tvalues,");
        body.Add("\t\tglobal::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + shape.ValueType + ">());");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Writes a list of key/value pairs, storing each pair's key and value in one checkable loop.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddPairListWrite</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddPairListWrite</c>.</param>
    private static void AddPairListWrite(List<string> body, PayloadRenderModel model, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        var shape = member.Collection;
        string local = RoleName(names, "value");
        string span = RoleName(names, "span");
        string pair = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        AddCollectionNull(body, local, exit, w, reserve: true, countMarker: true);
        body.Add("");
        body.Add("var " + span + " = " + w.A("LuminPackMarshal") + ".GetListSpan(" + w.A("Unsafe") + ".AsRef(in " + local + "));");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(" + span + ".Length);");
        body.Add("for (int i = 0; i < " + span + ".Length; i++)");
        body.Add("{");
        body.Add("writer.CheckBuffer();");
        body.Add("ref readonly " + shape.EntryType + " " + pair + " = ref " + span + "[i]!;");
        body.Add("writer.Write(" + pair + ".Key);");
        AddCollectionValueWrite(body, shape, "writer", pair + ".Value", model);
        body.Add("}");
        body.Add("");
        body.Add("writer.CheckBuffer();");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Writes a list of lists: the outer span, then each inner span with its elements' expanded record bodies.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddNestedListWrite</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddNestedListWrite</c>.</param>
    /// <param name="cursors">The cursors supplied to <c>AddNestedListWrite</c>.</param>
    /// <param name="spans">The spans supplied to <c>AddNestedListWrite</c>.</param>
    private static void AddNestedListWrite(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        var element = member.Nested;
        string local = RoleName(names, "value");
        // The element's null exit is numbered first, then the inner list's, then the outer list's.
        string elementExit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string innerExit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string span = RoleName(names, "span");
        string index = depth == 0 ? "i" : "i" + (depth + 1).ToString(CultureInfo.InvariantCulture);
        string innerIndex = "i" + (depth + 2).ToString(CultureInfo.InvariantCulture);
        string source = owner + "." + member.Name;
        if (element.Net8)
        {
            string helper = RoleName(names, "WriteNestedList");
            body.Add(helper + "(ref writer, in " + source + ");");
            body.Add(w.MethodImpl("NoInlining"));
            body.Add("static void " + helper + "(scoped ref " + w.A("LuminPackWriter") + " writer, scoped in " + member.TypeName + " nestedList)");
            source = "nestedList";
        }
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + source + ";");
        AddCollectionNull(body, local, exit, w, reserve: true, countMarker: true);
        body.Add("");
        body.Add("var " + span + " = " + w.A("LuminPackMarshal") + ".GetListSpan(" + w.A("Unsafe") + ".AsRef(in " + local + "));");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(" + span + ".Length);");
        body.Add("for (int " + index + " = 0; " + index + " < " + span + ".Length; " + index + "++)");
        body.Add("{");
        body.Add("writer.CheckBuffer();");
        body.Add("{");
        string inner = RoleName(names, "value");
        body.Add("ref readonly " + member.Collection.ItemType + " " + inner + " = ref " + span + "[" + index + "]!;");
        AddCollectionNull(body, inner, innerExit, w, reserve: true, countMarker: true);
        body.Add("");
        string innerSpan = RoleName(names, "span");
        body.Add("var " + innerSpan + " = " + w.A("LuminPackMarshal") + ".GetListSpan(" + w.A("Unsafe") + ".AsRef(in " + inner + "));");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(" + innerSpan + ".Length);");
        body.Add("for (int " + innerIndex + " = 0; " + innerIndex + " < " + innerSpan + ".Length; " + innerIndex + "++)");
        body.Add("{");
        body.Add("writer.CheckBuffer();");
        body.Add("{");
        string input = RoleName(names, "input");
        string value = RoleName(names, "value");
        body.Add("ref readonly " + element.GlobalName + " " + input + " = ref " + innerSpan + "[" + innerIndex + "]!;");
        body.Add("var " + value + " = " + input + ";");
        body.Add("if (" + value + " is null)");
        body.Add("{");
        body.Add("writer.Cursor = (byte)255;");
        body.Add("writer.Advance(1);");
        body.Add("goto " + elementExit + ";");
        body.Add("}");
        body.Add("");
        AddElementWrite(body, element, value, depth + 1, w, names, ref ends, ref cursors, ref spans);
        body.Add(elementExit + ":");
        body.Add("\t;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("writer.CheckBuffer();");
        body.Add(innerExit + ":");
        body.Add("\t;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("writer.CheckBuffer();");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Writes a dictionary with a nullable raw value as sparse raw entries whose keys use their varint record.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddSparseDictionaryWrite</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddSparseDictionaryWrite</c>.</param>
    private static void AddSparseDictionaryWrite(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        var shape = member.Collection;
        string local = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        AddCollectionNull(body, local, exit, w, reserve: false, countMarker: false);
        body.Add("");
        body.Add("int count = " + local + ".Count;");
        body.Add("writer.EnsureAdditionalCapacity(checked(sizeof(int) + count * (" + w.A("Unsafe") + ".SizeOf<" + shape.KeyType + ">() + " +
            w.A("Unsafe") + ".SizeOf<" + shape.ValueType + ">())));");
        body.Add("writer.WriteCollectionHeader(count);");
        body.Add("WriteSparseRawDictionary(ref writer, " + local + ");");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void WriteSparseRawDictionary(");
        body.Add("\tref " + w.A("LuminPackWriter") + " writer2,");
        body.Add("\t" + member.TypeName + " source)");
        body.Add("{");
        body.Add("foreach (var item in source)");
        body.Add("{");
        body.Add("ref byte entryCursor = ref writer2.Cursor;");
        body.Add("int keyBytes = " + w.A("LuminPackWriter") + ".Write(ref entryCursor, item.Key);");
        body.Add(w.A("LuminPackWriter") + ".Write(ref " + w.A("Unsafe") + ".Add(ref entryCursor, unchecked((nint)(uint)(keyBytes))), item.Value);");
        body.Add("writer2.Advance(keyBytes + " + w.A("Unsafe") + ".SizeOf<" + shape.ValueType + ">());");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Writes one sorted or paired value: a text record through the string formatter, or a raw scalar store.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="shape">The collection or payload shape to use.</param>
    /// <param name="writer">The writer receiving the output.</param>
    /// <param name="source">The source value or text being processed.</param>
    /// <param name="model">The normalized model for this contract.</param>
    private static void AddCollectionValueWrite(List<string> body, RenderCollection shape, string writer, string source, PayloadRenderModel model)
    {
        if (shape.ValueText)
            body.Add(TextFormatter(model) + ".Write<" + Profile + ">(ref " + writer + ", " + source + ");");
        else
            body.Add(writer + ".Write(" + source + ");");
    }

    /// <summary>Writes a collection's null header: either the null collection code or the list's negative count marker.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="value">The local holding the collection.</param>
    /// <param name="exit">The label the null branch jumps to.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="reserve">Whether the header reserves its own four bytes before storing.</param>
    /// <param name="countMarker">Whether the header stores the list's negative count instead of the null collection code.</param>
    internal static void AddCollectionNull(List<string> body, string value, string exit, CodeWriter w, bool reserve, bool countMarker)
    {
        body.Add("if (" + value + " is null)");
        body.Add("{");
        if (reserve)
            body.Add("writer.EnsureAdditionalCapacity(4);");
        if (countMarker)
            body.Add("writer.Write(-1);");
        else
        {
            body.Add(w.A("Unsafe") + ".WriteUnaligned(ref writer.Cursor, " + w.A("LuminPackCode") + ".NullCollection);");
            body.Add("writer.Advance(4);");
        }
        body.Add("goto " + exit + ";");
        body.Add("}");
    }
}

