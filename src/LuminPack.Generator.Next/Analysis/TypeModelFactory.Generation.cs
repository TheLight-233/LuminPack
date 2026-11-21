using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    // Keep the retired numeric values internal so metadata with the old constructor can still describe
    // its original format when the compiler supplies that constructor.
    internal enum PayloadGeneration : byte
    {
        Default = 0,
        Version = 1,
        LegacyTrackedVersion = 2,
        LegacyManual = 3,
        Custom = 4,
    }

    /// <summary>Checks for a declared or inherited LuminPackable marker.</summary>
    /// <param name="symbol">The candidate declaration.</param>
    /// <returns>Whether the declaration has the marker.</returns>
    internal static bool IsPackable(INamedTypeSymbol symbol) => HasDirectPackableMarker(symbol) || InheritsPackableMarker(symbol);

    /// <summary>Identifies metadata whose original serialization selection the compiler cannot decode.</summary>
    /// <param name="symbol">The declaration that may carry a marker compiled against an earlier runtime.</param>
    /// <returns>Whether the imported marker constructor is unavailable.</returns>
    private static bool HasUnresolvedImportedMarker(INamedTypeSymbol symbol)
    {
        if (symbol.DeclaringSyntaxReferences.Length != 0)
            return false;
        foreach (AttributeData attribute in symbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == PackableAttribute && attribute.AttributeConstructor == null)
                return true;
        return false;
    }

    /// <summary>Reads the generation mode declared by the type itself.</summary>
    /// <param name="symbol">The declaration whose contract is being analyzed.</param>
    /// <param name="fallback">The mode inherited from a consuming-assembly option, when present.</param>
    /// <returns>The current option, the bound legacy mode, or the default representation.</returns>
    internal static PayloadGeneration GenerationMode(INamedTypeSymbol symbol, PayloadGeneration fallback = PayloadGeneration.Default)
    {
        PayloadGeneration legacy = fallback;
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            string? name = attribute.AttributeClass?.ToDisplayString();
            if (name == OptionAttribute && IsUntargetedOption(attribute))
            {
                foreach (var argument in attribute.NamedArguments)
                    if (argument.Key == "GeneratorType" && argument.Value.Value is byte value && IsCurrentGenerationMode(value))
                        return (PayloadGeneration)value;
            }
            else if (name == PackableAttribute && attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Kind == TypedConstantKind.Enum &&
                attribute.ConstructorArguments[0].Value is byte value && value <= (byte)PayloadGeneration.Custom)
            {
                legacy = (PayloadGeneration)value;
            }
        }
        return legacy;
    }

    /// <summary>Validates the target and selected generation mode for a declaration option.</summary>
    /// <param name="symbol">The option's target.</param>
    /// <param name="argument">The declared enum constant.</param>
    /// <param name="assemblyMarker">Whether an assembly marker declares the referenced payload.</param>
    /// <returns>The diagnostic reason for an invalid option, or null.</returns>
    internal static string? GenerationOptionError(ISymbol symbol, TypedConstant argument, bool assemblyMarker = false)
    {
        if (symbol is not INamedTypeSymbol type || !assemblyMarker && !IsPackable(type))
            return "GeneratorType applies only to LuminPackable type declarations.";
        if (argument.Value is not byte value || !IsCurrentGenerationMode(value))
            return "Select GeneratorType.Default, GeneratorType.Version or GeneratorType.Custom.";
        if (value == (byte)PayloadGeneration.Version && (type.IsAbstract || HasAttribute(type, UnionAttribute)))
            return "Version applies to record declarations. Set it on individual Union cases.";
        return null;
    }

    /// <summary>Checks whether a numeric value is exposed by the current enum.</summary>
    /// <param name="value">The declared enum constant.</param>
    /// <returns>Whether the value selects a current mode.</returns>
    private static bool IsCurrentGenerationMode(byte value) =>
        (PayloadGeneration)value is PayloadGeneration.Default or PayloadGeneration.Version or PayloadGeneration.Custom;
}
