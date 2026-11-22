using System.Text.Json;

internal sealed partial class CppEmitter
{
    private const string PairDefinition = "System.Collections.Generic.KeyValuePair";

    /// <summary>Binds the standalone key and value operations in their existing wire order.</summary>
    /// <param name="formatter">The normalized CLR pair formatter.</param>
    /// <param name="profile">The effective binary profile.</param>
    /// <param name="label">The member or type used in diagnostics.</param>
    /// <param name="collection">Whether the pair is an element of a binary array or list.</param>
    /// <returns>The C++ pair type and codecs.</returns>
    private Binding BindPair(JsonElement formatter, int profile, string label, bool collection = false)
    {
        JsonElement components = formatter.GetProperty("arguments");
        if (components.GetArrayLength() != 2 || Flag(formatter, "raw"))
            throw Missing(label, "an unsupported standalone KeyValuePair formatter");
        if (collection && formatter.TryGetProperty("rawCompoundStorage", out _))
            return BindRawCompound(formatter, profile, label);

        Binding key = BindFormatter(components[0], profile, label);
        Binding value = BindFormatter(components[1], profile, label);
        string binary = Apply("luminpack::pair_codec", new[] { key.Binary, value.Binary });
        string json = Apply("luminpack::json_pair_codec", new[] { key.Json, value.Json });
        var pair = new Binding(binary + "::value_type", binary, json);
        return collection ? PairCollection(formatter, [components[0], components[1]], [key, value], pair, label) : pair;
    }

    /// <summary>Checks ordinary pair collection storage or binds the exported Tagged models.</summary>
    /// <param name="formatter">The normalized CLR pair element.</param>
    /// <param name="arguments">The normalized key and value formatters.</param>
    /// <param name="components">The emitted key and value codecs.</param>
    /// <param name="pair">The logical element binding.</param>
    /// <param name="label">The collection used in diagnostics.</param>
    /// <returns>The element binding and any required native storage specialization.</returns>
    private Binding PairCollection(JsonElement formatter, JsonElement[] arguments, Binding[] components, Binding pair, string label)
    {
        JsonElement layout = formatter.GetProperty("layout");
        if (layout.ValueKind == JsonValueKind.Null && !Flag(formatter, "rawCollection"))
            return pair;

        int keyWidth = PairScalarWidth(arguments[0]);
        int valueWidth = PairScalarWidth(arguments[1]);
        if (keyWidth > 0 && valueWidth > 0)
        {
            // A CLR pair has consecutive key and value fields. Equal scalar sizes require no intervening or tail padding.
            if (layout.ValueKind == JsonValueKind.Null && keyWidth == valueWidth)
                return pair;

            if (layout.ValueKind == JsonValueKind.Object && layout.GetProperty("wireSize").GetInt32() == keyWidth + valueWidth)
            {
                JsonElement fields = layout.GetProperty("fields");
                if (fields.GetArrayLength() == 2 && ScalarStorageFieldMatches(fields[0], arguments[0], "key", keyWidth) &&
                    ScalarStorageFieldMatches(fields[1], arguments[1], "value", valueWidth))
                {
                    if (!Flag(layout, "tagged"))
                        return pair;

                    StorageLeaf[] leaves = [new(string.Empty, keyWidth, false), new(string.Empty, valueWidth, false)];
                    StorageModel[] models = StorageModels(layout, leaves, keyWidth + valueWidth, label, tagged: true);
                    string sequence = Apply("luminpack::tagged_pair_list_codec", components.Select(item => item.Binary).Concat(
                        models.Select(static model => "luminpack::storage_model<2>" + ModelInitializer(model))));
                    return pair with { SequenceBinary = sequence };
                }
            }
        }

        if (layout.ValueKind == JsonValueKind.Null && Flag(formatter, "rawCollection"))
            throw Missing(label, "a headerless framework value without a unique proven CLR layout");
        throw Missing(label, "a KeyValuePair collection with unproven CLR storage");
    }

    /// <summary>Verifies that a raw compound storage leaf represents the corresponding closed formatter.</summary>
    /// <param name="field">The physical field in the CLR storage plan.</param>
    /// <param name="component">The matching key or value formatter.</param>
    /// <param name="name">The expected field name.</param>
    /// <param name="width">The formatter's fixed wire width.</param>
    /// <returns>Whether the field has the required scalar storage.</returns>
    private static bool ScalarStorageFieldMatches(JsonElement field, JsonElement component, string name, int width)
    {
        JsonElement storage = field.GetProperty("storage");
        return Text(field, "name") == name && !Flag(field, "volatile") && storage.GetProperty("fields").GetArrayLength() == 0 &&
            Normalize(Text(storage, "type")) == Normalize(Text(component, "type")) && storage.GetProperty("wireSize").GetInt32() == width;
    }

    /// <summary>Finds the width of a portable scalar or enum in a pair's storage proof.</summary>
    /// <param name="component">The normalized CLR component formatter.</param>
    /// <returns>The full fixed width, or zero for a variable or unsupported component.</returns>
    private static int PairScalarWidth(JsonElement component) =>
        Scalar(Normalize(component.GetProperty("underlying").GetString() ?? Text(component, "type"))) switch
        {
            "std::int8_t" or "std::uint8_t" => 1,
            "std::int16_t" or "std::uint16_t" or "char16_t" => 2,
            "std::int32_t" or "std::uint32_t" or "float" => 4,
            "std::int64_t" or "std::uint64_t" or "double" => 8,
            _ => 0
        };
}
