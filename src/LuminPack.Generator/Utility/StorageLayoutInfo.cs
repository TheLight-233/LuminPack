using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Reads actual CLR layout from source annotations or a referenced module's metadata tables.</summary>
internal sealed class StorageLayoutInfo
{
    private static readonly ConditionalWeakTable<INamedTypeSymbol, StorageLayoutInfo> Cache = new();
    internal StructLayout Kind = StructLayout.Default;
    internal int Pack;
    internal int Size;
    internal int CharSet;
    private readonly Dictionary<string, int> _offsets = new(StringComparer.Ordinal);

    /// <summary>Obtains declaration-owned layout without retaining compilations in frozen generation plans.</summary>
    /// <param name="type">The source or imported storage declaration.</param>
    /// <returns>The same layout for every construction of this declaration.</returns>
    internal static StorageLayoutInfo Get(INamedTypeSymbol type)
        => Cache.GetValue(type.OriginalDefinition, Read);

    /// <summary>Obtains the explicit offset of a field, including imported pseudo-custom attributes.</summary>
    /// <param name="field">The actual storage field.</param>
    /// <returns>The explicit offset, or minus one when none was declared.</returns>
    internal static int FieldOffset(IFieldSymbol field)
    {
        foreach (var attribute in field.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.FieldOffsetAttribute")
                return (int)attribute.ConstructorArguments[0].Value;
        return Get(field.ContainingType)._offsets.TryGetValue(field.MetadataName, out int offset) ? offset : -1;
    }

    /// <summary>Emits the ABI-preserving attribute for a Local storage replica.</summary>
    /// <param name="builder">The generated declaration builder.</param>
    /// <param name="indent">The declaration indentation.</param>
    internal void AppendAttribute(StringBuilder builder, string indent)
    {
        if (Kind == StructLayout.Default) return;
        builder.Append(indent).Append("[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.").Append(Kind);
        if (Pack != 0) builder.Append(", Pack = ").Append(Pack);
        if (Size != 0) builder.Append(", Size = ").Append(Size);
        if (CharSet != 0) builder.Append(", CharSet = (global::System.Runtime.InteropServices.CharSet)").Append(CharSet);
        builder.AppendLine(")]");
    }

    /// <summary>Reads source attributes first, then the defining module's type and field layout tables.</summary>
    /// <param name="type">The original storage declaration.</param>
    /// <returns>The declaration's layout with no runtime probing.</returns>
    private static StorageLayoutInfo Read(INamedTypeSymbol type)
    {
        var result = new StorageLayoutInfo();
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.InteropServices.StructLayoutAttribute") continue;
            result.Kind = (int)attribute.ConstructorArguments[0].Value switch
            {
                0 => StructLayout.Sequential,
                2 => StructLayout.Explicit,
                _ => StructLayout.Auto
            };
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "Pack") result.Pack = (int)argument.Value.Value;
                if (argument.Key == "Size") result.Size = (int)argument.Value.Value;
                if (argument.Key == "CharSet") result.CharSet = (int)argument.Value.Value;
            }
            return result;
        }
        // Source modules have no PE image. Older Unity compiler implementations throw here instead of returning null.
        if (type.TypeKind == TypeKind.Error || type.DeclaringSyntaxReferences.Length != 0) return result;
        var metadata = type.ContainingModule.GetMetadata();
        if (metadata == null) return result;
        var reader = metadata.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            if (!Matches(reader, definition, type)) continue;
            result.Kind = (definition.Attributes & TypeAttributes.LayoutMask) switch
            {
                TypeAttributes.SequentialLayout => StructLayout.Sequential,
                TypeAttributes.ExplicitLayout => StructLayout.Explicit,
                _ => StructLayout.Auto
            };
            var layout = definition.GetLayout();
            result.Pack = layout.PackingSize;
            result.Size = layout.Size;
            result.CharSet = (definition.Attributes & TypeAttributes.StringFormatMask) switch
            {
                TypeAttributes.UnicodeClass => 3,
                TypeAttributes.AutoClass => 4,
                _ => 2
            };
            foreach (var fieldHandle in definition.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                int offset = field.GetOffset();
                if (offset >= 0) result._offsets.Add(reader.GetString(field.Name), offset);
            }
            return result;
        }
        return result;
    }

    /// <summary>Matches nested metadata identities without confusing equal names from different namespaces.</summary>
    /// <param name="reader">The defining module's metadata.</param>
    /// <param name="definition">The candidate type definition.</param>
    /// <param name="type">The original compiler symbol.</param>
    /// <returns>Whether both identify the same declaration.</returns>
    private static bool Matches(MetadataReader reader, TypeDefinition definition, INamedTypeSymbol type)
    {
        if (reader.GetString(definition.Name) != type.MetadataName) return false;
        var declaring = definition.GetDeclaringType();
        if (type.ContainingType != null)
            return !declaring.IsNil && Matches(reader, reader.GetTypeDefinition(declaring), type.ContainingType);
        return declaring.IsNil && reader.GetString(definition.Namespace) ==
            (type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString());
    }
}
