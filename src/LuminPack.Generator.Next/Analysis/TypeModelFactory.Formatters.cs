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
                return new SpecialMember(member.Name, storage, policy, formatter.Type, type.IsReferenceType, storage == MemberStorage.AutoProperty, formatter);
            issues.AddRange(specializedIssues);
            return specialized;
        }

        /// <summary>Retains complete argument contracts when a specialized collection cannot publish all dependent owners.</summary>
        /// <param name = "model">The normalized formatter or storage contract.</param>
        /// <returns>Whether the requested contract or capability is available.</returns>
        private static bool RequiresDefault(FormatterModel model) => model.Layout != null || model.Capabilities.Length != 0 ||
            model.PublishPayload || model.Definition == FormatterDefinitions.GenericUnion || FormatterDefinitions.HasGeneric(model.Definition) ||
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
                return element == null ? null : new FormatterModel(spelling, FormatterDefinitions.Array, new[] { element }, rank: array.Rank, reuseArraySlots: element.Definition == FormatterDefinitions.FrameworkScalar || element.Definition == FormatterDefinitions.Scalar && element.Underlying == null || array.Rank == 2 && element.Type == "string" && HasStringMatrixReader(_compilation));
            }

            if (type is not INamedTypeSymbol named)
                return null;
            if (GenerationMode(named)is "Custom" or "NonGenerator")
                return new FormatterModel(spelling, FormatterDefinitions.Registered);
            string definition = named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.OriginalDefinition.Name;
            if (FormatterRegistry.HasStandalone(definition))
                return new FormatterModel(spelling, definition);
            FormatterStorageModel? layout = FormatterStorage(named);
            // Nullable<T> is not an unmanaged type argument, so its collections keep their element operations.
            bool rawCollection = named.IsUnmanagedType && named.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T && layout == null &&
                !ContainsCustomContract(named, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
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

                bool raw = definition == GenericIdentity.ValueTuple && rawCollection;
                return new FormatterModel(spelling, definition, arguments, raw, rawCollection, layout);
            }

            bool ordinary = named.TypeKind == TypeKind.Class && GenerationMode(named) == "Object" && !HasAttribute(named, UnionAttribute);
            if (path.Active.Contains(named))
                return new FormatterModel(spelling, FormatterDefinitions.PayloadReference, ordinaryObject: ordinary);
            // A closed generic Union dispatches through its generic definition owner unless an exact external marker publishes
            // the closed root directly. Both forms use the defining assembly's Union slots.
            bool genericUnion = named.IsGenericType &&
                (HasAttribute(named, UnionAttribute) || IsLocal(named) && (named.IsAbstract || named.TypeKind == TypeKind.Interface));
            if (genericUnion && !ContainsTypeParameter(named) && IsPackable(named.OriginalDefinition) &&
                (IsLocal(named) || !ExplicitMarker(named) && ExplicitMarker(named.OriginalDefinition) &&
                    Analyze(named.OriginalDefinition, depth + 1, path) is { IsComplete: true, Model: UnionModel }))
                return new FormatterModel(spelling, FormatterDefinitions.GenericUnion,
                    payload: IsLocal(named) && Analyze(named, depth + 1, path) is { IsComplete: true, Model: UnionModel closed } ? closed : null,
                    underlying:
                    ReadableTypeName.Of(GeneratedNames.PayloadKey(named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) +
                    "|" + string.Join(", ", named.TypeArguments.Select(argument => argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))),
                    ordinaryObject: false);
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
                        layout: layout, payload: result.Model, ordinaryObject: ordinary, publishPayload: named.IsGenericType && (local || !ExplicitMarker(named)));
            }

            if (IsImplicitFormatterType(named))
            {
                var localIssues = new List<string>();
                StructModel payload = CreateStructModel(named, Identity(named), TypeAccessibility.Internal, TypePolicy(named, PayloadKind.Struct), depth + 1, localIssues, path, "Object", Array.Empty<string>());
                if (localIssues.Count == 0 && CanPublish(payload))
                    return new FormatterModel(spelling, FormatterDefinitions.PayloadReference, raw: rawCollection, rawCollection: rawCollection, layout: layout, payload: payload, publishPayload: true);
            }

            return null;
        }

        /// <summary>Requires a real payload implementation before a container calls its owner.</summary>
        /// <param name = "model">The normalized formatter or storage contract.</param>
        /// <returns>Whether the requested contract or capability is available.</returns>
        private bool CanPublish(TypeModel model) => (model is StructModel { StoragePlan: not null } || RenderModelFactory.Create(model, Options) != null) && (model is UnionModel || RenderModelFactory.Create(model, Options, json: true) != null);
        /// <summary>Checks complete unmanaged storage for explicit custom contracts.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <param name = "visited">The visited used by this operation.</param>
        /// <returns>Whether the requested contract or capability is available.</returns>
        private static bool ContainsCustomContract(ITypeSymbol type, HashSet<ITypeSymbol> visited)
        {
            if (!visited.Add(type))
                return false;
            if (type is not INamedTypeSymbol named)
                return false;
            return GenerationMode(named)is "Custom" or "NonGenerator" || named.TypeArguments.Any(item => ContainsCustomContract(item, visited)) || named.TypeKind == TypeKind.Struct && named.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic && ContainsCustomContract(field.Type, visited));
        }

        /// <summary>Normalizes the established Packed, Native and Tagged layout selection.</summary>
        /// <param name = "type">The declared type or its source-ready spelling.</param>
        /// <returns>The normalized result of the operation.</returns>
        private FormatterStorageModel? FormatterStorage(INamedTypeSymbol type)
        {
            if (!type.IsUnmanagedType || GenerationMode(type) != "Object")
                return null;
            PortablePlan? plan;
            if (LayoutStrategy(type) == 1)
            {
                plan = AnalyzePortable(type, strict: true);
                if (plan?.HasPadding != true)
                    return null;
            }
            else
                plan = TryTaggedPlan(type);
            return plan == null ? null : NormalizeStorage(plan);
        }

        /// <summary>Copies compiler storage plans into value-equal renderer input.</summary>
        /// <param name = "plan">The plan used by this operation.</param>
        /// <returns>The normalized result of the operation.</returns>
        private static FormatterStorageModel NormalizeStorage(PortablePlan plan)
        {
            var fields = plan.Fields == null ? Array.Empty<FormatterStorageField>() : plan.Fields.Select((field, index) => new FormatterStorageField(NormalizeStorage(plan.Children![index]), field.Offset, field.IsVolatile, field.Name)).ToArray();
            return new FormatterStorageModel(FormatterSpelling(plan.Type), plan.Type is INamedTypeSymbol named ? LayoutAttribute(named) : null, fields, plan.WireSize, plan.Tagged, HasVolatile(plan), plan.Models.Select(model => model.Size).ToArray(), plan.Models.Select(model => model.Offsets).ToArray(), direct: IsValueTuple(plan.Type), aggregate: plan.Fields != null);
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
