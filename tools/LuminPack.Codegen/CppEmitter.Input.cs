using System.Text.Json;

internal sealed partial class CppEmitter
{
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
