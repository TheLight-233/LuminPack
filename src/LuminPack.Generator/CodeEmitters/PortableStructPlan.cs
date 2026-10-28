using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>One storage field of an analyzed aggregate, independent of whether the compiler exposes the real field symbol.</summary>
internal sealed class PortableField
{
    internal ITypeSymbol Type;
    internal string Name;
    internal bool IsVolatile;
    internal int Offset = -1;
}

/// <summary>One generated native layout model: the element width and where each packed data byte sits in it.</summary>
internal sealed class StorageModel
{
    internal int Size;
    internal int[] Offsets;
}

/// <summary>Analyzes portable unmanaged storage entirely from compiler symbols.</summary>
internal sealed class PortableStructPlan
{
    internal ITypeSymbol Type;
    internal PortableField[] Fields;
    internal PortableStructPlan[] Children;
    internal int WireSize;
    internal int Size32;
    internal int Size64;
    internal int Alignment32;
    internal int Alignment64;
    internal bool HasPadding;

    /// <summary>The declared layout kind: zero for sequential, two for explicit, three for automatic.</summary>
    internal int Kind;
    internal int Pack;
    internal int DeclaredSize;

    /// <summary>Whether contiguous payloads carry a one-byte layout tag and native storage instead of the packed wire.</summary>
    internal bool Tagged;

    /// <summary>Whether the type has its own generated or built-in formatter, so a single value can be written through its packed form.</summary>
    /// <remarks>Source-declared structs without the attribute receive an implicit formatter; structs of other assemblies keep their native copy.</remarks>
    internal bool FormatterOwned;

    /// <summary>The distinct native layouts a process may use; the tag of a model is its one-based index.</summary>
    internal StorageModel[] Models;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ITypeSymbol, TaggedEntry> TaggedCache = new();
    private PortableStructPlan _packed;

    /// <summary>Gets whether the type's own single-value formatter writes exactly the packed field-wise wire.</summary>
    /// <remarks>True for structs formatted by the object generator. Nullable, KeyValuePair and ValueTuple have dedicated formatters whose
    /// single-value form differs, so collections that write elements one at a time cannot be read through a contiguous packed payload.</remarks>
    internal bool SingleIsPacked
        => Type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T &&
           !IsValueTuple(named) && !(named.OriginalDefinition.MetadataName == "KeyValuePair`2" &&
                                      named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic");

    /// <summary>Selects the plan for a contiguous payload shared by writers and readers of element-wise families.</summary>
    /// <param name="type">The element type.</param>
    /// <returns>The packed plan when the element's single-value form is the packed wire; otherwise null so elements use their own formatter.</returns>
    internal static PortableStructPlan TryCreateContiguous(ITypeSymbol type)
        => TryCreate(type) is { SingleIsPacked: true } plan ? plan.AsPacked() : null;

    /// <summary>Views a tagged plan as its packed field-wise wire, which equals a concatenation of single values.</summary>
    /// <returns>This plan when it is not tagged; otherwise an untagged copy sharing the same storage description.</returns>
    /// <remarks>Collections whose writer emits elements one at a time cannot use the tagged contiguous payload, so they keep the packed form.</remarks>
    internal PortableStructPlan AsPacked()
        => !Tagged ? this : _packed ??= new PortableStructPlan
        {
            Type = Type, Fields = Fields, Children = Children, WireSize = WireSize, Size32 = Size32, Size64 = Size64,
            Alignment32 = Alignment32, Alignment64 = Alignment64, HasPadding = HasPadding, Kind = Kind, Pack = Pack,
            DeclaredSize = DeclaredSize, Models = Models, Tagged = false, FormatterOwned = FormatterOwned
        };

    /// <summary>Caches the possibly absent tagged plan of one type.</summary>
    private sealed class TaggedEntry
    {
        internal PortableStructPlan Plan;
    }

    /// <summary>Resolves the packed layout option on the struct, then on its declaring assembly.</summary>
    /// <param name="type">The current closed or open declaration.</param>
    /// <returns>Whether this struct's storage is subject to the portable contract.</returns>
    /// <remarks>An explicit struct setting, including false, overrides the assembly; the assembly applies only to unmanaged structs.</remarks>
    internal static bool IsOptedIn(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Struct) return false;
        int? declared = ReadStructLayout(type.GetAttributes());
        return declared.HasValue ? declared.Value == 1 : type.IsUnmanagedType && LayoutStrategy(type) == 1;
    }

