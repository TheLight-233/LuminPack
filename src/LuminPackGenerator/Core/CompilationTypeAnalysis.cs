using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>
/// Immutable, compilation-wide semantic index.  Every syntax tree and semantic model is
/// traversed once; formatter generation and registration only consume this cached result.
/// </summary>
internal sealed class CompilationTypeAnalysis
{
    private readonly Dictionary<ITypeSymbol, INamedTypeSymbol> _formatterOwners;
    private readonly Dictionary<ITypeSymbol, INamedTypeSymbol> _layoutOwners;

    internal CompilationTypeAnalysis(
        ImmutableArray<ITypeSymbol> formatterTypes,
        ImmutableArray<ProjectTypeData> declaredTypes,
        Dictionary<ITypeSymbol, INamedTypeSymbol> formatterOwners,
        Dictionary<ITypeSymbol, INamedTypeSymbol> layoutOwners,
        bool usesLuminPack)
    {
        FormatterTypes = formatterTypes;
        DeclaredTypes = declaredTypes;
        _formatterOwners = formatterOwners;
        _layoutOwners = layoutOwners;
        UsesLuminPack = usesLuminPack;
    }

    internal ImmutableArray<ITypeSymbol> FormatterTypes { get; }

    internal ImmutableArray<ProjectTypeData> DeclaredTypes { get; }

    internal bool UsesLuminPack { get; }

    internal bool IsOwnedBy(ITypeSymbol type, INamedTypeSymbol owner)
        => _formatterOwners.TryGetValue(type, out INamedTypeSymbol actual) &&
           SymbolEqualityComparer.Default.Equals(actual, owner);

    internal bool HasOwner(ITypeSymbol type) => _formatterOwners.ContainsKey(type);

    internal bool IsLayoutOwnedBy(ITypeSymbol type, INamedTypeSymbol owner)
        => _layoutOwners.TryGetValue(NormalizeLayoutType(type), out INamedTypeSymbol actual) &&
           SymbolEqualityComparer.Default.Equals(actual, owner);

    private static ITypeSymbol NormalizeLayoutType(ITypeSymbol type)
        => type is INamedTypeSymbol named ? named.OriginalDefinition : type;
}

/// <summary>Reusable structural facts for one source-declared type.</summary>
internal sealed class ProjectTypeData
{
    internal ProjectTypeData(INamedTypeSymbol symbol)
    {
        Symbol = symbol;
        BaseType = symbol.BaseType;
        Interfaces = symbol.Interfaces;
        Fields = symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(static field => !field.IsStatic && !field.IsImplicitlyDeclared)
            .ToImmutableArray();
        Properties = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(static property => !property.IsStatic && !property.IsImplicitlyDeclared)
            .ToImmutableArray();
    }

    internal INamedTypeSymbol Symbol { get; }
    internal INamedTypeSymbol BaseType { get; }
    internal ImmutableArray<INamedTypeSymbol> Interfaces { get; }
    internal ImmutableArray<IFieldSymbol> Fields { get; }
    internal ImmutableArray<IPropertySymbol> Properties { get; }
}

