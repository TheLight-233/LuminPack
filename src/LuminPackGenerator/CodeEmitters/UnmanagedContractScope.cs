using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Resolves custom contracts for a closed operation without reanalyzing its generic wire layout.</summary>
internal sealed class UnmanagedContractScope : IDisposable
{
    [ThreadStatic] private static UnmanagedContractScope _current;
    private readonly UnmanagedContractScope _previous;
    private readonly Dictionary<string, ITypeSymbol> _arguments = new(StringComparer.Ordinal);
    private readonly Dictionary<ITypeSymbol, ITypeSymbol> _resolved = new(SymbolEqualityComparer.IncludeNullability);
    private readonly Compilation _compilation;

    /// <summary>Captures only the type substitutions of the operation currently being rendered.</summary>
    /// <param name="type">The closed operation type, or null for a nongeneric builtin.</param>
    /// <param name="compilation">The current compilation used to construct array symbols.</param>
    internal UnmanagedContractScope(INamedTypeSymbol type, Compilation compilation)
    {
        _previous = _current;
        _compilation = compilation;
        for (var owner = type; owner != null; owner = owner.ContainingType)
            for (int index = 0; index < owner.Arity; index++)
                _arguments[ParameterKey(owner.OriginalDefinition.TypeParameters[index])] = owner.TypeArguments[index];
        _current = this;
    }

    /// <summary>Restores the caller's emission scope without retaining compilation state in operation plans.</summary>
    public void Dispose() => _current = _previous;

    /// <summary>Substitutes only for unmanaged/custom eligibility; serialization metadata stays definition-based.</summary>
    /// <param name="type">The original storage type.</param>
    /// <returns>The corresponding closed storage type when this emission has its arguments.</returns>
    internal static ITypeSymbol Resolve(ITypeSymbol type) => _current?.Substitute(type) ?? type;

    /// <summary>Forms a parameter identity valid across the ordinary and complete metadata views.</summary>
    /// <param name="parameter">A definition-owned parameter.</param>
    /// <returns>The exact owner/ordinal identity.</returns>
    private static string ParameterKey(ITypeParameterSymbol parameter)
        => parameter.ContainingAssembly.Identity + "|" + parameter.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "|" + parameter.Ordinal;

    /// <summary>Closes nested storage graphs while preserving containing-type argument owners.</summary>
    /// <param name="type">A storage type from the original template.</param>
    /// <returns>The substituted type.</returns>
    private ITypeSymbol Substitute(ITypeSymbol type)
    {
        if (_resolved.TryGetValue(type, out var resolved)) return resolved;
        resolved = SubstituteCore(type);
        _resolved.Add(type, resolved);
        return resolved;
    }

    /// <summary>Constructs one storage substitution after the emission-local memo lookup.</summary>
    /// <param name="type">The original storage type.</param>
    /// <returns>The substituted type without changing serialization metadata.</returns>
    private ITypeSymbol SubstituteCore(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol parameter)
            return _arguments.TryGetValue(ParameterKey(parameter), out var argument) ? argument : type;
        if (type is IArrayTypeSymbol array)
            return _compilation.CreateArrayTypeSymbol(Substitute(array.ElementType), array.Rank);
        if (type is not INamedTypeSymbol named || !named.IsGenericType || named.IsUnboundGenericType) return type;
        var definition = named.ConstructedFrom;
        if (named.ContainingType is { } containing)
        {
            var owner = (INamedTypeSymbol)Substitute(containing);
            definition = owner.GetTypeMembers(named.Name, named.Arity).FirstOrDefault(candidate => candidate.MetadataName == named.MetadataName) ?? definition;
        }
        return named.Arity == 0 ? definition : definition.Construct(named.TypeArguments.Select(Substitute).ToArray());
    }
}
