using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Identifies optional ordered collection kernels exposed by the referenced Core.</summary>
internal static class FormatterOrderedCollectionTraits
{
    /// <summary>Checks the default-priority fast fill for a compatible fixed pair.</summary>
    /// <param name="compilation">The actual consumer references.</param>
    /// <param name="collection">The closed framework queue.</param>
    /// <returns>Whether the checked signature and both element contracts match.</returns>
    internal static bool HasPriorityReader(Compilation compilation, INamedTypeSymbol collection)
    {
        if (!collection.TypeArguments[0].IsUnmanagedType || collection.TypeArguments[0].SpecialType == SpecialType.System_Boolean ||
            collection.TypeArguments[1].SpecialType is not (SpecialType.System_Int32 or SpecialType.System_Int64 or
                SpecialType.System_UInt32 or SpecialType.System_UInt64))
            return false;
        return HasPriorityStorageTransfer(compilation, collection, "TryReadPriorityQueueStorage", "LuminPackReader", 3);
    }

    /// <summary>Checks the exact binary storage writer accepted by the pair formatter.</summary>
    /// <param name="compilation">The actual consumer references.</param>
    /// <param name="collection">The closed framework queue.</param>
    /// <returns>Whether the checked signature and both fixed element types match.</returns>
    internal static bool HasPriorityWriter(Compilation compilation, INamedTypeSymbol collection) =>
        collection.TypeArguments.All(type => type.IsUnmanagedType) &&
        HasPriorityStorageTransfer(compilation, collection, "TryWritePriorityQueueStorage", "LuminPackWriter", 2);

