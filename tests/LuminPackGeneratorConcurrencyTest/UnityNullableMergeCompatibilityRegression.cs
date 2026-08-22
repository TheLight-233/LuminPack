using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class UnityNullableMergeCompatibilityRegression
{
    public static void Run(MetadataReference[] platformReferences)
    {
        const string source = """
            using System;
            using LuminPack.Attribute;

            [LuminPackable]
            public sealed class UnityNullableMergeProbe
            {
                public DateTime Timestamp;
                public int? Optional;
                public long Suffix;
            }
            """;

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.CSharp10);
        MetadataReference[] references = platformReferences
            .Append(MetadataReference.CreateFromFile(
                typeof(global::LuminPack.Core.LuminPackWriter).Assembly.Location))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "UnityNullableMergeCompatibility",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out Compilation output, out var generatorDiagnostics);

        Diagnostic[] errors = generatorDiagnostics
            .Concat(output.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException(
                "Unity/C# 10 nullable merge compatibility failed: " +
                string.Join(" | ", errors.Select(static error => error.ToString())));
        }

        string generated = string.Join(
            "\n",
            driver.GetRunResult().Results
                .SelectMany(static result => result.GeneratedSources)
                .Select(static sourceResult => sourceResult.SourceText.ToString()));
        bool nullableWasMerged = generated
            .Split('\n')
            .Any(static line =>
                line.Contains("Optional", StringComparison.Ordinal) &&
                (line.Contains("WriteUnmanaged(", StringComparison.Ordinal) ||
                 line.Contains("ReadUnmanaged(", StringComparison.Ordinal)));
        if (nullableWasMerged)
        {
            throw new InvalidOperationException(
                "Nullable<T> was merged into an unmanaged-constrained multi-value call.");
        }
    }
}
