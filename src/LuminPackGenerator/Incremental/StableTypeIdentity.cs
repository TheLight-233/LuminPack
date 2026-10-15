using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Describes a rebindable type without retaining a symbol from an earlier compilation.</summary>
internal sealed class StableTypeIdentity
{
    private readonly string _kind;
    private readonly int _reference;
    private readonly string _metadataName;
    private readonly string _simpleName;
    private readonly int _arity;
    private readonly int _rank;
    private readonly int _mode;
    private readonly SpecialType _special;
    private readonly NullableAnnotation _nullable;
    private readonly StableTypeIdentity _containing;
    private readonly ImmutableArray<StableTypeIdentity> _arguments;
    private readonly ImmutableArray<string> _tupleNames;
    internal readonly string Key;

    /// <summary>Creates a stable description of an encodable type.</summary>
    /// <param name="kind">The type construction category.</param>
    /// <param name="reference">The referenced assembly index, or minus one for source.</param>
    /// <param name="metadataName">The complete definition metadata name.</param>
    /// <param name="simpleName">The nested declaration's source name.</param>
    /// <param name="arity">The declaration's own generic arity.</param>
    /// <param name="rank">The array rank.</param>
    /// <param name="mode">Definition, construction, or unbound generic mode.</param>
    /// <param name="special">The special framework type identity.</param>
    /// <param name="nullable">The original nullable annotation.</param>
    /// <param name="containing">The constructed containing type, if required.</param>
    /// <param name="arguments">Element or generic type descriptions.</param>
    /// <param name="tupleNames">Tuple element names, if any.</param>
    internal StableTypeIdentity(string kind, int reference, string metadataName, string simpleName, int arity, int rank, int mode,
        SpecialType special, NullableAnnotation nullable, StableTypeIdentity containing, IEnumerable<StableTypeIdentity> arguments, IEnumerable<string> tupleNames)
    {
        _kind = kind; _reference = reference; _metadataName = metadataName; _simpleName = simpleName; _arity = arity;
        _rank = rank; _mode = mode; _special = special; _nullable = nullable; _containing = containing;
        _arguments = arguments.ToImmutableArray(); _tupleNames = tupleNames.ToImmutableArray();
        var key = new StringBuilder(kind).Append('|').Append(reference).Append('|').Append((int)special).Append('|').Append((int)nullable)
            .Append('|').Append(arity).Append('|').Append(rank).Append('|').Append(mode).Append('|');
        AppendPart(key, metadataName ?? string.Empty);
        AppendPart(key, containing?.Key ?? string.Empty);
        foreach (var argument in _arguments) AppendPart(key, argument.Key);
        key.Append('|');
        foreach (var name in _tupleNames) AppendPart(key, name ?? string.Empty);
        Key = key.ToString();
    }

    /// <summary>Writes a length-prefixed key component to avoid ambiguous concatenation.</summary>
    /// <param name="builder">The destination key.</param>
    /// <param name="value">The component text.</param>
    private static void AppendPart(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value);

