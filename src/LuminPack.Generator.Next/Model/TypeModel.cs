using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>The access of a generated declaration, mirroring the payload's own access.</summary>
internal enum TypeAccessibility
{
    /// <summary>A public payload.</summary>
    Public,

    /// <summary>An internal payload.</summary>
    Internal,
}

/// <summary>The payload protocol a type follows; each kind owns one renderer.</summary>
internal enum PayloadKind
{
    /// <summary>An ordinary or version-tolerant class record.</summary>
    Record,

    /// <summary>A value type: raw unmanaged, portable or managed record storage.</summary>
    Struct,

    /// <summary>A polymorphic root with tagged cases.</summary>
    Union,

    /// <summary>A reference-tracked graph node.</summary>
    CircleReference,
}

/// <summary>A normalized payload type. The model owns no Roslyn or legacy analysis object.</summary>
internal abstract class TypeModel : IEquatable<TypeModel>
{
    /// <summary>The type's identity.</summary>
    internal readonly TypeIdentity Identity;

    /// <summary>The declared accessibility.</summary>
    internal readonly TypeAccessibility Accessibility;

    /// <summary>The payload protocol.</summary>
    internal readonly PayloadKind Kind;

    /// <summary>The serialized members in wire order.</summary>
    internal readonly MemberModel[] Members;

    /// <summary>The effective wire policy, including the input policy and the string profile.</summary>
    internal readonly WirePolicy Policy;

    /// <summary>The constraint clauses of a generic definition, in declaration order.</summary>
    internal readonly string[] Constraints;

    /// <summary>Captures the facts shared by every payload kind.</summary>
    protected TypeModel(TypeIdentity identity, TypeAccessibility accessibility, PayloadKind kind, MemberModel[] members, WirePolicy policy,
        string[] constraints)
    {
        Identity = identity;
        Accessibility = accessibility;
        Kind = kind;
        Members = members;
        Policy = policy;
        Constraints = constraints;
    }

    /// <inheritdoc/>
    public bool Equals(TypeModel? other) => other != null && Kind == other.Kind && Identity.Equals(other.Identity) &&
        Accessibility == other.Accessibility && Policy.Equals(other.Policy) && Members.Length == other.Members.Length &&
        Same(Members, other.Members) && Same(Constraints, other.Constraints);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();

    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T>
        => left.Length == right.Length && System.Linq.Enumerable.SequenceEqual(left, right);
}

/// <summary>An ordinary class record, optionally version-tolerant.</summary>
internal sealed class RecordModel : TypeModel
{
    /// <summary>Whether the record writes a version schema prefix.</summary>
    internal readonly bool Versioned;

    /// <summary>Whether the record has a base payload whose members follow its own.</summary>
    internal readonly bool Derived;

    /// <summary>Whether every variable member is a string, which allows the proven object write.</summary>
    internal readonly bool Proven;

    /// <summary>Captures a record.</summary>
    internal RecordModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy, string[] constraints,
        bool versioned, bool derived, bool proven)
        : base(identity, accessibility, PayloadKind.Record, members, policy, constraints)
    {
        Proven = proven;
        Derived = derived;
        Versioned = versioned;
    }
}

/// <summary>The storage protocol of a value type.</summary>
internal enum StructStorage
{
    /// <summary>The struct's storage is its wire layout and is copied as one value.</summary>
    RawUnmanaged,

    /// <summary>Unmanaged fields are transferred one by one at their packed offsets.</summary>
    PortableUnmanaged,

    /// <summary>Reference members make the struct write a record body.</summary>
    ManagedRecord,
}

/// <summary>A value type with its storage protocol.</summary>
internal sealed class StructModel : TypeModel
{
    /// <summary>The storage protocol.</summary>
    internal readonly StructStorage Storage;

    /// <summary>The positional transfer layout of a portable unmanaged struct, or null.</summary>
    internal readonly PortableLayout? Layout;

    /// <summary>Captures a struct.</summary>
    internal StructModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy, string[] constraints,
        StructStorage storage, PortableLayout? layout)
        : base(identity, accessibility, PayloadKind.Struct, members, policy, constraints)
    {
        Layout = layout;
        Storage = storage;
    }
}

