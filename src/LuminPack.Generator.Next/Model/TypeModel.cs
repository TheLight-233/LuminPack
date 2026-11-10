using System;
using System.Linq;

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
    public virtual bool Equals(TypeModel? other) => ReferenceEquals(this, other) || other != null && Kind == other.Kind &&
        Identity.Equals(other.Identity) && Accessibility == other.Accessibility && Policy.Equals(other.Policy) &&
        Members.Length == other.Members.Length && Same(Members, other.Members) && Same(Constraints, other.Constraints);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();

    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T>
        => ReferenceEquals(left, right) || left.Length == right.Length && System.Linq.Enumerable.SequenceEqual(left, right);
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

    /// <summary>Whether the declaration is a supported fixed record, which gives the struct its <c>MeasureRecord</c> entry.</summary>
    internal readonly bool RecordWidth;

    /// <summary>Captures a struct.</summary>
    internal StructModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy, string[] constraints,
        StructStorage storage, PortableLayout? layout, bool recordWidth)
        : base(identity, accessibility, PayloadKind.Struct, members, policy, constraints)
    {
        RecordWidth = recordWidth;
        Layout = layout;
        Storage = storage;
    }
}

/// <summary>One scalar transfer of a portable struct's packed wire.</summary>
internal sealed class PortableLeafModel : IEquatable<PortableLeafModel>
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
    internal PortableLeafModel(string access, string scalar, int size, bool isVolatile)
    {
        Access = access;
        Scalar = scalar;
        Size = size;
        Volatile = isVolatile;
    }

    /// <inheritdoc/>
    public bool Equals(PortableLeafModel? other) => ReferenceEquals(this, other) || other != null && Access == other.Access &&
        Scalar == other.Scalar && Size == other.Size && Volatile == other.Volatile;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PortableLeafModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Access.GetHashCode();
}

/// <summary>One field of a portable struct's positional storage view.</summary>
internal sealed class PortableFieldModel : IEquatable<PortableFieldModel>
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
    internal readonly PortableLeafModel[] Leaves;

    /// <summary>Captures one view field.</summary>
    internal PortableFieldModel(string name, string type, bool isVolatile, int offset, PortableLeafModel[] leaves)
    {
        Leaves = leaves;
        Offset = offset;
        Volatile = isVolatile;
        Type = type;
        Name = name;
    }

    /// <inheritdoc/>
    public bool Equals(PortableFieldModel? other) => ReferenceEquals(this, other) || other != null && Name == other.Name &&
        Type == other.Type && Volatile == other.Volatile && Offset == other.Offset &&
        (ReferenceEquals(Leaves, other.Leaves) || Enumerable.SequenceEqual(Leaves, other.Leaves));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PortableFieldModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}

/// <summary>The positional transfer layout of a portable unmanaged struct: its view, packed fields and leaves.</summary>
internal sealed class PortableLayout : IEquatable<PortableLayout>
{
    /// <summary>The view type name.</summary>
    internal readonly string ViewName;

    /// <summary>The view's fields in declaration order.</summary>
    internal readonly PortableFieldModel[] Fields;

    /// <summary>Every scalar transfer in wire order.</summary>
    internal readonly PortableLeafModel[] Leaves;

    /// <summary>The complete packed wire width.</summary>
    internal readonly int WireSize;

    /// <summary>The predicted native storage width on a 64-bit runtime.</summary>
    internal readonly int Size64;

    /// <summary>The predicted native storage width on a 32-bit runtime.</summary>
    internal readonly int Size32;

    /// <summary>The predicted aggregate alignment on a 64-bit runtime.</summary>
    internal readonly int Alignment64;

    /// <summary>The predicted aggregate alignment on a 32-bit runtime.</summary>
    internal readonly int Alignment32;

    /// <summary>Whether the plan describes architecture-tagged storage rather than an explicitly packed struct.</summary>
    internal readonly bool Tagged;

    /// <summary>Whether every view field can be flattened and the root is not itself a tuple, so the positional view is renderable.</summary>
    internal readonly bool Complete;

    /// <summary>The declared CLR layout attribute line the view reproduces, or null.</summary>
    internal readonly string? LayoutAttribute;

    /// <summary>Captures a portable layout.</summary>
    internal PortableLayout(string viewName, PortableFieldModel[] fields, int wireSize, int size64, int size32, int alignment64,
        int alignment32, bool tagged, bool complete, string? layoutAttribute)
    {
        LayoutAttribute = layoutAttribute;
        Complete = complete;
        Tagged = tagged;
        Alignment32 = alignment32;
        Alignment64 = alignment64;
        Size32 = size32;
        Size64 = size64;
        WireSize = wireSize;
        Fields = fields;
        Leaves = fields.Length == 0 ? Array.Empty<PortableLeafModel>() : Flatten(fields);
        ViewName = viewName;
    }

