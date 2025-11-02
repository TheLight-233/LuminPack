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
            .Where(t => t.GetAttributes().Any(a => a.AttributeClass?.Name == LuminPackableAttributeName))
            .Where(t => IsValidDerivedType(t, baseDef, baseName, baseArity))
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
    /// 验证派生类型是否符合收集条件
    /// 规则：
    /// 1. 如果子类泛型参数数量超过基类，不收集
    /// 2. 如果子类泛型参数不是直接传递给基类（如 MyClass&lt;U&gt; : MyClassBase&lt;T&gt;），不收集
    /// 3. 如果子类有约束且和基类约束不一样，不收集
    /// 4. 如果基类被完全具象化（如 MyClassBase&lt;int&gt;），子类不能有泛型参数
    /// 允许的情况：
    /// - MyClass&lt;T&gt; : MyClassBase&lt;T&gt; （泛型参数完全匹配，约束一致）
    /// - MyClass : MyClassBase&lt;int&gt; （完全具体化，子类无泛型）
    /// </summary>
    private static bool IsValidDerivedType(INamedTypeSymbol derivedType, INamedTypeSymbol baseDef, string baseName, int baseArity)
    {
        // 首先找到继承/实现的基类/接口
        var matchedBase = FindMatchedBaseType(derivedType, baseDef, baseName, baseArity);
        if (matchedBase == null)
            return false;
        
        // 如果基类不是泛型，直接通过
        if (baseArity == 0)
            return true;
        
        // 检查基类的类型参数是否都是具体类型（被完全具象化）
        bool baseIsFullyConcrete = matchedBase.TypeArguments.All(ta => ta is not ITypeParameterSymbol);
        
        if (baseIsFullyConcrete)
        {
            // 基类被完全具象化（如 AbstractGenericBase<int>）
            // 这种情况下，子类不能有任何泛型参数
            // 允许: ConcreteClass : AbstractGenericBase<int>
            // 拒绝: ConcreteClass<T> : AbstractGenericBase<int>
            return derivedType.TypeParameters.Length == 0;
        }
        
        // 基类使用了类型参数（如 AbstractGenericBase<T>）
        // 子类必须也是泛型，且参数数量必须匹配
        
        // 如果子类没有泛型参数，不符合要求
        if (derivedType.TypeParameters.Length == 0)
            return false;
        
        // 如果子类泛型参数数量超过基类，不应该被收集
        if (derivedType.TypeParameters.Length > baseArity)
            return false;
        
        // 如果子类泛型参数数量少于基类，不符合要求
        // 这种情况意味着部分泛型参数被具体化，如 MyClass<T> : MyClassBase<T, int>
        if (derivedType.TypeParameters.Length < baseArity)
            return false;
        
        // 子类泛型参数数量等于基类，需要验证泛型参数是否一一对应
        for (int i = 0; i < baseArity; i++)
        {
            var baseTypeArg = matchedBase.TypeArguments[i];
            
            // 基类的类型参数必须是类型参数符号
            if (baseTypeArg is not ITypeParameterSymbol typeParam)
                return false; // 基类使用了具体类型，不符合 MyClass<T> : MyClassBase<T> 模式
            
            // 检查这个类型参数是否是子类的对应位置的类型参数
            var derivedTypeParam = derivedType.TypeParameters[i];
            
            // 验证类型参数的序号必须匹配
            if (typeParam.Ordinal != i)
                return false; // 类型参数顺序不对应，如 MyClass<T, U> : MyClassBase<U, T>
            
            // 验证约束是否兼容：子类的约束必须和基类定义中的约束完全一致
            // 获取基类定义中对应位置的类型参数
            var baseDefinitionTypeParam = baseDef.TypeParameters[i];
            if (!AreConstraintsCompatible(derivedTypeParam, baseDefinitionTypeParam))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 检查子类类型参数的约束是否和基类一致
    /// </summary>
    private static bool AreConstraintsCompatible(ITypeParameterSymbol derivedParam, ITypeParameterSymbol baseParam)
    {
        // 检查特殊约束
        if (derivedParam.HasConstructorConstraint != baseParam.HasConstructorConstraint)
            return false;
        
        if (derivedParam.HasReferenceTypeConstraint != baseParam.HasReferenceTypeConstraint)
            return false;
        
        if (derivedParam.HasValueTypeConstraint != baseParam.HasValueTypeConstraint)
            return false;
        
        if (derivedParam.HasUnmanagedTypeConstraint != baseParam.HasUnmanagedTypeConstraint)
            return false;
        
        if (derivedParam.HasNotNullConstraint != baseParam.HasNotNullConstraint)
            return false;
        
        // 检查约束类型数量
        if (derivedParam.ConstraintTypes.Length != baseParam.ConstraintTypes.Length)
            return false;
        
        // 检查约束类型是否一致
        for (int i = 0; i < derivedParam.ConstraintTypes.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                derivedParam.ConstraintTypes[i],
                baseParam.ConstraintTypes[i]))
            {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// 查找派生类匹配的基类或接口
    /// </summary>
    private static INamedTypeSymbol? FindMatchedBaseType(INamedTypeSymbol derived, INamedTypeSymbol baseDef, string baseName, int baseArity)
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
                    return cur;
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
                    return implementedInterface;
            }
        }
        
        return null;
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