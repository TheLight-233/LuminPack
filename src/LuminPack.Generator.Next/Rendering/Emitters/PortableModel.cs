#nullable disable
using System;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>One scalar transfer of a portable struct's packed wire.</summary>
internal sealed class PortableLeaf : IEquatable<PortableLeaf>
{
    /// <summary>The storage access below the positional view, such as <c>F0</c> or <c>F1.Item2</c>.</summary>
    internal readonly string Access;
    /// <summary>The C# spelling of the transferred scalar.</summary>
    internal readonly string Scalar;
    /// <summary>The packed width of the scalar.</summary>
    internal readonly int Size;
    /// <summary>Whether the field is volatile, whose write snapshots it first.</summary>
    internal readonly bool Volatile;

    /// <summary>Captures one scalar transfer.</summary>
    internal PortableLeaf(string access, string scalar, int size, bool isVolatile)
    {
        Volatile = isVolatile;
        Size = size;
        Scalar = scalar;
        Access = access;
    }

    /// <inheritdoc/>
    public bool Equals(PortableLeaf other) => other != null && Access == other.Access && Scalar == other.Scalar &&
        Size == other.Size && Volatile == other.Volatile;

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as PortableLeaf);

    /// <inheritdoc/>
    public override int GetHashCode() => Access.GetHashCode();
}

/// <summary>One field of a portable struct's positional storage view.</summary>
internal sealed class PortableViewField : IEquatable<PortableViewField>
{
    /// <summary>The view field name, such as <c>F0</c>.</summary>
    internal readonly string Name;
    /// <summary>The C# spelling of the view field's type.</summary>
    internal readonly string Type;
    /// <summary>Whether the field is volatile.</summary>
    internal readonly bool Volatile;
    /// <summary>The declared explicit layout offset, or a negative value for a sequential field.</summary>
    internal readonly int Offset;
    /// <summary>The scalar transfers the field contributes, in wire order.</summary>
    internal readonly PortableLeaf[] Leaves;

    /// <summary>Captures one view field.</summary>
    internal PortableViewField(string name, string type, bool isVolatile, int offset, PortableLeaf[] leaves)
    {
        Offset = offset;
        Leaves = leaves;
        Volatile = isVolatile;
        Type = type;
        Name = name;
    }

    /// <inheritdoc/>
    public bool Equals(PortableViewField other) => other != null && Name == other.Name && Type == other.Type &&
        Volatile == other.Volatile && Offset == other.Offset && Leaves.SequenceEqual(other.Leaves);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as PortableViewField);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}

/// <summary>The positional storage view of a portable struct, whose fields are copied one by one at packed wire offsets.</summary>
internal sealed class PortableView : IEquatable<PortableView>
{
    /// <summary>The view's declaration name.</summary>
    internal readonly string Name;
    /// <summary>The view's fields in declaration order.</summary>
    internal readonly PortableViewField[] Fields;
    /// <summary>Every scalar transfer in wire order.</summary>
    internal readonly PortableLeaf[] Leaves;
    /// <summary>The complete packed wire width.</summary>
    internal readonly int WireSize;

    /// <summary>Captures one view.</summary>
    internal PortableView(string name, PortableViewField[] fields)
    {
        Name = name;
        Fields = fields;
        Leaves = fields.SelectMany(field => field.Leaves).ToArray();
        WireSize = Leaves.Sum(leaf => leaf.Size);
    }

    /// <inheritdoc/>
    public bool Equals(PortableView other) => other != null && Name == other.Name && WireSize == other.WireSize &&
        Fields.SequenceEqual(other.Fields);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as PortableView);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}

