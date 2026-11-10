using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>The single analysis boundary: Roslyn symbols become <see cref="TypeModel"/> values here.</summary>
/// <remarks>Everything below this boundary is a pure model and never sees a symbol or a compilation. The factory classifies the
/// payload protocol, enumerates the serialized storage, normalizes every member into a model value and records the declaration-time
/// wire policy. Shapes outside the modeled surface return an incomplete or unsupported result with a specific reason; analysis never
/// throws at the consumer.</remarks>
internal static class TypeModelFactory
{
    private const string PackableAttribute = "LuminPack.Attribute.LuminPackableAttribute";
    private const string IgnoreAttribute = "LuminPack.Attribute.LuminPackIgnoreAttribute";
    private const string IncludeAttribute = "LuminPack.Attribute.LuminPackIncludeAttribute";
    private const string OptionAttribute = "LuminPack.Attribute.LuminPackOptionAttribute";
    private const string BoundedAttribute = "LuminPack.Attribute.LuminPackBoundedAttribute";
    private const string UnionAttribute = "LuminPack.Attribute.LuminPackUnionAttribute";
    private const string WideTagAttribute = "LuminPack.Attribute.LuminPackWideTagAttribute";
    private const string OrderAttribute = "LuminPack.Attribute.LuminPackOrderAttribute";
    private const string StructLayoutAttribute = "System.Runtime.InteropServices.StructLayoutAttribute";
    private const string FieldOffsetAttribute = "System.Runtime.InteropServices.FieldOffsetAttribute";

    /// <summary>The deepest nested payload chain the analysis expands.</summary>
    private const int MaxDepth = 8;

    /// <summary>The analysis state shared by every payload of one compilation.</summary>
    /// <remarks>A compilation is a weak key, so the cache does not retain a finished run. A nested payload is analyzed once
    /// per depth and reused by every parent that embeds it.</remarks>
    private static readonly ConditionalWeakTable<Compilation, Session> Sessions = new ConditionalWeakTable<Compilation, Session>();

    /// <summary>Builds a model for one attributed type.</summary>
    /// <param name="symbol">The attributed type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>A complete model, an incomplete model or an unsupported result.</returns>
    internal static ModelResult TryCreate(INamedTypeSymbol symbol, Compilation compilation, GeneratorOptions options)
    {
        Session session = Sessions.GetValue(compilation, item => new Session(item, options));
        // Project facts are created once per compilation. A caller that supplies different facts on the same compilation
        // gets an isolated run instead of a cache that was built under another caller's facts.
        return session.Options.Equals(options) ? session.Create(symbol, depth: 0) : new Session(compilation, options).Create(symbol, depth: 0);
    }

    /// <summary>Holds the per-compilation analysis state: the consumer compilation, its project facts and the completed models.</summary>
    private sealed class Session
    {
        private readonly Compilation _compilation;
        private readonly Dictionary<AnalysisKey, ModelResult> _completed = new Dictionary<AnalysisKey, ModelResult>();
        private readonly object _gate = new object();

        internal Session(Compilation compilation, GeneratorOptions options)
        {
            Options = options;
            _compilation = compilation;
        }

        /// <summary>The project facts this session analyzes under.</summary>
        internal GeneratorOptions Options { get; }

        /// <summary>Models one payload, stopping before a cycle or an over-deep nested expansion can recurse.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <param name="depth">The number of nested payload expansions already entered.</param>
        /// <returns>The analysis result.</returns>
        internal ModelResult Create(INamedTypeSymbol symbol, int depth) => Analyze(symbol, depth, new Path());

        /// <summary>Returns a completed model when one exists, otherwise analyzes one payload and records it.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <param name="depth">The number of nested payload expansions already entered.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <returns>The analysis result.</returns>
        /// <remarks>A result is published to the session cache only when its own expansion saw no cycle. A model reached
        /// through a cycle keeps a framing that depends on the path, so it must be recomputed from another root.</remarks>
        private ModelResult Analyze(INamedTypeSymbol symbol, int depth, Path path)
        {
            if (depth > MaxDepth)
                return ModelResult.Unsupported("payload nesting exceeds " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " levels");
            var key = new AnalysisKey(symbol, depth);
            lock (_gate)
            {
                if (_completed.TryGetValue(key, out ModelResult? cached))
                    return cached;
            }
            if (!path.Active.Add(symbol))
            {
                path.SawCycle = true;
                return ModelResult.Unsupported("payload refers back to '" + symbol.ToDisplayString() + "'");
            }
            var issues = new List<string>();
            try
            {
                TypeModel? model = CreateCore(symbol, depth, issues, path);
                ModelResult result;
                if (model == null)
                    result = ModelResult.Unsupported(issues.Count == 0 ? "not a modeled payload" : string.Join("; ", issues));
                else if (issues.Count == 0)
                    result = ModelResult.Ready(model);
                else
                    result = ModelResult.Incomplete(model, string.Join("; ", issues.Distinct(StringComparer.Ordinal)));
                if (!path.SawCycle)
                {
                    lock (_gate)
                        _completed[key] = result;
                }
                return result;
            }
            finally
            {
                path.Active.Remove(symbol);
            }
        }

        /// <summary>Classifies one payload and builds the model shared by every payload kind.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reasons the model is not complete.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <returns>The model, or null when the declaration itself is outside the surface.</returns>
        private TypeModel? CreateCore(INamedTypeSymbol symbol, int depth, List<string> issues, Path path)
        {
            var attribute = symbol.GetAttributes().FirstOrDefault(item =>
                item.AttributeClass?.ToDisplayString() == PackableAttribute);
            if (attribute == null)
            {
                issues.Add("not a LuminPackable declaration");
                return null;
            }
            if (symbol.IsStatic)
            {
                issues.Add("a static declaration has no serialized instance");
                return null;
            }
            if (symbol.IsRefLikeType)
            {
                issues.Add("a ref-like declaration cannot be stored across a generated operation");
                return null;
            }

            TypeIdentity identity;
            try
            {
                identity = Identity(symbol);
            }
            catch (NotSupportedException error)
            {
                issues.Add(error.Message);
                return null;
            }

            string generatorType = GeneratorTypeName(attribute);
            if (generatorType is "NonGenerator" or "Custom")
            {
                issues.Add("generator type '" + generatorType + "' owns its formatter outside the payload renderers");
                return null;
            }

            TypeAccessibility accessibility = symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public
                ? TypeAccessibility.Public : TypeAccessibility.Internal;
            string[] constraints = symbol.TypeParameters.Select(parameter => parameter.Name).ToArray();
            PayloadKind kind = Kind(symbol, generatorType);
            WirePolicy policy = TypePolicy(symbol, kind);

            switch (kind)
            {
                case PayloadKind.Struct:
                {
                    MemberModel[] members = Members(symbol, policy, depth, issues, out _, path);
                    PortableLayout? layout = symbol.IsUnmanagedType && generatorType == "Object" ? TryPortableLayout(symbol) : null;
                    StructStorage storage = !symbol.IsUnmanagedType ? StructStorage.ManagedRecord :
                        layout == null ? StructStorage.RawUnmanaged : StructStorage.PortableUnmanaged;
                    // A managed record body is framed by the declaration's constructors, callbacks and rent; a value type that
                    // is version-tolerant writes a schema prefix the struct roots do not reproduce.
                    bool managed = storage == StructStorage.ManagedRecord;
                    return new StructModel(identity, accessibility, members, policy, constraints, storage, layout,
                        RecordWidthSupported(symbol), versioned: generatorType == "VersionTolerant",
                        rent: managed && Rent(symbol), constructors: managed && Constructors(symbol),
                        callbacks: managed && Callbacks(symbol));
                }
                case PayloadKind.Union:
                {
                    var declared = UnionMembers(symbol, issues);
                    bool wide = HasAttribute(symbol, WideTagAttribute) || OptionSwitch(symbol, "WideTag") ||
                        declared.Any(item => item.Tag >= 250);
                    HashSet<ITypeSymbol> fixedCases = FixedUnionCases(symbol, declared, issues);
                    UnionCaseModel[] cases = UnionCases(symbol, declared, fixedCases, depth, issues, path, out bool readGeneric);
                    if (cases.Length == 0)
                        issues.Add("the Union root declares no cases");
                    UnionDispatchTier tier = UnionTier(symbol, declared);
                    int encoding = (int)policy.Strings.Encoding, recording = (int)policy.Strings.Recording;
                    return new UnionModel(identity, accessibility, policy, constraints, cases,
                        isInterface: symbol.TypeKind == TypeKind.Interface, wideTags: wide, unionSuffix: SlotSuffix(identity),
                        tier: tier, tierCases: TierCases(declared, tier), tierSealed: TierSealed(declared, tier),
                        cursor: fixedCases.Count != 0, readGeneric: readGeneric, chain: Chain(symbol), @namespace: identity.Namespace,
                        binarySlots: UnionSlots(json: false, encoding, recording), jsonSlots: UnionSlots(json: true, encoding, 0),
                        canonical: CanonicalUnionProfiles(),
                        standardBinary: UnionProfileAlias(encoding, recording),
                        standardJson: UnionProfileAlias(encoding, recording));
                }
                case PayloadKind.CircleReference:
                {
                    MemberModel[] members = Members(symbol, policy, depth, issues, out bool derived, path);
                    MirrorFields(symbol, out string[]? mirrorFields, out int[]? mirrorOffsets, out string? mirrorLayout);
                    return new CircleReferenceModel(identity, accessibility, members, policy, constraints, IsRecursive(symbol), derived,
                        Rent(symbol), SelectedConstructor(symbol), Callbacks(symbol), FieldsInOrder(symbol, members),
                        mirrorFields, mirrorOffsets, mirrorLayout);
                }
                default:
                {
                    MemberModel[] members = Members(symbol, policy, depth, issues, out bool derived, path);
                    bool versioned = generatorType == "VersionTolerant";
                    // Storage reached through auto-property or included non-public fields needs the legacy layout mirror before
                    // the record emitters can write or read it. Every shape the mirror cannot reproduce stays unmapped.
                    string? mirror = null, mirrorLayout = null;
                    string[]? mirrorFields = null;
                    int[]? mirrorOffsets = null;
                    VersionSchemaModel? version = null;
                    if (versioned)
                    {
                        version = TryVersionSchema(symbol, identity, members, policy, derived, issues);
                        if (members.Any(member => member.Mirrored))
                            TryVersionMirror(symbol, identity, members, issues,
                                out mirror, out mirrorFields, out mirrorOffsets, out mirrorLayout);
                    }
                    else if (members.Any(member => member.Mirrored))
                        TryMirror(symbol, identity, versioned, derived, policy, members, issues,
                            out mirror, out mirrorFields, out mirrorOffsets, out mirrorLayout);
                    bool proven = !versioned && !derived && mirror == null && policy.Bound == null && Options.Net8 && Options.Unsafe &&
                        !Options.Unity && members.Length != 0 && members.Any(member => member is TextMember) &&
                        members.All(member => member is ScalarMember or TextMember);
                    return new RecordModel(identity, accessibility, members, policy, constraints, versioned,
                        fixedUnionCase: !versioned && members.All(member => member is ScalarMember) && IsFixedUnionCase(symbol),
                        derived, proven, RecordWidthSupported(symbol), CanDeserializeDirectlyIntoFreshResult(symbol, members),
                        mirror, mirrorFields, mirrorOffsets, mirrorLayout, version);
                }
            }
        }

        /// <summary>Enumerates the serialized storage of a payload and its base chain, base members first.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <param name="ownerPolicy">The policy resolved on the declaring payload.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reasons a member could not be modeled.</param>
        /// <param name="derived">Whether the payload has a base type whose storage participates in its record.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <returns>The members in wire order.</returns>
        private MemberModel[] Members(INamedTypeSymbol symbol, WirePolicy ownerPolicy, int depth, List<string> issues, out bool derived,
            Path path)
        {
            var levels = new List<INamedTypeSymbol>();
            for (var level = symbol; level != null && level.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
                level = level.BaseType)
                levels.Insert(0, level);
            derived = levels.Count > 1;

            var members = new List<MemberModel>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var level in levels)
            foreach (var member in OrderedStorageMembers(level))
            {
                if (!TryStorage(member, out MemberStorage storage))
                    continue;
                WirePolicy policy = MemberPolicy(member, ownerPolicy);
                MemberModel? model = Classify(member, storage, policy, symbol, depth, issues, path);
                if (model == null)
                    continue;
                if (!names.Add(model.Name))
                {
                    issues.Add("the payload chain declares member '" + model.Name + "' more than once");
                    continue;
                }
                members.Add(model);
            }
            return members.ToArray();
        }

        /// <summary>Decides whether a declared member has serialized instance storage and how it is reached.</summary>
        /// <param name="member">The declared member.</param>
        /// <param name="storage">Receives the storage shape.</param>
        /// <returns>Whether the member participates in the wire format.</returns>
        private static bool TryStorage(ISymbol member, out MemberStorage storage)
        {
            switch (member)
            {
                case IFieldSymbol field when !field.IsStatic && !field.IsConst:
                    if (HasAttribute(field, IgnoreAttribute))
                        break;
                    if (field.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public)
                    {
                        storage = MemberStorage.Field;
                        return true;
                    }
                    if (HasAttribute(field, IncludeAttribute))
                    {
                        storage = MemberStorage.IncludedField;
                        return true;
                    }
                    break;
                case IPropertySymbol property when IsAutoProperty(property):
                    if (HasAttribute(property, IgnoreAttribute))
                        break;
                    if (property.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public ||
                        HasAttribute(property, IncludeAttribute))
                    {
                        storage = MemberStorage.AutoProperty;
                        return true;
                    }
                    break;
            }
            storage = default;
            return false;
        }

        /// <summary>Classifies one serialized member into its model shape.</summary>
        /// <param name="member">The declared field or auto-property.</param>
        /// <param name="storage">How the member's storage is reached.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <param name="owner">The payload that declares the member.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reason an unsupported member is omitted.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <returns>The member model, or null when the type is outside the modeled surface.</returns>
        private MemberModel? Classify(ISymbol member, MemberStorage storage, WirePolicy policy, INamedTypeSymbol owner, int depth,
            List<string> issues, Path path)
        {
            ITypeSymbol type = member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;
            string name = member.Name;

            if (type is ITypeParameterSymbol parameter)
            {
                if (!Owns(parameter, owner))
                {
                    issues.Add("member '" + name + "' uses the foreign type parameter '" + parameter.Name + "'");
                    return null;
                }
                return new TypeParameterMember(name, storage, policy, parameter.Name, parameter.HasValueTypeConstraint,
                    parameter.HasReferenceTypeConstraint);
            }
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            {
                if (type.NullableAnnotation == NullableAnnotation.Annotated || enumType.EnumUnderlyingType == null ||
                    Scalar(enumType.EnumUnderlyingType) is not { } underlying)
                {
                    issues.Add("member '" + name + "' is a nullable or unsupported enum");
                    return null;
                }
                return new ScalarMember(name, storage, policy, underlying.Keyword, underlying.Size, raw: false, builtin: false,
                    enumType: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), underlying: underlying.Keyword);
            }
            if (Scalar(type) is { } scalar)
                return new ScalarMember(name, storage, policy, scalar.Keyword, scalar.Size, raw: false, builtin: false);
            if (Builtin(type) is { } builtin)
                return new ScalarMember(name, storage, policy, builtin, 0, raw: true, builtin: true);
            if (type.SpecialType == SpecialType.System_String)
                return new TextMember(name, storage, policy);
            if (type is IArrayTypeSymbol array)
                return ArrayMember(name, storage, policy, array);

