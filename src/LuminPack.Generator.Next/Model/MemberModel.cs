using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>How a member's storage is reached.</summary>
internal enum MemberStorage
{
    /// <summary>A public field.</summary>
    Field,

    /// <summary>An auto-property reached through the layout mirror.</summary>
    AutoProperty,

    /// <summary>A non-public field included in the wire format and reached through the layout mirror.</summary>
    IncludedField,
}

/// <summary>One serialized member, normalized independently of the analysis layer.</summary>
internal abstract class MemberModel : IEquatable<MemberModel>
{
    /// <summary>The member name, which is also its JSON property name.</summary>
    internal readonly string Name;

    /// <summary>How the member's storage is reached.</summary>
    internal readonly MemberStorage Storage;

    /// <summary>The member-level wire policy when the declaration overrides the containing profile, otherwise the inherited one.</summary>
    internal readonly WirePolicy Policy;

    /// <summary>Captures the facts shared by every member kind.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>MemberModel</c>.</param>
    /// <param name="policy">The policy supplied to <c>MemberModel</c>.</param>
    protected MemberModel(string name, MemberStorage storage, WirePolicy policy)
    {
        Policy = policy;
        Storage = storage;
        Name = name;
    }

    /// <summary>Whether the member's storage is reached through the layout mirror.</summary>
    internal bool Mirrored => Storage != MemberStorage.Field;

    /// <inheritdoc/>
    public bool Equals(MemberModel? other) => ReferenceEquals(this, other) || other != null && other.GetType() == GetType() &&
        Name == other.Name && Storage == other.Storage && Policy.Equals(other.Policy) && Compressed == other.Compressed &&
        DirectDictionaryField == other.DirectDictionaryField && EqualsCore(other);

    /// <summary>Whether the dictionary uses a direct non-volatile field and its ordinary formatter.</summary>
    internal bool DirectDictionaryField { get; set; }

    /// <summary>Whether a <c>[LuminPackCompress]</c> declaration writes and reads the member through its compressed kernels.</summary>
    internal bool Compressed { get; private set; }

    /// <summary>Copies the member with its compressed binary representation.</summary>
    /// <returns>The compressed member.</returns>
    internal MemberModel WithCompression()
    {
        var copy = (MemberModel)MemberwiseClone();
        copy.Compressed = true;
        return copy;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as MemberModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();

    /// <summary>Compares the kind-specific facts.</summary>
    /// <param name="other">The value to compare with this instance.</param>
    /// <returns>Whether the input satisfies the equals core check.</returns>
    protected abstract bool EqualsCore(MemberModel other);
}

/// <summary>A scalar stored at a fixed width.</summary>
internal sealed class ScalarMember : MemberModel
{
    /// <summary>The scalar spelling, such as <c>int</c> or <c>global::System.Guid</c>.</summary>
    internal readonly string Scalar;

    /// <summary>The packed width, or zero for a runtime-sized built-in struct.</summary>
    internal readonly int Size;

    /// <summary>Whether the width is the runtime size of an unmanaged struct.</summary>
    internal readonly bool Raw;

    /// <summary>Whether the scalar is a framework built-in with a generated JSON formatter.</summary>
    internal readonly bool Builtin;

    /// <summary>The enum's fully qualified spelling when the member is an enum, otherwise null.</summary>
    internal readonly string? EnumType;

    /// <summary>Whether physical field access requires volatile scalar operations.</summary>
    internal readonly bool Volatile;

    /// <summary>The underlying scalar keyword of an enum member, otherwise null.</summary>
    internal readonly string? Underlying;

