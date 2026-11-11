#nullable disable
using System;
using LuminPack.SourceGenerator.Next.Model;
using System.Linq;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>How a member's binary record is stored.</summary>
internal enum RenderMemberKind : byte
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
    /// <summary>A culture-info member, written and read through its shared generated formatter owner.</summary>
    CultureInfo,
    /// <summary>A framework member, written and read through its shared generated formatter owner.</summary>
    Special,
    /// <summary>A collection read and written through a generated owner with static element forwarding.</summary>
    ForwardedCollection,
    /// <summary>A sequence whose formatter owns its inline write and static read.</summary>
    Sequence,
}

/// <summary>The entry shape of a sorted or paired collection member.</summary>
internal sealed class RenderCollection : IEquatable<RenderCollection>
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
    internal RenderCollection(string keyType, string valueType, bool valueText, string itemType = null, string entryType = null)
    {
        EntryType = entryType;
        ItemType = itemType;
        ValueText = valueText;
        ValueType = valueType;
        KeyType = keyType;
    }

    /// <inheritdoc/>
    public bool Equals(RenderCollection other) => other != null && KeyType == other.KeyType && ValueType == other.ValueType &&
        ValueText == other.ValueText && ItemType == other.ItemType && EntryType == other.EntryType;

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as RenderCollection);

    /// <inheritdoc/>
    public override int GetHashCode() => (KeyType ?? string.Empty).GetHashCode();
}

/// <summary>The stored entry shape of a dictionary member and the text its kernel spells.</summary>
internal sealed class RenderDictionary : IEquatable<RenderDictionary>
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
    internal RenderDictionary(bool pair, bool keyText, bool valueText, string keyType, string valueType, string typeName, string formatter)
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
    public bool Equals(RenderDictionary other) => other != null && Pair == other.Pair && KeyText == other.KeyText &&
        ValueText == other.ValueText && KeyType == other.KeyType && ValueType == other.ValueType && TypeName == other.TypeName &&
        Formatter == other.Formatter;

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as RenderDictionary);

    /// <inheritdoc/>
    public override int GetHashCode() => TypeName.GetHashCode();
}

/// <summary>One serialized member of a model-direct type, reduced to the facts its emitted text depends on.</summary>
internal sealed class RenderMember : IEquatable<RenderMember>
{
    /// <summary>The member name, which is also its identifier and JSON name.</summary>
    internal readonly string Name;
    /// <summary>The storage class of the member's binary record.</summary>
    internal readonly RenderMemberKind Kind;
    /// <summary>The scalar keyword of a fixed member, or the element keyword of a collection.</summary>
    internal readonly string Scalar;
    /// <summary>The C# spelling of the declared member type, as the emitters spell temporary locals.</summary>
    internal readonly string TypeName;
    /// <summary>The fixed width of a scalar member, or zero.</summary>
    internal readonly int Size;
    /// <summary>The generated formatter owner of a collection payload, or null.</summary>
    internal readonly string Formatter;
    /// <summary>The emitter's scalar classification.</summary>
    internal readonly ScalarKind FieldType;
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
    internal readonly PayloadRenderModel Nested;
    /// <summary>Whether a collection member writes through its generated formatter call instead of a reserved span, which an
    /// array of unmanaged scalars does.</summary>
    internal readonly bool FormatterCall;
    /// <summary>Whether the member is declared as an auto-property: its mirror storage is assigned rather than passed by
    /// reference, and its existing-instance string read keeps the decoded instance.</summary>
    internal readonly bool Property;
    /// <summary>The entry shape of a dictionary member, or null.</summary>
    internal readonly RenderDictionary Dictionary;
    /// <summary>The entry shape of a sorted, paired or nested collection member, or null.</summary>
    internal readonly RenderCollection Collection;
    /// <summary>Whether a type-parameter member's parameter is constrained to a value type, whose read assigns no reset guard.</summary>
    internal readonly bool ValueTypeParameter;
    /// <summary>Whether the member's declared type is the parameter itself rather than a type that contains it, whose reset guard
    /// tests the runtime type instead of a null object marker.</summary>
    internal readonly bool BareParameter;
    /// <summary>Whether a payload member of a reference type is preceded by a null object marker, whose read resets the member.</summary>
    internal readonly bool NullObjectGuard;
    /// <summary>Whether the member's declared type is a reference type, whose temporary local carries the nullable annotation.</summary>
    internal readonly bool ReferencePayload;
    /// <summary>The imported string options type a member-level declaration owns, or null when the member inherits the
    /// containing record's profile.</summary>
    internal readonly string ScopeProfile;

