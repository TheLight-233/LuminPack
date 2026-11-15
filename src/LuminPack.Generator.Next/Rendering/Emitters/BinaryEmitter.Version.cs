#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class BinaryEmitter
{
    /// <summary>Builds a version-tolerant record write: the null object marker, one reservation covering the schema prefix, the raw
    /// members and the length-prefixed strings, the prefix store, and the members stored like an ordinary reserved record, each
    /// string behind its four-byte payload length.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    /// <returns>The write statements.</returns>
    private static List<string> VersionWriteBody(PayloadRenderModel model, CodeWriter w)
    {
        var members = model.Members;
        var schema = model.Version;
        if (schema.Dynamic)
            return ApplyBoundedWrite(model, VersionStreamWriteBody(model, w));
        for (int index = 0; index < members.Length; index++)
            if (members[index].Volatile)
                return ApplyBoundedWrite(model, VersionStreamWriteBody(model, w));
        var body = new List<string>
        {
            "var value = input;",
        };
        if (!model.IsStruct)
        {
            body.Add("if (value is null)");
            body.Add("{");
            body.Add("writer.Cursor = " + w.A("LuminPackCode") + ".NullObject;");
            body.Add("writer.Advance(1);");
            body.Add("return;");
            body.Add("}");
            body.Add("");
        }
        body.Add("ref byte cursor = ref writer.Cursor;");
        bool prefix = true;
        int lengths = 0;
        for (int index = 0; index <= members.Length;)
        {
            int end = index;
            while (end < members.Length && VersionReservable(members[end], schema.Widths[end]))
                end++;
            if (prefix || end > index)
            {
                AppendVersionReservation(model, w, body, index, end, prefix, ref lengths);
                prefix = false;
            }
            if (end == members.Length)
                break;
            body.Add("writer.Cursor = ref cursor;");
            AppendVersionVariable(body, model, members[end], w, "value." + members[end].Name);
            body.Add("cursor = ref writer.Cursor;");
            index = end + 1;
        }
        if (body[body.Count - 1].Length != 0 && members.Any(member => member.Kind is RenderMemberKind.Text or RenderMemberKind.List))
            body.Add("");
        body.Add("writer.Cursor = ref cursor;");
        return ApplyBoundedWrite(model, body);
    }

    /// <summary>Whether the complete slot can be written inside a fixed reservation.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="width">The width of the storage or input range.</param>
    /// <returns>Whether the input satisfies the version reservable check.</returns>
    private static bool VersionReservable(RenderMember member, int width) => width != 255 ||
        member.Kind == RenderMemberKind.Text ||
        member.Kind == RenderMemberKind.List && member.Nested == null && member.Scalar != "string";

    /// <summary>Reserves one run of fixed or measured slots and optionally stores the schema prefix.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="first">The first supplied to <c>AppendVersionReservation</c>.</param>
    /// <param name="end">The end supplied to <c>AppendVersionReservation</c>.</param>
    /// <param name="prefix">The prefix to use for the generated name.</param>
    /// <param name="lengths">The lengths supplied to <c>AppendVersionReservation</c>.</param>
    private static void AppendVersionReservation(PayloadRenderModel model, CodeWriter w, List<string> body,
        int first, int end, bool prefix, ref int lengths)
    {
        var members = model.Members;
        var terms = new List<string>();
        if (prefix)
            terms.Add(model.Version.Schema + ".Prefix.Length");
        for (int index = first; index < end; index++)
        {
            if (members[index].Kind == RenderMemberKind.List)
            {
                // A reserved-span list captures its complete payload bound before the record is reserved.
                body.Add("var " + VersionSnapshot(model, index) + " = " + w.A("LuminPackWriter") + ".Capture(value." + members[index].Name + ");");
                terms.Add("4");
                terms.Add(VersionSnapshot(model, index) + ".Bound");
                continue;
            }
            if (members[index].Kind != RenderMemberKind.Text)
            {
                terms.Add(members[index].Width);
                continue;
            }
            body.Add("string " + VersionSnapshot(model, index) + " = value." + members[index].Name + ";");
            terms.Add("4");
            terms.Add("writer.RecordBound<" + WriteProfile(model, w, members[index]) + ">(" + VersionSnapshot(model, index) + ")");
        }
        body.Add("cursor = ref writer.ReserveRecord(ref cursor, checked(" + string.Join(" + ", terms) + "));");
        if (prefix)
            body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, unchecked((nint)(uint)(" + model.Version.Schema + ".StorePrefix<" + Profile + ">(ref writer, ref cursor))));");
        for (int index = first; index < end; index++)
        {
            var member = members[index];
            if (member.Kind is RenderMemberKind.Text or RenderMemberKind.List)
            {
                // A string or reserved-span list payload is preceded by its byte length, patched afterwards.
                string length = lengths++ == 0 ? "length" : "length" + (lengths - 1).ToString(CultureInfo.InvariantCulture);
                body.Add("// Field: " + member.Name);
                body.Add("ref byte " + length + " = ref cursor;");
                body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 4);");
                body.Add(member.Kind == RenderMemberKind.Text
                    ? "cursor = ref writer.WriteReservedRecordString<" + WriteProfile(model, w, member) + ">(ref cursor, " + VersionSnapshot(model, index) + ");"
                    : "cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref cursor, " + VersionSnapshot(model, index) + ");");
                body.Add(w.A("Unsafe") + ".WriteUnaligned(ref " + length + ", (int)" + w.A("Unsafe") + ".ByteOffset(ref " + w.A("Unsafe") + ".Add(ref " + length + ", 4), ref cursor));");
                continue;
            }
            int last = VersionRunEnd(model, index);
            if (last == index)
            {
                if (member.Size >= 8 || member.Raw)
                {
                    body.Add("if (" + w.A("Unsafe") + ".SizeOf<global::System.IntPtr>() == 8)");
                    body.Add("{");
                    body.Add(w.A("Unsafe") + ".WriteUnaligned<" + member.Scalar + ">(ref cursor, value." + member.Name + ");");
                    body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + w.A("Unsafe") + ".SizeOf<" + member.Scalar + ">());");
                    body.Add("}");
                    body.Add("else");
                    body.Add("{");
                    body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReserved<" + member.Scalar + ">(ref cursor, value." + member.Name + ");");
                    body.Add("}");
                    body.Add("");
                }
                else
                {
                    body.Add(w.A("Unsafe") + ".WriteUnaligned<" + member.Scalar + ">(ref cursor, value." + member.Name + ");");
                    body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + w.A("Unsafe") + ".SizeOf<" + member.Scalar + ">());");
                }
                continue;
            }
            AddVersionFixedStores(body, w, members, index, last, Width(members, index, last));
            body.Add("");
            index = last;
        }
    }

    /// <summary>Whether a version-tolerant write root keeps its inline hint: its template stays within the sixteen-call budget
    /// only without a fixed group and with at most one string.</summary>
    /// <param name="model">The render model.</param>
    /// <returns>Whether the write root takes the aggressive inlining hint.</returns>
    internal static bool VersionWriteInline(PayloadRenderModel model)
    {
        var members = model.Members;
        int strings = 0;
        for (int index = 0; index < members.Length; index++)
            if (members[index].IsFixed && VersionRunEnd(model, index) > index ||
                members[index].Kind == RenderMemberKind.Text && ++strings > 1)
                return false;
        return true;
    }

    /// <summary>Spells a string member's write snapshot, camel-cased like the other bound locals of these operations.</summary>
    /// <param name="model">The record that owns the member.</param>
    /// <param name="index">The member's index.</param>
    /// <returns>The snapshot local name.</returns>
    private static string VersionSnapshot(PayloadRenderModel model, int index)
    {
        string trimmed = model.Members[index].Name.TrimStart('_');
        string stem = trimmed + "Snapshot";
        string name = stem + index.ToString(CultureInfo.InvariantCulture);
        string chosen = AsciiLetters(trimmed) && model.Members.All(member => member.Name != stem) ? stem : name;
        return char.ToLowerInvariant(chosen[0]) + chosen.Substring(1);
    }

    /// <summary>Builds a version-tolerant record read: the same-schema record read like an ordinary record (raw runs as fixed
    /// groups, lone raw members and length-framed strings one by one), the evolved record mapped by name in a separate non-inlined
    /// function, and the construction or reuse of the instance.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    /// <returns>The read statements.</returns>
    private static List<string> VersionReadBody(PayloadRenderModel model, CodeWriter w)
    {
        var members = model.Members;
        var schema = model.Version;
        string count = members.Length.ToString(CultureInfo.InvariantCulture);
        string text = "global::LuminPack." + GeneratedNames.Owner(model.Extension, "string");
        string Policy() => w.A("LuminPackInputPolicy");
        string Reader() => w.A("LuminPackReader");
        var temps = members.Select(member => PayloadEmitter.LocalName(member.Name) + "Temp").ToArray();
        // Frame-end locals are numbered across the root and its mapped read, as are the saved field limits of the mapped read.
        int ends = 0, limits = 0;
        string End() => ends++ == 0 ? "end" : "end" + ends.ToString(CultureInfo.InvariantCulture);
        string Limit() => limits++ == 0 ? "previousLength" : "previousLength" + limits.ToString(CultureInfo.InvariantCulture);
        var body = new List<string>();
        for (int index = 0; index < members.Length; index++)
            body.Add(schema.TempTypes[index] + " " + temps[index] + " = default !;");
        body.Add("bool sameSchema = " + schema.Schema + ".TryReadPrefix<" + Profile + ">(ref reader);");
        body.Add("int revision = 2;");
        body.Add("int count = sameSchema ? " + count + " : reader.ReadVersionHeader<" + Profile + ">(out revision);");
        body.Add("if (count < 0)");
        body.Add("{");
        body.Add("value = default;");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        bool allRaw = schema.Widths.All(width => width != 255);
        if (!allRaw && !model.IsStruct)
        {
            // Variable members start from the reused instance's values.
            body.Add("if (value is not null)");
            body.Add("{");
            for (int index = 0; index < members.Length; index++)
                if (!members[index].IsFixed)
                    body.Add(temps[index] + " = value!." + members[index].Name + ";");
            body.Add("}");
            body.Add("");
        }
        body.Add("if (sameSchema)");
        body.Add("{");
        int cursors = 0;
        // The publication layout separates an item from a preceding group or string, unless the item is a group itself.
        bool separate = false;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            int last = VersionRunEnd(model, index);
            bool group = last > index;
            if (separate && !group)
                body.Add("");
            separate = !member.IsFixed || group;
            if (schema.Widths[index] == 255)
            {
                string length = "length" + index.ToString(CultureInfo.InvariantCulture), end = End();
                body.Add("int " + length + " = reader.ReadVersionPayloadLength();");
                body.Add("int " + end + " = reader.Position + " + length + ";");
                AddMemberRead(body, model, member, w, "reader", temps[index]);
                body.Add(Reader() + ".ValidateVersionFieldEnd(reader.Position, " + end + ");");
                continue;
            }
            if (last == index)
            {
                string size = "Unsafe.SizeOf<" + schema.TypeNames[index] + ">()";
                if (schema.Widths[index] == 0)
                {
                    string length = "length" + index.ToString(CultureInfo.InvariantCulture);
                    body.Add("int " + length + " = " + size + " < 255 ? " + size + " : reader.ReadVersionPayloadLength();");
                    body.Add(Reader() + ".ValidateVersionFieldEnd(" + length + ", " + size + ");");
                }
                else
                    body.Add(Reader() + ".ValidateVersionFieldEnd(" + size + ", " + size + ");");
                body.Add("reader.EnsureReadable(" + size + ");");
                body.Add("reader.Advance(" + (member.IsBoolean && !model.Trusted ? Policy() + ".ReadBoolean" : Reader() + ".Read") + "(ref reader.Cursor, out " + temps[index] + "));");
                continue;
            }
            string width = Width(members, index, last);
            string cursor = "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));
            body.Add("ref byte " + cursor + " = ref reader.Cursor;");
            body.Add("reader.EnsureReadable(" + width + ");");
            body.Add("reader.Cursor = ref " + w.A("Unsafe") + ".Add(ref reader.Cursor, " + width + ");");
            string offset = "0";
            for (int item = index; item <= last; item++)
            {
                string at = At(w, cursor, offset, plain: true);
                body.Add(temps[item] + " = " + (members[item].IsBoolean && !model.Trusted ? Policy() + ".ReadBoolean(ref " + at + ");"
                    : Reader() + ".Read<" + members[item].Scalar + ">(ref " + at + ");"));
                offset = Add(offset, members[item].Width);
            }
            index = last;
        }
        body.Add("}");
        // A raw record of at most seven members returns its values; any other, including an empty record, fills the caller's
        // temporaries by reference or returns nothing.
        bool returns = allRaw && members.Length is > 0 and <= 7;
        string assignment = !returns ? string.Empty : members.Length == 1 ? temps[0] + " = " : "(" + string.Join(", ", temps) + ") = ";
        body.Add("else");
        body.Add("\t" + assignment + "ReadEvolvedFields(ref reader, count, revision" + (returns ? string.Empty : string.Concat(temps.Select(temp => ", ref " + temp))) + ");");
        body.Add("// Keep the uncommon mapped read separate from the same-schema native body.");
        body.Add(w.MethodImpl("NoInlining"));
        string returnType = !returns ? "void" : members.Length == 1 ? schema.TypeNames[0] : "(" + string.Join(", ", schema.TypeNames) + ")";
        body.Add("static " + returnType + " ReadEvolvedFields(ref " + Reader() + " reader2, int count2, int revision2" +
            (returns ? string.Empty : string.Concat(Enumerable.Range(0, members.Length).Select(index => ", ref " + schema.QualifiedTypes[index] + " " + temps[index] + "2"))) + ")");
        body.Add("{");
        if (returns)
            for (int index = 0; index < members.Length; index++)
                body.Add(schema.TypeNames[index] + " " + temps[index] + "2 = default;");
        body.Add("global::System.Span<int> widths = stackalloc int[count2];");
        body.Add("global::System.Span<int> slots = stackalloc int[count2];");
        body.Add("global::System.Span<byte> present = stackalloc byte[" + count + "];");
        string tableArguments = schema.Schema + ".Fields, revision2, slots, widths, present";
        body.Add(model.Net8
            ? "reader2.ReadVersionTable(" + tableArguments + ");"
            : "reader2.Position = " + Reader() + ".ReadVersionTable(reader2, " + tableArguments + ");");
        body.Add("for (int i = 0; i < count2; i++)");
        body.Add("{");
        body.Add("int length = widths[i] >= 0 ? widths[i] : reader2.ReadVersionPayloadLength();");
        body.Add("switch (slots[i])");
        body.Add("{");
        for (int index = 0; index < members.Length; index++)
        {
            body.Add("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
            body.Add("{");
            if (schema.Widths[index] == 255)
            {
                // A variable field is read within a temporary input limit of its recorded payload length.
                string end = End(), limit = Limit();
                body.Add("int " + end + " = reader2.Position + length;");
                body.Add("int " + limit + " = reader2.EnterVersionField(length);");
                body.Add("try");
                body.Add("{");
                AddMemberRead(body, model, members[index], w, "reader2", temps[index] + "2");
                body.Add("}");
                body.Add("finally");
                body.Add("{");
                body.Add("reader2.ExitVersionField(" + limit + ");");
                body.Add("}");
                body.Add("");
                body.Add(Reader() + ".ValidateVersionFieldEnd(reader2.Position, " + end + ");");
            }
            else
            {
                string size = "Unsafe.SizeOf<" + schema.TypeNames[index] + ">()";
                body.Add(Reader() + ".ValidateVersionFieldEnd(length, " + size + ");");
                body.Add("reader2.EnsureReadable(" + size + ");");
                body.Add("reader2.Advance(" + (members[index].IsBoolean && !model.Trusted ? Policy() + ".ReadBoolean" : Reader() + ".Read") + "(ref reader2.Cursor, out " + temps[index] + "2));");
            }
            body.Add("}");
            body.Add("");
            body.Add("\tbreak;");
        }
        body.Add("default:");
        body.Add("{");
        body.Add("reader2.EnsureReadable(length);");
        body.Add("reader2.Cursor = ref " + w.A("Unsafe") + ".Add(ref reader2.Cursor, unchecked((nint)(uint)((length))));");
        body.Add("}");
        body.Add("");
        body.Add("\tbreak;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        for (int index = 0; index < members.Length; index++)
        {
            body.Add("if (present[" + index.ToString(CultureInfo.InvariantCulture) + "] == 0)");
            body.Add("\t" + temps[index] + "2 = default !;");
        }
        if (returns)
            body.Add("return " + (members.Length == 1 ? temps[0] + "2" : "(" + string.Join(", ", temps.Select(temp => temp + "2")) + ")") + ";");
        body.Add("}");
        body.Add("");
        bool mirroredMembers = model.Members.Any(member => member.Mirrored);
        var initializer = members.Where(member => !member.Mirrored &&
            (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
        if (model.IsStruct)
        {
            if (model.Rent != null)
            {
                body.Add("value = " + (model.RentByRef ? "ref " : string.Empty) + model.Rent + ";");
                for (int index = 0; index < members.Length; index++)
                    if (!members[index].Mirrored)
                        body.Add("value." + members[index].Name + " = " + temps[index] + "!;");
            }
            else
                AddVersionConstruction(model, body, initializer, temps);
            if (mirroredMembers)
            {
                body.Add("ref var local = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value);");
                for (int index = 0; index < members.Length; index++)
                    if (members[index].Mirrored && (model.Rent != null || model.ConstructorMembers == null || !model.ConstructorMembers.Contains(members[index].Name)))
                        body.Add("local." + members[index].Name + " = " + temps[index] + "!;");
            }
            return body;
        }
        body.Add("var completedInstance = value;");
        body.Add("if (value is null)");
        body.Add("{");
        if (model.Rent != null)
        {
            // A rented instance is populated field by field, mirrored members through the layout mirror.
            body.Add("value = " + model.Rent + ";");
            body.Add("// Populate every field on the pooled instance.");
            if (mirroredMembers)
                body.Add("ref var local = ref " + "LuminPackMarshal" + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
            for (int index = 0; index < members.Length; index++)
                body.Add((members[index].Mirrored ? "local." : "value!.") + members[index].Name + " = " + temps[index] + "!;");
        }
        else
        {
            AddVersionConstruction(model, body, initializer, temps);
            if (mirroredMembers)
            {
                body.Add("// Assign private fields.");
                body.Add("ref var local = ref " + "LuminPackMarshal" + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
                for (int index = 0; index < members.Length; index++)
                    if (members[index].Mirrored && (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(members[index].Name)))
                        body.Add("local." + members[index].Name + " = " + temps[index] + "!;");
            }
        }
        body.Add("completedInstance = value;");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        if (mirroredMembers)
            body.Add("ref var reuseLocal = ref " + "LuminPackMarshal" + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref completedInstance!);");
        for (int index = 0; index < members.Length; index++)
            body.Add((members[index].Mirrored ? "reuseLocal." : "completedInstance!.") + members[index].Name + " = " + temps[index] + "!;");
        body.Add("}");
        return body;
    }

    /// <summary>Spells the fresh construction of a version-tolerant value, with the object initializer only for visible members.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="body">The receiving statements.</param>
    /// <param name="initializer">The members assigned by the object initializer.</param>
    /// <param name="temps">The temporary local of each member.</param>
    private static void AddVersionConstruction(PayloadRenderModel model, List<string> body, RenderMember[] initializer, string[] temps)
    {
        string arguments = model.ConstructorArguments == null ? string.Empty : string.Join(", ", model.ConstructorArguments.Select(name =>
            name == null ? "default" : temps[Array.FindIndex(model.Members, member => member.Name == name)] + "!"));
        if (initializer.Length == 0)
        {
            body.Add("value = new " + model.GlobalName + "(" + arguments + ");");
            return;
        }
        body.Add("value = new " + model.GlobalName + "(" + arguments + ")");
        body.Add("{");
        foreach (var member in initializer)
            body.Add(member.Name + " = " + temps[System.Array.IndexOf(model.Members, member)] + "!,");
        body.Add("};");
    }

    /// <summary>Emits version-tolerant value roots through their non-nullable state and schema contracts.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void EmitVersionStruct(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
        => EmitStruct(model, w, operations);

    /// <summary>Writes the schema class: the field table, the constant metadata and prefixes, the fingerprint, the short-record
    /// state and the prefix match, write and store, with constant prefixes compared and stored as disjoint words.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    private static void VersionSchemaClass(PayloadRenderModel model, CodeWriter w)
    {
        var schema = model.Version;
        byte[] prefix = schema.Dynamic ? Array.Empty<byte>() : schema.Prefix;
        string unsafeName = w.A("Unsafe"), writerName = w.A("LuminPackWriter"), readerName = w.A("LuminPackReader"), options = w.A("ILuminPackOptions");
        string inline = w.MethodImpl("AggressiveInlining");
        string length = prefix.Length.ToString(CultureInfo.InvariantCulture);
        string profile = "(ulong)" + writerName + ".BinaryProfile<TOptions>() << 16";
        w.Line("internal static class " + schema.Name);
        if (model.TypeConstraintText.Length != 0)
            w.Indented(() => w.Line(model.TypeConstraintText));
        w.Open();
        w.Line("internal static readonly global::LuminPack.Core.LuminPackVersionField[] Fields = new global::LuminPack.Core.LuminPackVersionField[]");
        w.Open();
        foreach (string entry in schema.FieldEntries)
            w.Line(entry);
        w.Close(";");
        if (schema.Dynamic)
        {
            w.Line("internal static readonly byte[] Metadata = global::LuminPack.Core.LuminPackVersionField.CreateMetadata(Fields);");
            w.Line("internal static readonly byte[] DefaultPrefix = global::LuminPack.Core.LuminPackVersionField.CreateDefaultPrefix(Metadata, " + model.Members.Length.ToString(CultureInfo.InvariantCulture) + ");");
            w.Line("internal static readonly byte[] Prefix = global::LuminPack.Core.LuminPackVersionField.CreatePrefix(Metadata, " + model.Members.Length.ToString(CultureInfo.InvariantCulture) +
                ", new int[] { " + string.Join(", ", schema.Widths.Select((width, index) => width == 0 ? unsafeName + ".SizeOf<" + schema.QualifiedTypes[index] + ">()" :
                    (width == 255 ? "-1" : width.ToString(CultureInfo.InvariantCulture)))) + " });");
            w.Line("internal static readonly ulong Fingerprint = global::LuminPack.Core.LuminPackVersionField.Fingerprint(global::System.MemoryExtensions.AsSpan(Prefix, 4));");
        }
        else
        {
            ConstantBytes("Metadata", schema.Metadata);
            ConstantBytes("DefaultPrefix", new byte[] { 251, 1, 0, (byte)model.Members.Length }.Concat(schema.Metadata).ToArray());
            ConstantBytes("Prefix", prefix);
            w.Line("internal const ulong Fingerprint = 0x" + schema.Fingerprint.ToString("X16", CultureInfo.InvariantCulture) + "UL;");
        }
        w.Blank();
        w.Line("internal const byte Count = " + model.Members.Length.ToString(CultureInfo.InvariantCulture) + ";");
        w.Line("internal static bool Shortens => " + (schema.Dynamic ? "Prefix.Length > 12" : prefix.Length > 12 ? "true" : "false") + ";");
        w.Blank();
        w.Line("[global::System.ThreadStatic]");
        w.Line("private static int t_written;");
        w.Blank();
        w.Line(inline);
        w.Line("private static bool WrittenBefore(ref " + writerName + " writer)");
        w.Open();
        w.Line("if (!Shortens)");
        w.Indented(() => w.Line("return false;"));
        w.Line("int operation = writer.GetSchemaOperation();");
        w.Line("if (t_written == operation)");
        w.Indented(() => w.Line("return true;"));
        w.Line("t_written = operation;");
        w.Line("return false;");
        w.Close();
        w.Blank();

        // Same-schema prefix test: eight-byte words folded into one branch.
        w.Line(inline);
        w.Line("internal static bool TryReadPrefix<TOptions>(ref " + readerName + " reader)");
        w.Indented(() => w.Line("where TOptions : struct, " + options));
        w.Open();
        w.Line("if (Shortens && reader.TryReadVersionShort<TOptions>(Count, Fingerprint))");
        w.Indented(() => w.Line("return true;"));
        if (prefix.Length < 8)
            w.Line("return reader.TryReadVersionPrefix<TOptions>(Prefix);");
        else
        {
            w.Line("if (" + unsafeName + ".SizeOf<nint>() != 8)");
            w.Indented(() => w.Line("return reader.TryReadVersionPrefix<TOptions>(Prefix);"));
            w.Line("ref byte input = ref reader.PeekInput(" + length + ", out bool available);");
            w.Line("if (!available)");
            w.Indented(() => w.Line("return false;"));
            var offsets = new List<int>();
            for (int offset = 0; offset + 8 <= prefix.Length; offset += 8)
                offsets.Add(offset);
            if (prefix.Length % 8 != 0)
                offsets.Add(prefix.Length - 8);
            var terms = offsets.Select(offset =>
            {
                ulong word = 0;
                for (int index = 7; index >= 0; index--)
                    word = word << 8 | prefix[offset + index];
                string load = offset == 0 ? unsafeName + ".ReadUnaligned<ulong>(ref input)"
                    : unsafeName + ".ReadUnaligned<ulong>(ref " + unsafeName + ".Add(ref input, " + offset.ToString(CultureInfo.InvariantCulture) + "))";
                string expected = "0x" + word.ToString("X16", CultureInfo.InvariantCulture) + "UL";
                if (offset == 0)
                    expected = "(" + expected + " | " + profile + ")";
                return "(" + load + " ^ " + expected + ")";
            });
            w.Line("if ((" + string.Join(" | ", terms) + ") != 0)");
            w.Indented(() => w.Line("return false;"));
            w.Line("reader.EnsureReadable(" + length + ");");
            w.Line("reader.Cursor = ref " + unsafeName + ".Add(ref reader.Cursor, " + length + ");");
            w.Line("return true;");
        }
        w.Close();
        w.Blank();

        // Prefix write into a fresh reservation.
        w.Line(inline);
        w.Line("internal static void WritePrefix<TOptions>(ref " + writerName + " writer)");
        w.Indented(() => w.Line("where TOptions : struct, " + options));
        w.Open();
        w.Line("if (WrittenBefore(ref writer))");
        w.Open();
        w.Line(writerName + ".StoreVersionShort<TOptions>(ref writer.ReservePrefix(12), Count, Fingerprint);");
        w.Line("return;");
        w.Close();
        w.Blank();
        if (prefix.Length < 8)
        {
            w.Line("writer.WriteVersionPrefix<TOptions>(Prefix);");
            w.Close();
            w.Blank();
        }
        else
        {
            w.Line("if (" + unsafeName + ".SizeOf<nint>() != 8)");
            w.Open();
            w.Line("writer.WriteVersionPrefix<TOptions>(Prefix);");
            w.Line("return;");
            w.Close();
            w.Blank();
            w.Line("ref byte target = ref writer.ReservePrefix(" + length + ");");
            PrefixWords(w, prefix, unsafeName, profile);
            w.Close();
            w.Blank();
        }

        // Prefix store into reserved record room, returning the stored byte count.
        w.Line(inline);
        w.Line("internal static int StorePrefix<TOptions>(ref " + writerName + " writer, ref byte target)");
        w.Indented(() => w.Line("where TOptions : struct, " + options));
        w.Open();
        w.Line("if (WrittenBefore(ref writer))");
        w.Open();
        w.Line(writerName + ".StoreVersionShort<TOptions>(ref target, Count, Fingerprint);");
        w.Line("return 12;");
        w.Close();
        w.Blank();
        if (prefix.Length < 8)
        {
            w.Line(writerName + ".StoreVersionPrefix<TOptions>(ref target, Prefix);");
            w.Line("return Prefix.Length;");
        }
        else
        {
            w.Line("if (" + unsafeName + ".SizeOf<nint>() != 8)");
            w.Open();
            w.Line(writerName + ".StoreVersionPrefix<TOptions>(ref target, Prefix);");
            w.Line("return Prefix.Length;");
            w.Close();
            w.Blank();
            PrefixWords(w, prefix, unsafeName, profile);
            w.Line("return " + length + ";");
        }
        w.Close();
        w.Close();

        void ConstantBytes(string name, byte[] bytes)
        {
            string values = string.Join(", ", bytes);
            w.Line(model.ConstantSpans
                ? "internal static global::System.ReadOnlySpan<byte> " + name + " => [" + values + "];"
                : "internal static readonly byte[] " + name + " = new byte[] { " + values + " };");
        }
    }

    /// <summary>Stores a constant prefix as disjoint eight-byte words and at most one value of each narrower width; the binary
    /// profile is stamped into the third header byte at run time.</summary>
    /// <param name="w">The writer.</param>
    /// <param name="prefix">The constant prefix bytes.</param>
    /// <param name="unsafeName">The resolved Unsafe spelling.</param>
    /// <param name="profile">The profile expression stamped into the first word.</param>
    private static void PrefixWords(CodeWriter w, byte[] prefix, string unsafeName, string profile)
    {
        int offset = 0;
        foreach (int width in new[] { 8, 4, 2, 1 })
            while (offset + width <= prefix.Length && (width == 8 || prefix.Length - offset < 2 * width))
            {
                ulong word = 0;
                for (int index = width - 1; index >= 0; index--)
                    word = word << 8 | prefix[offset + index];
                string type = width == 8 ? "ulong" : width == 4 ? "uint" : width == 2 ? "ushort" : "byte";
                string hex = word.ToString("X", CultureInfo.InvariantCulture);
                string value = offset == 0 ? "(" + type + ")(0x" + hex + "UL | " + profile + ")" : "(" + type + ")0x" + hex + (width == 8 ? "UL" : "U");
                string location = offset == 0 ? "ref target" : "ref " + unsafeName + ".Add(ref target, " + offset.ToString(CultureInfo.InvariantCulture) + ")";
                w.Line(unsafeName + ".WriteUnaligned(" + location + ", " + value + ");");
                offset += width;
            }
    }
}
