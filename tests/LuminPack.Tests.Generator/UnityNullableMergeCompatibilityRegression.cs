using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Provides unity nullable merge compatibility regression functionality.</summary>
internal static class UnityNullableMergeCompatibilityRegression
{
    /// <summary>Verifies run.</summary>
    /// <param name="platformReferences">The platform references used by this operation.</param>
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
        MetadataReference[] references = CompatibilityCompilationReferences.Create();
        var compilation = CSharpCompilation.Create(
            "UnityNullableMergeCompatibility",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out Compilation output, out var generatorDiagnostics);
        if (Environment.GetEnvironmentVariable("LUMINPACK_MECHANISM_CAPTURE") is { Length: > 0 } capture)
        {
            Directory.CreateDirectory(capture);
            foreach (var file in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources))
                File.WriteAllText(Path.Combine(capture, file.HintName), file.SourceText.ToString());
        }

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
