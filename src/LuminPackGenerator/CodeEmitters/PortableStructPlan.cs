using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Analyzes portable unmanaged storage entirely from compiler symbols.</summary>
internal sealed class PortableStructPlan
{
    internal const string AttributeName = "LuminPack.Attribute.LuminPackSupport32bitAttribute";
    internal ITypeSymbol Type;
    internal IFieldSymbol[] Fields;
    internal PortableStructPlan[] Children;
    internal int WireSize;
    internal int Size32;
    internal int Size64;
    internal int Alignment32;
    internal int Alignment64;
    internal bool HasPadding;

    /// <summary>Validates the opt-in before an unsupported value could fall back to a raw copy.</summary>
    /// <param name="type">The annotated model definition.</param>
    /// <returns>Whether its complete storage can be expanded, or is awaiting generic substitution.</returns>
    internal static bool IsSupported(INamedTypeSymbol type)
        => !type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == AttributeName) ||
           TypeMetaChecker.CheckGeneratorType(type) == GeneratorType.Object &&
           IsSupportedStorage(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    /// <summary>Checks unsupported storage before generic arguments are closed at an operation boundary.</summary>
    /// <param name="type">The current storage type.</param>
    /// <param name="visited">Finite storage nodes already inspected.</param>
    /// <returns>Whether every field has a supported fixed representation.</returns>
    private static bool IsSupportedStorage(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type) || ScalarSize(type) != 0) return true;
        if (type is ITypeParameterSymbol parameter) return parameter.HasUnmanagedTypeConstraint;
        if (type is not INamedTypeSymbol { IsUnmanagedType: true, TypeKind: TypeKind.Struct } named ||
            named.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr ||
            IsRuntimeSizedVector(named) ||
            LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(named) ||
            named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InlineArrayAttribute")) return false;
        return named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic)
            .All(field => !field.IsFixedSizeBuffer && IsSupportedStorage(field.Type, visited));
    }

    /// <summary>Selects the opted-in field representation, leaving every unmarked root unchanged.</summary>
    /// <param name="type">The storage type in the current operation.</param>
    /// <returns>A packed plan when raw storage is not portable; otherwise null.</returns>
    internal static PortableStructPlan TryCreate(ITypeSymbol type)
    {
        if (type == null) return null;
        type = UnmanagedContractScope.Resolve(type);
        if (type is not INamedTypeSymbol named || !named.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == AttributeName)) return null;
        if (TypeMetaChecker.CheckGeneratorType(named) != GeneratorType.Object) return null;
        var plan = Analyze(named);
        return plan?.HasPadding == true ? plan : null;
    }

    /// <summary>Keeps open generic state entries on the existing closed formatter dispatch boundary.</summary>
    /// <param name="type">The declaration-owned type before or after operation substitution.</param>
    /// <returns>Whether its portable width still depends on an undetermined unmanaged argument.</returns>
    internal static bool RequiresClosedDispatch(ITypeSymbol type)
    {
        if (type == null) return false;
        type = UnmanagedContractScope.Resolve(type);
        return type is INamedTypeSymbol { IsGenericType: true } named &&
            named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == AttributeName) &&
            TypeMetaChecker.CheckGeneratorType(named) == GeneratorType.Object && IsSupported(named) && Analyze(named) == null;
    }

    /// <summary>Builds recursive widths and conservative layout proofs for both architectures.</summary>
    /// <param name="type">The finite unmanaged storage type.</param>
    /// <returns>The storage plan, or null for storage without an architecture-independent scalar representation.</returns>
    private static PortableStructPlan Analyze(ITypeSymbol type)
    {
        int scalar = ScalarSize(type);
        if (scalar != 0)
            return new PortableStructPlan { Type = type, WireSize = scalar, Size32 = scalar, Size64 = scalar,
                Alignment32 = Math.Min(scalar, 4), Alignment64 = Math.Min(scalar, 8) };
        if (type is not INamedTypeSymbol { IsUnmanagedType: true, TypeKind: TypeKind.Struct } named ||
            LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(type) ||
            named.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr ||
            IsRuntimeSizedVector(named) ||
            named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                "System.Runtime.CompilerServices.InlineArrayAttribute")) return null;
        var fields = named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic).ToArray();
        if (fields.Any(field => field.IsFixedSizeBuffer || field.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)) return null;
        var children = fields.Select(field => Analyze(field.Type)).ToArray();
        if (children.Any(child => child == null)) return null;
        var plan = new PortableStructPlan { Type = type, Fields = fields, Children = children,
            WireSize = children.Sum(child => child.WireSize), HasPadding = children.Any(child => child.HasPadding) ||
                named.ContainingNamespace.ToDisplayString().StartsWith("System.Runtime.Intrinsics", StringComparison.Ordinal) };
        int kind = 0, pack = 8, declaredSize = 0;
        var layout = named.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() ==
            "System.Runtime.InteropServices.StructLayoutAttribute");
        if (layout != null)
        {
            kind = (int)layout.ConstructorArguments[0].Value;
            foreach (var argument in layout.NamedArguments)
            {
                if (argument.Key == "Pack" && (int)argument.Value.Value != 0) pack = (int)argument.Value.Value;
                if (argument.Key == "Size") declaredSize = (int)argument.Value.Value;
            }
        }
        plan.Size32 = plan.Measure(false, kind, pack, declaredSize, out plan.Alignment32);
        plan.Size64 = plan.Measure(true, kind, pack, declaredSize, out plan.Alignment64);
        plan.HasPadding |= kind == 3 || plan.Size32 != plan.WireSize || plan.Size64 != plan.WireSize;
        return plan;
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
                var attribute = Fields[index].GetAttributes().FirstOrDefault(item =>
                    item.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.FieldOffsetAttribute");
                if (attribute == null) { HasPadding = true; offset = end; }
                else offset = (int)attribute.ConstructorArguments[0].Value;
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
