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
        bool genericWrite = model.Version != null || model.ProfilePayload || model.InlineArray;
        // A fixed member or an inline object of fixed members needs no profile; anything else, including a string in an inline
        // object, does.
        bool genericRead = model.Version != null || model.ProfilePayload || model.InlineArray || model.InlineList ||
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
        // A payload whose write body contains an element loop advances through an isolated copy of the writer state, so a
        // failure leaves the caller's state untouched.
        if (model.ElementList)
        {
            IsolatedWrite("internal static void Write(this ref " + writer + " state, in " + type + "? value)", null,
                genericWrite ? Standard() : null);
            w.Blank();
            IsolatedWrite("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + "? value)", "where TOptions : struct, " + Options(),
                genericWrite ? "TOptions" : null);
        }
        else
        {
            w.Line(inline);
            w.Line("internal static void Write(this ref " + writer + " state, in " + type + "? value) => " + Serializer() + "." + container + "." +
                (genericWrite ? "RootWrite<" + Standard() + ">" : "RootWrite") + "(ref state, value);");
            w.Line(inline);
            w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + "? value)");
            w.Indented(() => w.Line("where TOptions : struct, " + Options() + " => " + Serializer() + "." + container + "." +
                (genericWrite ? "RootWrite<TOptions>" : "RootWrite") + "(ref state, value);"));
        }
        w.Suppressed = !reads;
        // A version-tolerant record and a type built by its selected constructor read through an isolated copy of the reader
        // state, so a failure leaves the caller's state untouched.
        if (model.Version != null || !model.IsStruct && model.ConstructorArguments != null)
        {
            // A version-tolerant record reads through an isolated copy of the reader state.
            IsolatedRead("internal static void Read(this ref " + reader + " state, ref " + type + "? value)", null,
                genericRead ? Standard() : null);
            w.Blank();
            IsolatedRead("internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + "? value)", "where TOptions : struct, " + Options(),
                genericRead ? "TOptions" : null);
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
            w.Line(Serializer() + "." + container + ".RootRead" + (profile == null ? string.Empty : "<" + profile + ">") + "(ref isolated, ref value);");
            w.Line("isolated.CopyTo(ref state);");
            w.Close();
        }

        void IsolatedWrite(string signature, string constraint, string profile)
        {
            w.Line(inline);
            w.Line(signature);
            if (constraint != null)
                w.Indented(() => w.Line(constraint));
            w.Open();
            w.Line(writer + " isolated = default;");
            w.Line("state.CopyTo(ref isolated);");
            w.Line(Serializer() + "." + container + ".RootWrite" + (profile == null ? string.Empty : "<" + profile + ">") + "(ref isolated, value);");
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
        // mixed record counts its inline objects' expanded bodies structurally, because their member runs, not their call
        // sites, decide the budget. A member write stays a call boundary whose callee keeps its own budget, so the runtime
        // sizes the entry instead, and any record that calls one keeps no inline hint.
        string WriteHint(List<string> body, bool union) => (model.StreamRecord || model.MixedRecord) &&
            !body.Any(line => line.IndexOf(".SerializeMember_", StringComparison.Ordinal) >= 0) &&
            !body.Any(line => line.StartsWith("for (", StringComparison.Ordinal))
            ? ((model.StreamRecord ? DirectCalls.Count(body) <= 16 : DirectInlining.MixedWrite(model, union)) ? inline : null)
            : (model.StreamRecord || model.MixedRecord ? null : (DirectInlining.Write(model, union, Sizes(body)) ? inline : null));

        string ReadHint(List<string> body) => model.StreamRecord || model.MixedRecord
            ? (DirectCalls.Count(body) <= 16 ? inline : null)
            : (model.ConstructorArguments != null ? (DirectInlining.ConstructedRead(model) ? inline : null)
                : (DirectInlining.Read(model, Sizes(body)) ? inline : null));

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
            write = AddCallbacks(write, model.WriteBefore, model.WriteAfter);
            Method(write, "RootWrite", genericWrite, "(scoped ref " + writer + " writer, " + type + "? input)",
                WriteHint(write, union: false));
            w.Blank();
            w.Suppressed = !reads;
            var read = Mirrored(model, w, ReadBody(model, w, union: false), writes: false, union: false);
            read = AddReadCallbacks(model, read);
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
                writeUnionBody = AddCallbacks(writeUnionBody, model.WriteBefore, model.WriteAfter);
                Method(writeUnionBody, "RootWriteUnion", genericWrite, "(scoped ref " + writer + " writer, " + type + "? input)",
                    WriteHint(writeUnionBody, union: true));
            }
            w.Blank();
            w.Suppressed = !reads;
            var readUnionBody = Mirrored(model, w, ReadBody(model, w, union: true), writes: false, union: true);
            readUnionBody = AddReadCallbacks(model, readUnionBody);
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
            // A mirrored record keeps its null handling in both bodies, so the mirror view is declared at the same position.
            var write = Mirrored(model, w, VersionWriteBody(model, w), writes: true, union: false);
            string writeHint = VersionWriteInline(model) ? inline : null;
            w.Suppressed = !writes;
            Method(write, "RootWrite", true, "(scoped ref " + writer + " writer, " + type + "? input)", writeHint);
            w.Blank();
            w.Suppressed = !reads;
            Method(VersionReadBody(model, w), "RootRead", true, "(scoped ref " + reader + " reader, scoped ref " + type + "? value)", null);
            w.Blank();
            w.Suppressed = !writes;
            Method(Mirrored(model, w, VersionWriteBody(model, w), writes: true, union: false), "RootWriteUnion", true,
                "(scoped ref " + writer + " writer, " + type + "? input)", writeHint);
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
            for (int index = 0; index < body.Count; index++)
            {
                string line = body[index];
                if (line.Length == 0)
                {
                    // A blank requested before an opening brace is a separator between two statement blocks, which the
                    // opening brace would otherwise drop.
                    if (index + 1 < body.Count && body[index + 1] == "{")
                        w.HardBlank();
                    else
                        w.Blank();
                }
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
    /// <param name="model">The record that owns the member.</param>
    /// <param name="index">The member's index.</param>
    /// <param name="camel">Whether the mixed record body camel-cases every snapshot local, as its inline object members do.</param>
    private static string Snapshot(DirectTypeModel model, int index, bool camel = false)
    {
        string name = model.Members[index].Name + "Snapshot" + index.ToString(CultureInfo.InvariantCulture);
        // The presentation drops an expansion number from a purely alphabetic name whose base is otherwise unused.
        string stem = model.Members[index].Name + "Snapshot";
        string chosen = model.Members[index].Name.All(char.IsLetter) && model.Members.All(member => member.Name != stem) ? stem : name;
        // Snapshots of mirrored members are named like role locals.
        return !(model.CamelSnapshots || camel) ? chosen : char.ToLowerInvariant(chosen[0]) + chosen.Substring(1);
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
        // Inline objects and reserving members alternate: the leading members share the header's reservation, each object
        // writes in place, and every following run reserves on its own.
        if (model.MixedRecord)
            return MixedWriteBody(model, w, union, body);

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
            string snapshot = Snapshot(model, index, camel: model.HasCallbacks);
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
                body.Add("cursor = ref writer.WriteReservedRecordString<" + Profile + ">(ref cursor, " + Snapshot(model, index, camel: model.HasCallbacks) + ");");
            else
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref cursor, " + Snapshot(model, index, camel: model.HasCallbacks) + ");");
        }
        if (body[body.Count - 1].Length != 0)
            body.Add("");
        body.Add("writer.Cursor = ref cursor;");
        return body;
    }

    /// <summary>Spells instance creation for a read: the pool rent call of a pooled type, or its parameterless constructor.</summary>
    private static string NewInstance(DirectTypeModel model) => model.Rent ?? "new " + model.GlobalName + "()";

    /// <summary>Adds the serialization callbacks around a write body: after the value local is bound, and at the end.</summary>
    private static List<string> AddCallbacks(List<string> body, string[] before, string[] after)
    {
        if (before.Length == 0 && after.Length == 0)
            return body;
        var result = new List<string>(body.Count + before.Length + after.Length);
        int anchor = body.FindIndex(line => line.StartsWith("var value = input;", StringComparison.Ordinal));
        for (int index = 0; index < body.Count; index++)
        {
            result.Add(body[index]);
            if (index == anchor)
                result.AddRange(before);
        }
        if (anchor < 0)
            result.InsertRange(0, before);
        result.AddRange(after);
        return result;
    }

    /// <summary>Adds the deserialization callbacks around a read body; a constructed read places the completion callback inside
    /// its branches itself.</summary>
    private static List<string> AddReadCallbacks(DirectTypeModel model, List<string> body)
    {
        if (!model.HasCallbacks || !model.IsStruct && model.ConstructorArguments != null)
            return body;
        var result = new List<string>(body.Count + model.ReadBefore.Length + model.ReadAfter.Length);
        result.AddRange(model.ReadBefore);
        result.AddRange(body);
        result.AddRange(model.ReadAfter);
        return result;
    }

    /// <summary>Builds a mixed record's write body: the leading members reserved with the header, each inline object in
    /// place, and every following run of reserving members written through its own reservation.</summary>
    private static List<string> MixedWriteBody(DirectTypeModel model, DirectWriter w, bool union, List<string> body)
    {
        var members = model.Members;
        int ends = 0, cursors = 1, spans = 0;
        var names = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
        // The leading members before the first inline object share the header's reservation; without any, the header is a
        // lone record of its own.
        int first = 0;
        while (first < members.Length && !IsInlineSegment(members[first]))
            first++;
        body.Add("ref byte cursor = ref writer.Cursor;");
        if (first == 0)
        {
            if (!union)
                body.Add("cursor = ref writer.WriteRecord(ref cursor, (byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ");");
        }
        else if (union && first == 1)
            AddLoneRecord(body, members[0]);
        else
            AddReservedSegment(model, w, body, 0, first - 1, header: !union);
        body.Add("writer.Cursor = ref cursor;");
        int index = first;
        while (index < members.Length)
        {
            var member = members[index];
            if (IsTextList(member))
            {
                AddInlineListWrite(body, member, "value", w, names, ref ends, ref spans);
                index++;
                if (index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            if (IsElementList(member))
            {
                AddInlineElementListWrite(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                index++;
                if (index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            if (member.Kind == DirectMemberKind.Object)
            {
                AddInlineObjectWrite(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                index++;
                if (!member.Nested.MemberWrite && index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            // A formatter-call array is a plain call beside the value members, which keeps no separating blank line.
            if (member.Kind == DirectMemberKind.Array && member.FormatterCall)
            {
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, value." + member.Name + ");");
                index++;
                continue;
            }
            int last = index;
            while (last + 1 < members.Length && !IsInlineSegment(members[last + 1]))
                last++;
            body.Add("cursor = ref writer.Cursor;");
            if (last == index)
                AddLoneRecord(body, member);
            else
                AddReservedSegment(model, w, body, index, last, header: false);
            body.Add("writer.Cursor = ref cursor;");
            index = last + 1;
        }
        ReduceInlineNames(body);
        return body;
    }

    /// <summary>Writes one inline object member: a named member operation when the child publishes one, otherwise its marker
    /// and its members in place.</summary>
    private static void AddInlineObjectWrite(List<string> body, DirectMember member, string owner, int depth, DirectWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        if (member.Nested.MemberWrite)
        {
            body.Add(w.A("LuminPackSerializer") + ".SerializeMember_" + member.Nested.Readable + "<" + Profile + ">(ref writer, " +
                owner + "." + member.Name + ");");
            return;
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
        AddStreamWrite(body, member.Nested, name, depth + 1, w, names, ref ends, ref cursors, ref spans);
        body.Add("}");
    }

    /// <summary>Writes one list member in place: a null count marker, otherwise the element count and payload. A string list
    /// writes its strings through one profiled span call; a scalar list stores the span directly.</summary>
    private static void AddInlineListWrite(List<string> body, DirectMember member, string owner, DirectWriter w,
        Dictionary<string, int> names, ref int ends, ref int spans)
    {
        string local = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + local + " is null)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(-1);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        if (member.Scalar == "string")
        {
            string span = RoleName(names, "span");
            body.Add("var " + span + " = LuminPackMarshal.GetListSpan(" + w.A("Unsafe") + ".AsRef(in " + local + "));");
            body.Add("writer.EnsureAdditionalCapacity(4);");
            body.Add("writer.Write(" + span + ".Length);");
            body.Add("writer.WriteStrings<" + Profile + ">(" + span + ");");
            body.Add("writer.CheckBuffer();");
        }
        else
            body.Add("writer.Write<" + member.Scalar + ">(LuminPackMarshal.GetListSpan(" + local + "), " + local + ".Count);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Writes one list of packable classes in place: a null count marker, otherwise the element count and a loop that
    /// expands each element's record body, skipping a null element through its own exit label.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The list member, whose <see cref="DirectMember.Nested"/> model is the element.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth the element body continues.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method; the element label is numbered before the list's.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    private static void AddInlineElementListWrite(List<string> body, DirectMember member, string owner, int depth, DirectWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        var element = member.Nested;
        string local = RoleName(names, "value");
        // The element's null exit claims the first label even though the list's null exit appears first in the text.
        string elementExit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string span = RoleName(names, "span");
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + local + " is null)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(-1);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("var " + span + " = LuminPackMarshal.GetListSpan(" + w.A("Unsafe") + ".AsRef(in " + local + "));");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(" + span + ".Length);");
        body.Add("for (int i = 0; i < " + span + ".Length; i++)");
        body.Add("{");
        body.Add("writer.CheckBuffer();");
        body.Add("{");
        string input = RoleName(names, "input");
        string value = RoleName(names, "value");
        body.Add("ref readonly " + element.GlobalName + " " + input + " = ref " + span + "[i]!;");
        body.Add("var " + value + " = " + input + ";");
        body.Add("if (" + value + " is null)");
        body.Add("{");
        body.Add("writer.Cursor = (byte)255;");
        body.Add("writer.Advance(1);");
        body.Add("goto " + elementExit + ";");
        body.Add("}");
        body.Add("");
        AddElementWrite(body, element, value, depth, w, names, ref ends, ref cursors, ref spans);
        body.Add(elementExit + ":");
        body.Add("\t;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("writer.CheckBuffer();");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Expands one sequence element's record body in the loop, without its null handling and with camel-cased
    /// snapshots, as a member expansion does.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="element">The element's model.</param>
    /// <param name="owner">The local holding the element.</param>
    /// <param name="depth">The inline nesting depth of the element body.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    private static void AddElementWrite(List<string> body, DirectTypeModel element, string owner, int depth, DirectWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        var members = element.Members;
        string count = members.Length.ToString(CultureInfo.InvariantCulture);
        if (members.All(member => member.IsFixed))
        {
            body.Add("writer.Cursor = (byte)" + count + ";");
            body.Add("writer.Advance(1);");
            if (members.Length == 1)
            {
                body.Add("writer.Write(" + owner + "." + members[0].Name + ");");
                return;
            }
            string width = Width(members, 0, members.Length - 1);
            body.Add("if (" + Proof(w, members, 0, members.Length - 1, owner, readOnly: true) + ")");
            body.Add("\twriter.Write(ref " + Bytes(w, members[0], owner, readOnly: true) + ", " + width + ");");
            body.Add("else");
            body.Add("{");
            string cursor = CursorName(ref cursors);
            body.Add("ref byte " + cursor + " = ref writer.Cursor;");
            AddFixedStores(w, members, 0, members.Length - 1, cursor, body, owner);
            body.Add("writer.Advance(" + width + ");");
            body.Add("}");
            body.Add("");
            return;
        }
        if (element.StreamRecord)
        {
            body.Add("writer.Cursor = (byte)" + count + ";");
            body.Add("writer.Advance(1);");
            AddStreamWrite(body, element, owner, depth, w, names, ref ends, ref cursors, ref spans);
            return;
        }
        // The element reserves its record as a whole; its members keep the member order of the template.
        string elementCursor = CursorName(ref cursors);
        body.Add("ref byte " + elementCursor + " = ref writer.Cursor;");
        body.Add("// Fields: " + string.Join(", ", members.Select(member => member.Name)));
        var terms = new List<string> { "1" };
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            string snapshot = Snapshot(element, index, camel: true);
            if (member.Kind == DirectMemberKind.Fixed)
                terms.Add(member.Width);
            else if (member.Kind == DirectMemberKind.Text)
            {
                body.Add("string " + snapshot + " = " + owner + "." + member.Name + ";");
                terms.Add("writer.RecordBound<" + Profile + ">(" + snapshot + ")");
            }
            else
            {
                body.Add("var " + snapshot + " = " + w.A("LuminPackWriter") + ".Capture(" + owner + "." + member.Name + ");");
                terms.Add(snapshot + ".Bound");
            }
        }
        body.Add(elementCursor + " = ref writer.ReserveRecord(ref " + elementCursor + ", checked(" + string.Join(" + ", terms) + "));");
        body.Add(elementCursor + " = (byte)" + count + ";");
        body.Add(elementCursor + " = ref " + w.A("Unsafe") + ".Add(ref " + elementCursor + ", 1);");
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
                        body.Add("if (" + w.A("Unsafe") + ".SizeOf<global::System.IntPtr>() == 8" +
                            (member.Raw ? " || " + w.A("Unsafe") + ".SizeOf<" + member.Scalar + ">() <= 4" : string.Empty) + ")");
                        body.Add("{");
                        body.Add(w.A("Unsafe") + ".WriteUnaligned<" + member.Scalar + ">(ref " + elementCursor + ", " + owner + "." + member.Name + ");");
                        body.Add(elementCursor + " = ref " + w.A("Unsafe") + ".Add(ref " + elementCursor + ", " + w.A("Unsafe") + ".SizeOf<" + member.Scalar + ">());");
                        body.Add("}");
                        body.Add("else");
                        body.Add("{");
                        body.Add(elementCursor + " = ref " + w.A("LuminPackWriter") + ".WriteReserved<" + member.Scalar + ">(ref " + elementCursor + ", " +
                            owner + "." + member.Name + ");");
                        body.Add("}");
                        body.Add("");
                    }
                    else
                    {
                        body.Add(w.A("Unsafe") + ".WriteUnaligned<" + member.Scalar + ">(ref " + elementCursor + ", " + owner + "." + member.Name + ");");
                        body.Add(elementCursor + " = ref " + w.A("Unsafe") + ".Add(ref " + elementCursor + ", " + w.A("Unsafe") + ".SizeOf<" + member.Scalar + ">());");
                    }
                    continue;
                }
                string runWidth = Width(members, index, last);
                body.Add("if (" + Proof(w, members, index, last, owner, readOnly: true) + ")");
                body.Add("\t" + elementCursor + " = ref " + w.A("LuminPackWriter") + ".WriteReservedBlock(ref " + elementCursor + ", ref " +
                    Bytes(w, member, owner, readOnly: true) + ", " + runWidth + ");");
                body.Add("else");
                body.Add("{");
                AddFixedStores(w, members, index, last, elementCursor, body, owner);
                body.Add(elementCursor + " = ref " + w.A("Unsafe") + ".Add(ref " + elementCursor + ", " + runWidth + ");");
                body.Add("}");
                body.Add("");
                index = last;
                continue;
            }
            body.Add("// Field: " + member.Name);
            if (member.Kind == DirectMemberKind.Text)
                body.Add(elementCursor + " = ref writer.WriteReservedRecordString<" + Profile + ">(ref " + elementCursor + ", " + Snapshot(element, index, camel: true) + ");");
            else
                body.Add(elementCursor + " = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref " + elementCursor + ", " + Snapshot(element, index, camel: true) + ");");
        }
        if (body[body.Count - 1].Length != 0)
            body.Add("");
        body.Add("writer.Cursor = ref " + elementCursor + ";");
    }

    /// <summary>Spells the method-wide cursor local: the first is <c>cursor</c>, the following ones carry their ordinal.</summary>
    private static string CursorName(ref int cursors)
        => "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes a lone member through the cursor without reserving first, as a one-member record does.</summary>
    private static void AddLoneRecord(List<string> body, DirectMember member)
    {
        body.Add("// Fields: " + member.Name);
        if (member.Kind == DirectMemberKind.Fixed)
            body.Add("cursor = ref writer.WriteRecord(ref cursor, value." + member.Name + ");");
        else if (member.Kind == DirectMemberKind.Text)
            body.Add("cursor = ref writer.WriteRecordString<" + Profile + ">(ref cursor, value." + member.Name + ");");
        else
            body.Add("cursor = ref writer.WriteRecordSpan(ref cursor, value." + member.Name + ");");
    }

    /// <summary>Writes a span of a mixed record through one reservation, optionally including the record header byte.</summary>
    private static void AddReservedSegment(DirectTypeModel model, DirectWriter w, List<string> body, int first, int last, bool header)
    {
        var members = model.Members;
        var names = new List<string>();
        for (int index = first; index <= last; index++)
            names.Add(members[index].Name);
        body.Add("// Fields: " + string.Join(", ", names));
        var terms = new List<string>();
        if (header)
            terms.Add("1");
        for (int index = first; index <= last; index++)
        {
            var member = members[index];
            string snapshot = Snapshot(model, index, camel: model.InlineObject);
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
        if (header)
        {
            body.Add("cursor = (byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ";");
            body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 1);");
        }
        for (int index = first; index <= last; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                int end = RunEnd(members, index);
                if (end > last)
                    end = last;
                if (end == index)
                {
                    if (member.Size == 8 || member.Raw)
                    {
                        // A raw framework struct keeps its aligned store on 64-bit targets and its checked reserved store
                        // otherwise; an eight-byte scalar uses the same split without the size test.
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
                string width = Width(members, index, end);
                body.Add("if (" + Proof(w, members, index, end, "value", readOnly: true) + ")");
                body.Add("\tcursor = ref " + w.A("LuminPackWriter") + ".WriteReservedBlock(ref cursor, ref " + Bytes(w, member, "value", readOnly: true) + ", " +
                    width + ");");
                body.Add("else");
                body.Add("{");
                AddFixedStores(w, members, index, end, "cursor", body);
                body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + width + ");");
                body.Add("}");
                body.Add("");
                index = end;
                continue;
            }
            body.Add("// Field: " + member.Name);
            if (member.Kind == DirectMemberKind.Text)
                body.Add("cursor = ref writer.WriteReservedRecordString<" + Profile + ">(ref cursor, " + Snapshot(model, index, camel: model.InlineObject) + ");");
            else
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref cursor, " + Snapshot(model, index, camel: model.InlineObject) + ");");
        }
        if (body[body.Count - 1].Length != 0)
            body.Add("");
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
        int ends = 0, cursors = 0, spans = 0;
        AddStreamWrite(body, model, "value", 0, w, new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 }, ref ends, ref cursors, ref spans);
        ReduceInlineNames(body);
        return body;
    }

    /// <summary>Writes the members of a stream record or of one of its inline objects.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="model">The record written at this depth.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth, which names the inline locals.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method; the root's own value local holds the
    /// first occurrence of the list alias name.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method; a mixed record's own cursor holds the first
    /// occurrence.</param>
    /// <param name="spans">The string list span counter shared by the whole method.</param>
    private static void AddStreamWrite(List<string> body, DirectTypeModel model, string owner, int depth, DirectWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        var members = model.Members;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            // A string inside an inline object writes with the operation's profile, as the old pipeline's inline object
            // emits it: writer.Write<TLuminProfile>(inlineValue.Name); (ProbeMixed.O1).
            if (member.Kind == DirectMemberKind.Text)
            {
                body.Add("writer.Write<" + Profile + ">(" + owner + "." + member.Name + ");");
                continue;
            }
            if (member.Kind == DirectMemberKind.Object)
            {
                AddInlineObjectWrite(body, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                // An inline object leaves a blank line before a following field unless that field opens another inline object;
                // a named member write stays a plain call and keeps no blank, as the old body's continue does.
                if (!member.Nested.MemberWrite && index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                    body.Add("");
                continue;
            }
            // An array member writes through its generated formatter, which keeps its own framing and profile.
            if (member.Kind == DirectMemberKind.Array)
            {
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, " + owner + "." + member.Name + ");");
                continue;
            }
            // A list inside a stream record or an inline object expands its generated null header and payload in place, whose
            // failure exits through a method-wide label. A string list separates itself from the member that follows it, while
            // a scalar list keeps the presentation's own blank rule.
            if (member.Kind == DirectMemberKind.List)
            {
                if (member.Scalar == "string")
                {
                    AddInlineListWrite(body, member, owner, w, names, ref ends, ref spans);
                    if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                        body.Add("");
                }
                else if (member.Nested != null)
                {
                    AddInlineElementListWrite(body, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                    if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                        body.Add("");
                }
                else
                {
                    string local = RoleName(names, "value");
                    string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
                    ends++;
                    body.Add("{");
                    body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
                    body.Add("if (" + local + " is null)");
                    body.Add("{");
                    body.Add("writer.EnsureAdditionalCapacity(4);");
                    body.Add("writer.Write(-1);");
                    body.Add("goto " + exit + ";");
                    body.Add("}");
                    body.Add("");
                    body.Add("writer.Write<" + member.Scalar + ">(LuminPackMarshal.GetListSpan(" + local + "), " + local + ".Count);");
                    body.Add(exit + ":");
                    body.Add("\t;");
                    body.Add("}");
                    if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                        body.Add("");
                }
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
            string cursor = "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));
            body.Add("ref byte " + cursor + " = ref writer.Cursor;");
            AddFixedStores(w, members, index, last, cursor, body, owner);
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

    /// <summary>Spells a role local without a depth suffix, as the expanded list write names the field alias it holds.</summary>
    private static string RoleName(Dictionary<string, int> names, string role)
    {
        int count = names.TryGetValue(role, out int seen) ? seen + 1 : 1;
        names[role] = count;
        return count == 1 ? role : role + count.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a member's write opens with a local declaration, which the presentation keeps directly under the
    /// preceding block instead of separating it with a blank line.</summary>
    private static bool StartsInlineObject(DirectMember member) => member.Kind == DirectMemberKind.Object && !member.Nested.MemberWrite;

    /// <summary>Whether a member is a list of strings, which writes its payload in place instead of entering a reservation.</summary>
    private static bool IsTextList(DirectMember member) => member.Kind == DirectMemberKind.List && member.Scalar == "string";

    /// <summary>Whether a member is a list whose elements expand their record body in place.</summary>
    private static bool IsElementList(DirectMember member) => member.Kind == DirectMemberKind.List && member.Nested != null;

    /// <summary>Whether a member is written in its own block rather than inside the surrounding reservation.</summary>
    private static bool IsInlineSegment(DirectMember member) => member.Kind == DirectMemberKind.Object || IsTextList(member) ||
        IsElementList(member) || member.Kind == DirectMemberKind.Array && member.FormatterCall;

    /// <summary>Whether a member opens a block in place, whose following member starts without a separating blank line.</summary>
    private static bool IsInlineBlock(DirectMember member) =>
        member.Kind == DirectMemberKind.Object && !member.Nested.MemberWrite || IsTextList(member) || IsElementList(member);

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
        // A pool mirror only declares the layout for reuse; public fields keep their direct access.
        if (model.Mirror == null || !model.Members.Any(member => member.Mirrored))
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
        {
            // A constructed read declares its mirror views itself, so it has no instance local to attach one to.
            int anchor = result.FindIndex(line => line.StartsWith("var instance = ", StringComparison.Ordinal));
            if (anchor >= 0)
                result.Insert(anchor + 1, "var local = " + marshal + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref instance);");
        }
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
                names.Add(Snapshot(model, index, camel: model.HasCallbacks));
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

    /// <summary>Builds the read body of a type whose selected constructor takes parameters: temporary locals hold the decoded
    /// values, an existing instance reads in place, and a fresh one is constructed with its arguments and remaining fields.</summary>
    private static List<string> ConstructedReadBody(DirectTypeModel model, DirectWriter w, bool union)
    {
        var members = model.Members;
        string policy = w.A("LuminPackInputPolicy");
        var body = new List<string> { "// Declare locals to hold the field values." };
        // The constructed read declares its temporaries in declaration order, while the wire order drives the decodes.
        var declared = model.DeclaredOrder == null ? members : model.DeclaredOrder
            .Select(name => System.Array.Find(members, member => member.Name == name)).Where(member => member != null).ToArray();
        foreach (var member in declared)
            body.Add(TempType(member) + " " + Camel(member.Name) + "Temp = default !;");
        if (!union)
        {
            body.Add("if (" + policy + ".ReadNullMarker(ref reader, " + members.Length.ToString(CultureInfo.InvariantCulture) + "))");
            body.Add("{");
            body.Add("return;");
            body.Add("}");
            body.Add("");
        }
        body.Add("if (value is not null)");
        body.Add("{");
        body.Add("ReadExisting(ref reader, value);");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void ReadExisting(ref " + w.A("LuminPackReader") + " reader2, " + model.GlobalName + "? value2)");
        body.Add("{");
        if (model.Mirror != null)
            body.Add("ref var reuseLocal = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value2!);");
        for (int index = 0; index < members.Length; index++)
            AddConstructedReadExisting(body, members[index], index + 1 < members.Length);
        foreach (string call in model.ReadAfter)
            body.Add(call.Replace("value?.", "value2?."));
        body.Add("}");
        body.Add("");
        foreach (var member in members)
            body.Add(Camel(member.Name) + "Temp = " + FixedOrTextRead(member, "reader") + ";");
        body.Add("// Construct the object and populate its members.");
        string arguments = string.Join(", ", model.ConstructorArguments.Select(name =>
            name == null ? "default" : Camel(name) + "Temp!"));
        string construction = "value = new " + model.GlobalName + "(" + arguments + ")";
        var initialized = members.Where(member => !member.Mirrored &&
            (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
        if (initialized.Length == 0)
            body.Add(construction + ";");
        else
        {
            body.Add(construction);
            body.Add("{");
            foreach (var member in initialized)
                body.Add(member.Name + " = " + Camel(member.Name) + "Temp!,");
            body.Add("};");
        }
        var mirrored = model.Mirror == null ? Array.Empty<DirectMember>() : members.Where(member => member.Mirrored &&
            (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
        if (mirrored.Length != 0)
        {
            body.Add("// Assign private fields.");
            body.Add("ref var local = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
            foreach (var member in mirrored)
                body.Add("local." + member.Name + " = " + Camel(member.Name) + "Temp!;");
        }
        body.AddRange(model.ReadAfter);
        if (model.ReadBefore.Length != 0)
            body.InsertRange(0, model.ReadBefore);
        return body;
    }

    /// <summary>Reads one member in place for the constructed read's existing-instance path.</summary>
    private static void AddConstructedReadExisting(List<string> body, DirectMember member, bool hasNext)
    {
        string owner = member.Mirrored ? "reuseLocal" : "value2!";
        if (member.Kind == DirectMemberKind.Text && member.Mirrored)
        {
            body.Add("var currentString = " + owner + "." + member.Name + ";");
            body.Add("var decodedString = reader2.Read<" + Profile + ">(currentString);");
            body.Add("if (!object.ReferenceEquals(currentString, decodedString))");
            body.Add("\t" + owner + "." + member.Name + " = decodedString!;");
            if (hasNext)
                body.Add("");
            return;
        }
        if (member.Kind == DirectMemberKind.Text)
        {
            body.Add("reader2.Read<" + Profile + ">(ref " + owner + "." + member.Name + ");");
            return;
        }
        body.Add(owner + "." + member.Name + " = " + FixedOrTextRead(member, "reader2") + ";");
    }

    /// <summary>Spells a fixed or string member's read from the given reader expression.</summary>
    private static string FixedOrTextRead(DirectMember member, string reader) =>
        member.Kind == DirectMemberKind.Text ? reader + ".Read<" + Profile + ">((string? )null)!" :
        member.IsBoolean ? "LuminPackInputPolicy.ReadBoolean(ref " + reader + ")" :
        reader + ".Read<" + member.Scalar + ">()";

    /// <summary>Spells the temporary local type of one member, as the constructed read declares it.</summary>
    private static string TempType(DirectMember member) => member.Kind == DirectMemberKind.Text ? "string?" : member.TypeName;

    /// <summary>Spells a member's temporary local with the leading letter in lower case.</summary>
    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

    /// <summary>Builds a root read body.</summary>
    private static List<string> ReadBody(DirectTypeModel model, DirectWriter w, bool union)
    {
        // A selected constructor builds the instance from temporary locals; a raw struct's arguments only reach its JSON
        // read, so the binary body keeps the struct paths.
        if (!model.IsStruct && model.ConstructorArguments != null)
            return ConstructedReadBody(model, w, union);
        // A mixed record reads with the stream protocol: the inline objects keep their markers, so the reserved write
        // segments change nothing about the order and shape of the reads.
        if (model.StreamRecord || model.MixedRecord)
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
        body.Add("var instance = value ??= " + NewInstance(model) + ";");
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
        body.Add("var instance = value ??= " + NewInstance(model) + ";");
        int cursors = 0, strings = 0;
        AddStreamRead(body, model, "instance", 0, !union, !union, w, new Dictionary<string, int>(StringComparer.Ordinal), ref cursors, ref strings);
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
    /// <param name="strings">The mirrored string local counter shared by the whole method.</param>
    private static void AddStreamRead(List<string> body, DirectTypeModel model, string owner, int depth, bool prove, bool markerSeen,
        DirectWriter w, Dictionary<string, int> names, ref int cursors, ref int strings)
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
                body.Add("var " + name + " = " + owner + "." + member.Name + " ?? " + NewInstance(member.Nested) + ";");
                AddStreamRead(body, member.Nested, name, depth + 1, prove, markerSeen, w, names, ref cursors, ref strings);
                body.Add(owner + "." + member.Name + " = " + name + ";");
                body.Add("}");
                // A following unproven fixed run opens with its own cursor local, which the presentation keeps directly
                // under the block instead of separating it with a blank line; a proven run opens with its proof. The parent's
                // assignment after the last member keeps the blank, so it is added even without a next member.
                bool opensCursor = !prove && index + 1 < members.Length && members[index + 1].IsFixed && RunEnd(members, index + 1) > index + 1;
                if (!opensCursor)
                    body.Add("");
                continue;
            }
            if (member.Kind == DirectMemberKind.Text)
            {
                if (member.Mirrored)
                {
                    // A mirrored string is assigned only when the decoded instance differs, so its storage keeps the old one.
                    string number = strings++ == 0 ? string.Empty : strings.ToString(CultureInfo.InvariantCulture);
                    body.Add("var currentString" + number + " = " + owner + "." + member.Name + ";");
                    body.Add("var decodedString" + number + " = reader.Read<" + Profile + ">(currentString" + number + ");");
                    body.Add("if (!object.ReferenceEquals(currentString" + number + ", decodedString" + number + "))");
                    body.Add("\t" + owner + "." + member.Name + " = decodedString" + number + "!;");
                    body.Add("");
                    continue;
                }
                body.Add("reader.Read<" + Profile + ">(ref " + owner + "." + member.Name + ");");
                continue;
            }
            // An array or list member reads through its generated formatter, which keeps its own framing and profile.
            if (member.Kind is DirectMemberKind.Array or DirectMemberKind.List)
            {
                body.Add(member.Formatter + ".Read<" + Profile + ">(ref reader, ref " + owner + "." + member.Name + "!);");
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
