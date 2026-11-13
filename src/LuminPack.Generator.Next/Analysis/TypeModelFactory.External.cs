using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
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
    private static readonly ConditionalWeakTable<Compilation, IReadOnlyList<INamedTypeSymbol>> ExternalMarkers = new();
    private const string ReferenceAssemblyAttribute = "System.Runtime.CompilerServices.ReferenceAssemblyAttribute";

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
    internal static IReadOnlyList<INamedTypeSymbol> MarkedTypes(Compilation compilation) =>
        ExternalMarkers.GetValue(compilation, static item => ReadMarkedTypes(item));

    /// <summary>Recognizes a marked Union whose definition owns generic dispatch.</summary>
    /// <param name="type">The marked type.</param>
    /// <returns>Whether the type declares a Union contract.</returns>
    internal static bool IsUnion(INamedTypeSymbol type) => HasAttribute(type, UnionAttribute);

    /// <summary>Finds omitted storage in marked generic constructions reached by a serializer call.</summary>
    /// <param name="type">The closed payload or formatter container.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The missing storage diagnostics, or an empty array.</returns>
    internal static DiagnosticInfo[] MissingCallStorage(ITypeSymbol type, Compilation compilation)
    {
        IReadOnlyList<INamedTypeSymbol> markers = MarkedTypes(compilation);
        List<DiagnosticInfo>? diagnostics = null;
        Visit(type);
        return diagnostics?.ToArray() ?? Array.Empty<DiagnosticInfo>();

        void Visit(ITypeSymbol item)
        {
            if (item is IArrayTypeSymbol array)
            {
                Visit(array.ElementType);
                return;
            }
            if (item is not INamedTypeSymbol { IsGenericType: true } named)
                return;
            if (HasAttribute(named.ContainingAssembly, ReferenceAssemblyAttribute))
            {
                bool open = false;
                for (int index = 0; index < markers.Count; index++)
                {
                    INamedTypeSymbol marker = markers[index];
                    if (SymbolEqualityComparer.Default.Equals(marker, named))
                        return;
                    open |= SymbolEqualityComparer.Default.Equals(marker, named.OriginalDefinition);
                }
                if (open)
                {
                    DiagnosticInfo[] missing = MissingStorage(InView(named, Complete(compilation)));
                    if (missing.Length != 0)
                        (diagnostics ??= new List<DiagnosticInfo>()).AddRange(missing);
                    return;
                }
            }
            string definition = named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.OriginalDefinition.Name;
            if (FormatterDefinitions.HasGeneric(definition) || FormatterRegistry.FindSequence(definition) != null ||
                FormatterRegistry.FindDictionary(Session.FormatterSpelling(named)) != null)
                foreach (ITypeSymbol argument in named.TypeArguments)
                    Visit(argument);
        }
    }

    /// <summary>Reads exact closed markers and the definitions selected by open markers.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The external marker index in declaration order.</returns>
    private static IReadOnlyList<INamedTypeSymbol> ReadMarkedTypes(Compilation compilation)
    {
        List<INamedTypeSymbol>? types = null;
        HashSet<INamedTypeSymbol>? seen = null;
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != PackableAttribute || attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol type || type.TypeKind == TypeKind.Error ||
                type.ContainingAssembly == null || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
                continue;
            INamedTypeSymbol marker = type.IsUnboundGenericType ? type.OriginalDefinition : type;
            if ((seen ??= new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default)).Add(marker))
                (types ??= new List<INamedTypeSymbol>()).Add(marker);
        }
        if (types == null || seen == null)
            return Array.Empty<INamedTypeSymbol>();
        // An imported Union owns its declared case contracts just as a source-declared root does.
        for (int index = 0; index < types.Count; index++)
            foreach (AttributeData attribute in types[index].GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == UnionAttribute && attribute.ConstructorArguments.Length == 2 &&
                    attribute.ConstructorArguments[1].Value is INamedTypeSymbol member && member.TypeKind != TypeKind.Error)
                {
                    INamedTypeSymbol marker = member.IsUnboundGenericType ? member.OriginalDefinition : member;
                    if (seen.Add(marker))
                        types.Add(marker);
                }
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
            if (current.DeclaringSyntaxReferences.Length != 0 || !HasAttribute(current.ContainingAssembly, ReferenceAssemblyAttribute))
                continue;
            foreach (ISymbol member in current.GetMembers())
            {
                if (member is not IPropertySymbol property || property.IsStatic || property.IsAbstract || property.IsExtern || property.IsIndexer ||
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
        /// assembly declares. The defining assembly retains ownership of the virtual and generic case slots.</summary>
        /// <param name="root">The imported Union root.</param>
        /// <param name="suffix">The root's slot suffix.</param>
        /// <param name="issues">Receives the reason the root cannot be read here.</param>
        private void ReferencedDispatch(INamedTypeSymbol root, string suffix, List<string> issues)
        {
            foreach (string slot in new[] { "Write_" + suffix + "_Compiled", "WriteJson_" + suffix + "_Compiled" })
                if (!root.GetMembers(slot).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 1 &&
                        method.Parameters.Length == 2 && _compilation.IsSymbolAccessibleWithin(method, _compilation.Assembly)))
                {
                    issues.Add("the imported Union root '" + root.ToDisplayString() + "' does not expose its generated dispatch '" + slot + "'");
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
        internal bool Marked(INamedTypeSymbol type) =>
            ExplicitMarker(type) || !type.IsDefinition && ExplicitMarker(type.OriginalDefinition);

        /// <summary>Whether the marker index contains this exact type symbol.</summary>
        /// <param name="type">A type of this session's compilation.</param>
        /// <returns>Whether publication can start from this marker directly.</returns>
        private bool ExplicitMarker(INamedTypeSymbol type)
        {
            HashSet<INamedTypeSymbol>? marked = System.Threading.Volatile.Read(ref _marked);
            if (marked == null)
            {
                marked = new HashSet<INamedTypeSymbol>(MarkedTypes(_compilation), SymbolEqualityComparer.Default);
                marked = System.Threading.Interlocked.CompareExchange(ref _marked, marked, null) ?? marked;
            }
            return marked.Contains(type);
        }
    }
}