    /// <summary>Resolves an explicit type strategy before the declaring assembly strategy.</summary>
    /// <param name="type">The unmanaged declaration.</param>
    /// <returns>Zero for tagged, one for packed, or two for native storage.</returns>
    private static int LayoutStrategy(INamedTypeSymbol type)
        => ReadStructLayout(type.GetAttributes()) ?? ReadStructLayout(type.ContainingAssembly.GetAttributes()) ?? 0;

    /// <summary>Reads an explicitly supplied layout strategy.</summary>
    /// <param name="attributes">The declaration's attributes.</param>
    /// <returns>The supplied value, or null when the declaration does not set it.</returns>
    private static int? ReadStructLayout(System.Collections.Immutable.ImmutableArray<AttributeData> attributes)
    {
        int? value = null;
        foreach (var attribute in attributes)
            if (attribute.AttributeClass?.ToDisplayString() == LuminPack.SourceGenerator.CompiledOptions.AttributeName)
                foreach (var argument in attribute.NamedArguments)
                    if (argument.Key == "StructLayout" && argument.Value.Value is int selected)
                        value = selected;
        return value;
    }

    /// <summary>Validates the opt-in before an unsupported value could fall back to a raw copy.</summary>
    /// <param name="type">The annotated model definition.</param>
    /// <returns>Whether its complete storage can be expanded, or is awaiting generic substitution.</returns>
    internal static bool IsSupported(INamedTypeSymbol type)
        => !IsOptedIn(type) ||
           TypeMetaChecker.CheckGeneratorType(type) == GeneratorType.Object &&
           IsSupportedStorage(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    /// <summary>Checks unsupported storage before generic arguments are closed at an operation boundary.</summary>
    /// <param name="type">The current storage type.</param>
    /// <param name="visited">Finite storage nodes already inspected.</param>
    /// <returns>Whether every field has a supported fixed representation.</returns>
    private static bool IsSupportedStorage(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type) || ScalarSize(type) != 0 || OpaqueLeaf(type) != null) return true;
        if (type is ITypeParameterSymbol parameter) return parameter.HasUnmanagedTypeConstraint;
        if (type is not INamedTypeSymbol { IsUnmanagedType: true, TypeKind: TypeKind.Struct } named ||
            named.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr ||
            IsRuntimeSizedVector(named) ||
            LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(named) ||
            named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InlineArrayAttribute")) return false;
        var fields = PlanFields(named, true);
        return fields != null && fields.All(field => IsSupportedStorage(field.Type, visited));
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

    /// <summary>Selects the opted-in field representation, leaving every unmarked root unchanged.</summary>
    /// <param name="type">The storage type in the current operation.</param>
    /// <returns>A packed plan when raw storage is not portable; otherwise null.</returns>
    internal static PortableStructPlan TryCreate(ITypeSymbol type)
    {
        if (type == null) return null;
        type = UnmanagedContractScope.Resolve(type);
        if (type is not INamedTypeSymbol named) return null;
        if (!IsOptedIn(named)) return TryCreateTagged(named) is { FormatterOwned: true } tagged ? tagged : null;
        if (TypeMetaChecker.CheckGeneratorType(named) != GeneratorType.Object) return null;
        var plan = Analyze(named, strict: true);
        return plan?.HasPadding == true ? plan : null;
    }

    /// <summary>Recognizes a struct declared in this compilation that has no attribute and no built-in formatter.</summary>
    /// <param name="type">The candidate type.</param>
    /// <returns>Whether the generator provides it an implicit binary formatter, so it behaves like an ordinary local packable struct.</returns>
    /// <remarks>Only top-level closed unmanaged structs qualify: their generated concrete operations are then accessible from every consumer
    /// of this assembly. Nested, generic-open, ref-like and referenced-assembly structs keep their previous handling.</remarks>
    internal static bool IsImplicitFormatterType(ITypeSymbol type)
        => type is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsUnmanagedType: true, IsRefLikeType: false, ContainingType: null } named &&
           named.SpecialType == SpecialType.None && !named.IsTupleType && named.DeclaringSyntaxReferences.Length != 0 &&
           !named.IsGenericType && !TypeMetaChecker.TryCheckPackableAttribute(named) &&
           !LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(named) && OpaqueLeaf(named) == null &&
           CodeEmitterRegistry.GetEmitter(LuminPack.Code.Core.FormatterTypeName.Get(named)).Write is null;

