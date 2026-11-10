using System;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>How a member's binary record is stored.</summary>
internal enum DirectMemberKind : byte
{
    /// <summary>A raw unmanaged scalar stored at a fixed width.</summary>
    Fixed,
    /// <summary>A string record.</summary>
    Text,
    /// <summary>A raw one-dimensional array of unmanaged scalars.</summary>
    Array,
    /// <summary>A raw <c>List&lt;T&gt;</c> of unmanaged scalars.</summary>
    List,
    /// <summary>A dictionary stored as paired records or fused entry records.</summary>
    Dictionary,
    /// <summary>A packable class of this assembly whose members are written inline in the parent record.</summary>
    Object,
    /// <summary>A <c>Stack&lt;T&gt;</c> of raw scalars, written from its storage span.</summary>
    Stack,
    /// <summary>A <c>SortedSet&lt;T&gt;</c> of raw scalars, written through the tree storage helper.</summary>
    SortedSet,
    /// <summary>A <c>SortedList&lt;TKey, TValue&gt;</c> written as paired entries.</summary>
    SortedList,
    /// <summary>A <c>List&lt;KeyValuePair&lt;TKey, TValue&gt;&gt;</c> written as paired entries.</summary>
    PairList,
    /// <summary>A <c>List&lt;List&lt;T&gt;&gt;</c> whose inner lists expand their element records in place.</summary>
    NestedList,
    /// <summary>A dictionary with a nullable raw value, written as sparse raw entries.</summary>
    SparseDictionary,
    /// <summary>A member whose declared type is one of the containing definition's type parameters, written by the generic
    /// serializer itself.</summary>
    Parameter,
}

/// <summary>The entry shape of a sorted or paired collection member.</summary>
internal sealed class DirectCollection : IEquatable<DirectCollection>
{
    /// <summary>The C# spelling of a raw key, such as <c>int</c>.</summary>
    internal readonly string KeyType;
    /// <summary>The C# spelling of a raw value, such as <c>int?</c>.</summary>
    internal readonly string ValueType;
    /// <summary>Whether the value is a string record.</summary>
    internal readonly bool ValueText;
    /// <summary>The inner list spelling of a nested list, or null.</summary>
    internal readonly string ItemType;
    /// <summary>The pair entry spelling of a key/value list, or null.</summary>
    internal readonly string EntryType;

    /// <summary>Captures a collection's entry shape.</summary>
    internal DirectCollection(string keyType, string valueType, bool valueText, string itemType = null, string entryType = null)
    {
        EntryType = entryType;
        ItemType = itemType;
        ValueText = valueText;
        ValueType = valueType;
        KeyType = keyType;
    }

    /// <inheritdoc/>
    public bool Equals(DirectCollection other) => other != null && KeyType == other.KeyType && ValueType == other.ValueType &&
        ValueText == other.ValueText && ItemType == other.ItemType && EntryType == other.EntryType;

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectCollection);

    /// <inheritdoc/>
    public override int GetHashCode() => (KeyType ?? string.Empty).GetHashCode();
}

/// <summary>The stored entry shape of a dictionary member and the text its kernel spells.</summary>
internal sealed class DirectDictionary : IEquatable<DirectDictionary>
{
    /// <summary>Whether both key and value are raw scalars, which stores paired records instead of fused entries.</summary>
    internal readonly bool Pair;
    /// <summary>Whether the key is a string record.</summary>
    internal readonly bool KeyText;
    /// <summary>Whether the value is a string record.</summary>
    internal readonly bool ValueText;
    /// <summary>The C# spelling of a raw key, such as <c>int</c>.</summary>
    internal readonly string KeyType;
    /// <summary>The C# spelling of a raw value, such as <c>long</c>.</summary>
    internal readonly string ValueType;
    /// <summary>The declared dictionary spelling.</summary>
    internal readonly string TypeName;
    /// <summary>The generated formatter owner of the dictionary.</summary>
    internal readonly string Formatter;

    /// <summary>Captures a dictionary's entry shape.</summary>
    internal DirectDictionary(bool pair, bool keyText, bool valueText, string keyType, string valueType, string typeName, string formatter)
    {
        Pair = pair;
        KeyText = keyText;
        ValueText = valueText;
        KeyType = keyType;
        ValueType = valueType;
        TypeName = typeName;
        Formatter = formatter;
    }

    /// <inheritdoc/>
    public bool Equals(DirectDictionary other) => other != null && Pair == other.Pair && KeyText == other.KeyText &&
        ValueText == other.ValueText && KeyType == other.KeyType && ValueType == other.ValueType && TypeName == other.TypeName &&
        Formatter == other.Formatter;

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectDictionary);

    /// <inheritdoc/>
    public override int GetHashCode() => TypeName.GetHashCode();
}

/// <summary>One serialized member of a model-direct type, reduced to the facts its emitted text depends on.</summary>
internal sealed class DirectMember : IEquatable<DirectMember>
{
    /// <summary>The member name, which is also its identifier and JSON name.</summary>
    internal readonly string Name;
    /// <summary>The storage class of the member's binary record.</summary>
    internal readonly DirectMemberKind Kind;
    /// <summary>The scalar keyword of a fixed member, or the element keyword of a collection.</summary>
    internal readonly string Scalar;
    /// <summary>The C# spelling of the declared member type, as the emitters spell temporary locals.</summary>
    internal readonly string TypeName;
    /// <summary>The fixed width of a scalar member, or zero.</summary>
    internal readonly int Size;
    /// <summary>The generated formatter owner of a collection payload, or null.</summary>
    internal readonly string Formatter;
    /// <summary>The emitter's scalar classification.</summary>
    internal readonly LuminFiledType FieldType;
    /// <summary>Whether the member is an auto-property, whose storage is reached through the type's layout mirror.</summary>
    internal readonly bool Mirrored;
    /// <summary>The underlying scalar keyword of an enum member, whose <see cref="Scalar"/> is the enum's global spelling, or null.</summary>
    internal readonly string Underlying;
    /// <summary>Whether the member is a raw unmanaged packable struct, whose width is the runtime size <c>Unsafe.SizeOf&lt;T&gt;()</c>
    /// and whose JSON goes through its binary formatter owner (<see cref="Formatter"/>).</summary>
    internal readonly bool Raw;
    /// <summary>Whether <see cref="Raw"/> names a built-in framework struct with a fixed native width rather than a user declaration.</summary>
    internal readonly bool Builtin;
    /// <summary>The inline model of a packable class member, whose members the parent record stores without a separate record
    /// header, or null.</summary>
    internal readonly DirectTypeModel Nested;
    /// <summary>Whether a collection member writes through its generated formatter call instead of a reserved span, which an
    /// array of unmanaged scalars does.</summary>
    internal readonly bool FormatterCall;
    /// <summary>Whether the member is declared as an auto-property: its mirror storage is assigned rather than passed by
    /// reference, and its existing-instance string read keeps the decoded instance.</summary>
    internal readonly bool Property;
    /// <summary>The entry shape of a dictionary member, or null.</summary>
    internal readonly DirectDictionary Dictionary;
    /// <summary>The entry shape of a sorted, paired or nested collection member, or null.</summary>
    internal readonly DirectCollection Collection;
    /// <summary>Whether a type-parameter member's parameter is constrained to a value type, whose read assigns no reset guard.</summary>
    internal readonly bool ValueTypeParameter;
    /// <summary>Whether the member's declared type is the parameter itself rather than a type that contains it, whose reset guard
    /// tests the runtime type instead of a null object marker.</summary>
    internal readonly bool BareParameter;
    /// <summary>Whether a payload member of a reference type is preceded by a null object marker, whose read resets the member.</summary>
    internal readonly bool NullObjectGuard;
    /// <summary>Whether the member's declared type is a reference type, whose temporary local carries the nullable annotation.</summary>
    internal readonly bool ReferencePayload;

    /// <summary>Captures one member.</summary>
    internal DirectMember(string name, DirectMemberKind kind, string scalar, string typeName, int size, string formatter, LuminFiledType fieldType,
        bool mirrored, string underlying = null, bool raw = false, bool builtin = false, DirectTypeModel nested = null, bool formatterCall = false,
        bool property = false, DirectDictionary dictionary = null, DirectCollection collection = null, bool valueTypeParameter = false,
        bool bareParameter = false, bool nullObjectGuard = false, bool referencePayload = false)
    {
        ReferencePayload = referencePayload;
        NullObjectGuard = nullObjectGuard;
        BareParameter = bareParameter;
        ValueTypeParameter = valueTypeParameter;
        Collection = collection;
        Dictionary = dictionary;
        Property = property;
        FormatterCall = formatterCall;
        Nested = nested;
        Raw = raw;
        Builtin = builtin;
        Underlying = underlying;
        Mirrored = mirrored;
        Name = name;
        Kind = kind;
        Scalar = scalar;
        TypeName = typeName;
        Size = size;
        Formatter = formatter;
        FieldType = fieldType;
    }

    /// <summary>Whether the member stores a fixed scalar or raw struct.</summary>
    internal bool IsFixed => Kind == DirectMemberKind.Fixed;

    /// <summary>Whether the member stores a variable-length payload: a string or a separately framed record.</summary>
    internal bool IsPayload => Kind is DirectMemberKind.Text or DirectMemberKind.Object;

    /// <summary>The member's packed width as the original template spells it: a constant, or the runtime size of a raw struct.</summary>
    internal string Width => Raw ? "Unsafe.SizeOf<" + Scalar + ">()" : Size.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Whether the scalar is a Boolean.</summary>
    internal bool IsBoolean => Kind == DirectMemberKind.Fixed && Scalar == "bool";

    /// <summary>Whether the member is a JSON floating-point read site.</summary>
    internal bool IsFloating => Kind == DirectMemberKind.Fixed && Scalar is "float" or "double";

    /// <inheritdoc/>
    public bool Equals(DirectMember other) => other != null && Name == other.Name && Kind == other.Kind && Scalar == other.Scalar &&
        TypeName == other.TypeName && Size == other.Size && Formatter == other.Formatter && FieldType == other.FieldType && Mirrored == other.Mirrored &&
        Underlying == other.Underlying && Raw == other.Raw && Builtin == other.Builtin && FormatterCall == other.FormatterCall &&
        Property == other.Property && Equals(Dictionary, other.Dictionary) && Equals(Collection, other.Collection) &&
        ValueTypeParameter == other.ValueTypeParameter && BareParameter == other.BareParameter &&
        NullObjectGuard == other.NullObjectGuard && ReferencePayload == other.ReferencePayload && Equals(Nested, other.Nested);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectMember);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}

