using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Decides which shapes the untrusted-input option accepts, from the same ordered members as the formatter.</summary>
/// <remarks>Unsupported shapes fail closed at run time with the first rejection found in the graph.</remarks>
internal static class SafeContract
{
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<string, string>> Cache = new();
    private const string Accepted = "";

    /// <summary>Finds the first unsupported shape reachable from a type.</summary>
    /// <param name="type">The operation type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The formatter generation settings.</param>
    /// <returns>The rejection reason, or null when every reachable shape is accepted.</returns>
    internal static string Rejection(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        var cache = Cache.GetValue(compilation, static _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        string result = cache.GetOrAdd(FormatterTypeName.Get(type), _ => Visit(type, compilation, metadata, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)) ?? Accepted);
        return result.Length == 0 ? null : result;
    }

    /// <summary>Walks serialized members, elements and type arguments once per type.</summary>
    /// <param name="type">The current type.</param><param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">Generation settings.</param><param name="seen">Types already visited on this walk.</param>
    /// <returns>The first rejection, or null.</returns>
    private static string Visit(ITypeSymbol type, Compilation compilation, MetaInfo metadata, HashSet<ITypeSymbol> seen)
    {
        // A type parameter is checked through the closed type argument that replaces it.
        if (type == null || type is ITypeParameterSymbol || !seen.Add(type)) return null;
        string name = FormatterTypeName.Get(type);
        if (type is IArrayTypeSymbol array) return Visit(array.ElementType, compilation, metadata, seen);
        if (type is not INamedTypeSymbol named) return "The untrusted-input option does not accept this type: " + name;
        // Types chosen by the input are never accepted.
        string space = named.ContainingNamespace?.ToDisplayString() ?? "";
        if (named.SpecialType == SpecialType.System_Object || named.TypeKind == TypeKind.Delegate || name == "global::System.Type" ||
            space.StartsWith("System.Reflection", StringComparison.Ordinal) ||
            named.TypeKind == TypeKind.Interface && named.TypeArguments.Length == 0 && (space == "System" || space.StartsWith("System.", StringComparison.Ordinal)))
            return "The untrusted-input option does not accept this type: " + name;
        foreach (var argument in named.TypeArguments)
            if (Visit(argument, compilation, metadata, seen) is string rejection) return rejection;
        // Framework values, raw unmanaged storage and registered formatters use the library's or the application's own code.
        if (named.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface) || !CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named))
            return null;
        // Generic members are analyzed on the definition; their arguments were checked above.
        var data = LuminPackSourceGenerator.GetSerializationMetadata(named.IsGenericType ? named.OriginalDefinition : named, compilation, metadata);
        // Custom formats are user code, which stays trusted like a manually registered formatter.
        if (data.generatorType is not (GeneratorType.Object or GeneratorType.VersionTolerant or GeneratorType.CircleReference)) return null;
        if (data.isUnion)
        {
            foreach (var member in data.UnionMembers)
                if (Visit(member.Type, compilation, metadata, seen) is string rejection) return rejection;
            return null;
        }
        foreach (var field in data.fields)
            if (Visit(field.TypeSymbol, compilation, metadata, seen) is string rejection) return rejection;
        return null;
    }
}
