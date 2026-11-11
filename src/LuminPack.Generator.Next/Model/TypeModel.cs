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

/// <summary>The version-tolerant schema facts an emitted record depends on, reduced to strings and bytes.</summary>
internal sealed class VersionSchemaModel : IEquatable<VersionSchemaModel>
{
    /// <summary>The qualified schema class, such as <c>global::LuminPack.Generated.LuminPackExtensions_App.Version_global_N_Model</c>.</summary>
    internal readonly string Schema;

    /// <summary>The constant member metadata.</summary>
    internal readonly byte[] Metadata;

    /// <summary>The constant revision-2 prefix with a zero profile byte.</summary>
    internal readonly byte[] Prefix;

    /// <summary>The field table entries, one <c>new LuminPackVersionField(...)</c> per member.</summary>
    internal readonly string[] FieldEntries;

    /// <summary>The declared types of the read temporaries, per member.</summary>
    internal readonly string[] TempTypes;

    /// <summary>The analyzed type names of the members.</summary>
    internal readonly string[] TypeNames;

    /// <summary>The fully qualified member types.</summary>
    internal readonly string[] QualifiedTypes;

    /// <summary>The root-private layout mirror every version-tolerant record declares.</summary>
    internal readonly string Layout;

    /// <summary>The mirror's field declarations in storage order.</summary>
    internal readonly string[] LayoutFields;

    /// <summary>The former names of each member, which JSON reads accept.</summary>
    internal readonly string[][] OldNames;

    /// <summary>Whether metadata or a raw width depends on a closed generic construction or its runtime layout.</summary>
    internal readonly bool Dynamic;

    /// <summary>The revision-2 widths, with zero for a runtime-sized raw value.</summary>
    internal readonly int[] Widths;

    /// <summary>Captures a version schema.</summary>
    internal VersionSchemaModel(string schema, byte[] metadata, byte[] prefix, string[] fieldEntries, string[] tempTypes,
        string[] typeNames, string[] qualifiedTypes, string layout, string[] layoutFields, string[][] oldNames,
        bool dynamic = false, int[]? widths = null)
    {
        Dynamic = dynamic;
        Widths = widths ?? Array.Empty<int>();
        OldNames = oldNames;
        Schema = schema;
        Metadata = metadata;
        Prefix = prefix;
        FieldEntries = fieldEntries;
        TempTypes = tempTypes;
        TypeNames = typeNames;
        QualifiedTypes = qualifiedTypes;
        Layout = layout;
        LayoutFields = layoutFields;
    }

    /// <inheritdoc/>
    public bool Equals(VersionSchemaModel? other) => ReferenceEquals(this, other) || other != null && Schema == other.Schema &&
        Layout == other.Layout && Dynamic == other.Dynamic && Widths.SequenceEqual(other.Widths) && Metadata.SequenceEqual(other.Metadata) && Prefix.SequenceEqual(other.Prefix) &&
        FieldEntries.SequenceEqual(other.FieldEntries) && TempTypes.SequenceEqual(other.TempTypes) &&
        TypeNames.SequenceEqual(other.TypeNames) && QualifiedTypes.SequenceEqual(other.QualifiedTypes) &&
        LayoutFields.SequenceEqual(other.LayoutFields) && OldNames.Length == other.OldNames.Length &&
        OldNames.Zip(other.OldNames, (left, right) => left.SequenceEqual(right)).All(same => same);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as VersionSchemaModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Schema.GetHashCode();
}

/// <summary>The selected constructor of a record: the member per parameter, the members it sets and the storage-order
/// temporaries its read declares.</summary>
internal sealed class ConstructorModel : IEquatable<ConstructorModel>
{
    /// <summary>The matched member name per constructor parameter, <c>null</c> for a <c>default</c> argument, or null when a
    /// pool rent supplies the instance instead of the selected constructor.</summary>
    internal readonly string?[]? Arguments;

    /// <summary>The members the selected constructor sets, or null when a pool rent supplies the instance.</summary>
    internal readonly string[]? Members;

