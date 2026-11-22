using System.Text.Json;

internal sealed partial class CppEmitter
{
    /// <summary>Projects the managed compressed collection operation without changing its JSON codec.</summary>
    /// <param name="shape">The compiler-projected member shape.</param>
    /// <param name="profile">The active string profile.</param>
    /// <param name="label">The consuming member used in diagnostics.</param>
    /// <returns>The typed collection bindings.</returns>
    private Binding BindCompressedMember(JsonElement shape, int profile, string label)
    {
        if (shape.TryGetProperty("formatter", out JsonElement formatter) && formatter.ValueKind == JsonValueKind.Object)
            return BindCompressedSequence(formatter, profile, label);
        if (Text(shape, "kind") is not ("Array" or "List") || shape.GetProperty("rank").GetInt32() > 1)
            throw Missing(label, "a compressed member without a vector or List formatter");
        return BindCompressedElement(Text(shape, "element"), profile, label);
    }

    /// <summary>Preserves the ordinary count at each forwarded vector rank and compresses the contiguous leaf.</summary>
    /// <param name="formatter">The normalized array or List formatter.</param>
    /// <param name="profile">The active string profile.</param>
    /// <param name="label">The consuming member used in diagnostics.</param>
    /// <returns>The type and both format bindings.</returns>
    private Binding BindCompressedSequence(JsonElement formatter, int profile, string label)
    {
        string definition = Text(formatter, "definition");
        if (definition is not ("$array" or "System.Collections.Generic.List") || formatter.GetProperty("rank").GetInt32() > 1)
            throw Missing(label, "a compressed member without a vector or List formatter");

        JsonElement element = formatter.GetProperty("arguments")[0];
        if (Text(element, "definition") == "$array")
            return Sequence(BindCompressedSequence(element, profile, label));
        if (!Flag(element, "rawCollection") || element.GetProperty("layout").ValueKind != JsonValueKind.Null ||
            element.GetProperty("nullableEncoding").GetInt32() != 0)
            throw Missing(label, "a compressed collection without a proven raw element representation");
        return BindCompressedElement(Text(element, "type"), profile, label);
    }

    /// <summary>Binds CLR scalar, enum, or headerless unmanaged storage to the compressed collection codec.</summary>
    /// <param name="rawType">The compiler's element type spelling.</param>
    /// <param name="profile">The active string profile.</param>
    /// <param name="label">The consuming member used in diagnostics.</param>
    /// <returns>The raw Binary collection and its ordinary element-wise JSON.</returns>
    private Binding BindCompressedElement(string rawType, int profile, string label)
    {
        string type = Normalize(rawType);
        if (type is not ("bool" or "System.Boolean" or "nint" or "System.IntPtr" or "nuint" or "System.UIntPtr") &&
            Scalar(type) == null && !enums.ContainsKey(type) &&
            (!contracts.TryGetValue(type, out JsonElement contract) || Text(contract, "kind") != "Struct" ||
                Text(contract, "storageKind") != "RawUnmanaged" || Flag(contract, "versioned") ||
                contract.GetProperty("storage").ValueKind != JsonValueKind.Null))
            throw Missing(label, "a compressed collection without a proven raw element representation for " + rawType);

        Binding element = BindType(type, profile, label, false);
        Binding sequence = Sequence(element);
        return sequence with { Binary = Apply("luminpack::compressed_array_codec", new[] { element.Binary }) };
    }
}
