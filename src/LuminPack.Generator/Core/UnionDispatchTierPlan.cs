using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>The dispatch tier selected for a Union root's compiled write slot.</summary>
internal enum UnionDispatchTier
{
    /// <summary>The original single virtual call.</summary>
    Virtual,

    /// <summary>Exact type tests for each generated case, then the original virtual call for every other type.</summary>
    Guard,

    /// <summary>A direct call of the only possible case's slot, without a type test.</summary>
    Direct,
}

/// <summary>
/// Selects the tiered Union write dispatch: a direct call when exactly one case can exist and dynamic registration
/// is not generated, exact type guards before the virtual fallback for one to four cases, and the original virtual
/// call for larger Unions.
/// </summary>
internal sealed class UnionDispatchTierPlan
{
    /// <summary>The largest case count that receives exact type guards.</summary>
    internal const int MaximumGuardedCases = 4;

    private static readonly UnionDispatchTierPlan VirtualPlan = new(UnionDispatchTier.Virtual, new List<(string, bool)>());
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<INamedTypeSymbol, UnionDispatchTierPlan>> Cache = new();

    /// <summary>The selected tier.</summary>
    internal readonly UnionDispatchTier Tier;

    /// <summary>The fully qualified generated case types in tag order, and whether each is sealed.</summary>
    internal readonly List<(string Type, bool Sealed)> Cases;

    /// <summary>Captures an immutable decision.</summary>
    /// <param name="tier">The selected tier.</param>
    /// <param name="cases">The guarded or direct cases.</param>
    private UnionDispatchTierPlan(UnionDispatchTier tier, List<(string Type, bool Sealed)> cases)
    {
        Tier = tier;
        Cases = cases;
    }

    /// <summary>Obtains the tier of a local class Union root.</summary>
    /// <param name="root">The Union root whose compiled slot is emitted.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>The cached decision; <see cref="UnionDispatchTier.Virtual"/> when no other tier is provably correct.</returns>
    internal static UnionDispatchTierPlan Get(INamedTypeSymbol root, Compilation compilation)
    {
        if (root is not { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } ||
            !LuminPackUnionDispatchUtilities.CanGeneratePartial(root, compilation)) return VirtualPlan;
        return Cache.GetValue(compilation, _ => new ConcurrentDictionary<INamedTypeSymbol, UnionDispatchTierPlan>(SymbolEqualityComparer.Default))
            .GetOrAdd(root.OriginalDefinition, current => Build(current, compilation));
    }

    /// <summary>Analyzes the root's generated cases and registration settings.</summary>
    /// <param name="root">The Union root.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>The selected plan.</returns>
    private static UnionDispatchTierPlan Build(INamedTypeSymbol root, Compilation compilation)
    {
        var metadata = CreateMetadata(compilation);
        var data = LuminPackSourceGenerator.GetSerializationMetadata(root, compilation, metadata);
        if (!data.isUnion || !data.CanGenerateUnionDispatch || data.UnionMembers.Count is 0 or > MaximumGuardedCases) return VirtualPlan;
        var types = new List<INamedTypeSymbol>();
        foreach (var member in data.UnionMembers.OrderBy(member => member.Id))
        {
            // Every guarded case must own the generated sealed override that the guard calls directly.
            if (member.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } type ||
                !LuminPackUnionDispatchCodeGenerator.IsFastPathMember(data, member) ||
                !LuminPackUnionDispatchUtilities.CanGeneratePartial(type, compilation) ||
                types.Contains(type, SymbolEqualityComparer.Default)) return VirtualPlan;
            types.Add(type);
        }
        var cases = types.Select(type => (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), type.IsSealed)).ToList();
        bool registration = metadata.AllowUnsafe && metadata.RegisterMode != LuminPackRegisterMode.Disabled;
        if (!registration && types.Count == 1 && types[0].IsSealed && IsClosed(root, compilation) && HasOnlyDerivedType(root, types[0], compilation))
            return new UnionDispatchTierPlan(UnionDispatchTier.Direct, cases);
        return new UnionDispatchTierPlan(UnionDispatchTier.Guard, cases);
    }

    /// <summary>Emits the selected dispatch of one compiled profile slot.</summary>
    /// <param name="slot">The profile slot name.</param>
    /// <returns>A statement block that calls the slot and returns.</returns>
    internal string Dispatch(string slot)
    {
        if (Tier == UnionDispatchTier.Direct)
            return "{ global::System.Runtime.CompilerServices.Unsafe.As<" + Cases[0].Type + ">(value)." + slot + "(ref writer); return; }";
        var source = new System.Text.StringBuilder("{ ");
        if (Tier == UnionDispatchTier.Guard)
            foreach (var (type, sealedType) in Cases)
                // A sealed case's type test is exact; other cases compare the exact runtime type.
                source.Append("if (").Append(sealedType ? "value is " + type : "value.GetType() == typeof(" + type + ")")
                    .Append(") { global::System.Runtime.CompilerServices.Unsafe.As<").Append(type).Append(">(value).").Append(slot).Append("(ref writer); return; } ");
        return source.Append("value.").Append(slot).Append("(ref writer); return; }").ToString();
    }

    /// <summary>Checks that no other assembly can add a case to the root.</summary>
    /// <param name="root">The Union root.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>Whether every implementation must be declared in this compilation.</returns>
    private static bool IsClosed(INamedTypeSymbol root, Compilation compilation)
    {
        if (TypeMetaChecker.HasUnionOption(root, "SingleImplementation") || root.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackSingleImplementationAttribute")) return true;
        if (compilation.Assembly.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")) return false;
        for (var current = root; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal)) return true;
        // An externally visible root stays closed only when no constructor is accessible from another assembly.
        return root.InstanceConstructors.All(constructor =>
            constructor.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal));
    }

    /// <summary>Checks that the case is the only type in this compilation deriving from the root.</summary>
    /// <param name="root">The Union root.</param>
    /// <param name="only">The single generated case.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>Whether no other source type derives from the root.</returns>
    private static bool HasOnlyDerivedType(INamedTypeSymbol root, INamedTypeSymbol only, Compilation compilation)
    {
        foreach (var symbol in compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type))
        {
            if (symbol is not INamedTypeSymbol type || SymbolEqualityComparer.Default.Equals(type, only)) continue;
            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
                if (SymbolEqualityComparer.Default.Equals(parent.OriginalDefinition, root)) return false;
        }
        return true;
    }

    /// <summary>Creates the settings snapshot used by Union analysis outside a generator pipeline.</summary>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>The equivalent generation settings.</returns>
    private static MetaInfo CreateMetadata(Compilation compilation)
    {
        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions ?? CSharpParseOptions.Default;
        return new MetaInfo(options, options.LanguageVersion, options.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER"),
            options.PreprocessorSymbolNames.Contains("NET9_0_OR_GREATER"),
            compilation.Options is CSharpCompilationOptions { AllowUnsafe: true }, LuminPackGenerationTierResolver.RegisterModeFromAssembly(compilation));
    }
}
