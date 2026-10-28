using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{

    /// <summary>Performs the enumerate formatter fields step for the current operation.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    private static IEnumerable<LuminLocalFieldData> EnumerateFormatterFields(LuminDataInfo data)
    {
        HashSet<ITypeSymbol> visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (LuminDataField field in data.fields)
        {
            foreach (LuminLocalFieldData formatterField in EnumerateFormatterFields(field.TypeSymbol, visited))
            {
                yield return formatterField;
            }
        }
    }

    /// <summary>Performs the enumerate formatter fields step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="visited">The visited used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static IEnumerable<LuminLocalFieldData> EnumerateFormatterFields(ITypeSymbol? type, HashSet<ITypeSymbol> visited)
    {
        if (type is null || type is ITypeParameterSymbol || !visited.Add(type))
        {
            yield break;
        }

        if (type is IArrayTypeSymbol array)
        {
            foreach (LuminLocalFieldData element in EnumerateFormatterFields(array.ElementType, visited))
            {
                yield return element;
            }
        }
        else if (type is INamedTypeSymbol named)
        {
            foreach (ITypeSymbol argument in named.TypeArguments)
            {
                foreach (LuminLocalFieldData argumentField in EnumerateFormatterFields(argument, visited))
                {
                    yield return argumentField;
                }
            }

            // These LINQ formatters serialize through concrete interface views which may
            // never appear explicitly in user syntax. Keep their exact closed helper types
            // in the formatter dependency graph so nested calls can bind statically.
            string definitionName = named.OriginalDefinition.ToDisplayString();
            if (definitionName == "System.Linq.ILookup<TKey, TElement>" ||
                definitionName == "System.Linq.IGrouping<TKey, TElement>")
            {
                foreach (INamedTypeSymbol enumerable in named.AllInterfaces.Where(static candidate =>
                    candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
                {
                    foreach (LuminLocalFieldData dependency in EnumerateFormatterFields(enumerable, visited))
                    {
                        yield return dependency;
                    }
                }
            }
        }

        yield return new LuminLocalFieldData
        {
            TypeName = FormatterTypeName.Get(type),
            TypeSymbol = type,
            Name = "value",
            IsValue = type.IsValueType
        };
    }

    /// <summary>Determines whether the metadata contains type parameter.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal static bool ContainsTypeParameter(ITypeSymbol? type)
    {
        if (type is null || type is ITypeParameterSymbol)
        {
            return type is ITypeParameterSymbol;
        }

        return type switch
        {
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            INamedTypeSymbol named => named.IsUnboundGenericType || ContainsTypeParameter(named.ContainingType) || named.TypeArguments.Any(ContainsTypeParameter),
            _ => false
        };
    }

    /// <summary>Determines whether the supplied value has the aot visible classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal static bool IsAotVisible(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return IsAotVisible(array.ElementType);
        }

        if (type is not INamedTypeSymbol named || named.IsAnonymousType)
        {
            return !type.IsAnonymousType;
        }

        if (named.TypeArguments.Any(argument => !IsAotVisible(argument)))
        {
            return false;
        }

        for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Obtains the global type name for the supplied input.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetGlobalTypeName(LuminDataInfo data)
    {
        string typeName = data.classFullName;
        return typeName.IndexOf('.') < 0 && data.classNameSpace != "<global namespace>"
            ? "global::" + data.classNameSpace + "." + typeName
            : typeName;
    }

    /// <summary>Builds the generic constraints source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    internal static void AppendGenericConstraints(StringBuilder sb, LuminDataInfo data)
    {
        foreach (GenericParameterConstraint constraint in data.GenericConstraints)
        {
            List<string> parts = new List<string>();
            if (constraint.IsUnmanaged) parts.Add("unmanaged");
            if (constraint.IsClass) parts.Add("class");
            if (constraint.IsStruct) parts.Add("struct");
            if (constraint.IsNotNull) parts.Add("notnull");
            parts.AddRange(constraint.Constraints);
            if (constraint.HasNewConstructor) parts.Add("new()");
            if (constraint.HasDefault) parts.Add("default");
            if (parts.Count > 0) sb.AppendLine("            where " + constraint.ParameterName + " : " + string.Join(", ", parts));
        }
    }

}
