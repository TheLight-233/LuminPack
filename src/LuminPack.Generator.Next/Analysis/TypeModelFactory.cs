using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
                    return new StructModel(identity, accessibility, members, policy, constraints, storage, layout,
                        RecordWidthSupported(symbol));
                }
                case PayloadKind.Union:
                {
                    UnionCaseModel[] cases = UnionCases(symbol, issues);
                    if (cases.Length == 0)
                        issues.Add("the Union root declares no cases");
                    bool wide = HasAttribute(symbol, WideTagAttribute) || OptionSwitch(symbol, "WideTag");
                    return new UnionModel(identity, accessibility, policy, constraints, cases,
                        isInterface: symbol.TypeKind == TypeKind.Interface, wideTags: wide, slotSuffix: SlotSuffix(identity),
                        tier: UnionDispatchTier.Virtual);
                }
                case PayloadKind.CircleReference:
                {
                    MemberModel[] members = Members(symbol, policy, depth, issues, out _, path);
                    return new CircleReferenceModel(identity, accessibility, members, policy, constraints, guarded: true);
                }
                default:
                {
                    MemberModel[] members = Members(symbol, policy, depth, issues, out bool derived, path);
                    bool versioned = generatorType == "VersionTolerant";
                    bool proven = !versioned && !derived && policy.Bound == null && Options.Net8 && Options.Unsafe && !Options.Unity &&
                        members.Length != 0 && members.Any(member => member is TextMember) &&
                        members.All(member => member is ScalarMember or TextMember);
                    return new RecordModel(identity, accessibility, members, policy, constraints, versioned, derived, proven);
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
                return new ScalarMember(name, storage, policy, underlying.Keyword, underlying.Size, raw: false, builtin: false);
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

        /// <summary>Builds the Union cases declared on a root.</summary>
        /// <param name="symbol">The Union root.</param>
        /// <param name="issues">Receives invalid case declarations.</param>
        /// <returns>The cases in tag order.</returns>
        private static UnionCaseModel[] UnionCases(INamedTypeSymbol symbol, List<string> issues)
        {
            var cases = new List<UnionCaseModel>();
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
                try
                {
                    cases.Add(new UnionCaseModel(tag, Identity(caseType), UnionCaseRead.OwnEntry, caseType.IsSealed, fixedCursor: false));
                }
                catch (NotSupportedException error)
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + ": " + error.Message);
                }
            }
            cases.Sort((left, right) => left.Tag.CompareTo(right.Tag));
            return cases.ToArray();
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
        // The final spelling is a stable hash of the metadata name; the scaffold keeps the readable portion so the shape is visible.
        return identity.Name;
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
