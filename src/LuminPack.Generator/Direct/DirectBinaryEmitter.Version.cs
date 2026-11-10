using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>The schema facts of a version-tolerant record, reduced to the strings and bytes its emitted text depends on.</summary>
/// <remarks>The bytes come from <see cref="VersionMetadataEncoder"/>, as in <c>LuminPackVersionTolerantCodeGenerator.GenerateSchema</c>.</remarks>
internal sealed class DirectVersionSchema : IEquatable<DirectVersionSchema>
{
    /// <summary>The qualified schema class, such as <c>global::LuminPack.Generated.LuminPackExtensions_App.Version_global_N_Model</c>.</summary>
    internal readonly string Schema;
    /// <summary>The schema class's own name.</summary>
    internal readonly string Name;
    /// <summary>The field table entries, one <c>new LuminPackVersionField(...)</c> per member.</summary>
    internal readonly string[] FieldEntries;
    /// <summary>The constant member metadata.</summary>
    internal readonly byte[] Metadata;
    /// <summary>The constant revision-2 prefix with a zero profile byte.</summary>
    internal readonly byte[] Prefix;
    /// <summary>The declared types of the read temporaries, per member.</summary>
    internal readonly string[] TempTypes;
    /// <summary>The analyzed type names of the members, used by the mapped read's tuple and size checks.</summary>
    internal readonly string[] TypeNames;
    /// <summary>The fully qualified member types, used by the mapped read's by-reference parameters.</summary>
    internal readonly string[] QualifiedTypes;
    /// <summary>The root-private layout mirror every version-tolerant record declares.</summary>
    internal readonly string Layout;
    /// <summary>The mirror's field declarations in storage order.</summary>
    internal readonly string[] LayoutFields;
    /// <summary>The former names of each member, which JSON reads accept.</summary>
    internal readonly string[][] OldNames;

    private DirectVersionSchema(string schema, byte[] metadata, byte[] prefix, string[] fieldEntries, string[] tempTypes, string[] typeNames,
        string[] qualifiedTypes, string layout, string[] layoutFields, string[][] oldNames)
    {
        OldNames = oldNames;
        Schema = schema;
        Name = schema.Substring(schema.LastIndexOf(".Version_", StringComparison.Ordinal) + 1);
        Metadata = metadata;
        Prefix = prefix;
        FieldEntries = fieldEntries;
        TempTypes = tempTypes;
        TypeNames = typeNames;
        QualifiedTypes = qualifiedTypes;
        Layout = layout;
        LayoutFields = layoutFields;
    }

