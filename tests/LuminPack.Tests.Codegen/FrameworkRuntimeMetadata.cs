using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

/// <summary>Builds metadata-only older-runtime images for optional generated API binding checks.</summary>
internal static class FrameworkRuntimeMetadata
{
    /// <summary>Renames selected methods and refreshes the module identity without executing the resulting image.</summary>
    /// <param name="path">The referenced runtime image.</param>
    /// <param name="names">The optional method names to hide.</param>
    /// <returns>An image for binding and compilation only.</returns>
    internal static byte[] WithoutMethods(string path, params string[] names)
    {
        byte[] image = File.ReadAllBytes(path);
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int strings = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.String);
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            string name = metadata.GetString(method.Name);
            if (!names.Contains(name, StringComparer.Ordinal)) continue;
            image[strings + MetadataTokens.GetHeapOffset(method.Name)] = (byte)'X';
            found.Add(name);
        }
        if (found.Count != names.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("An optional runtime API was not found.");
        byte[] mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToByteArray();
        int guids = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Guid);
        for (int offset = 0; offset < metadata.GetHeapSize(HeapIndex.Guid); offset += 16)
        {
            if (!image.AsSpan(guids + offset, 16).SequenceEqual(mvid)) continue;
            Guid.NewGuid().TryWriteBytes(image.AsSpan(guids + offset, 16));
            return image;
        }
        throw new InvalidOperationException("The compatibility module identity was not updated.");
    }
}
