#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes a covered type's JSON roots, entries, extensions and name tables in their final published form.</summary>
/// <remarks>Property heads use the fused integer writer or the generated literal stores of the original emitter, reads use the
/// ordered-property fast path with length- or hash-dispatched name lookups, and floating-point reads move into non-inlined
/// local functions under the same budget as <c>JsonScalarLoopProjection</c>.</remarks>
internal static class JsonEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the complete JSON file body.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls; see <see cref="BinaryEmitter.Emit"/>.</param>
    internal static void Emit(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.JsonWrite) != 0;
        bool reads = (operations & GeneratedOperations.JsonRead) != 0;
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        string jsonWriter = w.A("LuminPackJsonWriter"), jsonReader = w.A("LuminPackJsonReader"), options = w.A("ILuminPackOptions");
        string serializer = w.A("LuminPackSerializer"), standard = model.DeclaredProfile != null ? w.A(model.DeclaredProfile) : w.A("LuminPackUtf16LengthOptions");
        string constraint = "where TLuminProfile : struct, " + options;
        // Every method of a generic definition repeats its type parameters first, then the operation's profile.
        string Arguments(params string[] extra) => model.TypeArguments(extra);
        string TypeConstraint() => model.TypeConstraintText;
        string ConstraintLine(bool profile) => profile
            ? (TypeConstraint().Length == 0 ? constraint : TypeConstraint() + " " + constraint)
            : TypeConstraint();
        string OptionsConstraint() => TypeConstraint().Length == 0
            ? "where TOptions : struct, " + options : TypeConstraint() + " where TOptions : struct, " + options;
        string inline = w.MethodImpl("AggressiveInlining");
        string helper = "global::LuminPack.Generated." + model.Extension + "." + model.JsonHelper + model.TypeParameterList;
        // A struct is passed by reference without a null annotation.
        string nullable = model.IsStruct ? string.Empty : "?", input = model.IsStruct ? "scoped in " : string.Empty, pass = model.IsStruct ? "in " : string.Empty;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line("internal static void RootWriteJson" + Arguments(Profile) + "(scoped ref " + jsonWriter + " writer, " + input + type + nullable + " input)");
        w.Indented(() => w.Line(ConstraintLine(true)));
        w.Open();
        var names = new JsonTable();
        var writeBody = WriteBody(model, w, helper, names);
        JsonTable.Resolve(writeBody);
        Lines(w, writeBody);
        w.Close();
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line("internal static void RootReadJson" + Arguments(Profile) + "(scoped ref " + jsonReader + " reader, scoped ref " + type + nullable + " value)");
        w.Indented(() => w.Line(ConstraintLine(true)));
        w.Open();
        ReadBody(model, w, helper);
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !writes;
        Entry("SerializeJson", "(scoped ref " + jsonWriter + " writer, scoped in " + type + nullable + " input)", "RootWriteJson", "ref writer, " + pass + "input");
        w.Suppressed = !reads;
        Entry("DeserializeJson", "(scoped ref " + jsonReader + " reader, scoped ref " + type + nullable + " value)", "RootReadJson", "ref reader, ref value");
        w.Suppressed = false;
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        string write = "internal static void Write" + Arguments() + "(this ref " + jsonWriter + " state, in " + type + nullable + " value)";
        string writeCall = serializer + "." + container + ".RootWriteJson" + model.TypeArguments(standard) + "(ref state, " + pass + "value);";
        if (TypeConstraint().Length == 0)
            w.Line(write + " => " + writeCall);
        else
        {
            w.Line(write);
            w.Indented(() => w.Line(TypeConstraint() + " => " + writeCall));
        }
        w.Line(inline);
        w.Line("internal static void Write" + Arguments("TOptions") + "(this ref " + jsonWriter + " state, in " + type + nullable + " value)");
        w.Indented(() => w.Line(OptionsConstraint() + " => " + serializer + "." + container + ".RootWriteJson" + model.TypeArguments("TOptions") +
            "(ref state, " + pass + "value);"));
        w.Suppressed = !reads;
        foreach (string profile in new[] { standard, "TOptions" })
        {
            bool profiled = profile == "TOptions";
            w.Line(inline);
            w.Line("internal static void Read" + (profiled ? Arguments(profile) : Arguments()) + "(this ref " + jsonReader + " state, ref " + type + nullable + " value)");
            if (profiled)
                w.Indented(() => w.Line(OptionsConstraint()));
            else if (TypeConstraint().Length != 0)
                w.Indented(() => w.Line(TypeConstraint()));
            w.Open();
            w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + profile + ">())");
            w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
            w.Line(serializer + "." + container + ".RootReadJson" + model.TypeArguments(profile) + "(ref state, ref value);");
            w.Line("state.CompleteValue();");
            w.Close();
            w.Blank();
        }
        w.Suppressed = false;
        w.Close();
        w.Close();
        w.Blank();

        w.Line("namespace LuminPack.Generated");
        w.Open();
        w.Line("internal static partial class " + model.Extension);
        w.Open();
        w.Line("internal static class " + model.JsonHelper + Arguments());
        if (TypeConstraint().Length != 0)
            w.Indented(() => w.Line(TypeConstraint()));
        w.Open();
        NameTables(model, w, reads);
        w.Close();
        w.Close();
        w.Close();

        void Entry(string prefix, string parameters, string root, string arguments)
        {
            string name = prefix + (model.UniqueEntries ? "_" + readable : string.Empty);
            w.Line(w.Preserve);
            w.Line(inline);
            string standardSignature = "internal static void " + name + Arguments() + parameters;
            string standardCall = container + "." + root + model.TypeArguments(standard) + "(" + arguments + ");";
            if (TypeConstraint().Length == 0)
                w.Line(standardSignature + " => " + standardCall);
            else
            {
                w.Line(standardSignature);
                w.Indented(() => w.Line(TypeConstraint() + " => " + standardCall));
            }
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + name + Arguments(Profile) + parameters);
            w.Indented(() => w.Line(ConstraintLine(true) + " => " + container + "." + root + model.TypeArguments(Profile) + "(" + arguments + ");"));
            w.Blank();
        }
    }

    /// <summary>Spells a member's table entry, as the JSON name layout does.</summary>
    private static string Member(RenderMember member) => JsonNames.MemberSuffix(member.Name);

    /// <summary>Encodes a little-endian 64-bit word of a literal's first or last eight bytes.</summary>
    private static (string First, string Last) Words(byte[] bytes)
    {
        ulong first = 0, last = 0;
        for (int i = 0; i < 8; i++)
        {
            first |= (ulong)bytes[i] << (i * 8);
            last |= (ulong)bytes[bytes.Length - 8 + i] << (i * 8);
        }
        return ("0x" + first.ToString("X16", CultureInfo.InvariantCulture) + "UL", "0x" + last.ToString("X16", CultureInfo.InvariantCulture) + "UL");
    }

    /// <summary>The complete property head of a member, including its leading brace or separator.</summary>
    private static string Head(PayloadRenderModel model, int index) => (index == 0 ? "{" : ",") + "\"" + model.Members[index].Name + "\":";

    /// <summary>Names the locals and exit labels of one JSON write method.</summary>
    /// <remarks>An inline object copies the child's whole write body into the parent method, so every repeated role takes the
    /// occurrence count as a suffix and every copied return becomes a jump to a label placed where the copy closes.</remarks>
    private sealed class JsonTable
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
        private int _tokens;

        /// <summary>The next occurrence of a role-named local.</summary>
        internal string Next(string role)
        {
            int count = _counts.TryGetValue(role, out int seen) ? seen + 1 : 1;
            _counts[role] = count;
            return count == 1 ? role : role + count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Reserves the placeholder of one inline object's exit label.</summary>
        internal string Token() => "\u0004" + (_tokens++).ToString(CultureInfo.InvariantCulture) + "\u0004";

        /// <summary>Numbers the exit labels in the order the finished body closes them, which is the post-order the original
        /// inline expansion numbers them in, and replaces their placeholders.</summary>
        internal static void Resolve(List<string> body)
        {
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in body)
            {
                if (line.Length == 0 || line[0] != '\u0004')
                    continue;
                int end = line.IndexOf('\u0004', 1);
                if (end < 0)
                    continue;
                string token = line.Substring(0, end + 1);
                if (!labels.ContainsKey(token))
                    labels.Add(token, "end" + labels.Count.ToString(CultureInfo.InvariantCulture));
            }
            if (labels.Count == 0)
                return;
            for (int index = 0; index < body.Count; index++)
                if (body[index].IndexOf('\u0004') >= 0)
                    foreach (var pair in labels)
                        body[index] = body[index].Replace(pair.Key, pair.Value);
        }
    }

    /// <summary>Builds the JSON write body.</summary>
    /// <param name="model">The record whose members are written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="helper">The JSON name-table helper of the record.</param>
    /// <param name="names">The shared local names of the enclosing root method, or null for a standalone body.</param>
    /// <param name="expand">Whether a small inline object member copies the child's write body into this one instead of calling its
    /// formatter; the inline budget of the original pipeline is measured on the copy that keeps the call.</param>
    /// <param name="inputName">The already named input parameter of an inline object copy, or null.</param>
    /// <param name="exit">The placeholder an inline object copy jumps to instead of returning, or null for a root.</param>
    private static List<string> WriteBody(PayloadRenderModel model, CodeWriter w, string helper, JsonTable names = null, bool expand = true,
        string inputName = null, string exit = null)
    {
        names ??= new JsonTable();
        string value = names.Next("value"), input = inputName ?? names.Next("input"), isUtf8 = names.Next("isUtf8");
        var body = new List<string>();
        body.Add("var " + value + " = " + input + ";");
        if (!model.IsStruct)
        {
            body.Add("if (" + value + " == null)");
            body.Add("{");
            body.Add("writer.WriteNull<" + Profile + ">();");
            body.Add(exit == null ? "return;" : "goto " + exit + ";");
            body.Add("}");
            body.Add("");
        }
        if (model.Members.Length == 0)
        {
            body.Add("writer.WriteObjectStart<" + Profile + ">();");
            body.Add("writer.WriteObjectEnd<" + Profile + ">();");
            return body;
        }
        // A mirrored type's storage is reached through its layout mirror.
        if (model.Mirror != null && model.Members.Any(member => member.Mirrored))
            body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref " + value +
                (model.IsStruct ? ");" : "!);"));
        body.Add("if (!writer.IsFirstElement)");
        body.Add("\twriter.WriteByteRaw<" + Profile + ">((byte)',');");
        body.Add("bool " + isUtf8 + " = default(" + Profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8;");
        for (int index = 0; index < model.Members.Length; index++)
        {
            var member = model.Members[index];
            string head = Head(model, index);
            string utf8 = helper + ".Utf8Head." + Member(member), utf16 = helper + ".Utf16Head." + Member(member);
            body.Add("// Field: " + member.Name);
            string owner = member.Mirrored ? "local" : value;
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(head);
            // The fused head-and-integer write applies to a directly named field only.
            if (model.Net8 && !member.Mirrored && member.FieldType is ScalarKind.Int or ScalarKind.UInt or ScalarKind.Long or ScalarKind.ULong &&
                utf8Bytes.Length >= 8 && utf8Bytes.Length <= 16)
            {
                var (first, last) = Words(utf8Bytes);
                body.Add("if (" + isUtf8 + " && global::System.BitConverter.IsLittleEndian)");
                body.Add("\twriter.WriteGeneratedInteger<" + Profile + ">(" + first + ", " + last + ", " + utf8Bytes.Length.ToString(CultureInfo.InvariantCulture) +
                    ", " + owner + "." + member.Name + ");");
                body.Add("else");
                body.Add("{");
                body.Add("if (" + isUtf8 + ")");
                body.Add("\twriter.WriteRaw(" + utf8 + ");");
                body.Add("else");
                if (Literal(model, body, head, utf16, utf8: false))
                    body.Add("");
                body.Add("writer.SetFirstElement(true);");
                body.Add("writer.Write<" + Profile + ">(" + owner + "." + member.Name + ");");
                body.Add("}");
                body.Add("");
                continue;
            }
            body.Add("if (" + isUtf8 + ")");
            Literal(model, body, head, utf8, utf8: true);
            body.Add("else");
            bool block = Literal(model, body, head, utf16, utf8: false);
            if (block)
                body.Add("");
            body.Add("writer.SetFirstElement(true);");
            // A type-parameter member is written by the generic serializer from a copy, without a null branch.
            if (member.Kind == RenderMemberKind.Parameter)
            {
                string temp = names.Next("temp");
                body.Add("var " + temp + " = " + owner + "." + member.Name + ";");
                body.Add(w.A("LuminPackSerializer") + ".SerializeJson<" + member.Scalar + ", " + Profile + ">(ref writer, in " + temp + ");");
                body.Add("");
                continue;
            }
            if (member.Raw)
            {
                // A small declared struct copies its object body in place; a built-in or larger one keeps its formatter call.
                if (!member.Builtin && member.Nested != null && expand && WriteCalls(member.Nested) <= 16)
                {
                    ExpandStruct(body, member, owner, w, names);
                    body.Add("");
                    continue;
                }
                // A raw struct is written by its binary formatter owner from a copy, without a null branch.
                string temp = names.Next("temp");
                body.Add("var " + temp + " = " + owner + "." + member.Name + ";");
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, " + temp + ");");
                body.Add("");
                continue;
            }
            if (member.Kind is RenderMemberKind.CultureInfo or RenderMemberKind.ConcurrentQueue or RenderMemberKind.ImmutableArray)
            {
                // The owner frames the null value itself, so the member copies its storage and calls the owner directly.
                string temp = names.Next("temp");
                body.Add("var " + temp + " = " + owner + "." + member.Name + ";");
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, " + temp + ");");
                body.Add("");
                continue;
            }
            if (member.IsFixed)
            {
                body.Add("writer.Write<" + Profile + ">(" + (member.Underlying != null ? "(" + member.Underlying + ")" : string.Empty) + owner + "." + member.Name + ");");
                continue;
            }
            body.Add("if (" + owner + "." + member.Name + " == null)");
            body.Add("{");
            body.Add("writer.WriteNull<" + Profile + ">();");
            body.Add("}");
            body.Add("else");
            body.Add("{");
            if (member.Kind == RenderMemberKind.Object && expand && WriteCalls(member.Nested) <= 16)
                Expand(body, member, owner, w, names);
            else if (member.Kind == RenderMemberKind.Text)
                body.Add("writer.Write<" + Profile + ">(" + owner + "." + member.Name + ");");
            else
            {
                string temp = names.Next("temp");
                body.Add("var " + temp + " = " + owner + "." + member.Name + ";");
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, " + temp + ");");
            }
            body.Add("}");
            body.Add("");
        }
        body.Add("writer.WriteObjectEnd<" + Profile + ">();");
        return body;
    }

    /// <summary>Copies a declared struct member's write body in place: a struct value is read-only, has no null branch and
    /// needs no exit label.</summary>
    private static void ExpandStruct(List<string> body, RenderMember member, string owner, CodeWriter w, JsonTable names)
    {
        string temp = names.Next("temp");
        body.Add("var " + temp + " = " + owner + "." + member.Name + ";");
        string input = names.Next("input");
        body.Add("ref readonly " + member.Nested.GlobalName + " " + input + " = ref " + temp + ";");
        string helper = "global::LuminPack.Generated." + member.Nested.Extension + "." + member.Nested.JsonHelper;
        body.AddRange(WriteBody(member.Nested, w, helper, names, expand: true, inputName: input));
    }

    /// <summary>Copies an inline object's write body into its parent's method, renaming its locals and turning its return into a
    /// jump to a label placed where the copy closes.</summary>
    private static void Expand(List<string> body, RenderMember member, string owner, CodeWriter w, JsonTable names)
    {
        string temp = names.Next("temp"), input = names.Next("input"), token = names.Token();
        body.Add("var " + temp + " = " + owner + "." + member.Name + ";");
        body.Add("{");
        body.Add("ref readonly " + member.Nested.GlobalName + " " + input + " = ref " + temp + ";");
        string helper = "global::LuminPack.Generated." + member.Nested.Extension + "." + member.Nested.JsonHelper;
        body.AddRange(WriteBody(member.Nested, w, helper, names, expand: true, inputName: input, exit: token));
        body.Add(token + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Lists the former names of the members, in member order, as the original name tables number them.</summary>
    private static IEnumerable<(string Name, int Index, string Suffix)> OldNames(PayloadRenderModel model)
    {
        if (model.Version == null)
            yield break;
        for (int index = 0; index < model.Members.Length; index++)
            for (int alias = 0; alias < model.Version.OldNames[index].Length; alias++)
                yield return (model.Version.OldNames[index][alias], index, "Alias_" + index.ToString(CultureInfo.InvariantCulture) + "_" + alias.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The reused value's member access: a struct read through its selected constructor reaches it without the
    /// null-forgiving operator.</summary>
    private static string Value(PayloadRenderModel model) => model.IsStruct ? "value." : "value!.";

    /// <summary>Counts the calls of a type's JSON write body, which decide whether a containing operation copies it inline: a body
    /// of more than sixteen calls stays behind its formatter's call.</summary>
    internal static int WriteCalls(PayloadRenderModel model) => InvocationCounter.Count(WriteBody(model, new CodeWriter(-1), model.JsonHelper, expand: false));

    /// <summary>Writes a property head as a generated literal when it spans eight to sixteen bytes on .NET 8 or later, otherwise as
    /// its raw span.</summary>
    /// <returns>Whether the head was written as a braced block.</returns>
    private static bool Literal(PayloadRenderModel model, List<string> body, string head, string raw, bool utf8)
    {
        byte[] bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(head);
        if (model.Net8 && bytes.Length >= 8 && bytes.Length <= 16)
        {
            var (first, last) = Words(bytes);
            body.Add("{");
            body.Add("if (global::System.BitConverter.IsLittleEndian)");
            body.Add("\twriter.WriteGeneratedLiteral(" + first + ", " + last + ", " + bytes.Length.ToString(CultureInfo.InvariantCulture) + ");");
            body.Add("else");
            body.Add("\twriter.WriteRaw(" + raw + ");");
            body.Add("}");
            return true;
        }
        body.Add("\twriter.WriteRaw(" + raw + ");");
        return false;
    }

    /// <summary>Builds the JSON read body.</summary>
    private static void ReadBody(PayloadRenderModel model, CodeWriter w, string helper)
    {
        var members = model.Members;
        string jsonReader = w.A("LuminPackJsonReader");
        bool fresh = model.FreshResult;
        bool packed = members.Length >= 4;
        var body = new List<string>();
        if (model.IsStruct)
        {
            // A struct reuses its existing storage when any reference member is already set. A type parameter may hold a
            // reference, so its check keeps a runtime value-type test.
            var referenced = members.Where(member => member.Kind is RenderMemberKind.Text or RenderMemberKind.Object or
                RenderMemberKind.Array or RenderMemberKind.List || member.Kind == RenderMemberKind.Parameter).ToArray();
            string reuse = referenced.Length == 0 ? "false" : string.Join(" || ", referenced.Select(member =>
                (member.Kind == RenderMemberKind.Parameter && member.BareParameter && !member.ValueTypeParameter
                    ? "(!typeof(" + member.Scalar + ").IsValueType && (object? )"
                    : string.Empty) +
                (member.Mirrored ? "LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value)." : "value.") +
                member.Name + " is not null" +
                (member.Kind == RenderMemberKind.Parameter && member.BareParameter && !member.ValueTypeParameter ? ")" : string.Empty)));
            body.Add("bool reuseExisting = " + reuse + ";");
        }
        else
        {
            body.Add("if (reader.IsNull())");
            body.Add("{");
            body.Add("value = null;");
            body.Add("return;");
            body.Add("}");
            body.Add("");
            // A pooled type rents its instance before deciding whether it is reused, as the original read does.
            if (model.Rent != null)
            {
                body.Add("if (value is null)");
                body.Add("\tvalue = " + model.Rent + ";");
            }
            body.Add("bool reuseExisting = value is not null;");
        }
        var mirroredMembers = members.Where(member => member.Mirrored).ToArray();
        if (fresh)
        {
            body.Add("var result = reuseExisting ? value! : new " + model.GlobalName + "();");
            // A mirrored member decodes into a temporary and reaches the result through the layout mirror afterwards.
            if (mirroredMembers.Length != 0)
            {
                body.Add("// Temp vars");
                foreach (var member in mirroredMembers)
                    body.Add(member.TypeName + (member.IsFixed ? string.Empty : "?") + " " + Camel(member.Name) + "Temp = default;");
            }
            if (packed)
                body.Add("uint seenFields = 0;");
            else
                foreach (var member in members.Where(member => !member.Mirrored))
                    body.Add("bool " + Camel(member.Name) + "Seen = false;");
        }
        else
        {
            body.Add("// Temp vars");
            foreach (var member in members)
                body.Add(member.TypeName + (member.IsFixed || member.Kind == RenderMemberKind.Parameter && !member.ReferencePayload ? string.Empty : "?") +
                    " " + Camel(member.Name) + "Temp = default;");
            if (model.Mirror != null && members.Any(member => member.Mirrored))
                body.Add("ref var reuseLocal = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref " +
                    (model.IsStruct ? "value" : "value!") + ");");
        }
        body.Add("reader.TryConsumeObjectStart<" + Profile + ">();");
        body.Add("bool isUtf8 = default(" + Profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8;");
        body.Add("int nextPropertyIndex = 0;");
        body.Add("while (true)");
        body.Add("{");
        body.Add("int propertyIndex = nextPropertyIndex;");
        body.Add("if (!" + helper + ".TryReadOrderedJsonProperty<" + Profile + ">(ref reader, propertyIndex))");
        body.Add("{");
        body.Add("if (!reader.Read<" + Profile + ">() || reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
        body.Add("\tbreak;");
        body.Add("if (reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.String)");
        body.Add("{");
        body.Add("reader.Skip<" + Profile + ">();");
        body.Add("continue;");
        body.Add("}");
        body.Add("");
        body.Add("propertyIndex = isUtf8 ? " + helper + ".GetJsonPropertyIndexUtf8(reader.ReadStringUtf8<" + Profile + ">()) : " + helper +
            ".GetJsonPropertyIndexUtf16(reader.ReadStringUtf16<" + Profile + ">());");
        body.Add("if (!reader.Read<" + Profile + ">())");
        body.Add("\tbreak;");
        body.Add("}");
        body.Add("");
        body.Add("nextPropertyIndex = propertyIndex + 1;");
        body.Add("switch (propertyIndex)");
        body.Add("{");

        // Floating-point read sites move into one non-inlined local function per scalar when the body is large.
        int floating = members.Count(member => member.IsFloating);
        var scalarHelpers = new List<(string Scalar, string Name, string State)>();
        var reads = new Dictionary<string, string>();
        var caseLines = new List<string>();
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            string target = fresh ? (member.Mirrored ? Camel(member.Name) + "Temp" : "result." + member.Name) : Camel(member.Name) + "Temp";
            caseLines.Add("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
            caseLines.Add("{");
            if (fresh)
            {
                if (member.Mirrored)
                {
                    caseLines.Add("if (reuseExisting)");
                    caseLines.Add("\t" + target + " = LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref result)." + member.Name + ";");
                }
                else
                    caseLines.Add(packed ? "seenFields |= " + (1u << index).ToString(CultureInfo.InvariantCulture) + "u;" : Camel(member.Name) + "Seen = true;");
            }
            else
            {
                caseLines.Add("if (reuseExisting)");
                caseLines.Add("\t" + target + " = " + (model.Mirror == null || !model.Members.Any(member => member.Mirrored) ? Value(model) : "reuseLocal.") + member.Name + ";");
            }
            if (member.Kind == RenderMemberKind.Parameter)
            {
                // A type-parameter member is read by the generic serializer, which owns its null handling.
                caseLines.Add(w.A("LuminPackSerializer") + ".DeserializeJson<" + member.Scalar + ", " + Profile + ">(ref reader, ref " + target + ");");
                caseLines.Add("break;");
            }
            else if (member.Raw)
            {
                caseLines.Add(member.Formatter + ".Read<" + Profile + ">(ref reader, ref " + target + ");");
                caseLines.Add("break;");
            }
            else if (member.Kind is RenderMemberKind.CultureInfo or RenderMemberKind.ConcurrentQueue or RenderMemberKind.ImmutableArray)
            {
                caseLines.Add(member.Formatter + ".Read<" + Profile + ">(ref reader, ref " + target + ");");
                caseLines.Add("break;");
            }
            else if (member.IsFixed)
            {
                string options = member.IsBoolean ? w.A("LuminPackDefaultOptions") : Profile;
                caseLines.Add(target + " = " + (member.IsFloating ? "\u0001" + member.Scalar + "\u0001" : member.Underlying != null
                    ? "(" + member.Scalar + ")reader.Read<" + member.Underlying + ", " + options + ">()" : "reader.Read<" + member.Scalar + ", " + options + ">()") + ";");
                caseLines.Add("break;");
            }
            else
            {
                caseLines.Add("if (reader.IsNull())");
                caseLines.Add("{");
                caseLines.Add(target + " = null;");
                caseLines.Add("}");
                caseLines.Add("else");
                caseLines.Add("{");
                caseLines.Add(member.Kind == RenderMemberKind.Text ? "reader.Read<" + Profile + ">(ref " + target + ");"
                    : member.Formatter + ".Read<" + Profile + ">(ref reader, ref " + target + ");");
                caseLines.Add("}");
                caseLines.Add("");
                caseLines.Add("break;");
            }
            caseLines.Add("}");
            caseLines.Add("");
        }
        caseLines.Add("default:");
        caseLines.Add("\treader.Skip<" + Profile + ">();");
        caseLines.Add("\tbreak;");
        body.AddRange(caseLines);
        body.Add("}");
        body.Add("}");
        body.Add("");
        if (fresh)
        {
            for (int index = 0; index < members.Length; index++)
            {
                if (members[index].Mirrored)
                    continue;
                body.Add("if (" + (packed ? "(seenFields & " + (1u << index).ToString(CultureInfo.InvariantCulture) + "u) == 0" : "!" + Camel(members[index].Name) + "Seen") + ")");
                body.Add("\tresult." + members[index].Name + " = default !;");
            }
            body.Add("value = result;");
            if (mirroredMembers.Length != 0)
            {
                body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value);");
                foreach (var member in mirroredMembers)
                    body.Add("local." + member.Name + " = " + Camel(member.Name) + "Temp!;");
            }
        }
        else
        {
            body.Add("if (!reuseExisting)");
            body.Add("{");
            // Public fields are set by the selected constructor or the object initializer, mirrored members through the layout mirror afterwards.
            string construction = "value = new " + model.GlobalName + "(" + (model.ConstructorArguments == null ? string.Empty : string.Join(", ", model.ConstructorArguments.Select(name => name == null ? "default" : Camel(name) + "Temp!"))) + ")";
            var initialized = members.Where(member => !member.Mirrored && (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
            if (model.Rent != null)
            {
                // A pooled type populates the rented instance field by field, mirrored members through the layout mirror.
                body.Add("value = " + model.Rent + ";");
                body.Add("// Populate every field on the pooled instance.");
                bool mirroredStorage = members.Any(member => member.Mirrored);
                if (model.Mirror != null && mirroredStorage)
                    body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref " +
                        (model.IsStruct ? "value" : "value!") + ");");
                foreach (var member in members)
                    body.Add((mirroredStorage && member.Mirrored ? "local." : Value(model)) + member.Name + " = " + Camel(member.Name) + "Temp!;");
            }
            else if (initialized.Length == 0)
                body.Add(construction + ";");
            else
            {
                body.Add(construction);
                body.Add("{");
                foreach (var member in initialized)
                    body.Add(member.Name + " = " + Camel(member.Name) + "Temp!,");
                body.Add("};");
            }
            if (model.Rent == null && model.Mirror != null && members.Any(member => member.Mirrored))
            {
                // A selected constructor already set its own mirrored members, so only the rest are assigned afterwards.
                var mirrored = members.Where(member => member.Mirrored &&
                    (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
                if (mirrored.Length != 0)
                {
                    body.Add("// Assign private fields.");
                    body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref " +
                        (model.IsStruct ? "value" : "value!") + ");");
                    foreach (var member in mirrored)
                        body.Add("local." + member.Name + " = " + Camel(member.Name) + "Temp!;");
                }
            }
            body.Add("}");
            body.Add("else");
            body.Add("{");
            foreach (var member in members)
                body.Add((model.Mirror == null || !members.Any(member => member.Mirrored) ? Value(model) : "reuseLocal.") + member.Name + " = " + Camel(member.Name) + "Temp!;");
            body.Add("}");
        }

        // The projection isolates floating-point parsers only when there are several sites in a large body.
        var measured = body.Select(line =>
        {
            int start = line.IndexOf('');
            return start < 0 ? line : line.Substring(0, start) + "reader.Read<double, TLuminProfile>()" + line.Substring(line.IndexOf('', start + 1) + 1);
        });
        bool isolate = floating > 1 && InvocationCounter.Count(measured) > 16;
        string jsonReaderType = jsonReader;
        for (int index = 0; index < body.Count; index++)
        {
            string line = body[index];
            int start = line.IndexOf('\u0001');
            if (start < 0)
                continue;
            int end = line.IndexOf('\u0001', start + 1);
            string scalar = line.Substring(start + 1, end - start - 1);
            string replacement;
            if (isolate)
            {
                if (!reads.TryGetValue(scalar, out replacement))
                {
                    string suffix = new string('_', scalarHelpers.Count);
                    string name = "luminReadJsonScalar" + suffix;
                    scalarHelpers.Add((scalar, name, "luminJsonScalarState" + suffix));
                    reads.Add(scalar, replacement = name + "(ref reader)");
                }
            }
            else
                replacement = "reader.Read<" + scalar + ", " + Profile + ">()";
            body[index] = line.Substring(0, start) + replacement + line.Substring(end + 1);
        }
        foreach (var (scalar, name, state) in scalarHelpers)
        {
            // Helpers were block-bodied before presentation: each follows a closing brace, the first unless after a statement.
            if (name != scalarHelpers[0].Name || body[body.Count - 1] == "}")
                body.Add("");
            body.Add(w.MethodImpl("NoInlining"));
            body.Add("static " + scalar + " " + name + "(ref " + jsonReaderType + " " + state + ") => " + state + ".Read<" + scalar + ", " + Profile + ">();");
        }
        Lines(w, body);
    }

    /// <summary>Lower-cases the first character, as role-named generated locals are spelled.</summary>
    private static string Camel(string name) => PayloadEmitter.LocalName(name);

    /// <summary>Writes body lines: an empty string is a blank line, braces open and close blocks, and a tab indents one line.</summary>
    internal static void Lines(CodeWriter w, List<string> body)
    {
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
            else if (line[0] == '\t')
                w.Indented(() => w.Line(line.Substring(1)));
            else
                w.Line(line);
        }
    }

    /// <summary>Writes the nested name tables, the name lookups and the ordered-property reader.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="reads">Whether the read-only lookups and the ordered-property reader are written.</param>
    private static void NameTables(PayloadRenderModel model, CodeWriter w, bool reads)
    {
        var members = model.Members;
        string method = w.MethodImpl("AggressiveInlining");
        // A memberless record has no name tables at all.
        if (members.Length != 0)
        {
            w.Line("internal static class Utf8");
            w.Open();
            foreach (var member in members)
                w.Line("internal static readonly byte[] " + Member(member) + " = new byte[] { " + string.Join(", ", Encoding.UTF8.GetBytes(member.Name)
                    .Select(item => item.ToString(CultureInfo.InvariantCulture))) + " };");
            w.Close();
            w.Blank();
            w.Line("internal static class Utf16");
            w.Open();
            foreach (var member in members)
                w.Line("internal static readonly char[] " + Member(member) + " = new char[] { " + string.Join(", ", member.Name.Select(item => "'" + item + "'")) + " };");
            w.Close();
            w.Blank();
        }
        foreach (bool utf8 in members.Length == 0 ? new bool[0] : new[] { true, false })
        {
            w.Line("internal static class " + (utf8 ? "Utf8Head" : "Utf16Head"));
            w.Open();
            for (int index = 0; index < members.Length; index++)
            {
                if (!model.Utf8Literals)
                {
                    // Without UTF-8 string literals the heads are arrays of the punctuation and the name's units.
                    string first = index == 0 ? "'{'" : "','";
                    w.Line(utf8
                        ? "internal static readonly byte[] " + Member(members[index]) + " = new byte[] { (byte)" + first + ", (byte)'\"', " +
                            string.Join(", ", Encoding.UTF8.GetBytes(members[index].Name).Select(item => item.ToString(CultureInfo.InvariantCulture))) + ", (byte)'\"', (byte)':' };"
                        : "internal static readonly char[] " + Member(members[index]) + " = new char[] { (char)" + first + ", (char)'\"', " +
                            string.Join(", ", members[index].Name.Select(item => "(char)'" + item + "'")) + ", (char)'\"', (char)':' };");
                    continue;
                }
                string literal = SymbolDisplay.FormatLiteral(Head(model, index), true);
                // The publication layout expands an attributed accessor written on the head's line.
                w.Line("internal static global::System.ReadOnlySpan<" + (utf8 ? "byte" : "char") + "> " + Member(members[index]) + " {" + method);
                w.Line("get => " + (utf8 ? literal + "u8" : "global::System.MemoryExtensions.AsSpan(" + literal + ")") + "; }");
            }
            w.Close();
            w.Blank();
        }
        var oldNames = OldNames(model).ToArray();
        if (oldNames.Length != 0)
        {
            // Former member names, which reads accept beside the current names.
            w.Line("internal static class LuminPackOldNames");
            w.Open();
            foreach (bool utf8 in new[] { true, false })
            {
                w.Line("internal static class " + (utf8 ? "Utf8" : "Utf16"));
                w.Open();
                foreach (var alias in oldNames)
                    w.Line(utf8
                        ? "internal static readonly byte[] " + alias.Suffix + " = new byte[] { " + string.Join(", ", Encoding.UTF8.GetBytes(alias.Name)) + " };"
                        : "internal static readonly char[] " + alias.Suffix + " = new char[] { " + string.Join(", ", alias.Name.Select(c => "(char)" + ((int)c).ToString(CultureInfo.InvariantCulture))) + " };");
                w.Close();
                if (utf8)
                    w.Blank();
            }
            w.Close();
            w.Blank();
        }
        w.Suppressed = !reads;
        foreach (bool utf8 in new[] { true, false })
        {
            Lookup(model, w, utf8);
            w.Blank();
        }
        string reader = w.A("LuminPackJsonReader");
        w.Line("internal static bool TryReadOrderedJsonProperty(ref " + reader + " reader, int index) => TryReadOrderedJsonProperty<" +
            w.A("LuminPackUtf16LengthOptions") + ">(ref reader, index);");
        w.Blank();
        w.Line("internal static bool TryReadOrderedJsonProperty<" + Profile + ">(ref " + reader + " reader, int index)");
        if (members.Length == 0)
        {
            // A memberless record never matches an ordered property.
            w.Indented(() => w.Line("where TLuminProfile : struct, " + w.A("ILuminPackOptions") + " => false;"));
            return;
        }
        w.Indented(() => w.Line("where TLuminProfile : struct, " + w.A("ILuminPackOptions")));
        // The ordered-name locals avoid the definition's type parameters, whose spelling the published text strips to the
        // leading-underscore form shown below.
        var identifiers = new System.Collections.Generic.HashSet<string>(model.TypeParameters.Select(name => name.TrimStart('@')), StringComparer.Ordinal);
        string utf8Local = "luminOrderedNameUtf8", utf16Local = "luminOrderedNameUtf16";
        while (!identifiers.Add("__" + utf8Local))
            utf8Local += "_";
        while (!identifiers.Add("__" + utf16Local))
            utf16Local += "_";
        var body = new List<string>
        {
            "byte[] " + utf8Local + ";",
            "char[] " + utf16Local + ";",
            "switch (index)",
            "{",
        };
        for (int index = 0; index < members.Length; index++)
        {
            body.Add("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
            body.Add("\t" + utf8Local + " = Utf8." + Member(members[index]) + ";");
            body.Add("\t" + utf16Local + " = Utf16." + Member(members[index]) + ";");
            body.Add("\tbreak;");
        }
        body.Add("default:");
        body.Add("\treturn false;");
        body.Add("}");
        body.Add("");
        body.Add("return reader.TryReadPropertyName<" + Profile + ">(" + utf8Local + ", " + utf16Local + ");");
        w.Open();
        Lines(w, body);
        w.Close();
        w.Suppressed = false;
    }

    /// <summary>Writes one encoding's name lookup: a length switch for up to eight names, otherwise a hash switch.</summary>
    private static void Lookup(PayloadRenderModel model, CodeWriter w, bool utf8)
    {
        var members = model.Members;
        // Former names follow the members as extra candidates of the same lookup.
        var candidates = members.Select((member, index) => (Name: member.Name, Index: index, Table: (utf8 ? "Utf8." : "Utf16.") + Member(member)))
            .Concat(OldNames(model).Select(alias => (alias.Name, alias.Index, Table: "LuminPackOldNames." + (utf8 ? "Utf8." : "Utf16.") + alias.Suffix))).ToArray();
        bool small = candidates.Length <= 8;
        string element = utf8 ? "byte" : "char";
        w.Line("internal static int GetJsonPropertyIndex" + (utf8 ? "Utf8" : "Utf16") + "(global::System.ReadOnlySpan<" + element + "> name)");
        var body = new List<string>();
        body.Add(small ? "switch (name.Length)" : "switch (global::LuminPack.Internal.XxHash3.Hash64" + (utf8 ? "Utf8" : "Utf16") + "(name))");
        body.Add("{");
        var groups = candidates.Select(item => (item.Name, item.Index, item.Table, Bytes: Encoding.UTF8.GetBytes(item.Name)))
            .GroupBy(item => small ? (ulong)(utf8 ? item.Bytes.Length : item.Name.Length)
                : utf8 ? JsonNames.ComputeUtf8BytesHash(item.Bytes) : JsonNames.ComputeUtf16Hash(item.Name));
        foreach (var group in groups)
        {
            body.Add("case " + group.Key.ToString(CultureInfo.InvariantCulture) + (small ? ":" : "UL:"));
            foreach (var item in group)
            {
                string table = item.Table;
                string fallback = "global::System.MemoryExtensions.SequenceEqual<" + element + ">(name, " + table + ")";
                int length = utf8 ? item.Bytes.Length : item.Name.Length;
                string index = item.Index.ToString(CultureInfo.InvariantCulture);
                if (length > (utf8 ? 16 : 8))
                {
                    body.Add("\tif (" + fallback + ")");
                    body.Add("\t\treturn " + index + ";");
                    continue;
                }
                var condition = new StringBuilder("global::System.BitConverter.IsLittleEndian ? name.Length == ").Append(length);
                int byteLength = utf8 ? length : length * 2;
                for (int offset = 0; offset < byteLength;)
                {
                    int width = byteLength - offset >= 4 ? 4 : byteLength - offset >= 2 ? 2 : 1;
                    uint word = 0;
                    for (int i = 0; i < width; i++)
                    {
                        int position = offset + i;
                        int value = utf8 ? item.Bytes[position] : (byte)(item.Name[position / 2] >> (8 * (position & 1)));
                        word |= (uint)value << (8 * i);
                    }
                    condition.Append(" && ");
                    if (width > 1)
                        condition.Append(w.A("Unsafe")).Append(".ReadUnaligned<").Append(width == 4 ? "uint" : "ushort").Append(">(ref ");
                    if (offset != 0)
                        condition.Append(w.A("Unsafe")).Append(".Add(ref ");
                    condition.Append(w.A("Unsafe")).Append(".As<").Append(element).Append(", byte>(ref ").Append(w.A("MemoryMarshal")).Append(".GetReference(name))");
                    if (offset != 0)
                        condition.Append(", ").Append(offset).Append(')');
                    if (width > 1)
                        condition.Append(')');
                    condition.Append(" == ").Append(word).Append('u');
                    offset += width;
                }
                condition.Append(" : ").Append(fallback);
                body.Add("\tif (" + condition + ")");
                body.Add("\t\treturn " + index + ";");
            }
            body.Add("\tbreak;");
        }
        body.Add("}");
        body.Add("");
        body.Add("return -1;");
        w.Open();
        foreach (string line in body)
        {
            int tabs = 0;
            while (tabs < line.Length && line[tabs] == '\t')
                tabs++;
            if (line.Length == 0)
                w.Blank();
            else if (line == "{")
                w.Open();
            else if (line == "}")
                w.Close();
            else if (tabs == 0)
                w.Line(line);
            else if (tabs == 1)
                w.Indented(() => w.Line(line.Substring(1)));
            else
                w.Indented(() => w.Indented(() => w.Line(line.Substring(2))));
        }
        w.Close();
    }
}

