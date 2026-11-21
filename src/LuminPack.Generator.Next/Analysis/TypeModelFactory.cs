using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>The single analysis boundary: Roslyn symbols become <see cref="TypeModel"/> values here.</summary>
/// <remarks>Everything below this boundary is a pure model and never sees a symbol or a compilation. The factory classifies the
/// payload protocol, enumerates the serialized storage, normalizes every member into a model value and records the declaration-time
/// wire policy. Shapes outside the modeled surface return an incomplete or unsupported result with a specific reason; analysis never
/// throws at the consumer.</remarks>
internal static partial class TypeModelFactory
{
    private const string PackableAttribute = "LuminPack.Attribute.LuminPackableAttribute";
    private const string IgnoreAttribute = "LuminPack.Attribute.LuminPackIgnoreAttribute";
    private const string IncludeAttribute = "LuminPack.Attribute.LuminPackIncludeAttribute";
    private const string PackableObjectAttribute = "LuminPack.Attribute.LuminPackableObjectAttribute";
    private const string OptionAttribute = "LuminPack.Attribute.LuminPackOptionAttribute";
    private const string BoundedAttribute = "LuminPack.Attribute.LuminPackBoundedAttribute";
    private const string UnionAttribute = "LuminPack.Attribute.LuminPackUnionAttribute";
    private const string WideTagAttribute = "LuminPack.Attribute.LuminPackWideTagAttribute";
    private const string OrderAttribute = "LuminPack.Attribute.LuminPackOrderAttribute";
    private const string ConstructorAttribute = "LuminPack.Attribute.LuminPackConstructorAttribute";
    private const string PoolRentAttribute = "LuminPack.Attribute.LuminPackPoolRentAttribute";
    private const string OnSerializingAttribute = "LuminPackOnSerializingAttribute";
    private const string OnSerializedAttribute = "LuminPackOnSerializedAttribute";
    private const string OnDeserializingAttribute = "LuminPackOnDeserializingAttribute";
    private const string OnDeserializedAttribute = "LuminPackOnDeserializedAttribute";
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
        // A compilation that marks referenced roots is analyzed through its complete-metadata view.
        Compilation view = Complete(compilation);
        if (!ReferenceEquals(view, compilation))
        {
            symbol = InView(symbol, view);
            compilation = view;
        }
        Session session = Sessions.GetValue(compilation, item => new Session(item, options));
        // Project facts are created once per compilation. A caller that supplies different facts on the same compilation
        // gets an isolated run instead of a cache that was built under another caller's facts.
        return session.Options.Equals(options) ? session.Create(symbol, depth: 0) : new Session(compilation, options).Create(symbol, depth: 0);
    }

    /// <summary>Models the closed root type of an ordinary serializer call as the member shape that publishes its shared owner.</summary>
    /// <param name="type">The closed call payload type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The root member, or null when the type is a packable payload, unsupported or not completely modeled.</returns>
    /// <remarks>A packable payload publishes its own roots from its type model. Any other root is classified exactly like a
    /// field of that type, so it reuses the specialized and default formatter contracts of members.</remarks>
    internal static MemberModel? TryCreateRoot(ITypeSymbol type, Compilation compilation, GeneratorOptions options)
    {
        Compilation view = Complete(compilation);
        if (!ReferenceEquals(view, compilation))
        {
            type = InView(type, view);
            compilation = view;
        }
        Session session = Sessions.GetValue(compilation, item => new Session(item, options));
        return session.Options.Equals(options) ? session.ClassifyRoot(type) : new Session(compilation, options).ClassifyRoot(type);
    }

    /// <summary>Captures declaration diagnostics through the compilation-owned analysis cache.</summary>
    /// <param name="symbol">The attributed declaration.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="options">The immutable project facts.</param>
    /// <returns>The declaration diagnostics.</returns>
    internal static DiagnosticInfo[] Diagnose(INamedTypeSymbol symbol, Compilation compilation, GeneratorOptions options)
    {
        Session session = Sessions.GetValue(compilation, item => new Session(item, options));
        return session.Options.Equals(options) ? session.Diagnose(symbol) : new Session(compilation, options).Diagnose(symbol);
    }

    /// <summary>Captures assembly option errors once for the compilation.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The assembly diagnostics.</returns>
    internal static DiagnosticInfo[] DiagnoseAssembly(Compilation compilation, GeneratorOptions options)
    {
        Session session = Sessions.GetValue(compilation, item => new Session(item, options));
        return session.Options.Equals(options) ? session.DiagnoseAssembly() : new Session(compilation, options).DiagnoseAssembly();
    }

    /// <summary>Finds the registration a valid Custom declaration publishes.</summary>
    /// <param name="symbol">The attributed type.</param>
    /// <param name="options">The project facts.</param>
    /// <param name="json">Whether the type declares the complete JSON pair.</param>
    /// <returns>The fully qualified type to register, or null when the type registers nothing.</returns>
    /// <remarks>The binary pair is required and the JSON pair is registered only when both methods exist; registration is
    /// disabled by the assembly option. Signature errors are reported separately and suppress the registration.</remarks>
    internal static string? CustomRegistration(INamedTypeSymbol symbol, GeneratorOptions options, out bool json)
    {
        json = false;
        if (options.RegisterMode == RegisterMode.Disabled || GenerationMode(symbol) != "Custom")
            return null;
        bool Declares(string name) => symbol.GetMembers(name).OfType<IMethodSymbol>().Any(method => method.MethodKind == MethodKind.Ordinary);
        if (!Declares("Serialize") || !Declares("Deserialize"))
            return null;
        json = Declares("SerializeJson") && Declares("DeserializeJson");
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>Resolves the consuming assembly's own declared policy, which the registered fallback and generic entries use.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The value-equal assembly policy.</returns>
    internal static WirePolicy AssemblyPolicy(Compilation compilation) => Session.Apply(WirePolicy.Default, compilation.Assembly);

    /// <summary>Holds the per-compilation analysis state: the consumer compilation, its project facts and the completed models.</summary>
    private sealed partial class Session
    {
        private readonly Compilation _compilation;
        private readonly WirePolicy _assemblyPolicy;
        private readonly Dictionary<AnalysisKey, ModelResult> _completed = new Dictionary<AnalysisKey, ModelResult>();
        private readonly Dictionary<ITypeSymbol, MemberModel?> _roots = new Dictionary<ITypeSymbol, MemberModel?>(SymbolEqualityComparer.Default);
        private readonly Dictionary<INamedTypeSymbol, bool> _packedFields = new Dictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
        private readonly object _gate = new object();
        private bool? _canReuseCultureInfo;

        /// <summary>Initializes a <c>Session</c> from the supplied data.</summary>
        /// <param name="compilation">The compilation that supplies source symbols and references.</param>
        /// <param name="options">The options governing the operation.</param>
        internal Session(Compilation compilation, GeneratorOptions options)
        {
            Options = options;
            _compilation = compilation;
            _assemblyPolicy = Apply(WirePolicy.Default, compilation.Assembly);
        }

        /// <summary>The project facts this session analyzes under.</summary>
        internal GeneratorOptions Options { get; }

        /// <summary>Models one payload, stopping before a cycle or an over-deep nested expansion can recurse.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <param name="depth">The number of nested payload expansions already entered.</param>
        /// <returns>The analysis result.</returns>
        internal ModelResult Create(INamedTypeSymbol symbol, int depth) => Analyze(symbol, depth, new Path());

        /// <summary>Classifies a closed call root as an unnamed field of its type under the assembly policy.</summary>
        /// <param name="type">The closed call payload type.</param>
        /// <returns>The complete root member, or null.</returns>
        internal MemberModel? ClassifyRoot(ITypeSymbol type)
        {
            lock (_gate)
            {
                if (_roots.TryGetValue(type, out MemberModel? cached))
                    return cached;
            }
            MemberModel? result = ClassifyUncachedRoot(type);
            lock (_gate)
                _roots[type] = result;
            return result;
        }

        /// <summary>Builds one root's normalized formatter graph before publishing it to the compilation-owned cache.</summary>
        /// <param name="type">The closed call payload type.</param>
        /// <returns>The complete root member, or null.</returns>
        private MemberModel? ClassifyUncachedRoot(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && !CompilerUnion.IsUnion(named, _compilation) &&
                (!named.IsGenericType && (IsPackable(named) || Marked(named)) ||
                 named.IsGenericType && !IsLocal(named) && ExplicitMarker(named)))
                return null;
            var issues = new List<string>();
            MemberModel? model = Classify(new MemberSite("Root", type, isVolatile: false, containing: null), MemberStorage.Field, _assemblyPolicy,
                _compilation.GetSpecialType(SpecialType.System_Object), depth: 0, issues, new Path());
            return issues.Count == 0 ? model : null;
        }

        /// <summary>Whether the referenced runtime exposes the validated culture-info cache the shared owner uses.</summary>
        private bool CanReuseCultureInfo => _canReuseCultureInfo ??= CultureInfoCacheAvailable();

        /// <summary>Checks the cache declaration the generated owner resolves names through.</summary>
        /// <returns>Whether <c>LuminPack.Code.LuminPackCultureInfoCache.Name</c> and <c>ResolveCulture</c> are public.</returns>
        private bool CultureInfoCacheAvailable()
        {
            INamedTypeSymbol? cache = _compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCultureInfoCache");
            return cache?.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
                cache.GetMembers("Name").OfType<IPropertySymbol>().Any(property => property.IsStatic &&
                    property.GetMethod?.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
                    property.Type.SpecialType == SpecialType.System_String) &&
                cache.GetMembers("ResolveCulture").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 0 &&
                    method.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
                    method.ReturnType.ToDisplayString() == "System.Globalization.CultureInfo" &&
                    method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_String);
        }

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

        /// <summary>Models the native, packed, tagged or managed storage and JSON mirror of a struct.</summary>
        /// <param name="symbol">The declared struct.</param>
        /// <param name="identity">The normalized type identity.</param>
        /// <param name="accessibility">The generated declaration access.</param>
        /// <param name="policy">The declaration-owned wire policy.</param>
        /// <param name="depth">The current payload expansion depth.</param>
        /// <param name="issues">Receives unsupported payload-body facts.</param>
        /// <param name="path">The active payload expansion path.</param>
        /// <param name="generatorType">The declared serialization protocol.</param>
        /// <param name="constraints">The generic declaration constraints.</param>
        /// <returns>The struct storage and member model.</returns>
        private StructModel CreateStructModel(INamedTypeSymbol symbol, TypeIdentity identity, TypeAccessibility accessibility,
            WirePolicy policy, int depth, List<string> issues, Path path, string generatorType, string[] constraints)
        {
            MemberModel[] members = Members(symbol, policy, depth, issues, out _, path);
            // A closed generic struct whose definition is not unmanaged keeps its definition's object contract, which can close over
            // references, as the original state contract rule does; only a definition that is unmanaged itself stores natively.
            bool unmanaged = symbol.IsUnmanagedType && !(symbol.IsGenericType && !symbol.OriginalDefinition.IsUnmanagedType);
            PortableLayout? layout = unmanaged && generatorType == "Object" ? TryPortableLayout(symbol) : null;
            bool unsupportedPacked = unmanaged && IsUnsupportedPackedStorage(symbol);
            if (unsupportedPacked)
                issues.Add("the recursive Packed layout contains unsupported storage");
            bool nativeContract = unmanaged && generatorType == "Object" && !unsupportedPacked &&
                !ContainsCustomContract(symbol, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
            StructStorage storage = !nativeContract ? StructStorage.ManagedRecord :
                layout == null ? StructStorage.RawUnmanaged : StructStorage.PortableUnmanaged;
            // Managed and versioned values retain their constructors, callbacks and rental contracts alongside storage facts.
            bool managed = storage == StructStorage.ManagedRecord;
            string? mirror = null, mirrorLayout = null;
            string[]? mirrorFields = null;
            int[]? mirrorOffsets = null;
            if (members.Any(member => member.Mirrored))
                TryMirror(symbol, identity, false, false, policy, members, issues,
                    out mirror, out mirrorFields, out mirrorOffsets, out mirrorLayout);
            ConstructorModel? construction = SelectedConstructorModel(symbol, members, issues);
            CallbackModel? callbacks = SerializationCallbacks(symbol, identity.GlobalName);
            string? rentCall = PoolRent(symbol, identity.GlobalName, issues);
            if (rentCall != null && construction != null)
                construction = construction.Rented();
            bool rentByRef = rentCall != null && symbol.GetMembers().OfType<IMethodSymbol>()
                .Any(method => HasAttribute(method, PoolRentAttribute) && method.ReturnsByRef);
            VersionSchemaModel? version = generatorType == "VersionTolerant" ?
                TryVersionSchema(symbol, identity, members, policy, false, issues) : null;
            return new StructModel(identity, accessibility, members, policy, constraints, storage, layout,
                RecordWidthSupported(symbol), versioned: generatorType == "VersionTolerant",
                rent: managed && Rent(symbol), constructors: managed && Constructors(symbol),
                callbacks: managed && Callbacks(symbol), storagePlan: unmanaged && generatorType == "Object" ? FormatterStorage(symbol) : null,
                mirror: mirror, mirrorFields: mirrorFields, mirrorOffsets: mirrorOffsets, mirrorLayout: mirrorLayout,
                construction: construction, serializationCallbacks: callbacks, version: version, rentCall: rentCall, rentByRef: rentByRef);
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
            // A plain class reached as a member is expanded like a nested object record without its own declaration; a referenced
            // type marked by the assembly is an Object payload of this compilation.
            if (attribute == null && !IsImplicitInlineClass(symbol) && !Marked(symbol))
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

            string generatorType = attribute == null ? "Object" : GeneratorTypeName(attribute);
            if (generatorType is "NonGenerator" or "Custom")
            {
                issues.Add("generator type '" + generatorType + "' owns its formatter outside the payload renderers");
                return null;
            }

            TypeAccessibility accessibility = symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public
                ? TypeAccessibility.Public : TypeAccessibility.Internal;
            string[] constraints = TypeConstraints(symbol);
            PayloadKind kind = Kind(symbol);
            WirePolicy policy = TypePolicy(symbol);
            // An explicit hostile-input declaration rejects input-selected shapes in both directions; the implicit default keeps
            // them working with checked reads.
            if (policy.Input == InputPolicy.Untrusted && policy.Limits.Explicit &&
                kind != PayloadKind.Union && Rejection(symbol) is { } rejection)
            {
                if (kind == PayloadKind.Struct)
                    return new StructModel(identity, accessibility, Array.Empty<MemberModel>(), policy, constraints,
                        StructStorage.ManagedRecord, layout: null, recordWidth: false, versioned: false,
                        rent: false, constructors: false, callbacks: false, rejection: rejection);
                return new RecordModel(identity, accessibility, Array.Empty<MemberModel>(), policy, constraints, versioned: false,
                    fixedUnionCase: false, derived: false, proven: false, recordWidth: false, freshResult: false, mirror: null,
                    mirrorFields: null, mirrorOffsets: null, mirrorLayout: null, version: null, rejection: rejection);
            }

            switch (kind)
            {
                case PayloadKind.Struct:
                {
                    return CreateStructModel(symbol, identity, accessibility, policy, depth, issues, path, generatorType, constraints);
                }
                case PayloadKind.Union:
                {
                    var declared = UnionMembers(symbol, issues, out bool wideCase);
                    bool wide = HasAttribute(symbol, WideTagAttribute) || OptionSwitch(symbol, "WideTag") || wideCase;
                    HashSet<ITypeSymbol> fixedCases = FixedUnionCases(symbol, declared, issues);
                    UnionCaseModel[] cases = UnionCases(symbol, declared, fixedCases, depth, issues, path, out bool readGeneric);
                    if (cases.Length == 0)
                        issues.Add("the Union root declares no cases");
                    UnionDispatchTier tier = UnionTier(symbol);
                    int encoding = (int)policy.Strings.Encoding, recording = (int)policy.Strings.Recording;
                    // An imported root writes through the virtual dispatch its own assembly generated, as the original referenced
                    // dispatch did; this assembly only reads, so the root must expose the compiled slots of both formats.
                    bool referenced = !SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _compilation.Assembly);
                    string suffix = SlotSuffix(referenced && symbol.IsGenericType ? Identity(symbol.OriginalDefinition) : identity);
                    if (referenced && tier == UnionDispatchTier.Virtual)
                        ReferencedDispatch(symbol, suffix, issues);
                    string? unionRejection = policy.Input == InputPolicy.Untrusted && policy.Limits.Explicit ? Rejection(symbol) : null;
                    // The open definition keeps its virtual slots, including registered and user-supplied overrides.
                    // A closed class writer can call those same slots directly for exact sealed cases.
                    string? closedWriteSlot = !referenced && tier == UnionDispatchTier.Virtual && unionRejection == null &&
                        symbol is { TypeKind: TypeKind.Class, IsGenericType: true, IsDefinition: false } &&
                        !ContainsTypeParameter(symbol) && cases.Length is > 0 and <= 32 &&
                        declared.All(item => item.Type is { TypeKind: TypeKind.Class, IsSealed: true })
                        ? SlotSuffix(Identity(symbol.OriginalDefinition)) : null;
                    return new UnionModel(identity, accessibility, policy, constraints, cases,
                        isInterface: symbol.TypeKind == TypeKind.Interface, wideTags: wide, unionSuffix: suffix,
                        tier: tier,
                        cursor: fixedCases.Count != 0, readGeneric: readGeneric, chain: Chain(symbol), @namespace: identity.Namespace,
                        binarySlots: UnionSlots(json: false, encoding, recording), jsonSlots: UnionSlots(json: true, encoding, 0),
                        canonical: CanonicalUnionProfiles(),
                        standardBinary: UnionProfileAlias(encoding, recording),
                        standardJson: UnionProfileAlias(encoding, recording), referenced: referenced, rejection: unionRejection,
                        closedWriteSlot: closedWriteSlot, hidesGetType: HidesObjectGetType(symbol));
                }
                default:
                {
                    MemberModel[] members = Members(symbol, policy, depth, issues, out bool derived, path);
                    bool versioned = generatorType is "VersionTolerant" or "CircleReference";
                    // A class whose serialized content is only a supported List<T> base publishes that list's body. Modeling it
                    // as an empty record would write the object marker alone and silently drop every element on read.
                    bool includeCollectionBase = OptionSwitch(symbol, "IncludeCollectionBase") && CanIncludeCollectionBase(symbol);
                    INamedTypeSymbol? sequenceBase = !includeCollectionBase && members.Length == 0 && !versioned ? SequenceBase(symbol) : null;
                    FormatterModel? collectionBase = includeCollectionBase ? Formatter(CollectionBaseType(symbol)!, depth + 1, path) : null;
                    if (includeCollectionBase && collectionBase == null)
                        issues.Add("the inherited list's element formatter is not modeled");
                    // A selected constructor, a pool rent and the serialization callbacks shape the record roots the same way
                    // the legacy model computes them; a rent supplies the instance instead of the constructor.
                    ConstructorModel? constructor = SelectedConstructorModel(symbol, members, issues);
                    string? rent = PoolRent(symbol, identity.GlobalName, issues);
                    bool rentByRef = policy.TrackReferences && rent != null && symbol.GetMembers().OfType<IMethodSymbol>()
                        .Any(method => HasAttribute(method, PoolRentAttribute) && method.ReturnsByRef);
                    CallbackModel? callbacks = SerializationCallbacks(symbol, identity.GlobalName);
                    if (constructor != null && rent != null)
                        constructor = constructor.Rented();
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
                    else if (members.Any(member => member.Mirrored) || rent != null)
                        TryMirror(symbol, identity, versioned, derived, policy, members, issues,
                            out mirror, out mirrorFields, out mirrorOffsets, out mirrorLayout);
                    // The proven object write needs every variable member to be a string; a rent declares a layout mirror for
                    // its public fields, which the proven write still reaches directly.
                    bool proven = !policy.TrackReferences && !versioned && !derived && !members.Any(member => member.Mirrored) &&
                        policy.Bound == null && Options.Net8 && Options.Unsafe && !Options.Unity && members.Length != 0 &&
                        members.Any(member => member is TextMember) && members.All(member => member is ScalarMember or TextMember);
                    return new RecordModel(identity, accessibility, members, policy, constraints, versioned,
                        fixedUnionCase: !versioned && members.All(member => member is ScalarMember) && IsFixedUnionCase(symbol),
                        derived, proven, !includeCollectionBase && !policy.TrackReferences && RecordWidthSupported(symbol),
                        // A pooled read rents its instance, so it never assigns a fresh result directly.
                        rent == null && CanDeserializeDirectlyIntoFreshResult(symbol, members),
                        mirror, mirrorFields, mirrorOffsets, mirrorLayout, version,
                        constructor, callbacks, rent, (mirror != null || versioned) ? CaptureMirrors(symbol, identity, issues) : Array.Empty<StorageMirrorModel>(),
                        // A declared depth limit counts nesting only on recursive records, including a trusted record read under
                        // a bounded root; without one, recursion keeps only its stack guard. A list-derived payload nests through
                        // its own elements, so it keeps the guard as well.
                        recursive: sequenceBase != null || IsRecursive(symbol), implicitPayload: attribute == null,
                        sequenceBase: sequenceBase?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        sequenceElement: sequenceBase?.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), collectionBase: collectionBase,
                        scalarHeader: symbol.BaseType?.SpecialType == SpecialType.System_Object && symbol.AllInterfaces.IsEmpty && HasScalarHeader,
                        stringPairs: Options.BinaryStringPairReader && !versioned && constructor == null &&
                            callbacks == null && rent == null && mirror == null && policy.Bound == null &&
                            symbol.BaseType is { SpecialType: SpecialType.System_Object } &&
                            !symbol.AllInterfaces.Any(contract => IsPackable(contract.OriginalDefinition) ||
                                HasAttribute(contract.OriginalDefinition, UnionAttribute)) &&
                            !symbol.GetMembers().OfType<IFieldSymbol>().Any(field => field.IsVolatile),
                        rentByRef: rentByRef, referenceConstruction: rent == null && (policy.TrackReferences ||
                            versioned && constructor == null && callbacks is { ReadBefore.Length: > 0 })
                            ? ReferenceConstruction(symbol, identity.GlobalName) : null);
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

            var members = new List<(MemberModel Model, uint Order)>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var level in levels)
            foreach (var member in OrderedStorageMembers(level))
            {
                if (!TryStorage(member, out MemberStorage storage))
                    continue;
                // Internal storage of a referenced assembly is reached through the layout mirror, as private storage is.
                if (storage == MemberStorage.Field && !_compilation.IsSymbolAccessibleWithin(member, _compilation.Assembly))
                    storage = MemberStorage.IncludedField;
                WirePolicy policy = MemberPolicy(member, ownerPolicy);
                bool ownFormatter = HasAttribute(member, PackableObjectAttribute);
                MemberModel? model = Classify(MemberSite.Of(member), storage, policy, symbol, depth, issues, path, ownFormatter);
                if (model == null)
                    continue;
                if (HasAttribute(member, CompressAttribute) && CompressibleCollection(StorageType(member), ownFormatter))
                    model = model.WithCompression();
                if (!names.Add(model.Name))
                {
                    issues.Add("the payload chain declares member '" + model.Name + "' more than once");
                    continue;
                }
                uint order = Order(member) ?? (uint)(member.DeclaringSyntaxReferences.FirstOrDefault()?.Span.Start ?? int.MaxValue);
                members.Add((model, order));
            }
            members.Sort((left, right) => left.Order.CompareTo(right.Order));
            return members.Select(item => item.Model).ToArray();
        }

        /// <summary>Finds the declared list base whose contents are a payload's entire body.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <returns>The closed list base whose element is the payload itself, or null when the shape is not supported.</returns>
        private INamedTypeSymbol? SequenceBase(INamedTypeSymbol symbol)
        {
            if (symbol.IsGenericType || symbol.IsAbstract || symbol.TypeKind != TypeKind.Class ||
                !symbol.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0 &&
                    _compilation.IsSymbolAccessibleWithin(constructor, _compilation.Assembly)))
                return null;
            INamedTypeSymbol? baseType = symbol.BaseType;
            // Only a self-referential list keeps the element formatter reachable: the payload's own owner writes each element.
            if (baseType is not { IsGenericType: true } || baseType.TypeArguments.Length != 1 ||
                !SymbolEqualityComparer.Default.Equals(baseType.TypeArguments[0], symbol))
                return null;
            INamedTypeSymbol definition = baseType.OriginalDefinition;
            return FormatterRegistry.FindSequence(definition.ContainingNamespace.ToDisplayString() + "." + definition.Name) is
                { Kind: CollectionKind.List } ? baseType : null;
        }

        /// <summary>Finds the framework list whose existing collection record can follow the declared members.</summary>
        /// <param name="symbol">The concrete record declaration.</param>
        /// <param name="includeSortedDictionary">Whether an inherited sorted dictionary is also eligible.</param>
        /// <returns>The list base, or null when no supported base exists.</returns>
        private INamedTypeSymbol? CollectionBaseType(INamedTypeSymbol symbol, bool includeSortedDictionary = false)
        {
            for (INamedTypeSymbol? type = symbol.BaseType; type != null; type = type.BaseType)
                if (type.Arity == 1 && IsFrameworkCollection(type, "System.Collections.Generic.List`1") ||
                    includeSortedDictionary && type.Arity == 2 && IsFrameworkCollection(type, "System.Collections.Generic.SortedDictionary`2"))
                    return type;
            return null;
        }

        /// <summary>Checks the declaration boundary of the appended collection record.</summary>
        /// <param name="symbol">The declaration that owns the option.</param>
        /// <returns>Whether the ordinary record and its closed list base can be written together.</returns>
        private bool CanIncludeCollectionBase(INamedTypeSymbol symbol) =>
            symbol is { TypeKind: TypeKind.Class, IsAbstract: false } && IsPackable(symbol) &&
            GenerationMode(symbol) == "Object" && !TracksReferences(symbol) &&
            CollectionBaseType(symbol) is { } list && !ContainsTypeParameter(list);

        /// <summary>Decides whether a declared member has serialized instance storage and how it is reached.</summary>
        /// <param name="member">The declared member.</param>
        /// <param name="storage">Receives the storage shape.</param>
        /// <returns>Whether the member participates in the wire format.</returns>
        private static bool TryStorage(ISymbol member, out MemberStorage storage)
        {
            switch (member)
            {
                case IFieldSymbol field when !field.IsStatic && !field.IsConst:
                    if (field.IsImplicitlyDeclared || HasAttribute(field, IgnoreAttribute))
                        break;
                    if (!field.IsReadOnly && field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)
                    {
                        storage = MemberStorage.Field;
                        return true;
                    }
                    if (HasAttribute(field, IncludeAttribute) || field.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)
                    {
                        storage = MemberStorage.IncludedField;
                        return true;
                    }
                    break;
                case IPropertySymbol property when IsAutoProperty(property):
                    if (InvalidOverrideAnnotation(property) != null || HasAttribute(property, IgnoreAttribute))
                        break;
                    if (property.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal ||
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
        private MemberModel? ClassifySpecialized(MemberSite member, MemberStorage storage, WirePolicy policy, INamedTypeSymbol owner, int depth,
            List<string> issues, Path path)
        {
            ITypeSymbol type = member.Type;
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
                    enumType: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), underlying: underlying.Keyword,
                    isVolatile: member.Volatile);
            }
            if (Scalar(type) is { } scalar)
                return new ScalarMember(name, storage, policy, scalar.Keyword, scalar.Size, raw: false, builtin: false,
                    isVolatile: member.Volatile);
            if (Builtin(type) is { } builtin)
                return new ScalarMember(name, storage, policy, builtin, 0, raw: true, builtin: true,
                    isVolatile: member.Volatile);
            if (type.SpecialType == SpecialType.System_String)
                return new TextMember(name, storage, policy);
            if (IsCultureInfo(type))
                return new CultureInfoMember(name, storage, policy, type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    type.NullableAnnotation == NullableAnnotation.Annotated, storage == MemberStorage.AutoProperty, CanReuseCultureInfo);
            // A key and value pair, a nullable or lazy value or a tuple of raw scalars and strings has a closed generic owner.
            if (GenericIdentity.Of(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) is { } genericIdentity)
                return new SpecialMember(name, storage, policy, "global::" + genericIdentity, false, storage == MemberStorage.AutoProperty);
            if (FormatterRegistry.HasStandalone(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty)))
                return new SpecialMember(name, storage, policy, type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    type.NullableAnnotation == NullableAnnotation.Annotated, storage == MemberStorage.AutoProperty);
            if (type is IArrayTypeSymbol array)
            {
                // No shared owner frames an array of culture values, and a payload-long formatter is not published.
                if (IsCultureInfo(array.ElementType))
                {
                    issues.Add("member '" + name + "' is a CultureInfo array, which has no shared formatter owner");
                    return null;
                }
                return ArrayMember(name, storage, policy, array, depth, issues, path);
            }

            if (type is INamedTypeSymbol named)
            {
                if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    issues.Add("member '" + name + "' is a nullable value type, which has no modeled scalar form");
                    return null;
                }
                if (DictionaryKind(named) is { } dictionaryKind)
                {
                    // No shared owner frames a keyed collection of culture values.
                    if (named.TypeArguments.Any(IsCultureInfo))
                    {
                        issues.Add("member '" + name + "' is a keyed collection of CultureInfo, which has no shared formatter owner");
                        return null;
                    }
                    return DictionaryMember(name, storage, policy, named, dictionaryKind, depth, issues, path);
                }
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
        /// <param name="depth">The current nesting depth.</param>
        /// <param name="issues">Receives unsupported contract details.</param>
        /// <param name="path">The path to the input, output, or active dependency.</param>
        private MemberModel ArrayMember(string name, MemberStorage storage, WirePolicy policy, IArrayTypeSymbol array, int depth,
            List<string> issues, Path path)
        {
            string declared = DeclaredType(array);
            TypeRef element = Reference(array.ElementType);
            if (array.Rank != 1)
            {
                if (array.ElementType.SpecialType == SpecialType.System_String)
                    return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element,
                        scalar: "string", elementText: true, formatterCall: true, arrayRank: array.Rank,
                        reuseArraySlots: array.Rank == 2 && HasStringMatrixReader(_compilation));
                if (RawElement(array.ElementType) is { } raw && raw.Owned != string.Empty)
                    return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element,
                        scalar: raw.Spelling, raw: true, formatterCall: true, ownedScalar: raw.Owned,
                        arrayRank: array.Rank, reuseArraySlots: Scalar(array.ElementType) != null || Builtin(array.ElementType) != null);
                if (NullableSequenceElement(array.ElementType) is { } nullableItem)
                    return new CollectionMember(name, storage, policy, declared,
                        CollectionKind.Array, element, scalar: nullableItem, formatterCall: true, arrayRank: array.Rank);
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element, arrayRank: array.Rank);
            }
            if (array.IsSZArray && NullableSequenceElement(array.ElementType) is { } nullable)
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element,
                    scalar: nullable, raw: true);
            if (Scalar(array.ElementType) is { } scalar)
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element, scalar: scalar.Keyword);
            if (array.IsSZArray && TryForwardedCollection(array.ElementType, depth, issues, path, name) is { } child)
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element, forwardedElement: child);
            // An array of unmanaged elements stores its span directly, like a list of them.
            if (array.IsSZArray && IsReservedElement(array.ElementType))
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element,
                    scalar: array.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    ownedScalar: ScalarFormatter(array.ElementType));
            // An array of strings transfers its elements through the shared string owner.
            if (array.IsSZArray && array.ElementType.SpecialType == SpecialType.System_String)
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element, scalar: "string",
                    elementText: true, formatterCall: true);
            // An array of packable classes expands each element's record body through the array owner.
            if (array.IsSZArray && LocalPackableClass(array.ElementType) is { } packable &&
                ElementModel(packable, depth, issues, path, name) is { } nested)
                return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element,
                    scalar: packable.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), nested: nested, formatterCall: true);
            // Any other element (unsupported nullable values, arrays of unsupported shapes, object) has no shared owner, so the member stays
            // unmodeled and the record keeps the original pipeline instead of calling an owner nothing publishes.
            return new CollectionMember(name, storage, policy, declared, CollectionKind.Array, element);
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
            if (kind == CollectionKind.List && NullableSequenceElement(item) is { } nullable)
                return new CollectionMember(name, storage, policy, declared,
                    kind, element, scalar: nullable, raw: true);
            if (kind == CollectionKind.Stack && RawElement(item) is { } stackItem)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: stackItem.Spelling, raw: true,
                    ownedScalar: stackItem.Owned);
            // A stack of strings transfers its elements through the shared string owner.
            if (kind == CollectionKind.Stack && item.SpecialType == SpecialType.System_String)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: "string", elementText: true, raw: true);
            // A sorted set stores its ordered tree as raw unmanaged storage, so a string element stays unclassified.
            // A sorted set of strings counts its in-order enumeration through the shared string owner.
            if (kind == CollectionKind.SortedSet && item.SpecialType == SpecialType.System_String)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: "string", elementText: true);
            if (kind == CollectionKind.SortedSet && RawElement(item) is { } setItem)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: setItem.Spelling, raw: true,
                    ownedScalar: setItem.Owned);
            // A linked list, hash set or queue of raw scalars transfers its elements through the shared formatter owner.
            if (kind is CollectionKind.Linked or CollectionKind.HashSet or CollectionKind.Queue or CollectionKind.Wrapper or CollectionKind.Interface or CollectionKind.Frozen && RawElement(item) is { } enumerableItem)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: enumerableItem.Spelling,
                    ownedScalar: enumerableItem.Owned);
            // The same families transfer string elements one at a time through the shared string owner.
            if (kind is CollectionKind.Linked or CollectionKind.HashSet or CollectionKind.Queue or CollectionKind.Wrapper or
                CollectionKind.Interface or CollectionKind.Frozen && item.SpecialType == SpecialType.System_String)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: "string", elementText: true);
            // A concurrent or immutable sequence of raw scalars transfers its storage through the shared formatter owner.
            if (kind is CollectionKind.Concurrent or CollectionKind.Immutable && RawElement(item) is { } rawSequenceItem)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: rawSequenceItem.Spelling,
                    ownedScalar: rawSequenceItem.Owned);
            if (kind == CollectionKind.Blocking && Scalar(item) is { } sequenceItem)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: sequenceItem.Keyword);
            // A concurrent or immutable sequence of strings transfers its elements through the shared string owner.
            if (kind is CollectionKind.Concurrent or CollectionKind.Immutable or CollectionKind.Blocking && item.SpecialType == SpecialType.System_String)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: "string", elementText: true);
            // A concurrent or immutable sequence of packable classes expands each element's record body through the sequence owner.
            if (kind is CollectionKind.Concurrent or CollectionKind.Immutable or CollectionKind.Linked or CollectionKind.HashSet or
                CollectionKind.Wrapper or CollectionKind.Interface or CollectionKind.Frozen or CollectionKind.Queue or CollectionKind.Stack or
                CollectionKind.SortedSet && LocalPackableClass(item) is { } sequenceElement)
            {
                TypeModel? nested = ElementModel(sequenceElement, depth, issues, path, name);
                if (nested == null)
                    return new CollectionMember(name, storage, policy, declared, kind, element);
                return new CollectionMember(name, storage, policy, declared, kind, element,
                    scalar: sequenceElement.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), nested: nested);
            }
            if (kind is CollectionKind.Concurrent or CollectionKind.List or CollectionKind.NestedList or CollectionKind.Blocking &&
                TryForwardedCollection(item, depth, issues, path, name) is { } child &&
                FormatterRegistry.FindSequence(type.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + type.OriginalDefinition.Name)?.Forwarded != null)
                return new CollectionMember(name, storage, policy, declared, kind, element, forwardedElement: child);
            if (kind is not (CollectionKind.List or CollectionKind.NestedList))
                return new CollectionMember(name, storage, policy, declared, kind, element);

            // A list of raw scalars stores its span directly; a raw unmanaged struct keeps its runtime-sized storage path.
            if (Scalar(item) is { } scalar)
                return new CollectionMember(name, storage, policy, declared, kind, element, scalar: scalar.Keyword);
            if (IsReservedElement(item))
                return new CollectionMember(name, storage, policy, declared, kind, element,
                    scalar: item.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), ownedScalar: ScalarFormatter(item));
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

        /// <summary>Describes a collection that can be reached through a static element owner.</summary>
        /// <param name="type">The closed element type of the enclosing collection.</param>
        /// <returns>The child owner requirements, or null if the family or element is not yet supported.</returns>
        /// <param name="depth">The current nesting depth.</param>
        /// <param name="issues">Receives unsupported contract details.</param>
        /// <param name="path">The path to the input, output, or active dependency.</param>
        /// <param name="memberName">The name of the member.</param>
        private MemberModel? TryForwardedCollection(ITypeSymbol type, int depth, List<string> issues, Path path, string memberName)
        {
            if (type is IArrayTypeSymbol array && (array.IsSZArray || array.Rank > 1))
            {
                var modeled = (CollectionMember)ArrayMember(string.Empty, MemberStorage.Field, WirePolicy.Default, array, depth, issues, path);
                return modeled.Scalar != null || modeled.ForwardedElement != null ? modeled : null;
            }

            if (type is not INamedTypeSymbol named || named.IsUnboundGenericType)
                return null;
            if (named.Arity == 2 && DictionaryKind(named) == Model.DictionaryKind.Dictionary)
            {
                var modeled = (DictionaryMember)DictionaryMember(string.Empty, MemberStorage.Field, WirePolicy.Default, named,
                    Model.DictionaryKind.Dictionary, depth, issues, path);
                return modeled.Nested == null && (modeled.KeyText || modeled.KeyType != null) && (modeled.ValueText || modeled.ValueType != null)
                    ? modeled : null;
            }
            if (named.Arity != 1)
                return null;

            var definition = named.OriginalDefinition;
            var formatter = FormatterRegistry.FindSequence(definition.ContainingNamespace.ToDisplayString() + "." + definition.Name);
            if (formatter == null)
                return null;
            CollectionKind kind = formatter.Kind;

            ITypeSymbol item = named.TypeArguments[0];
            if (kind == CollectionKind.List && NullableSequenceElement(item) is { } nullable)
                return new CollectionMember(string.Empty, MemberStorage.Field, WirePolicy.Default, DeclaredType(named), kind,
                    Reference(item), scalar: nullable, raw: true);
            var element = Scalar(item);
            bool text = formatter.Text != null && item.SpecialType == SpecialType.System_String;
            var child = element == null && !text && formatter.Forwarded != null ?
                TryForwardedCollection(item, depth, issues, path, memberName) : null;
            TypeModel? payload = null;
            INamedTypeSymbol? packed = element == null && !text && child == null && formatter.Payload != null ? LocalPackableClass(item) : null;
            if (packed != null)
                payload = ElementModel(packed, depth, issues, path, memberName);
            if (element == null && child == null && !text && payload == null)
                return null;

            return new CollectionMember(string.Empty, MemberStorage.Field, WirePolicy.Default, DeclaredType(named), kind,
                Reference(item), scalar: text ? "string" : payload != null ? packed!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : element?.Keyword,
                elementText: text, forwardedElement: child, nested: payload);
        }

        /// <summary>Models a dictionary, sorted list or sparse dictionary member.</summary>
        /// <param name="name">The member name.</param>
        /// <param name="storage">How the member's storage is reached.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <param name="type">The declared keyed type.</param>
        /// <param name="kind">The declared family.</param>
        /// <returns>The keyed member.</returns>
        /// <param name="depth">The current nesting depth.</param>
        /// <param name="issues">Receives unsupported contract details.</param>
        /// <param name="path">The path to the input, output, or active dependency.</param>
        private MemberModel DictionaryMember(string name, MemberStorage storage, WirePolicy policy, INamedTypeSymbol type, DictionaryKind kind,
            int depth, List<string> issues, Path path)
        {
            string declared = DeclaredType(type);
            TypeRef key = Reference(type.TypeArguments[0]), value = Reference(type.TypeArguments[1]);
            if (kind == Model.DictionaryKind.Dictionary)
            {
                bool textKey = type.TypeArguments[0].SpecialType == SpecialType.System_String;
                var rawKey = textKey ? null : RawEntry(type.TypeArguments[0]);
                if ((textKey || rawKey is { Owned: not "" }) &&
                    TryForwardedCollection(type.TypeArguments[1], depth, issues, path, name) is { } child)
                {
                    string childType = child is CollectionMember sequence ? sequence.DeclaredType : ((DictionaryMember)child).DeclaredType;
                    return new DictionaryMember(name, storage, policy, declared, kind, key, value, keyText: textKey,
                        keyType: rawKey?.Spelling, keyOwned: rawKey?.Owned, valueType: childType, forwardedValue: child);
                }
            }
            // A packable class value is expanded in place by the dictionary owner, whatever the raw or string key.
            if (kind != Model.DictionaryKind.SortedList && LocalPackableClass(type.TypeArguments[1]) is { } packable)
            {
                bool packedKeyText = type.TypeArguments[0].SpecialType == SpecialType.System_String;
                var packedKey = packedKeyText ? null : Scalar(type.TypeArguments[0], allowBoolean: false);
                if ((packedKeyText || packedKey != null) && ElementModel(packable, depth, issues, path, name) is { } nested)
                    return new DictionaryMember(name, storage, policy, declared, kind, key, value, keyText: packedKeyText,
                        keyType: packedKey?.Keyword, valueType: packable.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        nested: nested);
            }
            // A sorted list of a raw key and a raw or string value writes both stored arrays as paired entries.
            if (kind == Model.DictionaryKind.SortedList)
            {
                bool sortedKeyText = type.TypeArguments[0].SpecialType == SpecialType.System_String;
                var sortedKey = sortedKeyText ? null : Scalar(type.TypeArguments[0], allowBoolean: false);
                bool sortedValueText = type.TypeArguments[1].SpecialType == SpecialType.System_String;
                var sortedValue = sortedValueText ? null : Scalar(type.TypeArguments[1], allowBoolean: false);
                if ((sortedKeyText || sortedKey != null) && (sortedValueText || sortedValue != null))
                    return new DictionaryMember(name, storage, policy, declared, kind, key, value, keyText: sortedKeyText,
                        valueText: sortedValueText, keyType: sortedKey?.Keyword,
                        valueType: sortedValueText ? "string" : sortedValue.GetValueOrDefault().Keyword);
                return new DictionaryMember(name, storage, policy, declared, kind, key, value);
            }
            if (kind != Model.DictionaryKind.Dictionary)
            {
                // The paired owners of the other families store each entry as a raw scalar or a string.
                bool otherKeyText = type.TypeArguments[0].SpecialType == SpecialType.System_String;
                bool otherValueText = type.TypeArguments[1].SpecialType == SpecialType.System_String;
                var otherKey = otherKeyText ? null : RawEntry(type.TypeArguments[0]);
                var otherValue = otherValueText ? null : RawEntry(type.TypeArguments[1]);
                if ((otherKeyText || otherKey != null) && (otherValueText || otherValue != null))
                    return new DictionaryMember(name, storage, policy, declared, kind, key, value, keyText: otherKeyText,
                        valueText: otherValueText, keyType: otherKey?.Spelling, valueType: otherValue?.Spelling,
                        keyOwned: otherKey?.Owned, valueOwned: otherValue?.Owned);
                return new DictionaryMember(name, storage, policy, declared, kind, key, value);
            }
            // A dictionary writes fused entries when a string member bounds its record, or paired records when both key and
            // value are raw scalars.
            int reserved = ReservedKind(type);
            bool keyText = type.TypeArguments[0].SpecialType == SpecialType.System_String;
            bool valueText = type.TypeArguments[1].SpecialType == SpecialType.System_String;
            var keyRaw = keyText ? null : RawEntry(type.TypeArguments[0]);
            var valueRaw = valueText ? null : RawEntry(type.TypeArguments[1]);
            if (reserved is 5 or 6 && (keyText || keyRaw != null) && (valueText || valueRaw != null))
                return new DictionaryMember(name, storage, policy, declared, kind, key, value, pair: reserved == 5, keyText, valueText,
                    keyType: keyRaw?.Spelling, valueType: valueRaw?.Spelling, keyOwned: keyRaw?.Owned, valueOwned: valueRaw?.Owned);
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
                Owned(named) && (IsPackable(named) || Marked(named)) ? named : null;

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
            // As in the original formatter rule, only the ordinary object mode stores raw: version-tolerant, circle-reference,
            // custom and formatter-less declarations frame their values themselves.
            if (GenerationMode(named) != "Object")
                return false;
            // A closed generic packable struct whose definition is not unmanaged keeps its definition's object contract, which
            // can close over references; its closed value is not raw storage.
            if (named.IsGenericType && !named.OriginalDefinition.IsUnmanagedType && IsPackable(named.OriginalDefinition))
                return false;
            if (HasAttribute(named, UnionAttribute) || HasAttribute(named, "System.Runtime.CompilerServices.UnionAttribute"))
                return false;
            return TryPortableLayout(named) == null && !IsUnsupportedPackedStorage(named);
        }

        /// <summary>Classifies an element the raw sequence owners store as native storage: a scalar keyword, an enum or a framework
        /// built-in.</summary>
        /// <param name="element">The element type.</param>
        /// <returns>The element spelling and its shared scalar owner identity, or null for every other element.</returns>
        private (string Spelling, string? Owned)? RawElement(ITypeSymbol element)
        {
            if (Scalar(element) is { } scalar)
                return (scalar.Keyword, null);
            if (element.NullableAnnotation == NullableAnnotation.Annotated || element.IsRefLikeType || !element.IsUnmanagedType)
                return null;
            // An enum or framework built-in has a shared scalar owner; a packable struct that is plain unmanaged storage has the owner
            // of its own payload, which the empty identity stands for.
            if (ScalarFormatter(element) is { } owned)
                return (element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), owned);
            return IsReservedElement(element) && element is INamedTypeSymbol { TypeKind: TypeKind.Struct } ?
                (element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), string.Empty) : null;
        }

        /// <summary>Classifies a dictionary key or value stored as native storage: a non-Boolean scalar keyword, an enum or a framework
        /// built-in.</summary>
        /// <param name="entry">The key or value type.</param>
        /// <returns>The spelling and shared scalar owner identity, or null for every other type.</returns>
        private (string Spelling, string? Owned)? RawEntry(ITypeSymbol entry)
            => entry.SpecialType == SpecialType.System_Boolean ? null : RawElement(entry);

        /// <summary>Whether a raw element is an enum or a framework built-in, whose shared scalar owner the element JSON calls.</summary>
        /// <param name="element">The raw element type.</param>
        /// <returns>The scalar owner identity, or null when no shared scalar owner serves the element.</returns>
        private static string? ScalarFormatter(ITypeSymbol element)
        {
            if (element is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying } enumType && Scalar(underlying) is { } keyword)
                return enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "=" + keyword.Keyword;
            return Builtin(element);
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
        private MemberModel? Payload(MemberSite member, INamedTypeSymbol type, MemberStorage storage, WirePolicy policy, int depth,
            List<string> issues, Path path)
        {
            string name = member.Name;
            bool marked = Marked(type);
            if (!IsPackable(type) && !IsImplicitInlineClass(type) && !marked)
            {
                // A type that mentions the definition's own parameters is written and read by the generic serializer.
                if (member.Containing != null && MentionsOwnParameter(type, member.Containing))
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
                return new PayloadMember(name, storage, policy, Reference(type),
                    TracksReferences(type) ? PayloadShape.ReferenceTracked : PayloadShape.MemberEntry, nested: null);
            // A referenced type keeps its own assembly's operations unless this assembly marks it as an external root.
            if (!marked && !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);

            if (path.Active.Contains(type))
            {
                // The framing of this member depends on the expansion path, so neither this payload nor its ancestors may be
                // published to the session cache.
                path.SawCycle = true;
                if (TracksReferences(type))
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
            // A struct whose own root copies its storage as one value is raw fixed storage of the containing record, as in the
            // original pipeline: fixed cursors, field-group copies and Union cursor kernels include it with Unsafe.SizeOf.
            if (nested.Model is StructModel { Storage: StructStorage.RawUnmanaged, StoragePlan: null, Versioned: false, Mirror: null,
                    Callbacks: false, Rent: false } &&
                type.NullableAnnotation != NullableAnnotation.Annotated)
                return new ScalarMember(name, storage, policy, type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), 0,
                    raw: true, builtin: false, isVolatile: member.Volatile);
            return new PayloadMember(name, storage, policy, Reference(type), ShapeOf(nested.Model), nested.Model);
        }

        /// <summary>Reads the declared Union cases in declaration order.</summary>
        /// <param name="symbol">The Union root.</param>
        /// <param name="issues">Receives invalid case declarations.</param>
        /// <param name="wideCase">Whether any case of the definition requires the wide tag format.</param>
        /// <returns>The declared tag and case type of each attribute.</returns>
        private List<(int Tag, INamedTypeSymbol Type)> UnionMembers(INamedTypeSymbol symbol, List<string> issues, out bool wideCase)
        {
            var cases = new List<(int Tag, INamedTypeSymbol Type)>();
            var tags = new HashSet<int>();
            bool closed = symbol.IsGenericType && !symbol.IsDefinition && !ContainsTypeParameter(symbol);
            ITypeSymbol[]? arguments = null;
            wideCase = false;
            // A closed checked read shares the definition's tags and framing. Only its case bodies are specialized.
            foreach (UnionCaseDeclaration declaration in UnionCaseDeclarations(closed ? symbol.OriginalDefinition : symbol))
            {
                wideCase |= declaration.Tag >= 250;
                INamedTypeSymbol type = declaration.Type;
                if (closed)
                {
                    if (type.IsDefinition && GenericCaseArguments(symbol.OriginalDefinition, type) != null)
                        type = type.Construct(arguments ??= symbol.TypeArguments.ToArray());
                    if (!SymbolEqualityComparer.Default.Equals(UnionRootMatch(type, symbol.OriginalDefinition), symbol))
                        continue;
                }
                if (!tags.Add(declaration.Tag))
                {
                    issues.Add("the Union root declares tag " + declaration.Tag.ToString(CultureInfo.InvariantCulture) + " more than once");
                    continue;
                }
                cases.Add((declaration.Tag, type));
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
            bool referenced = !SymbolEqualityComparer.Default.Equals(root.ContainingAssembly, _compilation.Assembly);
            var names = UnionCaseDeclarations(root.OriginalDefinition).GroupBy(item => item.Tag).ToDictionary(group => group.Key, group => group.First().Type);
            foreach (var (tag, caseType) in declared)
            {
                // A struct case implements an interface root; its slots are explicit interface implementations and its read
                // boxes the value its own Union entry reads.
                bool structCase = caseType.TypeKind == TypeKind.Struct && !caseType.IsGenericType && root.TypeKind == TypeKind.Interface;
                // A generic case is either closed over concrete arguments or open over exactly the root's own parameters; it reads
                // and writes through its definition's generic entries, as the original pipeline does.
                string? genericArguments = GenericCaseArguments(root, caseType);
                if (genericArguments != null)
                {
                    ModelResult? definitionModel = referenced ? null : Analyze(caseType.OriginalDefinition, 0, path);
                    if (definitionModel != null && (definitionModel.Model is not RecordModel || !definitionModel.IsComplete))
                    {
                        issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " generic case '" + caseType.ToDisplayString() +
                            "' is not modeled: " + (definitionModel.Reason ?? "not a record"));
                        continue;
                    }
                    string definition = caseType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    // A closed case dispatches to the closed owner's own Union entries, as the original called the closed overloads;
                    // only an open case writes and reads through its definition's generic entries.
                    FormatterModel? closed = !ContainsTypeParameter(caseType) && Formatter(caseType, depth, path) is
                        { Definition: FormatterDefinitions.PayloadReference, Payload: not null } closedOwner ? closedOwner : null;
                    if (referenced && closed?.Payload is not RecordModel)
                    {
                        issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " has no imported closed case owner");
                        continue;
                    }
                    readGeneric = true;
                    fixedCases.Remove(caseType);
                    string caseReadable = closed != null
                        ? ReadableTypeName.Of(GeneratedNames.PayloadKey(caseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                        : ReadableTypeName.Of(GeneratedNames.PayloadKey(definition));
                    try
                    {
                        cases.Add(new UnionCaseModel(tag, UnionNameHash(names[tag]), Identity(caseType), UnionCaseRead.OwnEntry, ownEntry: true, readGeneric: true,
                            sealedCase: root.TypeKind == TypeKind.Class && !HasDerivedCase(symbols, caseType.OriginalDefinition), fixedCursor: false,
                            definitionModel?.Model ?? closed?.Payload, caseReadable, Chain(caseType.OriginalDefinition),
                            NamespaceOf(caseType), closed != null ? string.Empty : genericArguments, definition, closed));
                    }
                    catch (NotSupportedException error)
                    {
                        issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + ": " + error.Message);
                    }
                    continue;
                }
                if (!structCase && (caseType.TypeKind != TypeKind.Class || caseType.IsAbstract || caseType.IsGenericType || caseType.IsUnboundGenericType))
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " names the case '" +
                        caseType.ToDisplayString() + "', which is not an ordinary class record");
                    continue;
                }
                string? interfaceContract = root.TypeKind == TypeKind.Interface && root.IsGenericType && root.IsDefinition
                    ? UnionRootMatch(caseType, root.OriginalDefinition)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : null;
                bool ownEntry = interfaceContract != null || ReadsOwnUnionEntry(caseType);
                ModelResult analyzed = Analyze(caseType, 0, path);
                TypeModel? caseModel = analyzed.Model;
                bool caseReadGeneric = caseModel == null || NeedsReadProfile(caseModel);
                // Pooled cases own their read entry because that entry also owns the rental's storage mirror.
                bool expandable = caseModel is RecordModel { Versioned: false, Policy.TrackReferences: false, Rent: null, CollectionBase: null } && analyzed.IsComplete &&
                    caseModel.Members.Length != 0 && !caseModel.Members.Any(member => member.Mirrored) &&
                    caseModel.Members.All(member => member is ScalarMember) && !SelectedConstructor(caseType);
                // A complete local record whose fixed body this pipeline does not expand inline reads through its own Union
                // entry. The case bytes are the same; only the dispatch arm calls the published entry instead of the inline body.
                if (structCase && caseModel is StructModel && analyzed.IsComplete)
                {
                    // A struct case reads through its payload owner, which takes the operation's profile.
                    ownEntry = true;
                    caseReadGeneric = true;
                }
                if (!ownEntry && !expandable && caseModel is RecordModel && analyzed.IsComplete &&
                    SymbolEqualityComparer.Default.Equals(caseType.ContainingAssembly, _compilation.Assembly) &&
                    !ContainsTypeParameter(caseType) && GenerationMode(caseType) != "Custom")
                    ownEntry = true;
                if (!ownEntry && !expandable)
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + " case read: '" +
                        caseType.ToDisplayString() + "' is not a fixed record this pipeline expands");
                    caseReadGeneric = true;
                }
                readGeneric |= caseReadGeneric;
                // The root writes through the returning cursor kernel only when the case record publishes SerializeUnionAt.
                if (fixedCases.Contains(caseType) && caseModel is not RecordModel { FixedUnionCase: true })
                    fixedCases.Remove(caseType);
                bool sealedCase = root.TypeKind == TypeKind.Class && !HasDerivedCase(symbols, caseType.OriginalDefinition);
                try
                {
                    string globalName = caseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    cases.Add(new UnionCaseModel(tag, UnionNameHash(names[tag]), Identity(caseType),
                        ownEntry ? UnionCaseRead.OwnEntry : UnionCaseRead.ExpandedFixed, ownEntry, caseReadGeneric, sealedCase,
                        fixedCases.Contains(caseType), caseModel, ReadableTypeName.Of(GeneratedNames.PayloadKey(globalName)),
                        Chain(caseType), NamespaceOf(caseType), interfaceContract: interfaceContract));
                }
                catch (NotSupportedException error)
                {
                    issues.Add("Union tag " + tag.ToString(CultureInfo.InvariantCulture) + ": " + error.Message);
                }
            }
            return cases.ToArray();
        }

        /// <summary>Spells the type arguments a generic case passes to its definition's entries.</summary>
        /// <param name="root">The Union root definition.</param>
        /// <param name="caseType">The declared or discovered case.</param>
        /// <returns>The closed arguments, the root's own parameter names for an open case, or null for any other case.</returns>
        private static string? GenericCaseArguments(INamedTypeSymbol root, INamedTypeSymbol caseType)
        {
            if (!caseType.IsGenericType || caseType.TypeKind != TypeKind.Class || caseType.IsAbstract || caseType.IsUnboundGenericType ||
                caseType.ContainingType is { IsGenericType: true })
                return null;
            if (!caseType.IsDefinition)
                return caseType.TypeArguments.Any(ContainsTypeParameter) ? null :
                    string.Join(", ", caseType.TypeArguments.Select(argument => argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            // An open case must implement the root over exactly its own parameters, which carry the root's parameter names.
            if (!root.IsGenericType || UnionRootMatch(caseType, root.OriginalDefinition) is not { } match ||
                match.TypeArguments.Length != caseType.TypeParameters.Length)
                return null;
            for (int index = 0; index < caseType.TypeParameters.Length; index++)
                if (!SymbolEqualityComparer.Default.Equals(match.TypeArguments[index], caseType.TypeParameters[index]) ||
                    caseType.TypeParameters[index].Name != root.TypeParameters[index].Name)
                    return null;
            return string.Join(", ", root.TypeParameters.Select(parameter => parameter.Name));
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
            => type is { IsReferenceType: true, IsAbstract: false, TypeKind: TypeKind.Class } && Owned(type) &&
               !ContainsTypeParameter(type) && GenerationMode(type) != "Custom" && (TracksReferences(type) || HasVariablePayload(type));

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
            RecordModel record => record.Versioned || record.CollectionBase != null || ProfilePayload(record) ||
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
            CollectionMember { Scalar: "string" } or CollectionMember { ForwardedElement: not null } => true,
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
            // A generic declaration repeats its own type parameters; a containing type's parameters belong to its own line.
            if (type.Arity != 0)
                name += "<" + string.Join(", ", type.OriginalDefinition.TypeParameters.Select(parameter => parameter.Name)) + ">";
            return accessibility + "partial " + (type.TypeKind == TypeKind.Interface ? "interface " : type.TypeKind == TypeKind.Struct ? "struct " : "class ") + name;
        }

        /// <summary>Spells a type's namespace, or null in the global namespace.</summary>
        /// <param name="type">The declared type.</param>
        /// <returns>The namespace, or null.</returns>
        private static string? NamespaceOf(INamedTypeSymbol type)
            => type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace
                ? null : type.ContainingNamespace.ToDisplayString();

        /// <summary>Selects the compiled dispatch tier of a local class Union root.</summary>
        /// <param name="root">The Union root.</param>
        /// <returns>The selected tier.</returns>
        private UnionDispatchTier UnionTier(INamedTypeSymbol root)
        {
            return UsesVirtualUnionDispatch(root) ? UnionDispatchTier.Virtual : UnionDispatchTier.Exact;
        }

        /// <summary>Finds declarations that can change how a parameterless call to <c>GetType</c> binds.</summary>
        /// <param name="root">The exact static type of the generated writer's receiver.</param>
        /// <returns>Whether the writer needs an object-typed receiver to call <c>object.GetType</c>.</returns>
        private bool HidesObjectGetType(INamedTypeSymbol root)
        {
            for (INamedTypeSymbol? type = root; type != null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
                if (HasCandidate(type))
                    return true;
            return root.TypeKind == TypeKind.Interface && root.AllInterfaces.Any(HasCandidate);

            bool HasCandidate(INamedTypeSymbol type) => type.GetMembers(nameof(object.GetType)).Any(member =>
                _compilation.IsSymbolAccessibleWithin(member, _compilation.Assembly) &&
                (member is not IMethodSymbol method || method is { MethodKind: MethodKind.Ordinary, Arity: 0 } &&
                    method.Parameters.All(parameter => parameter.IsOptional || parameter.IsParams)));
        }

        /// <summary>Checks whether a Union root can select the fixed cursor protocol for its eligible cases.</summary>
        /// <param name="root">The Union root.</param>
        /// <returns>Whether the root is a non-generic abstract class or a non-generic interface with exact dispatch.</returns>
        private bool IsFixedCursorUnionRoot(INamedTypeSymbol root) =>
            !root.IsGenericType &&
            (root is { TypeKind: TypeKind.Class, IsAbstract: true } ||
                root.TypeKind == TypeKind.Interface && UnionTier(root) == UnionDispatchTier.Exact);

        /// <summary>Lists the cases whose complete fixed payload uses the returning cursor kernel.</summary>
        /// <param name="root">The Union root.</param>
        /// <param name="declared">The declared cases.</param>
        /// <param name="issues">Receives the invalid declarations of the root.</param>
        /// <returns>The eligible cases, or empty when the root keeps the original protocol.</returns>
        private HashSet<ITypeSymbol> FixedUnionCases(INamedTypeSymbol root, List<(int Tag, INamedTypeSymbol Type)> declared, List<string> issues)
        {
            var cases = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            if (issues.Count != 0 || !IsFixedCursorUnionRoot(root) ||
                !SymbolEqualityComparer.Default.Equals(root.ContainingAssembly, _compilation.Assembly) || GenerationMode(root) != "Object")
                return cases;
            foreach (var item in declared.OrderBy(member => member.Tag))
                if (item.Type is { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } &&
                    CanUseCursorPayload(item.Type))
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
            if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly) ||
                GenerationMode(type) != "Object")
                return false;
            INamedTypeSymbol? root = FixedUnionRoot(type);
            if (root == null)
                return false;
            var issues = new List<string>();
            var declared = UnionMembers(root, issues, out _);
            if (issues.Count != 0 || !IsFixedCursorUnionRoot(root) ||
                !SymbolEqualityComparer.Default.Equals(root.ContainingAssembly, _compilation.Assembly) || GenerationMode(root) != "Object")
                return false;
            return declared.Any(item => SymbolEqualityComparer.Default.Equals(item.Type, type)) && CanUseCursorPayload(type);
        }

        /// <summary>Finds the nearest declaring base Union or the first eligible declaring interface Union.</summary>
        /// <param name="type">The candidate case.</param>
        /// <returns>The declaring root, or null when the type is not a declared case.</returns>
        private INamedTypeSymbol? FixedUnionRoot(INamedTypeSymbol type)
        {
            for (var parent = type.BaseType; parent != null && parent.SpecialType != SpecialType.System_Object;
                parent = parent.BaseType)
            {
                if (!parent.IsAbstract || parent.IsGenericType || !IsPackable(parent))
                    continue;
                // Explicit and discovered cases are both declared cases; the root's own analysis uses the same list.
                if (UnionCaseDeclarations(parent).Any(declaration => SymbolEqualityComparer.Default.Equals(declaration.Type, type)))
                    return parent;
            }
            return type.AllInterfaces.FirstOrDefault(parent => IsPackable(parent) &&
                IsFixedCursorUnionRoot(parent) && SymbolEqualityComparer.Default.Equals(parent.ContainingAssembly, _compilation.Assembly) &&
                GenerationMode(parent) == "Object" && UnionCaseDeclarations(parent).Any(declaration =>
                    SymbolEqualityComparer.Default.Equals(declaration.Type, type)));
        }

        /// <summary>Whether a case payload is entirely public raw fields, as the returning cursor kernel requires.</summary>
        /// <param name="type">The concrete case.</param>
        /// <returns>Whether the case can use the shared fixed write body.</returns>
        private bool CanUseCursorPayload(INamedTypeSymbol type)
        {
            if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
                return false;
            if (TracksReferences(type))
                return false;
            for (var parent = type.BaseType; parent != null && parent.SpecialType != SpecialType.System_Object; parent = parent.BaseType)
                if (parent.IsGenericType)
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
        /// <returns>The effective policy.</returns>
        private WirePolicy TypePolicy(INamedTypeSymbol symbol)
        {
            WirePolicy policy = _assemblyPolicy;
            var scopes = new Stack<INamedTypeSymbol>();
            for (var type = symbol; type != null; type = type.ContainingType)
                scopes.Push(type);
            while (scopes.Count != 0)
                policy = Apply(policy, scopes.Pop());
            if (TracksReferences(symbol))
                policy = new WirePolicy(policy.Input, policy.Strings, policy.Bound, trackReferences: true, declared: policy.Declared,
                    limits: policy.Limits);
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
        internal static WirePolicy Apply(WirePolicy policy, ISymbol symbol)
        {
            char format = policy.Strings.Format;
            byte precision = policy.Strings.Precision;
            int depth = policy.Strings.Depth;
            bool strict = policy.Strings.Strict;
            StringEncoding encoding = policy.Strings.Encoding;
            StringRecording recording = policy.Strings.Recording;
            InputPolicy input = policy.Input;
            InputLimits limits = policy.Limits;
            int? bound = policy.Bound;
            bool changed = false;
            bool declared = policy.Declared;

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
                if (attribute.NamedArguments.Length == 0 || attribute.NamedArguments.Any(argument => argument.Key is
                    "StringEncoding" or "StringRecording" or "MaxJsonDepth" or "ValidateJsonSyntax" or "StandardFormat"))
                {
                    declared = true;
                    changed = true;
                }
                foreach (var argument in attribute.NamedArguments)
                {
                    switch (argument.Key)
                    {
                        case "UntrustedData" when argument.Value.Value is bool untrusted:
                            input = untrusted ? InputPolicy.Untrusted : InputPolicy.Trusted;
                            limits = limits.With(explicitInput: untrusted);
                            changed = true;
                            break;
                        // Only the limits enforced by generated checks are frozen here; each one declares hostile input.
                        case "MaxInputBytes" when argument.Value.Value is int maxInput:
                            limits = limits.With(input: maxInput, explicitInput: true);
                            changed = true;
                            break;
                        case "MaxCollectionLength" when argument.Value.Value is int maxCollection:
                            limits = limits.With(collection: maxCollection, explicitInput: true);
                            changed = true;
                            break;
                        case "MaxStringLength" when argument.Value.Value is int maxString:
                            limits = limits.With(@string: maxString, explicitInput: true);
                            changed = true;
                            break;
                        case "MaxDepth" when argument.Value.Value is int maxDepth:
                            limits = limits.With(depth: maxDepth, explicitInput: true);
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
                policy.TrackReferences, declared, limits);
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
            PortablePlan? plan;
            if (LayoutStrategy(symbol) == 1)
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
        /// <param name="strategy">The consumer-resolved strategy, when the type is used through a collection formatter.</param>
        /// <returns>The tagged plan, or null when the struct is not analyzable, has a single layout or owns no formatter.</returns>
        private PortablePlan? TryTaggedPlan(INamedTypeSymbol named, int? strategy = null)
        {
            if (named.TypeKind != TypeKind.Struct || named.SpecialType != SpecialType.None || !named.IsUnmanagedType)
                return null;
            if ((strategy ?? LayoutStrategy(named)) != 0 || GenerationMode(named) != "Object")
                return null;
            if (PortableScalarSize(named) != 0 || ContainsTypeParameter(named) || Opaque(named) != null)
                return null;
            var plan = AnalyzePortable(named, strict: true);
            if (plan == null || plan.Fields == null || plan.WireSize <= 0 || HasVolatile(plan))
                return null;
            plan.Models = BuildModels(plan);
            if (plan.Models.Length < 2)
                return null;
            plan.Tagged = true;
            return plan;
        }

        /// <summary>Resolves unmanaged storage from the type, its physical fields and the applicable assembly default.</summary>
        /// <param name="symbol">The unmanaged struct.</param>
        /// <returns>Zero for tagged, one for packed or two for native storage.</returns>
        private int LayoutStrategy(INamedTypeSymbol symbol)
        {
            int? declaration = ReadStructLayoutOption(symbol.GetAttributes());
            int strategy = declaration ?? ReadStructLayoutOption(LayoutAssembly(symbol, _compilation.Assembly).GetAttributes()) ?? 0;
            if (declaration == 2 || strategy == 1 || !CanInheritPackedStorage(symbol))
                return strategy;
            return HasPackedField(symbol) ? 1 : strategy;
        }

        /// <summary>Selects the assembly whose layout default a declaration inherits.</summary>
        /// <param name="symbol">The storage declaration.</param>
        /// <param name="consumer">The assembly generating the enclosing contract.</param>
        /// <returns>The declaring assembly for a packable type, or the consumer for unannotated storage.</returns>
        private static IAssemblySymbol LayoutAssembly(INamedTypeSymbol symbol, IAssemblySymbol consumer) =>
            IsPackable(symbol) ? symbol.ContainingAssembly : consumer;

        /// <summary>Identifies native aggregate declarations which can adopt a field's Packed storage.</summary>
        /// <param name="symbol">The prospective enclosing type.</param>
        /// <returns>Whether the type has an ordinary unmanaged layout with fields to inspect.</returns>
        private static bool CanInheritPackedStorage(INamedTypeSymbol symbol) =>
            symbol is { TypeKind: TypeKind.Struct, IsUnmanagedType: true, SpecialType: SpecialType.None or SpecialType.System_Nullable_T } &&
            Opaque(symbol) == null && !FrameworkScalarBody.Contains(FormatterName(symbol)) && GenerationMode(symbol) == "Object";

        /// <summary>Recognizes aggregates whose selected Packed layout cannot be emitted.</summary>
        /// <param name="symbol">The unmanaged aggregate.</param>
        /// <returns>Whether emitting its native layout would silently bypass Packed storage.</returns>
        private bool IsUnsupportedPackedStorage(INamedTypeSymbol symbol) =>
            symbol.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T && CanInheritPackedStorage(symbol) && LayoutStrategy(symbol) == 1 &&
            !SupportedPortableStorage(symbol, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

        /// <summary>Shares the recursive Packed field check across entry, declaration and formatter analysis.</summary>
        /// <param name="symbol">The enclosing storage type.</param>
        /// <returns>Whether the enclosing storage contains a Packed field.</returns>
        private bool HasPackedField(INamedTypeSymbol symbol)
        {
            lock (_gate)
                if (_packedFields.TryGetValue(symbol, out bool cached))
                    return cached;
            IAssemblySymbol assembly = LayoutAssembly(symbol, _compilation.Assembly);
            bool result = HasPackedField(symbol, assembly, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
            lock (_gate)
                _packedFields[symbol] = result;
            return result;
        }

        /// <summary>Finds a Packed declaration reachable through physical value fields without crossing an explicit Native boundary.</summary>
        /// <param name="symbol">The enclosing storage type.</param>
        /// <param name="assembly">The enclosing contract's assembly.</param>
        /// <param name="visited">The aggregate types already visited.</param>
        /// <returns>Whether the enclosing native copy would override a nested Packed declaration.</returns>
        private static bool HasPackedField(INamedTypeSymbol symbol, IAssemblySymbol assembly, HashSet<ITypeSymbol> visited)
        {
            if (!visited.Add(symbol))
                return false;
            // Inspect fields even when a sibling makes portable transfer unsupported. Framework Nullable and pair metadata
            // need their closed arguments because the reference assembly can omit or substitute their physical fields.
            IEnumerable<ITypeSymbol> fields = symbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || IsKeyValuePair(symbol)
                ? symbol.TypeArguments : StorageFields(symbol).Select(field => field.Type);
            try
            {
                foreach (ITypeSymbol field in fields)
                {
                    if (field is not INamedTypeSymbol child || !CanInheritPackedStorage(child))
                        continue;
                    int? declaration = ReadStructLayoutOption(child.GetAttributes());
                    if (declaration == 2)
                        continue;
                    IAssemblySymbol childAssembly = LayoutAssembly(child, assembly);
                    if ((declaration ?? ReadStructLayoutOption(childAssembly.GetAttributes()) ?? 0) == 1 || HasPackedField(child, childAssembly, visited))
                        return true;
                }
                return false;
            }
            finally
            {
                visited.Remove(symbol);
            }
        }

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

        /// <summary>Builds type name.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>The type name.</returns>
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

        /// <summary>Builds named type name.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>The named type name.</returns>
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

        /// <summary>Builds type namespace.</summary>
        /// <param name="typeNamespace">The type namespace supplied to <c>BuildTypeNamespace</c>.</param>
        /// <returns>The type namespace.</returns>
        private static string BuildTypeNamespace(INamespaceSymbol typeNamespace)
            => typeNamespace == null || typeNamespace.IsGlobalNamespace
                ? "global::"
                : BuildTypeNamespace(typeNamespace.ContainingNamespace) + EscapeIdentifier(typeNamespace.Name) + ".";

        /// <summary>Checks for tuple.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>Whether the input satisfies the contains tuple check.</returns>
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

        /// <summary>Handles escape identifier for <c>Session</c>.</summary>
        /// <param name="name">The name to look up or emit.</param>
        /// <returns>The resulting text.</returns>
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
            if (IsKeyValuePair(named))
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

        /// <summary>Recognizes the framework's generic key and value storage pair.</summary>
        /// <param name="type">The candidate storage type.</param>
        /// <returns>Whether its physical fields are the two closed generic arguments.</returns>
        private static bool IsKeyValuePair(INamedTypeSymbol type) =>
            type.OriginalDefinition.MetadataName == "KeyValuePair`2" &&
            type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";

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
        private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol => true,
            // An array or pointer of an open element is open as well.
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol { IsGenericType: true } named => named.TypeArguments.Any(ContainsTypeParameter),
            _ => false,
        };

        /// <summary>Checks whether any aggregate in the tree uses the runtime's automatic field order.</summary>
        /// <param name="plan">The analyzed storage.</param>
        /// <returns>Whether an automatic layout appears.</returns>
        private static bool HasAutomatic(PortablePlan plan)
            => plan.Fields != null && (plan.Kind == 3 || plan.Children!.Any(HasAutomatic));

        /// <summary>Selects how a modeled platform places automatic-layout aggregates.</summary>
        private enum AutomaticLayout
        {
            DeclarationOrder,
            Legacy,
            CoreClr64
        }

        /// <summary>Places the storage under each layout model and keeps the distinct results.</summary>
        /// <param name="plan">The analyzed storage.</param>
        /// <returns>The distinct storage models in persisted tag order.</returns>
        private static PortableStorageModel[] BuildModels(PortablePlan plan)
        {
            var kinds = new List<(bool Is64, AutomaticLayout Automatic)>
            {
                (true, AutomaticLayout.DeclarationOrder), (false, AutomaticLayout.DeclarationOrder)
            };
            if (HasAutomatic(plan))
            {
                kinds.Add((true, AutomaticLayout.Legacy));
                kinds.Add((false, AutomaticLayout.Legacy));
                // Append new models so existing tags still name the same offsets and stride.
                kinds.Add((true, AutomaticLayout.CoreClr64));
            }
            var models = new List<PortableStorageModel>();
            foreach (var (is64, automatic) in kinds)
            {
                var placed = PlacePortable(plan, is64, automatic);
                if (!models.Any(model => model.Size == placed.Size && model.Offsets.AsSpan().SequenceEqual(placed.Offsets)))
                    models.Add(new PortableStorageModel { Size = placed.Size, Offsets = placed.Offsets });
            }
            return models.ToArray();
        }

        /// <summary>Positions every field of an aggregate under one layout model.</summary>
        /// <param name="plan">The analyzed storage.</param>
        /// <param name="is64">Whether eight-byte primitives align to eight bytes.</param>
        /// <param name="automatic">The rule for automatic-layout aggregates.</param>
        /// <returns>The element size, alignment, and the storage offset of every packed data byte.</returns>
        private static (int Size, int Alignment, int[] Offsets) PlacePortable(PortablePlan plan, bool is64, AutomaticLayout automatic)
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
                places[index] = PlacePortable(plan.Children[index], is64, automatic);
            int pack = plan.Pack == 0 ? 8 : plan.Pack;
            var order = Enumerable.Range(0, count);
            bool coreClrAuto = plan.Kind == 3 && automatic == AutomaticLayout.CoreClr64;
            if (coreClrAuto)
                order = order.OrderByDescending(index => CoreClrPrimitiveWidth(plan.Children[index].Type));
            else if (plan.Kind == 3 && automatic == AutomaticLayout.Legacy)
                order = order.OrderBy(index => plan.Children[index].Fields == null ? 0 : 1)
                    .ThenByDescending(index => plan.Children[index].Fields == null ? places[index].Size : 0);
            var offsets = new int[count];
            int end = 0, alignment = 1;
            foreach (int index in order)
            {
                int fieldAlignment = Math.Max(1, Math.Min(pack, places[index].Alignment));
                if (coreClrAuto)
                {
                    int primitiveWidth = CoreClrPrimitiveWidth(plan.Children[index].Type);
                    fieldAlignment = primitiveWidth == 0 ? places[index].Alignment : primitiveWidth;
                    if (primitiveWidth != 0)
                        alignment = Math.Max(alignment, 8);
                }
                alignment = Math.Max(alignment, fieldAlignment);
                int offset = plan.Kind == 2 && plan.Fields[index].Offset >= 0
                    ? plan.Fields[index].Offset
                    : (end + fieldAlignment - 1) / fieldAlignment * fieldAlignment;
                offsets[index] = offset;
                end = Math.Max(end, offset + places[index].Size);
            }
            // CoreCLR HandleAutoLayout places primitives before embedded value types. Small Auto values round
            // to a power of two; larger values use the maximum alignment, at least a pointer when they contain a primitive.
            if (coreClrAuto && end <= 8)
            {
                alignment = 1;
                while (alignment < end)
                    alignment *= 2;
            }
            int declaredSize = coreClrAuto ? 0 : plan.DeclaredSize;
            int size = (Math.Max(Math.Max(1, declaredSize), end) + alignment - 1) / alignment * alignment;
            var data = new int[plan.WireSize];
            int next = 0;
            for (int index = 0; index < count; index++)
                foreach (int byteOffset in places[index].Offsets)
                    data[next++] = offsets[index] + byteOffset;
            return (size, alignment, data);
        }

        /// <summary>Returns the CoreCLR primitive placement width, with embedded value types in the final group.</summary>
        /// <param name="type">A field in an automatic-layout aggregate.</param>
        /// <returns>A primitive width, or zero for an embedded value type.</returns>
        private static int CoreClrPrimitiveWidth(ITypeSymbol type)
            => type.SpecialType == SpecialType.System_Decimal ? 0 : PortableScalarSize(type);

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

        /// <summary>Models the constructor the legacy analysis selects for a record's reads.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="members">The serialized members in wire order.</param>
        /// <param name="issues">Receives the reason a required constructor is missing.</param>
        /// <returns>The selected constructor facts, or null when the parameterless constructor is used.</returns>
        /// <remarks>The legacy analysis selects the single public explicit constructor, or the one marked with
        /// <c>[LuminPackConstructor]</c>; a parameter matches the first serialized member whose name equals it, ignoring case.
        /// A mismatched parameter becomes a <c>default</c> argument.</remarks>
        private static ConstructorModel? SelectedConstructorModel(INamedTypeSymbol type, MemberModel[] members, List<string> issues)
        {
            IMethodSymbol? selected = SelectConstructor(type, out bool ambiguous);
            if (ambiguous)
            {
                issues.Add("constructor: selection is ambiguous");
                return null;
            }
            // The generated roots create a parameterless instance; a selected constructor only supplies the arguments when
            // its parameter list is non-empty.
            bool parameterless = type.InstanceConstructors.Any(constructor =>
                constructor.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public && constructor.Parameters.Length == 0);
            if (selected == null || selected.Parameters.Length == 0)
            {
                if (!parameterless)
                    issues.Add("constructor: no public parameterless constructor and no selected parameterized constructor");
                return null;
            }
            string?[] arguments = selected.Parameters.Select(parameter => MatchConstructorParameter(members, parameter.Name)).ToArray();
            string[] constructed = arguments.Where(name => name != null).Select(name => name!).ToArray();
            return new ConstructorModel(arguments, constructed, StorageOrder(type));
        }

        /// <summary>Creates an instance before its payload is available, using the selected constructor with typed defaults.</summary>
        /// <param name="type">The class declaration whose instance must exist before its members are read.</param>
        /// <param name="name">The generated spelling of the class.</param>
        /// <returns>The constructor and any required initializer assignments.</returns>
        private static string ReferenceConstruction(INamedTypeSymbol type, string name)
        {
            IMethodSymbol? constructor = SelectConstructor(type, out _);
            string arguments = constructor == null ? string.Empty : string.Join(", ", constructor.Parameters.Select(parameter =>
                "default(" + parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")!"));
            string expression = "new " + name + "(" + arguments + ")";
            if (constructor != null && HasAttribute(constructor, "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
                return expression;
            string[] required = Levels(type).SelectMany(level => level.GetMembers()).Where(IsRequiredMember)
                .Select(member => "@" + member.Name + " = default!").Distinct(StringComparer.Ordinal).ToArray();
            return required.Length == 0 ? expression : expression + " { " + string.Join(", ", required) + " }";
        }

        /// <summary>Recognizes required members without depending on a newer Roslyn symbol API.</summary>
        /// <param name="member">The candidate field or property.</param>
        /// <returns>Whether construction must set the member.</returns>
        private static bool IsRequiredMember(ISymbol member)
        {
            if (member is not (IFieldSymbol or IPropertySymbol))
                return false;
            if (HasAttribute(member, "System.Runtime.CompilerServices.RequiredMemberAttribute"))
                return true;
            foreach (SyntaxReference reference in member.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax();
                var declaration = syntax is VariableDeclaratorSyntax ? syntax.Parent?.Parent : syntax;
                if (declaration is PropertyDeclarationSyntax property && property.Modifiers.Any(token => token.ValueText == "required") ||
                    declaration is FieldDeclarationSyntax field && field.Modifiers.Any(token => token.ValueText == "required"))
                    return true;
            }
            return false;
        }

        /// <summary>Matches a constructor parameter to the first serialized member with equal name, ignoring case.</summary>
        /// <param name="members">The serialized members in wire order.</param>
        /// <param name="parameter">The constructor parameter name.</param>
        /// <returns>The member's exact name, or null when no member matches.</returns>
        private static string? MatchConstructorParameter(MemberModel[] members, string parameter)
        {
            foreach (MemberModel member in members)
                if (string.Equals(member.Name, parameter, StringComparison.OrdinalIgnoreCase))
                    return member.Name;
            return null;
        }

        /// <summary>Enumerates every instance storage member name in base-first storage order, for a constructed read's
        /// temporary declarations.</summary>
        /// <param name="type">The declared payload.</param>
        /// <returns>The storage names in declaration order.</returns>
        private static string[] StorageOrder(INamedTypeSymbol type)
        {
            var names = new List<string>();
            foreach (INamedTypeSymbol level in Levels(type))
                foreach (ISymbol member in OrderedStorageMembers(level))
                {
                    if (member.IsImplicitlyDeclared || member.IsStatic)
                        continue;
                    if (member is IPropertySymbol property && !IsAutoProperty(property))
                        continue;
                    names.Add(member.Name);
                }
            return names.ToArray();
        }

        /// <summary>Models the pool rent method of a record.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="globalName">The fully qualified payload spelling.</param>
        /// <param name="issues">The owning analysis issue collection.</param>
        /// <returns>The static rent call, or null when the payload declares no usable rent method.</returns>
        private string? PoolRent(INamedTypeSymbol type, string globalName, List<string> issues)
        {
            var methods = type.GetMembers().OfType<IMethodSymbol>()
                .Where(method => method.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == PoolRentAttribute))
                .ToList();
            if (methods.Count != 1)
                return null;
            IMethodSymbol method = methods[0];
            if (!method.IsStatic || method.Parameters.Length != 0 || method.IsGenericMethod ||
                !_compilation.IsSymbolAccessibleWithin(method, _compilation.Assembly) ||
                !SymbolEqualityComparer.Default.Equals(method.ReturnType, type))
                return null;
            return globalName + "." + Identifier(method.Name) + "()";
        }

        /// <summary>Models the serialization callbacks as the statements a record's write and read roots run.</summary>
        /// <param name="type">The declared payload.</param>
        /// <param name="globalName">The fully qualified payload spelling.</param>
        /// <returns>The callback statements in phase, receiver and inheritance order, or null when none apply.</returns>
        /// <remarks>An invalid callback declaration keeps the original pipeline's diagnostic and never runs; it does not
        /// change the record's rendering.</remarks>
        private CallbackModel? SerializationCallbacks(INamedTypeSymbol type, string globalName)
        {
            var writeBefore = new List<(bool Static, string Call)>();
            var writeAfter = new List<(bool Static, string Call)>();
            var readBefore = new List<(bool Static, string Call)>();
            var readAfter = new List<(bool Static, string Call)>();
            foreach (INamedTypeSymbol level in Levels(type))
            {
                bool declared = SymbolEqualityComparer.Default.Equals(type, level);
                string declaringName = declared ? globalName : level.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                foreach (IMethodSymbol method in level.GetMembers().OfType<IMethodSymbol>())
                {
                    var attributes = method.GetAttributes();
                    if (attributes.IsEmpty || !Accessible(method))
                        continue;
                    string receiver = method.IsStatic ? declaringName + "." : type.IsValueType ? "value." :
                        declared ? "value?." : "((" + declaringName + "?)value)?.";
                    var callback = (method.IsStatic, receiver + Identifier(method.Name) + "();");
                    foreach (AttributeData attribute in attributes)
                    {
                        string? name = attribute.AttributeClass?.Name;
                        List<(bool Static, string Call)>? phase = name switch
                        {
                            OnSerializingAttribute => writeBefore,
                            OnSerializedAttribute => writeAfter,
                            OnDeserializingAttribute => readBefore,
                            OnDeserializedAttribute => readAfter,
                            _ => null,
                        };
                        if (phase != null && !HasEarlierVirtualCallback(method, name!))
                            phase.Add(callback);
                    }
                }
            }
            if (writeBefore.Count == 0 && writeAfter.Count == 0 && readBefore.Count == 0 && readAfter.Count == 0)
                return null;
            return new CallbackModel(Ordered(writeBefore), Ordered(writeAfter), Ordered(readBefore), Ordered(readAfter));

            bool Accessible(IMethodSymbol method) => ValidCallback(type, method) &&
                _compilation.IsSymbolAccessibleWithin(method, _compilation.Assembly);

            bool HasEarlierVirtualCallback(IMethodSymbol method, string name)
            {
                // Calling an attributed base method already invokes the most-derived virtual override.
                for (IMethodSymbol? ancestor = method.OverriddenMethod; ancestor != null; ancestor = ancestor.OverriddenMethod)
                    if (Accessible(ancestor) && ancestor.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == name))
                        return true;
                return false;
            }

            static string[] Ordered(List<(bool Static, string Call)> phase) =>
                phase.OrderByDescending(callback => callback.Static).Select(callback => callback.Call).ToArray();
        }

        /// <summary>Whether a callback declaration can be spelled as a generated statement.</summary>
        /// <param name="type">The declaring payload.</param>
        /// <param name="method">The attributed method.</param>
        /// <returns>Whether the method is an accessible parameterless instance or static method.</returns>
        private static bool ValidCallback(INamedTypeSymbol type, IMethodSymbol method)
        {
            // An unmanaged struct's callback would change its storage copy, and the original pipeline reports the rest.
            if (type.TypeKind == TypeKind.Struct && type.IsUnmanagedType)
                return false;
            if (method.DeclaredAccessibility is Microsoft.CodeAnalysis.Accessibility.NotApplicable
                or Microsoft.CodeAnalysis.Accessibility.Private
                or Microsoft.CodeAnalysis.Accessibility.Protected
                or Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal)
                return false;
            return method.Parameters.Length == 0 && !method.IsGenericMethod;
        }

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
        /// <param name="includeBase">Whether this compatibility field list includes inherited storage.</param>
        private static void MirrorFields(INamedTypeSymbol symbol, out string[]? fields, out int[]? offsets, out string? layout, bool includeBase = true)
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
                if (includeBase && level.BaseType != null && level.BaseType.SpecialType != SpecialType.System_Object)
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
                        if (StorageSnapshots.GetValue(level, CaptureStorage).BackingFields.TryGetValue(property, out IFieldSymbol? backing))
                            offsetOwner = backing;
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
                    declarations.Add((member is IFieldSymbol { IsVolatile: true } ? "volatile " : string.Empty) + spelling +
                        (IsUnmanagedKeyword(spelling) || isValue ? string.Empty : "?") + " " + Identifier(member.Name));
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
        /// <remarks>The descriptor hashes retain the former contract rules. Open identities compute their type hash for
        /// each closed construction through the same contract helper the revision-1 reader uses.</remarks>
        private VersionSchemaModel? TryVersionSchema(INamedTypeSymbol type, TypeIdentity identity, MemberModel[] members,
            WirePolicy policy, bool derived, List<string> issues)
        {
            // Metadata follows wire order, while the Local mirror retains every physical storage slot.
            var declarations = Levels(type).SelectMany(OrderedStorageMembers).Where(member => TryStorage(member, out _))
                .ToDictionary(member => member.Name, StringComparer.Ordinal);
            var storage = members.Select(member => declarations[member.Name]).ToArray();
            var locals = Levels(type).SelectMany(VersionLocals).ToList();

            var keys = new HashSet<uint>();
            var nameKeys = new uint[members.Length][];
            var oldNames = new string[members.Length][];
            var fields = new (int Type, bool Compressed)[members.Length];
            var widths = new int[members.Length];
            var fixedNullable = new bool[members.Length];
            var memberTypes = new ITypeSymbol[members.Length];
            for (int index = 0; index < members.Length; index++)
            {
                ISymbol declared = storage[index];
                ITypeSymbol memberType = declared is IFieldSymbol field ? field.Type : ((IPropertySymbol)declared).Type;
                // A keyword-named member is escaped where the source writer emits it.
                if (!SyntaxFacts.IsValidIdentifier(declared.Name))
                {
                    issues.Add("version tolerant: member options '" + declared.Name + "'");
                    return null;
                }
                string?[] aliases = declared.GetAttributes()
                    .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOldNameAttribute")
                    .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
                    .ToArray();
                // The oldest name is the first key and remains the written identity.
                var orderedKeys = new List<uint>(aliases.Length + 1);
                foreach (string? name in aliases.Concat(new[] { declared.Name }))
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        issues.Add("version tolerant: member names");
                        return null;
                    }
                    try
                    {
                        uint key = WireNameHash(name!, StrictMemberEncoding);
                        if (!keys.Add(key))
                        {
                            issues.Add("version tolerant: member name keys");
                            return null;
                        }
                        orderedKeys.Add(key);
                    }
                    catch (EncoderFallbackException)
                    {
                        issues.Add("version tolerant: member names");
                        return null;
                    }
                }
                // The complete-formatter annotation records the legacy Other classification. Non-object
                // packable records retain their declared class or struct classification in the name table.
                bool ownFormatter = HasAttribute(declared, PackableObjectAttribute);
                int? fieldType = ownFormatter ? VersionOwnFormatterType(memberType) : VersionFieldType(memberType);
                if (fieldType == null)
                {
                    issues.Add("version tolerant: members '" + declared.Name + "'");
                    return null;
                }
                nameKeys[index] = orderedKeys.ToArray();
                oldNames[index] = aliases!;
                fields[index] = (fieldType.Value, members[index].Compressed || ownFormatter && HasAttribute(declared, CompressAttribute));
                if (ownFormatter || fields[index].Compressed)
                    widths[index] = 255;
                else if (memberType.SpecialType == SpecialType.System_String)
                {
                    WirePolicy memberPolicy = members[index].Policy;
                    bool pinned = memberPolicy.Declared && (!policy.Declared ||
                        memberPolicy.Strings.Encoding != policy.Strings.Encoding || memberPolicy.Strings.Recording != policy.Strings.Recording);
                    widths[index] = pinned ? 255 : 254;
                }
                else
                {
                    var nullable = memberType as INamedTypeSymbol;
                    bool optional = nullable?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                    int? width = VersionWidth(optional ? nullable!.TypeArguments[0] : memberType);
                    if (width.HasValue && optional)
                        width++;
                    widths[index] = width is >= 0 and <= 250 ? width.Value : 255;
                    fixedNullable[index] = optional && widths[index] != 255;
                }
                memberTypes[index] = memberType;
            }

            var descriptors = new ulong[members.Length];
            var runtimeContracts = new bool[members.Length];
            var entries = new string[members.Length];
            var tempTypes = new string[members.Length];
            for (int index = 0; index < members.Length; index++)
            {
                ITypeSymbol memberType = memberTypes[index];
                string serialized = oldNames[index].Length == 0 ? members[index].Name : oldNames[index][0];
                var (fieldType, compressed) = fields[index];
                string representation = VersionRepresentation(fieldType, members[index].Policy, compressed);
                string? typeIdentity = VersionIsDirectPrimitive(fieldType, memberType) ? string.Empty : VersionTypeIdentity(memberType);
                runtimeContracts[index] = typeIdentity == null;
                uint hash = typeIdentity == null ? 0 : WireNameHash(representation + typeIdentity, Encoding.UTF8) & 0xFFFFFFu;
                descriptors[index] = nameKeys[index][0] | (ulong)hash << 32 | (ulong)widths[index] << 56;
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
            }
            string globalName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string schema = "global::LuminPack.Generated." + GeneratedNames.Extension(Options.AssemblyName) + ".Version_" +
                GeneratedNames.Sanitize(globalName) + (identity.TypeParameters.Length == 0 ? string.Empty :
                    "<" + string.Join(", ", identity.TypeParameters) + ">");
            var layoutFields = locals.Select(local => local.TypeName +
                (IsUnmanagedKeyword(local.TypeName) || local.IsValue ? string.Empty : "?") + " " + Identifier(local.Name)).ToArray();
            return new VersionSchemaModel(schema, descriptors, runtimeContracts, nameKeys, entries, tempTypes,
                VersionLayout(type), layoutFields, oldNames, fixedNullable);
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
            return "LuminLayout_" + digest + "_" + PortableSlotSuffix(type) + "_Local" + local +
                (type.IsDefinition && type.TypeParameters.Length != 0 ? "<" + string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)) + ">" : string.Empty);
        }

        /// <summary>Returns the name-table classification of a member with an explicit formatter annotation.</summary>
        /// <param name="type">The declared member type.</param>
        /// <returns>The legacy Other code, or the declared class or struct code of a non-object packable.</returns>
        private static int VersionOwnFormatterType(ITypeSymbol type) =>
            type is INamedTypeSymbol { IsRecord: false, IsAbstract: false, TypeKind: TypeKind.Class or TypeKind.Struct } named &&
            IsPackable(named) && GenerationMode(named) != "Object" ? named.TypeKind == TypeKind.Class ? 16 : 15 : 19;

        /// <summary>Classifies a member's declared type with the legacy schema codes.</summary>
        /// <param name="type">The declared member type.</param>
        /// <returns>The classification, or null for a member the schema does not cover.</returns>
        private int? VersionFieldType(ITypeSymbol type)
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
            if (type is IArrayTypeSymbol array)
                return array.ElementType is INamedTypeSymbol element && (FormatterStorage(element) != null || ContainsTypeParameter(element)) ? 19 : 18;
            if (type is INamedTypeSymbol named)
            {
                if (Builtin(named) != null || IsCultureInfo(named) || named.IsAbstract || named.IsRecord ||
                    FormatterRegistry.HasStandalone(named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.Name) ||
                    FormatterDefinitions.HasGeneric(named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.Name) ||
                    DictionaryKind(named) != null)
                    return 19;
                if (SequenceKind(named) is { } sequence)
                    return sequence == CollectionKind.List && !(named.TypeArguments[0] is INamedTypeSymbol item &&
                        (FormatterStorage(item) != null || ContainsTypeParameter(item))) ? 17 : 19;
                if (named.TypeKind == TypeKind.Struct && FormatterStorage(named) != null)
                    return 19;
                if (named.TypeKind == TypeKind.Struct && !named.IsRecord)
                    return 15;
                if (IsPackable(named) && !named.IsAbstract)
                    return 16;
            }
            return 19;
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
        /// <param name="compressed">Whether the member uses its compressed representation.</param>
        /// <returns>The one-character representation string.</returns>
        private static string VersionRepresentation(int fieldType, WirePolicy policy, bool compressed)
        {
            int encoding = policy.Strings.Encoding == StringEncoding.Utf8 ? 0 : 1;
            // A compressed member records its compressed representation, as the original schema does.
            return new string((char)(fieldType | (compressed ? 32 : 0) | (encoding << 6) | ((int)policy.Strings.Recording << 7)), 1);
        }

        /// <summary>Gets a width that the existing field operation guarantees on each supported target.</summary>
        /// <param name="type">The declared field or nullable payload type.</param>
        /// <returns>The wire width, or null when it can vary.</returns>
        private int? VersionWidth(ITypeSymbol type)
        {
            int scalarWidth = PortableScalarSize(type);
            if (scalarWidth != 0)
                return scalarWidth;
            if (type is not INamedTypeSymbol { IsUnmanagedType: true } named || GenerationMode(named) != "Object" ||
                ContainsTypeParameter(named) || IsRuntimeSizedVector(named))
                return null;
            if (type.SpecialType == SpecialType.System_DateTime || Builtin(type) == "global::System.TimeSpan")
                return 8;
            if (Builtin(type) == "global::System.Guid")
                return 16;
            if (Builtin(type) == "global::System.DateTimeOffset")
                return null;
            if (FormatterStorage(named) is { } layout)
                return layout.WireSize;
            var plan = AnalyzePortable(named, strict: true);
            if (plan == null)
                return null;
            var models = BuildModels(plan);
            return models.All(model => model.Size == models[0].Size) ? models[0].Size : null;
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
            // Versioned records resolve their schema-owned storage through TryVersionMirror. Inherited storage is also
            // captured as separate physical levels by CaptureMirrors.
            if (versioned)
            {
                issues.Add("a versioned record requires its schema-owned storage mirror");
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
        private static IEnumerable<ISymbol> OrderedStorageMembers(INamedTypeSymbol type) =>
            StorageSnapshots.GetValue(type, CaptureStorage).Members;

        private static readonly ConditionalWeakTable<INamedTypeSymbol, StorageSnapshot> StorageSnapshots =
            new ConditionalWeakTable<INamedTypeSymbol, StorageSnapshot>();

        /// <summary>Captures physical source or metadata declaration order once for each compiler symbol.</summary>
        /// <param name="type">The storage declaration.</param>
        /// <returns>The analysis-only ordered storage snapshot.</returns>
        private static StorageSnapshot CaptureStorage(INamedTypeSymbol type)
        {
            var declared = type.GetMembers();
            var properties = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
            foreach (ISymbol member in declared)
                if (member is IPropertySymbol property)
                    properties[property.Name] = property;
            var backingFields = new Dictionary<IPropertySymbol, IFieldSymbol>(SymbolEqualityComparer.Default);
            foreach (ISymbol member in declared)
            {
                if (member is not IFieldSymbol field)
                    continue;
                if (field.AssociatedSymbol is IPropertySymbol property)
                    backingFields[property] = field;
                else if (field.Name.StartsWith("<", StringComparison.Ordinal) && field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal) &&
                    properties.TryGetValue(field.Name.Substring(1, field.Name.Length - 17), out IPropertySymbol? imported))
                    backingFields[imported] = field;
            }
            if (type.DeclaringSyntaxReferences.Length != 0)
                return new StorageSnapshot(declared.Where(member => member is IPropertySymbol or IFieldSymbol)
                    .OrderBy(member => member.DeclaringSyntaxReferences.FirstOrDefault()?.Span.Start ?? int.MaxValue).ToArray(), backingFields);
            var ordered = new List<ISymbol>();
            foreach (ISymbol member in declared)
                if (member is IFieldSymbol field)
                {
                    IPropertySymbol? property = field.AssociatedSymbol as IPropertySymbol;
                    if (property == null && field.Name.StartsWith("<", StringComparison.Ordinal) && field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal))
                        properties.TryGetValue(field.Name.Substring(1, field.Name.Length - 17), out property);
                    ordered.Add(property ?? (ISymbol)field);
                }
            return new StorageSnapshot(ordered.ToArray(), backingFields);
        }

        /// <summary>Owns compiler symbols only behind a weak analysis cache.</summary>
        private sealed class StorageSnapshot
        {
            internal readonly ISymbol[] Members;
            internal readonly Dictionary<IPropertySymbol, IFieldSymbol> BackingFields;

            /// <summary>Captures declaration order and the physical backing storage of automatic properties.</summary>
            /// <param name="members">The complete physical storage sequence.</param>
            /// <param name="backingFields">The automatic-property storage index.</param>
            internal StorageSnapshot(ISymbol[] members, Dictionary<IPropertySymbol, IFieldSymbol> backingFields)
            {
                Members = members;
                BackingFields = backingFields;
            }
        }

        /// <summary>Whether a property is an instance auto-property with compiler-generated backing storage.</summary>
        /// <param name="property">The declared property.</param>
        /// <returns>Whether the property stores its value in a backing field.</returns>
        private static bool IsAutoProperty(IPropertySymbol property)
        {
            if (property.IsStatic || property.IsAbstract || property.IsExtern || property.IsIndexer)
                return false;
            return StorageSnapshots.GetValue(property.ContainingType, CaptureStorage).BackingFields.ContainsKey(property);
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

        /// <summary>Initializes a <c>AnalysisKey</c> from the supplied data.</summary>
        /// <param name="symbol">The symbol being analyzed.</param>
        /// <param name="depth">The current nesting depth.</param>
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
    /// <remarks>A definition nested in a generic type, such as <c>Outer&lt;T&gt;.Inner</c>, is generic over the containing
    /// parameters, which precede its own. A closed construction spells every level's type arguments in its names.</remarks>
    /// <param name="symbol">The symbol being analyzed.</param>
    /// <returns>The resulting <c>TypeIdentity</c>.</returns>
    private static TypeIdentity Identity(INamedTypeSymbol symbol)
    {
        int depth = 0;
        for (var current = symbol.ContainingType; current != null; current = current.ContainingType)
            depth++;
        var containing = new string[depth];
        var arities = new int[depth];
        bool definition = symbol.IsDefinition;
        int level = depth;
        for (var current = symbol.ContainingType; current != null; current = current.ContainingType)
        {
            level--;
            containing[level] = definition ? current.Name : ClosedName(current);
            arities[level] = definition ? current.Arity : 0;
        }
        string[] parameters = Array.Empty<string>();
        if (definition)
        {
            List<ITypeParameterSymbol> all = AllTypeParameters(symbol);
            parameters = new string[all.Count];
            for (int index = 0; index < parameters.Length; index++)
                parameters[index] = all[index].Name;
        }
        string? @namespace = symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace
            ? null : symbol.ContainingNamespace.ToDisplayString();
        return new TypeIdentity(@namespace, containing, definition ? symbol.Name : ClosedName(symbol), parameters, arities);
    }

    /// <summary>Spells one closed level of a nested type: its name and its own type arguments.</summary>
    /// <param name="symbol">A constructed type or one of its containing types.</param>
    /// <returns>The level's name with its own type arguments.</returns>
    private static string ClosedName(INamedTypeSymbol symbol)
    {
        if (symbol.Arity == 0)
            return symbol.Name;
        var text = new System.Text.StringBuilder(symbol.Name, 32).Append('<');
        for (int index = 0; index < symbol.TypeArguments.Length; index++)
            (index == 0 ? text : text.Append(", ")).Append(Session.FormatterSpelling(symbol.TypeArguments[index]));
        return text.Append('>').ToString();
    }

    /// <summary>Lists every type parameter a definition is generic over, from the outermost containing type inward.</summary>
    /// <param name="symbol">A type definition.</param>
    /// <returns>The containing types' parameters followed by the type's own.</returns>
    internal static List<ITypeParameterSymbol> AllTypeParameters(INamedTypeSymbol symbol)
    {
        var parameters = new List<ITypeParameterSymbol>();
        for (var current = symbol; current != null; current = current.ContainingType)
            parameters.InsertRange(0, current.TypeParameters);
        return parameters;
    }

    /// <summary>Lists every type argument of a construction, from the outermost containing type inward.</summary>
    /// <param name="symbol">A type construction.</param>
    /// <returns>The containing types' arguments followed by the type's own.</returns>
    internal static List<ITypeSymbol> AllTypeArguments(INamedTypeSymbol symbol)
    {
        var arguments = new List<ITypeSymbol>();
        for (var current = symbol; current != null; current = current.ContainingType)
            arguments.InsertRange(0, current.TypeArguments);
        return arguments;
    }

    /// <summary>Spells the <see cref="TypeIdentity.MetadataName"/> that <see cref="Identity"/> gives a type, without building the
    /// identity.</summary>
    /// <param name="symbol">A source type.</param>
    /// <returns>The identity key, or null when the type has no model.</returns>
    internal static string? ReachKey(INamedTypeSymbol symbol)
    {
        var text = new System.Text.StringBuilder(64);
        if (symbol.ContainingNamespace is { IsGlobalNamespace: false } @namespace)
            text.Append(@namespace.ToDisplayString()).Append('.');
        int start = text.Length;
        bool definition = symbol.IsDefinition;
        for (INamedTypeSymbol? current = symbol.ContainingType; current != null; current = current.ContainingType)
        {
            string level = !definition ? ClosedName(current) :
                current.Arity == 0 ? current.Name : current.Name + "`" + current.Arity.ToString(CultureInfo.InvariantCulture);
            text.Insert(start, level + "+");
        }
        if (!definition)
            text.Append(ClosedName(symbol));
        else
        {
            text.Append(symbol.Name);
            if (symbol.Arity != 0)
                text.Append('`').Append(symbol.Arity.ToString(CultureInfo.InvariantCulture));
        }
        return text.ToString();
    }

    /// <summary>Classifies the payload protocol from the declaration and its generator type.</summary>
    /// <param name="symbol">The declared payload type.</param>
    /// <returns>The payload protocol.</returns>
    private static PayloadKind Kind(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind == TypeKind.Interface || symbol.IsAbstract)
            return PayloadKind.Union;
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
    /// <param name="fallback">The value when no option declaration sets the switch.</param>
    /// <returns>The first explicit setting, or <paramref name="fallback"/>.</returns>
    private static bool OptionSwitch(ISymbol symbol, string name, bool fallback = false)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != OptionAttribute)
                continue;
            foreach (var argument in attribute.NamedArguments)
                if (argument.Key == name && argument.Value.Value is bool enabled)
                    return enabled;
        }
        return fallback;
    }

    /// <summary>Whether a type carries the packable attribute.</summary>
    /// <param name="symbol">The candidate type.</param>
    /// <returns>Whether the type declares a generated payload contract.</returns>
    private static bool IsPackable(INamedTypeSymbol symbol) => HasAttribute(symbol, PackableAttribute);

    /// <summary>Whether an unannotated class is expanded inline as a nested object, as the original pipeline does.</summary>
    /// <param name="type">The member type.</param>
    /// <returns>Whether the class is a concrete, non-record, non-generic source class of this compilation.</returns>
    /// <remarks>Abstract and record classes and separately formatted records still require <c>[LuminPackable]</c>.</remarks>
    private static bool IsImplicitInlineClass(INamedTypeSymbol type) => type.TypeKind == TypeKind.Class && !type.IsAbstract &&
        !type.IsStatic && !type.IsRecord && !type.IsGenericType && type.SpecialType == SpecialType.None &&
        type.DeclaringSyntaxReferences.Length != 0 && !IsPackable(type) && !HasAttribute(type, UnionAttribute) &&
        !(type.ContainingNamespace?.ToDisplayString() ?? string.Empty).StartsWith("System", StringComparison.Ordinal);

    private const string CompressAttribute = "LuminPack.Attribute.LuminPackCompressAttribute";

    /// <summary>Whether a declared member uses the compressed sequence formatter.</summary>
    /// <param name="type">The declared member type.</param>
    /// <param name="ownFormatter">Whether the member opts into complete formatter dispatch.</param>
    /// <returns>Whether the compress declaration applies.</returns>
    private static bool CompressibleCollection(ITypeSymbol type, bool ownFormatter)
    {
        ITypeSymbol? element = type switch
        {
            IArrayTypeSymbol { IsSZArray: true } array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: 1 } list when list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"
                => list.TypeArguments[0],
            _ => null,
        };
        if (element == null || element.IsUnmanagedType)
            return element != null;
        if (!ownFormatter || element is not IArrayTypeSymbol { IsSZArray: true })
            return false;

        // The original managed sequence formatter propagates compression down jagged arrays of known values.
        while (element is IArrayTypeSymbol { IsSZArray: true } array)
            element = array.ElementType;
        return KnownValueTypes.Contains(Session.FormatterSpelling(element));
    }

    /// <summary>Resolves the reference contract directly from a concrete class declaration.</summary>
    /// <param name="type">The declaration whose own option applies.</param>
    /// <returns>Whether its generated records preserve object identity.</returns>
    private static bool TracksReferences(INamedTypeSymbol type) =>
        type is { TypeKind: TypeKind.Class, IsAbstract: false } &&
        (OptionSwitch(type, "TrackReferences") || GenerationMode(type) == "CircleReference");

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

    /// <summary>Spells a collection type with explicit nullable value arguments for stable owner identities.</summary>
    /// <param name="type">The declared type.</param>
    /// <returns>The fully qualified spelling.</returns>
    private static string DeclaredType(ITypeSymbol type) => type.ToDisplayString(CollectionDisplay);

    private static readonly SymbolDisplayFormat CollectionDisplay = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.ExpandNullable);

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

    /// <summary>Recognizes the framework culture type the shared formatter owner writes.</summary>
    /// <param name="type">The declared member type.</param>
    /// <returns>Whether the declaration is <c>System.Globalization.CultureInfo</c>.</returns>
    private static bool IsCultureInfo(ITypeSymbol type)
        => type is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } named &&
           named.ContainingNamespace.ToDisplayString() == "System.Globalization" && named.Name == "CultureInfo";

    /// <summary>Normalizes a nullable element whose underlying value has a shared scalar owner.</summary>
    /// <param name="element">The declared collection element.</param>
    /// <returns>The canonical nullable spelling, or null for an unsupported element.</returns>
    private static string? NullableSequenceElement(ITypeSymbol element) =>
        element is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable &&
        Scalar(nullable.TypeArguments[0]) is { } scalar ? "global::System.Nullable<" + scalar.Keyword + ">" : null;

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

    /// <summary>Checks both projected overloads for selecting and reading existing matrix slots.</summary>
    /// <param name="compilation">The consumer's runtime and framework metadata.</param>
    /// <returns>Whether the complete slot selection and read API is available.</returns>
    private static bool HasStringMatrixReader(Compilation compilation)
    {
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader");
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var text = compilation.GetSpecialType(SpecialType.System_String);
        var span = compilation.GetTypeByMetadataName("System.Span`1")?.Construct(text);
        if (reader == null || options == null || span == null)
            return false;
        var matrix = compilation.CreateArrayTypeSymbol(text, 2);
        bool BothProfiles(string name, System.Func<IMethodSymbol, bool> signature)
        {
            var methods = reader.GetMembers(name).OfType<IMethodSymbol>().Where(m => !m.IsStatic && !m.ReturnsByRef &&
                !m.ReturnsByRefReadonly && m.DeclaredAccessibility == Accessibility.Public && signature(m)).ToArray();
            return methods.Any(m => m.Arity == 0) && methods.Any(m => m.Arity == 1 && m.TypeParameters[0].HasValueTypeConstraint &&
                !m.TypeParameters[0].HasUnmanagedTypeConstraint && m.TypeParameters[0].ConstraintTypes.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(m.TypeParameters[0].ConstraintTypes[0], options));
        }
        return BothProfiles("TryGetStringMatrixSlots", m => m.ReturnType.SpecialType == SpecialType.System_Boolean &&
            m.Parameters.Length == 4 && m.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, matrix) &&
            m.Parameters[1].RefKind == RefKind.None && m.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
            m.Parameters[2].RefKind == RefKind.None && m.Parameters[2].Type.SpecialType == SpecialType.System_Int32 &&
            m.Parameters[3].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(m.Parameters[3].Type, span)) &&
            BothProfiles("ReadStrings", m => m.ReturnsVoid && m.Parameters.Length == 1 && m.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, span));
    }

    /// <summary>Classifies a declared sequence family.</summary>
    /// <param name="type">The declared member type.</param>
    /// <returns>The family, or null when the declaration is not a modeled sequence.</returns>
    private static CollectionKind? SequenceKind(INamedTypeSymbol type)
    {
        if (!type.IsGenericType || type.TypeArguments.Length != 1)
            return null;
        var definition = type.OriginalDefinition;
        var formatter = FormatterRegistry.FindSequence(definition.ContainingNamespace.ToDisplayString() + "." + definition.Name);
        if (formatter?.Kind == CollectionKind.List && type.TypeArguments[0] is INamedTypeSymbol inner &&
            FormatterRegistry.FindSequence(inner.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." +
                inner.OriginalDefinition.Name)?.Kind == CollectionKind.List)
            return CollectionKind.NestedList;
        return formatter?.Kind;

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
                case "PriorityQueue":
                    return Model.DictionaryKind.Priority;
                case "IDictionary":
                case "IReadOnlyDictionary":
                    return Model.DictionaryKind.Interface;
            }
        }
        if (@namespace == "System.Collections.ObjectModel" && name == "ReadOnlyDictionary")
            return Model.DictionaryKind.ReadOnly;
        if (@namespace == "System.Collections.Frozen" && name == "FrozenDictionary")
            return Model.DictionaryKind.Frozen;
        if (@namespace == "System.Collections.Concurrent" && name == "ConcurrentDictionary")
            return Model.DictionaryKind.Concurrent;
        if (@namespace == "System.Collections.Immutable" && name is "ImmutableDictionary" or "ImmutableSortedDictionary" or "IImmutableDictionary")
            return Model.DictionaryKind.Immutable;
        return null;
    }

    /// <summary>Selects the framing of a complete nested payload.</summary>
    /// <param name="nested">The nested payload model.</param>
    /// <returns>The member framing.</returns>
    private static PayloadShape ShapeOf(TypeModel nested) => nested switch
    {
        RecordModel { Policy.TrackReferences: true } => PayloadShape.ReferenceTracked,
        RecordModel { CollectionBase: not null } => PayloadShape.MemberEntry,
        RecordModel { Versioned: false } => PayloadShape.Inline,
        _ => PayloadShape.MemberEntry,
    };
}