/// <summary>An immutable, value-equal description of a packable type whose generated roots are written directly from the
/// model instead of through the emitter text and its syntax projections.</summary>
/// <remarks>Only shapes whose complete output the direct emitters reproduce are modeled; every other type returns null from
/// <see cref="TryCreate"/> and keeps the original pipeline. The model holds strings only, so it never retains a compilation.</remarks>
internal sealed class DirectTypeModel : IEquatable<DirectTypeModel>
{
    /// <summary>The fully qualified type spelling, such as <c>global::N.Model</c>.</summary>
    internal readonly string GlobalName;
    /// <summary>The readable payload name used by root containers and facade entries.</summary>
    internal readonly string Readable;
    /// <summary>The assembly's extension container, such as <c>LuminPackExtensions_App</c>.</summary>
    internal readonly string Extension;
    /// <summary>The JSON name-table helper class.</summary>
    internal readonly string JsonHelper;
    /// <summary>Whether JSON reads assign members of the fresh or reused result directly.</summary>
    internal readonly bool FreshResult;
    /// <summary>Whether interception gives the state entries payload-specific names, such as <c>Serialize_Model</c>.</summary>
    internal readonly bool UniqueEntries;
    /// <summary>The serialized members in wire order.</summary>
    internal readonly DirectMember[] Members;
    /// <summary>Whether the variable members are all strings, so the type has a named member write and its reserved record
    /// writes take the proven object path.</summary>
    internal readonly bool Proven;
    /// <summary>Whether the type also has the named member write, which a proven type with a base type does not.</summary>
    internal readonly bool MemberWrite;
    /// <summary>Whether snapshot locals are camel-cased, as the bound operation pipeline names them for mirrored or derived types.</summary>
    internal readonly bool CamelSnapshots;
    /// <summary>Whether the type is a struct: raw unmanaged, written and read as one value, or portable (see <see cref="View"/>).</summary>
    internal readonly bool IsStruct;
    /// <summary>Whether the struct has reference members and is written as a record instead of one raw value.</summary>
    internal readonly bool ManagedStruct;
    /// <summary>The positional storage view and packed transfers of a portable struct, or null.</summary>
    internal readonly DirectPortableView PortableView;
    /// <summary>Whether a member is written in place instead of entering a reservation: an inline object, a string list or a
    /// list whose elements expand their record body in the loop.</summary>
    private static bool InlineBlock(DirectMember member) => member.Kind == DirectMemberKind.Object && !member.Nested.MemberWrite ||
        member.Kind == DirectMemberKind.List && (member.Scalar == "string" || member.Nested != null);
    /// <summary>Whether a member is a write segment of its own: an inline object, a string list, a formatter-call array or a
    /// collection that writes through the writer in place.</summary>
    private static bool Segment(DirectMember member) => member.Kind == DirectMemberKind.Object || InlineBlock(member) ||
        member.Kind == DirectMemberKind.Array && member.FormatterCall ||
        member.Kind is DirectMemberKind.Stack or DirectMemberKind.SortedSet or DirectMemberKind.SortedList or
            DirectMemberKind.PairList or DirectMemberKind.NestedList or DirectMemberKind.SparseDictionary ||
        member.Kind == DirectMemberKind.Parameter;
    /// <summary>Whether every member is a write segment or a fixed scalar, which takes the stream record protocol alone: a
    /// member count, then each member, with no reservation.</summary>
    internal bool StreamRecord => (ManagedStruct || !IsStruct) && Members.Any(Segment) && Members.All(member => member.IsFixed || Segment(member));
    /// <summary>Whether write segments sit beside members that reserve: the record writes its leading members with the header,
    /// then each segment and every following run in place.</summary>
    internal bool MixedRecord => (ManagedStruct || !IsStruct) && Members.Any(Segment) && !StreamRecord;
    /// <summary>Whether a member is copied in place rather than through its named member operation; only a record with such a
    /// member or a formatter-call array camel-cases its snapshot locals.</summary>
    internal bool InlineObject => Members.Any(member => InlineBlock(member) || member.Kind == DirectMemberKind.Array && member.FormatterCall);
    /// <summary>Whether a string anywhere in this record or in an inline object below it needs the profile, which makes the roots
    /// generic.</summary>
    internal bool ProfilePayload => Members.Any(member => member.Kind is DirectMemberKind.Text or DirectMemberKind.Parameter ||
        member.Kind == DirectMemberKind.List && (member.Scalar == "string" || member.Nested != null && member.Nested.ProfilePayload) ||
        member.Kind == DirectMemberKind.Dictionary && (member.Dictionary.KeyText || member.Dictionary.ValueText) ||
        member.Kind is DirectMemberKind.SortedList or DirectMemberKind.PairList && member.Collection.ValueText ||
        member.Kind == DirectMemberKind.NestedList && member.Nested.ProfilePayload ||
        member.Kind == DirectMemberKind.Array && member.FormatterCall ||
        member.Kind == DirectMemberKind.Object && member.Nested.ProfilePayload);

    /// <summary>An array inside an inline object calls its generated formatter with the operation's profile, which makes the
    /// parent's roots generic; a top-level array keeps its own non-generic write.</summary>
    internal bool InlineArray => Members.Any(member => member.Kind == DirectMemberKind.Object &&
        (member.Nested.Members.Any(item => item.Kind == DirectMemberKind.Array) || member.Nested.InlineArray));
    /// <summary>A list inside an inline object reads through its generated formatter with the operation's profile, which makes
    /// the parent's read root generic; its write expands inline and needs no profile, as a top-level list does.</summary>
    internal bool InlineList => Members.Any(member => member.Kind == DirectMemberKind.Object &&
        (member.Nested.Members.Any(item => item.Kind == DirectMemberKind.List) || member.Nested.InlineList));
    /// <summary>Whether the write body contains an element loop or expands an inline object that does, which leaves native
    /// inlining to the runtime.</summary>
    internal bool ElementList => Members.Any(member => member.Kind == DirectMemberKind.List && member.Nested != null ||
        member.Kind is DirectMemberKind.Stack or DirectMemberKind.SortedSet or DirectMemberKind.SortedList or
            DirectMemberKind.PairList or DirectMemberKind.NestedList or DirectMemberKind.SparseDictionary ||
        member.Kind == DirectMemberKind.Object && member.Nested.ElementList);
    /// <summary>Whether a Boolean array or list member takes the checked read completion, whose inlined root renames the
    /// instance local.</summary>
    internal bool BooleanCollection => Members.Any(member => member.Kind is DirectMemberKind.Array or DirectMemberKind.List &&
        member.Scalar == "bool");
    /// <summary>Whether a dictionary member takes the cursor write path with fused entries or paired records.</summary>
    internal bool HasDictionary => Members.Any(member => member.Kind == DirectMemberKind.Dictionary ||
        member.Kind == DirectMemberKind.Object && member.Nested.HasDictionary);
    /// <summary>The positional storage view of a portable struct, whose fields are copied one by one at packed wire offsets, or null.</summary>
    internal readonly string View;
    /// <summary>Whether the portable view is elided: every positional view name is also a field name of the struct, so the
    /// original projection names the struct's fields directly (by name, not by position).</summary>
    internal readonly bool ViewElided;
    /// <summary>The root-private layout mirror that auto-property storage is reached through, or null for public fields.</summary>
    internal readonly string Mirror;
    /// <summary>The mirror's field declarations, such as <c>string? Name</c>, in storage order.</summary>
    internal readonly string[] MirrorFields;
    /// <summary>The explicit-layout offset of each mirror field, or -1 for a field without one; null when the type has no
    /// explicit layout.</summary>
    internal readonly int[] MirrorOffsets;
    /// <summary>Whether the Union write is the shared returning cursor kernel <c>SerializeUnionAt</c> of a fixed Union case.</summary>
    internal readonly bool FixedUnionCase;
    /// <summary>Whether the type has the record measurement entry <c>MeasureRecord</c>, which the original selection gives only to
    /// supported fixed records.</summary>
    internal readonly bool MeasureRecord;
    /// <summary>The arguments of a raw struct's selected constructor, which its JSON read constructs a fresh value with: a member's
    /// name, or null for <c>default</c>, per parameter; null when the value is created by its parameterless constructor.</summary>
    internal readonly string[] ConstructorArguments;
    /// <summary>The members the selected constructor sets, which the object initializer leaves out.</summary>
    internal readonly string[] ConstructorMembers;
    /// <summary>The member names in declaration order, which the constructed read declares its temporaries in, or null.</summary>
    internal readonly string[] DeclaredOrder;
    /// <summary>Whether the project targets .NET 8 or later, whose operations take <c>scoped</c> state and write generated JSON
    /// literals.</summary>
    internal readonly bool Net8;
    /// <summary>Whether JSON property heads are UTF-8 string literal properties (.NET 8 and C# 11 or later) rather than arrays.</summary>
    internal readonly bool Utf8Literals;
    /// <summary>The version-tolerant schema, or null for an ordinary record.</summary>
    internal readonly DirectVersionSchema Version;
    /// <summary>Whether a declared instance member is not unmanaged storage, which gives the Union read its outlined entry.</summary>
    internal readonly bool ReferencePayload;
    /// <summary>The CLR layout attribute line of a declared layout replica, or null.</summary>
    internal readonly string LayoutAttribute;
    /// <summary>The static pool rent call of a pooled type, such as <c>global::T.Rent()</c>, or null.</summary>
    internal readonly string Rent;
    /// <summary>The complete-record byte bound a <c>[LuminPackBounded]</c> declaration guarantees, which the write roots reserve
    /// once and assert, or null.</summary>
    internal readonly int? Bounded;
    /// <summary>Whether the type is reference-tracked: its record carries a slot table and an object reference id.</summary>
    internal readonly bool Circle;
    /// <summary>Whether a reference-tracked type participates in a reference cycle, so its roots guard the stack.</summary>
    internal readonly bool CircleGuarded;
    /// <summary>The serialization callbacks, spelled as statements of the write or read root, in declaration order.</summary>
    internal readonly string[] WriteBefore = System.Array.Empty<string>(), WriteAfter = System.Array.Empty<string>(),
        ReadBefore = System.Array.Empty<string>(), ReadAfter = System.Array.Empty<string>();

    /// <summary>Whether this is a Union root, whose file is the root operations plus the virtual dispatch, registration and
    /// per-case slots rather than a record or struct payload.</summary>
    internal readonly bool UnionRoot;
    /// <summary>Whether the Union root is an interface, whose slots are implicit interface members and have no dispatch tier.</summary>
    internal readonly bool UnionInterface;
    /// <summary>The canonical Union slot suffix.</summary>
    internal readonly string UnionSuffix;
    /// <summary>The declared cases in tag order.</summary>
    internal readonly DirectUnionCase[] UnionCases;
    /// <summary>Whether a fixed case gives the root the returning cursor protocol.</summary>
    internal readonly bool UnionCursor;
    /// <summary>Whether the binary read root takes the operation's profile, because some case entry does.</summary>
    internal readonly bool UnionReadGeneric;
    /// <summary>The partial declarations from the outermost containing type to the root.</summary>
    internal readonly string[] UnionChain;
    /// <summary>The root's namespace, or null in the global namespace.</summary>
    internal readonly string UnionNamespace;
    /// <summary>The compiled binary profile slots in profile order.</summary>
    internal readonly DirectUnionSlot[] UnionBinarySlots;
    /// <summary>The compiled JSON profile slots in profile order.</summary>
    internal readonly DirectUnionSlot[] UnionJsonSlots;
    /// <summary>Whether the exact canonical profile shortcuts can bind the referenced runtime's profiles.</summary>
    internal readonly bool UnionCanonical;
    /// <summary>The imported string options type of the declared binary profile, which the non-generic entries pass.</summary>
    internal readonly string UnionStandardBinary;
    /// <summary>The imported string options type of the declared JSON profile.</summary>
    internal readonly string UnionStandardJson;
    /// <summary>The selected compiled-slot dispatch tier: zero virtual, one exact patterns, two direct.</summary>
    internal readonly int UnionTier;
    /// <summary>The exact case types of a pattern or direct tier, in tag order.</summary>
    internal readonly string[] UnionTierCases;
    /// <summary>Whether each exact tier case is sealed, which needs no exact runtime type test.</summary>
    internal readonly bool[] UnionTierSealed;
    /// <summary>Whether the root writes and reads three-byte Union headers, because a declared case tag exceeds the narrow
    /// byte protocol or the root declares <c>[LuminPackWideTag]</c>.</summary>
    internal readonly bool UnionWide;

    /// <summary>The names of the definition's type parameters, in declaration order, or empty for a closed type.</summary>
    internal readonly string[] TypeParameters;
    /// <summary>The definition's constraint clauses, each spelled as <c>where T : class</c>, in declaration order.</summary>
    internal readonly string[] TypeConstraints;

    /// <summary>The angle-bracketed type-parameter list of method and mirror declarations, or an empty string for a closed type.</summary>
    internal string TypeParameterList => TypeParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", TypeParameters) + ">";

    /// <summary>Spells a method's type-argument list: the definition's parameters, then the given operation tokens.</summary>
    /// <param name="extra">The profile or options parameter that follows the definition's own parameters.</param>
    /// <returns>The angle-bracketed argument list, or an empty string for a closed type without extras.</returns>
    internal string TypeArguments(params string[] extra) => extra.Length == 0 ? TypeParameterList :
        "<" + string.Join(", ", TypeParameters.Concat(extra)) + ">";

    /// <summary>The joined constraint clauses, without trailing text, or an empty string.</summary>
    internal string TypeConstraintText => string.Join(" ", TypeConstraints);

    /// <summary>Whether any serialization callback runs, which keeps the type out of the named member write.</summary>
    internal bool HasCallbacks => WriteBefore.Length != 0 || WriteAfter.Length != 0 || ReadBefore.Length != 0 || ReadAfter.Length != 0;

