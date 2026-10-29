using System.Linq;
using System.Collections.Generic;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides emitter type traits functionality.</summary>
internal static class EmitterTypeTraits
{
    /// <summary>Normalizes the generated spellings of the scalar types that have fused JSON element writers.</summary>
    /// <param name="type">A generated element, key or value type name.</param>
    /// <returns>The C# keyword for string, int or long; otherwise null.</returns>
    private static string JsonFusedKind(string type)
    {
        switch (type?.Replace("global::", "").Trim())
        {
            case "string": case "string?": case "System.String": case "System.String?": return "string";
            case "int": case "System.Int32": return "int";
            case "long": case "System.Int64": return "long";
            default: return null;
        }
    }

    /// <summary>Recognizes array or list elements that <c>WriteElement</c> writes with their separator in one reservation.</summary>
    /// <param name="type">The generated element type name.</param>
    /// <returns>Whether the element is a string, int or long.</returns>
    internal static bool HasJsonElementWriter(string type) => JsonFusedKind(type) != null;

    /// <summary>Recognizes dictionary entries that <c>WriteEntryKey</c> and <c>WriteEntryValue</c> write as one fused pair array.</summary>
    /// <param name="keyType">The generated key type name.</param>
    /// <param name="valueType">The generated value type name.</param>
    /// <returns>Whether the key is a string or int and the value a string, int or long.</returns>
    internal static bool HasJsonEntryWriter(string keyType, string valueType)
        => JsonFusedKind(keyType) is "string" or "int" && JsonFusedKind(valueType) != null;

    /// <summary>Identifies declaration-owned scalar framing that a raw unmanaged instance call would bypass.</summary>
    /// <param name="type">The actual scalar argument type.</param>
    /// <returns>Whether the explicit serializer entry must retain its contract selection.</returns>
    internal static bool RequiresExplicitStateContract(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } ||
           type?.IsUnmanagedType == true &&
           (!CanUseUnmanagedSerialization(type) || type is INamedTypeSymbol { OriginalDefinition.IsUnmanagedType: false } named && TypeMetaChecker.TryCheckPackableAttribute(named));

    /// <summary>Checks that the value's own formatter (<c>WriteValue</c>/<c>ReadValue</c>) is exactly a copy of its native storage.</summary>
    /// <param name="type">The element, key or value type.</param>
    /// <returns>Whether a raw copy and the per-value formatter produce identical bytes, so one may replace the other.</returns>
    /// <remarks>
    /// Contiguous collections and key/value pairs may copy a Nullable, KeyValuePair or specialized-mode unmanaged value as raw
    /// storage, but their per-value formatters frame it differently. Any path that falls back to the per-value formatter must
    /// not assume raw storage for those types, and any raw path must not hand them to a per-value reader.
    /// </remarks>
    internal static bool FormatterIsRawStorage(ITypeSymbol type)
    {
        if (!CanUseUnmanagedSerialization(type) || RequiresExplicitStateContract(type)) return false;
        if (type is not INamedTypeSymbol named) return true;
        if (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>") return false;
        return TypeMetaChecker.CheckGeneratorType(named) == GeneratorType.Object &&
            !named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is
                LuminPackSourceGenerator.LUMIN_UNION_ATTRIBUTE or "System.Runtime.CompilerServices.UnionAttribute");
    }

