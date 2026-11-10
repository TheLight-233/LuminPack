using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;

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

    /// <summary>Builds a model for one attributed type.</summary>
    /// <param name="symbol">The attributed type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>A complete model, an incomplete model or an unsupported result.</returns>
    internal static ModelResult TryCreate(INamedTypeSymbol symbol, Compilation compilation, GeneratorOptions options)
        => new Session(compilation, options).Create(symbol, 0);

    /// <summary>Holds the per-run analysis state: the consumer compilation, its project facts and the payloads being expanded.</summary>
    private sealed class Session
    {
        private readonly Compilation _compilation;
        private readonly GeneratorOptions _options;
        private readonly HashSet<INamedTypeSymbol> _inProgress = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        internal Session(Compilation compilation, GeneratorOptions options)
        {
            _options = options;
            _compilation = compilation;
        }

        /// <summary>Models one payload, stopping before a cycle or an over-deep nested expansion can recurse.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <param name="depth">The number of nested payload expansions already entered.</param>
        /// <returns>The analysis result.</returns>
        internal ModelResult Create(INamedTypeSymbol symbol, int depth)
        {
            if (depth > MaxDepth)
                return ModelResult.Unsupported("payload nesting exceeds " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " levels");
            if (!_inProgress.Add(symbol))
                return ModelResult.Unsupported("payload refers back to '" + symbol.ToDisplayString() + "'");
            var issues = new List<string>();
            try
            {
                TypeModel? model = CreateCore(symbol, depth, issues);
                if (model == null)
                    return ModelResult.Unsupported(issues.Count == 0 ? "not a modeled payload" : string.Join("; ", issues));
                if (issues.Count == 0)
                    return ModelResult.Ready(model);
                return ModelResult.Incomplete(model, string.Join("; ", issues.Distinct(StringComparer.Ordinal)));
            }
            finally
            {
                _inProgress.Remove(symbol);
            }
        }

        /// <summary>Classifies one payload and builds the model shared by every payload kind.</summary>
        /// <param name="symbol">The declared payload type.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reasons the model is not complete.</param>
        /// <returns>The model, or null when the declaration itself is outside the surface.</returns>
        private TypeModel? CreateCore(INamedTypeSymbol symbol, int depth, List<string> issues)
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
                    MemberModel[] members = Members(symbol, policy, depth, issues, out _);
                    PortableLayout? layout = symbol.IsUnmanagedType ? TryPortableLayout(symbol) : null;
                    StructStorage storage = !symbol.IsUnmanagedType ? StructStorage.ManagedRecord :
                        layout == null ? StructStorage.RawUnmanaged : StructStorage.PortableUnmanaged;
                    return new StructModel(identity, accessibility, members, policy, constraints, storage, layout);
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
                    MemberModel[] members = Members(symbol, policy, depth, issues, out _);
                    return new CircleReferenceModel(identity, accessibility, members, policy, constraints, guarded: true);
                }
                default:
                {
                    MemberModel[] members = Members(symbol, policy, depth, issues, out bool derived);
                    bool versioned = generatorType == "VersionTolerant";
                    bool proven = !versioned && !derived && policy.Bound == null && _options.Net8 && _options.Unsafe && !_options.Unity &&
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
        /// <returns>The members in wire order.</returns>
        private MemberModel[] Members(INamedTypeSymbol symbol, WirePolicy ownerPolicy, int depth, List<string> issues, out bool derived)
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
                MemberModel? model = Classify(member, storage, policy, symbol, depth, issues);
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
        /// <returns>The member model, or null when the type is outside the modeled surface.</returns>
        private MemberModel? Classify(ISymbol member, MemberStorage storage, WirePolicy policy, INamedTypeSymbol owner, int depth,
            List<string> issues)
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

            if (type is INamedTypeSymbol named)
            {
                if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    issues.Add("member '" + name + "' is a nullable value type, which has no modeled scalar form");
                    return null;
                }
                if (DictionaryKind(named) is { } dictionaryKind)
                    return new DictionaryMember(name, storage, policy, DeclaredType(named), dictionaryKind,
                        Reference(named.TypeArguments[0]), Reference(named.TypeArguments[1]));
                if (SequenceKind(named) is { } collectionKind)
                    return new CollectionMember(name, storage, policy, DeclaredType(named), collectionKind,
                        Reference(named.TypeArguments[0]));
                if (named.TypeKind is TypeKind.Class or TypeKind.Struct)
                    return Payload(member, named, storage, policy, depth, issues);
            }

            issues.Add("member '" + name + "' has the unsupported type '" + type.ToDisplayString() + "'");
            return null;
        }

        /// <summary>Models a member that embeds another payload or a generic definition.</summary>
        /// <param name="member">The declared member.</param>
        /// <param name="type">The member's class or struct type.</param>
        /// <param name="storage">How the member's storage is reached.</param>
        /// <param name="policy">The member's resolved wire policy.</param>
        /// <param name="depth">The nested payload depth.</param>
        /// <param name="issues">Receives the reason a nested payload is not expanded.</param>
        /// <returns>The payload member.</returns>
        private MemberModel? Payload(ISymbol member, INamedTypeSymbol type, MemberStorage storage, WirePolicy policy, int depth,
            List<string> issues)
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

            if (_inProgress.Contains(type))
            {
                if (GenerationMode(type) == "CircleReference")
                    return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.ReferenceTracked, nested: null);
                issues.Add("member '" + name + "' refers back to '" + type.ToDisplayString() + "' before its payload is complete");
                return new PayloadMember(name, storage, policy, Reference(type), PayloadShape.MemberEntry, nested: null);
            }

            ModelResult nested = Create(type, depth + 1);
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
        private PortableLayout? TryPortableLayout(INamedTypeSymbol symbol)
        {
            if (symbol.SpecialType != SpecialType.None || symbol.IsRefLikeType)
                return null;
            IFieldSymbol[] fields = symbol.GetMembers().OfType<IFieldSymbol>()
                .Where(field => !field.IsStatic && !field.IsConst).ToArray();
            if (fields.Length == 0 || fields.Any(field => field.IsFixedSizeBuffer))
                return null;
            (System.Runtime.InteropServices.LayoutKind kind, _, _) = LayoutInfo(symbol);
            if (kind == System.Runtime.InteropServices.LayoutKind.Auto)
                return new PortableLayout(symbol.Name + "View", fields.Length);
            int strategy = LayoutStrategy(symbol);
            if (strategy == 2)
                return null;
            if (!MeasureStruct(symbol, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default), out StructMeasure measure))
                return null;
            bool portable = strategy == 1 ? measure.Padding : measure.Size32 != measure.Size64;
            return portable ? new PortableLayout(symbol.Name + "View", fields.Length) : null;
        }

        /// <summary>Resolves the declared unmanaged storage strategy, type first and assembly second.</summary>
        /// <param name="symbol">The unmanaged struct.</param>
        /// <returns>Zero for tagged, one for packed or two for native storage.</returns>
        private int LayoutStrategy(INamedTypeSymbol symbol)
        {
            int? declared = ReadStructLayoutOption(symbol.GetAttributes());
            if (declared.HasValue)
                return declared.Value;
            return ReadStructLayoutOption(symbol.ContainingAssembly?.GetAttributes() ?? _compilation.Assembly.GetAttributes()) ?? 0;
        }

        /// <summary>Measures a struct's packed wire and its predicted native layouts.</summary>
        /// <param name="symbol">The struct to measure.</param>
        /// <param name="visiting">The aggregates already on the measurement path.</param>
        /// <param name="measure">Receives the packed width, native sizes and padding evidence.</param>
        /// <returns>Whether every storage field has a fixed representation.</returns>
        private static bool MeasureStruct(INamedTypeSymbol symbol, HashSet<INamedTypeSymbol> visiting, out StructMeasure measure)
        {
            measure = new StructMeasure();
            if (!visiting.Add(symbol))
                return false;
            try
            {
                (System.Runtime.InteropServices.LayoutKind kind, int pack, int declaredSize) = LayoutInfo(symbol);
                int effectivePack = pack == 0 ? 8 : pack;
                int end32 = 0, end64 = 0, wire = 0, packedEnd = 0, alignment32 = 1, alignment64 = 1;
                bool padding = kind == System.Runtime.InteropServices.LayoutKind.Auto;
                foreach (var field in symbol.GetMembers().OfType<IFieldSymbol>().Where(item => !item.IsStatic && !item.IsConst))
                {
                    if (field.IsFixedSizeBuffer ||
                        !TryMeasure(field.Type, is64Bit: false, visiting, out int size32, out int fieldAlignment32, out int fieldWire) ||
                        !TryMeasure(field.Type, is64Bit: true, visiting, out int size64, out int fieldAlignment64, out int fieldWire64) ||
                        fieldWire != fieldWire64)
                        return false;
                    int alignmentField32 = Math.Min(effectivePack, fieldAlignment32);
                    int alignmentField64 = Math.Min(effectivePack, fieldAlignment64);
                    alignment32 = Math.Max(alignment32, alignmentField32);
                    alignment64 = Math.Max(alignment64, alignmentField64);
                    int offset32, offset64;
                    if (kind == System.Runtime.InteropServices.LayoutKind.Explicit)
                    {
                        offset32 = offset64 = FieldOffset(field);
                        if (offset32 < 0)
                        {
                            padding = true;
                            offset32 = offset64 = end32;
                        }
                    }
                    else
                    {
                        offset32 = RoundUp(end32, alignmentField32);
                        offset64 = RoundUp(end64, alignmentField64);
                    }
                    padding |= offset32 != packedEnd;
                    end32 = Math.Max(end32, offset32 + size32);
                    end64 = Math.Max(end64, offset64 + size64);
                    packedEnd += fieldWire;
                    wire += fieldWire;
                }
                measure.Padding = padding;
                measure.Wire = wire;
                measure.Alignment32 = alignment32;
                measure.Alignment64 = alignment64;
                measure.Size32 = Math.Max(Math.Max(1, declaredSize), RoundUp(end32, alignment32));
                measure.Size64 = Math.Max(Math.Max(1, declaredSize), RoundUp(end64, alignment64));
                measure.Padding |= measure.Size32 != wire || measure.Size64 != wire;
                return true;
            }
            finally
            {
                visiting.Remove(symbol);
            }
        }

        /// <summary>Measures one storage leaf or aggregate for one pointer width.</summary>
        /// <param name="type">The storage type.</param>
        /// <param name="is64Bit">Whether the pointer width is 64 bits.</param>
        /// <param name="visiting">The aggregates already on the measurement path.</param>
        /// <param name="size">Receives the value's native size.</param>
        /// <param name="alignment">Receives the value's natural alignment.</param>
        /// <param name="wire">Receives the value's packed wire width.</param>
        /// <returns>Whether the value has a fixed representation.</returns>
        private static bool TryMeasure(ITypeSymbol type, bool is64Bit, HashSet<INamedTypeSymbol> visiting, out int size,
            out int alignment, out int wire)
        {
            size = alignment = wire = 0;
            if (Scalar(type) is { } scalar)
            {
                size = wire = scalar.Size;
                alignment = Math.Min(scalar.Size, is64Bit ? 8 : 4);
                return true;
            }
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying })
                return TryMeasure(underlying, is64Bit, visiting, out size, out alignment, out wire);
            if (type.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr ||
                type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
            {
                size = alignment = wire = is64Bit ? 8 : 4;
                return true;
            }
            if (type is not INamedTypeSymbol named)
                return false;
            if (Opaque(named) is { } opaque)
            {
                size = wire = opaque.Size;
                alignment = is64Bit ? opaque.Alignment64 : opaque.Alignment32;
                return true;
            }
            if (named.TypeKind != TypeKind.Struct || named.IsRefLikeType || !named.IsUnmanagedType ||
                named.GetAttributes().Any(item => item.AttributeClass?.ToDisplayString() ==
                    "System.Runtime.CompilerServices.InlineArrayAttribute"))
                return false;
            if (!MeasureStruct(named, visiting, out StructMeasure measure))
                return false;
            size = is64Bit ? measure.Size64 : measure.Size32;
            alignment = is64Bit ? measure.Alignment64 : measure.Alignment32;
            wire = measure.Wire;
            return size > 0;
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

    /// <summary>Rounds a value up to the next multiple of an alignment.</summary>
    /// <param name="value">The unaligned value.</param>
    /// <param name="alignment">The positive alignment.</param>
    /// <returns>The aligned value.</returns>
    private static int RoundUp(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    /// <summary>The measured packed wire and predicted native storage of one struct.</summary>
    private sealed class StructMeasure
    {
        internal int Size32;
        internal int Size64;
        internal int Alignment32;
        internal int Alignment64;
        internal int Wire;
        internal bool Padding;
    }
}