    /// <summary>Models the schema of a version-tolerant record whose members are public raw scalars with constant widths.</summary>
    /// <returns>The schema, or null when the record keeps the original pipeline.</returns>
    internal static DirectVersionSchema TryCreate(LuminDataInfo data, INamedTypeSymbol type, MetaInfo metadata, DirectMember[] members, bool derived, bool mirrored)
    {
        if (derived || data.isValueType || data.VersionSchemaType == null)
            return DirectEmission.Reject<DirectVersionSchema>("version tolerant: shape");
        if (members.Any(member => member.Kind is DirectMemberKind.Array or DirectMemberKind.List || member.Raw))
            return DirectEmission.Reject<DirectVersionSchema>("version tolerant: members");
        // Constant schema bytes are emitted as collection expressions, which the original emits on .NET 8 with C# 12 or later.
        if (!metadata.IsNet8 || metadata.CSharpVersion < (LanguageVersion)1200)
            return DirectEmission.Reject<DirectVersionSchema>("version tolerant: settings");
        var metadataBytes = VersionMetadataEncoder.Encode(data);
        var widths = VersionMetadataEncoder.Widths(data);
        if (metadataBytes == null || widths.Any(width => width == null))
            return DirectEmission.Reject<DirectVersionSchema>("version tolerant: widths");
        var prefix = new byte[] { 251, 2, 0, (byte)data.fields.Count }.Concat(metadataBytes).Concat(widths.Select(width => (byte)width.Value)).ToArray();
        var entries = data.fields.Select(field =>
        {
            var aliases = new[] { field.Name }.Concat(field.OldNames).Where(alias => alias != field.SerializedName).Select(Literal).ToArray();
            return "new global::LuminPack.Core.LuminPackVersionField(" + Literal(field.SerializedName) + ", new string[] " +
                (aliases.Length == 0 ? "{ }" : "{ " + string.Join(", ", aliases) + " }") + ", typeof(" +
                field.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "), " + Literal(VersionMetadataEncoder.Representation(field)) + "),";
        }).ToArray();
        var temps = new string[data.fields.Count];
        for (int index = 0; index < temps.Length; index++)
        {
            var local = data.localFields.FirstOrDefault(candidate => candidate.Identifier == data.fields[index].Identifier);
            if (local == null)
                return DirectEmission.Reject<DirectVersionSchema>("version tolerant: shape");
            temps[index] = local.TypeName + (local.IsValue ? string.Empty : "?");
        }
        if (data.localFields.Count != members.Length)
            return DirectEmission.Reject<DirectVersionSchema>("version tolerant: shape");
        string layout;
        using (var hash = System.Security.Cryptography.SHA256.Create())
            layout = "LuminLayout_" + string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(FormatterTypeName.Get(type)))
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture))) + "_" +
                LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type) + "_" + TypeMetaChecker.BuildLocalMyClassName(data);
        var layoutFields = data.localFields.Select(local => local.TypeName +
            (LuminPackCodeGenerator.IsUnmanagedFiledType(local.TypeName) || local.IsValue ? string.Empty : "?") + " " + local.Identifier).ToArray();
        return new DirectVersionSchema(data.VersionSchemaType, metadataBytes, prefix, entries, temps, data.fields.Select(field => field.TypeName).ToArray(),
            data.fields.Select(field => field.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToArray(), layout, layoutFields,
            data.fields.Select(field => field.OldNames.ToArray()).ToArray());
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    /// <summary>The FNV-1a fingerprint of the prefix after its four header bytes.</summary>
    internal ulong Fingerprint
    {
        get
        {
            ulong fingerprint = 14695981039346656037UL;
            foreach (byte value in Prefix.Skip(4))
                fingerprint = (fingerprint ^ value) * 1099511628211UL;
            return fingerprint;
        }
    }

    /// <inheritdoc/>
    public bool Equals(DirectVersionSchema other) => other != null && Schema == other.Schema && Layout == other.Layout &&
        Metadata.SequenceEqual(other.Metadata) && Prefix.SequenceEqual(other.Prefix) && FieldEntries.SequenceEqual(other.FieldEntries) &&
        TempTypes.SequenceEqual(other.TempTypes) && TypeNames.SequenceEqual(other.TypeNames) && QualifiedTypes.SequenceEqual(other.QualifiedTypes) &&
        LayoutFields.SequenceEqual(other.LayoutFields) && OldNames.Length == other.OldNames.Length &&
        OldNames.Zip(other.OldNames, (left, right) => left.SequenceEqual(right)).All(same => same);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectVersionSchema);

    /// <inheritdoc/>
    public override int GetHashCode() => Schema.GetHashCode();
}

