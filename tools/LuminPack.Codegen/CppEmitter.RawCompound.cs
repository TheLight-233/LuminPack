using System.Text.Json;

internal sealed partial class CppEmitter
{
    private readonly Dictionary<string, Binding> rawCompounds = new(StringComparer.Ordinal);

    /// <summary>Creates a native carrier whose explicit byte fields preserve CLR padding through C++ copies.</summary>
    /// <param name="formatter">The raw framework formatter.</param>
    /// <param name="profile">The active binary profile used to bind scalar components.</param>
    /// <param name="label">The consuming field used in diagnostics.</param>
    /// <returns>The validated raw aggregate and its matching JSON adapter.</returns>
    private Binding BindRawCompound(JsonElement formatter, int profile, string label)
    {
        if (!formatter.TryGetProperty("rawCompoundStorage", out JsonElement storage) || storage.ValueKind != JsonValueKind.Object ||
            Flag(storage, "tagged"))
            throw Missing(label, "a headerless framework value without a unique proven CLR layout");

        string type = Normalize(Text(formatter, "type"));
        string name = "raw_" + Identifier(type);
        var projected = new List<RawCompoundLeaf>();
        RawCompoundJson logical = ProjectRawCompound(formatter, storage, profile, label, "field", "logical", "logical_codec::value_type", projected);
        StorageLeaf[] leaves = projected.Select(leaf => new StorageLeaf("offsetof(" + name + ", " + leaf.Name + ")", leaf.Width, false)).ToArray();
        int packedWidth = storage.GetProperty("wireSize").GetInt32();
        StorageModel[] models = StorageModels(storage, leaves, packedWidth, label, tagged: false);
        if (models.Length != 1)
            throw Missing(label, "multiple storage models for a headerless framework value");
        if (rawCompounds.TryGetValue(type, out Binding known))
            return known;

        StorageModel model = models[0];
        int[] order = Enumerable.Range(0, leaves.Length).OrderBy(index => model.Offsets[index]).ToArray();
        var physical = new StorageModel(model.Stride, order.Select(index => model.Offsets[index]).ToArray());
        code.Append("struct ").Append(name).Append("\n{\n");
        EmitNativeFields(order.Select(index => projected[index].Declaration).ToArray(),
            order.Select(index => leaves[index]).ToArray(), physical, label);
        code.Append("};\n");
        string modelType = "luminpack::storage_model<" + Integer(leaves.Length) + ">";
        code.Append("static_assert(").Append(modelType).Append("{ sizeof(").Append(name).Append("), {")
            .Append(string.Join(", ", leaves.Select(leaf => leaf.Offset))).Append("} } == ").Append(modelType).Append(ModelInitializer(model))
            .Append(", \"C++ layout does not match the managed native framework storage model\");\n");
        Alias(name + "_binary", "luminpack::raw_struct_codec", new[] { name });
        EmitRawCompoundJson(name, logical, projected);

        var binding = new Binding(name, name + "_binary", name + "_json");
        rawCompounds.Add(type, binding);
        return binding;
    }

