using System.Globalization;
using System.Text.Json;

internal sealed partial class CppEmitter
{
    private const string NullableDefinition = "System.Nullable";

    /// <summary>Binds a normalized formatter, keeping its element and contiguous-storage contracts distinct.</summary>
    /// <param name="formatter">The statically analyzed formatter.</param>
    /// <param name="profile">The effective binary string profile.</param>
    /// <param name="label">The field or type used in diagnostics.</param>
    /// <param name="required">Whether the codec is for a required dictionary key.</param>
    /// <param name="nullableWidth">The padded Version width, or zero for ordinary framing.</param>
    /// <returns>The typed C++ binding.</returns>
    private Binding BindFormatter(JsonElement formatter, int profile, string label, bool required = false, int nullableWidth = 0)
    {
        JsonElement layout = formatter.GetProperty("layout");
        string type = Text(formatter, "type");
        string definition = Text(formatter, "definition");
        if (definition == PairDefinition && nullableWidth == 0)
        {
            if (required)
                throw Missing(label, "a KeyValuePair dictionary key");
            return BindPair(formatter, profile, label);
        }
        if (definition == TupleDefinition && nullableWidth == 0)
        {
            if (required)
                throw Missing(label, "a ValueTuple dictionary key");
            return BindTuple(formatter, profile, label);
        }
        if (IsContiguousView(definition) && nullableWidth == 0)
        {
            if (required)
                throw Missing(label, "a memory view dictionary key");
            return BindContiguousView(definition, formatter.GetProperty("arguments")[0], profile, label);
        }
        if (layout.ValueKind != JsonValueKind.Null &&
            (!contracts.TryGetValue(Normalize(type), out JsonElement contract) ||
                Text(contract, "kind") != "Struct" || !JsonElement.DeepEquals(contract.GetProperty("storage"), layout)))
            throw Missing(label, "a specialized layout block");

        JsonElement arguments = formatter.GetProperty("arguments");
        if (definition == NullableDefinition)
        {
            if (required)
                throw Missing(label, "a nullable dictionary key");
            Binding payload = BindFormatter(arguments[0], profile, label);
            return Optional(payload, nullableWidth);
        }
        if (nullableWidth != 0)
            throw Missing(label, "the fixed-width nullable formatter");

        if (definition == "$array")
        {
            int rank = formatter.GetProperty("rank").GetInt32();
            Binding sequence = BindElementSequence(arguments[0], profile, label);
            return rank > 1 ? BindRectangular(rank, sequence) : sequence;
        }
        if (definition == "System.Collections.Generic.List")
            return BindElementSequence(arguments[0], profile, label);
        if (definition == "System.Collections.Generic.Dictionary")
        {
            Binding key = BindFormatter(arguments[0], profile, label, required: true);
            JsonElement valueFormatter = arguments[1];
            Binding value = BindFormatter(valueFormatter, profile, label);
            if (Text(valueFormatter, "definition") == NullableDefinition && valueFormatter.GetProperty("nullableEncoding").GetInt32() != 0)
            {
                if (!valueFormatter.TryGetProperty("nativeNullableElement", out JsonElement native))
                    throw Missing(label, "an updated manifest with the nullable dictionary entry layout");
                if (native.GetBoolean())
                {
                    var storage = NullableStorage(valueFormatter, profile, label);
                    string codec = Apply("luminpack::nullable_storage_codec", new[]
                    {
                        storage.Payload.Binary, UntaggedNullableModel(valueFormatter, storage.Payload, storage.Models, label)
                    });
                    value = value with { Binary = codec };
                }
            }
            return Dictionary(key, value);
        }
        return BindType(type, profile, label, required);
    }

    /// <summary>Maps nullable fields to the marker and payload form, with optional Version null padding.</summary>
    /// <param name="payload">The nonnullable value contract.</param>
    /// <param name="width">The padded Version width, or zero for ordinary framing.</param>
    /// <returns>The Binary and JSON optional codecs.</returns>
    private static Binding Optional(Binding payload, int width)
    {
        string[] arguments = width == 0 ? [payload.Binary] : [payload.Binary, width.ToString(CultureInfo.InvariantCulture)];
        string binary = Apply("luminpack::nullable_codec", arguments);
        return new Binding(binary + "::value_type", binary, "luminpack::json_optional_codec<" + payload.Json + ">");
    }