    /// <summary>The member names the constructed read declares temporaries in, in storage order.</summary>
    internal readonly string[] DeclaredOrder;

    /// <summary>Captures a selected constructor.</summary>
    internal ConstructorModel(string?[]? arguments, string[]? members, string[] declaredOrder)
    {
        DeclaredOrder = declaredOrder;
        Members = members;
        Arguments = arguments;
    }

    /// <summary>Drops the construction facts a pool rent replaces, keeping the declared temporary order.</summary>
    /// <returns>The constructor facts of a pooled record.</returns>
    internal ConstructorModel Rented() => new ConstructorModel(null, null, DeclaredOrder);

    /// <inheritdoc/>
    public bool Equals(ConstructorModel? other) => ReferenceEquals(this, other) ||
        other != null && SameArguments(Arguments, other.Arguments) && Same(Members, other.Members) &&
        Same(DeclaredOrder, other.DeclaredOrder);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ConstructorModel);

    private static bool SameArguments(string?[]? left, string?[]? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                return false;
        return true;
    }

    private static bool Same<T>(T[]? left, T[]? right) where T : IEquatable<T>
        => ReferenceEquals(left, right) || left != null && right != null && left.Length == right.Length &&
            Enumerable.SequenceEqual(left, right);

    /// <inheritdoc/>
    public override int GetHashCode() => DeclaredOrder.Length;
}

/// <summary>The serialization callbacks of a record, spelled as statements of the write or read root.</summary>
internal sealed class CallbackModel : IEquatable<CallbackModel>
{
    /// <summary>The statements that run before the write body.</summary>
    internal readonly string[] WriteBefore;

    /// <summary>The statements that run after the write body.</summary>
    internal readonly string[] WriteAfter;

    /// <summary>The statements that run before the read body.</summary>
    internal readonly string[] ReadBefore;

    /// <summary>The statements that run after the read body.</summary>
    internal readonly string[] ReadAfter;

    /// <summary>Captures the callback statements in declaration order.</summary>
    internal CallbackModel(string[] writeBefore, string[] writeAfter, string[] readBefore, string[] readAfter)
    {
        ReadAfter = readAfter;
        ReadBefore = readBefore;
        WriteAfter = writeAfter;
        WriteBefore = writeBefore;
    }

    /// <inheritdoc/>
    public bool Equals(CallbackModel? other) => ReferenceEquals(this, other) || other != null &&
        Same(WriteBefore, other.WriteBefore) && Same(WriteAfter, other.WriteAfter) &&
        Same(ReadBefore, other.ReadBefore) && Same(ReadAfter, other.ReadAfter);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CallbackModel);

    private static bool Same(string[] left, string[] right)
        => ReferenceEquals(left, right) || left.Length == right.Length && Enumerable.SequenceEqual(left, right);

    /// <inheritdoc/>
    public override int GetHashCode() => WriteBefore.Length + WriteAfter.Length + ReadBefore.Length + ReadAfter.Length;
}

/// <summary>An ordinary class record, optionally version-tolerant.</summary>
internal sealed class RecordModel : TypeModel
{
    /// <summary>The first input-selected shape an explicitly untrusted graph reaches, or null.</summary>
    /// <remarks>A rejected payload publishes its ordinary roots, and every operation fails closed with this reason.</remarks>
    internal readonly string? Rejection;

    /// <summary>The base-first Local declarations of an inherited storage contract.</summary>
    internal readonly StorageMirrorModel[] Mirrors;
    /// <summary>Whether the record writes a version schema prefix.</summary>
    internal readonly bool Versioned;

    /// <summary>Whether the record is a case of a Union root that uses the shared returning cursor kernel.</summary>
    internal readonly bool FixedUnionCase;

    /// <summary>Whether the record has a base payload whose members follow its own.</summary>
    internal readonly bool Derived;

    /// <summary>Whether every variable member is a string, which allows the proven object write.</summary>
    internal readonly bool Proven;

