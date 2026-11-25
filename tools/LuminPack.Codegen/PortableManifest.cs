using System.Text.Json.Nodes;

/// <summary>Resolves reachable portable wire shapes from the same Next manifest used by the C++ emitter.</summary>
internal sealed class PortableManifest
{
    private readonly Dictionary<string, JsonObject> declarations;
    private readonly Dictionary<string, JsonObject> enums;
    private readonly SortedDictionary<string, JsonObject> reachable = new(StringComparer.Ordinal);
    private readonly int nativeIntegerBytes;

    private static readonly Dictionary<string, string> Scalars = new(StringComparer.Ordinal)
    {
        ["System.Boolean"] = "bool", ["System.Byte"] = "byte", ["System.SByte"] = "sbyte",
        ["System.Int16"] = "short", ["System.UInt16"] = "ushort", ["System.Char"] = "char",
        ["System.Int32"] = "int", ["System.UInt32"] = "uint", ["System.Single"] = "float",
        ["System.Int64"] = "long", ["System.UInt64"] = "ulong", ["System.Double"] = "double",
        ["System.IntPtr"] = "nint", ["System.UIntPtr"] = "nuint"
    };

    /// <summary>Indexes the analyzed identities once for all of the portable targets.</summary>
    /// <param name="source">The compiler manifest.</param>
    /// <param name="nativeIntegerBytes">The width used by the producer for native integers.</param>
    private PortableManifest(JsonObject source, int nativeIntegerBytes)
    {
        declarations = Objects(source, "contracts").ToDictionary(item => Normalize(Text(item, "type")), StringComparer.Ordinal);
        enums = Objects(source, "enums").ToDictionary(item => Normalize(Text(item, "type")), StringComparer.Ordinal);
        this.nativeIntegerBytes = nativeIntegerBytes;
    }

