using System.Collections.Immutable;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Models compiler hosts whose reference resolver deliberately omits value equality.</summary>
internal static class CompilerResolverRegression
{
    /// <summary>Requires retained generation to rebuild safely when option equality is unavailable.</summary>
    internal static void Run()
    {
        const string source = "using LuminPack.Attribute; [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)] [LuminPackable] public class ResolverPayload { public int Value; } public static class Entry { public static int Measure(ResolverPayload value) => LuminPack.LuminPackSerializer.Sizeof(value); }";
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp10), "ResolverPayload.cs");
        var resolved = CompatibilityCompilationReferences.Create();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ISourceGenerator[]
        {
            new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()
        }, parseOptions: (CSharpParseOptions)tree.Options);
        string? baseline = null;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            var compilation = CSharpCompilation.Create("ResolverCompatibility", new[] { tree }, resolved,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
                    .WithMetadataReferenceResolver(new IncomparableResolver()));
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            var failures = output.GetDiagnostics().Concat(diagnostics)
                .Where(item => item.Severity == DiagnosticSeverity.Error || item.Id is "CS8785" or "CS8784").ToArray();
            if (failures.Length != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures.Select(item => item.ToString())));
            string generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(item => item.ToString()));
            if (baseline != null && generated != baseline) throw new InvalidOperationException("Unavailable resolver equality changed generated output.");
            baseline = generated;
        }
        if (IncomparableResolver.Comparisons == 0) throw new InvalidOperationException("The unavailable resolver equality was not exercised.");
        Console.WriteLine("Passed compiler-host resolver equality fallback without generator failures.");
    }

    /// <summary>Matches the command-line compiler resolver that throws during option equality.</summary>
    private sealed class IncomparableResolver : MetadataReferenceResolver
    {
        internal static int Comparisons;

        /// <summary>Provides no script reference resolution for this ordinary compilation.</summary>
        /// <param name="reference">The requested reference identity.</param>
        /// <param name="baseFilePath">The requesting file.</param>
        /// <param name="properties">The reference metadata properties.</param>
        /// <returns>No additional script references.</returns>
        public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string baseFilePath, MetadataReferenceProperties properties)
            => ImmutableArray<PortableExecutableReference>.Empty;

        /// <summary>Reproduces the compiler host's unavailable comparison.</summary>
        /// <param name="other">The other host resolver.</param>
        /// <returns>No result because equality is unavailable.</returns>
        public override bool Equals(object other)
        {
            Comparisons++;
            throw new NotImplementedException();
        }

        /// <summary>Uses one stable bucket without invoking unsupported equality.</summary>
        /// <returns>The fixed hash code.</returns>
        public override int GetHashCode() => 0;
    }
}