    /// <summary>Flattens physical scalar leaves and retains the corresponding logical JSON nesting.</summary>
    /// <param name="formatter">The current pair or tuple formatter.</param>
    /// <param name="storage">The current node of the proven CLR layout.</param>
    /// <param name="profile">The active binary profile for scalar bindings.</param>
    /// <param name="label">The path used in diagnostics.</param>
    /// <param name="prefix">The physical field name before adding the current component.</param>
    /// <param name="logicalValue">The ordered expression addressing the current logical component.</param>
    /// <param name="logicalType">The corresponding C++ logical JSON value type.</param>
    /// <param name="leaves">The scalar leaves in compiler-projected packed order.</param>
    /// <returns>The nested logical JSON codec and initializer.</returns>
    private RawCompoundJson ProjectRawCompound(JsonElement formatter, JsonElement storage, int profile, string label,
        string prefix, string logicalValue, string logicalType, List<RawCompoundLeaf> leaves)
    {
        bool pair = Text(formatter, "definition") == PairDefinition;
        JsonElement[] arguments = pair ? formatter.GetProperty("arguments").EnumerateArray().ToArray() : TupleArguments(formatter, label);
        JsonElement fields = storage.GetProperty("fields");
        if (Normalize(Text(storage, "type")) != Normalize(Text(formatter, "type")) ||
            fields.GetArrayLength() != arguments.Length || pair && arguments.Length != 2)
            throw Missing(label, "inconsistent headerless framework storage types");

        var logical = new RawCompoundJson[arguments.Length];
        int start = leaves.Count;
        for (int index = 0; index < arguments.Length; index++)
        {
            JsonElement argument = arguments[index];
            JsonElement field = fields[index];
            JsonElement child = field.GetProperty("storage");
            string item = pair ? (index == 0 ? "Key" : "Value") : index == 7 ? "Rest" : "Item" + Integer(index + 1);
            string physical = prefix + '_' + item;
            string component = label + '.' + item;
            string access = "std::get<" + Integer(index) + ">(" + logicalValue + ")";
            string spelling = pair ? item.ToLowerInvariant() : item;
            if (Text(field, "name") != spelling || Flag(field, "volatile"))
                throw Missing(component, "an inconsistent raw framework storage field");

            if (Text(argument, "definition") is PairDefinition or TupleDefinition)
            {
                string nestedType = "std::tuple_element_t<" + Integer(index) + ", " + logicalType + ">";
                logical[index] = ProjectRawCompound(argument, child, profile, component, physical, access, nestedType, leaves);
                continue;
            }

            int width = PairScalarWidth(argument);
            if (width <= 0 || !ScalarStorageFieldMatches(field, argument, spelling, width))
                throw Missing(component, "a raw framework component without fixed-width scalar storage");
            Binding scalar = BindFormatter(argument, profile, component);
            leaves.Add(new RawCompoundLeaf(physical, scalar.Type, width, access));
            logical[index] = new RawCompoundJson(scalar.Json, "value." + physical);
        }

        if (leaves.Skip(start).Sum(leaf => leaf.Width) != storage.GetProperty("wireSize").GetInt32())
            throw Missing(label, "inconsistent headerless framework storage width");
        string codec = Apply(pair ? "luminpack::json_pair_codec" : "luminpack::json_tuple_codec", logical.Select(item => item.Codec));
        return new RawCompoundJson(codec, logicalType + "{" + string.Join(", ", logical.Select(item => item.Expression)) + "}");
    }

    /// <summary>Uses the established logical JSON array codec while excluding CLR storage padding.</summary>
    /// <param name="name">The generated native carrier name.</param>
    /// <param name="logical">The logical pair or tuple's JSON codec and initializer.</param>
    /// <param name="fields">The carrier's fields and logical accessor paths.</param>
    private void EmitRawCompoundJson(string name, RawCompoundJson logical, IReadOnlyList<RawCompoundLeaf> fields)
    {
        code.Append("struct ").Append(name).Append("_json\n{\n")
            .Append("    using value_type = ").Append(name).Append(";\n")
            .Append("    using logical_codec = ").Append(logical.Codec).Append(";\n")
            .Append("    static value_type read(auto& input)\n    {\n")
            .Append("        const auto logical = logical_codec::read(input);\n        value_type result{};\n");
        foreach (RawCompoundLeaf field in fields)
            code.Append("        result.").Append(field.Name).Append(" = ").Append(field.LogicalAccess).Append(";\n");
        code.Append("        return result;\n    }\n")
            .Append("    static void write(auto& output, const value_type& value)\n    {\n")
            .Append("        logical_codec::write(output, ").Append(logical.Expression).Append(");\n    }\n};\n\n");
    }

    private readonly record struct RawCompoundLeaf(string Name, string Type, int Width, string LogicalAccess)
    {
        internal string Declaration => Type + " " + Name + "{};";
    }

    private readonly record struct RawCompoundJson(string Codec, string Expression);
}
