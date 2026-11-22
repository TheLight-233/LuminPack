using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>Emits closed C++ DTO and codec declarations from a Next manifest.</summary>
internal sealed class CppEmitter
{
    private readonly Dictionary<string, JsonElement> contracts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> layouts = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Type, int Profile), Binding> generated = new();
    private readonly HashSet<(string Type, int Profile)> building = new();
    private readonly StringBuilder code = new("#pragma once\n\n#include <luminpack/luminpack.h>\n\n");
    private static readonly string[] Profiles =
        ["utf8_length", "utf8_token", "utf16_length", "utf16_token", "utf8_varint", "utf16_varint"];

    /// <summary>Indexes declared types from the current manifest version.</summary>
    /// <param name="manifest">The validated manifest root.</param>
    private CppEmitter(JsonElement manifest)
    {
        if (manifest.GetProperty("version").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported LuminPack C++ manifest version.");
        foreach (JsonElement contract in manifest.GetProperty("contracts").EnumerateArray())
            contracts.Add(Normalize(Text(contract, "type")), contract);
        if (manifest.TryGetProperty("valueLayouts", out JsonElement valueLayouts))
        {
            foreach (JsonElement item in valueLayouts.EnumerateArray())
                layouts.Add(Text(item, "type"), item);
        }
        if (manifest.TryGetProperty("enums", out JsonElement definitions))
        {
            foreach (JsonElement item in definitions.EnumerateArray())
                enums.Add(Text(item, "type"), item);
        }
    }

    /// <summary>Emits a requested root and its reachable declarations before replacing the generated header.</summary>
    /// <param name="manifestPath">The compiler-projected manifest.</param>
    /// <param name="root">The explicit root's CLR metadata name.</param>
    /// <param name="headerPath">The generated C++ header destination.</param>
    internal static void Generate(string manifestPath, string root, string headerPath)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var emitter = new CppEmitter(manifest.RootElement);
        JsonElement declaration = emitter.contracts.Values.FirstOrDefault(item => Text(item, "id") == root);
        if (declaration.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("No LuminPack C++ contract for root " + root + ".");

        emitter.code.Append("namespace luminpack_generated::root_").Append(Identifier(root)).Append("\n{\n");
        JsonElement profile = declaration.GetProperty("profile");
        Binding binding = emitter.BindContract(declaration, profile.GetProperty("binary").GetInt32());
        emitter.EmitRoot(binding, profile);
        emitter.code.Append("}\n");
        OutputFile.Replace(manifestPath, headerPath, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.Write(emitter.code.ToString());
        });
    }

    /// <summary>Binds a normalized contract and emits its dependencies before its own definitions.</summary>
    /// <param name="contract">The compiler-projected declaration.</param>
    /// <param name="inheritedProfile">The active enclosing binary string profile.</param>
    /// <returns>The C++ codecs for this declaration.</returns>
    private Binding BindContract(JsonElement contract, int inheritedProfile)
    {
        string type = Normalize(Text(contract, "type"));
        JsonElement profile = contract.GetProperty("profile");
        int declared = profile.GetProperty("binary").GetInt32();
        int binary = Flag(profile, "declared") ? declared : inheritedProfile;
        var key = (type, binary);
        if (generated.TryGetValue(key, out Binding known))
            return known;
        if (!building.Add(key))
            throw Missing(type, "recursive type graphs");
        if (contract.GetProperty("typeParameters").GetArrayLength() != 0 || Flag(profile, "trackReferences"))
            throw Missing(type, "open parameters or reference tracking");

        string name = "type_" + Identifier(Text(contract, "id"));
        if (binary != declared)
            name += "_profile" + binary.ToString(CultureInfo.InvariantCulture);
        Binding binding = Text(contract, "kind") switch
        {
            "Record" => EmitRecord(contract, name, binary),
            "Union" => EmitUnion(contract, name, binary),
            "Struct" => EmitStruct(contract, name, binary),
            _ => throw Missing(type, "this value-type storage model")
        };
        building.Remove(key);
        generated.Add(key, binding);
        return binding;
    }

    /// <summary>Emits a positional or Version record and its JSON object codec.</summary>
    /// <param name="contract">The analyzed record.</param>
    /// <param name="name">The sanitized C++ declaration name.</param>
    /// <param name="profile">The record's effective binary string profile.</param>
    /// <returns>The record's C++ bindings.</returns>
    private Binding EmitRecord(JsonElement contract, string name, int profile)
    {
        string owner = Text(contract, "id");
        if (contract.GetProperty("sequenceBase").ValueKind != JsonValueKind.Null ||
            contract.GetProperty("collectionBase").ValueKind != JsonValueKind.Null || Flag(contract, "dynamicVersion"))
            throw Missing(owner, "an inherited collection body or dynamic Version schema");

        bool version = Flag(contract, "versioned");
        var declarations = new List<string>();
        var binaryMembers = new List<string>();
        var jsonMembers = new List<string>();
        foreach (JsonElement field in contract.GetProperty("members").EnumerateArray())
        {
            string property = Text(field, "name");
            string label = owner + "." + property;
            if (Flag(field, "compressed") || Flag(field, "fixedNullable"))
                throw Missing(label, "compressed or fixed-width nullable member framing");

            JsonElement memberProfile = field.GetProperty("profile");
            int active = Flag(memberProfile, "declared") ? memberProfile.GetProperty("binary").GetInt32() : profile;
            Binding member = BindShape(field.GetProperty("shape"), active, label);
            string fieldName = "field_" + Identifier(property);
            string pointer = "&" + name + "::" + fieldName;
            declarations.Add("    " + member.Type + " " + fieldName + "{};");

            if (version)
            {
                string descriptor = Text(field, "descriptor");
                if (!ulong.TryParse(descriptor, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value))
                    throw Missing(label, "a missing Version descriptor");
                var aliases = field.GetProperty("nameKeys").EnumerateArray().Skip(1)
                    .Select(static item => item.GetUInt32().ToString(CultureInfo.InvariantCulture) + "u");
                binaryMembers.Add(Apply("luminpack::version_member", new[] { pointer, member.Binary, Hex(value) }.Concat(aliases)));
            }
            else
                binaryMembers.Add(Apply("luminpack::record_member", new[] { pointer, member.Binary }));

            IEnumerable<string> names = field.GetProperty("oldNames").ValueKind == JsonValueKind.Array
                ? field.GetProperty("oldNames").EnumerateArray().Select(static item => Literal(item.GetString()!))
                : Array.Empty<string>();
            jsonMembers.Add(Apply("luminpack::json_record_member", new[] { Literal(property), pointer, member.Json }.Concat(names)));
        }

        code.Append("struct ").Append(name).Append("\n{\n");
        foreach (string declaration in declarations)
            code.Append(declaration).Append('\n');
        code.Append("};\n");
        if (version)
            Alias(name + "_binary", "luminpack::version_codec", new[] { name, Profile(profile) }.Concat(binaryMembers));
        else
            Alias(name + "_binary", "luminpack::record_codec", new[] { name }.Concat(binaryMembers));
        Alias(name + "_json_body", "luminpack::json_record_body", new[] { name }.Concat(jsonMembers));
        Alias(name + "_json", "luminpack::json_optional_codec<" + name + "_json_body>");
        code.Append('\n');
        return new Binding("std::optional<" + name + ">", name + "_binary", name + "_json",
            version ? null : name + "_binary::payload", name + "_json_body");
    }

    /// <summary>Emits a raw value when sequential managed layout and local field offsets prove the same wire block.</summary>
    /// <param name="contract">The analyzed struct.</param>
    /// <param name="name">The sanitized C++ declaration name.</param>
    /// <param name="profile">The active binary profile for the scalar bindings.</param>
    /// <returns>The binary and JSON value codecs.</returns>
    private Binding EmitStruct(JsonElement contract, string name, int profile)
    {
        string owner = Text(contract, "id");
        string type = Normalize(Text(contract, "type"));
        if (Text(contract, "storageKind") != "RawUnmanaged" ||
            contract.GetProperty("storage").ValueKind != JsonValueKind.Null ||
            !layouts.TryGetValue(type, out JsonElement layout))
            throw Missing(owner, "a value layout without a sequential raw-copy proof");

        var declarations = new List<string>();
        var binaryMembers = new List<string>();
        var jsonMembers = new List<string>();
        var offsets = new List<string>();
        var declaredFields = layout.GetProperty("fields").EnumerateArray().Select(static item => item.GetString()).ToArray();
        JsonElement.ArrayEnumerator members = contract.GetProperty("members").EnumerateArray();
        if (contract.GetProperty("members").GetArrayLength() != declaredFields.Length || declaredFields.Length == 0)
            throw Missing(owner, "a raw struct with hidden or empty storage");

        int stride = 0;
        int commonWidth = 0;
        int index = 0;
        foreach (JsonElement member in members)
        {
            string property = Text(member, "name");
            string label = owner + "." + property;
            JsonElement shape = member.GetProperty("shape");
            string spelling = Normalize(Text(shape, "type"));
            if (declaredFields[index++] != property || Text(shape, "kind") is not ("scalar" or "enum") ||
                spelling is "bool" or "System.Boolean" || Flag(shape, "volatile") || Flag(member, "compressed"))
                throw Missing(label, "raw member order or unchecked storage");
            int width = shape.GetProperty("size").GetInt32();
            if (width <= 0 || (commonWidth != 0 && width != commonWidth))
                throw Missing(owner, "a raw struct with different scalar widths");
            commonWidth = width;

            Binding field = BindType(spelling, profile, label, false);
            string fieldName = "field_" + Identifier(property);
            string pointer = "&" + name + "::" + fieldName;
            declarations.Add("    " + field.Type + " " + fieldName + "{};");
            binaryMembers.Add(Apply("luminpack::record_member", new[] { pointer, field.Binary }));
            jsonMembers.Add(Apply("luminpack::json_record_member", new[] { Literal(property), pointer, field.Json }));
            offsets.Add("offsetof(" + name + ", " + fieldName + ") == " + stride.ToString(CultureInfo.InvariantCulture));
            stride = checked(stride + width);
        }

        code.Append("struct ").Append(name).Append("\n{\n");
        foreach (string declaration in declarations)
            code.Append(declaration).Append('\n');
        code.Append("};\n");
        Alias(name + "_fields", "luminpack::record_payload_codec", new[] { name }.Concat(binaryMembers));
        string matches = "sizeof(" + name + ") == " + stride.ToString(CultureInfo.InvariantCulture) +
            " && " + string.Join(" && ", offsets);
        Alias(name + "_binary", "std::conditional_t", new[] { matches, "luminpack::raw_struct_codec<" + name + ">", name + "_fields" });
        Alias(name + "_json", "luminpack::json_record_body", new[] { name }.Concat(jsonMembers));
        code.Append('\n');
        return new Binding(name, name + "_binary", name + "_json", name + "_binary", name + "_json");
    }

    /// <summary>Emits static dispatch for a Next class or interface Union.</summary>
    /// <param name="contract">The analyzed Union declaration.</param>
    /// <param name="name">The sanitized C++ declaration name.</param>
    /// <param name="profile">The enclosing binary profile.</param>
    /// <returns>The Union's C++ bindings.</returns>
    private Binding EmitUnion(JsonElement contract, string name, int profile)
    {
        var binaryMembers = new List<string>();
        var jsonMembers = new List<string>();
        foreach (JsonElement item in contract.GetProperty("cases").EnumerateArray())
        {
            Binding member = BindType(Text(item, "type"), profile, Text(contract, "id"), false);
            string hash = item.GetProperty("hash").GetUInt32().ToString(CultureInfo.InvariantCulture) + "u";
            if (member.Payload == null || member.JsonPayload == null)
                throw Missing(Text(item, "id"), "a Union case without a matching positional payload codec");
            binaryMembers.Add(Apply("luminpack::union_case", new[] { hash, member.Payload }));
            jsonMembers.Add(Apply("luminpack::union_case", new[] { hash, member.JsonPayload }));
        }
        if (binaryMembers.Count == 0)
            throw Missing(Text(contract, "id"), "a Union without generated cases");
        Alias(name + "_binary", "luminpack::union_codec", binaryMembers);
        Alias(name + "_json", "luminpack::json_union_codec", jsonMembers);
        code.Append('\n');
        return new Binding(name + "_binary::value_type", name + "_binary", name + "_json");
    }

    /// <summary>Binds one member's projected type shape.</summary>
    /// <param name="shape">The normalized member shape.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <param name="label">The member name used in errors.</param>
    /// <returns>The C++ bindings for the member.</returns>
    private Binding BindShape(JsonElement shape, int profile, string label)
    {
        if (shape.TryGetProperty("formatter", out JsonElement formatter) && formatter.ValueKind == JsonValueKind.Object)
        {
            if (formatter.GetProperty("nullableEncoding").GetInt32() != 0 ||
                formatter.GetProperty("layout").ValueKind != JsonValueKind.Null)
                throw Missing(label, "a specialized layout block");
        }
        if (!shape.TryGetProperty("type", out JsonElement type) || type.ValueKind != JsonValueKind.String)
            throw Missing(label, "the projected member type");
        return BindType(type.GetString()!, profile, label, false);
    }

    /// <summary>Resolves a supported CLR type identity and its nested collection arguments.</summary>
    /// <param name="rawType">The projected C# type spelling.</param>
    /// <param name="profile">The active binary profile.</param>
    /// <param name="label">The member name used in errors.</param>
    /// <param name="required">Whether the slot requires a non-null value.</param>
    /// <returns>The C++ bindings for the type.</returns>
    private Binding BindType(string rawType, int profile, string label, bool required)
    {
        string type = Normalize(rawType);
        if (type.EndsWith('?'))
        {
            string bare = type[..^1];
            if (bare is "string" or "System.String" || bare.EndsWith("[]", StringComparison.Ordinal) ||
                bare.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal) ||
                bare.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal) ||
                (contracts.TryGetValue(bare, out JsonElement reference) && Text(reference, "kind") is "Record" or "Union"))
                type = bare;
        }
        if (type is "string" or "System.String")
        {
            string codec = "luminpack::string_codec<" + Profile(profile) + ">";
            if (required)
                return new Binding("std::u16string", "luminpack::required_codec<" + codec + ">", "luminpack::json_string_codec");
            return new Binding(codec + "::value_type", codec, "luminpack::json_codec<" + codec + ">");
        }
        if (type is "bool" or "System.Boolean")
            return new Binding("bool", "luminpack::boolean_codec", "luminpack::json_codec<luminpack::boolean_codec>");
        if (Scalar(type) is { } scalar)
        {
            string codec = "luminpack::scalar_codec<" + scalar + ">";
            return new Binding(scalar, codec, "luminpack::json_codec<" + codec + ">");
        }
        if (enums.TryGetValue(type, out JsonElement enumeration))
            return BindEnum(enumeration);
        if (type.EndsWith("[]", StringComparison.Ordinal))
            return Sequence(BindType(type[..^2], profile, label, false));
        if (contracts.TryGetValue(type, out JsonElement contract))
        {
            if (required)
                throw Missing(label, "an object dictionary key");
            return BindContract(contract, profile);
        }

        int open = type.IndexOf('<');
        if (open > 0 && type.EndsWith('>'))
        {
            string family = type[..open];
            string arguments = type[(open + 1)..^1];
            if (family == "System.Collections.Generic.List")
                return Sequence(BindType(arguments, profile, label, false));
            if (family == "System.Collections.Generic.Dictionary")
            {
                int comma = ArgumentSeparator(arguments);
                if (comma < 0)
                    throw Missing(label, "an incomplete Dictionary type");
                var key = BindType(arguments[..comma], profile, label, true);
                var value = BindType(arguments[(comma + 1)..], profile, label, false);
                string binary = Apply("luminpack::dictionary_codec", new[] { key.Binary, value.Binary });
                string json = Apply("luminpack::json_dictionary_codec", new[] { key.Json, value.Json });
                return new Binding(binary + "::value_type", binary, json);
            }
        }
        throw Missing(label, "the type " + rawType);
    }

    /// <summary>Emits a metadata enum without changing its integral width or named constants.</summary>
    /// <param name="enumeration">The ordered enum fields.</param>
    /// <returns>The enum codecs.</returns>
    private Binding BindEnum(JsonElement enumeration)
    {
        string type = Text(enumeration, "type");
        const int invariantProfile = -1;
        if (generated.TryGetValue((type, invariantProfile), out Binding binding))
            return binding;

        string underlying = Scalar(Text(enumeration, "underlying")) ?? throw Missing(type, "the enum's integral storage type");
        string name = "enum_" + Identifier(type);
        code.Append("enum class ").Append(name).Append(" : ").Append(underlying).Append("\n{\n");
        foreach (JsonElement item in enumeration.GetProperty("members").EnumerateArray())
        {
            string value = Text(item, "value");
            string literal;
            if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long signed))
                literal = signed == long.MinValue ? "(-9223372036854775807LL - 1)" : signed.ToString(CultureInfo.InvariantCulture) + "LL";
            else if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong unsigned))
                literal = unsigned.ToString(CultureInfo.InvariantCulture) + "ULL";
            else
                throw Missing(type, "an invalid enum constant");
            code.Append("    value_").Append(Identifier(Text(item, "name"))).Append(" = ").Append(literal).Append(",\n");
        }
        code.Append("};\n\n");
        string codec = "luminpack::enum_codec<" + name + ">";
        binding = new Binding(name, codec, "luminpack::json_codec<" + codec + ">");
        generated.Add((type, invariantProfile), binding);
        return binding;
    }

    /// <summary>Exports root aliases and declared document options next to the generated types.</summary>
    /// <param name="binding">The generated root codecs.</param>
    /// <param name="profile">The .NET declaration profile.</param>
    private void EmitRoot(Binding binding, JsonElement profile)
    {
        Alias("root_binary", binding.Binary);
        Alias("root_json", binding.Json);
        Alias("root_value", binding.Type);
        string encoding = profile.GetProperty("jsonEncoding").GetInt32() == 0 ? "utf8" : "utf16";
        code.Append("inline constexpr auto root_json_encoding = luminpack::json_encoding::").Append(encoding).Append(";\n");
        code.Append("inline constexpr luminpack::json_read_options root_json_options{")
            .Append(Math.Max(0, profile.GetProperty("jsonDepth").GetInt32()).ToString(CultureInfo.InvariantCulture)).Append("};\n");
        code.Append("inline constexpr luminpack::reader_limits root_binary_limits{")
            .Append(profile.GetProperty("maxCollection").GetInt32().ToString(CultureInfo.InvariantCulture)).Append(", ")
            .Append(profile.GetProperty("maxString").GetInt32().ToString(CultureInfo.InvariantCulture)).Append("};\n");
    }

    /// <summary>Appends a C++ type alias.</summary>
    /// <param name="name">The declared alias name.</param>
    /// <param name="type">The type expression.</param>
    private void Alias(string name, string type) => code.Append("using ").Append(name).Append(" = ").Append(type).Append(";\n");

    /// <summary>Spells a codec specialization with one argument per line.</summary>
    /// <param name="name">The alias identifier.</param>
    /// <param name="type">The template name.</param>
    /// <param name="arguments">Its ordered template arguments.</param>
    private void Alias(string name, string type, IEnumerable<string> arguments)
    {
        code.Append("using ").Append(name).Append(" = ").Append(type).Append("<\n    ");
        code.Append(string.Join(",\n    ", arguments)).Append("\n>;\n");
    }

    /// <summary>Constructs the sequence bindings from its element.</summary>
    /// <param name="item">The element codecs.</param>
    /// <returns>The array or list bindings.</returns>
    private static Binding Sequence(Binding item)
    {
        string binary = "luminpack::list_codec<" + item.Binary + ">";
        return new Binding(binary + "::value_type", binary, "luminpack::json_list_codec<" + item.Json + ">");
    }

    /// <summary>Spells a supported scalar with an explicit C++ wire width.</summary>
    /// <param name="type">The normalized C# type.</param>
    /// <returns>The scalar spelling or null when there is no binding.</returns>
    private static string? Scalar(string type) => type switch
    {
        "sbyte" or "System.SByte" => "std::int8_t",
        "byte" or "System.Byte" => "std::uint8_t",
        "short" or "System.Int16" => "std::int16_t",
        "ushort" or "System.UInt16" => "std::uint16_t",
        "int" or "System.Int32" => "std::int32_t",
        "uint" or "System.UInt32" => "std::uint32_t",
        "long" or "System.Int64" => "std::int64_t",
        "ulong" or "System.UInt64" => "std::uint64_t",
        "float" or "System.Single" => "float",
        "double" or "System.Double" => "double",
        "char" or "System.Char" => "char16_t",
        _ => null
    };

    /// <summary>Removes C# source qualification without discarding a nullable type wrapper.</summary>
    /// <param name="value">The compiler's C# spelling.</param>
    /// <returns>The assembly-local identity.</returns>
    private static string Normalize(string value) => value.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();

    /// <summary>Finds the comma separating a pair of generic arguments.</summary>
    /// <param name="arguments">The arguments without their outer brackets.</param>
    /// <returns>The outer comma, or minus one.</returns>
    private static int ArgumentSeparator(string arguments)
    {
        int depth = 0;
        for (int index = 0; index < arguments.Length; index++)
        {
            char character = arguments[index];
            if (character is '<' or '[')
                depth++;
            else if (character is '>' or ']')
                depth--;
            else if (character == ',' && depth == 0)
                return index;
        }
        return -1;
    }

    /// <summary>Spells a C++ template application.</summary>
    /// <param name="type">The template name.</param>
    /// <param name="arguments">The ordered template arguments.</param>
    /// <returns>The closed template spelling.</returns>
    private static string Apply(string type, IEnumerable<string> arguments) => type + "<" + string.Join(", ", arguments) + ">";

    /// <summary>Encodes any identifier suffix without collisions or language punctuation.</summary>
    /// <param name="value">The original identifier.</param>
    /// <returns>A C++-safe suffix.</returns>
    private static string Identifier(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
                result.Append(character);
            else
                result.Append("_x").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    /// <summary>Writes a safe UTF-16 constant retaining each C# property-name code unit.</summary>
    /// <param name="value">The JSON property name.</param>
    /// <returns>A C++ UTF-16 string expression.</returns>
    private static string Literal(string value)
    {
        if (value.All(static character => character >= ' ' && character <= '~' && character is not '"' and not '\\'))
            return "u\"" + value + "\"";
        return string.Join(" ", value.Select(static character =>
            "u\"\\x" + ((int)character).ToString("x4", CultureInfo.InvariantCulture) + "\""));
    }

    /// <summary>Spells an existing binary string profile.</summary>
    /// <param name="profile">The Next profile tag.</param>
    /// <returns>The matching C++ enum constant.</returns>
    private static string Profile(int profile)
    {
        if ((uint)profile >= (uint)Profiles.Length)
            throw Missing("string profile", profile.ToString(CultureInfo.InvariantCulture));
        return "luminpack::string_profile::" + Profiles[profile];
    }

    /// <summary>Formats one exact wire descriptor.</summary>
    /// <param name="value">The descriptor emitted by Next.</param>
    /// <returns>The C++ unsigned literal.</returns>
    private static string Hex(ulong value) => "0x" + value.ToString("x16", CultureInfo.InvariantCulture) + "ull";

    /// <summary>Reads a required manifest string property.</summary>
    /// <param name="node">The manifest object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The non-null property value.</returns>
    private static string Text(JsonElement node, string name) => node.GetProperty(name).GetString() ??
        throw new InvalidOperationException("Missing LuminPack manifest text property " + name + ".");

    /// <summary>Checks a Boolean projection field, including optional fields absent in version 1.</summary>
    /// <param name="node">The manifest object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>Whether it is set.</returns>
    private static bool Flag(JsonElement node, string name) =>
        node.TryGetProperty(name, out JsonElement flag) && flag.ValueKind == JsonValueKind.True;

    /// <summary>Identifies an unsupported wire shape before publishing any generated file.</summary>
    /// <param name="label">The declaration or member.</param>
    /// <param name="reason">The unsupported shape.</param>
    /// <returns>The generation failure.</returns>
    private static InvalidOperationException Missing(string label, string reason) =>
        new InvalidOperationException("Cannot emit C++ for " + label + ": " + reason + ".");

    private readonly record struct Binding(string Type, string Binary, string Json, string? Payload = null,
        string? JsonPayload = null);
}
