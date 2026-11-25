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

}

/// <summary>Required members initialized before reading or from staged decoded storage.</summary>
internal sealed class RequiredConstructionModel : IEquatable<RequiredConstructionModel>
{
    /// <summary>The members a parameterless construction initializes to their storage defaults.</summary>
    internal readonly string[] Defaults;

    /// <summary>The members a staged construction initializes from the decoded record.</summary>
    internal readonly string[] Decoded;

    /// <summary>Captures the initialization requirements for the existing construction paths.</summary>
    /// <param name="defaults">The members that need initializers on early construction.</param>
    /// <param name="decoded">The members that need initializers on staged construction.</param>
    internal RequiredConstructionModel(string[] defaults, string[] decoded)
    {
        Defaults = defaults;
        Decoded = decoded;
    }

    /// <inheritdoc/>
    public bool Equals(RequiredConstructionModel? other) => ReferenceEquals(this, other) ||
        other != null && Defaults.SequenceEqual(other.Defaults) && Decoded.SequenceEqual(other.Decoded);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as RequiredConstructionModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Defaults.Length * 397 ^ Decoded.Length;
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

    /// <summary>The explicit initializers required by the selected C# construction paths.</summary>
    internal readonly RequiredConstructionModel? RequiredConstruction;

    /// <summary>Captures the facts shared by every payload kind.</summary>
    /// <param name="identity">The identity supplied to <c>TypeModel</c>.</param>
    /// <param name="accessibility">The accessibility supplied to <c>TypeModel</c>.</param>
    /// <param name="kind">The kind supplied to <c>TypeModel</c>.</param>
    /// <param name="members">The members supplied to <c>TypeModel</c>.</param>
    /// <param name="policy">The policy supplied to <c>TypeModel</c>.</param>
    /// <param name="constraints">The constraints supplied to <c>TypeModel</c>.</param>
    /// <param name="requiredConstruction">The required initialization facts.</param>
    protected TypeModel(TypeIdentity identity, TypeAccessibility accessibility, PayloadKind kind, MemberModel[] members, WirePolicy policy,
        string[] constraints, RequiredConstructionModel? requiredConstruction = null)
    {
        Identity = identity;
        Accessibility = accessibility;
        Kind = kind;
        Members = members;
        Policy = policy;
        Constraints = constraints;
        RequiredConstruction = requiredConstruction;
    }

    /// <inheritdoc/>
    public virtual bool Equals(TypeModel? other) => ReferenceEquals(this, other) || other != null && Kind == other.Kind &&
        Identity.Equals(other.Identity) && Accessibility == other.Accessibility && Policy.Equals(other.Policy) &&
        Members.Length == other.Members.Length && Same(Members, other.Members) && Same(Constraints, other.Constraints) &&
        Equals(RequiredConstruction, other.RequiredConstruction);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();

    /// <summary>Handles same for <c>TypeModel</c>.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="left">The first input or comparison operand.</param>
    /// <param name="right">The second input or comparison operand.</param>
    /// <returns>Whether the input satisfies the same check.</returns>
    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T>
        => ReferenceEquals(left, right) || left.Length == right.Length && System.Linq.Enumerable.SequenceEqual(left, right);
}

/// <summary>The descriptor and compatibility facts a version-tolerant record publishes.</summary>
internal sealed class VersionSchemaModel : IEquatable<VersionSchemaModel>
{
    /// <summary>The qualified schema class.</summary>
    internal readonly string Schema;

    /// <summary>The schema class's unqualified name.</summary>
    internal string Name => Schema.Substring(Schema.LastIndexOf(".Version_", StringComparison.Ordinal) + 1);

    /// <summary>The descriptor words in wire order, with an empty type hash for runtime contracts.</summary>
    internal readonly ulong[] Descriptors;

    /// <summary>Whether each descriptor needs the closed generic type's contract.</summary>
    internal readonly bool[] RuntimeContracts;

