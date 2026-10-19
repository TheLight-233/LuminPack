using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks canonical leaf eligibility, target gates and the retained dictionary fallback.</summary>
internal static class SerializerDictionarySlotRegression
{
    /// <summary>Compiles complete entries and verifies approved versus excluded operation shapes.</summary>
    /// <param name="references">Current runtime and platform metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            Verify(references, mode, "[LuminPackable] public sealed class Leaf { public int Number; public string Text = \"\"; }", true);
            Verify(references, mode, "[LuminPackable(GeneratorType.VersionTolerant)] public sealed class Leaf { public int Number; public string Text = \"\"; }", true);
        }
        Verify(references, "Minimal", "[LuminPackable] public sealed class Leaf { public static int Created; [LuminPackInclude] private int Number = ++Created; public int Read() => Number; }", true);
        Verify(references, "Minimal", "[LuminPackable] public sealed class Leaf { public int Number; [LuminPackOnDeserializing] public static void BeforeRead() {} }", false);
        Verify(references, "Minimal", "[LuminPackable] public sealed class Leaf { public int Number { get; set; } }", false);
        Verify(references, "Minimal", "[LuminPackable] public sealed class Leaf { public Child Item = new(); } [LuminPackable] public sealed class Child { public int Number; }", false);
        Verify(references, "Minimal", "[LuminPackable(GeneratorType.CircleReference)] public sealed class Leaf { [LuminPackOrder(0)] public int Number; }", false);
        Verify(references, "Minimal", "[LuminPackable] public sealed class Leaf { public int Number; }", false, modern: false);
        Verify(references, "Minimal", "[LuminPackable] public sealed class Leaf { public int Number; }", false, unity: true);
        Console.WriteLine("Passed dictionary leaf-slot guards, ordinary/named Full/Minimal entries, cold staging, and modern/Unity fallback shapes.");
    }

    /// <summary>Verifies one complete generated program, including its observable refill order.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="mode">The generation reachability mode.</param>
    /// <param name="leaf">The complete canonical value declaration.</param>
    /// <param name="slots">Whether the slot path must be generated.</param>
    /// <param name="modern">Whether the consuming target declares modern .NET support.</param>
    /// <param name="unity">Whether Unity must keep the original fallback.</param>
    private static void Verify(MetadataReference[] references, string mode, string leaf, bool slots, bool modern = true, bool unity = false)
    {
        string privateCheck = leaf.Contains("public int Read()", StringComparison.Ordinal)
            ? "if (map[1].Read() != 2 || map[2].Read() != 1) throw new System.InvalidOperationException(\"Private leaf storage was not restored.\");" : "";
        string source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
            namespace DictionarySlotModels;
            public static class Entry
            {
                public static int Run()
                {
                    var bytes = LuminPackSerializer.Serialize(new Dictionary<int, Leaf> { [2] = new(), [1] = new() });
                    var map = new Dictionary<int, Leaf> { [1] = new(), [2] = new() };
                    LuminPackSerializer.Deserialize<Dictionary<int, Leaf>>(bytes, ref map);
                    {{privateCheck}}
                    return map.First().Key;
                }
            }
            """;
        string[] symbols = modern ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["NET5_0_OR_GREATER"];
        if (unity) symbols = [.. symbols, "UNITY_2022_3_OR_NEWER"];
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse, preprocessorSymbols: symbols);
        var leafTree = CSharpSyntaxTree.ParseText("using LuminPack.Attribute; namespace DictionarySlotModels; " + leaf, parse, "Leaf.cs");
        var input = CSharpCompilation.Create("DictionarySlots_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parse, "Entry.cs"), leafTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        string generated = string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.SourceText.ToString()));
        Require(generated.Contains("CollectionsMarshal.GetValueRefOrNullRef", StringComparison.Ordinal) == slots, "Incorrect dictionary slot eligibility: " + leaf);
        if (slots)
        {
            Require(generated.Contains("Unsafe.IsNullRef", StringComparison.Ordinal), "Missing non-inserting slot guard.");
            Require(generated.Contains("seen", StringComparison.Ordinal) && generated.Contains("parsed", StringComparison.Ordinal), "Missing cold parsed-prefix staging.");
            Require(generated.Contains("ReferenceEquals", StringComparison.Ordinal), "Missing default comparer identity guard.");
        }
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        var assembly = Assembly.Load(image.ToArray());
        int order = (int)assembly.GetType("DictionarySlotModels.Entry")!.GetMethod("Run")!.Invoke(null, null)!;
        Require(order == (slots ? 1 : 2), "Dictionary refill ordering does not match its selected mechanism.");
        if (slots && mode == "Minimal" && !leaf.Contains("VersionTolerant", StringComparison.Ordinal))
            VerifyEdits(driver, input, leafTree, parse);
    }

    /// <summary>Checks dependency invalidation when only the leaf declaration changes in a retained driver.</summary>
    /// <param name="initial">The completed eligible generation.</param>
    /// <param name="input">The input before any leaf edit.</param>
    /// <param name="leaf">The separate declaration tree.</param>
    /// <param name="parse">Modern compiler and target settings.</param>
    private static void VerifyEdits(GeneratorDriver initial, CSharpCompilation input, SyntaxTree leaf, CSharpParseOptions parse)
    {
        foreach (string members in new[]
        {
            "public int Number; [LuminPackOnDeserializing] public static void BeforeRead() {}",
            "public int Number { get; set; }",
            "public System.Collections.Generic.List<int> Numbers = new();"
        })
        {
            var changedLeaf = CSharpSyntaxTree.ParseText("using LuminPack.Attribute; namespace DictionarySlotModels; [LuminPackable] public sealed class Leaf { " + members + " }", parse, "Leaf.cs");
            var changed = input.ReplaceSyntaxTree(leaf, changedLeaf);
            var retained = initial.RunGenerators(changed);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse);
            fresh = fresh.RunGenerators(changed);
            var actual = retained.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToDictionary(result => result.HintName, result => result.SourceText.ToString());
            var expected = fresh.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToDictionary(result => result.HintName, result => result.SourceText.ToString());
            Require(!actual.Values.Any(text => text.Contains("CollectionsMarshal.GetValueRefOrNullRef", StringComparison.Ordinal)), "Leaf edit retained an unsafe dictionary slot operation.");
            Require(actual.Count == expected.Count && actual.All(pair => expected.GetValueOrDefault(pair.Key) == pair.Value), "Retained dictionary eligibility differs from fresh generation.");
            retained = retained.RunGenerators(input);
            Require(retained.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .Any(result => result.SourceText.ToString().Contains("CollectionsMarshal.GetValueRefOrNullRef", StringComparison.Ordinal)), "Removing an exclusion did not restore slot reuse.");
        }
    }

    /// <summary>Reports an incorrect generated dictionary contract.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
