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

        Binding key = BindFormatter(components[0], profile, label);
        Binding value = BindFormatter(components[1], profile, label);
        string binary = Apply("luminpack::pair_codec", new[] { key.Binary, value.Binary });
        string json = Apply("luminpack::json_pair_codec", new[] { key.Json, value.Json });
        string? sequence = collection ? PairCollection(formatter, components, key.Binary, value.Binary, label) : null;
        return new Binding(binary + "::value_type", binary, json, SequenceBinary: sequence);
    }

    /// <summary>Checks ordinary pair collection storage or binds the exported Tagged models.</summary>
    /// <param name="formatter">The normalized CLR pair element.</param>
    /// <param name="components">The normalized key and value formatters.</param>
    /// <param name="key">The emitted Binary key codec.</param>
    /// <param name="value">The emitted Binary value codec.</param>
    /// <param name="label">The collection used in diagnostics.</param>
    /// <returns>The specialized Tagged block codec, or null for element-wise Binary.</returns>
    private static string? PairCollection(JsonElement formatter, JsonElement components, string key, string value, string label)
    {
        JsonElement layout = formatter.GetProperty("layout");
        if (layout.ValueKind == JsonValueKind.Null && !Flag(formatter, "rawCollection"))
            return null;

        int keyWidth = PairScalarWidth(components[0]);
        int valueWidth = PairScalarWidth(components[1]);
        if (keyWidth > 0 && valueWidth > 0)
        {
            // A CLR pair has consecutive key and value fields. Equal scalar sizes require no intervening or tail padding.
            if (layout.ValueKind == JsonValueKind.Null && keyWidth == valueWidth)
                return null;

            if (layout.ValueKind == JsonValueKind.Object && layout.GetProperty("wireSize").GetInt32() == keyWidth + valueWidth)
            {
                JsonElement fields = layout.GetProperty("fields");
                if (fields.GetArrayLength() == 2 && PairFieldMatches(fields[0], components[0], "key", keyWidth) &&
                    PairFieldMatches(fields[1], components[1], "value", valueWidth))
                {
                    if (!Flag(layout, "tagged"))
                        return null;

                    StorageLeaf[] leaves = [new(string.Empty, keyWidth, false), new(string.Empty, valueWidth, false)];
                    StorageModel[] models = StorageModels(layout, leaves, keyWidth + valueWidth, label, tagged: true);
                    return Apply("luminpack::tagged_pair_list_codec", new[] { key, value }.Concat(
                        models.Select(static model => "luminpack::storage_model<2>" + ModelInitializer(model))));
                }
            }
        }

        throw Missing(label, "a KeyValuePair collection with padded or unproven CLR storage");
    }

    /// <summary>Verifies that a pair storage leaf represents the corresponding closed formatter.</summary>
    /// <param name="field">The physical field in the CLR storage plan.</param>
    /// <param name="component">The matching key or value formatter.</param>
    /// <param name="name">The expected field name.</param>
    /// <param name="width">The formatter's fixed wire width.</param>
    /// <returns>Whether the field has the required scalar storage.</returns>
    private static bool PairFieldMatches(JsonElement field, JsonElement component, string name, int width)
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
