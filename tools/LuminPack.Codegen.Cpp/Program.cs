using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

try
{
    if (args is ["export", var assemblyPath, var manifestPath])
        ContractExport.Write(assemblyPath, manifestPath);
    else if (args is ["generate", ..] && CppGenerateCommand.TryRun(args.AsSpan(1)))
        return 0;
    else
    {
        Console.Error.WriteLine("Usage: LuminPack.Codegen.Cpp export <assembly.dll> <manifest.json>");
        Console.Error.WriteLine("       LuminPack.Codegen.Cpp generate [--binary-profile <profile>] [--rtti] <manifest.json> <root-type> <output.h>");
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
    private const string CompleteBinaryInputMetadataKey = "LuminPack.Cpp.CompleteBinaryInput.v1";

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
        bool? completeBinaryInput = null;
        foreach (CustomAttributeHandle attribute in definition.GetCustomAttributes())
        {
            if (ReadMetadata(metadata, attribute) is not { } entry)
                continue;
            if (entry.Key == CompleteBinaryInputMetadataKey)
            {
                if (completeBinaryInput.HasValue || entry.Value is not ("true" or "false"))
                    throw new InvalidOperationException("Invalid LuminPack C++ complete binary input policy.");
                completeBinaryInput = entry.Value == "true";
                continue;
            }
            using JsonDocument document = JsonDocument.Parse(entry.Value);
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
            if (completeBinaryInput.HasValue)
                writer.WriteBoolean("requireCompleteBinaryInput", completeBinaryInput.Value);
            writer.WriteStartArray("contracts");
            foreach (JsonElement row in rows.Values)
                row.WriteTo(writer);
            writer.WriteEndArray();
            EnumExport.Write(writer, metadata);
            ValueLayoutExport.Write(writer, metadata);
            writer.WriteEndObject();
        });
    }

    /// <summary>Reads only standard assembly metadata with a recognized versioned LuminPack key.</summary>
    /// <param name="metadata">The input assembly's metadata reader.</param>
    /// <param name="handle">A candidate assembly custom attribute.</param>
    /// <returns>The name and contents of the metadata, or null when the attribute is unrelated.</returns>
    private static (string Key, string Value)? ReadMetadata(MetadataReader metadata, CustomAttributeHandle handle)
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
        if (blob.ReadUInt16() != 1)
            return null;
        string? key = blob.ReadSerializedString();
        if (key is not (MetadataKey or CompleteBinaryInputMetadataKey))
            return null;
        string? row = blob.ReadSerializedString();
        if (row == null || blob.ReadUInt16() != 0 || blob.RemainingBytes != 0)
            throw new BadImageFormatException("Invalid LuminPack C++ contract attribute.");
        return (key, row);
    }
}
