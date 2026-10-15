using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Describes one bound invocation method without retaining a previous compilation's symbols.</summary>
internal sealed class StableMethodIdentity
{
    private readonly StableTypeIdentity _containing;
    private readonly string _name;
    private readonly string _declaration;
    private readonly ImmutableArray<StableTypeIdentity> _arguments;
    private readonly StableTypeIdentity _receiver;
    internal readonly string Key;

    /// <summary>Captures exact overload, containing construction, generic arguments, and extension reduction.</summary>
    /// <param name="containing">The exact containing type and reference identity.</param>
    /// <param name="name">The original method name.</param>
    /// <param name="declaration">The unambiguous original declaration documentation id.</param>
    /// <param name="arguments">Complete constructed method arguments.</param>
    /// <param name="receiver">The receiver for a reduced extension invocation, otherwise null.</param>
    internal StableMethodIdentity(StableTypeIdentity containing, string name, string declaration,
        IEnumerable<StableTypeIdentity> arguments, StableTypeIdentity receiver)
    {
        _containing = containing; _name = name; _declaration = declaration;
        _arguments = arguments.ToImmutableArray(); _receiver = receiver;
        var key = new StringBuilder();
        void Part(string value) => key.Append(value.Length).Append(':').Append(value);
        Part(containing.Key); Part(name); Part(declaration); Part(receiver?.Key ?? string.Empty);
        foreach (var argument in _arguments) Part(argument.Key);
        Key = key.ToString();
    }

    /// <summary>Finds the exact declaration in its current assembly and reconstructs its original invocation form.</summary>
    /// <param name="types">The resolver containing only current-compilation semantic objects.</param>
    /// <returns>The current method, or null to require fresh binding.</returns>
    internal IMethodSymbol Resolve(CurrentTypeResolver types)
    {
        var owner = types.Resolve(_containing) as INamedTypeSymbol;
        if (owner == null) return null;
        var matches = owner.GetMembers(_name).OfType<IMethodSymbol>()
            .Where(method => method.OriginalDefinition.GetDocumentationCommentId() == _declaration).ToArray();
        if (matches.Length != 1) return null;
        IMethodSymbol result = matches[0];
        if (_arguments.Length != 0)
        {
            var arguments = _arguments.Select(types.Resolve).ToImmutableArray();
            if (arguments.Any(type => type == null) || arguments.Length != result.Arity) return null;
            result = result.Construct(arguments, arguments.Select(type => type.NullableAnnotation).ToImmutableArray());
        }
        if (_receiver != null)
        {
            var receiver = types.Resolve(_receiver);
            if (receiver == null) return null;
            result = result.ReduceExtensionMethod(receiver);
        }
        return result;
    }
}

/// <summary>Encodes supported current method symbols and verifies their round trip before retaining any identity.</summary>
internal sealed class StableMethodIdentityFactory
{
    private readonly StableTypeIdentityFactory _types;
    private readonly CurrentTypeResolver _verification;
    private readonly Dictionary<IMethodSymbol, StableMethodIdentity> _known = new Dictionary<IMethodSymbol, StableMethodIdentity>(SymbolEqualityComparer.IncludeNullability);

    /// <summary>Creates definition-aware identity helpers for one compilation.</summary>
    /// <param name="compilation">The compilation supplying current bound symbols.</param>
    internal StableMethodIdentityFactory(Compilation compilation)
    {
        _types = new StableTypeIdentityFactory(compilation, supportTypeParameters: true);
        _verification = new CurrentTypeResolver(compilation);
    }

    /// <summary>Freezes a method only when its exact current invocation form can be reconstructed.</summary>
    /// <param name="method">The actual invocation target.</param>
    /// <returns>A verified identity or null for a safe fresh-binding fallback.</returns>
    internal StableMethodIdentity Create(IMethodSymbol method)
    {
        if (method == null) return null;
        if (_known.TryGetValue(method, out var known)) return known;
        var staticForm = method.ReducedFrom == null ? method : method.GetConstructedReducedFrom();
        string declaration = staticForm?.OriginalDefinition.GetDocumentationCommentId();
        var owner = staticForm?.ContainingType == null ? null : _types.Create(staticForm.ContainingType);
        if (owner == null || declaration == null) return Remember(null);
        var arguments = staticForm.IsGenericMethod ? staticForm.TypeArguments.Select(_types.Create).ToArray() : Array.Empty<StableTypeIdentity>();
        var receiver = method.ReducedFrom == null ? null : _types.Create(method.ReceiverType);
        if (arguments.Any(argument => argument == null) || method.ReducedFrom != null && receiver == null) return Remember(null);
        var identity = new StableMethodIdentity(owner, staticForm.Name, declaration, arguments, receiver);
        var reconstructed = identity.Resolve(_verification);
        if (reconstructed == null || !SymbolEqualityComparer.IncludeNullability.Equals(method, reconstructed) ||
            method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != reconstructed.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            return Remember(null);
        return Remember(identity);

        StableMethodIdentity Remember(StableMethodIdentity value) { _known[method] = value; return value; }
    }
}

/// <summary>Memoizes current method reconstruction without retaining symbols outside their compilation context.</summary>
internal sealed class CurrentMethodResolver
{
    private readonly CurrentTypeResolver _types;
    private readonly Dictionary<string, IMethodSymbol> _known = new Dictionary<string, IMethodSymbol>(StringComparer.Ordinal);

    /// <summary>Creates a resolver for the current compilation's method and type identities.</summary>
    /// <param name="compilation">The current compilation.</param>
    internal CurrentMethodResolver(Compilation compilation) => _types = new CurrentTypeResolver(compilation);

    /// <summary>Resolves each retained method identity once using current symbols only.</summary>
    /// <param name="identity">The verified symbol-free invocation identity.</param>
    /// <returns>The current invocation method or null for a failed rebind.</returns>
    internal IMethodSymbol Resolve(StableMethodIdentity identity)
    {
        if (_known.TryGetValue(identity.Key, out var method)) return method;
        method = identity.Resolve(_types);
        _known[identity.Key] = method;
        return method;
    }
}