    /// <summary>Whether the declaration is a supported fixed record, which gives it the <c>MeasureRecord</c> entry.</summary>
    internal readonly bool RecordWidth;

    /// <summary>Whether JSON reads assign a fresh instance directly, which requires public storage and an implicit
    /// parameterless construction path at every level.</summary>
    internal readonly bool FreshResult;

    /// <summary>The root-private layout mirror that auto-property and included non-public storage is reached through, or null
    /// when every serialized member is a publicly nameable field.</summary>
    internal readonly string? Mirror;

    /// <summary>The mirror's field declarations, such as <c>string? Name</c>, in storage order, or null.</summary>
    internal readonly string[]? MirrorFields;

    /// <summary>The explicit-layout offset of each mirror field, or null when the storage is sequential.</summary>
    internal readonly int[]? MirrorOffsets;

    /// <summary>The CLR layout attribute line of the storage mirror, or null.</summary>
    internal readonly string? MirrorLayout;

    /// <summary>The version-tolerant schema of the record, or null for an ordinary record.</summary>
    internal readonly VersionSchemaModel? Version;

    /// <summary>The selected constructor of the record, or null when its reads use the parameterless constructor.</summary>
    internal readonly ConstructorModel? Constructor;

    /// <summary>The serialization callbacks of the record, or null when it declares none.</summary>
    internal readonly CallbackModel? Callbacks;

    /// <summary>The static pool rent call of a pooled record, such as <c>global::N.T.Rent()</c>, or null.</summary>
    internal readonly string? Rent;