    /// <summary>The name keys accepted by each slot, starting with its oldest identity.</summary>
    internal readonly uint[][] NameKeys;

    /// <summary>The revision-1 field table entries, also used for open type contracts.</summary>
    internal readonly string[] FieldEntries;

    /// <summary>The exact revision-1 wire contracts, or null where only runtime types can resolve them.</summary>
    internal readonly string?[] LegacyContracts;

    /// <summary>The declared types of the staged members.</summary>
    internal readonly string[] TempTypes;

    /// <summary>The root-private layout mirror.</summary>
    internal readonly string Layout;

    /// <summary>The mirror's field declarations in storage order.</summary>
    internal readonly string[] LayoutFields;

    /// <summary>The former names of each member, which JSON reads accept.</summary>
    internal readonly string[][] OldNames;

    /// <summary>Whether each fixed-width field uses a padded nullable payload.</summary>
    internal readonly bool[] FixedNullable;

    /// <summary>Whether the descriptor table depends on a closed generic construction.</summary>
    internal bool Dynamic => Array.IndexOf(RuntimeContracts, true) >= 0;

    /// <summary>Gets the size code of a member's descriptor.</summary>
    /// <param name="slot">The zero-based member index.</param>
    /// <returns>The one-byte wire size code.</returns>
    internal byte Width(int slot) => (byte)(Descriptors[slot] >> 56);

    /// <summary>Captures the analysis facts used by the binary and JSON emitters.</summary>
    /// <param name="schema">The qualified generated class name.</param>
    /// <param name="descriptors">The member descriptor words.</param>
    /// <param name="runtimeContracts">The members that compute a type hash for each closed construction.</param>
    /// <param name="nameKeys">The accepted keys per slot.</param>
    /// <param name="fieldEntries">The legacy schema entries.</param>
    /// <param name="legacyContracts">The compiled legacy type contracts in field order.</param>
    /// <param name="tempTypes">The staged field types.</param>
    /// <param name="layout">The storage mirror name.</param>
    /// <param name="layoutFields">The storage mirror field declarations.</param>
    /// <param name="oldNames">The former member names.</param>
    /// <param name="fixedNullable">The members with a fixed-width nullable representation.</param>
    internal VersionSchemaModel(string schema, ulong[] descriptors, bool[] runtimeContracts, uint[][] nameKeys,
        string[] fieldEntries, string?[] legacyContracts, string[] tempTypes, string layout, string[] layoutFields,
        string[][] oldNames, bool[] fixedNullable)
    {
        Schema = schema;
        Descriptors = descriptors;
        RuntimeContracts = runtimeContracts;
        NameKeys = nameKeys;
        FieldEntries = fieldEntries;
        LegacyContracts = legacyContracts;
        TempTypes = tempTypes;
        Layout = layout;
        LayoutFields = layoutFields;
        OldNames = oldNames;
        FixedNullable = fixedNullable;
    }

    /// <inheritdoc/>
    public bool Equals(VersionSchemaModel? other) => ReferenceEquals(this, other) || other != null && Schema == other.Schema &&
        Layout == other.Layout && Descriptors.SequenceEqual(other.Descriptors) && RuntimeContracts.SequenceEqual(other.RuntimeContracts) &&
        FixedNullable.SequenceEqual(other.FixedNullable) && FieldEntries.SequenceEqual(other.FieldEntries) &&
        LegacyContracts.SequenceEqual(other.LegacyContracts) &&
        TempTypes.SequenceEqual(other.TempTypes) && LayoutFields.SequenceEqual(other.LayoutFields) &&
        NameKeys.Length == other.NameKeys.Length && NameKeys.Zip(other.NameKeys, (left, right) => left.SequenceEqual(right)).All(same => same) &&
        OldNames.Length == other.OldNames.Length && OldNames.Zip(other.OldNames, (left, right) => left.SequenceEqual(right)).All(same => same);

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
    /// <param name="arguments">The arguments supplied to <c>ConstructorModel</c>.</param>
    /// <param name="members">The members supplied to <c>ConstructorModel</c>.</param>
    /// <param name="declaredOrder">The declared order supplied to <c>ConstructorModel</c>.</param>
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