    /// <summary>Selects tagged native storage for an unmarked struct whose native layout is not identical in every layout model.</summary>
    /// <param name="named">The resolved storage type.</param>
    /// <returns>A tagged plan, or null when the struct is not analyzable, has a single layout, or was explicitly opted out.</returns>
    /// <remarks>A struct with one layout in every model keeps its headerless native copy. Contiguous payloads of the rest write a
    /// one-byte layout tag followed by native storage; single values use the packed field-wise representation of an opted-in struct.</remarks>
    private static PortableStructPlan TryCreateTagged(INamedTypeSymbol named)
    {
        if (named.TypeKind != TypeKind.Struct || named.SpecialType != SpecialType.None || !named.IsUnmanagedType) return null;
        var entry = TaggedCache.GetValue(named, static key => new TaggedEntry { Plan = CreateTagged((INamedTypeSymbol)key) });
        return entry.Plan;
    }

    /// <summary>Analyzes one struct and decides whether its layout varies between models.</summary>
    /// <param name="named">The resolved storage type.</param>
    /// <returns>The tagged plan, or null.</returns>
    private static PortableStructPlan CreateTagged(INamedTypeSymbol named)
    {
        if (LayoutStrategy(named) != 0) return null;
        if (ScalarSize(named) != 0 || ContainsTypeParameter(named) || OpaqueLeaf(named) != null) return null;
        if (LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(named) ||
            TypeMetaChecker.CheckGeneratorType(named) != GeneratorType.Object) return null;
        var plan = Analyze(named, strict: true);
        if (plan?.Fields == null || plan.WireSize <= 0 || HasVolatile(plan)) return null;
        plan.Models = BuildModels(plan);
        if (plan.Models.Length < 2) return null;
        plan.Tagged = true;
        plan.FormatterOwned = IsValueTuple(named) || named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
            named.OriginalDefinition.MetadataName == "KeyValuePair`2" && named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" ||
            TypeMetaChecker.TryCheckPackableAttribute(named) || IsImplicitFormatterType(named);
        return plan;
    }

    /// <summary>Recognizes volatile storage anywhere in an analyzed aggregate.</summary>
    /// <param name="plan">The analyzed storage.</param>
    /// <returns>Whether any expanded field is volatile.</returns>
    private static bool HasVolatile(PortableStructPlan plan)
    {
        if (plan.Fields == null) return false;
        for (int index = 0; index < plan.Fields.Length; index++)
            if (plan.Fields[index].IsVolatile || HasVolatile(plan.Children[index])) return true;
        return false;
    }

    /// <summary>Recognizes unresolved generic arguments anywhere in a closed construction.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>Whether a type parameter remains.</returns>
    private static bool ContainsTypeParameter(ITypeSymbol type)
        => type is ITypeParameterSymbol || type is INamedTypeSymbol { IsGenericType: true } named && named.TypeArguments.Any(ContainsTypeParameter);

    /// <summary>Checks whether any aggregate in the tree uses the runtime's automatic field order.</summary>
    /// <param name="plan">The analyzed storage.</param>
    /// <returns>Whether an automatic layout appears.</returns>
    private static bool HasAutomatic(PortableStructPlan plan)
        => plan.Fields != null && (plan.Kind == 3 || plan.Children.Any(HasAutomatic));

