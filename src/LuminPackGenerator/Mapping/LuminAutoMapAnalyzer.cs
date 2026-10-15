using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.Mapping;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

// ═════════════════════════════════════════════════════════════════════════════
//  Automatic mapping analyzer: LuminMapTo to AutoMapSourceInfo.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Provides lumin auto map analyzer functionality.</summary>
public static class LuminAutoMapAnalyzer
{
    private const string LuminMapToAttr = "LuminPack.Attribute.LuminMapToAttribute";

    /// <summary>Analyzes  and records the resulting metadata or diagnostics.</summary>
    /// <param name="srcSymbol">The src symbol used by this operation.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    public static AutoMapSourceInfo? Analyze(INamedTypeSymbol srcSymbol, Compilation compilation)
    {
        var mapToAttrs = srcSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == LuminMapToAttr)
            .ToList();
        if (mapToAttrs.Count == 0) return null;

        var info = new AutoMapSourceInfo
        {
            SourceClassName = srcSymbol.Name,
            SourceNamespace = srcSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            SourceFullName = srcSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsGlobalNamespace = srcSymbol.ContainingNamespace?.IsGlobalNamespace ?? true,
            IsUnityProject = TypeMetaChecker.IsUnityProject(compilation),
        };

        foreach (var attr in mapToAttrs)
        {
            if (attr.ConstructorArguments.IsEmpty) continue;
            var destType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
            if (destType == null) continue;

            string? overrideSuffix = null;
            bool genCollections = true;
            foreach (var na in attr.NamedArguments)
            {
                if (na.Key == "MethodSuffix") overrideSuffix = na.Value.Value as string;
                if (na.Key == "GenerateCollectionMethods" && na.Value.Value is bool b) genCollections = b;
            }

            string suffix = overrideSuffix ?? InferSuffix(srcSymbol.Name, destType.Name);

            var pair = new AutoMapPairInfo
            {
                SourceTypeFullName = srcSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestTypeFullName = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestIsValueType = destType.IsValueType,
                DestHasDefaultCtor = LuminMapBindingAnalyzer.HasDefaultCtor(destType),
                MethodSuffix = suffix,
                GenerateCollectionMethods = genCollections,
            };
            pair.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcSymbol, destType));
            info.Pairs.Add(pair);
        }

        return info;
    }

    // ── Bug fix: Infer suffixes at PascalCase word boundaries. ────────────
    //
    //   "PlayerData" → ["Player","Data"],  "PlayerDto" → ["Player","Dto"]
    //   Shared prefix [Player], remaining destination word [Dto], resulting suffix Dto and method ToDto.
    //
    //   Character-prefix comparison would incorrectly infer PlayerD, suffix to, and method Toto.
    //
    /// <summary>Performs the infer suffix step for the current operation.</summary>
    /// <param name="srcName">The src name used by this operation.</param>
    /// <param name="destName">The dest name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string InferSuffix(string srcName, string destName)
    {
        var srcWords = SplitPascalCase(srcName);
        var destWords = SplitPascalCase(destName);

        int common = 0;
        int limit = Math.Min(srcWords.Count, destWords.Count);
        while (common < limit && srcWords[common] == destWords[common])
            common++;

        string suffix = string.Concat(destWords.Skip(common));
        // Use the destination type name when no distinct suffix can be inferred.
        return string.IsNullOrEmpty(suffix) ? destName : suffix;
    }

    /// <summary>Performs the split pascal case step for the current operation.</summary>
    /// <param name="name">The name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static List<string> SplitPascalCase(string name)
    {
        var words = new List<string>();
        int start = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                words.Add(name.Substring(start, i - start));
                start = i;
            }
        }
        if (start < name.Length)
            words.Add(name.Substring(start));
        return words;
    }
}
