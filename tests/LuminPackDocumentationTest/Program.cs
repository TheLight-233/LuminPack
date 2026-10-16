using Microsoft.CodeAnalysis;

try
{
    var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();
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
    if (args is ["--verify-mechanisms", var baseline])
    {
        SerializerMechanismRegression.Run(references, baseline);
        return 0;
    }
    if (args is ["--verify-special-modes"])
    {
        SerializerSpecialModeBoundaryRegression.Run(references);
        return 0;
    }
    if (args is ["--verify-custom"])
    {
        SerializerCustomFormatterRegression.Run(references);
        return 0;
    }
    SerializerDocumentationRegression.Run(references);
    SerializerRootApiRegression.Run(references);
    SerializerAssemblyPackableRegression.Run(references);
    SerializerGenericOverloadRegression.Run(references);
    SerializerReviewRegression.Run(references);
    SerializerCustomFormatterRegression.Run(references);
    SerializerPartitionRegression.Run(references);
    SerializerBinaryInliningRegression.Run(references);
    SerializerCollectionInliningRegression.Run(references);
    SerializerUnionInliningRegression.Run(references);
    SerializerExternalUnionRegression.Run(references);
    SerializerMechanismRegression.Run(references);
    SerializerSpecialModeBoundaryRegression.Run(references);
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