    /// <summary>Captures a scalar member.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>ScalarMember</c>.</param>
    /// <param name="policy">The policy supplied to <c>ScalarMember</c>.</param>
    /// <param name="scalar">The scalar supplied to <c>ScalarMember</c>.</param>
    /// <param name="size">The requested size.</param>
    /// <param name="raw">Whether to enable raw for this operation.</param>
    /// <param name="builtin">Whether to enable builtin for this operation.</param>
    /// <param name="enumType">The type of the enum.</param>
    /// <param name="underlying">The underlying supplied to <c>ScalarMember</c>.</param>
    /// <param name="isVolatile">Whether to enable is volatile for this operation.</param>
    internal ScalarMember(string name, MemberStorage storage, WirePolicy policy, string scalar, int size, bool raw, bool builtin,
        string? enumType = null, string? underlying = null, bool isVolatile = false)
        : base(name, storage, policy)
    {
        Volatile = isVolatile;
        Builtin = builtin;
        Raw = raw;
        Size = size;
        Scalar = scalar;
        EnumType = enumType;
        Underlying = underlying;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (ScalarMember)other;
        return Scalar == member.Scalar && Size == member.Size && Raw == member.Raw && Builtin == member.Builtin &&
            EnumType == member.EnumType && Underlying == member.Underlying && Volatile == member.Volatile;
    }
}

/// <summary>A string record.</summary>
internal sealed class TextMember : MemberModel
{
    /// <summary>Captures a string member.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>TextMember</c>.</param>
    /// <param name="policy">The policy supplied to <c>TextMember</c>.</param>
    internal TextMember(string name, MemberStorage storage, WirePolicy policy)
        : base(name, storage, policy)
    {
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other) => true;
}

/// <summary>A culture-info member, written and read through its shared generated formatter owner.</summary>
internal sealed class CultureInfoMember : MemberModel
{
    /// <summary>The fully qualified spelling of the declared culture type.</summary>
    internal readonly string Declared;

    /// <summary>Whether the declaration admits null.</summary>
    internal readonly bool Nullable;

    /// <summary>Whether the member is an auto-property whose storage is reached through the layout mirror.</summary>
    internal readonly bool Property;

    /// <summary>Whether the referenced runtime exposes <c>LuminPack.Code.LuminPackCultureInfoCache</c>.</summary>
    internal readonly bool CanReuse;

    /// <summary>Captures a culture-info member.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>CultureInfoMember</c>.</param>
    /// <param name="policy">The policy supplied to <c>CultureInfoMember</c>.</param>
    /// <param name="declared">The declared supplied to <c>CultureInfoMember</c>.</param>
    /// <param name="nullable">Whether the type can hold a null value.</param>
    /// <param name="property">Whether to enable property for this operation.</param>
    /// <param name="canReuse">Whether to enable can reuse for this operation.</param>
    internal CultureInfoMember(string name, MemberStorage storage, WirePolicy policy, string declared, bool nullable, bool property,
        bool canReuse)
        : base(name, storage, policy)
    {
        CanReuse = canReuse;
        Property = property;
        Nullable = nullable;
        Declared = declared;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (CultureInfoMember)other;
        return Declared == member.Declared && Nullable == member.Nullable && Property == member.Property && CanReuse == member.CanReuse;
    }
}

/// <summary>A framework member written and read through its shared generated formatter owner.</summary>
internal sealed class SpecialMember : MemberModel
{
    /// <summary>The fully qualified spelling of the declared member type.</summary>
    internal readonly string Declared;

    /// <summary>Whether the declaration admits null.</summary>
    internal readonly bool Nullable;

    /// <summary>Whether the member is an auto-property whose storage is reached through the layout mirror.</summary>
    internal readonly bool Property;

    /// <summary>The general formatter contract when the member uses recursive static forwarding.</summary>
    internal readonly FormatterModel? Formatter;

    /// <summary>Whether the ordinary binary writer can reserve the complete raw vector or list record.</summary>
    internal readonly bool ReservableSpan;

    /// <summary>Whether the framework formatter's binary operation is a direct unmanaged store.</summary>
    internal readonly bool NativeFrameworkScalar;

    /// <summary>Captures a framework member.</summary>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="storage">The storage used by this operation.</param>
    /// <param name="policy">The policy used by this operation.</param>
    /// <param name="declared">The declared used by this operation.</param>
    /// <param name="nullable">The nullable used by this operation.</param>
    /// <param name="property">The property used by this operation.</param>
    /// <param name="formatter">The formatter used by this operation.</param>
    /// <param name="reservableSpan">Whether a scalar vector or list supports a reserved binary write.</param>
    /// <param name="nativeFrameworkScalar">Whether the positional binary operation may write the scalar's native bytes.</param>
    internal SpecialMember(string name, MemberStorage storage, WirePolicy policy, string declared, bool nullable,
        bool property, FormatterModel? formatter = null, bool reservableSpan = false, bool nativeFrameworkScalar = false)
        : base(name, storage, policy)
    {
        Formatter = formatter;
        ReservableSpan = reservableSpan;
        NativeFrameworkScalar = nativeFrameworkScalar;
        Property = property;
        Nullable = nullable;
        Declared = declared;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (SpecialMember)other;
        return Declared == member.Declared && Nullable == member.Nullable && Property == member.Property &&
            ReservableSpan == member.ReservableSpan && NativeFrameworkScalar == member.NativeFrameworkScalar && Equals(Formatter, member.Formatter);
    }
}

/// <summary>How an inline payload member is framed.</summary>
internal enum PayloadShape
{
    /// <summary>A packable class whose body is expanded in place.</summary>
    Inline,