    private DirectTypeModel(string globalName, string readable, string extension, string jsonHelper, bool freshResult, bool uniqueEntries,
        DirectMember[] members, bool proven, string mirror, string[] mirrorFields, bool derived, bool isStruct, string view, bool viewElided,
        bool fixedUnionCase, bool measureRecord, string[] constructorArguments, string[] constructorMembers, bool memberWrite, bool net8, bool utf8Literals,
        DirectVersionSchema version, bool referencePayload, string layoutAttribute, string rent = null, string[] writeBefore = null,
        string[] writeAfter = null, string[] readBefore = null, string[] readAfter = null, string[] declaredOrder = null, int? bounded = null,
        bool unionRoot = false, bool unionInterface = false, string unionSuffix = null, DirectUnionCase[] unionCases = null,
        bool unionCursor = false, bool unionReadGeneric = false, string[] unionChain = null, string unionNamespace = null,
        DirectUnionSlot[] unionBinarySlots = null, DirectUnionSlot[] unionJsonSlots = null, bool unionCanonical = false,
        string unionStandardBinary = null, string unionStandardJson = null, int unionTier = 0, string[] unionTierCases = null,
        bool[] unionTierSealed = null, bool circle = false, bool circleGuarded = false, bool managedStruct = false,
        DirectPortableView portableView = null, bool unionWide = false, int[] mirrorOffsets = null, string[] typeParameters = null,
        string[] typeConstraints = null)
    {
        TypeConstraints = typeConstraints ?? System.Array.Empty<string>();
        TypeParameters = typeParameters ?? System.Array.Empty<string>();
        MirrorOffsets = mirrorOffsets;
        UnionWide = unionWide;
        PortableView = portableView;
        ManagedStruct = managedStruct;
        CircleGuarded = circleGuarded;
        Circle = circle;
        UnionTierSealed = unionTierSealed;
        UnionTierCases = unionTierCases;
        UnionTier = unionTier;
        UnionStandardJson = unionStandardJson;
        UnionStandardBinary = unionStandardBinary;
        UnionCanonical = unionCanonical;
        UnionJsonSlots = unionJsonSlots;
        UnionBinarySlots = unionBinarySlots;
        UnionNamespace = unionNamespace;
        UnionChain = unionChain;
        UnionReadGeneric = unionReadGeneric;
        UnionCursor = unionCursor;
        UnionCases = unionCases;
        UnionSuffix = unionSuffix;
        UnionInterface = unionInterface;
        UnionRoot = unionRoot;
        Bounded = bounded;
        Version = version;
        ReferencePayload = referencePayload;
        LayoutAttribute = layoutAttribute;
        Rent = rent;
        DeclaredOrder = declaredOrder;
        if (writeBefore != null)
            WriteBefore = writeBefore;
        if (writeAfter != null)
            WriteAfter = writeAfter;
        if (readBefore != null)
            ReadBefore = readBefore;
        if (readAfter != null)
            ReadAfter = readAfter;
        Net8 = net8;
        Utf8Literals = utf8Literals;
        ConstructorArguments = constructorArguments;
        ConstructorMembers = constructorMembers;
        MeasureRecord = measureRecord;
        FixedUnionCase = fixedUnionCase;
        IsStruct = isStruct;
        View = view;
        ViewElided = viewElided;
        Proven = proven;
        MemberWrite = memberWrite;
        CamelSnapshots = isStruct || derived || members.Any(member => member.Mirrored);
        Mirror = mirror;
        MirrorFields = mirrorFields;
        UniqueEntries = uniqueEntries;
        GlobalName = globalName;
        Readable = readable;
        Extension = extension;
        JsonHelper = jsonHelper;
        FreshResult = freshResult;
        Members = members;
    }

    /// <summary>The binary formatter owner of this payload.</summary>
    internal string Owner => BinaryExtensionProjection.Owner(Extension, GlobalName);

