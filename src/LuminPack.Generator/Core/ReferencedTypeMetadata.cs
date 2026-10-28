using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>Imports complete referenced storage metadata without changing the consuming compilation.</summary>
internal static class ReferencedTypeMetadata
{
    private static readonly ConditionalWeakTable<Compilation, Context> Contexts = new();

    /// <summary>Detects reference assemblies that omit the backing storage required by an imported class contract.</summary>
    /// <param name="type">A selected generated formatter type.</param>
    /// <param name="compilation">The unchanged consumer compilation.</param>
    /// <returns>Errors for properties whose storage and declaration order cannot be reconstructed.</returns>
    internal static Diagnostic[] ValidatePropertyStorage(INamedTypeSymbol type, Compilation compilation)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.DeclaringSyntaxReferences.Length != 0 ||
            LuminPackExtensionGenerator.IsCustomGeneratorType(type)) return System.Array.Empty<Diagnostic>();
        List<Diagnostic> diagnostics = null;
        for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            if (!current.ContainingAssembly.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                    "System.Runtime.CompilerServices.ReferenceAssemblyAttribute")) continue;
            var complete = Resolve(current, compilation);
            foreach (var property in complete.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsAbstract || property.IsExtern || property.IsIndexer ||
                    complete.GetMembers("<" + property.Name + ">k__BackingField").OfType<IFieldSymbol>().Any()) continue;
                if (property.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal) &&
                    !TypeMetaChecker.TryCheckIncludeAttribute(property)) continue;
                bool generated = property.GetMethod?.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                    "System.Runtime.CompilerServices.CompilerGeneratedAttribute") == true ||
                    property.SetMethod?.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                    "System.Runtime.CompilerServices.CompilerGeneratedAttribute") == true;
                if (!generated) continue;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.IncompleteReferencedPropertyStorage,
                    type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None,
                    complete.ToDisplayString(), property.Name));
            }
        }
        return diagnostics?.ToArray() ?? System.Array.Empty<Diagnostic>();
    }

    /// <summary>Resolves a referenced type with private fields and compiler-generated backing fields visible.</summary>
    /// <param name="type">The current type whose storage must be mirrored.</param>
    /// <param name="compilation">The unchanged consuming compilation.</param>
    /// <returns>The equivalent complete metadata type, or the original source type.</returns>
    internal static INamedTypeSymbol Resolve(INamedTypeSymbol type, Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly) ||
            compilation.Options.MetadataImportOptions == MetadataImportOptions.All) return type;
        return Contexts.GetValue(compilation, current => new Context((CSharpCompilation)current)).Resolve(type);
    }

    /// <summary>Projects a full-metadata member type back into the consuming compilation's symbol universe.</summary>
    /// <param name="type">The member type obtained from the full metadata view.</param>
    /// <param name="compilation">The unchanged consuming compilation.</param>
    /// <returns>The equivalent current-compilation type.</returns>
    internal static ITypeSymbol ToCurrent(ITypeSymbol type, Compilation compilation)
        => compilation.Options.MetadataImportOptions == MetadataImportOptions.All ? type
            : Contexts.GetValue(compilation, current => new Context((CSharpCompilation)current)).ToCurrent(type);

    /// <summary>Owns the full metadata view only for the lifetime of its consuming compilation.</summary>
    private sealed class Context
    {
        private readonly StableTypeIdentityFactory _identities;
        private readonly CurrentTypeResolver _resolver;
        private readonly StableTypeIdentityFactory _fullIdentities;
        private readonly CurrentTypeResolver _currentResolver;
        private readonly object _gate = new();

        /// <summary>Creates one complete metadata view, sharing the original syntax and references.</summary>
        /// <param name="compilation">The consuming compilation.</param>
        internal Context(CSharpCompilation compilation)
        {
            _identities = new StableTypeIdentityFactory(compilation, supportTypeParameters: true);
            var full = compilation.WithOptions(compilation.Options.WithMetadataImportOptions(MetadataImportOptions.All));
            _resolver = new CurrentTypeResolver(full);
            _fullIdentities = new StableTypeIdentityFactory(full, supportTypeParameters: true);
            _currentResolver = new CurrentTypeResolver(compilation);
        }

        /// <summary>Rebinds a constructed type while retaining exact assembly and generic argument identities.</summary>
        /// <param name="type">The referenced type to import completely.</param>
        /// <returns>The corresponding type from the complete metadata view.</returns>
        internal INamedTypeSymbol Resolve(INamedTypeSymbol type)
        {
            lock (_gate)
            {
                var identity = _identities.Create(type) ?? _fullIdentities.Create(type);
                return identity != null && _resolver.Resolve(identity) is INamedTypeSymbol resolved ? resolved
                    : LuminPackExceptionHelper.ThrowInvalidOperationException<INamedTypeSymbol>(
                        "Cannot import the complete storage metadata for " + type.ToDisplayString());
            }
        }

        /// <summary>Rebinds a metadata member without publishing symbols from the auxiliary compilation.</summary>
        /// <param name="type">The complete-view member type.</param>
        /// <returns>The consuming compilation's corresponding type.</returns>
        internal ITypeSymbol ToCurrent(ITypeSymbol type)
        {
            lock (_gate)
            {
                var identity = _fullIdentities.Create(type);
                return identity != null && _currentResolver.Resolve(identity) is { } resolved ? resolved
                    : LuminPackExceptionHelper.ThrowInvalidOperationException<ITypeSymbol>(
                        "Cannot resolve the referenced member type " + type.ToDisplayString());
            }
        }
    }
}
