using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
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

    /// <summary>Classifies the allocating entry of a closed root payload, as the original concrete facade did.</summary>
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
        internal EntryShape EntryShapeOf(ITypeSymbol type)
        {
            // Entries stage their input when the reader cannot hold a managed span: before .NET 8 or against a netstandard Core.
            if (!Options.Net8 || ReaderTargetsNetStandard())
                return EntryShape.Pooled;
            bool containsReferences = !CanUseUnmanagedSerialization(type);
            string name = FormatterName(type);
            if (KnownValueTypes.Contains(name) && !containsReferences)
                return new EntryShape(EntryKind.RawScalar);
            if (name == "global::System.TimeZoneInfo")
                return new EntryShape(EntryKind.TimeZoneSnapshot);
            bool fixedEnum = type.TypeKind == TypeKind.Enum && !containsReferences;
            bool fixedRecord = IsFixedRecord(type) || fixedEnum;
            if (fixedRecord || IsDirectRecord(type))
            {
                if (fixedEnum)
                    return new EntryShape(EntryKind.FixedEnum);
                if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
                    return new EntryShape(EntryKind.NullableDirect, FormatterName(nullable.TypeArguments[0]));
                if (name == "global::System.Numerics.BigInteger")
                    return new EntryShape(EntryKind.BigInteger);
                return new EntryShape(fixedRecord ? EntryKind.FixedRecord : EntryKind.DirectRecord);
            }
            if (type is INamedTypeSymbol { TypeArguments.Length: 1 } sequence &&
                sequence.OriginalDefinition.ToDisplayString() == "System.Buffers.ReadOnlySequence<T>" && IsKnownScalar(sequence.TypeArguments[0]))
                return new EntryShape(EntryKind.RawSequence, FormatterName(sequence.TypeArguments[0]));
            if (type is INamedTypeSymbol { TypeArguments.Length: 1, TypeKind: TypeKind.Interface } collection)
            {
                string definition = collection.OriginalDefinition.ToDisplayString();
                ITypeSymbol element = collection.TypeArguments[0];
                if (definition is "System.Collections.Generic.IEnumerable<T>" or "System.Collections.Generic.ICollection<T>" or
                    "System.Collections.Generic.IReadOnlyList<T>" && IsKnownScalar(element))
                    return new EntryShape(EntryKind.StatelessRead);
                if (definition is "System.Collections.Generic.IList<T>" or "System.Collections.Generic.ISet<T>" or
                    "System.Collections.Generic.IReadOnlySet<T>")
                {
                    string spelling = FormatterName(element);
                    // The original exact-interface path admits supported framework scalars with native storage.
                    if ((element.SpecialType != SpecialType.None || spelling != "global::System.Runtime.InteropServices.GCHandle" &&
                         FrameworkScalarBody.Contains(spelling)) && KnownValueTypes.Contains(spelling) && CanUseUnmanagedSerialization(element))
                    {
                        EntryKind kind = definition == "System.Collections.Generic.IList<T>" ? EntryKind.ExactList :
                            FormatterRuntimeTraits.CanGroupSmallHashSetWrites(element, _compilation) ? EntryKind.ExactSmallHashSet : EntryKind.ExactHashSet;
                        return new EntryShape(kind, spelling);
                    }
                }
            }
            return EntryShape.Pooled;
        }

        private bool ReaderTargetsNetStandard() => _compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader")?.ContainingAssembly
            .GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.Versioning.TargetFrameworkAttribute" &&
                attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string framework &&
                framework.StartsWith(".NETStandard", StringComparison.Ordinal)) == true;

        /// <summary>The original formatter spelling: a keyword for special types, otherwise fully qualified.</summary>
        private static string FormatterName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.ExpandNullable));

        /// <summary>Whether a reference-free value is serialized as its complete storage: no portable plan and no Custom contract.</summary>
        private bool CanUseUnmanagedSerialization(ITypeSymbol type) => type.IsUnmanagedType && !ContainsOpenParameter(type) &&
            (type is not INamedTypeSymbol named || TryPortableLayout(named) == null) &&
            !ContainsCustomContract(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

        private bool IsKnownScalar(ITypeSymbol type) => KnownValueTypes.Contains(FormatterName(type)) && CanUseUnmanagedSerialization(type);

        private static bool ContainsOpenParameter(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsOpenParameter(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.Any(ContainsOpenParameter),
            _ => false,
        };

        /// <summary>The original direct-record rule: strings, scalar sequences and a few framework records size their output exactly.</summary>
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
                    "System.ReadOnlyMemory<T>" or "System.Collections.Immutable.ImmutableArray<T>" ||
                named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return IsKnownScalar(named.TypeArguments[0]);
            if (named.TypeArguments.Length == 1 && definition == "System.Lazy<T>")
                return IsKnownScalar(named.TypeArguments[0]);
            return named.TypeArguments.Length > 0 &&
                named.TypeArguments.All(argument => argument.SpecialType == SpecialType.System_String || IsKnownScalar(argument)) &&
                (definition.StartsWith("System.Collections.Generic.KeyValuePair<", StringComparison.Ordinal) || named.IsTupleType ||
                 definition.StartsWith("System.Tuple<", StringComparison.Ordinal));
        }

        /// <summary>The original fixed-record rule: an object or named-version record of fixed-width primitive members only.</summary>
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
