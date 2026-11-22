using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;

/// <summary>Publishes sequential CLI value-type field order for the C++ raw-copy proof.</summary>
internal static class ValueLayoutExport
{
    /// <summary>Collects layouts that can be inspected without executing assembly code.</summary>
    /// <param name="output">The manifest writer.</param>
    /// <param name="metadata">The managed assembly metadata.</param>
    internal static void Write(Utf8JsonWriter output, MetadataReader metadata)
    {
        var candidates = new SortedDictionary<string, TypeDefinition>(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (type.BaseType.Kind != HandleKind.TypeReference ||
                (type.Attributes & TypeAttributes.LayoutMask) != TypeAttributes.SequentialLayout ||
                type.GetLayout().Size != 0 || type.GetLayout().PackingSize != 0)
                continue;
            TypeReference parent = metadata.GetTypeReference((TypeReferenceHandle)type.BaseType);
            if (metadata.GetString(parent.Name) == "ValueType" && metadata.GetString(parent.Namespace) == "System")
                candidates.Add(EnumExport.Name(metadata, type), type);
        }

        output.WriteStartArray("valueLayouts");
        foreach (var candidate in candidates)
        {
            output.WriteStartObject();
            output.WriteString("type", candidate.Key);
            output.WriteStartArray("fields");
            foreach (FieldDefinitionHandle handle in candidate.Value.GetFields())
            {
                FieldDefinition field = metadata.GetFieldDefinition(handle);
                if ((field.Attributes & FieldAttributes.Static) == 0)
                    output.WriteStringValue(metadata.GetString(field.Name));
            }
            output.WriteEndArray();
            output.WriteEndObject();
        }
        output.WriteEndArray();
    }
}
