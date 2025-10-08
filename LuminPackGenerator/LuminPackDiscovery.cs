using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

internal static class LuminPackDiscovery
{
    const string LuminPackableAttributeName = "LuminPackableAttribute";
    
    public static void Run(
        INamedTypeSymbol baseType,
        LuminDataInfo dataInfo,
        Compilation compilation)
    {
        var baseDef = baseType.OriginalDefinition;
        var baseName = baseDef.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var baseArity = baseDef.Arity;

        // 获取所有可能的派生类型
        var derivedList = GetAllTypesInCompilation(compilation)
            .Where(t => !t.IsAbstract && !t.IsStatic)
            .Where(t => t.TypeParameters.Length <= baseArity)
            .Where(t => IsDerivedFromOrImplements(t, baseDef, baseName, baseArity))
            .Where(t => t.GetAttributes().Any(a => a.AttributeClass?.Name == LuminPackableAttributeName))
            .OrderBy(d => d.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToList();

        ushort tag = 0;
        foreach (var d in derivedList)
        {
            while (dataInfo.UnionMembers.Any(m => m.Id == tag)) tag++;
            dataInfo.UnionMembers.Add(new LuminUnionMemberInfo(tag++, d));
            
            if (baseType.IsGenericType)
            {
                RecordGenericTypeInfo(d, baseType, dataInfo);
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypesInCompilation(Compilation comp)
    {
        foreach (var tree in comp.SyntaxTrees)
        {
            var sem = comp.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax)
                {
                    var sym = sem.GetDeclaredSymbol(node) as INamedTypeSymbol;
                    if (sym != null) yield return sym;
                }
            }
        }
    }

    /// <summary>
    /// 检查类型是否派生自指定基类或实现指定接口
    /// </summary>
    private static bool IsDerivedFromOrImplements(INamedTypeSymbol derived, INamedTypeSymbol baseDef, string baseName, int baseArity)
    {
        // 处理类继承
        if (baseDef.TypeKind == TypeKind.Class)
        {
            var cur = derived.BaseType;
            while (cur != null)
            {
                var def = cur.OriginalDefinition;
                if (def.Arity == baseArity &&
                    def.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) == baseName)
                    return true;
                cur = cur.BaseType;
            }
        }
        // 处理接口实现
        else if (baseDef.TypeKind == TypeKind.Interface)
        {
            // 检查所有实现的接口
            foreach (var implementedInterface in derived.AllInterfaces)
            {
                var def = implementedInterface.OriginalDefinition;
                if (def.Arity == baseArity &&
                    def.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) == baseName)
                    return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// 记录泛型类型信息
    /// </summary>
    private static void RecordGenericTypeInfo(INamedTypeSymbol derivedType, INamedTypeSymbol baseType, LuminDataInfo dataInfo)
    {
        if (baseType.TypeKind == TypeKind.Class)
        {
            var current = derivedType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(
                        current.OriginalDefinition, 
                        baseType.OriginalDefinition))
                {
                    dataInfo.UnionGenericTypes[derivedType] = 
                        current.TypeArguments.Select(t => t.ToDisplayString()).ToList();
                    break;
                }
                current = current.BaseType;
            }
        }
        else if (baseType.TypeKind == TypeKind.Interface)
        {
            foreach (var implementedInterface in derivedType.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(
                        implementedInterface.OriginalDefinition,
                        baseType.OriginalDefinition))
                {
                    dataInfo.UnionGenericTypes[derivedType] = 
                        implementedInterface.TypeArguments.Select(t => t.ToDisplayString()).ToList();
                    break;
                }
            }
        }
    }
}