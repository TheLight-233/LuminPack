using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        /// <summary>Captures each definition's complete source-level constraint clauses once during analysis.</summary>
        /// <param name="type">The generic declaration.</param>
        /// <returns>Symbol-free constraint clauses in declaration order.</returns>
        private static string[] TypeConstraints(INamedTypeSymbol type)
        {
            if (!type.IsDefinition || type.TypeParameters.Length == 0)
                return Array.Empty<string>();
            var clauses = new List<string>();
            foreach (ITypeParameterSymbol parameter in type.TypeParameters)
            {
                var constraints = new List<string>();
                if (parameter.HasUnmanagedTypeConstraint)
                    constraints.Add("unmanaged");
                else if (parameter.HasValueTypeConstraint)
                    constraints.Add("struct");
                else if (parameter.HasReferenceTypeConstraint)
                    constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
                else if (parameter.HasNotNullConstraint)
                    constraints.Add("notnull");
                foreach (ITypeSymbol constraint in parameter.ConstraintTypes)
                    constraints.Add(constraint.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                if (parameter.HasConstructorConstraint && !parameter.HasValueTypeConstraint && !parameter.HasUnmanagedTypeConstraint)
                    constraints.Add("new()");
                if (constraints.Count != 0)
                    clauses.Add("where " + Identifier(parameter.Name) + " : " + string.Join(", ", constraints));
            }
            return clauses.ToArray();
        }
    }
}
