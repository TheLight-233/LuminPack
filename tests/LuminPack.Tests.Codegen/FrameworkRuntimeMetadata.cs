using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

/// <summary>Builds metadata-only older-runtime images for optional generated API binding checks.</summary>
internal static class FrameworkRuntimeMetadata
{
    /// <summary>Supplies the legacy pointer cursor signature for generator selection without executing the modified image.</summary>
    /// <param name="references">The platform references and compatible Core asset.</param>
    /// <returns>The same reference set with the writer cursor represented as a native pointer.</returns>
    internal static Microsoft.CodeAnalysis.MetadataReference[] WithPointerCursor(Microsoft.CodeAnalysis.MetadataReference[] references)
    {
        var runtime = references.Single(reference => Path.GetFileName(reference.Display) == "LuminPack.Core.dll");
        using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(runtime.Display!);
        var module = assembly.MainModule;
        var cursor = module.GetType("LuminPack.Core.LuminPackWriter").Fields.Single(field => field.Name == "Cursor");
        cursor.FieldType = new Mono.Cecil.PointerType(module.TypeSystem.Byte);
        module.Mvid = Guid.NewGuid();
        using var stream = new MemoryStream();
        assembly.Write(stream);
        return references.Select(reference => ReferenceEquals(reference, runtime)
            ? Microsoft.CodeAnalysis.MetadataReference.CreateFromImage(stream.ToArray(), filePath: "PointerCore.dll")
            : reference).ToArray();
    }

    /// <summary>Renames selected methods and refreshes the module identity without executing the resulting image.</summary>
    /// <param name="path">The referenced runtime image.</param>
    /// <param name="names">The optional method names to hide, optionally qualified by declaring type name.</param>
    /// <returns>An image for binding and compilation only.</returns>
    internal static byte[] WithoutMethods(string path, params string[] names)
    {
        byte[] image = File.ReadAllBytes(path);
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int methods = pe.PEHeaders.MetadataStartOffset + metadata.GetTableMetadataOffset(TableIndex.MethodDef);
        int rowSize = metadata.GetTableRowSize(TableIndex.MethodDef);
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            string name = metadata.GetString(method.Name);
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                var owner = metadata.GetTypeDefinition(method.GetDeclaringType());
                name = metadata.GetString(owner.Name) + "." + name;
                if (!names.Contains(name, StringComparer.Ordinal)) continue;
            }
            // Skip the first character for this definition only. Member references can share its string heap entry.
            int nameColumn = methods + (MetadataTokens.GetRowNumber(handle) - 1) * rowSize + 8;
            int renamed = MetadataTokens.GetHeapOffset(method.Name) + 1;
            if (metadata.GetHeapSize(HeapIndex.String) > ushort.MaxValue)
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(nameColumn), renamed);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(nameColumn), checked((ushort)renamed));
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
