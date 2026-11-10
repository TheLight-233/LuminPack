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
        Name == other.Name && Storage == other.Storage && Policy.Equals(other.Policy) && EqualsCore(other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as MemberModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();

    /// <summary>Compares the kind-specific facts.</summary>
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

    /// <summary>Captures a scalar member.</summary>
    internal ScalarMember(string name, MemberStorage storage, WirePolicy policy, string scalar, int size, bool raw, bool builtin)
        : base(name, storage, policy)
    {
        Builtin = builtin;
        Raw = raw;
        Size = size;
        Scalar = scalar;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (ScalarMember)other;
        return Scalar == member.Scalar && Size == member.Size && Raw == member.Raw && Builtin == member.Builtin;
    }
}

/// <summary>A string record.</summary>
internal sealed class TextMember : MemberModel
{
    /// <summary>Captures a string member.</summary>
    internal TextMember(string name, MemberStorage storage, WirePolicy policy)
        : base(name, storage, policy)
    {
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other) => true;
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

    /// <summary>Captures a sequence member.</summary>
    internal CollectionMember(string name, MemberStorage storage, WirePolicy policy, string declaredType, CollectionKind kind, TypeRef element)
        : base(name, storage, policy)
    {
        Element = element;
        Kind = kind;
        DeclaredType = declaredType;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (CollectionMember)other;
        return DeclaredType == member.DeclaredType && Kind == member.Kind && Element.Equals(member.Element);
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

    /// <summary>Captures a dictionary member.</summary>
    internal DictionaryMember(string name, MemberStorage storage, WirePolicy policy, string declaredType, DictionaryKind kind, TypeRef key, TypeRef value)
        : base(name, storage, policy)
    {
        Value = value;
        Key = key;
        Kind = kind;
        DeclaredType = declaredType;
    }

    /// <inheritdoc/>
    protected override bool EqualsCore(MemberModel other)
    {
        var member = (DictionaryMember)other;
        return DeclaredType == member.DeclaredType && Kind == member.Kind && Key.Equals(member.Key) && Value.Equals(member.Value);
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
