using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private const string IncludeDerivedTypesOption = "IncludeDerivedTypes";

    /// <summary>Checks for a LuminPackable marker declared on the type itself.</summary>
    /// <param name="type">The declaration that may own a marker.</param>
    /// <returns>Whether the type declares its own marker.</returns>
    internal static bool HasDirectPackableMarker(INamedTypeSymbol type) => HasAttribute(type, PackableAttribute);

    /// <summary>Finds an explicitly marked class ancestor that includes its descendants.</summary>
    /// <param name="type">The prospective descendant.</param>
    /// <returns>Whether the base chain supplies LuminPackable membership.</returns>
    internal static bool InheritsPackableMarker(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class)
            return false;
        for (INamedTypeSymbol? parent = type.BaseType;
            parent is { TypeKind: TypeKind.Class, SpecialType: not SpecialType.System_Object };
            parent = parent.BaseType)
        {
            INamedTypeSymbol definition = parent.OriginalDefinition;
            if (HasDirectPackableMarker(definition) && OptionSwitch(definition, IncludeDerivedTypesOption))
                return true;
        }
        return false;
    }

    /// <summary>Validates a declaration that states the descendant inclusion option.</summary>
    /// <param name="target">The attributed declaration.</param>
    /// <param name="argument">The option value bound by the compiler.</param>
    /// <returns>A reason when the declaration cannot own the option.</returns>
    internal static string? DerivedInclusionOptionError(ISymbol target, TypedConstant argument)
    {
        if (argument.Value is not bool)
            return "IncludeDerivedTypes requires a Boolean value.";
        if (target is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsStatic: false } type || !IsPackable(type))
            return "IncludeDerivedTypes applies only to LuminPackable class declarations.";
        return null;
    }
}
