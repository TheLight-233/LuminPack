using System.Globalization;
using System.Text;
using LuminPack.SourceGenerator;

internal sealed partial class CppEmitter
{
    private sealed record JsonMember(string Name, string Pointer, string Codec, string[] FormerNames);

    private readonly record struct JsonCandidate(string Name, int Index);

    /// <summary>Emits the existing length-switch lookup for each encoding with sparse names.</summary>
    /// <param name="codec">The generated JSON object codec name.</param>
    /// <param name="type">The generated object type.</param>
    /// <param name="members">The fields in contract order, including former names.</param>
    private void EmitJsonBody(string codec, string type, IReadOnlyList<JsonMember> members)
    {
        // Keep the first matching member when a former name conflicts with a later member.
        JsonCandidate[] candidates = members.SelectMany((member, index) => new[] { member.Name }
            .Concat(member.FormerNames).Select(name => new JsonCandidate(name, index))).ToArray();
        int[] utf8 = candidates.Select(candidate => Encoding.UTF8.GetByteCount(candidate.Name)).ToArray();
        int[] utf16 = candidates.Select(candidate => candidate.Name.Length).ToArray();
        bool emitUtf8 = candidates.Length != 0 && (candidates.Length <= 8 ||
            JsonNameDispatch.HasSmallBuckets(utf8));
        bool emitUtf16 = candidates.Length != 0 && (candidates.Length <= 8 ||
            JsonNameDispatch.HasSmallBuckets(utf16));
        IEnumerable<string> bindings = members.Select(member => Apply("luminpack::json_record_member",
            new[] { Literal(member.Name), member.Pointer, member.Codec }.Concat(member.FormerNames.Select(Literal))));
        if (!emitUtf8 && !emitUtf16)
        {
            Alias(codec, "luminpack::json_record_body", new[] { type }.Concat(bindings));
            return;
        }

        string lookup = codec + "_name_index";
        code.Append("struct ").Append(lookup).Append("\n{\n");
        if (emitUtf8)
            EmitJsonNameIndex("utf8", candidates, utf8, members.Count);
        if (emitUtf16)
            EmitJsonNameIndex("utf16", candidates, utf16, members.Count);
        code.Append("};\n");
        Alias(codec, "luminpack::json_lookup_record_body", new[] { type, lookup }.Concat(bindings));
    }

    /// <summary>Emits one encoded length switch with exact name equality in each case.</summary>
    /// <param name="encoding">The C++ encoding enum member.</param>
    /// <param name="candidates">The field or former names and their member ordinals.</param>
    /// <param name="lengths">The names' lengths in this encoding.</param>
    /// <param name="miss">The sentinel for an unknown name.</param>
    private void EmitJsonNameIndex(string encoding, JsonCandidate[] candidates, int[] lengths, int miss)
    {
        code.Append("    static std::size_t find(const luminpack::json_property_name<luminpack::json_encoding::").Append(encoding)
            .Append(">& name) noexcept\n    {\n        switch (name.encoded_length())\n        {\n");
        var groups = candidates.Select((candidate, index) => (Candidate: candidate, Length: lengths[index]))
            .GroupBy(item => item.Length, item => item.Candidate);
        foreach (IGrouping<int, JsonCandidate> group in groups)
        {
            code.Append("        case ").Append(group.Key.ToString(CultureInfo.InvariantCulture)).Append(":\n");
            foreach (JsonCandidate item in group)
                code.Append("            if (name.matches<luminpack::json_name{").Append(Literal(item.Name)).Append("}>())\n")
                    .Append("                return ").Append(item.Index.ToString(CultureInfo.InvariantCulture)).Append(";\n");
            code.Append("            break;\n");
        }
        code.Append("        }\n        return ").Append(miss.ToString(CultureInfo.InvariantCulture)).Append(";\n    }\n");
    }
}
