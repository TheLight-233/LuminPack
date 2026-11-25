using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly HashSet<string> recursiveRecords = new(StringComparer.Ordinal);
    private readonly HashSet<string> recursiveReferenceTypes = new(StringComparer.Ordinal);
    private readonly HashSet<string> recursiveReferenceBindings = new(StringComparer.Ordinal);
    private readonly Queue<(JsonElement Contract, string Name, int Profile)> deferredRecursiveRecords = new();
    private readonly HashSet<string> recursiveLists = new(StringComparer.Ordinal);
    private readonly List<string> recursiveRecordBodies = new();
    private bool isolatedRecursiveEntries;

    /// <summary>Finds the records whose C++ storage must break a cycle in the projected member graph.</summary>
    /// <param name="rootType">The normalized model or standalone collection type.</param>
    /// <param name="rootProfile">The operation root policy.</param>
    private void AnalyzeRecursiveRecords(string rootType, JsonElement rootProfile)
    {
        var reachable = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (string child in RecursiveDependencies(rootType))
            Visit(child);
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
            bool rootProjectsLimits = HasFiniteLimit(rootType, "maxDepth") || HasFiniteLimit(rootType, "maxCollection") ||
                HasFiniteLimit(rootType, "maxString");
            if (isolatedRecursiveEntries)
            {
                if (recursiveRecords.Any(type => Reaches(type, child => IsUnion(child) && Reaches(child, recursiveRecords.Contains))))
                    throw Missing(rootType, "a Union below owning recursion that leads back to an owning recursive record");
                if (boundaries.Any(type => IsUnion(type) && reachable[type].Any(recursiveRecords.Contains)))
                    throw Missing(rootType, "an owning recursive record used directly as a Union case");
                if (boundaries.Any(type => Reaches(type, recursiveRecords.Contains) &&
                        reachable[type].Any(child => Reaches(child, candidate => candidate == type))))
                    throw Missing(rootType, "a recursive reference or Union boundary above an owning recursive subtree");
                if (reachable.Keys.Any(type => HasFiniteLimit(type, "maxDepth")))
                    throw Missing(rootType, "a finite Binary depth shared by owning recursion and references or a Union");

                foreach (string type in recursiveRecords)
                    if (Reaches(type, IsTracked))
                        recursiveReferenceTypes.Add(type);
                string[] outgoing = reachable.Keys.Where(type =>
                    recursiveReferenceTypes.Any(owner => Reaches(owner, candidate => candidate == type))).ToArray();
                IEnumerable<string> trackedDescendants = outgoing.Where(IsTracked);
                if (trackedDescendants.Any(type => Reaches(type, recursiveRecords.Contains) ||
                        reachable[type].Any(child => Reaches(child, candidate => candidate == type))))
                    throw Missing(rootType, "a recursive tracked descendant of an owning recursive subtree");
                if (outgoing.Any(type => HasIndependentLimit(type, "maxCollection") || HasIndependentLimit(type, "maxString")))
                    throw Missing(rootType, "an outgoing recursive subtree with independent Binary collection or string limits");
            }
            if (reachable.Keys.Any(type => HasFiniteLimit(type, "maxDepth") &&
                    Reaches(type, candidate => recursiveRecords.Contains(candidate) && Flag(contracts[candidate], "versioned"))))
                throw Missing(rootType, "a finite Binary depth across isolated recursive Version reads");
            if (!rootProjectsLimits && reachable.Keys.Any(type => type != rootType && HasFiniteLimit(type, "maxDepth") &&
                    Reaches(type, recursiveRecords.Contains)))
                throw Missing(rootType, "a recursive child with an independent Binary depth limit");
            if (HasFiniteLimit(rootType, "maxDepth") && reachable.Keys.Any(type => Text(contracts[type], "kind") == "Record" &&
                    contracts[type].TryGetProperty("recursive", out JsonElement managed) &&
                    managed.ValueKind is JsonValueKind.True or JsonValueKind.False && managed.GetBoolean() != recursiveRecords.Contains(type)))
                throw Missing(rootType, "managed Binary depth accounting outside the projected recursive record graph");
        }

        bool IsUnion(string type) => Text(contracts[type], "kind") == "Union";

        bool IsTracked(string type) => Flag(contracts[type].GetProperty("profile"), "trackReferences");

        bool IsContextBoundary(string type) => IsUnion(type) || IsTracked(type);

        bool HasFiniteLimit(string type, string limit)
        {
            JsonElement profile = type == rootType ? rootProfile : contracts[type].GetProperty("profile");
            return !Flag(profile, "trusted") && profile.TryGetProperty(limit, out JsonElement value) &&
                value.ValueKind == JsonValueKind.Number && value.GetInt32() < int.MaxValue;
        }

        bool HasIndependentLimit(string type, string limit) => HasFiniteLimit(type, limit) &&
            (!HasFiniteLimit(rootType, limit) || contracts[type].GetProperty("profile").GetProperty(limit).GetInt32() !=
                rootProfile.GetProperty(limit).GetInt32());

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
            if (contract.TryGetProperty("sequenceBase", out JsonElement sequence) && sequence.ValueKind == JsonValueKind.String)
                members = members.Append(sequence.GetString()!);
            if (contract.TryGetProperty("collectionBase", out JsonElement collection) && collection.ValueKind == JsonValueKind.Object)
                members = members.Append(Text(collection, "type"));
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
        bool tracking = recursiveReferenceTypes.Contains(key.Type);
        if (tracking)
            recursiveReferenceBindings.Add(name);
        string readState = tracking ? ", luminpack::reference_read_state&" : string.Empty;
        string writeState = tracking ? ", luminpack::reference_write_state&" : string.Empty;
        bool listBody = contract.GetProperty("sequenceBase").ValueKind == JsonValueKind.String;
        code.Append("struct ").Append(name).Append(";\nstruct ").Append(binding.Binary)
            .Append("\n{\n    using value_type = ").Append(binding.Type).Append(";\n")
            .Append("    static constexpr std::size_t minimum_size = ").Append(listBody ? "4" : "1").Append(";\n")
            .Append("    static value_type read(luminpack::reader&, luminpack::version_state&").Append(readState)
            .Append(", luminpack::detail::recursive_record_depth&);\n")
            .Append("    static void write(luminpack::writer&, const value_type&, luminpack::version_state&").Append(writeState)
            .Append(", luminpack::detail::recursive_record_depth&);\n");
        if (isolatedRecursiveEntries)
        {
            string referenceArgument = tracking ? ", references" : string.Empty;
            // The subtree starts a private stack guard and borrows its operation registries.
            code.Append("    static value_type read(luminpack::reader& input, luminpack::version_state& version");
            if (tracking)
                code.Append(readState).Append(" references");
            code.Append(")\n    {\n        luminpack::detail::recursive_record_depth depth;\n        return read(input, version")
                .Append(referenceArgument).Append(", depth);\n    }\n")
                .Append("    static void write(luminpack::writer& output, const value_type& value, luminpack::version_state& version");
            if (tracking)
                code.Append(writeState).Append(" references");
            code.Append(")\n    {\n        luminpack::detail::recursive_record_depth depth;\n        write(output, value, version")
                .Append(referenceArgument).Append(", depth);\n    }\n");
        }
        code.Append("};\nstruct ").Append(binding.Json).Append("\n{\n    using value_type = ").Append(binding.Type).Append(";\n")
            .Append("    static value_type read(auto&);\n    static void write(auto&, const value_type&);\n};\n");
        generated.Add(key, binding);
        deferredRecursiveRecords.Enqueue((contract, name, key.Profile));
        return binding;
    }

    /// <summary>Binds a list-derived node to collection framing while retaining its recursive or graph-owned identity.</summary>
    /// <param name="contract">The self-referential declaration.</param>
    /// <param name="name">The active profile codec prefix.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <param name="trackedStorage">The shared graph storage name for a tracked node.</param>
    /// <returns>The previously declared recursive binding.</returns>
    private Binding EmitSelfList(JsonElement contract, string name, int profile, string? trackedStorage)
    {
        string storage = trackedStorage ?? name;
        var binding = generated[(Normalize(Text(contract, "type")), profile)];
        if (trackedStorage == null || definedReferenceStorage.Add(storage))
            code.Append("struct ").Append(storage).Append(RttiInheritance(Normalize(Text(contract, "type"))))
                .Append("\n{\n    std::vector<").Append(binding.Type).Append("> items;\n};\n");
        string body = Apply("luminpack::detail::self_list_binary", [storage, binding.Binary]);
        string jsonItems = Apply("luminpack::detail::self_list_json_items", [binding.Json]);
        if (trackedStorage != null)
        {
            EmitTrackedRecordCodec(name, storage, profile, false, [],
                [new JsonMember("$value", "&" + storage + "::items", jsonItems, [])], body);
        }
        else
        {
            recursiveLists.Add(name);
            recursiveRecordBodies.Add(name);
            Alias(name + "_binary_body", body);
            Alias(name + "_json_body", "luminpack::detail::self_list_json_body", [name, jsonItems]);
        }
        return binding;
    }

    /// <summary>Emits recursive storage and discovers the concrete codecs of its dependencies.</summary>
    private void EmitRecursiveStorage()
    {
        while (deferredRecursiveRecords.TryDequeue(out var item))
            EmitRecord(item.Contract, item.Name, item.Profile);
    }

    /// <summary>Emits recursive bodies after both owning and tracked dependencies are complete.</summary>
    private void EmitRecursiveCodecs()
    {
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
        if (recursiveLists.Contains(name)) binaryCodec = name + "_binary_body";
        string jsonCodec = $"luminpack::detail::recursive_json_record<{name}, {json}_body>";
        bool tracking = recursiveReferenceBindings.Contains(name);
        string references = tracking ? ", references" : string.Empty;
        code.Append("inline ").Append(binary).Append("::value_type ").Append(binary)
            .Append("::read(luminpack::reader& input, luminpack::version_state& version");
        if (tracking)
            code.Append(", luminpack::reference_read_state& references");
        code.Append(", luminpack::detail::recursive_record_depth& depth)\n")
            .Append("{\n    return ").Append(binaryCodec).Append("::read(input, version, depth").Append(references).Append(");\n}\n")
            .Append("inline void ").Append(binary)
            .Append("::write(luminpack::writer& output, const value_type& value, luminpack::version_state& version");
        if (tracking)
            code.Append(", luminpack::reference_write_state& references");
        code.Append(", luminpack::detail::recursive_record_depth& depth)\n")
            .Append("{\n    ").Append(binaryCodec).Append("::write(output, value, version, depth").Append(references).Append(");\n}\n")
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