    /// <summary>A payload written through its named member entry.</summary>
    MemberEntry,

    /// <summary>A reference-tracked node that writes a slot table and reference id.</summary>
    ReferenceTracked,
}

/// <summary>An embedded payload or type-parameter member.</summary>
internal sealed class PayloadMember : MemberModel
{
    /// <summary>The declared payload type spelling.</summary>
    internal readonly TypeRef Type;

    /// <summary>The framing of the member.</summary>
    internal readonly PayloadShape Shape;

    /// <summary>The nested model of an inline payload, or null.</summary>
    internal readonly TypeModel? Nested;

    /// <summary>Captures a payload member.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>PayloadMember</c>.</param>
    /// <param name="policy">The policy supplied to <c>PayloadMember</c>.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <param name="shape">The collection or payload shape to use.</param>
    /// <param name="nested">The nested supplied to <c>PayloadMember</c>.</param>
    internal PayloadMember(string name, MemberStorage storage, WirePolicy policy, TypeRef type, PayloadShape shape, TypeModel? nested)
        : base(name, storage, policy)
    {
        Nested = nested;
        Shape = shape;
        Type = type;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (PayloadMember)other;
        return Type.Equals(member.Type) && Shape == member.Shape && Equals(Nested, member.Nested);
    }
}

/// <summary>The collection families a member can declare.</summary>
internal enum CollectionKind
{
    /// <summary>A one-dimensional array.</summary>
    Array,

    /// <summary>A <c>List&lt;T&gt;</c>.</summary>
    List,

    /// <summary>A <c>Stack&lt;T&gt;</c>.</summary>
    Stack,

    /// <summary>A <c>SortedSet&lt;T&gt;</c>.</summary>
    SortedSet,

    /// <summary>A list of lists.</summary>
    NestedList,

    /// <summary>An immutable collection.</summary>
    Immutable,

    /// <summary>A concurrent collection.</summary>
    Concurrent,

    /// <summary>A <c>BlockingCollection&lt;T&gt;</c>.</summary>
    Blocking,

    /// <summary>A <c>LinkedList&lt;T&gt;</c>.</summary>
    Linked,

    /// <summary>A <c>HashSet&lt;T&gt;</c>.</summary>
    HashSet,

    /// <summary>A <c>Queue&lt;T&gt;</c>.</summary>
    Queue,

    /// <summary>A list-backed object-model wrapper or collection builder.</summary>
    Wrapper,

    /// <summary>A sequence or set interface.</summary>
    Interface,

    /// <summary>A <c>FrozenSet&lt;T&gt;</c>.</summary>
    Frozen,
}

/// <summary>The stored entry shape of a paired, nested or sorted sequence member.</summary>
internal sealed class CollectionShape : IEquatable<CollectionShape?>
{
    /// <summary>The raw key spelling of a paired or sorted entry, or null.</summary>
    internal readonly string? Key;

    /// <summary>The raw value spelling, or <c>string</c> when the value is a string record, or null.</summary>
    internal readonly string? Value;

    /// <summary>Whether the value is a string record.</summary>
    internal readonly bool ValueText;

    /// <summary>The inner list spelling of a nested list, or null.</summary>
    internal readonly string? ItemType;

    /// <summary>The pair entry spelling of a key/value list, or null.</summary>
    internal readonly string? EntryType;

    /// <summary>Captures a collection's entry shape.</summary>
    /// <param name="key">The key identifying the entry.</param>
    /// <param name="value">The value to process.</param>
    /// <param name="valueText">Whether to enable value text for this operation.</param>
    /// <param name="itemType">The type of the item.</param>
    /// <param name="entryType">The type of the entry.</param>
    internal CollectionShape(string? key, string? value, bool valueText, string? itemType = null, string? entryType = null)
    {
        EntryType = entryType;
        ItemType = itemType;
        ValueText = valueText;
        Value = value;
        Key = key;
    }

