using System.Text;
using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly Dictionary<string, int> rttiSlotPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<int, StringBuilder> rttiSlotDeclarations = new();
    private readonly HashSet<(string Owner, string Slot, string Parameters)> emittedRttiSlots = new();

    /// <summary>Checks the producer's compiled tier, including older manifests of closed roots.</summary>
    /// <param name="contract">The normalized Union declaration.</param>
    /// <returns>Whether to use the virtual writer tier.</returns>
    private static bool UsesVirtualRttiSlots(JsonElement contract) =>
        contract.TryGetProperty("unionTier", out JsonElement tier) && tier.ValueKind == JsonValueKind.String
            ? tier.GetString() == "Virtual"
            : contract.GetProperty("cases").GetArrayLength() > 32;

    /// <summary>Records where a concrete DTO can later declare reachable virtual slots.</summary>
    /// <param name="storage">The C++ name of the record storage.</param>
    private void MarkRttiCaseSlots(string storage)
    {
        if (rttiBases != null)
            rttiSlotPositions.Add(storage, code.Length);
    }

    /// <summary>Spells the default slot or the C++ specialization of the profile slot.</summary>
    /// <param name="baseName">The C++ name of the shared polymorphic base.</param>
    /// <param name="contract">The root's producer profile declaration.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <returns>The generated method name.</returns>
    private static string VirtualRttiBinarySlot(string baseName, JsonElement contract, int profile) =>
        baseName + "_write_binary_" + (profile == contract.GetProperty("profile").GetProperty("binary").GetInt32()
            ? "default" : "profile" + Integer(profile));

    /// <summary>Declares one virtual write entry, keeping unlisted subclasses on the registry fallback.</summary>
    /// <param name="owner">The base or concrete DTO receiving the method.</param>
    /// <param name="slot">The root-specific method name.</param>
    /// <param name="parameters">The typed C++ argument declarations without names.</param>
    /// <param name="isOverride">Whether the method belongs to a generated case.</param>
    /// <returns>Whether this declaration was added.</returns>
    private bool DeclareRttiSlot(string owner, string slot, string parameters, bool isOverride)
    {
        if (!emittedRttiSlots.Add((owner, slot, parameters)))
            return false;
        int position = rttiSlotPositions[owner];
        if (!rttiSlotDeclarations.TryGetValue(position, out StringBuilder? declarations))
            rttiSlotDeclarations.Add(position, declarations = new StringBuilder());
        declarations.Append(isOverride ? "    bool " : "    virtual bool ").Append(slot).Append('(').Append(parameters)
            .Append(isOverride ? ") override;\n" : ") { return false; }\n");
        return true;
    }

    /// <summary>Inserts virtual declarations after all of the reachable profile bindings are known.</summary>
    private void InsertRttiSlotDeclarations()
    {
        foreach (var item in rttiSlotDeclarations.OrderByDescending(static item => item.Key))
            code.Insert(item.Key, item.Value.ToString());
    }

    /// <summary>Emits each concrete write slot and the shared cached selector for closed generic groups.</summary>
    /// <param name="name">The current Union profile's C++ binding prefix.</param>
    /// <param name="baseName">The shared polymorphic base.</param>
    /// <param name="cases">The ordered concrete cases and their codec entries.</param>
    /// <param name="binarySlot">The active default or compiled binary slot.</param>
    /// <param name="jsonSlot">The JSON slot overloaded by encoding.</param>
    private void EmitVirtualRttiSlots(string name, string baseName, IReadOnlyList<RttiCase> cases, string binarySlot, string jsonSlot)
    {
        bool graphDocument = declaredReferenceStorage.Count != 0;
        string binaryParameters = "luminpack::writer&, luminpack::version_state&, luminpack::reference_write_state*";
        string binaryDefinition = "luminpack::writer& output, luminpack::version_state& version, " +
            "[[maybe_unused]] luminpack::reference_write_state* references";
        string cursor = graphDocument ? "luminpack::reference_json_writer" : "luminpack::basic_json_writer";
        string[] jsonParameters = [$"{cursor}<luminpack::json_encoding::utf8>&", $"{cursor}<luminpack::json_encoding::utf16>&"];
        DeclareRttiSlot(baseName, binarySlot, binaryParameters, isOverride: false);
        foreach (string parameter in jsonParameters)
            DeclareRttiSlot(baseName, jsonSlot, parameter, isOverride: false);

        foreach (var group in cases.GroupBy(static item => item.Definition, StringComparer.Ordinal))
        {
            string? selector = group.Key == null ? null : name + "_virtual_" + Identifier(group.Key);
            if (selector != null)
                EmitVirtualRttiGroup(selector, group, binaryDefinition, graphDocument);
            foreach (RttiCase item in group)
            {
                if (DeclareRttiSlot(item.Storage, binarySlot, binaryParameters, isOverride: true))
                    EmitVirtualRttiOverride(item, binarySlot, binaryDefinition, selector, json: false, graphDocument);
                foreach (string parameter in jsonParameters)
                {
                    if (DeclareRttiSlot(item.Storage, jsonSlot, parameter, isOverride: true))
                        EmitVirtualRttiOverride(item, jsonSlot, parameter + " output", selector, json: true, graphDocument);
                }
            }
        }
    }

    /// <summary>Writes an ordinary override directly or routes a generic closure through its shared selector.</summary>
    /// <param name="item">The concrete case receiving the override.</param>
    /// <param name="slot">The root-specific write slot.</param>
    /// <param name="parameters">The concrete writer and state parameters.</param>
    /// <param name="selector">The generic group selector or null for an ordinary case.</param>
    /// <param name="json">Whether this writes the JSON case.</param>
    /// <param name="graphDocument">Whether the document can write tracked references.</param>
    private void EmitVirtualRttiOverride(RttiCase item, string slot, string parameters, string? selector, bool json, bool graphDocument)
    {
        code.Append("inline bool ").Append(item.Storage).Append("::").Append(slot).Append('(').Append(parameters).Append(")\n{\n");
        if (selector != null)
        {
            code.Append("    return ").Append(selector).Append('<').Append(item.Storage)
                .Append(json ? ">::write_json(output, *this);\n" : ">::write_binary(*this, output, version, references);\n");
        }
        else
        {
            EmitVirtualRttiWriteCase(item, "this", "    ", json, graphDocument);
            code.Append("    return true;\n");
        }
        code.Append("}\n\n");
    }

    /// <summary>Shares the closed-type cached case hash across the two generic virtual writers.</summary>
    /// <param name="selector">The C++ name of the group selector.</param>
    /// <param name="group">The closed case bindings of one generic definition.</param>
    /// <param name="binaryParameters">The cursor followed by optional binary context.</param>
    /// <param name="graphDocument">Whether this document can write tracked references.</param>
    private void EmitVirtualRttiGroup(string selector, IEnumerable<RttiCase> group, string binaryParameters, bool graphDocument)
    {
        RttiCase[] cases = group.ToArray();
        code.Append("template<class Closed>\nstruct ").Append(selector)
            .Append("\n{\n    static constexpr auto hash = luminpack::detail::virtual_union_case_hash<Closed");
        foreach (RttiCase item in cases)
        {
            code.Append(",\n        luminpack::detail::virtual_union_case<").Append(item.Storage).Append(", ").Append(item.Hash).Append('>');
        }
        code.Append(">;\n\n    static bool write_binary(Closed& value, ").Append(binaryParameters).Append(")\n");
        EmitVirtualRttiGroupSwitch(cases, json: false, graphDocument);
        code.Append("\n    static bool write_json(auto& output, Closed& value)\n");
        EmitVirtualRttiGroupSwitch(cases, json: true, graphDocument);
        code.Append("};\n\n");
    }

    /// <summary>Selects a generic case with the shared case hash after the virtual lookup.</summary>
    /// <param name="cases">The closures and their canonical discriminator values.</param>
    /// <param name="json">Whether to emit a JSON payload writer.</param>
    /// <param name="graphDocument">Whether this document can write tracked references.</param>
    private void EmitVirtualRttiGroupSwitch(IReadOnlyList<RttiCase> cases, bool json, bool graphDocument)
    {
        code.Append("    {\n        switch (hash)\n        {\n");
        foreach (RttiCase item in cases)
        {
            code.Append("        case ").Append(item.Hash).Append(":\n            if constexpr (std::same_as<Closed, ")
                .Append(item.Storage).Append(">)\n            {\n");
            EmitVirtualRttiWriteCase(item, "&value", "                ", json, graphDocument);
            code.Append("                return true;\n            }\n            break;\n");
        }
        code.Append("        }\n        return false;\n    }\n");
    }

    /// <summary>Spells a virtual slot's concrete case call with the existing document context.</summary>
    /// <param name="item">The selected case and its payload codec.</param>
    /// <param name="pointer">The exact receiver as a pointer expression.</param>
    /// <param name="indent">The C++ indentation of the current statements.</param>
    /// <param name="json">Whether to emit the JSON envelope.</param>
    /// <param name="graphDocument">Whether the document can write tracked references.</param>
    private void EmitVirtualRttiWriteCase(RttiCase item, string pointer, string indent, bool json, bool graphDocument)
    {
        string value = item.Tracked ? pointer : "*(" + pointer + ")";
        string context = graphDocument ? "version, references" : "version";
        EmitRttiWriteCaseBody(item, value, indent, json, context, virtualGraph: graphDocument);
    }

    /// <summary>Writes a generated case's unchanged discriminator and payload.</summary>
    /// <param name="item">The selected case and its payload codecs.</param>
    /// <param name="value">The concrete object or tracked pointer expression.</param>
    /// <param name="indent">The C++ indentation of the current statements.</param>
    /// <param name="json">Whether to emit the JSON envelope.</param>
    /// <param name="context">The active binary state arguments.</param>
    /// <param name="virtualGraph">Whether the virtual entry forwards an optional reference state.</param>
    private void EmitRttiWriteCaseBody(RttiCase item, string value, string indent, bool json, string context, bool virtualGraph = false)
    {
        if (json)
        {
            code.Append(indent).Append("output.token('{');\n")
                .Append(indent).Append("output.template write_property<luminpack::detail::json_union_type_name>();\n")
                .Append(indent).Append("output.write_number(").Append(item.Hash).Append(");\n")
                .Append(indent).Append("output.token(',');\n")
                .Append(indent).Append("output.template write_property<luminpack::detail::json_union_value_name>();\n")
                .Append(indent).Append(item.Json).Append("::write(output, ").Append(value).Append(");\n")
                .Append(indent).Append("output.token('}');\n");
            return;
        }

        code.Append(indent).Append("output.write(").Append(item.Hash).Append(");\n");
        string write = virtualGraph ? "luminpack::detail::write_virtual_union_case_payload<" : "luminpack::detail::write_codec<";
        code.Append(indent).Append(write).Append(item.Binary).Append(">(output, ").Append(value).Append(", ").Append(context).Append(");\n");
    }
}
