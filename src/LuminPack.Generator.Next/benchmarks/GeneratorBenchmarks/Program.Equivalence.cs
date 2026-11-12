using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using LuminPack.SourceGenerator.Next.Diagnostics;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Compares every generated filename and character against a frozen generator build.</summary>
    /// <param name="next">The candidate generator factory.</param>
    /// <param name="reference">The reference generator factory.</param>
    /// <param name="corpus">The common source corpus.</param>
    /// <param name="references">The common metadata references.</param>
    /// <param name="parse">The common parse options.</param>
    /// <param name="options">The common compilation options.</param>
    /// <returns>Zero when full and retained-driver publications are identical.</returns>
    private static int VerifyEquivalence(Func<ISourceGenerator[]> next, Func<ISourceGenerator[]> reference,
        IReadOnlyList<CorpusFile> corpus, MetadataReference[] references, CSharpParseOptions parse, CSharpCompilationOptions options)
    {
        SyntaxTree[] trees = CreateTrees(corpus, parse);
        var compilation = CSharpCompilation.Create("GeneratorBenchmarks", trees, references, options);
        GeneratorDriver candidate = CSharpGeneratorDriver.Create(next(), parseOptions: parse);
        GeneratorDriver baseline = CSharpGeneratorDriver.Create(reference(), parseOptions: parse);
        int payload = PayloadFile(corpus);
        var edits = new List<(string Name, Compilation Input)> { ("full", compilation) };
        edits.Add(("comment", compilation.ReplaceSyntaxTree(trees[0],
            CSharpSyntaxTree.ParseText(corpus[0].Text + "\n// Comparison edit.\n", parse, corpus[0].Path))));
        edits.Add(("member", compilation.ReplaceSyntaxTree(trees[payload],
            CSharpSyntaxTree.ParseText(AddMember(corpus[payload].Text, parse), parse, corpus[payload].Path))));
        edits.Add(("removed declaration", compilation.RemoveSyntaxTrees(trees[payload])));
        edits.Add(("restored declaration", compilation));
        foreach (var edit in edits)
        {
            candidate = candidate.RunGeneratorsAndUpdateCompilation(edit.Input, out _, out var candidateDiagnostics);
            baseline = baseline.RunGeneratorsAndUpdateCompilation(edit.Input, out _, out var baselineDiagnostics);
            foreach (var result in candidate.GetRunResult().Results)
                if (result.Exception != null)
                    return LuminPackExceptionHelper.ThrowInvalidOperation($"{edit.Name}: candidate failed: {result.Exception}");
            foreach (var result in baseline.GetRunResult().Results)
                if (result.Exception != null)
                    return LuminPackExceptionHelper.ThrowInvalidOperation($"{edit.Name}: reference failed: {result.Exception}");
            var actual = Sources(candidate);
            var expected = Sources(baseline);
            if (actual.Count != expected.Count)
                return LuminPackExceptionHelper.ThrowInvalidOperation($"{edit.Name}: file count differs: {actual.Count}/{expected.Count}");
            foreach (var file in expected)
                if (!actual.TryGetValue(file.Key, out string? text) || text != file.Value)
                    return SourceMismatch(edit.Name, file.Key, text, file.Value);
            var actualErrors = Errors(candidateDiagnostics);
            var expectedErrors = Errors(baselineDiagnostics);
            if (!actualErrors.SetEquals(expectedErrors))
                return LuminPackExceptionHelper.ThrowInvalidOperation($"{edit.Name}: generator diagnostics differ");
            Console.WriteLine($"Equivalent {edit.Name}: {actual.Count} generated files.");
        }
        return 0;
    }

    /// <summary>Preserves both sources when a comparison fails so the difference is reviewable.</summary>
    /// <param name="scenario">The full or incremental scenario.</param>
    /// <param name="hint">The differing filename.</param>
    /// <param name="actual">The candidate source, or null for a missing file.</param>
    /// <param name="expected">The reference source.</param>
    /// <returns>Never returns a value.</returns>
    private static int SourceMismatch(string scenario, string hint, string? actual, string expected)
    {
        string folder = Path.Combine(Path.GetTempPath(), "LuminPackSourceComparison-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "candidate.g.cs"), actual ?? string.Empty);
        File.WriteAllText(Path.Combine(folder, "reference.g.cs"), expected);
        return LuminPackExceptionHelper.ThrowInvalidOperation($"{scenario}: generated source differs: {hint}. Sources: {folder}");
    }

    /// <summary>Captures generated files independently of publication order.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>The exact source text indexed by hint name.</returns>
    private static Dictionary<string, string> Sources(GeneratorDriver driver)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var result in driver.GetRunResult().Results)
            foreach (var source in result.GeneratedSources)
                files.Add(source.HintName, source.SourceText.ToString());
        return files;
    }

    /// <summary>Captures error diagnostics without ordering or warning differences.</summary>
    /// <param name="diagnostics">The driver diagnostics.</param>
    /// <returns>The formatted error set.</returns>
    private static HashSet<string> Errors(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = new HashSet<string>(StringComparer.Ordinal);
        foreach (Diagnostic diagnostic in diagnostics)
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                errors.Add(diagnostic.ToString());
        return errors;
    }
}
