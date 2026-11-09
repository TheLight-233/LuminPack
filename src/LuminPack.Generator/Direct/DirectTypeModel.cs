using System;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
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

    /// <summary>Captures one member.</summary>
    internal DirectMember(string name, DirectMemberKind kind, string scalar, string typeName, int size, string formatter, LuminFiledType fieldType,
        bool mirrored)
    {
        Mirrored = mirrored;
        Name = name;
        Kind = kind;
        Scalar = scalar;
        TypeName = typeName;
        Size = size;
        Formatter = formatter;
        FieldType = fieldType;
    }

    /// <summary>Whether the member stores a fixed scalar.</summary>
    internal bool IsFixed => Kind == DirectMemberKind.Fixed;

    /// <summary>Whether the scalar is a Boolean.</summary>
    internal bool IsBoolean => Kind == DirectMemberKind.Fixed && Scalar == "bool";

    /// <summary>Whether the member is a JSON floating-point read site.</summary>
    internal bool IsFloating => Kind == DirectMemberKind.Fixed && Scalar is "float" or "double";

    /// <inheritdoc/>
    public bool Equals(DirectMember other) => other != null && Name == other.Name && Kind == other.Kind && Scalar == other.Scalar &&
        TypeName == other.TypeName && Size == other.Size && Formatter == other.Formatter && FieldType == other.FieldType && Mirrored == other.Mirrored;

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
    /// <summary>The root-private layout mirror that auto-property storage is reached through, or null for public fields.</summary>
    internal readonly string Mirror;
    /// <summary>The mirror's field declarations, such as <c>string? Name</c>, in storage order.</summary>
    internal readonly string[] MirrorFields;

    private DirectTypeModel(string globalName, string readable, string extension, string jsonHelper, bool freshResult, bool uniqueEntries,
        DirectMember[] members, bool proven, string mirror, string[] mirrorFields)
    {
        Proven = proven;
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
        UniqueEntries == other.UniqueEntries && Proven == other.Proven && Mirror == other.Mirror &&
        (MirrorFields == null ? other.MirrorFields == null : other.MirrorFields != null && MirrorFields.SequenceEqual(other.MirrorFields)) &&
        Members.SequenceEqual(other.Members);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectTypeModel);

    /// <inheritdoc/>
    public override int GetHashCode() => GlobalName.GetHashCode();

    /// <summary>Models a type when its complete binary and JSON roots belong to a shape the direct emitters cover.</summary>
    /// <param name="data">The analyzed type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The project generation settings.</param>
    /// <returns>The model, or null when the type keeps the original pipeline.</returns>
    internal static DirectTypeModel TryCreate(LuminDataInfo data, Compilation compilation, MetaInfo metadata)
    {
        if (DirectEmission.Mode == DirectEmissionMode.Off || !GeneratedSourceLayout.Enabled)
            return null;
        if (!SupportedProject(compilation, metadata))
            return null;
        var type = data.TypeSymbol;
        if (type == null || data.generatorType != GeneratorType.Object || data.isUnion || data.isGeneric || data.isValueType)
            return DirectEmission.Reject<DirectTypeModel>("type kind");
        if (data.Parent != null)
            return DirectEmission.Reject<DirectTypeModel>("base type");
        if (data.RentPoolMethod != null || data.callBackMethods.Count != 0 || data.enableBurst)
            return DirectEmission.Reject<DirectTypeModel>("callbacks, pool or burst");
        if (data.Diagnostics.Length != 0)
            return DirectEmission.Reject<DirectTypeModel>("diagnostics");
        if (data.fields.Count == 0 || data.fields.Count > 32)
            return DirectEmission.Reject<DirectTypeModel>("member count");
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic || type.IsRecord || type.IsGenericType ||
            type.ContainingType != null || type.DeclaredAccessibility != Accessibility.Public ||
            type.BaseType?.SpecialType != SpecialType.System_Object || type.Interfaces.Length != 0)
            return DirectEmission.Reject<DirectTypeModel>("type declaration");
        if (type.InstanceConstructors.Length != 1 || type.InstanceConstructors[0].Parameters.Length != 0 ||
            type.InstanceConstructors[0].DeclaredAccessibility != Accessibility.Public)
            return DirectEmission.Reject<DirectTypeModel>("constructor");
        var attributes = type.GetAttributes();
        if (attributes.Length != 1 || attributes[0].AttributeClass?.ToDisplayString() != LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE ||
            attributes[0].NamedArguments.Length != 0 ||
            attributes[0].ConstructorArguments.Any(argument => argument.Kind != TypedConstantKind.Enum || System.Convert.ToInt64(argument.Value, System.Globalization.CultureInfo.InvariantCulture) != 0))
            return DirectEmission.Reject<DirectTypeModel>("type attributes");
        // Members without storage in the instance and without attributes, such as methods, computed properties and static
        // fields, are never serialized. Any other declared member keeps the original pipeline.
        int fieldsDeclared = 0;
        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;
            if (member.GetAttributes().Length != 0)
                return DirectEmission.Reject<DirectTypeModel>("member attributes");
            // An auto-property is stored in its backing field, which the generated code reaches through the layout mirror.
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } auto &&
                type.GetMembers().OfType<IFieldSymbol>().Any(backing => SymbolEqualityComparer.Default.Equals(backing.AssociatedSymbol, auto)))
            {
                if (auto.GetMethod is not { DeclaredAccessibility: Accessibility.Public } || auto.SetMethod is not { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false } ||
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
                    return DirectEmission.Reject<DirectTypeModel>("declared field");
                fieldsDeclared++;
                continue;
            }
            if (!Unserialized(member))
                return DirectEmission.Reject<DirectTypeModel>("declared member");
        }
        if (fieldsDeclared != data.fields.Count)
            return DirectEmission.Reject<DirectTypeModel>("declared members");

        string extension = LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation);
        var members = new DirectMember[data.fields.Count];
        for (int index = 0; index < members.Length; index++)
            if ((members[index] = Member(data.fields[index], extension)) == null)
                return DirectEmission.Reject<DirectTypeModel>("member shape");
        // Strings without collections take the named member write and the proven object write; both need the runtime's own profiles.
        bool mirrored = data.fields.Any(field => field.isProperty);
        bool proven = !mirrored && members.Any(member => member.Kind == DirectMemberKind.Text) &&
            members.All(member => member.Kind is DirectMemberKind.Fixed or DirectMemberKind.Text);
        if (proven && !ProvenWriteProfiles.AreCanonical(compilation))
            return DirectEmission.Reject<DirectTypeModel>("proven object write");
        // JSON read locals are named after the camel-cased member; the presentation renames a leading underscore or a clash.
        if (members.Any(member => member.Name[0] == '_') ||
            members.Select(member => char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1)).Distinct(StringComparer.Ordinal).Count() != members.Length)
            return DirectEmission.Reject<DirectTypeModel>("member names");

        string globalName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (data.classFullName != null && data.classFullName != globalName && "global::" + data.classFullName != globalName)
            return DirectEmission.Reject<DirectTypeModel>("type spelling");
        string mirror = null;
        string[] mirrorFields = null;
        bool fresh = LuminPackJsonCodeGenerator.CanDeserializeDirectlyIntoFreshResult(data);
        if (mirrored)
        {
            // The mirror declares every instance field of the type in storage order, so it must hold exactly the members.
            if (fresh || data.localFields.Count != members.Length ||
                data.localFields.Where((local, index) => local.Identifier != data.fields[index].Identifier).Any())
                return DirectEmission.Reject<DirectTypeModel>("layout mirror");
            using (var hash = System.Security.Cryptography.SHA256.Create())
                mirror = "LuminLayout_" + string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(FormatterTypeName.Get(type)))
                    .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))) + "_" +
                    LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type) + "_" + TypeMetaChecker.BuildLocalMyClassName(data);
            mirrorFields = data.localFields.Select(local => local.TypeName +
                (LuminPackCodeGenerator.IsUnmanagedFiledType(local.TypeName) || local.IsValue ? string.Empty : "?") + " " + local.Identifier).ToArray();
        }
        string helper = GeneratedSourceLayout.JsonHelperName("Json_" + LuminPackExtensionGenerator.SanitizeAssemblyName(globalName));
        return new DirectTypeModel(globalName, ReadableTypeName.Of(RootContainerProjection.PayloadKey(globalName)), extension, helper,
            fresh, InterceptionSupport.IsEnabled(metadata.ParseOptions), members, proven, mirror, mirrorFields);
    }

    /// <summary>Accepts the project settings the direct emitters were verified against.</summary>
    private static bool SupportedProject(Compilation compilation, MetaInfo metadata)
    {
        if (!metadata.IsNet8 || !metadata.IsNet9_OR_GREATER || metadata.IsForUnity)
            return DirectEmission.Reject<DirectTypeModel>("target framework") != null;
        if (!metadata.AllowUnsafe)
            return DirectEmission.Reject<DirectTypeModel>("unsafe code") != null;
        if (metadata.CSharpVersion <= LanguageVersion.CSharp10 || compilation is not CSharpCompilation)
            return DirectEmission.Reject<DirectTypeModel>("language version") != null;
        return DirectEmission.ProjectFacts(compilation) || DirectEmission.Reject<DirectTypeModel>("assembly attributes") != null;
    }

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
    private static DirectMember Member(LuminDataField field, string extension)
    {
        if (field.IsPrivate || field.IsVolatile || field.IsCompress || field.OldNames.Length != 0 ||
            field.Options != null && field.Options.IsDeclared || field.TypeSymbol == null || field.Identifier != field.Name ||
            !SyntaxFacts.IsValidIdentifier(field.Name) || field.Name.Any(c => c > 127))
            return DirectEmission.Reject<DirectMember>("member options");
        var symbol = field.TypeSymbol;
        if (Scalar(symbol, allowBoolean: true) is { } scalar)
        {
            if (field.FieldType != LuminDataType.Value)
                return DirectEmission.Reject<DirectMember>("fixed member");
            return new DirectMember(field.Name, DirectMemberKind.Fixed, scalar.Keyword, scalar.Keyword, scalar.Size, null, field.Type, field.isProperty);
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
        return DirectEmission.Reject<DirectMember>("member type");
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