/// <summary>A positional view used to copy unmanaged fields at packed wire offsets.</summary>
internal sealed class PortableLayout
{
    /// <summary>The view type name.</summary>
    internal readonly string ViewName;

    /// <summary>The field count, which is also the number of wire slots.</summary>
    internal readonly int FieldCount;

    /// <summary>Captures a portable layout.</summary>
    internal PortableLayout(string viewName, int fieldCount)
    {
        FieldCount = fieldCount;
        ViewName = viewName;
    }
}

/// <summary>The dispatch strategy of a Union root.</summary>
internal enum UnionDispatchTier
{
    /// <summary>Every case overrides the virtual slot.</summary>
    Virtual,

    /// <summary>Sealed exact cases are matched by runtime type before the virtual slot.</summary>
    ExactPatterns,

    /// <summary>The root dispatches directly on the exact case type.</summary>
    Direct,
}

/// <summary>The read strategy of one Union case.</summary>
internal enum UnionCaseRead
{
    /// <summary>The case is a fixed record whose read body the dispatch expands.</summary>
    ExpandedFixed,

    /// <summary>The case owns a generated read entry the dispatch calls.</summary>
    OwnEntry,
}

/// <summary>One declared case of a Union root.</summary>
internal sealed class UnionCaseModel : IEquatable<UnionCaseModel>
{
    /// <summary>The narrow or wide tag.</summary>
    internal readonly int Tag;

    /// <summary>The case type.</summary>
    internal readonly TypeIdentity Type;

    /// <summary>The read strategy.</summary>
    internal readonly UnionCaseRead Read;

    /// <summary>Whether the case slot can be sealed.</summary>
    internal readonly bool Sealed;

    /// <summary>Whether the case payload uses the returning cursor kernel.</summary>
    internal readonly bool FixedCursor;

    /// <summary>Captures a Union case.</summary>
    internal UnionCaseModel(int tag, TypeIdentity type, UnionCaseRead read, bool sealedCase, bool fixedCursor)
    {
        FixedCursor = fixedCursor;
        Sealed = sealedCase;
        Read = read;
        Type = type;
        Tag = tag;
    }

    /// <inheritdoc/>
    public bool Equals(UnionCaseModel? other) => other != null && Tag == other.Tag && Type.Equals(other.Type) && Read == other.Read &&
        Sealed == other.Sealed && FixedCursor == other.FixedCursor;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as UnionCaseModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Tag;
}

/// <summary>A polymorphic root.</summary>
internal sealed class UnionModel : TypeModel
{
    /// <summary>The declared cases in tag order.</summary>
    internal readonly UnionCaseModel[] Cases;

    /// <summary>Whether the root is an interface rather than an abstract class.</summary>
    internal readonly bool IsInterface;

    /// <summary>Whether cases use the wide tag protocol.</summary>
    internal readonly bool WideTags;

    /// <summary>The canonical slot suffix.</summary>
    internal readonly string SlotSuffix;

    /// <summary>The compiled dispatch tier.</summary>
    internal readonly UnionDispatchTier Tier;

    /// <summary>Captures a Union root.</summary>
    internal UnionModel(TypeIdentity identity, TypeAccessibility accessibility, WirePolicy policy, string[] constraints, UnionCaseModel[] cases,
        bool isInterface, bool wideTags, string slotSuffix, UnionDispatchTier tier)
        : base(identity, accessibility, PayloadKind.Union, System.Array.Empty<MemberModel>(), policy, constraints)
    {
        Tier = tier;
        SlotSuffix = slotSuffix;
        WideTags = wideTags;
        IsInterface = isInterface;
        Cases = cases;
    }
}

/// <summary>A reference-tracked graph node.</summary>
internal sealed class CircleReferenceModel : TypeModel
{
    /// <summary>Whether the node participates in cycle detection or only reference sharing.</summary>
    internal readonly bool Guarded;

    /// <summary>Captures a reference-tracked node.</summary>
    internal CircleReferenceModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy,
        string[] constraints, bool guarded)
        : base(identity, accessibility, PayloadKind.CircleReference, members, policy, constraints)
    {
        Guarded = guarded;
    }
}