    /// <summary>Captures a record.</summary>
    internal RecordModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy, string[] constraints,
        bool versioned, bool fixedUnionCase, bool derived, bool proven, bool recordWidth, bool freshResult, string? mirror,
        string[]? mirrorFields, int[]? mirrorOffsets, string? mirrorLayout, VersionSchemaModel? version,
        ConstructorModel? constructor = null, CallbackModel? callbacks = null, string? rent = null, StorageMirrorModel[]? mirrors = null,
        string? rejection = null)
        : base(identity, accessibility, PayloadKind.Record, members, policy, constraints)
    {
        Rejection = rejection;
        Rent = rent;
        Callbacks = callbacks;
        Mirrors = mirrors ?? Array.Empty<StorageMirrorModel>();
        Constructor = constructor;
        FixedUnionCase = fixedUnionCase;
        RecordWidth = recordWidth;
        FreshResult = freshResult;
        Proven = proven;
        Derived = derived;
        Versioned = versioned;
        Mirror = mirror;
        MirrorFields = mirrorFields;
        MirrorOffsets = mirrorOffsets;
        MirrorLayout = mirrorLayout;
        Version = version;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeModel? other) => ReferenceEquals(this, other) ||
        other is RecordModel record && base.Equals(other) && Mirrors.SequenceEqual(record.Mirrors) && Versioned == record.Versioned &&
        FixedUnionCase == record.FixedUnionCase && Derived == record.Derived &&
        Proven == record.Proven && RecordWidth == record.RecordWidth && FreshResult == record.FreshResult &&
        Mirror == record.Mirror && Rent == record.Rent && Rejection == record.Rejection && Equals(Constructor, record.Constructor) &&
        Equals(Callbacks, record.Callbacks) &&
        MirrorLayout == record.MirrorLayout && Equals(Version, record.Version) &&
        (ReferenceEquals(MirrorFields, record.MirrorFields) || MirrorFields != null && record.MirrorFields != null &&
            Enumerable.SequenceEqual(MirrorFields, record.MirrorFields)) &&
        (ReferenceEquals(MirrorOffsets, record.MirrorOffsets) || MirrorOffsets != null && record.MirrorOffsets != null &&
            Enumerable.SequenceEqual(MirrorOffsets, record.MirrorOffsets));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as RecordModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();
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

    /// <summary>The JSON and managed-record storage mirror.</summary>
    internal readonly string? Mirror;
    /// <summary>The complete mirror field declarations.</summary>
    internal readonly string[]? MirrorFields;
    /// <summary>The explicit field offsets of the mirror.</summary>
    internal readonly int[]? MirrorOffsets;
    /// <summary>The CLR layout attribute of the mirror.</summary>
    internal readonly string? MirrorLayout;

    /// <summary>The complete recursive packed or tagged storage contract.</summary>
    internal readonly FormatterStorageModel? StoragePlan;

    /// <summary>Whether the declaration is a supported fixed record, which gives the struct its <c>MeasureRecord</c> entry.</summary>
    internal readonly bool RecordWidth;

    /// <summary>Whether the declaration is version-tolerant, whose schema prefix the struct roots do not reproduce.</summary>
    internal readonly bool Versioned;

    /// <summary>Whether the declaration declares a pool rent method.</summary>
    internal readonly bool Rent;

    /// <summary>Whether the declaration declares an instance constructor with parameters.</summary>
    internal readonly bool Constructors;

    /// <summary>Whether the declaration registers a serialization callback.</summary>
    internal readonly bool Callbacks;

    /// <summary>The selected constructor used by managed binary and JSON reads.</summary>
    internal readonly ConstructorModel? Construction;

    /// <summary>The validated callbacks in declaration order.</summary>
    internal readonly CallbackModel? SerializationCallbacks;

    /// <summary>The complete named schema of a version-tolerant value.</summary>
    internal readonly VersionSchemaModel? Version;

    /// <summary>The validated static pool rental expression.</summary>
    internal readonly string? RentCall;

    /// <summary>Whether the pool method returns its destination by reference.</summary>
    internal readonly bool RentByRef;

    /// <summary>Captures a struct.</summary>
    /// <param name="identity">The identity used by this operation.</param>
    /// <param name="accessibility">The accessibility used by this operation.</param>
    /// <param name="members">The members used by this operation.</param>
    /// <param name="policy">The policy used by this operation.</param>
    /// <param name="constraints">The constraints used by this operation.</param>
    /// <param name="storage">The storage used by this operation.</param>
    /// <param name="layout">The layout used by this operation.</param>
    /// <param name="recordWidth">The record width used by this operation.</param>
    /// <param name="versioned">The versioned used by this operation.</param>
    /// <param name="rent">The rent used by this operation.</param>
    /// <param name="constructors">The constructors used by this operation.</param>
    /// <param name="callbacks">The callbacks used by this operation.</param>
    /// <param name="storagePlan">The storage plan used by this operation.</param>
    /// <param name="mirror">The mirror used by this operation.</param>
    /// <param name="mirrorFields">The mirror fields used by this operation.</param>
    /// <param name="mirrorOffsets">The mirror offsets used by this operation.</param>
    /// <param name="mirrorLayout">The mirror layout used by this operation.</param>
    internal StructModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy, string[] constraints,
        StructStorage storage, PortableLayout? layout, bool recordWidth, bool versioned, bool rent, bool constructors, bool callbacks, FormatterStorageModel? storagePlan = null, string? mirror = null, string[]? mirrorFields = null, int[]? mirrorOffsets = null, string? mirrorLayout = null, ConstructorModel? construction = null, CallbackModel? serializationCallbacks = null,
        VersionSchemaModel? version = null, string? rentCall = null, bool rentByRef = false)
        : base(identity, accessibility, PayloadKind.Struct, members, policy, constraints)
    {
        Mirror = mirror;
        MirrorFields = mirrorFields;
        MirrorOffsets = mirrorOffsets;
        MirrorLayout = mirrorLayout;
        Construction = construction;
        SerializationCallbacks = serializationCallbacks;
        Version = version;
        RentCall = rentCall;
        RentByRef = rentByRef;
        StoragePlan = storagePlan;
        Callbacks = callbacks;
        Constructors = constructors;
        Rent = rent;
        Versioned = versioned;
        RecordWidth = recordWidth;
        Layout = layout;
        Storage = storage;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeModel? other) => ReferenceEquals(this, other) ||
        other is StructModel model && base.Equals(other) && Storage == model.Storage && RecordWidth == model.RecordWidth &&
        Versioned == model.Versioned && Rent == model.Rent && Constructors == model.Constructors &&
        Callbacks == model.Callbacks && Equals(Construction, model.Construction) && Equals(SerializationCallbacks, model.SerializationCallbacks) &&
        Equals(Version, model.Version) && RentCall == model.RentCall && RentByRef == model.RentByRef && Equals(Layout, model.Layout) && Equals(StoragePlan, model.StoragePlan) && Mirror == model.Mirror && MirrorLayout == model.MirrorLayout &&
        (MirrorFields ?? Array.Empty<string>()).SequenceEqual(model.MirrorFields ?? Array.Empty<string>()) &&
        (MirrorOffsets ?? Array.Empty<int>()).SequenceEqual(model.MirrorOffsets ?? Array.Empty<int>());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StructModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();
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

    /// <summary>Whether the case's binary read entry the dispatch calls instead of expanding its members.</summary>
    internal readonly bool OwnEntry;

    /// <summary>Whether the case's binary read entry takes the operation's profile.</summary>
    internal readonly bool ReadGeneric;

    /// <summary>Whether the case slot can be sealed.</summary>
    internal readonly bool Sealed;

    /// <summary>Whether the case payload uses the returning cursor kernel.</summary>
    internal readonly bool FixedCursor;

    /// <summary>The analyzed case payload, whose fixed member reads the dispatch expands, or null.</summary>
    internal readonly TypeModel? Model;

    /// <summary>The readable payload name of the case, which names its own Union entry.</summary>
    internal readonly string Readable;

    /// <summary>The partial declarations from the outermost containing type to the case.</summary>
    internal readonly string[] Chain;

    /// <summary>The case's namespace, or null in the global namespace.</summary>
    internal readonly string? Namespace;

    /// <summary>Captures a Union case.</summary>
    internal UnionCaseModel(int tag, TypeIdentity type, UnionCaseRead read, bool ownEntry, bool readGeneric, bool sealedCase, bool fixedCursor,
        TypeModel? model, string readable, string[] chain, string? @namespace)
    {
        Namespace = @namespace;
        Chain = chain;
        Readable = readable;
        Model = model;
        FixedCursor = fixedCursor;
        Sealed = sealedCase;
        ReadGeneric = readGeneric;
        OwnEntry = ownEntry;
        Read = read;
        Type = type;
        Tag = tag;
    }

    /// <inheritdoc/>
    public bool Equals(UnionCaseModel? other) => ReferenceEquals(this, other) || other != null && Tag == other.Tag && Type.Equals(other.Type) &&
        Read == other.Read && OwnEntry == other.OwnEntry && ReadGeneric == other.ReadGeneric && Sealed == other.Sealed &&
        FixedCursor == other.FixedCursor && Readable == other.Readable && Namespace == other.Namespace &&
        (ReferenceEquals(Chain, other.Chain) || Enumerable.SequenceEqual(Chain, other.Chain)) && Equals(Model, other.Model);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as UnionCaseModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Tag;
}

