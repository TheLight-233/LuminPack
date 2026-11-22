using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;

/// <summary>Reads declared enum constants from metadata without loading the assembly.</summary>
internal static class EnumExport
{
    /// <summary>Appends the assembly's enum declarations to the manifest.</summary>
    /// <param name="output">The manifest writer.</param>
    /// <param name="metadata">The assembly metadata.</param>
    internal static void Write(Utf8JsonWriter output, MetadataReader metadata)
    {
        var enums = new SortedDictionary<string, TypeDefinition>(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (type.BaseType.Kind != HandleKind.TypeReference)
                continue;
            TypeReference parent = metadata.GetTypeReference((TypeReferenceHandle)type.BaseType);
            if (metadata.GetString(parent.Name) == "Enum" && metadata.GetString(parent.Namespace) == "System")
                enums.Add(Name(metadata, type), type);
        }

        output.WriteStartArray("enums");
        foreach (var entry in enums)
        {
            output.WriteStartObject();
            output.WriteString("type", entry.Key);
            output.WriteString("underlying", Underlying(metadata, entry.Value));
            output.WriteStartArray("members");
            foreach (FieldDefinitionHandle handle in entry.Value.GetFields())
            {
                FieldDefinition field = metadata.GetFieldDefinition(handle);
                if ((field.Attributes & FieldAttributes.Literal) == 0)
                    continue;
                Constant constant = metadata.GetConstant(field.GetDefaultValue());
                output.WriteStartObject();
                output.WriteString("name", metadata.GetString(field.Name));
                output.WriteString("value", ConstantText(metadata, constant));
                output.WriteEndObject();
            }
            output.WriteEndArray();
            output.WriteEndObject();
        }
        output.WriteEndArray();
    }

    /// <summary>Gets a fully qualified C# name without the source namespace prefix.</summary>
    /// <param name="metadata">The assembly metadata.</param>
    /// <param name="type">The enum declaration.</param>
    /// <returns>The qualified name, including any enclosing type names.</returns>
    internal static string Name(MetadataReader metadata, TypeDefinition type)
    {
        string name = metadata.GetString(type.Name);
        TypeDefinitionHandle container = type.GetDeclaringType();
        if (!container.IsNil)
            return Name(metadata, metadata.GetTypeDefinition(container)) + "." + name;
        string scope = metadata.GetString(type.Namespace);
        return scope.Length == 0 ? name : scope + "." + name;
    }

    /// <summary>Reads an enum's special storage field to identify its integral type.</summary>
    /// <param name="metadata">The assembly metadata.</param>
    /// <param name="type">The enum declaration.</param>
    /// <returns>The matching C# scalar keyword.</returns>
    private static string Underlying(MetadataReader metadata, TypeDefinition type)
    {
        foreach (FieldDefinitionHandle handle in type.GetFields())
        {
            FieldDefinition field = metadata.GetFieldDefinition(handle);
            if (metadata.GetString(field.Name) != "value__")
                continue;
            BlobReader signature = metadata.GetBlobReader(field.Signature);
            if (signature.ReadSignatureHeader().Kind != SignatureKind.Field)
                break;
            return signature.ReadSignatureTypeCode() switch
            {
                SignatureTypeCode.SByte => "sbyte",
                SignatureTypeCode.Byte => "byte",
                SignatureTypeCode.Int16 => "short",
                SignatureTypeCode.UInt16 => "ushort",
                SignatureTypeCode.Int32 => "int",
                SignatureTypeCode.UInt32 => "uint",
                SignatureTypeCode.Int64 => "long",
                SignatureTypeCode.UInt64 => "ulong",
                _ => throw new BadImageFormatException("Unsupported enum storage type in the managed assembly.")
            };
        }
        throw new BadImageFormatException("The enum declaration lacks an integral storage field.");
    }

    /// <summary>Formats a metadata constant without losing the range of a signed or unsigned 64-bit enum.</summary>
    /// <param name="metadata">The assembly metadata.</param>
    /// <param name="constant">The enum field's constant.</param>
    /// <returns>The exact decimal representation.</returns>
    private static string ConstantText(MetadataReader metadata, Constant constant)
    {
        BlobReader value = metadata.GetBlobReader(constant.Value);
        IFormattable number = constant.TypeCode switch
        {
            ConstantTypeCode.SByte => value.ReadSByte(),
            ConstantTypeCode.Byte => value.ReadByte(),
            ConstantTypeCode.Int16 => value.ReadInt16(),
            ConstantTypeCode.UInt16 => value.ReadUInt16(),
            ConstantTypeCode.Int32 => value.ReadInt32(),
            ConstantTypeCode.UInt32 => value.ReadUInt32(),
            ConstantTypeCode.Int64 => value.ReadInt64(),
            ConstantTypeCode.UInt64 => value.ReadUInt64(),
            _ => throw new BadImageFormatException("Unsupported enum constant in the managed assembly.")
        };
        return number.ToString(null, CultureInfo.InvariantCulture);
    }
}
