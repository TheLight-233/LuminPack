using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.Mapping;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

// ═════════════════════════════════════════════════════════════════════════════
//  Manual mapping analyzer: LuminMapper partial class to LuminMapperClassInfo.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Provides lumin map analyzer functionality.</summary>
public static class LuminMapAnalyzer
{
    /// <summary>Analyzes  and records the resulting metadata or diagnostics.</summary>
    /// <param name="mapperSymbol">The mapper symbol used by this operation.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    public static LuminMapperClassInfo Analyze(
        INamedTypeSymbol mapperSymbol, Compilation compilation)
    {
        var info = new LuminMapperClassInfo
        {
            ClassName = mapperSymbol.Name,
            ClassNamespace = mapperSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            ClassFullName = mapperSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsGlobalNamespace = mapperSymbol.ContainingNamespace?.IsGlobalNamespace ?? true,
            IsUnityProject = TypeMetaChecker.IsUnityProject(compilation),
        };

        foreach (var m in mapperSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (!m.IsStatic || !m.IsPartialDefinition) continue;
            var mi = AnalyzeMethod(m);
            if (mi != null) info.Methods.Add(mi);
        }
        return info;
    }

    /// <summary>Analyzes method and records the resulting metadata or diagnostics.</summary>
    /// <param name="method">The method used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static LuminMapMethodInfo? AnalyzeMethod(IMethodSymbol method)
    {
        bool sourceIsIn = method.Parameters.Length > 0
                          && method.Parameters[0].RefKind == RefKind.In;

        // Creating mapping: TDest Method([in] TSource source).
        if (method.ReturnType.SpecialType != SpecialType.System_Void
            && method.Parameters.Length == 1)
        {
            var srcType = method.Parameters[0].Type as INamedTypeSymbol;
            var destType = method.ReturnType as INamedTypeSymbol;
            if (srcType == null || destType == null) return null;

            // Collection mapping.
            var (dstColl, destElem) = LuminMapBindingAnalyzer.DetectCollection(destType);
            var (srcColl, srcElem) = LuminMapBindingAnalyzer.DetectCollection(srcType);

            if (dstColl != CollectionMapKind.None && srcColl != CollectionMapKind.None
                && destElem != null && srcElem != null)
            {
                var elemMi = new LuminMapMethodInfo
                {
                    MethodName = method.Name + "_elem",
                    SourceTypeFullName = srcElem.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    DestTypeFullName = destElem.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    SourceIsIn = false,
                    IsCreateNew = true,
                    DestIsValueType = destElem.IsValueType,
                    DestHasDefaultCtor = LuminMapBindingAnalyzer.HasDefaultCtor(destElem),
                };
                elemMi.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcElem, destElem));

                return new LuminMapMethodInfo
                {
                    MethodName = method.Name,
                    SourceTypeFullName = srcType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    DestTypeFullName = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    SourceIsIn = sourceIsIn,   // Preserve the in modifier from the user's declared signature.
                    IsCreateNew = true,
                    CollectionKind = dstColl,
                    ElementMethod = elemMi,
                };
            }

            // Member mapping.
            var mi = new LuminMapMethodInfo
            {
                MethodName = method.Name,
                SourceTypeFullName = srcType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestTypeFullName = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SourceIsIn = sourceIsIn,
                IsCreateNew = true,
                DestIsValueType = destType.IsValueType,
                DestHasDefaultCtor = LuminMapBindingAnalyzer.HasDefaultCtor(destType),
            };
            mi.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcType, destType));
            return mi;
        }

        // In-place mapping: void Method([in] TSource source, ref TDest dest).
        if (method.ReturnType.SpecialType == SpecialType.System_Void
            && method.Parameters.Length == 2
            && method.Parameters[1].RefKind == RefKind.Ref)
        {
            var srcType = method.Parameters[0].Type as INamedTypeSymbol;
            var destType = method.Parameters[1].Type as INamedTypeSymbol;
            if (srcType == null || destType == null) return null;

            var mi = new LuminMapMethodInfo
            {
                MethodName = method.Name,
                SourceTypeFullName = srcType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestTypeFullName = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SourceIsIn = sourceIsIn,
                IsCreateNew = false,
                DestIsValueType = destType.IsValueType,
                DestHasDefaultCtor = LuminMapBindingAnalyzer.HasDefaultCtor(destType),
            };
            mi.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcType, destType));
            return mi;
        }

        return null;
    }
}