    /// <summary>The registered sequence implementation, when the member uses a family formatter.</summary>
    internal readonly SequenceFormatter Sequence;

    /// <summary>Captures one member.</summary>
    internal RenderMember(string name, RenderMemberKind kind, string scalar, string typeName, int size, string formatter, ScalarKind fieldType,
        bool mirrored, string underlying = null, bool raw = false, bool builtin = false, PayloadRenderModel nested = null, bool formatterCall = false,
        bool property = false, RenderDictionary dictionary = null, RenderCollection collection = null, bool valueTypeParameter = false,
        bool bareParameter = false, bool nullObjectGuard = false, bool referencePayload = false, string scopeProfile = null, SequenceFormatter sequence = null, bool isVolatile = false)
    {
        Volatile = isVolatile;
        Sequence = sequence;
        ScopeProfile = scopeProfile;
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

    /// <summary>Applies a declaration-owned profile while sharing the member's immutable operation dependencies.</summary>
    /// <param name="profile">The closed stateless profile type.</param>
    /// <returns>The compiled member scope.</returns>
    internal RenderMember WithScope(string profile) => new RenderMember(Name, Kind, Scalar, TypeName, Size, Formatter, FieldType,
        Mirrored, Underlying, Raw, Builtin, Nested, FormatterCall, Property, Dictionary, Collection, ValueTypeParameter,
        BareParameter, NullObjectGuard, ReferencePayload, profile, Sequence, Volatile);

    /// <summary>Whether fixed field access is volatile and excluded from adjacent-field copies.</summary>
    internal readonly bool Volatile;

    /// <summary>Whether the member stores a fixed scalar or raw struct.</summary>
    internal bool IsFixed => Kind == RenderMemberKind.Fixed;

    /// <summary>Whether the member stores a variable-length payload: a string or a separately framed record.</summary>
    internal bool IsPayload => Kind is RenderMemberKind.Text or RenderMemberKind.Object;

    /// <summary>The member's packed width as the original template spells it: a constant, or the runtime size of a raw struct.</summary>
    internal string Width => Raw ? "Unsafe.SizeOf<" + Scalar + ">()" : Size.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Whether the scalar is a Boolean.</summary>
    internal bool IsBoolean => Kind == RenderMemberKind.Fixed && Scalar == "bool";

    /// <summary>Whether the member is a JSON floating-point read site.</summary>
    internal bool IsFloating => Kind == RenderMemberKind.Fixed && Scalar is "float" or "double";

    /// <inheritdoc/>
    public bool Equals(RenderMember other) => other != null && Name == other.Name && Kind == other.Kind && Scalar == other.Scalar &&
        TypeName == other.TypeName && Size == other.Size && Formatter == other.Formatter && FieldType == other.FieldType && Mirrored == other.Mirrored &&
        Underlying == other.Underlying && Raw == other.Raw && Builtin == other.Builtin && FormatterCall == other.FormatterCall &&
        Property == other.Property && Equals(Dictionary, other.Dictionary) && Equals(Collection, other.Collection) &&
        ValueTypeParameter == other.ValueTypeParameter && BareParameter == other.BareParameter &&
        NullObjectGuard == other.NullObjectGuard && ReferencePayload == other.ReferencePayload &&
        Volatile == other.Volatile && ReferenceEquals(Sequence, other.Sequence) && ScopeProfile == other.ScopeProfile && Equals(Nested, other.Nested);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as RenderMember);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}

