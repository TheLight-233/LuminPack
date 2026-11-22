using System.Text.Json;

internal sealed partial class CppEmitter
{
    /// <summary>Retains the existing binding for a homogeneous, densely stored CLI declaration.</summary>
    /// <param name="type">The CLR type name.</param>
    /// <param name="native">The exported native model.</param>
    /// <returns>Whether the dense binding already supports the storage.</returns>
    private bool CanUseDenseNativeBinding(string type, JsonElement native)
    {
        if (!layouts.ContainsKey(type))
            return false;

        int width = native.GetProperty("wireSize").GetInt32();
        if (native.GetProperty("modelSizes").EnumerateArray().Any(size => size.GetInt32() != width))
            return false;
        return native.GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("storage"))
            .All(field => field.GetProperty("fields").GetArrayLength() == 0) &&
            native.GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("storage").GetProperty("wireSize").GetInt32())
                .Distinct().Count() == 1;
    }

    /// <summary>Emits a native aggregate with explicit padding and a compile-time ABI proof.</summary>
    /// <param name="contract">The resolved struct contract.</param>
    /// <param name="storage">The CLR model exported for native transfer.</param>
    /// <param name="name">The generated C++ struct name.</param>
    /// <param name="profile">The profile for the member codecs.</param>
    /// <returns>The native byte-copy and JSON bindings.</returns>
    private Binding EmitNativeStruct(JsonElement contract, JsonElement storage, string name, int profile)
    {
        string owner = Text(contract, "id");
        JsonElement fields = storage.GetProperty("fields");
        if (Flag(contract, "versioned") || Text(contract, "storageKind") != "RawUnmanaged" || Flag(storage, "tagged") ||
            fields.EnumerateArray().Any(field => field.GetProperty("storage").GetProperty("fields").GetArrayLength() != 0))
            throw Missing(owner, "a native model without flat, untagged storage");

        var leaves = new List<StorageLeaf>();
        CollectStorageLeaves(storage, contract, name, owner, null, leaves);
        int width = storage.GetProperty("wireSize").GetInt32();
        if (leaves.Count == 0 || leaves.Sum(leaf => leaf.Width) != width || leaves.Any(leaf => leaf.Boolean))
            throw Missing(owner, "a native storage model without all fixed-width numeric fields");

        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        var jsonMembers = new List<string>();
        foreach (JsonElement member in contract.GetProperty("members").EnumerateArray())
        {
            string property = Text(member, "name");
            string field = "field_" + Identifier(property);
            Binding binding = BindShape(member.GetProperty("shape"), profile, owner + "." + property);
            declarations.Add(property, binding.Type + " " + field + "{};");
            jsonMembers.Add(Apply("luminpack::json_record_member", new[] { Literal(property), "&" + name + "::" + field, binding.Json }));
        }

        string[] physicalFields = fields.EnumerateArray().Select(field => declarations[Text(field, "name")]).ToArray();
        StorageModel[] models = StorageModels(storage, leaves, width, owner, tagged: false);
        if (contract.GetProperty("nativeStrategy").GetInt32() == 0 && models.Length != 1)
            throw Missing(owner, "multiple models for a tag-free Tagged value");

        code.Append("struct ").Append(name).Append("\n{\n");
        if (models.Length == 2)
            code.Append("#if INTPTR_MAX == INT64_MAX\n");
        EmitNativeFields(physicalFields, leaves, models[0], owner);
        if (models.Length == 2)
        {
            code.Append("#else\n");
            EmitNativeFields(physicalFields, leaves, models[1], owner);
            code.Append("#endif\n");
        }
        code.Append("};\n");

        string modelType = "luminpack::storage_model<" + Integer(leaves.Count) + ">";
        string expected = modelType + ModelInitializer(models[0]);
        if (models.Length == 2)
        {
            code.Append("static_assert(sizeof(void*) == 4 || sizeof(void*) == 8);\n");
            expected = "(sizeof(void*) == 8 ? " + expected + " : " + modelType + ModelInitializer(models[1]) + ")";
        }
        code.Append("static_assert(").Append(modelType).Append("{ sizeof(").Append(name).Append("), {")
            .Append(string.Join(", ", leaves.Select(leaf => leaf.Offset))).Append("} } == ").Append(expected)
            .Append(", \"C++ layout does not match the managed native storage model\");\n");
        Alias(name + "_binary", "luminpack::raw_struct_codec", new[] { name });
        Alias(name + "_json", "luminpack::json_record_body", new[] { name }.Concat(jsonMembers));
        code.Append('\n');
        return new Binding(name, name + "_binary", name + "_json", name + "_binary", name + "_json");
    }

    /// <summary>Emits the native fields and names padding bytes so ordinary DTO copies preserve them.</summary>
    /// <param name="fields">The fields in physical declaration order.</param>
    /// <param name="leaves">The scalar widths.</param>
    /// <param name="model">The exact CLR layout model for this target.</param>
    /// <param name="label">The declaration used in diagnostics.</param>
    private void EmitNativeFields(IReadOnlyList<string> fields, IReadOnlyList<StorageLeaf> leaves, StorageModel model, string label)
    {
        int offset = 0;
        for (int field = 0; field < fields.Count; field++)
        {
            int gap = model.Offsets[field] - offset;
            if (gap < 0)
                throw Missing(label, "a native model with overlapping or reordered fields");
            EmitPadding(field, gap);
            code.Append("    ").Append(fields[field]).Append('\n');
            offset = model.Offsets[field] + leaves[field].Width;
        }
        EmitPadding(fields.Count, model.Stride - offset);
    }

    /// <summary>Emits a nonempty padding array at an exported physical gap.</summary>
    /// <param name="index">The gap's zero-based position.</param>
    /// <param name="length">The padded byte count.</param>
    private void EmitPadding(int index, int length)
    {
        if (length > 0)
            code.Append("    std::byte padding_").Append(index).Append('[').Append(length).Append("]{};\n");
    }
}