    /// <inheritdoc/>
    public bool Equals(CollectionShape? other) => ReferenceEquals(this, other) || other != null && Key == other.Key &&
        Value == other.Value && ValueText == other.ValueText && ItemType == other.ItemType && EntryType == other.EntryType;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CollectionShape);

    /// <inheritdoc/>
    public override int GetHashCode() => (Key ?? ItemType ?? EntryType ?? string.Empty).GetHashCode();
}

/// <summary>A sequence member.</summary>
internal sealed class CollectionMember : MemberModel
{
    /// <summary>The declared spelling, such as <c>global::System.Collections.Generic.List&lt;int&gt;</c>.</summary>
    internal readonly string DeclaredType;

    /// <summary>The collection family.</summary>
    internal readonly CollectionKind Kind;

    /// <summary>The element type.</summary>
    internal readonly TypeRef Element;

    /// <summary>The element keyword of a scalar collection, the element spelling of a raw or payload element, or the declared
    /// family spelling of a paired or nested list, or null when no raw storage form covers the declaration.</summary>
    internal readonly string? Scalar;

    /// <summary>Whether the member copies raw storage through the writer in place.</summary>
    internal readonly bool Raw;

    /// <summary>Whether the member writes through its generated formatter instead of a reserved span.</summary>
    internal readonly bool FormatterCall;

    /// <summary>Whether the member is a list of strings.</summary>
    internal readonly bool ElementText;

    /// <summary>The model of the element payload, or null.</summary>
    internal readonly TypeModel? Nested;

    /// <summary>The stored entry shape of a paired, nested or sorted list, or null.</summary>
    internal readonly CollectionShape? Shape;

    /// <summary>The child collection whose generated owner reads and writes each element.</summary>
    internal readonly MemberModel? ForwardedElement;

    /// <summary>The shared scalar owner identity of an enum or framework built-in raw element, or null: the element spelling, then
    /// <c>=</c> and the underlying keyword for an enum.</summary>
    internal readonly string? OwnedScalar;

    /// <summary>The multidimensional array rank, or zero for other sequences.</summary>
    internal readonly int ArrayRank;

    /// <summary>Whether binary reading may reuse the referenced runtime's array slots.</summary>
    internal readonly bool ReuseArraySlots;

    /// <summary>Captures a sequence member.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>CollectionMember</c>.</param>
    /// <param name="policy">The policy supplied to <c>CollectionMember</c>.</param>
    /// <param name="declaredType">The type of the declared.</param>
    /// <param name="kind">The kind supplied to <c>CollectionMember</c>.</param>
    /// <param name="element">The collection element or its formatter contract.</param>
    /// <param name="scalar">The scalar supplied to <c>CollectionMember</c>.</param>
    /// <param name="raw">Whether to enable raw for this operation.</param>
    /// <param name="formatterCall">Whether to enable formatter call for this operation.</param>
    /// <param name="elementText">Whether to enable element text for this operation.</param>
    /// <param name="nested">The nested supplied to <c>CollectionMember</c>.</param>
    /// <param name="shape">The collection or payload shape to use.</param>
    /// <param name="forwardedElement">The forwarded element supplied to <c>CollectionMember</c>.</param>
    /// <param name="ownedScalar">The owned scalar supplied to <c>CollectionMember</c>.</param>
    /// <param name="arrayRank">The array rank supplied to <c>CollectionMember</c>.</param>
    /// <param name="reuseArraySlots">Whether to enable reuse array slots for this operation.</param>
    internal CollectionMember(string name, MemberStorage storage, WirePolicy policy, string declaredType, CollectionKind kind, TypeRef element,
        string? scalar = null, bool raw = false, bool formatterCall = false, bool elementText = false, TypeModel? nested = null,
        CollectionShape? shape = null, MemberModel? forwardedElement = null, string? ownedScalar = null,
        int arrayRank = 0, bool reuseArraySlots = false)
        : base(name, storage, policy)
    {
        ArrayRank = arrayRank;
        ReuseArraySlots = reuseArraySlots;
        OwnedScalar = ownedScalar;
        ForwardedElement = forwardedElement;
        Shape = shape;
        Nested = nested;
        ElementText = elementText;
        FormatterCall = formatterCall;
        Raw = raw;
        Scalar = scalar;
        Element = element;
        Kind = kind;
        DeclaredType = declaredType;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (CollectionMember)other;
        return DeclaredType == member.DeclaredType && Kind == member.Kind && Element.Equals(member.Element) &&
            Scalar == member.Scalar && Raw == member.Raw && FormatterCall == member.FormatterCall &&
            ElementText == member.ElementText && OwnedScalar == member.OwnedScalar && Equals(Nested, member.Nested) && Equals(Shape, member.Shape) &&
            Equals(ForwardedElement, member.ForwardedElement) && ArrayRank == member.ArrayRank &&
            ReuseArraySlots == member.ReuseArraySlots;
    }
}

