using System.Text.Json;

internal sealed partial class CppEmitter
{
    private const string PairDefinition = "System.Collections.Generic.KeyValuePair";

    /// <summary>Binds the standalone key and value operations in their existing wire order.</summary>
    /// <param name="formatter">The normalized CLR pair formatter.</param>
    /// <param name="profile">The effective binary profile.</param>
    /// <param name="label">The member or type used in diagnostics.</param>
    /// <returns>The C++ pair type and codecs.</returns>
    private Binding BindPair(JsonElement formatter, int profile, string label)
    {
        JsonElement components = formatter.GetProperty("arguments");
        if (components.GetArrayLength() != 2 || Flag(formatter, "raw"))
            throw Missing(label, "an unsupported standalone KeyValuePair formatter");

        Binding key = BindFormatter(components[0], profile, label);
        Binding value = BindFormatter(components[1], profile, label);
        string binary = Apply("luminpack::pair_codec", new[] { key.Binary, value.Binary });
        string json = Apply("luminpack::json_pair_codec", new[] { key.Json, value.Json });
        return new Binding(binary + "::value_type", binary, json);
    }

    /// <summary>Checks when CLR pair collection storage equals the standalone field stream.</summary>
    /// <param name="formatter">The normalized CLR pair element.</param>
    /// <param name="label">The collection used in diagnostics.</param>
    private static void CheckPairCollection(JsonElement formatter, string label)
    {
        JsonElement layout = formatter.GetProperty("layout");
        if (layout.ValueKind == JsonValueKind.Null && !Flag(formatter, "rawCollection"))
            return;

        JsonElement components = formatter.GetProperty("arguments");
        if (components.GetArrayLength() != 2)
            throw Missing(label, "an incomplete KeyValuePair element contract");

        int keyWidth = PairScalarWidth(components[0]);
        int valueWidth = PairScalarWidth(components[1]);
        if (keyWidth > 0 && valueWidth > 0)
        {
            // A CLR pair has consecutive key and value fields. Equal scalar sizes require no intervening or tail padding.
            if (layout.ValueKind == JsonValueKind.Null && keyWidth == valueWidth)
                return;

            if (layout.ValueKind == JsonValueKind.Object && !Flag(layout, "tagged"))
            {
                JsonElement fields = layout.GetProperty("fields");
                if (fields.GetArrayLength() == 2 && Text(fields[0], "name") == "key" && Text(fields[1], "name") == "value" &&
                    fields[0].GetProperty("storage").GetProperty("wireSize").GetInt32() == keyWidth &&
                    fields[1].GetProperty("storage").GetProperty("wireSize").GetInt32() == valueWidth &&
                    layout.GetProperty("wireSize").GetInt32() == keyWidth + valueWidth)
                    return;
            }
        }

        throw Missing(label, "a KeyValuePair collection with tagged, padded, or unproven CLR storage");
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
