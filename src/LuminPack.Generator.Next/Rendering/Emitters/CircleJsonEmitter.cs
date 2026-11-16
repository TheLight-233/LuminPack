#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes a reference-tracked type's JSON roots, entries, extensions and name tables in their final published form.</summary>
/// <remarks>Each object writes its <c>$id</c> beside its member names and refers back to an emitted object by <c>$ref</c>. A small
/// reference member below the root copies its own write body once; the read always calls the member's generated entry.</remarks>
internal static class CircleJsonEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the complete JSON file body.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls; the members of the others are left out.</param>
    internal static void Emit(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.JsonWrite) != 0;
        bool reads = (operations & GeneratedOperations.JsonRead) != 0;
        string type = model.GlobalName, container = "LuminRoots_" + model.Readable;
        string jsonWriter = w.A("LuminPackJsonWriter"), jsonReader = w.A("LuminPackJsonReader"), options = w.A("ILuminPackOptions");
        string serializer = w.A("LuminPackSerializer"), standard = model.DeclaredProfile != null ? w.A(model.DeclaredProfile) : w.A("LuminPackUtf16LengthOptions");
        string constraint = "where TLuminProfile : struct, " + options;
        string inline = w.MethodImpl("AggressiveInlining");
        string helper = Helper(model);

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
        var writeBody = WriteBody(model, w, helper);
        JsonEmitter.Lines(w, writeBody);
        w.Close();
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line("internal static void RootReadJson<" + Profile + ">(scoped ref " + jsonReader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint));
        w.Open();
        JsonEmitter.Lines(w, ReadBody(model, w, helper));
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
            w.Line("internal static void " + prefix + (model.UniqueEntries ? "_" + model.Readable : string.Empty) + parameters + " => " + container + "." +
                root + "<" + standard + ">(" + arguments + ");");
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + prefix + (model.UniqueEntries ? "_" + model.Readable : string.Empty) + "<" + Profile + ">" + parameters);
            w.Indented(() => w.Line(constraint + " => " + container + "." + root + "<" + Profile + ">(" + arguments + ");"));
            w.Blank();
        }
    }

    /// <summary>Handles helper for <c>CircleJsonEmitter</c>.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <returns>The resulting text.</returns>
    private static string Helper(PayloadRenderModel model) => "global::LuminPack.Generated." + model.Extension + "." + model.JsonHelper;

    /// <summary>Builds the JSON write body of one record.</summary>
    /// <param name="model">The record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="helper">The record's JSON name-table helper.</param>
    /// <returns>The generated write statements.</returns>
    private static List<string> WriteBody(PayloadRenderModel model, CodeWriter w, string helper)
    {
        var body = new List<string>();
        const string value = "value", isUtf8 = "isUtf8", exists = "existsReference", id = "id";
        if (model.CircleGuarded)
            body.Add(w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
        body.Add("var " + value + " = input;");
        body.Add("if (" + value + " == null)");
        body.Add("{");
        body.Add("writer.WriteNull<" + Profile + ">();");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add("bool " + isUtf8 + " = default(" + Profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8;");
        body.Add("var(" + exists + ", " + id + ") = writer.OptionState.GetOrAddReference(" + value + ");");
        body.Add("if (" + exists + ")");
        body.Add("{");
        body.Add("writer.WriteObjectStart<" + Profile + ">();");
        bool staticHeads = model.GeneratedPropertyHeads;
        AppendProperty(body, helper, isUtf8, "@ref", "$ref", staticHeads);
        body.Add("writer.Write<" + Profile + ">(" + id + ");");
        body.Add("writer.WriteObjectEnd<" + Profile + ">();");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        if (model.Members.Any(member => member.Mirrored))
            body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref " + value + "!);");
        body.Add("writer.WriteObjectStart<" + Profile + ">();");
        body.Add("// Write the object reference identifier.");
        AppendProperty(body, helper, isUtf8, "id", "$id", staticHeads);
        body.Add("writer.Write<" + Profile + ">(" + id + ");");
        int tempCount = 0;
        foreach (var member in model.Members)
        {
            string access = (member.Mirrored ? "local" : value) + "." + member.Name;
            body.Add("// Field: " + member.Name);
            AppendProperty(body, helper, isUtf8, Member(member), member.Name, staticHeads);
            if (member.Raw)
            {
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, in " + access + ");");
                continue;
            }
            if (member.IsFixed)
            {
                body.Add("writer.Write<" + Profile + ">(" + (member.Underlying == null ? access : "(" + member.Underlying + ")" + access) + ");");
                continue;
            }
            if (member.Kind == RenderMemberKind.Text)
            {
                body.Add("if (" + access + " == null)");
                body.Add("{");
                body.Add("writer.WriteNull<" + Profile + ">();");
                body.Add("}");
                body.Add("else");
                body.Add("{");
                body.Add("writer.Write<" + Profile + ">(" + access + ");");
                body.Add("}");
                body.Add("");
                continue;
            }
            if (member.Kind != RenderMemberKind.Object || member.Nested != null && !member.Nested.Circle)
            {
                body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, in " + access + ");");
                continue;
            }
            string temp = ++tempCount == 1 ? "temp" : "temp" + tempCount.ToString(CultureInfo.InvariantCulture);
            body.Add("var " + temp + " = " + access + ";");
            body.Add(member.Formatter + ".Write<" + Profile + ">(ref writer, " + temp + ");");
            body.Add("");
        }
        body.Add("writer.WriteObjectEnd<" + Profile + ">();");
        return body;
    }

    /// <summary>Emits the static property head and retains the original writer on a short window.</summary>
    /// <param name="body">The generated operation body.</param>
    /// <param name="helper">The owner of the existing name buffers.</param>
    /// <param name="isUtf8">The name of the document encoding local.</param>
    /// <param name="member">The emitted name-buffer identifier.</param>
    /// <param name="name">The name that goes into JSON.</param>
    /// <param name="staticHead">Whether the bounded static head is available.</param>
    private static void AppendProperty(List<string> body, string helper, string isUtf8, string member, string name, bool staticHead)
    {
        if (staticHead)
        {
            body.Add("if (!writer.TryWriteGeneratedPropertyName<" + Profile + ">(" + GeneratedJsonProperty.Arguments(name) + "))");
            body.Add("{");
        }
        body.Add("if (" + isUtf8 + ")");
        body.Add("\twriter.WritePropertyName<" + Profile + ">(" + helper + ".Utf8." + member + ");");
        body.Add("else");
        body.Add("\twriter.WritePropertyName<" + Profile + ">(" + helper + ".Utf16." + member + ");");
        if (staticHead)
            body.Add("}");
    }

    /// <summary>Builds the JSON read body of one record.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="helper">The helper supplied to <c>ReadBody</c>.</param>
    /// <returns>The body.</returns>
    private static List<string> ReadBody(PayloadRenderModel model, CodeWriter w, string helper)
    {
        var members = model.Members;
        string type = model.GlobalName, jsonReader = w.A("LuminPackJsonReader");
        var body = new List<string>();
        if (model.CircleGuarded)
            body.Add(w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
        body.Add("if (reader.IsNull())");
        body.Add("{");
        body.Add("value = null;");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add("uint? objectId = null;");
        body.Add("uint? refId = null;");
        body.Add("bool reuseExisting = value is not null && !reader.OptionState.IsObjectReferenceRegistered(value);");
        body.Add(type + " preCreatedInstance = value!;");
        body.Add("if (!reuseExisting)");
        body.Add("{");
        body.Add("preCreatedInstance = " + CircleBinaryEmitter.NewInstance(model) + ";");
        body.Add("}");
        body.Add("");
        body.Add("value = preCreatedInstance;");
        if (members.Any(member => member.Mirrored))
            body.Add("ref var reuseLocal = ref " + w.A("LuminPackMarshal") + ".As<" + type + ", " + model.Mirror + ">(ref value!);");
        var temps = new string[members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            temps[index] = Camel(member.Name) + "Temp";
            body.Add(member.TypeName + " " + temps[index] + " = default !;");
        }
        body.Add("reader.TryConsumeObjectStart<" + Profile + ">();");
        body.Add("bool isUtf8 = default(" + Profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8;");
        body.Add("while (reader.Read<" + Profile + ">())");
        body.Add("{");
        body.Add("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
        body.Add("\tbreak;");
        body.Add("if (reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.String)");
        body.Add("{");
        body.Add("reader.Skip<" + Profile + ">();");
        body.Add("continue;");
        body.Add("}");
        body.Add("");
        body.Add("int propertyIndex;");
        body.Add("if (isUtf8)");
        body.Add("{");
        body.Add("var propNameUtf8 = reader.ReadStringUtf8<" + Profile + ">();");
        body.Add("propertyIndex = " + helper + ".GetJsonPropertyIndexUtf8(propNameUtf8);");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        body.Add("var propNameUtf16 = reader.ReadStringUtf16<" + Profile + ">();");
        body.Add("propertyIndex = " + helper + ".GetJsonPropertyIndexUtf16(propNameUtf16);");
        body.Add("}");
        body.Add("");
        body.Add("if (!reader.Read<" + Profile + ">())");
        body.Add("\tbreak;");
        body.Add("if (propertyIndex == -2)");
        body.Add("{");
        body.Add("var id = reader.Read<uint, " + Profile + ">();");
        body.Add("objectId = id;");
        body.Add("reader.OptionState.AddObjectReference(id, preCreatedInstance);");
        body.Add("continue;");
        body.Add("}");
        body.Add("");
        body.Add("if (propertyIndex == -3)");
        body.Add("{");
        body.Add("var rId = reader.Read<uint, " + Profile + ">();");
        body.Add("refId = rId;");
        body.Add("value = (" + type + ")reader.OptionState.GetObjectReference(rId);");
        body.Add("while (reader.Read<" + Profile + ">() && reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.ObjectEnd)");
        body.Add("{");
        body.Add("}");
        body.Add("");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add("switch (propertyIndex)");
        body.Add("{");
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            body.Add("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
            body.Add("{");
            body.Add("if (reuseExisting)");
            body.Add("\t" + temps[index] + " = " + Reuse(member) + ";");
            if (member.Raw || member.Kind is RenderMemberKind.Special or RenderMemberKind.ForwardedCollection or RenderMemberKind.Sequence or RenderMemberKind.CultureInfo)
            {
                body.Add(member.Formatter + ".Read<" + Profile + ">(ref reader, ref " + temps[index] + ");");
            }
            else if (member.IsFixed)
            {
                body.Add("if (reader.IsNull())");
                body.Add("\t" + temps[index] + " = default;");
                body.Add("else");
                body.Add("\t" + temps[index] + " = " + (member.Underlying == null ? string.Empty : "(" + member.Scalar + ")") +
                    "reader.Read<" + (member.Underlying ?? member.Scalar) + ", " + (member.IsBoolean ? w.A("LuminPackDefaultOptions") : Profile) + ">();");
            }
            else
            {
                body.Add("if (reader.IsNull())");
                body.Add("{");
                body.Add(temps[index] + " = default!;");
                body.Add("}");
                body.Add("else");
                body.Add("{");
                body.Add(member.Kind == RenderMemberKind.Text
                    ? "reader.Read<" + Profile + ">(ref " + temps[index] + ");"
                    : member.Formatter + ".Read<" + Profile + ">(ref reader, ref " + temps[index] + ");");
                body.Add("}");
                body.Add("");
            }
            body.Add("break;");
            body.Add("}");
            body.Add("");
        }
        body.Add("default:");
        body.Add("{");
        body.Add("reader.Skip<" + Profile + ">();");
        body.Add("break;");
        body.Add("}");
        body.Add("}");
        body.Add("}");
        body.Add("");
        if (members.Any(member => member.Mirrored))
        {
            body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + type + ", " + model.Mirror + ">(ref value!);");
            for (int index = 0; index < members.Length; index++)
                body.Add((members[index].Mirrored ? "local" : "value!") + "." + members[index].Name + " = " + temps[index] + "!;");
        }
        else
            for (int index = 0; index < members.Length; index++)
                body.Add("value." + members[index].Name + " = " + temps[index] + "!;");
        return body;

        string Reuse(RenderMember member) => (member.Mirrored ? "reuseLocal" : "value!") + "." + member.Name;
    }

    /// <summary>Handles member for <c>CircleJsonEmitter</c>.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>The resulting text.</returns>
    private static string Member(RenderMember member) => JsonNames.MemberSuffix(member.Name);

    /// <summary>Handles camel for <c>CircleJsonEmitter</c>.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <returns>The resulting text.</returns>
    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

    /// <summary>Writes the reference name tables, the lookups and the ordered-property reader.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="reads">Whether to enable reads for this operation.</param>
    private static void NameTables(PayloadRenderModel model, CodeWriter w, bool reads)
    {
        var members = model.Members;
        foreach (bool utf8 in new[] { true, false })
        {
            w.Line("internal static class " + (utf8 ? "Utf8" : "Utf16"));
            w.Open();
            foreach (var member in members)
                w.Line(utf8
                    ? "internal static readonly byte[] " + Member(member) + " = new byte[] { " + string.Join(", ", Encoding.UTF8.GetBytes(member.Name)
                        .Select(item => item.ToString(CultureInfo.InvariantCulture))) + " };"
                    : "internal static readonly char[] " + Member(member) + " = new char[] { " + string.Join(", ", member.Name.Select(item => "'" + item + "'")) + " };");
            w.Line(utf8
                ? "internal static readonly byte[] id = new byte[] { " + string.Join(", ", Encoding.UTF8.GetBytes("$id").Select(item => item.ToString(CultureInfo.InvariantCulture))) + " };"
                : "internal static readonly char[] id = new char[] { '$', 'i', 'd' };");
            w.Line(utf8
                ? "internal static readonly byte[] @ref = new byte[] { " + string.Join(", ", Encoding.UTF8.GetBytes("$ref").Select(item => item.ToString(CultureInfo.InvariantCulture))) + " };"
                : "internal static readonly char[] @ref = new char[] { '$', 'r', 'e', 'f' };");
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
        JsonEmitter.Lines(w, body);
        w.Close();
        w.Suppressed = false;
    }

    /// <summary>Writes a length switch for small tables or sparse length buckets, otherwise a hash switch.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="utf8">Whether to enable utf8 for this operation.</param>
    private static void Lookup(PayloadRenderModel model, CodeWriter w, bool utf8)
    {
        var candidates = model.Members.Select((member, index) => (Name: member.Name, Index: index, Table: (utf8 ? "Utf8." : "Utf16.") + Member(member)))
            .Concat(new[]
            {
                (Name: "$id", Index: -2, Table: (utf8 ? "Utf8." : "Utf16.") + "id"),
                (Name: "$ref", Index: -3, Table: (utf8 ? "Utf8." : "Utf16.") + "@ref"),
            })
            .ToArray();
        bool small = candidates.Length <= 8 || JsonNameDispatch.HasSmallBuckets(
            candidates.Select(item => utf8 ? Encoding.UTF8.GetByteCount(item.Name) : item.Name.Length));
        string element = utf8 ? "byte" : "char";
        w.Line("internal static int GetJsonPropertyIndex" + (utf8 ? "Utf8" : "Utf16") + "(global::System.ReadOnlySpan<" + element + "> name)");
        var body = new List<string>();
        body.Add(small ? "switch (name.Length)" : "switch (" + JsonNameDispatch.HashExpression(utf8) + ")");
        body.Add("{");
        var groups = candidates.Select(item => (item.Name, item.Index, item.Table, Bytes: Encoding.UTF8.GetBytes(item.Name)))
            .GroupBy(item => small ? (ulong)(utf8 ? item.Bytes.Length : item.Name.Length)
                : utf8 ? JsonNames.ComputeUtf8BytesHash(item.Bytes) : JsonNames.ComputeUtf16Hash(item.Name));
        foreach (var group in groups)
        {
            body.Add("case " + group.Key.ToString(CultureInfo.InvariantCulture) + (small ? ":" : "UL:"));
            foreach (var item in group)
            {
                string fallback = "global::System.MemoryExtensions.SequenceEqual<" + element + ">(name, " + item.Table + ")";
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
                        int cell = utf8 ? item.Bytes[position] : (byte)(item.Name[position / 2] >> (8 * (position & 1)));
                        word |= (uint)cell << (8 * i);
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
        JsonEmitter.Lines(w, body);
        w.Close();
    }
}