/// <summary>The dictionary families a member can declare.</summary>
internal enum DictionaryKind
{
    /// <summary>A <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    Dictionary,

    /// <summary>A <c>SortedDictionary&lt;TKey, TValue&gt;</c>.</summary>
    SortedDictionary,

    /// <summary>A <c>SortedList&lt;TKey, TValue&gt;</c>.</summary>
    SortedList,

    /// <summary>A concurrent dictionary.</summary>
    Concurrent,

    /// <summary>An immutable dictionary.</summary>
    Immutable,

    /// <summary>A <c>ReadOnlyDictionary&lt;TKey, TValue&gt;</c>.</summary>
    ReadOnly,

    /// <summary>A <c>FrozenDictionary&lt;TKey, TValue&gt;</c>.</summary>
    Frozen,

    /// <summary>A mutable dictionary interface.</summary>
    Interface,

    /// <summary>A <c>PriorityQueue&lt;TElement, TPriority&gt;</c>.</summary>
    Priority,
}

/// <summary>A keyed member.</summary>
internal sealed class DictionaryMember : MemberModel
{
    /// <summary>The declared spelling.</summary>
    internal readonly string DeclaredType;

    /// <summary>The dictionary family.</summary>
    internal readonly DictionaryKind Kind;

    /// <summary>The key type.</summary>
    internal readonly TypeRef Key;

    /// <summary>The value type.</summary>
    internal readonly TypeRef Value;

    /// <summary>Whether both entries are raw scalars, which stores paired records instead of fused entries.</summary>
    internal readonly bool Pair;

    /// <summary>Whether the key is a string record.</summary>
    internal readonly bool KeyText;

    /// <summary>Whether the value is a string record.</summary>
    internal readonly bool ValueText;

    /// <summary>Whether the value is a nullable raw scalar, whose entries are sparse.</summary>
    internal readonly bool Sparse;

    /// <summary>The recursively modeled collection value, or null for a scalar or payload value.</summary>
    internal readonly MemberModel? ForwardedValue;

    /// <summary>The raw key spelling, or null when the key is a string or unsupported.</summary>
    internal readonly string? KeyType;

    /// <summary>The raw value spelling, or null when the value is a string or unsupported.</summary>
    internal readonly string? ValueType;

    /// <summary>The normalized model of a packable class value, which the dictionary owner expands in place, or null.</summary>
    internal readonly TypeModel? Nested;

    /// <summary>The shared scalar owner identity of an enum or framework built-in key, or null.</summary>
    internal readonly string? KeyOwned;

    /// <summary>The shared scalar owner identity of an enum or framework built-in value, or null.</summary>
    internal readonly string? ValueOwned;

