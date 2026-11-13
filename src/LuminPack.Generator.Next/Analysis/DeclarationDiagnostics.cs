using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        private static readonly UTF8Encoding StrictMemberEncoding = new UTF8Encoding(false, true);
        private readonly Dictionary<INamedTypeSymbol, DiagnosticInfo[]> _declarationDiagnostics =
            new Dictionary<INamedTypeSymbol, DiagnosticInfo[]>(SymbolEqualityComparer.Default);

        /// <summary>Validates one declaration once per compilation and retains only value-equal diagnostic facts.</summary>
        /// <param name="type">The declaration to validate.</param>
        /// <returns>The declaration diagnostics.</returns>
        internal DiagnosticInfo[] Diagnose(INamedTypeSymbol type)
        {
            lock (_gate)
            {
                if (_declarationDiagnostics.TryGetValue(type, out DiagnosticInfo[]? cached))
                    return cached;
            }
            var diagnostics = new DeclarationDiagnosticBag();
            ValidateDeclaration(type, ref diagnostics);
            DiagnosticInfo[] result = diagnostics.ToArray();
            lock (_gate)
                _declarationDiagnostics[type] = result;
            return result;
        }

        /// <summary>Checks the legacy declaration contract independently of renderer availability.</summary>
        /// <param name="type">The attributed payload.</param>
        /// <param name="diagnostics">Receives invalid declaration facts.</param>
        private void ValidateDeclaration(INamedTypeSymbol type, ref DeclarationDiagnosticBag diagnostics)
        {
            ValidateCompiledOptions(type, ref diagnostics);
            foreach (INamedTypeSymbol scope in ContainingTypes(type))
                ValidateCompiledOptions(scope, ref diagnostics);
            if (type.ContainingType != null && type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            {
                diagnostics.Add(DiagnosticDescriptors.NestedClassMustBePublicOrInternal, type, type.Name);
                diagnostics.Add(DiagnosticDescriptors.NestedClassAccessibilityError, type, type.Name, type.DeclaredAccessibility.ToString());
            }
            if (HasAttribute(type, BoundedAttribute) && !ValidBound(type))
                diagnostics.Add(DiagnosticDescriptors.InvalidBoundedSize, type, type.ToDisplayString());
            string mode = GenerationMode(type);
            if (mode == "Custom")
            {
                ValidateCustom(type, ref diagnostics);
                return;
            }
            if (type.IsStatic)
                diagnostics.Add(DiagnosticDescriptors.StaticClass, type, type.Name);
            if (type.IsRefLikeType)
                diagnostics.Add(DiagnosticDescriptors.TypeIsRefStruct, type, type.Name);
            ValidatePortableStorage(type, ref diagnostics);
            bool union = type.IsAbstract || HasAttribute(type, UnionAttribute);
            if (union)
                ValidateUnion(type, ref diagnostics);
            if (mode == "CircleReference")
                ValidateCircleOrders(type, ref diagnostics);

            var members = new List<ISymbol>();
            var storageNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (INamedTypeSymbol level in Levels(type))
            foreach (ISymbol member in OrderedStorageMembers(level))
            {
                ValidateCompiledOptions(member, ref diagnostics);
                if (member.IsStatic || member.IsImplicitlyDeclared || member is IFieldSymbol { IsConst: true })
                    continue;
                if (member is IPropertySymbol property && !IsAutoProperty(property))
                    continue;
                if (!storageNames.Add(member.Name))
                    diagnostics.Add(DiagnosticDescriptors.ContainsDuplicateNameField, member, member.Name);
                if (InvalidOverrideAnnotation(member) is { } annotation)
                {
                    diagnostics.Add(DiagnosticDescriptors.OverrideMemberCantAddAnnotation, member, level.Name, member.Name, annotation);
                    continue;
                }
                if (!TryStorage(member, out _))
                    continue;
                if (!SymbolEqualityComparer.Default.Equals(level, type) && member.DeclaredAccessibility == Accessibility.Private &&
                    HasAttribute(member, IncludeAttribute))
                {
                    diagnostics.Add(DiagnosticDescriptors.InheritTypeCanNotIncludeParentPrivateMember, member, type.Name, member.Name);
                    continue;
                }
                members.Add(member);
                ITypeSymbol memberType = StorageType(member);
                bool ownFormatter = HasAttribute(member, PackableObjectAttribute);
                if (memberType.IsRefLikeType)
                    diagnostics.Add(DiagnosticDescriptors.MemberIsRefStruct, member, type.Name, member.Name, memberType.ToDisplayString());
                else if (!ownFormatter)
                    ValidateNestedType(memberType, member, ref diagnostics);
                else if (memberType is INamedTypeSymbol explicitType && (IsPackable(explicitType) || Marked(explicitType)))
                    ValidateImportedStorage(explicitType, member, ref diagnostics);
                ITypeSymbol? element = memberType is IArrayTypeSymbol array ? array.ElementType :
                    memberType is INamedTypeSymbol named && SequenceKind(named) == CollectionKind.List ? named.TypeArguments[0] : null;
                if (element != null && !element.IsUnmanagedType && HasAttribute(member, "LuminPack.Attribute.LuminPackCompressAttribute") &&
                    !ownFormatter)
                    diagnostics.Add(DiagnosticDescriptors.ManagedArrayDataCompress, member, type.Name, member.Name,
                        memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
            if (members.Count > 249)
                diagnostics.Add(DiagnosticDescriptors.MembersCountOver250, type, type.Name, members.Count.ToString(CultureInfo.InvariantCulture));
            ValidateVersionNames(type, members, mode, ref diagnostics);
            ValidateCallbacks(type, ref diagnostics);
            if (!union && !type.IsStatic)
                ValidateConstructors(type, members, ref diagnostics);
            ValidatePoolRent(type, ref diagnostics);
        }

        /// <summary>Enumerates the enclosing type scopes without retaining them in a model.</summary>
        /// <param name="type">The payload declaration.</param>
        /// <returns>The enclosing scopes.</returns>
        private static IEnumerable<INamedTypeSymbol> ContainingTypes(INamedTypeSymbol type)
        {
            for (INamedTypeSymbol? scope = type.ContainingType; scope != null; scope = scope.ContainingType)
                yield return scope;
        }

        /// <summary>Gets the storage type of a serialized field or property.</summary>
        /// <param name="member">The storage declaration.</param>
        /// <returns>The declared value type.</returns>
        private static ITypeSymbol StorageType(ISymbol member) => member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;

        /// <summary>Finds the forbidden include or ignore annotation on an overridden property.</summary>
        /// <param name="member">The storage declaration.</param>
        /// <returns>The legacy annotation spelling, or null.</returns>
        private static string? InvalidOverrideAnnotation(ISymbol member)
        {
            if (member is not IPropertySymbol { OverriddenProperty: not null })
                return null;
            foreach (AttributeData attribute in member.GetAttributes())
            {
                string? name = attribute.AttributeClass?.ToDisplayString();
                if (name == IncludeAttribute)
                    return "LuminPackInclude";
                if (name == IgnoreAttribute)
                    return "LuminPackIgnore";
            }
            return null;
        }

        /// <summary>Validates recursive element contracts without building an emitter metadata graph.</summary>
        /// <param name="type">The member or element type.</param>
        /// <param name="member">The original diagnostic location.</param>
        /// <param name="diagnostics">Receives invalid nested types.</param>
        private void ValidateNestedType(ITypeSymbol type, ISymbol member, ref DeclarationDiagnosticBag diagnostics)
        {
            if (type is ITypeParameterSymbol || type.TypeKind == TypeKind.Error || type.IsUnmanagedType ||
                type.SpecialType is SpecialType.System_String or SpecialType.System_Object)
                return;
            if (type is IArrayTypeSymbol array)
            {
                ValidateNestedType(array.ElementType, member, ref diagnostics);
                return;
            }
            if (type is not INamedTypeSymbol named)
                return;
            if (IsPackable(named) || Marked(named))
            {
                ValidateImportedStorage(named, member, ref diagnostics);
                return;
            }
            // A concrete plain class is expanded inline as a nested object and needs no declaration.
            if (IsImplicitInlineClass(named))
                return;
            // A direct opaque value from a referenced assembly can use its registered formatter.
            if (named is { TypeKind: TypeKind.Struct, IsGenericType: false, IsRecord: false, IsRefLikeType: false } &&
                !IsLocal(named) && SymbolEqualityComparer.Default.Equals(named, StorageType(member)))
                return;
            string name = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
            if (SequenceKind(named) != null || DictionaryKind(named) != null || GenericIdentity.Of(FormatterSpelling(named)) != null)
            {
                foreach (ITypeSymbol argument in named.TypeArguments)
                    ValidateNestedType(argument, member, ref diagnostics);
                return;
            }
            if (Builtin(named) != null || IsCultureInfo(named) || FormatterRegistry.HasStandalone(name))
                return;
            diagnostics.Add(DiagnosticDescriptors.FieldMustBeLuminPackable, member, named.Name,
                additional: named.OriginalDefinition.Locations);
        }

        /// <summary>Checks revisioned binary identities and former-name aliases using the legacy Unicode contract.</summary>
        /// <param name="type">The payload declaration.</param>
        /// <param name="members">The complete filtered storage.</param>
        /// <param name="mode">The declared generator mode.</param>
        /// <param name="diagnostics">Receives invalid names.</param>
        private static void ValidateVersionNames(INamedTypeSymbol type, List<ISymbol> members, string mode,
            ref DeclarationDiagnosticBag diagnostics)
        {
            HashSet<string>? names = mode == "VersionTolerant" ? new HashSet<string>(StringComparer.Ordinal) : null;
            foreach (ISymbol member in members)
            {
                bool hasAliases = HasAttribute(member, "LuminPack.Attribute.LuminPackOldNameAttribute");
                if (names == null)
                {
                    if (hasAliases)
                        diagnostics.Add(DiagnosticDescriptors.InvalidVersionMemberName, type, type.Name,
                            "[LuminPackOldName] requires VersionTolerant mode.");
                    continue;
                }
                ValidateVersionName(type, member.Name, names, ref diagnostics);
                if (!hasAliases)
                    continue;
                foreach (AttributeData attribute in member.GetAttributes())
                    if (attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOldNameAttribute")
                        ValidateVersionName(type, attribute.ConstructorArguments.FirstOrDefault().Value as string, names, ref diagnostics);
            }
        }

        /// <summary>Checks one current or former binary member identity.</summary>
        /// <param name="type">The declaring payload.</param>
        /// <param name="name">The serialized identity.</param>
        /// <param name="names">The identities already encountered.</param>
        /// <param name="diagnostics">Receives invalid identities.</param>
        private static void ValidateVersionName(INamedTypeSymbol type, string? name, HashSet<string> names,
            ref DeclarationDiagnosticBag diagnostics)
        {
            string? error = null;
            if (string.IsNullOrEmpty(name))
                error = "Serialized member names must not be null or empty.";
            else if (!names.Add(name!))
                error = "Serialized member name '" + name + "' is ambiguous.";
            else
            {
                try
                {
                    StrictMemberEncoding.GetByteCount(name!);
                }
                catch (EncoderFallbackException)
                {
                    error = "Serialized member names must contain valid Unicode.";
                }
            }
            if (error != null)
                diagnostics.Add(DiagnosticDescriptors.InvalidVersionMemberName, type, type.Name, error);
        }

        /// <summary>Checks omitted backing storage in an imported reference assembly.</summary>
        /// <param name="type">The imported payload.</param>
        /// <param name="member">The consuming storage declaration.</param>
        /// <param name="diagnostics">Receives storage reconstruction errors.</param>
        private static void ValidateImportedStorage(INamedTypeSymbol type, ISymbol member, ref DeclarationDiagnosticBag diagnostics)
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.DeclaringSyntaxReferences.Length != 0 || GenerationMode(type) == "Custom")
                return;
            foreach (INamedTypeSymbol level in Levels(type))
            {
                if (!HasAttribute(level.ContainingAssembly, ReferenceAssemblyAttribute))
                    continue;
                foreach (IPropertySymbol property in level.GetMembers().OfType<IPropertySymbol>())
                {
                    if (property.IsStatic || property.IsAbstract || property.IsExtern || property.IsIndexer || IsAutoProperty(property) ||
                        HasAttribute(property, IgnoreAttribute))
                        continue;
                    if (property.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal) &&
                        !HasAttribute(property, IncludeAttribute))
                        continue;
                    if (property.GetMethod != null && HasAttribute(property.GetMethod, "System.Runtime.CompilerServices.CompilerGeneratedAttribute") ||
                        property.SetMethod != null && HasAttribute(property.SetMethod, "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
                        diagnostics.Add(DiagnosticDescriptors.IncompleteReferencedPropertyStorage, member,
                            level.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(), property.Name);
                }
            }
        }
    }
}

/// <summary>Allocates diagnostic storage only after the first failed declaration rule.</summary>
internal struct DeclarationDiagnosticBag
{
    private List<DiagnosticInfo>? _items;

    /// <summary>Captures a legacy diagnostic at its original declaration.</summary>
    /// <param name="descriptor">The stable descriptor.</param>
    /// <param name="symbol">The primary source declaration.</param>
    /// <param name="first">The first message argument.</param>
    /// <param name="second">The second message argument.</param>
    /// <param name="third">The third message argument.</param>
    /// <param name="additional">Related declarations used by code fixes.</param>
    internal void Add(DiagnosticDescriptor descriptor, ISymbol symbol, string first, string? second = null, string? third = null,
        IEnumerable<Location>? additional = null)
    {
        string[] arguments = third != null ? new[] { first, second!, third } : second != null ? new[] { first, second } : new[] { first };
        Add(DiagnosticInfo.Capture(descriptor.Id, symbol.Locations.FirstOrDefault(location => location.IsInSource), arguments, additional));
    }

    /// <summary>Stores a captured diagnostic.</summary>
    /// <param name="diagnostic">The value-equal diagnostic facts.</param>
    internal void Add(DiagnosticInfo diagnostic) => (_items ??= new List<DiagnosticInfo>()).Add(diagnostic);

    /// <summary>Completes this declaration's immutable diagnostic snapshot.</summary>
    /// <returns>The captured facts, sharing the empty array for valid declarations.</returns>
    internal DiagnosticInfo[] ToArray() => _items?.ToArray() ?? Array.Empty<DiagnosticInfo>();
}