    /// <summary>Writes a proven raw collection, including nullable values excluded by C# unmanaged constraints.</summary>
    /// <param name="builder">The generated operation.</param>
    /// <param name="type">The collection element symbol.</param>
    /// <param name="elementType">The generated element type name.</param>
    /// <param name="span">The source span expression.</param>
    /// <param name="count">The logical element count.</param>
    internal static void AppendRawCollectionWrite(System.Text.StringBuilder builder, ITypeSymbol type, string elementType, string span, string count)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            builder.AppendLine("            var payload = " + span + ";");
            builder.AppendLine("            int bytes = global::LuminPack.Core.LuminPackWriter.Size<" + elementType + ">(" + count + ", true, false);");
            builder.AppendLine("            writer.AdvanceSafe(global::LuminPack.Core.LuminPackWriter.Write<" + elementType + ">(writer.Reserve(writer.Position, bytes), payload, " + count + ", true, false));");
        }
        else
            builder.AppendLine("            writer.Write<" + elementType + ">(" + span + ", " + count + ");");
    }

    /// <summary>Copies a raw collection payload after its complete header and byte width were validated before allocation.</summary>
    /// <param name="builder">The generated operation.</param>
    /// <param name="elementType">The proven reference-free element type.</param>
    /// <param name="span">The destination expression, including any required count/version mutation.</param>
    /// <param name="length">The validated element count.</param>
    internal static void AppendValidatedRawRead(System.Text.StringBuilder builder, string elementType, string span, string length)
    {
        builder.AppendLine("            var payload = " + span + ";");
        builder.AppendLine("            if (" + length + " != 0) reader.Read(ref global::System.Runtime.CompilerServices.Unsafe.As<" + elementType +
            ", byte>(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(payload)), unchecked(" + length +
            " * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">()));");
    }

    /// <summary>Identifies ordinary reference-object framing whose root null rule must not retain a nested value.</summary>
    /// <param name="type">The canonical formatter type.</param>
    /// <returns>Whether this is the ordinary object formatter, without Custom, Union or built-in framing.</returns>
    internal static bool UsesOrdinaryObjectHeader(ITypeSymbol type)
        => type is INamedTypeSymbol { IsReferenceType: true, TypeKind: TypeKind.Class } named &&
           TypeMetaChecker.TryCheckPackableAttribute(named) &&
           TypeMetaChecker.CheckGeneratorType(named) == GeneratorType.Object &&
           !TypeMetaChecker.TryCheckUnionAttribute(named) &&
           !LuminPackExtensionGenerator.IsCustomGeneratorType(named) &&
           CodeEmitterRegistry.GetEmitter(FormatterTypeName.Get(named)).Read == null;

    /// <summary>Reads a reusable collection value while keeping null elements distinct from the root merge rule.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="type">The canonical element type.</param>
    /// <param name="target">The existing writable element storage.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="compressed">Whether the original call uses the compressed operation.</param>
    internal static void AppendReusableElementRead(System.Text.StringBuilder sb, ITypeSymbol type, string target, string indent, bool compressed = false)
    {
        if (UsesOrdinaryObjectHeader(type))
        {
            sb.AppendLine(indent + "if (reader.PeekIsNullObject(ref reader.GetCurrentSpanOffset())) " + target + " = default!;");
        }
        else if (type is ITypeParameterSymbol { IsValueType: false })
            sb.AppendLine(indent + "if (global::LuminPack.LuminPackSerializer.FormatterDispatch.__LuminPackShouldResetMember<" + type.ToDisplayString() + ">(ref reader)) " + target + " = default!;");
        sb.AppendLine(indent + "reader." + (compressed ? "ReadValueWithCompress" : "ReadValue") + "(ref " + target + "!);");
    }

    /// <summary>
    /// Resolves whether raw serialization can preserve the formatter contract.
    /// Open types retain per-value dispatch because an unmanaged constraint does not exclude Custom.
    /// </summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="isUnmanaged">Receives the is unmanaged produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool TryGetIsUnmanaged(ITypeSymbol type, out bool isUnmanaged)
    {
        if (type is not null && ContainsUndeterminedTypeParameter(type))
        {
            // Keep a proven unmanaged constraint's original raw path when its actual argument
            // is available and does not carry a Custom contract. Unknown arguments stay conservative.
            isUnmanaged = type.IsUnmanagedType && CanUseUnmanagedSerialization(type);
            return true;
        }
        if (type?.IsUnmanagedType == true)
        {
            isUnmanaged = CanUseUnmanagedSerialization(type);
            return true;
        }

        if (type?.IsReferenceType == true)
        {
            isUnmanaged = false;
            return true;
        }

        if (type is null || ContainsUndeterminedTypeParameter(type))
        {
            isUnmanaged = false;
            return false;
        }

        isUnmanaged = false;
        return true;
    }

    /// <summary>Identifies a closed built-in record that a root entry may process without operation state.</summary>
    /// <param name="type">The closed dispatch type.</param>
    /// <returns>Whether the binary operations of every value touch no reference-tracking state and the exact evaluator
    /// needs no traversal beyond its strings: a supported count-bearing collection of known scalars, a Nullable known scalar,
    /// a Lazy known scalar, a string, Uri, CultureInfo, StringBuilder or BitArray record, a Version, a BigInteger, or a pair or tuple
    /// whose members are all known scalars or strings.</returns>
    internal static bool IsDirectRecord(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol { Rank: 1 } array) return IsKnownScalar(array.ElementType);
        if (type is not INamedTypeSymbol named) return false;
        if (named.SpecialType == SpecialType.System_String) return true;
        if (named.SpecialType == SpecialType.None && named.ToDisplayString() is "System.Version" or "System.Numerics.BigInteger" or
                "System.Uri" or "System.Globalization.CultureInfo" or "System.Text.StringBuilder" or "System.Collections.BitArray") return true;
        string definition = named.OriginalDefinition.ToDisplayString();
        if (named.TypeArguments.Length == 1 && definition is "System.Collections.Generic.List<T>" or "System.Collections.Generic.HashSet<T>" or
                "System.Collections.Generic.Stack<T>" or "System.Collections.Generic.Queue<T>" or "System.Collections.Generic.LinkedList<T>" or
                "System.Collections.Generic.SortedSet<T>" or
                "System.Collections.Immutable.ImmutableHashSet<T>" or "System.Collections.Immutable.ImmutableSortedSet<T>" or "System.ArraySegment<T>" or
                "System.Memory<T>" or "System.ReadOnlyMemory<T>" or "System.Collections.Immutable.ImmutableArray<T>" ||
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return IsKnownScalar(named.TypeArguments[0]);
        if (named.TypeArguments.Length == 1 && definition == "System.Lazy<T>")
            return IsKnownScalar(named.TypeArguments[0]);
        return named.TypeArguments.Length > 0 && named.TypeArguments.All(IsDirectMember) &&
            (definition.StartsWith("System.Collections.Generic.KeyValuePair<", System.StringComparison.Ordinal) ||
             named.IsTupleType || definition.StartsWith("System.Tuple<", System.StringComparison.Ordinal));
    }

    /// <summary>Identifies sequence-interface reads whose existing fresh-array root never accesses reference state.</summary>
    /// <param name="type">The closed interface contract.</param>
    /// <returns>Whether the read can use a stateless entry without changing write or size evaluation eligibility.</returns>
    internal static bool IsStatelessScalarSequenceRead(ITypeSymbol type)
        => type is INamedTypeSymbol { TypeArguments.Length: 1 } named &&
           named.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.IEnumerable<T>" or
               "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyList<T>" &&
           IsKnownScalar(named.TypeArguments[0]);

    /// <summary>Identifies the exact runtime implementation whose allocating interface write has passed acceptance.</summary>
    /// <param name="type">The closed interface contract.</param>
    /// <returns>The concrete implementation name, or null for an interface retaining its original pooled entry.</returns>
    /// <remarks>Uses the same closed framework-scalar storage proof as direct records; user, portable and unresolved elements retain their prior eligibility.</remarks>
    internal static string GetExactScalarInterfaceWriteImplementation(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { TypeArguments.Length: 1 } named || !IsKnownScalar(named.TypeArguments[0]))
            return null;
        var element = named.TypeArguments[0];
        string metadataName = element.ContainingNamespace.ToDisplayString() + "." + element.MetadataName;
        if (element.SpecialType == SpecialType.None && !CodeEmitterTypeCatalog.SupportedMetadataNames.Contains(metadataName) &&
            !CodeEmitterTypeCatalog.SupportedMetadataNames.Contains("global::" + metadataName))
            return null;
        string implementation = named.OriginalDefinition.ToDisplayString() switch
        {
            "System.Collections.Generic.IList<T>" => "List",
            "System.Collections.Generic.ISet<T>" or "System.Collections.Generic.IReadOnlySet<T>" => "HashSet",
            _ => null
        };
        return implementation is null ? null : "global::System.Collections.Generic." + implementation + "<" + FormatterTypeName.Get(named.TypeArguments[0]) + ">";
    }

    /// <summary>Checks the closed small native storage family and the referenced runtime's grouped-store capability.</summary>
    /// <param name="element">The exact collection element type.</param>
    /// <param name="compilation">The consumer compilation supplying the actual runtime API.</param>
    /// <returns>Whether generation may select the one-/two-byte dense hash-set writer.</returns>
    internal static bool CanGroupSmallHashSetWrites(ITypeSymbol element, Compilation compilation)
    {
        if (!IsKnownScalar(element)) return false;
        bool small = element.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Char ||
            element.MetadataName == "Half" && element.ContainingNamespace.ToDisplayString() == "System";
        return small && compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal")?
            .GetMembers("WriteSmallHashSetRaw").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 1 &&
                method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 2 &&
                method.Parameters[0].RefKind == RefKind.Ref && method.ReturnsVoid) == true;
    }

    /// <summary>Checks the runtime's optional fresh sorted-set builder.</summary>
    /// <param name="element">The closed element formatter contract.</param>
    /// <param name="compilation">The consumer compilation supplying the referenced runtime.</param>
    /// <returns>Whether a fresh set may avoid the public constructor's sort for ordered input.</returns>
    /// <remarks>Any element type qualifies: the builder uses the same default comparer as the constructor, checking the
    /// order with one comparison per element and falling back to the constructor otherwise.</remarks>
    internal static bool CanBuildSortedSet(ITypeSymbol element, Compilation compilation)
    {
        return element is not null && compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage")?
            .GetMembers("CreateSortedSet").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 1 &&
                method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 2 &&
                method.Parameters[0].RefKind == RefKind.None && method.Parameters[1].RefKind == RefKind.None &&
                method.Parameters[0].Type is IArrayTypeSymbol { Rank: 1 } array &&
                SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                method.ReturnType is INamedTypeSymbol { MetadataName: "SortedSet`1", TypeArguments.Length: 1 } result &&
                result.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
                SymbolEqualityComparer.Default.Equals(result.TypeArguments[0], method.TypeParameters[0])) == true;
    }

    /// <summary>Checks both optional mutable sorted-set helpers.</summary>
    /// <param name="element">The independently formatted element contract.</param>
    /// <param name="compilation">The consumer compilation supplying the actual runtime API.</param>
    /// <returns>Whether generation may use the existing shared element pool and ordered reconstruction.</returns>
    /// <remarks>The runtime admits only destinations using the default comparer, so user comparers keep Clear/Add.</remarks>
    internal static bool CanRebuildSortedSet(ITypeSymbol element, Compilation compilation)
    {
        if (element is null) return false;
        var owner = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        bool Common(IMethodSymbol method) => method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            method.Parameters[0].Type is INamedTypeSymbol { MetadataName: "SortedSet`1", TypeArguments.Length: 1 } set &&
            set.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            SymbolEqualityComparer.Default.Equals(set.TypeArguments[0], method.TypeParameters[0]);
        return owner?.GetMembers("CanRebuildSortedSet").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 1 && Common(method)) == true &&
            owner.GetMembers("TryRebuildSortedSet").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 3 && Common(method) &&
                method.Parameters[1].Type is IArrayTypeSymbol { Rank: 1 } array &&
                SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) &&
                method.Parameters[2].Type.SpecialType == SpecialType.System_Int32);
    }

    /// <summary>Checks string-key eligibility and the complete optional ordered-list append signatures.</summary>
    /// <param name="key">The selected key formatter contract.</param>
    /// <param name="compilation">The consumer compilation supplying the actual runtime API.</param>
    /// <returns>Whether generated reads may bypass a certified append's binary search.</returns>
    internal static bool CanAppendSortedListOrdered(ITypeSymbol key, Compilation compilation)
    {
        if (key?.SpecialType != SpecialType.System_String) return false;
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        bool Common(IMethodSymbol method) => method.IsStatic && method.Arity == 2 && method.DeclaredAccessibility == Accessibility.Public &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            method.Parameters[0].Type is INamedTypeSymbol { MetadataName: "SortedList`2", TypeArguments.Length: 2 } list &&
            list.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            SymbolEqualityComparer.Default.Equals(list.TypeArguments[0], method.TypeParameters[0]) &&
            SymbolEqualityComparer.Default.Equals(list.TypeArguments[1], method.TypeParameters[1]);
        return owner?.GetMembers("CanAppendSortedListOrdered").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 1 && Common(method)) == true &&
            owner.GetMembers("TryAppendSortedListOrdered").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 4 && Common(method) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, method.TypeParameters[0]) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, method.TypeParameters[1]) &&
                method.Parameters[3].Type.SpecialType == SpecialType.System_Boolean);
    }

    /// <summary>Recognizes framework comparisons whose generator selection excludes user comparison callbacks.</summary>
    /// <param name="element">The closed element formatter.</param>
    /// <param name="compilation">The consuming compilation identifying its actual core library.</param>
    /// <returns>Whether this is string or an already supported comparable framework scalar.</returns>
    private static bool IsFrameworkComparison(ITypeSymbol element, Compilation compilation)
        => element?.SpecialType == SpecialType.System_String || IsKnownScalar(element) &&
           SymbolEqualityComparer.Default.Equals(element.ContainingAssembly, compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly) &&
           element is INamedTypeSymbol named && named.AllInterfaces.Any(contract =>
               contract.OriginalDefinition.ToDisplayString() == "System.IComparable<T>" &&
               SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], element));

    /// <summary>Identifies a source-declared generated object whose binary record width depends only on whether the root is null.</summary>
    /// <param name="type">The closed dispatch type.</param>
    /// <returns>Whether every instance field and property of the type and its source-declared bases is a primitive or enum, so the
    /// generated evaluator and writer are constant-width, touch no reference-tracking state and contain no dispatch.</returns>
    /// <remarks>The test does not depend on member selection: when no instance member of any other type exists, every selected
    /// member is fixed-width. Version-tolerant, circle-reference, custom, surrogate, union, compressed and wide-tag
    /// declarations, and bases that are not declared in this compilation, are excluded.</remarks>
    internal static bool IsFixedRecord(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named || named.TypeKind is not (TypeKind.Class or TypeKind.Struct) ||
            named.IsAbstract || named.IsStatic || named.IsRefLikeType || ContainsUndeterminedTypeParameter(named))
            return false;
        var packable = named.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);
        if (packable is null || packable.ConstructorArguments.Length == 1 &&
            !(packable.ConstructorArguments[0].Kind == TypedConstantKind.Enum && System.Convert.ToInt32(packable.ConstructorArguments[0].Value) == 0))
            return false;
        for (var current = named; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             current = current.BaseType)
        {
            if (!current.Locations.Any(location => location.IsInSource)) return false;
            foreach (var attribute in current.GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() is LuminPackSourceGenerator.LUMIN_UNION_ATTRIBUTE or
                    "System.Runtime.CompilerServices.UnionAttribute" or "LuminPack.Attribute.LuminPackCompressAttribute" or
                    "LuminPack.Attribute.LuminPackWideTagAttribute" or "LuminPack.Attribute.LuminPackSingleImplementationAttribute")
                    return false;
            if (TypeMetaChecker.HasUnionOption(current, "WideTag") || TypeMetaChecker.HasUnionOption(current, "SingleImplementation"))
                return false;
            if (current != named && current.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE &&
                    attribute.ConstructorArguments.Length == 1 &&
                    !(attribute.ConstructorArguments[0].Kind == TypedConstantKind.Enum && System.Convert.ToInt32(attribute.ConstructorArguments[0].Value) == 0)))
                return false;
            foreach (var member in current.GetMembers())
            {
                if (member.IsStatic) continue;
                ITypeSymbol memberType;
                switch (member)
                {
                    case IFieldSymbol field: memberType = field.Type; break;
                    case IPropertySymbol property: memberType = property.Type; break;
                    case IEventSymbol: return false;
                    default: continue;
                }
                if (!IsFixedWidthPrimitive(memberType)) return false;
            }
        }
        return true;
    }

    /// <summary>Identifies a predefined numeric, Boolean or character type, or an enum over one.</summary>
    /// <param name="type">The member type.</param>
    /// <returns>Whether the member is always written as its fixed-width raw value.</returns>
    private static bool IsFixedWidthPrimitive(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration) type = enumeration.EnumUnderlyingType;
        return type?.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double;
    }

    /// <summary>Identifies a predefined or known framework unmanaged scalar without any formatter contract.</summary>
    /// <param name="type">The member or element type.</param>
    /// <returns>Whether the value is stored as its complete raw scalar storage.</returns>
    internal static bool IsKnownScalar(ITypeSymbol type) =>
        type is not null && CodeEmitterRegistry.KnownValueTypes.Contains(FormatterTypeName.Get(type)) && CanUseUnmanagedSerialization(type);

    /// <summary>Checks the closed raw scalar contract and the exact optional complete-staging runtime entry.</summary>
    /// <param name="key">The canonical dictionary key type.</param>
    /// <param name="value">The canonical dictionary value contract.</param>
    /// <param name="compilation">The consuming compilation and referenced runtime API.</param>
    /// <returns>Whether generation may attempt one-pass string-key staging without changing element operations.</returns>
    internal static bool CanReadStableStringKeys(ITypeSymbol key, ITypeSymbol value, Compilation compilation)
        => key.SpecialType == SpecialType.System_String && !LuminPackExtensionGenerator.IsCustomGeneratorType(key) && IsKnownScalar(value) &&
            compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal")?
                .GetMembers("TryReadStableStringKeys").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 2 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                    method.Parameters.Length == 3 && method.Parameters[0].RefKind == RefKind.Ref &&
                    method.Parameters[0].Type.ToDisplayString() == "LuminPack.Core.LuminPackReader" &&
                    method.Parameters[1].Type is INamedTypeSymbol { MetadataName: "Dictionary`2", TypeArguments.Length: 2 } dictionary &&
                    dictionary.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
                    dictionary.TypeArguments[0].SpecialType == SpecialType.System_String &&
                    SymbolEqualityComparer.Default.Equals(dictionary.TypeArguments[1], method.TypeParameters[0]) &&
                    method.Parameters[2].Type.SpecialType == SpecialType.System_Int32) == true;

    /// <summary>Identifies a pair or tuple member whose record is a raw scalar or a string record.</summary>
    /// <param name="type">The member type.</param>
    /// <returns>Whether the member never registers or resolves references and its exact width needs no traversal beyond the string.</returns>
    private static bool IsDirectMember(ITypeSymbol type) =>
        type is not null && (type.SpecialType == SpecialType.System_String || IsKnownScalar(type));

    /// <summary>Checks whether a closed unmanaged value has no custom formatter in its storage graph.</summary>
    /// <param name="type">The value type considered for raw copying.</param>
    /// <returns>Whether raw copying preserves every nested formatter boundary.</returns>
    internal static bool CanUseUnmanagedSerialization(ITypeSymbol type)
    {
        if (type?.IsUnmanagedType != true) return false;
        var storage = UnmanagedContractScope.Resolve(type);
        return !ContainsUndeterminedTypeParameter(storage) &&
            PortableStructPlan.TryCreate(storage) == null &&
            !ContainsCustomFormatter(storage, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
    }

    /// <summary>Finds custom contracts inside finite unmanaged storage without following reference graphs.</summary>
    /// <param name="type">The current storage type.</param>
    /// <param name="visited">Storage types already inspected.</param>
    /// <returns>Whether the storage contains a custom formatter contract.</returns>
    private static bool ContainsCustomFormatter(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type)) return false;
        if (LuminPackExtensionGenerator.IsCustomGeneratorType(type)) return true;
        return type is INamedTypeSymbol { TypeKind: TypeKind.Struct } named &&
            (named.TypeArguments.Any(argument => ContainsCustomFormatter(argument, visited)) ||
             named.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic && ContainsCustomFormatter(field.Type, visited)));
    }

    /// <summary>Obtains the first type argument for the supplied input.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <returns>The result of the operation.</returns>
    public static ITypeSymbol GetFirstTypeArgument(LuminLocalFieldData fieldData)
    {
        return GetCollectionElementType(fieldData.TypeSymbol);
    }

    /// <summary>Obtains the collection element type for the supplied input.</summary>
    /// <param name="collectionType">The collection type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ITypeSymbol GetCollectionElementType(ITypeSymbol collectionType)
    {
        return collectionType switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: > 0 } named => named.TypeArguments[0],
            _ => null
        };
    }

    /// <summary>Obtains the array pool clear expression for the supplied input.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string GetArrayPoolClearExpression(LuminLocalFieldData fieldData, string elementType)
    {
        var element = GetFirstTypeArgument(fieldData);
        if (element?.IsUnmanagedType == true) return "false";
        if (TryGetIsUnmanaged(GetFirstTypeArgument(fieldData), out bool isUnmanaged))
        {
            return isUnmanaged ? "false" : "true";
        }

        return "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" +
               elementType + ">()";
    }

    /// <summary>Determines whether the metadata contains undetermined type parameter.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal static bool ContainsUndeterminedTypeParameter(ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsUndeterminedTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsUndeterminedTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol named =>
                (named.ContainingType is not null && ContainsUndeterminedTypeParameter(named.ContainingType)) ||
                named.TypeArguments.Any(ContainsUndeterminedTypeParameter),
            _ => false
        };
    }

    /// <summary>Fully qualified name of the bounded-size guarantee attribute.</summary>
    private const string BoundedAttributeName = "LuminPack.Attribute.LuminPackBoundedAttribute";

    /// <summary>Reads the guaranteed maximum serialized length declared by the bounded-size attribute.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="maxBytes">Receives the guaranteed maximum complete serialized length in bytes.</param>
    /// <returns>Whether the type declares a usable bounded-size guarantee.</returns>
    internal static bool TryGetBoundedMaxBytes(ITypeSymbol type, out int maxBytes)
    {
        maxBytes = 0;
        if (type is null) return false;

        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != BoundedAttributeName) continue;
            if (attribute.ConstructorArguments.Length != 1) return false;
            if (attribute.ConstructorArguments[0].Value is not int declared || declared <= 0) return false;
            maxBytes = declared;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to express the serialized upper bound of one value of the supplied type as C# source, using
    /// <c>Unsafe.SizeOf</c> for existing raw values or an explicit complete-record guarantee. Containers,
    /// nested object graphs, Union slots, Custom formatters and tolerant/circle-reference records are not
    /// expressible and therefore fall back to the per-element check.
    /// </summary>
    /// <param name="type">The type whose value is being written.</param>
    /// <param name="boundExpression">Receives a C# expression for the element bound.</param>
    /// <returns>Whether a statically expressible upper bound exists.</returns>
    internal static bool TryGetElementUpperBound(ITypeSymbol type, out string boundExpression)
    {
        boundExpression = null;
        if (type is null) return false;

        var portable = PortableStructPlan.TryCreate(type);
        if (portable != null)
        {
            boundExpression = portable.WireSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (TryGetBoundedMaxBytes(type, out int boundedMaxBytes))
        {
            // The guarantee covers the complete record, so a container can reserve count * MaxBytes once.
            boundExpression = boundedMaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            // String bounds require a container-local Length prescan rather than a constant element size.
            return false;
        }

        if (type is INamedTypeSymbol named && named.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE) &&
            TypeMetaChecker.CheckGeneratorType(named) != GeneratorType.Object) return false;
        if (type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.UnionAttribute")) return false;
        if (TryGetIsUnmanaged(type, out bool isUnmanaged) && isUnmanaged)
        {
            boundExpression = "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" +
                              FormatterTypeName.Get(type) + ">()";
            return true;
        }

        return false;
    }

}
