using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects exact scalar sequence writes without broadening readers or other formatter contracts.</summary>
internal static class SerializerSequenceRawWriteRegression
{
    /// <summary>Compiles all tiers and checks retained scalar-to-portable write qualification edits.</summary>
    /// <param name="references">Platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using Element = System.Int32;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable,LuminPackOption(StructLayout=LuminPackStructLayout.Packed)]
            public struct Portable { public byte Tag;public long Value; }
            public static class SequenceEntry
            {
                public static byte[] Write(ReadOnlySequence<Element> value) => LuminPackSerializer.Serialize(value);
                public static byte[] Text(ReadOnlySequence<string> value) => LuminPackSerializer.Serialize(value);
                public static byte[] Nullable(ReadOnlySequence<int?> value) => LuminPackSerializer.Serialize(value);
                public static void Check()
                {
                    Element[] values=new Element[17];
                    var sequence=new ReadOnlySequence<Element>(values);
                    byte[] bytes=Write(sequence);
                    ReadOnlySequence<Element> read=default;
                    if(LuminPackSerializer.Deserialize(bytes,ref read)!=bytes.Length || read.Length!=17) throw new Exception("Sequence framing changed.");
                    if(!LuminPackSerializer.Serialize(sequence).AsSpan().SequenceEqual(bytes)) throw new Exception("Sequence bytes changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("SequenceWrite" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckSelection(driver, scalar: true);
            Execute(output);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(
                text.Replace("using Element = System.Int32;", "using Element = Portable;", StringComparison.Ordinal), parse, "Entry.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out output, out diagnostics);
            CheckSelection(driver, scalar: false);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(edited))), "A scalar-to-portable edit retained stale sequence writing.");
            Execute(output);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(compilation))), "Restoring a scalar sequence retained stale write eligibility.");
            CheckSelection(driver, scalar: true);
            Execute(output);
        }
        Console.WriteLine("Passed raw sequence write boundaries, exclusions, four tiers and retained/fresh portable edits.");
    }

    /// <summary>Checks segment bulk writes and the separate allocating-write qualification.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="scalar">Whether the selected element is a known raw scalar.</param>
    private static void CheckSelection(GeneratorDriver driver, bool scalar)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        int roots = 0;
        foreach (var method in methods.Where(method => method.Identifier.ValueText == "RootWrite" && method.ParameterList.Parameters.Count == 2))
        {
            string type = method.ParameterList.Parameters[1].Type!.ToString();
            if (!type.Contains("ReadOnlySequence<", StringComparison.Ordinal)) continue;
            bool expected = scalar && type.Contains("<int>", StringComparison.Ordinal);
            Require(method.ToString().Contains("value.IsSingleSegment", StringComparison.Ordinal) == expected, "Sequence bulk qualification differs: " + type);
            if (expected)
            {
                Require(method.ToString().Contains(".Overlaps(", StringComparison.Ordinal), "Sequence bulk copying lost its source/output non-overlap proof.");
                Require(method.ToString().Contains("NoInlining", StringComparison.Ordinal), "Sequence slow traversals lost their independent call boundaries.");
            }
            roots++;
        }
        Require(roots == 3, "The sequence fixture omitted a scalar or excluded root.");
        var writers = methods.Where(method => method.Identifier.ValueText == "Serialize" && method.ParameterList.Parameters.Count == 1 &&
            method.ParameterList.Parameters[0].Type!.ToString().Contains("ReadOnlySequence<", StringComparison.Ordinal)).ToArray();
        Require(writers.Any(method => method.ToString().Contains("directValue.Length", StringComparison.Ordinal)) == scalar,
            "The sequence allocating entry lost separate scalar qualification.");
        var readers = methods.Where(method => method.Identifier.ValueText == "Deserialize" && method.ParameterList.Parameters.Count >= 2 &&
            method.ParameterList.Parameters[0].Type!.ToString().Contains("ReadOnlySpan", StringComparison.Ordinal) &&
            method.ParameterList.Parameters[1].Type!.ToString().Contains("ReadOnlySequence<", StringComparison.Ordinal)).ToArray();
        Require(readers.Any(method => ResolveBody(methods, method).ToString().Contains("Lease", StringComparison.Ordinal)),
            "The write-only qualification removed read state.");
    }

    /// <summary>Follows a concrete entry through its optional shared facade.</summary>
    /// <param name="methods">All generated operation declarations.</param>
    /// <param name="entry">The concrete entry being checked.</param>
    /// <returns>The complete body owning operation state.</returns>
    private static BlockSyntax ResolveBody(MethodDeclarationSyntax[] methods, MethodDeclarationSyntax entry)
    {
        var body = entry.Body!;
        var shared = body.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault(call => call.Expression is SimpleNameSyntax name &&
            name.Identifier.ValueText.StartsWith("__LuminFacade", StringComparison.Ordinal));
        return shared?.Expression is SimpleNameSyntax sharedName
            ? methods.Single(method => method.Identifier.ValueText == sharedName.Identifier.ValueText).Body! : body;
    }

    /// <summary>Creates an independent generator for fresh comparison.</summary>
    /// <param name="parse">The selected language and symbols.</param>
    /// <returns>The new generator driver.</returns>
    private static GeneratorDriver Create(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Compiles and executes the complete selected consumer.</summary>
    /// <param name="output">The generated consumer.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("SequenceEntry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Captures deterministic generated hint names and complete source.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The sorted complete snapshot.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a focused failed invariant.</summary>
    /// <param name="condition">Whether the requirement holds.</param>
    /// <param name="message">The failed requirement.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
