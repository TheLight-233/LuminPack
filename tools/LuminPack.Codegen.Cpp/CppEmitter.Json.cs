using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Internal;
using LuminPack.SourceGenerator;

internal sealed partial class CppEmitter
{
    private sealed record JsonMember(string Name, string Pointer, string Codec, string[] FormerNames);

    private readonly record struct JsonCandidate(string Name, int Index);

    /// <summary>Emits the length or seed-zero XXH3 switch independently for each encoding.</summary>
    /// <param name="codec">The generated JSON object codec name.</param>
    /// <param name="type">The generated object type.</param>
    /// <param name="members">The fields in contract order, including former names.</param>
    private void EmitJsonBody(string codec, string type, IReadOnlyList<JsonMember> members)
    {
        // Keep the first matching member when a former name conflicts with a later member.
        JsonCandidate[] candidates = members.SelectMany((member, index) => new[] { member.Name }
            .Concat(member.FormerNames).Select(name => new JsonCandidate(name, index))).ToArray();
        IEnumerable<string> bindings = members.Select(member => Apply("luminpack::json_record_member",
            new[] { Literal(member.Name), member.Pointer, member.Codec }.Concat(member.FormerNames.Select(Literal))));
        if (candidates.Length == 0)
        {
            Alias(codec, "luminpack::json_record_body", new[] { type }.Concat(bindings));
            return;
        }

        string lookup = codec + "_name_index";
        code.Append("struct ").Append(lookup).Append("\n{\n");
        EmitJsonNameIndex(candidates, utf8: true, members.Count);
        EmitJsonNameIndex(candidates, utf8: false, members.Count);
        code.Append("};\n");
        Alias(codec, "luminpack::json_lookup_record_body", new[] { type, lookup }.Concat(bindings));
    }

    /// <summary>Emits one switch with exact name equality in each length or hash case.</summary>
    /// <param name="candidates">The field or former names and their member ordinals.</param>
    /// <param name="utf8">Whether to use UTF-8 bytes or UTF-16 code units.</param>
    /// <param name="miss">The sentinel for an unknown name.</param>
    private void EmitJsonNameIndex(JsonCandidate[] candidates, bool utf8, int miss)
    {
        int[] lengths = candidates.Select(candidate => utf8 ? Encoding.UTF8.GetByteCount(candidate.Name) : candidate.Name.Length).ToArray();
        bool sparse = candidates.Length <= 8 || JsonNameDispatch.HasSmallBuckets(lengths);
        string encoding = utf8 ? "utf8" : "utf16";
        code.Append("    static std::size_t find(const luminpack::json_property_name<luminpack::json_encoding::").Append(encoding)
            .Append(">& name)").Append(sparse ? " noexcept" : "").Append("\n    {\n        switch (name.")
            .Append(sparse ? "encoded_length" : "encoded_hash").Append("())\n        {\n");
        var groups = candidates.Select((candidate, index) => (Candidate: candidate,
            Key: sparse ? (ulong)lengths[index] : JsonNameHash(candidate.Name, utf8)))
            .GroupBy(item => item.Key, item => item.Candidate);
        foreach (IGrouping<ulong, JsonCandidate> group in groups)
        {
            string key = sparse ? group.Key.ToString(CultureInfo.InvariantCulture) : Hex(group.Key);
            code.Append("        case ").Append(key).Append(":\n");
            foreach (JsonCandidate item in group)
                code.Append("            if (name.matches<luminpack::json_name{").Append(Literal(item.Name)).Append("}>())\n")
                    .Append("                return ").Append(item.Index.ToString(CultureInfo.InvariantCulture)).Append(";\n");
            code.Append("            break;\n");
        }
        code.Append("        }\n        return ").Append(miss.ToString(CultureInfo.InvariantCulture)).Append(";\n    }\n");
    }

    /// <summary>Hashes the encoded name through the same seed-zero implementation as the managed generator.</summary>
    /// <param name="name">The decoded member or former name.</param>
    /// <param name="utf8">Whether the name is looked up as UTF-8.</param>
    /// <returns>The hash of the name's encoded bytes.</returns>
    private static ulong JsonNameHash(string name, bool utf8)
    {
        ReadOnlySpan<byte> bytes = utf8 ? Encoding.UTF8.GetBytes(name) : MemoryMarshal.AsBytes(name.AsSpan());
        return XxHash3Algorithm.Compute(ref MemoryMarshal.GetReference(bytes), (uint)bytes.Length);
    }
}