    /// <summary>Matches the reader or writer state, framework queue and optional collection count.</summary>
    /// <param name="compilation">The actual consumer references.</param>
    /// <param name="collection">The closed framework queue.</param>
    /// <param name="name">The runtime operation name.</param>
    /// <param name="state">The serializer state type.</param>
    /// <param name="parameters">The number of parameters required by the operation.</param>
    /// <returns>Whether the runtime publishes the complete signature.</returns>
    private static bool HasPriorityStorageTransfer(Compilation compilation, INamedTypeSymbol collection, string name, string state, int parameters)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var stateType = compilation.GetTypeByMetadataName("LuminPack.Core." + state);
        return stateType != null && access?.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean && method.TypeParameters.All(type => type.HasUnmanagedTypeConstraint) &&
            method.Parameters.Length == parameters && method.Parameters[0].RefKind == RefKind.Ref &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType) &&
            method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type,
                collection.OriginalDefinition.Construct(method.TypeParameters[0], method.TypeParameters[1])) &&
            (parameters == 2 || method.Parameters[2].RefKind == RefKind.None &&
                method.Parameters[2].Type.SpecialType == SpecialType.System_Int32)) == true;
    }

    /// <summary>Checks the checked heap-span and version operations against the actual framework tuple.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="collection">The closed priority queue.</param>
    /// <returns>Whether both operations accept the exact generated signatures.</returns>
    internal static bool HasPriorityTraversal(Compilation compilation, INamedTypeSymbol collection)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        var tuple = compilation.GetTypeByMetadataName("System.ValueTuple`2");
        if (access == null || span == null || tuple == null)
            return false;
        return access.GetMembers("TryGetPriorityQueueTraversal").OfType<IMethodSymbol>().Any(method => Matches(method, true)) &&
            access.GetMembers("CheckPriorityQueueTraversalVersion").OfType<IMethodSymbol>().Any(method => Matches(method, false));

        bool Matches(IMethodSymbol method, bool capture)
        {
            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Arity != 2 ||
                method.Parameters.Length != (capture ? 3 : 2) || method.Parameters[0].RefKind != RefKind.None ||
                !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type,
                    collection.OriginalDefinition.Construct(method.TypeParameters[0], method.TypeParameters[1])))
                return false;
            var version = method.Parameters[capture ? 2 : 1];
            if (version.Type.SpecialType != SpecialType.System_Int32 || version.RefKind != (capture ? RefKind.Out : RefKind.None))
                return false;
            return capture ? method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                method.Parameters[1].RefKind == RefKind.Out && method.Parameters[1].Type is INamedTypeSymbol range &&
                SymbolEqualityComparer.Default.Equals(range.OriginalDefinition, span) &&
                range.TypeArguments[0] is INamedTypeSymbol pair &&
                SymbolEqualityComparer.Default.Equals((pair.TupleUnderlyingType ?? pair).OriginalDefinition, tuple) &&
                SymbolEqualityComparer.Default.Equals(pair.TypeArguments[0], method.TypeParameters[0]) &&
                SymbolEqualityComparer.Default.Equals(pair.TypeArguments[1], method.TypeParameters[1]) : method.ReturnsVoid;
        }
    }

    /// <summary>Captures the stack and queue operations usable by one closed sequence.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="collection">The closed framework collection.</param>
    /// <param name="isStack">Whether the sequence is a stack.</param>
    /// <param name="result">The available runtime operations.</param>
    internal static void Capture(Compilation compilation, INamedTypeSymbol collection, bool isStack, List<string> result)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        if (access == null || options == null)
            return;

        var element = collection.TypeArguments[0];
        if (element.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 &&
            HasKernel(isStack ? "TryWriteIntegerStackJson" : "TryWriteIntegerQueueJson", "LuminPackJsonWriter", 2, false, false))
            result.Add("OrderedIntegerJson");
        if (!isStack)
        {
            if (element.SpecialType == SpecialType.System_String &&
                HasKernel("TryWriteStringQueueStorage", "LuminPackWriter", 1, true, true, SpecialType.System_Boolean))
                result.Add("WriteStringQueueStorage");
            if (element.IsUnmanagedType && HasKernel("TryWriteQueueRecord", "LuminPackWriter", 1, true, false))
                result.Add("WriteQueueRecord");
            return;
        }
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        if (span != null && access.GetMembers("RestoreStackFromTop").OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, ReturnsVoid: true, Arity: 1 } &&
            method.Parameters.Length == 2 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, collection.OriginalDefinition.Construct(method.TypeParameters[0])) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, span.Construct(method.TypeParameters[0]))))
            result.Add("RestoreStackFromTop");
        if (element.SpecialType == SpecialType.System_String)
        {
            if (HasKernel("TryWriteStringStackStorage", "LuminPackWriter", 1, true, true, SpecialType.System_Boolean))
                result.Add("WriteStringStackStorage");
            if (HasKernel("TryReadStringStackStorage", "LuminPackReader", 1, true, true))
                result.Add("ReadStringStackStorage");
        }
        else if (element.IsUnmanagedType)
        {
            if (HasKernel("TryWriteStackRecord", "LuminPackWriter", 1, true, false))
                result.Add("WriteStackRecord");
            if (HasKernel("TryWriteStackStorage", "LuminPackWriter", 1, true, false))
                result.Add("WriteStackStorage");
            if (HasKernel("TryReadStackStorage", "LuminPackReader", 1, true, false))
                result.Add("ReadStackStorage");
        }

        bool HasKernel(string name, string state, int arity, bool hasCount, bool text, SpecialType final = SpecialType.System_Int32)
        {
            var stateType = compilation.GetTypeByMetadataName("LuminPack.Core." + state);
            return stateType != null && access.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Arity == arity &&
                method.Parameters.Length == (hasCount ? 3 : 2) && method.Parameters[0].RefKind == RefKind.Ref &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType) && method.Parameters[1].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, text ? collection :
                    collection.OriginalDefinition.Construct(method.TypeParameters[arity - 1])) &&
                (text || method.TypeParameters[arity - 1].HasUnmanagedTypeConstraint) &&
                (arity == 1 && !text || method.TypeParameters[0].HasValueTypeConstraint &&
                    !method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options)) &&
                (!hasCount || method.Parameters[2].RefKind == RefKind.None &&
                    method.Parameters[2].Type.SpecialType == final));
        }
    }
}