    /// <summary>Places the storage under each layout model and keeps the distinct results.</summary>
    /// <param name="plan">The analyzed storage.</param>
    /// <returns>Natural 64-bit, natural 32-bit and, for automatic layouts, the CoreCLR automatic orders, without duplicates.</returns>
    private static StorageModel[] BuildModels(PortableStructPlan plan)
    {
        var kinds = new List<(bool Is64, bool Auto)> { (true, false), (false, false) };
        if (HasAutomatic(plan)) { kinds.Add((true, true)); kinds.Add((false, true)); }
        var models = new List<StorageModel>();
        foreach (var (is64, auto) in kinds)
        {
            var placed = Place(plan, is64, auto);
            if (!models.Any(model => model.Size == placed.Size && model.Offsets.AsSpan().SequenceEqual(placed.Offsets)))
                models.Add(new StorageModel { Size = placed.Size, Offsets = placed.Offsets });
        }
        return models.ToArray();
    }

    /// <summary>Positions every field of an aggregate under one layout model.</summary>
    /// <param name="plan">The analyzed storage.</param>
    /// <param name="is64">Whether eight-byte primitives align to eight bytes.</param>
    /// <param name="auto">Whether automatic aggregates order fields like CoreCLR: larger primitives first, in declaration order within a size.</param>
    /// <returns>The element size, alignment, and the storage offset of every packed data byte.</returns>
    private static (int Size, int Alignment, int[] Offsets) Place(PortableStructPlan plan, bool is64, bool auto)
    {
        if (plan.Fields == null)
        {
            var bytes = new int[plan.WireSize];
            for (int index = 0; index < bytes.Length; index++) bytes[index] = index;
            return (plan.WireSize, is64 ? plan.Alignment64 : plan.Alignment32, bytes);
        }
        int count = plan.Children.Length;
        var places = new (int Size, int Alignment, int[] Offsets)[count];
        for (int index = 0; index < count; index++) places[index] = Place(plan.Children[index], is64, auto);
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
            int offset = plan.Kind == 2 && plan.Fields[index].Offset >= 0 ? plan.Fields[index].Offset : (end + fieldAlignment - 1) / fieldAlignment * fieldAlignment;
            offsets[index] = offset;
            end = Math.Max(end, offset + places[index].Size);
        }
        int size = (Math.Max(Math.Max(1, plan.DeclaredSize), end) + alignment - 1) / alignment * alignment;
        var data = new int[plan.WireSize];
        int next = 0;
        for (int index = 0; index < count; index++)
            foreach (int byteOffset in places[index].Offsets) data[next++] = offsets[index] + byteOffset;
        return (size, alignment, data);
    }

    /// <summary>Keeps open generic state entries on the existing closed formatter dispatch boundary.</summary>
    /// <param name="type">The declaration-owned type before or after operation substitution.</param>
    /// <returns>Whether its portable width still depends on an undetermined unmanaged argument.</returns>
    internal static bool RequiresClosedDispatch(ITypeSymbol type)
    {
        if (type == null) return false;
        type = UnmanagedContractScope.Resolve(type);
        return type is INamedTypeSymbol { IsGenericType: true } named &&
            IsOptedIn(named) &&
            TypeMetaChecker.CheckGeneratorType(named) == GeneratorType.Object && IsSupported(named) && Analyze(named) == null;
    }

    /// <summary>Builds recursive widths and conservative layout proofs for both architectures.</summary>
    /// <param name="type">The finite unmanaged storage type.</param>
    /// <param name="strict">Whether reference-assembly structs must be modeled from known shapes instead of their placeholder fields.</param>
    /// <returns>The storage plan, or null for storage without an architecture-independent scalar representation.</returns>
    private static PortableStructPlan Analyze(ITypeSymbol type, bool strict = false)
    {
        int scalar = ScalarSize(type);
        if (scalar != 0)
            return new PortableStructPlan { Type = type, WireSize = scalar, Size32 = scalar, Size64 = scalar,
                Alignment32 = Math.Min(scalar, 4), Alignment64 = Math.Min(scalar, 8) };
        if (OpaqueLeaf(type) is { } opaque)
            return new PortableStructPlan { Type = type, WireSize = opaque.Size, Size32 = opaque.Size, Size64 = opaque.Size,
                Alignment32 = opaque.Alignment32, Alignment64 = opaque.Alignment64 };
        if (type is not INamedTypeSymbol { IsUnmanagedType: true, TypeKind: TypeKind.Struct } named ||
            LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(type) ||
            named.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr ||
            IsRuntimeSizedVector(named) ||
            named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                "System.Runtime.CompilerServices.InlineArrayAttribute")) return null;
        var fields = PlanFields(named, strict);
        if (fields == null) return null;
        var children = fields.Select(field => Analyze(field.Type, strict)).ToArray();
        if (children.Any(child => child == null)) return null;
        var plan = new PortableStructPlan { Type = type, Fields = fields, Children = children,
            WireSize = children.Sum(child => child.WireSize), HasPadding = named.IsTupleType || children.Any(child => child.HasPadding) ||
                named.ContainingNamespace.ToDisplayString().StartsWith("System.Runtime.Intrinsics", StringComparison.Ordinal) };
        var layout = StorageLayoutInfo.Get(named);
        int kind = layout.Kind == StructLayout.Explicit ? 2 : layout.Kind == StructLayout.Auto || IsValueTuple(named) ? 3 : 0;
        int pack = layout.Pack == 0 ? 8 : layout.Pack, declaredSize = layout.Size;
        plan.Kind = kind;
        plan.Pack = layout.Pack;
        plan.DeclaredSize = declaredSize;
        plan.Size32 = plan.Measure(false, kind, pack, declaredSize, out plan.Alignment32);
        plan.Size64 = plan.Measure(true, kind, pack, declaredSize, out plan.Alignment64);
        plan.HasPadding |= kind == 3 || plan.Size32 != plan.WireSize || plan.Size64 != plan.WireSize;
        return plan;
    }

    /// <summary>Recognizes the automatic-layout ValueTuple family regardless of how the compiler spells the type.</summary>
    /// <param name="type">The storage type.</param>
    /// <returns>Whether the type is a ValueTuple construction.</returns>
    private static bool IsValueTuple(INamedTypeSymbol type)
        => type.IsTupleType || type.OriginalDefinition.MetadataName.StartsWith("ValueTuple`", StringComparison.Ordinal) &&
           type.ContainingNamespace.ToDisplayString() == "System";

    /// <summary>Lists the fields whose layout the plan models, repairing shapes a reference assembly hides.</summary>
    /// <param name="named">The aggregate.</param>
    /// <param name="strict">Whether the aggregate must be modeled from facts rather than from unreliable reference metadata.</param>
    /// <returns>The declaration-ordered fields, or null when the storage cannot be modeled.</returns>
    /// <remarks>Reference assemblies replace private struct fields with placeholders, so Nullable and KeyValuePair are modeled from their
    /// documented shapes and every other reference-assembly struct is analyzable only through the known-leaf table.</remarks>
    private static PortableField[] PlanFields(INamedTypeSymbol named, bool strict)
    {
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1 &&
            named.GetMembers("HasValue").OfType<IPropertySymbol>().FirstOrDefault()?.Type is { } boolean)
            return new[] { new PortableField { Type = boolean, Name = "hasValue" }, new PortableField { Type = named.TypeArguments[0], Name = "value" } };
        if (named.OriginalDefinition.MetadataName == "KeyValuePair`2" && named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
            return new[] { new PortableField { Type = named.TypeArguments[0], Name = "key" }, new PortableField { Type = named.TypeArguments[1], Name = "value" } };
        var fields = StorageFields(named);
        // SDK reference metadata for framework values may contain dummy fields instead of their real storage.
        // C# project reference assemblies retain value-type fields, including private ones needed for layout.
        if (IsReferenceAssembly(named.ContainingAssembly) && !IsValueTuple(named) &&
            !TypeMetaChecker.TryCheckPackableAttribute(named) &&
            (named.ContainingAssembly.Name.StartsWith("System.", StringComparison.Ordinal) || named.ContainingAssembly.Name is "mscorlib" or "netstandard")) return null;
        if (fields.Any(field => field.IsFixedSizeBuffer || field.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)) return null;
        return fields.Select(field => new PortableField { Type = field.Type, Name = field.Name, IsVolatile = field.IsVolatile, Offset = StorageLayoutInfo.FieldOffset(field) }).ToArray();
    }

    /// <summary>Recognizes an assembly that carries only the public shape of its types.</summary>
    /// <param name="assembly">The declaring assembly.</param>
    /// <returns>Whether it is marked as a reference assembly.</returns>
    private static bool IsReferenceAssembly(IAssemblySymbol assembly)
        => assembly != null && assembly.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute");

    /// <summary>Gives the width and alignments of framework structs whose fields a reference assembly hides.</summary>
    /// <param name="type">The storage type.</param>
    /// <returns>The fixed leaf description, or null for any other type.</returns>
    /// <remarks>Each entry is an unpadded block of data bytes that is identical on every runtime; only its alignment inside a containing struct varies.</remarks>
    private static (int Size, int Alignment64, int Alignment32)? OpaqueLeaf(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct } named) return null;
        return named.ToDisplayString() switch
        {
            "System.DateTime" or "System.TimeSpan" or "System.TimeOnly" => (8, 8, 4),
            "System.DateOnly" or "System.Index" => (4, 4, 4),
            "System.Guid" => (16, 4, 4),
            "System.Range" => (8, 4, 4),
            "System.Numerics.Vector2" => (8, 4, 4),
            "System.Numerics.Vector3" => (12, 4, 4),
            "System.Numerics.Vector4" or "System.Numerics.Quaternion" or "System.Numerics.Plane" => (16, 4, 4),
            _ => null
        };
    }

    /// <summary>Excludes vectors whose physical width can change with the executing JIT's instruction set.</summary>
    /// <param name="type">The nested storage type.</param>
    /// <returns>Whether compiler field metadata cannot describe its complete runtime width.</returns>
    private static bool IsRuntimeSizedVector(INamedTypeSymbol type)
        => type.MetadataName == "Vector`1" && type.ContainingNamespace.ToDisplayString() == "System.Numerics";

    /// <summary>Proves contiguous storage, including explicit offsets and trailing padding.</summary>
    /// <param name="is64Bit">The layout architecture.</param>
    /// <param name="kind">The declared layout kind.</param>
    /// <param name="pack">The declared maximum alignment.</param>
    /// <param name="declaredSize">The explicit minimum storage size.</param>
    /// <param name="alignment">Receives the aggregate alignment.</param>
    /// <returns>The predicted aggregate storage size.</returns>
    private int Measure(bool is64Bit, int kind, int pack, int declaredSize, out int alignment)
    {
        int end = 0, packedEnd = 0;
        alignment = 1;
        for (int index = 0; index < Children.Length; index++)
        {
            var child = Children[index];
            int fieldAlignment = Math.Min(pack, is64Bit ? child.Alignment64 : child.Alignment32);
            alignment = Math.Max(alignment, fieldAlignment);
            int offset = (end + fieldAlignment - 1) / fieldAlignment * fieldAlignment;
            if (kind == 2)
            {
                offset = Fields[index].Offset;
                if (offset < 0) { HasPadding = true; offset = end; }
            }
            HasPadding |= offset != packedEnd;
            end = Math.Max(end, offset + (is64Bit ? child.Size64 : child.Size32));
            packedEnd += child.WireSize;
        }
        return Math.Max(Math.Max(1, declaredSize), (end + alignment - 1) / alignment * alignment);
    }

    /// <summary>Obtains fixed scalar widths without consulting the running process's layout.</summary>
    /// <param name="type">A primitive, enum, or nested storage type.</param>
    /// <returns>The scalar byte width, or zero for an aggregate.</returns>
    private static int ScalarSize(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration) return ScalarSize(enumeration.EnumUnderlyingType);
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
}
