using Microsoft.CodeAnalysis;

try
{
    var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();
    SerializerDocumentationRegression.Run(references);
    Console.WriteLine("Passed generated overload XML documentation and serialization contract checks.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
