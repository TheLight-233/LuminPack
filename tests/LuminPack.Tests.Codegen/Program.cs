using Microsoft.CodeAnalysis;

try
{
    // These contracts read statements and comments as single lines; final line layout is whitespace only.
    typeof(LuminPack.SourceGenerator.LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.GeneratedSourceLayout")!
        .GetField("Enabled", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, false);
    var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();
    if (args is ["--verify-closed-union-cases"])
    {
        SerializerIssueRegression.RunUnion(references);
        return 0;
    }
    if (args is ["--verify-rectangular-nullable-wire"])
    {
        SerializerIssueRegression.RunRectangularNullableWire(references);
        return 0;
    }
    if (args is ["--verify-union-byte-payload"])
    {
        SerializerIssueRegression.RunUnionBytePayload(references);
        return 0;
    }
    if (args is ["--verify-framework-scalar-collections"])
    {
        SerializerIssueRegression.RunFrameworkCollections(references);
        return 0;
    }
    if (args is ["--verify-fixed-record-reservations"])
    {
        SerializerIssueRegression.RunFixedRecordReservations(references);
        return 0;
    }
    if (args is ["--verify-frozen-collections"])
    {
        SerializerImmutableListWriteRegression.RunFrozen(references);
        return 0;
    }
    if (args is ["--verify-concurrent-collections"])
    {
        SerializerImmutableListWriteRegression.RunConcurrent(references);
        return 0;
    }
    if (args is ["--verify-immutable-builders"])
    {
        SerializerImmutableQueueConstructionRegression.RunBuilders(references);
        return 0;
    }
    if (args is ["--verify-bit-storage"])
    {
        SerializerFrameworkEntryRegression.RunBitStorage(references);
        return 0;
    }
    if (args is ["--verify-json-scalars"])
    {
        SerializerJsonScalarLoopRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-json-dictionary-reads"])
    {
        SerializerJsonEnumerableWriteRegression.RunDictionaryReads(references);
        return 0;
    }
    if (args is ["--verify-json-dictionary-boundaries"])
    {
        SerializerJsonEnumerableWriteRegression.RunDictionaryBoundaries(references);
        return 0;
    }
    if (args is ["--verify-json-dictionary-entries"])
    {
        SerializerJsonEnumerableWriteRegression.RunDictionaryEntries(references);
        return 0;
    }
    if (args is ["--verify-json-enumerable-writes"])
    {
        SerializerJsonEnumerableWriteRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-json-sequence-capacity"])
    {
        SerializerJsonSequenceCapacityRegression.Run(references);
        return 0;
    }
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
    if (args is ["--verify-interceptors"])
    {
        SerializerInterceptorRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-binary-entry-shapes"])
    {
        SerializerBinaryEntryShapeRegression.Run(references);
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
    if (args is ["--verify-type-name-pattern"])
    {
        SerializerFrameworkEntryRegression.VerifyTypeNamePattern();
        return 0;
    }
    if (args is ["--verify-text-wrappers"])
    {
        SerializerFrameworkEntryRegression.RunTextWrappers(references);
        return 0;
    }
    if (args is ["--verify-memory-loops"])
    {
        SerializerFrameworkEntryRegression.RunMemoryLoops(references);
        return 0;
    }
    if (args is ["--verify-framework-entries"])
    {
        SerializerFrameworkEntryRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-builder-paths"])
    {
        SerializerBuilderPathRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-biginteger-paths"])
    {
        SerializerFrameworkEntryRegression.VerifyBigIntegerPaths(references);
        return 0;
    }
    if (args is ["--verify-version-paths"])
    {
        SerializerFrameworkEntryRegression.VerifyVersionPaths(references);
        return 0;
    }
    if (args is ["--verify-collection-layout-probes"])
    {
        SerializerImmutableListWriteRegression.RunLayoutProbes(references);
        return 0;
    }
    if (args is ["--verify-sorted-list-json-writes"])
    {
        SerializerImmutableListWriteRegression.RunSortedJson(references);
        return 0;
    }
    if (args is ["--verify-collection-leases"])
    {
        SerializerImmutableListWriteRegression.RunLeases(references);
        return 0;
    }
    if (args is ["--verify-set-families"])
    {
        SerializerImmutableListWriteRegression.RunSetFamilies(references);
        return 0;
    }
    if (args is ["--verify-remaining-collections"])
    {
        SerializerImmutableListWriteRegression.RunRemainingCollections(references);
        return 0;
    }
    if (args is ["--verify-empty-collections"])
    {
        SerializerImmutableListWriteRegression.RunEmptyCollections(references);
        return 0;
    }
    if (args is ["--verify-priority-json"])
    {
        SerializerImmutableListWriteRegression.RunPriorityJson(references);
        return 0;
    }
    if (args is ["--verify-linq-dependencies"])
    {
        SerializerImmutableListWriteRegression.RunLinqDependencies(references);
        return 0;
    }
    if (args is ["--verify-sequence-families"])
    {
        SerializerImmutableListWriteRegression.RunSequenceFamilies(references);
        return 0;
    }
    if (args is ["--verify-immutable-interface-writes"])
    {
        SerializerImmutableListWriteRegression.RunInterfaces(references);
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
    if (args is ["--verify-immutable-list-construction"])
    {
        SerializerImmutableQueueConstructionRegression.RunLists(references);
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
    if (args is ["--verify-state-roots"])
    {
        SerializerReviewRegression.VerifyStateRoots(references);
        return 0;
    }
    if (args is ["--verify-operation-selection"])
    {
        SerializerPartitionRegression.VerifyOperationSelection(references);
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
    if (args is ["--verify-wide-reads"])
    {
        SerializerWideReadRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-inlining"])
    {
        SerializerBinaryInliningRegression.Run(references);
        SerializerCollectionInliningRegression.Run(references);
        SerializerUnionInliningRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-static-kernels"])
    {
        SerializerBinaryInliningRegression.VerifyNamedKernels(references);
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
    if (args is ["--verify-proven-object-writes"])
    {
        SerializerCodegenPerformanceRegression.VerifyProvenObjectWrites(references);
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
    SerializerIssueRegression.RunUnion(references);
    SerializerIssueRegression.RunRectangularNullableWire(references);
    SerializerIssueRegression.RunUnionBytePayload(references);
    SerializerIssueRegression.RunFrameworkCollections(references);
    SerializerIssueRegression.RunFixedRecordReservations(references);
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
    SerializerWideReadRegression.Run(references);
    SerializerBinaryInliningRegression.Run(references);
    SerializerCollectionInliningRegression.Run(references);
    SerializerUnionInliningRegression.Run(references);
    SerializerExternalUnionRegression.Run(references);
    SerializerMechanismRegression.Run(references);
    SerializerCodegenPerformanceRegression.Run(references);
    SerializerTimeZoneCacheRegression.Run(references);
    SerializerFrameworkEntryRegression.RunTextWrappers(references);
    SerializerFrameworkEntryRegression.RunBitStorage(references);
    SerializerFrameworkEntryRegression.RunMemoryLoops(references);
    SerializerBuilderPathRegression.Run(references);
    SerializerImmutableQueueConstructionRegression.Run(references);
    SerializerImmutableQueueConstructionRegression.RunLists(references);
    SerializerImmutableListWriteRegression.RunFrozen(references);
    SerializerImmutableListWriteRegression.RunConcurrent(references);
    SerializerImmutableQueueConstructionRegression.RunBuilders(references);
    SerializerImmutableListWriteRegression.RunInterfaces(references);
    SerializerImmutableListWriteRegression.RunLeases(references);
    SerializerImmutableListWriteRegression.RunSortedJson(references);
    SerializerImmutableListWriteRegression.RunLayoutProbes(references);
    SerializerImmutableListWriteRegression.RunSetFamilies(references);
    SerializerImmutableListWriteRegression.RunSequenceFamilies(references);
    SerializerImmutableListWriteRegression.RunRemainingCollections(references);
    SerializerImmutableListWriteRegression.RunPriorityJson(references);
    SerializerImmutableListWriteRegression.RunLinqDependencies(references);
    SerializerScalarInterfaceReadRegression.Run(references);
    SerializerScalarInterfaceWriteRegression.Run(references);
    SerializerSmallHashSetWriteRegression.Run(references);
    SerializerSortedSetConstructionRegression.Run(references);
    SerializerReferencePropertyMetadataRegression.Run(references);
    SerializerReferenceCollectionReuseRegression.Run(references);
    SerializerSourcePresentationRegression.Run(references);
    SerializerUnionCursorRegression.Run(references);
    SerializerCapacityTierRegression.Run(references);
    SerializerJsonSequenceCapacityRegression.Run(references);
    SerializerJsonReuseRegression.Run(references);
    SerializerJsonScalarLoopRegression.Run(references);
    SerializerJsonEnumerableWriteRegression.Run(references);
    SerializerJsonEnumerableWriteRegression.RunDictionaryEntries(references);
    SerializerJsonEnumerableWriteRegression.RunDictionaryBoundaries(references);
    SerializerJsonEnumerableWriteRegression.RunDictionaryReads(references);
    SerializerCompiledOptionRegression.Run(references);
    SerializerInterceptorRegression.Run(references);
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
