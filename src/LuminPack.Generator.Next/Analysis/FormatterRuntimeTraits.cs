using System.Linq;
using Microsoft.CodeAnalysis;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Checks optional formatter runtime APIs against the consumer metadata.</summary>
internal static class FormatterRuntimeTraits
{
    /// <summary>Checks the optional collection builder traversal and its element capture operation.</summary>
    /// <param name="compilation">The consumer's runtime references.</param>
    /// <param name="builder">The closed framework builder.</param>
    /// <returns>Whether generated code can use the complete value traversal.</returns>
    internal static bool HasBuilderTraversal(Compilation compilation, INamedTypeSymbol builder)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var traversal = access?.GetTypeMembers("BuilderTraversal", 1).SingleOrDefault();
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } &&
            traversal is { IsValueType: true, DeclaredAccessibility: Accessibility.Public } &&
            traversal.GetMembers("MoveNext").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Arity: 0, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 1 &&
                method.Parameters[0].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, traversal.TypeParameters[0])) &&
            access.GetMembers("TryGetBuilderTraversal").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
                method.Parameters[0].RefKind == RefKind.None && method.Parameters[1].RefKind == RefKind.Out &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, builder.OriginalDefinition.Construct(method.TypeParameters[0])) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, traversal.Construct(method.TypeParameters[0])));
    }

    /// <summary>Checks the existing listener-aware insertion API.</summary>
    /// <param name="compilation">The referenced runtime and framework metadata.</param>
    /// <returns>Whether the exact append operation is available.</returns>
    internal static bool HasObservableAppend(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var collection = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ObservableCollection`1");
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && collection is not null &&
            access.GetMembers("AppendObservable").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } &&
                method.Parameters.Length == 2 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
                !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasReferenceTypeConstraint &&
                !method.TypeParameters[0].HasConstructorConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, collection.Construct(method.TypeParameters[0])) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, method.TypeParameters[0]));
    }

    /// <summary>Checks the existing exact sequence storage API used to size fresh observable results.</summary>
    /// <param name="compilation">The referenced runtime and framework metadata.</param>
    /// <returns>Whether the public storage operation is available.</returns>
    internal static bool HasSequenceStorage(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var sequence = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && sequence is not null &&
            access.GetMembers("GetSequenceStorage").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Object &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasReferenceTypeConstraint &&
                !method.TypeParameters[0].HasConstructorConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, sequence.Construct(method.TypeParameters[0])));
    }

    /// <summary>Proves the Core staging lease and prefix release APIs are available in the consumer.</summary>
    /// <param name="compilation">The referenced runtime metadata.</param>
    /// <returns>Whether the generated lease can use Core-owned staging.</returns>
    internal static bool HasStaging(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminStaging`1");
        if (owner is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public })
            return false;
        var element = owner.TypeParameters[0];
        var array = compilation.CreateArrayTypeSymbol(element);
        return owner.GetMembers("Rent").OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 0, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, array) && method.Parameters.Length == 1 && IsCount(method.Parameters[0])) &&
            owner.GetMembers("Return").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 0, ReturnsVoid: true } &&
                method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, array) && IsCount(method.Parameters[1]));

        static bool IsCount(IParameterSymbol parameter) => parameter.RefKind == RefKind.None && parameter.Type.SpecialType == SpecialType.System_Int32;
    }

    /// <summary>Checks the framework grouping copy source's exact public signature.</summary>
    /// <param name="compilation">The referenced runtime and framework metadata.</param>
    /// <param name="grouping">The closed grouping being projected.</param>
    /// <returns>Whether the optional grouping optimization is callable.</returns>
    internal static bool HasFrameworkGroupingCopy(Compilation compilation, INamedTypeSymbol grouping)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var collection = compilation.GetTypeByMetadataName("System.Collections.Generic.ICollection`1");
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && collection != null &&
            access.GetMembers("GetFrameworkGroupingCopySource").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type,
                    grouping.OriginalDefinition.Construct(method.TypeParameters[0], method.TypeParameters[1])) &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, collection.Construct(method.TypeParameters[1])));
    }

    /// <summary>Checks for the bounded marker-prefixed nullable writer on the referenced binary state.</summary>
    /// <param name="compilation">The referenced runtime metadata.</param>
    /// <returns>Whether the generator can emit the bounded span fast path.</returns>
    internal static bool HasFramedNullableSpanWriter(Compilation compilation)
    {
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        var nullable = compilation.GetSpecialType(SpecialType.System_Nullable_T);
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        return writer is not null && span is not null &&
            writer.GetMembers("TryWriteFramedNullableSpan").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                method.TypeParameters[0].ConstraintTypes.IsEmpty && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, span.Construct(nullable.Construct(method.TypeParameters[0]))));
    }

    /// <summary>Checks for the bounded reader of a marker-prefixed nullable integer span.</summary>
    /// <param name="compilation">The consuming compilation and its runtime metadata.</param>
    /// <param name="element">The closed element type.</param>
    /// <returns>Whether the runtime supports reading the exact framed element.</returns>
    internal static bool HasFramedNullableInt32Reader(Compilation compilation, ITypeSymbol element)
    {
        if (element is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ||
            nullable.TypeArguments[0].SpecialType != SpecialType.System_Int32)
            return false;
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader");
        var span = compilation.GetTypeByMetadataName("System.Span`1")?.Construct(element);
        return reader != null && span != null &&
            reader.GetMembers("ReadFramedNullableInt32s").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Arity: 0, ReturnsVoid: true } &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, span));
    }

    /// <summary>Recognizes the Core operations for checked rectangular nullable storage blocks.</summary>
    /// <param name="compilation">The consuming compilation and its Core reference.</param>
    /// <returns>Whether the selected runtime exposes the complete block contract.</returns>
    internal static bool HasNullableRectangleStorage(Compilation compilation)
    {
        var operations = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackNullableRectangle");
        var layout = compilation.GetTypeByMetadataName("LuminPack.Utility.ILuminNullableStorageLayout");
        return operations is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } &&
            layout is { TypeKind: TypeKind.Interface, DeclaredAccessibility: Accessibility.Public } &&
            Has("Write", 4) && Has("Validate", 5) && Has("Read", 4);

        bool Has(string name, int count) => operations!.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2, ReturnsVoid: true } &&
            method.Parameters.Length == count && method.Parameters[0].RefKind == RefKind.Ref &&
            method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[1].HasValueTypeConstraint &&
            method.TypeParameters[1].ConstraintTypes.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(method.TypeParameters[1].ConstraintTypes[0], layout));
    }

    /// <summary>Checks the referenced runtime for the present nullable native-payload reader.</summary>
    /// <param name="compilation">The consuming compilation's runtime metadata.</param>
    /// <returns>Whether the exact advancing reader entry is available.</returns>
    internal static bool HasNativeNullablePayloadReader(Compilation compilation)
    {
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader");
        var nullable = compilation.GetSpecialType(SpecialType.System_Nullable_T);
        return reader != null && reader.GetMembers("ReadNativeNullablePayload").OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } &&
            method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
            method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.Ref &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, nullable.Construct(method.TypeParameters[0])));
    }

    /// <summary>Checks the optional owning reader for an exact scalar sorted list.</summary>
    /// <param name="compilation">The referenced framework and Core metadata.</param>
    /// <param name="list">The selected exact framework list.</param>
    /// <returns>Whether the reader has the expected public metadata.</returns>
    internal static bool HasSeededSortedListKeys(Compilation compilation, INamedTypeSymbol list)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader");
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var key = compilation.GetSpecialType(SpecialType.System_String);
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && reader != null && options != null &&
            access.GetMembers("TryReadSeededSortedListKeys").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2,
                    ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 3 &&
                method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, reader) &&
                method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type,
                    list.OriginalDefinition.Construct(key, method.TypeParameters[0])) &&
                method.Parameters[2].RefKind == RefKind.None && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32 &&
                method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
                method.TypeParameters[1].HasValueTypeConstraint && !method.TypeParameters[1].HasUnmanagedTypeConstraint &&
                method.TypeParameters[1].ConstraintTypes.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.TypeParameters[1].ConstraintTypes[0], options));
    }

    /// <summary>Checks the optional stable-key reader for the exact framework wrapper.</summary>
    /// <param name="compilation">The referenced Core and framework metadata.</param>
    /// <param name="wrapper">The selected framework wrapper.</param>
    /// <returns>Whether the exact public reader is available.</returns>
    internal static bool HasStableReadOnlyDictionaryRead(Compilation compilation, INamedTypeSymbol wrapper)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader");
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var key = compilation.GetSpecialType(SpecialType.System_String);
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && reader != null && options != null &&
            access.GetMembers("TryReadStableReadOnlyDictionary").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2,
                    ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
                method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, reader) &&
                method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type,
                    wrapper.OriginalDefinition.Construct(key, method.TypeParameters[0])) &&
                !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasReferenceTypeConstraint &&
                !method.TypeParameters[0].HasConstructorConstraint && !method.TypeParameters[0].HasNotNullConstraint &&
                method.TypeParameters[0].ConstraintTypes.IsEmpty &&
                method.TypeParameters[1].HasValueTypeConstraint && !method.TypeParameters[1].HasUnmanagedTypeConstraint &&
                method.TypeParameters[1].ConstraintTypes.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.TypeParameters[1].ConstraintTypes[0], options));
    }

    /// <summary>Checks the optional dictionary backing getter for a framework read-only wrapper.</summary>
    /// <param name="compilation">The consuming compilation and its Core references.</param>
    /// <param name="wrapper">The dictionary wrapper symbol.</param>
    /// <returns>Whether the referenced helper has the expected signature.</returns>
    internal static bool HasReadOnlyStringDictionaryStorage(Compilation compilation, INamedTypeSymbol wrapper)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
        var text = compilation.GetSpecialType(SpecialType.System_String);
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && dictionary != null &&
            access.GetMembers("GetReadOnlyStringDictionaryStorage").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1,
                    ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                method.TypeParameters[0] is { HasValueTypeConstraint: false, HasReferenceTypeConstraint: false, HasConstructorConstraint: false } parameter &&
                parameter.ConstraintTypes.IsEmpty &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type,
                    wrapper.OriginalDefinition.Construct(text, parameter)) &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, dictionary.Construct(text, parameter)));
    }

    /// <summary>Checks the binary writer that accepts an enumerable string wrapper.</summary>
    /// <param name="compilation">The referenced runtime and framework metadata.</param>
    /// <returns>Whether the exact profile-aware operation is available.</returns>
    internal static bool HasWrappedStringWriter(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var sequence = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        return access is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } &&
            options is not null && sequence is not null && writer is not null &&
            access.GetMembers("TryWriteWrappedStrings").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                method.TypeParameters[0].ConstraintTypes.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options) &&
                method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.Ref &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) && method.Parameters[1].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, sequence.Construct(compilation.GetSpecialType(SpecialType.System_String))));
    }

    /// <summary>Checks a complete collection operation against the actual referenced API.</summary>
    /// <param name="compilation">The referenced runtime and framework metadata.</param>
    /// <param name="collection">The framework collection type.</param>
    /// <param name="owner">The fully qualified operation owner.</param>
    /// <param name="name">The operation name.</param>
    /// <param name="state">The reader or writer type name.</param>
    /// <param name="result">The required result type.</param>
    /// <param name="takesCount">Whether the operation takes a count, with no profile type parameter.</param>
    /// <param name="nullableElement">Whether the element is the nullable form of the unmanaged parameter, with no profile.</param>
    /// <returns>Whether the exact public signature exists.</returns>
    internal static bool HasCollectionTransfer(Compilation compilation, INamedTypeSymbol collection, string owner, string name,
        string state, SpecialType result, bool takesCount, bool nullableElement = false)
    {
        var access = compilation.GetTypeByMetadataName(owner);
        var operationState = compilation.GetTypeByMetadataName("LuminPack.Core." + state);
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        bool hasProfile = !takesCount && !nullableElement;
        var nullable = nullableElement ? compilation.GetSpecialType(SpecialType.System_Nullable_T) : null;
        if (access is null || !access.IsStatic || access.DeclaredAccessibility != Accessibility.Public ||
            operationState is null || (hasProfile && options is null))
            return false;
        return access.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.DeclaredAccessibility == Accessibility.Public &&
            method.Arity == (hasProfile ? 2 : 1) && method.ReturnType.SpecialType == result &&
            !method.ReturnsByRef && !method.ReturnsByRefReadonly &&
            method.TypeParameters[method.Arity - 1].HasUnmanagedTypeConstraint &&
            method.TypeParameters[method.Arity - 1].ConstraintTypes.IsEmpty &&
            (!hasProfile || (method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                method.TypeParameters[0].ConstraintTypes.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options))) &&
            method.Parameters.Length == (takesCount ? 3 : 2) &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, operationState) &&
            method.Parameters[1].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, collection.OriginalDefinition.Construct(
                nullable?.Construct(method.TypeParameters[0]) ?? (ITypeSymbol)method.TypeParameters[method.Arity - 1])) &&
            (!takesCount || (method.Parameters[2].RefKind == RefKind.None && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32)));
    }

    /// <summary>Checks a profile-aware span operation on a reader or writer.</summary>
    /// <param name="compilation">The referenced runtime and framework metadata.</param>
    /// <param name="owner">The short reader or writer name.</param>
    /// <param name="name">The optional runtime operation.</param>
    /// <param name="element">The span's element type.</param>
    /// <param name="withCaptureOrder">Whether the operation takes a second argument for the input load order.</param>
    /// <returns>Whether the expected public overload exists.</returns>
    internal static bool HasReadOnlySpanWriter(Compilation compilation, string owner, string name, ITypeSymbol element, bool withCaptureOrder = false)
    {
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        var type = compilation.GetTypeByMetadataName("LuminPack.Core." + owner);
        if (options is null || span is null || type is null)
            return false;
        var closedSpan = span.Construct(element);
        return type.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            !method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.ReturnsVoid &&
            method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
            method.TypeParameters[0].ConstraintTypes.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options) &&
            method.Parameters.Length == (withCaptureOrder ? 2 : 1) && method.Parameters[0].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, closedSpan) &&
            (!withCaptureOrder || method.Parameters[1].RefKind == RefKind.None &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Boolean));
    }

    /// <summary>Checks the profile-aware string slot reader on the referenced binary runtime.</summary>
    /// <param name="compilation">The referenced runtime and framework metadata.</param>
    /// <param name="element">The string element of the destination span.</param>
    /// <returns>Whether a compatible public reader is available.</returns>
    internal static bool HasStringSpanReader(Compilation compilation, ITypeSymbol element)
    {
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader");
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var span = compilation.GetTypeByMetadataName("System.Span`1")?.Construct(element);
        return reader is not null && options is not null && span is not null &&
            reader.GetMembers("ReadStrings").OfType<IMethodSymbol>().Any(method =>
                !method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.ReturnsVoid &&
                method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                method.TypeParameters[0].ConstraintTypes.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options) &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, span));
    }

    /// <summary>Checks the two backing arrays supplied to a fresh sorted-list reader.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="list">The sorted list being formatted.</param>
    /// <returns>Whether the consumer exposes writable sorted-list storage.</returns>
    internal static bool HasSortedListArrays(Compilation compilation, INamedTypeSymbol list)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        return access?.GetMembers("TryGetSortedListStorage").OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2, Parameters.Length: 3,
                ReturnsByRef: false, ReturnsByRefReadonly: false, ReturnType.SpecialType: SpecialType.System_Boolean } &&
            method.Parameters[0].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, list.OriginalDefinition.Construct(method.TypeParameters[0], method.TypeParameters[1])) &&
            IsArray(method, 1) && IsArray(method, 2) && method.TypeParameters.All(type =>
                !type.HasValueTypeConstraint && !type.HasReferenceTypeConstraint && !type.HasConstructorConstraint && type.ConstraintTypes.IsEmpty)) == true;

        static bool IsArray(IMethodSymbol method, int index) =>
            method.Parameters[index].RefKind == RefKind.Out && method.Parameters[index].Type is IArrayTypeSymbol { IsSZArray: true } array &&
            SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[index - 1]);
    }

    /// <summary>Checks both halves of the checked sorted-list traversal.</summary>
    /// <param name="compilation">The consumer's referenced runtime.</param>
    /// <returns>Whether the runtime can capture and validate the matching sorted-list storage.</returns>
    internal static bool HasSortedListTraversal(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var list = compilation.GetTypeByMetadataName("System.Collections.Generic.SortedList`2");
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        if (access is null || list is null || span is null)
            return false;
        bool Matches(IMethodSymbol method, bool capture)
        {
            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Arity != 2 ||
                method.Parameters.Length != (capture ? 4 : 2) || method.Parameters[0].RefKind != RefKind.None ||
                !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, list.Construct(method.TypeParameters[0], method.TypeParameters[1])))
                return false;
            var version = method.Parameters[capture ? 3 : 1];
            if (version.Type.SpecialType != SpecialType.System_Int32 || version.RefKind != (capture ? RefKind.Out : RefKind.None))
                return false;
            return capture ? method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                method.Parameters[1].RefKind == RefKind.Out && method.Parameters[2].RefKind == RefKind.Out &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, span.Construct(method.TypeParameters[0])) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, span.Construct(method.TypeParameters[1])) : method.ReturnsVoid;
        }
        return access.GetMembers("TryGetSortedListTraversal").OfType<IMethodSymbol>().Any(method => Matches(method, true)) &&
            access.GetMembers("CheckSortedListTraversalVersion").OfType<IMethodSymbol>().Any(method => Matches(method, false));
    }

    /// <summary>Checks the read-only structural hint before emitting a presized JSON list.</summary>
    /// <param name="compilation">The consuming compilation and its runtime references.</param>
    /// <returns>Whether the runtime exposes the profile-aware capacity hint.</returns>
    internal static bool HasJsonArrayCapacityHint(Compilation compilation)
    {
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader");
        return options != null && reader?.GetMembers("GetArrayCapacityHint").OfType<IMethodSymbol>().Any(method =>
            !method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public &&
            !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.ReturnType.SpecialType == SpecialType.System_Int32 &&
            method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
            method.TypeParameters[0].ConstraintTypes.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options) &&
            method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_Int32) == true;
    }

    /// <summary>Recognizes established raw framework scalar storage.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    private static bool IsKnownScalar(ITypeSymbol type) => type != null && type.IsUnmanagedType && (type.SpecialType != SpecialType.None || FrameworkScalarBody.Contains(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    /// <summary>Checks the compiled string-key comparer guard against the referenced reader.</summary>
    /// <param name = "compilation">The consuming compilation and its runtime references.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    internal static bool HasEnumeratedStringKeyCheck(Compilation compilation) => compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackReader")?.GetMembers("CanReuseEnumeratedStringKey").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && method.Parameters[0].Type.ToDisplayString() == "System.Collections.Generic.IEqualityComparer<string>") == true;
    /// <summary>Checks the closed small native storage family and the referenced runtime's grouped-store capability.</summary>
    /// <param name = "element">The exact collection element type.</param>
    /// <param name = "compilation">The consumer compilation supplying the actual runtime API.</param>
    /// <returns>Whether generation may select the one-/two-byte dense hash-set writer.</returns>
    internal static bool CanGroupSmallHashSetWrites(ITypeSymbol element, Compilation compilation)
    {
        if (!IsKnownScalar(element))
            return false;
        bool small = element.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Char || element.MetadataName == "Half" && element.ContainingNamespace.ToDisplayString() == "System";
        return small && compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal")?.GetMembers("WriteSmallHashSetRaw").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.Ref && method.ReturnsVoid) == true;
    }

    /// <summary>Checks the runtime's optional fresh sorted-set builder.</summary>
    /// <param name = "element">The closed element formatter contract.</param>
    /// <param name = "compilation">The consumer compilation supplying the referenced runtime.</param>
    /// <returns>Whether a fresh set may avoid the public constructor's sort for ordered input.</returns>
    /// <remarks>Any element type qualifies: the builder uses the same default comparer as the constructor, checking the
    /// order with one comparison per element and falling back to the constructor otherwise.</remarks>
    internal static bool CanBuildSortedSet(ITypeSymbol element, Compilation compilation)
    {
        return element is not null && compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage")?.GetMembers("CreateSortedSet").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.None && method.Parameters[1].RefKind == RefKind.None && method.Parameters[0].Type is IArrayTypeSymbol { Rank: 1 } array && SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 && method.ReturnType is INamedTypeSymbol { MetadataName: "SortedSet`1", TypeArguments.Length: 1 } result && result.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" && SymbolEqualityComparer.Default.Equals(result.TypeArguments[0], method.TypeParameters[0])) == true;
    }

    /// <summary>Checks both optional mutable sorted-set helpers.</summary>
    /// <param name = "element">The independently formatted element contract.</param>
    /// <param name = "compilation">The consumer compilation supplying the actual runtime API.</param>
    /// <returns>Whether generation may use the existing shared element pool and ordered reconstruction.</returns>
    /// <remarks>The runtime admits only destinations using the default comparer, so user comparers keep Clear/Add.</remarks>
    internal static bool CanRebuildSortedSet(ITypeSymbol element, Compilation compilation)
    {
        if (element is null)
            return false;
        var owner = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        bool Common(IMethodSymbol method) => method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) && method.Parameters[0].Type is INamedTypeSymbol { MetadataName: "SortedSet`1", TypeArguments.Length: 1 } set && set.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" && SymbolEqualityComparer.Default.Equals(set.TypeArguments[0], method.TypeParameters[0]);
        return owner?.GetMembers("CanRebuildSortedSet").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 1 && Common(method)) == true && owner.GetMembers("TryRebuildSortedSet").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 3 && Common(method) && method.Parameters[1].Type is IArrayTypeSymbol { Rank: 1 } array && SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32);
    }

    /// <summary>Checks string-key eligibility and the complete optional ordered-list append signatures.</summary>
    /// <param name = "key">The selected key formatter contract.</param>
    /// <param name = "compilation">The consumer compilation supplying the actual runtime API.</param>
    /// <returns>Whether generated reads may bypass a certified append's binary search.</returns>
    internal static bool CanAppendSortedListOrdered(ITypeSymbol key, Compilation compilation)
    {
        if (key?.SpecialType != SpecialType.System_String)
            return false;
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        bool Common(IMethodSymbol method) => method.IsStatic && method.Arity == 2 && method.DeclaredAccessibility == Accessibility.Public && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) && method.Parameters[0].Type is INamedTypeSymbol { MetadataName: "SortedList`2", TypeArguments.Length: 2 } list && list.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" && SymbolEqualityComparer.Default.Equals(list.TypeArguments[0], method.TypeParameters[0]) && SymbolEqualityComparer.Default.Equals(list.TypeArguments[1], method.TypeParameters[1]);
        return owner?.GetMembers("CanAppendSortedListOrdered").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 1 && Common(method)) == true && owner.GetMembers("TryAppendSortedListOrdered").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 4 && Common(method) && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, method.TypeParameters[0]) && SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, method.TypeParameters[1]) && method.Parameters[3].Type.SpecialType == SpecialType.System_Boolean);
    }

    /// <summary>Checks a referenced public sequence factory instead of inferring its API from target symbols.</summary>
    /// <param name = "compilation">The consuming compilation and its actual immutable collection references.</param>
    /// <param name = "name">The concrete immutable sequence family name.</param>
    /// <returns>Whether the factory can construct the sequence from the complete buffered span.</returns>
    internal static bool CanCreateImmutableSequenceFromSpan(Compilation compilation, string name)
    {
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        var sequence = compilation.GetTypeByMetadataName("System.Collections.Immutable." + name + "`1");
        return span is not null && sequence is not null && compilation.GetTypeByMetadataName("System.Collections.Immutable." + name)?.GetMembers("Create").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 && method.TypeParameters[0].ConstraintTypes.IsEmpty && !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint && !method.TypeParameters[0].HasConstructorConstraint && !method.TypeParameters[0].HasNotNullConstraint && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, span.Construct(method.TypeParameters[0])) && SymbolEqualityComparer.Default.Equals(method.ReturnType, sequence.Construct(method.TypeParameters[0]))) == true;
    }

    /// <summary>Checks whether a consumer may initialize the mutable memory layout before JIT compilation.</summary>
    /// <param name="compilation">The consumer's actual runtime and framework metadata.</param>
    /// <returns>Whether the required initializer attribute and runtime method are available.</returns>
    internal static bool HasMemoryStorageInitializer(Compilation compilation) =>
        compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is { DeclaredAccessibility: Accessibility.Public } &&
        compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal")?.GetMembers("InitializeMemoryStorage")
            .OfType<IMethodSymbol>().Any(method => method is { IsStatic: true, Arity: 0, DeclaredAccessibility: Accessibility.Public, ReturnsVoid: true } &&
                method.Parameters.Length == 0) == true;

    /// <summary>Checks the exact optional portable bit-storage writer signature.</summary>
    /// <param name = "compilation">The consumer's referenced runtime metadata.</param>
    /// <returns>Whether the direct writer can be emitted.</returns>
    internal static bool CanWriteBitArrayPayload(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var bits = compilation.GetTypeByMetadataName("System.Collections.BitArray");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        return access is not null && bits is not null && writer is not null && access.GetMembers("TryWriteBitArrayPayload").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 0 && method.DeclaredAccessibility == Accessibility.Public && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) && method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, bits));
    }

    /// <summary>Checks the complete optional fresh bit-array construction signature.</summary>
    /// <param name = "compilation">The consumer's actual runtime and framework metadata.</param>
    /// <returns>Whether portable readers can fill final bit storage.</returns>
    internal static bool CanCreateBitArrayPayload(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var bits = compilation.GetTypeByMetadataName("System.Collections.BitArray");
        var span = compilation.GetTypeByMetadataName("System.Span`1");
        return access is not null && bits is not null && span is not null && access.GetMembers("TryCreateBitArrayPayload").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 0 && method.DeclaredAccessibility == Accessibility.Public && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 3 && method.Parameters[0].RefKind == RefKind.None && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[1].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, bits) && method.Parameters[2].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, span.Construct(compilation.GetSpecialType(SpecialType.System_Byte))));
    }
}