/// <summary>One compiled wire-profile slot of a Union root.</summary>
internal sealed class UnionSlotModel : IEquatable<UnionSlotModel>
{
    /// <summary>The method-name suffix: empty for the declared profile, otherwise <c>_Options_&lt;identity&gt;</c>.</summary>
    internal readonly string Suffix;

    /// <summary>The imported name of the profile's string options type.</summary>
    internal readonly string Profile;

    /// <summary>The complete compiled-profile identity.</summary>
    internal readonly string Identity;

    /// <summary>The profile's string encoding, zero for UTF-8 or one for UTF-16.</summary>
    internal readonly int Encoding;

    /// <summary>The profile's string recording, zero for lengths, one for tokens or two for variable lengths.</summary>
    internal readonly int Recording;

    /// <summary>Captures one slot.</summary>
    internal UnionSlotModel(string suffix, string profile, string identity, int encoding, int recording)
    {
        Recording = recording;
        Encoding = encoding;
        Identity = identity;
        Profile = profile;
        Suffix = suffix;
    }

    /// <inheritdoc/>
    public bool Equals(UnionSlotModel? other) => ReferenceEquals(this, other) || other != null && Suffix == other.Suffix &&
        Profile == other.Profile && Identity == other.Identity && Encoding == other.Encoding && Recording == other.Recording;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as UnionSlotModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Suffix.GetHashCode();
}

