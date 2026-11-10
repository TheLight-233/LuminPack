using System;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>The schema facts of a version-tolerant record, reduced to the strings and bytes its emitted text depends on.</summary>
/// <remarks>The Next model does not carry the schema bytes, revision prefix or former names yet, so the model factory leaves
/// <see cref="PayloadRenderModel.Version"/> null. The class remains the renderer's contract for the version migration task.</remarks>
internal sealed class VersionSchema : IEquatable<VersionSchema>
{
    /// <summary>The qualified schema class.</summary>
    internal readonly string Schema;

    /// <summary>The schema class's own name.</summary>
    internal readonly string Name;

    /// <summary>The field table entries.</summary>
    internal readonly string[] FieldEntries;

    /// <summary>The constant member metadata.</summary>
    internal readonly byte[] Metadata;

    /// <summary>The constant revision-2 prefix.</summary>
    internal readonly byte[] Prefix;

    /// <summary>The declared types of the read temporaries.</summary>
    internal readonly string[] TempTypes;

    /// <summary>The analyzed type names of the members.</summary>
    internal readonly string[] TypeNames;

    /// <summary>The fully qualified member types.</summary>
    internal readonly string[] QualifiedTypes;

    /// <summary>The root-private layout mirror.</summary>
    internal readonly string Layout;

    /// <summary>The mirror's field declarations in storage order.</summary>
    internal readonly string[] LayoutFields;

    /// <summary>The former names of each member.</summary>
    internal readonly string[][] OldNames;

    /// <summary>Captures a version schema.</summary>
    /// <param name="schema">The qualified schema class.</param>
    /// <param name="metadata">The constant member metadata.</param>
    /// <param name="prefix">The constant revision prefix.</param>
    /// <param name="fieldEntries">The field table entries.</param>
    /// <param name="tempTypes">The read temporary types.</param>
    /// <param name="typeNames">The analyzed member type names.</param>
    /// <param name="qualifiedTypes">The fully qualified member types.</param>
    /// <param name="layout">The layout mirror name.</param>
    /// <param name="layoutFields">The layout mirror fields.</param>
    /// <param name="oldNames">The former names of each member.</param>
    internal VersionSchema(string schema, byte[] metadata, byte[] prefix, string[] fieldEntries, string[] tempTypes, string[] typeNames,
        string[] qualifiedTypes, string layout, string[] layoutFields, string[][] oldNames)
    {
        OldNames = oldNames;
        Schema = schema;
        Name = schema.Substring(schema.LastIndexOf(".Version_", StringComparison.Ordinal) + 1);
        Metadata = metadata;
        Prefix = prefix;
        FieldEntries = fieldEntries;
        TempTypes = tempTypes;
        TypeNames = typeNames;
        QualifiedTypes = qualifiedTypes;
        Layout = layout;
        LayoutFields = layoutFields;
    }

    /// <summary>The FNV-1a fingerprint of the prefix after its four header bytes.</summary>
    internal ulong Fingerprint
    {
        get
        {
            ulong fingerprint = 14695981039346656037UL;
            foreach (byte value in Prefix.Skip(4))
                fingerprint = (fingerprint ^ value) * 1099511628211UL;
            return fingerprint;
        }
    }

    /// <inheritdoc/>
    public bool Equals(VersionSchema? other) => other != null && Schema == other.Schema && Layout == other.Layout &&
        Metadata.SequenceEqual(other.Metadata) && Prefix.SequenceEqual(other.Prefix) && FieldEntries.SequenceEqual(other.FieldEntries) &&
        TempTypes.SequenceEqual(other.TempTypes) && TypeNames.SequenceEqual(other.TypeNames) && QualifiedTypes.SequenceEqual(other.QualifiedTypes) &&
        LayoutFields.SequenceEqual(other.LayoutFields) && OldNames.Length == other.OldNames.Length &&
        OldNames.Zip(other.OldNames, (left, right) => left.SequenceEqual(right)).All(same => same);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as VersionSchema);

    /// <inheritdoc/>
    public override int GetHashCode() => Schema.GetHashCode();
}
