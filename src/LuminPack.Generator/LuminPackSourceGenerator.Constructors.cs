using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator
{
        public sealed partial class LuminPackSourceGenerator : IIncrementalGenerator
    {

        /// <summary>
        /// Analyzes constructors using the same selection rules for root and nested types.
        /// </summary>
        /// <param name="typeSymbol">The type or symbol to analyze.</param>
        /// <param name="availableFields">The available fields used by this operation.</param>
        /// <param name="location">The location used by this operation.</param>
        /// <param name="isNestedClass">The is nested class used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private static LuminConstructorData AnalyzeTypeConstructors(
            INamedTypeSymbol typeSymbol,
            List<LuminDataField> availableFields,
            Location location,
            bool isNestedClass = false)
        {
            var instanceConstructors = typeSymbol.Constructors
                .Where(c => !c.IsStatic)
                .ToList();

            // Collect all explicitly declared public instance constructors.
            var explicitConstructors = typeSymbol.Constructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public &&
                            !c.IsStatic &&
                            !c.IsImplicitlyDeclared)
                .ToList();

            // Handle types without explicitly declared constructors.
            if (explicitConstructors.Count == 0)
            {
                // Structs provide an implicit parameterless constructor.
                if (typeSymbol.TypeKind == TypeKind.Struct)
                {
                    return new LuminConstructorData
                    {
                        Accessibility = Accessibility.Public,
                        IsMarkedWithAttribute = false,
                        Parameters = new List<ConstructorParameter>()
                    };
                }

                // Use the implicit parameterless constructor when a class declares no constructor.
                if (typeSymbol.TypeKind == TypeKind.Class)
                {
                    if (instanceConstructors.Any(c => !c.IsImplicitlyDeclared))
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.NoPublicConstructor,
                            location,
                            typeSymbol.Name));
                        return null;
                    }

                    return new LuminConstructorData
                    {
                        Accessibility = Accessibility.Public,
                        IsMarkedWithAttribute = false,
                        Parameters = new List<ConstructorParameter>()
                    };
                }

                return null;
            }

            // Collect available member names with case-insensitive matching.
            var allFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in availableFields)
            {
                allFieldNames.Add(field.Name);
            }

            var allConstructorData = new List<LuminConstructorData>();

            // Analyze each explicit constructor.
            foreach (var constructor in explicitConstructors)
            {
                var constructorData = new LuminConstructorData
                {
                    Accessibility = constructor.DeclaredAccessibility,
                    IsMarkedWithAttribute = constructor.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackConstructorAttribute")
                };

                foreach (var parameter in constructor.Parameters)
                {
                    var paramData = new ConstructorParameter
                    {
                        Name = parameter.Name,
                        Type = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        TypeNamespace = parameter.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace"
                    };

                    // Check parameter names against serialized member names.
                    var matchingField = availableFields.FirstOrDefault(f =>
                        string.Equals(f.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

                    if (matchingField != null)
                    {
                        paramData.MatchesField = true;
                        paramData.MatchingFieldName = matchingField.Name;
                    }
                    else
                    {
                        paramData.MatchesField = false;
                    }

                    constructorData.Parameters.Add(paramData);
                }

                allConstructorData.Add(constructorData);
            }

            // Select the construction strategy.
            var markedConstructors = allConstructorData.Where(c => c.IsMarkedWithAttribute).ToList();

            LuminConstructorData selectedConstructor = null;

            if (markedConstructors.Count > 1)
            {
                // Report multiple annotated constructors.
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.MultipleConstructorsRequireAttribute,
                    location,
                    typeSymbol.Name
                ));
                return null;
            }
            else if (markedConstructors.Count == 1)
            {
                // Use the annotated constructor.
                selectedConstructor = markedConstructors[0];
            }
            else
            {
                // Select an unannotated constructor only when the choice is unambiguous.
                if (allConstructorData.Count == 1)
                {
                    // Select the single explicit constructor automatically.
                    selectedConstructor = allConstructorData[0];
                }
                else if (allConstructorData.Count > 1)
                {
                    // Report multiple explicit constructors without an annotated selection.
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.MultipleConstructorsRequireAttribute,
                        location,
                        typeSymbol.Name
                    ));
                    return null;
                }
                // With no explicit constructors, use the implicit parameterless constructor handled above.
            }

            // Validate member matching for the selected constructor's non-empty parameter list.
            if (selectedConstructor != null && selectedConstructor.Parameters.Count > 0)
            {
                ValidateConstructorParameters(selectedConstructor, typeSymbol, allFieldNames, location, isNestedClass);
            }

            return selectedConstructor;
        }

        /// <summary>
        /// Validates constructor parameter names against serialized members.
        /// </summary>
        /// <param name="constructor">The constructor used by this operation.</param>
        /// <param name="typeSymbol">The type or symbol to analyze.</param>
        /// <param name="fieldNames">The field names used by this operation.</param>
        /// <param name="location">The location used by this operation.</param>
        /// <param name="isNestedClass">The is nested class used by this operation.</param>
        private static void ValidateConstructorParameters(
            LuminConstructorData constructor,
            INamedTypeSymbol typeSymbol,
            HashSet<string> fieldNames,
            Location location,
            bool isNestedClass = false)
        {
            // Parameterless constructors need no member matching.
            if (constructor.Parameters.Count == 0)
                return;

            foreach (var parameter in constructor.Parameters)
            {
                if (!fieldNames.Contains(parameter.Name))
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorParameterNameMismatch,
                        location,
                        parameter.Name, typeSymbol.Name
                    ));
                }
            }
        }

        /// <summary>Analyzes main class constructors and records the resulting metadata or diagnostics.</summary>
        /// <param name="typeSymbol">The type or symbol to analyze.</param>
        /// <param name="dataInfo">The metadata describing the serializable type.</param>
        private static void AnalyzeMainClassConstructors(INamedTypeSymbol typeSymbol, LuminDataInfo dataInfo)
        {
            dataInfo.SelectedConstructor = AnalyzeTypeConstructors(
                typeSymbol,
                dataInfo.fields,
                typeSymbol.Locations.FirstOrDefault(),
                isNestedClass: false);

            // Collect constructor metadata for diagnostics and inspection.
            dataInfo.AllConstructors = typeSymbol.Constructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
                .Select(constructor => new LuminConstructorData
                {
                    Accessibility = constructor.DeclaredAccessibility,
                    IsMarkedWithAttribute = constructor.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackConstructorAttribute"),
                    Parameters = constructor.Parameters.Select(parameter => new ConstructorParameter
                    {
                        Name = parameter.Name,
                        Type = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        TypeNamespace = parameter.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                        MatchesField = dataInfo.fields.Any(f =>
                            string.Equals(f.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
                    }).ToList()
                }).ToList();
        }

        /// <summary>
        /// Analyzes the constructors of a nested type.
        /// </summary>
        /// <param name="typeSymbol">The type or symbol to analyze.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        private static void AnalyzeNestedClassConstructors(INamedTypeSymbol typeSymbol, LuminDataField field)
        {
            try
            {
                field.SelectedConstructor = AnalyzeTypeConstructors(
                    typeSymbol,
                    field.ClassFields,
                    _currentSymbol?.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                    isNestedClass: true);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Nested class"))
            {
                // Mark the nested type as unsupported when constructor parameters do not match its members.
                field.Type = LuminFiledType.Other;
                TypeMetaChecker.CheckFieldIsLuminPackable(typeSymbol, _currentSymbol?.Locations.FirstOrDefault());
            }
        }

        /// <summary>
        /// Validates the accessibility of a nested type.
        /// </summary>
        /// <param name="nestedType">The nested type used by this operation.</param>
        private static void CheckNestedClassAccessibility(INamedTypeSymbol nestedType)
        {
            if (nestedType.DeclaredAccessibility != Accessibility.Public &&
                nestedType.DeclaredAccessibility != Accessibility.Internal)
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.NestedClassMustBePublicOrInternal,
                    nestedType.Locations.FirstOrDefault() ?? _location,
                    nestedType.Name
                ));

                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.NestedClassAccessibilityError,
                    nestedType.Locations.FirstOrDefault() ?? _location,
                    nestedType.Name,
                    nestedType.DeclaredAccessibility.ToString()
                ));
            }
        }
    }
}