/// <summary>A polymorphic root.</summary>
internal sealed class UnionModel : TypeModel
{
    /// <summary>The declared cases in declaration order.</summary>
    internal readonly UnionCaseModel[] Cases;

    /// <summary>Whether the root is an interface rather than an abstract class.</summary>
    internal readonly bool IsInterface;

    /// <summary>Whether cases use the wide tag protocol.</summary>
    internal readonly bool WideTags;

    /// <summary>The canonical slot suffix.</summary>
    internal readonly string UnionSuffix;

    /// <summary>The compiled dispatch tier.</summary>
    internal readonly UnionDispatchTier Tier;

    /// <summary>The exact case types of a pattern or direct tier, in tag order.</summary>
    internal readonly string[] TierCases;

    /// <summary>Whether each exact tier case is declared sealed.</summary>
    internal readonly bool[] TierSealed;

    /// <summary>Whether a fixed case gives the root the returning cursor protocol.</summary>
    internal readonly bool Cursor;

    /// <summary>Whether the binary read root takes the operation's profile, because some case entry does.</summary>
    internal readonly bool ReadGeneric;

    /// <summary>The partial declarations from the outermost containing type to the root.</summary>
    internal readonly string[] Chain;

    /// <summary>The root's namespace, or null in the global namespace.</summary>
    internal readonly string? Namespace;

    /// <summary>The compiled binary profile slots in profile order.</summary>
    internal readonly UnionSlotModel[] BinarySlots;

    /// <summary>The compiled JSON profile slots in profile order.</summary>
    internal readonly UnionSlotModel[] JsonSlots;

    /// <summary>Whether the exact canonical profile shortcuts can bind the referenced runtime's profiles.</summary>
    internal readonly bool Canonical;

    /// <summary>The imported string options type of the declared binary profile.</summary>
    internal readonly string StandardBinary;

    /// <summary>The imported string options type of the declared JSON profile.</summary>
    internal readonly string StandardJson;

    /// <summary>Captures a Union root.</summary>
    internal UnionModel(TypeIdentity identity, TypeAccessibility accessibility, WirePolicy policy, string[] constraints, UnionCaseModel[] cases,
        bool isInterface, bool wideTags, string unionSuffix, UnionDispatchTier tier, string[] tierCases, bool[] tierSealed, bool cursor,
        bool readGeneric, string[] chain, string? @namespace, UnionSlotModel[] binarySlots, UnionSlotModel[] jsonSlots, bool canonical,
        string standardBinary, string standardJson)
        : base(identity, accessibility, PayloadKind.Union, System.Array.Empty<MemberModel>(), policy, constraints)
    {
        StandardJson = standardJson;
        StandardBinary = standardBinary;
        Canonical = canonical;
        JsonSlots = jsonSlots;
        BinarySlots = binarySlots;
        Namespace = @namespace;
        Chain = chain;
        ReadGeneric = readGeneric;
        Cursor = cursor;
        TierSealed = tierSealed;
        TierCases = tierCases;
        Tier = tier;
        UnionSuffix = unionSuffix;
        WideTags = wideTags;
        IsInterface = isInterface;
        Cases = cases;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeModel? other) => ReferenceEquals(this, other) ||
        other is UnionModel union && base.Equals(other) && IsInterface == union.IsInterface && WideTags == union.WideTags &&
        UnionSuffix == union.UnionSuffix && Tier == union.Tier && Cursor == union.Cursor && ReadGeneric == union.ReadGeneric &&
        Canonical == union.Canonical && StandardBinary == union.StandardBinary && StandardJson == union.StandardJson &&
        Namespace == union.Namespace && Same(Cases, union.Cases) && Same(TierCases, union.TierCases) &&
        Same(TierSealed, union.TierSealed) && Same(Chain, union.Chain) && Same(BinarySlots, union.BinarySlots) &&
        Same(JsonSlots, union.JsonSlots);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();

    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T>
        => ReferenceEquals(left, right) || left.Length == right.Length && Enumerable.SequenceEqual(left, right);
}