    /// <summary>The field count, which is also the number of wire slots.</summary>
    internal int FieldCount => Fields.Length;

    private static PortableLeafModel[] Flatten(PortableFieldModel[] fields)
    {
        int count = 0;
        for (int index = 0; index < fields.Length; index++)
            count += fields[index].Leaves.Length;
        var leaves = new PortableLeafModel[count];
        int next = 0;
        for (int index = 0; index < fields.Length; index++)
            for (int leaf = 0; leaf < fields[index].Leaves.Length; leaf++)
                leaves[next++] = fields[index].Leaves[leaf];
        return leaves;
    }

    /// <inheritdoc/>
    public bool Equals(PortableLayout? other) => ReferenceEquals(this, other) || other != null && ViewName == other.ViewName &&
        WireSize == other.WireSize && Size64 == other.Size64 && Size32 == other.Size32 && Alignment64 == other.Alignment64 &&
        Alignment32 == other.Alignment32 && Tagged == other.Tagged && Complete == other.Complete &&
        LayoutAttribute == other.LayoutAttribute && (ReferenceEquals(Fields, other.Fields) || Enumerable.SequenceEqual(Fields, other.Fields));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PortableLayout);

    /// <inheritdoc/>
    public override int GetHashCode() => ViewName.GetHashCode();
}

/// <summary>The dispatch strategy of a Union root.</summary>
internal enum UnionDispatchTier
{
    /// <summary>Every case overrides the virtual slot.</summary>
    Virtual,

    /// <summary>Sealed exact cases are matched by runtime type before the virtual slot.</summary>
    ExactPatterns,

    /// <summary>The root dispatches on the exact case type.</summary>
    ExactType,
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
    public bool Equals(UnionCaseModel? other) => ReferenceEquals(this, other) || other != null && Tag == other.Tag && Type.Equals(other.Type) &&
        Read == other.Read && Sealed == other.Sealed && FixedCursor == other.FixedCursor;

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

    /// <summary>Whether the node has a base payload whose storage would join its mirror.</summary>
    internal readonly bool Derived;

    /// <summary>Whether the node declares a pool rent method.</summary>
    internal readonly bool Rent;

    /// <summary>Whether the node selects a constructor with parameters for its reads.</summary>
    internal readonly bool SelectedConstructor;

    /// <summary>Whether the node declares a serialization callback.</summary>
    internal readonly bool Callbacks;

    /// <summary>Whether every serialized member sits at its declared wire position.</summary>
    internal readonly bool FieldsInOrder;

    /// <summary>The node's complete storage mirror declarations in storage order, or null when the storage cannot be
    /// reproduced exactly.</summary>
    internal readonly string[]? MirrorFields;

    /// <summary>The explicit-layout offset per mirror field, or null when the storage is sequential.</summary>
    internal readonly int[]? MirrorOffsets;

    /// <summary>The CLR layout attribute line of the storage mirror, or null.</summary>
    internal readonly string? MirrorLayout;

    /// <summary>Captures a reference-tracked node.</summary>
    internal CircleReferenceModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy,
        string[] constraints, bool guarded, bool derived, bool rent, bool selectedConstructor, bool callbacks, bool fieldsInOrder,
        string[]? mirrorFields, int[]? mirrorOffsets, string? mirrorLayout)
        : base(identity, accessibility, PayloadKind.CircleReference, members, policy, constraints)
    {
        MirrorLayout = mirrorLayout;
        MirrorOffsets = mirrorOffsets;
        MirrorFields = mirrorFields;
        FieldsInOrder = fieldsInOrder;
        Callbacks = callbacks;
        SelectedConstructor = selectedConstructor;
        Rent = rent;
        Derived = derived;
        Guarded = guarded;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeModel? other) => ReferenceEquals(this, other) ||
        other is CircleReferenceModel circle && base.Equals(other) && Guarded == circle.Guarded && Derived == circle.Derived &&
        Rent == circle.Rent && SelectedConstructor == circle.SelectedConstructor && Callbacks == circle.Callbacks &&
        FieldsInOrder == circle.FieldsInOrder && MirrorLayout == circle.MirrorLayout &&
        (ReferenceEquals(MirrorFields, circle.MirrorFields) || MirrorFields != null && circle.MirrorFields != null &&
            Enumerable.SequenceEqual(MirrorFields, circle.MirrorFields)) &&
        (ReferenceEquals(MirrorOffsets, circle.MirrorOffsets) || MirrorOffsets != null && circle.MirrorOffsets != null &&
            Enumerable.SequenceEqual(MirrorOffsets, circle.MirrorOffsets));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();
}