            if (type is INamedTypeSymbol named)
            {
                if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    issues.Add("member '" + name + "' is a nullable value type, which has no modeled scalar form");
                    return null;
                }
                if (DictionaryKind(named) is { } dictionaryKind)
                    return DictionaryMember(name, storage, policy, named, dictionaryKind);
                if (SequenceKind(named) is { } collectionKind)
                    return SequenceMemberModel(name, storage, policy, named, collectionKind, depth, issues, path);
                if (named.TypeKind is TypeKind.Class or TypeKind.Struct)
                    return Payload(member, named, storage, policy, depth, issues, path);
            }

            issues.Add("member '" + name + "' has the unsupported type '" + type.ToDisplayString() + "'");
            return null;
        }

        /// <summary>Models a one-dimensional or higher array member.</summary>
        /// <param name="name">The member name.</param>
        /// <param name="storage">How the member's storage is reached.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <param name="array">The declared array type.</param>
        /// <returns>The sequence member.</returns>
        /// <remarks>A one-dimensional array of raw scalars stores its span directly; every other array writes through its
        /// generated formatter, and a multi-dimensional array keeps its runtime-sized raw path.</remarks>
        private static MemberModel ArrayMember(string name, MemberStorage storage, WirePolicy policy, IArrayTypeSymbol array)
        {
            string declared = array.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            TypeRef element = Reference(array.ElementType);
            if (array.Rank != 1)
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element,
                    scalar: declared, raw: true, formatterCall: true);
            if (Scalar(array.ElementType) is { } scalar)
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element, scalar: scalar.Keyword);
            return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element,
                scalar: declared, formatterCall: true);
        }

        /// <summary>Models a generic sequence member of a covered family.</summary>
        /// <param name="name">The member name.</param>
        /// <param name="storage">How the member's storage is reached.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <param name="type">The declared sequence type.</param>
        /// <param name="kind">The declared family.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reason an element payload is not expanded.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <returns>The sequence member.</returns>
        private MemberModel SequenceMemberModel(string name, MemberStorage storage, WirePolicy policy, INamedTypeSymbol type,
            CollectionKind kind, int depth, List<string> issues, Path path)
        {
            string declared = DeclaredType(type);
            ITypeSymbol item = type.TypeArguments[0];
            TypeRef element = Reference(item);
            if (kind == CollectionKind.Stack && Scalar(item) is { } stackItem)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: stackItem.Keyword, raw: true);
            if (kind == CollectionKind.SortedSet && Scalar(item) is { } setItem)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: setItem.Keyword, raw: true);
            if (kind is not (CollectionKind.List or CollectionKind.NestedList))
                return new CollectionMember(name, storage, policy, declared, kind, element);

            // A list of raw scalars stores its span directly; a raw unmanaged struct keeps its runtime-sized storage path.
            if (Scalar(item) is { } scalar)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: scalar.Keyword);
            if (IsReservedElement(item))
                return new CollectionMember(name, storage, policy, declared, kind, element,
                    scalar: item.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            // A list of strings writes its payload through one span call and reads through its element formatter.
            if (item.SpecialType == SpecialType.System_String)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: "string", elementText: true);
            // A list of key/value pairs writes each pair's key and value.
            if (item is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 2 } pair &&
                pair.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            {
                var pairKey = Scalar(pair.TypeArguments[0], allowBoolean: false);
                bool pairValueText = pair.TypeArguments[1].SpecialType == SpecialType.System_String;
                var pairValue = pairValueText ? null : Scalar(pair.TypeArguments[1], allowBoolean: false);
                if (pairKey != null && (pairValueText || pairValue != null))
                    return new CollectionMember(name, storage, policy, declared, kind, element, scalar: declared,
                        shape: new CollectionShape(pairKey.Value.Keyword, pairValueText ? "string" : pairValue.GetValueOrDefault().Keyword,
                            pairValueText, entryType: pair.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
            // A list of lists whose inner elements expand their record body writes both levels in place.
            if (item is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } innerList &&
                innerList.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" &&
                LocalPackableClass(innerList.TypeArguments[0]) is { } nestedElement)
            {
                TypeModel? nested = ElementModel(nestedElement, depth, issues, path, name);
                if (nested == null)
                    return new CollectionMember(name, storage, policy, declared, kind, element);
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: declared,
                    shape: new CollectionShape(null, null, false,
                        itemType: innerList.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)), nested: nested);
            }
            // A list of packable classes expands each element's record body in the loop.
            if (LocalPackableClass(item) is { } elementType)
            {
                TypeModel? nested = ElementModel(elementType, depth, issues, path, name);
                if (nested == null)
                    return new CollectionMember(name, storage, policy, declared, kind, element);
                return new CollectionMember(name, storage, policy, declared, kind, element,
                    scalar: elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), nested: nested);
            }
            return new CollectionMember(name, storage, policy, declared, kind, element);
        }

        /// <summary>Models a dictionary, sorted list or sparse dictionary member.</summary>
        /// <param name="name">The member name.</param>
        /// <param name="storage">How the member's storage is reached.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <param name="type">The declared keyed type.</param>
        /// <param name="kind">The declared family.</param>
        /// <returns>The keyed member.</returns>
        private MemberModel DictionaryMember(string name, MemberStorage storage, WirePolicy policy, INamedTypeSymbol type, DictionaryKind kind)
        {
            string declared = DeclaredType(type);
            TypeRef key = Reference(type.TypeArguments[0]), value = Reference(type.TypeArguments[1]);
            // A sorted list of a raw key and a raw or string value writes both stored arrays as paired entries.
            if (kind == Model.DictionaryKind.SortedList)
            {
                var sortedKey = Scalar(type.TypeArguments[0], allowBoolean: false);
                bool sortedValueText = type.TypeArguments[1].SpecialType == SpecialType.System_String;
                var sortedValue = sortedValueText ? null : Scalar(type.TypeArguments[1], allowBoolean: false);
                if (sortedKey != null && (sortedValueText || sortedValue != null))
                    return new DictionaryMember(name, storage, policy, declared, kind, key, value, keyText: false,
                        valueText: sortedValueText, keyType: sortedKey.Value.Keyword,
                        valueType: sortedValueText ? "string" : sortedValue.GetValueOrDefault().Keyword);
                return new DictionaryMember(name, storage, policy, declared, kind, key, value);
            }
            if (kind != Model.DictionaryKind.Dictionary)
                return new DictionaryMember(name, storage, policy, declared, kind, key, value);
            // A dictionary writes fused entries when a string member bounds its record, or paired records when both key and
            // value are raw scalars.
            int reserved = ReservedKind(type);
            bool keyText = type.TypeArguments[0].SpecialType == SpecialType.System_String;
            bool valueText = type.TypeArguments[1].SpecialType == SpecialType.System_String;
            var keyRaw = keyText ? null : Scalar(type.TypeArguments[0], allowBoolean: false);
            var valueRaw = valueText ? null : Scalar(type.TypeArguments[1], allowBoolean: false);
            if (reserved is 5 or 6 && (keyText || keyRaw != null) && (valueText || valueRaw != null))
                return new DictionaryMember(name, storage, policy, declared, kind, key, value, pair: reserved == 5, keyText, valueText,
                    keyType: keyRaw?.Keyword, valueType: valueRaw?.Keyword);
            // A dictionary whose value is a nullable raw scalar writes sparse entries with a varint key and the fixed value.
            if (reserved == 0 && Scalar(type.TypeArguments[1]) == null &&
                type.TypeArguments[1] is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } nullable &&
                nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                Scalar(nullable.TypeArguments[0]) is { } innerValue && Scalar(type.TypeArguments[0], allowBoolean: false) is { } innerKey)
                return new DictionaryMember(name, storage, policy, declared, kind, key, value, sparse: true,
                    keyType: innerKey.Keyword, valueType: innerValue.Keyword + "?");
            return new DictionaryMember(name, storage, policy, declared, kind, key, value);
        }

        /// <summary>Expands the element payload of a list, when its complete record can be written in place.</summary>
        /// <param name="element">The element payload type.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reason the element is not expanded.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <param name="memberName">The declaring member, for the issue text.</param>
        /// <returns>The element model, or null when the element is outside the inline record shape.</returns>
        private TypeModel? ElementModel(INamedTypeSymbol element, int depth, List<string> issues, Path path, string memberName)
        {
            if (path.Active.Contains(element))
            {
                path.SawCycle = true;
                issues.Add("member '" + memberName + "' refers back to '" + element.ToDisplayString() + "' before its payload is complete");
                return null;
            }
            ModelResult nested = Analyze(element, depth + 1, path);
            if (nested.Model is RecordModel { Versioned: false } record && nested.IsComplete)
                return record;
            issues.Add("member '" + memberName + "': " + (nested.Reason ?? "the element payload is not a plain record"));
            return null;
        }

        /// <summary>Recognizes a packable class of the consumer assembly that a list can expand element by element.</summary>
        /// <param name="type">The candidate element type.</param>
        /// <returns>The class symbol, or null for every other declaration.</returns>
        private INamedTypeSymbol? LocalPackableClass(ITypeSymbol type)
            => type is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } named && named.SpecialType == SpecialType.None &&
                SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, _compilation.Assembly) && IsPackable(named) ? named : null;

        /// <summary>Classifies a member whose complete record has an upper bound computable from one read of its storage.</summary>
        /// <param name="type">The declared member type.</param>
        /// <returns>Zero for an unbounded member, four for a raw list and five or six for a bounded-entry dictionary.</returns>
        private int ReservedKind(INamedTypeSymbol type)
        {
            if (TryPortableLayout(type) != null || RequiresClosedDispatch(type))
                return 0;
            if (type is { IsGenericType: true, TypeArguments.Length: 1 } list &&
                list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" && IsReservedElement(list.TypeArguments[0]))
                return 4;
            if (type is { IsGenericType: true, TypeArguments.Length: 2 } dictionary &&
                dictionary.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>")
            {
                if (IsReservedElement(dictionary.TypeArguments[0]) && IsReservedElement(dictionary.TypeArguments[1]))
                    return 5;
                // Raw and string members bound each entry from one read, so the dictionary is an open record checked entry by entry.
                if (IsEntryMember(dictionary.TypeArguments[0]) && IsEntryMember(dictionary.TypeArguments[1]))
                    return 6;
            }
            return 0;
        }

        /// <summary>Accepts a dictionary key or value whose record has a bound computable from one read.</summary>
        /// <param name="type">The key or value type.</param>
        /// <returns>Whether it is a string or raw element storage.</returns>
        private bool IsEntryMember(ITypeSymbol type)
            => type.SpecialType == SpecialType.System_String || IsReservedElement(type);

        /// <summary>Accepts only elements the raw collection path copies as their native storage.</summary>
        /// <param name="element">The element type.</param>
        /// <returns>Whether the element is closed, raw and unmanaged, with no separate framing contract.</returns>
        private bool IsReservedElement(ITypeSymbol element)
        {
            if (element is ITypeParameterSymbol || element.IsRefLikeType || !element.IsUnmanagedType)
                return false;
            if (element is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
                return false;
            if (element is not INamedTypeSymbol named)
                return true;
            // A pair's per-value formatter frames its two members separately, so it is never raw storage.
            if (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
                return false;
            if (GenerationMode(named) is "NonGenerator" or "Custom")
                return false;
            if (HasAttribute(named, UnionAttribute) || HasAttribute(named, "System.Runtime.CompilerServices.UnionAttribute"))
                return false;
            return TryPortableLayout(named) == null;
        }

        /// <summary>Whether a generic declaration still dispatches its unmanaged storage through a closed formatter.</summary>
        /// <param name="type">The candidate storage type.</param>
        /// <returns>Whether its width depends on an undetermined type argument.</returns>
        private static bool RequiresClosedDispatch(ITypeSymbol type)
            => type is INamedTypeSymbol { IsGenericType: true } named && IsPackable(named) && GenerationMode(named) == "Object";

        /// <summary>Models a member that embeds another payload or a generic definition.</summary>
        /// <param name="member">The declared member.</param>
        /// <param name="type">The member's class or struct type.</param>
        /// <param name="storage">How the member's storage is reached.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reason a nested payload is not expanded.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <returns>The payload member.</returns>
        private MemberModel? Payload(ISymbol member, INamedTypeSymbol type, MemberStorage storage, WirePolicy policy, int depth,
            List<string> issues, Path path)
        {
            string name = member.Name;
            if (!IsPackable(type))
            {
                // A type that mentions the definition's own parameters is written and read by the generic serializer.
                if (MentionsOwnParameter(type, member.ContainingType))
                    return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);
                issues.Add("member '" + name + "' has the unsupported type '" + type.ToDisplayString() + "'");
                return null;
            }
            // A type that owns its formatter outside the payload renderers is referenced through its registered member entry.
            if (GenerationMode(type) is "NonGenerator" or "Custom")
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);
            // A generic payload closes over a formatter contract at the operation boundary; its complete member graph is not
            // expanded here, so it stays a member entry.
            if (type.IsGenericType)
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);
            if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);

            if (path.Active.Contains(type))
            {
                // The framing of this member depends on the expansion path, so neither this payload nor its ancestors may be
                // published to the session cache.
                path.SawCycle = true;
                if (GenerationMode(type) == "CircleReference")
                    return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.ReferenceTracked, nested: null);
                issues.Add("member '" + name + "' refers back to '" + type.ToDisplayString() + "' before its payload is complete");
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);
            }

            ModelResult nested = Analyze(type, depth + 1, path);
            if (nested.Model == null)
            {
                issues.Add("member '" + name + "': " + (nested.Reason ?? "the nested payload is not modeled"));
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);
            }
            if (!nested.IsComplete)
            {
                issues.Add("member '" + name + "': " + nested.Reason);
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);
            }
            return new PayloadMember(name, storage, policy, Reference(type), ShapeOf(nested.Model), nested.Model);
        }

        /// <summary>Reads the declared Union cases in declaration order.</summary>
        /// <param name="symbol">The Union root.</param>
        /// <param name="issues">Receives invalid case declarations.</param>
        /// <returns>The declared tag and case type of each attribute.</returns>
        private static List<(int Tag, INamedTypeSymbol Type)> UnionMembers(INamedTypeSymbol symbol, List<string> issues)
        {
            var cases = new List<(int Tag, INamedTypeSymbol Type)>();
            var tags = new HashSet<int>();
            foreach (var attribute in symbol.GetAttributes().Where(item => item.AttributeClass?.ToDisplayString() == UnionAttribute))
            {
                if (attribute.ConstructorArguments.Length < 2)
                    continue;
                int tag = Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
                if (!tags.Add(tag))
                {
                    issues.Add("the Union root declares tag " + tag.ToString(CultureInfo.InvariantCulture) + " more than once");
                    continue;
                }
                if (attribute.ConstructorArguments[1].Value is not INamedTypeSymbol caseType ||
                    caseType.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " does not name a class or struct case");
                    continue;
                }
                if (caseType.IsGenericType && !SymbolEqualityComparer.Default.Equals(caseType, caseType.OriginalDefinition))
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " names the constructed generic case '" +
                        caseType.ToDisplayString() + "', which has no modeled identity");
                    continue;
                }
                cases.Add((tag, caseType));
            }
            return cases;
        }

        /// <summary>Models each declared case: its own read entry, read profile, sealing, cursor and expanded payload.</summary>
        /// <param name="root">The Union root.</param>
        /// <param name="declared">The declared tag and case type of each attribute.</param>
        /// <param name="fixedCases">The cases whose complete fixed payload uses the returning cursor kernel.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives invalid case declarations.</param>
        /// <param name="path">The payloads on the current expansion path.</param>
        /// <param name="readGeneric">Receives whether any case read entry takes the operation's profile.</param>
        /// <returns>The modeled cases.</returns>
        private UnionCaseModel[] UnionCases(INamedTypeSymbol root, List<(int Tag, INamedTypeSymbol Type)> declared,
            HashSet<ITypeSymbol> fixedCases, int depth, List<string> issues, Path path, out bool readGeneric)
        {
            readGeneric = false;
            var cases = new List<UnionCaseModel>(declared.Count);
            var symbols = declared.Select(item => item.Type).ToArray();
            foreach (var (tag, caseType) in declared)
            {
                if (caseType.TypeKind != TypeKind.Class || caseType.IsAbstract || caseType.IsGenericType || caseType.IsUnboundGenericType)
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " names the case '" +
                        caseType.ToDisplayString() + "', which is not an ordinary class record");
                    continue;
                }
                bool ownEntry = ReadsOwnUnionEntry(caseType);
                ModelResult analyzed = Analyze(caseType, 0, path);
                TypeModel? caseModel = analyzed.Model;
                bool caseReadGeneric = caseModel == null || NeedsReadProfile(caseModel);
                if (!ownEntry && (caseModel == null || !analyzed.IsComplete || caseModel is UnionModel || caseModel is StructModel ||
                    caseModel.Members.Length == 0 || caseModel.Members.Any(member => member.Mirrored) ||
                    caseModel.Members.Any(member => member is not ScalarMember) || SelectedConstructor(caseType)))
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " case read: '" +
                        caseType.ToDisplayString() + "' is not a fixed record this pipeline expands");
                    caseReadGeneric = true;
                }
                readGeneric |= caseReadGeneric;
                bool sealedCase = root.TypeKind == TypeKind.Class && !HasDerivedCase(symbols, caseType.OriginalDefinition);
                try
                {
                    string globalName = caseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    cases.Add(new UnionCaseModel(tag, Identity(caseType),
                        ownEntry ? UnionCaseRead.OwnEntry : UnionCaseRead.ExpandedFixed, ownEntry, caseReadGeneric, sealedCase,
                        fixedCases.Contains(caseType), caseModel, ReadableTypeName.Of(GeneratedNames.PayloadKey(globalName)),
                        Chain(caseType), NamespaceOf(caseType)));
                }
                catch (NotSupportedException error)
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + ": " + error.Message);
                }
            }
            return cases.ToArray();
        }

        /// <summary>Whether another declared case derives from or implements the case definition.</summary>
        /// <param name="cases">The declared case symbols.</param>
        /// <param name="definition">The case definition.</param>
        /// <returns>Whether a derived case exists.</returns>
        private static bool HasDerivedCase(INamedTypeSymbol[] cases, INamedTypeSymbol definition)
        {
            foreach (var item in cases)
            {
                if (SymbolEqualityComparer.Default.Equals(item.OriginalDefinition, definition))
                    continue;
                if (UnionRootMatch(item, definition) != null)
                    return true;
            }
            return false;
        }

        /// <summary>Finds the base or interface of a case that is the given root definition.</summary>
        /// <param name="member">The declared case.</param>
        /// <param name="root">The union root definition.</param>
        /// <returns>The matched base or interface, or null.</returns>
        private static INamedTypeSymbol? UnionRootMatch(INamedTypeSymbol member, INamedTypeSymbol root)
        {
            if (root.TypeKind == TypeKind.Class)
            {
                for (var current = member.BaseType; current != null; current = current.BaseType)
                    if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, root))
                        return current;
                return null;
            }
            foreach (var current in member.AllInterfaces)
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, root))
                    return current;
            return null;
        }

        /// <summary>Whether the case is a local reference type with a variable payload, whose binary read entry the dispatch
        /// calls instead of expanding its members.</summary>
        /// <param name="type">The concrete case.</param>
        /// <returns>Whether the case owns its Union read entry.</returns>
        private bool ReadsOwnUnionEntry(INamedTypeSymbol type)
            => type is { IsReferenceType: true, IsAbstract: false, TypeKind: TypeKind.Class } &&
               SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly) &&
               !ContainsTypeParameter(type) && GenerationMode(type) != "Custom" && HasVariablePayload(type);

        /// <summary>Whether some instance field or property of the case or its bases is not unmanaged storage.</summary>
        /// <param name="type">The concrete case.</param>
        /// <returns>Whether the case payload is variable.</returns>
        private static bool HasVariablePayload(INamedTypeSymbol type)
        {
            for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
                foreach (var member in current.GetMembers())
                {
                    if (member.IsStatic || member.IsImplicitlyDeclared)
                        continue;
                    if (member is IFieldSymbol field && !field.Type.IsUnmanagedType)
                        return true;
                    if (member is IPropertySymbol { IsIndexer: false } property && !property.Type.IsUnmanagedType)
                        return true;
                }
            return false;
        }

        /// <summary>Whether a case's binary read entry takes the operation's profile.</summary>
        /// <param name="model">The analyzed case.</param>
        /// <returns>Whether the case read is generic.</returns>
        private static bool NeedsReadProfile(TypeModel model) => model switch
        {
            RecordModel record => record.Versioned || ProfilePayload(record) ||
                record.Members.Any(member => member is not (ScalarMember or PayloadMember { Shape: PayloadShape.Inline })) ||
                InlineArray(record) || InlineList(record),
            _ => true,
        };

        /// <summary>Whether a string anywhere in this record or in an inline object below it needs the profile.</summary>
        /// <param name="model">The record model.</param>
        /// <returns>Whether the profile is required.</returns>
        private static bool ProfilePayload(TypeModel model) => model.Members.Any(member => member switch
        {
            TextMember or TypeParameterMember => true,
            CollectionMember { Scalar: "string" } => true,
            CollectionMember collection => collection.Nested != null && ProfilePayload(collection.Nested),
            DictionaryMember dictionary => dictionary.KeyText || dictionary.ValueText,
            PayloadMember { Shape: PayloadShape.Inline, Nested: { } nested } => ProfilePayload(nested),
            PayloadMember { Shape: PayloadShape.ReferenceTracked } => true,
            _ => false,
        });

        /// <summary>Whether an array inside an inline object makes the parent read root generic.</summary>
        /// <param name="model">The record model.</param>
        /// <returns>Whether such an array exists.</returns>
        private static bool InlineArray(TypeModel model) => model.Members.Any(member =>
            member is PayloadMember { Shape: PayloadShape.Inline, Nested: { } nested } &&
            (nested.Members.Any(item => item is CollectionMember { Kind: CollectionKind.Array }) || InlineArray(nested)));

        /// <summary>Whether a list inside an inline object makes the parent read root generic.</summary>
        /// <param name="model">The record model.</param>
        /// <returns>Whether such a list exists.</returns>
        private static bool InlineList(TypeModel model) => model.Members.Any(member =>
            member is PayloadMember { Shape: PayloadShape.Inline, Nested: { } nested } &&
            (nested.Members.Any(item => item is CollectionMember { Kind: CollectionKind.List }) || InlineList(nested)));

        /// <summary>Lists the partial declarations from the outermost containing type to the given type.</summary>
        /// <param name="type">The declared type.</param>
        /// <returns>The partial declaration lines.</returns>
        private static string[] Chain(INamedTypeSymbol type)
        {
            var chain = new List<string>();
            for (var current = type; current != null; current = current.ContainingType)
                chain.Insert(0, UnionDeclaration(current));
            return chain.ToArray();
        }

        /// <summary>Spells one partial declaration of a containing chain.</summary>
        /// <param name="type">The declared type.</param>
        /// <returns>The partial declaration line.</returns>
        private static string UnionDeclaration(INamedTypeSymbol type)
        {
            string accessibility = type.DeclaredAccessibility switch
            {
                Accessibility.Public => "public ",
                Accessibility.Internal => "internal ",
                Accessibility.Private => "private ",
                Accessibility.Protected => "protected ",
                Accessibility.ProtectedAndInternal => "private protected ",
                Accessibility.ProtectedOrInternal => "protected internal ",
                _ => string.Empty,
            };
            string name = SyntaxFacts.GetKeywordKind(type.Name) == SyntaxKind.None ? type.Name : "@" + type.Name;
            return accessibility + "partial " + (type.TypeKind == TypeKind.Interface ? "interface " : "class ") + name;
        }

        /// <summary>Spells a type's namespace, or null in the global namespace.</summary>
        /// <param name="type">The declared type.</param>
        /// <returns>The namespace, or null.</returns>
        private static string? NamespaceOf(INamedTypeSymbol type)
            => type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace
                ? null : type.ContainingNamespace.ToDisplayString();

        /// <summary>Selects the compiled dispatch tier of a local class Union root.</summary>
        /// <param name="root">The Union root.</param>
        /// <param name="declared">The declared cases.</param>
        /// <returns>The selected tier.</returns>
        private UnionDispatchTier UnionTier(INamedTypeSymbol root, List<(int Tag, INamedTypeSymbol Type)> declared)
        {
            if (root is not { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } || !CanGeneratePartial(root))
                return UnionDispatchTier.Virtual;
            if (declared.Count == 0 || Options.Net8 && declared.Count > 4)
                return UnionDispatchTier.Virtual;
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var item in declared.OrderBy(member => member.Tag))
            {
                if (item.Type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } ||
                    !CanGeneratePartial(item.Type) || !seen.Add(item.Type.OriginalDefinition))
                    return UnionDispatchTier.Virtual;
            }
            if (Options.Net8 && Options.RegisterMode == RegisterMode.Disabled && declared.Count == 1 &&
                declared[0].Type.IsSealed && ClosedUnion(root))
                return UnionDispatchTier.ExactType;
            return UnionDispatchTier.ExactPatterns;
        }

        /// <summary>Lists the exact tier cases in tag order, or nothing for the virtual tier.</summary>
        /// <param name="declared">The declared cases.</param>
        /// <param name="tier">The selected tier.</param>
        /// <returns>The fully qualified exact case types.</returns>
        private static string[] TierCases(List<(int Tag, INamedTypeSymbol Type)> declared, UnionDispatchTier tier)
            => tier == UnionDispatchTier.Virtual
                ? Array.Empty<string>()
                : declared.OrderBy(item => item.Tag).Select(item => item.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToArray();

        /// <summary>Lists whether each exact tier case is declared sealed, or nothing for the virtual tier.</summary>
        /// <param name="declared">The declared cases.</param>
        /// <param name="tier">The selected tier.</param>
        /// <returns>The sealed flags in tag order.</returns>
        private static bool[] TierSealed(List<(int Tag, INamedTypeSymbol Type)> declared, UnionDispatchTier tier)
            => tier == UnionDispatchTier.Virtual
                ? Array.Empty<bool>()
                : declared.OrderBy(item => item.Tag).Select(item => item.Type.IsSealed).ToArray();

        /// <summary>Checks that no other assembly can add a case to the root.</summary>
        /// <param name="root">The Union root.</param>
        /// <returns>Whether every implementation must be declared in this compilation.</returns>
        private bool ClosedUnion(INamedTypeSymbol root)
        {
            if (OptionSwitch(root, "SingleImplementation") || HasAttribute(root, "LuminPack.Attribute.LuminPackSingleImplementationAttribute"))
                return true;
            if (_compilation.Assembly.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute"))
                return false;
            for (var current = root; current != null; current = current.ContainingType)
                if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                    return true;
            return root.InstanceConstructors.All(constructor =>
                constructor.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal));
        }

        /// <summary>Lists the cases whose complete fixed payload uses the returning cursor kernel.</summary>
        /// <param name="root">The Union root.</param>
        /// <param name="declared">The declared cases.</param>
        /// <param name="issues">Receives the invalid declarations of the root.</param>
        /// <returns>The eligible cases, or empty when the root keeps the original protocol.</returns>
        private HashSet<ITypeSymbol> FixedUnionCases(INamedTypeSymbol root, List<(int Tag, INamedTypeSymbol Type)> declared, List<string> issues)
        {
            var cases = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            if (issues.Count != 0 || root is not { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } ||
                !IsLocal(root) || GenerationMode(root) != "Object" || HasAttribute(root, WideTagAttribute) ||
                OptionSwitch(root, "WideTag") || declared.Any(item => item.Tag >= 250))
                return cases;
            foreach (var item in declared.OrderBy(member => member.Tag))
                if (item.Type is { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } &&
                    CanGeneratePartial(item.Type) && CanUseCursorPayload(item.Type))
                    cases.Add(item.Type);
            return cases;
        }

        /// <summary>Whether the type is a declared case of a Union root that selects the returning cursor kernel.</summary>
        /// <param name="type">The candidate case.</param>
        /// <returns>Whether the case payload must publish the shared <c>SerializeUnionAt</c> entry.</returns>
        /// <remarks>The root and its declarations are re-checked from the case alone, because a case is also modeled as an
        /// independent payload; the checks mirror <see cref="FixedUnionCases"/> for that root.</remarks>
        private bool IsFixedUnionCase(INamedTypeSymbol type)
        {
            if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } || !IsLocal(type) ||
                GenerationMode(type) != "Object")
                return false;
            INamedTypeSymbol? root = FixedUnionRoot(type);
            if (root == null)
                return false;
            var issues = new List<string>();
            var declared = UnionMembers(root, issues);
            if (issues.Count != 0 || root is not { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } ||
                !IsLocal(root) || GenerationMode(root) != "Object" || HasAttribute(root, WideTagAttribute) ||
                OptionSwitch(root, "WideTag") || declared.Any(item => item.Tag >= 250))
                return false;
            return declared.Any(item => SymbolEqualityComparer.Default.Equals(item.Type, type)) && CanUseCursorPayload(type);
        }

        /// <summary>Finds the nearest base Union root that declares the given case.</summary>
        /// <param name="type">The candidate case.</param>
        /// <returns>The declaring root, or null when the type is not a declared case.</returns>
        private static INamedTypeSymbol? FixedUnionRoot(INamedTypeSymbol type)
        {
            for (var parent = type.BaseType; parent != null && parent.SpecialType != SpecialType.System_Object;
                parent = parent.BaseType)
            {
                if (!parent.IsAbstract || parent.IsGenericType || !IsPackable(parent))
                    continue;
                if (parent.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == UnionAttribute &&
                        attribute.ConstructorArguments.Length >= 2 &&
                        attribute.ConstructorArguments[1].Value is INamedTypeSymbol declared &&
                        SymbolEqualityComparer.Default.Equals(declared, type)))
                    return parent;
            }
            return null;
        }

        /// <summary>Whether a case payload is entirely public raw fields, as the returning cursor kernel requires.</summary>
        /// <param name="type">The concrete case.</param>
        /// <returns>Whether the case can use the shared fixed write body.</returns>
        private bool CanUseCursorPayload(INamedTypeSymbol type)
        {
            if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } || !IsLocal(type))
                return false;
            bool hasRoot = false;
            for (var parent = type.BaseType; parent != null && parent.SpecialType != SpecialType.System_Object; parent = parent.BaseType)
            {
                if (parent.IsGenericType)
                    return false;
                hasRoot |= parent.IsAbstract && IsLocal(parent) && IsPackable(parent);
            }
            if (!hasRoot)
                return false;
            int fields = 0;
            var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            for (var level = type; level != null && level.SpecialType != SpecialType.System_Object; level = level.BaseType)
            {
                if (GenerationMode(level) != "Object")
                    return false;
                foreach (var member in level.GetMembers())
                {
                    if (member is IMethodSymbol method && method.GetAttributes().Any(attribute =>
                            attribute.AttributeClass?.ToDisplayString() is "LuminPack.Attribute.LuminPackOnSerializingAttribute" or
                                "LuminPack.Attribute.LuminPackOnSerializedAttribute"))
                        return false;
                    if (member.IsStatic || member.IsImplicitlyDeclared)
                        continue;
                    if (member is IPropertySymbol)
                        return false;
                    if (member is not IFieldSymbol field)
                        continue;
                    if (field.DeclaredAccessibility != Accessibility.Public || field.IsVolatile ||
                        field.GetAttributes().Any(attribute =>
                            attribute.AttributeClass?.ContainingNamespace.ToDisplayString() == "LuminPack.Attribute"))
                        return false;
                    if (!HasRawContract(field.Type, visited))
                        return false;
                    fields++;
                }
            }
            return fields != 0;
        }

        /// <summary>Whether the complete storage of a field type is copied as raw bytes without a nested formatter boundary.</summary>
        /// <param name="type">The field type.</param>
        /// <param name="visited">The finite storage already inspected.</param>
        /// <returns>Whether the type is raw unmanaged storage.</returns>
        private static bool HasRawContract(ITypeSymbol type, HashSet<ITypeSymbol> visited)
        {
            if (type.NullableAnnotation == NullableAnnotation.Annotated || !type.IsUnmanagedType || ContainsTypeParameter(type))
                return false;
            if (!visited.Add(type))
                return true;
            if (type is not INamedTypeSymbol named)
                return true;
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || GenerationMode(named) != "Object" ||
                HasAttribute(named, UnionAttribute) || HasAttribute(named, "System.Runtime.CompilerServices.UnionAttribute"))
                return false;
            return named.TypeKind != TypeKind.Struct || named.GetMembers().OfType<IFieldSymbol>()
                .Where(field => !field.IsStatic).All(field => HasRawContract(field.Type, visited));
        }

        /// <summary>Whether the type is declared in this compilation and can receive partial members.</summary>
        /// <param name="type">The candidate type.</param>
        /// <returns>Whether partial dispatch can be emitted into it.</returns>
        private bool IsLocal(INamedTypeSymbol type)
            => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly) && CanGeneratePartial(type);

        /// <summary>Whether every declaration of the type and its containing types is partial source.</summary>
        /// <param name="type">The candidate type.</param>
        /// <returns>Whether a partial declaration can be added.</returns>
        private static bool CanGeneratePartial(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.ContainingType)
            {
                if (current.DeclaringSyntaxReferences.Length == 0)
                    return false;
                foreach (var reference in current.DeclaringSyntaxReferences)
                    if (reference.GetSyntax() is not TypeDeclarationSyntax declaration ||
                        !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                        return false;
            }
            return true;
        }

        /// <summary>Lists the compiled binary or JSON slots of a declared profile in profile order.</summary>
        /// <param name="json">Whether only the JSON encoding selects a slot.</param>
        /// <param name="declaredEncoding">The declared string encoding.</param>
        /// <param name="declaredRecording">The declared string recording the slot family compares against.</param>
        /// <returns>The slots.</returns>
        private static UnionSlotModel[] UnionSlots(bool json, int declaredEncoding, int declaredRecording)
        {
            var slots = new List<UnionSlotModel>();
            foreach (var (encoding, recording) in UnionProfiles(json))
            {
                string identity = UnionProfileIdentity(encoding, recording);
                slots.Add(new UnionSlotModel(encoding == declaredEncoding && recording == declaredRecording ? string.Empty : "_Options_" + identity,
                    UnionProfileAlias(encoding, recording), identity, encoding, recording));
            }
            return slots.ToArray();
        }

        /// <summary>Lists the finite profile combinations in the legacy slot order.</summary>
        /// <param name="json">Whether only the JSON encoding selects a slot.</param>
        /// <returns>The encoding and recording pairs.</returns>
        private static IEnumerable<(int Encoding, int Recording)> UnionProfiles(bool json)
        {
            for (int encoding = 0; encoding != 2; encoding++)
                for (int recording = 0; recording != (json ? 1 : 2); recording++)
                    yield return (encoding, recording);
            if (!json)
                for (int encoding = 0; encoding != 2; encoding++)
                    yield return (encoding, 2);
        }

        /// <summary>Spells the stable identity of one canonical Union profile.</summary>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="recording">The string recording.</param>
        /// <returns>The profile identity.</returns>
        private static string UnionProfileIdentity(int encoding, int recording) =>
            "E" + encoding.ToString(CultureInfo.InvariantCulture) + "R" + recording.ToString(CultureInfo.InvariantCulture) +
            "F71P255D1024V0";

        /// <summary>The imported string options type of a profile.</summary>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="recording">The string recording.</param>
        /// <returns>The imported alias name.</returns>
        private static string UnionProfileAlias(int encoding, int recording) => recording switch
        {
            1 => encoding == 0 ? "LuminPackUtf8TokenOptions" : "LuminPackUtf16TokenOptions",
            2 => encoding == 0 ? "LuminPackUtf8VarIntOptions" : "LuminPackUtf16VarIntOptions",
            _ => encoding == 0 ? "LuminPackUtf8LengthOptions" : "LuminPackUtf16LengthOptions",
        };

        /// <summary>Whether the referenced runtime exposes every canonical profile, as the exact slot shortcuts require.</summary>
        /// <returns>Whether the exact canonical profile shortcuts can bind.</returns>
        private bool CanonicalUnionProfiles()
        {
            var writer = _compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
            if (writer == null || SymbolEqualityComparer.Default.Equals(writer.ContainingAssembly, _compilation.Assembly) ||
                writer.GetMembers("Cursor").OfType<IFieldSymbol>().Any(field => field.Type is IPointerTypeSymbol) ||
                _compilation.SyntaxTrees.Any(tree => tree.Options is CSharpParseOptions parse &&
                    parse.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal))))
                return false;
            var names = new List<string>();
            foreach (var (encoding, recording) in UnionProfiles(json: false))
                names.Add("LuminPack.Option." + UnionProfileAlias(encoding, recording));
            names.Add("LuminPack.Option.LuminPackFixedOptions`1");
            names.Add("LuminPack.Option.LuminPackDefaultOptions");
            names.Add("LuminPack.Option.LuminPackDeclaredOptions");
            return names.All(name => _compilation.GetTypeByMetadataName(name) is { } symbol &&
                SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, writer.ContainingAssembly));
        }

        /// <summary>Resolves the declaration-time wire policy of one payload.</summary>
        /// <param name="symbol">The payload type.</param>
        /// <param name="kind">The payload protocol, which selects reference tracking.</param>
        /// <returns>The effective policy.</returns>
        private WirePolicy TypePolicy(INamedTypeSymbol symbol, PayloadKind kind)
        {
            WirePolicy policy = Apply(WirePolicy.Default, _compilation.Assembly);
            var scopes = new Stack<INamedTypeSymbol>();
            for (var type = symbol; type != null; type = type.ContainingType)
                scopes.Push(type);
            while (scopes.Count != 0)
                policy = Apply(policy, scopes.Pop());
            if (kind == PayloadKind.CircleReference && !policy.TrackReferences)
                policy = new WirePolicy(policy.Input, policy.Strings, policy.Bound, trackReferences: true);
            return policy;
        }

        /// <summary>Overlays one member's declaration-time options on the enclosing payload policy.</summary>
        /// <param name="member">The declared field or auto-property.</param>
        /// <param name="ownerPolicy">The policy resolved on the declaring payload.</param>
        /// <returns>The member's effective policy.</returns>
        private static WirePolicy MemberPolicy(ISymbol member, WirePolicy ownerPolicy)
        {
            var scopes = new Stack<INamedTypeSymbol>();
            for (var type = member.ContainingType; type != null; type = type.ContainingType)
                scopes.Push(type);
            while (scopes.Count != 0)
                ownerPolicy = Apply(ownerPolicy, scopes.Pop());
            return Apply(ownerPolicy, member);
        }

        /// <summary>Overlays the options a declaration explicitly states on an inherited policy.</summary>
        /// <param name="policy">The inherited policy.</param>
        /// <param name="symbol">The assembly, type, field or property that declares the options.</param>
        /// <returns>The overlaid policy, or the inherited instance when nothing is declared.</returns>
        private static WirePolicy Apply(WirePolicy policy, ISymbol symbol)
        {
            char format = policy.Strings.Format;
            byte precision = policy.Strings.Precision;
            int depth = policy.Strings.Depth;
            bool strict = policy.Strings.Strict;
            StringEncoding encoding = policy.Strings.Encoding;
            StringRecording recording = policy.Strings.Recording;
            InputPolicy input = policy.Input;
            int? bound = policy.Bound;
            bool changed = false;

            foreach (var attribute in symbol.GetAttributes())
            {
                string? attributeName = attribute.AttributeClass?.ToDisplayString();
                if (attributeName == BoundedAttribute && attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Value is int maxBytes && maxBytes > 0)
                {
                    bound = maxBytes;
                    changed = true;
                    continue;
                }
                if (attributeName != OptionAttribute)
                    continue;
                foreach (var argument in attribute.NamedArguments)
                {
                    switch (argument.Key)
                    {
                        case "UntrustedData" when argument.Value.Value is bool untrusted:
                            input = untrusted ? InputPolicy.Untrusted : InputPolicy.Trusted;
                            changed = true;
                            break;
                        case "StringEncoding" when argument.Value.Value is not null:
                            encoding = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture) == 0
                                ? StringEncoding.Utf8 : StringEncoding.Utf16;
                            changed = true;
                            break;
                        case "StringRecording" when argument.Value.Value is not null:
                            recording = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture) switch
                            {
                                1 => StringRecording.Token,
                                2 => StringRecording.VarInt,
                                _ => StringRecording.Length,
                            };
                            changed = true;
                            break;
                        case "StandardFormat" when argument.Value.Value is string text && text.Length != 0:
                            format = text[0];
                            precision = text.Length == 1 ? byte.MaxValue :
                                byte.TryParse(text.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out byte parsed) && parsed <= 99
                                    ? parsed : byte.MaxValue;
                            changed = true;
                            break;
                        case "MaxJsonDepth" when argument.Value.Value is int maxDepth:
                            depth = maxDepth;
                            changed = true;
                            break;
                        case "ValidateJsonSyntax" when argument.Value.Value is bool validate:
                            strict = validate;
                            changed = true;
                            break;
                    }
                }
            }
            if (!changed)
                return policy;
            return new WirePolicy(input, new StringProfile(format, precision, depth, strict, encoding, recording), bound,
                policy.TrackReferences);
        }

        /// <summary>Classifies unmanaged value storage and detects a layout that needs field-wise packed transfer.</summary>
        /// <param name="symbol">The unmanaged struct.</param>
        /// <returns>The portable layout when one applies, otherwise null for a raw copy.</returns>
        /// <remarks>A layout is created when the declaration opts into packed storage and its packed wire differs from its native
        /// storage, or when an unmarked struct with a formatter of its own has different native layouts per pointer width.
        /// The layout carries the positional view facts; <see cref="PortableLayout.Complete"/> records whether that view can be
        /// rendered, which the adapter requires.</remarks>
        private PortableLayout? TryPortableLayout(INamedTypeSymbol symbol)
        {
            if (symbol.SpecialType != SpecialType.None || symbol.IsRefLikeType)
                return null;
            int? declared = ReadStructLayoutOption(symbol.GetAttributes());
            bool optedIn = declared.HasValue ? declared.Value == 1 : symbol.IsUnmanagedType && LayoutStrategy(symbol) == 1;
            PortablePlan? plan;
            if (optedIn)
            {
                if (GenerationMode(symbol) != "Object")
                    return null;
                plan = AnalyzePortable(symbol, strict: true);
                if (plan?.HasPadding != true)
                    return null;
            }
            else
            {
                plan = TryTaggedPlan(symbol);
                if (plan == null)
                    return null;
            }
            return CreatePortableLayout(symbol, plan);
        }

        /// <summary>Selects tagged native storage for an unmarked struct whose native layout varies between layout models.</summary>
        /// <param name="named">The resolved storage type.</param>
        /// <returns>The tagged plan, or null when the struct is not analyzable, has a single layout or owns no formatter.</returns>
        private static PortablePlan? TryTaggedPlan(INamedTypeSymbol named)
        {
            if (named.TypeKind != TypeKind.Struct || named.SpecialType != SpecialType.None || !named.IsUnmanagedType)
                return null;
            if (LayoutStrategy(named) != 0 || GenerationMode(named) != "Object")
                return null;
            if (PortableScalarSize(named) != 0 || ContainsTypeParameter(named) || Opaque(named) != null)
                return null;
            var plan = AnalyzePortable(named, strict: true);
            if (plan == null || plan.Fields == null || plan.WireSize <= 0 || HasVolatile(plan))
                return null;
            plan.Models = BuildModels(plan);
            if (plan.Models.Length < 2 || !FormatterOwned(named))
                return null;
            plan.Tagged = true;
            return plan;
        }

        /// <summary>Resolves the declared unmanaged storage strategy, type first and assembly second.</summary>
        /// <param name="symbol">The unmanaged struct.</param>
        /// <returns>Zero for tagged, one for packed or two for native storage.</returns>
        private static int LayoutStrategy(INamedTypeSymbol symbol)
        {
            int? declared = ReadStructLayoutOption(symbol.GetAttributes());
            if (declared.HasValue)
                return declared.Value;
            return ReadStructLayoutOption(symbol.ContainingAssembly.GetAttributes()) ?? 0;
        }

        /// <summary>Recognizes a struct this compilation gives an implicit formatter, so a tagged plan has a writer and reader.</summary>
        /// <param name="type">The candidate type.</param>
        /// <returns>Whether the struct behaves like an ordinary local packable struct.</returns>
        private static bool FormatterOwned(INamedTypeSymbol type)
            => IsValueTuple(type) || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
               type.OriginalDefinition.MetadataName == "KeyValuePair`2" &&
               type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" ||
               IsPackable(type) || IsImplicitFormatterType(type);

        /// <summary>Recognizes a top-level source struct without the packable attribute that still receives an implicit formatter.</summary>
        /// <param name="type">The candidate type.</param>
        /// <returns>Whether the generator provides it a binary formatter.</returns>
        private static bool IsImplicitFormatterType(INamedTypeSymbol type)
            => type.TypeKind == TypeKind.Struct && type.IsUnmanagedType && !type.IsRefLikeType && type.ContainingType == null &&
               type.SpecialType == SpecialType.None && !type.IsTupleType && type.DeclaringSyntaxReferences.Length != 0 &&
               !type.IsGenericType && !IsPackable(type) && GenerationMode(type) != "Custom" && Opaque(type) == null;

        /// <summary>Whether JSON can assign a fresh instance directly instead of staging members in temporaries.</summary>
        /// <param name="symbol">The record declaration.</param>
        /// <param name="members">The serialized members.</param>
        /// <returns>Whether every level is a source class with an implicit parameterless constructor and no initializers.</returns>
        private static bool CanDeserializeDirectlyIntoFreshResult(INamedTypeSymbol symbol, MemberModel[] members)
        {
            // Omitted JSON members keep constructor and initializer values, so only public storage without either can
            // take the fresh-result path. Versioned records keep the same rule: their layout mirror does not stage members.
            if (members.Length == 0 || members.Any(member => member.Mirrored))
                return false;
            for (var type = symbol; type != null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
            {
                if (type.TypeKind != TypeKind.Class || type.DeclaringSyntaxReferences.Length == 0)
                    return false;
                var constructors = type.InstanceConstructors;
                if (constructors.Length != 1 || !constructors[0].IsImplicitlyDeclared || constructors[0].Parameters.Length != 0)
                    return false;
                foreach (var member in type.GetMembers())
                {
                    if (member.IsStatic || member.IsImplicitlyDeclared)
                        continue;
                    foreach (var reference in member.DeclaringSyntaxReferences)
                    {
                        var syntax = reference.GetSyntax();
                        if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax { Initializer: not null } ||
                            syntax is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax { Initializer: not null })
                            return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Records whether a declaration is a supported fixed record, which gives it the <c>MeasureRecord</c> entry.</summary>
        /// <param name="symbol">The struct declaration.</param>
        /// <returns>Whether every instance member is a fixed-width primitive.</returns>
        private static bool RecordWidthSupported(INamedTypeSymbol symbol)
            => GenerationMode(symbol) is not ("NonGenerator" or "Custom") && IsFixedRecord(symbol);

        /// <summary>Recognizes a source-declared object record whose every instance field and property is a primitive or enum.</summary>
        /// <param name="type">The candidate type.</param>
        /// <returns>Whether the record width is constant, so the measurement entry is selected.</returns>
        private static bool IsFixedRecord(INamedTypeSymbol type)
        {
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsAbstract || type.IsStatic || type.IsRefLikeType ||
                ContainsTypeParameter(type))
                return false;
            var packable = type.GetAttributes().FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() == PackableAttribute);
            if (packable == null || packable.ConstructorArguments.Length == 1 &&
                !(packable.ConstructorArguments[0].Kind == TypedConstantKind.Enum &&
                  Convert.ToInt32(packable.ConstructorArguments[0].Value, CultureInfo.InvariantCulture) is 0 or 1))
                return false;
            for (var current = type; current != null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
                current = current.BaseType)
            {
                if (!current.Locations.Any(location => location.IsInSource))
                    return false;
                foreach (var attribute in current.GetAttributes())
                    if (attribute.AttributeClass?.ToDisplayString() is UnionAttribute or
                        "System.Runtime.CompilerServices.UnionAttribute" or "LuminPack.Attribute.LuminPackCompressAttribute" or
                        "LuminPack.Attribute.LuminPackWideTagAttribute" or "LuminPack.Attribute.LuminPackSingleImplementationAttribute")
                        return false;
                if (OptionSwitch(current, "WideTag") || OptionSwitch(current, "SingleImplementation"))
                    return false;
                if (!SymbolEqualityComparer.Default.Equals(current, type) && current.GetAttributes().Any(attribute =>
                        attribute.AttributeClass?.ToDisplayString() == PackableAttribute &&
                        attribute.ConstructorArguments.Length == 1 &&
                        !(attribute.ConstructorArguments[0].Kind == TypedConstantKind.Enum &&
                          Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture) == 0)))
                    return false;
                foreach (var member in current.GetMembers())
                {
                    if (member.IsStatic)
                        continue;
                    ITypeSymbol memberType;
                    switch (member)
                    {
                        case IFieldSymbol field:
                            memberType = field.Type;
                            break;
                        case IPropertySymbol property:
                            memberType = property.Type;
                            break;
                        case IEventSymbol:
                            return false;
                        default:
                            continue;
                    }
                    if (!IsFixedWidthPrimitive(memberType))
                        return false;
                }
            }
            return true;
        }

        /// <summary>Recognizes a fixed-width primitive or an enum over one.</summary>
        /// <param name="type">The member type.</param>
        /// <returns>Whether the type is always written at a fixed width.</returns>
        private static bool IsFixedWidthPrimitive(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration)
                type = enumeration.EnumUnderlyingType ?? type;
            return type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or
                SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
                SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or
                SpecialType.System_Double;
        }

        /// <summary>Builds the positional view of a portable storage plan.</summary>
        /// <param name="type">The root declaration.</param>
        /// <param name="plan">The analyzed storage.</param>
        /// <returns>The model the adapter maps onto the emitter view.</returns>
        private static PortableLayout CreatePortableLayout(INamedTypeSymbol type, PortablePlan plan)
        {
            // A portable struct with a nested aggregate needs its child view; tuple components flatten into their parent view.
            bool complete = !type.IsTupleType && plan.Fields != null && plan.Children != null &&
                plan.Children.All(child => child.Fields == null || IsValueTuple(child.Type));
            var fields = complete ? new PortableFieldModel[plan.Fields!.Length] : Array.Empty<PortableFieldModel>();
            for (int index = 0; index < fields.Length; index++)
            {
                string name = "F" + index.ToString(CultureInfo.InvariantCulture);
                var leaves = new List<PortableLeafModel>();
                FlattenLeaves(plan.Children![index], name, plan.Fields![index].IsVolatile, leaves);
                fields[index] = new PortableFieldModel(name, FormatterTypeName(plan.Fields[index].Type), plan.Fields[index].IsVolatile,
                    plan.Fields[index].Offset, leaves.ToArray());
            }
            return new PortableLayout(ViewName(type), fields, plan.WireSize, plan.Size64, plan.Size32, plan.Alignment64, plan.Alignment32,
                plan.Tagged, complete, LayoutAttribute(type));
        }

        /// <summary>A tuple's components are its own storage fields; a scalar ends the descent at its packed width.</summary>
        /// <param name="node">The storage node.</param>
        /// <param name="access">The access below the view.</param>
        /// <param name="isVolatile">Whether an enclosing field is volatile.</param>
        /// <param name="leaves">Receives the scalar transfers in wire order.</param>
        private static void FlattenLeaves(PortablePlan node, string access, bool isVolatile, List<PortableLeafModel> leaves)
        {
            if (node.Fields == null)
            {
                leaves.Add(new PortableLeafModel(access, FormatterTypeName(node.Type), node.WireSize, isVolatile));
                return;
            }
            if (!IsValueTuple(node.Type))
                return;
            for (int index = 0; index < node.Fields.Length; index++)
                FlattenLeaves(node.Children![index], access + "." + node.Fields[index].Name,
                    isVolatile || node.Fields[index].IsVolatile, leaves);
        }

        /// <summary>Spells the deterministic positional view name of a portable struct.</summary>
        /// <param name="type">The root declaration.</param>
        /// <returns>The view declaration name.</returns>
        private static string ViewName(INamedTypeSymbol type)
        {
            using var hash = SHA256.Create();
            string digest = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(FormatterTypeName(type)))
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            return "LuminLayout_" + digest + "_" + PortableSlotSuffix(type) + "_LocalPortable" + LocalDeclarationName(type);
        }

        /// <summary>Spells the canonical Union slot suffix, whose FNV identity is a public override contract.</summary>
        /// <param name="type">The declared type.</param>
        /// <returns>The readable identity and its eight-digit hash.</returns>
        private static string PortableSlotSuffix(INamedTypeSymbol type)
        {
            string identity = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            uint hash = 2166136261;
            foreach (char character in identity)
            {
                hash ^= character;
                hash *= 16777619;
            }
            string readable = new string(identity.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());
            return readable + "_" + hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>Spells the local declaration name the layout generator gives a mirrored storage type.</summary>
        /// <param name="type">The declared type.</param>
        /// <returns>The mangled local declaration name.</returns>
        private static string LocalDeclarationName(INamedTypeSymbol type)
        {
            string fullName = FormatterTypeName(type);
            if (fullName.StartsWith("global::", StringComparison.Ordinal))
                fullName = fullName.Substring(8);
            var name = new StringBuilder(fullName.Length + 8);
            foreach (char character in fullName)
            {
                // Punctuation such as "::" or ", " maps to one separator; two would form a reserved identifier.
                if (SyntaxFacts.IsIdentifierPartCharacter(character))
                    name.Append(character);
                else if (name.Length == 0 || name[name.Length - 1] != '_')
                    name.Append('_');
            }
            return "Local" + name;
        }

        /// <summary>Spells the source-ready type name the generated view and leaves use.</summary>
        /// <param name="type">The referenced type.</param>
        /// <returns>The fully qualified spelling, with tuples expanded to <c>ValueTuple</c> syntax.</returns>
        private static string FormatterTypeName(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol { IsNativeIntegerType: true } nativeInteger)
                return nativeInteger.SpecialType == SpecialType.System_UIntPtr ? "global::System.UIntPtr" : "global::System.IntPtr";
            return ContainsTuple(type) ? BuildTypeName(type) : type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string BuildTypeName(ITypeSymbol type)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    var ranks = new StringBuilder();
                    ITypeSymbol element = array;
                    while (element is IArrayTypeSymbol level)
                    {
                        ranks.Append('[').Append(',', level.Rank - 1).Append(']');
                        element = level.ElementType;
                    }
                    return BuildTypeName(element) + ranks;
                case IPointerTypeSymbol pointer:
                    return BuildTypeName(pointer.PointedAtType) + "*";
                case ITypeParameterSymbol parameter:
                    return EscapeIdentifier(parameter.Name);
                case IDynamicTypeSymbol:
                    return "dynamic";
                case INamedTypeSymbol named:
                    return BuildNamedTypeName(named);
                default:
                    return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }

        private static string BuildNamedTypeName(INamedTypeSymbol type)
        {
            if (type.IsTupleType)
                return "global::System.ValueTuple<" + string.Join(", ", type.TypeArguments.Select(BuildTypeName)) + ">";
            if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && type.TypeArguments.Length == 1)
                return BuildTypeName(type.TypeArguments[0]) + "?";
            string prefix = type.ContainingType is not null
                ? BuildNamedTypeName(type.ContainingType) + "."
                : BuildTypeNamespace(type.ContainingNamespace);
            string name = prefix + EscapeIdentifier(type.Name);
            return type.TypeArguments.Length == 0
                ? name
                : name + "<" + string.Join(", ", type.TypeArguments.Select(BuildTypeName)) + ">";
        }

        private static string BuildTypeNamespace(INamespaceSymbol typeNamespace)
            => typeNamespace == null || typeNamespace.IsGlobalNamespace
                ? "global::"
                : BuildTypeNamespace(typeNamespace.ContainingNamespace) + EscapeIdentifier(typeNamespace.Name) + ".";

        private static bool ContainsTuple(ITypeSymbol type)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    return ContainsTuple(array.ElementType);
                case IPointerTypeSymbol pointer:
                    return ContainsTuple(pointer.PointedAtType);
                case INamedTypeSymbol named:
                    return named.IsTupleType || named.ContainingType != null && ContainsTuple(named.ContainingType) ||
                        named.TypeArguments.Any(ContainsTuple);
                default:
                    return false;
            }
        }

        private static string EscapeIdentifier(string name)
            => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
                ? "@" + name
                : name;

        /// <summary>Builds recursive widths and layout proofs for both architectures.</summary>
        /// <param name="type">The finite unmanaged storage type.</param>
        /// <param name="strict">Whether reference-assembly structs must be modeled from known shapes instead of placeholder fields.</param>
        /// <returns>The storage plan, or null for storage without an architecture-independent scalar representation.</returns>
        private static PortablePlan? AnalyzePortable(ITypeSymbol type, bool strict)
        {
            int scalar = PortableScalarSize(type);
            if (scalar != 0)
                return new PortablePlan
                {
                    Type = type, WireSize = scalar, Size32 = scalar, Size64 = scalar,
                    Alignment32 = Math.Min(scalar, 4), Alignment64 = Math.Min(scalar, 8)
                };
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Struct } opaqueNamed && Opaque(opaqueNamed) is { } opaque)
                return new PortablePlan
                {
                    Type = type, WireSize = opaque.Size, Size32 = opaque.Size, Size64 = opaque.Size,
                    Alignment32 = opaque.Alignment32, Alignment64 = opaque.Alignment64
                };
            if (type is not INamedTypeSymbol { IsUnmanagedType: true, TypeKind: TypeKind.Struct } named ||
                GenerationMode(named) == "Custom" ||
                named.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr ||
                IsRuntimeSizedVector(named) ||
                named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                    "System.Runtime.CompilerServices.InlineArrayAttribute"))
                return null;
            var fields = PlanFields(named, strict);
            if (fields == null)
                return null;
            var children = new PortablePlan[fields.Length];
            for (int index = 0; index < fields.Length; index++)
            {
                var child = AnalyzePortable(fields[index].Type, strict);
                if (child == null)
                    return null;
                children[index] = child;
            }
            var plan = new PortablePlan
            {
                Type = type, Fields = fields, Children = children, WireSize = children.Sum(child => child.WireSize),
                HasPadding = named.IsTupleType || children.Any(child => child.HasPadding) ||
                    named.ContainingNamespace.ToDisplayString().StartsWith("System.Runtime.Intrinsics", StringComparison.Ordinal)
            };
            (System.Runtime.InteropServices.LayoutKind layoutKind, int declaredPack, int declaredSize) = LayoutInfo(named);
            int kind = layoutKind == System.Runtime.InteropServices.LayoutKind.Explicit ? 2 :
                layoutKind == System.Runtime.InteropServices.LayoutKind.Auto || IsValueTuple(named) ? 3 : 0;
            int pack = declaredPack == 0 ? 8 : declaredPack;
            plan.Kind = kind;
            plan.Pack = declaredPack;
            plan.DeclaredSize = declaredSize;
            plan.Size32 = plan.MeasurePortable(false, kind, pack, declaredSize, out plan.Alignment32);
            plan.Size64 = plan.MeasurePortable(true, kind, pack, declaredSize, out plan.Alignment64);
            plan.HasPadding |= kind == 3 || plan.Size32 != plan.WireSize || plan.Size64 != plan.WireSize;
            return plan;
        }

        /// <summary>Recognizes the automatic-layout ValueTuple family regardless of how the compiler spells the type.</summary>
        /// <param name="type">The storage type.</param>
        /// <returns>Whether the type is a ValueTuple construction.</returns>
        private static bool IsValueTuple(ITypeSymbol type)
            => type is INamedTypeSymbol named && (named.IsTupleType ||
                named.OriginalDefinition.MetadataName.StartsWith("ValueTuple`", StringComparison.Ordinal) &&
                named.ContainingNamespace.ToDisplayString() == "System");

        /// <summary>Lists the fields whose layout the plan models, repairing shapes a reference assembly hides.</summary>
        /// <param name="named">The aggregate.</param>
        /// <param name="strict">Whether the aggregate must be modeled from facts rather than unreliable reference metadata.</param>
        /// <returns>The declaration-ordered fields, or null when the storage cannot be modeled.</returns>
        private static PortablePlanField[]? PlanFields(INamedTypeSymbol named, bool strict)
        {
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1 &&
                named.GetMembers("HasValue").OfType<IPropertySymbol>().FirstOrDefault()?.Type is { } boolean)
                return new[]
                {
                    new PortablePlanField { Type = boolean, Name = "hasValue" },
                    new PortablePlanField { Type = named.TypeArguments[0], Name = "value" }
                };
            if (named.OriginalDefinition.MetadataName == "KeyValuePair`2" &&
                named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
                return new[]
                {
                    new PortablePlanField { Type = named.TypeArguments[0], Name = "key" },
                    new PortablePlanField { Type = named.TypeArguments[1], Name = "value" }
                };
            var fields = StorageFields(named);
            // SDK reference metadata for framework values may contain dummy fields instead of their real storage.
            // C# project reference assemblies retain value-type fields, including private ones needed for layout.
            if (IsReferenceAssembly(named.ContainingAssembly) && !IsValueTuple(named) &&
                !IsPackable(named) &&
                (named.ContainingAssembly.Name.StartsWith("System.", StringComparison.Ordinal) ||
                 named.ContainingAssembly.Name is "mscorlib" or "netstandard"))
                return null;
            if (fields.Any(field => field.IsFixedSizeBuffer ||
                field.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer))
                return null;
            return fields.Select(field => new PortablePlanField
            {
                Type = field.Type, Name = field.Name, IsVolatile = field.IsVolatile, Offset = FieldOffset(field)
            }).ToArray();
        }

        /// <summary>Gets physical instance storage without tuple aliases or flattened elements already stored in Rest.</summary>
        /// <param name="type">The current aggregate storage type.</param>
        /// <returns>Ordinary fields in declaration order, or tuple fields in component order with Rest last.</returns>
        private static IFieldSymbol[] StorageFields(INamedTypeSymbol type)
        {
            if (!type.IsTupleType)
                return type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic).ToArray();
            type = type.TupleUnderlyingType ?? type;
            return type.GetMembers().OfType<IFieldSymbol>()
                .Where(field => !field.IsStatic && field.Name is
                    "Item1" or "Item2" or "Item3" or "Item4" or "Item5" or "Item6" or "Item7" or "Rest")
                .OrderBy(field => field.Name == "Rest" ? 8 : field.Name[4] - '0')
                .ToArray();
        }

        /// <summary>Recognizes an assembly that carries only the public shape of its types.</summary>
        /// <param name="assembly">The declaring assembly.</param>
        /// <returns>Whether it is marked as a reference assembly.</returns>
        private static bool IsReferenceAssembly(IAssemblySymbol assembly)
            => assembly != null && assembly.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute");

        /// <summary>Recognizes volatile storage anywhere in an analyzed aggregate.</summary>
        /// <param name="plan">The analyzed storage.</param>
        /// <returns>Whether any expanded field is volatile.</returns>
        private static bool HasVolatile(PortablePlan plan)
        {
            if (plan.Fields == null)
                return false;
            for (int index = 0; index < plan.Fields.Length; index++)
                if (plan.Fields[index].IsVolatile || HasVolatile(plan.Children![index]))
                    return true;
            return false;
        }

        /// <summary>Recognizes unresolved generic arguments anywhere in a closed construction.</summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>Whether a type parameter remains.</returns>
        private static bool ContainsTypeParameter(ITypeSymbol type)
            => type is ITypeParameterSymbol || type is INamedTypeSymbol { IsGenericType: true } named &&
                named.TypeArguments.Any(ContainsTypeParameter);

        /// <summary>Checks whether any aggregate in the tree uses the runtime's automatic field order.</summary>
        /// <param name="plan">The analyzed storage.</param>
        /// <returns>Whether an automatic layout appears.</returns>
        private static bool HasAutomatic(PortablePlan plan)
            => plan.Fields != null && (plan.Kind == 3 || plan.Children!.Any(HasAutomatic));

        /// <summary>Places the storage under each layout model and keeps the distinct results.</summary>
        /// <param name="plan">The analyzed storage.</param>
        /// <returns>Natural 64-bit, natural 32-bit and, for automatic layouts, the CoreCLR automatic orders, without duplicates.</returns>
        private static PortableStorageModel[] BuildModels(PortablePlan plan)
        {
            var kinds = new List<(bool Is64, bool Auto)> { (true, false), (false, false) };
            if (HasAutomatic(plan))
            {
                kinds.Add((true, true));
                kinds.Add((false, true));
            }
            var models = new List<PortableStorageModel>();
            foreach (var (is64, auto) in kinds)
            {
                var placed = PlacePortable(plan, is64, auto);
                if (!models.Any(model => model.Size == placed.Size && model.Offsets.AsSpan().SequenceEqual(placed.Offsets)))
                    models.Add(new PortableStorageModel { Size = placed.Size, Offsets = placed.Offsets });
            }
            return models.ToArray();
        }

        /// <summary>Positions every field of an aggregate under one layout model.</summary>
        /// <param name="plan">The analyzed storage.</param>
        /// <param name="is64">Whether eight-byte primitives align to eight bytes.</param>
        /// <param name="auto">Whether automatic aggregates order fields like CoreCLR.</param>
        /// <returns>The element size, alignment, and the storage offset of every packed data byte.</returns>
        private static (int Size, int Alignment, int[] Offsets) PlacePortable(PortablePlan plan, bool is64, bool auto)
        {
            if (plan.Fields == null)
            {
                var bytes = new int[plan.WireSize];
                for (int index = 0; index < bytes.Length; index++)
                    bytes[index] = index;
                return (plan.WireSize, is64 ? plan.Alignment64 : plan.Alignment32, bytes);
            }
            int count = plan.Children!.Length;
            var places = new (int Size, int Alignment, int[] Offsets)[count];
            for (int index = 0; index < count; index++)
                places[index] = PlacePortable(plan.Children[index], is64, auto);
            int pack = plan.Pack == 0 ? 8 : plan.Pack;
            var order = Enumerable.Range(0, count).ToList();
            if (auto && plan.Kind == 3)
                order = order.OrderBy(index => plan.Children[index].Fields == null ? 0 : 1)
                    .ThenByDescending(index => plan.Children[index].Fields == null ? places[index].Size : 0).ToList();
            var offsets = new int[count];
            int end = 0, alignment = 1;
            foreach (int index in order)
            {
                int fieldAlignment = Math.Max(1, Math.Min(pack, places[index].Alignment));
                alignment = Math.Max(alignment, fieldAlignment);
                int offset = plan.Kind == 2 && plan.Fields[index].Offset >= 0
                    ? plan.Fields[index].Offset
                    : (end + fieldAlignment - 1) / fieldAlignment * fieldAlignment;
                offsets[index] = offset;
                end = Math.Max(end, offset + places[index].Size);
            }
            int size = (Math.Max(Math.Max(1, plan.DeclaredSize), end) + alignment - 1) / alignment * alignment;
            var data = new int[plan.WireSize];
            int next = 0;
            for (int index = 0; index < count; index++)
                foreach (int byteOffset in places[index].Offsets)
                    data[next++] = offsets[index] + byteOffset;
            return (size, alignment, data);
        }

        /// <summary>Obtains fixed scalar widths without consulting the running process's layout.</summary>
        /// <param name="type">A primitive, enum, or nested storage type.</param>
        /// <returns>The scalar byte width, or zero for an aggregate.</returns>
        private static int PortableScalarSize(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration)
                return PortableScalarSize(enumeration.EnumUnderlyingType!);
            return type.SpecialType switch
            {
                SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
                SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
                SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
                SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
                SpecialType.System_Decimal => 16,
                _ => 0
            };
        }

        /// <summary>Excludes vectors whose physical width can change with the executing JIT's instruction set.</summary>
        /// <param name="type">The nested storage type.</param>
        /// <returns>Whether compiler field metadata cannot describe its complete runtime width.</returns>
        private static bool IsRuntimeSizedVector(INamedTypeSymbol type)
            => type.MetadataName == "Vector`1" && type.ContainingNamespace.ToDisplayString() == "System.Numerics";

        /// <summary>Emits the ABI-preserving layout attribute line of a declared storage replica.</summary>
        /// <param name="type">The storage declaration.</param>
        /// <returns>The attribute line, or null when the declaration states no CLR layout.</returns>
        private static string? LayoutAttribute(INamedTypeSymbol type)
        {
            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != StructLayoutAttribute)
                    continue;
                int rawKind = attribute.ConstructorArguments.Length != 0 && attribute.ConstructorArguments[0].Value is int declared
                    ? declared : 0;
                string kind = rawKind switch { 0 => "Sequential", 2 => "Explicit", _ => "Auto" };
                int pack = 0, size = 0, charSet = 0;
                if (attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is int declaredPack)
                    pack = declaredPack;
                foreach (var argument in attribute.NamedArguments)
                {
                    if (argument.Key == "Pack" && argument.Value.Value is int namedPack)
                        pack = namedPack;
                    if (argument.Key == "Size" && argument.Value.Value is int namedSize)
                        size = namedSize;
                    if (argument.Key == "CharSet" && argument.Value.Value is int namedCharSet)
                        charSet = namedCharSet;
                }
                var line = new StringBuilder("[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.");
                line.Append(kind);
                if (pack != 0)
                    line.Append(", Pack = ").Append(pack.ToString(CultureInfo.InvariantCulture));
                if (size != 0)
                    line.Append(", Size = ").Append(size.ToString(CultureInfo.InvariantCulture));
                if (charSet != 0)
                    line.Append(", CharSet = (global::System.Runtime.InteropServices.CharSet)").Append(charSet.ToString(CultureInfo.InvariantCulture));
                line.Append(")]");
                return line.ToString();
            }
            return null;
        }

        /// <summary>One storage field of an analyzed aggregate, independent of the compiler's field symbol.</summary>
        private sealed class PortablePlanField
        {
            internal ITypeSymbol Type = null!;
            internal string Name = string.Empty;
            internal bool IsVolatile;
            internal int Offset = -1;
        }

        /// <summary>One native layout model: the element width and where each packed data byte sits in it.</summary>
        private sealed class PortableStorageModel
        {
            internal int Size;
            internal int[] Offsets = Array.Empty<int>();
        }

        /// <summary>The recursive storage plan of one unmanaged aggregate.</summary>
        private sealed class PortablePlan
        {
            internal ITypeSymbol Type = null!;
            internal PortablePlanField[]? Fields;
            internal PortablePlan[]? Children;
            internal int WireSize;
            internal int Size32;
            internal int Size64;
            internal int Alignment32;
            internal int Alignment64;
            internal bool HasPadding;
            internal int Kind;
            internal int Pack;
            internal int DeclaredSize;
            internal bool Tagged;
            internal PortableStorageModel[] Models = Array.Empty<PortableStorageModel>();

            /// <summary>Proves contiguous storage, including explicit offsets and trailing padding.</summary>
            /// <param name="is64Bit">The layout architecture.</param>
            /// <param name="kind">The declared layout kind.</param>
            /// <param name="pack">The declared maximum alignment.</param>
            /// <param name="declaredSize">The explicit minimum storage size.</param>
            /// <param name="alignment">Receives the aggregate alignment.</param>
            /// <returns>The predicted aggregate storage size.</returns>
            internal int MeasurePortable(bool is64Bit, int kind, int pack, int declaredSize, out int alignment)
            {
                int end = 0, packedEnd = 0;
                alignment = 1;
                for (int index = 0; index < Children!.Length; index++)
                {
                    var child = Children[index];
                    int fieldAlignment = Math.Min(pack, is64Bit ? child.Alignment64 : child.Alignment32);
                    alignment = Math.Max(alignment, fieldAlignment);
                    int offset = (end + fieldAlignment - 1) / fieldAlignment * fieldAlignment;
                    if (kind == 2)
                    {
                        offset = Fields![index].Offset;
                        if (offset < 0)
                        {
                            HasPadding = true;
                            offset = end;
                        }
                    }
                    HasPadding |= offset != packedEnd;
                    end = Math.Max(end, offset + (is64Bit ? child.Size64 : child.Size32));
                    packedEnd += child.WireSize;
                }
                return Math.Max(Math.Max(1, declaredSize), (end + alignment - 1) / alignment * alignment);
            }
        }


        /// <summary>Reads the resolved standard struct-layout attribute values.</summary>
        /// <param name="symbol">The unmanaged struct.</param>
        /// <returns>The declared layout kind, pack and explicit size.</returns>
        private static (System.Runtime.InteropServices.LayoutKind Kind, int Pack, int Size) LayoutInfo(INamedTypeSymbol symbol)
        {
            System.Runtime.InteropServices.LayoutKind kind = System.Runtime.InteropServices.LayoutKind.Sequential;
            int pack = 0, size = 0;
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != StructLayoutAttribute)
                    continue;
                if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int declaredKind)
                    kind = (System.Runtime.InteropServices.LayoutKind)declaredKind;
                if (attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is int declaredPack)
                    pack = declaredPack;
                foreach (var argument in attribute.NamedArguments)
                {
                    if (argument.Key == "Pack" && argument.Value.Value is int namedPack)
                        pack = namedPack;
                    if (argument.Key == "Size" && argument.Value.Value is int namedSize)
                        size = namedSize;
                }
            }
            return (kind, pack, size);
        }

        /// <summary>Reads one field's declared explicit offset.</summary>
        /// <param name="field">The storage field.</param>
        /// <returns>The offset, or a negative value when the field has none.</returns>
        private static int FieldOffset(IFieldSymbol field)
        {
            foreach (var attribute in field.GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == FieldOffsetAttribute &&
                    attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is int offset)
                    return offset;
            return -1;
        }

        /// <summary>Reads an explicitly declared unmanaged storage strategy.</summary>
        /// <param name="attributes">The declaration's attributes.</param>
        /// <returns>The strategy, or null when no declaration sets it.</returns>
        private static int? ReadStructLayoutOption(IEnumerable<AttributeData> attributes)
        {
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeClass?.ToDisplayString() != OptionAttribute)
                    continue;
                foreach (var argument in attribute.NamedArguments)
                    if (argument.Key == "StructLayout" && argument.Value.Value is not null)
                        return Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture);
            }
            return null;
        }

        /// <summary>Reports whether serializing the type can re-enter an operation of the same type.</summary>
        /// <param name="type">The formatter type.</param>
        /// <returns>Whether a member, element, type argument, base type or Union case path leads back to the type.</returns>
        private static bool IsRecursive(INamedTypeSymbol type)
            => Successors(type).Any(next => Reaches(next, type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)));

        /// <summary>Searches the member graph depth-first for a target type.</summary>
        /// <param name="current">The type being expanded.</param>
        /// <param name="target">The type that closes a cycle.</param>
        /// <param name="visited">The finite set of types already expanded.</param>
        /// <returns>Whether the target is reachable from the current type.</returns>
        private static bool Reaches(ITypeSymbol current, ITypeSymbol target, HashSet<ITypeSymbol> visited)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
                return true;
            if (!visited.Add(current))
                return false;
            return Successors(current).Any(next => Reaches(next, target, visited));
        }

        /// <summary>Lists the types serialized inside a value of the given type.</summary>
        /// <param name="type">The containing type.</param>
        /// <returns>Element, argument, member, base and Union case types.</returns>
        private static IEnumerable<ITypeSymbol> Successors(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                yield return array.ElementType;
                yield break;
            }
            if (type is not INamedTypeSymbol named)
                yield break;
            foreach (ITypeSymbol argument in named.TypeArguments)
                yield return argument;
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                yield break;
            // Only declarations that LuminPack analyzes expose members; framework types are opaque beyond their arguments.
            if (!IsPackable(named))
                yield break;
            for (var level = named; level != null && level.SpecialType != SpecialType.System_Object &&
                level.SpecialType != SpecialType.System_ValueType; level = level.BaseType)
                foreach (ISymbol member in level.GetMembers())
                {
                    if (member is IFieldSymbol { IsStatic: false } field)
                        yield return field.Type;
                    else if (member is IPropertySymbol { IsStatic: false, IsIndexer: false } property)
                        yield return property.Type;
                }
            foreach (AttributeData attribute in named.GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == UnionAttribute && attribute.ConstructorArguments.Length == 2 &&
                    attribute.ConstructorArguments[1].Value is ITypeSymbol caseType)
                    yield return caseType;
        }

        /// <summary>Whether the declaration declares a pool rent method, whose framing the circle roots do not reproduce.</summary>
        /// <param name="type">The declared payload.</param>
        /// <returns>Whether a rent method exists.</returns>
        private static bool Rent(INamedTypeSymbol type)
            => type.GetMembers().OfType<IMethodSymbol>().Any(method => method.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackPoolRentAttribute"));

        /// <summary>Whether the declaration selects a constructor with parameters for its reads.</summary>
        /// <param name="type">The declared payload.</param>
        /// <returns>Whether such a constructor is selected.</returns>
        private static bool SelectedConstructor(INamedTypeSymbol type)
            => type.InstanceConstructors.Any(constructor => constructor.Parameters.Length != 0 && constructor.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackConstructorAttribute"));

        /// <summary>Whether the declaration declares an instance constructor with parameters.</summary>
        /// <param name="type">The declared payload.</param>
        /// <returns>Whether a parameterized constructor exists.</returns>
        private static bool Constructors(INamedTypeSymbol type)
            => type.InstanceConstructors.Any(constructor => constructor.Parameters.Length != 0);

        /// <summary>Whether the declaration registers a serialization callback.</summary>
        /// <param name="type">The declared payload.</param>
        /// <returns>Whether a callback method exists.</returns>
        private static bool Callbacks(INamedTypeSymbol type)
            => type.GetMembers().OfType<IMethodSymbol>().Any(method => method.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() is "LuminPack.Attribute.LuminPackOnSerializingAttribute"
                    or "LuminPack.Attribute.LuminPackOnSerializedAttribute"
                    or "LuminPack.Attribute.LuminPackOnDeserializingAttribute"
                    or "LuminPack.Attribute.LuminPackOnDeserializedAttribute"));

        /// <summary>Whether every serialized member sits at its declared wire position.</summary>
        /// <param name="symbol">The declared payload.</param>
        /// <param name="members">The serialized members in wire order.</param>
        /// <returns>Whether each member's declared order equals its position.</returns>
        private static bool FieldsInOrder(INamedTypeSymbol symbol, MemberModel[] members)
        {
            int serialized = 0;
            foreach (INamedTypeSymbol level in Levels(symbol))
                foreach (ISymbol member in OrderedStorageMembers(level))
                {
                    if (!TryStorage(member, out _))
                        continue;
                    // A member the analysis omitted keeps the model incomplete; the remaining order still has to match.
                    if (!members.Any(model => model.Name == member.Name))
                        continue;
                    uint order = Order(member) ?? (uint)serialized;
                    if (order != (uint)serialized)
                        return false;
                    serialized++;
                }
            return true;
        }

        /// <summary>Reads a member's declared wire position.</summary>
        /// <param name="member">The declared member.</param>
        /// <returns>The position, or null when the member declares none.</returns>
        private static uint? Order(ISymbol member)
        {
            foreach (AttributeData attribute in member.GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == OrderAttribute && attribute.ConstructorArguments.Length == 1)
                    return Convert.ToUInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
            return null;
        }

        /// <summary>Builds the complete storage mirror of a reference-tracked node.</summary>
        /// <param name="symbol">The declared payload.</param>
        /// <param name="fields">Receives the mirror declarations in storage order, or null when they cannot be reproduced.</param>
        /// <param name="offsets">Receives the explicit-layout offset per field, or null for sequential storage.</param>
        /// <param name="layout">Receives the CLR layout attribute line, or null.</param>
        private static void MirrorFields(INamedTypeSymbol symbol, out string[]? fields, out int[]? offsets, out string? layout)
        {
            var declarations = new List<string>();
            var positions = new List<int>();
            bool complete = true;
            Collect(symbol);
            if (!complete)
            {
                fields = null;
                offsets = null;
                layout = null;
                return;
            }
            fields = declarations.ToArray();
            offsets = positions.Any(position => position >= 0) ? positions.ToArray() : null;
            layout = MirrorLayout(symbol);

            void Collect(INamedTypeSymbol level)
            {
                if (level.BaseType != null && level.BaseType.SpecialType != SpecialType.System_Object)
                    Collect(level.BaseType);
                foreach (ISymbol member in OrderedStorageMembers(level))
                {
                    if (member.IsImplicitlyDeclared || member.IsStatic)
                        continue;
                    ITypeSymbol storage;
                    bool isValue;
                    ISymbol offsetOwner = member;
                    if (member is IPropertySymbol property)
                    {
                        if (!IsAutoProperty(property))
                            continue;
                        storage = property.Type;
                        isValue = property.Type.IsValueType;
                        offsetOwner = level.GetMembers().OfType<IFieldSymbol>()
                            .FirstOrDefault(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property)) ?? member;
                    }
                    else if (member is IFieldSymbol field)
                    {
                        // A fixed-size buffer is inline storage the mirror cannot spell from its field type.
                        if (field.IsFixedSizeBuffer)
                        {
                            complete = false;
                            return;
                        }
                        storage = field.Type;
                        isValue = field.Type.IsValueType;
                    }
                    else
                        continue;
                    string spelling = storage is IArrayTypeSymbol { Rank: > 1 } multi
                        ? multi.ElementType.ToDisplayString() + "[" + new string(',', multi.Rank - 1) + " ]"
                        : storage.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    declarations.Add(spelling + (IsUnmanagedKeyword(spelling) || isValue ? string.Empty : "?") + " " + Identifier(member.Name));
                    positions.Add(FieldOffset(offsetOwner));
                }
            }
        }

        /// <summary>Spells a name the way a declaration does, escaping language keywords.</summary>
        /// <param name="name">The declared name.</param>
        /// <returns>The identifier spelling.</returns>
        private static string Identifier(string name)
            => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
                ? "@" + name : name;

        /// <summary>Reads a field's declared explicit offset, including an auto-property's backing field.</summary>
        /// <param name="member">The declared storage member.</param>
        /// <returns>The offset, or minus one when none is declared.</returns>
        private static int FieldOffset(ISymbol member)
        {
            foreach (AttributeData attribute in member.GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == FieldOffsetAttribute && attribute.ConstructorArguments.Length != 0)
                    return Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
            return -1;
        }

        /// <summary>Spells the CLR layout attribute a storage replica reproduces.</summary>
        /// <param name="type">The storage declaration.</param>
        /// <returns>The attribute line, or null for the default layout.</returns>
        private static string? MirrorLayout(INamedTypeSymbol type)
        {
            foreach (AttributeData attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != StructLayoutAttribute || attribute.ConstructorArguments.Length == 0)
                    continue;
                string kind = Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture) switch
                {
                    0 => "Sequential",
                    2 => "Explicit",
                    _ => "Auto",
                };
                var text = new StringBuilder("[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.")
                    .Append(kind);
                foreach (var argument in attribute.NamedArguments)
                {
                    if (argument.Value.Value is not int value || value == 0)
                        continue;
                    if (argument.Key == "Pack")
                        text.Append(", Pack = ").Append(value.ToString(CultureInfo.InvariantCulture));
                    else if (argument.Key == "Size")
                        text.Append(", Size = ").Append(value.ToString(CultureInfo.InvariantCulture));
                    else if (argument.Key == "CharSet")
                        text.Append(", CharSet = (global::System.Runtime.InteropServices.CharSet)").Append(value.ToString(CultureInfo.InvariantCulture));
                }
                return text.Append(")]").ToString();
            }
            return null;
        }

        /// <summary>Computes the schema facts a version-tolerant class record writes and reads.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="identity">The payload's identity.</param>
        /// <param name="members">The serialized members in wire order.</param>
        /// <param name="policy">The effective wire policy.</param>
        /// <param name="derived">Whether the record's base type contributes storage.</param>
        /// <param name="issues">Receives the reason the schema is outside the modeled surface.</param>
        /// <returns>The schema model, or null when the record keeps the original pipeline.</returns>
        /// <remarks>Versioned structs, callbacks, pool rents, base payloads, generic definitions, bounds, non-default profiles and
        /// every member shape the version emitter does not cover stay unmapped.</remarks>
        private VersionSchemaModel? TryVersionSchema(INamedTypeSymbol type, TypeIdentity identity, MemberModel[] members,
            WirePolicy policy, bool derived, List<string> issues)
        {
            if (derived || identity.IsGeneric)
            {
                issues.Add("a derived or generic version-tolerant record keeps its schema outside the direct emitters");
                return null;
            }
            // Constant schema bytes are emitted as collection expressions, which the original emits on .NET 8 with C# 12.
            if (!Options.Net8 || Options.CSharpVersion < 1200)
            {
                issues.Add("version tolerant: settings");
                return null;
            }
            if (policy.Bound != null || !policy.Equals(WirePolicy.Default))
            {
                issues.Add("a bounded or non-default version-tolerant record keeps its schema outside the direct emitters");
                return null;
            }
            if (Callbacks(type) || Rent(type) || SelectedConstructor(type))
            {
                issues.Add("a version-tolerant record with callbacks, a pool rent or a selected constructor keeps its schema outside the direct emitters");
                return null;
            }
            if (!type.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0 &&
                constructor.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public))
            {
                issues.Add("a version-tolerant record without a public parameterless constructor keeps its schema outside the direct emitters");
                return null;
            }
            if (members.Length > 32)
            {
                issues.Add("member count: " + members.Length.ToString(CultureInfo.InvariantCulture));
                return null;
            }
            // The analyzed serialized storage in declaration order; the schema's metadata table follows the wire order.
            var storage = new List<ISymbol>();
            foreach (ISymbol declared in OrderedStorageMembers(type))
                if (TryStorage(declared, out _))
                    storage.Add(declared);
            if (storage.Count != members.Length)
            {
                issues.Add("version tolerant: shape");
                return null;
            }
            for (int index = 0; index < members.Length; index++)
                if (storage[index].Name != members[index].Name || Order(storage[index]) is uint declared && declared != (uint)index)
                {
                    issues.Add("version tolerant: member order");
                    return null;
                }
            var locals = VersionLocals(type);
            if (locals.Count != members.Length)
            {
                issues.Add("version tolerant: shape");
                return null;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var oldNames = new string[members.Length][];
            var fieldTypes = new int[members.Length];
            var memberTypes = new ITypeSymbol[members.Length];
            for (int index = 0; index < members.Length; index++)
            {
                ISymbol declared = storage[index];
                ITypeSymbol memberType = declared is IFieldSymbol field ? field.Type : ((IPropertySymbol)declared).Type;
                if (!SyntaxFacts.IsValidIdentifier(declared.Name) || declared.Name != Identifier(declared.Name) ||
                    HasAttribute(declared, "LuminPack.Attribute.LuminPackCompressAttribute"))
                {
                    issues.Add("version tolerant: member options '" + declared.Name + "'");
                    return null;
                }
                string?[] aliases = declared.GetAttributes()
                    .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOldNameAttribute")
                    .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
                    .ToArray();
                if (aliases.Length != 0 && declared is not IFieldSymbol)
                {
                    issues.Add("version tolerant: member attributes '" + declared.Name + "'");
                    return null;
                }
                foreach (string? name in new string?[] { declared.Name }.Concat(aliases))
                {
                    if (string.IsNullOrEmpty(name) || !names.Add(name!))
                    {
                        issues.Add("version tolerant: member names");
                        return null;
                    }
                    try
                    {
                        new UTF8Encoding(false, true).GetByteCount(name!);
                    }
                    catch (EncoderFallbackException)
                    {
                        issues.Add("version tolerant: member names");
                        return null;
                    }
                }
                int? fieldType = VersionFieldType(memberType);
                if (fieldType == null || !VersionMemberCovered(members[index], fieldType.Value))
                {
                    issues.Add("version tolerant: members '" + declared.Name + "'");
                    return null;
                }
                oldNames[index] = aliases!;
                fieldTypes[index] = fieldType.Value;
                memberTypes[index] = memberType;
            }

            var metadata = new List<byte>();
            var widths = new int[members.Length];
            var entries = new string[members.Length];
            var tempTypes = new string[members.Length];
            var typeNames = new string[members.Length];
            var qualifiedTypes = new string[members.Length];
            for (int index = 0; index < members.Length; index++)
            {
                ITypeSymbol memberType = memberTypes[index];
                string serialized = oldNames[index].Length == 0 ? members[index].Name : oldNames[index][0];
                string? typeIdentity = VersionTypeIdentity(memberType);
                if (typeIdentity == null)
                {
                    issues.Add("version tolerant: metadata '" + members[index].Name + "'");
                    return null;
                }
                string representation = VersionRepresentation(fieldTypes[index], members[index].Policy);
                AppendVersionLength(metadata, serialized);
                AppendVersionLength(metadata, representation + (VersionIsDirectPrimitive(fieldTypes[index], memberType) ? string.Empty : typeIdentity));
                widths[index] = VersionWidth(fieldTypes[index], memberType);
                if (widths[index] == 0)
                {
                    issues.Add("version tolerant: widths");
                    return null;
                }
                string[] aliasLiterals = new[] { members[index].Name }.Concat(oldNames[index]).Where(alias => alias != serialized)
                    .Select(VersionLiteral).ToArray();
                entries[index] = "new global::LuminPack.Core.LuminPackVersionField(" + VersionLiteral(serialized) + ", new string[] " +
                    (aliasLiterals.Length == 0 ? "{ }" : "{ " + string.Join(", ", aliasLiterals) + " }") + ", typeof(" +
                    memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "), " + VersionLiteral(representation) + "),";
                var local = locals.FirstOrDefault(candidate => candidate.Name == members[index].Name);
                if (local.Name == null)
                {
                    issues.Add("version tolerant: shape");
                    return null;
                }
                tempTypes[index] = local.TypeName + (local.IsValue ? string.Empty : "?");
                typeNames[index] = memberType is INamedTypeSymbol named ? named.ToDisplayString() : memberType.Name;
                qualifiedTypes[index] = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            var prefix = new List<byte> { 251, 2, 0, (byte)members.Length };
            prefix.AddRange(metadata);
            foreach (int width in widths)
                prefix.Add((byte)width);
            string globalName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string schema = "global::LuminPack.Generated." + GeneratedNames.Extension(Options.AssemblyName) + ".Version_" +
                GeneratedNames.Sanitize(globalName);
            var layoutFields = locals.Select(local => local.TypeName +
                (IsUnmanagedKeyword(local.TypeName) || local.IsValue ? string.Empty : "?") + " " + Identifier(local.Name)).ToArray();
            return new VersionSchemaModel(schema, metadata.ToArray(), prefix.ToArray(), entries, tempTypes, typeNames, qualifiedTypes,
                VersionLayout(type), layoutFields, oldNames);
        }

        /// <summary>Builds the layout mirror a version-tolerant record declares for auto-property and included storage.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="identity">The payload's identity, which supplies the mirror's parameter list.</param>
        /// <param name="members">The serialized members in wire order.</param>
        /// <param name="issues">Receives the reason the mirror is outside the modeled surface.</param>
        /// <param name="mirror">Receives the mirror declaration name, or null.</param>
        /// <param name="mirrorFields">Receives the mirror declarations in storage order, or null.</param>
        /// <param name="mirrorOffsets">Receives the explicit-layout offsets, or null.</param>
        /// <param name="mirrorLayout">Receives the CLR layout attribute line, or null.</param>
        private static void TryVersionMirror(INamedTypeSymbol type, TypeIdentity identity, MemberModel[] members, List<string> issues,
            out string? mirror, out string[]? mirrorFields, out int[]? mirrorOffsets, out string? mirrorLayout)
        {
            mirror = null;
            mirrorFields = null;
            mirrorOffsets = null;
            mirrorLayout = null;
            MirrorFields(type, out string[]? fields, out int[]? offsets, out string? layout);
            if (fields == null)
            {
                issues.Add("the layout mirror of '" + identity.DisplayName + "' cannot be reproduced");
                return;
            }
            foreach (MemberModel member in members)
            {
                if (member.Storage == MemberStorage.AutoProperty && !PublicAutoProperty(type, member.Name))
                {
                    issues.Add("member '" + member.Name + "' has a non-public or attributed accessor");
                    return;
                }
                if (!MirrorStorageContains(type, member.Name))
                {
                    issues.Add("member '" + member.Name + "' is not part of the reconstructed storage");
                    return;
                }
            }
            mirror = MirrorName(type, identity);
            mirrorFields = fields;
            mirrorOffsets = offsets;
            mirrorLayout = layout;
        }

        /// <summary>Lists the instance storage fields of a version-tolerant record in declaration order.</summary>
        /// <param name="type">The declared payload.</param>
        /// <returns>Each storage member's name, fully qualified type and value-type fact.</returns>
        private static List<(string Name, string TypeName, bool IsValue)> VersionLocals(INamedTypeSymbol type)
        {
            var locals = new List<(string Name, string TypeName, bool IsValue)>();
            foreach (ISymbol member in OrderedStorageMembers(type))
            {
                if (member.IsImplicitlyDeclared || member.IsStatic)
                    continue;
                ITypeSymbol storage;
                bool isValue;
                if (member is IPropertySymbol property)
                {
                    if (!IsAutoProperty(property))
                        continue;
                    storage = property.Type;
                    isValue = property.Type.IsValueType;
                }
                else if (member is IFieldSymbol field)
                {
                    storage = field.Type;
                    isValue = field.Type.IsValueType;
                }
                else
                    continue;
                locals.Add((member.Name, storage.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), isValue));
            }
            return locals;
        }

        /// <summary>Spells the layout mirror name of the schema, which omits the type-parameter list.</summary>
        /// <param name="type">The declared payload.</param>
        /// <returns>The mirror declaration name.</returns>
        private static string VersionLayout(INamedTypeSymbol type)
        {
            string formatted = FormatterTypeName(type);
            string digest;
            using (var hash = SHA256.Create())
            {
                digest = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(formatted))
                    .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
            string local = formatted.StartsWith("global::", StringComparison.Ordinal) ? formatted.Substring(8) : formatted;
            local = local.Replace('.', '_').Replace('+', '_');
            int generic = local.IndexOf('<');
            if (generic >= 0)
                local = local.Substring(0, generic);
            return "LuminLayout_" + digest + "_" + PortableSlotSuffix(type) + "_Local" + local;
        }

        /// <summary>Whether a modeled member is one of the shapes the version emitter writes and reads.</summary>
        /// <param name="member">The analyzed member.</param>
        /// <param name="fieldType">The legacy schema classification of the member's type.</param>
        /// <returns>Whether the version schema covers the member.</returns>
        /// <remarks>The legacy direct model rejects every sequence member of a version-tolerant record, including a plain list,
        /// so only fixed scalars, enums and strings are covered here.</remarks>
        private static bool VersionMemberCovered(MemberModel member, int fieldType) => member switch
        {
            ScalarMember { Raw: false } scalar => scalar.EnumType != null ? fieldType == 14 : fieldType is >= 0 and <= 13,
            TextMember => fieldType == 12,
            _ => false,
        };

        /// <summary>Classifies a member's declared type with the legacy schema codes.</summary>
        /// <param name="type">The declared member type.</param>
        /// <returns>The classification, or null for a member the schema does not cover.</returns>
        private static int? VersionFieldType(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Int32: return 0;
                case SpecialType.System_UInt32: return 1;
                case SpecialType.System_Byte: return 2;
                case SpecialType.System_SByte: return 3;
                case SpecialType.System_Int16: return 4;
                case SpecialType.System_UInt16: return 5;
                case SpecialType.System_Int64: return 6;
                case SpecialType.System_UInt64: return 7;
                case SpecialType.System_Single: return 8;
                case SpecialType.System_Double: return 9;
                case SpecialType.System_Decimal: return 10;
                case SpecialType.System_Char: return 11;
                case SpecialType.System_String: return 12;
                case SpecialType.System_Boolean: return 13;
            }
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum })
                return 14;
            if (type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } list &&
                list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>")
                return 17;
            return null;
        }

        /// <summary>Whether the classification byte already identifies the exact primitive type.</summary>
        /// <param name="fieldType">The legacy schema classification.</param>
        /// <param name="type">The declared member type.</param>
        /// <returns>Whether an additional type identity would repeat the same information.</returns>
        private static bool VersionIsDirectPrimitive(int fieldType, ITypeSymbol type) => fieldType == type.SpecialType switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_UInt32 => 1,
            SpecialType.System_Byte => 2,
            SpecialType.System_SByte => 3,
            SpecialType.System_Int16 => 4,
            SpecialType.System_UInt16 => 5,
            SpecialType.System_Int64 => 6,
            SpecialType.System_UInt64 => 7,
            SpecialType.System_Single => 8,
            SpecialType.System_Double => 9,
            SpecialType.System_Decimal => 10,
            SpecialType.System_Char => 11,
            SpecialType.System_String => 12,
            SpecialType.System_Boolean => 13,
            _ => -1,
        };

        /// <summary>Builds the compact declaration-time wire classification byte.</summary>
        /// <param name="fieldType">The legacy schema classification.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <returns>The one-character representation string.</returns>
        private static string VersionRepresentation(int fieldType, WirePolicy policy)
        {
            int encoding = policy.Strings.Encoding == StringEncoding.Utf8 ? 0 : 1;
            return new string((char)(fieldType | (encoding << 6) | ((int)policy.Strings.Recording << 7)), 1);
        }

        /// <summary>The revision-2 payload width of a member: a constant, 255 for a length-prefixed payload, or zero when unknown.</summary>
        /// <param name="fieldType">The legacy schema classification.</param>
        /// <param name="type">The declared member type.</param>
        /// <returns>The width byte value, or zero for a width known only at run time.</returns>
        private static int VersionWidth(int fieldType, ITypeSymbol type)
        {
            if (fieldType is 12 or 17)
                return 255;
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying })
                type = underlying;
            return type.SpecialType switch
            {
                SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
                SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
                SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
                SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
                SpecialType.System_Decimal => 16,
                _ => 0,
            };
        }

        /// <summary>Normalizes a symbol using the runtime's assembly-independent primitive codes.</summary>
        /// <param name="type">The declared member or generic argument.</param>
        /// <returns>The exact identity, or null for an open type parameter.</returns>
        private static string? VersionTypeIdentity(ITypeSymbol type)
        {
            string? primitive = type.SpecialType switch
            {
                SpecialType.System_Boolean => "b",
                SpecialType.System_Byte => "u1",
                SpecialType.System_SByte => "i1",
                SpecialType.System_Int16 => "i2",
                SpecialType.System_UInt16 => "u2",
                SpecialType.System_Int32 => "i4",
                SpecialType.System_UInt32 => "u4",
                SpecialType.System_Int64 => "i8",
                SpecialType.System_UInt64 => "u8",
                SpecialType.System_Single => "f4",
                SpecialType.System_Double => "f8",
                SpecialType.System_Decimal => "d16",
                SpecialType.System_Char => "c2",
                SpecialType.System_String => "s",
                SpecialType.System_Object => "o",
                SpecialType.System_IntPtr => "ni",
                SpecialType.System_UIntPtr => "nu",
                SpecialType.System_DateTime => "dt",
                _ => null,
            };
            if (primitive != null)
                return "#" + primitive;
            if (type is IArrayTypeSymbol array)
            {
                string? element = VersionTypeIdentity(array.ElementType);
                return element == null ? null : element + "[" + new string(',', array.Rank - 1) + "]";
            }
            if (type is not INamedTypeSymbol named)
                return null;
            if (named.ContainingAssembly.Name is "System.Private.CoreLib" or "mscorlib" or "System.Runtime" or "netstandard")
            {
                string? known = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) switch
                {
                    "global::System.Guid" => "g",
                    "global::System.TimeSpan" => "ts",
                    "global::System.DateTimeOffset" => "dto",
                    _ => null,
                };
                if (known != null)
                    return "#" + known;
            }
            var levels = new Stack<INamedTypeSymbol>();
            for (INamedTypeSymbol? current = named; current != null; current = current.ContainingType)
                levels.Push(current);
            var namespaces = new Stack<string>();
            for (INamespaceSymbol? current = named.ContainingNamespace; current != null && !current.IsGlobalNamespace;
                current = current.ContainingNamespace)
                namespaces.Push(current.MetadataName);
            string ns = namespaces.Count == 0 ? string.Empty : string.Join(".", namespaces) + ".";
            string name = "@" + ns + string.Join("+", levels.Select(level => level.MetadataName));
            var arguments = levels.SelectMany(level => level.TypeArguments).Select(VersionTypeIdentity).ToArray();
            if (arguments.Any(argument => argument == null))
                return null;
            return arguments.Length == 0 ? name : name + "<" + string.Join(";", arguments) + ";>";
        }

        /// <summary>Appends a UTF-8 metadata string with its unsigned base-128 length.</summary>
        /// <param name="target">The table under construction.</param>
        /// <param name="value">The exact metadata value.</param>
        private static void AppendVersionLength(List<byte> target, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            int length = bytes.Length;
            while (length >= 128)
            {
                target.Add((byte)(length | 128));
                length >>= 7;
            }
            target.Add((byte)length);
            target.AddRange(bytes);
        }

        /// <summary>Formats a value as a C# literal, as the schema field table spells its constants.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The quoted literal.</returns>
        private static string VersionLiteral(string value) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, true);

        /// <summary>Builds the legacy layout mirror of a record whose serialized storage is not publicly nameable.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="identity">The payload's identity, which supplies the mirror's parameter list.</param>
        /// <param name="versioned">Whether the record is version-tolerant.</param>
        /// <param name="derived">Whether the record's base type contributes storage.</param>
        /// <param name="policy">The effective wire policy.</param>
        /// <param name="members">The serialized members in wire order.</param>
        /// <param name="issues">Receives the reason the mirror is outside the modeled surface.</param>
        /// <param name="mirror">Receives the mirror declaration name, or null.</param>
        /// <param name="mirrorFields">Receives the mirror declarations in storage order, or null.</param>
        /// <param name="mirrorOffsets">Receives the explicit-layout offsets, or null.</param>
        /// <param name="mirrorLayout">Receives the CLR layout attribute line, or null.</param>
        private static void TryMirror(INamedTypeSymbol type, TypeIdentity identity, bool versioned, bool derived, WirePolicy policy,
            MemberModel[] members, List<string> issues, out string? mirror, out string[]? mirrorFields, out int[]? mirrorOffsets,
            out string? mirrorLayout)
        {
            mirror = null;
            mirrorFields = null;
            mirrorOffsets = null;
            mirrorLayout = null;
            // A versioned record owns a schema-owned mirror, a derived record needs its base storage and a declared profile,
            // selected constructor, callbacks or rent change the framing the direct record emitters reproduce.
            if (versioned || derived)
            {
                issues.Add("a mirrored record that is versioned or derived keeps its layout mirror outside the direct emitters");
                return;
            }
            if (!policy.Equals(WirePolicy.Default))
            {
                issues.Add("a mirrored record with a declared profile keeps its layout mirror outside the direct emitters");
                return;
            }
            if (SelectedConstructor(type) || Callbacks(type) || Rent(type))
            {
                issues.Add("a mirrored record with a selected constructor, callbacks or a pool rent keeps its layout mirror outside the direct emitters");
                return;
            }
            MirrorFields(type, out string[]? fields, out int[]? offsets, out string? layout);
            if (fields == null)
            {
                issues.Add("the layout mirror of '" + identity.DisplayName + "' cannot be reproduced");
                return;
            }
            foreach (MemberModel member in members)
            {
                // An auto-property is reached through its backing storage, which only the public accessors keep writable.
                if (member.Storage == MemberStorage.AutoProperty && !PublicAutoProperty(type, member.Name))
                {
                    issues.Add("member '" + member.Name + "' has a non-public or attributed accessor");
                    return;
                }
                if (!MirrorStorageContains(type, member.Name))
                {
                    issues.Add("member '" + member.Name + "' is not part of the reconstructed storage");
                    return;
                }
            }
            mirror = MirrorName(type, identity);
            mirrorFields = fields;
            mirrorOffsets = offsets;
            mirrorLayout = layout;
        }

        /// <summary>Spells the legacy layout mirror name of a declared payload.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="identity">The payload's identity, which supplies the mirror's parameter list.</param>
        /// <returns>The mirror declaration name.</returns>
        private static string MirrorName(INamedTypeSymbol type, TypeIdentity identity)
        {
            string formatted = FormatterTypeName(type);
            string digest;
            using (var hash = SHA256.Create())
            {
                digest = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(formatted))
                    .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
            // BuildLocalMyClassName strips the global:: prefix, joins the declaring names with underscores and drops the
            // generic arguments, which the parameter list spells after the local class name.
            string local = formatted.StartsWith("global::", StringComparison.Ordinal) ? formatted.Substring(8) : formatted;
            local = local.Replace('.', '_').Replace('+', '_');
            int generic = local.IndexOf('<');
            if (generic >= 0)
                local = local.Substring(0, generic);
            string parameters = identity.TypeParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", identity.TypeParameters) + ">";
            return "LuminLayout_" + digest + "_" + PortableSlotSuffix(type) + "_Local" + local + parameters;
        }

        /// <summary>Whether a declared auto-property has public accessors and no accessor attributes.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="name">The property name.</param>
        /// <returns>Whether the mirror reaches the property's backing storage without accessor routing.</returns>
        private static bool PublicAutoProperty(INamedTypeSymbol type, string name)
        {
            foreach (INamedTypeSymbol level in Levels(type))
                foreach (IPropertySymbol property in level.GetMembers().OfType<IPropertySymbol>())
                {
                    if (property.Name != name)
                        continue;
                    IMethodSymbol? getter = property.GetMethod, setter = property.SetMethod;
                    return getter is { DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Public } &&
                        (setter == null || setter.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public) &&
                        getter.GetAttributes().Length == 0 && (setter == null || setter.GetAttributes().Length == 0);
                }
            return false;
        }

        /// <summary>Whether the reconstructed storage of a payload declares a member with this name.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="name">The serialized member name.</param>
        /// <returns>Whether the layout mirror declares the member's storage slot.</returns>
        private static bool MirrorStorageContains(INamedTypeSymbol type, string name)
        {
            foreach (INamedTypeSymbol level in Levels(type))
                foreach (ISymbol member in OrderedStorageMembers(level))
                {
                    if (member.IsImplicitlyDeclared || member.IsStatic)
                        continue;
                    if (member is IPropertySymbol property && !IsAutoProperty(property))
                        continue;
                    if (member is IFieldSymbol or IPropertySymbol && member.Name == name)
                        return true;
                }
            return false;
        }

        /// <summary>Recognizes the scalar spellings a mirror declares without a nullable annotation.</summary>
        /// <param name="spelling">The type spelling.</param>
        /// <returns>Whether the spelling names an unmanaged scalar keyword.</returns>
        private static bool IsUnmanagedKeyword(string spelling) => spelling is "byte" or "sbyte" or "bool" or "short" or "ushort" or
            "int" or "uint" or "long" or "ulong" or "float" or "double" or "decimal" or "char";

        /// <summary>Lists the payload's base chain from the outermost base to the declaration.</summary>
        /// <param name="symbol">The declared payload.</param>
        /// <returns>The levels whose storage joins the record.</returns>
        private static List<INamedTypeSymbol> Levels(INamedTypeSymbol symbol)
        {
            var levels = new List<INamedTypeSymbol>();
            for (var level = symbol; level != null && level.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
                level = level.BaseType)
                levels.Insert(0, level);
            return levels;
        }

        /// <summary>Enumerates a type's fields and auto-properties in declaration order.</summary>
        /// <param name="type">The type whose storage order is required.</param>
        /// <returns>The declared storage members in source or metadata order.</returns>
        private static IEnumerable<ISymbol> OrderedStorageMembers(INamedTypeSymbol type)
        {
            if (type.DeclaringSyntaxReferences.Length != 0)
                return type.GetMembers().Where(member => member is IPropertySymbol or IFieldSymbol)
                    .OrderBy(member => member.DeclaringSyntaxReferences.FirstOrDefault()?.Span.Start ?? int.MaxValue);
            // Metadata groups properties separately from fields. Field-table order retains each automatic
            // property's original storage position among ordinary and hidden fields.
            var properties = type.GetMembers().OfType<IPropertySymbol>().Where(IsAutoProperty)
                .ToDictionary(property => "<" + property.Name + ">k__BackingField", property => (ISymbol)property, StringComparer.Ordinal);
            return type.GetMembers().OfType<IFieldSymbol>()
                .Select(field => properties.TryGetValue(field.Name, out ISymbol? property) ? property : field);
        }

        /// <summary>Whether a property is an instance auto-property with compiler-generated backing storage.</summary>
        /// <param name="property">The declared property.</param>
        /// <returns>Whether the property stores its value in a backing field.</returns>
        private static bool IsAutoProperty(IPropertySymbol property)
        {
            if (property.IsStatic || property.IsAbstract || property.IsExtern || property.IsIndexer)
                return false;
            return property.ContainingType.GetMembers().OfType<IFieldSymbol>()
                .Any(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
        }
    }

    /// <summary>The payloads on one expansion path, and whether that path reached a cycle.</summary>
    private sealed class Path
    {
        /// <summary>The payloads currently being expanded.</summary>
        internal readonly HashSet<INamedTypeSymbol> Active = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        /// <summary>Whether any payload on this path referred back to an active payload.</summary>
        internal bool SawCycle;
    }

    /// <summary>The cache key of a completed analysis: one payload at one nested depth.</summary>
    private readonly struct AnalysisKey : IEquatable<AnalysisKey>
    {
        private readonly INamedTypeSymbol _symbol;
        private readonly int _depth;

        internal AnalysisKey(INamedTypeSymbol symbol, int depth)
        {
            _symbol = symbol;
            _depth = depth;
        }

        /// <inheritdoc/>
        public bool Equals(AnalysisKey other) => _depth == other._depth && SymbolEqualityComparer.Default.Equals(_symbol, other._symbol);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is AnalysisKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => (SymbolEqualityComparer.Default.GetHashCode(_symbol) * 31) + _depth;
    }

    /// <summary>Builds the identity of a declared type, including its containing chain and parameters.</summary>
    private static TypeIdentity Identity(INamedTypeSymbol symbol)
    {
        var containing = new List<string>();
        for (var current = symbol.ContainingType; current != null; current = current.ContainingType)
        {
            if (current.IsGenericType)
                throw new NotSupportedException("a payload nested in a generic definition is not modeled yet");
            containing.Insert(0, current.Name);
        }
        string? @namespace = symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace
            ? null : symbol.ContainingNamespace.ToDisplayString();
        return new TypeIdentity(@namespace, containing.ToArray(), symbol.Name,
            symbol.TypeParameters.Select(parameter => parameter.Name).ToArray());
    }

    /// <summary>Classifies the payload protocol from the declaration and its generator type.</summary>
    /// <param name="symbol">The declared payload type.</param>
    /// <param name="generatorType">The declared generation mode name.</param>
    /// <returns>The payload protocol.</returns>
    private static PayloadKind Kind(INamedTypeSymbol symbol, string generatorType)
    {
        if (symbol.TypeKind == TypeKind.Interface || symbol.IsAbstract)
            return PayloadKind.Union;
        if (generatorType == "CircleReference")
            return PayloadKind.CircleReference;
        if (symbol.IsValueType)
            return PayloadKind.Struct;
        return PayloadKind.Record;
    }

    /// <summary>Reads the enum member name of the packable attribute's generation mode.</summary>
    /// <param name="attribute">The packable attribute instance.</param>
    /// <returns>The declared mode name, or <c>Object</c> for the default.</returns>
    private static string GeneratorTypeName(AttributeData attribute)
    {
        TypedConstant argument = default;
        bool found = false;
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key != "generatorType")
                continue;
            argument = named.Value;
            found = true;
        }
        if (!found && attribute.ConstructorArguments.Length != 0)
        {
            argument = attribute.ConstructorArguments[0];
            found = true;
        }
        if (!found || argument.Value == null || argument.Type is not INamedTypeSymbol enumType)
            return "Object";
        int value = Convert.ToInt32(argument.Value, CultureInfo.InvariantCulture);
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            if (member.HasConstantValue &&
                Convert.ToInt32(member.ConstantValue, CultureInfo.InvariantCulture) == value)
                return member.Name;
        return "Object";
    }

    /// <summary>Spells the canonical Union slot suffix for an identity.</summary>
    /// <param name="identity">The Union root identity.</param>
    /// <returns>The slot suffix.</returns>
    private static string SlotSuffix(TypeIdentity identity)
    {
        // Slot names are a public override contract, so this spelling stays as the original FNV identity.
        string name = identity.GlobalName;
        uint hash = 2166136261;
        foreach (char character in name)
        {
            hash ^= character;
            hash *= 16777619;
        }
        string readable = new string(name.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());
        return readable + "_" + hash.ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a symbol carries the named attribute.</summary>
    /// <param name="symbol">The attributed symbol.</param>
    /// <param name="attributeName">The attribute's full metadata name.</param>
    /// <returns>Whether the attribute is present.</returns>
    private static bool HasAttribute(ISymbol symbol, string attributeName)
        => symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == attributeName);

    /// <summary>Reads a named Boolean declaration switch on the symbol's option attributes.</summary>
    /// <param name="symbol">The attributed symbol.</param>
    /// <param name="name">The named argument.</param>
    /// <returns>Whether any option declaration sets the switch to true.</returns>
    private static bool OptionSwitch(ISymbol symbol, string name)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != OptionAttribute)
                continue;
            foreach (var argument in attribute.NamedArguments)
                if (argument.Key == name && argument.Value.Value is bool enabled)
                    return enabled;
        }
        return false;
    }

    /// <summary>Whether a type carries the packable attribute.</summary>
    /// <param name="symbol">The candidate type.</param>
    /// <returns>Whether the type declares a generated payload contract.</returns>
    private static bool IsPackable(INamedTypeSymbol symbol) => HasAttribute(symbol, PackableAttribute);

    /// <summary>Reads a packable declaration's generation mode.</summary>
    /// <param name="symbol">The candidate type.</param>
    /// <returns>The declared mode name, or <c>Object</c> when the type is not packable.</returns>
    private static string GenerationMode(INamedTypeSymbol symbol)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(item =>
            item.AttributeClass?.ToDisplayString() == PackableAttribute);
        return attribute == null ? "Object" : GeneratorTypeName(attribute);
    }

    /// <summary>Whether a type mentions a parameter declared by the payload definition's chain.</summary>
    /// <param name="type">The referenced type.</param>
    /// <param name="owner">The payload that declares the member.</param>
    /// <returns>Whether the generic serializer owns the reference.</returns>
    private static bool MentionsOwnParameter(ITypeSymbol type, INamedTypeSymbol owner)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return Owns(parameter, owner);
            case IArrayTypeSymbol array:
                return MentionsOwnParameter(array.ElementType, owner);
            case INamedTypeSymbol named:
                return named.TypeArguments.Any(argument => MentionsOwnParameter(argument, owner));
            default:
                return false;
        }
    }

    /// <summary>Whether a type parameter belongs to the payload definition or one of its containing types.</summary>
    /// <param name="parameter">The referenced parameter.</param>
    /// <param name="owner">The payload that declares the member.</param>
    /// <returns>Whether the parameter is declared by the payload chain.</returns>
    private static bool Owns(ITypeParameterSymbol parameter, INamedTypeSymbol owner)
    {
        for (var type = owner; type != null; type = type.ContainingType)
            if (type.TypeParameters.Any(candidate => candidate.Name == parameter.Name))
                return true;
        return false;
    }

    /// <summary>Spells a collection or dictionary exactly as the declaration names it.</summary>
    /// <param name="type">The declared type.</param>
    /// <returns>The fully qualified spelling.</returns>
    private static string DeclaredType(INamedTypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Builds a symbol-free reference to a declared type.</summary>
    /// <param name="type">The referenced type.</param>
    /// <returns>The reference used by element, key and payload slots.</returns>
    private static TypeRef Reference(ITypeSymbol type) => new TypeRef(
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        type.NullableAnnotation == NullableAnnotation.Annotated || IsNullableValueType(type),
        type is ITypeParameterSymbol);

    /// <summary>Whether a type is a constructed nullable value type.</summary>
    /// <param name="type">The referenced type.</param>
    /// <returns>Whether the type admits null through <see cref="Nullable{T}"/>.</returns>
    private static bool IsNullableValueType(ITypeSymbol type)
        => type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    /// <summary>Spells a covered fixed scalar.</summary>
    /// <param name="type">The member or leaf type.</param>
    /// <returns>The scalar keyword and packed width, or null for every other type.</returns>
    private static (string Keyword, int Size)? Scalar(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return null;
        return type.SpecialType switch
        {
            SpecialType.System_Boolean => ("bool", 1),
            SpecialType.System_Byte => ("byte", 1),
            SpecialType.System_SByte => ("sbyte", 1),
            SpecialType.System_Int16 => ("short", 2),
            SpecialType.System_UInt16 => ("ushort", 2),
            SpecialType.System_Char => ("char", 2),
            SpecialType.System_Int32 => ("int", 4),
            SpecialType.System_UInt32 => ("uint", 4),
            SpecialType.System_Single => ("float", 4),
            SpecialType.System_Int64 => ("long", 8),
            SpecialType.System_UInt64 => ("ulong", 8),
            SpecialType.System_Double => ("double", 8),
            SpecialType.System_Decimal => ("decimal", 16),
            _ => null,
        };
    }

    /// <summary>Spells a covered fixed scalar, optionally excluding Booleans.</summary>
    /// <param name="type">The element, key or value type.</param>
    /// <param name="allowBoolean">Whether a Boolean is a covered scalar.</param>
    /// <returns>The scalar keyword and packed width, or null for every other type.</returns>
    private static (string Keyword, int Size)? Scalar(ITypeSymbol type, bool allowBoolean)
        => !allowBoolean && type.SpecialType == SpecialType.System_Boolean ? null : Scalar(type);

    /// <summary>Spells a framework built-in that has a generated formatter and a runtime native width.</summary>
    /// <param name="type">The member type.</param>
    /// <returns>The fully qualified spelling, or null for every other type.</returns>
    private static string? Builtin(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated ||
            type is not INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } named)
            return null;
        return named.ToDisplayString() switch
        {
            "System.DateTime" or "System.Guid" or "System.DateTimeOffset" or "System.TimeSpan"
                => named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            _ => null,
        };
    }

    /// <summary>Gives the fixed width and alignment of framework structs whose metadata hides their storage.</summary>
    /// <param name="type">The storage type.</param>
    /// <returns>The width and both target alignments, or null for every other type.</returns>
    private static (int Size, int Alignment64, int Alignment32)? Opaque(INamedTypeSymbol type)
        => type.ToDisplayString() switch
        {
            "System.DateTime" or "System.TimeSpan" or "System.TimeOnly" => (8, 8, 4),
            "System.DateOnly" or "System.Index" => (4, 4, 4),
            "System.Guid" => (16, 4, 4),
            "System.Range" => (8, 4, 4),
            "System.Numerics.Vector2" => (8, 4, 4),
            "System.Numerics.Vector3" => (12, 4, 4),
            "System.Numerics.Vector4" or "System.Numerics.Quaternion" or "System.Numerics.Plane" => (16, 4, 4),
            _ => null,
        };

    /// <summary>Classifies a declared sequence family.</summary>
    /// <param name="type">The declared member type.</param>
    /// <returns>The family, or null when the declaration is not a modeled sequence.</returns>
    private static CollectionKind? SequenceKind(INamedTypeSymbol type)
    {
        if (!type.IsGenericType || type.TypeArguments.Length != 1)
            return null;
        string @namespace = type.OriginalDefinition.ContainingNamespace.ToDisplayString();
        string name = type.OriginalDefinition.Name;
        if (@namespace == "System.Collections.Generic")
        {
            switch (name)
            {
                case "List":
                    return type.TypeArguments[0] is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } inner &&
                        inner.OriginalDefinition.Name == "List" &&
                        inner.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
                        ? CollectionKind.NestedList : CollectionKind.List;
                case "Stack":
                    return CollectionKind.Stack;
                case "SortedSet":
                    return CollectionKind.SortedSet;
            }
        }
        if (@namespace == "System.Collections.Immutable" &&
            name is "ImmutableArray" or "ImmutableList" or "ImmutableHashSet" or "ImmutableSortedSet" or "ImmutableQueue" or "ImmutableStack")
            return CollectionKind.Immutable;
        if (@namespace == "System.Collections.Concurrent" && name is "ConcurrentBag" or "ConcurrentQueue" or "ConcurrentStack")
            return CollectionKind.Concurrent;
        return null;
    }

    /// <summary>Classifies a declared dictionary family.</summary>
    /// <param name="type">The declared member type.</param>
    /// <returns>The family, or null when the declaration is not a modeled dictionary.</returns>
    private static DictionaryKind? DictionaryKind(INamedTypeSymbol type)
    {
        if (!type.IsGenericType || type.TypeArguments.Length != 2)
            return null;
        string @namespace = type.OriginalDefinition.ContainingNamespace.ToDisplayString();
        string name = type.OriginalDefinition.Name;
        if (@namespace == "System.Collections.Generic")
        {
            switch (name)
            {
                case "Dictionary":
                    return Model.DictionaryKind.Dictionary;
                case "SortedDictionary":
                    return Model.DictionaryKind.SortedDictionary;
                case "SortedList":
                    return Model.DictionaryKind.SortedList;
            }
        }
        if (@namespace == "System.Collections.Concurrent" && name == "ConcurrentDictionary")
            return Model.DictionaryKind.Concurrent;
        if (@namespace == "System.Collections.Immutable" && name is "ImmutableDictionary" or "ImmutableSortedDictionary")
            return Model.DictionaryKind.Immutable;
        return null;
    }

    /// <summary>Selects the framing of a complete nested payload.</summary>
    /// <param name="nested">The nested payload model.</param>
    /// <returns>The member framing.</returns>
    private static PayloadShape ShapeOf(TypeModel nested) => nested switch
    {
        CircleReferenceModel => PayloadShape.ReferenceTracked,
        RecordModel { Versioned: false } => PayloadShape.Inline,
        _ => PayloadShape.MemberEntry,
    };
}