internal static partial class DirectBinaryEmitter
{
    /// <summary>Builds a version-tolerant record write: the null object marker, one reservation covering the schema prefix, the raw
    /// members and the length-prefixed strings, the prefix store, and the members stored like an ordinary reserved record, each
    /// string behind its four-byte payload length.</summary>
    private static List<string> VersionWriteBody(DirectTypeModel model, DirectWriter w)
    {
        var members = model.Members;
        var schema = model.Version;
        var body = new List<string>
        {
            "var value = input;",
            "if (value is null)",
            "{",
            "writer.Cursor = " + w.A("LuminPackCode") + ".NullObject;",
            "writer.Advance(1);",
            "return;",
            "}",
            "",
            "ref byte cursor = ref writer.Cursor;",
        };
        var terms = new List<string> { schema.Schema + ".Prefix.Length" };
        for (int index = 0; index < members.Length; index++)
        {
            if (members[index].Kind != DirectMemberKind.Text)
            {
                terms.Add(members[index].Width);
                continue;
            }
            body.Add("string " + VersionSnapshot(model, index) + " = value." + members[index].Name + ";");
            terms.Add("4");
            terms.Add("writer.RecordBound<" + Profile + ">(" + VersionSnapshot(model, index) + ")");
        }
        body.Add("cursor = ref writer.ReserveRecord(ref cursor, checked(" + string.Join(" + ", terms) + "));");
        body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, unchecked((nint)(uint)(" + schema.Schema + ".StorePrefix<" + Profile + ">(ref writer, ref cursor))));");
        int lengths = 0;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.Kind == DirectMemberKind.Text)
            {
                // A string payload is preceded by its byte length, patched after the string is written.
                string length = lengths++ == 0 ? "length" : "length" + (lengths - 1).ToString(CultureInfo.InvariantCulture);
                body.Add("// Field: " + member.Name);
                body.Add("ref byte " + length + " = ref cursor;");
                body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 4);");
                body.Add("cursor = ref writer.WriteReservedRecordString<" + Profile + ">(ref cursor, " + VersionSnapshot(model, index) + ");");
                body.Add(w.A("Unsafe") + ".WriteUnaligned(ref " + length + ", (int)" + w.A("Unsafe") + ".ByteOffset(ref " + w.A("Unsafe") + ".Add(ref " + length + ", 4), ref cursor));");
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                if (member.Size == 8)
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
            string width = Width(members, index, last);
            body.Add("if (" + Proof(w, members, index, last, "value", readOnly: true) + ")");
            body.Add("\tcursor = ref " + w.A("LuminPackWriter") + ".WriteReservedBlock(ref cursor, ref " + Bytes(w, member, "value", readOnly: true) + ", " + width + ");");
            body.Add("else");
            body.Add("{");
            AddFixedStores(w, members, index, last, "cursor", body);
            body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + width + ");");
            body.Add("}");
            body.Add("");
            index = last;
        }
        // A record with a string separates the commit from its last store.
        if (members.Any(member => member.Kind == DirectMemberKind.Text) && body[body.Count - 1].Length != 0)
            body.Add("");
        body.Add("writer.Cursor = ref cursor;");
        return body;
    }

    /// <summary>Whether a version-tolerant write root keeps its inline hint: its template stays within the sixteen-call budget
    /// only without a fixed group and with at most one string.</summary>
    internal static bool VersionWriteInline(DirectTypeModel model)
    {
        var members = model.Members;
        bool group = Enumerable.Range(0, members.Length).Any(index => members[index].IsFixed && RunEnd(members, index) > index);
        return !group && members.Count(member => member.Kind == DirectMemberKind.Text) <= 1;
    }

    /// <summary>Spells a string member's write snapshot, camel-cased like the other bound locals of these operations.</summary>
    private static string VersionSnapshot(DirectTypeModel model, int index)
    {
        string trimmed = model.Members[index].Name.TrimStart('_');
        string stem = trimmed + "Snapshot";
        string name = stem + index.ToString(CultureInfo.InvariantCulture);
        string chosen = DirectBinaryEmitter.AsciiLetters(trimmed) && model.Members.All(member => member.Name != stem) ? stem : name;
        return char.ToLowerInvariant(chosen[0]) + chosen.Substring(1);
    }

    /// <summary>Builds a version-tolerant record read: the same-schema record read like an ordinary record (raw runs as fixed
    /// groups, lone raw members and length-framed strings one by one), the evolved record mapped by name in a separate non-inlined
    /// function, and the construction or reuse of the instance.</summary>
    private static List<string> VersionReadBody(DirectTypeModel model, DirectWriter w)
    {
        var members = model.Members;
        var schema = model.Version;
        string count = members.Length.ToString(CultureInfo.InvariantCulture);
        string text = "global::LuminPack." + BinaryExtensionProjection.Owner(model.Extension, "string");
        string Policy() => w.A("LuminPackInputPolicy");
        string Reader() => w.A("LuminPackReader");
        var temps = members.Select(member => DirectEmission.LocalName(member.Name) + "Temp").ToArray();
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
        bool allRaw = members.All(member => member.IsFixed);
        if (!allRaw)
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
            bool group = member.IsFixed && RunEnd(members, index) > index;
            if (separate && !group)
                body.Add("");
            separate = !member.IsFixed || group;
            if (member.Kind == DirectMemberKind.Text)
            {
                string length = "length" + index.ToString(CultureInfo.InvariantCulture), end = End();
                body.Add("int " + length + " = reader.ReadVersionPayloadLength();");
                body.Add("int " + end + " = reader.Position + " + length + ";");
                body.Add(text + ".Read<" + Profile + ">(ref reader, ref " + temps[index] + "!);");
                body.Add(Reader() + ".ValidateVersionFieldEnd(reader.Position, " + end + ");");
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                string size = "Unsafe.SizeOf<" + schema.TypeNames[index] + ">()";
                body.Add(Reader() + ".ValidateVersionFieldEnd(" + size + ", " + size + ");");
                body.Add("reader.EnsureReadable(" + size + ");");
                body.Add("reader.Advance(" + (member.IsBoolean ? Policy() + ".ReadBoolean" : Reader() + ".Read") + "(ref reader.Cursor, out " + temps[index] + "));");
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
                body.Add(temps[item] + " = " + (members[item].IsBoolean ? Policy() + ".ReadBoolean(ref " + at + ");"
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
        body.Add("scoped global::System.Span<int> widths = stackalloc int[count2];");
        body.Add("scoped global::System.Span<int> slots = stackalloc int[count2];");
        body.Add("scoped global::System.Span<byte> present = stackalloc byte[" + count + "];");
        body.Add("reader2.ReadVersionTable(" + schema.Schema + ".Fields, revision2, slots, widths, present);");
        body.Add("for (int i = 0; i < count2; i++)");
        body.Add("{");
        body.Add("int length = widths[i] >= 0 ? widths[i] : reader2.ReadVersionPayloadLength();");
        body.Add("switch (slots[i])");
        body.Add("{");
        for (int index = 0; index < members.Length; index++)
        {
            body.Add("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
            body.Add("{");
            if (members[index].Kind == DirectMemberKind.Text)
            {
                // A string is read within a temporary input limit of its recorded payload length.
                string end = End(), limit = Limit();
                body.Add("int " + end + " = reader2.Position + length;");
                body.Add("int " + limit + " = reader2.EnterVersionField(length);");
                body.Add("try");
                body.Add("{");
                body.Add(text + ".Read<" + Profile + ">(ref reader2, ref " + temps[index] + "2!);");
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
                body.Add("reader2.Advance(" + (members[index].IsBoolean ? Policy() + ".ReadBoolean" : Reader() + ".Read") + "(ref reader2.Cursor, out " + temps[index] + "2));");
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
        body.Add("var completedInstance = value;");
        body.Add("if (value is null)");
        body.Add("{");
        bool mirroredMembers = model.Members.Any(member => member.Mirrored);
        var initializer = members.Where(member => !member.Mirrored).ToArray();
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
        else if (initializer.Length == 0)
            body.Add("value = new " + model.GlobalName + "();");
        else
        {
            body.Add("value = new " + model.GlobalName + "()");
            body.Add("{");
            foreach (var member in initializer)
                body.Add(member.Name + " = " + temps[System.Array.IndexOf(members, member)] + "!,");
            body.Add("};");
        }
        if (model.Rent == null && mirroredMembers)
        {
            body.Add("// Assign private fields.");
            body.Add("ref var local = ref " + "LuminPackMarshal" + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
            for (int index = 0; index < members.Length; index++)
                if (members[index].Mirrored)
                    body.Add("local." + members[index].Name + " = " + temps[index] + "!;");
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

    /// <summary>Writes the schema class: the field table, the constant metadata and prefixes, the fingerprint, the short-record
    /// state and the prefix match, write and store, with constant prefixes compared and stored as disjoint words.</summary>
    private static void VersionSchemaClass(DirectTypeModel model, DirectWriter w)
    {
        var schema = model.Version;
        byte[] prefix = schema.Prefix;
        string unsafeName = w.A("Unsafe"), writerName = w.A("LuminPackWriter"), readerName = w.A("LuminPackReader"), options = w.A("ILuminPackOptions");
        string inline = w.MethodImpl("AggressiveInlining");
        string length = prefix.Length.ToString(CultureInfo.InvariantCulture);
        string profile = "(ulong)" + writerName + ".BinaryProfile<TOptions>() << 16";
        w.Line("internal static class " + schema.Name);
        w.Open();
        w.Line("internal static readonly global::LuminPack.Core.LuminPackVersionField[] Fields = new global::LuminPack.Core.LuminPackVersionField[]");
        w.Open();
        foreach (string entry in schema.FieldEntries)
            w.Line(entry);
        w.Close(";");
        w.Line("internal static global::System.ReadOnlySpan<byte> Metadata => [" + string.Join(", ", schema.Metadata) + "];");
        w.Line("internal static global::System.ReadOnlySpan<byte> DefaultPrefix => [251, 1, 0, " + model.Members.Length.ToString(CultureInfo.InvariantCulture) +
            (schema.Metadata.Length == 0 ? string.Empty : ", " + string.Join(", ", schema.Metadata)) + "];");
        w.Line("internal static global::System.ReadOnlySpan<byte> Prefix => [" + string.Join(", ", prefix) + "];");
        w.Blank();
        w.Line("internal const ulong Fingerprint = 0x" + schema.Fingerprint.ToString("X16", CultureInfo.InvariantCulture) + "UL;");
        w.Line("internal const byte Count = " + model.Members.Length.ToString(CultureInfo.InvariantCulture) + ";");
        w.Line("internal static bool Shortens => " + (prefix.Length > 12 ? "true" : "false") + ";");
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
    }

    /// <summary>Stores a constant prefix as disjoint eight-byte words and at most one value of each narrower width; the binary
    /// profile is stamped into the third header byte at run time.</summary>
    private static void PrefixWords(DirectWriter w, byte[] prefix, string unsafeName, string profile)
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