    /// <inheritdoc/>
    public bool Equals(DirectTypeModel other) => ReferenceEquals(this, other) || other != null && GlobalName == other.GlobalName &&
        Readable == other.Readable && Extension == other.Extension && JsonHelper == other.JsonHelper && FreshResult == other.FreshResult &&
        UniqueEntries == other.UniqueEntries && Proven == other.Proven && MemberWrite == other.MemberWrite &&
        CamelSnapshots == other.CamelSnapshots && IsStruct == other.IsStruct && ManagedStruct == other.ManagedStruct &&
        Equals(PortableView, other.PortableView) && View == other.View && ViewElided == other.ViewElided && Mirror == other.Mirror &&
        FixedUnionCase == other.FixedUnionCase && MeasureRecord == other.MeasureRecord &&
        Rent == other.Rent && Same(WriteBefore, other.WriteBefore) && Same(WriteAfter, other.WriteAfter) &&
        Same(ReadBefore, other.ReadBefore) && Same(ReadAfter, other.ReadAfter) &&
        Same(ConstructorArguments, other.ConstructorArguments) && Same(ConstructorMembers, other.ConstructorMembers) &&
        Same(DeclaredOrder, other.DeclaredOrder) && Bounded == other.Bounded &&
        Net8 == other.Net8 && Utf8Literals == other.Utf8Literals && Equals(Version, other.Version) &&
        ReferencePayload == other.ReferencePayload && LayoutAttribute == other.LayoutAttribute &&
        UnionRoot == other.UnionRoot && UnionInterface == other.UnionInterface && UnionSuffix == other.UnionSuffix &&
        UnionCursor == other.UnionCursor && UnionReadGeneric == other.UnionReadGeneric && UnionNamespace == other.UnionNamespace &&
        UnionCanonical == other.UnionCanonical && UnionStandardBinary == other.UnionStandardBinary && UnionStandardJson == other.UnionStandardJson &&
        UnionTier == other.UnionTier && UnionWide == other.UnionWide && Same(UnionChain, other.UnionChain) && Same(UnionTierCases, other.UnionTierCases) &&
        Same(UnionTierSealed, other.UnionTierSealed) && Same(UnionBinarySlots, other.UnionBinarySlots) && Same(UnionJsonSlots, other.UnionJsonSlots) &&
        Circle == other.Circle && CircleGuarded == other.CircleGuarded &&
        Same(UnionCases, other.UnionCases) && Same(TypeParameters, other.TypeParameters) && Same(TypeConstraints, other.TypeConstraints) &&
        (MirrorFields == null ? other.MirrorFields == null : other.MirrorFields != null && MirrorFields.SequenceEqual(other.MirrorFields)) &&
        Same(MirrorOffsets, other.MirrorOffsets) &&
        Members.SequenceEqual(other.Members);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectTypeModel);

    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T> => left == null ? right == null : right != null && left.SequenceEqual(right);

    /// <inheritdoc/>
    public override int GetHashCode() => GlobalName.GetHashCode();

    /// <summary>How many models the current thread is building, which stops a cycle of inline objects from recursing forever.</summary>
    [ThreadStatic]
    private static int _depth;

    /// <summary>How many Union case models the current thread is building, whose bases may declare wire profile options.</summary>
    [ThreadStatic]
    private static int _unionCase;

    /// <summary>Models a type when its complete binary and JSON roots belong to a shape the direct emitters cover.</summary>
    /// <param name="data">The analyzed type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The project generation settings.</param>
    /// <returns>The model, or null when the type keeps the original pipeline.</returns>
    internal static DirectTypeModel TryCreate(LuminDataInfo data, Compilation compilation, MetaInfo metadata)
    {
        if (DirectEmission.Mode == DirectEmissionMode.Off || !GeneratedSourceLayout.Enabled)
            return null;
        string traced = data.classFullName ?? data.TypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "?";
        DirectEmission.Enter(traced);
        _depth++;
        try
        {
            var model = Create(data, compilation, metadata);
            if (model != null)
                DirectEmission.Accept(traced);
            return model;
        }
        finally
        {
            _depth--;
            DirectEmission.Exit();
        }
    }

    /// <summary>Models one type, recording the failing check under the caller's trace context.</summary>
    private static DirectTypeModel Create(LuminDataInfo data, Compilation compilation, MetaInfo metadata)
    {
        if (!SupportedProject(compilation, metadata))
            return null;
        if (data.isUnion)
            return data.isValueType ? DirectEmission.Reject<DirectTypeModel>("union value type") : CreateUnion(data, compilation, metadata);
        var type = data.TypeSymbol;
        // A struct is covered when its storage is its wire layout (a raw unmanaged struct), or when a portable plan copies its
        // scalar fields one by one at packed offsets.
        bool raw = data.isValueType && type != null && type.TypeKind == TypeKind.Struct && EmitterTypeTraits.CanUseUnmanagedSerialization(type);
        var portable = data.isValueType && !raw && type != null && type.TypeKind == TypeKind.Struct && !PortableStructPlan.RequiresClosedDispatch(type)
            ? PortableStructPlan.TryCreate(type) : null;
        // A portable struct with a nested aggregate needs its child view; tuple components flatten into their parent view.
        if (portable != null && (portable.Fields == null || portable.Children == null ||
            portable.Children.Any(child => child.Fields != null && !TupleStorage(child.Type)) || type is INamedTypeSymbol { IsTupleType: true }))
            portable = null;
        // A struct with reference members keeps the record protocol instead of a raw copy.
        bool managedStruct = data.isValueType && type != null && type.TypeKind == TypeKind.Struct && !raw && portable == null;
        bool isStruct = raw || portable != null || managedStruct;
        bool versioned = data.generatorType == GeneratorType.VersionTolerant;
        bool circle = data.generatorType == GeneratorType.CircleReference;
        // A version-tolerant struct writes a schema prefix and revisions, which the struct roots do not reproduce yet.
        if (isStruct && versioned)
            return DirectEmission.Reject<DirectTypeModel>("versioned struct");
        // A raw or portable generic value type closes over a formatter-dispatch contract, a version-tolerant definition keeps
        // its own schema and name tables, and the circle roots spell their own storage protocol, so those keep the original
        // pipeline. A managed generic struct writes the record protocol and is covered.
        if (data.isGeneric && (versioned || circle || data.isValueType && !managedStruct))
            return DirectEmission.Reject<DirectTypeModel>("type kind: generic " +
                (data.isValueType ? "struct" : versioned ? "version tolerant" : "circle"));
        if (type == null || data.generatorType != GeneratorType.Object && !versioned && !circle || data.isUnion ||
            data.isValueType && !isStruct)
            return DirectEmission.Reject<DirectTypeModel>("type kind: " + (type == null ? "none" : data.isUnion ? "union" :
                data.isValueType ? "struct" : data.generatorType.ToString()));
        // Callbacks and pool rents shape the binary roots; a version-tolerant root without callbacks covers a plain rent,
        // while a rent returning by reference needs its own storage protocol and is not modeled yet. A version-tolerant
        // struct keeps its callbacks around the same schema prefix and revision body.
        if (data.enableBurst || data.RentPoolMethod is { ReturnsByRef: true } ||
            versioned && data.callBackMethods.Count != 0 && !isStruct)
            return DirectEmission.Reject<DirectTypeModel>("callbacks, pool or burst");
        if (data.Diagnostics.Length != 0)
            return DirectEmission.Reject<DirectTypeModel>("diagnostics");
        if (data.fields.Count > 32)
            return DirectEmission.Reject<DirectTypeModel>("member count: " + data.fields.Count);
        if (type.TypeKind != (isStruct ? TypeKind.Struct : TypeKind.Class) || type.IsAbstract || type.IsStatic ||
            type.ContainingType is { IsGenericType: true } || !Reachable(type))
            return DirectEmission.Reject<DirectTypeModel>("type declaration: " + (type.TypeKind != TypeKind.Class ? "kind" : type.IsAbstract ? "abstract" :
                type.IsStatic ? "static" : type.ContainingType != null && type.ContainingType.IsGenericType ? "nested in generic" :
                "accessibility"));
        // The definition's own parameters and constraints are spelled on every generic member, exactly as the original
        // templates append them before the profile constraint.
        string[] typeParameters = data.GenericParameters.Count == 0 ? null : data.GenericParameters.ToArray();
        string[] typeConstraints = ConstraintClauses(data);
        string parameterList = typeParameters == null ? string.Empty : "<" + string.Join(", ", typeParameters) + ">";
        // Base types contribute their fields after the type's own, and must be plain or abstract classes of the same assembly. A Union
        // base's case list only shapes the base's own dispatch file, never the files of its cases.
        var levels = new System.Collections.Generic.List<INamedTypeSymbol> { type };
        for (var level = type.BaseType; level != null && level.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); level = level.BaseType)
        {
            // A generic base is covered only when it closes over the derived definition's own type parameters, so every member
            // keeps the type-parameter spelling the generic templates repeat.
            if (level.TypeKind != TypeKind.Class || level.IsStatic || level.IsRecord ||
                level.ContainingType is { IsGenericType: true } || !Reachable(level) ||
                level.IsGenericType && !OwnParameters(level, type) ||
                !SymbolEqualityComparer.Default.Equals(level.ContainingAssembly, compilation.Assembly) ||
                level.GetAttributes().Any(attribute => !PlainPackable(attribute) && !Foreign(attribute) && !UnionCase(attribute) && !JsonPolymorphism(attribute) &&
                    !StructLayoutAttribute(attribute) && !WideTagAttribute(attribute) &&
                    !(_unionCase > 0 && OptionAttribute(attribute)) && !(OptionAttribute(attribute) && UnionBase(level))))
                return DirectEmission.Reject<DirectTypeModel>("base type: " + level.ToDisplayString());
            levels.Add(level);
        }
        bool derived = levels.Count > 1;
        // A selected constructor with parameters builds the instance from temporary locals on read; otherwise the type needs
        // one public parameterless constructor, as the generated roots create it with new T(). A record also declares its
        // protected copy constructor, which never runs here.
        bool selectedConstructor = data.SelectedConstructor is { Parameters.Count: > 0 };
        // The generated reads create a parameterless instance, while other overloads never run; a record's positional and
        // copy constructors may sit beside it. A selected constructor with parameters builds the instance from temporaries.
        bool parameterless = type.InstanceConstructors.Any(ctor => ctor.DeclaredAccessibility == Accessibility.Public &&
            ctor.Parameters.Length == 0);
        if (!raw && !selectedConstructor && !parameterless)
            return DirectEmission.Reject<DirectTypeModel>("constructor: " + type.InstanceConstructors.Length + " " +
                string.Join("+", type.InstanceConstructors.Select(item => item.Parameters.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    item.DeclaredAccessibility.ToString())));
        // A reference-tracked record owns its complete storage mirror, one instance and one allocation site. Base types, pool
        // rents and constructor arguments change that framing and keep the original pipeline.
        if (circle && (derived || data.RentPoolMethod != null || selectedConstructor))
            return DirectEmission.Reject<DirectTypeModel>("circle storage");
        // The constructed read stores each member in a temporary local, so it is limited after the members are reduced.
        var constructedMembers = !raw && selectedConstructor;
        var attributes = type.GetAttributes().Where(attribute => !Foreign(attribute) && !StructLayoutOption(attribute) && !StructLayoutAttribute(attribute)).ToArray();
        if (!PackableAttributes(attributes, versioned, circle, out int? bounded))
            return DirectEmission.Reject<DirectTypeModel>("type attributes: " + string.Join("+", attributes.Select(attribute =>
                attribute.AttributeClass?.Name + "(" + attribute.ConstructorArguments.Length + "," + attribute.NamedArguments.Length + ")")));
        // Members without storage in the instance and without attributes, such as methods, computed properties and static
        // fields, are never serialized. Any other declared member keeps the original pipeline.
        int fieldsDeclared = 0;
        foreach (var member in levels.SelectMany(level => level.GetMembers()))
        {
            // A base type's constructors only run as part of the type's own construction.
            if (member is IMethodSymbol { MethodKind: MethodKind.Constructor } && !SymbolEqualityComparer.Default.Equals(member.ContainingType, type))
                continue;
            if (member.IsImplicitlyDeclared)
                continue;
            // A nested declaration has no instance storage and is never serialized; its own attributes do not describe this type.
            if (member is INamedTypeSymbol)
                continue;
            if (member.GetAttributes().Any(attribute => !Foreign(attribute) &&
                    !(member is IMethodSymbol { MethodKind: MethodKind.Ordinary } && attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.MethodImplAttribute") &&
                    !(member is IMethodSymbol { MethodKind: MethodKind.Ordinary } && SerializationCallback(attribute)) &&
                    !(member is IMethodSymbol { MethodKind: MethodKind.Constructor } && attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackConstructorAttribute") &&
                    !(member is IFieldSymbol or IPropertySymbol && attribute.AttributeClass?.ToDisplayString() is "LuminPack.Attribute.LuminPackOrderAttribute"
                        or "LuminPack.Attribute.LuminPackIgnoreAttribute" or "LuminPack.Attribute.LuminPackIncludeAttribute") &&
                    !(isStruct && member is IFieldSymbol && attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.FieldOffsetAttribute") &&
                    !(versioned && member is IFieldSymbol && attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOldNameAttribute")))
                return DirectEmission.Reject<DirectTypeModel>("member attributes: " + member.Name + " [" + string.Join("+",
                    member.GetAttributes().Select(attribute => attribute.AttributeClass?.Name)) + "]");
            // An ignored member keeps its storage but is never serialized, whatever its accessibility.
            if (member is IFieldSymbol or IPropertySymbol && member.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackIgnoreAttribute"))
                continue;
            // An auto-property is stored in its backing field, which the generated code reaches through the layout mirror.
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } auto &&
                auto.ContainingType.GetMembers().OfType<IFieldSymbol>().Any(backing => SymbolEqualityComparer.Default.Equals(backing.AssociatedSymbol, auto)))
            {
                // The setter may be init-only or absent: every auto-property gets a layout mirror, and the binary and JSON
                // paths read and write it through that mirror, never through the setter.
                if (auto.GetMethod is not { DeclaredAccessibility: Accessibility.Public } ||
                    auto.SetMethod is { } setter && setter.DeclaredAccessibility != Accessibility.Public ||
                    auto.GetMethod.GetAttributes().Length != 0 || auto.SetMethod?.GetAttributes().Length > 0)
                    return DirectEmission.Reject<DirectTypeModel>("declared property");
                fieldsDeclared++;
                continue;
            }
            if (member is IFieldSymbol field)
            {
                if (field.IsStatic || field.IsConst)
                    continue;
                // A non-public field is part of the wire format only when [LuminPackInclude] names it; other private and
                // protected storage never enters the record. Portable struct fields may be volatile snapshots.
                bool included = field.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackIncludeAttribute");
                if (!included && field.DeclaredAccessibility != Accessibility.Public)
                    continue;
                // A private included field may be readonly when the mirror or positional view assigns it; any other readonly
                // field keeps the original pipeline.
                bool storageMirrored = data.fields.Any(item => item.isProperty || item.IsPrivate);
                if (field.IsReadOnly && !(included && (storageMirrored || portable != null)) ||
                    field.IsVolatile && portable == null)
                    return DirectEmission.Reject<DirectTypeModel>("declared field: " + field.Name + " " + field.DeclaredAccessibility +
                        (field.IsReadOnly ? " readonly" : string.Empty) + (field.IsVolatile ? " volatile" : string.Empty));
                fieldsDeclared++;
                continue;
            }
            // A private auto-property that is not included has instance storage but is never serialized. This holds for
            // classes and structs alike.
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false } privateProperty &&
                privateProperty.DeclaredAccessibility != Accessibility.Public &&
                !privateProperty.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackIncludeAttribute"))
                continue;
            if (!Unserialized(member))
                return DirectEmission.Reject<DirectTypeModel>("declared member: " + member.Kind + " " + member.Name);
        }
        if (fieldsDeclared != data.fields.Count)
            return DirectEmission.Reject<DirectTypeModel>("declared members: " + fieldsDeclared + "/" + data.fields.Count);

        string extension = LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation);
        var memberParameters = typeParameters == null ? null : new System.Collections.Generic.HashSet<string>(typeParameters, StringComparer.Ordinal);
        var members = new DirectMember[data.fields.Count];
        for (int index = 0; index < members.Length; index++)
            if ((members[index] = Member(data.fields[index], extension, compilation, metadata, versioned, circle, type,
                data.fields[index].IsPrivate, portable != null, memberParameters)) == null)
                return DirectEmission.Reject<DirectTypeModel>("member shape");
        // The constructed read stores each member in a temporary local, which fixed scalars, strings, built-in values and
        // type parameters spell.
        if (constructedMembers && members.Any(member => member.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Text or
            DirectMemberKind.Parameter) && !(member.Raw && member.Builtin)))
            return DirectEmission.Reject<DirectTypeModel>("constructor members");
        // A reference-tracked record covers fixed scalars of at most four bytes, strings and other reference-tracked records.
        // Collections, raw struct widths, slot gaps and oversized tables change its table protocol and keep the original pipeline.
        if (circle && (members.Any(member => member.Kind is DirectMemberKind.Array or DirectMemberKind.List or
                DirectMemberKind.Dictionary or DirectMemberKind.Stack or DirectMemberKind.SortedSet or DirectMemberKind.SortedList or
                DirectMemberKind.PairList or DirectMemberKind.NestedList or DirectMemberKind.SparseDictionary ||
                member.Kind == DirectMemberKind.Fixed && (member.Raw || member.Size > 4)) ||
            data.fields.Where((field, index) => field.Order != (uint)index).Any() || data.fields.Count > 249))
            return DirectEmission.Reject<DirectTypeModel>("circle members");
        // Reserve-able members may only precede the reference members; a later one would reopen a reservation group.
        int firstObject = System.Array.FindIndex(members, member => member.Kind == DirectMemberKind.Object);
        if (circle && firstObject >= 0 && members.Skip(firstObject).Any(member => !member.IsPayload))
            return DirectEmission.Reject<DirectTypeModel>("circle member order");
        // A bounded write reserves the complete record once; the version-tolerant template reserves per member instead, and
        // a list or inline object owns exit labels and its own capacity handling.
        if (bounded != null && (versioned || isStruct || circle || members.Any(member => member.Kind is DirectMemberKind.Object
            or DirectMemberKind.List or DirectMemberKind.Dictionary or DirectMemberKind.Stack or DirectMemberKind.SortedSet or
            DirectMemberKind.SortedList or DirectMemberKind.PairList or DirectMemberKind.NestedList or DirectMemberKind.SparseDictionary ||
            member.Kind == DirectMemberKind.Array && member.FormatterCall)))
            return DirectEmission.Reject<DirectTypeModel>("bounded record");
        // A local case of an abstract packable base whose payload is raw fixed storage writes through the fixed Union cursor protocol.
        bool fixedUnionCase = !isStruct && members.All(member => member.IsFixed) && FixedUnionCursorPlan.CanUsePayload(type, compilation, metadata);
        // Raw struct members are covered in fixed Union case records of at least two fields without Booleans, whose batch masks
        // need constant offsets; a raw struct copies its complete storage, so its nested raw members need no fixed-run proof.
        if (!raw && members.Any(member => member.IsFixed && member.Raw && !member.Builtin) &&
            (!fixedUnionCase || members.Length < 2 || members.Any(member => member.IsBoolean || member.Mirrored)))
            return DirectEmission.Reject<DirectTypeModel>("struct member record");
        // Included non-public fields are reached through the same layout mirror as auto-property storage, and a
        // circle-reference payload always declares its storage mirror.
        bool mirrored = circle || data.fields.Any(field => field.isProperty || field.IsPrivate);
        // A raw struct copies its complete storage; a portable struct transfers only fixed fields; a managed struct records
        // fixed scalars, strings, built-in values and type-parameter payloads until inline objects and collections join it.
        if (isStruct)
        {
            if (raw && members.Any(member => !member.IsFixed))
                return DirectEmission.Reject<DirectTypeModel>("struct members");
            if (portable != null && members.Any(member => !member.IsFixed))
                return DirectEmission.Reject<DirectTypeModel>("struct members");
            if (managedStruct && members.Any(member => !member.IsFixed && member.Kind is not (DirectMemberKind.Text or DirectMemberKind.Parameter) ||
                member.Raw && !member.Builtin))
                return DirectEmission.Reject<DirectTypeModel>("struct members");
        }
        bool strings = !mirrored && members.Any(member => member.Kind == DirectMemberKind.Text) &&
            members.All(member => member.Kind is DirectMemberKind.Fixed or DirectMemberKind.Text);
        // The proven object write needs .NET 8, unsafe code and a non-Unity project, as ProvenObjectWriteProjection checks.
        bool unity = metadata.IsForUnity || metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal));
        bool proven = !isStruct && strings && !versioned && metadata.IsNet8 && metadata.AllowUnsafe && !unity &&
            data.callBackMethods.Count == 0 && bounded == null;
        if (proven && !ProvenWriteProfiles.AreCanonical(compilation))
            return DirectEmission.Reject<DirectTypeModel>("proven object write");
        // JSON read locals drop a leading underscore; two members that would share one local cannot be told apart. A bounded
        // record stages such a member through a projection-owned placeholder whose spelled name is not modeled yet.
        if (bounded != null && members.Any(member => member.Name[0] == '_') ||
            members.Select(member => DirectEmission.LocalName(member.Name)).Distinct(StringComparer.Ordinal).Count() != members.Length)
            return DirectEmission.Reject<DirectTypeModel>("member names");
        // Inline object members keep the stream record protocol: a marker holding the member count, then each member in
        // order. Beside reserving members the record writes a reserved prefix with the header, then each object and every
        // following run in place.
        bool objects = members.Any(member => member.Kind == DirectMemberKind.Object);
        if (objects && (versioned || derived || isStruct))
            return DirectEmission.Reject<DirectTypeModel>("nested object record");

        string globalName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (data.classFullName != null && data.classFullName != globalName && "global::" + data.classFullName != globalName)
            return DirectEmission.Reject<DirectTypeModel>("type spelling");
        string mirror = null;
        string[] mirrorFields = null;
        int[] mirrorOffsets = null;
        bool fresh = !circle && LuminPackJsonCodeGenerator.CanDeserializeDirectlyIntoFreshResult(data);
        // A pooled type declares a mirror even for public fields, which its JSON read uses when it reuses an instance.
        if (mirrored || data.RentPoolMethod != null)
        {
            // The mirror declares every instance field of the type in storage order, so every wire member must be one of
            // those storage fields; a fresh record and ignored storage keep their fields beside the serialized ones. A
            // derived type would need its base storage as well and keeps the original pipeline.
            if (derived || members.Any(member => data.localFields.All(local => local.Identifier != member.Name)))
                return DirectEmission.Reject<DirectTypeModel>("layout mirror");
            using (var hash = System.Security.Cryptography.SHA256.Create())
                mirror = "LuminLayout_" + string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(FormatterTypeName.Get(type)))
                    .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))) + "_" +
                    LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type) + "_" + TypeMetaChecker.BuildLocalMyClassName(data) + parameterList;
            mirrorFields = data.localFields.Select(local => (local.TypeSymbol is IArrayTypeSymbol { Rank: > 1 } multi
                ? MultiDimensionalName(multi) : local.TypeName) +
                (LuminPackCodeGenerator.IsUnmanagedFiledType(local.TypeName) || local.IsValue ? string.Empty : "?") + " " + local.Identifier).ToArray();
            // An explicit-layout mirror reproduces each field's declared offset.
            mirrorOffsets = data.localFields.Any(local => local.filedOffset >= 0)
                ? data.localFields.Select(local => local.filedOffset).ToArray() : null;
        }
        string view = null;
        bool viewElided = false;
        DirectPortableView portableView = null;
        if (portable != null)
        {
            // The plan's fields are the struct's fields in declaration order; LuminPackOrder may reorder the wire members.
            // A positional record's storage fields are its generated backing fields, so only the count is compared.
            if (portable.Fields.Length != members.Length)
                return DirectEmission.Reject<DirectTypeModel>("portable struct");
            using (var hash = System.Security.Cryptography.SHA256.Create())
                view = "LuminLayout_" + string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(FormatterTypeName.Get(type)))
                    .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))) + "_" +
                    LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type) + "_LocalPortable" + LuminPackLocalLayoutGenerator.GetLocalDeclarationName(type);
            var names = new System.Collections.Generic.HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
            viewElided = Enumerable.Range(0, members.Length).All(index => names.Contains("F" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            portableView = CreatePortableView(portable, view);
        }
        string[] arguments = null, constructed = null;
        if (data.SelectedConstructor is { Parameters.Count: > 0 } selected)
        {
            arguments = selected.Parameters.Select(parameter => members.Any(member => member.Name == parameter.MatchingFieldName)
                ? parameter.MatchingFieldName : null).ToArray();
            constructed = selected.Parameters.Select(parameter => parameter.MatchingFieldName).Where(name => name != null).ToArray();
        }
        DirectVersionSchema version = null;
        if (versioned && (version = DirectVersionSchema.TryCreate(data, type, metadata, members, derived, mirrored)) == null)
            return null;
        string helper = GeneratedSourceLayout.JsonHelperName("Json_" + LuminPackExtensionGenerator.SanitizeAssemblyName(globalName));
        // A declared instance member that is not unmanaged storage makes the union read a call boundary.
        bool referencePayload = false;
        for (var level = type; level != null && level.SpecialType != SpecialType.System_Object && !referencePayload; level = level.BaseType)
            foreach (var member in level.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                    continue;
                // A type parameter is written and read by one call, so it never needs the outlined Union read.
                if (member is IFieldSymbol field && field.Type is not ITypeParameterSymbol && !field.Type.IsUnmanagedType ||
                    member is IPropertySymbol { IsIndexer: false } property && property.Type is not ITypeParameterSymbol && !property.Type.IsUnmanagedType)
                {
                    referencePayload = true;
                    break;
                }
            }
        var layoutText = new System.Text.StringBuilder();
        StorageLayoutInfo.Get(type).AppendAttribute(layoutText, string.Empty);
        string layoutAttribute = layoutText.Length == 0 ? null : layoutText.ToString().TrimEnd('\r', '\n');
        // A pooled type rents its instance instead of constructing one, so the selected constructor only shapes its JSON
        // construction when no pool exists; the callbacks run only in the binary roots.
        string rent = data.RentPoolMethod == null ? null : globalName + "." + data.RentPoolMethod.Name + "()";
        if (rent != null)
            arguments = constructed = null;
        var writeBefore = new System.Collections.Generic.List<string>();
        var writeAfter = new System.Collections.Generic.List<string>();
        var readBefore = new System.Collections.Generic.List<string>();
        var readAfter = new System.Collections.Generic.List<string>();
        foreach (var (name, kind, isStatic) in data.callBackMethods)
        {
            string call = isStatic ? globalName + "." + name + "();" : "value?." + name + "();";
            switch (kind)
            {
                case SerializeCallBackType.OnSerializing: writeBefore.Add(call); break;
                case SerializeCallBackType.OnSerialized: writeAfter.Add(call); break;
                case SerializeCallBackType.OnDeserializing: readBefore.Add(call); break;
                default: readAfter.Add(call); break;
            }
        }
        // Unique state-entry names are skipped for a payload containing a type parameter, whose entries stay overloads.
        bool unique = InterceptionSupport.IsEnabled(metadata.ParseOptions) && !data.isGeneric;
        return new DirectTypeModel(globalName, ReadableTypeName.Of(RootContainerProjection.PayloadKey(globalName)), extension, helper,
            fresh, unique, members, proven, mirror, mirrorFields, derived, isStruct, view, viewElided, fixedUnionCase,
            members.All(member => member.IsFixed) && LuminPackExtensionGenerator.SupportsRecordWidth(type), arguments, constructed,
            strings && !derived && !versioned && data.callBackMethods.Count == 0 && bounded == null && !isStruct, metadata.IsNet8,
            metadata.IsNet8 && metadata.CSharpVersion > LanguageVersion.CSharp10, version,
            referencePayload, layoutAttribute, rent, writeBefore.ToArray(), writeAfter.ToArray(), readBefore.ToArray(), readAfter.ToArray(),
            selectedConstructor ? data.localFields.Select(local => local.Identifier).ToArray() : null, bounded,
            circle: circle, circleGuarded: circle && RecursiveTypeAnalysis.IsRecursive(type),
            managedStruct: managedStruct, portableView: portableView, mirrorOffsets: mirrorOffsets,
            typeParameters: typeParameters, typeConstraints: typeConstraints);
    }

    /// <summary>Spells the definition's constraint clauses in the order the original templates append them.</summary>
    /// <param name="data">The analyzed type.</param>
    /// <returns>One <c>where</c> clause per constrained parameter, or empty.</returns>
    private static string[] ConstraintClauses(LuminDataInfo data)
    {
        var clauses = new System.Collections.Generic.List<string>();
        foreach (GenericParameterConstraint constraint in data.GenericConstraints)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (constraint.IsUnmanaged)
                parts.Add("unmanaged");
            if (constraint.IsClass)
                parts.Add("class");
            if (constraint.IsStruct)
                parts.Add("struct");
            if (constraint.IsNotNull)
                parts.Add("notnull");
            parts.AddRange(constraint.Constraints);
            if (constraint.HasNewConstructor)
                parts.Add("new()");
            if (constraint.HasDefault)
                parts.Add("default");
            if (parts.Count != 0)
                clauses.Add("where " + constraint.ParameterName + " : " + string.Join(", ", parts));
        }
        return clauses.ToArray();
    }

    /// <summary>Whether a constructed generic base closes only over the derived definition's own type parameters.</summary>
    /// <param name="level">The constructed base type.</param>
    /// <param name="type">The derived definition.</param>
    /// <returns>Whether every base argument is one of the derived definition's parameters.</returns>
    private static bool OwnParameters(INamedTypeSymbol level, INamedTypeSymbol type)
    {
        if (!level.IsGenericType || level.TypeArguments.Length != level.OriginalDefinition.TypeParameters.Length)
            return false;
        foreach (var argument in level.TypeArguments)
            if (argument is not ITypeParameterSymbol parameter ||
                !type.TypeParameters.Any(own => SymbolEqualityComparer.Default.Equals(own, parameter)))
                return false;
        return true;
    }

    /// <summary>Whether a member type mentions one of the given type parameters, including inside arrays and generic arguments.</summary>
    /// <param name="symbol">The declared member type.</param>
    /// <param name="typeParameters">The definition's own parameter names.</param>
    /// <returns>Whether the type contains one of the parameters.</returns>
    private static bool ContainsParameter(ITypeSymbol symbol, System.Collections.Generic.HashSet<string> typeParameters)
    {
        switch (symbol)
        {
            case ITypeParameterSymbol parameter:
                return typeParameters.Contains(parameter.Name);
            case IArrayTypeSymbol array:
                return ContainsParameter(array.ElementType, typeParameters);
            case INamedTypeSymbol named:
                return named.TypeArguments.Any(argument => ContainsParameter(argument, typeParameters));
            default:
                return false;
        }
    }

    /// <summary>Models a polymorphic Union root: its declared cases, compiled profile slots and dispatch tier, when every case
    /// either owns a generated read entry or is a fixed local class this pipeline can expand.</summary>
    /// <param name="data">The analyzed Union root.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The project generation settings.</param>
    /// <returns>The model, or null when the root keeps the original pipeline.</returns>
    private static DirectTypeModel CreateUnion(LuminDataInfo data, Compilation compilation, MetaInfo metadata)
    {
        var type = data.TypeSymbol;
        if (metadata.RegisterMode == LuminPackRegisterMode.Disabled)
            return DirectEmission.Reject<DirectTypeModel>("union registration");
        if (!metadata.IsNet8 || metadata.CSharpVersion <= LanguageVersion.CSharp10)
            return DirectEmission.Reject<DirectTypeModel>("union project");
        if (type == null || data.isGeneric || type.IsGenericType || type.ContainingType is { IsGenericType: true })
            return DirectEmission.Reject<DirectTypeModel>("union generic");
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Interface) || type.TypeKind == TypeKind.Class && !type.IsAbstract)
            return DirectEmission.Reject<DirectTypeModel>("union declaration: " + type.TypeKind);
        if (!Reachable(type))
            return DirectEmission.Reject<DirectTypeModel>("union accessibility");
        if (!data.CanGenerateUnionDispatch || data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            return DirectEmission.Reject<DirectTypeModel>("union dispatch");
        bool wide = data.IsWideTag || data.UnionMembers.Any(member => member.Id >= 250);
        if (data.callBackMethods.Count != 0 || data.RentPoolMethod != null)
            return DirectEmission.Reject<DirectTypeModel>("union callbacks or pool");
        if (data.UnionMembers.Count == 0)
            return DirectEmission.Reject<DirectTypeModel>("union cases");
        string globalName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (data.classFullName != null && data.classFullName != globalName && "global::" + data.classFullName != globalName)
            return DirectEmission.Reject<DirectTypeModel>("union spelling");
        var declaredBinary = CompiledOptions.Resolve(type, compilation).UnionProfile(false);
        var declaredJson = CompiledOptions.Resolve(type, compilation).UnionProfile(true);
        if (!CanonicalUnionProfile(declaredBinary) || !CanonicalUnionProfile(declaredJson))
            return DirectEmission.Reject<DirectTypeModel>("union declared profile");
        var cursor = FixedUnionCursorPlan.GetRoot(type, compilation, metadata);
        var cases = new DirectUnionCase[data.UnionMembers.Count];
        bool readGeneric = false;
        for (int index = 0; index < cases.Length; index++)
        {
            var member = data.UnionMembers[index];
            var caseType = member.Type;
            if (caseType == null || !member.CanGenerateDispatch || caseType.TypeKind != TypeKind.Class || caseType.IsAbstract ||
                caseType.IsGenericType || caseType.IsUnboundGenericType || !Reachable(caseType))
                return DirectEmission.Reject<DirectTypeModel>("union case: " + (caseType?.Name ?? "none"));
            if (Cases(cases, caseType))
                return DirectEmission.Reject<DirectTypeModel>("union duplicate case: " + caseType.Name);
            bool ownEntry = LuminPackExtensionGenerator.ReadsOwnUnionEntry(caseType, compilation.Assembly);
            DirectTypeModel caseModel;
            _unionCase++;
            try
            {
                caseModel = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(caseType, compilation, metadata), compilation, metadata);
            }
            finally
            {
                _unionCase--;
            }
            bool caseReadGeneric = caseModel == null || NeedsReadProfile(caseModel);
            if (!ownEntry && (caseModel == null || caseModel.UnionRoot || caseModel.IsStruct || caseModel.Mirror != null ||
                caseModel.Version != null || caseModel.ConstructorArguments != null || caseModel.Members.Length == 0 ||
                !caseModel.Members.All(item => item.IsFixed)))
                return DirectEmission.Reject<DirectTypeModel>("union case read: " + caseType.Name);
            readGeneric |= caseReadGeneric;
            string caseName = caseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            bool sealedCase = type.TypeKind == TypeKind.Class && !HasDerivedCase(data, caseType.OriginalDefinition);
            var caseChain = new System.Collections.Generic.List<string>();
            for (var current = caseType; current != null; current = current.ContainingType)
                caseChain.Insert(0, UnionDeclaration(current));
            cases[index] = new DirectUnionCase(member.Id, caseName, ownEntry, caseReadGeneric, sealedCase,
                cursor?.Contains(caseType) == true, caseModel, ReadableTypeName.Of(RootContainerProjection.PayloadKey(caseName)), caseChain.ToArray(),
                caseType.ContainingNamespace == null || caseType.ContainingNamespace.IsGlobalNamespace ? null : caseType.ContainingNamespace.ToDisplayString());
        }
        var tier = UnionDispatchTierPlan.Get(type, compilation);
        var tierCases = tier.Cases.Select(item => item.Type).ToArray();
        var tierSealed = tier.Cases.Select(item => item.Sealed).ToArray();
        var chain = new System.Collections.Generic.List<string>();
        for (var current = type; current != null; current = current.ContainingType)
            chain.Insert(0, UnionDeclaration(current));
        string extension = LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation);
        string helper = GeneratedSourceLayout.JsonHelperName("Json_" + LuminPackExtensionGenerator.SanitizeAssemblyName(globalName));
        return new DirectTypeModel(globalName, ReadableTypeName.Of(RootContainerProjection.PayloadKey(globalName)), extension, helper,
            false, InterceptionSupport.IsEnabled(metadata.ParseOptions), System.Array.Empty<DirectMember>(), false, null, null, false, false,
            null, false, false, false, null, null, false, metadata.IsNet8, metadata.IsNet8 && metadata.CSharpVersion > LanguageVersion.CSharp10,
            null, false, null, unionRoot: true, unionInterface: type.TypeKind == TypeKind.Interface,
            unionSuffix: LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type), unionCases: cases, unionCursor: cursor != null,
            unionReadGeneric: readGeneric, unionChain: chain.ToArray(),
            unionNamespace: type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            unionBinarySlots: UnionSlots(false, declaredBinary), unionJsonSlots: UnionSlots(true, declaredJson),
            unionCanonical: CanonicalUnionProfiles(compilation, false),
            unionStandardBinary: UnionProfileAlias(declaredBinary.Encoding, declaredBinary.Recording),
            unionStandardJson: UnionProfileAlias(declaredBinary.Encoding, declaredBinary.Recording),
            unionTier: (int)tier.Tier, unionTierCases: tierCases, unionTierSealed: tierSealed, unionWide: wide);
    }

    /// <summary>Whether the case list already holds a type.</summary>
    private static bool Cases(DirectUnionCase[] cases, INamedTypeSymbol type)
    {
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        foreach (var item in cases)
            if (item != null && item.Type == name)
                return true;
        return false;
    }

    /// <summary>Whether another declared case derives from or implements the case definition.</summary>
    private static bool HasDerivedCase(LuminDataInfo data, INamedTypeSymbol definition)
    {
        foreach (var member in data.UnionMembers)
        {
            if (SymbolEqualityComparer.Default.Equals(member.Type.OriginalDefinition, definition))
                continue;
            if (UnionRootMatch(member.Type, definition) != null)
                return true;
        }
        return false;
    }

    /// <summary>Finds the base or interface of a case that is the given root definition, as the original slot decisions do.</summary>
    private static INamedTypeSymbol UnionRootMatch(INamedTypeSymbol member, INamedTypeSymbol root)
    {
        if (root.TypeKind == TypeKind.Class)
        {
            for (var current = member.BaseType; current != null; current = current.BaseType)
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, root))
                    return current;
            return null;
        }
        foreach (var current in member.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, root))
                return current;
        return null;
    }

    /// <summary>Whether a case's binary read entry takes the operation's profile.</summary>
    private static bool NeedsReadProfile(DirectTypeModel model) => model.Version != null || model.ProfilePayload || model.InlineArray ||
        model.InlineList || model.Members.Any(member => member.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Object));

    /// <summary>Accepts only the standard wire format with the trusted input policy, whose string options type has an alias.</summary>
    private static bool CanonicalUnionProfile(CompiledOptions profile) => profile.Format == 'G' && profile.Precision == byte.MaxValue &&
        profile.Depth == 1024 && !profile.Strict && !profile.Input.Enabled && profile.Encoding is 0 or 1 && profile.Recording is >= 0 and <= 2;

    /// <summary>The imported string options type of a profile.</summary>
    private static string UnionProfileAlias(int encoding, int recording) => recording switch
    {
        1 => encoding == 0 ? "LuminPackUtf8TokenOptions" : "LuminPackUtf16TokenOptions",
        2 => encoding == 0 ? "LuminPackUtf8VarIntOptions" : "LuminPackUtf16VarIntOptions",
        _ => encoding == 0 ? "LuminPackUtf8LengthOptions" : "LuminPackUtf16LengthOptions",
    };

    /// <summary>Lists the compiled slots of one format in profile order.</summary>
    private static DirectUnionSlot[] UnionSlots(bool json, CompiledOptions declared)
    {
        var slots = new System.Collections.Generic.List<DirectUnionSlot>();
        foreach (var profile in CompiledOptions.UnionProfiles(json))
            slots.Add(new DirectUnionSlot(profile.Identity == declared.Identity ? string.Empty : "_Options_" + profile.Identity,
                UnionProfileAlias(profile.Encoding, profile.Recording), profile.Identity, profile.Encoding, profile.Recording));
        return slots.ToArray();
    }

    /// <summary>Whether the referenced runtime exposes every canonical profile, as the exact slot shortcuts require.</summary>
    private static bool CanonicalUnionProfiles(Compilation compilation, bool json)
    {
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        if (writer == null || SymbolEqualityComparer.Default.Equals(writer.ContainingAssembly, compilation.Assembly) ||
            writer.GetMembers("Cursor").OfType<IFieldSymbol>().Any(field => field.Type is IPointerTypeSymbol) ||
            compilation.SyntaxTrees.Any(tree => tree.Options is CSharpParseOptions parse &&
                parse.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal))))
            return false;
        var names = new System.Collections.Generic.List<string>();
        foreach (var profile in CompiledOptions.UnionProfiles(json))
            names.Add(profile.TypeName.Replace("global::", string.Empty));
        names.Add("LuminPack.Option.LuminPackFixedOptions`1");
        names.Add("LuminPack.Option.LuminPackDefaultOptions");
        names.Add("LuminPack.Option.LuminPackDeclaredOptions");
        return names.All(name => compilation.GetTypeByMetadataName(name) is { } symbol &&
            SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, writer.ContainingAssembly));
    }

    /// <summary>Spells one partial declaration of the root's containing chain.</summary>
    private static string UnionDeclaration(INamedTypeSymbol type)
    {
        string accessibility = type.DeclaredAccessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.Internal => "internal ",
            Accessibility.Private => "private ",
            Accessibility.Protected => "protected ",
            Accessibility.ProtectedAndInternal => "private protected ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            _ => string.Empty,
        };
        string name = SyntaxFacts.GetKeywordKind(type.Name) == SyntaxKind.None ? type.Name : "@" + type.Name;
        return accessibility + "partial " + (type.TypeKind == TypeKind.Interface ? "interface " : "class ") + name;
    }

    /// <summary>Accepts the project settings the direct emitters were verified against.</summary>
    private static bool SupportedProject(Compilation compilation, MetaInfo metadata)
    {
        // Target framework, Unity, unsafe code and language version only select variants of the emitted text (see Net8,
        // Utf8Literals and the proven object write), so every C# project is supported.
        if (compilation is not CSharpCompilation)
            return DirectEmission.Reject<DirectTypeModel>("language") != null;
        return DirectEmission.ProjectFacts(compilation) || DirectEmission.Reject<DirectTypeModel>("assembly attributes") != null;
    }

    /// <summary>Recognizes a tuple or <c>ValueTuple</c> construction, whose components flatten into the parent portable view.</summary>
    private static bool TupleStorage(ITypeSymbol type)
        => type is INamedTypeSymbol named && (named.IsTupleType || named.OriginalDefinition.MetadataName.StartsWith("ValueTuple`", StringComparison.Ordinal) &&
            named.ContainingNamespace.ToDisplayString() == "System");

    /// <summary>Builds the positional view and its scalar transfers from the portable storage plan.</summary>
    /// <param name="plan">The analyzed portable storage.</param>
    /// <param name="view">The view's declaration name.</param>
    /// <returns>The view with every scalar transfer in wire order.</returns>
    private static DirectPortableView CreatePortableView(PortableStructPlan plan, string view)
    {
        var fields = new DirectPortableViewField[plan.Fields.Length];
        for (int index = 0; index < fields.Length; index++)
        {
            var leaves = new System.Collections.Generic.List<DirectPortableLeaf>();
            Flatten(plan.Children[index], "F" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), plan.Fields[index].IsVolatile, leaves);
            fields[index] = new DirectPortableViewField("F" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FormatterTypeName.Get(plan.Fields[index].Type), plan.Fields[index].IsVolatile, plan.Fields[index].Offset, leaves.ToArray());
        }
        return new DirectPortableView(view, fields);

        // A tuple's components are its own storage fields; a scalar ends the descent at its packed width.
        void Flatten(PortableStructPlan node, string access, bool isVolatile, System.Collections.Generic.List<DirectPortableLeaf> leaves)
        {
            if (node.Fields == null)
            {
                leaves.Add(new DirectPortableLeaf(access, FormatterTypeName.Get(node.Type), node.WireSize, isVolatile));
                return;
            }
            if (!TupleStorage(node.Type))
                return;
            for (int index = 0; index < node.Fields.Length; index++)
                Flatten(node.Children[index], access + "." + node.Fields[index].Name, isVolatile || node.Fields[index].IsVolatile, leaves);
        }
    }

    /// <summary>Accepts a public or internal type whose containing types are public or internal too.</summary>
    private static bool Reachable(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return false;
        return true;
    }

    /// <summary>Accepts a plain <c>[LuminPackable]</c> attribute without arguments other than the default generation mode.</summary>
    private static bool PlainPackable(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE && attribute.NamedArguments.Length == 0 &&
            attribute.ConstructorArguments.All(argument => argument.Kind == TypedConstantKind.Enum &&
                System.Convert.ToInt64(argument.Value, System.Globalization.CultureInfo.InvariantCulture) == 0);

    /// <summary>Accepts the packable attribute of the generation mode, an explicit default input policy and a complete-record
    /// byte bound, which only changes the write roots' reservation.</summary>
    private static bool PackableAttributes(AttributeData[] attributes, bool versioned, bool circle, out int? bounded)
    {
        bool packable = false;
        bounded = null;
        foreach (var attribute in attributes)
        {
            if (versioned ? VersionedPackable(attribute) : circle ? CirclePackable(attribute) : PlainPackable(attribute))
            {
                if (packable)
                    return false;
                packable = true;
                continue;
            }
            if (ExplicitTrustedInput(attribute))
                continue;
            if (BoundedAttribute(attribute, out int maxBytes))
            {
                if (bounded != null)
                    return false;
                bounded = maxBytes;
                continue;
            }
            return false;
        }
        return packable;
    }

    /// <summary>Reads a positive <c>[LuminPackBounded(maxBytes)]</c> guarantee, which the write roots reserve once.</summary>
    private static bool BoundedAttribute(AttributeData attribute, out int maxBytes)
    {
        maxBytes = 0;
        if (attribute.AttributeClass?.ToDisplayString() != "LuminPack.Attribute.LuminPackBoundedAttribute" || attribute.NamedArguments.Length != 0 ||
            attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not int value || value <= 0)
            return false;
        maxBytes = value;
        return true;
    }

    /// <summary>Accepts <c>[LuminPackOption(UntrustedData = true)]</c>, which restates the default input policy.</summary>
    private static bool ExplicitTrustedInput(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOptionAttribute" &&
            attribute.ConstructorArguments.Length == 0 && attribute.NamedArguments.Length == 1 &&
            attribute.NamedArguments[0].Key == "UntrustedData" && attribute.NamedArguments[0].Value.Value is true;

    /// <summary>Accepts an attribute of another library, such as another serializer's <c>[Key(0)]</c>, which the analysis never reads:
    /// its class name does not start with LuminPack, and its outermost namespace is not LuminPack, System, Microsoft or Unity.</summary>
    private static bool Foreign(AttributeData attribute)
    {
        var type = attribute.AttributeClass;
        if (type == null || type.TypeKind == TypeKind.Error || type.Name.StartsWith("LuminPack", StringComparison.Ordinal))
            return false;
        var root = type.ContainingNamespace;
        if (root == null || root.IsGlobalNamespace)
            return false;
        while (!root.ContainingNamespace.IsGlobalNamespace)
            root = root.ContainingNamespace;
        return root.Name is not ("LuminPack" or "System" or "Microsoft" or "Unity" or "UnityEngine" or "UnityEditor");
    }

    /// <summary>Accepts <c>[LuminPackable(GeneratorType.VersionTolerant)]</c> without other arguments.</summary>
    private static bool VersionedPackable(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE && attribute.NamedArguments.Length == 0 &&
            attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } kind &&
            kind.GetMembers().OfType<IFieldSymbol>().Any(member => member.Name == "VersionTolerant" && member.HasConstantValue &&
                System.Convert.ToInt64(member.ConstantValue, System.Globalization.CultureInfo.InvariantCulture) ==
                System.Convert.ToInt64(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Accepts <c>[LuminPackable(GeneratorType.CircleReference)]</c> without other arguments.</summary>
    private static bool CirclePackable(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE && attribute.NamedArguments.Length == 0 &&
            attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } kind &&
            kind.GetMembers().OfType<IFieldSymbol>().Any(member => member.Name == "CircleReference" && member.HasConstantValue &&
                System.Convert.ToInt64(member.ConstantValue, System.Globalization.CultureInfo.InvariantCulture) ==
                System.Convert.ToInt64(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Accepts <c>[LuminPackOption(StructLayout = ...)]</c> without other settings.</summary>
    private static bool StructLayoutOption(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOptionAttribute" && attribute.ConstructorArguments.Length == 0 &&
            attribute.NamedArguments.Length == 1 && attribute.NamedArguments[0].Key == "StructLayout";

    /// <summary>Accepts a wire profile declaration, which only shapes the declared Union case's own roots.</summary>
    private static bool OptionAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOptionAttribute";

    /// <summary>Accepts the CLR struct layout attribute, which describes storage that the emitters read from the type itself.</summary>
    private static bool StructLayoutAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.StructLayoutAttribute";

    /// <summary>Accepts one case of a Union base, <c>[LuminPackUnion(tag, typeof(Case))]</c> or <c>[Union(tag, typeof(Case))]</c>.</summary>
    private static bool UnionCase(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() is LuminPackSourceGenerator.LUMIN_UNION_ATTRIBUTE or "System.Runtime.CompilerServices.UnionAttribute" &&
            attribute.NamedArguments.Length == 0 && attribute.ConstructorArguments.Length == 2;

    /// <summary>Accepts a Union root's <c>[LuminPackWideTag]</c> declaration, which only shapes that root's own headers.</summary>
    private static bool WideTagAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackWideTagAttribute";

    /// <summary>Whether a base type declares Union cases, so a wire profile on it only shapes that root's dispatch.</summary>
    private static bool UnionBase(INamedTypeSymbol type)
        => type.IsAbstract && type.GetAttributes().Any(UnionCase);

    /// <summary>Accepts the System.Text.Json polymorphism attributes, which only System.Text.Json reads.</summary>
    private static bool JsonPolymorphism(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() is "System.Text.Json.Serialization.JsonPolymorphicAttribute" or
            "System.Text.Json.Serialization.JsonDerivedTypeAttribute";

    /// <summary>Whether an attribute marks a serialization callback or a pool rent method, which the generated roots run.</summary>
    private static bool SerializationCallback(AttributeData attribute) =>
        attribute.AttributeClass?.ToDisplayString() is "LuminPack.Attribute.LuminPackOnSerializingAttribute"
            or "LuminPack.Attribute.LuminPackOnSerializedAttribute"
            or "LuminPack.Attribute.LuminPackOnDeserializingAttribute"
            or "LuminPack.Attribute.LuminPackOnDeserializedAttribute"
            or "LuminPack.Attribute.LuminPackPoolRentAttribute";

    /// <summary>Accepts a declared member that has no instance storage and is never serialized.</summary>
    private static bool Unserialized(ISymbol member)
    {
        switch (member)
        {
            case IMethodSymbol method:
                return method.MethodKind is MethodKind.Ordinary or MethodKind.StaticConstructor or MethodKind.PropertyGet or MethodKind.PropertySet or
                    MethodKind.UserDefinedOperator or MethodKind.Conversion or MethodKind.Constructor && method.GetReturnTypeAttributes().Length == 0;
            case IPropertySymbol property:
                // An auto-property has a hidden backing field in the instance.
                return property.IsStatic || property.IsIndexer || property.ContainingType.GetMembers().OfType<IFieldSymbol>()
                    .All(field => !SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
            case INamedTypeSymbol:
                return true;
            // An event declaration has no serialized instance storage; an auto-event's backing field is private and unnamed.
            case IEventSymbol:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Reduces one analyzed member to the facts of a covered storage shape.</summary>
    private static DirectMember Member(LuminDataField field, string extension, Compilation compilation, MetaInfo metadata, bool versioned = false,
        bool circle = false, INamedTypeSymbol owner = null, bool mirroredStorage = false, bool volatileStorage = false,
        System.Collections.Generic.HashSet<string> typeParameters = null)
    {
        if (field.IsPrivate && !mirroredStorage || field.IsVolatile && !volatileStorage || field.IsCompress || field.OldNames.Length != 0 && !versioned ||
            field.Options != null && field.Options.IsDeclared || field.TypeSymbol == null || field.Identifier != field.Name ||
            !SyntaxFacts.IsValidIdentifier(field.Name))
            return DirectEmission.Reject<DirectMember>("member options: " + field.Name + " private=" + field.IsPrivate + " volatile=" + field.IsVolatile +
                " compress=" + field.IsCompress + " old=" + field.OldNames.Length + " declaredOptions=" + (field.Options?.IsDeclared ?? false) +
                " symbol=" + (field.TypeSymbol != null) + " identifier=" + (field.Identifier == field.Name) + " valid=" + SyntaxFacts.IsValidIdentifier(field.Name));
        var symbol = field.TypeSymbol;
        // Included protected and private storage is reached through the layout mirror, as auto-property storage is.
        bool mirrored = field.isProperty || field.IsPrivate;
        // A member declared as one of the definition's own type parameters is written and read by the generic serializer.
        if (symbol is ITypeParameterSymbol parameter)
        {
            if (typeParameters == null || !typeParameters.Contains(parameter.Name))
                return DirectEmission.Reject<DirectMember>("member type: foreign parameter " + parameter.Name);
            return new DirectMember(field.Name, DirectMemberKind.Parameter, parameter.Name, parameter.Name, 0, null, field.Type, mirrored,
                valueTypeParameter: parameter.HasValueTypeConstraint, bareParameter: true, referencePayload: parameter.IsReferenceType);
        }
        // A member whose declared type contains one of the definition's own type parameters is written and read by the
        // generic serializer itself, with the containing type as the payload spelling. An ordinary packable object payload
        // peeks at a null object marker before its read, while a collection owns its own null handling.
        if (typeParameters != null && ContainsParameter(symbol, typeParameters))
        {
            string spelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Parameter, spelling, spelling, 0, null, field.Type, mirrored,
                nullObjectGuard: symbol is INamedTypeSymbol { TypeKind: TypeKind.Class } payloadType && EmitterTypeTraits.UsesOrdinaryObjectHeader(payloadType),
                referencePayload: symbol.IsReferenceType);
        }
        if (Scalar(symbol, allowBoolean: true) is { } scalar)
        {
            if (field.FieldType != LuminDataType.Value)
                return DirectEmission.Reject<DirectMember>("fixed member");
            return new DirectMember(field.Name, DirectMemberKind.Fixed, scalar.Keyword, scalar.Keyword, scalar.Size, null, field.Type, mirrored, property: field.isProperty);
        }
        // An enum is stored as its underlying scalar; the binary emitters spell the enum itself, JSON casts through the scalar.
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying } enumType &&
            symbol.NullableAnnotation != NullableAnnotation.Annotated && Scalar(underlying, allowBoolean: false) is { } backing)
        {
            if (field.FieldType != LuminDataType.Value || field.Type != LuminFiledType.Enum)
                return DirectEmission.Reject<DirectMember>("enum member");
            string spelling = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Fixed, spelling, spelling, backing.Size, null, field.Type, mirrored, backing.Keyword,
                property: field.isProperty);
        }
        // A tuple has a generated binary formatter and no null form; its packed components feed a portable view.
        if (symbol is INamedTypeSymbol tupleType && symbol.NullableAnnotation != NullableAnnotation.Annotated && TupleStorage(tupleType) &&
            symbol.SpecialType == SpecialType.None)
        {
            if (field.FieldType != LuminDataType.Value || field.Type is not (LuminFiledType.Struct or LuminFiledType.Other))
                return DirectEmission.Reject<DirectMember>("built-in struct member");
            string tupleSpelling = FormatterTypeName.Get(tupleType);
            return new DirectMember(field.Name, DirectMemberKind.Fixed, tupleSpelling, tupleSpelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, tupleSpelling), field.Type, mirrored, raw: true, builtin: true, property: field.isProperty);
        }
        // A raw unmanaged packable struct of this assembly joins fixed runs at its runtime size.
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } rawStruct && symbol.SpecialType == SpecialType.None &&
            symbol.NullableAnnotation != NullableAnnotation.Annotated && EmitterTypeTraits.CanUseUnmanagedSerialization(rawStruct) &&
            TypeMetaChecker.TryCheckPackableAttribute(rawStruct) &&
            SymbolEqualityComparer.Default.Equals(rawStruct.ContainingAssembly, compilation.Assembly) &&
            LuminPackCodeGenerator.IsMergeableField(field))
        {
            if (field.FieldType != LuminDataType.Value || field.Type != LuminFiledType.Struct)
                return DirectEmission.Reject<DirectMember>("struct member");
            // A containing JSON write copies a small struct write inline; a larger one stays behind its formatter call. A
            // managed struct member keeps its own record operation and is not a fixed raw run here.
            var nested = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(rawStruct, compilation, metadata), compilation, metadata);
            if (nested == null || !nested.IsStruct || nested.ManagedStruct)
                return DirectEmission.Reject<DirectMember>("struct member JSON");
            string spelling = rawStruct.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Fixed, spelling, spelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, spelling), field.Type, mirrored, property: field.isProperty, raw: true,
                nested: nested);
        }
        // A reference-tracked member keeps its own slot table and reference id, so the parent writes its length and calls its
        // operation. A self member reuses this model; any other member is modeled as its own record.
        if (circle && symbol is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } circleType &&
            circleType.SpecialType == SpecialType.None && field.FieldType == LuminDataType.Reference &&
            SymbolEqualityComparer.Default.Equals(circleType.ContainingAssembly, compilation.Assembly) &&
            TypeMetaChecker.TryCheckPackableAttribute(circleType))
        {
            if (TypeMetaChecker.CheckGeneratorType(circleType) != GeneratorType.CircleReference)
                return DirectEmission.Reject<DirectMember>("circle member generator");
            if (_depth > 8)
                return DirectEmission.Reject<DirectMember>("circle member depth");
            DirectTypeModel nested = null;
            if (owner == null || !SymbolEqualityComparer.Default.Equals(circleType, owner))
            {
                nested = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(circleType, compilation, metadata), compilation, metadata);
                if (nested == null || !nested.Circle || nested.IsStruct || nested.Version != null || nested.Bounded != null)
                    return DirectEmission.Reject<DirectMember>("circle member model");
            }
            string circleSpelling = circleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Object, circleSpelling, circleSpelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, circleSpelling), field.Type, mirrored, property: field.isProperty, nested: nested);
        }
        // A packable class of this assembly with analyzed fields is stored inline: the parent record writes its members
        // directly after a marker holding their count. Versioned, mirrored, constructed and pooled children change that
        // record shape. A child with strings writes through its named member operation when every member is a fixed scalar
        // or a string; otherwise its strings write inline with the parent's profile.
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } objectType && symbol.SpecialType == SpecialType.None &&
            SymbolEqualityComparer.Default.Equals(objectType.ContainingAssembly, compilation.Assembly) &&
            TypeMetaChecker.TryCheckPackableAttribute(objectType) && field.Type == LuminFiledType.Class &&
            field.FieldType == LuminDataType.Reference && field.ClassFields.Count != 0 &&
            (field.SelectedConstructor == null || field.SelectedConstructor.Parameters.Count == 0))
        {
            if (_depth > 8)
                return DirectEmission.Reject<DirectMember>("nested object depth");
            var nested = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(objectType, compilation, metadata), compilation, metadata);
            if (nested == null || nested.IsStruct || nested.Members.Any(item => item.Mirrored) || nested.Version != null ||
                nested.ConstructorArguments != null || nested.Members.Length == 0 ||
                nested.Members.Any(item => item.Kind == DirectMemberKind.Dictionary && item.Dictionary.Pair) ||
                nested.Members.Any(item => item.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Text or DirectMemberKind.Object or
                    DirectMemberKind.Array or DirectMemberKind.List or DirectMemberKind.Dictionary)))
                return DirectEmission.Reject<DirectMember>("nested object member");
            string spelling = objectType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Object, spelling, spelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, spelling), field.Type, mirrored, property: field.isProperty, nested: nested);
        }
        // DateTime, DateTimeOffset, TimeSpan and Guid have a runtime native width and a generated formatter for the JSON
        // form.
        string builtinSpelling = symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } &&
            symbol.NullableAnnotation != NullableAnnotation.Annotated ? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : null;
        if (symbol.SpecialType == SpecialType.System_DateTime ||
            builtinSpelling is "global::System.Guid" or "global::System.DateTimeOffset" or "global::System.TimeSpan")
        {
            // The catalog marks a built-in formatter type as "other"; its native width still joins fixed runs.
            if (field.FieldType != LuminDataType.Value || field.Type is not (LuminFiledType.Struct or LuminFiledType.Other))
                return DirectEmission.Reject<DirectMember>("built-in struct member");
            return new DirectMember(field.Name, DirectMemberKind.Fixed, builtinSpelling ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                builtinSpelling ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, builtinSpelling ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                field.Type, mirrored, property: field.isProperty, raw: true, builtin: true);
        }
        if (symbol.SpecialType == SpecialType.System_String)
        {
            if (field.Type != LuminFiledType.String || field.FieldType != LuminDataType.Reference)
                return DirectEmission.Reject<DirectMember>("string member");
            return new DirectMember(field.Name, DirectMemberKind.Text, "string", "string", 0, null, field.Type, mirrored, property: field.isProperty);
        }
        string payload = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // A multi-dimensional array keeps its generated formatter for the write, the read and the JSON form; the declared
        // spelling of the analyzer names the JSON temporary local.
        if (symbol is IArrayTypeSymbol { Rank: > 1 } multi && field.Type is LuminFiledType.Array or LuminFiledType.Other &&
            field.FieldType == LuminDataType.Reference)
            return new DirectMember(field.Name, DirectMemberKind.Array, payload, MultiDimensionalName(multi), 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty, raw: true,
                formatterCall: true);
        if (symbol is IArrayTypeSymbol { Rank: 1 } array && field.Type == LuminFiledType.Array && field.FieldType == LuminDataType.Reference)
        {
            if (Scalar(array.ElementType, allowBoolean: true) is { } element)
                return new DirectMember(field.Name, DirectMemberKind.Array, element.Keyword, element.Keyword + "[]", 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, mirrored, property: field.isProperty);
            // Any other element type writes through its generated array formatter, which also serves JSON and reads.
            return new DirectMember(field.Name, DirectMemberKind.Array, payload, payload, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, mirrored, property: field.isProperty, formatterCall: true);
        }
        if (symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } list &&
            list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" &&
            field.FieldType == LuminDataType.Reference && field.Type is LuminFiledType.List)
        {
            if (Scalar(list.TypeArguments[0], allowBoolean: true) is { } item)
                return new DirectMember(field.Name, DirectMemberKind.List, item.Keyword, "global::System.Collections.Generic.List<" + item.Keyword + ">", 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, mirrored, property: field.isProperty);
            // A list of a raw unmanaged struct keeps the portable storage path: the record captures the list span and the
            // element formatter serves the read.
            if (LuminPackCodeGenerator.ReservedKind(field) == 4)
            {
                string rawElement = list.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return new DirectMember(field.Name, DirectMemberKind.List, rawElement,
                    "global::System.Collections.Generic.List<" + rawElement + ">", 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, mirrored, property: field.isProperty);
            }
            // A list of strings writes its payload through one span call and reads through its element formatter.
            if (list.TypeArguments[0].SpecialType == SpecialType.System_String)
                return new DirectMember(field.Name, DirectMemberKind.List, "string", "global::System.Collections.Generic.List<string>", 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, mirrored, property: field.isProperty);
            // A list of key/value pairs writes each pair's key and value, and reads through its element formatter.
            if (list.TypeArguments[0] is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 2 } pairType &&
                pairType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            {
                var pairKey = Scalar(pairType.TypeArguments[0], allowBoolean: false);
                bool pairValueText = pairType.TypeArguments[1].SpecialType == SpecialType.System_String;
                var pairValue = pairValueText ? null : Scalar(pairType.TypeArguments[1], allowBoolean: false);
                if (pairKey != null && (pairValueText || pairValue != null))
                {
                    string pairSpelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var shape = new DirectCollection(pairKey.Value.Keyword, pairValueText ? "string" : pairValue.Value.Keyword, pairValueText,
                        entryType: pairType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    return new DirectMember(field.Name, DirectMemberKind.PairList, pairSpelling, pairSpelling, 0,
                        "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty, raw: true,
                        property: field.isProperty, collection: shape);
                }
            }
            // A list of lists whose inner elements expand their record body writes both levels in place.
            if (list.TypeArguments[0] is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } innerList &&
                innerList.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" &&
                innerList.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } nestedElement &&
                nestedElement.SpecialType == SpecialType.None &&
                SymbolEqualityComparer.Default.Equals(nestedElement.ContainingAssembly, compilation.Assembly) &&
                TypeMetaChecker.TryCheckPackableAttribute(nestedElement))
            {
                if (_depth > 8)
                    return DirectEmission.Reject<DirectMember>("list element depth");
                var nested = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(nestedElement, compilation, metadata), compilation, metadata);
                if (nested == null || nested.IsStruct || nested.Version != null || nested.ConstructorArguments != null || nested.Mirror != null ||
                    nested.HasCallbacks || nested.Rent != null || nested.MixedRecord || nested.Members.Length == 0 ||
                    nested.Members.Any(item => item.Mirrored || item.Kind == DirectMemberKind.Dictionary && item.Dictionary.Pair ||
                        item.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Text or
                        DirectMemberKind.Object or DirectMemberKind.Array or DirectMemberKind.List or DirectMemberKind.Dictionary)))
                    return DirectEmission.Reject<DirectMember>("list element member");
                string nestedSpelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var shape = new DirectCollection(null, null, false, innerList.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                return new DirectMember(field.Name, DirectMemberKind.NestedList, nestedSpelling, nestedSpelling, 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty,
                    property: field.isProperty, nested: nested, collection: shape);
            }
            // A list of packable classes expands each element's record body in the loop; an unmanaged struct element keeps
            // the portable storage path instead.
            if (list.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } elementType &&
                elementType.SpecialType == SpecialType.None &&
                SymbolEqualityComparer.Default.Equals(elementType.ContainingAssembly, compilation.Assembly) &&
                TypeMetaChecker.TryCheckPackableAttribute(elementType))
            {
                if (_depth > 8)
                    return DirectEmission.Reject<DirectMember>("list element depth");
                var nested = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(elementType, compilation, metadata), compilation, metadata);
                if (nested == null || nested.IsStruct || nested.Version != null || nested.ConstructorArguments != null || nested.Mirror != null ||
                    nested.HasCallbacks || nested.Rent != null || nested.MixedRecord || nested.Members.Length == 0 ||
                    nested.Members.Any(item => item.Mirrored || item.Kind == DirectMemberKind.Dictionary && item.Dictionary.Pair ||
                        item.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Text or
                        DirectMemberKind.Object or DirectMemberKind.Array or DirectMemberKind.List or DirectMemberKind.Dictionary)))
                    return DirectEmission.Reject<DirectMember>("list element member");
                string elementSpelling = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return new DirectMember(field.Name, DirectMemberKind.List, elementSpelling,
                    "global::System.Collections.Generic.List<" + elementSpelling + ">", 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, mirrored, property: field.isProperty, nested: nested);
            }
        }
        // A stack of raw scalars writes its storage span as one batch and reads through its element formatter.
        if (symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } stack &&
            stack.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Stack<T>" &&
            field.FieldType == LuminDataType.Reference && Scalar(stack.TypeArguments[0], allowBoolean: true) is { } stackItem)
        {
            string stackSpelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Stack, stackItem.Keyword, stackSpelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty, raw: true,
                property: field.isProperty);
        }
        // A sorted set of raw scalars writes its tree storage in place and reads through its element formatter.
        if (symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } sortedSet &&
            sortedSet.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.SortedSet<T>" &&
            field.FieldType == LuminDataType.Reference && Scalar(sortedSet.TypeArguments[0], allowBoolean: true) is { } setItem)
        {
            string setSpelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.SortedSet, setItem.Keyword, setSpelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty, raw: true,
                property: field.isProperty);
        }
        // A sorted list of a raw key and a raw or string value writes both stored arrays as paired entries.
        if (symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 2 } sortedList &&
            sortedList.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.SortedList<TKey, TValue>" &&
            field.FieldType == LuminDataType.Reference && Scalar(sortedList.TypeArguments[0], allowBoolean: false) is { } sortedKey)
        {
            bool sortedValueText = sortedList.TypeArguments[1].SpecialType == SpecialType.System_String;
            var sortedValue = sortedValueText ? null : Scalar(sortedList.TypeArguments[1], allowBoolean: false);
            if (sortedValueText || sortedValue != null)
            {
                string sortedSpelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var shape = new DirectCollection(sortedKey.Keyword, sortedValueText ? "string" : sortedValue.Value.Keyword, sortedValueText);
                return new DirectMember(field.Name, DirectMemberKind.SortedList, sortedSpelling, sortedSpelling, 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty, raw: true,
                    property: field.isProperty, collection: shape);
            }
        }
        // A dictionary writes fused entries when a string member bounds its record, or paired records when both key and
        // value are raw scalars. Untrusted Boolean entries and nullable values keep their original operations.
        if (symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 2 } dictionary &&
            dictionary.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>" &&
            field.FieldType == LuminDataType.Reference)
        {
            int reserved = LuminPackCodeGenerator.ReservedKind(field);
            bool keyText = dictionary.TypeArguments[0].SpecialType == SpecialType.System_String;
            bool valueText = dictionary.TypeArguments[1].SpecialType == SpecialType.System_String;
            var keyRaw = keyText ? null : Scalar(dictionary.TypeArguments[0], allowBoolean: false);
            var valueRaw = valueText ? null : Scalar(dictionary.TypeArguments[1], allowBoolean: false);
            if (reserved is 5 or 6 && (keyText || keyRaw != null) && (valueText || valueRaw != null))
            {
                bool pair = reserved == 5;
                string type = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var shape = new DirectDictionary(pair, keyText, valueText, keyRaw?.Keyword, valueRaw?.Keyword, type,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload));
                return new DirectMember(field.Name, DirectMemberKind.Dictionary, type, type, 0, shape.Formatter, field.Type,
                    field.isProperty, raw: true, dictionary: shape);
            }
            // A dictionary whose value is a nullable raw scalar writes sparse entries with a varint key and the fixed value.
            string sparseValue = Scalar(dictionary.TypeArguments[1], allowBoolean: true)?.Keyword;
            var nullableValue = dictionary.TypeArguments[1] as INamedTypeSymbol;
            bool isNullableValue = nullableValue is { IsGenericType: true, TypeArguments.Length: 1 } &&
                nullableValue.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
            var nullableRaw = isNullableValue ? Scalar(nullableValue.TypeArguments[0], allowBoolean: true) : null;
            var sparseKey = Scalar(dictionary.TypeArguments[0], allowBoolean: false);
            if (reserved == 0 && sparseValue == null && nullableRaw is { } innerValue && sparseKey is { } innerKey)
            {
                string sparseSpelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var shape = new DirectCollection(innerKey.Keyword, innerValue.Keyword + "?", false);
                return new DirectMember(field.Name, DirectMemberKind.SparseDictionary, sparseSpelling, sparseSpelling, 0,
                    "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty, raw: true,
                    property: field.isProperty, collection: shape);
            }
        }
        return DirectEmission.Reject<DirectMember>("member type: " + (symbol.NullableAnnotation == NullableAnnotation.Annotated ? "annotated " : string.Empty) +
            (symbol.TypeKind == TypeKind.Enum ? "enum" : symbol is IArrayTypeSymbol ? "array" : symbol is INamedTypeSymbol { IsGenericType: true } generic ?
                generic.OriginalDefinition.Name : symbol.SpecialType != SpecialType.None ? symbol.SpecialType.ToString() :
                TypeMetaChecker.TryCheckPackableAttribute(symbol as INamedTypeSymbol) ? "packable " + symbol.TypeKind : symbol.TypeKind + " " + symbol.Name));
    }

    /// <summary>Spells a multi-dimensional array the way the analyzer's local declarations do: the element name, then the
    /// rank specifiers with the original space before the closing bracket.</summary>
    private static string MultiDimensionalName(IArrayTypeSymbol array) =>
        array.ElementType.ToDisplayString() + "[" + new string(',', array.Rank - 1) + " ]";

    /// <summary>Spells a covered raw scalar.</summary>
    private static (string Keyword, int Size)? Scalar(ITypeSymbol type, bool allowBoolean)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return null;
        return type.SpecialType switch
        {
            SpecialType.System_Boolean when allowBoolean => ("bool", 1),
            SpecialType.System_Byte => ("byte", 1),
            SpecialType.System_SByte => ("sbyte", 1),
            SpecialType.System_Int16 => ("short", 2),
            SpecialType.System_UInt16 => ("ushort", 2),
            SpecialType.System_Char => ("char", 2),
            SpecialType.System_Int32 => ("int", 4),
            SpecialType.System_UInt32 => ("uint", 4),
            SpecialType.System_Single => ("float", 4),
            SpecialType.System_Int64 => ("long", 8),
            SpecialType.System_UInt64 => ("ulong", 8),
            SpecialType.System_Double => ("double", 8),
            // Decimal is a sixteen-byte unmanaged scalar; the writer stores it through its reserved overload.
            SpecialType.System_Decimal => ("decimal", 16),
            _ => null,
        };
    }
}
