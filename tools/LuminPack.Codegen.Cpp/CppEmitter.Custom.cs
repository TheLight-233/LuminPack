using System.Text.Json;

internal sealed partial class CppEmitter
{
    /// <summary>Binds an opaque Custom contract to the application's matching typed codecs.</summary>
    /// <param name="contract">The validated Custom registration exported by Next.</param>
    /// <param name="name">The C++ aliases for the declared type.</param>
    /// <returns>The registered application's value, Binary and JSON codecs.</returns>
    private Binding EmitCustom(JsonElement contract, string name)
    {
        string adapter = name + "_binding";
        string binary = name + "_binary";
        string json = name + "_json";
        string value = name + "_value";
        Alias(adapter, "luminpack::custom_binding<" + Literal(Text(contract, "id")) + ">");
        Alias(binary, adapter + "::binary");
        Alias(value, binary + "::value_type");
        bool hasJson = Flag(contract, "customJson");
        if (hasJson)
        {
            code.Append("static_assert(std::same_as<").Append(value).Append(", ").Append(adapter)
                .Append("::json::value_type>, \"Custom Binary and JSON codecs must use the same value type.\");\n");
        }
        string arguments = hasJson ? value + ", " + adapter + "::json" : value;
        Alias(json, "luminpack::detail::registered_custom_json_codec<" + arguments + ">");
        code.Append('\n');
        return new Binding(value, binary, json);
    }
}