/// <summary>A reference-tracked graph node.</summary>
internal sealed class CircleReferenceModel : TypeModel
{
    /// <summary>The inherited Local storage declarations.</summary>
    internal readonly StorageMirrorModel[] Mirrors;
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

    /// <summary>The explicit wire slot of each member in sorted member order.</summary>
    internal readonly int[] Slots;

    /// <summary>The raw or length-framed width of each member, with zero for a runtime raw width.</summary>
    internal readonly int[] Widths;

    /// <summary>The selected construction metadata.</summary>
    internal readonly ConstructorModel? Construction;

    /// <summary>The validated callbacks.</summary>
    internal readonly CallbackModel? SerializationCallbacks;

    /// <summary>The static rental expression, or null.</summary>
    internal readonly string? RentCall;

    /// <summary>Whether rental rebinds destination storage by reference.</summary>
    internal readonly bool RentByRef;

    /// <summary>Captures a reference-tracked node.</summary>
    internal CircleReferenceModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy,
        string[] constraints, bool guarded, bool derived, bool rent, bool selectedConstructor, bool callbacks, bool fieldsInOrder,
        string[]? mirrorFields, int[]? mirrorOffsets, string? mirrorLayout, int[] slots, int[] widths,
        ConstructorModel? construction, CallbackModel? serializationCallbacks, string? rentCall, bool rentByRef, StorageMirrorModel[]? mirrors = null)
        : base(identity, accessibility, PayloadKind.CircleReference, members, policy, constraints)
    {
        MirrorLayout = mirrorLayout;
        MirrorOffsets = mirrorOffsets;
        MirrorFields = mirrorFields;
        Mirrors = mirrors ?? Array.Empty<StorageMirrorModel>();
        Slots = slots;
        Widths = widths;
        Construction = construction;
        SerializationCallbacks = serializationCallbacks;
        RentCall = rentCall;
        RentByRef = rentByRef;
        FieldsInOrder = fieldsInOrder;
        Callbacks = callbacks;
        SelectedConstructor = selectedConstructor;
        Rent = rent;
        Derived = derived;
        Guarded = guarded;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeModel? other) => ReferenceEquals(this, other) ||
        other is CircleReferenceModel circle && base.Equals(other) && Mirrors.SequenceEqual(circle.Mirrors) && Guarded == circle.Guarded && Derived == circle.Derived &&
        Rent == circle.Rent && SelectedConstructor == circle.SelectedConstructor && Callbacks == circle.Callbacks &&
        FieldsInOrder == circle.FieldsInOrder && Slots.SequenceEqual(circle.Slots) && Widths.SequenceEqual(circle.Widths) &&
        Equals(Construction, circle.Construction) && Equals(SerializationCallbacks, circle.SerializationCallbacks) &&
        RentCall == circle.RentCall && RentByRef == circle.RentByRef && MirrorLayout == circle.MirrorLayout &&
        (ReferenceEquals(MirrorFields, circle.MirrorFields) || MirrorFields != null && circle.MirrorFields != null &&
            Enumerable.SequenceEqual(MirrorFields, circle.MirrorFields)) &&
        (ReferenceEquals(MirrorOffsets, circle.MirrorOffsets) || MirrorOffsets != null && circle.MirrorOffsets != null &&
            Enumerable.SequenceEqual(MirrorOffsets, circle.MirrorOffsets));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();
}
