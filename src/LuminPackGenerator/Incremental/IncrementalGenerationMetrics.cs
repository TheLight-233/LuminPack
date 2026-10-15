namespace LuminPack.SourceGenerator;

/// <summary>Tracks internal generation and cache activity for performance verification.</summary>
internal static class IncrementalGenerationMetrics
{
    internal static long PackableModelBuilds;
    internal static long PackableAnalysisTransforms;
    internal static long PackableDiagnosticCallbacks;
    internal static long PackableCacheHits;
    internal static long PackableOutputCallbacks;
    internal static long MetadataAnalyses;
    internal static long SerializerFragmentBuilds;
    internal static long SerializerFragmentCacheHits;
    internal static long AggregateBuilds;
    internal static long AggregateCacheHits;
    internal static long AggregateFileOutputCallbacks;
    internal static long SemanticFileObservations;
    internal static long SemanticFileCacheHits;
    internal static long SemanticFileFallbacks;
    internal static long InvocationSupportBuilds;
    internal static long InvocationSupportCacheHits;
    internal static long OwnershipGraphBuilds;
    internal static long OwnershipGraphCacheHits;
    internal static long OwnershipGraphFallbacks;
    internal static long ProjectMemberInspections;
    internal static long AutomaticMapperBuilds;
    internal static long AutomaticMapperCacheHits;
    internal static long ManualMapperBuilds;
    internal static long ManualMapperCacheHits;
    internal static long MapperFileOutputCallbacks;
    internal static long CustomRegistrationBuilds;
    internal static long CustomRegistrationCacheHits;
    internal static long MapperRegistrySourceBuilds;
    internal static long MapperRegistrySourceCacheHits;
    internal static long CustomRegistrySourceBuilds;
    internal static long CustomRegistrySourceCacheHits;
    internal static long ReachabilityAnalyses;
    internal static long ReachabilityInvocationBindings;
    internal static long ApiSignatureBuilds;
    internal static long ApiRootInvocationBindings;
    internal static long ApiFileCacheHits;
    internal static long PartitionedFileBuilds;
    internal static long LayoutFileOutputCallbacks;
    internal static long InvocationFormatterBuilds;
    internal static long InvocationFileObservations;
    internal static long InvocationFileCacheHits;
    internal static long InvocationFileFallbacks;
    internal static long BinaryOperationPlanBuilds;
    internal static long BinaryOperationExpansions;
}