    /// <summary>Projects the selected root and its reachable contracts without loading managed application code.</summary>
    /// <param name="manifestPath">The Next manifest file.</param>
    /// <param name="root">A CLR metadata identity or projected type name.</param>
    /// <param name="binaryProfile">An explicit operation profile or the declared default.</param>
    /// <param name="nativeIntegerBytes">The producer ABI's native integer width.</param>
    /// <param name="nativeScalarAlignment">The producer ABI's maximum alignment for fixed-width primitive slots.</param>
    /// <returns>A complete portable descriptor document.</returns>
    internal static JsonObject Project(string manifestPath, string root, int? binaryProfile, int nativeIntegerBytes,
        int nativeScalarAlignment)
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject ??
            throw new InvalidOperationException("The LuminPack manifest must be an object.");
        if (source["version"]?.GetValue<int>() != 1)
            throw new InvalidOperationException("Unsupported LuminPack manifest version.");
        var projector = new PortableManifest(source, nativeIntegerBytes);
        string type = Normalize(root);
        JsonObject? declaration = projector.declarations.GetValueOrDefault(type) ??
            projector.declarations.Values.FirstOrDefault(item => Text(item, "id") == root);
        if (declaration != null)
        {
            type = Normalize(Text(declaration, "type"));
            projector.VisitContract(type);
            JsonObject profile = (JsonObject)declaration["profile"]!;
            int declared = profile["binary"]!.GetValue<int>();
            binaryProfile = profile["declared"]?.GetValue<bool>() == true ? declared : binaryProfile ?? declared;
        }
        else
        {
            string key = RootKey(type);
            JsonNode? shape = (source["roots"] as JsonObject)?.FirstOrDefault(entry => RootKey(entry.Key) == key).Value;
            if (shape?["formatter"] is JsonObject formatter)
                source["rootWire"] = projector.ResolveFormatter(formatter, "root");
            else
            {
                CheckRootLayout(key);
                source["rootWire"] = projector.Resolve(type, "root");
            }
            binaryProfile ??= source["rootProfile"]?["binary"]?.GetValue<int>();
        }
        source.Remove("roots");
        source["root"] = type;
        source["binaryProfile"] = binaryProfile ?? 2;
        source["nativeIntegerBytes"] = nativeIntegerBytes;
        source["nativeScalarAlignment"] = nativeScalarAlignment;
        source["contracts"] = new JsonArray(projector.reachable.Values.Cast<JsonNode>().ToArray());
        return source;
    }

    /// <summary>Matches CLR primitive aliases and nullable spellings in exported root identities.</summary>
    /// <param name="type">The CLI or compiler spelling of a closed root.</param>
    /// <returns>The comparison key for a formatter lookup.</returns>
    internal static string RootKey(string type)
    {
        string key = Normalize(type).Replace(" ", string.Empty, StringComparison.Ordinal);
        key = System.Text.RegularExpressions.Regex.Replace(key, @"[\w.]+",
            match => Scalars.GetValueOrDefault(match.Value, match.Value == "System.String" ? "string" : match.Value));
        return System.Text.RegularExpressions.Regex.Replace(key, @"\bSystem\.Nullable<([\w.]+)>", "$1?");
    }

    /// <summary>Requires the producer layout for nullable value slots in standalone collections.</summary>
    /// <param name="key">The normalized root comparison key.</param>
    internal static void CheckRootLayout(string key)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(key, @"\b(bool|byte|sbyte|short|ushort|char|int|uint|long|ulong|float|double|nint|nuint)\?(?=[,>\[])"))
            throw new InvalidOperationException("Export a closed serializer call for " + key + " to specify its nullable collection layout.");
    }

    /// <summary>Projects one contract and schedules nested members or Union cases once, including recursive references.</summary>
    /// <param name="type">The normalized identity.</param>
    private void VisitContract(string type)
    {
        if (reachable.ContainsKey(type))
            return;
        if (!declarations.TryGetValue(type, out JsonObject? source))
            throw new InvalidOperationException("No exported LuminPack contract for " + type + ".");
        if (source["typeParameters"] is JsonArray { Count: > 0 })
            throw new InvalidOperationException("A portable root requires closed contracts: " + type + ".");
        if (source["sequenceBase"] != null)
            throw new InvalidOperationException("A self-referential collection body requires a portable sequence codec: " + type + ".");
        if (source["dynamicVersion"]?.GetValue<bool>() == true)
            throw new InvalidOperationException("A portable Version contract requires closed field descriptors: " + type + ".");
        var contract = (JsonObject)source.DeepClone();
        reachable.Add(type, contract);
        foreach (JsonObject member in Objects(contract, "members"))
        {
            string label = type + "." + Text(member, "name");
            JsonObject shape = (JsonObject)member["shape"]!;
            member["wire"] = shape["formatter"] is JsonObject formatter ? ResolveFormatter(formatter, label) :
                Resolve(Text(shape, "type"), label);
            if (shape["size"] is JsonValue size && member["wire"] is JsonObject wire &&
                wire["type"]?.GetValue<string>() is "nint" or "nuint" && size.GetValue<int>() is 4 or 8 &&
                size.GetValue<int>() != nativeIntegerBytes)
                throw new InvalidOperationException("Native integer width differs from the manifest at " + label + ".");
        }
        if (contract["collectionBase"] is JsonObject collection)
        {
            var members = (JsonArray)contract["members"]!;
            contract["binaryMemberCount"] = members.Count;
            JsonObject items = ResolveFormatter(collection, type + ".$items");
            items["required"] = true;
            members.Add(new JsonObject
            {
                ["name"] = "$items", ["profile"] = new JsonObject(),
                ["wire"] = items
            });
        }
        foreach (JsonObject item in Objects(contract, "cases"))
            VisitContract(Normalize(Text(item, "type")));
    }

    /// <summary>Resolves a normalized formatter and retains its collection and physical layout annotations.</summary>
    /// <param name="formatter">The analyzed C# formatter.</param>
    /// <param name="label">The member name used for diagnostics.</param>
    /// <returns>The portable wire descriptor.</returns>
    private JsonObject ResolveFormatter(JsonObject formatter, string label)
    {
        string definition = Text(formatter, "definition");
        JsonObject[] arguments = Objects(formatter, "arguments").ToArray();
        int rank = formatter["rank"]?.GetValue<int>() ?? 0;
        JsonObject result = definition switch
        {
            "$array" => Sequence(ResolveFormatter(arguments[0], label), rank),
            "System.Nullable" => Wrap("nullable", ResolveFormatter(arguments[0], label)),
            "System.Collections.Generic.List" => Sequence(ResolveFormatter(arguments[0], label)),
            "System.Collections.Generic.Dictionary" => Dictionary(ResolveFormatter(arguments[0], label), ResolveFormatter(arguments[1], label)),
            "System.Collections.Generic.KeyValuePair" => Aggregate("pair", arguments.Select(item => ResolveFormatter(item, label))),
            "System.ValueTuple" => Aggregate("tuple", arguments.Select(item => ResolveFormatter(item, label))),
            _ when IsMemory(definition) => Memory(definition, ResolveFormatter(arguments[0], label)),
            _ => Resolve(Text(formatter, "type"), label)
        };
        foreach (string property in new[]
        {
            "layout", "rawCompoundStorage", "denseTupleStorage", "nullableLayout", "nullableEncoding",
            "nativeNullableElement", "raw", "rawCollection"
        })
        {
            if (formatter[property] is { } value)
                result[property] = value.DeepClone();
        }
        return result;
    }

    /// <summary>Resolves scalar, aggregate, collection and declared type names found in member projections.</summary>
    /// <param name="rawType">The C# display name.</param>
    /// <param name="label">The field used for diagnostics.</param>
    /// <returns>The portable descriptor.</returns>
    private JsonObject Resolve(string rawType, string label)
    {
        string type = Normalize(rawType);
        bool optional = type.EndsWith('?');
        if (optional)
            type = type[..^1];

        if (type is "string" or "System.String")
            return Describe("string");
        if (Scalars.TryGetValue(type, out string? alias) || (alias = Scalars.Values.FirstOrDefault(item => item == type)) != null)
        {
            JsonObject scalar = Describe("scalar", alias);
            if (alias is "nint" or "nuint")
                scalar["width"] = nativeIntegerBytes;
            return optional ? Wrap("nullable", scalar) : scalar;
        }
        if (enums.TryGetValue(type, out JsonObject? enumeration))
        {
            JsonObject scalar = Resolve(Text(enumeration, "underlying"), label);
            scalar["enumType"] = type;
            return optional ? Wrap("nullable", scalar) : scalar;
        }
        if (declarations.TryGetValue(type, out JsonObject? contract))
        {
            VisitContract(type);
            JsonObject value = Describe("contract", type);
            return optional && Text(contract, "kind") == "Struct" ? Wrap("nullable", value) : value;
        }
        if (type.EndsWith(']') && type.LastIndexOf('[') is int bracket && bracket > 0 &&
            type.AsSpan(bracket + 1, type.Length - bracket - 2).IndexOfAnyExcept(',') < 0)
            return Sequence(Resolve(type[..bracket], label), type.Length - bracket - 1);
        if (type.EndsWith('>') && type.IndexOf('<') is int open && open > 0)
        {
            string family = type[..open];
            string[] arguments = SplitArguments(type[(open + 1)..^1]);
            JsonObject? result = (family, arguments.Length) switch
            {
                ("System.Nullable", 1) => Wrap("nullable", Resolve(arguments[0], label)),
                ("System.Collections.Generic.List", 1) => Sequence(Resolve(arguments[0], label)),
                ("System.Collections.Generic.Dictionary", 2) => Dictionary(Resolve(arguments[0], label), Resolve(arguments[1], label)),
                ("System.Collections.Generic.KeyValuePair", 2) => Aggregate("pair", arguments.Select(item => Resolve(item, label))),
                ("System.ValueTuple", > 0) => Aggregate("tuple", arguments.Select(item => Resolve(item, label))),
                (_, 1) when IsMemory(family) => Memory(family, Resolve(arguments[0], label)),
                _ => null
            };
            if (result != null)
                return result;
        }
        throw new InvalidOperationException("No portable wire representation for " + rawType + " at " + label + ".");
    }

    /// <summary>Splits only outer commas in a normalized generic argument list.</summary>
    /// <param name="value">The contents between the outer angle brackets.</param>
    /// <returns>Individual closed type names.</returns>
    private static string[] SplitArguments(string value)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is '<' or '[' or '(')
                depth++;
            else if (character is '>' or ']' or ')')
                depth--;
            else if (character == ',' && depth == 0)
            {
                parts.Add(value[start..index].Trim());
                start = index + 1;
            }
        }
        parts.Add(value[start..].Trim());
        return [.. parts];
    }

    /// <summary>Recognizes CLR collection views that project to owned portable values.</summary>
    /// <param name="family">The formatter definition.</param>
    /// <returns>Whether the family uses a collection payload.</returns>
    private static bool IsMemory(string family) => family is "System.Memory" or "System.ReadOnlyMemory" or
        "System.ArraySegment" or "System.Buffers.ReadOnlySequence" or "System.Collections.Immutable.ImmutableArray";

    /// <summary>Retains the view family to identify its null and segmented-layout rules.</summary>
    /// <param name="family">The CLR definition.</param>
    /// <param name="element">The projected element.</param>
    /// <returns>The portable sequence descriptor.</returns>
    private static JsonObject Memory(string family, JsonObject element) => new()
    {
        ["kind"] = "sequence", ["family"] = family, ["element"] = element
    };

    /// <summary>Describes an element array, optionally with explicit rectangular framing.</summary>
    /// <param name="element">The projected element.</param>
    /// <param name="rank">The managed array rank, if any.</param>
    /// <returns>The portable array descriptor.</returns>
    private static JsonObject Sequence(JsonObject element, int rank = 1) => rank > 1
        ? new JsonObject { ["kind"] = "rectangular", ["rank"] = rank, ["element"] = element }
        : Wrap("sequence", element);

    /// <summary>Describes a dictionary with distinct key and value operations.</summary>
    /// <param name="key">The projected key.</param>
    /// <param name="value">The projected value.</param>
    /// <returns>The dictionary descriptor.</returns>
    private static JsonObject Dictionary(JsonObject key, JsonObject value) => new()
    {
        ["kind"] = "dictionary", ["key"] = key, ["value"] = value
    };

    /// <summary>Describes ordered pair or tuple components.</summary>
    /// <param name="kind">The aggregate wire kind.</param>
    /// <param name="elements">The ordered component operations.</param>
    /// <returns>The tuple descriptor.</returns>
    private static JsonObject Aggregate(string kind, IEnumerable<JsonObject> elements) => new()
    {
        ["kind"] = kind, ["elements"] = new JsonArray(elements.Cast<JsonNode>().ToArray())
    };

    /// <summary>Describes an operation with one nested element.</summary>
    /// <param name="kind">The wire kind.</param>
    /// <param name="element">The nested value contract.</param>
    /// <returns>The wrapper descriptor.</returns>
    private static JsonObject Wrap(string kind, JsonObject element) => new() { ["kind"] = kind, ["element"] = element };

    /// <summary>Describes a primitive or a reference to a generated contract.</summary>
    /// <param name="kind">The wire kind.</param>
    /// <param name="type">The optional type identity.</param>
    /// <returns>The descriptor.</returns>
    private static JsonObject Describe(string kind, string? type = null) => type == null
        ? new JsonObject { ["kind"] = kind }
        : new JsonObject { ["kind"] = kind, ["type"] = type };

    /// <summary>Reads an optional array of analyzed manifest rows.</summary>
    /// <param name="node">The object owning the array.</param>
    /// <param name="key">The array property.</param>
    /// <returns>The object rows.</returns>
    internal static IEnumerable<JsonObject> Objects(JsonObject node, string key) =>
        node[key] is JsonArray rows ? rows.Select(row => row as JsonObject ??
            throw new InvalidOperationException("Invalid LuminPack manifest row in " + key + ".")) : [];

    /// <summary>Reads a required string manifest property.</summary>
    /// <param name="node">The property owner.</param>
    /// <param name="key">The property name.</param>
    /// <returns>The manifest string.</returns>
    internal static string Text(JsonObject node, string key) => node[key]?.GetValue<string>() ??
        throw new InvalidOperationException("Missing LuminPack manifest property " + key + ".");

    /// <summary>Removes source qualification from a C# display name.</summary>
    /// <param name="type">The analyzed type name.</param>
    /// <returns>The normalized identity.</returns>
    internal static string Normalize(string type) => type.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
}