    /// <summary>Reconstructs this identity using only symbols from the current compilation.</summary>
    /// <param name="resolver">The current-compilation resolver and memo.</param>
    /// <returns>The current type, or null when rebinding cannot preserve the identity.</returns>
    internal ITypeSymbol Resolve(CurrentTypeResolver resolver)
    {
        var compilation = resolver.Compilation;
        ITypeSymbol result;
        switch (_kind)
        {
            case "special": result = compilation.GetSpecialType(_special); break;
            case "dynamic": result = compilation.DynamicType; break;
            case "native": result = compilation.CreateNativeIntegerTypeSymbol(_special == SpecialType.System_IntPtr); break;
            case "array":
                var element = resolver.Resolve(_arguments[0]);
                result = element == null ? null : compilation.CreateArrayTypeSymbol(element, _rank, element.NullableAnnotation);
                break;
            case "pointer":
                var target = resolver.Resolve(_arguments[0]);
                result = target == null ? null : compilation.CreatePointerTypeSymbol(target);
                break;
            case "tuple":
                var elements = _arguments.Select(resolver.Resolve).ToImmutableArray();
                result = elements.Any(type => type == null) ? null : compilation.CreateTupleTypeSymbol(elements, _tupleNames,
                    elementNullableAnnotations: elements.Select(type => type.NullableAnnotation).ToImmutableArray());
                break;
            case "parameter":
                var parameterOwner = resolver.Resolve(_containing) as INamedTypeSymbol;
                if (parameterOwner == null) return null;
                if (_mode == 0) result = _rank < parameterOwner.TypeParameters.Length ? parameterOwner.TypeParameters[_rank] : null;
                else
                {
                    var methodOwner = parameterOwner.GetMembers().OfType<IMethodSymbol>()
                        .FirstOrDefault(method => method.OriginalDefinition.GetDocumentationCommentId() == _metadataName);
                    result = methodOwner != null && _rank < methodOwner.TypeParameters.Length ? methodOwner.TypeParameters[_rank] : null;
                }
                break;
            case "named":
                INamedTypeSymbol definition;
                if (_containing != null)
                {
                    var owner = resolver.Resolve(_containing) as INamedTypeSymbol;
                    definition = owner?.GetTypeMembers(_simpleName, _arity).FirstOrDefault();
                }
                else
                {
                    IAssemblySymbol assembly = _reference < 0 ? compilation.Assembly : resolver.Assemblies[_reference];
                    definition = assembly?.GetTypeByMetadataName(_metadataName);
                }
                if (definition == null) return null;
                if (_mode == 2) result = definition.ConstructUnboundGenericType();
                else if (_mode == 1 && _arity != 0)
                {
                    var arguments = _arguments.Select(resolver.Resolve).ToArray();
                    result = arguments.Any(type => type == null) ? null : definition.Construct(arguments.ToImmutableArray(),
                        arguments.Select(type => type.NullableAnnotation).ToImmutableArray());
                }
                else result = definition;
                break;
            default: return null;
        }
        return result?.WithNullableAnnotation(_nullable);
    }
}

/// <summary>Encodes observed current symbols into identities safe for a cross-compilation file cache.</summary>
internal sealed class StableTypeIdentityFactory
{
    private readonly Compilation _compilation;
    private readonly bool _supportTypeParameters;
    private readonly IAssemblySymbol[] _assemblies;
    private readonly Dictionary<ITypeSymbol, StableTypeIdentity> _known = new Dictionary<ITypeSymbol, StableTypeIdentity>(SymbolEqualityComparer.IncludeNullability);

    /// <summary>Creates a factory for one compilation's reference ordering.</summary>
    /// <param name="compilation">The current compilation.</param>
    internal StableTypeIdentityFactory(Compilation compilation) : this(compilation, false) { }

    /// <summary>Creates a factory that optionally supports declaration-owned invocation type arguments.</summary>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="supportTypeParameters">Whether invocation identities may encode definition-owned type parameters.</param>
    internal StableTypeIdentityFactory(Compilation compilation, bool supportTypeParameters)
    {
        _compilation = compilation;
        _supportTypeParameters = supportTypeParameters;
        _assemblies = compilation.References.Select(reference => compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol).ToArray();
    }

    /// <summary>Encodes an observed type, declining unsupported shapes rather than dropping them.</summary>
    /// <param name="type">The current type.</param>
    /// <returns>A stable type value, or null to require fresh semantic observation.</returns>
    internal StableTypeIdentity Create(ITypeSymbol type)
    {
        if (type == null || type.TypeKind == TypeKind.Error || type is ITypeParameterSymbol && !_supportTypeParameters || type is IFunctionPointerTypeSymbol) return null;
        if (_known.TryGetValue(type, out var existing)) return existing;
        StableTypeIdentity result = CreateCore(type);
        _known[type] = result;
        return result;
    }

