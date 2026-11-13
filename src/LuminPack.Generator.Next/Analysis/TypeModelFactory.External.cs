using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Models referenced types that the consuming assembly marks with <c>[assembly: LuminPackable(typeof(T))]</c>.</summary>
/// <remarks>This ports the original <c>ReferencedTypeMetadata</c> and the assembly marker roots of <c>CompilationTypeAnalysis</c>.
/// A marked type is packable in the consuming assembly, which generates its operations. Ordinary metadata import omits private
/// fields and automatic-property backing fields of referenced assemblies, so a compilation that declares markers is analyzed through
/// a view importing all metadata. A reference assembly that omits backing storage cannot prove the storage contract and reports
/// LuminPack112.</remarks>
internal static partial class TypeModelFactory
{
    private static readonly ConditionalWeakTable<Compilation, Compilation> CompleteViews = new();

    /// <summary>Returns the compilation analysis runs on: itself, or its complete-metadata view when it declares external roots.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The analysis compilation.</returns>
    internal static Compilation Complete(Compilation compilation) => CompleteViews.GetValue(compilation, static item =>
        item.Options.MetadataImportOptions == MetadataImportOptions.All || MarkedTypes(item).Count == 0
            ? item
            : item.WithOptions(item.Options.WithMetadataImportOptions(MetadataImportOptions.All)));

    /// <summary>Finds the same type in another view of the compilation.</summary>
    /// <param name="type">A type of the consuming compilation.</param>
    /// <param name="view">The analysis view.</param>
    /// <returns>The equivalent type, or the original type when it cannot be resolved.</returns>
    internal static T InView<T>(T type, Compilation view) where T : class, ITypeSymbol
    {
        if (type is IErrorTypeSymbol || type is ITypeParameterSymbol)
            return type;
        string? id = DocumentationCommentId.CreateReferenceId(type);
        if (id == null)
            return type;
        foreach (ISymbol candidate in DocumentationCommentId.GetSymbolsForReferenceId(id, view))
            if (candidate is T resolved)
                return resolved;
        return type;
    }

    /// <summary>Reads the referenced definitions marked by the assembly, with the declared cases of a marked Union.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The marked definitions in declaration order.</returns>
    internal static List<INamedTypeSymbol> MarkedTypes(Compilation compilation)
    {
        var types = new List<INamedTypeSymbol>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != PackableAttribute || attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol type || type.TypeKind == TypeKind.Error ||
                type.ContainingAssembly == null || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
                continue;
            if (seen.Add(type.OriginalDefinition))
                types.Add(type.OriginalDefinition);
        }
        // An imported Union owns its declared case contracts just as a source-declared root does.
        for (int index = 0; index < types.Count; index++)
            foreach (AttributeData attribute in types[index].GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == UnionAttribute && attribute.ConstructorArguments.Length == 2 &&
                    attribute.ConstructorArguments[1].Value is INamedTypeSymbol member && member.TypeKind != TypeKind.Error &&
                    seen.Add(member.OriginalDefinition))
                    types.Add(member.OriginalDefinition);
        return types;
    }

    /// <summary>Reports automatic properties of a marked type whose backing storage the referenced metadata omits.</summary>
    /// <param name="type">The marked type in the complete-metadata view.</param>
    /// <returns>The LuminPack112 diagnostics.</returns>
    internal static DiagnosticInfo[] MissingStorage(INamedTypeSymbol type)
    {
        List<DiagnosticInfo>? diagnostics = null;
        for (INamedTypeSymbol? current = type; current != null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
            current = current.BaseType)
        {
            if (current.DeclaringSyntaxReferences.Length != 0)
                continue;
            foreach (ISymbol member in current.GetMembers())
            {
                if (member is not IPropertySymbol property || property.IsStatic || property.IsAbstract || property.IsIndexer ||
                    current.GetMembers("<" + property.Name + ">k__BackingField").Length != 0)
                    continue;
                if (property.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal) &&
                    !HasAttribute(property, IncludeAttribute) || HasAttribute(property, IgnoreAttribute))
                    continue;
                bool generated = property.GetMethod != null && HasAttribute(property.GetMethod, "System.Runtime.CompilerServices.CompilerGeneratedAttribute") ||
                    property.SetMethod != null && HasAttribute(property.SetMethod, "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
                if (!generated)
                    continue;
                (diagnostics ??= new List<DiagnosticInfo>()).Add(DiagnosticInfo.Capture(DiagnosticDescriptors.IncompleteReferencedPropertyStorage.Id,
                    null, new[] { current.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(), property.Name }));
            }
        }
        return diagnostics?.ToArray() ?? Array.Empty<DiagnosticInfo>();
    }

    private sealed partial class Session
    {
        private HashSet<INamedTypeSymbol>? _marked;

        /// <summary>Requires the generated dispatch of an imported Union root: the public compiled binary and JSON slots its own
        /// assembly declares, and closed cases, whose generic slot fields only that assembly owns.</summary>
        /// <param name="root">The imported Union root.</param>
        /// <param name="suffix">The root's slot suffix.</param>
        /// <param name="declared">The declared cases.</param>
        /// <param name="issues">Receives the reason the root cannot be read here.</param>
        private void ReferencedDispatch(INamedTypeSymbol root, string suffix, List<(int Tag, INamedTypeSymbol Type)> declared, List<string> issues)
        {
            foreach (string slot in new[] { "Write_" + suffix + "_Compiled", "WriteJson_" + suffix + "_Compiled" })
                if (!root.GetMembers(slot).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 1 &&
                        method.Parameters.Length == 2 && _compilation.IsSymbolAccessibleWithin(method, _compilation.Assembly)))
                {
                    issues.Add("the imported Union root '" + root.ToDisplayString() + "' does not expose its generated dispatch '" + slot + "'");
                    return;
                }
            foreach (var item in declared)
                if (item.Type.IsGenericType)
                {
                    issues.Add("the imported Union root '" + root.ToDisplayString() + "' declares the generic case '" + item.Type.ToDisplayString() + "'");
                    return;
                }
        }

        /// <summary>Whether this compilation generates the type's operations: it declares the type or marks it as an external root.</summary>
        /// <param name="type">A type of this session's compilation.</param>
        /// <returns>Whether the type's operations are generated here.</returns>
        internal bool Owned(INamedTypeSymbol type) =>
            SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly) || Marked(type);

        /// <summary>Whether the consuming assembly marks a referenced type as packable.</summary>
        /// <param name="type">A type of this session's compilation.</param>
        /// <returns>Whether its definition is an external root.</returns>
        internal bool Marked(INamedTypeSymbol type)
        {
            HashSet<INamedTypeSymbol>? marked = System.Threading.Volatile.Read(ref _marked);
            if (marked == null)
            {
                marked = new HashSet<INamedTypeSymbol>(MarkedTypes(_compilation), SymbolEqualityComparer.Default);
                marked = System.Threading.Interlocked.CompareExchange(ref _marked, marked, null) ?? marked;
            }
            return marked.Contains(type.OriginalDefinition);
        }
    }
}
