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
    /// <summary>A packable class of this assembly whose members are written inline in the parent record.</summary>
    Object,
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

    /// <summary>Captures one member.</summary>
    internal DirectMember(string name, DirectMemberKind kind, string scalar, string typeName, int size, string formatter, LuminFiledType fieldType,
        bool mirrored, string underlying = null, bool raw = false, bool builtin = false, DirectTypeModel nested = null)
    {
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

    /// <summary>The member's packed width as the original template spells it: a constant, or the runtime size of a raw struct.</summary>
    internal string Width => Raw ? "Unsafe.SizeOf<" + Scalar + ">()" : Size.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Whether the scalar is a Boolean.</summary>
    internal bool IsBoolean => Kind == DirectMemberKind.Fixed && Scalar == "bool";

    /// <summary>Whether the member is a JSON floating-point read site.</summary>
    internal bool IsFloating => Kind == DirectMemberKind.Fixed && Scalar is "float" or "double";

    /// <inheritdoc/>
    public bool Equals(DirectMember other) => other != null && Name == other.Name && Kind == other.Kind && Scalar == other.Scalar &&
        TypeName == other.TypeName && Size == other.Size && Formatter == other.Formatter && FieldType == other.FieldType && Mirrored == other.Mirrored &&
        Underlying == other.Underlying && Raw == other.Raw && Builtin == other.Builtin && Equals(Nested, other.Nested);

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
    /// <summary>Whether every member is an inline object or a fixed scalar, which takes the stream record protocol alone: a
    /// member count, then each member, with no reservation.</summary>
    internal bool StreamRecord => !IsStruct && Members.Any(member => member.Kind == DirectMemberKind.Object) &&
        Members.All(member => member.Kind is DirectMemberKind.Fixed or DirectMemberKind.Object);
    /// <summary>Whether inline objects sit beside members that reserve: the record writes its leading members with the header,
    /// then each object and every following run in place.</summary>
    internal bool MixedRecord => !IsStruct && Members.Any(member => member.Kind == DirectMemberKind.Object) && !StreamRecord;
    /// <summary>Whether an object member is copied in place rather than through its named member operation; only that mixed
    /// shape camel-cases its snapshot locals.</summary>
    internal bool InlineObject => Members.Any(member => member.Kind == DirectMemberKind.Object && !member.Nested.MemberWrite);
    /// <summary>Whether a string anywhere in this record or in an inline object below it needs the profile, which makes the roots
    /// generic.</summary>
    internal bool ProfilePayload => Members.Any(member => member.Kind == DirectMemberKind.Text ||
        member.Kind == DirectMemberKind.Object && member.Nested.ProfilePayload);

    /// <summary>An array inside an inline object calls its generated formatter with the operation's profile, which makes the
    /// parent's roots generic; a top-level array keeps its own non-generic write.</summary>
    internal bool InlineArray => Members.Any(member => member.Kind == DirectMemberKind.Object &&
        (member.Nested.Members.Any(item => item.Kind == DirectMemberKind.Array) || member.Nested.InlineArray));
    /// <summary>A list inside an inline object reads through its generated formatter with the operation's profile, which makes
    /// the parent's read root generic; its write expands inline and needs no profile, as a top-level list does.</summary>
    internal bool InlineList => Members.Any(member => member.Kind == DirectMemberKind.Object &&
        (member.Nested.Members.Any(item => item.Kind == DirectMemberKind.List) || member.Nested.InlineList));
    /// <summary>The positional storage view of a portable struct, whose fields are copied one by one at packed wire offsets, or null.</summary>
    internal readonly string View;
    /// <summary>Whether the portable view is elided: every positional view name is also a field name of the struct, so the
    /// original projection names the struct's fields directly (by name, not by position).</summary>
    internal readonly bool ViewElided;
    /// <summary>The root-private layout mirror that auto-property storage is reached through, or null for public fields.</summary>
    internal readonly string Mirror;
    /// <summary>The mirror's field declarations, such as <c>string? Name</c>, in storage order.</summary>
    internal readonly string[] MirrorFields;
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
    /// <summary>The serialization callbacks, spelled as statements of the write or read root, in declaration order.</summary>
    internal readonly string[] WriteBefore = System.Array.Empty<string>(), WriteAfter = System.Array.Empty<string>(),
        ReadBefore = System.Array.Empty<string>(), ReadAfter = System.Array.Empty<string>();

    /// <summary>Whether any serialization callback runs, which keeps the type out of the named member write.</summary>
    internal bool HasCallbacks => WriteBefore.Length != 0 || WriteAfter.Length != 0 || ReadBefore.Length != 0 || ReadAfter.Length != 0;

    private DirectTypeModel(string globalName, string readable, string extension, string jsonHelper, bool freshResult, bool uniqueEntries,
        DirectMember[] members, bool proven, string mirror, string[] mirrorFields, bool derived, bool isStruct, string view, bool viewElided,
        bool fixedUnionCase, bool measureRecord, string[] constructorArguments, string[] constructorMembers, bool memberWrite, bool net8, bool utf8Literals,
        DirectVersionSchema version, bool referencePayload, string layoutAttribute, string rent = null, string[] writeBefore = null,
        string[] writeAfter = null, string[] readBefore = null, string[] readAfter = null)
    {
        Version = version;
        ReferencePayload = referencePayload;
        LayoutAttribute = layoutAttribute;
        Rent = rent;
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
        CamelSnapshots = derived || members.Any(member => member.Mirrored);
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
        CamelSnapshots == other.CamelSnapshots && IsStruct == other.IsStruct && View == other.View && ViewElided == other.ViewElided && Mirror == other.Mirror &&
        FixedUnionCase == other.FixedUnionCase && MeasureRecord == other.MeasureRecord &&
        Rent == other.Rent && Same(WriteBefore, other.WriteBefore) && Same(WriteAfter, other.WriteAfter) &&
        Same(ReadBefore, other.ReadBefore) && Same(ReadAfter, other.ReadAfter) &&
        Same(ConstructorArguments, other.ConstructorArguments) && Same(ConstructorMembers, other.ConstructorMembers) &&
        Net8 == other.Net8 && Utf8Literals == other.Utf8Literals && Equals(Version, other.Version) &&
        ReferencePayload == other.ReferencePayload && LayoutAttribute == other.LayoutAttribute &&
        (MirrorFields == null ? other.MirrorFields == null : other.MirrorFields != null && MirrorFields.SequenceEqual(other.MirrorFields)) &&
        Members.SequenceEqual(other.Members);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectTypeModel);

    private static bool Same(string[] left, string[] right) => left == null ? right == null : right != null && left.SequenceEqual(right);

    /// <inheritdoc/>
    public override int GetHashCode() => GlobalName.GetHashCode();

    /// <summary>How many models the current thread is building, which stops a cycle of inline objects from recursing forever.</summary>
    [ThreadStatic]
    private static int _depth;

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
        var type = data.TypeSymbol;
        // A struct is covered when its storage is its wire layout (a raw unmanaged struct), or when a portable plan copies its
        // scalar fields one by one at packed offsets.
        bool raw = data.isValueType && type != null && type.TypeKind == TypeKind.Struct && EmitterTypeTraits.CanUseUnmanagedSerialization(type);
        var portable = data.isValueType && !raw && type != null && type.TypeKind == TypeKind.Struct && !PortableStructPlan.RequiresClosedDispatch(type)
            ? PortableStructPlan.TryCreate(type) : null;
        if (portable != null && (portable.Fields == null || portable.Children == null || portable.Children.Any(child => child.Fields != null) ||
            portable.Fields.Any(field => field.IsVolatile) || type is INamedTypeSymbol { IsTupleType: true }))
            portable = null;
        bool isStruct = raw || portable != null;
        bool versioned = data.generatorType == GeneratorType.VersionTolerant;
        if (type == null || data.generatorType != GeneratorType.Object && !versioned || data.isUnion || data.isGeneric || data.isValueType && !isStruct)
            return DirectEmission.Reject<DirectTypeModel>("type kind: " + (type == null ? "none" : data.isUnion ? "union" : data.isGeneric ? "generic" :
                data.isValueType ? "struct" : data.generatorType.ToString()));
        // Callbacks and pool rents shape the binary roots; the version-tolerant roots keep their own body and callbacks there
        // are not modeled yet.
        if (data.enableBurst || versioned && (data.RentPoolMethod != null || data.callBackMethods.Count != 0))
            return DirectEmission.Reject<DirectTypeModel>("callbacks, pool or burst");
        if (data.Diagnostics.Length != 0)
            return DirectEmission.Reject<DirectTypeModel>("diagnostics");
        if (data.fields.Count > 32)
            return DirectEmission.Reject<DirectTypeModel>("member count: " + data.fields.Count);
        if (type.TypeKind != (isStruct ? TypeKind.Struct : TypeKind.Class) || type.IsAbstract || type.IsStatic || type.IsGenericType ||
            type.ContainingType is { IsGenericType: true } || !Reachable(type))
            return DirectEmission.Reject<DirectTypeModel>("type declaration: " + (type.TypeKind != TypeKind.Class ? "kind" : type.IsAbstract ? "abstract" :
                type.IsStatic ? "static" : type.IsGenericType ? "generic" : type.ContainingType != null && type.ContainingType.IsGenericType ? "nested in generic" :
                "accessibility"));
        // Base types contribute their fields after the type's own, and must be plain or abstract classes of the same assembly. A Union
        // base's case list only shapes the base's own dispatch file, never the files of its cases.
        var levels = new System.Collections.Generic.List<INamedTypeSymbol> { type };
        for (var level = type.BaseType; level != null && level.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); level = level.BaseType)
        {
            if (level.TypeKind != TypeKind.Class || level.IsStatic || level.IsRecord || level.IsGenericType ||
                level.ContainingType is { IsGenericType: true } || !Reachable(level) ||
                !SymbolEqualityComparer.Default.Equals(level.ContainingAssembly, compilation.Assembly) ||
                level.GetAttributes().Any(attribute => !PlainPackable(attribute) && !Foreign(attribute) && !UnionCase(attribute) && !JsonPolymorphism(attribute) &&
                    !StructLayoutAttribute(attribute)))
                return DirectEmission.Reject<DirectTypeModel>("base type: " + level.ToDisplayString());
            levels.Add(level);
        }
        bool derived = levels.Count > 1;
        // A selected constructor with parameters builds the instance from temporary locals on read; otherwise the type needs
        // one public parameterless constructor, as the generated roots create it with new T(). A record also declares its
        // protected copy constructor, which never runs here.
        bool selectedConstructor = data.SelectedConstructor is { Parameters.Count: > 0 };
        var publicConstructors = type.InstanceConstructors.Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public).ToArray();
        if (!raw && !selectedConstructor && (publicConstructors.Length != 1 || publicConstructors[0].Parameters.Length != 0))
            return DirectEmission.Reject<DirectTypeModel>("constructor: " + type.InstanceConstructors.Length + " " +
                string.Join("+", type.InstanceConstructors.Select(item => item.Parameters.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    item.DeclaredAccessibility.ToString())));
        // The constructed read stores each member in a temporary local, so it is limited after the members are reduced.
        var constructedMembers = !raw && selectedConstructor;
        var attributes = type.GetAttributes().Where(attribute => !Foreign(attribute) && !StructLayoutOption(attribute) && !StructLayoutAttribute(attribute)).ToArray();
        if (attributes.Length != 1 || !(versioned ? VersionedPackable(attributes[0]) : PlainPackable(attributes[0])))
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
                    !(member is IFieldSymbol or IPropertySymbol && attribute.AttributeClass?.ToDisplayString() is "LuminPack.Attribute.LuminPackOrderAttribute" or "LuminPack.Attribute.LuminPackIgnoreAttribute") &&
                    !(versioned && member is IFieldSymbol && attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOldNameAttribute")))
                return DirectEmission.Reject<DirectTypeModel>("member attributes: " + member.Name + " [" + string.Join("+",
                    member.GetAttributes().Select(attribute => attribute.AttributeClass?.Name)) + "]");
            // An ignored member keeps its storage but is never serialized.
            if (member is IFieldSymbol or IPropertySymbol && member.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackIgnoreAttribute"))
            {
                if (member is IPropertySymbol || member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
                    return DirectEmission.Reject<DirectTypeModel>("ignored member");
                continue;
            }
            // An auto-property is stored in its backing field, which the generated code reaches through the layout mirror.
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } auto &&
                auto.ContainingType.GetMembers().OfType<IFieldSymbol>().Any(backing => SymbolEqualityComparer.Default.Equals(backing.AssociatedSymbol, auto)))
            {
                // The setter may be init-only: every auto-property gets a layout mirror, and the binary and JSON paths read and
                // write it through that mirror, never through the setter.
                if (auto.GetMethod is not { DeclaredAccessibility: Accessibility.Public } || auto.SetMethod is not { DeclaredAccessibility: Accessibility.Public } ||
                    auto.GetMethod.GetAttributes().Length != 0 || auto.SetMethod.GetAttributes().Length != 0)
                    return DirectEmission.Reject<DirectTypeModel>("declared property");
                fieldsDeclared++;
                continue;
            }
            if (member is IFieldSymbol field)
            {
                if (field.IsStatic || field.IsConst)
                    continue;
                if (field.IsReadOnly || field.IsVolatile || field.DeclaredAccessibility != Accessibility.Public)
                    return DirectEmission.Reject<DirectTypeModel>("declared field: " + field.Name + " " + field.DeclaredAccessibility +
                        (field.IsReadOnly ? " readonly" : string.Empty) + (field.IsVolatile ? " volatile" : string.Empty));
                fieldsDeclared++;
                continue;
            }
            if (!Unserialized(member))
                return DirectEmission.Reject<DirectTypeModel>("declared member: " + member.Kind + " " + member.Name);
        }
        if (fieldsDeclared != data.fields.Count)
            return DirectEmission.Reject<DirectTypeModel>("declared members: " + fieldsDeclared + "/" + data.fields.Count);

        string extension = LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation);
        var members = new DirectMember[data.fields.Count];
        for (int index = 0; index < members.Length; index++)
            if ((members[index] = Member(data.fields[index], extension, compilation, metadata, versioned)) == null)
                return DirectEmission.Reject<DirectTypeModel>("member shape");
        // The constructed read stores each member in a temporary local, which only fixed scalars and strings spell today.
        if (constructedMembers && members.Any(member => member.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Text)))
            return DirectEmission.Reject<DirectTypeModel>("constructor members");
        // A local case of an abstract packable base whose payload is raw fixed storage writes through the fixed Union cursor protocol.
        bool fixedUnionCase = !isStruct && members.All(member => member.IsFixed) && FixedUnionCursorPlan.CanUsePayload(type, compilation, metadata);
        // Raw struct members are covered in fixed Union case records of at least two fields without Booleans, whose batch masks need
        // constant offsets.
        if (members.Any(member => member.Raw && !member.Builtin) && (!fixedUnionCase || members.Length < 2 || members.Any(member => member.IsBoolean || member.Mirrored)))
            return DirectEmission.Reject<DirectTypeModel>("struct member record");
        // Strings without collections take the named member write and the proven object write; both need the runtime's own profiles.
        bool mirrored = data.fields.Any(field => field.isProperty);
        if (isStruct && (mirrored || members.Any(member => !member.IsFixed || member.Raw)))
            return DirectEmission.Reject<DirectTypeModel>("struct members");
        bool strings = !mirrored && members.Any(member => member.Kind == DirectMemberKind.Text) &&
            members.All(member => member.Kind is DirectMemberKind.Fixed or DirectMemberKind.Text);
        // The proven object write needs .NET 8, unsafe code and a non-Unity project, as ProvenObjectWriteProjection checks.
        bool unity = metadata.IsForUnity || metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal));
        bool proven = strings && !versioned && metadata.IsNet8 && metadata.AllowUnsafe && !unity &&
            data.callBackMethods.Count == 0;
        if (proven && !ProvenWriteProfiles.AreCanonical(compilation))
            return DirectEmission.Reject<DirectTypeModel>("proven object write");
        // JSON read locals are named after the camel-cased member; the presentation renames a leading underscore or a clash.
        if (members.Any(member => member.Name[0] == '_') ||
            members.Select(member => char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1)).Distinct(StringComparer.Ordinal).Count() != members.Length)
            return DirectEmission.Reject<DirectTypeModel>("member names");
        // Inline object members keep the stream record protocol: a marker holding the member count, then each member in
        // order. Beside reserving members the record writes a reserved prefix with the header, then each object and every
        // following run in place.
        bool objects = members.Any(member => member.Kind == DirectMemberKind.Object);
        if (objects && (mirrored || versioned || derived || isStruct))
            return DirectEmission.Reject<DirectTypeModel>("nested object record");

        string globalName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (data.classFullName != null && data.classFullName != globalName && "global::" + data.classFullName != globalName)
            return DirectEmission.Reject<DirectTypeModel>("type spelling");
        string mirror = null;
        string[] mirrorFields = null;
        bool fresh = LuminPackJsonCodeGenerator.CanDeserializeDirectlyIntoFreshResult(data);
        // A pooled type declares a mirror even for public fields, which its JSON read uses when it reuses an instance.
        if (mirrored || data.RentPoolMethod != null)
        {
            // The mirror declares every instance field of the type in storage order, so it must hold exactly the members.
            if (fresh || derived || data.localFields.Count != members.Length ||
                data.localFields.Where((local, index) => local.Identifier != data.fields[index].Identifier).Any())
                return DirectEmission.Reject<DirectTypeModel>("layout mirror");
            using (var hash = System.Security.Cryptography.SHA256.Create())
                mirror = "LuminLayout_" + string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(FormatterTypeName.Get(type)))
                    .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))) + "_" +
                    LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type) + "_" + TypeMetaChecker.BuildLocalMyClassName(data);
            mirrorFields = data.localFields.Select(local => local.TypeName +
                (LuminPackCodeGenerator.IsUnmanagedFiledType(local.TypeName) || local.IsValue ? string.Empty : "?") + " " + local.Identifier).ToArray();
        }
        string view = null;
        bool viewElided = false;
        if (portable != null)
        {
            // The plan's fields are the struct's fields in declaration order, as are the members.
            if (portable.Fields.Length != members.Length || portable.Fields.Where((field, index) => field.Name != members[index].Name).Any())
                return DirectEmission.Reject<DirectTypeModel>("portable struct");
            using (var hash = System.Security.Cryptography.SHA256.Create())
                view = "LuminLayout_" + string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(FormatterTypeName.Get(type)))
                    .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))) + "_" +
                    LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type) + "_LocalPortable" + LuminPackLocalLayoutGenerator.GetLocalDeclarationName(type);
            var names = new System.Collections.Generic.HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
            viewElided = Enumerable.Range(0, members.Length).All(index => names.Contains("F" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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
                if (member is IFieldSymbol field && !field.Type.IsUnmanagedType || member is IPropertySymbol { IsIndexer: false } property && !property.Type.IsUnmanagedType)
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
        return new DirectTypeModel(globalName, ReadableTypeName.Of(RootContainerProjection.PayloadKey(globalName)), extension, helper,
            fresh, InterceptionSupport.IsEnabled(metadata.ParseOptions), members, proven, mirror, mirrorFields, derived, isStruct, view, viewElided, fixedUnionCase,
            members.All(member => member.IsFixed) && LuminPackExtensionGenerator.SupportsRecordWidth(type), arguments, constructed,
            strings && !derived && !versioned && data.callBackMethods.Count == 0, metadata.IsNet8,
            metadata.IsNet8 && metadata.CSharpVersion > LanguageVersion.CSharp10, version,
            referencePayload, layoutAttribute, rent, writeBefore.ToArray(), writeAfter.ToArray(), readBefore.ToArray(), readAfter.ToArray());
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

    /// <summary>Accepts <c>[LuminPackOption(StructLayout = ...)]</c> without other settings.</summary>
    private static bool StructLayoutOption(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOptionAttribute" && attribute.ConstructorArguments.Length == 0 &&
            attribute.NamedArguments.Length == 1 && attribute.NamedArguments[0].Key == "StructLayout";

    /// <summary>Accepts the CLR struct layout attribute, which describes storage that the emitters read from the type itself.</summary>
    private static bool StructLayoutAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.StructLayoutAttribute";

    /// <summary>Accepts one case of a Union base, <c>[LuminPackUnion(tag, typeof(Case))]</c> or <c>[Union(tag, typeof(Case))]</c>.</summary>
    private static bool UnionCase(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() is LuminPackSourceGenerator.LUMIN_UNION_ATTRIBUTE or "System.Runtime.CompilerServices.UnionAttribute" &&
            attribute.NamedArguments.Length == 0 && attribute.ConstructorArguments.Length == 2;

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
            default:
                return false;
        }
    }

    /// <summary>Reduces one analyzed member to the facts of a covered storage shape.</summary>
    private static DirectMember Member(LuminDataField field, string extension, Compilation compilation, MetaInfo metadata, bool versioned = false)
    {
        if (field.IsPrivate || field.IsVolatile || field.IsCompress || field.OldNames.Length != 0 && !versioned ||
            field.Options != null && field.Options.IsDeclared || field.TypeSymbol == null || field.Identifier != field.Name ||
            !SyntaxFacts.IsValidIdentifier(field.Name) || field.Name.Any(c => c > 127))
            return DirectEmission.Reject<DirectMember>("member options: " + field.Name + " private=" + field.IsPrivate + " volatile=" + field.IsVolatile +
                " compress=" + field.IsCompress + " old=" + field.OldNames.Length + " declaredOptions=" + (field.Options?.IsDeclared ?? false) +
                " symbol=" + (field.TypeSymbol != null) + " identifier=" + (field.Identifier == field.Name) + " valid=" + SyntaxFacts.IsValidIdentifier(field.Name));
        var symbol = field.TypeSymbol;
        if (Scalar(symbol, allowBoolean: true) is { } scalar)
        {
            if (field.FieldType != LuminDataType.Value)
                return DirectEmission.Reject<DirectMember>("fixed member");
            return new DirectMember(field.Name, DirectMemberKind.Fixed, scalar.Keyword, scalar.Keyword, scalar.Size, null, field.Type, field.isProperty);
        }
        // An enum is stored as its underlying scalar; the binary emitters spell the enum itself, JSON casts through the scalar.
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying } enumType &&
            symbol.NullableAnnotation != NullableAnnotation.Annotated && Scalar(underlying, allowBoolean: false) is { } backing)
        {
            if (field.FieldType != LuminDataType.Value || field.Type != LuminFiledType.Enum)
                return DirectEmission.Reject<DirectMember>("enum member");
            string spelling = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Fixed, spelling, spelling, backing.Size, null, field.Type, field.isProperty, backing.Keyword);
        }
        // A raw unmanaged packable struct of this assembly joins fixed runs at its runtime size.
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } rawStruct && symbol.SpecialType == SpecialType.None &&
            symbol.NullableAnnotation != NullableAnnotation.Annotated && EmitterTypeTraits.CanUseUnmanagedSerialization(rawStruct) &&
            TypeMetaChecker.TryCheckPackableAttribute(rawStruct) && rawStruct.ContainingType == null &&
            SymbolEqualityComparer.Default.Equals(rawStruct.ContainingAssembly, compilation.Assembly) &&
            LuminPackCodeGenerator.IsMergeableField(field))
        {
            if (field.FieldType != LuminDataType.Value || field.Type != LuminFiledType.Struct)
                return DirectEmission.Reject<DirectMember>("struct member");
            // A containing JSON write copies a small struct write inline; only a struct written behind its formatter call is covered.
            // The struct's own model gives that count.
            var nested = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(rawStruct, compilation, metadata), compilation, metadata);
            if (nested == null || !nested.IsStruct || nested.View != null || DirectJsonEmitter.WriteCalls(nested) <= 16)
                return DirectEmission.Reject<DirectMember>("struct member JSON");
            string spelling = rawStruct.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Fixed, spelling, spelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, spelling), field.Type, field.isProperty, raw: true);
        }
        // A packable class of this assembly with analyzed fields is stored inline: the parent record writes its members
        // directly after a marker holding their count. Versioned, mirrored, constructed and pooled children change that
        // record shape. A child with strings writes through its named member operation when every member is a fixed scalar
        // or a string; otherwise its strings write inline with the parent's profile.
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } objectType && symbol.SpecialType == SpecialType.None &&
            objectType.ContainingType == null &&
            SymbolEqualityComparer.Default.Equals(objectType.ContainingAssembly, compilation.Assembly) &&
            TypeMetaChecker.TryCheckPackableAttribute(objectType) && field.Type == LuminFiledType.Class &&
            field.FieldType == LuminDataType.Reference && field.ClassFields.Count != 0 && field.RentPoolMethod == null &&
            (field.SelectedConstructor == null || field.SelectedConstructor.Parameters.Count == 0))
        {
            if (_depth > 8)
                return DirectEmission.Reject<DirectMember>("nested object depth");
            var nested = TryCreate(LuminPackSourceGenerator.GetSerializationMetadata(objectType, compilation, metadata), compilation, metadata);
            if (nested == null || nested.IsStruct || nested.Mirror != null || nested.Version != null ||
                nested.ConstructorArguments != null || nested.Members.Length == 0 ||
                nested.Members.Any(item => item.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Text or DirectMemberKind.Object or
                    DirectMemberKind.Array or DirectMemberKind.List)))
                return DirectEmission.Reject<DirectMember>("nested object member");
            string spelling = objectType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Object, spelling, spelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, spelling), field.Type, field.isProperty, nested: nested);
        }
        // DateTime and Guid have a fixed native width and a generated formatter for the JSON form.
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } &&
            symbol.NullableAnnotation != NullableAnnotation.Annotated &&
            (symbol.SpecialType == SpecialType.System_DateTime || symbol.ToDisplayString() == "global::System.Guid"))
        {
            // The catalog marks a built-in formatter type as "other"; its native width still joins fixed runs.
            if (field.FieldType != LuminDataType.Value || field.Type is not (LuminFiledType.Struct or LuminFiledType.Other))
                return DirectEmission.Reject<DirectMember>("built-in struct member");
            string spelling = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new DirectMember(field.Name, DirectMemberKind.Fixed, spelling, spelling, 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, spelling), field.Type, field.isProperty, raw: true, builtin: true);
        }
        if (symbol.SpecialType == SpecialType.System_String)
        {
            if (field.Type != LuminFiledType.String || field.FieldType != LuminDataType.Reference)
                return DirectEmission.Reject<DirectMember>("string member");
            return new DirectMember(field.Name, DirectMemberKind.Text, "string", "string", 0, null, field.Type, field.isProperty);
        }
        string payload = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (symbol is IArrayTypeSymbol { Rank: 1 } array && Scalar(array.ElementType, allowBoolean: false) is { } element &&
            field.Type == LuminFiledType.Array && field.FieldType == LuminDataType.Reference)
            return new DirectMember(field.Name, DirectMemberKind.Array, element.Keyword, element.Keyword + "[]", 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty);
        if (symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } list &&
            list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" &&
            Scalar(list.TypeArguments[0], allowBoolean: false) is { } item && field.FieldType == LuminDataType.Reference &&
            field.Type is LuminFiledType.List)
            return new DirectMember(field.Name, DirectMemberKind.List, item.Keyword, "global::System.Collections.Generic.List<" + item.Keyword + ">", 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type, field.isProperty);
        return DirectEmission.Reject<DirectMember>("member type: " + (symbol.NullableAnnotation == NullableAnnotation.Annotated ? "annotated " : string.Empty) +
            (symbol.TypeKind == TypeKind.Enum ? "enum" : symbol is IArrayTypeSymbol ? "array" : symbol is INamedTypeSymbol { IsGenericType: true } generic ?
                generic.OriginalDefinition.Name : symbol.SpecialType != SpecialType.None ? symbol.SpecialType.ToString() :
                TypeMetaChecker.TryCheckPackableAttribute(symbol as INamedTypeSymbol) ? "packable " + symbol.TypeKind : symbol.TypeKind + " " + symbol.Name));
    }

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
            _ => null,
        };
    }
}
