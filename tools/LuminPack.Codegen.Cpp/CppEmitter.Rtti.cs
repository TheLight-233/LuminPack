using System.Globalization;
using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly HashSet<string> emittedRttiBases = new(StringComparer.Ordinal);
    private readonly HashSet<string> emittedRttiRegistrations = new(StringComparer.Ordinal);
    private readonly Queue<(JsonElement Contract, string Name, int Profile)> deferredRttiUnions = new();

    /// <summary>Indexes the polymorphic base declarations required by each exact case.</summary>
    /// <param name="declarations">The analyzed contracts in the producer assembly.</param>
    /// <returns>The C++ base names for each normalized case identity.</returns>
    private static ILookup<string, string> IndexRttiBases(IEnumerable<JsonElement> declarations) => declarations
        .Where(static item => Text(item, "kind") == "Union")
        .SelectMany(static root => root.GetProperty("cases").EnumerateArray().Select(item =>
            (Case: Normalize(Text(item, "type")), Base: RttiBaseName(root))))
        .ToLookup(static item => item.Case, static item => item.Base, StringComparer.Ordinal);

    /// <summary>Produces one polymorphic base name independent of its concrete string profile.</summary>
    /// <param name="contract">The analyzed Union declaration.</param>
    /// <returns>The generated C++ base name.</returns>
    private static string RttiBaseName(JsonElement contract) => "type_" + Identifier(Text(contract, "id")) + "_base";

    /// <summary>Declares the polymorphic bases of a case before emitting its concrete DTO.</summary>
    /// <param name="type">The case's normalized identity.</param>
    /// <returns>The C++ inheritance clause or an empty string.</returns>
    private string RttiInheritance(string type)
    {
        if (rttiBases == null)
            return string.Empty;

        string[] bases = rttiBases[type].Distinct(StringComparer.Ordinal).OrderBy(static item => item, StringComparer.Ordinal).ToArray();
        foreach (string name in bases)
            EmitRttiBase(name);
        return bases.Length == 0 ? string.Empty : " : " + string.Join(", ", bases.Select(static name => "public " + name));
    }

    /// <summary>Emits one owning type's base with a virtual destructor for exact RTTI and deletion.</summary>
    /// <param name="name">The sanitized base name.</param>
    private void EmitRttiBase(string name)
    {
        if (emittedRttiBases.Add(name))
        {
            code.Append("struct ").Append(name).Append("\n{\n    virtual ~").Append(name).Append("() = default;\n");
            rttiSlotPositions.Add(name, code.Length);
            code.Append("};\n\n");
        }
    }

    /// <summary>Projects Next's closed writer dispatch and checked hash switch.</summary>
    /// <param name="contract">The Union declaration with its canonical hashes and case order.</param>
    /// <param name="name">The name of this profile's dispatch.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <returns>The owning or graph-borrowed Binary and JSON bindings.</returns>
    private Binding EmitRttiUnion(JsonElement contract, string name, int profile)
    {
        string owner = Text(contract, "id");
        string baseName = RttiBaseName(contract);
        EmitRttiBase(baseName);
        JsonElement cases = contract.GetProperty("cases");
        if (cases.GetArrayLength() == 0)
            throw Missing(owner, "a Union without generated cases");

        bool graph = cases.EnumerateArray().Any(item =>
            contracts.TryGetValue(Normalize(Text(item, "type")), out JsonElement declaration) &&
            Flag(declaration.GetProperty("profile"), "trackReferences"));
        bool registered = Flag(contract, "unionRegistration");
        if (graph || UsesVirtualRttiSlots(contract))
        {
            code.Append("struct ").Append(name).Append("_binary_dispatch;\nstruct ").Append(name).Append("_json_dispatch;\n");
            deferredRttiUnions.Enqueue((contract, name, profile));
        }
        else
        {
            var bindings = BindRttiCases(cases, owner, profile);
            EmitRttiDispatch(name + "_binary_dispatch", baseName, bindings, json: false, registered, graph);
            EmitRttiDispatch(name + "_json_dispatch", baseName, bindings, json: true, registered, graph);
        }
        string policy = registered ? EmitRttiRegistration(contract, baseName, cases) : baseName;
        string binary = graph ? "graph_polymorphic_union_codec" : "polymorphic_union_codec";
        string json = graph ? "graph_json_polymorphic_union_codec" : "json_polymorphic_union_codec";
        string scope = registered ? "luminpack::registered_" : "luminpack::";
        Alias(name + "_binary", scope + binary, new[] { policy, name + "_binary_dispatch" });
        Alias(name + "_json", scope + json, new[] { policy, name + "_json_dispatch" });
        code.Append('\n');
        return new Binding(name + "_binary::value_type", name + "_binary", name + "_json");
    }

    /// <summary>Completes the graph before dispatch code inspects any concrete pointer target.</summary>
    private void EmitDeferredRecordsAndRttiUnions()
    {
        var complete = new List<(JsonElement Contract, string Name, int Profile, IReadOnlyList<RttiCase> Cases)>();
        EmitTrackedRecords();
        while (deferredRttiUnions.TryDequeue(out var item))
        {
            complete.Add((item.Contract, item.Name, item.Profile,
                BindRttiCases(item.Contract.GetProperty("cases"), Text(item.Contract, "id"), item.Profile)));
            EmitTrackedRecords();
        }
        foreach (var item in complete)
        {
            string baseName = RttiBaseName(item.Contract);
            bool registered = Flag(item.Contract, "unionRegistration");
            bool graph = item.Cases.Any(static item => item.Tracked);
            bool virtualSlots = UsesVirtualRttiSlots(item.Contract);
            string? binarySlot = virtualSlots ? VirtualRttiBinarySlot(baseName, item.Contract, item.Profile) : null;
            string? jsonSlot = virtualSlots ? baseName + "_write_json" : null;
            if (virtualSlots)
                EmitVirtualRttiSlots(item.Name, baseName, item.Cases, binarySlot!, jsonSlot!);
            EmitRttiDispatch(item.Name + "_binary_dispatch", baseName, item.Cases, json: false, registered, graph, binarySlot);
            EmitRttiDispatch(item.Name + "_json_dispatch", baseName, item.Cases, json: true, registered, graph, jsonSlot);
        }
        InsertRttiSlotDeclarations();
    }

    /// <summary>Uses full record entries for directly tracked cases and payload entries for ordinary cases.</summary>
    /// <param name="cases">The compiler-projected Union cases.</param>
    /// <param name="owner">The Union's diagnostic name.</param>
    /// <param name="profile">The effective binary profile.</param>
    /// <returns>The validated hashes and concrete codec entries in dispatch order.</returns>
    private IReadOnlyList<RttiCase> BindRttiCases(JsonElement cases, string owner, int profile)
    {
        var bindings = new List<RttiCase>(cases.GetArrayLength());
        var hashes = new HashSet<uint>();
        foreach (JsonElement item in cases.EnumerateArray().OrderBy(static item => item.GetProperty("tag").GetInt32()))
        {
            RejectOpaqueUnionCase(item);
            string type = Normalize(Text(item, "type"));
            if (!contracts.TryGetValue(type, out JsonElement declaration) || Text(declaration, "kind") != "Record")
                throw Missing(Text(item, "id"), "an RTTI case without a class DTO");
            Binding member = BindContract(declaration, profile);
            bool tracked = Flag(declaration.GetProperty("profile"), "trackReferences");
            string? binary = tracked ? member.Binary : member.Payload;
            string? json = tracked ? member.Json : member.JsonPayload;
            if (binary == null || json == null)
                throw Missing(Text(item, "id"), "an RTTI case without a positional payload codec");
            uint hash = item.GetProperty("hash").GetUInt32();
            if (hash == 0 || hash == uint.MaxValue || !hashes.Add(hash))
                throw Missing(owner, "a reserved or duplicate RTTI Union hash");
            string? definition = item.TryGetProperty("definition", out JsonElement open) ? open.GetString() : null;
            bindings.Add(new RttiCase(hash.ToString(CultureInfo.InvariantCulture) + "u", binary, json, tracked, member.RecordStorage!, definition));
        }
        return bindings;
    }

    /// <summary>Emits one registration policy shared by all profiles of a generated Union.</summary>
    /// <param name="contract">The managed Union contract.</param>
    /// <param name="baseName">The generated polymorphic base name.</param>
    /// <param name="cases">The cases whose declared tags and numeric-range hashes are reserved.</param>
    /// <returns>The C++ registration policy name.</returns>
    private string EmitRttiRegistration(JsonElement contract, string baseName, JsonElement cases)
    {
        string policy = baseName + "_registration";
        if (emittedRttiRegistrations.Add(policy))
        {
            uint[] tags = cases.EnumerateArray().SelectMany(static item => new[]
                {
                    item.GetProperty("tag").GetUInt32(), item.GetProperty("hash").GetUInt32()
                })
                .Where(static tag => tag <= ushort.MaxValue).Distinct().ToArray();
            code.Append("struct ").Append(policy).Append("\n{\n    using base_type = ").Append(baseName).Append(";\n")
                .Append("    static constexpr bool wide_tags = ").Append(Flag(contract, "unionWideTags") ? "true" : "false")
                .Append(";\n    static constexpr std::array<std::uint16_t, ").Append(Integer(tags.Length)).Append("> reserved_tags{")
                .AppendJoin(", ", tags.Select(static tag => tag.ToString(CultureInfo.InvariantCulture))).Append("};\n};\n\n");
        }
        return policy;
    }

    /// <summary>Emits matching concrete case calls for the selected wire format.</summary>
    /// <param name="name">The generated dispatch name.</param>
    /// <param name="baseName">The shared polymorphic type.</param>
    /// <param name="cases">The ordered hashes and case payload bindings.</param>
    /// <param name="json">Whether to use JSON property framing.</param>
    /// <param name="registered">Whether unknown cases can fall back to registered delegates.</param>
    /// <param name="graph">Whether decoded pointers borrow from their document graph.</param>
    /// <param name="virtualSlot">The virtual write slot or null for the exact writer.</param>
    private void EmitRttiDispatch(string name, string baseName, IReadOnlyList<RttiCase> cases, bool json, bool registered, bool graph,
        string? virtualSlot = null)
    {
        bool tryRead = json && registered;
        code.Append("struct ").Append(name).Append("\n{\n");
        foreach (RttiCase item in cases)
        {
            code.Append("    using case_").Append(item.Hash).Append(" = ");
            if (item.Tracked)
                code.Append("std::remove_pointer_t<");
            code.Append(item.Binary).Append("::value_type").Append(item.Tracked ? ">;\n" : ";\n");
        }
        code.Append('\n');
        if (!json && !graph)
            code.Append("    template<class... Context>\n");
        string resultType = graph ? baseName + "*" : $"std::unique_ptr<{baseName}>";
        if (graph && tryRead)
            resultType = $"std::optional<{baseName}*>";
        code.Append("    static ").Append(resultType).Append(tryRead ? " try_read(" : " read(");
        if (json)
            code.Append("auto& input, std::uint32_t hash)");
        else if (graph)
            code.Append("luminpack::reader& input, std::uint32_t hash, ").Append(baseName)
                .Append("* seed, luminpack::version_state& version, luminpack::reference_read_state& references)");
        else
            code.Append("luminpack::reader& input, std::uint32_t hash, Context&... context)");
        code.Append("\n    {\n        switch (hash)\n        {\n");
        foreach (RttiCase item in cases)
        {
            code.Append("        case ").Append(item.Hash).Append(":\n");
            EmitRttiReadCase(item, json, graph);
        }
        code.Append("        default:\n").Append(tryRead ? "            return {};\n" :
            "            throw luminpack::wire_error(\"Unknown LuminPack Union case hash.\");\n").Append("        }\n    }\n\n");
        if (tryRead && !graph)
        {
            code.Append("    static std::unique_ptr<").Append(baseName).Append("> read(auto& input, std::uint32_t hash)\n")
                .Append("    {\n        if (auto value = try_read(input, hash))\n            return value;\n")
                .Append("        throw luminpack::wire_error(\"Unknown LuminPack Union case hash.\");\n    }\n\n");
        }

        if (!json && !graph)
            code.Append("    template<class... Context>\n");
        string writeCursor = json ? "auto& output, " : "luminpack::writer& output, ";
        string writeContext = json ? ")" : graph ?
            ", luminpack::version_state& version, luminpack::reference_write_state& references)" : ", Context&... context)";
        string writeParameters = writeCursor + (graph || virtualSlot != null ? "" : "const ") + baseName + "& value" + writeContext;
        code.Append(registered ? "    static bool try_write(" : "    static void write(").Append(writeParameters)
            .Append("\n    {\n");
        if (virtualSlot != null)
        {
            string call = json ? $"value.{virtualSlot}(output)" :
                $"luminpack::detail::try_write_virtual_binary_union<&{baseName}::{virtualSlot}>(output, value, " +
                (graph ? "version, references)" : "context...)");
            code.Append(registered ? "        return " : "        if (").Append(call)
                .Append(registered ? ";\n    }\n\n" : ")\n            return;\n");
        }
        else
        {
            code.Append("        const std::type_info& type = typeid(value);\n");
            string branch = "if";
            foreach (RttiCase item in cases)
            {
                code.Append("        ").Append(branch).Append(" (type == typeid(case_").Append(item.Hash).Append("))\n        {\n");
                string value = (item.Tracked ? "&static_cast<case_" : "static_cast<const case_") + item.Hash + "&>(value)";
                EmitRttiWriteCaseBody(item, value, "            ", json, graph ? "version, references" : "context...");
                code.Append(registered ? "            return true;\n        }\n" : "            return;\n        }\n");
                branch = "else if";
            }
            if (registered)
                code.Append("        return false;\n    }\n\n");
        }
        if (registered)
        {
            if (!graph)
            {
                if (!json)
                    code.Append("    template<class... Context>\n");
                code.Append("    static void write(").Append(writeParameters).Append("\n    {\n")
                    .Append(json ? "        if (!try_write(output, value))\n" : "        if (!try_write(output, value, context...))\n")
                    .Append("            throw std::invalid_argument(\"Unknown LuminPack Union runtime type.\");\n    }\n");
            }
        }
        else
            code.Append("        throw std::invalid_argument(\"Unknown LuminPack Union runtime type.\");\n    }\n");
        code.Append("};\n\n");
    }

    /// <summary>Decodes a selected case into its graph shell, or creates an ordinary case from its payload.</summary>
    /// <param name="item">The concrete case entry and hash.</param>
    /// <param name="json">Whether to use the JSON case codec.</param>
    /// <param name="graph">Whether the reference graph owns ordinary decoded cases.</param>
    private void EmitRttiReadCase(RttiCase item, bool json, bool graph)
    {
        if (item.Tracked)
        {
            if (json)
                code.Append("            return ").Append(item.Json).Append("::read(input);\n");
            else
            {
                string concrete = "case_" + item.Hash;
                code.Append("        {\n            auto* existing = seed && typeid(*seed) == typeid(").Append(concrete)
                    .Append(") ? static_cast<").Append(concrete).Append("*>(seed) : nullptr;\n")
                    .Append("            ").Append(item.Binary).Append("::read_into(input, existing, version, references);\n")
                    .Append("            return existing;\n        }\n");
            }
            return;
        }

        bool depthScope = graph && !json;
        if (depthScope)
            code.Append("        {\n            luminpack::reference_read_state::depth_scope depth(references);\n");
        string adoption = !graph ? "" : json ? "input.references().own(" : "references.own(";
        code.Append("            return ").Append(adoption).Append("std::make_unique<case_").Append(item.Hash).Append(">(");
        if (json)
            code.Append(item.Json).Append("::read(input))");
        else
            code.Append("luminpack::detail::read_codec<").Append(item.Binary).Append(">(input, ")
                .Append(graph ? "version, references))" : "context...))");
        code.Append(graph ? ");\n" : ";\n");
        if (depthScope)
            code.Append("        }\n");
    }

    private readonly record struct RttiCase(string Hash, string Binary, string Json, bool Tracked, string Storage, string? Definition);
}
