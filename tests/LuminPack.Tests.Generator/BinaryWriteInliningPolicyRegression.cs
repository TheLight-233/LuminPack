using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Provides binary write inlining policy regression functionality.</summary>
internal static class BinaryWriteInliningPolicyRegression
{
    /// <summary>Verifies run.</summary>
    /// <param name="platformReferences">The platform references used by this operation.</param>
    public static void Run(MetadataReference[] platformReferences)
    {
        const string source = """
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;

            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Full)]

            [LuminPackable]
            public partial class BinaryWriteInliningProbe
            {
                public int[] UnmanagedArray = default!;
                public string[] ManagedArray = default!;
                public string[,] ManagedMultiDimensionalArray = default!;
                public List<int> UnmanagedList = default!;
                public List<string> ManagedList = default!;
                public Dictionary<int, int> UnmanagedDictionary = default!;
                public Dictionary<string, long> ManagedDictionary = default!;
            }
            public static class Entry
            {
                public static byte[] Write(BinaryWriteInliningProbe value) => LuminPackSerializer.Serialize(value);
            }
            """;

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("NETSTANDARD2_1");
        var references = CompatibilityCompilationReferences.Create();
        var compilation = CSharpCompilation.Create(
            "BinaryWriteInliningPolicy",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        GeneratorDriverRunResult result = driver.RunGenerators(compilation).GetRunResult();
        if (Environment.GetEnvironmentVariable("LUMINPACK_MECHANISM_CAPTURE") is { Length: > 0 } capture)
        {
            Directory.CreateDirectory(capture);
            foreach (var file in result.Results.SelectMany(generatorResult => generatorResult.GeneratedSources))
                File.WriteAllText(Path.Combine(capture, file.HintName), file.SourceText.ToString());
        }
        if (result.Results.Any(generatorResult => generatorResult.Exception is not null))
        {
            throw new InvalidOperationException("Binary write inlining policy generation failed.", result.Results.First(generatorResult => generatorResult.Exception is not null).Exception);
        }
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString())));

        string generated = string.Join(
            "\n",
            result.Results.SelectMany(generatorResult => generatorResult.GeneratedSources).Select(static item => item.SourceText.ToString()))
            .Replace("\r\n", "\n");

        AssertWriteAttribute(generated, "int[]", "AggressiveInlining");
        AssertWriteAttribute(generated, "string[]", "NoInlining");
        AssertWriteAttribute(generated, "string[,]", "NoInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.List<int>", "AggressiveInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.List<string>", "NoInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.Dictionary<int, int>", "NoInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.Dictionary<string, long>", "NoInlining");
    }

    /// <summary>Verifies assert write attribute.</summary>
    /// <param name="generated">The generated used by this operation.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="expectedAttribute">The expected attribute used by this operation.</param>
    private static void AssertWriteAttribute(string generated, string typeName, string expectedAttribute)
    {
        var methods = CSharpSyntaxTree.ParseText(generated).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == "RootWrite" && method.ParameterList.Parameters.Count == 2 &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?').Replace(" ", string.Empty) == typeName.Replace(" ", string.Empty)).ToArray();
        if (methods.Length == 0)
            throw new InvalidOperationException("Missing binary RootWrite overload for " + typeName + ". Available: " +
                string.Join(", ", CSharpSyntaxTree.ParseText(generated).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Where(method => method.Identifier.ValueText == "RootWrite" && method.ParameterList.Parameters.Count == 2)
                    .Select(method => method.ParameterList.Parameters[1].Type!.ToString())));
        if (methods.Any(method => !method.AttributeLists.ToString().Contains("MethodImplOptions." + expectedAttribute, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                typeName + " did not use the expected " + expectedAttribute + " write inlining policy." +
                Environment.NewLine + string.Join(Environment.NewLine, methods.Select(method => method.AttributeLists.ToString())));
        }
    }
}
