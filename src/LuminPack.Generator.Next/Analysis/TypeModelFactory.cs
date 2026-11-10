using System;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>The single analysis boundary: Roslyn symbols become <see cref="TypeModel"/> values here.</summary>
/// <remarks>Everything below this boundary is a pure model and never sees a symbol or a compilation. The scaffold classifies
/// the payload kind and its identity; member analysis is the next porting step and deliberately returns an incomplete model.</remarks>
internal static class TypeModelFactory
{
    private const string PackableAttribute = "LuminPack.Attribute.LuminPackableAttribute";
    private const string IgnoreAttribute = "LuminPack.Attribute.LuminPackIgnoreAttribute";

    /// <summary>Builds a model for one attributed type.</summary>
    /// <param name="symbol">The attributed type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>A complete model, an incomplete scaffold or an unsupported result.</returns>
    internal static ModelResult TryCreate(INamedTypeSymbol symbol, Compilation compilation, GeneratorOptions options)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(item =>
            item.AttributeClass?.ToDisplayString() == PackableAttribute);
        if (attribute == null)
            return ModelResult.Unsupported("not a LuminPackable declaration");

        TypeIdentity identity;
        try
        {
            identity = Identity(symbol);
        }
        catch (NotSupportedException error)
        {
            return ModelResult.Unsupported(error.Message);
        }

        TypeAccessibility accessibility = symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public
            ? TypeAccessibility.Public : TypeAccessibility.Internal;
        string[] constraints = symbol.TypeParameters.Select(parameter => parameter.Name).ToArray();
        var policy = WirePolicy.Default;
        PayloadKind kind = Kind(symbol, attribute);

        TypeModel model;
        switch (kind)
        {
            case PayloadKind.Struct:
                model = new StructModel(identity, accessibility, System.Array.Empty<MemberModel>(), policy, constraints,
                    StructStorage.ManagedRecord, layout: null);
                break;
            case PayloadKind.Union:
                model = new UnionModel(identity, accessibility, policy, constraints, System.Array.Empty<UnionCaseModel>(),
                    isInterface: symbol.TypeKind == TypeKind.Interface, wideTags: false, slotSuffix: SlotSuffix(identity),
                    tier: UnionDispatchTier.Virtual);
                break;
            case PayloadKind.CircleReference:
                model = new CircleReferenceModel(identity, accessibility, System.Array.Empty<MemberModel>(), policy, constraints, guarded: false);
                break;
            default:
                model = new RecordModel(identity, accessibility, System.Array.Empty<MemberModel>(), policy, constraints, versioned: false,
                    derived: false, proven: false);
                break;
        }
        // Member analysis, storage decisions and wire policies are the next porting step; until then the scaffold emits nothing.
        return ModelResult.Incomplete(model, "member analysis is not ported yet");
    }

    /// <summary>Builds the identity of a declared type, including its containing chain and parameters.</summary>
    private static TypeIdentity Identity(INamedTypeSymbol symbol)
    {
        var containing = new System.Collections.Generic.List<string>();
        for (var current = symbol.ContainingType; current != null; current = current.ContainingType)
        {
            if (current.IsGenericType)
                throw new NotSupportedException("a payload nested in a generic definition is not modeled yet");
            containing.Insert(0, current.Name);
        }
        string? @namespace = symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace
            ? null : symbol.ContainingNamespace.ToDisplayString();
        return new TypeIdentity(@namespace, containing.ToArray(), symbol.Name, symbol.TypeParameters.Select(parameter => parameter.Name).ToArray());
    }

    /// <summary>Classifies the payload protocol from the declaration and its generator type.</summary>
    private static PayloadKind Kind(INamedTypeSymbol symbol, AttributeData attribute)
    {
        if (symbol.TypeKind == TypeKind.Interface || symbol.IsAbstract)
            return PayloadKind.Union;
        string generatorType = GeneratorTypeName(attribute);
        if (generatorType == "CircleReference")
            return PayloadKind.CircleReference;
        if (symbol.IsValueType)
            return PayloadKind.Struct;
        return PayloadKind.Record;
    }

    /// <summary>Reads the enum member name of the packable attribute's generation mode.</summary>
    private static string GeneratorTypeName(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value == null)
            return "Object";
        var argument = attribute.ConstructorArguments[0];
        if (argument.Type is not INamedTypeSymbol enumType)
            return "Object";
        int value = Convert.ToInt32(argument.Value, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
            if (field.HasConstantValue && Convert.ToInt32(field.ConstantValue, System.Globalization.CultureInfo.InvariantCulture) == value)
                return field.Name;
        return "Object";
    }

    /// <summary>Spells the canonical Union slot suffix for an identity.</summary>
    private static string SlotSuffix(TypeIdentity identity)
    {
        // The final spelling is a stable hash of the metadata name; the scaffold keeps the readable portion so the shape is visible.
        return identity.Name;
    }
}
