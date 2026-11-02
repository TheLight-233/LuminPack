using Microsoft.CodeAnalysis;

try
{
    var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();
    if (args is ["--verify-json-reuse"])
    {
        SerializerJsonReuseRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-registered-dispatch"])
    {
        SerializerGenericOverloadRegression.VerifyRegisteredOnly(references);
        return 0;
    }
    if (args is ["--verify-immutable-scalar-set-reuse"])
    {
        SerializerImmutableScalarSetReuseRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-immutable-string-set-reuse"])
    {
        SerializerImmutableStringSetReuseRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-scalar-storage-reuse"])
    {
        SerializerScalarStorageReuseRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-framework-entries"])
    {
        SerializerFrameworkEntryRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-immutable-list-writes"])
    {
        SerializerImmutableListWriteRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-string-list-reads"])
    {
        SerializerStringListReadRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-compound-string-reuse"])
    {
        SerializerCompoundStringReuseRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-raw-sequence-writes"])
    {
        SerializerSequenceRawWriteRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-immutable-queue-construction"])
    {
        SerializerImmutableQueueConstructionRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-reference-property-metadata"])
    {
        SerializerReferencePropertyMetadataRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-reference-collection-reuse"])
    {
        SerializerReferenceCollectionReuseRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-timezone-cache"])
    {
        SerializerTimeZoneCacheRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-scalar-interface-reads"])
    {
        SerializerScalarInterfaceReadRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-scalar-interface-writes"])
    {
        SerializerScalarInterfaceWriteRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-small-hashset-writes"])
    {
        SerializerSmallHashSetWriteRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-sorted-set-construction"])
    {
        SerializerSortedSetConstructionRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-priority-queue-compatibility"])
    {
        SerializerPriorityQueueCompatibilityRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-assembly-packable"])
    {
        SerializerAssemblyPackableRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-external-union"])
    {
        SerializerExternalUnionRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-special-modes"])
    {
        SerializerSpecialModeBoundaryRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-named-versions"])
    {
        SerializerNamedVersionRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-dictionary-slots"])
    {
        SerializerDictionarySlotRegression.Run(references);
        SerializerDictionaryUnionSlotRegression.Run(references);
        SerializerDictionaryScalarSlotRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-portable-structs"])
    {
        SerializerPortableStructRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-audit-layouts"])
    {
        SerializerAuditLayoutRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-imported-layouts"])
    {
        SerializerImportedLayoutRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-custom"])
    {
        SerializerCustomFormatterRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-partitions"])
    {
        SerializerPartitionRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-shared-facades"])
    {
        SerializerSharedFacadeRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-inlining"])
    {
        SerializerBinaryInliningRegression.Run(references);
        SerializerCollectionInliningRegression.Run(references);
        SerializerUnionInliningRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-mechanisms"])
    {
        SerializerMechanismRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-union-inlining"])
    {
        SerializerUnionInliningRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-codegen-performance"])
    {
        SerializerCodegenPerformanceRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-advancing-api"])
    {
        SerializerAdvancingApiRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-source-presentation"])
    {
        SerializerSourcePresentationRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-union-cursor"])
    {
        SerializerUnionCursorRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-capacity-tiers"])
    {
        SerializerCapacityTierRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-formatter-dispatch"])
    {
        SerializerFormatterDispatchRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-generic-overloads"])
    {
        SerializerGenericOverloadRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-compiled-options"])
    {
        SerializerCompiledOptionRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-input-facts"])
    {
        SerializerCompiledOptionRegression.VerifyInputFacts(references);
        Console.WriteLine("Passed untrusted-input read-site check contracts.");
        return 0;
    }
    if (args is ["--verify-documentation"])
    {
        SerializerDocumentationRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-operation-cache"])
    {
        SerializerOperationCacheRegression.Run(references);
        return 0;
    }
    SerializerImportedLayoutRegression.Run(references);
    SerializerDocumentationRegression.Run(references);
    SerializerRootApiRegression.Run(references);
    SerializerAdvancingApiRegression.Run(references);
    SerializerAssemblyPackableRegression.Run(references);
    SerializerGenericOverloadRegression.Run(references);
    SerializerFormatterDispatchRegression.Run(references);
    SerializerReviewRegression.Run(references);
    SerializerCustomFormatterRegression.Run(references);
    SerializerPartitionRegression.Run(references);
    SerializerSharedFacadeRegression.Run(references);
    SerializerBinaryInliningRegression.Run(references);
    SerializerCollectionInliningRegression.Run(references);
    SerializerUnionInliningRegression.Run(references);
    SerializerExternalUnionRegression.Run(references);
    SerializerMechanismRegression.Run(references);
    SerializerCodegenPerformanceRegression.Run(references);
    SerializerTimeZoneCacheRegression.Run(references);
    SerializerImmutableQueueConstructionRegression.Run(references);
    SerializerScalarInterfaceReadRegression.Run(references);
    SerializerScalarInterfaceWriteRegression.Run(references);
    SerializerSmallHashSetWriteRegression.Run(references);
    SerializerSortedSetConstructionRegression.Run(references);
    SerializerReferencePropertyMetadataRegression.Run(references);
    SerializerReferenceCollectionReuseRegression.Run(references);
    SerializerSourcePresentationRegression.Run(references);
    SerializerUnionCursorRegression.Run(references);
    SerializerCapacityTierRegression.Run(references);
    SerializerJsonReuseRegression.Run(references);
    SerializerCompiledOptionRegression.Run(references);
    SerializerSpecialModeBoundaryRegression.Run(references);
    SerializerNamedVersionRegression.Run(references);
    SerializerDictionarySlotRegression.Run(references);
    SerializerDictionaryUnionSlotRegression.Run(references);
    SerializerDictionaryScalarSlotRegression.Run(references);
    SerializerPortableStructRegression.Run(references);
    SerializerAuditLayoutRegression.Run(references);
    MetadataReference[]? compatibilityReferences = args.Length == 2
        ? Directory.GetFiles(args[0], "*.dll").Select(path => MetadataReference.CreateFromFile(path)).Append(MetadataReference.CreateFromFile(args[1])).ToArray()
        : null;
    SerializerOperationCacheRegression.Run(references, compatibilityReferences);
    Console.WriteLine("Passed generated overload XML documentation and serialization contract checks.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

/// <summary>Supplies concrete value-returning read helpers to fixtures that use them.</summary>
internal static class TestCompilation
{
    private static readonly string ConcreteReadsSource = ReadSource();

    /// <summary>Creates a fixture with source helpers compiled against its own framework references.</summary>
    /// <param name="assemblyName">The fixture assembly identity.</param>
    /// <param name="syntaxTrees">The original fixture sources.</param>
    /// <param name="references">The fixture's runtime and framework references.</param>
    /// <param name="options">The original compilation settings.</param>
    /// <returns>The fixture, with a helper tree only when its sources require one.</returns>
    internal static Microsoft.CodeAnalysis.CSharp.CSharpCompilation Create(string? assemblyName,
        IEnumerable<SyntaxTree>? syntaxTrees = null, IEnumerable<MetadataReference>? references = null,
        Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions? options = null)
    {
        var trees = syntaxTrees?.ToArray() ?? [];
        if (trees.Any(tree => tree.GetText().ToString().Contains("global::LuminPackUnitTest.ConcreteReads.", StringComparison.Ordinal)))
            trees = [.. trees, Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(ConcreteReadsSource,
                (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions)trees[0].Options, "ConcreteReads.cs")];
        return Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(assemblyName, trees, references, options);
    }

    /// <summary>Reads the shared helper text without depending on a working directory.</summary>
    /// <returns>The independently compiled helper declarations.</returns>
    private static string ReadSource()
    {
        using var stream = typeof(TestCompilation).Assembly.GetManifestResourceStream("TestSupport.ConcreteReads.cs")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
