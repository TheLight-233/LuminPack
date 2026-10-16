using Microsoft.CodeAnalysis;

try
{
    var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();
    SerializerDocumentationRegression.Run(references);
    SerializerRootApiRegression.Run(references);
    SerializerGenericOverloadRegression.Run(references);
    SerializerReviewRegression.Run(references);
    SerializerCustomFormatterRegression.Run(references);
    SerializerPartitionRegression.Run(references);
    SerializerBinaryInliningRegression.Run(references);
    SerializerCollectionInliningRegression.Run(references);
    SerializerUnionInliningRegression.Run(references);
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
