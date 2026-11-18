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
    /// <summary>The scalar and framework value spellings the original entries copy as complete raw storage.</summary>
    private static readonly HashSet<string> KnownValueTypes = new(StringComparer.Ordinal)
    {
        "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "bool", "char", "nint", "nuint",
        "global::System.Guid", "global::System.DateTime", "global::System.DateTimeOffset", "global::System.TimeSpan",
        "global::System.Numerics.Complex", "global::System.Numerics.Plane", "global::System.Numerics.Quaternion",
        "global::System.Numerics.Matrix3x2", "global::System.Numerics.Matrix4x4", "global::System.Numerics.Vector2",
        "global::System.Numerics.Vector3", "global::System.Numerics.Vector4", "global::System.Half", "global::System.Int128",
        "global::System.UInt128", "global::System.DateOnly", "global::System.TimeOnly", "global::System.Text.Rune",
        "global::System.Runtime.InteropServices.GCHandle",
    };

    /// <summary>Classifies the allocating write and span read of a closed root payload.</summary>
    /// <param name="type">The closed payload.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The entry shape.</returns>
    internal static EntryShape EntryShapeOf(ITypeSymbol type, Compilation compilation, GeneratorOptions options)
    {
        Compilation view = Complete(compilation);
        if (!ReferenceEquals(view, compilation))
        {
            type = InView(type, view);
            compilation = view;
        }
        Session session = Sessions.GetValue(compilation, item => new Session(item, options));
        return (session.Options.Equals(options) ? session : new Session(compilation, options)).EntryShapeOf(type);
    }

    private sealed partial class Session
    {
        /// <summary>Resolves the declared storage strategy of a collection's unmanaged payload.</summary>
        /// <param name="payload">The reference-free value wrapped by the nullable element.</param>
        /// <returns>The existing Tagged, Packed, or Native strategy value.</returns>
        private int CollectionLayoutStrategy(INamedTypeSymbol payload)
        {
            if (ReadStructLayoutOption(payload.GetAttributes()) is int declared)
                return declared;
            var assembly = payload.ContainingAssembly;
            if (payload.SpecialType != SpecialType.None || payload.TypeKind == TypeKind.Enum || IsValueTuple(payload) ||
                FrameworkScalarBody.Contains(FormatterName(payload)))
                assembly = _compilation.Assembly;
            return ReadStructLayoutOption(assembly.GetAttributes()) ?? 0;
        }

        /// <summary>Determines the read and write shapes independently from the published root contract.</summary>
        /// <param name="type">The closed root type.</param>
        /// <returns>The closed entry shape.</returns>
        internal EntryShape EntryShapeOf(ITypeSymbol type)
        {
            // Entries stage their input when the reader cannot hold a managed span: before .NET 8 or against a netstandard Core.
            if (!Options.Net8 || Options.PointerCursor || ReaderTargetsNetStandard())
                return EntryShape.Pooled;
            MemberModel? member = ClassifyRoot(type);
            TypeModel? payload = (member as SpecialMember)?.Formatter?.Payload;
            if (member == null && type is INamedTypeSymbol named && (IsPackable(named) || Marked(named)) &&
                Create(named, depth: 0) is { IsComplete: true } declared)
                payload = declared.Model;

            EntryShape write = EntryWriteShape(type, member, payload);
            bool stateless = payload == null ? IsStatelessMember(member) : IsStatelessPayload(payload);
            bool nullObject = payload is RecordModel
                { Callbacks: null, Recursive: false, Versioned: false, SequenceBase: null, Rejection: null } &&
                !payload.Policy.TrackReferences && payload.Policy.Bound == null && !payload.Policy.Limits.Explicit;
            return new EntryShape(write.Kind, write.Element, write.Width, stateless, nullObject, HasRetainedDirectWrite,
                outlineNonNullRead: nullObject && HasLargeRootRead(payload!));
        }

        /// <summary>Estimates when a positional reader has enough work to outline the nonnull path.</summary>
        /// <param name="payload">The normalized record or nested payload.</param>
        /// <returns>Whether the payload has a wide record body, collection read or Union dispatch.</returns>
        private static bool HasLargeRootRead(TypeModel payload) =>
            // Member count is a conservative proxy for the size of a lowered record, independent of the target JIT.
            payload.Members.Length >= 8 || payload.Members.Any(member => member switch
            {
                CollectionMember or Model.DictionaryMember or PayloadMember { Nested: UnionModel } => true,
                PayloadMember { Shape: PayloadShape.Inline, Nested: { } nested } => HasLargeRootRead(nested),
                SpecialMember { Formatter: { } formatter } => HasLargeFormatterRead(formatter),
                _ => false,
            });

        /// <summary>Recognizes collection and Union reads behind shared formatter contracts.</summary>
        /// <param name="formatter">The normalized formatter and its static dependencies.</param>
        /// <returns>Whether the formatter reaches a collection read or a Union dispatch.</returns>
        private static bool HasLargeFormatterRead(FormatterModel formatter) =>
            formatter.Payload is UnionModel ||
            formatter.Definition is FormatterDefinitions.Array or FormatterDefinitions.CompilerUnion or FormatterDefinitions.GenericUnion or
                GenericIdentity.Grouping or GenericIdentity.Lookup ||
            FormatterDefinitions.TryContiguous(formatter.Definition, out _) ||
            FormatterRegistry.FindSequence(formatter.Definition) != null ||
            FormatterRegistry.FindDictionary(formatter.Type) != null ||
            formatter.Arguments.Any(HasLargeFormatterRead);

        private bool? _hasRetainedDirectWrite;
        private bool? _hasScalarHeader;

        /// <summary>Checks the referenced Core before using its combined marker and scalar field write.</summary>
        private bool HasScalarHeader => _hasScalarHeader ??=
            _compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter")?.GetMembers("WriteObjectHeader")
                .OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                    method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.None &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_Byte && method.Parameters[1].RefKind == RefKind.In &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, method.TypeParameters[0])) == true;

        /// <summary>Checks the referenced Core for the retained root storage API before emitting a call to it.</summary>
        private bool HasRetainedDirectWrite => _hasRetainedDirectWrite ??=
            _compilation.GetTypeByMetadataName("LuminPack.Utility.LuminBufferWriter")?.GetMembers("TryBeginFixedRoot")
                .OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.DeclaredAccessibility == Accessibility.Public &&
                    method.Arity == 0 && method.ReturnsByRef && method.ReturnType.SpecialType == SpecialType.System_Byte &&
                    method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_Int32) == true;

        /// <summary>Checks the target Core's address-only bit storage writer.</summary>
        /// <param name="bitArray">The resolved framework bit array.</param>
        /// <returns>Whether the target can encode the modern word-rounded payload into reserved storage.</returns>
        private bool CanWriteReservedBits(INamedTypeSymbol bitArray) =>
            _compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess")?.GetMembers("WriteReservedBitArray")
                .OfType<IMethodSymbol>().Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public &&
                    method.ReturnsVoid && method.Arity == 0 && method.Parameters.Length == 3 &&
                    method.Parameters[0].RefKind == RefKind.Ref && method.Parameters[0].Type.SpecialType == SpecialType.System_Byte &&
                    method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, bitArray) &&
                    method.Parameters[2].RefKind == RefKind.None && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32) == true;

        /// <summary>Selects an allocating write only when its width matches the published writer's representation.</summary>
        /// <param name="type">The closed root type.</param>
        /// <param name="member">The normalized shared root, if one is published.</param>
        /// <param name="payload">The normalized payload, if one is published.</param>
        /// <returns>The write shape before the separate reader-state proof.</returns>
        private EntryShape EntryWriteShape(ITypeSymbol type, MemberModel? member, TypeModel? payload)
        {
            bool containsReferences = !CanUseUnmanagedSerialization(type);
            string name = FormatterName(type);
            if (!containsReferences && (KnownValueTypes.Contains(name) || payload is StructModel
                { Storage: StructStorage.RawUnmanaged, StoragePlan: null, Layout: null, Versioned: false, Rejection: null }))
                return new EntryShape(EntryKind.RawScalar);
            if (payload is StructModel { Storage: StructStorage.PortableUnmanaged, Rejection: null } structure)
            {
                if (type is INamedTypeSymbol { IsGenericType: false } && UnmanagedOwnerFile.CanWriteFixedRoot(structure))
                    return new EntryShape(EntryKind.FixedWidthRecord,
                        ReadableTypeName.Of(GeneratedNames.PayloadKey(structure.Identity.GlobalName)), structure.StoragePlan!.WireSize);
                if (structure.Layout is { Complete: true } layout)
                    return new EntryShape(EntryKind.FixedWidthRecord, width: layout.WireSize);
            }
            if (name == "global::System.TimeZoneInfo")
                return new EntryShape(EntryKind.TimeZoneSnapshot);
            if (name == "global::System.Version" && payload == null && type.DeclaringSyntaxReferences.IsEmpty)
                return new EntryShape(EntryKind.FixedVersion);
            if (name == "string")
                return new EntryShape(EntryKind.RootString);
            if (name == "global::System.Uri" && payload == null && type.DeclaringSyntaxReferences.IsEmpty)
                return new EntryShape(EntryKind.RootUri);
            if (name == "global::System.Globalization.CultureInfo" && payload == null && type.DeclaringSyntaxReferences.IsEmpty)
                return new EntryShape(EntryKind.RootCulture);
            if (type is INamedTypeSymbol { IsSealed: true, IsGenericType: false } && FixedRootObject(payload) is { } fixedRoot)
                return new EntryShape(EntryKind.FixedObject, fixedRoot.Readable);
            if (type is INamedTypeSymbol { IsSealed: true, IsGenericType: false } &&
                payload is RecordModel { Implicit: false, Derived: false } &&
                RenderModelFactory.Create(payload, Options) is { } boundedRoot && BinaryEmitter.CanReserveBoundedNullableObject(boundedRoot))
                return new EntryShape(EntryKind.BoundedNullableObject, boundedRoot.Readable);
            if (type is IArrayTypeSymbol { IsSZArray: true } array)
            {
                if (HasRetainedDirectWrite && IsKnownScalar(array.ElementType))
                    return new EntryShape(EntryKind.RawArray, FormatterName(array.ElementType));
                if (member is SpecialMember { Formatter.Arguments.Length: 1 } arrayMember)
                {
                    FormatterModel item = arrayMember.Formatter!.Arguments[0];
                    if (HasRetainedDirectWrite && item is { RawCollection: true, Layout: null,
                        Payload: StructModel { Storage: StructStorage.RawUnmanaged, Versioned: false, Rejection: null } })
                        return new EntryShape(EntryKind.RawArray, item.Type);
                    if (array.ElementType is INamedTypeSymbol { IsSealed: true, IsGenericType: false } &&
                        item.OrdinaryObject && FixedRootObject(item.Payload) is { } fixedElement)
                        return new EntryShape(EntryKind.FixedObjectArray, fixedElement.Readable);
                }
                if (member is CollectionMember { Nested: { } nested } && array.ElementType is INamedTypeSymbol
                    { IsSealed: true, IsGenericType: false } && FixedRootObject(nested) is { } fixedNested)
                    return new EntryShape(EntryKind.FixedObjectArray, fixedNested.Readable);
            }
            if (HasRetainedDirectWrite && type is INamedTypeSymbol { TypeArguments.Length: 1 } list &&
                IsFrameworkCollection(list, "System.Collections.Generic.List`1") && IsKnownScalar(list.TypeArguments[0]))
                return new EntryShape(EntryKind.RawList, FormatterName(list.TypeArguments[0]));
            if (type is INamedTypeSymbol { Name: "PriorityQueue", TypeArguments.Length: 2 } heap &&
                IsFrameworkCollection(heap, "System.Collections.Generic.PriorityQueue`2") &&
                FormatterOrderedCollectionTraits.HasPriorityAllocatingWriter(_compilation, heap))
                return new EntryShape(EntryKind.PackedPriorityQueue);
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            {
                ITypeSymbol element = nullable.TypeArguments[0];
                if (IsKnownScalar(element))
                    return new EntryShape(EntryKind.NullableDirect, FormatterName(element));
                if (member is SpecialMember { Formatter.Arguments.Length: 1 } nullableMember)
                {
                    FormatterModel argument = nullableMember.Formatter!.Arguments[0];
                    if (argument is { Definition: FormatterDefinitions.Scalar, Raw: true } || argument.Payload is StructModel
                        { Storage: StructStorage.RawUnmanaged, StoragePlan: null, Layout: null, Rejection: null })
                        return new EntryShape(EntryKind.NullableDirect, FormatterName(element));
                    if (argument.Payload is StructModel
                        { Storage: StructStorage.PortableUnmanaged, Layout: { Complete: true } itemLayout, Rejection: null })
                        return new EntryShape(EntryKind.NullableDirect, FormatterName(element), itemLayout.WireSize);
                }
                return EntryShape.Pooled;
            }
            bool fixedEnum = type.TypeKind == TypeKind.Enum && !containsReferences;
            bool fixedRecord = IsFixedRecord(type) || fixedEnum;
            if (fixedRecord || IsDirectRecord(type))
            {
                if (fixedEnum)
                    return new EntryShape(EntryKind.FixedEnum);
                if (name == "global::System.Collections.BitArray" && type is INamedTypeSymbol bitArray &&
                    IsFrameworkCollection(bitArray, "System.Collections.BitArray") && CanWriteReservedBits(bitArray))
                    return new EntryShape(EntryKind.BitArray);
                if (name == "global::System.Numerics.BigInteger")
                    return new EntryShape(EntryKind.BigInteger);
                if (!fixedRecord && type is INamedTypeSymbol compound)
                {
                    string definition = compound.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + compound.Name;
                    if (definition is GenericIdentity.Tuple or GenericIdentity.ValueTuple or GenericIdentity.KeyValuePair or GenericIdentity.Lazy)
                        return new EntryShape(EntryKind.CompoundDirect);
                }
                return new EntryShape(fixedRecord ? EntryKind.FixedRecord : EntryKind.DirectRecord);
            }
            if (type is INamedTypeSymbol { TypeArguments.Length: 1 } sequence &&
                sequence.OriginalDefinition.ToDisplayString() == "System.Buffers.ReadOnlySequence<T>" && IsKnownScalar(sequence.TypeArguments[0]))
                return new EntryShape(EntryKind.RawSequence, FormatterName(sequence.TypeArguments[0]));
            if (type is INamedTypeSymbol { TypeArguments.Length: 1, TypeKind: TypeKind.Interface } collection)
            {
                string definition = collection.OriginalDefinition.ToDisplayString();
                ITypeSymbol element = collection.TypeArguments[0];
                bool exactCollection = definition is "System.Collections.Generic.IEnumerable<T>" or "System.Collections.Generic.ICollection<T>" or
                    "System.Collections.Generic.IReadOnlyList<T>" or "System.Collections.Generic.IReadOnlyCollection<T>" &&
                    IsFrameworkCollection(collection, "System.Collections.Generic." + collection.Name + "`1");
                if (exactCollection || definition is "System.Collections.Generic.IList<T>" or "System.Collections.Generic.ISet<T>" or
                    "System.Collections.Generic.IReadOnlySet<T>")
                {
                    string spelling = FormatterName(element);
                    // The original exact-interface path admits supported framework scalars with native storage.
                    if ((element.SpecialType != SpecialType.None || spelling != "global::System.Runtime.InteropServices.GCHandle" &&
                         FrameworkScalarBody.Contains(spelling)) && KnownValueTypes.Contains(spelling) && CanUseUnmanagedSerialization(element))
                    {
                        if (exactCollection)
                            return new EntryShape(EntryKind.ExactCapturedList, spelling);
                        EntryKind kind = definition == "System.Collections.Generic.IList<T>" ? EntryKind.ExactList :
                            FormatterRuntimeTraits.CanGroupSmallHashSetWrites(element, _compilation) ? EntryKind.ExactSmallHashSet : EntryKind.ExactHashSet;
                        return new EntryShape(kind, spelling);
                    }
                }
            }
            return EntryShape.Pooled;
        }

        /// <summary>Finds a published positional class writer with an exact callback-free fixed width.</summary>
        /// <param name="payload">The normalized class contract.</param>
        /// <returns>The fixed record model, or null when the ordinary writer must be used.</returns>
        private PayloadRenderModel? FixedRootObject(TypeModel? payload) =>
            payload is RecordModel { Implicit: false, Derived: false, Versioned: false, SequenceBase: null } &&
            RenderModelFactory.Create(payload, Options) is { } model && BinaryEmitter.CanReserveFixedObject(model) ? model : null;

        /// <summary>Proves a generated payload can be read without a preinitialized operation context.</summary>
        /// <param name="payload">The fully modeled generated payload.</param>
        /// <returns>Whether all its binary readers are known to leave optional state unused.</returns>
        private static bool IsStatelessPayload(TypeModel payload) => payload switch
        {
            // The raw and portable binary operations do not visit the modeled JSON members.
            StructModel { Storage: StructStorage.RawUnmanaged or StructStorage.PortableUnmanaged } => true,
            StructModel { Versioned: false } => payload.Members.All(IsStatelessMember),
            RecordModel { Versioned: false, SequenceBase: null } => payload.Members.All(IsStatelessMember),
            // A Union may dispatch a registered case; reference-tracked nodes always share a table.
            _ => false,
        };

        /// <summary>Follows the binary dependencies carried by a specialized or forwarded member.</summary>
        /// <param name="member">The member or shared root contract.</param>
        /// <returns>Whether the modeled reader proves state is unnecessary.</returns>
        private static bool IsStatelessMember(MemberModel? member) => member switch
        {
            ScalarMember or TextMember or CultureInfoMember => true,
            SpecialMember { Formatter: { } formatter } => IsStatelessFormatter(formatter),
            SpecialMember special => FormatterRegistry.HasStandalone(special.Declared.Replace("global::", string.Empty)),
            PayloadMember { Shape: not PayloadShape.ReferenceTracked, Nested: { } nested } => IsStatelessPayload(nested),
            CollectionMember { Nested: { } nested } => IsStatelessPayload(nested),
            CollectionMember { ForwardedElement: { } forwarded } => IsStatelessMember(forwarded),
            CollectionMember { Scalar: not null } => true,
            DictionaryMember dictionary => (dictionary.KeyText || dictionary.KeyType != null) &&
                (dictionary.Nested is { } nested ? IsStatelessPayload(nested) : dictionary.ForwardedValue is { } forwarded
                    ? IsStatelessMember(forwarded) : dictionary.ValueText || dictionary.ValueType != null),
            _ => false,
        };

        /// <summary>Follows the binary dependencies of a general formatter owner.</summary>
        /// <param name="formatter">The formatter's normalized contract.</param>
        /// <returns>Whether the owner and its children are statically known to be state free.</returns>
        private static bool IsStatelessFormatter(FormatterModel formatter)
        {
            // Opaque registrations, unresolved recursive references and runtime-selected objects keep the lease.
            if (formatter.Definition is FormatterDefinitions.Registered or FormatterDefinitions.GenericUnion or FormatterDefinitions.Object)
                return false;
            if (formatter.Definition == FormatterDefinitions.PayloadReference)
                return formatter.Payload is { } payload && IsStatelessPayload(payload);
            return formatter.Arguments.All(IsStatelessFormatter);
        }

        /// <summary>Recognizes the actual framework definition rather than a source or reference with the same type name.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <param name="metadataName">The name of the metadata.</param>
        /// <returns>Whether the input is framework collection.</returns>
        private bool IsFrameworkCollection(INamedTypeSymbol type, string metadataName)
        {
            IAssemblySymbol assembly = type.ContainingAssembly;
            if (!SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _compilation.GetTypeByMetadataName(metadataName)))
                return false;
            return SymbolEqualityComparer.Default.Equals(assembly, _compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly) ||
                assembly.Identity.Name == "System.Collections" && assembly.Identity.PublicKeyToken.SequenceEqual(
                    new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a });
        }

        /// <summary>Returns the reader targets net standard for <c>Session</c>.</summary>
        /// <returns>Whether the input satisfies the reader targets net standard check.</returns>
        private bool ReaderTargetsNetStandard() => _compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader")?.ContainingAssembly
            .GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.Versioning.TargetFrameworkAttribute" &&
                attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string framework &&
                framework.StartsWith(".NETStandard", StringComparison.Ordinal)) == true;

        /// <summary>The original formatter spelling: a keyword for special types, otherwise fully qualified.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>The ter name.</returns>
        private static string FormatterName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.ExpandNullable));

        /// <summary>Whether a reference-free value is serialized as its complete storage: no portable plan and no Custom contract.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>Whether the operation can use unmanaged serialization.</returns>
        private bool CanUseUnmanagedSerialization(ITypeSymbol type) => type.IsUnmanagedType && !ContainsOpenParameter(type) &&
            (type is not INamedTypeSymbol named || TryPortableLayout(named) == null) &&
            !ContainsCustomContract(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

        /// <summary>Checks whether known scalar.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>Whether the input is known scalar.</returns>
        private bool IsKnownScalar(ITypeSymbol type) => KnownValueTypes.Contains(FormatterName(type)) && CanUseUnmanagedSerialization(type);

        /// <summary>Checks for open parameter.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>Whether the input satisfies the contains open parameter check.</returns>
        private static bool ContainsOpenParameter(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsOpenParameter(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.Any(ContainsOpenParameter),
            _ => false,
        };

        /// <summary>The original direct-record rule: strings, scalar sequences and a few framework records size their output exactly.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>Whether the input is direct record.</returns>
        private bool IsDirectRecord(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol { Rank: 1 } array)
                return IsKnownScalar(array.ElementType);
            if (type is not INamedTypeSymbol named)
                return false;
            if (named.SpecialType == SpecialType.System_String)
                return true;
            if (named.SpecialType == SpecialType.None && named.ToDisplayString() is "System.Version" or "System.Numerics.BigInteger" or
                    "System.Uri" or "System.Globalization.CultureInfo" or "System.Text.StringBuilder" or "System.Collections.BitArray")
                return true;
            string definition = named.OriginalDefinition.ToDisplayString();
            if (named.TypeArguments.Length == 1 && definition is "System.Collections.Generic.List<T>" or "System.Collections.Generic.HashSet<T>" or
                    "System.Collections.Generic.Stack<T>" or "System.Collections.Generic.Queue<T>" or "System.Collections.Generic.LinkedList<T>" or
                    "System.Collections.Generic.SortedSet<T>" or "System.Collections.Immutable.ImmutableHashSet<T>" or
                    "System.Collections.Immutable.ImmutableSortedSet<T>" or "System.ArraySegment<T>" or "System.Memory<T>" or
                    "System.ReadOnlyMemory<T>" or "System.Collections.Immutable.ImmutableArray<T>")
                return IsKnownScalar(named.TypeArguments[0]);
            if (named.TypeArguments.Length == 1 && definition == "System.Lazy<T>")
                return IsKnownScalar(named.TypeArguments[0]);
            return named.TypeArguments.Length > 0 &&
                named.TypeArguments.All(argument => argument.SpecialType == SpecialType.System_String || IsKnownScalar(argument)) &&
                (definition.StartsWith("System.Collections.Generic.KeyValuePair<", StringComparison.Ordinal) || named.IsTupleType ||
                 definition.StartsWith("System.Tuple<", StringComparison.Ordinal));
        }

        /// <summary>The original fixed-record rule: an object or named-version record of fixed-width primitive members only.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        /// <returns>Whether the input is fixed record.</returns>
        private static bool IsFixedRecord(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol named || named.TypeKind is not (TypeKind.Class or TypeKind.Struct) || named.IsAbstract ||
                named.IsStatic || named.IsRefLikeType || ContainsOpenParameter(named))
                return false;
            AttributeData? packable = named.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == PackableAttribute);
            if (packable is null || packable.ConstructorArguments.Length == 1 &&
                !(packable.ConstructorArguments[0].Kind == TypedConstantKind.Enum && Convert.ToInt32(packable.ConstructorArguments[0].Value) is 0 or 1))
                return false;
            for (INamedTypeSymbol? current = named; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
                 current = current.BaseType)
            {
                if (!current.Locations.Any(location => location.IsInSource))
                    return false;
                foreach (AttributeData attribute in current.GetAttributes())
                    if (attribute.AttributeClass?.ToDisplayString() is UnionAttribute or "System.Runtime.CompilerServices.UnionAttribute" or
                        "LuminPack.Attribute.LuminPackCompressAttribute" or "LuminPack.Attribute.LuminPackWideTagAttribute" or
                        "LuminPack.Attribute.LuminPackSingleImplementationAttribute")
                        return false;
                if (OptionSwitch(current, "WideTag") || OptionSwitch(current, "SingleImplementation"))
                    return false;
                if (!SymbolEqualityComparer.Default.Equals(current, named) && current.GetAttributes().Any(attribute =>
                        attribute.AttributeClass?.ToDisplayString() == PackableAttribute && attribute.ConstructorArguments.Length == 1 &&
                        !(attribute.ConstructorArguments[0].Kind == TypedConstantKind.Enum && Convert.ToInt32(attribute.ConstructorArguments[0].Value) == 0)))
                    return false;
                foreach (ISymbol member in current.GetMembers())
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
                    if (memberType is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration)
                        memberType = enumeration.EnumUnderlyingType!;
                    if (memberType?.SpecialType is not (SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or
                        SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
                        SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or
                        SpecialType.System_Double))
                        return false;
                }
            }
            return true;
        }
    }
}
