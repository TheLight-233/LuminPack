using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Classifies serializable types whose member graph can reach themselves.</summary>
internal static class RecursiveTypeAnalysis
{
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<string, bool>> Cache = new();

    /// <summary>Reports whether serializing the type can re-enter an operation of the same type.</summary>
    /// <param name="type">The formatter type.</param>
    /// <param name="compilation">The compilation whose referenced declarations are inspected.</param>
    /// <returns>Whether a member, element, type argument, base type or Union case path leads back to the type.</returns>
    /// <remarks>The walk follows declared members only for types with a LuminPack declaration; framework containers contribute their type arguments.</remarks>
    internal static bool IsRecursive(ITypeSymbol type, Compilation compilation)
    {
        if (type is null) return false;
        var cache = Cache.GetValue(compilation, static _ => new ConcurrentDictionary<string, bool>(StringComparer.Ordinal));
        string key = FormatterTypeName.Get(type);
        if (cache.TryGetValue(key, out bool known)) return known;
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        bool recursive = Successors(type).Any(next => Reaches(next, type, visited));
        cache[key] = recursive;
        return recursive;
    }

    /// <summary>Searches the member graph depth-first for a target type.</summary>
    /// <param name="current">The type being expanded.</param>
    /// <param name="target">The type that closes a cycle.</param>
    /// <param name="visited">The finite set of types already expanded.</param>
    /// <returns>Whether the target is reachable from the current type.</returns>
    private static bool Reaches(ITypeSymbol current, ITypeSymbol target, HashSet<ITypeSymbol> visited)
    {
        if (SymbolEqualityComparer.Default.Equals(current, target)) return true;
        if (!visited.Add(current)) return false;
        return Successors(current).Any(next => Reaches(next, target, visited));
    }

    /// <summary>Lists the types serialized inside a value of the given type.</summary>
    /// <param name="type">The containing type.</param>
    /// <returns>Element, argument, member, base and Union case types.</returns>
    private static IEnumerable<ITypeSymbol> Successors(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            yield return array.ElementType;
            yield break;
        }
        if (type is not INamedTypeSymbol named) yield break;
        foreach (var argument in named.TypeArguments) yield return argument;
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) yield break;
        // Only declarations that LuminPack analyzes expose members; framework types are opaque beyond their arguments.
        if (!SourceGenerator.TypeMetaChecker.TryCheckPackableAttribute(named)) yield break;
        for (var level = named; level != null && level.SpecialType != SpecialType.System_Object && level.SpecialType != SpecialType.System_ValueType; level = level.BaseType)
        {
            foreach (var member in level.GetMembers())
            {
                if (member is IFieldSymbol { IsStatic: false } field) yield return field.Type;
                else if (member is IPropertySymbol { IsStatic: false, IsIndexer: false } property) yield return property.Type;
            }
        }
        foreach (var attribute in named.GetAttributes())
            if (attribute.AttributeClass?.Name is "LuminPackUnionAttribute" && attribute.ConstructorArguments.Length == 2 &&
                attribute.ConstructorArguments[1].Value is ITypeSymbol caseType)
                yield return caseType;
    }
}

/// <summary>Bounds the stack depth of recursive operations so a forged or very deep graph fails instead of ending the process.</summary>
/// <remarks>Binary write operations of recursive payloads also check output capacity on every re-entry. Fixed-width members carry no per-value
/// check, and a chain of fixed-width nodes has no collection or string boundary that would grow owned storage.</remarks>
internal sealed class RecursionGuardProjection : CSharpSyntaxRewriter
{
    private static readonly HashSet<string> Roots = new(StringComparer.Ordinal)
    {
        "RootWrite", "RootRead", "RootWriteUnion", "RootReadUnion", "RootWriteJson", "RootReadJson", "Sizeof",
        "RootWriteCompressed", "RootReadCompressed", "RootReadFresh"
    };

    private readonly Func<string, bool> _recursive;

    /// <summary>Captures the payload classification.</summary>
    /// <param name="recursive">Reports whether a payload formatter type name is recursive.</param>
    internal RecursionGuardProjection(Func<string, bool> recursive) => _recursive = recursive;

    /// <summary>Inserts one stack check at the start of every operation body of a recursive payload.</summary>
    /// <param name="node">A generated operation.</param>
    /// <returns>The same operation with its body guarded, or the unchanged declaration.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var result = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (result.Body is null || !Roots.Contains(result.Identifier.ValueText) || result.ParameterList.Parameters.Count != 2) return result;
        // A forwarding wrapper has no body of its own; the operation it calls carries the guard.
        if (result.Body.Statements.Count <= 1 && !result.Identifier.ValueText.StartsWith("Root", StringComparison.Ordinal)) return result;
        string payload = result.ParameterList.Parameters[1].Type?.ToString().TrimEnd('?') ?? string.Empty;
        if (!_recursive(payload)) return result;
        var guard = SyntaxFactory.ParseStatement("global::LuminPack.Code.LuminPackExceptionHelper.EnsureSufficientStack();");
        var parameter = result.ParameterList.Parameters[0];
        string state = parameter.Type?.ToString().Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty);
        var statements = result.Body.Statements.Insert(0, guard);
        if (state == "LuminPackWriter")
            statements = statements.Insert(1, SyntaxFactory.ParseStatement(parameter.Identifier.ValueText + ".CheckBuffer();"));
        return result.WithBody(result.Body.WithStatements(statements));
    }
}
