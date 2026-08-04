using System.Collections.Concurrent;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

const int CompilationCount = 32;
const int ValidTypesPerCompilation = 16;

var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator)
    .Select(static path => MetadataReference.CreateFromFile(path))
    .ToArray();

var failures = new ConcurrentQueue<string>();
Parallel.For(0, CompilationCount, compilationIndex =>
{
    var source = BuildSource(compilationIndex);
    var parseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.CSharp10)
        .WithPreprocessorSymbols(compilationIndex % 2 == 0
            ? new[] { "NET8_0_OR_GREATER" }
            : Array.Empty<string>());
    var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
    var compilation = CSharpCompilation.Create(
        $"GeneratorConcurrency_{compilationIndex}",
        new[] { tree },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    driver = driver.RunGenerators(compilation);
    var runResult = driver.GetRunResult();
    var result = runResult.Results.Single();

    if (result.Exception is not null)
    {
        failures.Enqueue($"Compilation {compilationIndex} generator exception: {result.Exception}");
        return;
    }

    var ownInvalidName = $"Invalid_{compilationIndex}";
    var errorDiagnostics = result.Diagnostics
        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .ToArray();
    if (errorDiagnostics.Length != 1 ||
        !errorDiagnostics[0].GetMessage().Contains(ownInvalidName, StringComparison.Ordinal))
    {
        failures.Enqueue(
            $"Compilation {compilationIndex} diagnostics leaked or were lost: " +
            string.Join(" | ", errorDiagnostics.Select(static diagnostic => diagnostic.ToString())));
    }

    var generatedHints = result.GeneratedSources
        .Select(static generated => generated.HintName)
        .ToHashSet(StringComparer.Ordinal);
    for (var typeIndex = 0; typeIndex < ValidTypesPerCompilation; typeIndex++)
    {
        var expectedHint = $"Case_{compilationIndex}.Model_{typeIndex}Parser.g.cs";
        if (!generatedHints.Contains(expectedHint))
        {
            failures.Enqueue($"Compilation {compilationIndex} is missing {expectedHint}.");
        }
    }

    if (generatedHints.Any(hint =>
            hint.Contains("Case_", StringComparison.Ordinal) &&
            !hint.Contains($"Case_{compilationIndex}.", StringComparison.Ordinal)))
    {
        failures.Enqueue($"Compilation {compilationIndex} received generated source from another compilation.");
    }
});

if (!failures.IsEmpty)
{
    throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
}

VerifyUnionPartialDiagnostic(references);

Console.WriteLine(
    $"Passed {CompilationCount} parallel compilations and " +
    $"{CompilationCount * ValidTypesPerCompilation} valid generated types.");

static void VerifyUnionPartialDiagnostic(MetadataReference[] references)
{
    const string source = """
        using System;

        namespace LuminPack.Attribute
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : Attribute
            {
            }
        }

        [LuminPack.Attribute.LuminPackable]
        public interface INonPartialUnion
        {
        }

        [LuminPack.Attribute.LuminPackable]
        public sealed class NonPartialUnionMember : INonPartialUnion
        {
        }
        """;

    var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
    var compilation = CSharpCompilation.Create(
        "UnionPartialDiagnostic",
        new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    var diagnostics = driver.RunGenerators(compilation)
        .GetRunResult()
        .Results.Single()
        .Diagnostics
        .Where(static diagnostic => diagnostic.Id == "LuminPack041")
        .ToArray();

    if (diagnostics.Length != 2 ||
        !diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("INonPartialUnion", StringComparison.Ordinal)) ||
        !diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("NonPartialUnionMember", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException(
            "Expected LuminPack041 for both a non-partial Union root and its non-partial local member.");
    }
}

static string BuildSource(int compilationIndex)
{
    var types = string.Join(
        Environment.NewLine,
        Enumerable.Range(0, ValidTypesPerCompilation).Select(typeIndex => $$"""
            [LuminPack.Attribute.LuminPackable]
            public partial class Model_{{typeIndex}}
            {
                public int Value_{{typeIndex}};
            }
            """));

    return $$"""
        using System;

        namespace LuminPack.Attribute
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : Attribute
            {
            }
        }

        namespace Case_{{compilationIndex}}
        {
            {{types}}

            [LuminPack.Attribute.LuminPackable]
            public static partial class Invalid_{{compilationIndex}}
            {
            }
        }
        """;
}
