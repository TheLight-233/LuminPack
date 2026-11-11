using System;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>One inheritance-preserving Local storage declaration, including hidden and ignored storage placeholders.</summary>
internal sealed class StorageMirrorModel : IEquatable<StorageMirrorModel>
{
    internal readonly string Name;
    internal readonly string? Base;
    internal readonly string[] Fields;
    internal readonly int[]? Offsets;
    internal readonly string? Layout;
    internal readonly string[] Constraints;

    /// <summary>Captures one physical level of a class storage mirror.</summary>
    /// <param name="name">The generated declaration name.</param>
    /// <param name="baseType">The preceding storage level.</param>
    /// <param name="fields">All declared instance storage slots.</param>
    /// <param name="offsets">Explicit field offsets, when applicable.</param>
    /// <param name="layout">The original CLR layout attribute.</param>
    /// <param name="constraints">The generic declaration constraints.</param>
    internal StorageMirrorModel(string name, string? baseType, string[] fields, int[]? offsets, string? layout, string[] constraints)
    {
        Name = name;
        Base = baseType;
        Fields = fields;
        Offsets = offsets;
        Layout = layout;
        Constraints = constraints;
    }

    /// <inheritdoc/>
    public bool Equals(StorageMirrorModel? other) => ReferenceEquals(this, other) || other != null && Name == other.Name &&
        Base == other.Base && Layout == other.Layout && Fields.SequenceEqual(other.Fields) && Constraints.SequenceEqual(other.Constraints) &&
        (Offsets ?? Array.Empty<int>()).SequenceEqual(other.Offsets ?? Array.Empty<int>());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StorageMirrorModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}
