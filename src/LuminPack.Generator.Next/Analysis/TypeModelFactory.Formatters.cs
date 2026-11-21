using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        /// <summary>Retains proven specialized members and supplies the general formatter contract for other supported elements.</summary>
        /// <param name = "member">The member used by this operation.</param>
        /// <param name = "storage">The storage used by this operation.</param>
        /// <param name = "policy">The policy used by this operation.</param>
        /// <param name = "owner">The owner used by this operation.</param>
        /// <param name = "depth">The depth used by this operation.</param>
        /// <param name = "issues">The issues used by this operation.</param>
        /// <param name = "path">The path used by this operation.</param>
        /// <param name="ownFormatter">Whether the member explicitly selects its complete formatter.</param>
        /// <returns>The normalized result of the operation.</returns>
        private MemberModel? Classify(MemberSite member, MemberStorage storage, WirePolicy policy, INamedTypeSymbol owner, int depth,
            List<string> issues, Path path, bool ownFormatter = false)
        {
            ITypeSymbol type = member.Type;
            // A closed unconstrained T? can carry an annotation even when substituted with a non-nullable value type.
            if (policy.TrackReferences && type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated &&
                type is INamedTypeSymbol namedValue && namedValue.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
            {
                type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                member = new MemberSite(member.Name, type, member.Volatile, member.Containing);
            }
            if (type is ITypeParameterSymbol || type.TypeKind == TypeKind.Enum ||
                type.SpecialType == SpecialType.System_String || Scalar(type) != null)
                return ClassifySpecialized(member, storage, policy, owner, depth, issues, path);
            if (type is not ITypeParameterSymbol && ContainsTypeParameter(type) && MentionsOwnParameter(type, owner))
                return new TypeParameterMember(member.Name, storage, policy, FormatterSpelling(type), type.IsValueType, type.IsReferenceType);
            FormatterModel? formatter = type is ITypeParameterSymbol ? null : Formatter(type, depth, path);
            if (ownFormatter)
            {
                formatter ??= new FormatterModel(FormatterSpelling(type), FormatterDefinitions.Registered);
                return new SpecialMember(member.Name, storage, policy, formatter.Type, type.IsReferenceType,
                    storage == MemberStorage.AutoProperty, formatter);
            }
            var specializedIssues = new List<string>();
            MemberModel? specialized = ClassifySpecialized(member, storage, policy, owner, depth, specializedIssues, path);
            bool missingElementBody = specialized is CollectionMember { Nested: { } nested } && RenderModelFactory.SequenceElement(nested, Options) == null;
            bool container = type is IArrayTypeSymbol || type is INamedTypeSymbol named &&
                (FormatterRegistry.FindSequence(named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.Name) != null ||
                 FormatterRegistry.FindDictionary(FormatterSpelling(named)) != null ||
                 FormatterDefinitions.HasGeneric(named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.Name));
            if (formatter == null && (missingElementBody || container))
            {
                issues.Add("member '" + member.Name + "' requires a payload body that has not been modeled");
                return null;
            }

            // Raw struct storage keeps its fixed member shape; its owner still serves JSON and standalone calls.
            if (specialized is ScalarMember { Raw: true, Builtin: false } && specializedIssues.Count == 0)
                return specialized;
            bool genericContract = formatter != null && (RequiresDefault(formatter) || missingElementBody);
            if (!genericContract && specialized != null && specializedIssues.Count == 0 && RenderModelFactory.Member(specialized, GeneratedNames.Extension(Options.AssemblyName), Options) != null)
                return specialized;
            if (formatter != null)
                return new SpecialMember(member.Name, storage, policy, formatter.Type, type.IsReferenceType, storage == MemberStorage.AutoProperty,
                    formatter, reservableSpan: !member.Volatile && formatter.Arguments.Length == 1 && formatter.Arguments[0] is
                        { Raw: true, RawStorage: true, Layout: null, Definition: FormatterDefinitions.Scalar or FormatterDefinitions.FrameworkScalar } &&
                        (formatter.Definition == FormatterDefinitions.Array && type is IArrayTypeSymbol { IsSZArray: true } ||
                         formatter.Definition == "System.Collections.Generic.List" && type is INamedTypeSymbol list &&
                         IsFrameworkCollection(list, "System.Collections.Generic.List`1")),
                    nativeFrameworkScalar: !member.Volatile && type.IsUnmanagedType && type.DeclaringSyntaxReferences.IsEmpty &&
                        formatter is { Definition: FormatterDefinitions.FrameworkScalar, Raw: true });
            issues.AddRange(specializedIssues);
            return specialized;
        }

        /// <summary>Retains complete argument contracts when a specialized collection cannot publish all dependent owners.</summary>
        /// <param name = "model">The normalized formatter or storage contract.</param>
        /// <returns>Whether the requested contract or capability is available.</returns>
        // The specialized scalar owner handles only these four framework types. Other framework scalars need their full
        // formatter graph retained so collections publish the element's JSON owner as well as their raw binary path.
        private static bool RequiresDefault(FormatterModel model) => model.Layout != null || model.Capabilities.Length != 0 ||
            model.Definition == FormatterDefinitions.Registered && model.RawCollection ||
            model.Definition == FormatterDefinitions.FrameworkScalar && model.Type is not
                ("global::System.DateTime" or "global::System.Guid" or "global::System.DateTimeOffset" or "global::System.TimeSpan") ||
            model.PublishPayload || model.Definition is FormatterDefinitions.GenericUnion or FormatterDefinitions.CompilerUnion or FormatterDefinitions.CollectionSubclass ||
            FormatterDefinitions.HasGeneric(model.Definition) ||
            model.Arguments.Any(RequiresDefault);
        /// <summary>Analyzes one closed formatter graph independently of its containing collection.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <param name = "depth">The depth used by this operation.</param>
        /// <param name = "path">The path used by this operation.</param>
        /// <returns>The normalized result of the operation.</returns>
        private FormatterModel? Formatter(ITypeSymbol type, int depth, Path path)
        {
            FormatterModel? model = FormatterCore(type, depth, path);
            return model?.WithCapabilities(Capabilities(type));
        }

        private readonly Dictionary<ITypeSymbol, string[]> _formatterCapabilities =
            new Dictionary<ITypeSymbol, string[]>(SymbolEqualityComparer.Default);

        /// <summary>Captures each closed type's optional runtime API signatures once per compilation.</summary>
        /// <param name="type">The closed formatter type.</param>
        /// <returns>The shared immutable capability names.</returns>
        private string[] Capabilities(ITypeSymbol type)
        {
            lock (_gate)
            {
                if (_formatterCapabilities.TryGetValue(type, out string[]? cached))
                    return cached;
            }
            string[] result = FormatterCapabilities.Capture(_compilation, type, Options.Net8, Options.Unity);
            lock (_gate)
                _formatterCapabilities[type] = result;
            return result;
        }
        /// <summary>Builds a formatter's representation before attaching optional runtime APIs.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <param name = "depth">The depth used by this operation.</param>
        /// <param name = "path">The path used by this operation.</param>
        /// <returns>The normalized result of the operation.</returns>
        private FormatterModel? FormatterCore(ITypeSymbol type, int depth, Path path)
        {
            string spelling = FormatterSpelling(type);
            if (type.IsRefLikeType || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Error)
                return null;
            if (type is ITypeParameterSymbol)
                return new FormatterModel(spelling, FormatterDefinitions.Registered);
            if (type.SpecialType == SpecialType.System_String)
                return new FormatterModel("string", FormatterRegistry.String);
            if (type.SpecialType == SpecialType.System_Object)
                return new FormatterModel(spelling, FormatterDefinitions.Object, ordinaryObject: true);
            if (Scalar(type)is { } primitive)
                return new FormatterModel(primitive.Keyword, FormatterDefinitions.Scalar, raw: true, rawCollection: true);
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying } && Scalar(underlying)is { } enumeration)
                return new FormatterModel(spelling, FormatterDefinitions.Scalar, raw: true, rawCollection: true, underlying: enumeration.Keyword);
            if (FrameworkScalarBody.Contains(spelling))
                return new FormatterModel(spelling, FormatterDefinitions.FrameworkScalar, raw: true, rawCollection: true, formattedJson: HasFormattedJson(type));
            if (type is IArrayTypeSymbol array)
            {
                FormatterModel? element = Formatter(array.ElementType, depth, path);
                if (element == null)
                    return null;
                bool reuseArraySlots = element.Definition == FormatterDefinitions.FrameworkScalar ||
                    element.Definition == FormatterDefinitions.Scalar && element.Underlying == null;
                if (array.Rank == 2)
                {
                    reuseArraySlots |= element.Type == "string" && HasStringMatrixReader(_compilation);
                    reuseArraySlots |= Options.Net8 && element is
                        { OrdinaryObject: true, Definition: FormatterDefinitions.PayloadReference,
                            Payload: RecordModel { Versioned: false, Derived: false, Constructor: null, Callbacks: null, Rent: null, Rejection: null } } &&
                        array.ElementType is INamedTypeSymbol declaration && TypeSwitch(declaration, "ReuseRectangularArraySlots", true);
                }
                return new FormatterModel(spelling, FormatterDefinitions.Array, new[] { element }, rank: array.Rank,
                    reuseArraySlots: reuseArraySlots);
            }

            if (type is not INamedTypeSymbol named)
                return null;
            if (SelectedGeneration(named) is PayloadGeneration.Custom or PayloadGeneration.LegacyManual)
                return new FormatterModel(spelling, FormatterDefinitions.Registered);
            if (IsUnsupportedPackedStorage(named))
                return null;
            if (Owned(named) && CompilerUnion.IsUnion(named, _compilation))
                return CompilerUnionFormatter(named, spelling, depth, path);
            string definition = named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.OriginalDefinition.Name;
            if (FormatterRegistry.HasStandalone(definition))
                return new FormatterModel(spelling, definition);
            bool isNullable = named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
            FormatterStorageModel? layout = isNullable ? null : FormatterStorage(named);
            bool rawCollection = named.IsUnmanagedType && !isNullable && layout == null &&
                !ContainsNonRawContract(named, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
            if (FormatterDefinitions.HasGeneric(definition) || FormatterRegistry.FindSequence(definition) != null || FormatterRegistry.FindDictionary(spelling) != null)
            {
                var arguments = new FormatterModel[named.Arity];
                for (int i = 0; i < arguments.Length; i++)
                {
                    FormatterModel? argument = Formatter(named.TypeArguments[i], depth, path);
                    if (argument == null)
                        return null;
                    arguments[i] = argument;
                }

                if (isNullable)
                {
                    var nullable = NullableCollectionStorage(named, arguments[0]);
                    return new FormatterModel(spelling, definition, arguments, nullableStorage: nullable.Layout,
                        nullableEncoding: nullable.Encoding, unmanagedNullablePayload: named.TypeArguments[0].IsUnmanagedType);
                }
                bool raw = definition == GenericIdentity.ValueTuple && rawCollection;
                return new FormatterModel(spelling, definition, arguments, raw, rawCollection, layout);
            }

            bool ordinary = named.TypeKind == TypeKind.Class && !TracksTypeReferences(named) && SelectedGeneration(named) == PayloadGeneration.Default &&
                !HasAttribute(named, UnionAttribute) && !HasAttribute(named, "System.Runtime.CompilerServices.UnionAttribute");
            if (path.Active.Contains(named))
                return new FormatterModel(spelling, FormatterDefinitions.PayloadReference, ordinaryObject: ordinary);
            // A closed generic Union dispatches through its generic definition owner unless an exact external marker publishes
            // the closed root directly. Both forms use the defining assembly's Union slots.
            bool genericUnion = named.IsGenericType &&
                (HasAttribute(named, UnionAttribute) || IsLocal(named) && (named.IsAbstract || named.TypeKind == TypeKind.Interface));
            // A local abstract base without a usable Union body has no generated slots to forward to; the member falls back to
            // runtime registration instead of a call to a generic entry the definition never published.
            bool unionDefinitionReady = !genericUnion || HasAttribute(named, UnionAttribute) ||
                Analyze(named.OriginalDefinition, depth + 1, path) is { IsComplete: true, Model: UnionModel };
            if (genericUnion && !ContainsTypeParameter(named) && IsPackable(named.OriginalDefinition) && unionDefinitionReady &&
                (IsLocal(named) || !ExplicitMarker(named) && ExplicitMarker(named.OriginalDefinition) &&
                    Analyze(named.OriginalDefinition, depth + 1, path) is { IsComplete: true, Model: UnionModel }))
                return new FormatterModel(spelling, FormatterDefinitions.GenericUnion,
                    payload: Analyze(named, depth + 1, path) is { IsComplete: true, Model: UnionModel closed } ? closed : null,
                    underlying:
                    ReadableTypeName.Of(GeneratedNames.PayloadKey(named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) +
                    "|" + string.Join(", ", named.TypeArguments.Select(argument => argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))),
                    ordinaryObject: false);
            if (CollectionSubclassBase(named) is { } collectionBase && Formatter(collectionBase, depth + 1, path) is { } baseFormatter)
                return new FormatterModel(spelling, FormatterDefinitions.CollectionSubclass, new[] { baseFormatter },
                    underlying: ProfileNames.Type(_assemblyPolicy.Strings), checkedCollectionSubclass: _assemblyPolicy.Input == InputPolicy.Untrusted);
            if (!Owned(named) || genericUnion && !ExplicitMarker(named))
                return new FormatterModel(spelling, FormatterDefinitions.Registered, rawCollection: rawCollection, layout: layout, ordinaryObject: ordinary);
            if (IsPackable(named) || Marked(named))
            {
                // Open assembly markers authorize closed owners reached by calls or members. Their storage is only known after substitution.
                bool local = IsLocal(named);
                if (named.IsGenericType && !local && (ContainsTypeParameter(named) || MissingStorage(named).Length != 0))
                    return null;
                ModelResult result = Analyze(named, depth + 1, path);
                if (result.Model != null && result.IsComplete && CanPublish(result.Model))
                    return new FormatterModel(spelling, FormatterDefinitions.PayloadReference, raw: rawCollection, rawCollection: rawCollection,
                        layout: layout, payload: result.Model, ordinaryObject: ordinary && result.Model is not UnionModel,
                        publishPayload: named.IsGenericType && (local || !ExplicitMarker(named)));
            }

            if (IsImplicitFormatterType(named))
            {
                var localIssues = new List<string>();
                StructModel payload = CreateStructModel(named, Identity(named), TypeAccessibility.Internal, TypePolicy(named), depth + 1, localIssues, path,
                    PayloadGeneration.Default, Array.Empty<string>());
                if (localIssues.Count == 0 && CanPublish(payload))
                    return new FormatterModel(spelling, FormatterDefinitions.PayloadReference, raw: rawCollection, rawCollection: rawCollection, layout: layout, payload: payload, publishPayload: true);
            }

            return null;
        }

        /// <summary>Finds a collection-only source subclass that can be constructed and refilled through its base API.</summary>
        /// <param name="type">The concrete candidate.</param>
        /// <returns>The supported closed collection base, or null.</returns>
        private INamedTypeSymbol? CollectionSubclassBase(INamedTypeSymbol type)
        {
            if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, BaseType.SpecialType: not SpecialType.System_Object } ||
                !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly) || IsPackable(type) ||
                ContainsTypeParameter(type) || Marked(type) ||
                !_compilation.IsSymbolAccessibleWithin(type, _compilation.Assembly) ||
                !type.InstanceConstructors.Any(ctor => ctor.Parameters.IsEmpty && _compilation.IsSymbolAccessibleWithin(ctor, _compilation.Assembly)) ||
                CollectionBaseType(type, includeSortedDictionary: true) is not { } collectionBase)
                return null;
            for (INamedTypeSymbol? level = type; level != null && !SymbolEqualityComparer.Default.Equals(level, collectionBase); level = level.BaseType)
                if (IsPackable(level) || level.GetMembers().Any(member => member is IFieldSymbol { IsStatic: false }))
                    return null;
            return collectionBase;
        }

        /// <summary>Builds the closed case owners in compiler Union declaration order.</summary>
        /// <param name="type">The closed Union struct.</param>
        /// <param name="spelling">The fully qualified Union type name.</param>
        /// <param name="depth">The current formatter expansion depth.</param>
        /// <param name="path">The active formatter expansion path.</param>
        /// <returns>The case graph or a reference when a case leads back to the Union.</returns>
        private FormatterModel? CompilerUnionFormatter(INamedTypeSymbol type, string spelling, int depth, Path path)
        {
            if (ContainsTypeParameter(type) || depth > MaxDepth)
                return null;
            if (!path.Active.Add(type))
                return new FormatterModel(spelling, FormatterDefinitions.CompilerUnion);
            try
            {
                ITypeSymbol[] cases = CompilerUnion.Cases(type, _compilation);
                if (cases.Length == 0 || cases.Length > ushort.MaxValue)
                    return null;
                var arguments = new FormatterModel[cases.Length];
                for (int index = 0; index < cases.Length; index++)
                {
                    FormatterModel? argument = Formatter(cases[index], depth + 1, path);
                    if (argument == null)
                        return null;
                    arguments[index] = argument;
                }
                return new FormatterModel(spelling, FormatterDefinitions.CompilerUnion, arguments);
            }
            finally
            {
                path.Active.Remove(type);
            }
        }

        /// <summary>Requires a real payload implementation before a container calls its owner.</summary>
        /// <param name = "model">The normalized formatter or storage contract.</param>
        /// <returns>Whether the requested contract or capability is available.</returns>
        private bool CanPublish(TypeModel model) => (model is StructModel { StoragePlan: not null } || RenderModelFactory.Create(model, Options) != null) && (model is UnionModel || RenderModelFactory.Create(model, Options, json: true) != null);
        /// <summary>Checks whether any nested value uses a formatter instead of direct unmanaged storage.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <param name = "visited">The visited used by this operation.</param>
        /// <returns>Whether the requested contract or capability is available.</returns>
        private bool ContainsNonRawContract(ITypeSymbol type, HashSet<ITypeSymbol> visited)
        {
            if (!visited.Add(type))
                return false;
            if (type is not INamedTypeSymbol named)
                return false;
            PayloadGeneration generation = SelectedGeneration(named);
            return generation is PayloadGeneration.Custom or PayloadGeneration.LegacyManual ||
                generation == PayloadGeneration.Version && TargetedOptionValue(named, "GeneratorType") != null ||
                named.TypeArguments.Any(item => ContainsNonRawContract(item, visited)) ||
                named.TypeKind == TypeKind.Struct && named.GetMembers().OfType<IFieldSymbol>()
                    .Any(field => !field.IsStatic && ContainsNonRawContract(field.Type, visited));
        }

        /// <summary>Normalizes the established Packed, Native and Tagged layout selection.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <returns>The normalized result of the operation.</returns>
        private FormatterStorageModel? FormatterStorage(INamedTypeSymbol type)
        {
            if (!type.IsUnmanagedType || SelectedGeneration(type) != PayloadGeneration.Default)
                return null;
            int strategy = LayoutStrategy(type);
            PortablePlan? plan;
            if (strategy == 1)
            {
                plan = AnalyzePortable(type, strict: true);
                if (plan?.HasPadding != true)
                    return null;
            }
            else
                plan = TryTaggedPlan(type, strategy);
            return plan == null ? null : NormalizeStorage(plan);
        }

        /// <summary>Models a reference-free nullable's flag and payload once for every binary collection.</summary>
        /// <param name="nullable">The closed nullable element.</param>
        /// <param name="value">The standalone payload's formatter contract.</param>
        /// <returns>The verified storage candidate and its collection encoding.</returns>
        private (FormatterStorageModel? Layout, int Encoding) NullableCollectionStorage(INamedTypeSymbol nullable, FormatterModel value)
        {
            if (!Options.Net8 || Options.Unity || nullable.TypeArguments[0] is not INamedTypeSymbol { IsUnmanagedType: true } payload)
                return default;
            if (!(value.Raw && value.RawStorage && value.Layout == null) &&
                (value.Layout is not { Tagged: true, Volatile: false } ||
                 !(value.Definition == GenericIdentity.ValueTuple || value.Payload is StructModel { Versioned: false }) ||
                 ContainsNonRawContract(payload, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default))))
                return default;

            int strategy = LayoutStrategy(payload);
            if (strategy == 1 || !FormatterRuntimeTraits.HasNullableCollectionStorage(_compilation))
                return default;

            if (AnalyzePortable(payload, strict: true) is not { WireSize: > 0 } payloadPlan || HasVolatile(payloadPlan) ||
                // The raw reader validates a scalar Boolean payload and the outer presence flag.
                payloadPlan.Fields != null && ContainsBooleanStorage(payloadPlan))
                return default;

            ITypeSymbol boolean = _compilation.GetSpecialType(SpecialType.System_Boolean);
            var plan = new PortablePlan
            {
                Type = nullable,
                Fields = new[]
                {
                    new PortablePlanField { Type = boolean, Name = "hasValue" },
                    new PortablePlanField { Type = payload, Name = "value" }
                },
                Children = new[] { AnalyzePortable(boolean, strict: true)!, payloadPlan },
                WireSize = checked(1 + payloadPlan.WireSize),
                Tagged = true
            };
            plan.Models = BuildModels(plan);
            foreach (PortableStorageModel model in plan.Models)
            {
                if (model.Size <= 0)
                    return default;
                var used = new HashSet<int>();
                foreach (int offset in model.Offsets)
                    if ((uint)offset >= (uint)model.Size || !used.Add(offset))
                        return default;
            }
            return (NormalizeStorage(plan, FormatterSpelling(payload)), strategy == 2 ? 2 : 1);
        }

        /// <summary>Detects Boolean domains which need extra validation before publishing a compound raw value.</summary>
        /// <param name="plan">The supported compiler storage model.</param>
        /// <returns>Whether the storage contains a Boolean leaf.</returns>
        private static bool ContainsBooleanStorage(PortablePlan plan) =>
            plan.Type.SpecialType == SpecialType.System_Boolean ||
            plan.Type is INamedTypeSymbol { EnumUnderlyingType.SpecialType: SpecialType.System_Boolean } ||
            (plan.Children != null && plan.Children.Any(ContainsBooleanStorage));

        /// <summary>Copies compiler storage plans into value-equal renderer input.</summary>
        /// <param name = "plan">The plan used by this operation.</param>
        /// <param name="nullablePayload">The wrapper payload whose actual CLR field address will be verified.</param>
        /// <returns>The normalized result of the operation.</returns>
        private static FormatterStorageModel NormalizeStorage(PortablePlan plan, string? nullablePayload = null)
        {
            var fields = plan.Fields == null ? Array.Empty<FormatterStorageField>() : plan.Fields.Select((field, index) => new FormatterStorageField(NormalizeStorage(plan.Children![index]), field.Offset, field.IsVolatile, field.Name)).ToArray();
            return new FormatterStorageModel(FormatterSpelling(plan.Type), plan.Type is INamedTypeSymbol named ? LayoutAttribute(named) : null, fields, plan.WireSize, plan.Tagged, HasVolatile(plan), plan.Models.Select(model => model.Size).ToArray(), plan.Models.Select(model => model.Offsets).ToArray(), direct: IsValueTuple(plan.Type), aggregate: plan.Fields != null, nullablePayload: nullablePayload);
        }

        /// <summary>Checks the runtime reference's public formatted-scalar writer overload.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <returns>Whether the requested contract or capability is available.</returns>
        private bool HasFormattedJson(ITypeSymbol type) => _compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?.GetMembers("Write").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 1 && method.ReturnsVoid && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true;
        /// <summary>Canonicalizes nullable values, tuple syntax and native integer aliases.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <returns>The generated source or selected source-ready name.</returns>
        internal static string FormatterSpelling(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                ITypeSymbol element = array;
                var ranks = new System.Text.StringBuilder();
                while (element is IArrayTypeSymbol level)
                {
                    ranks.Append('[').Append(',', level.Rank - 1).Append(']');
                    element = level.ElementType;
                }

                return FormatterSpelling(element) + ranks;
            }

            if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                string prefix = named.ContainingType != null ? FormatterSpelling(named.ContainingType) + "." : named.ContainingNamespace.IsGlobalNamespace ? "global::" : "global::" + named.ContainingNamespace.ToDisplayString() + ".";
                // A type generic only through its containing type spells the arguments on that level.
                return named.Arity == 0 ? prefix + named.Name :
                    prefix + named.Name + "<" + string.Join(", ", named.TypeArguments.Select(FormatterSpelling)) + ">";
            }

            return FormatterTypeName(type);
        }
    }
}