/// <summary>An immutable, value-equal description of a packable type whose generated roots are written directly from the
/// model instead of through the emitter text and its syntax projections.</summary>
/// <remarks>Only shapes whose complete output the direct emitters reproduce are modeled; every other type returns null from
/// <see cref="RenderModelFactory.Create"/> and keeps the original pipeline. The model holds strings only, so it never
/// retains a compilation.</remarks>
internal sealed class PayloadRenderModel : IEquatable<PayloadRenderModel>
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
    internal readonly RenderMember[] Members;
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
    internal readonly PortableView PortableView;
    /// <summary>Whether a member is written in place instead of entering a reservation: an inline object, a string list or a
    /// list whose elements expand their record body in the loop.</summary>
    private static bool InlineBlock(RenderMember member) => member.Kind == RenderMemberKind.Object && !member.Nested.MemberWrite ||
        member.Kind == RenderMemberKind.List && (member.Scalar == "string" || member.Nested != null);
    /// <summary>Whether a member is a write segment of its own: an inline object, a string list, a formatter-call array or a
    /// collection that writes through the writer in place.</summary>
    private static bool Segment(RenderMember member) => member.Kind == RenderMemberKind.Object || InlineBlock(member) ||
        member.Kind == RenderMemberKind.Array && member.FormatterCall ||
        member.Kind is RenderMemberKind.SortedList or
            RenderMemberKind.PairList or RenderMemberKind.NestedList or RenderMemberKind.SparseDictionary ||
        member.Kind is RenderMemberKind.Parameter or RenderMemberKind.ForwardedCollection or RenderMemberKind.CultureInfo or RenderMemberKind.Special or RenderMemberKind.Sequence;
    /// <summary>Whether every member is a write segment or a fixed scalar, which takes the stream record protocol alone: a
    /// member count, then each member, with no reservation.</summary>
    internal bool StreamRecord => (ManagedStruct || !IsStruct) && Members.Any(Segment) && Members.All(member => member.IsFixed || Segment(member));
    /// <summary>Whether write segments sit beside members that reserve: the record writes its leading members with the header,
    /// then each segment and every following run in place.</summary>
    internal bool MixedRecord => (ManagedStruct || !IsStruct) && Members.Any(Segment) && !StreamRecord;
    /// <summary>Whether a member is copied in place rather than through its named member operation; only a record with such a
    /// member or a formatter-call array camel-cases its snapshot locals.</summary>
    internal bool InlineObject => Members.Any(member => InlineBlock(member) || member.Kind == RenderMemberKind.Array && member.FormatterCall);
    /// <summary>Whether a string anywhere in this record or in an inline object below it needs the profile, which makes the roots
    /// generic.</summary>
    internal bool ProfilePayload => Members.Any(member => member.Kind is RenderMemberKind.Text or RenderMemberKind.Parameter ||
        member.Kind == RenderMemberKind.List && (member.Scalar == "string" || member.Nested != null && member.Nested.ProfilePayload) ||
        member.Kind == RenderMemberKind.Dictionary && (member.Dictionary.KeyText || member.Dictionary.ValueText) ||
        member.Kind is RenderMemberKind.SortedList or RenderMemberKind.PairList && member.Collection.ValueText ||
        member.Kind == RenderMemberKind.NestedList && member.Nested.ProfilePayload ||
        member.Kind == RenderMemberKind.Array && member.FormatterCall ||
        member.Kind is RenderMemberKind.CultureInfo or RenderMemberKind.Special or RenderMemberKind.ForwardedCollection or RenderMemberKind.SparseDictionary ||
        (member.Kind == RenderMemberKind.Sequence && member.Sequence.ProfileElements &&
            (member.Scalar == "string" || member.Nested != null && member.Nested.ProfilePayload)) ||
        member.Kind == RenderMemberKind.Object && member.Nested.ProfilePayload);

    /// <summary>An array inside an inline object calls its generated formatter with the operation's profile, which makes the
    /// parent's roots generic; a top-level array keeps its own non-generic write.</summary>
    internal bool InlineArray => Members.Any(member => member.Kind == RenderMemberKind.Object &&
        (member.Nested.Members.Any(item => item.Kind == RenderMemberKind.Array) || member.Nested.InlineArray));
    /// <summary>A list inside an inline object reads through its generated formatter with the operation's profile, which makes
    /// the parent's read root generic; its write expands inline and needs no profile, as a top-level list does.</summary>
    internal bool InlineList => Members.Any(member => member.Kind == RenderMemberKind.Object &&
        (member.Nested.Members.Any(item => item.Kind == RenderMemberKind.List) || member.Nested.InlineList));
    /// <summary>Whether the write body contains an element loop or expands an inline object that does, which leaves native
    /// inlining to the runtime.</summary>
    internal bool ElementList => Members.Any(member => member.Kind == RenderMemberKind.List && member.Nested != null ||
        member.Kind == RenderMemberKind.Sequence && member.Sequence.PayloadLoops && member.Nested != null ||
        member.Kind is RenderMemberKind.SortedList or
            RenderMemberKind.PairList or RenderMemberKind.NestedList or RenderMemberKind.SparseDictionary ||
        member.Kind == RenderMemberKind.Sequence && member.Sequence.AlwaysLoops ||
        member.Kind == RenderMemberKind.Object && member.Nested.ElementList);
    /// <summary>Whether a Boolean array or list member takes the checked read completion, whose inlined root renames the
    /// instance local.</summary>
    internal bool BooleanCollection => Members.Any(member => member.Kind is RenderMemberKind.Array or RenderMemberKind.List &&
        member.Scalar == "bool");
    /// <summary>Whether a dictionary member takes the cursor write path with fused entries or paired records.</summary>
    internal bool HasDictionary => Members.Any(member => member.Kind == RenderMemberKind.Dictionary ||
        member.Kind == RenderMemberKind.Object && member.Nested.HasDictionary);
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
    internal readonly VersionSchema Version;
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
    /// <summary>Whether a declaration disables hostile-input handling for this type's roots, whose reads then keep the plain
    /// marker, batch and Boolean checks of the trusted template.</summary>
    internal readonly bool Trusted;
    /// <summary>The imported string options type a type-level declaration owns, or null when the roots take the operation's
    /// profile. Its member writes also fix the record's string operations to that profile.</summary>
    internal readonly string DeclaredProfile;
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
    internal readonly UnionRenderCase[] UnionCases;
    /// <summary>Whether a fixed case gives the root the returning cursor protocol.</summary>
    internal readonly bool UnionCursor;
    /// <summary>Whether the binary read root takes the operation's profile, because some case entry does.</summary>
    internal readonly bool UnionReadGeneric;
    /// <summary>The partial declarations from the outermost containing type to the root.</summary>
    internal readonly string[] UnionChain;
    /// <summary>The root's namespace, or null in the global namespace.</summary>
    internal readonly string UnionNamespace;
    /// <summary>The compiled binary profile slots in profile order.</summary>
    internal readonly UnionRenderSlot[] UnionBinarySlots;
    /// <summary>The compiled JSON profile slots in profile order.</summary>
    internal readonly UnionRenderSlot[] UnionJsonSlots;
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

    /// <summary>The inheritance-preserving Local storage levels.</summary>
    internal readonly StorageMirrorModel[] Mirrors;

    /// <summary>The declared circle-reference slots and widths in member order.</summary>
    internal readonly int[] CircleSlots;
    internal readonly int[] CircleWidths;

    /// <summary>The number of circle slots, including declared gaps.</summary>
    internal int CircleSlotCount => CircleSlots.Length == 0 ? 0 : CircleSlots.Max() + 1;

    /// <summary>Whether the compiler supports static constant span collection expressions.</summary>
    internal readonly bool ConstantSpans;

    /// <summary>Whether the pool expression returns destination storage by reference.</summary>
    internal readonly bool RentByRef;

    /// <summary>The joined constraint clauses, without trailing text, or an empty string.</summary>
    internal string TypeConstraintText => string.Join(" ", TypeConstraints);

    /// <summary>Whether any serialization callback runs, which keeps the type out of the named member write.</summary>
    internal bool HasCallbacks => WriteBefore.Length != 0 || WriteAfter.Length != 0 || ReadBefore.Length != 0 || ReadAfter.Length != 0;

    /// <summary>The reason every operation of an explicitly untrusted payload fails closed, or null.</summary>
    internal readonly string Rejection;

    /// <summary>The frozen limits of an untrusted root, or null for a trusted one.</summary>
    internal readonly InputLimits Limits;

    /// <summary>Whether the root reaches itself, so its read counts nesting against a declared depth limit.</summary>
    internal readonly bool Recursive;

    internal PayloadRenderModel(string globalName, string readable, string extension, string jsonHelper, bool freshResult, bool uniqueEntries,
        RenderMember[] members, bool proven, string mirror, string[] mirrorFields, bool derived, bool isStruct, string view, bool viewElided,
        bool fixedUnionCase, bool measureRecord, string[] constructorArguments, string[] constructorMembers, bool memberWrite, bool net8, bool utf8Literals,
        VersionSchema version, bool referencePayload, string layoutAttribute, string rent = null, string[] writeBefore = null,
        string[] writeAfter = null, string[] readBefore = null, string[] readAfter = null, string[] declaredOrder = null, int? bounded = null,
        bool unionRoot = false, bool unionInterface = false, string unionSuffix = null, UnionRenderCase[] unionCases = null,
        bool unionCursor = false, bool unionReadGeneric = false, string[] unionChain = null, string unionNamespace = null,
        UnionRenderSlot[] unionBinarySlots = null, UnionRenderSlot[] unionJsonSlots = null, bool unionCanonical = false,
        string unionStandardBinary = null, string unionStandardJson = null, int unionTier = 0, string[] unionTierCases = null,
        bool[] unionTierSealed = null, bool circle = false, bool circleGuarded = false, bool managedStruct = false,
        PortableView portableView = null, bool unionWide = false, int[] mirrorOffsets = null, string[] typeParameters = null,
        string[] typeConstraints = null, bool trusted = false, string declaredProfile = null, bool rentByRef = false, bool constantSpans = false, int[] circleSlots = null, int[] circleWidths = null, StorageMirrorModel[] mirrors = null,
        string rejection = null, InputLimits limits = null, bool recursive = false)
    {
        Limits = limits;
        Recursive = recursive;
        Rejection = rejection;
        Mirrors = mirrors ?? Array.Empty<StorageMirrorModel>();
        CircleSlots = circleSlots ?? Array.Empty<int>();
        CircleWidths = circleWidths ?? Array.Empty<int>();
        ConstantSpans = constantSpans;
        RentByRef = rentByRef;
        DeclaredProfile = declaredProfile;
        Trusted = trusted;
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
    internal string Owner => GeneratedNames.Owner(Extension, GlobalName);

    /// <inheritdoc/>
    public bool Equals(PayloadRenderModel other) => ReferenceEquals(this, other) || other != null && GlobalName == other.GlobalName &&
        Readable == other.Readable && Extension == other.Extension && JsonHelper == other.JsonHelper && FreshResult == other.FreshResult &&
        UniqueEntries == other.UniqueEntries && Proven == other.Proven && MemberWrite == other.MemberWrite &&
        CamelSnapshots == other.CamelSnapshots && IsStruct == other.IsStruct && ManagedStruct == other.ManagedStruct &&
        Equals(PortableView, other.PortableView) && View == other.View && ViewElided == other.ViewElided && Mirror == other.Mirror &&
        FixedUnionCase == other.FixedUnionCase && MeasureRecord == other.MeasureRecord &&
        Rent == other.Rent && RentByRef == other.RentByRef && ConstantSpans == other.ConstantSpans && Same(WriteBefore, other.WriteBefore) && Same(WriteAfter, other.WriteAfter) &&
        Same(ReadBefore, other.ReadBefore) && Same(ReadAfter, other.ReadAfter) && Trusted == other.Trusted &&
        DeclaredProfile == other.DeclaredProfile &&
        Same(ConstructorArguments, other.ConstructorArguments) && Same(ConstructorMembers, other.ConstructorMembers) &&
        Same(DeclaredOrder, other.DeclaredOrder) && Bounded == other.Bounded &&
        Net8 == other.Net8 && Utf8Literals == other.Utf8Literals && Equals(Version, other.Version) &&
        ReferencePayload == other.ReferencePayload && LayoutAttribute == other.LayoutAttribute &&
        UnionRoot == other.UnionRoot && UnionInterface == other.UnionInterface && UnionSuffix == other.UnionSuffix &&
        UnionCursor == other.UnionCursor && UnionReadGeneric == other.UnionReadGeneric && UnionNamespace == other.UnionNamespace &&
        UnionCanonical == other.UnionCanonical && UnionStandardBinary == other.UnionStandardBinary && UnionStandardJson == other.UnionStandardJson &&
        UnionTier == other.UnionTier && UnionWide == other.UnionWide && Same(UnionChain, other.UnionChain) && Same(UnionTierCases, other.UnionTierCases) &&
        Same(UnionTierSealed, other.UnionTierSealed) && Same(UnionBinarySlots, other.UnionBinarySlots) && Same(UnionJsonSlots, other.UnionJsonSlots) &&
        Same(Mirrors, other.Mirrors) && Circle == other.Circle && CircleGuarded == other.CircleGuarded && Same(CircleSlots, other.CircleSlots) &&
        Same(CircleWidths, other.CircleWidths) &&
        Same(UnionCases, other.UnionCases) && Same(TypeParameters, other.TypeParameters) && Same(TypeConstraints, other.TypeConstraints) &&
        (MirrorFields == null ? other.MirrorFields == null : other.MirrorFields != null && MirrorFields.SequenceEqual(other.MirrorFields)) &&
        Same(MirrorOffsets, other.MirrorOffsets) &&
        Members.SequenceEqual(other.Members);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as PayloadRenderModel);

    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T> => left == null ? right == null : right != null && left.SequenceEqual(right);

    /// <inheritdoc/>
    public override int GetHashCode() => GlobalName.GetHashCode();
}

