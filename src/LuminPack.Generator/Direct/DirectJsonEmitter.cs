using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Writes a covered type's JSON roots, entries, extensions and name tables in their final published form.</summary>
/// <remarks>Property heads use the fused integer writer or the generated literal stores of the original emitter, reads use the
/// ordered-property fast path with length- or hash-dispatched name lookups, and floating-point reads move into non-inlined
/// local functions under the same budget as <c>JsonScalarLoopProjection</c>.</remarks>
internal static class DirectJsonEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the complete JSON file body.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls; see <see cref="DirectBinaryEmitter.Emit"/>.</param>
    internal static void Emit(DirectTypeModel model, DirectWriter w, LuminPack.Code.Core.GeneratedOperations operations)
    {
        bool writes = (operations & LuminPack.Code.Core.GeneratedOperations.JsonWrite) != 0;
        bool reads = (operations & LuminPack.Code.Core.GeneratedOperations.JsonRead) != 0;
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        string jsonWriter = w.A("LuminPackJsonWriter"), jsonReader = w.A("LuminPackJsonReader"), options = w.A("ILuminPackOptions");
        string serializer = w.A("LuminPackSerializer"), standard = w.A("LuminPackUtf16LengthOptions");
        string constraint = "where TLuminProfile : struct, " + options;
        string inline = w.MethodImpl("AggressiveInlining");
        string helper = "global::LuminPack.Generated." + model.Extension + "." + model.JsonHelper;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line("internal static void RootWriteJson<" + Profile + ">(scoped ref " + jsonWriter + " writer, " + type + "? input)");
        w.Indented(() => w.Line(constraint));
        w.Open();
        WriteBody(model, w, helper);
        w.Close();
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line("internal static void RootReadJson<" + Profile + ">(scoped ref " + jsonReader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint));
        w.Open();
        ReadBody(model, w, helper);
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !writes;
        Entry("SerializeJson", "(scoped ref " + jsonWriter + " writer, scoped in " + type + "? input)", "RootWriteJson", "ref writer, input");
        w.Suppressed = !reads;
        Entry("DeserializeJson", "(scoped ref " + jsonReader + " reader, scoped ref " + type + "? value)", "RootReadJson", "ref reader, ref value");
        w.Suppressed = false;
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write(this ref " + jsonWriter + " state, in " + type + "? value) => " + serializer + "." + container +
            ".RootWriteJson<" + standard + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + options + " => " + serializer + "." + container + ".RootWriteJson<TOptions>(ref state, value);"));
        w.Suppressed = !reads;
        foreach (string profile in new[] { standard, "TOptions" })
        {
            w.Line(inline);
            w.Line("internal static void Read" + (profile == "TOptions" ? "<TOptions>" : string.Empty) + "(this ref " + jsonReader + " state, ref " + type + "? value)");
            if (profile == "TOptions")
                w.Indented(() => w.Line("where TOptions : struct, " + options));
            w.Open();
            w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + profile + ">())");
            w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
            w.Line(serializer + "." + container + ".RootReadJson<" + profile + ">(ref state, ref value);");
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
        w.Line("internal static class " + model.JsonHelper);
        w.Open();
        NameTables(model, w, reads);
        w.Close();
        w.Close();
        w.Close();

        void Entry(string prefix, string parameters, string root, string arguments)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + prefix + (model.UniqueEntries ? "_" + readable : string.Empty) + parameters + " => " + container + "." + root + "<" + standard + ">(" + arguments + ");");
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + prefix + (model.UniqueEntries ? "_" + readable : string.Empty) + "<" + Profile + ">" + parameters);
            w.Indented(() => w.Line(constraint + " => " + container + "." + root + "<" + Profile + ">(" + arguments + ");"));
            w.Blank();
        }
    }

    /// <summary>Spells a member's table entry, as the JSON name layout does.</summary>
    private static string Member(DirectMember member) => LuminPackJsonCodeGenerator.MemberSuffix(member.Name);

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
    private static string Head(DirectTypeModel model, int index) => (index == 0 ? "{" : ",") + "\"" + model.Members[index].Name + "\":";

    /// <summary>Builds the JSON write body.</summary>
    private static void WriteBody(DirectTypeModel model, DirectWriter w, string helper)
    {
        var body = new List<string>();
        body.Add("var value = input;");
        body.Add("if (value == null)");
        body.Add("{");
        body.Add("writer.WriteNull<" + Profile + ">();");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        // A mirrored type's storage is reached through its layout mirror.
        if (model.Mirror != null)
            body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
        body.Add("if (!writer.IsFirstElement)");
        body.Add("\twriter.WriteByteRaw<" + Profile + ">((byte)',');");
        body.Add("bool isUtf8 = default(" + Profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8;");
        int temps = 0;
        for (int index = 0; index < model.Members.Length; index++)
        {
            var member = model.Members[index];
            string head = Head(model, index);
            string utf8 = helper + ".Utf8Head." + Member(member), utf16 = helper + ".Utf16Head." + Member(member);
            body.Add("// Field: " + member.Name);
            string owner = member.Mirrored ? "local" : "value";
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(head);
            // The fused head-and-integer write applies to a directly named field only.
            if (!member.Mirrored && member.FieldType is LuminFiledType.Int or LuminFiledType.UInt or LuminFiledType.Long or LuminFiledType.ULong &&
                utf8Bytes.Length >= 8 && utf8Bytes.Length <= 16)
            {
                var (first, last) = Words(utf8Bytes);
                body.Add("if (isUtf8 && global::System.BitConverter.IsLittleEndian)");
                body.Add("\twriter.WriteGeneratedInteger<" + Profile + ">(" + first + ", " + last + ", " + utf8Bytes.Length.ToString(CultureInfo.InvariantCulture) +
                    ", value." + member.Name + ");");
                body.Add("else");
                body.Add("{");
                body.Add("if (isUtf8)");
                body.Add("\twriter.WriteRaw(" + utf8 + ");");
                body.Add("else");
                if (Literal(body, head, utf16, utf8: false))
                    body.Add("");
                body.Add("writer.SetFirstElement(true);");
                body.Add("writer.Write<" + Profile + ">(value." + member.Name + ");");
                body.Add("}");
                body.Add("");
                continue;
            }
            body.Add("if (isUtf8)");
            Literal(body, head, utf8, utf8: true);
            body.Add("else");
            bool block = Literal(body, head, utf16, utf8: false);
            if (block)
                body.Add("");
            body.Add("writer.SetFirstElement(true);");
            if (member.IsFixed)
            {
                body.Add("writer.Write<" + Profile + ">(" + owner + "." + member.Name + ");");
                continue;
            }
            body.Add("if (" + owner + "." + member.Name + " == null)");
            body.Add("{");
            body.Add("writer.WriteNull<" + Profile + ">();");
            body.Add("}");
            body.Add("else");
            body.Add("{");
            if (member.Kind == DirectMemberKind.Text)
                body.Add("writer.Write<" + Profile + ">(" + owner + "." + member.Name + ");");
            else
            {
                string temp = temps++ == 0 ? "temp" : "temp" + temps.ToString(CultureInfo.InvariantCulture);
                body.Add("var " + temp + " = " + owner + "." + member.Name + ";");
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, " + temp + ");");
            }
            body.Add("}");
            body.Add("");
        }
        body.Add("writer.WriteObjectEnd<" + Profile + ">();");
        Lines(w, body);
    }

    /// <summary>Writes a property head as a generated literal when it spans eight to sixteen bytes, otherwise as its raw span.</summary>
    /// <returns>Whether the head was written as a braced block.</returns>
    private static bool Literal(List<string> body, string head, string raw, bool utf8)
    {
        byte[] bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(head);
        if (bytes.Length >= 8 && bytes.Length <= 16)
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
    private static void ReadBody(DirectTypeModel model, DirectWriter w, string helper)
    {
        var members = model.Members;
        string jsonReader = w.A("LuminPackJsonReader");
        bool fresh = model.FreshResult;
        bool packed = members.Length >= 4;
        var body = new List<string>();
        body.Add("if (reader.IsNull())");
        body.Add("{");
        body.Add("value = null;");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add("bool reuseExisting = value is not null;");
        if (fresh)
        {
            body.Add("var result = reuseExisting ? value! : new " + model.GlobalName + "();");
            if (packed)
                body.Add("uint seenFields = 0;");
            else
                foreach (var member in members)
                    body.Add("bool " + Camel(member.Name) + "Seen = false;");
        }
        else
        {
            body.Add("// Temp vars");
            foreach (var member in members)
                body.Add(member.TypeName + (member.IsFixed ? string.Empty : "?") + " " + Camel(member.Name) + "Temp = default;");
            if (model.Mirror != null)
                body.Add("ref var reuseLocal = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
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
            string target = fresh ? "result." + member.Name : Camel(member.Name) + "Temp";
            caseLines.Add("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
            caseLines.Add("{");
            if (fresh)
                caseLines.Add(packed ? "seenFields |= " + (1u << index).ToString(CultureInfo.InvariantCulture) + "u;" : Camel(member.Name) + "Seen = true;");
            else
            {
                caseLines.Add("if (reuseExisting)");
                caseLines.Add("\t" + target + " = " + (model.Mirror == null ? "value!." : "reuseLocal.") + member.Name + ";");
            }
            if (member.IsFixed)
            {
                string options = member.IsBoolean ? w.A("LuminPackDefaultOptions") : Profile;
                caseLines.Add(target + " = " + (member.IsFloating ? "\u0001" + member.Scalar + "\u0001" : "reader.Read<" + member.Scalar + ", " + options + ">()") + ";");
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
                caseLines.Add(member.Kind == DirectMemberKind.Text ? "reader.Read<" + Profile + ">(ref " + target + ");"
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
                body.Add("if (" + (packed ? "(seenFields & " + (1u << index).ToString(CultureInfo.InvariantCulture) + "u) == 0" : "!" + Camel(members[index].Name) + "Seen") + ")");
                body.Add("\tresult." + members[index].Name + " = default !;");
            }
            body.Add("value = result;");
        }
        else
        {
            body.Add("if (!reuseExisting)");
            body.Add("{");
            // Public fields are set by the object initializer, mirrored members through the layout mirror afterwards.
            if (members.All(member => member.Mirrored))
                body.Add("value = new " + model.GlobalName + "();");
            else
            {
                body.Add("value = new " + model.GlobalName + "()");
                body.Add("{");
                foreach (var member in members.Where(member => !member.Mirrored))
                    body.Add(member.Name + " = " + Camel(member.Name) + "Temp!,");
                body.Add("};");
            }
            if (model.Mirror != null)
            {
                body.Add("// Assign private fields.");
                body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
                foreach (var member in members.Where(member => member.Mirrored))
                    body.Add("local." + member.Name + " = " + Camel(member.Name) + "Temp!;");
            }
            body.Add("}");
            body.Add("else");
            body.Add("{");
            foreach (var member in members)
                body.Add((model.Mirror == null ? "value!." : "reuseLocal.") + member.Name + " = " + Camel(member.Name) + "Temp!;");
            body.Add("}");
        }

        // The projection isolates floating-point parsers only when there are several sites in a large body.
        var measured = body.Select(line =>
        {
            int start = line.IndexOf('');
            return start < 0 ? line : line.Substring(0, start) + "reader.Read<double, TLuminProfile>()" + line.Substring(line.IndexOf('', start + 1) + 1);
        });
        bool isolate = floating > 1 && DirectCalls.Count(measured) > 16;
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
    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

    /// <summary>Writes body lines: an empty string is a blank line, braces open and close blocks, and a tab indents one line.</summary>
    internal static void Lines(DirectWriter w, List<string> body)
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
    private static void NameTables(DirectTypeModel model, DirectWriter w, bool reads)
    {
        var members = model.Members;
        string method = w.MethodImpl("AggressiveInlining");
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
        foreach (bool utf8 in new[] { true, false })
        {
            w.Line("internal static class " + (utf8 ? "Utf8Head" : "Utf16Head"));
            w.Open();
            for (int index = 0; index < members.Length; index++)
            {
                string literal = SymbolDisplay.FormatLiteral(Head(model, index), true);
                // The publication layout expands an attributed accessor written on the head's line.
                w.Line("internal static global::System.ReadOnlySpan<" + (utf8 ? "byte" : "char") + "> " + Member(members[index]) + " {" + method);
                w.Line("get => " + (utf8 ? literal + "u8" : "global::System.MemoryExtensions.AsSpan(" + literal + ")") + "; }");
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
        w.Indented(() => w.Line("where TLuminProfile : struct, " + w.A("ILuminPackOptions")));
        var body = new List<string>
        {
            "byte[] luminOrderedNameUtf8;",
            "char[] luminOrderedNameUtf16;",
            "switch (index)",
            "{",
        };
        for (int index = 0; index < members.Length; index++)
        {
            body.Add("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
            body.Add("\tluminOrderedNameUtf8 = Utf8." + Member(members[index]) + ";");
            body.Add("\tluminOrderedNameUtf16 = Utf16." + Member(members[index]) + ";");
            body.Add("\tbreak;");
        }
        body.Add("default:");
        body.Add("\treturn false;");
        body.Add("}");
        body.Add("");
        body.Add("return reader.TryReadPropertyName<" + Profile + ">(luminOrderedNameUtf8, luminOrderedNameUtf16);");
        w.Open();
        Lines(w, body);
        w.Close();
        w.Suppressed = false;
    }

    /// <summary>Writes one encoding's name lookup: a length switch for up to eight names, otherwise a hash switch.</summary>
    private static void Lookup(DirectTypeModel model, DirectWriter w, bool utf8)
    {
        var members = model.Members;
        bool small = members.Length <= 8;
        string element = utf8 ? "byte" : "char";
        w.Line("internal static int GetJsonPropertyIndex" + (utf8 ? "Utf8" : "Utf16") + "(global::System.ReadOnlySpan<" + element + "> name)");
        var body = new List<string>();
        body.Add(small ? "switch (name.Length)" : "switch (global::LuminPack.Internal.XxHash3.Hash64" + (utf8 ? "Utf8" : "Utf16") + "(name))");
        body.Add("{");
        var groups = members.Select((member, index) => (Member: member, Index: index, Bytes: Encoding.UTF8.GetBytes(member.Name)))
            .GroupBy(item => small ? (ulong)(utf8 ? item.Bytes.Length : item.Member.Name.Length)
                : utf8 ? LuminPackJsonCodeGenerator.ComputeUtf8BytesHash(item.Bytes) : LuminPackJsonCodeGenerator.ComputeUtf16Hash(item.Member.Name));
        foreach (var group in groups)
        {
            body.Add("case " + group.Key.ToString(CultureInfo.InvariantCulture) + (small ? ":" : "UL:"));
            foreach (var item in group)
            {
                string table = (utf8 ? "Utf8." : "Utf16.") + Member(item.Member);
                string fallback = "global::System.MemoryExtensions.SequenceEqual<" + element + ">(name, " + table + ")";
                int length = utf8 ? item.Bytes.Length : item.Member.Name.Length;
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
                        int value = utf8 ? item.Bytes[position] : (byte)(item.Member.Name[position / 2] >> (8 * (position & 1)));
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
