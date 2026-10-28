using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects approved immutable-list writer staging and retained formatter exclusions.</summary>
internal static class SerializerImmutableListWriteRegression
{
    /// <summary>Executes concrete profiles in every tier and compares retained/fresh specialized edits.</summary>
    /// <param name="references">Platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Immutable;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class Plain { [LuminPackOrder(0)]public int Value; [LuminPackOrder(1)]public string Text; }
            [LuminPackable(GeneratorType.Custom)] public sealed class Manual
            {
                public static void Serialize(ref LuminPackWriter writer,in Manual value) => writer.Write(137);
                public static void Deserialize(ref LuminPackReader reader,ref Manual value) { reader.Read<int>();value=new(); }
            }
            [LuminPackable,LuminPackOption(StructLayout=LuminPackStructLayout.Packed)] public struct Portable { public byte Tag;public long Value; }
            public static class ImmutableWriteEntry
            {
                public static byte[] Text(ImmutableList<string> value) => LuminPackSerializer.Serialize(value);
                public static byte[] Ordinary(ImmutableList<Plain> value) => LuminPackSerializer.Serialize(value);
                public static byte[] Custom(ImmutableList<Manual> value) => LuminPackSerializer.Serialize(value);
                public static byte[] Packed(ImmutableList<Portable> value) => LuminPackSerializer.Serialize(value);
                public static byte[] Interface(IImmutableList<Plain> value) => LuminPackSerializer.Serialize(value);
                public static void Check()
                {
                    CheckProfile<LuminPackUtf8LengthOptions>();
                    CheckProfile<LuminPackUtf8TokenOptions>();
                    CheckProfile<LuminPackUtf16LengthOptions>();
                    CheckProfile<LuminPackUtf16TokenOptions>();
                }
                private static void CheckProfile<TProfile>() where TProfile:struct,ILuminPackOptions
                {
                    string[] items={"a","",null,"\u00e9\u4e2d\ud83d\ude00"};
                    var list=ImmutableList.Create(items);
                    if(!LuminPackSerializer.Serialize<TProfile>(list).AsSpan().SequenceEqual(LuminPackSerializer.Serialize<TProfile>(items))) throw new Exception("Staging changed string bytes.");
                    Plain[] objects={new Plain{Value=137,Text="value"},null};
                    if(!LuminPackSerializer.Serialize(ImmutableList.Create(objects)).AsSpan().SequenceEqual(LuminPackSerializer.Serialize(objects))) throw new Exception("Staging changed object bytes.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("ImmutableWrite" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckSelection(driver, ordinary: true);
            Execute(output);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(
                text.Replace("[LuminPackable] public sealed class Plain", "[LuminPackable(GeneratorType.VersionTolerant)] public sealed class Plain", StringComparison.Ordinal), parse, "Entry.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var editedOutput, out diagnostics);
            CheckSelection(driver, ordinary: false);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(edited))), "An ordinary-to-versioned edit retained immutable-list staging.");
            Execute(editedOutput);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics);
            CheckSelection(driver, ordinary: true);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(compilation))), "Restoring ordinary storage retained a stale immutable-list writer.");
            Execute(output);
        }
        Console.WriteLine("Passed immutable-list staging profiles, exclusions, four tiers and retained/fresh specialized edits.");
    }

    /// <summary>Checks concrete reference staging without changing specialized or interface traversal.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="ordinary">Whether Plain still has its ordinary formatter.</param>
    private static void CheckSelection(GeneratorDriver driver, bool ordinary)
    {
        int selected = 0;
        foreach (var method in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                     .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                     .Where(method => method.Identifier.ValueText == "RootWrite" && method.ParameterList.Parameters.Count == 2))
        {
            string type = method.ParameterList.Parameters[1].Type!.ToString();
            if (!type.Contains("ImmutableList<", StringComparison.Ordinal)) continue;
            bool expected = !type.Contains("IImmutableList<", StringComparison.Ordinal) &&
                (type.Contains("<string>", StringComparison.Ordinal) || ordinary && type.Contains("<global::Plain>", StringComparison.Ordinal));
            Require(method.ToString().Contains("value.CopyTo(items)", StringComparison.Ordinal) == expected, "Immutable-list staging qualification differs: " + type);
            if (expected)
                Require(method.ToString().Contains("Return(items, true)", StringComparison.Ordinal), "Reference staging lost complete rental clearing.");
            selected++;
        }
        Require(selected == 5, "The immutable-list writer fixture omitted a selected contract.");
    }

    /// <summary>Creates an independent driver for fresh comparisons.</summary>
    /// <param name="parse">The selected language and target symbols.</param>
    /// <returns>The fresh driver.</returns>
    private static GeneratorDriver Create(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Compiles and executes the complete selected consumer.</summary>
    /// <param name="output">The generated consumer.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("ImmutableWriteEntry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Records deterministic generated hint names and complete texts.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The sorted generated-source snapshot.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a failed focused invariant.</summary>
    /// <param name="condition">Whether the requirement holds.</param>
    /// <param name="message">The precise failed requirement.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
