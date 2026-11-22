using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
    /// <param name="orderedRuns">The Next JSON integer run lengths indexed by start ordinal.</param>
    /// <param name="tracked">Whether the lookup includes the reference envelope's metadata names.</param>
    /// <param name="generatedIntegerProperties">The eligible managed UTF-8 integer fields by ordinal.</param>
    private void EmitJsonBody(string codec, string type, IReadOnlyList<JsonMember> members, int[]? orderedRuns = null,
        bool tracked = false, bool[]? generatedIntegerProperties = null)
    {
        if (generatedIntegerProperties != null && generatedIntegerProperties.Length != members.Count)
            throw new InvalidOperationException("Invalid generated JSON integer property table for " + type + ".");

        // Keep the first matching member when a former name conflicts with a later member.
        JsonCandidate[] candidates = members.SelectMany((member, index) => new[] { member.Name }
            .Concat(member.FormerNames).Select(name => new JsonCandidate(name, index))).ToArray();
        if (tracked)
            candidates = [.. candidates, new("$id", members.Count + 1), new("$ref", members.Count + 2)];
        IEnumerable<string> bindings = members.Select((member, index) =>
        {
            string memberCodec = generatedIntegerProperties?[index] == true
                ? "luminpack::detail::generated_json_integer_member"
                : "luminpack::json_record_member";
            return Apply(memberCodec, new[] { Literal(member.Name), member.Pointer, member.Codec }
                .Concat(member.FormerNames.Select(Literal)));
        });
        if (candidates.Length == 0)
        {
            Alias(codec, "luminpack::json_record_body", new[] { type }.Concat(bindings));
            return;
        }

        string lookup = codec + "_name_index";
        code.Append("struct ").Append(lookup).Append("\n{\n");
        EmitJsonNameIndex(candidates, utf8: true, members.Count);
        EmitJsonNameIndex(candidates, utf8: false, members.Count);
        if (orderedRuns != null)
            EmitJsonInt32Runs(type, members, orderedRuns);
        code.Append("};\n");
        Alias(codec, tracked ? "luminpack::tracked_json_codec" : "luminpack::json_lookup_record_body",
            new[] { type, lookup }.Concat(bindings));
    }

    /// <summary>Reads the managed JSON integer write plan when the export includes it.</summary>
    /// <param name="contract">The generated C++ contract row.</param>
    /// <returns>The eligible field ordinals as flags, or null for an ordinary write.</returns>
    private static bool[]? ReadGeneratedJsonIntegerProperties(JsonElement contract) =>
        contract.TryGetProperty("jsonGeneratedIntegerProperties", out JsonElement properties) &&
        properties.ValueKind == JsonValueKind.Array
            ? properties.EnumerateArray().Select(static item => item.GetBoolean()).ToArray()
            : null;

    /// <summary>Emits the Next run's bounded chunks, publishing each chunk before ordinary property lookup.</summary>
    /// <param name="type">The generated object type.</param>
    /// <param name="members">The typed fields in contract order.</param>
    /// <param name="runs">The Next JSON integer run lengths indexed by start ordinal.</param>
    private void EmitJsonInt32Runs(string type, IReadOnlyList<JsonMember> members, int[] runs)
    {
        if (runs.Length != members.Count)
            throw new InvalidOperationException("Invalid generated JSON Int32 run table for " + type + ".");

        code.Append("    template<luminpack::json_encoding Encoding>\n")
            .Append("    static void read_int32_runs(luminpack::basic_json_reader<Encoding>& input, ").Append(type)
            .Append("& result, std::size_t& next_property, bool& first)\n    {\n");
        int end = 0;
        for (int start = 0; start < runs.Length; start++)
        {
            int count = runs[start];
            if (count == 0)
                continue;
            if (count < 3 || count > members.Count - start || start < end)
                throw new InvalidOperationException("Invalid generated JSON Int32 run for " + type + ".");

            end = start + count;
            // GeneratedJsonInt32Runs.Emit limits each cursor publication to sixteen fields.
            const int MaxFields = 16;
            for (int chunk = start; chunk < end; chunk += MaxFields)
            {
                code.Append("        if (next_property == ").Append(Integer(chunk)).Append(chunk == 0 ? " && first" : " && !first")
                    .Append(")\n        {\n            auto run = input.begin_generated_int32_run();\n")
                    .Append("            do\n            {\n");
                for (int index = chunk; index < Math.Min(end, chunk + MaxFields); index++)
                {
                    JsonMember member = members[index];
                    code.Append("                if (!run.template read<luminpack::json_name{").Append(Literal(member.Name))
                        .Append(index == 0 ? "}, true>" : "}, false>").Append("(result.*(").Append(member.Pointer)
                        .Append("))) break;\n").Append("                next_property = ").Append(Integer(index + 1)).Append(";\n");
                }
                code.Append("            } while (false);\n            run.complete();\n");
                if (chunk == 0)
                    code.Append("            first = next_property == 0;\n");
                code.Append("        }\n");
            }
        }
        code.Append("    }\n");
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