    /// <summary>Handles same arguments for <c>ConstructorModel</c>.</summary>
    /// <param name="left">The first input or comparison operand.</param>
    /// <param name="right">The second input or comparison operand.</param>
    /// <returns>Whether the input satisfies the same arguments check.</returns>
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

    /// <summary>Handles same for <c>ConstructorModel</c>.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="left">The first input or comparison operand.</param>
    /// <param name="right">The second input or comparison operand.</param>
    /// <returns>Whether the input satisfies the same check.</returns>
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
    /// <param name="writeBefore">The write before supplied to <c>CallbackModel</c>.</param>
    /// <param name="writeAfter">The write after supplied to <c>CallbackModel</c>.</param>
    /// <param name="readBefore">The read before supplied to <c>CallbackModel</c>.</param>
    /// <param name="readAfter">The read after supplied to <c>CallbackModel</c>.</param>
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

    /// <summary>Handles same for <c>CallbackModel</c>.</summary>
    /// <param name="left">The first input or comparison operand.</param>
    /// <param name="right">The second input or comparison operand.</param>
    /// <returns>Whether the input satisfies the same check.</returns>
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

    /// <summary>Whether a declared depth limit applies to this record: it reaches itself through its serialized graph.</summary>
    internal readonly bool Recursive;

    /// <summary>Whether the record is an unannotated class expanded as a nested object; the payloads that reach it publish it.</summary>
    internal readonly bool Implicit;

    /// <summary>The base-first Local declarations of an inherited storage contract.</summary>
    internal readonly StorageMirrorModel[] Mirrors;
    /// <summary>Whether the record writes a version schema prefix.</summary>
    internal readonly bool Versioned;

    /// <summary>Whether the record is a case of a Union root that uses the shared returning cursor kernel.</summary>
    internal readonly bool FixedUnionCase;
    /// <summary>Whether a plain class can use the referenced writer's scalar header overload.</summary>
    internal readonly bool ScalarHeader;
    /// <summary>Whether ordinary record reads can group direct string fields.</summary>
    internal readonly bool StringPairs;

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

    /// <summary>Whether the pool returns its reusable storage by reference.</summary>
    internal readonly bool RentByRef;

    /// <summary>The construction expression for a tracked instance before its members are read.</summary>
    internal readonly string? ReferenceConstruction;

    /// <summary>The declared list base whose contents are this record's entire payload, or null.</summary>
    /// <remarks>A list-derived class with no serialized members would otherwise publish a memberless object and drop its
    /// elements. When this is set, the record writes and reads the list base body instead of member framing.</remarks>
    internal readonly string? SequenceBase;

    /// <summary>The list base's element spelling, which is the record itself, or null.</summary>
    internal readonly string? SequenceElement;

    /// <summary>The list formatter appended to the ordinary member record, or null.</summary>
    internal readonly FormatterModel? CollectionBase;

