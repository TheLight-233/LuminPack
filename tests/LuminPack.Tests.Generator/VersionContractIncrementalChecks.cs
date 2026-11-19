extern alias ModelBasedAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks that retained Next drivers republish versioned schemas and reference envelopes after declaration edits.</summary>
internal static class VersionContractIncrementalChecks
{
    /// <summary>Applies schema and reference option edits to the same driver and compares with fresh generation.</summary>
    /// <param name="references">The platform and portable runtime metadata available to the fixture.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string model = """
            using LuminPack.Attribute;

            [LuminPackable(GeneratorType.VersionTolerant)]
            public sealed class VersionedModel
            {
                [LuminPackOldName("FirstName")] public int Name;
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var original = CSharpSyntaxTree.ParseText(model, parse, "VersionedModel.cs");
        var renamed = CSharpSyntaxTree.ParseText(
            model.Replace("FirstName", "SecondName", StringComparison.Ordinal), parse, original.FilePath);
        var tracked = CSharpSyntaxTree.ParseText(
            renamed.ToString().Replace("public sealed class", "[LuminPackOption(TrackReferences = true)]\npublic sealed class", StringComparison.Ordinal),
            parse, original.FilePath);
        var neighbor = CSharpSyntaxTree.ParseText(
            "[LuminPack.Attribute.LuminPackable] public sealed class UnchangedModel { public long Value; }",
            parse, "UnchangedModel.cs");
        var first = CSharpCompilation.Create("VersionContractIncremental", new[] { original, neighbor }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var second = first.ReplaceSyntaxTree(original, renamed);
        var third = second.ReplaceSyntaxTree(renamed, tracked);

        GeneratorDriver driver = NewDriver().RunGenerators(first);
        var initial = Sources(driver);
        var edited = CheckEdit(second, initial, stableNeighbor: true);
        CheckEdit(third, edited, stableNeighbor: false);

        /// <summary>Checks republishing for a changed contract and stable output for the unchanged declaration.</summary>
        /// <param name="input">The compilation with the current declaration edit.</param>
        /// <param name="previous">The generated sources from before this edit.</param>
        /// <param name="stableNeighbor">Whether assembly-wide generation settings stay unchanged.</param>
        /// <returns>The retained driver's current generated output.</returns>
        Dictionary<string, string> CheckEdit(CSharpCompilation input, Dictionary<string, string> previous, bool stableNeighbor)
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var error = output.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            if (error is not null)
                throw new InvalidOperationException("Next emitted an invalid version contract: " + error);

            var actual = Sources(driver);
            var fresh = Sources(NewDriver().RunGenerators(input));
            if (actual.Count != fresh.Count || actual.Any(file => fresh.GetValueOrDefault(file.Key) != file.Value))
                throw new InvalidOperationException("Retained and fresh drivers produced different version contracts.");

            var changed = actual.Where(file => file.Key.EndsWith("LuminPackGen.VersionedModel.g.cs", StringComparison.Ordinal)).ToArray();
            var stable = actual.Where(file => file.Key.EndsWith("LuminPackGen.UnchangedModel.g.cs", StringComparison.Ordinal)).ToArray();
            if (changed.Length != 2 || changed.Any(file => previous.GetValueOrDefault(file.Key) == file.Value) ||
                stable.Length != 2 || stableNeighbor && stable.Any(file => previous.GetValueOrDefault(file.Key) != file.Value))
                throw new InvalidOperationException("A descriptor or reference envelope edit republished the wrong model sources. " +
                    string.Join(", ", actual.Keys.Where(key => key.Contains("Model", StringComparison.Ordinal)).OrderBy(key => key)
                        .Select(key => key + "=" + (previous.GetValueOrDefault(key) == actual[key] ? "same" : "changed"))));
            return actual;
        }

        /// <summary>Creates an independent Roslyn generator driver for the fixture language version.</summary>
        /// <returns>A fresh Next generator driver.</returns>
        CSharpGeneratorDriver NewDriver() =>
            CSharpGeneratorDriver.Create(new[] { new ModelBasedGenerator().AsSourceGenerator() }, parseOptions: parse);
    }

    /// <summary>Collects generated file content and ensures generation completed without an error.</summary>
    /// <param name="driver">The driver that has run on the fixture.</param>
    /// <returns>The generated sources indexed by hint name.</returns>
    private static Dictionary<string, string> Sources(GeneratorDriver driver)
    {
        var run = driver.GetRunResult();
        foreach (var result in run.Results)
            if (result.Exception is not null)
                throw new InvalidOperationException("Next failed while analyzing a version contract.", result.Exception);
        var error = run.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        if (error is not null)
            throw new InvalidOperationException("Next reported an invalid version contract: " + error);
        return run.Results.SelectMany(result => result.GeneratedSources)
            .ToDictionary(file => file.HintName, file => file.SourceText.ToString(), StringComparer.Ordinal);
    }
}
