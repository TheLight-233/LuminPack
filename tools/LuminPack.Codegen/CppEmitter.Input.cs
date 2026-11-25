using System.Text.Json;

internal sealed partial class CppEmitter
{
    /// <summary>Emits root-scoped Binary operations with the projected read policy and ownership.</summary>
    /// <param name="manifest">The compiler-projected manifest.</param>
    private void EmitRootOperations(JsonElement manifest)
    {
        bool graph = declaredReferenceStorage.Count != 0;
        string writer = graph ? "write_binary_reference_document" : "write_binary_document";
        code.Append("\n[[nodiscard]] inline auto deserialize(std::span<const std::byte> input)\n{\n    return ");
        if (manifest.TryGetProperty("requireCompleteBinaryInput", out _))
            code.Append("read_root_binary(input)");
        else
            code.Append(graph ? "luminpack::read_binary_reference_document<root_binary>(input, root_binary_limits, root_binary_reference_depth)"
                : "luminpack::read_binary_document<root_binary>(input, root_binary_limits)");
        code.Append(";\n}\n\n[[nodiscard]] inline auto serialize(const root_value& value, std::span<std::byte> output)\n{\n")
            .Append("    return luminpack::").Append(writer).Append("<root_binary>(output, value);\n}\n");
        // A retry must never replay application-defined writes or virtual Union callbacks.
        bool retryable = !generated.Keys.Any(key => contracts.TryGetValue(key.Type, out JsonElement contract) &&
            (Text(contract, "kind") == "Custom" || (rttiBases != null && Text(contract, "kind") == "Union")));
        if (retryable)
            code.Append("\n[[nodiscard]] inline auto serialize(const root_value& value)\n{\n")
                .Append("    return luminpack::detail::write_owned([&](std::span<std::byte> output) { return serialize(value, output); });\n}\n");
        if (graph)
        {
            code.Append("\n[[nodiscard]] inline auto serialize(const luminpack::reference_document<root_value>& doc, std::span<std::byte> output)\n")
                .Append("{\n    return serialize(doc.value, output);\n}\n");
            if (retryable)
                code.Append("\n[[nodiscard]] inline auto serialize(const luminpack::reference_document<root_value>& doc)\n")
                    .Append("{\n    return serialize(doc.value);\n}\n")
                    .Append("inline auto luminpack_serialize_root(std::type_identity<luminpack::reference_document<root_value>>, const luminpack::reference_document<root_value>& doc)\n")
                    .Append("{\n    return serialize(doc);\n}\n");
        }
        code.Append("\n[[nodiscard]] inline auto luminpack_deserialize_root(std::type_identity<root_value>, std::span<const std::byte> input)\n")
            .Append("{\n    return deserialize(input);\n}\n");
        if (retryable)
            code.Append("\n[[nodiscard]] inline auto luminpack_serialize_root(std::type_identity<root_value>, const root_value& value)\n")
                .Append("{\n    return serialize(value);\n}\n");
    }

    /// <summary>Emits the managed assembly's value-returning Binary read policy.</summary>
    /// <param name="manifest">The compiler-projected manifest.</param>
    private void EmitRootBinaryReadFacade(JsonElement manifest)
    {
        if (!manifest.TryGetProperty("requireCompleteBinaryInput", out JsonElement complete))
            return;
        if (complete.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException("Invalid LuminPack C++ complete binary input policy.");

        bool graph = declaredReferenceStorage.Count != 0;
        code.Append("inline constexpr bool root_binary_requires_complete_input = ")
            .Append(complete.GetBoolean() ? "true" : "false").Append(";\n\n")
            .Append("[[nodiscard]] inline auto read_root_binary(std::span<const std::byte> input)\n{\n")
            .Append(graph ? "    return luminpack::read_binary_reference_document" : "    return luminpack::read_binary_document")
            .Append("<root_binary, root_binary_requires_complete_input>(input, root_binary_limits")
            .Append(graph ? ", root_binary_reference_depth" : string.Empty).Append(");\n}\n");
    }
}
