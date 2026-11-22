using System.Text.Json;

internal sealed partial class CppEmitter
{
    private const string TupleDefinition = "System.ValueTuple";

    /// <summary>Binds the closed CLR ValueTuple and its optional per-collection storage model.</summary>
    /// <param name="formatter">The normalized CLR tuple.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <param name="label">The field or type used in diagnostics.</param>
    /// <param name="collection">Whether the tuple is a contiguous collection element.</param>
    /// <returns>The tuple bindings and an optional specialized block codec.</returns>
    private Binding BindTuple(JsonElement formatter, int profile, string label, bool collection = false)
    {
        JsonElement layout = formatter.GetProperty("layout");
        if (layout.ValueKind == JsonValueKind.Object)
        {
            var leaves = new List<StorageLeaf>();
            Binding tuple = BindTupleStorage(formatter, layout, profile, label, leaves);
            if (!collection || !Flag(layout, "tagged"))
                return tuple;

            if (leaves.Any(leaf => leaf.Boolean))
                throw Missing(label, "a Tagged ValueTuple block containing Boolean storage");
            int width = layout.GetProperty("wireSize").GetInt32();
            StorageModel[] models = StorageModels(layout, leaves, width, label, tagged: true);
            string modelType = "luminpack::storage_model<" + Integer(leaves.Count) + ">";
            string sequence = Apply("luminpack::tagged_tuple_list_codec", new[] { tuple.Binary }.Concat(
                models.Select(model => modelType + ModelInitializer(model))));
            return tuple with { SequenceBinary = sequence };
        }

        if ((Flag(formatter, "raw") || collection && Flag(formatter, "rawCollection")) && !Flag(formatter, "denseTupleStorage"))
            throw Missing(label, "a raw ValueTuple without proven dense declaration-order CLR storage");
        return Tuple(TupleArguments(formatter, label).Select(argument => BindFormatter(argument, profile, label)));
    }

    /// <summary>Binds the tuple in its containing Packed layout and records the exported scalar leaf order.</summary>
    /// <param name="formatter">The normalized tuple formatter.</param>
    /// <param name="layout">The layout of this tuple in the parent storage tree.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <param name="label">The field or type used in diagnostics.</param>
    /// <param name="leaves">The flattened fixed-width storage leaves.</param>
    /// <returns>The packed tuple's Binary and JSON bindings.</returns>
    private Binding BindTupleStorage(JsonElement formatter, JsonElement layout, int profile, string label, List<StorageLeaf> leaves)
    {
        JsonElement[] arguments = TupleArguments(formatter, label);
        JsonElement fields = layout.GetProperty("fields");
        if (fields.GetArrayLength() != arguments.Length)
            throw Missing(label, "a ValueTuple plan without the declared components");

        var components = new Binding[arguments.Length];
        int start = leaves.Count;
        for (int index = 0; index < arguments.Length; index++)
        {
            JsonElement argument = arguments[index];
            JsonElement field = fields[index];
            JsonElement child = field.GetProperty("storage");
            string name = index == 7 ? "Rest" : "Item" + Integer(index + 1);
            string type = Normalize(Text(argument, "type"));
            if (Text(field, "name") != name || Flag(field, "volatile") || Normalize(Text(child, "type")) != type)
                throw Missing(label, "a ValueTuple plan with inconsistent component storage");

            string componentLabel = label + "." + name;
            if (Text(argument, "definition") == TupleDefinition)
                components[index] = BindTupleStorage(argument, child, profile, componentLabel, leaves);
            else
            {
                bool boolean = type is "bool" or "System.Boolean";
                int width = boolean ? 1 : PairScalarWidth(argument);
                if (width == 0 || Text(argument, "definition") != "$scalar" || child.GetProperty("fields").GetArrayLength() != 0 ||
                    child.GetProperty("wireSize").GetInt32() != width)
                    throw Missing(componentLabel, "an unsupported ValueTuple storage leaf");
                leaves.Add(new StorageLeaf(string.Empty, width, boolean));
                components[index] = BindFormatter(argument, profile, componentLabel);
            }
        }
        if (leaves.Skip(start).Sum(leaf => leaf.Width) != layout.GetProperty("wireSize").GetInt32())
            throw Missing(label, "a ValueTuple plan with inconsistent packed width");
        return Tuple(components);
    }

    /// <summary>Retains the eighth component as the CLR's nested Rest tuple.</summary>
    /// <param name="formatter">The normalized tuple formatter.</param>
    /// <param name="label">The field or type used in diagnostics.</param>
    /// <returns>The exact closed generic argument positions.</returns>
    private static JsonElement[] TupleArguments(JsonElement formatter, string label)
    {
        JsonElement[] arguments = formatter.GetProperty("arguments").EnumerateArray().ToArray();
        if (arguments.Length is < 1 or > 8 || arguments.Length == 8 && Text(arguments[7], "definition") != TupleDefinition)
            throw Missing(label, "an unsupported ValueTuple arity or Rest component");
        return arguments;
    }

    /// <summary>Composes each component's Binary and JSON codec without introducing extra framing.</summary>
    /// <param name="components">The ordered component bindings.</param>
    /// <returns>The standard tuple type and matching codecs.</returns>
    private static Binding Tuple(IEnumerable<Binding> components)
    {
        Binding[] ordered = components.ToArray();
        string binary = Apply("luminpack::tuple_codec", ordered.Select(item => item.Binary));
        string json = Apply("luminpack::json_tuple_codec", ordered.Select(item => item.Json));
        return new Binding(binary + "::value_type", binary, json);
    }
}
