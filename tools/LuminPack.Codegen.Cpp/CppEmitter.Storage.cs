using System.Globalization;
using System.Text.Json;

internal sealed partial class CppEmitter
{
    /// <summary>Emits a fixed-layout aggregate from the storage plan already selected by Next.</summary>
    /// <param name="contract">The portable struct contract.</param>
    /// <param name="name">The name of the C++ struct.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <returns>The standalone and collection bindings.</returns>
    private Binding EmitPortableStruct(JsonElement contract, string name, int profile)
    {
        string owner = Text(contract, "id");
        JsonElement storage = contract.GetProperty("storage");
        if (Flag(contract, "versioned") || storage.ValueKind != JsonValueKind.Object)
            throw Missing(owner, "a portable struct without its unmanaged storage model");

        var declarations = new List<string>();
        var jsonMembers = new List<JsonMember>();
        foreach (JsonElement member in contract.GetProperty("members").EnumerateArray())
        {
            string property = Text(member, "name");
            string field = "field_" + Identifier(property);
            Binding binding = BindShape(member.GetProperty("shape"), profile, owner + "." + property);
            declarations.Add("    " + binding.Type + " " + field + "{};");
            jsonMembers.Add(new JsonMember(property, "&" + name + "::" + field, binding.Json, []));
        }

        var leaves = new List<StorageLeaf>();
        CollectStorageLeaves(storage, contract, name, owner, null, leaves);
        int count = leaves.Count;
        int width = storage.GetProperty("wireSize").GetInt32();
        if (count == 0 || leaves.Sum(leaf => leaf.Width) != width)
            throw Missing(owner, "a portable storage plan whose fields do not match its packed width");

        code.Append("struct ").Append(name).Append("\n{\n");
        foreach (string declaration in declarations)
            code.Append(declaration).Append('\n');
        code.Append("};\n");

        string widths = "{" + string.Join(", ", leaves.Select(leaf => Integer(leaf.Width))) + "}";
        string local = "{ sizeof(" + name + "), {" + string.Join(", ", leaves.Select(leaf => leaf.Offset)) + "} }";
        string packed = name + "_packed_layout";
        code.Append("inline constexpr luminpack::packed_layout<").Append(count).Append("> ").Append(packed)
            .Append("{ ").Append(widths).Append(", ").Append(local).Append(" };\n");
        string booleans = EmitBooleanFields(name, leaves);
        Alias(name + "_binary", "luminpack::packed_struct_codec", new[] { name, packed }.Concat(booleans.Length == 0 ? [] : new[] { booleans }));
        EmitJsonBody(name + "_json", name, jsonMembers);

        string sequenceLayout = packed;
        if (Flag(storage, "tagged"))
        {
            sequenceLayout = name + "_tagged_layout";
            string[] models = StorageModels(storage, leaves, width, owner, tagged: true).Select(ModelInitializer).ToArray();
            code.Append("inline constexpr luminpack::tagged_layout<").Append(count).Append(", ")
                .Append(models.Length).Append("> ").Append(sequenceLayout).Append("{ ").Append(widths).Append(", ")
                .Append(local).Append(", {{\n    ").Append(string.Join(",\n    ", models)).Append("\n}} };\n");
        }
        string sequence = name + "_sequence_binary";
        Alias(sequence, "luminpack::storage_list_codec", new[] { name, sequenceLayout }.Concat(booleans.Length == 0 ? [] : new[] { booleans }));
        code.Append('\n');
        return new Binding(name, name + "_binary", name + "_json", name + "_binary", name + "_json", sequence);
    }

