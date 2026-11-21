extern alias ModelBasedAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks that unsupported declaration edges are reported before publishing invalid formatter code.</summary>
internal static class SerializerDeclarationBoundaryRegression
{
    /// <summary>Compiles invalid and supported signatures together with the generated formatters.</summary>
    /// <param name="references">The platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using LuminPack.Attribute;

            [LuminPackable] file class FilePayload { public int Count; }
            file class FileContainer
            {
                [LuminPackable] public class FileNestedPayload { public int Count; }
            }
            public class Container
            {
                private class Hidden
                {
                    [LuminPackable] public class HiddenPayload { public int Count; }
                }
            }
            [LuminPackable] public class GenericCallback
            {
                [LuminPackOnSerialized] public void Notify<T>() { }
            }
            [LuminPackable] public class GenericPool
            {
                [LuminPackPoolRent] public static GenericPool Rent<T>() => new();
            }
            [LuminPackable] public class PrivatePool
            {
                [LuminPackPoolRent] private static PrivatePool Rent() => new();
            }
            [LuminPackable] public class ReferenceConstructor
            {
                public int Count;
                public ReferenceConstructor(ref int count) => Count = count;
            }
            [LuminPackable] public class IncompatibleConstructor
            {
                public int Count;
                public IncompatibleConstructor(string count) => Count = count.Length;
            }
            internal class AccessibleContainer
            {
                [LuminPackable] public class SupportedConstructor
                {
                    public int Count;
                    public SupportedConstructor(in long count) => Count = (int)count;
                    [LuminPackOnDeserialized] internal void Notify() { }
                }
            }
            [LuminPackable] public class SupportedPool
            {
                public int Count;
                [LuminPackPoolRent] internal static SupportedPool Rent() => new();
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols:
            ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER"]);
        var tree = CSharpSyntaxTree.ParseText(source, parse, "DeclarationBoundaries.cs");
        var input = TestCompilation.Create("DeclarationBoundaries", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Diagnostic[] errors = output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(item => item.ToString())));

        var expected = new (string Id, string Symbol)[]
        {
            ("LuminPack004", "Notify"),
            ("LuminPack031", "Rent"),
            ("LuminPack120", "FileNestedPayload"),
            ("LuminPack120", "FilePayload"),
            ("LuminPack120", "HiddenPayload"),
            ("LuminPack121", "Rent"),
            ("LuminPack122", "count"),
            ("LuminPack122", "count"),
        };
        var actual = diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)
            .Select(item => (item.Id, item.Location.SourceTree == tree
                ? source.Substring(item.Location.SourceSpan.Start, item.Location.SourceSpan.Length)
                : string.Empty)).OrderBy(item => item.Id, StringComparer.Ordinal)
            .ThenBy(item => item.Item2, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected))
            throw new InvalidOperationException("Unexpected declaration diagnostics: " + string.Join(", ", actual.AsEnumerable()));

        var hints = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(item => item.HintName).ToArray();
        foreach (string supported in new[] { "SupportedConstructor", "SupportedPool" })
            if (!hints.Any(hint => hint.Contains(supported, StringComparison.Ordinal)))
                throw new InvalidOperationException("Missing formatter for " + supported + ".");
        Console.WriteLine("Passed declaration diagnostics and supported constructor, callback and pool signatures.");
    }
}
