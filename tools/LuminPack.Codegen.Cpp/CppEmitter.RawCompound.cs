using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly Dictionary<string, Binding> rawCompounds = new(StringComparer.Ordinal);

    /// <summary>Creates a native carrier whose explicit byte fields preserve CLR padding through C++ copies.</summary>
    /// <param name="formatter">The raw framework formatter.</param>
    /// <param name="arguments">The key/value or item formatters in logical order.</param>
    /// <param name="components">The matching logical component codecs.</param>
    /// <param name="logical">The existing pair or tuple binding that defines JSON order.</param>
    /// <param name="label">The consuming field used in diagnostics.</param>
    /// <returns>The validated raw aggregate and its matching JSON adapter.</returns>
    private Binding BindRawCompound(JsonElement formatter, JsonElement[] arguments, Binding[] components, Binding logical, string label)
    {
        if (!formatter.TryGetProperty("rawCompoundStorage", out JsonElement storage) || storage.ValueKind != JsonValueKind.Object ||
            Flag(storage, "tagged"))
            throw Missing(label, "a headerless framework value without a unique proven CLR layout");

        string type = Normalize(Text(formatter, "type"));
        if (Normalize(Text(storage, "type")) != type || storage.GetProperty("fields").GetArrayLength() != arguments.Length)
            throw Missing(label, "inconsistent headerless framework storage types");
        bool pair = Text(formatter, "definition") == PairDefinition;
        string name = "raw_" + Identifier(type);
        var declarations = new string[arguments.Length];
        var names = new string[arguments.Length];
        var leaves = new StorageLeaf[arguments.Length];
        JsonElement fields = storage.GetProperty("fields");
        for (int index = 0; index < arguments.Length; index++)
        {
            string field = pair ? (index == 0 ? "key" : "value") : index == 7 ? "Rest" : "Item" + Integer(index + 1);
            int width = PairScalarWidth(arguments[index]);
            if (width <= 0 || !ScalarStorageFieldMatches(fields[index], arguments[index], field, width))
                throw Missing(label + "." + field, "a raw framework component without flat fixed-width scalar storage");
            names[index] = pair ? (index == 0 ? "field_Key" : "field_Value") : "field_" + field;
            declarations[index] = components[index].Type + " " + names[index] + "{};";
            leaves[index] = new StorageLeaf("offsetof(" + name + ", " + names[index] + ")", width, false);
        }

        int packedWidth = storage.GetProperty("wireSize").GetInt32();
        if (leaves.Sum(leaf => leaf.Width) != packedWidth)
            throw Missing(label, "inconsistent headerless framework storage width");
        StorageModel[] models = StorageModels(storage, leaves, packedWidth, label, tagged: false);
        if (models.Length != 1)
            throw Missing(label, "multiple storage models for a headerless framework value");
        if (rawCompounds.TryGetValue(type, out Binding known))
            return known;

        StorageModel model = models[0];
        int[] order = Enumerable.Range(0, names.Length).OrderBy(index => model.Offsets[index]).ToArray();
        var physical = new StorageModel(model.Stride, order.Select(index => model.Offsets[index]).ToArray());
        code.Append("struct ").Append(name).Append("\n{\n");
        EmitNativeFields(order.Select(index => declarations[index]).ToArray(), order.Select(index => leaves[index]).ToArray(), physical, label);
        code.Append("};\n");
        string modelType = "luminpack::storage_model<" + Integer(leaves.Length) + ">";
        code.Append("static_assert(").Append(modelType).Append("{ sizeof(").Append(name).Append("), {")
            .Append(string.Join(", ", leaves.Select(leaf => leaf.Offset))).Append("} } == ").Append(modelType).Append(ModelInitializer(model))
            .Append(", \"C++ layout does not match the managed native framework storage model\");\n");
        Alias(name + "_binary", "luminpack::raw_struct_codec", new[] { name });
        EmitRawCompoundJson(name, logical.Json, names);

        var binding = new Binding(name, name + "_binary", name + "_json");
        rawCompounds.Add(type, binding);
        return binding;
    }

    /// <summary>Uses the established logical JSON array codec while excluding CLR storage padding.</summary>
    /// <param name="name">The generated native carrier name.</param>
    /// <param name="logicalCodec">The logical pair or tuple's JSON codec.</param>
    /// <param name="fields">The carrier's field names in logical order.</param>
    private void EmitRawCompoundJson(string name, string logicalCodec, IReadOnlyList<string> fields)
    {
        code.Append("struct ").Append(name).Append("_json\n{\n")
            .Append("    using value_type = ").Append(name).Append(";\n")
            .Append("    using logical_codec = ").Append(logicalCodec).Append(";\n")
            .Append("    static value_type read(auto& input)\n    {\n")
            .Append("        const auto logical = logical_codec::read(input);\n        value_type result{};\n");
        for (int index = 0; index < fields.Count; index++)
            code.Append("        result.").Append(fields[index]).Append(" = std::get<").Append(Integer(index)).Append(">(logical);\n");
        code.Append("        return result;\n    }\n")
            .Append("    static void write(auto& output, const value_type& value)\n    {\n")
            .Append("        logical_codec::write(output, {").Append(string.Join(", ", fields.Select(field => "value." + field)))
            .Append("});\n    }\n};\n\n");
    }
}
