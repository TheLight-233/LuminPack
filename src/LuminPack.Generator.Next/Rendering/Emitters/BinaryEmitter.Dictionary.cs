#nullable disable
using System.Collections.Generic;
using System.Globalization;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes the types whose records carry dictionary members with the original cursor protocol: fused entry kernels
/// beside an inline block, or paired raw records inside a reservation.</summary>
internal static partial class BinaryEmitter
{
    /// <summary>Builds a record write whose dictionary members take the cursor path, reproducing the original member
    /// grouping: a reserved prefix, each dictionary kernel in place and every inline block between them.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="reserveNativeSpans">Whether newly captured spans may share the reservation.</param>
    private static void DictionaryWriteBody(PayloadRenderModel model, CodeWriter w, bool union, List<string> body, bool reserveNativeSpans)
    {
        var members = model.Members;
        // The body's own cursor local takes the first name, so nested blocks number theirs after it.
        int ends = 0, cursors = 1, spans = 0;
        var names = new Dictionary<string, int>(System.StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
        body.Add("ref byte cursor = ref writer.Cursor;");
        bool header = !union, current = true;
        int index = 0;
        while (index < members.Length)
        {
            var member = members[index];
            if (IsInlineSegment(member, reserveNativeSpans))
            {
                if (header)
                {
                    body.Add("cursor = ref writer.WriteRecord(ref cursor, (byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ");");
                    header = false;
                }
                if (current)
                {
                    body.Add("writer.Cursor = ref cursor;");
                    current = false;
                }
                if (member.Kind == RenderMemberKind.ForwardedCollection || member.Kind == RenderMemberKind.Array && member.FormatterCall)
                    body.Add(member.BinaryFormatter + ".Write<" + WriteProfile(model, w, member) + ">(ref writer, value." + member.Name + ");");
                else if (member.Kind is RenderMemberKind.Special or RenderMemberKind.CultureInfo or RenderMemberKind.Parameter)
                    AddMemberWrite(body, model, member, w, "value." + member.Name);
                else if (member.Kind == RenderMemberKind.Sequence)
                    member.Sequence.WriteMember(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                else if (IsTextList(member))
                    AddInlineListWrite(body, member, "value", w, names, ref ends, ref spans);
                else if (IsElementList(member))
                    AddInlineElementListWrite(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                else if (IsCollectionBlock(member))
                    AddCollectionWrite(body, model, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                else
                    AddInlineObjectWrite(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                index++;
                bool named = member.Kind == RenderMemberKind.Object && member.Nested.MemberWrite;
                if (!named && index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            if (member.Kind == RenderMemberKind.Dictionary && !member.Dictionary.Pair)
            {
                if (header)
                {
                    body.Add("cursor = ref writer.WriteRecord(ref cursor, (byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ");");
                    header = false;
                }
                if (!current)
                {
                    body.Add("cursor = ref writer.Cursor;");
                    current = true;
                }
                AddDictionaryEntries(body, model, member, index, "value", w, names, ref ends);
                index++;
                continue;
            }
            int last = index;
            while (last + 1 < members.Length && !IsInlineSegment(members[last + 1], reserveNativeSpans) &&
                !(members[last + 1].Kind == RenderMemberKind.Dictionary && !members[last + 1].Dictionary.Pair))
                last++;
            if (!current && !header)
            {
                body.Add("cursor = ref writer.Cursor;");
                current = true;
            }
            if (header)
                AddReservedSegment(model, w, body, index, last, header: true, camel: true);
            else if (last == index)
                AddLoneRecord(model, w, body, member);
            else
                AddReservedSegment(model, w, body, index, last, header: false, camel: true);
            header = false;
            current = true;
            index = last + 1;
        }
        if (header)
            body.Add("cursor = ref writer.WriteRecord(ref cursor, (byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ");");
        if (current)
            body.Add("writer.Cursor = ref cursor;");
    }

    /// <summary>Writes one fused-entry dictionary member at the cursor: the kernel under a UTF-16 record profile and the
    /// original operation as its fallback.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="index">The zero-based item index.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddDictionaryEntries</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddDictionaryEntries</c>.</param>
    private static void AddDictionaryEntries(List<string> body, PayloadRenderModel model, RenderMember member, int index, string owner,
        CodeWriter w, Dictionary<string, int> names, ref int ends)
    {
        var shape = member.Dictionary;
        string entries = "entries" + index.ToString(CultureInfo.InvariantCulture);
        string title = "Entries" + index.ToString(CultureInfo.InvariantCulture);
        string value = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        TouchDictionaryAliases(model, w);
        body.Add("{");
        body.Add("var " + entries + " = " + owner + "." + member.Name + ";");
        body.Add("int " + entries + "Width = writer.UsesUtf16LengthRecords<" + Profile + ">()");
        body.Add("\t? " + title + "Kernel(ref cursor, writer.RecordRoom(ref cursor), " + entries + ")");
        body.Add("\t: -1;");
        body.Add("if (" + entries + "Width >= 0)");
        body.Add("\tcursor = ref " + w.A("Unsafe") + ".Add(ref cursor, unchecked((nint)(uint)" + entries + "Width));");
        body.Add("else");
        body.Add("{");
        body.Add("writer.Cursor = ref cursor;");
        body.Add(title + "Fallback(ref writer, " + entries + ");");
        body.Add("cursor = ref writer.Cursor;");
        body.Add("}");
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void " + title + "Fallback(");
        body.Add("\tref " + w.A("LuminPackWriter") + " writer2,");
        body.Add("\t" + shape.TypeName + " fallbackValue)");
        body.Add("{");
        body.Add("{");
        body.Add("ref readonly " + shape.TypeName + " " + value + " = ref fallbackValue;");
        body.Add("if (" + value + " is null)");
        body.Add("{");
        body.Add(w.A("Unsafe") + ".WriteUnaligned(ref writer2.Cursor, LuminPackCode.NullCollection);");
        body.Add("writer2.Advance(4);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("int count = " + value + ".Count;");
        body.Add("writer2.WriteCollectionHeader(count);");
        body.Add("if (count != 0)");
        body.Add("{");
        body.Add("if (" + w.A("Unsafe") + ".SizeOf<global::System.IntPtr>() != 8)");
        body.Add("{");
        body.Add("WriteSparseDictionary(ref writer2, " + value + ");");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("var dictionaryView = LuminPackMarshal.GetDictionaryView(" + value + ");");
        body.Add("if (dictionaryView._count != count)");
        body.Add("{");
        body.Add("WriteSparseDictionary(ref writer2, " + value + ");");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("int version = dictionaryView._version;");
        body.Add("ref var entryRef = ref LuminPackMarshal.GetNotNullArrayReference(dictionaryView._entries!);");
        body.Add("for (int i = 0; i < count; i++)");
        body.Add("{");
        if (!shape.KeyText)
            body.Add("writer2.CheckBuffer();");
        body.Add("ref var entry = ref entryRef;");
        AddEntryWrite(body, shape, "writer2", "entry", TextFormatter(model), w.A("Unsafe"));
        body.Add("entryRef = ref " + w.A("Unsafe") + ".Add(ref entryRef, 1);");
        body.Add("}");
        body.Add("");
        body.Add("if (dictionaryView._version != version || dictionaryView._count != count || dictionaryView._freeCount != 0)");
        body.Add("\tLuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
        body.Add("}");
        body.Add("");
        body.Add("writer2.CheckBuffer();");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void WriteSparseDictionary(");
        body.Add("\tref " + w.A("LuminPackWriter") + " writer3,");
        body.Add("\t" + shape.TypeName + " source)");
        body.Add("{");
        body.Add("foreach (var item in source)");
        body.Add("{");
        if (!shape.KeyText)
            body.Add("writer3.CheckBuffer();");
        AddSparseEntryWrite(body, shape, "writer3", "item", TextFormatter(model));
        body.Add("}");
        body.Add("");
        body.Add("writer3.CheckBuffer();");
        body.Add("}");
        body.Add("");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        AddEntryKernel(body, model, member, title, w);
        body.Add("}");
        body.Add("");
    }

    /// <summary>Writes a dictionary member's original operation inside an inline object, which writes through the writer
    /// without a cursor local.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddDictionaryInline</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddDictionaryInline</c>.</param>
    private static void AddDictionaryInline(List<string> body, PayloadRenderModel model, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        var shape = member.Dictionary;
        string value = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        TouchDictionaryAliases(model, w);
        body.Add("{");
        body.Add("ref readonly " + shape.TypeName + " " + value + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + value + " is null)");
        body.Add("{");
        body.Add(w.A("Unsafe") + ".WriteUnaligned(ref writer.Cursor, LuminPackCode.NullCollection);");
        body.Add("writer.Advance(4);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("int count = " + value + ".Count;");
        body.Add("writer.WriteCollectionHeader(count);");
        body.Add("if (count != 0)");
        body.Add("{");
        body.Add("if (" + w.A("Unsafe") + ".SizeOf<global::System.IntPtr>() != 8)");
        body.Add("{");
        body.Add("WriteSparseDictionary(ref writer, " + value + ");");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("var dictionaryView = LuminPackMarshal.GetDictionaryView(" + value + ");");
        body.Add("if (dictionaryView._count != count)");
        body.Add("{");
        body.Add("WriteSparseDictionary(ref writer, " + value + ");");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("int version = dictionaryView._version;");
        body.Add("ref var entryRef = ref LuminPackMarshal.GetNotNullArrayReference(dictionaryView._entries!);");
        body.Add("for (int i = 0; i < count; i++)");
        body.Add("{");
        if (!shape.KeyText)
            body.Add("writer.CheckBuffer();");
        body.Add("ref var entry = ref entryRef;");
        AddEntryWrite(body, shape, "writer", "entry", TextFormatter(model), w.A("Unsafe"));
        body.Add("entryRef = ref " + w.A("Unsafe") + ".Add(ref entryRef, 1);");
        body.Add("}");
        body.Add("");
        body.Add("if (dictionaryView._version != version || dictionaryView._count != count || dictionaryView._freeCount != 0)");
        body.Add("\tLuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
        body.Add("}");
        body.Add("");
        body.Add("writer.CheckBuffer();");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void WriteSparseDictionary(");
        body.Add("\tref " + w.A("LuminPackWriter") + " writer2,");
        body.Add("\t" + shape.TypeName + " source)");
        body.Add("{");
        body.Add("foreach (var item in source)");
        body.Add("{");
        if (!shape.KeyText)
            body.Add("writer2.CheckBuffer();");
        AddSparseEntryWrite(body, shape, "writer2", "item", TextFormatter(model));
        body.Add("}");
        body.Add("");
        body.Add("writer2.CheckBuffer();");
        body.Add("}");
        body.Add("");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
        body.Add("");
    }

    /// <summary>Writes one dense dictionary key and value from an entry expression.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="shape">The entry shape.</param>
    /// <param name="writer">The writer local.</param>
    /// <param name="entry">The entry local.</param>
    /// <param name="textFormatter">The generated string formatter owner.</param>
    /// <param name="unsafeAlias">The <c>Unsafe</c> spelling of the file.</param>
    private static void AddEntryWrite(List<string> body, RenderDictionary shape, string writer, string entry, string textFormatter,
        string unsafeAlias)
    {
        if (shape.KeyText)
        {
            body.Add("string key = " + entry + ".Key;");
            body.Add(textFormatter + ".Write<" + Profile + ">(ref " + writer + ", in key);");
        }
        else
            AddRawStore(body, shape.KeyType, writer, entry + ".Key", unsafeAlias);
        if (shape.ValueText)
        {
            body.Add("string itemValue = " + entry + ".Value;");
            body.Add(textFormatter + ".Write<" + Profile + ">(ref " + writer + ", in itemValue);");
        }
        else
            AddRawStore(body, shape.ValueType, writer, entry + ".Value", unsafeAlias);
    }

    /// <summary>Writes one key and value of a sparse dictionary enumeration, which copies raw values from a local.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="shape">The collection or payload shape to use.</param>
    /// <param name="writer">The writer receiving the output.</param>
    /// <param name="item">The current item.</param>
    /// <param name="textFormatter">The text formatter supplied to <c>AddSparseEntryWrite</c>.</param>
    private static void AddSparseEntryWrite(List<string> body, RenderDictionary shape, string writer, string item, string textFormatter)
    {
        if (shape.KeyText)
        {
            body.Add("string key2 = " + item + ".Key;");
            body.Add(textFormatter + ".Write<" + Profile + ">(ref " + writer + ", in key2);");
        }
        else
        {
            body.Add(shape.KeyType + " key = " + item + ".Key;");
            body.Add(writer + ".Write(in key);");
        }
        if (shape.ValueText)
        {
            body.Add("string itemValue2 = " + item + ".Value;");
            body.Add(textFormatter + ".Write<" + Profile + ">(ref " + writer + ", in itemValue2);");
        }
        else
        {
            body.Add(shape.ValueType + " itemValue = " + item + ".Value;");
            body.Add(writer + ".Write(in itemValue);");
        }
    }

    /// <summary>Copies one raw entry key or value at the writer's cursor.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <param name="writer">The writer receiving the output.</param>
    /// <param name="source">The source value or text being processed.</param>
    /// <param name="unsafeAlias">The unsafe alias supplied to <c>AddRawStore</c>.</param>
    private static void AddRawStore(List<string> body, string type, string writer, string source, string unsafeAlias)
    {
        body.Add("LuminPackMarshal.CopyBlockUnaligned(");
        body.Add("\tref " + writer + ".Cursor,");
        body.Add("\tref " + unsafeAlias + ".As<" + type + ", byte>(ref " + source + "),");
        body.Add("\t(uint)" + unsafeAlias + ".SizeOf<" + type + ">());");
        body.Add(writer + ".Advance(" + unsafeAlias + ".SizeOf<" + type + ">());");
    }

    /// <summary>Writes the dense-entry kernel of a fused dictionary member.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="title">The title supplied to <c>AddEntryKernel</c>.</param>
    /// <param name="w">The generated source writer.</param>
    private static void AddEntryKernel(List<string> body, PayloadRenderModel model, RenderMember member, string title, CodeWriter w)
    {
        var shape = member.Dictionary;
        string keyLocal = shape.KeyText ? "key3" : null;
        string valueLocal = shape.ValueText ? "item2" : null;
        string keyBound = shape.KeyText ? "(" + keyLocal + " is null ? 0 : (long)" + keyLocal + ".Length * sizeof(char)) + sizeof(int)"
            : w.A("Unsafe") + ".SizeOf<" + shape.KeyType + ">()";
        string valueBound = shape.ValueText ? "(" + valueLocal + " is null ? 0 : (long)" + valueLocal + ".Length * sizeof(char)) + sizeof(int)"
            : w.A("Unsafe") + ".SizeOf<" + shape.ValueType + ">()";
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static int " + title + "Kernel(");
        body.Add("\tref byte destination,");
        body.Add("\tint room,");
        body.Add("\t" + shape.TypeName + " dictionary)");
        body.Add("{");
        body.Add("if (room < sizeof(int))");
        body.Add("\treturn -1;");
        body.Add("if (dictionary is null)");
        body.Add("{");
        body.Add(w.A("LuminPackWriter") + ".Write<int>(ref destination, -1);");
        body.Add("return sizeof(int);");
        body.Add("}");
        body.Add("");
        body.Add("if (" + w.A("Unsafe") + ".SizeOf<global::System.IntPtr>() != 8)");
        body.Add("\treturn -1;");
        body.Add("var view = LuminPackMarshal.GetDictionaryView(dictionary);");
        body.Add("int count2 = view._count;");
        body.Add("var entries = view._entries;");
        body.Add("if (view._freeCount != 0 || (entries is null ? count2 != 0 : (uint)count2 > (uint)entries.Length))");
        body.Add("\treturn -1;");
        body.Add("int version2 = view._version;");
        body.Add(w.A("LuminPackWriter") + ".Write<int>(ref destination, count2);");
        body.Add("int at = sizeof(int);");
        body.Add("for (int i2 = 0; i2 < count2; i2++)");
        body.Add("{");
        body.Add("ref var entry2 = ref " + w.A("Unsafe") + ".Add(");
        body.Add("\tref LuminPackMarshal.GetNotNullArrayReference(entries!),");
        body.Add("\tunchecked((nint)(uint)i2));");
        if (shape.KeyText)
            body.Add("string " + keyLocal + " = entry2.Key;");
        if (shape.ValueText)
            body.Add("string " + valueLocal + " = entry2.Value;");
        body.Add("if (at + " + keyBound + " + " + valueBound + " > room)");
        body.Add("\treturn -1;");
        AddKernelStore(body, shape.KeyText, shape.KeyType, "destination", "entry2.Key", keyLocal, w.A("Unsafe"), w.A("LuminPackWriter"));
        AddKernelStore(body, shape.ValueText, shape.ValueType, "destination", "entry2.Value", valueLocal, w.A("Unsafe"), w.A("LuminPackWriter"));
        body.Add("}");
        body.Add("");
        body.Add("if (view._version != version2 || view._count != count2 || view._freeCount != 0)");
        body.Add("\tLuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
        body.Add("return at;");
        body.Add("}");
    }

    /// <summary>Stores one kernel key or value at the running destination offset.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="text">Whether to enable text for this operation.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="source">The source value or text being processed.</param>
    /// <param name="local">The local supplied to <c>AddKernelStore</c>.</param>
    /// <param name="unsafeAlias">The unsafe alias supplied to <c>AddKernelStore</c>.</param>
    /// <param name="writerAlias">The writer alias supplied to <c>AddKernelStore</c>.</param>
    private static void AddKernelStore(List<string> body, bool text, string type, string destination, string source,
        string local, string unsafeAlias, string writerAlias)
    {
        if (text)
        {
            body.Add("at += " + writerAlias + ".WriteReservedUtf16Record(");
            body.Add("\tref " + unsafeAlias + ".Add(ref " + destination + ", unchecked((nint)(uint)at)),");
            body.Add("\t" + local + ");");
        }
        else
        {
            body.Add("LuminPackMarshal.CopyBlockUnaligned(");
            body.Add("\tref " + unsafeAlias + ".Add(ref " + destination + ", unchecked((nint)(uint)at)),");
            body.Add("\tref " + unsafeAlias + ".As<" + type + ", byte>(ref " + source + "),");
            body.Add("\t(uint)" + unsafeAlias + ".SizeOf<" + type + ">());");
            body.Add("at += " + unsafeAlias + ".SizeOf<" + type + ">();");
        }
    }

    /// <summary>The generated string formatter owner of the containing extension container.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <returns>The resulting text.</returns>
    private static string TextFormatter(PayloadRenderModel model) => "global::LuminPack." +
        GeneratedNames.Owner(model.Extension, "global::System.String");

    /// <summary>Registers the runtime helper aliases the dictionary text spells by name. A mirrored record reaches its
    /// storage through the qualified marshal type, so only an unmirrored file declares that alias.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    private static void TouchDictionaryAliases(PayloadRenderModel model, CodeWriter w)
    {
        if (model.Mirror == null)
            w.A("LuminPackMarshal");
        w.A("LuminPackCode");
        w.A("LuminPackExceptionHelper");
    }
}


