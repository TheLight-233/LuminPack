using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>The dispatch tier selected for a Union root's compiled write slot.</summary>
internal enum UnionDispatchTier
{
    /// <summary>The original single virtual call.</summary>
    Virtual,

    /// <summary>Exact type patterns for the generated cases with a virtual fallback.</summary>
    Pattern,

    /// <summary>A direct call of the only possible case's slot, without a type test.</summary>
    Direct,
}

/// <summary>
/// Selects one write dispatch plan for each eligible local Union root and target framework.
/// </summary>
internal sealed class UnionDispatchTierPlan
{
    /// <summary>The largest case count that uses patterns on modern targets.</summary>
    private const int MaximumModernPatternCases = 4;

    private static readonly UnionDispatchTierPlan VirtualPlan = new(UnionDispatchTier.Virtual, new List<(string, bool)>());
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<INamedTypeSymbol, UnionDispatchTierPlan>> Cache = new();
    private static readonly ConditionalWeakTable<Compilation, SourceClosure> Closures = new();

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
            !LuminPackUnionDispatchUtilities.CanGeneratePartial(root, compilation))
            return VirtualPlan;
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
        if (!data.isUnion || !data.CanGenerateUnionDispatch || data.UnionMembers.Count == 0 ||
            metadata.IsNet8 && data.UnionMembers.Count > MaximumModernPatternCases)
            return VirtualPlan;
        var types = new List<INamedTypeSymbol>(data.UnionMembers.Count);
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in data.UnionMembers.OrderBy(member => member.Id))
        {
            // Each pattern needs a concrete case with a generated slot that can be called on its exact type.
            if (member.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } type ||
                !LuminPackUnionDispatchCodeGenerator.IsFastPathMember(data, member) ||
                !LuminPackUnionDispatchUtilities.CanGeneratePartial(type, compilation) ||
                !seen.Add(type))
                return VirtualPlan;
            types.Add(type);
        }
        var cases = types.Select(type => (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), type.IsSealed)).ToList();
        if (CanCallDirect(metadata) && types.Count == 1 && types[0].IsSealed && IsClosed(root, compilation) &&
            !GetClosure(compilation).MultipleDescendants.Contains(root))
            return new UnionDispatchTierPlan(UnionDispatchTier.Direct, cases);
        return new UnionDispatchTierPlan(UnionDispatchTier.Pattern, cases);
    }

    /// <summary>Emits the selected dispatch of one compiled profile slot.</summary>
    /// <param name="slot">The profile slot name.</param>
    /// <returns>A statement block that calls the slot and returns.</returns>
    internal string Dispatch(string slot)
    {
        if (Tier == UnionDispatchTier.Direct)
            return "{ global::System.Runtime.CompilerServices.Unsafe.As<" + Cases[0].Type + ">(value)." + slot + "(ref writer); return; }";
        string fallback = "value." + slot + "(ref writer); return;";
        if (Tier == UnionDispatchTier.Virtual)
            return "{ " + fallback + " }";
        var source = new StringBuilder().AppendLine("{");
        AppendPatternSwitch(source, "    ", "value", (_, value) => value + "." + slot + "(ref writer); return;", fallback);
        return source.Append('}').ToString();
    }

    /// <summary>Applies the root's tier to the existing dense dictionary cursor protocol.</summary>
    /// <param name="source">The dictionary operation receiving the dispatch.</param>
    /// <param name="indent">The indentation of the case selection.</param>
    /// <param name="suffix">The root's original cursor slot suffix.</param>
    /// <param name="fixedCases">The exact cases eligible for the fixed cursor kernel, in tag order.</param>
    internal void AppendCursorDispatch(StringBuilder source, string indent, string suffix, IReadOnlyList<(int Id, string Type)> fixedCases)
    {
        string VirtualCall(string value) => $"nextIndex = {value}.LuminPackUnionSerializeCursor_{suffix}(ref writer, nextIndex);";
        if (Tier == UnionDispatchTier.Virtual)
        {
            source.Append(indent).Append("else ").AppendLine(VirtualCall("itemValue"));
            return;
        }

        var tags = fixedCases.ToDictionary(item => item.Type, item => item.Id, StringComparer.Ordinal);
        string CaseCall(string type, string value) => tags.TryGetValue(type, out int id)
            ? $"nextIndex = global::LuminPack.LuminPackSerializer.SerializeUnionAt(ref writer, {value}, nextIndex, (byte){id});"
            : VirtualCall(value);

        if (Tier == UnionDispatchTier.Direct)
        {
            string type = Cases[0].Type;
            source.Append(indent).Append("else ").AppendLine(CaseCall(type, $"global::System.Runtime.CompilerServices.Unsafe.As<{type}>(itemValue)"));
        }
        else
        {
            source.Append(indent).AppendLine("else");
            source.Append(indent).AppendLine("{");
            AppendPatternSwitch(source, indent + "    ", "itemValue", (type, value) => CaseCall(type, value) + " break;",
                VirtualCall("itemValue") + " break;");
            source.Append(indent).AppendLine("}");
        }
    }

    /// <summary>Writes the root's exact type cases in tag order before the original virtual fallback.</summary>
    /// <param name="source">The generated operation.</param>
    /// <param name="indent">The switch indentation.</param>
    /// <param name="value">The already non-null root reference.</param>
    /// <param name="writeCase">The typed payload call and control flow for a case.</param>
    /// <param name="fallback">The original virtual call and control flow.</param>
    private void AppendPatternSwitch(StringBuilder source, string indent, string value, Func<string, string, string> writeCase, string fallback)
    {
        source.Append(indent).AppendLine("// Union cases");
        source.Append(indent).Append("switch (").Append(value).AppendLine(")");
        source.Append(indent).AppendLine("{");
        for (int index = 0; index < Cases.Count; index++)
        {
            var (type, sealedType) = Cases[index];
            string variable = "caseValue" + index;
            source.Append(indent).Append("    case ").Append(type).Append(' ').Append(variable);
            if (!sealedType)
                source.Append(" when ((object)").Append(value).Append(").GetType() == typeof(").Append(type).Append(')');
            source.Append(": ").AppendLine(writeCase(type, variable));
        }
        source.Append(indent).AppendLine("    default:");
        source.Append(indent).Append("        ").AppendLine(fallback);
        source.Append(indent).AppendLine("}");
    }

    /// <summary>Checks that no other assembly can add a case to the root.</summary>
    /// <param name="root">The Union root.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>Whether every implementation must be declared in this compilation.</returns>
    private static bool IsClosed(INamedTypeSymbol root, Compilation compilation)
    {
        if (TypeMetaChecker.HasUnionOption(root, "SingleImplementation") || root.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackSingleImplementationAttribute"))
            return true;
        if (compilation.Assembly.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute"))
            return false;
        for (var current = root; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                return true;
        // An externally visible root stays closed only when no constructor is accessible from another assembly.
        return root.InstanceConstructors.All(constructor =>
            constructor.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal));
    }

    /// <summary>Captures source inheritance facts that can change direct dispatch without editing the Union declarations.</summary>
    /// <param name="compilation">The active compilation.</param>
    /// <param name="metadata">The active target and registration settings.</param>
    /// <returns>A stable description of the potentially open roots.</returns>
    internal static string ClosureGuard(Compilation compilation, MetaInfo metadata = null) =>
        CanCallDirect(metadata ?? CreateMetadata(compilation)) ? GetClosure(compilation).Guard : string.Empty;

    private static bool CanCallDirect(MetaInfo metadata) =>
        metadata.IsNet8 && metadata.RegisterMode == LuminPackRegisterMode.Disabled;

    private static SourceClosure GetClosure(Compilation compilation) =>
        Closures.GetValue(compilation, static current => new SourceClosure(current));

    /// <summary>Indexes derived source types once, including types without a packable declaration.</summary>
    private sealed class SourceClosure
    {
        internal readonly HashSet<INamedTypeSymbol> MultipleDescendants = new(SymbolEqualityComparer.Default);
        internal readonly string Guard;

        internal SourceClosure(Compilation compilation)
        {
            var analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
            var roots = new HashSet<INamedTypeSymbol>(analysis.PackableTypes.Where(type =>
                type is { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } &&
                SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)), SymbolEqualityComparer.Default);
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var declaration in analysis.DeclaredTypes)
                for (var parent = declaration.Symbol.BaseType; parent != null; parent = parent.BaseType)
                    if (roots.Contains(parent) && !seen.Add(parent))
                        MultipleDescendants.Add(parent);
            Guard = string.Join("\n", MultipleDescendants.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .OrderBy(name => name, StringComparer.Ordinal));
        }
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