    /// <summary>Captures a dictionary member.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>DictionaryMember</c>.</param>
    /// <param name="policy">The policy supplied to <c>DictionaryMember</c>.</param>
    /// <param name="declaredType">The type of the declared.</param>
    /// <param name="kind">The kind supplied to <c>DictionaryMember</c>.</param>
    /// <param name="key">The key identifying the entry.</param>
    /// <param name="value">The value to process.</param>
    /// <param name="pair">Whether to enable pair for this operation.</param>
    /// <param name="keyText">Whether to enable key text for this operation.</param>
    /// <param name="valueText">Whether to enable value text for this operation.</param>
    /// <param name="sparse">Whether to enable sparse for this operation.</param>
    /// <param name="keyType">The type of the key.</param>
    /// <param name="valueType">The type of the value.</param>
    /// <param name="nested">The nested supplied to <c>DictionaryMember</c>.</param>
    /// <param name="keyOwned">The key owned supplied to <c>DictionaryMember</c>.</param>
    /// <param name="valueOwned">The value owned supplied to <c>DictionaryMember</c>.</param>
    /// <param name="forwardedValue">The forwarded value supplied to <c>DictionaryMember</c>.</param>
    internal DictionaryMember(string name, MemberStorage storage, WirePolicy policy, string declaredType, DictionaryKind kind, TypeRef key,
        TypeRef value, bool pair = false, bool keyText = false, bool valueText = false, bool sparse = false, string? keyType = null,
        string? valueType = null, TypeModel? nested = null, string? keyOwned = null, string? valueOwned = null,
        MemberModel? forwardedValue = null)
        : base(name, storage, policy)
    {
        ForwardedValue = forwardedValue;
        KeyOwned = keyOwned;
        ValueOwned = valueOwned;
        Nested = nested;
        ValueType = valueType;
        KeyType = keyType;
        Sparse = sparse;
        ValueText = valueText;
        KeyText = keyText;
        Pair = pair;
        Value = value;
        Key = key;
        Kind = kind;
        DeclaredType = declaredType;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (DictionaryMember)other;
        return DeclaredType == member.DeclaredType && Kind == member.Kind && Key.Equals(member.Key) && Value.Equals(member.Value) &&
            Pair == member.Pair && KeyText == member.KeyText && ValueText == member.ValueText && Sparse == member.Sparse &&
            KeyType == member.KeyType && ValueType == member.ValueType && Equals(Nested, member.Nested) && KeyOwned == member.KeyOwned &&
            ValueOwned == member.ValueOwned && Equals(ForwardedValue, member.ForwardedValue);
    }
}

/// <summary>A member declared as one of the containing definition's type parameters.</summary>
internal sealed class TypeParameterMember : MemberModel
{
    /// <summary>The parameter name.</summary>
    internal readonly string ParameterName;

    /// <summary>Whether the parameter is constrained to a value type.</summary>
    internal readonly bool ValueType;

    /// <summary>Whether the parameter is constrained to a reference type.</summary>
    internal readonly bool ReferenceType;

    /// <summary>Captures a type-parameter member.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="storage">The storage supplied to <c>TypeParameterMember</c>.</param>
    /// <param name="policy">The policy supplied to <c>TypeParameterMember</c>.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="valueType">Whether to enable value type for this operation.</param>
    /// <param name="referenceType">Whether to enable reference type for this operation.</param>
    internal TypeParameterMember(string name, MemberStorage storage, WirePolicy policy, string parameterName, bool valueType, bool referenceType)
        : base(name, storage, policy)
    {
        ReferenceType = referenceType;
        ValueType = valueType;
        ParameterName = parameterName;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (TypeParameterMember)other;
        return ParameterName == member.ParameterName && ValueType == member.ValueType && ReferenceType == member.ReferenceType;
    }
}

/// <summary>A reference to a declared type, independent of a symbol.</summary>
internal sealed class TypeRef : IEquatable<TypeRef>
{
    /// <summary>The fully qualified spelling.</summary>
    internal readonly string GlobalName;

    /// <summary>The short name used in generated locals.</summary>
    internal readonly string DisplayName;

    /// <summary>Whether the reference admits null.</summary>
    internal readonly bool Nullable;

    /// <summary>Whether the reference is a type parameter of the containing definition.</summary>
    internal readonly bool TypeParameter;

    /// <summary>Captures a type reference.</summary>
    /// <param name="globalName">The name of the global.</param>
    /// <param name="displayName">The name of the display.</param>
    /// <param name="nullable">Whether the type can hold a null value.</param>
    /// <param name="typeParameter">Whether to enable type parameter for this operation.</param>
    internal TypeRef(string globalName, string displayName, bool nullable, bool typeParameter)
    {
        TypeParameter = typeParameter;
        Nullable = nullable;
        DisplayName = displayName;
        GlobalName = globalName;
    }

    /// <inheritdoc/>
    public bool Equals(TypeRef? other) => ReferenceEquals(this, other) || other != null && GlobalName == other.GlobalName &&
        Nullable == other.Nullable && TypeParameter == other.TypeParameter;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeRef);

    /// <inheritdoc/>
    public override int GetHashCode() => GlobalName.GetHashCode();
}