    /// <summary>Builds one supported construction after memo lookup.</summary>
    /// <param name="type">The current type.</param>
    /// <returns>The complete stable construction, or null when unsupported.</returns>
    private StableTypeIdentity CreateCore(ITypeSymbol type)
    {
        var empty = Array.Empty<StableTypeIdentity>();
        var noNames = Array.Empty<string>();
        if (type is ITypeParameterSymbol parameter)
        {
            var owner = parameter.ContainingSymbol;
            var ownerType = (owner as INamedTypeSymbol ?? (owner as IMethodSymbol)?.ContainingType)?.OriginalDefinition;
            var containingIdentity = ownerType == null ? null : Create(ownerType);
            string methodIdentity = owner is IMethodSymbol method ? method.OriginalDefinition.GetDocumentationCommentId() : null;
            if (containingIdentity == null || owner is not INamedTypeSymbol && methodIdentity == null) return null;
            return new StableTypeIdentity("parameter", -1, methodIdentity, null, 0, parameter.Ordinal, owner is IMethodSymbol ? 1 : 0,
                SpecialType.None, type.NullableAnnotation, containingIdentity, empty, noNames);
        }
        if (type.TypeKind == TypeKind.Dynamic) return new StableTypeIdentity("dynamic", -1, null, null, 0, 0, 0, SpecialType.None, type.NullableAnnotation, null, empty, noNames);
        if (type is IArrayTypeSymbol array)
        {
            var element = Create(array.ElementType);
            return element == null ? null : new StableTypeIdentity("array", -1, null, null, 0, array.Rank, 0, SpecialType.None, type.NullableAnnotation, null, new[] { element }, noNames);
        }
        if (type is IPointerTypeSymbol pointer)
        {
            var target = Create(pointer.PointedAtType);
            return target == null ? null : new StableTypeIdentity("pointer", -1, null, null, 0, 0, 0, SpecialType.None, type.NullableAnnotation, null, new[] { target }, noNames);
        }
        if (type is not INamedTypeSymbol named || named.IsAnonymousType) return null;
        if (named.IsNativeIntegerType) return new StableTypeIdentity("native", -1, null, null, 0, 0, 0, type.SpecialType, type.NullableAnnotation, null, empty, noNames);
        if (named.IsTupleType)
        {
            var elements = named.TupleElements.Select(field => Create(field.Type)).ToArray();
            if (elements.Any(element => element == null)) return null;
            var names = named.TupleElements.Select((field, index) => field.Name == "Item" + (index + 1) ? null : field.Name);
            return new StableTypeIdentity("tuple", -1, null, null, 0, 0, 0, SpecialType.None, type.NullableAnnotation, null, elements, names);
        }
        if (named.SpecialType != SpecialType.None && (named.Arity == 0 || named.IsDefinition))
            return new StableTypeIdentity("special", -1, null, null, 0, 0, 0, named.SpecialType, type.NullableAnnotation, null, empty, noNames);

        var definition = named.OriginalDefinition;
        int reference = SymbolEqualityComparer.Default.Equals(definition.ContainingAssembly, _compilation.Assembly) ? -1 :
            Array.FindIndex(_assemblies, assembly => SymbolEqualityComparer.Default.Equals(assembly, definition.ContainingAssembly));
        if (reference < 0 && !SymbolEqualityComparer.Default.Equals(definition.ContainingAssembly, _compilation.Assembly)) return null;
        int mode = named.IsUnboundGenericType ? 2 : named.IsDefinition ? 0 : 1;
        var arguments = mode == 1 ? named.TypeArguments.Select(Create).ToArray() : empty;
        if (arguments.Any(argument => argument == null)) return null;
        var containing = mode == 1 && named.ContainingType != null ? Create(named.ContainingType) : null;
        if (mode == 1 && named.ContainingType != null && containing == null) return null;
        string metadata = MetadataName(definition);
        return new StableTypeIdentity("named", reference, metadata, named.Name, named.Arity, 0, mode, SpecialType.None,
            type.NullableAnnotation, containing, arguments, noNames);
    }

    /// <summary>Forms a definition lookup name including nested-type and generic arity syntax.</summary>
    /// <param name="type">The original definition.</param>
    /// <returns>The complete metadata name.</returns>
    private static string MetadataName(INamedTypeSymbol type)
    {
        if (type.ContainingType != null) return MetadataName(type.ContainingType) + "+" + type.MetadataName;
        string space = type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
        return string.IsNullOrEmpty(space) ? type.MetadataName : space + "." + type.MetadataName;
    }
}

/// <summary>Memoizes rebinding without allowing current symbols to escape their compilation context.</summary>
internal sealed class CurrentTypeResolver
{
    internal readonly Compilation Compilation;
    internal readonly IAssemblySymbol[] Assemblies;
    private readonly Dictionary<string, ITypeSymbol> _known = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);

    /// <summary>Creates a resolver using current metadata-reference instances.</summary>
    /// <param name="compilation">The current compilation.</param>
    internal CurrentTypeResolver(Compilation compilation)
    {
        Compilation = compilation;
        Assemblies = compilation.References.Select(reference => compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol).ToArray();
    }

    /// <summary>Rebinds an identity once in the current compilation.</summary>
    /// <param name="identity">The stable type description.</param>
    /// <returns>The current type symbol or null for a failed rebind.</returns>
    internal ITypeSymbol Resolve(StableTypeIdentity identity)
    {
        if (_known.TryGetValue(identity.Key, out var type)) return type;
        type = identity.Resolve(this);
        _known[identity.Key] = type;
        return type;
    }
}
