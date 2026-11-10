using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.Code.Core;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Writes a covered type's binary roots, facade entries and extensions in their final published form.</summary>
/// <remarks>The text is what the original pipeline publishes for these shapes: the cursor record write with one reservation,
/// proven fixed-field blocks with their field-wise fallbacks, checked reads with Boolean batch masks, the root container, the
/// state entries and the payload extensions. Lines longer than the layout width are wrapped by the publication formatter.</remarks>
internal static partial class DirectBinaryEmitter
{
    private const string Profile = "TLuminProfile";
    private const string Constraint = "where TLuminProfile : struct, ILuminPackOptions";

    /// <summary>Emits the complete binary file body.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls. The members of the others are left out, as the operation
    /// filter removes them from a finished file; the declarations that hold them stay.</param>
    internal static void Emit(DirectTypeModel model, DirectWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        var members = model.Members;
        // Version-tolerant roots always take the operation's profile, and so does a string anywhere in an inline object below.
        bool genericWrite = model.Version != null || model.ProfilePayload;
        // A fixed member or an inline object of fixed members needs no profile; anything else, including a string in an inline
        // object, does.
        bool genericRead = model.Version != null || model.ProfilePayload ||
            members.Any(member => member.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Object));
        bool allFixed = members.All(member => member.IsFixed);
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        // State parameters are spelled through the imported namespace; the other helper names are qualified until presentation.
        const string writer = "LuminPackWriter", reader = "LuminPackReader";
        string Options() => w.A("ILuminPackOptions");
        string Serializer() => w.A("LuminPackSerializer");
        string Standard() => w.A("LuminPackUtf16LengthOptions");
        string Constraint() => "where TLuminProfile : struct, " + Options();
        string writeRoot = genericWrite ? "RootWrite<" + Profile + ">" : "RootWrite";
        string readRoot = genericRead ? "RootRead<" + Profile + ">" : "RootRead";
        string writeUnion = genericWrite ? "RootWriteUnion<" + Profile + ">" : "RootWriteUnion";
        string readUnion = genericRead ? "RootReadUnion<" + Profile + ">" : "RootReadUnion";
        string inline = w.MethodImpl("AggressiveInlining");
        // Interception gives every payload's state entries unique names; otherwise they are overloads.
        string suffix = model.UniqueEntries ? "_" + readable : string.Empty;
        // The Union extension entries spell the inlining option through the imported alias name.
        string polymorphic = "[" + w.Attribute("MethodImpl") + "(MethodImplOptions.AggressiveInlining)]";

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        // Serialize entries; the root container follows the entry that carried the write body, which is the named member
        // write when the type has one.
        w.Suppressed = !writes;
        if (model.MemberWrite)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void SerializeMember" + suffix + "(scoped ref " + writer + " writer, scoped in " + type + "? input) => SerializeMember" +
                suffix + "<" + Standard() + ">(ref writer, in input);");
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void SerializeMember" + suffix + "<" + Profile + ">(scoped ref " + writer + " writer, scoped in " + type + "? input)");
            w.Indented(() => w.Line(Constraint() + " => " + container + ".RootWriteMember<" + Profile + ">(ref writer, input);"));
            w.Blank();
        }
        else
        {
            SerializeStandard();
            if (genericWrite)
            {
                SerializeProfiled();
                w.Blank();
            }
        }
        w.Suppressed = false;
        Roots();
        w.Blank();
        w.Suppressed = !writes;
        if (model.MemberWrite)
        {
            SerializeStandard();
            SerializeProfiled();
            w.Blank();
        }
        else if (!genericWrite)
        {
            SerializeProfiled();
            w.Blank();
        }
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize" + suffix + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) => " + container + "." +
            (genericRead ? "RootRead<" + Standard() + ">" : "RootRead") + "(ref reader, ref value);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize" + suffix + "<" + Profile + ">(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(Constraint() + " => " + container + "." + readRoot + "(ref reader, ref value);"));
        w.Blank();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeUnion(scoped ref " + writer + " writer, " + type + "? input) => " +
            (genericWrite ? "SerializeUnion<" + Standard() + ">(ref writer, input);" : container + ".RootWriteUnion(ref writer, input);"));
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeUnion<" + Profile + ">(scoped ref " + writer + " writer, " + type + "? input)");
        w.Indented(() => w.Line(Constraint() + " => " + (genericWrite ? container + "." + writeUnion + "(ref writer, input);" : "SerializeUnion(ref writer, input);")));
        w.Blank();
        if (model.FixedUnionCase)
        {
            Method(FixedUnionBody(w, WriteBody(model, w, union: true)), "SerializeUnionAt", false,
                "(scoped ref " + writer + " writer, " + type + "? input, int initialIndex, byte? unionTag)", inline, "int");
            w.Blank();
        }
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeUnion" + suffix + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) => " +
            (genericRead ? "DeserializeUnion" + suffix + "<" + Standard() + ">(ref reader, ref value);" : container + ".RootReadUnion(ref reader, ref value);"));
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeUnion" + suffix + "<" + Profile + ">(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(Constraint() + " => " + (genericRead ? container + "." + readUnion + "(ref reader, ref value);" : "DeserializeUnion" + suffix + "(ref reader, ref value);")));
        w.Suppressed = !writes;
        if (model.MeasureRecord)
        {
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void MeasureRecord" + suffix + "(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + type + "? value)");
            w.Open();
            w.Line("if (value is null)");
            w.Open();
            w.Line("evaluator.Add(1);");
            w.Line("return;");
            w.Close();
            w.Blank();
            if (model.Version != null)
                w.Line("evaluator.Add(" + model.Version.Schema + ".Prefix.Length + " + members.Sum(member => member.Size).ToString(CultureInfo.InvariantCulture) + ");");
            else
            {
                w.Line("int totalLength = 1;");
                // A memberless record's only byte is its marker, which the initializer already counts; constant widths
                // fold into one term, while a runtime struct size stays an expression.
                if (members.Length != 0)
                {
                    int constant = 0;
                    bool folded = members.All(member => int.TryParse(member.Width, NumberStyles.None, CultureInfo.InvariantCulture, out _));
                    if (folded)
                        foreach (var member in members)
                            constant += int.Parse(member.Width, CultureInfo.InvariantCulture);
                    w.Line("totalLength += " + (folded ? constant.ToString(CultureInfo.InvariantCulture) :
                        string.Join(" + ", members.Select(member => member.Width))) + ";");
                }
                w.Line("evaluator.Add(totalLength);");
            }
            w.Close();
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void MeasureRecord" + suffix + "<" + Profile + ">(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + type + "? value)");
            w.Indented(() => w.Line(Constraint() + " => MeasureRecord" + suffix + "(ref evaluator, ref value);"));
        }
        w.Suppressed = false;
        if ((model.Mirror ?? model.Version?.Layout) != null)
        {
            // The root-private layout mirror that the operations reach auto-property storage through; a version-tolerant record
            // always declares it.
            w.Blank();
            w.Line(w.Preserve);
            if (model.LayoutAttribute != null)
                w.Line(model.LayoutAttribute);
            w.Line("private class " + (model.Mirror ?? model.Version.Layout));
            w.Open();
            foreach (string field in model.MirrorFields ?? model.Version.LayoutFields)
                w.Line("internal " + field + ";");
            w.Close();
        }
        w.Close();
        w.Blank();

        // Payload extensions.
        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write(this ref " + writer + " state, in " + type + "? value) => " + Serializer() + "." + container + "." +
            (genericWrite ? "RootWrite<" + Standard() + ">" : "RootWrite") + "(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + Options() + " => " + Serializer() + "." + container + "." +
            (genericWrite ? "RootWrite<TOptions>" : "RootWrite") + "(ref state, value);"));
        w.Suppressed = !reads;
        if (model.Version != null)
        {
            // A version-tolerant record reads through an isolated copy of the reader state.
            IsolatedRead("internal static void Read(this ref " + reader + " state, ref " + type + "? value)", null, Standard());
            w.Blank();
            IsolatedRead("internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + "? value)", "where TOptions : struct, " + Options(), "TOptions");
        }
        else
        {
            w.Line(inline);
            w.Line("internal static void Read(this ref " + reader + " state, ref " + type + "? value) => " + Serializer() + "." + container + "." +
                (genericRead ? "RootRead<" + Standard() + ">" : "RootRead") + "(ref state, ref value);");
            w.Line(inline);
            w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + "? value)");
            w.Indented(() => w.Line("where TOptions : struct, " + Options() + " => " + Serializer() + "." + container + "." +
                (genericRead ? "RootRead<TOptions>" : "RootRead") + "(ref state, ref value);"));
        }
        w.Suppressed = false;
        w.Close();
        w.Close();
        w.Blank();

        w.Line("namespace LuminPack.Generated");
        w.Open();
        w.Line("internal static partial class " + model.Extension);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(polymorphic);
        w.Line("public static void WritePolymorphismValue(ref this " + writer + " writer, scoped in " + type + "? input) => " + Serializer() + ".SerializeUnion(ref writer, input);");
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(polymorphic);
        w.Line("public static void ReadPolymorphismValue(ref this " + reader + " reader, scoped ref " + type + "? value) => " + Serializer() +
            ".DeserializeUnion" + suffix + "(ref reader, ref value);");
        w.Suppressed = false;
        if (model.Version != null)
        {
            w.Blank();
            VersionSchemaClass(model, w);
        }
        w.Close();
        w.Close();

        void IsolatedRead(string signature, string constraint, string profile)
        {
            w.Line(inline);
            w.Line(signature);
            if (constraint != null)
                w.Indented(() => w.Line(constraint));
            w.Open();
            w.Line(reader + " isolated = default;");
            w.Line("state.CopyTo(ref isolated);");
            w.Line(Serializer() + "." + container + ".RootRead<" + profile + ">(ref isolated, ref value);");
            w.Line("isolated.CopyTo(ref state);");
            w.Close();
        }

        void SerializeStandard()
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void Serialize" + suffix + "(scoped ref " + writer + " writer, scoped in " + type + "? input) => " + container + "." +
                (genericWrite ? "RootWrite<" + Standard() + ">" : "RootWrite") + "(ref writer, input);");
            w.Blank();
        }

        void SerializeProfiled()
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void Serialize" + suffix + "<" + Profile + ">(scoped ref " + writer + " writer, scoped in " + type + "? input)");
            w.Indented(() => w.Line(Constraint() + " => " + container + "." + writeRoot + "(ref writer, input);"));
        }

        // A stream record's body is already the lowered statement text, so the call budget is counted from it directly. A
        // member write stays a call boundary whose callee keeps its own budget, so the runtime sizes the entry instead.
        string WriteHint(List<string> body, bool union) => model.StreamRecord
            ? (DirectCalls.Count(body) <= 16 && !body.Any(line => line.IndexOf(".SerializeMember_", StringComparison.Ordinal) >= 0) ? inline : null)
            : (DirectInlining.Write(model, union, Sizes(body)) ? inline : null);

        string ReadHint(List<string> body) => model.StreamRecord
            ? (DirectCalls.Count(body) <= 16 ? inline : null)
            : (DirectInlining.Read(model, Sizes(body)) ? inline : null);

        void Roots()
        {
            w.Line("internal static partial class " + container);
            w.Open();
            w.Suppressed = !writes;
            if (model.MemberWrite)
            {
                Method(MemberWriteBody(model, w), "RootWriteMember", true, "(scoped ref " + writer + " writer, " + type + "? input)", inline);
                w.Blank();
            }
            if (model.Version != null)
            {
                VersionRoots();
                return;
            }
            var write = Mirrored(model, w, WriteBody(model, w, union: false), writes: true, union: false);
            if (model.Proven)
                write = ProvenWriteBody(model, w, write, union: false);
            Method(write, "RootWrite", genericWrite, "(scoped ref " + writer + " writer, " + type + "? input)",
                WriteHint(write, union: false));
            w.Blank();
            w.Suppressed = !reads;
            var read = Mirrored(model, w, ReadBody(model, w, union: false), writes: false, union: false);
            Method(read, "RootRead", genericRead, "(scoped ref " + reader + " reader, scoped ref " + type + "? value)",
                ReadHint(read));
            w.Blank();
            w.Suppressed = !writes;
            if (model.FixedUnionCase)
            {
                // A fixed Union case's Union write publishes the cursor its shared returning kernel computes.
                w.Line(w.Preserve);
                w.Line(inline);
                w.Line("internal static void RootWriteUnion(scoped ref " + writer + " writer, " + type +
                    "? input) => writer.Position = SerializeUnionAt(ref writer, input, writer.Position, null);");
            }
            else
            {
                var writeUnionBody = Mirrored(model, w, WriteBody(model, w, union: true), writes: true, union: true);
                // A lone record checks itself, so only a reserved record takes the proven path.
                if (model.Proven && members.Length != 1)
                    writeUnionBody = ProvenWriteBody(model, w, writeUnionBody, union: true);
                Method(writeUnionBody, "RootWriteUnion", genericWrite, "(scoped ref " + writer + " writer, " + type + "? input)",
                    WriteHint(writeUnionBody, union: true));
            }
            w.Blank();
            w.Suppressed = !reads;
            var readUnionBody = Mirrored(model, w, ReadBody(model, w, union: true), writes: false, union: true);
            // A declared reference member keeps the Union read behind its outlined entry; a fixed record leaves the hint to the
            // runtime when a contiguous block cannot be proven, as the original expansion does.
            Method(readUnionBody, "RootReadUnion", genericRead, "(scoped ref " + reader + " reader, scoped ref " + type + "? value)",
                model.ReferencePayload ? w.MethodImpl("NoInlining")
                    : allFixed && DirectInlining.FixedUnionRead(model, Sizes(readUnionBody)) ? inline : null);
            w.Suppressed = false;
            w.Close();
        }

        void VersionRoots()
        {
            // The Union payload of a version-tolerant record is the record itself; only the inline hint follows the body size.
            var write = VersionWriteBody(model, w);
            string writeHint = VersionWriteInline(model) ? inline : null;
            w.Suppressed = !writes;
            Method(write, "RootWrite", true, "(scoped ref " + writer + " writer, " + type + "? input)", writeHint);
            w.Blank();
            w.Suppressed = !reads;
            Method(VersionReadBody(model, w), "RootRead", true, "(scoped ref " + reader + " reader, scoped ref " + type + "? value)", null);
            w.Blank();
            w.Suppressed = !writes;
            Method(VersionWriteBody(model, w), "RootWriteUnion", true, "(scoped ref " + writer + " writer, " + type + "? input)", writeHint);
            w.Blank();
            w.Suppressed = !reads;
            Method(VersionReadBody(model, w), "RootReadUnion", true, "(scoped ref " + reader + " reader, scoped ref " + type + "? value)",
                members.All(member => member.IsFixed) ? null : w.MethodImpl("NoInlining"));
            w.Suppressed = false;
            w.Close();
        }

        void Method(List<string> body, string name, bool generic, string parameters, string attribute, string returns = "void")
        {
            w.Line(w.Preserve);
            if (attribute != null)
                w.Line(attribute);
            w.Line("internal static " + returns + " " + name + (generic ? "<" + Profile + ">" : string.Empty) + parameters);
            if (generic)
                w.Indented(() => w.Line(Constraint()));
            w.Open();
            foreach (string line in body)
            {
                if (line.Length == 0)
                    w.Blank();
                else if (line == "}")
                    w.Close();
                else if (line == "};")
                    w.Close(";");
                else if (line == "{")
                    w.Open();
                else if (line.StartsWith("\t", StringComparison.Ordinal))
                    w.Indented(() => w.Line(line.Substring(1)));
                else
                    w.Line(line);
            }
            w.Close();
        }
    }

    /// <summary>Spells an unaligned cursor position; a plain spelling was already short in the template and registers no alias.</summary>
    private static string At(DirectWriter w, string cursor, string offset, bool plain = false)
        => offset == "0" ? cursor : (plain ? "Unsafe" : w.A("Unsafe")) + ".Add(ref " + cursor + ", " + offset + ")";

    /// <summary>Adds a member's width to an offset as the original template does: constants fold until the first runtime size, after
    /// which every term is appended in order.</summary>
    private static string Add(string offset, string size)
    {
        if (offset == "0")
            return size;
        if (int.TryParse(offset, NumberStyles.None, CultureInfo.InvariantCulture, out int total) &&
            int.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out int length) && total <= int.MaxValue - length)
            return (total + length).ToString(CultureInfo.InvariantCulture);
        return offset + " + " + size;
    }

    /// <summary>The packed width of a run of fixed members.</summary>
    private static string Width(DirectMember[] members, int first, int last)
    {
        string width = "0";
        for (int index = first; index <= last; index++)
            width = Add(width, members[index].Width);
        return width;
    }

    /// <summary>Counts the runtime-size calls of raw struct widths in a root body, which the inlining budget counts as calls.</summary>
    private static int Sizes(List<string> body)
    {
        int count = 0;
        foreach (string line in body)
            for (int index = line.IndexOf(".SizeOf<", StringComparison.Ordinal); index >= 0; index = line.IndexOf(".SizeOf<", index + 1, StringComparison.Ordinal))
                if (string.CompareOrdinal(line, index + 8, "global::System.IntPtr>", 0, 22) != 0)
                    count++;
        return count;
    }

    /// <summary>Spells a member's storage reinterpreted as bytes for a block copy or proof; see <see cref="At"/> for plain spellings.</summary>
    private static string Bytes(DirectWriter w, DirectMember member, string owner, bool readOnly, bool plain = false)
        => (plain ? "Unsafe" : w.A("Unsafe")) + ".As<" + member.Scalar + ", byte>(ref " + (readOnly ? (plain ? "Unsafe" : w.A("Unsafe")) + ".AsRef(in " + owner + "." + member.Name + ")" : owner + "." + member.Name) + ")";

    /// <summary>Spells the proof that a run of fixed fields is laid out contiguously on a 64-bit target.</summary>
    private static string Proof(DirectWriter w, DirectMember[] members, int first, int last, string owner, bool readOnly)
    {
        var terms = new List<string> { w.A("Unsafe") + ".SizeOf<global::System.IntPtr>() == 8" };
        for (int index = first; index < last; index++)
            terms.Add(w.A("Unsafe") + ".ByteOffset(ref " + Bytes(w, members[index], owner, readOnly) + ", ref " + Bytes(w, members[index + 1], owner, readOnly) +
                ") == " + (members[index].Raw ? w.A("Unsafe") + ".SizeOf<" + members[index].Scalar + ">()" : members[index].Width));
        return string.Join(" && ", terms);
    }

    /// <summary>Ends a run of consecutive fixed members.</summary>
    private static int RunEnd(DirectMember[] members, int first)
    {
        int last = first;
        while (last + 1 < members.Length && members[last + 1].IsFixed)
            last++;
        return last;
    }

    /// <summary>Spells the snapshot local of a variable member as the projected emitter names it.</summary>
    private static string Snapshot(DirectTypeModel model, int index)
    {
        string name = model.Members[index].Name + "Snapshot" + index.ToString(CultureInfo.InvariantCulture);
        // The presentation drops an expansion number from a purely alphabetic name whose base is otherwise unused.
        string stem = model.Members[index].Name + "Snapshot";
        string chosen = model.Members[index].Name.All(char.IsLetter) && model.Members.All(member => member.Name != stem) ? stem : name;
        // Snapshots of mirrored members are named like role locals.
        return !model.CamelSnapshots ? chosen : char.ToLowerInvariant(chosen[0]) + chosen.Substring(1);
    }

    /// <summary>Builds a root write body, with lines prefixed by a tab for one extra indentation level.</summary>
    private static List<string> WriteBody(DirectTypeModel model, DirectWriter w, bool union)
    {
        var members = model.Members;
        var body = new List<string>();
        string count = members.Length.ToString(CultureInfo.InvariantCulture);
        body.Add("var value = input;");
        if (!union)
        {
            body.Add("if (value is null)");
            body.Add("{");
            body.Add("writer.Cursor = (byte)255;");
            body.Add("writer.Advance(1);");
            body.Add("return;");
            body.Add("}");
            body.Add("");
        }
        // A memberless record is its marker alone; its Union payload writes no bytes.
        if (members.Length == 0)
        {
            if (!union)
            {
                body.Add("writer.Cursor = (byte)0;");
                body.Add("writer.Advance(1);");
            }
            return body;
        }
        if (members.All(member => member.IsFixed))
        {
            if (union)
            {
                string widths = string.Join(" + ", members.Select(member => member.Width));
                body.Add("if (checked(3 + " + widths + ") > 4096)");
                body.Add("\twriter.EnsureAdditionalCapacity(checked(" + widths + "));");
            }
            else
            {
                body.Add("writer.Cursor = (byte)" + count + ";");
                body.Add("writer.Advance(1);");
            }
            if (members.Length == 1)
            {
                // A lone fixed member is not a contiguous group; the writer stores it directly.
                body.Add("writer.Write(value." + members[0].Name + ");");
                return body;
            }
            string width = Width(members, 0, members.Length - 1);
            body.Add("if (" + Proof(w, members, 0, members.Length - 1, "value", readOnly: true) + ")");
            body.Add("\twriter.Write(ref " + Bytes(w, members[0], "value", readOnly: true, plain: true) + ", " + width + ");");
            body.Add("else");
            body.Add("{");
            body.Add("ref byte cursor = ref writer.Cursor;");
            AddFixedStores(w, members, 0, members.Length - 1, "cursor", body);
            body.Add("writer.Advance(" + width + ");");
            body.Add("}");
            return body;
        }
        // An inline object member needs no reservation, so the record writes its member count and then each member, with
        // every inline object writing its own count and members in place.
        if (model.StreamRecord)
            return StreamWriteBody(model, w, union, body);

        body.Add("ref byte cursor = ref writer.Cursor;");
        body.Add("// Fields: " + string.Join(", ", members.Select(member => member.Name)));
        if (union && members.Length == 1)
        {
            // A lone record checks itself.
            body.Add(members[0].Kind == DirectMemberKind.Text
                ? "cursor = ref writer.WriteRecordString<" + Profile + ">(ref cursor, value." + members[0].Name + ");"
                : "cursor = ref writer.WriteRecordSpan(ref cursor, value." + members[0].Name + ");");
            body.Add("writer.Cursor = ref cursor;");
            return body;
        }
        var terms = new List<string>();
        if (!union)
            terms.Add("1");
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            string snapshot = Snapshot(model, index);
            if (member.Kind == DirectMemberKind.Fixed)
                terms.Add(member.Width);
            else if (member.Kind == DirectMemberKind.Text)
            {
                body.Add("string " + snapshot + " = value." + member.Name + ";");
                terms.Add("writer.RecordBound<" + Profile + ">(" + snapshot + ")");
            }
            else
            {
                body.Add("var " + snapshot + " = " + w.A("LuminPackWriter") + ".Capture(value." + member.Name + ");");
                terms.Add(snapshot + ".Bound");
            }
        }
        body.Add("cursor = ref writer.ReserveRecord(ref cursor, checked(" + string.Join(" + ", terms) + "));");
        if (!union)
        {
            body.Add("cursor = (byte)" + count + ";");
            body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 1);");
        }
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                int last = RunEnd(members, index);
                if (last == index)
                {
                    if (member.Size == 8 || member.Raw)
                    {
                        // A raw framework struct keeps its aligned store on 64-bit targets and its checked reserved
                        // store otherwise; an eight-byte scalar uses the same split without the size test.
                        body.Add("if (" + w.A("Unsafe") + ".SizeOf<global::System.IntPtr>() == 8" +
                            (member.Raw ? " || " + w.A("Unsafe") + ".SizeOf<" + member.Scalar + ">() <= 4" : string.Empty) + ")");
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
                body.Add("\tcursor = ref " + w.A("LuminPackWriter") + ".WriteReservedBlock(ref cursor, ref " + Bytes(w, member, "value", readOnly: true) + ", " +
                    width + ");");
                body.Add("else");
                body.Add("{");
                AddFixedStores(w, members, index, last, "cursor", body);
                body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + width + ");");
                body.Add("}");
                body.Add("");
                index = last;
                continue;
            }
            body.Add("// Field: " + member.Name);
            if (member.Kind == DirectMemberKind.Text)
                body.Add("cursor = ref writer.WriteReservedRecordString<" + Profile + ">(ref cursor, " + Snapshot(model, index) + ");");
            else
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref cursor, " + Snapshot(model, index) + ");");
        }
        if (body[body.Count - 1].Length != 0)
            body.Add("");
        body.Add("writer.Cursor = ref cursor;");
        return body;
    }

    /// <summary>Builds a stream record's write body: the member count, then every member, with inline objects writing their own
    /// count and members in place.</summary>
    private static List<string> StreamWriteBody(DirectTypeModel model, DirectWriter w, bool union, List<string> body)
    {
        if (!union)
        {
            body.Add("writer.Cursor = (byte)" + model.Members.Length.ToString(CultureInfo.InvariantCulture) + ";");
            body.Add("writer.Advance(1);");
        }
        AddStreamWrite(body, model, "value", 0, w, new Dictionary<string, int>(StringComparer.Ordinal));
        ReduceInlineNames(body);
        return body;
    }

    /// <summary>Writes the members of a stream record or of one of its inline objects.</summary>
    private static void AddStreamWrite(List<string> body, DirectTypeModel model, string owner, int depth, DirectWriter w,
        Dictionary<string, int> names)
    {
        var members = model.Members;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.Kind == DirectMemberKind.Object)
            {
                // A child with strings publishes a named member write, which keeps its own framing; the parent calls it instead
                // of copying its body.
                if (member.Nested.MemberWrite)
                {
                    body.Add(w.A("LuminPackSerializer") + ".SerializeMember_" + member.Nested.Readable + "<" + Profile + ">(ref writer, " +
                        owner + "." + member.Name + ");");
                    continue;
                }
                string name = InlineName(names, "inlineValue", depth);
                body.Add("var " + name + " = " + owner + "." + member.Name + ";");
                body.Add("if (" + name + " is null)");
                body.Add("{");
                body.Add("writer.Cursor = (byte)255;");
                body.Add("writer.Advance(1);");
                body.Add("}");
                body.Add("else");
                body.Add("{");
                body.Add("writer.Cursor = (byte)" + member.Nested.Members.Length.ToString(CultureInfo.InvariantCulture) + ";");
                body.Add("writer.Advance(1);");
                AddStreamWrite(body, member.Nested, name, depth + 1, w, names);
                body.Add("}");
                // An inline object leaves a blank line before a following fixed field, as the original template does.
                if (index + 1 < members.Length && members[index + 1].IsFixed)
                    body.Add("");
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                body.Add("writer.Write(" + owner + "." + member.Name + ");");
                continue;
            }
            string width = Width(members, index, last);
            body.Add("if (" + Proof(w, members, index, last, owner, readOnly: true) + ")");
            body.Add("\twriter.Write(ref " + Bytes(w, members[index], owner, readOnly: true) + ", " + width + ");");
            body.Add("else");
            body.Add("{");
            body.Add("ref byte cursor = ref writer.Cursor;");
            AddFixedStores(w, members, index, last, "cursor", body, owner);
            body.Add("writer.Advance(" + width + ");");
            body.Add("}");
            index = last;
        }
    }

    /// <summary>Spells an inline object's local the way the template numbers its depth-suffixed role local.</summary>
    private static string InlineName(Dictionary<string, int> names, string role, int depth)
    {
        string raw = role + depth.ToString(CultureInfo.InvariantCulture);
        int count = names.TryGetValue(raw, out int seen) ? seen + 1 : 1;
        names[raw] = count;
        return count == 1 ? raw : raw + count.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Drops the depth suffix of an inline local whose purely alphabetic stem is otherwise unused, as the presentation
    /// does.</summary>
    private static void ReduceInlineNames(List<string> body)
    {
        var pattern = new System.Text.RegularExpressions.Regex(@"\b(inlineValue|inlineRead)\d+\b");
        var order = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in body)
            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(line))
                if (names.Add(match.Value))
                    order.Add(match.Value);
        // The first name that can claim a stem takes it; a later name whose stem is already taken keeps its digits.
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in order)
        {
            string stem = new string(name.TakeWhile(char.IsLetter).ToArray());
            if (stem != name && !names.Contains(stem) && claimed.Add(stem))
                replacements.Add(name, stem);
        }
        if (replacements.Count == 0)
            return;
        pattern = new System.Text.RegularExpressions.Regex(@"\b(" + string.Join("|", replacements.Keys) + @")\b");
        for (int index = 0; index < body.Count; index++)
            body[index] = pattern.Replace(body[index], match => replacements[match.Value]);
    }

    /// <summary>Turns an all-fixed Union write into the returning cursor kernel of a fixed Union case, as the fixed Union cursor
    /// projection does: the cursor is a local byte index into the buffer, advancing adds to it, a block write copies into the
    /// buffer, a lone store returns its width, and the index is published only before a capacity reservation.</summary>
    private static List<string> FixedUnionBody(DirectWriter w, List<string> original)
    {
        string destination = w.A("Unsafe") + ".Add(ref " + w.A("MemoryMarshal") + ".GetReference(writer.Buffer), unchecked((nint)(uint)(fixedUnionIndex)))";
        var body = new List<string>
        {
            "int fixedUnionIndex = initialIndex;",
            "if (unionTag.HasValue)",
            "{",
            destination + " = unionTag.GetValueOrDefault();",
            "fixedUnionIndex++;",
            "}",
            "",
        };
        const string block = "\twriter.Write(ref ";
        foreach (string line in original)
        {
            if (line.StartsWith("\twriter.EnsureAdditionalCapacity(", StringComparison.Ordinal))
            {
                body.Add("{");
                body.Add("writer.Position = fixedUnionIndex;");
                body.Add(line.Substring(1));
                body.Add("}");
                body.Add("");
            }
            else if (line.StartsWith(block, StringComparison.Ordinal))
            {
                // The block source and width are separated by the last top-level comma.
                string arguments = line.Substring(block.Length, line.Length - block.Length - 2);
                int comma = TopLevelComma(arguments);
                string source = arguments.Substring(0, comma), width = arguments.Substring(comma + 2);
                body.Add("{");
                body.Add(w.A("LuminPackMarshal") + ".CopyBlockUnaligned(ref " + destination + ", ref " + source + ", (uint)(" + width + "));");
                body.Add("fixedUnionIndex += " + width + ";");
                body.Add("}");
            }
            else if (line == "ref byte cursor = ref writer.Cursor;")
                body.Add("ref byte cursor = ref " + destination + ";");
            else if (line.StartsWith("writer.Advance(", StringComparison.Ordinal))
                body.Add("fixedUnionIndex += " + line.Substring("writer.Advance(".Length, line.Length - "writer.Advance(".Length - 2) + ";");
            else if (line.StartsWith("writer.Write(", StringComparison.Ordinal))
                body.Add("fixedUnionIndex += " + w.A("LuminPackWriter") + ".Write(ref " + destination + ", " + line.Substring("writer.Write(".Length));
            else
                body.Add(line);
        }
        if (body[body.Count - 1] == "}")
            body.Add("");
        body.Add("return fixedUnionIndex;");
        return body;
    }

    /// <summary>Finds the last comma outside parentheses and angle brackets.</summary>
    private static int TopLevelComma(string text)
    {
        int depth = 0, found = -1;
        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            if (c is '(' or '<')
                depth++;
            else if (c is ')' or '>')
                depth--;
            else if (c == ',' && depth == 0)
                found = index;
        }
        return found;
    }

    /// <summary>Reaches a mirrored type's storage through its layout mirror: the mirror view is declared after the null handling
    /// or the instance, and every member access goes through it.</summary>
    private static List<string> Mirrored(DirectTypeModel model, DirectWriter w, List<string> body, bool writes, bool union)
    {
        if (model.Mirror == null)
            return body;
        // The template already spelled these helpers through imported namespaces, so they register no alias.
        const string marshal = "LuminPackMarshal";
        // Public fields stay named directly; only auto-property storage goes through the mirror.
        string pattern = @"\b" + (writes ? "value" : "instance") + @"\.(" + string.Join("|", model.Members.Where(member => member.Mirrored)
            .Select(member => System.Text.RegularExpressions.Regex.Escape(member.Name))) + @")\b";
        var result = new List<string>(body.Count + 1);
        foreach (string line in body)
            result.Add(System.Text.RegularExpressions.Regex.Replace(line, pattern, "local.$1"));
        if (writes)
            result.Insert(union ? 1 : 8, "ref var local = ref " + marshal + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref Unsafe.AsRef(in value));");
        else
            result.Insert(result.FindIndex(line => line.StartsWith("var instance = ", StringComparison.Ordinal)) + 1,
                "var local = " + marshal + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref instance);");
        return result;
    }

    /// <summary>Builds the named member write: the original stream writes of an object nested in another record.</summary>
    private static List<string> MemberWriteBody(DirectTypeModel model, DirectWriter w)
    {
        var members = model.Members;
        var body = new List<string>
        {
            "var value = input;",
            "if (value is null)",
            "{",
            "writer.Write((byte)255);",
            "return;",
            "}",
            "",
            "writer.Write((byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ");",
        };
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.Kind == DirectMemberKind.Text)
            {
                body.Add("writer.Write<" + Profile + ">(value." + member.Name + ");");
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                body.Add("writer.Write(value." + member.Name + ");");
                continue;
            }
            string widthText = Width(members, index, last);
            body.Add("if (" + Proof(w, members, index, last, "value", readOnly: true) + ")");
            body.Add("\twriter.Write(ref " + Bytes(w, member, "value", readOnly: true) + ", " + widthText + ");");
            body.Add("else");
            body.Add("{");
            body.Add("ref byte cursor = ref writer.Cursor;");
            AddFixedStores(w, members, index, last, "cursor", body);
            body.Add("writer.Advance(" + widthText + ");");
            body.Add("}");
            body.Add("");
            index = last;
        }
        while (body[body.Count - 1].Length == 0)
            body.RemoveAt(body.Count - 1);
        return body;
    }

    /// <summary>Splits a reserved record write into the proven path: a record body reached directly when the record provably
    /// fits, and through the original reservation on excluded profiles and in the cold path.</summary>
    /// <param name="model">The covered type, whose variable members are strings.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="original">The reserved record write.</param>
    /// <param name="union">Whether the record is a Union payload without the null marker and member count.</param>
    /// <returns>The proven operation body.</returns>
    private static List<string> ProvenWriteBody(DirectTypeModel model, DirectWriter w, List<string> original, bool union)
    {
        int cursorLine = original.IndexOf("ref byte cursor = ref writer.Cursor;");
        int reserve = original.FindIndex(line => line.StartsWith("cursor = ref writer.ReserveRecord(", StringComparison.Ordinal));
        var prelude = original.GetRange(0, cursorLine);
        string comment = original[cursorLine + 1];
        var captures = original.GetRange(cursorLine + 2, reserve - cursorLine - 2);
        var record = original.GetRange(reserve + 1, original.Count - reserve - 1);
        var names = new List<string>();
        for (int index = 0; index < model.Members.Length; index++)
            if (model.Members[index].Kind == DirectMemberKind.Text)
                names.Add(Snapshot(model, index));
        string type = model.GlobalName + "?";
        string arguments = string.Join(", ", new[] { "ref writer", "value" }.Concat(names));
        string unsafeName = w.A("Unsafe"), standard = w.A("LuminPackUtf16LengthOptions"), fallback = w.A("LuminPackDefaultOptions");
        string profiles = "(typeof(" + Profile + ") == typeof(" + standard + ") || typeof(" + Profile + ") == typeof(" + fallback + ") || typeof(" + Profile +
            ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<" + standard + ">) || typeof(" + Profile +
            ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<" + fallback + ">))";
        // The complete bound in 64-bit arithmetic; a string record is its length prefix and UTF-16 payload.
        var terms = new List<string>();
        if (!union)
            terms.Add("1");
        int text = 0;
        foreach (var member in model.Members)
        {
            if (member.Kind != DirectMemberKind.Text)
            {
                terms.Add(member.Width);
                continue;
            }
            string name = names[text++];
            terms.Add("(4L + (" + name + " is null ? 0L : (long)" + name + ".Length * 2))");
        }
        string bound = "(long)(" + terms[0] + ")" + string.Concat(terms.Skip(1).Select(term => " + " + term));
        string inlineAttribute = w.MethodImpl("AggressiveInlining");

        var body = new List<string>
        {
            "if (!(" + unsafeName + ".SizeOf<global::System.IntPtr>() == 8 && " + profiles + "))",
            "{",
            "WriteChecked(ref writer, input);",
            "return;",
            "}",
            "",
            "if (input is not null)",
            "{",
            "var value = input;",
        };
        body.AddRange(captures);
        body.Add("long objectRecordBound = " + bound + ";");
        body.Add("if (writer.RecordRoom() >= objectRecordBound)");
        body.Add("{");
        body.Add("WriteRecord(" + arguments + ");");
        body.Add("return;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("WriteOriginalObject(ref writer, input);");
        body.Add(inlineAttribute);
        body.Add("static void WriteRecord(scoped ref LuminPackWriter writer, " + type + " value" + string.Concat(names.Select(name => ", string " + name)) + ")");
        body.Add("{");
        body.Add("ref byte cursor = ref writer.Cursor;");
        body.Add(comment);
        body.AddRange(record);
        body.Add("}");
        body.Add("");
        body.Add(inlineAttribute);
        body.Add("static void WriteChecked(scoped ref LuminPackWriter writer, " + type + " input)");
        body.Add("{");
        body.AddRange(prelude);
        body.Add("ref byte cursor = ref writer.Cursor;");
        body.AddRange(captures);
        body.Add(original[reserve]);
        body.Add("writer.Cursor = ref cursor;");
        body.Add("WriteRecord(" + arguments + ");");
        body.Add("}");
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void WriteOriginalObject(scoped ref LuminPackWriter writer, " + type + " input) => WriteChecked(ref writer, input);");
        return body;
    }

    /// <summary>Stores a run of fixed fields at their packed offsets from a cursor.</summary>
    private static void AddFixedStores(DirectWriter w, DirectMember[] members, int first, int last, string cursor, List<string> body)
        => AddFixedStores(w, members, first, last, cursor, body, "value");

    /// <summary>Stores a run of fixed fields of one record, which may be an inline object, at their packed offsets from a cursor.</summary>
    private static void AddFixedStores(DirectWriter w, DirectMember[] members, int first, int last, string cursor, List<string> body, string owner)
    {
        string offset = "0";
        for (int index = first; index <= last; index++)
        {
            var member = members[index];
            body.Add(member.Size == 8 || member.Raw
                ? w.A("LuminPackWriter") + ".Write(ref " + At(w, cursor, offset) + ", in " + owner + "." + member.Name + ");"
                : w.A("Unsafe") + ".WriteUnaligned(ref " + At(w, cursor, offset) + ", " + owner + "." + member.Name + ");");
            offset = Add(offset, member.Width);
        }
    }

    /// <summary>Builds a root read body.</summary>
    private static List<string> ReadBody(DirectTypeModel model, DirectWriter w, bool union)
    {
        if (model.StreamRecord)
            return StreamReadBody(model, w, union);
        var members = model.Members;
        var body = new List<string>();
        string Policy() => w.A("LuminPackInputPolicy");
        // Without the null marker, the safe-read projection checks the first lone byte read as the object's member marker.
        bool markerSeen = !union;
        if (!union)
        {
            body.Add("if (" + Policy() + ".ReadNullMarker(ref reader, " + members.Length.ToString(CultureInfo.InvariantCulture) + "))");
            body.Add("\treturn;");
        }
        body.Add("var instance = value ??= new " + model.GlobalName + "();");
        int cursors = 0, strings = 0;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                int last = RunEnd(members, index);
                if (last == index)
                {
                    if (member.Scalar == "byte" && !markerSeen)
                    {
                        markerSeen = true;
                        body.Add("instance." + member.Name + " = " + Policy() + ".ReadMarker(ref reader, " + members.Length.ToString(CultureInfo.InvariantCulture) + ");");
                    }
                    else
                        body.Add("instance." + member.Name + " = " + (member.IsBoolean ? Policy() + ".ReadBoolean(ref reader);" : "reader.Read<" + member.Scalar + ">();"));
                    continue;
                }
                int width = 0;
                var offsets = new List<int>();
                for (int item = index; item <= last; item++)
                {
                    if (members[item].IsBoolean)
                        offsets.Add(width);
                    width += members[item].Size;
                }
                string cursor = "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));
                string widthText = Width(members, index, last);
                // A runtime-sized advance is spelled as the unsigned native offset of the cursor projection.
                string advance = int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? widthText
                    : "unchecked((nint)(uint)((" + widthText + ")))";
                if (!union)
                {
                    body.Add("if (" + Proof(w, members, index, last, "instance", readOnly: false) + ")");
                    string block = Bytes(w, member, "instance", readOnly: false, plain: true);
                    if (offsets.Count == 0)
                        body.Add("\treader.Read(ref " + block + ", " + widthText + ");");
                    else if (BatchMask(offsets, width) is { } batch)
                        body.Add("\t" + Policy() + ".ReadBatch(ref reader, ref " + block + ", " + widthText + ", " + batch + ");");
                    else
                    {
                        body.Add("{");
                        body.Add("reader.Read(ref " + block + ", " + widthText + ");");
                        for (int item = index; item <= last; item++)
                            if (members[item].IsBoolean)
                                body.Add(Policy() + ".CheckBoolean(in instance." + members[item].Name + ");");
                        body.Add("}");
                    }
                    body.Add("else");
                    body.Add("{");
                }
                body.Add("ref byte " + cursor + " = ref reader.Cursor;");
                body.Add("reader.EnsureReadable(" + widthText + ");");
                body.Add("reader.Cursor = ref " + w.A("Unsafe") + ".Add(ref reader.Cursor, " + advance + ");");
                string offset = "0";
                for (int item = index; item <= last; item++)
                {
                    var field = members[item];
                    body.Add("instance." + field.Name + " = " + (field.IsBoolean ? Policy() + ".ReadBoolean(ref " + At(w, cursor, offset, plain: true) + ");"
                        : w.A("LuminPackReader") + ".Read<" + field.Scalar + ">(ref " + At(w, cursor, offset, plain: true) + ");"));
                    offset = Add(offset, field.Width);
                }
                if (!union)
                    body.Add("}");
                body.Add("");
                index = last;
                continue;
            }
            if (member.Kind == DirectMemberKind.Text && member.Mirrored)
            {
                string number = strings++ == 0 ? string.Empty : strings.ToString(CultureInfo.InvariantCulture);
                // A mirrored string is assigned only when the decoded instance differs; it follows a fixed run directly.
                if (body[body.Count - 1].Length == 0)
                    body.RemoveAt(body.Count - 1);
                body.Add("var currentString" + number + " = instance." + member.Name + ";");
                body.Add("var decodedString" + number + " = reader.Read<" + Profile + ">(currentString" + number + ");");
                body.Add("if (!object.ReferenceEquals(currentString" + number + ", decodedString" + number + "))");
                body.Add("\tinstance." + member.Name + " = decodedString" + number + "!;");
                body.Add("");
            }
            else if (member.Kind == DirectMemberKind.Text)
                body.Add("reader.Read<" + Profile + ">(ref instance." + member.Name + ");");
            else
                body.Add(member.Formatter + ".Read<" + Profile + ">(ref reader, ref instance." + member.Name + "!);");
        }
        // The presentation keeps no blank line between a mirrored string read and an unproven fixed run's cursor.
        for (int index = body.Count - 2; index > 0; index--)
            if (body[index].Length == 0 && body[index - 1].Contains(" = decodedString") && body[index + 1].StartsWith("ref byte ", StringComparison.Ordinal))
                body.RemoveAt(index);
        while (body[body.Count - 1].Length == 0)
            body.RemoveAt(body.Count - 1);
        return body;
    }

    /// <summary>Builds a stream record's read body: the null marker, then every member, with inline objects reading their own
    /// marker and members in place.</summary>
    private static List<string> StreamReadBody(DirectTypeModel model, DirectWriter w, bool union)
    {
        var members = model.Members;
        var body = new List<string>();
        string Policy() => w.A("LuminPackInputPolicy");
        if (!union)
        {
            body.Add("if (" + Policy() + ".ReadNullMarker(ref reader, " + members.Length.ToString(CultureInfo.InvariantCulture) + "))");
            body.Add("\treturn;");
        }
        body.Add("var instance = value ??= new " + model.GlobalName + "();");
        int cursors = 0;
        AddStreamRead(body, model, "instance", 0, !union, !union, w, new Dictionary<string, int>(StringComparer.Ordinal), ref cursors);
        while (body.Count != 0 && body[body.Count - 1].Length == 0)
            body.RemoveAt(body.Count - 1);
        ReduceInlineNames(body);
        return body;
    }

    /// <summary>Reads the members of a stream record or of one of its inline objects.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="model">The record read at this depth.</param>
    /// <param name="owner">The expression holding the record being filled.</param>
    /// <param name="depth">The inline nesting depth, which names the inline locals.</param>
    /// <param name="prove">Whether a contiguous fixed run is proven on a 64-bit target; a Union payload reads it without proof.</param>
    /// <param name="markerSeen">Whether the member count was already consumed, which makes a lone leading byte a plain value.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    private static void AddStreamRead(List<string> body, DirectTypeModel model, string owner, int depth, bool prove, bool markerSeen,
        DirectWriter w, Dictionary<string, int> names, ref int cursors)
    {
        var members = model.Members;
        string Policy() => w.A("LuminPackInputPolicy");
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.Kind == DirectMemberKind.Object)
            {
                string name = InlineName(names, "inlineRead", depth);
                body.Add("if (" + Policy() + ".ReadNullMarker(ref reader, " + member.Nested.Members.Length.ToString(CultureInfo.InvariantCulture) + "))");
                body.Add("\t" + owner + "." + member.Name + " = default !;");
                body.Add("else");
                body.Add("{");
                body.Add("var " + name + " = " + owner + "." + member.Name + " ?? new " + member.Nested.GlobalName + "();");
                AddStreamRead(body, member.Nested, name, depth + 1, prove, true, w, names, ref cursors);
                body.Add(owner + "." + member.Name + " = " + name + ";");
                body.Add("}");
                body.Add("");
                continue;
            }
            if (member.Kind == DirectMemberKind.Text)
            {
                body.Add("reader.Read<" + Profile + ">(ref " + owner + "." + member.Name + ");");
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                if (member.Scalar == "byte" && !markerSeen)
                {
                    markerSeen = true;
                    body.Add(owner + "." + member.Name + " = " + Policy() + ".ReadMarker(ref reader, " +
                        members.Length.ToString(CultureInfo.InvariantCulture) + ");");
                }
                else
                    body.Add(owner + "." + member.Name + " = " + (member.IsBoolean ? Policy() + ".ReadBoolean(ref reader);" :
                        "reader.Read<" + member.Scalar + ">();"));
                continue;
            }
            int width = 0;
            var offsets = new List<int>();
            for (int item = index; item <= last; item++)
            {
                if (members[item].IsBoolean)
                    offsets.Add(width);
                width += members[item].Size;
            }
            string cursor = "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));
            string widthText = Width(members, index, last);
            // A runtime-sized advance is spelled as the unsigned native offset of the cursor projection.
            string advance = int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? widthText
                : "unchecked((nint)(uint)((" + widthText + ")))";
            if (prove)
            {
                body.Add("if (" + Proof(w, members, index, last, owner, readOnly: false) + ")");
                string block = Bytes(w, member, owner, readOnly: false, plain: true);
                if (offsets.Count == 0)
                    body.Add("\treader.Read(ref " + block + ", " + widthText + ");");
                else if (BatchMask(offsets, width) is { } batch)
                    body.Add("\t" + Policy() + ".ReadBatch(ref reader, ref " + block + ", " + widthText + ", " + batch + ");");
                else
                {
                    body.Add("{");
                    body.Add("reader.Read(ref " + block + ", " + widthText + ");");
                    for (int item = index; item <= last; item++)
                        if (members[item].IsBoolean)
                            body.Add(Policy() + ".CheckBoolean(in " + owner + "." + members[item].Name + ");");
                    body.Add("}");
                }
                body.Add("else");
                body.Add("{");
            }
            body.Add("ref byte " + cursor + " = ref reader.Cursor;");
            body.Add("reader.EnsureReadable(" + widthText + ");");
            body.Add("reader.Cursor = ref " + w.A("Unsafe") + ".Add(ref reader.Cursor, " + advance + ");");
            string offset = "0";
            for (int item = index; item <= last; item++)
            {
                var field = members[item];
                body.Add(owner + "." + field.Name + " = " + (field.IsBoolean ? Policy() + ".ReadBoolean(ref " + At(w, cursor, offset, plain: true) + ");"
                    : w.A("LuminPackReader") + ".Read<" + field.Scalar + ">(ref " + At(w, cursor, offset, plain: true) + ");"));
                offset = Add(offset, field.Width);
            }
            if (prove)
                body.Add("}");
            body.Add("");
            index = last;
        }
    }

    /// <summary>Whether the Booleans of a fixed run, if any, fit one batch mask.</summary>
    internal static bool HasBatchMask(DirectMember[] members, int first, int last)
    {
        var offsets = new List<int>();
        int width = 0;
        for (int index = first; index <= last; index++)
        {
            if (members[index].IsBoolean)
                offsets.Add(width);
            width += members[index].Size;
        }
        return offsets.Count == 0 || BatchMask(offsets, width) != null;
    }

    /// <summary>Selects one four- or eight-byte window holding every Boolean of a batch and its rejection mask.</summary>
    private static string BatchMask(List<int> offsets, int length)
    {
        int low = offsets.Min(), high = offsets.Max();
        int width = length >= 8 && high - low < 8 ? 8 : length >= 4 && high - low < 4 ? 4 : 0;
        if (width == 0)
            return null;
        int window = Math.Min(low, length - width);
        ulong mask = offsets.Aggregate(0UL, (bits, value) => bits | 0xFEUL << (8 * (value - window)));
        return window.ToString(CultureInfo.InvariantCulture) + ", 0x" + mask.ToString(width == 8 ? "X16" : "X8", CultureInfo.InvariantCulture) + (width == 8 ? "UL" : "U");
    }
}
