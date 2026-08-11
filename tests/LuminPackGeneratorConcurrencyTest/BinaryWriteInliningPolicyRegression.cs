using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class BinaryWriteInliningPolicyRegression
{
    public static void Run(MetadataReference[] platformReferences)
    {
        const string source = """
            using System.Collections.Generic;
            using LuminPack.Attribute;

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
            """;

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("NET8_0_OR_GREATER");
        var references = platformReferences
            .Append(MetadataReference.CreateFromFile(typeof(global::LuminPack.LuminPackSerializer).Assembly.Location))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "BinaryWriteInliningPolicy",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        GeneratorDriverRunResult result = driver.RunGenerators(compilation).GetRunResult();
        GeneratorRunResult generatorResult = result.Results.Single();
        if (generatorResult.Exception is not null)
        {
            throw new InvalidOperationException("Binary write inlining policy generation failed.", generatorResult.Exception);
        }

        string generated = string.Join(
            "\n",
            generatorResult.GeneratedSources.Select(static item => item.SourceText.ToString()))
            .Replace("\r\n", "\n");

        AssertWriteAttribute(generated, "int[]", "AggressiveInlining");
        AssertWriteAttribute(generated, "string[]", "NoInlining");
        AssertWriteAttribute(generated, "string[,]", "AggressiveInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.List<int>", "AggressiveInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.List<string>", "NoInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.Dictionary<int, int>", "NoInlining");
        AssertWriteAttribute(generated, "global::System.Collections.Generic.Dictionary<string, long>", "NoInlining");
    }

    private static void AssertWriteAttribute(string generated, string typeName, string expectedAttribute)
    {
        string signature =
            "public static void WriteValue(ref this LuminPackWriter writer, scoped in " + typeName + " value)";
        int signatureIndex = generated.IndexOf(signature, StringComparison.Ordinal);
        if (signatureIndex < 0)
        {
            throw new InvalidOperationException("Missing binary WriteValue overload for " + typeName + ".");
        }

        int windowStart = Math.Max(0, signatureIndex - 180);
        string attributeWindow = generated.Substring(windowStart, signatureIndex - windowStart);
        if (!attributeWindow.Contains("MethodImplOptions." + expectedAttribute, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                typeName + " did not use the expected " + expectedAttribute + " write inlining policy." +
                Environment.NewLine + attributeWindow + signature);
        }
    }
}
