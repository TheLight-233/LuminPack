using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Certifies narrow class Union cases whose existing exact instances can be refilled without user code.</summary>
internal static class DictionaryUnionSlotPlan
{
    /// <summary>Selects cases from the canonical fixed Union cursor analysis plus read-side constructor and callback exclusions.</summary>
    /// <param name="type">The exact dictionary value type.</param>
    /// <param name="compilation">The compilation owning all analyzed symbols.</param>
    /// <param name="metadata">The current language and emission settings.</param>
    /// <returns>The eligible tags and case type names, or null for the original staged operation.</returns>
    internal static List<(int Id, string Type)> GetCases(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        if (!metadata.IsNet8 || metadata.IsForUnity ||
            metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", System.StringComparison.Ordinal))) return null;
        var plan = FixedUnionCursorPlan.GetRoot(type, compilation, metadata);
        if (plan == null || type is not INamedTypeSymbol root) return null;
        var data = LuminPackSourceGenerator.GetSerializationMetadata(root, compilation, metadata);
        if (!data.isUnion || data.IsWideTag || data.callBackMethods.Count != 0 || data.RentPoolMethod != null) return null;
        var cases = new List<(int Id, string Type)>();
        foreach (var member in data.UnionMembers)
        {
            if (member.Id >= 250 || member.Type is not INamedTypeSymbol { IsAbstract: false, IsGenericType: false } named || !plan.Contains(named)) continue;
            var payload = LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata);
            if (payload.callBackMethods.Count != 0 || payload.RentPoolMethod != null ||
                payload.SelectedConstructor is { Parameters.Count: > 0 }) continue;
            cases.Add((member.Id, named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }
        return cases.Count == 0 ? null : cases;
    }
}
