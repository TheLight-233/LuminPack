using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Builds one type's operation templates before concrete entry-point emission.</summary>
    /// <param name="type">The closed formatter type.</param>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <param name="guardNestedReads">Whether an enclosing external root needs typed child guards.</param>
    /// <returns>Only this type's operations and the layout declarations required to bind them.</returns>
    internal static string GenerateOperationTemplates(ITypeSymbol type, Compilation compilation, MetaInfo metadata, bool guardNestedReads = false)
    {
        var body = new StringBuilder();
        string name = FormatterTypeName.Get(type);
        // Only valid non-Union packable roots declare a JSON helper. Built-in and Union
        // operations already have their final container shape and need no helper projection.
        bool projectJsonHelper = false;
        if (type is INamedTypeSymbol named && CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named))
        {
            // Preserve the original generic definition's wire decisions. Analyzing a closed
            // construction can classify a formerly generic field as unmanaged and change framing.
            var data = LuminPackSourceGenerator.GetSerializationMetadata(named.OriginalDefinition, compilation, metadata);
            using var unmanagedContract = named.IsGenericType && !SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition)
                ? new UnmanagedContractScope(named, compilation) : null;
            name = FormatterTypeName.Get(named.OriginalDefinition);
            // Invalid models keep their original formatter boundary because their helper is absent.
            projectJsonHelper = !data.isUnion && !data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            if (data.isUnion && data.isValueType) LuminPackUnionCodeGenerator.GenerateCaseMapStruct(data, body);
            GenerateRootBinaryExtensions(body, data, name, metadata, SupportsFixedVersionPrefix(data, compilation),
                guardNestedReads || CompiledOptions.Resolve(type, compilation).Input.Enabled);
            GenerateRootJsonExtensions(body, data, name, metadata);
            var registry = new LuminPackLocalLayoutGenerator.LocalLayoutRegistry();
            LuminPackLocalLayoutGenerator.AppendRootLocalClass(body, registry, data);
            LuminPackLocalLayoutGenerator.AppendNestedLocalClasses(body, registry, data.fields);
            LuminPackLocalLayoutGenerator.AppendLocalMemberAccessors(body, registry, data.fields);
        }
        else
        {
            GenerateFormatterExtensions(body, CreateOperationField(type, compilation, metadata), metadata, new HashSet<string>(StringComparer.Ordinal));
        }
        PortableStructEmitter.AppendViews(body, type);
        string source = GenerateExtension(body, GetExtensionClassName(compilation));
        bool ownsExternalHelper = type is INamedTypeSymbol external &&
            CompilationTypeAnalysisCache.GetOrCreate(compilation).IsAssemblyPackable(external) &&
            !SymbolEqualityComparer.Default.Equals(external.ContainingAssembly, compilation.Assembly);
        return projectJsonHelper ? JsonOperationTemplates.Prepare(source, metadata, ownsExternalHelper) : source;
    }

    /// <summary>Attaches runtime capabilities, canonical cursor dispatch and collection reuse facts to a built-in formatter operation.</summary>
    /// <param name="type">The exact operation type.</param>
    /// <param name="compilation">The current consuming compilation.</param>
    /// <param name="metadata">Complete current emission settings.</param>
    /// <returns>Field metadata with no new serialization contract analysis in the collection emitter.</returns>
    private static LuminLocalFieldData CreateOperationField(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        var field = new LuminLocalFieldData { TypeName = FormatterTypeName.Get(type), TypeSymbol = type, Name = "value", IsValue = type.IsValueType };
        if (field.TypeName == "global::System.TimeZoneInfo")
        {
            var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
            var methods = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?
                .GetMembers("ReadTimeZoneInfo").OfType<IMethodSymbol>().Where(method => !method.IsStatic &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 0 &&
                    !method.ReturnsByRef && !method.ReturnsByRefReadonly && SymbolEqualityComparer.Default.Equals(method.ReturnType, type) &&
                    (method.Arity == 0 || method.Arity == 1 && method.TypeParameters[0].HasValueTypeConstraint &&
                        !method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.Length == 1 &&
                        SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options))).ToArray();
            field.CanReadJsonTimeZoneInfo = methods != null && methods.Any(method => method.Arity == 0) && methods.Any(method => method.Arity == 1);
        }
        if (field.TypeName == "global::System.Numerics.BigInteger")
        {
            var bytes = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")?.Construct(compilation.GetSpecialType(SpecialType.System_Byte));
            field.CanReadBinaryBigInteger = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal")?
                .GetMembers("ReadBigInteger").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.ReturnsVoid && method.Arity == 0 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 2 &&
                    method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, bytes) &&
                    method.Parameters[1].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, type)) == true;
            field.CanWriteJsonBigInteger = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?
                .GetMembers("WriteBigInteger").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 &&
                    method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true;
            field.CanReadJsonBigInteger = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?
                .GetMembers("ReadBigInteger").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 0 &&
                    SymbolEqualityComparer.Default.Equals(method.ReturnType, type)) == true;
        }
        if (field.TypeName == "global::System.Version")
        {
            field.CanWriteJsonVersion = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?
                .GetMembers("WriteVersion").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 &&
                    method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true;
            field.CanReadJsonVersion = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?
                .GetMembers("ReadVersion").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 0 &&
                    SymbolEqualityComparer.Default.Equals(method.ReturnType, type)) == true;
        }
        if (field.TypeName == "global::System.Text.StringBuilder" && type is INamedTypeSymbol builder)
        {
            field.CanReadJsonStringBuilder = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?
                .GetMembers("ReadStringBuilder").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 &&
                    method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true;
            field.CanWriteJsonStringBuilder = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?
                .GetMembers("WriteStringBuilder").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 &&
                    method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true;
            field.CanCopyStringBuilder = builder.GetMembers("CopyTo").OfType<IMethodSymbol>().Any(method => !method.IsStatic &&
                method.DeclaredAccessibility == Accessibility.Public && method.ReturnsVoid && method.Arity == 0 && method.Parameters.Length == 3 &&
                method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32 &&
                method.Parameters[1].Type is INamedTypeSymbol { MetadataName: "Span`1", TypeArguments.Length: 1 } span &&
                span.ContainingNamespace.ToDisplayString() == "System" && span.TypeArguments[0].SpecialType == SpecialType.System_Char);
        }
        if (field.TypeName is "global::System.DateOnly" or "global::System.TimeOnly" or "global::System.Int128" or "global::System.UInt128")
            field.CanFormatJsonScalar = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?
                .GetMembers("Write").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 &&
                    method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true;
        if (type is INamedTypeSymbol { MetadataName: "ImmutableQueue`1" or "IImmutableQueue`1", TypeArguments.Length: 1 } queue &&
            queue.ContainingNamespace.ToDisplayString() == "System.Collections.Immutable")
            field.CanCreateImmutableQueueFromSpan = EmitterTypeTraits.CanCreateImmutableQueueFromSpan(compilation);
        if (type is INamedTypeSymbol { MetadataName: "SortedSet`1", TypeArguments.Length: 1 } sortedSet &&
            sortedSet.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
        {
            field.CanBuildSortedSet = EmitterTypeTraits.CanBuildSortedSet(sortedSet.TypeArguments[0], compilation);
            field.CanRebuildSortedSet = EmitterTypeTraits.CanRebuildSortedSet(sortedSet.TypeArguments[0], compilation);
        }
        if (type is INamedTypeSymbol { TypeArguments.Length: > 0 } sequence)
            field.CanWriteJsonEnumerationElement = EmitterTypeTraits.HasJsonEnumerationWriter(compilation, sequence.TypeArguments[0]);
        if (type is INamedTypeSymbol { TypeArguments.Length: 2 } entry)
            field.CanWriteJsonIntegerEntry = EmitterTypeTraits.HasJsonIntegerEntryWriter(compilation, entry.TypeArguments[0], entry.TypeArguments[1]);
        if (type is INamedTypeSymbol { MetadataName: "SortedList`2", TypeArguments.Length: 2 } sortedList &&
            sortedList.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
        {
            field.CanAppendSortedListOrdered = EmitterTypeTraits.CanAppendSortedListOrdered(sortedList.TypeArguments[0], compilation);
            field.CanTraverseSortedList = EmitterTypeTraits.HasSortedListTraversal(compilation);
        }
        if (type is INamedTypeSymbol { MetadataName: "HashSet`1" or "ISet`1" or "IReadOnlySet`1", TypeArguments.Length: 1 } hashSet &&
            hashSet.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
            field.CanGroupSmallHashSetWrites = EmitterTypeTraits.CanGroupSmallHashSetWrites(hashSet.TypeArguments[0], compilation);
        if (type is INamedTypeSymbol { MetadataName: "PriorityQueue`2" } priorityQueue &&
            priorityQueue.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
            field.CanAccessPriorityQueueNodes = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess")?
                .GetMembers("TryGetPriorityQueueNodes").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public) == true;
        if (type is INamedTypeSymbol { MetadataName: "Dictionary`2", TypeArguments.Length: 2 } dictionary &&
            dictionary.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
        {
            var cursorPlan = FixedUnionCursorPlan.GetRoot(dictionary.TypeArguments[1], compilation, metadata);
            field.CanReadStableStringKeys = metadata.IsNet8 && !metadata.IsForUnity &&
                !metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", System.StringComparison.Ordinal)) &&
                EmitterTypeTraits.CanReadStableStringKeys(dictionary.TypeArguments[0], dictionary.TypeArguments[1], compilation);
            field.UnionCursorSuffix = cursorPlan?.Suffix;
            field.UnionCursorCases = cursorPlan?.Ordered;
            if (cursorPlan != null)
            {
                var declared = CompiledOptions.Resolve(dictionary.TypeArguments[1], compilation, importedScope: false).UnionProfile(false);
                field.UnionCursorEncoding = declared.Encoding;
                field.UnionCursorRecording = declared.Recording;
            }
            field.CanReuseDictionarySlots = dictionary.TypeArguments[0].SpecialType == SpecialType.System_Int32 &&
                DictionarySlotReusePlan.CanReadExistingValue(dictionary.TypeArguments[1], compilation, metadata);
            field.CanReuseDictionaryScalarSlots = dictionary.TypeArguments[0].SpecialType == SpecialType.System_Int32 && !field.CanReuseDictionarySlots &&
                metadata.IsNet8 && !metadata.IsForUnity &&
                !metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", System.StringComparison.Ordinal)) &&
                LuminPack.SourceGenerator.CodeEmitters.DictionaryEmitter.IsScalarSlotValue(dictionary.TypeArguments[1]);
            if (dictionary.TypeArguments[0].SpecialType == SpecialType.System_Int32 && !field.CanReuseDictionarySlots)
                field.UnionSlotCases = DictionaryUnionSlotPlan.GetCases(dictionary.TypeArguments[1], compilation, metadata);
        }
        return field;
    }
}
