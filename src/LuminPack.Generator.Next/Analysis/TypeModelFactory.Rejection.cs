using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        private const string RejectionPrefix = "The untrusted-input option does not accept this type: ";
        private readonly Dictionary<ITypeSymbol, string> _rejections = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);

        /// <summary>Finds the first input-selected shape reachable from an explicitly untrusted payload.</summary>
        /// <param name="type">The payload type.</param>
        /// <returns>The rejection reason, or null when every reachable shape is accepted.</returns>
        /// <remarks>Unsupported shapes fail closed at run time in both directions with the first rejection found in the graph,
        /// walking the same serialized members, elements, type arguments and Union cases as the original contract.</remarks>
        private string? Rejection(INamedTypeSymbol type)
        {
            lock (_gate)
            {
                if (_rejections.TryGetValue(type, out string? cached))
                    return cached.Length == 0 ? null : cached;
            }
            string result = VisitRejection(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)) ?? string.Empty;
            lock (_gate)
                _rejections[type] = result;
            return result.Length == 0 ? null : result;
        }

        /// <summary>Walks serialized members, elements and type arguments once per type.</summary>
        /// <param name="type">The current type.</param>
        /// <param name="seen">Types already visited on this walk.</param>
        /// <returns>The first rejection, or null.</returns>
        private string? VisitRejection(ITypeSymbol? type, HashSet<ITypeSymbol> seen)
        {
            // A type parameter is checked through the closed type argument that replaces it.
            if (type == null || type is ITypeParameterSymbol || !seen.Add(type))
                return null;
            string name = FormatterSpelling(type);
            if (type is IArrayTypeSymbol array)
                return VisitRejection(array.ElementType, seen);
            if (type is not INamedTypeSymbol named)
                return RejectionPrefix + name;
            // Types chosen by the input are never accepted.
            string space = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (named.SpecialType == SpecialType.System_Object || named.TypeKind == TypeKind.Delegate || name == "global::System.Type" ||
                space.StartsWith("System.Reflection", StringComparison.Ordinal) ||
                named.TypeKind == TypeKind.Interface && named.TypeArguments.Length == 0 &&
                (space == "System" || space.StartsWith("System.", StringComparison.Ordinal)))
                return RejectionPrefix + name;
            foreach (ITypeSymbol argument in named.TypeArguments)
                if (VisitRejection(argument, seen) is { } rejection)
                    return rejection;
            // Framework values, raw unmanaged storage and registered formatters use the library's or the application's own code.
            if (named.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface) ||
                !IsPackable(named) && !Marked(named) && !IsImplicitInlineClass(named))
                return null;
            // Generic members are analyzed on the definition; their arguments were checked above.
            INamedTypeSymbol definition = named.IsGenericType ? named.OriginalDefinition : named;
            // Custom formats are user code, which stays trusted like a manually registered formatter.
            if (GenerationMode(definition) is not ("Object" or "VersionTolerant" or "CircleReference"))
                return null;
            if (HasAttribute(definition, UnionAttribute))
            {
                foreach (var (_, caseType) in UnionMembers(definition, new List<string>()))
                    if (VisitRejection(caseType, seen) is { } rejection)
                        return rejection;
                return null;
            }
            foreach (INamedTypeSymbol level in Levels(definition))
            foreach (ISymbol member in OrderedStorageMembers(level))
                if (TryStorage(member, out _) &&
                    VisitRejection(member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type, seen) is { } rejection)
                    return rejection;
            return null;
        }
    }
}
