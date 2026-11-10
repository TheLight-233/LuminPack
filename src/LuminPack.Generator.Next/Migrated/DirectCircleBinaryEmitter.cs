using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Writes a reference-tracked type's binary roots, entries and extensions in their final published form.</summary>
/// <remarks>A reference-tracked record is <c>[slot count][one width byte per slot][VarInt id][members]</c>. The write reserves the
/// table and the reserve-able members once, then frames each reference member. The read compares the table as constant words and
/// fills the object in place, while reference members below the root expand their own read body once.</remarks>
internal static partial class DirectCircleBinaryEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the complete binary file body.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls; the members of the others are left out.</param>
    internal static void Emit(DirectTypeModel model, DirectWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        const string writer = "LuminPackWriter", reader = "LuminPackReader";
        string Options() => w.A("ILuminPackOptions");
        string Serializer() => w.A("LuminPackSerializer");
        string Standard() => w.A("LuminPackUtf16LengthOptions");
        string Constraint() => "where TLuminProfile : struct, " + Options();
        string inline = w.MethodImpl("AggressiveInlining");
        string suffix = model.UniqueEntries ? "_" + readable : string.Empty;
        string polymorphic = "[" + w.Attribute("MethodImpl") + "(MethodImplOptions.AggressiveInlining)]";

        var write = WriteBody(model, w);
        var readChildren = new List<DirectTypeModel>();
        var read = ReadBody(model, w, union: false, readChildren);
        var readUnion = ReadBody(model, w, union: true, readChildren);
        bool genericWrite = HasProfile(write);
        bool genericRead = HasProfile(read);

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Suppressed = !writes;
        if (genericWrite)
        {
            SerializeStandard();
            SerializeProfiled();
            w.Blank();
        }
        else
            SerializeStandard();
        w.Suppressed = false;
        Roots();
        w.Blank();
        w.Suppressed = !writes;
        if (!genericWrite)
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
        w.Indented(() => w.Line(Constraint() + " => " + container + "." + (genericRead ? "RootRead<" + Profile + ">" : "RootRead") + "(ref reader, ref value);"));
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
        w.Indented(() => w.Line(Constraint() + " => " + (genericWrite ? container + ".RootWriteUnion<" + Profile + ">(ref writer, input);" : "SerializeUnion(ref writer, input);")));
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeUnion" + suffix + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) => " +
            (genericRead ? "DeserializeUnion" + suffix + "<" + Standard() + ">(ref reader, ref value);" : container + ".RootReadUnion(ref reader, ref value);"));
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeUnion" + suffix + "<" + Profile + ">(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(Constraint() + " => " + (genericRead ? container + ".RootReadUnion<" + Profile + ">(ref reader, ref value);" : "DeserializeUnion" + suffix + "(ref reader, ref value);")));
        w.Suppressed = false;
        // An expanded member operation declares its own layout mirror in this file even when its body reaches public
        // fields directly, as the original expansion keeps the child's support declarations.
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        string prefix = model.Mirror == null ? null : model.Mirror.Substring(0, "LuminLayout_".Length + 64 + 1);
        foreach (var child in readChildren)
            if (child.Mirror != null && child.Mirror != model.Mirror && emitted.Add(child.Mirror))
                Mirror(prefix + child.Mirror.Substring(prefix.Length), child.MirrorFields, child.MirrorOffsets, child.LayoutAttribute);
        if (model.Mirror != null)
            Mirror(model.Mirror, model.MirrorFields, model.MirrorOffsets, model.LayoutAttribute);
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write(this ref " + writer + " state, in " + type + "? value) => " + Serializer() + "." + container + ".RootWrite" +
            (genericWrite ? "<" + Standard() + ">" : string.Empty) + "(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + Options() + " => " + Serializer() + "." + container + ".RootWrite" +
            (genericWrite ? "<TOptions>" : string.Empty) + "(ref state, value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read(this ref " + reader + " state, ref " + type + "? value) => " + Serializer() + "." + container + ".RootRead" +
            (genericRead ? "<" + Standard() + ">" : string.Empty) + "(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + Options() + " => " + Serializer() + "." + container + ".RootRead" +
            (genericRead ? "<TOptions>" : string.Empty) + "(ref state, ref value);"));
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
        w.Line("public static void WritePolymorphismValue(ref this " + writer + " writer, scoped in " + type + "? input) => " + Serializer() +
            ".SerializeUnion(ref writer, input);");
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(polymorphic);
        w.Line("public static void ReadPolymorphismValue(ref this " + reader + " reader, scoped ref " + type + "? value) => " + Serializer() +
            ".DeserializeUnion" + suffix + "(ref reader, ref value);");
        w.Suppressed = false;
        w.Close();
        w.Close();
        w.Blank();

        void Mirror(string name, string[] fields, int[] offsets, string layoutAttribute)
        {
            w.Blank();
            w.Line(w.Preserve);
            if (layoutAttribute != null)
                w.Line(layoutAttribute);
            w.Line("private class " + name);
            w.Open();
            DirectBinaryEmitter.MirrorFields(w, fields, offsets);
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
            w.Indented(() => w.Line(Constraint() + " => " + container + "." + (genericWrite ? "RootWrite<" + Profile + ">" : "RootWrite") + "(ref writer, input);"));
        }

        void Roots()
        {
            w.Line("internal static partial class " + container);
            w.Open();
            w.Suppressed = !writes;
            Method(write, "RootWrite", genericWrite, "(scoped ref " + writer + " writer, " + type + "? input)", genericWrite ? null : inline);
            w.Blank();
            w.Suppressed = !reads;
            Method(read, "RootRead", genericRead, "(scoped ref " + reader + " reader, scoped ref " + type + "? value)", null);
            w.Blank();
            w.Suppressed = !writes;
            Method(write, "RootWriteUnion", genericWrite, "(scoped ref " + writer + " writer, " + type + "? input)", genericWrite ? null : inline);
            w.Blank();
            w.Suppressed = !reads;
            Method(readUnion, "RootReadUnion", genericRead, "(scoped ref " + reader + " reader, scoped ref " + type + "? value)",
                model.ReferencePayload ? w.MethodImpl("NoInlining") : null);
            w.Suppressed = false;
            w.Close();
        }

        void Method(List<string> body, string name, bool generic, string parameters, string attribute)
        {
            w.Line(w.Preserve);
            if (attribute != null)
                w.Line(attribute);
            w.Line("internal static void " + name + (generic ? "<" + Profile + ">" : string.Empty) + parameters);
            if (generic)
                w.Indented(() => w.Line(Constraint()));
            w.Open();
            for (int index = 0; index < body.Count; index++)
            {
                string line = body[index];
                if (line.Length == 0)
                {
                    if (index + 1 < body.Count && body[index + 1] == "{")
                        w.HardBlank();
                    else
                        w.Blank();
                }
                else if (line == "}")
                    w.Close();
                else if (line == "{")
                    w.Open();
                else if (line[0] == '\t')
                    w.Indented(() => w.Line(line.Substring(1)));
                else
                    w.Line(line);
            }
            w.Close();
        }
    }

    /// <summary>Whether a generated body needs the operation's profile.</summary>
    private static bool HasProfile(List<string> body) => body.Any(line => line.IndexOf(Profile, StringComparison.Ordinal) >= 0);

    /// <summary>The record's slot table: the slot count, then each slot width or 255 for a framed payload.</summary>
    internal static byte[] Table(DirectTypeModel model)
    {
        var table = new byte[model.Members.Length + 1];
        table[0] = (byte)model.Members.Length;
        for (int index = 0; index < model.Members.Length; index++)
            table[index + 1] = model.Members[index].IsFixed ? (byte)model.Members[index].Size : (byte)255;
        return table;
    }

    /// <summary>Builds the write body, which is the same for the payload and Union entries.</summary>
    private static List<string> WriteBody(DirectTypeModel model, DirectWriter w)
    {
        var members = model.Members;
        var body = new List<string>();
        if (model.CircleGuarded)
        {
            body.Add(w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
            body.Add("writer.CheckBuffer();");
        }
        body.Add("var value = input;");
        body.AddRange(model.WriteBefore);
        body.Add("if (value is null)");
        body.Add("{");
        body.Add("writer.Cursor = " + w.A("LuminPackCode") + ".NullObject;");
        body.Add("writer.Advance(1);");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        if (members.Any(member => member.Mirrored))
            body.Add("ref var local = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref " + w.A("Unsafe") + ".AsRef(in value));");
        body.Add("int reference = writer.OptionState.TrackReference(value);");
        body.Add("if (reference < 0)");
        body.Add("{");
        body.Add("writer.WriteObjectReferenceId((uint)~reference);");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add("ref byte cursor = ref writer.Cursor;");
        var table = Table(model);
        var snapshots = new string[members.Length];
        var terms = new List<string> { (table.Length + 5).ToString(CultureInfo.InvariantCulture) };
        int lastReserved = -1;
        int lengthSlot = 0;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                terms.Add(member.Size.ToString(CultureInfo.InvariantCulture));
                lastReserved = index;
                continue;
            }
            if (member.Kind != DirectMemberKind.Text)
                break;
            string snapshot = Camel(member.Name) + "Snapshot";
            snapshots[index] = snapshot;
            body.Add("string " + snapshot + " = " + Access(member) + ";");
            terms.Add("4 + writer.RecordBound<" + Profile + ">(" + snapshot + ")");
            lastReserved = index;
        }
        body.Add("cursor = ref writer.ReserveRecord(ref cursor, checked(" + string.Join(" + ", terms) + "));");
        AppendTableStores(w, body, table);
        body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + table.Length.ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("cursor = ref " + w.A("LuminPackWriter") + ".StoreVarInt(ref cursor, (uint)reference);");
        for (int index = 0; index <= lastReserved; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                body.Add(w.A("Unsafe") + ".WriteUnaligned<" + member.Scalar + ">(ref cursor, " + Access(member) + ");");
                body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + w.A("Unsafe") + ".SizeOf<" + member.Scalar + ">());");
                continue;
            }
            string length = lengthSlot == 0 ? "length" : "length" + lengthSlot.ToString(CultureInfo.InvariantCulture);
            lengthSlot++;
            body.Add("// Field: " + member.Name);
            body.Add("ref byte " + length + " = ref cursor;");
            body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 4);");
            body.Add("cursor = ref writer.WriteReservedRecordString<" + Profile + ">(ref cursor, " + snapshots[index] + ");");
            body.Add(w.A("Unsafe") + ".WriteUnaligned(ref " + length + ", (int)" + w.A("Unsafe") + ".ByteOffset(ref " + w.A("Unsafe") + ".Add(ref " +
                length + ", 4), ref cursor));");
        }
        // A reserved string member closes its group with a separating line, as the original record writer does.
        if (lastReserved >= 0 && members.Take(lastReserved + 1).Any(member => member.Kind == DirectMemberKind.Text))
            body.Add("");
        body.Add("writer.Cursor = ref cursor;");
        for (int index = lastReserved + 1; index < members.Length; index++)
        {
            var member = members[index];
            string length = Camel(member.Name) + "Length";
            body.Add("int " + length + " = writer.ReserveVersionLength();");
            body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, " + Access(member) + ");");
            body.Add("writer.CompleteVersionLength(" + length + ");");
            body.Add("");
            body.Add("cursor = ref writer.Cursor;");
            body.Add("writer.Cursor = ref cursor;");
        }
        body.AddRange(model.WriteAfter);
        return body;
    }

    /// <summary>Stores the constant slot table at the cursor, as eight-byte or four-byte words.</summary>
    private static void AppendTableStores(DirectWriter w, List<string> body, byte[] table)
    {
        if (table.Length < 8)
        {
            foreach (string line in StoreLines(w, table, "cursor", 4))
                body.Add(line);
            return;
        }
        string memory = w.A("Unsafe");
        body.Add("if (" + memory + ".SizeOf<nint>() == 8)");
        body.Add("{");
        foreach (string line in StoreLines(w, table, "cursor", 8))
            body.Add(line);
        body.Add("}");
        body.Add("else");
        body.Add("{");
        foreach (string line in StoreLines(w, table, "cursor", 4))
            body.Add(line);
        body.Add("}");
    }

    private static IEnumerable<string> StoreLines(DirectWriter w, byte[] bytes, string target, int widest)
    {
        string memory = w.A("Unsafe");
        foreach (var word in ReservedRecordEmitter.Words(bytes, widest))
        {
            string location = word.Offset == 0 ? "ref " + target : "ref " + memory + ".Add(ref " + target + ", " + word.Offset.ToString(CultureInfo.InvariantCulture) + ")";
            yield return memory + ".WriteUnaligned(" + location + ", (" + ReservedRecordEmitter.WordType(word.Width) + ")0x" +
                word.Value.ToString("X", CultureInfo.InvariantCulture) + (word.Width == 8 ? "UL" : "U") + ");";
        }
    }

    /// <summary>Builds the same-table read body, expanding each reference member once below the Union root.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="union">Whether the Union read is built, which expands the root's own type once.</param>
    /// <param name="expanded">Collects the member operations whose bodies were expanded, in expansion order.</param>
    private static List<string> ReadBody(DirectTypeModel model, DirectWriter w, bool union, List<DirectTypeModel> expanded)
    {
        var state = new ReadState { W = w, Names = new NamePool("value"), Expanded = expanded };
        var body = new List<string>();
        if (!union)
            state.Stack.Add(model.GlobalName);
        BuildRead(state, model, body, "value", "value", top: true);
        return body;
    }

    /// <summary>The shared naming, cycle and writer state of one read method.</summary>
    private sealed class ReadState
    {
        internal DirectWriter W;
        internal NamePool Names;
        internal List<DirectTypeModel> Expanded;
        internal readonly HashSet<string> Stack = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <param name="state">The shared read state.</param>
    /// <param name="model">The record read at this position.</param>
    /// <param name="body">The statements receiving the read.</param>
    /// <param name="target">The assignment target, which has no null-forgiving suffix.</param>
    /// <param name="access">The readable spelling of the target, which a nullable local suffixes with <c>!</c>.</param>
    /// <param name="top">Whether this is the method's own body, whose exits return instead of jumping to an inline label.</param>
    private static void BuildRead(ReadState state, DirectTypeModel model, List<string> body, string target, string access, bool top)
    {
        var w = state.W;
        var names = state.Names;
        var members = model.Members;
        string type = model.GlobalName;
        string Exit() => top ? "return;" : "\u0003GOTO\u0003";
        string Null(string tail) => access.EndsWith("!", StringComparison.Ordinal) ? access + tail : access + " " + tail;
        if (top && model.CircleGuarded)
            body.Add(w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
        foreach (string callback in model.ReadBefore)
            body.Add(top ? callback : Rewrite(callback, access, null));

        // The same-table path fills the instance in place.
        var table = Table(model);
        string head = names.Next("head"), same = names.Next("sameTable"), id = names.Next("id"), instance = names.Next("instance");
        body.Add("ref byte " + head + " = ref reader.PeekInput(" + table.Length.ToString(CultureInfo.InvariantCulture) + ", out bool " + same + ");");
        string memory = w.A("Unsafe");
        string match = table.Length < 8 ? Difference(w, table, head, 4) : "(" + memory + ".SizeOf<nint>() == 8 ? " + Difference(w, table, head, 8) +
            " : " + Difference(w, table, head, 4) + ")";
        body.Add("if (" + same + " && " + match + " == 0)");
        body.Add("{");
        body.Add("reader.EnsureReadable(" + table.Length.ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, " + table.Length.ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("uint " + id + " = reader.ReadVarIntUInt32();");
        body.Add("if (" + Null("is null") + " || !reader.OptionState.ClaimReference(" + id + ", " + access + "))");
        body.Add("{");
        body.Add(target + " = new " + type + "();");
        body.Add("reader.OptionState.AddObjectReference(" + id + ", " + access + ");");
        body.Add("}");
        body.Add("");
        body.Add("var " + instance + " = " + access + ";");
        string local = null;
        if (members.Any(member => member.Mirrored))
        {
            local = names.Next("local");
            body.Add("var " + local + " = LuminPackMarshal.As<" + type + ", " + model.Mirror + ">(ref " + instance + ");");
        }
        bool afterPayload = false;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            string storage = (member.Mirrored ? local : instance) + "." + member.Name;
            if (member.IsFixed)
            {
                if (afterPayload)
                {
                    body.Add("");
                    afterPayload = false;
                }
                body.Add(storage + " = reader.Read<" + member.Scalar + ">();");
                continue;
            }
            string end = names.Next("end");
            body.Add("int " + end + " = reader.ReadVersionPayloadLength();");
            body.Add(end + " += reader.Position;");
            if (member.Kind == DirectMemberKind.Text)
            {
                if (member.Mirrored)
                {
                    string current = names.Next("currentString"), decoded = names.Next("decodedString");
                    body.Add("var " + current + " = " + storage + ";");
                    body.Add("var " + decoded + " = reader.Read<" + Profile + ">(" + current + ");");
                    body.Add("if (!object.ReferenceEquals(" + current + ", " + decoded + "))");
                    body.Add("\t" + storage + " = " + decoded + "!;");
                    body.Add("");
                }
                else
                    body.Add("reader.Read<" + Profile + ">(ref " + storage + ");");
            }
            else
                AppendObjectRead(state, model, member, body, storage, refLocal: true);
            body.Add(w.A("LuminPackReader") + ".ValidateVersionFieldEnd(reader.Position, " + end + ");");
            afterPayload = true;
        }
        if (afterPayload)
            body.Add("");
        foreach (string callback in model.ReadAfter)
            body.Add(Rewrite(callback, instance, instance));
        body.Add(Exit());
        body.Add("}");
        body.Add("");

        // The fallback reads each slot into a temporary and assigns the object once every slot is consumed.
        body.Add("// Declare locals to hold the field values.");
        var temps = new string[members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            temps[index] = names.Next(Camel(member.Name) + "Temp");
            body.Add((member.IsFixed ? member.Scalar : member.TypeName + "?") + " " + temps[index] + " = default !;");
        }
        string count = names.Next("count");
        body.Add("if (!reader.TryReadObjectHead(out var " + count + "))");
        body.Add("{");
        body.Add(target + " = default;");
        body.Add(Exit());
        body.Add("}");
        body.Add("");
        body.Add("if (" + count + " is " + w.A("LuminPackCode") + ".ReferenceId)");
        body.Add("{");
        body.Add(target + " = " + w.A("LuminPackInputPolicy") + ".ReadReference<" + type + ">(reader.OptionState.GetObjectReference(reader.ReadVarIntUInt32()));");
        body.Add(Exit());
        body.Add("}");
        body.Add("");
        body.Add("reader.EnsureReadable(" + count + ");");
        string tableName = names.Next("table");
        body.Add("ref byte " + tableName + " = ref reader.PeekInput(" + count + ", out _);");
        var widths = new string[members.Length];
        for (int order = 0; order < members.Length; order++)
        {
            widths[order] = names.Next("width" + order.ToString(CultureInfo.InvariantCulture));
            body.Add("int " + widths[order] + " = " + order.ToString(CultureInfo.InvariantCulture) + " < " + count + " ? " + memory + ".Add(ref " +
                tableName + ", " + order.ToString(CultureInfo.InvariantCulture) + ") : 0;");
        }
        string unknown = names.Next("unknownTable");
        body.Add("int " + unknown + " = reader.Position + " + members.Length.ToString(CultureInfo.InvariantCulture) + ";");
        body.Add("reader.EnsureReadable(" + count + ");");
        body.Add("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, unchecked((nint)(uint)((" + count + "))));");
        string second = names.Next("id");
        body.Add("uint " + second + " = reader.ReadVarIntUInt32();");
        string reuse = names.Next("reuseObject");
        body.Add("bool " + reuse + " = " + Null("is not null") + " && !reader.OptionState.IsObjectReferenceRegistered(" + access + ");");
        body.Add("if (!" + reuse + ")");
        body.Add("{");
        body.Add(target + " = new " + type + "();");
        body.Add("}");
        body.Add("");
        body.Add("reader.OptionState.AddObjectReference(" + second + ", " + target + "!);");
        body.Add("if (" + reuse + ")");
        body.Add("{");
        string reuseLocal = null;
        if (members.Any(member => member.Mirrored))
        {
            reuseLocal = names.Next("reuseLocal");
            body.Add("ref var " + reuseLocal + " = ref LuminPackMarshal.As<" + type + ", " + model.Mirror + ">(ref " + target + "!);");
        }
        for (int index = 0; index < members.Length; index++)
            body.Add(temps[index] + " = " + (members[index].Mirrored ? reuseLocal : target + "!") + "." + members[index].Name + ";");
        body.Add("}");
        body.Add("");
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            body.Add("if (" + widths[index] + " != 0)");
            body.Add("{");
            if (member.IsFixed)
            {
                body.Add("if (" + widths[index] + " != " + memory + ".SizeOf<" + member.Scalar + ">())");
                body.Add("\treader.ValidateReferenceWidth(" + widths[index] + ", " + memory + ".SizeOf<" + member.Scalar + ">());");
                body.Add("reader.EnsureReadable(" + memory + ".SizeOf<" + member.Scalar + ">());");
                body.Add("reader.Advance(" + w.A("LuminPackReader") + ".Read(ref reader.Cursor, out " + temps[index] + "));");
            }
            else
            {
                string end = names.Next(Camel(member.Name) + "End");
                body.Add("int " + end + " = reader.ReadReferencePayloadLength(" + widths[index] + ") + reader.Position;");
                if (member.Kind == DirectMemberKind.Text)
                    body.Add("global::LuminPack." + BinaryExtensionProjection.Owner(model.Extension, "string") + ".Read<" + Profile + ">(ref reader, ref " +
                        temps[index] + "!);");
                else
                    AppendObjectRead(state, model, member, body, temps[index], refLocal: false);
                body.Add(w.A("LuminPackReader") + ".ValidateVersionFieldEnd(reader.Position, " + end + ");");
            }
            body.Add("}");
            body.Add("else");
            body.Add("{");
            body.Add(temps[index] + " = default !;");
            body.Add("}");
            body.Add("");
        }
        body.Add("if (" + count + " > " + members.Length.ToString(CultureInfo.InvariantCulture) + ")");
        body.Add("\treader.SkipReferenceSlots(" + unknown + ", " + count + " - " + members.Length.ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("// Assign the field value.");
        if (members.Any(member => member.Mirrored))
        {
            body.Add("// Assign private fields.");
            string assign = names.Next("local");
            body.Add("ref var " + assign + " = ref LuminPackMarshal.As<" + type + ", " + model.Mirror + ">(ref " + target + "!);");
            for (int index = 0; index < members.Length; index++)
                body.Add((members[index].Mirrored ? assign : access) + "." + members[index].Name + " = " + temps[index] + "!;");
        }
        else
        {
            body.Add("// Assign public fields.");
            for (int index = 0; index < members.Length; index++)
                body.Add(access + "." + members[index].Name + " = " + temps[index] + "!;");
        }
        foreach (string callback in model.ReadAfter)
            body.Add(Rewrite(callback, access, null));
    }

    /// <summary>Reads one reference member, expanding its own record body once below the enclosing root.</summary>
    /// <param name="state">The shared read state.</param>
    /// <param name="owner">The record that declares the member.</param>
    /// <param name="member">The reference member.</param>
    /// <param name="body">The statements receiving the read.</param>
    /// <param name="expression">The member's storage or temporary local.</param>
    /// <param name="refLocal">Whether the same-table path reads through an alias of the member's storage.</param>
    private static void AppendObjectRead(ReadState state, DirectTypeModel owner, DirectMember member, List<string> body, string expression, bool refLocal)
    {
        var child = member.Nested ?? owner;
        if (state.Stack.Contains(child.GlobalName))
        {
            body.Add(member.Formatter + ".Read<" + Profile + ">(ref reader, ref " + expression + "!);");
            return;
        }
        if (state.Expanded != null)
            state.Expanded.Add(child);
        var names = state.Names;
        body.Add("{");
        string target;
        if (refLocal)
        {
            // The same-table path reads the member through a writable alias of its storage.
            target = names.Next("value");
            body.Add("ref " + child.GlobalName + " " + target + " = ref " + expression + "!;");
        }
        else
            target = expression;
        var inner = new List<string>();
        state.Stack.Add(child.GlobalName);
        BuildRead(state, child, inner, target, refLocal ? target : target + "!", top: false);
        state.Stack.Remove(child.GlobalName);
        string label = names.Label();
        foreach (string line in inner)
            body.Add(line.IndexOf("\u0003", StringComparison.Ordinal) >= 0 ? line.Replace("\u0003GOTO\u0003", "goto " + label + ";") : line);
        body.Add(label + ":");
        body.Add("\t;");
        body.Add("}");
        body.Add("");
    }

    /// <summary>Rewrites a callback that the model spelled for the root payload.</summary>
    private static string Rewrite(string callback, string access, string instance)
    {
        const string rooted = "value?.";
        if (!callback.StartsWith(rooted, StringComparison.Ordinal))
            return callback;
        string name = callback.Substring(rooted.Length);
        return instance != null ? instance + "." + name : access + "?." + name;
    }

    /// <summary>Builds a constant-bytes difference expression through the file aliases.</summary>
    private static string Difference(DirectWriter w, byte[] bytes, string source, int widest)
    {
        string memory = w.A("Unsafe");
        return "(" + string.Join(" | ", ReservedRecordEmitter.Words(bytes, widest).Select(word =>
        {
            string location = word.Offset == 0 ? "ref " + source : "ref " + memory + ".Add(ref " + source + ", " +
                word.Offset.ToString(CultureInfo.InvariantCulture) + ")";
            return "((ulong)" + memory + ".ReadUnaligned<" + ReservedRecordEmitter.WordType(word.Width) + ">(" + location + ") ^ 0x" +
                word.Value.ToString("X", CultureInfo.InvariantCulture) + "UL)";
        })) + ")";
    }

    /// <summary>The member storage expression of the write root's local view.</summary>
    private static string Access(DirectMember member) => (member.Mirrored ? "local" : "value") + "." + member.Name;

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

    /// <summary>Names the locals of one method the way the original inline expansion numbers repeated roles.</summary>
    private sealed class NamePool
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);
        private int _labels;

        /// <summary>Creates a pool that already holds the method's own parameter names.</summary>
        internal NamePool(params string[] reserved)
        {
            foreach (string name in reserved)
            {
                _counts[name] = 1;
                _used.Add(name);
            }
        }

        /// <summary>The next occurrence of a role local, dropping a trailing zero when its stem is still free.</summary>
        internal string Next(string canonical)
        {
            bool first = !_counts.ContainsKey(canonical);
            _counts[canonical] = first ? 1 : _counts[canonical] + 1;
            string name;
            if (first && canonical.Length > 1 && canonical[canonical.Length - 1] == '0' &&
                char.IsLetter(canonical[canonical.Length - 2]) && !_used.Contains(canonical.Substring(0, canonical.Length - 1)))
            {
                // The presentation drops the number from the lowest occurrence when the plain name is unused.
                name = canonical.Substring(0, canonical.Length - 1);
                _used.Add(canonical);
            }
            else
                name = canonical;
            int ordinal = 2;
            while (!_used.Add(name))
                name = canonical + ordinal++.ToString(CultureInfo.InvariantCulture);
            return name;
        }

        /// <summary>Reserves one inline exit label, numbered from zero in the order the exits close.</summary>
        internal string Label()
        {
            string name;
            do
                name = "end" + (_labels++).ToString(CultureInfo.InvariantCulture);
            while (!_used.Add(name));
            return name;
        }
    }
}
