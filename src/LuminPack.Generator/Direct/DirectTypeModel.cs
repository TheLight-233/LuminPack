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

    /// <summary>Captures one member.</summary>
    internal DirectMember(string name, DirectMemberKind kind, string scalar, string typeName, int size, string formatter, LuminFiledType fieldType)
    {
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
        TypeName == other.TypeName && Size == other.Size && Formatter == other.Formatter && FieldType == other.FieldType;

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
    /// <summary>The serialized members in wire order.</summary>
    internal readonly DirectMember[] Members;

    private DirectTypeModel(string globalName, string readable, string extension, string jsonHelper, bool freshResult, DirectMember[] members)
    {
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
        // Any other declared member, such as a method named like a callback or a property, keeps the original pipeline.
        int fieldsDeclared = 0;
        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;
            if (member is not IFieldSymbol field || field.IsStatic || field.IsConst || field.IsReadOnly || field.IsVolatile ||
                field.DeclaredAccessibility != Accessibility.Public || field.GetAttributes().Length != 0 || field.IsImplicitlyDeclared)
                return DirectEmission.Reject<DirectTypeModel>("declared member");
            fieldsDeclared++;
        }
        if (fieldsDeclared != data.fields.Count)
            return DirectEmission.Reject<DirectTypeModel>("declared members");

        string extension = LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation);
        var members = new DirectMember[data.fields.Count];
        for (int index = 0; index < members.Length; index++)
            if ((members[index] = Member(data.fields[index], extension)) == null)
                return DirectEmission.Reject<DirectTypeModel>("member shape");
        // Strings without collections take the proven object write, which the direct emitter does not reproduce.
        if (members.Any(member => member.Kind == DirectMemberKind.Text) && members.All(member => member.Kind is DirectMemberKind.Fixed or DirectMemberKind.Text))
            return DirectEmission.Reject<DirectTypeModel>("proven object write");
        if (members.Select(member => member.Name).Distinct(StringComparer.Ordinal).Count() != members.Length)
            return DirectEmission.Reject<DirectTypeModel>("member names");

        string globalName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (data.classFullName != null && data.classFullName != globalName && "global::" + data.classFullName != globalName)
            return DirectEmission.Reject<DirectTypeModel>("type spelling");
        string helper = GeneratedSourceLayout.JsonHelperName("Json_" + LuminPackExtensionGenerator.SanitizeAssemblyName(globalName));
        return new DirectTypeModel(globalName, ReadableTypeName.Of(RootContainerProjection.PayloadKey(globalName)), extension, helper,
            LuminPackJsonCodeGenerator.CanDeserializeDirectlyIntoFreshResult(data), members);
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
        if (GeneratedOperationUsage.IsClosed(compilation))
            return DirectEmission.Reject<DirectTypeModel>("closed operation selection") != null;
        if (!InterceptionSupport.IsEnabled(metadata.ParseOptions))
            return DirectEmission.Reject<DirectTypeModel>("interceptors") != null;
        return DirectEmission.ProjectFacts(compilation) || DirectEmission.Reject<DirectTypeModel>("assembly attributes") != null;
    }

    /// <summary>Reduces one analyzed member to the facts of a covered storage shape.</summary>
    private static DirectMember Member(LuminDataField field, string extension)
    {
        if (field.IsPrivate || field.isProperty || field.IsVolatile || field.IsCompress || field.OldNames.Length != 0 ||
            field.Options != null && field.Options.IsDeclared || field.TypeSymbol == null || field.Identifier != field.Name ||
            field.Name.Contains("_") || !SyntaxFacts.IsValidIdentifier(field.Name) || field.Name.Any(c => c > 127))
            return DirectEmission.Reject<DirectMember>("member options");
        var symbol = field.TypeSymbol;
        if (Scalar(symbol, allowBoolean: true) is { } scalar)
        {
            if (field.FieldType != LuminDataType.Value)
                return DirectEmission.Reject<DirectMember>("fixed member");
            return new DirectMember(field.Name, DirectMemberKind.Fixed, scalar.Keyword, scalar.Keyword, scalar.Size, null, field.Type);
        }
        if (symbol.SpecialType == SpecialType.System_String)
        {
            if (field.Type != LuminFiledType.String || field.FieldType != LuminDataType.Reference)
                return DirectEmission.Reject<DirectMember>("string member");
            return new DirectMember(field.Name, DirectMemberKind.Text, "string", "string", 0, null, field.Type);
        }
        string payload = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (symbol is IArrayTypeSymbol { Rank: 1 } array && Scalar(array.ElementType, allowBoolean: false) is { } element &&
            field.Type == LuminFiledType.Array && field.FieldType == LuminDataType.Reference)
            return new DirectMember(field.Name, DirectMemberKind.Array, element.Keyword, element.Keyword + "[]", 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type);
        if (symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } list &&
            list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" &&
            Scalar(list.TypeArguments[0], allowBoolean: false) is { } item && field.FieldType == LuminDataType.Reference &&
            field.Type is LuminFiledType.List)
            return new DirectMember(field.Name, DirectMemberKind.List, item.Keyword, "global::System.Collections.Generic.List<" + item.Keyword + ">", 0,
                "global::LuminPack." + BinaryExtensionProjection.Owner(extension, payload), field.Type);
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
