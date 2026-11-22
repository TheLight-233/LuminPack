using System.Globalization;
using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly HashSet<string> emittedRttiBases = new(StringComparer.Ordinal);
    private readonly HashSet<string> emittedRttiRegistrations = new(StringComparer.Ordinal);

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
            code.Append("struct ").Append(name).Append("\n{\n    virtual ~").Append(name).Append("() = default;\n};\n\n");
    }

    /// <summary>Projects Next's closed exact-dispatch tier as C++ type comparisons and a hash switch.</summary>
    /// <param name="contract">The Union declaration with its canonical hashes and case order.</param>
    /// <param name="name">The name of this profile's dispatch.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <returns>The Binary and JSON owning Union bindings.</returns>
    private Binding EmitRttiUnion(JsonElement contract, string name, int profile)
    {
        string owner = Text(contract, "id");
        string baseName = RttiBaseName(contract);
        EmitRttiBase(baseName);
        JsonElement cases = contract.GetProperty("cases");
        if (cases.GetArrayLength() is < 1 or > 32)
            throw Missing(owner, "a case count outside Next's closed exact-dispatch tier");

        var bindings = new List<RttiCase>(cases.GetArrayLength());
        var hashes = new HashSet<uint>();
        foreach (JsonElement item in cases.EnumerateArray().OrderBy(static item => item.GetProperty("tag").GetInt32()))
        {
            RejectOpaqueUnionCase(item);
            string type = Normalize(Text(item, "type"));
            if (!contracts.TryGetValue(type, out JsonElement declaration) || Text(declaration, "kind") != "Record")
                throw Missing(Text(item, "id"), "an RTTI case without a class DTO");
            Binding member = BindContract(declaration, profile);
            if (member.Payload == null || member.JsonPayload == null)
                throw Missing(Text(item, "id"), "an RTTI case without a positional payload codec");
            uint hash = item.GetProperty("hash").GetUInt32();
            if (hash == 0 || hash == uint.MaxValue || !hashes.Add(hash))
                throw Missing(owner, "a reserved or duplicate RTTI Union hash");
            bindings.Add(new RttiCase(hash.ToString(CultureInfo.InvariantCulture) + "u", member.Payload, member.JsonPayload));
        }

        bool registered = Flag(contract, "unionRegistration");
        EmitRttiDispatch(name + "_binary_dispatch", baseName, bindings, json: false, registered);
        EmitRttiDispatch(name + "_json_dispatch", baseName, bindings, json: true, registered);
        string policy = registered ? EmitRttiRegistration(contract, baseName, cases) : baseName;
        Alias(name + "_binary", registered ? "luminpack::registered_polymorphic_union_codec" : "luminpack::polymorphic_union_codec",
            new[] { policy, name + "_binary_dispatch" });
        Alias(name + "_json", registered ? "luminpack::registered_json_polymorphic_union_codec" : "luminpack::json_polymorphic_union_codec",
            new[] { policy, name + "_json_dispatch" });
        code.Append('\n');
        return new Binding(name + "_binary::value_type", name + "_binary", name + "_json");
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
    private void EmitRttiDispatch(string name, string baseName, IReadOnlyList<RttiCase> cases, bool json, bool registered)
    {
        bool tryRead = json && registered;
        code.Append("struct ").Append(name).Append("\n{\n");
        foreach (RttiCase item in cases)
            code.Append("    using case_").Append(item.Hash).Append(" = ").Append(item.Binary).Append("::value_type;\n");
        code.Append('\n');
        if (!json)
            code.Append("    template<class... Context>\n");
        code.Append("    static std::unique_ptr<").Append(baseName).Append(tryRead ? "> try_read(" : "> read(")
            .Append(json ? "auto& input, std::uint32_t hash)" : "luminpack::reader& input, std::uint32_t hash, Context&... context)")
            .Append("\n    {\n        switch (hash)\n        {\n");
        foreach (RttiCase item in cases)
        {
            code.Append("        case ").Append(item.Hash).Append(":\n            return std::make_unique<case_").Append(item.Hash).Append(">(");
            if (json)
                code.Append(item.Json).Append("::read(input));\n");
            else
                code.Append("luminpack::detail::read_codec<").Append(item.Binary).Append(">(input, context...));\n");
        }
        code.Append("        default:\n").Append(tryRead ? "            return {};\n" :
            "            throw luminpack::wire_error(\"Unknown LuminPack Union case hash.\");\n").Append("        }\n    }\n\n");
        if (tryRead)
        {
            code.Append("    static std::unique_ptr<").Append(baseName).Append("> read(auto& input, std::uint32_t hash)\n")
                .Append("    {\n        if (auto value = try_read(input, hash))\n            return value;\n")
                .Append("        throw luminpack::wire_error(\"Unknown LuminPack Union case hash.\");\n    }\n\n");
        }

        if (!json)
            code.Append("    template<class... Context>\n");
        string writeParameters = (json ? "auto& output, const " : "luminpack::writer& output, const ") +
            baseName + (json ? "& value)" : "& value, Context&... context)");
        code.Append(registered ? "    static bool try_write(" : "    static void write(").Append(writeParameters)
            .Append("\n    {\n        const std::type_info& type = typeid(value);\n");
        string branch = "if";
        foreach (RttiCase item in cases)
        {
            code.Append("        ").Append(branch).Append(" (type == typeid(case_").Append(item.Hash).Append("))\n        {\n");
            if (json)
            {
                code.Append("            output.token('{');\n")
                    .Append("            output.template write_property<luminpack::detail::json_union_type_name>();\n")
                    .Append("            output.write_number(").Append(item.Hash).Append(");\n")
                    .Append("            output.token(',');\n")
                    .Append("            output.template write_property<luminpack::detail::json_union_value_name>();\n")
                    .Append("            ").Append(item.Json).Append("::write(output, static_cast<const case_").Append(item.Hash).Append("&>(value));\n")
                    .Append("            output.token('}');\n");
            }
            else
            {
                code.Append("            output.write(").Append(item.Hash).Append(");\n")
                    .Append("            luminpack::detail::write_codec<").Append(item.Binary).Append(">(output, static_cast<const case_")
                    .Append(item.Hash).Append("&>(value), context...);\n");
            }
            code.Append(registered ? "            return true;\n        }\n" : "            return;\n        }\n");
            branch = "else if";
        }
        if (registered)
        {
            code.Append("        return false;\n    }\n\n");
            if (!json)
                code.Append("    template<class... Context>\n");
            code.Append("    static void write(").Append(writeParameters).Append("\n    {\n")
                .Append(json ? "        if (!try_write(output, value))\n" : "        if (!try_write(output, value, context...))\n")
                .Append("            throw std::invalid_argument(\"Unknown LuminPack Union runtime type.\");\n    }\n");
        }
        else
            code.Append("        throw std::invalid_argument(\"Unknown LuminPack Union runtime type.\");\n    }\n");
        code.Append("};\n\n");
    }

    private readonly record struct RttiCase(string Hash, string Binary, string Json);
}
