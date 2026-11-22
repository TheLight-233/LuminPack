using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly HashSet<string> recursiveRecords = new(StringComparer.Ordinal);
    private readonly Queue<(JsonElement Contract, string Name, int Profile)> deferredRecursiveRecords = new();
    private readonly List<string> recursiveRecordBodies = new();
    private bool isolatedRecursiveEntries;

    /// <summary>Finds the records whose C++ storage must break a cycle in the projected member graph.</summary>
    /// <param name="root">The requested closed model.</param>
    private void AnalyzeRecursiveRecords(JsonElement root)
    {
        var reachable = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string rootType = Normalize(Text(root, "type"));
        Visit(rootType);
        foreach (var item in reachable)
        {
            if (Text(contracts[item.Key], "kind") != "Record" ||
                Flag(contracts[item.Key].GetProperty("profile"), "trackReferences"))
                continue;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            if (item.Value.Any(ReachesRecord))
                recursiveRecords.Add(item.Key);

            // The existing tracked and RTTI representations already break storage cycles.
            bool ReachesRecord(string type) => type == item.Key || visited.Add(type) &&
                !Flag(contracts[type].GetProperty("profile"), "trackReferences") &&
                (rttiBases == null || Text(contracts[type], "kind") != "Union") && reachable[type].Any(ReachesRecord);
        }

        if (recursiveRecords.Count != 0)
        {
            string[] boundaries = reachable.Keys.Where(IsContextBoundary).ToArray();
            isolatedRecursiveEntries = boundaries.Length != 0;
            if (isolatedRecursiveEntries)
            {
                if (recursiveRecords.Any(type => Reaches(type, IsContextBoundary)))
                    throw Missing(Text(root, "id"), "an owning recursive subtree that reaches references or a Union");
                if (boundaries.Any(type => Text(contracts[type], "kind") == "Union" && reachable[type].Any(recursiveRecords.Contains)))
                    throw Missing(Text(root, "id"), "an owning recursive record used directly as a Union case");
                if (boundaries.Any(type => Reaches(type, recursiveRecords.Contains) &&
                        reachable[type].Any(child => Reaches(child, candidate => candidate == type))))
                    throw Missing(Text(root, "id"), "a recursive reference or Union boundary above an owning recursive subtree");
                if (reachable.Keys.Any(type => HasFiniteLimit(type, "maxDepth")))
                    throw Missing(Text(root, "id"), "a finite Binary depth shared by owning recursion and references or a Union");
            }
            if (reachable.Keys.Any(type => HasFiniteLimit(type, "maxDepth") &&
                    Reaches(type, candidate => recursiveRecords.Contains(candidate) && Flag(contracts[candidate], "versioned"))))
                throw Missing(Text(root, "id"), "a finite Binary depth across isolated recursive Version reads");
            bool rootProjectsLimits = HasFiniteLimit(rootType, "maxDepth") || HasFiniteLimit(rootType, "maxCollection") ||
                HasFiniteLimit(rootType, "maxString");
            if (!rootProjectsLimits && reachable.Keys.Any(type => type != rootType && HasFiniteLimit(type, "maxDepth") &&
                    Reaches(type, recursiveRecords.Contains)))
                throw Missing(Text(root, "id"), "a recursive child with an independent Binary depth limit");
            if (HasFiniteLimit(rootType, "maxDepth") && reachable.Keys.Any(type => Text(contracts[type], "kind") == "Record" &&
                    contracts[type].TryGetProperty("recursive", out JsonElement managed) &&
                    managed.ValueKind is JsonValueKind.True or JsonValueKind.False && managed.GetBoolean() != recursiveRecords.Contains(type)))
                throw Missing(Text(root, "id"), "managed Binary depth accounting outside the projected recursive record graph");
        }

        bool IsContextBoundary(string type) => Text(contracts[type], "kind") == "Union" ||
            Flag(contracts[type].GetProperty("profile"), "trackReferences");

        bool HasFiniteLimit(string type, string limit)
        {
            JsonElement profile = contracts[type].GetProperty("profile");
            return !Flag(profile, "trusted") && profile.TryGetProperty(limit, out JsonElement value) &&
                value.ValueKind == JsonValueKind.Number && value.GetInt32() < int.MaxValue;
        }

        bool Reaches(string start, Func<string, bool> matches)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            return Search(start);

            bool Search(string type) => visited.Add(type) && (matches(type) || reachable[type].Any(Search));
        }

        void Visit(string type)
        {
            if (reachable.ContainsKey(type))
                return;
            JsonElement contract = contracts[type];
            IEnumerable<string> members = contract.TryGetProperty("members", out JsonElement fields) && fields.ValueKind == JsonValueKind.Array
                ? fields.EnumerateArray().Select(item => item.GetProperty("shape"))
                    .Where(shape => shape.ValueKind == JsonValueKind.Object &&
                        shape.TryGetProperty("type", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                    .Select(shape => Text(shape, "type"))
                : [];
            IEnumerable<string> cases = contract.TryGetProperty("cases", out JsonElement types) && types.ValueKind == JsonValueKind.Array
                ? types.EnumerateArray().Select(item => Text(item, "type"))
                : [];
            string[] dependencies = members.Concat(cases).SelectMany(RecursiveDependencies).Distinct(StringComparer.Ordinal).ToArray();
            reachable.Add(type, dependencies);
            foreach (string child in dependencies)
                Visit(child);
        }
    }

    /// <summary>Finds projected contract identities inside a member or collection argument.</summary>
    /// <param name="spelling">The closed CLR type spelling.</param>
    /// <returns>The immediate modeled dependencies.</returns>
    private IEnumerable<string> RecursiveDependencies(string spelling)
    {
        string type = Normalize(spelling).TrimEnd('?');
        if (contracts.ContainsKey(type))
        {
            yield return type;
            yield break;
        }

        if (type.EndsWith("[]", StringComparison.Ordinal) || RectangularRank(type, out _) > 1)
        {
            foreach (string child in RecursiveDependencies(type[..type.LastIndexOf('[')]))
                yield return child;
            yield break;
        }

        int start = type.IndexOf('<');
        if (start <= 0 || !type.EndsWith('>'))
            yield break;
        string arguments = type[(start + 1)..^1];
        while (true)
        {
            int comma = ArgumentSeparator(arguments);
            foreach (string child in RecursiveDependencies(comma < 0 ? arguments : arguments[..comma]))
                yield return child;
            if (comma < 0)
                yield break;
            arguments = arguments[(comma + 1)..];
        }
    }

    /// <summary>Declares a recursive class and its complete codec interfaces before binding its members.</summary>
    /// <param name="contract">The projected recursive class.</param>
    /// <param name="key">The class identity and selected binary profile.</param>
    /// <param name="name">The profile-specific storage and codec prefix.</param>
    /// <returns>The binding to an owning recursive pointer.</returns>
    private Binding DeclareRecursiveRecord(JsonElement contract, (string Type, int Profile) key, string name)
    {
        var binding = new Binding("std::unique_ptr<" + name + ">", name + "_binary", name + "_json", RecordStorage: name);
        code.Append("struct ").Append(name).Append(";\nstruct ").Append(binding.Binary)
            .Append("\n{\n    using value_type = ").Append(binding.Type).Append(";\n")
            .Append("    static constexpr std::size_t minimum_size = 1;\n")
            .Append("    static value_type read(luminpack::reader&, luminpack::version_state&, luminpack::detail::recursive_record_depth&);\n")
            .Append("    static void write(luminpack::writer&, const value_type&, luminpack::version_state&, luminpack::detail::recursive_record_depth&);\n");
        if (isolatedRecursiveEntries)
        {
            // These subtrees have no graph or Union descendants. Their Binary codecs
            // can use the surrounding Version state and start a private stack guard.
            code.Append("    static value_type read(luminpack::reader& input, luminpack::version_state& version)\n")
                .Append("    {\n        luminpack::detail::recursive_record_depth depth;\n        return read(input, version, depth);\n    }\n")
                .Append("    static void write(luminpack::writer& output, const value_type& value, luminpack::version_state& version)\n")
                .Append("    {\n        luminpack::detail::recursive_record_depth depth;\n        write(output, value, version, depth);\n    }\n");
        }
        code.Append("};\nstruct ").Append(binding.Json).Append("\n{\n    using value_type = ").Append(binding.Type).Append(";\n")
            .Append("    static value_type read(auto&);\n    static void write(auto&, const value_type&);\n};\n");
        generated.Add(key, binding);
        deferredRecursiveRecords.Enqueue((contract, name, key.Profile));
        return binding;
    }

    /// <summary>Completes every recursive DTO before its codecs instantiate their owning bodies.</summary>
    private void EmitRecursiveRecords()
    {
        while (deferredRecursiveRecords.TryDequeue(out var item))
            EmitRecord(item.Contract, item.Name, item.Profile);
        foreach (string name in recursiveRecordBodies)
            EmitRecursiveRecordCodec(name);
    }

    /// <summary>Forwards the ordinary wire operations through the owning record codecs.</summary>
    /// <param name="name">The complete storage and codec prefix.</param>
    private void EmitRecursiveRecordCodec(string name)
    {
        string binary = name + "_binary";
        string json = name + "_json";
        string binaryCodec = $"luminpack::detail::recursive_binary_record<{name}, {binary}_body>";
        string jsonCodec = $"luminpack::detail::recursive_json_record<{name}, {json}_body>";
        code.Append("inline ").Append(binary).Append("::value_type ").Append(binary)
            .Append("::read(luminpack::reader& input, luminpack::version_state& version, luminpack::detail::recursive_record_depth& depth)\n")
            .Append("{\n    return ").Append(binaryCodec).Append("::read(input, version, depth);\n}\n")
            .Append("inline void ").Append(binary)
            .Append("::write(luminpack::writer& output, const value_type& value, luminpack::version_state& version, luminpack::detail::recursive_record_depth& depth)\n")
            .Append("{\n    ").Append(binaryCodec).Append("::write(output, value, version, depth);\n}\n")
            .Append("inline ").Append(json).Append("::value_type ").Append(json).Append("::read(auto& input)\n")
            .Append("{\n    return ").Append(jsonCodec).Append("::read(input);\n}\n")
            .Append("inline void ").Append(json).Append("::write(auto& output, const value_type& value)\n")
            .Append("{\n    ").Append(jsonCodec).Append("::write(output, value);\n}\n\n");
    }

    /// <summary>Preserves the managed operation's read budget on roots that reach a recursive record.</summary>
    /// <param name="binding">The normal root binding.</param>
    /// <param name="profile">The projected root policy.</param>
    /// <returns>The Binary root codec entry.</returns>
    private string RecursiveRootBinary(Binding binding, JsonElement profile)
    {
        if (recursiveRecords.Count == 0 || isolatedRecursiveEntries)
            return binding.Binary;
        string depth = !Flag(profile, "trusted") && profile.TryGetProperty("maxDepth", out JsonElement maximum) &&
            maximum.ValueKind == JsonValueKind.Number && maximum.TryGetInt32(out int limit) && limit is >= 0 and < int.MaxValue
            ? Integer(limit)
            : "std::numeric_limits<std::size_t>::max()";
        return $"luminpack::detail::recursive_binary_root<{binding.Binary}, {depth}>";
    }
}
