using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

try
{
    if (args is ["export", var assemblyPath, var manifestPath])
        ContractExport.Write(assemblyPath, manifestPath);
    else if (args is ["generate", var inputPath, var root, var headerPath])
        CppEmitter.Generate(inputPath, root, headerPath);
    else if (args is ["generate", "--rtti", var rttiInput, var rttiRoot, var rttiHeader])
        CppEmitter.Generate(rttiInput, rttiRoot, rttiHeader, useRtti: true);
    else
    {
        Console.Error.WriteLine("Usage: LuminPack.Codegen.Cpp export <assembly.dll> <manifest.json>");
        Console.Error.WriteLine("       LuminPack.Codegen.Cpp generate [--rtti] <manifest.json> <root-type> <output.h>");
        return 2;
    }
    return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or BadImageFormatException or JsonException
    or InvalidOperationException or ArgumentException or KeyNotFoundException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

/// <summary>Extracts Next's wire projection without loading or running the input assembly.</summary>
internal static class ContractExport
{
    private const string MetadataKey = "LuminPack.Cpp.Contract.v1";

    /// <summary>Reads compiler-emitted metadata and replaces the manifest after validation succeeds.</summary>
    /// <param name="assemblyPath">The input managed assembly.</param>
    /// <param name="manifestPath">The JSON output path.</param>
    internal static void Write(string assemblyPath, string manifestPath)
    {
        using FileStream assembly = File.OpenRead(assemblyPath);
        using var reader = new PEReader(assembly, PEStreamOptions.PrefetchMetadata);
        if (!reader.HasMetadata)
            throw new BadImageFormatException("The input file has no managed assembly metadata.");

        MetadataReader metadata = reader.GetMetadataReader();
        if (!metadata.IsAssembly)
            throw new BadImageFormatException("The input file is not a managed assembly.");

        AssemblyDefinition definition = metadata.GetAssemblyDefinition();
        var rows = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (CustomAttributeHandle attribute in definition.GetCustomAttributes())
        {
            string? row = ReadRow(metadata, attribute);
            if (row == null)
                continue;
            using JsonDocument document = JsonDocument.Parse(row);
            JsonElement data = document.RootElement;
            if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("id", out JsonElement identity) ||
                identity.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(identity.GetString()))
                throw new InvalidOperationException("A LuminPack C++ contract row has no type identity.");
            if (!rows.TryAdd(identity.GetString()!, data.Clone()))
                throw new InvalidOperationException("Duplicate LuminPack C++ contract for " + identity.GetString() + ".");
        }

        if (rows.Count == 0)
            throw new InvalidOperationException("No LuminPack C++ contracts in this assembly. Enable LuminPackExportCppContract in its build.");

        OutputFile.Replace(assemblyPath, manifestPath, stream =>
        {
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("assembly", metadata.GetString(definition.Name));
            writer.WriteStartArray("contracts");
            foreach (JsonElement row in rows.Values)
                row.WriteTo(writer);
            writer.WriteEndArray();
            EnumExport.Write(writer, metadata);
            ValueLayoutExport.Write(writer, metadata);
            writer.WriteEndObject();
        });
    }

    /// <summary>Reads only the standard AssemblyMetadataAttribute constructor and the versioned LuminPack key.</summary>
    /// <param name="metadata">The input assembly's metadata reader.</param>
    /// <param name="handle">A candidate assembly custom attribute.</param>
    /// <returns>The JSON row or null when the attribute is unrelated.</returns>
    private static string? ReadRow(MetadataReader metadata, CustomAttributeHandle handle)
    {
        CustomAttribute attribute = metadata.GetCustomAttribute(handle);
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
            return null;

        MemberReference constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        if (constructor.Parent.Kind != HandleKind.TypeReference || metadata.GetString(constructor.Name) != ".ctor")
            return null;
        TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
        if (metadata.GetString(type.Namespace) != "System.Reflection" || metadata.GetString(type.Name) != "AssemblyMetadataAttribute")
            return null;

        BlobReader blob = metadata.GetBlobReader(attribute.Value);
        if (blob.ReadUInt16() != 1 || blob.ReadSerializedString() != MetadataKey)
            return null;
        string? row = blob.ReadSerializedString();
        if (row == null || blob.ReadUInt16() != 0 || blob.RemainingBytes != 0)
            throw new BadImageFormatException("Invalid LuminPack C++ contract attribute.");
        return row;
    }
}