    /// <summary>Captures a record.</summary>
    /// <param name="identity">The identity supplied to <c>RecordModel</c>.</param>
    /// <param name="accessibility">The accessibility supplied to <c>RecordModel</c>.</param>
    /// <param name="members">The members supplied to <c>RecordModel</c>.</param>
    /// <param name="policy">The policy supplied to <c>RecordModel</c>.</param>
    /// <param name="constraints">The constraints supplied to <c>RecordModel</c>.</param>
    /// <param name="versioned">Whether to enable versioned for this operation.</param>
    /// <param name="fixedUnionCase">Whether to enable fixed union case for this operation.</param>
    /// <param name="derived">Whether to enable derived for this operation.</param>
    /// <param name="proven">Whether to enable proven for this operation.</param>
    /// <param name="recordWidth">Whether to enable record width for this operation.</param>
    /// <param name="freshResult">Whether to enable fresh result for this operation.</param>
    /// <param name="mirror">The mirror supplied to <c>RecordModel</c>.</param>
    /// <param name="mirrorFields">The mirror fields supplied to <c>RecordModel</c>.</param>
    /// <param name="mirrorOffsets">The mirror offsets supplied to <c>RecordModel</c>.</param>
    /// <param name="mirrorLayout">The mirror layout supplied to <c>RecordModel</c>.</param>
    /// <param name="version">The version supplied to <c>RecordModel</c>.</param>
    /// <param name="constructor">The constructor supplied to <c>RecordModel</c>.</param>
    /// <param name="callbacks">The callbacks supplied to <c>RecordModel</c>.</param>
    /// <param name="rent">The rent supplied to <c>RecordModel</c>.</param>
    /// <param name="mirrors">The mirrors supplied to <c>RecordModel</c>.</param>
    /// <param name="rejection">The rejection supplied to <c>RecordModel</c>.</param>
    /// <param name="recursive">Whether to enable recursive for this operation.</param>
    /// <param name="implicitPayload">Whether to enable implicit payload for this operation.</param>
    /// <param name="sequenceBase">The list base whose contents are the record's payload.</param>
    /// <param name="sequenceElement">The list base's element spelling.</param>
    /// <param name="scalarHeader">Whether the ordinary class can use a combined marker and scalar.</param>
    /// <param name="stringPairs">Whether ordinary reads can group direct string fields.</param>
    /// <param name="rentByRef">Whether the rental returns storage by reference.</param>
    /// <param name="referenceConstruction">Creates a shell to register before decoding its members.</param>
    /// <param name="collectionBase">The appended list record.</param>
    /// <param name="requiredConstruction">The required initialization facts.</param>
    internal RecordModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy, string[] constraints,
        bool versioned, bool fixedUnionCase, bool derived, bool proven, bool recordWidth, bool freshResult, string? mirror,
        string[]? mirrorFields, int[]? mirrorOffsets, string? mirrorLayout, VersionSchemaModel? version,
        ConstructorModel? constructor = null, CallbackModel? callbacks = null, string? rent = null, StorageMirrorModel[]? mirrors = null,
        string? rejection = null, bool recursive = false, bool implicitPayload = false,
        string? sequenceBase = null, string? sequenceElement = null, bool scalarHeader = false, bool stringPairs = false, bool rentByRef = false,
        string? referenceConstruction = null, FormatterModel? collectionBase = null, RequiredConstructionModel? requiredConstruction = null)
        : base(identity, accessibility, PayloadKind.Record, members, policy, constraints, requiredConstruction)
    {
        StringPairs = stringPairs;
        Implicit = implicitPayload;
        SequenceBase = sequenceBase;
        SequenceElement = sequenceElement;
        CollectionBase = collectionBase;
        ScalarHeader = scalarHeader;
        Recursive = recursive;
        Rejection = rejection;
        Rent = rent;
        RentByRef = rentByRef;
        ReferenceConstruction = referenceConstruction;
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
        FixedUnionCase == record.FixedUnionCase && Derived == record.Derived && StringPairs == record.StringPairs &&
        Proven == record.Proven && RecordWidth == record.RecordWidth && FreshResult == record.FreshResult &&
        Mirror == record.Mirror && Rent == record.Rent && RentByRef == record.RentByRef && ReferenceConstruction == record.ReferenceConstruction && Rejection == record.Rejection && Recursive == record.Recursive && Implicit == record.Implicit &&
        SequenceBase == record.SequenceBase && SequenceElement == record.SequenceElement && ScalarHeader == record.ScalarHeader &&
        Equals(CollectionBase, record.CollectionBase) && Equals(Constructor, record.Constructor) &&
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
    /// <summary>The input-selected shape that makes this contract fail closed.</summary>
    internal readonly string? Rejection;

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
    /// <param name="construction">The construction supplied to <c>StructModel</c>.</param>
    /// <param name="serializationCallbacks">The serialization callbacks supplied to <c>StructModel</c>.</param>
    /// <param name="version">The version supplied to <c>StructModel</c>.</param>
    /// <param name="rentCall">The rent call supplied to <c>StructModel</c>.</param>
    /// <param name="rentByRef">Whether to enable rent by ref for this operation.</param>
    /// <param name="rejection">The rejection supplied to <c>StructModel</c>.</param>
    /// <param name="requiredConstruction">The required initialization facts.</param>
    internal StructModel(TypeIdentity identity, TypeAccessibility accessibility, MemberModel[] members, WirePolicy policy, string[] constraints,
        StructStorage storage, PortableLayout? layout, bool recordWidth, bool versioned, bool rent, bool constructors, bool callbacks, FormatterStorageModel? storagePlan = null, string? mirror = null, string[]? mirrorFields = null, int[]? mirrorOffsets = null, string? mirrorLayout = null, ConstructorModel? construction = null, CallbackModel? serializationCallbacks = null,
        VersionSchemaModel? version = null, string? rentCall = null, bool rentByRef = false, string? rejection = null,
        RequiredConstructionModel? requiredConstruction = null)
        : base(identity, accessibility, PayloadKind.Struct, members, policy, constraints, requiredConstruction)
    {
        Rejection = rejection;
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
        Versioned == model.Versioned && Rent == model.Rent && Constructors == model.Constructors && Rejection == model.Rejection &&
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
    /// <param name="access">The access supplied to <c>PortableLeafModel</c>.</param>
    /// <param name="scalar">The scalar supplied to <c>PortableLeafModel</c>.</param>
    /// <param name="size">The requested size.</param>
    /// <param name="isVolatile">Whether to enable is volatile for this operation.</param>
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
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <param name="isVolatile">Whether to enable is volatile for this operation.</param>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <param name="leaves">The leaves supplied to <c>PortableFieldModel</c>.</param>
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
    /// <param name="viewName">The name of the view.</param>
    /// <param name="fields">The fields supplied to <c>PortableLayout</c>.</param>
    /// <param name="wireSize">The wire size supplied to <c>PortableLayout</c>.</param>
    /// <param name="size64">The size64 supplied to <c>PortableLayout</c>.</param>
    /// <param name="size32">The size32 supplied to <c>PortableLayout</c>.</param>
    /// <param name="alignment64">The alignment64 supplied to <c>PortableLayout</c>.</param>
    /// <param name="alignment32">The alignment32 supplied to <c>PortableLayout</c>.</param>
    /// <param name="tagged">Whether to enable tagged for this operation.</param>
    /// <param name="complete">Whether to enable complete for this operation.</param>
    /// <param name="layoutAttribute">The layout attribute supplied to <c>PortableLayout</c>.</param>
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

    /// <summary>Handles flatten for <c>PortableLayout</c>.</summary>
    /// <param name="fields">The fields supplied to <c>Flatten</c>.</param>
    /// <returns>The resulting <c>PortableLeafModel[]</c>.</returns>
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

    /// <summary>Generated cases use exact runtime type comparisons.</summary>
    Exact,
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

    /// <summary>The wire hash of the canonical case name.</summary>
    internal readonly uint NameHash;

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

    /// <summary>The type arguments a generic case passes to its definition's entries, or an empty string.</summary>
    internal readonly string Arguments;

    /// <summary>The open definition of a generic case, whose partial declaration holds its slots, or null.</summary>
    internal readonly string? Definition;

    /// <summary>The closed owner contract of a closed generic case, whose closed Union entries the dispatch calls, or null.</summary>
    internal readonly FormatterModel? Closed;

    /// <summary>The constructed interface implemented by a non-generic case of a generic Union root.</summary>
    internal readonly string? InterfaceContract;

    /// <summary>Captures a Union case.</summary>
    /// <param name="tag">The numeric Union case tag.</param>
    /// <param name="nameHash">The binary case-name hash.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <param name="read">Whether to emit the read operation.</param>
    /// <param name="ownEntry">Whether to enable own entry for this operation.</param>
    /// <param name="readGeneric">Whether to enable read generic for this operation.</param>
    /// <param name="sealedCase">Whether to enable sealed case for this operation.</param>
    /// <param name="fixedCursor">Whether to enable fixed cursor for this operation.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="readable">The readable supplied to <c>UnionCaseModel</c>.</param>
    /// <param name="chain">The chain supplied to <c>UnionCaseModel</c>.</param>
    /// <param name="namespace">The namespace supplied to <c>UnionCaseModel</c>.</param>
    /// <param name="arguments">The arguments supplied to <c>UnionCaseModel</c>.</param>
    /// <param name="definition">The definition supplied to <c>UnionCaseModel</c>.</param>
    /// <param name="closed">The closed supplied to <c>UnionCaseModel</c>.</param>
    /// <param name="interfaceContract">The interface contract supplied to <c>UnionCaseModel</c>.</param>
    internal UnionCaseModel(int tag, uint nameHash, TypeIdentity type, UnionCaseRead read, bool ownEntry, bool readGeneric, bool sealedCase, bool fixedCursor,
        TypeModel? model, string readable, string[] chain, string? @namespace, string arguments = "", string? definition = null,
        FormatterModel? closed = null, string? interfaceContract = null)
    {
        InterfaceContract = interfaceContract;
        Closed = closed;
        Arguments = arguments;
        Definition = definition;
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
        NameHash = nameHash;
    }

    /// <inheritdoc/>
    public bool Equals(UnionCaseModel? other) => ReferenceEquals(this, other) || other != null && Tag == other.Tag && NameHash == other.NameHash && Type.Equals(other.Type) &&
        Read == other.Read && OwnEntry == other.OwnEntry && ReadGeneric == other.ReadGeneric && Sealed == other.Sealed &&
        FixedCursor == other.FixedCursor && Readable == other.Readable && Namespace == other.Namespace && Arguments == other.Arguments &&
        Definition == other.Definition && InterfaceContract == other.InterfaceContract && Equals(Closed, other.Closed) &&
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
    /// <param name="suffix">The suffix supplied to <c>UnionSlotModel</c>.</param>
    /// <param name="profile">The compiled serialization profile or its generated type name.</param>
    /// <param name="identity">The identity supplied to <c>UnionSlotModel</c>.</param>
    /// <param name="encoding">The encoding supplied to <c>UnionSlotModel</c>.</param>
    /// <param name="recording">The recording supplied to <c>UnionSlotModel</c>.</param>
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
    /// <summary>The input-selected case shape that makes this contract fail closed.</summary>
    internal readonly string? Rejection;

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

    /// <summary>Whether the root's declarations can hide <c>object.GetType</c> from an exact dispatch branch.</summary>
    internal readonly bool HidesGetType;

    /// <summary>The original virtual slot suffix when a closed writer can dispatch directly to sealed cases.</summary>
    internal readonly string? ClosedWriteSlot;

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
    /// <param name="identity">The identity supplied to <c>UnionModel</c>.</param>
    /// <param name="accessibility">The accessibility supplied to <c>UnionModel</c>.</param>
    /// <param name="policy">The policy supplied to <c>UnionModel</c>.</param>
    /// <param name="constraints">The constraints supplied to <c>UnionModel</c>.</param>
    /// <param name="cases">The cases supplied to <c>UnionModel</c>.</param>
    /// <param name="isInterface">Whether to enable is interface for this operation.</param>
    /// <param name="wideTags">Whether to enable wide tags for this operation.</param>
    /// <param name="unionSuffix">The union suffix supplied to <c>UnionModel</c>.</param>
    /// <param name="tier">The tier supplied to <c>UnionModel</c>.</param>
    /// <param name="closedWriteSlot">The defining root's virtual slot suffix for a closed exact-type writer.</param>
    /// <param name="hidesGetType">Whether the root's declarations can hide <c>object.GetType</c>.</param>
    /// <param name="cursor">Whether to enable cursor for this operation.</param>
    /// <param name="readGeneric">Whether to enable read generic for this operation.</param>
    /// <param name="chain">The chain supplied to <c>UnionModel</c>.</param>
    /// <param name="namespace">The namespace supplied to <c>UnionModel</c>.</param>
    /// <param name="binarySlots">The binary slots supplied to <c>UnionModel</c>.</param>
    /// <param name="jsonSlots">The JSON slots supplied to <c>UnionModel</c>.</param>
    /// <param name="canonical">Whether to enable canonical for this operation.</param>
    /// <param name="standardBinary">The standard binary supplied to <c>UnionModel</c>.</param>
    /// <param name="standardJson">The standard JSON supplied to <c>UnionModel</c>.</param>
    /// <param name="referenced">Whether to enable referenced for this operation.</param>
    /// <param name="rejection">The rejection supplied to <c>UnionModel</c>.</param>
    internal UnionModel(TypeIdentity identity, TypeAccessibility accessibility, WirePolicy policy, string[] constraints, UnionCaseModel[] cases,
        bool isInterface, bool wideTags, string unionSuffix, UnionDispatchTier tier, bool cursor,
        bool readGeneric, string[] chain, string? @namespace, UnionSlotModel[] binarySlots, UnionSlotModel[] jsonSlots, bool canonical,
        string standardBinary, string standardJson, bool referenced = false, string? rejection = null, string? closedWriteSlot = null,
        bool hidesGetType = false)
        : base(identity, accessibility, PayloadKind.Union, System.Array.Empty<MemberModel>(), policy, constraints)
    {
        Rejection = rejection;
        Referenced = referenced;
        StandardJson = standardJson;
        StandardBinary = standardBinary;
        Canonical = canonical;
        JsonSlots = jsonSlots;
        BinarySlots = binarySlots;
        Namespace = @namespace;
        Chain = chain;
        ReadGeneric = readGeneric;
        Cursor = cursor;
        Tier = tier;
        HidesGetType = hidesGetType;
        ClosedWriteSlot = closedWriteSlot;
        UnionSuffix = unionSuffix;
        WideTags = wideTags;
        IsInterface = isInterface;
        Cases = cases;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeModel? other) => ReferenceEquals(this, other) ||
        other is UnionModel union && base.Equals(other) && IsInterface == union.IsInterface && WideTags == union.WideTags &&
        UnionSuffix == union.UnionSuffix && Tier == union.Tier && ClosedWriteSlot == union.ClosedWriteSlot &&
        HidesGetType == union.HidesGetType && Cursor == union.Cursor && ReadGeneric == union.ReadGeneric &&
        Canonical == union.Canonical && StandardBinary == union.StandardBinary && StandardJson == union.StandardJson &&
        Namespace == union.Namespace && Same(Cases, union.Cases) &&
        Same(Chain, union.Chain) && Same(BinarySlots, union.BinarySlots) &&
        Same(JsonSlots, union.JsonSlots) && Referenced == union.Referenced && Rejection == union.Rejection;

    /// <summary>Whether the root is declared by a referenced assembly whose generated virtual dispatch the writes call; the
    /// consuming assembly declares nothing inside the root or its cases.</summary>
    internal readonly bool Referenced;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Identity.GetHashCode();

    /// <summary>Handles same for <c>UnionModel</c>.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="left">The first input or comparison operand.</param>
    /// <param name="right">The second input or comparison operand.</param>
    /// <returns>Whether the input satisfies the same check.</returns>
    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T>
        => ReferenceEquals(left, right) || left.Length == right.Length && Enumerable.SequenceEqual(left, right);
}
