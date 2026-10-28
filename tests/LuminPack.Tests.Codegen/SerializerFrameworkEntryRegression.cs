using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects equivalent enum and BitArray facades and their original checked roots.</summary>
internal static class SerializerFrameworkEntryRegression
{
    /// <summary>Compiles all tiers, executes exact bytes and checks retained enum-width edits.</summary>
    /// <param name="references">Platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            public enum WireEnum : short { Value = 17 }
            public static class FrameworkEntry
            {
                public static byte[] Write(WireEnum value) => LuminPackSerializer.Serialize(value);
                public static byte[] WriteBits(BitArray value) => LuminPackSerializer.Serialize(value);
                public static int Read(ReadOnlySpan<byte> bytes, ref WireEnum value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadBits(ReadOnlySpan<byte> bytes, ref BitArray value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static void Check()
                {
                    WireEnum source = WireEnum.Value;
                    byte[] expected = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref source,1)).ToArray();
                    if (!Write(source).AsSpan().SequenceEqual(expected)) throw new Exception("Enum bytes changed.");
                    WireEnum value = default;
                    if (Read(expected,ref value) != expected.Length || value != source) throw new Exception("Enum read changed.");
                    for (int length=0; length<expected.Length; length++)
                    {
                        value = source;
                        bool failed=false;
                        try { Read(expected.AsSpan(0,length),ref value); } catch { failed=true; }
                        if (!failed || value != source) throw new Exception("Enum preaccess validation changed.");
                    }
                    var bits = new BitArray(33,true);
                    byte[] bytes = WriteBits(bits);
                    if (bytes.Length != 12) throw new Exception("BitArray word padding changed.");
                    var original = bits;
                    if (ReadBits(bytes,ref bits) != bytes.Length || !ReferenceEquals(bits,original) || !bits[32]) throw new Exception("BitArray read changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("FrameworkEntry" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckEntries(driver);
            Execute(output);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(text.Replace("WireEnum : short", "WireEnum : long", StringComparison.Ordinal), parse, "Entry.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var editedOutput, out diagnostics);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(edited))), "An enum width edit retained stale generated code.");
            CheckEntries(driver);
            Execute(editedOutput);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(compilation))), "Restoring enum width retained stale generated code.");
            Execute(output);
        }
        Console.WriteLine("Passed checked enum/BitArray entries, four tiers and retained/fresh enum-width edits.");
    }

    /// <summary>Checks exact facade selection without allowing the unchecked scalar read shortcut.</summary>
    /// <param name="driver">The completed generation.</param>
    private static void CheckEntries(GeneratorDriver driver)
    {
        var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        string all = string.Join("\n", sources.Select(source => source.SourceText));
        Require(all.Contains("directLength = Unsafe.SizeOf<global::WireEnum>()", StringComparison.Ordinal), "The enum writer lost its exact width.");
        Require(all.Contains("directEvaluator", StringComparison.Ordinal), "The BitArray writer lost its exact evaluator entry.");
        var roots = sources.SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText == "RootRead" && method.ParameterList.Parameters.Count == 2 &&
                method.ParameterList.Parameters[1].Type!.ToString() == "global::WireEnum").ToArray();
        Require(roots.Length != 0 && roots.All(method => method.ToString().Contains("EnsureReadable", StringComparison.Ordinal)), "The enum root lost its preaccess bounds check.");
        var entries = sources.SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText == "Deserialize" && method.ParameterList.Parameters.Count >= 2 &&
                method.ParameterList.Parameters[1].Type!.ToString() == "global::WireEnum");
        Require(entries.All(method => !method.ToString().Contains("ReadComplete(ref MemoryMarshal.GetReference(buffer), ref value)", StringComparison.Ordinal)),
            "The enum entry selected an unchecked raw scalar facade.");
    }

    /// <summary>Creates a fresh driver without retained generation state.</summary>
    /// <param name="parse">The target language and conditional symbols.</param>
    /// <returns>The new driver.</returns>
    private static GeneratorDriver Create(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Compiles and executes the complete concrete fixture.</summary>
    /// <param name="output">The generated consumer.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("FrameworkEntry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Records exact generated hint names and source for incremental comparison.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The sorted complete source snapshot.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a failed focused invariant.</summary>
    /// <param name="condition">Whether the required behavior holds.</param>
    /// <param name="message">The precise failed requirement.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