    /// <summary>Flattens the physical fields in the normalized layout's packed byte order.</summary>
    /// <param name="storage">The selected nested storage plan.</param>
    /// <param name="contract">The declaration used for the C++ field names and types.</param>
    /// <param name="type">The generated C++ type expression.</param>
    /// <param name="label">The field or declaration used in diagnostics.</param>
    /// <param name="prefix">The actual C++ offsets of the containing fields, if any.</param>
    /// <param name="leaves">Accumulates the scalar leaves.</param>
    private void CollectStorageLeaves(JsonElement storage, JsonElement contract, string type, string label,
        string? prefix, List<StorageLeaf> leaves)
    {
        JsonElement fields = storage.GetProperty("fields");
        var members = contract.GetProperty("members").EnumerateArray().ToDictionary(member => Text(member, "name"), StringComparer.Ordinal);
        if (fields.GetArrayLength() != members.Count)
            throw Missing(label, "a portable struct with hidden physical storage");
        foreach (JsonElement field in fields.EnumerateArray())
        {
            string name = Text(field, "name");
            string fieldLabel = label + "." + name;
            if (!members.Remove(name, out JsonElement member) || Flag(field, "volatile") || Flag(member, "compressed"))
                throw Missing(fieldLabel, "an inaccessible, volatile, or compressed storage field");

            JsonElement shape = member.GetProperty("shape");
            JsonElement child = field.GetProperty("storage");
            string managedType = Normalize(Text(shape, "type"));
            if (Normalize(Text(child, "type")) != managedType)
                throw Missing(fieldLabel, "inconsistent field and storage types");

            string cppField = "field_" + Identifier(name);
            string offset = prefix + "offsetof(" + type + ", " + cppField + ")";
            if (child.GetProperty("fields").GetArrayLength() != 0)
            {
                if (!contracts.TryGetValue(managedType, out JsonElement nested) || Text(nested, "kind") != "Struct")
                    throw Missing(fieldLabel, "a nested storage field without a generated struct");
                CollectStorageLeaves(child, nested, "decltype(" + type + "::" + cppField + ")", fieldLabel, offset + " + ", leaves);
            }
            else
            {
                int width = child.GetProperty("wireSize").GetInt32();
                bool boolean = managedType is "bool" or "System.Boolean";
                if (Text(shape, "kind") is not ("scalar" or "enum") || Flag(shape, "volatile") ||
                    width <= 0 || shape.GetProperty("size").GetInt32() != width ||
                    Scalar(managedType) == null && !boolean && !enums.ContainsKey(managedType))
                    throw Missing(fieldLabel, "an unsupported packed scalar field");
                leaves.Add(new StorageLeaf(offset, width, boolean));
            }
        }
    }

    /// <summary>Preserves the ordered CLR models and proves each contiguous scalar field span.</summary>
    /// <param name="storage">The selected storage model.</param>
    /// <param name="leaves">The flattened fields in packed order.</param>
    /// <param name="width">The packed byte width.</param>
    /// <param name="label">The declaration used in diagnostics.</param>
    /// <param name="tagged">Whether the models are ordered Tagged variants.</param>
    /// <returns>The proven models in their original order.</returns>
    private static StorageModel[] StorageModels(JsonElement storage, IReadOnlyList<StorageLeaf> leaves, int width, string label,
        bool tagged)
    {
        JsonElement sizes = storage.GetProperty("modelSizes");
        JsonElement byteOffsets = storage.GetProperty("modelOffsets");
        int count = sizes.GetArrayLength();
        if (count < (tagged ? 2 : 1) || count > (tagged ? 250 : 2) || count != byteOffsets.GetArrayLength())
            throw Missing(label, "invalid storage model counts");

        var models = new StorageModel[count];
        for (int model = 0; model < models.Length; model++)
        {
            int stride = sizes[model].GetInt32();
            int[] offsets = byteOffsets[model].EnumerateArray().Select(offset => offset.GetInt32()).ToArray();
            if (offsets.Length != width || offsets.Any(offset => offset < 0 || offset >= stride) || offsets.Distinct().Count() != width)
                throw Missing(label, "invalid storage model byte offsets");

            int position = 0;
            var fieldOffsets = new int[leaves.Count];
            for (int field = 0; field < leaves.Count; field++)
            {
                int start = offsets[position];
                for (int index = 0; index < leaves[field].Width; index++)
                    if (offsets[position++] != start + index)
                        throw Missing(label, "a model with non-contiguous field storage");
                fieldOffsets[field] = start;
            }
            models[model] = new StorageModel(stride, fieldOffsets);
        }
        return models;
    }

    /// <summary>Emits the Boolean fields requiring wire validation before an aggregate copy.</summary>
    /// <param name="name">The C++ struct name.</param>
    /// <param name="leaves">The packed-order scalar leaves.</param>
    /// <returns>The Boolean field array name, or an empty string if no checks are required.</returns>
    private string EmitBooleanFields(string name, IReadOnlyList<StorageLeaf> leaves)
    {
        string[] fields = Enumerable.Range(0, leaves.Count).Where(index => leaves[index].Boolean).Select(Integer).ToArray();
        if (fields.Length == 0)
            return string.Empty;
        string symbol = name + "_boolean_fields";
        code.Append("inline constexpr std::array<std::size_t, ").Append(fields.Length).Append("> ")
            .Append(symbol).Append("{ ").Append(string.Join(", ", fields)).Append(" };\n");
        return symbol;
    }

    /// <summary>Formats a nonnegative storage fact in invariant C++ syntax.</summary>
    /// <param name="value">The metadata width or offset.</param>
    /// <returns>The decimal literal.</returns>
    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Spells a validated CLR storage model as a C++ initializer.</summary>
    /// <param name="model">The stride and scalar field offsets.</param>
    /// <returns>A C++ initializer in declaration order.</returns>
    private static string ModelInitializer(StorageModel model) =>
        "{ " + Integer(model.Stride) + ", {" + string.Join(", ", model.Offsets.Select(Integer)) + "} }";

    private readonly record struct StorageLeaf(string Offset, int Width, bool Boolean);
    private readonly record struct StorageModel(int Stride, int[] Offsets);
}