internal static class CompilationTypeAnalysisCache
{
    private sealed class Holder
    {
        internal Holder(Compilation compilation)
        {
            Value = new Lazy<CompilationTypeAnalysis>(
                () => Analyze(compilation),
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal Lazy<CompilationTypeAnalysis> Value { get; }
    }

    private static readonly ConditionalWeakTable<Compilation, Holder> Cache = new();

    internal static CompilationTypeAnalysis GetOrCreate(Compilation compilation)
        => Cache.GetValue(compilation, static value => new Holder(value)).Value.Value;

    private static CompilationTypeAnalysis Analyze(Compilation compilation)
    {
        var formatterTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var declaredTypes = new List<ProjectTypeData>();
        var packableTypes = new List<INamedTypeSymbol>();
        bool usesLuminPack = false;

        CollectDeclaredTypes(compilation.Assembly.GlobalNamespace, declaredTypes, packableTypes);

        // A single semantic pass captures both explicitly written types and inferred types
        // such as tuple expressions, target-typed new expressions and collection expressions.
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SyntaxNode root = tree.GetRoot();
            SemanticModel model = compilation.GetSemanticModel(tree);

            foreach (SyntaxNode node in root.DescendantNodesAndSelf())
            {
                if (node is TypeSyntax syntax)
                {
                    ITypeSymbol type = model.GetTypeInfo(syntax).Type;
                    usesLuminPack |= IsLuminPackRelevantType(type);
                    AddTypeGraph(type, formatterTypes);
                }
                else if (node is ExpressionSyntax expression)
                {
                    TypeInfo typeInfo = model.GetTypeInfo(expression);
                    usesLuminPack |= IsLuminPackRelevantType(typeInfo.Type) ||
                                     IsLuminPackRelevantType(typeInfo.ConvertedType);
                    AddTypeGraph(typeInfo.Type, formatterTypes);
                    AddTypeGraph(typeInfo.ConvertedType, formatterTypes);
                }
            }
        }

        foreach (INamedTypeSymbol packable in packableTypes)
        {
            AddTypeGraph(packable, formatterTypes);
        }

        var owners = new Dictionary<ITypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
        INamedTypeSymbol[] orderedPackables = packableTypes.OrderBy(
                     static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                     StringComparer.Ordinal).ToArray();
        foreach (INamedTypeSymbol packable in orderedPackables)
        {
            var ownedGraph = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            AddPackableGraph(packable, ownedGraph, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
            foreach (ITypeSymbol type in ownedGraph)
            {
                if (!owners.ContainsKey(type))
                {
                    owners.Add(type, packable);
                }
            }
        }

        // Every packable owns its own local marshal layout.  Unannotated base layouts are
        // assigned deterministically to the first derived packable which needs them.
        var layoutOwners = new Dictionary<ITypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (INamedTypeSymbol packable in orderedPackables)
        {
			layoutOwners[packable.OriginalDefinition] = packable;
        }
        foreach (INamedTypeSymbol packable in orderedPackables)
        {
            for (INamedTypeSymbol current = packable.BaseType;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
				INamedTypeSymbol layoutType = current.OriginalDefinition;
				if (!layoutOwners.ContainsKey(layoutType))
				{
					layoutOwners.Add(layoutType, packable);
				}
            }
        }

        return new CompilationTypeAnalysis(
            formatterTypes
                .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ToImmutableArray(),
            declaredTypes
                .OrderBy(static data => data.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ToImmutableArray(),
            owners,
            layoutOwners,
            usesLuminPack);
    }

    private static bool IsLuminPackRelevantType(ITypeSymbol type)
    {
        if (type is null) return false;
        if (type.ContainingAssembly?.Identity.Name == "LuminPack") return true;

        return type switch
        {
            IArrayTypeSymbol array => IsLuminPackRelevantType(array.ElementType),
            IPointerTypeSymbol pointer => IsLuminPackRelevantType(pointer.PointedAtType),
            INamedTypeSymbol named => HasPackableAttribute(named) ||
                                      named.TypeArguments.Any(IsLuminPackRelevantType),
            _ => false
        };
    }

    private static void CollectDeclaredTypes(
        INamespaceSymbol root,
        ICollection<ProjectTypeData> declaredTypes,
        ICollection<INamedTypeSymbol> packableTypes)
    {
        foreach (INamespaceSymbol child in root.GetNamespaceMembers())
        {
            CollectDeclaredTypes(child, declaredTypes, packableTypes);
        }

        foreach (INamedTypeSymbol type in root.GetTypeMembers())
        {
            CollectDeclaredType(type, declaredTypes, packableTypes);
        }
    }

    private static void CollectDeclaredType(
        INamedTypeSymbol type,
        ICollection<ProjectTypeData> declaredTypes,
        ICollection<INamedTypeSymbol> packableTypes)
    {
        declaredTypes.Add(new ProjectTypeData(type));
        if (HasPackableAttribute(type))
        {
            packableTypes.Add(type);
        }

        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            CollectDeclaredType(nested, declaredTypes, packableTypes);
        }
    }

    private static void AddPackableGraph(
        INamedTypeSymbol packable,
        ISet<ITypeSymbol> types,
        ISet<ITypeSymbol> visited)
    {
        if (!visited.Add(packable)) return;
        AddTypeGraph(packable, types);

        foreach (ISymbol member in packable.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field:
                    AddTypeGraph(field.Type, types);
                    break;
                case IPropertySymbol { IsStatic: false, IsImplicitlyDeclared: false } property:
                    AddTypeGraph(property.Type, types);
                    break;
            }
        }

        if (packable.BaseType is { SpecialType: not SpecialType.System_Object } baseType &&
            HasPackableAttribute(baseType))
        {
            AddPackableGraph(baseType, types, visited);
        }
    }

    private static void AddTypeGraph(ITypeSymbol type, ISet<ITypeSymbol> types)
    {
        if (type is null || type.TypeKind == TypeKind.Error || type is ITypeParameterSymbol) return;

        if (!types.Add(type)) return;

        switch (type)
        {
            case IArrayTypeSymbol array:
                AddTypeGraph(array.ElementType, types);
                break;
            case INamedTypeSymbol named:
                if (named.ContainingType is not null)
                {
                    AddTypeGraph(named.ContainingType, types);
                }
                foreach (ITypeSymbol argument in named.TypeArguments)
                {
                    AddTypeGraph(argument, types);
                }
                break;
        }
    }

    private static bool HasPackableAttribute(INamedTypeSymbol type)
        => type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);
}