    /// <summary>Uses the compiler's tagged or native block models for a nullable array or list.</summary>
    /// <param name="element">The normalized nullable element formatter.</param>
    /// <param name="profile">The active profile.</param>
    /// <param name="label">The collection used in diagnostics.</param>
    /// <returns>The block Binary codec and the element-wise JSON codec.</returns>
    private Binding BindNullableBlock(JsonElement element, int profile, string label)
    {
        var storage = NullableStorage(element, profile, label);
        int encoding = element.GetProperty("nullableEncoding").GetInt32();
        string[] layouts = encoding switch
        {
            1 => storage.Models,
            2 => [UntaggedNullableModel(element, storage.Payload, storage.Models, label)],
            _ => throw Missing(label, "an unknown nullable storage encoding")
        };
        string codec = Apply("luminpack::nullable_list_codec", new[] { storage.Payload.Binary }.Concat(layouts));
        string json = "luminpack::json_list_codec<luminpack::json_optional_codec<" + storage.Payload.Json + ">>";
        return new Binding(codec + "::value_type", codec, json);
    }

    /// <summary>Validates the compiler's exact flag, contiguous payload offsets and stride.</summary>
    /// <param name="nullable">The closed nullable formatter.</param>
    /// <param name="profile">The effective binary string profile.</param>
    /// <param name="label">The consuming member used in diagnostics.</param>
    /// <returns>A primitive or enum payload and its CLR storage models.</returns>
    private (Binding Payload, string[] Models) NullableStorage(JsonElement nullable, int profile, string label)
    {
        JsonElement item = nullable.GetProperty("arguments")[0];
        string type = Normalize(Text(item, "type"));
        if (type is not ("bool" or "System.Boolean") && Scalar(type) == null && !enums.ContainsKey(type))
            throw Missing(label, "a nullable storage block with a non-scalar payload");

        JsonElement layout = nullable.GetProperty("nullableLayout");
        int width = layout.GetProperty("wireSize").GetInt32() - 1;
        JsonElement sizes = layout.GetProperty("modelSizes");
        JsonElement fields = layout.GetProperty("modelOffsets");
        if (width <= 0 || sizes.GetArrayLength() is < 1 or > 2 || sizes.GetArrayLength() != fields.GetArrayLength())
            throw Missing(label, "invalid nullable storage models");

        var models = new string[sizes.GetArrayLength()];
        for (int index = 0; index < models.Length; index++)
        {
            int stride = sizes[index].GetInt32();
            int[] offsets = fields[index].EnumerateArray().Select(static offset => offset.GetInt32()).ToArray();
            if (offsets.Length != width + 1 || offsets.Any(offset => offset < 0 || offset >= stride) ||
                offsets.Distinct().Count() != offsets.Length ||
                !offsets.AsSpan(1).SequenceEqual(Enumerable.Range(offsets[1], width).ToArray()))
                throw Missing(label, "non-contiguous nullable payload storage");
            models[index] = FormattableString.Invariant($"luminpack::nullable_storage_layout{{{stride}, {offsets[0]}, {offsets[1]}}}");
        }
        return (BindFormatter(item, profile, label), models);
    }

    /// <summary>Uses the target ABI proof for Native and the single exported layout for a tag-free Tagged slot.</summary>
    /// <param name="element">The normalized nullable formatter.</param>
    /// <param name="payload">The scalar or enum payload binding.</param>
    /// <param name="models">The exported CLR layout alternatives.</param>
    /// <param name="label">The consuming field used in diagnostics.</param>
    /// <returns>The selected layout expression.</returns>
    private static string UntaggedNullableModel(JsonElement element, Binding payload, string[] models, string label) =>
        element.GetProperty("nullableEncoding").GetInt32() switch
        {
            2 => Apply("luminpack::detail::select_native_nullable_layout", new[] { payload.Binary }.Concat(models)) + "()",
            1 when models.Length == 1 => models[0],
            _ => throw Missing(label, "an untagged nullable field without proven CLR storage")
        };
}
