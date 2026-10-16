using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>Imports complete referenced storage metadata without changing the consuming compilation.</summary>
internal static class ReferencedTypeMetadata
{
    private static readonly ConditionalWeakTable<Compilation, Context> Contexts = new();

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
