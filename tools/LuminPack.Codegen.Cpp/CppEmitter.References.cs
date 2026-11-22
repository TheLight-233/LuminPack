using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly HashSet<string> declaredReferenceStorage = new(StringComparer.Ordinal);
    private readonly HashSet<string> definedReferenceStorage = new(StringComparer.Ordinal);
    private readonly Queue<(JsonElement Contract, string CodecName, string Storage, int Profile)> trackedRecords = new();

    /// <summary>Declares the pointer and codec types before visiting members that can refer to them.</summary>
    /// <param name="contract">The compiler-projected tracked class.</param>
    /// <param name="key">The normalized class and effective binary profile.</param>
    /// <param name="name">The profile-specific codec prefix.</param>
    /// <param name="storage">The class declaration shared by all profiles.</param>
    /// <returns>The available binding for a reference edge.</returns>
    private Binding DeclareTrackedRecord(JsonElement contract, (string Type, int Profile) key, string name, string storage)
    {
        if (Text(contract, "kind") != "Record")
            throw Missing(key.Type, "reference tracking on a non-record contract");
        if (declaredReferenceStorage.Add(storage))
            code.Append("struct ").Append(storage).Append(";\n");

        var binding = new Binding(storage + "*", name + "_binary", name + "_json");
        code.Append("struct ").Append(binding.Binary).Append("\n{\n    using value_type = ").Append(binding.Type).Append(";\n")
            .Append("    static constexpr std::size_t minimum_size = 1;\n")
            .Append("    static value_type read(luminpack::reader&, luminpack::version_state&, luminpack::reference_read_state&);\n")
            .Append("    static void read_into(luminpack::reader&, value_type&, luminpack::version_state&, luminpack::reference_read_state&);\n")
            .Append("    static void write(luminpack::writer&, const value_type&, luminpack::version_state&, luminpack::reference_write_state&);\n")
            .Append("};\nstruct ").Append(binding.Json).Append("\n{\n    using value_type = ").Append(binding.Type).Append(";\n")
            .Append("    static value_type read(auto&);\n    static void read_into(auto&, value_type&);\n")
            .Append("    static void write(auto&, const value_type&);\n};\n");
        generated.Add(key, binding);
        trackedRecords.Enqueue((contract, name, storage, key.Profile));
        return binding;
    }

    /// <summary>Defines the deferred record bodies after every enclosing by-value dependency has been emitted.</summary>
    private void EmitTrackedRecords()
    {
        while (trackedRecords.TryDequeue(out var item))
            EmitRecord(item.Contract, item.CodecName, item.Profile, item.Storage);
    }

    /// <summary>Connects a graph-owned DTO shell to its existing Object or Version body and its JSON names.</summary>
    /// <param name="name">The profile-specific codec prefix.</param>
    /// <param name="storage">The complete DTO type.</param>
    /// <param name="profile">The effective binary profile.</param>
    /// <param name="version">Whether the binary body is version tolerant.</param>
    /// <param name="binaryMembers">The existing ordered body codecs.</param>
    /// <param name="jsonMembers">The projected JSON names, aliases and field codecs.</param>
    private void EmitTrackedRecordCodec(string name, string storage, int profile, bool version,
        IReadOnlyList<string> binaryMembers, IReadOnlyList<JsonMember> jsonMembers)
    {
        string binary = name + "_binary";
        string json = name + "_json";
        string binaryBody = binary + "_body";
        if (version)
            Alias(binaryBody, "luminpack::version_codec", new[] { storage, Profile(profile) }.Concat(binaryMembers));
        else
            Alias(binaryBody, "luminpack::record_codec", new[] { storage }.Concat(binaryMembers));

        string trackedBinary = $"luminpack::tracked_binary_codec<{storage}, {binaryBody}, {Integer(binaryMembers.Count)}>";
        code.Append("inline ").Append(binary).Append("::value_type ").Append(binary)
            .Append("::read(luminpack::reader& input, luminpack::version_state& version, luminpack::reference_read_state& references)\n")
            .Append("{\n    return ").Append(trackedBinary).Append("::read(input, version, references);\n}\n")
            .Append("inline void ").Append(binary)
            .Append("::read_into(luminpack::reader& input, value_type& value, luminpack::version_state& version, luminpack::reference_read_state& references)\n")
            .Append("{\n    ").Append(trackedBinary).Append("::read_into(input, value, version, references);\n}\n")
            .Append("inline void ").Append(binary)
            .Append("::write(luminpack::writer& output, const value_type& value, luminpack::version_state& version, luminpack::reference_write_state& references)\n")
            .Append("{\n    ").Append(trackedBinary).Append("::write(output, value, version, references);\n}\n");

        string jsonBody = json + "_body";
        EmitJsonBody(jsonBody, storage, jsonMembers, tracked: true);
        code.Append("inline ").Append(json).Append("::value_type ").Append(json).Append("::read(auto& input)\n")
            .Append("{\n    return ").Append(jsonBody).Append("::read(input);\n}\n")
            .Append("inline void ").Append(json).Append("::read_into(auto& input, value_type& value)\n")
            .Append("{\n    ").Append(jsonBody).Append("::read_into(input, value);\n}\n")
            .Append("inline void ").Append(json).Append("::write(auto& output, const value_type& value)\n")
            .Append("{\n    ").Append(jsonBody).Append("::write(output, value);\n}\n\n");
    }
}
