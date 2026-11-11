#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes a reference-tracked type's binary roots, entries and extensions in their final published form.</summary>
/// <remarks>A reference-tracked record is <c>[slot count][one width byte per slot][VarInt id][members]</c>. The write reserves the
/// table and the reserve-able members once, then frames each reference member. The read compares the table as constant words and
/// fills the object in place, while reference members below the root expand their own read body once.</remarks>
internal static partial class CircleBinaryEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the complete binary file body.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls; the members of the others are left out.</param>
    internal static void Emit(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        const string writer = "LuminPackWriter", reader = "LuminPackReader";
        string Options() => w.A("ILuminPackOptions");
        string Serializer() => w.A("LuminPackSerializer");
        string Standard() => model.DeclaredProfile != null ? w.A(model.DeclaredProfile) : w.A("LuminPackUtf16LengthOptions");
        string Constraint() => "where TLuminProfile : struct, " + Options();
        string inline = w.MethodImpl("AggressiveInlining");
        string suffix = model.UniqueEntries ? "_" + readable : string.Empty;
        string polymorphic = "[" + w.Attribute("MethodImpl") + "(MethodImplOptions.AggressiveInlining)]";

        var write = BinaryEmitter.ApplyBoundedWrite(model, WriteBody(model, w));
        var readChildren = new List<PayloadRenderModel>();
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
        if (model.Mirrors.Length != 0)
            StorageMirrorEmitter.Emit(w, model.Mirrors);
        else if (model.Mirror != null)
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
            BinaryEmitter.MirrorFields(w, fields, offsets);
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
    internal static byte[] Table(PayloadRenderModel model)
    {
        if (model.CircleWidths.Any(width => width == 0))
            return null;
        var table = new byte[model.CircleSlotCount + 1];
        table[0] = (byte)model.CircleSlotCount;
        for (int index = 0; index < model.Members.Length; index++)
            table[model.CircleSlots[index] + 1] = (byte)model.CircleWidths[index];
        return table;
    }

    /// <summary>Builds the write body, which is the same for the payload and Union entries.</summary>
    private static List<string> WriteBody(PayloadRenderModel model, CodeWriter w)
    {
        var members = model.Members;
        var table = Table(model);
        if (table == null || model.CircleWidths.Any(width => width > 4 && width != 255) ||
            members.SkipWhile(member => member.IsFixed || member.Kind == RenderMemberKind.Text).Any(member => member.Kind != RenderMemberKind.Object))
            return SequentialWriteBody(model, w);
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
            if (member.Kind != RenderMemberKind.Text)
                break;
            string snapshot = Camel(member.Name) + "Snapshot";
            snapshots[index] = snapshot;
            body.Add("string " + snapshot + " = " + Access(member) + ";");
            terms.Add("4 + writer.RecordBound<" + BinaryEmitter.WriteProfile(model, w, member) + ">(" + snapshot + ")");
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
            body.Add("cursor = ref writer.WriteReservedRecordString<" + BinaryEmitter.WriteProfile(model, w, member) + ">(ref cursor, " + snapshots[index] + ");");
            body.Add(w.A("Unsafe") + ".WriteUnaligned(ref " + length + ", (int)" + w.A("Unsafe") + ".ByteOffset(ref " + w.A("Unsafe") + ".Add(ref " +
                length + ", 4), ref cursor));");
        }
        // A reserved string member closes its group with a separating line, as the original record writer does.
        if (lastReserved >= 0 && members.Take(lastReserved + 1).Any(member => member.Kind == RenderMemberKind.Text))
            body.Add("");
        body.Add("writer.Cursor = ref cursor;");
        for (int index = lastReserved + 1; index < members.Length; index++)
        {
            var member = members[index];
            string length = Camel(member.Name) + "Length";
            body.Add("int " + length + " = writer.ReserveVersionLength();");
            body.Add(member.Formatter + ".Write<" + BinaryEmitter.WriteProfile(model, w, member) + ">(ref writer, " + Access(member) + ");");
            body.Add("writer.CompleteVersionLength(" + length + ");");
            body.Add("");
            body.Add("cursor = ref writer.Cursor;");
            body.Add("writer.Cursor = ref cursor;");
        }
        body.AddRange(model.WriteAfter);
        return body;
    }

    /// <summary>Stores the constant slot table at the cursor, as eight-byte or four-byte words.</summary>
    private static void AppendTableStores(CodeWriter w, List<string> body, byte[] table)
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

    private static IEnumerable<string> StoreLines(CodeWriter w, byte[] bytes, string target, int widest)
    {
        string memory = w.A("Unsafe");
        foreach (var word in Words(bytes, widest))
        {
            string location = word.Offset == 0 ? "ref " + target : "ref " + memory + ".Add(ref " + target + ", " + word.Offset.ToString(CultureInfo.InvariantCulture) + ")";
            yield return memory + ".WriteUnaligned(" + location + ", (" + WordType(word.Width) + ")0x" +
                word.Value.ToString("X", CultureInfo.InvariantCulture) + (word.Width == 8 ? "UL" : "U") + ");";
        }
    }

    /// <summary>Builds the same-table read body, expanding each reference member once below the Union root.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="union">Whether the Union read is built, which expands the root's own type once.</param>
    /// <param name="expanded">Collects the member operations whose bodies were expanded, in expansion order.</param>
    private static List<string> ReadBody(PayloadRenderModel model, CodeWriter w, bool union, List<PayloadRenderModel> expanded)
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
        internal CodeWriter W;
        internal NamePool Names;
        internal List<PayloadRenderModel> Expanded;
        internal readonly HashSet<string> Stack = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <param name="state">The shared read state.</param>
    /// <param name="model">The record read at this position.</param>
    /// <param name="body">The statements receiving the read.</param>
    /// <param name="target">The assignment target, which has no null-forgiving suffix.</param>
    /// <param name="access">The readable spelling of the target, which a nullable local suffixes with <c>!</c>.</param>
    /// <param name="top">Whether this is the method's own body, whose exits return instead of jumping to an inline label.</param>
    private static void BuildRead(ReadState state, PayloadRenderModel model, List<string> body, string target, string access, bool top)
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

        var table = Table(model);
        string memory = w.A("Unsafe");
        if (table != null && model.ConstructorArguments == null && !model.RentByRef)
        {
        // The same-table path fills the instance in place.
        string head = names.Next("head"), same = names.Next("sameTable"), id = names.Next("id"), instance = names.Next("instance");
        body.Add("ref byte " + head + " = ref reader.PeekInput(" + table.Length.ToString(CultureInfo.InvariantCulture) + ", out bool " + same + ");");
        string match = table.Length < 8 ? Difference(w, table, head, 4) : "(" + memory + ".SizeOf<nint>() == 8 ? " + Difference(w, table, head, 8) +
            " : " + Difference(w, table, head, 4) + ")";
        body.Add("if (" + same + " && " + match + " == 0)");
        body.Add("{");
        body.Add("reader.EnsureReadable(" + table.Length.ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, " + table.Length.ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("uint " + id + " = reader.ReadVarIntUInt32();");
        body.Add("if (" + Null("is null") + " || !reader.OptionState.ClaimReference(" + id + ", " + access + "))");
        body.Add("{");
        body.Add(target + " = " + NewInstance(model) + ";");
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
            if (model.CircleWidths[index] != 255)
            {
                if (afterPayload)
                {
                    body.Add("");
                    afterPayload = false;
                }
                BinaryEmitter.AddMemberRead(body, model, member, w, "reader", storage);
                continue;
            }
            string end = names.Next("end");
            body.Add("int " + end + " = reader.ReadVersionPayloadLength();");
            body.Add(end + " += reader.Position;");
            if (member.Kind == RenderMemberKind.Text)
            {
                if (member.Mirrored)
                {
                    string current = names.Next("currentString"), decoded = names.Next("decodedString");
                    body.Add("var " + current + " = " + storage + ";");
                    body.Add("var " + decoded + " = reader.Read<" + BinaryEmitter.ReadProfile(model, w, member) + ">(" + current + ");");
                    body.Add("if (!object.ReferenceEquals(" + current + ", " + decoded + "))");
                    body.Add("\t" + storage + " = " + decoded + "!;");
                    body.Add("");
                }
                else
                    body.Add("reader.Read<" + BinaryEmitter.ReadProfile(model, w, member) + ">(ref " + storage + ");");
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

        }

        // The fallback reads each slot into a temporary and assigns the object once every slot is consumed.
        body.Add("// Declare locals to hold the field values.");
        var temps = new string[members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            temps[index] = names.Next(Camel(member.Name) + "Temp");
            body.Add(member.TypeName + " " + temps[index] + " = default !;");
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
        var widths = new string[model.CircleSlotCount];
        for (int order = 0; order < model.CircleSlotCount; order++)
        {
            widths[order] = names.Next("width" + order.ToString(CultureInfo.InvariantCulture));
            body.Add("int " + widths[order] + " = " + order.ToString(CultureInfo.InvariantCulture) + " < " + count + " ? " + memory + ".Add(ref " +
                tableName + ", " + order.ToString(CultureInfo.InvariantCulture) + ") : 0;");
        }
        string unknown = names.Next("unknownTable");
        body.Add("int " + unknown + " = reader.Position + " + model.CircleSlotCount.ToString(CultureInfo.InvariantCulture) + ";");
        body.Add("reader.EnsureReadable(" + count + ");");
        body.Add("reader.Cursor = ref " + memory + ".Add(ref reader.Cursor, unchecked((nint)(uint)((" + count + "))));");
        string second = names.Next("id");
        body.Add("uint " + second + " = reader.ReadVarIntUInt32();");
        string reuse = names.Next("reuseObject");
        body.Add("bool " + reuse + " = " + Null("is not null") + " && !reader.OptionState.IsObjectReferenceRegistered(" + access + ");");
        body.Add("if (!" + reuse + ")");
        body.Add("{");
        body.Add(target + " = " + (model.RentByRef ? "ref " : string.Empty) + NewInstance(model) + ";");
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
        for (int slot = 0; slot < model.CircleSlotCount; slot++)
        {
            int index = Array.IndexOf(model.CircleSlots, slot);
            if (index < 0)
            {
                body.Add("reader.SkipReferenceSlot(" + widths[slot] + ");");
                continue;
            }
            var member = members[index];
            body.Add("if (" + widths[slot] + " != 0)");
            body.Add("{");
            if (model.CircleWidths[index] != 255)
            {
                body.Add("if (" + widths[slot] + " != " + memory + ".SizeOf<" + member.TypeName + ">())");
                body.Add("\treader.ValidateReferenceWidth(" + widths[slot] + ", " + memory + ".SizeOf<" + member.TypeName + ">());");
                body.Add("reader.EnsureReadable(" + memory + ".SizeOf<" + member.TypeName + ">());");
                BinaryEmitter.AddMemberRead(body, model, member, w, "reader", temps[index]);
            }
            else
            {
                string end = names.Next(Camel(member.Name) + "End");
                body.Add("int " + end + " = reader.ReadReferencePayloadLength(" + widths[slot] + ") + reader.Position;");
                if (member.Kind == RenderMemberKind.Text)
                    body.Add("global::LuminPack." + GeneratedNames.Owner(model.Extension, "string") + ".Read<" + BinaryEmitter.ReadProfile(model, w, member) + ">(ref reader, ref " +
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
        body.Add("if (" + count + " > " + model.CircleSlotCount.ToString(CultureInfo.InvariantCulture) + ")");
        body.Add("\treader.SkipReferenceSlots(" + unknown + ", " + count + " - " + model.CircleSlotCount.ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("// Assign the field value.");
        if (members.Any(member => member.Mirrored))
        {
            body.Add("// Assign private fields.");
            string assign = names.Next("local");
            body.Add("ref var " + assign + " = ref LuminPackMarshal.As<" + type + ", " + model.Mirror + ">(ref " + target + "!);");
            for (int index = 0; index < members.Length; index++)
                if (model.Rent != null || model.ConstructorMembers == null || !model.ConstructorMembers.Contains(members[index].Name))
                    body.Add((members[index].Mirrored ? assign : access) + "." + members[index].Name + " = " + temps[index] + "!;");
        }
        else
        {
            body.Add("// Assign public fields.");
            for (int index = 0; index < members.Length; index++)
                if (model.Rent != null || model.ConstructorMembers == null || !model.ConstructorMembers.Contains(members[index].Name))
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
    private static void AppendObjectRead(ReadState state, PayloadRenderModel owner, RenderMember member, List<string> body, string expression,
        bool refLocal)
    {
        if (member.Kind != RenderMemberKind.Object || member.Nested != null && !member.Nested.Circle)
        {
            BinaryEmitter.AddMemberRead(body, owner, member, state.W, "reader", expression);
            return;
        }
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
    private static string Difference(CodeWriter w, byte[] bytes, string source, int widest)
    {
        string memory = w.A("Unsafe");
        return "(" + string.Join(" | ", Words(bytes, widest).Select(word =>
        {
            string location = word.Offset == 0 ? "ref " + source : "ref " + memory + ".Add(ref " + source + ", " +
                word.Offset.ToString(CultureInfo.InvariantCulture) + ")";
            return "((ulong)" + memory + ".ReadUnaligned<" + WordType(word.Width) + ">(" + location + ") ^ 0x" +
                word.Value.ToString("X", CultureInfo.InvariantCulture) + "UL)";
        })) + ")";
    }

    /// <summary>Splits a constant byte sequence into the disjoint words a fixed store or comparison uses.</summary>
    /// <param name="bytes">The constant bytes.</param>
    /// <param name="widest">The widest word: eight on 64-bit targets, four where typed access must stay within four bytes.</param>
    /// <returns>Each word's offset, width and little-endian value.</returns>
    private static IEnumerable<(int Offset, int Width, ulong Value)> Words(byte[] bytes, int widest)
    {
        int offset = 0;
        foreach (int width in new[] { 8, 4, 2, 1 }.Where(width => width <= widest))
            for (; offset + width <= bytes.Length; offset += width)
            {
                ulong word = 0;
                for (int index = width - 1; index >= 0; index--)
                    word = word << 8 | bytes[offset + index];
                yield return (offset, width, word);
            }
    }

    /// <summary>Names the unsigned type of a word width.</summary>
    /// <param name="width">One, two, four or eight bytes.</param>
    /// <returns>The C# keyword.</returns>
    private static string WordType(int width) => width == 8 ? "ulong" : width == 4 ? "uint" : width == 2 ? "ushort" : "byte";

    /// <summary>The member storage expression of the write root's local view.</summary>
    private static string Access(RenderMember member) => (member.Mirrored ? "local" : "value") + "." + member.Name;

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
