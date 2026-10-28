using System.Reflection;
using System.Text.RegularExpressions;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks Union dictionary-slot certification, target gates, runtime ordering and retained incremental output.</summary>
internal static class SerializerDictionaryUnionSlotRegression
{
    private const string FixedCase = "[LuminPackable] public sealed partial class Fixed : Root { public int Number; }";

    /// <summary>Compiles complete entries and verifies certified versus excluded Union operation shapes.</summary>
    /// <param name="references">Current runtime and platform metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Minimal" })
            Verify(references, mode, FixedCase, slots: true, edits: mode == "Minimal");
        Verify(references, "Minimal", "[LuminPackable] public sealed partial class Fixed : Root { public int Number; [LuminPackOnDeserialized] public void After() { } }", slots: false);
        Verify(references, "Minimal", "[LuminPackable] public sealed partial class Fixed : Root { public int Number; [LuminPackConstructor] public Fixed(int number) => Number = number; }", slots: false);
        Verify(references, "Minimal", "[LuminPackable] public sealed partial class Fixed : Root { public volatile int Number; }", slots: false);
        Verify(references, "Minimal", FixedCase, slots: false, modern: false);
        Verify(references, "Minimal", FixedCase, slots: false, unity: true);
        Console.WriteLine("Passed Union dictionary-slot certification, string/callback/constructor/volatile exclusions, modern/Unity gates, retained ordering and retained/fresh eligibility edits.");
    }

    /// <summary>Verifies one generated program, including the observable refill order of its selected mechanism.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="mode">The generation reachability mode.</param>
    /// <param name="fixedCase">The declaration of the only potentially certified case.</param>
    /// <param name="slots">Whether the Union slot path must be generated.</param>
    /// <param name="modern">Whether the consuming target declares modern .NET support.</param>
    /// <param name="unity">Whether Unity must keep the original staged mechanism.</param>
    /// <param name="edits">Whether retained-driver eligibility edits are also checked.</param>
    private static void Verify(MetadataReference[] references, string mode, string fixedCase, bool slots, bool modern = true, bool unity = false, bool edits = false)
    {
        string source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
            [LuminPackable, LuminPackUnion(1, typeof(Fixed)), LuminPackUnion(2, typeof(Text))]
            public abstract partial class Root { }
            [LuminPackable] public sealed partial class Text : Root { public string Value = ""; }
            public static class Entry
            {
                public static int Run()
                {
                    var bytes = LuminPackSerializer.Serialize(new Dictionary<int, Root> { [2] = new Text { Value = "b" }, [1] = new Text { Value = "a" } });
                    var map = new Dictionary<int, Root> { [1] = new Text(), [2] = new Text() };
                    LuminPackSerializer.Deserialize<Dictionary<int, Root>>(bytes, ref map);
                    if (((Text)map[1]).Value != "a" || ((Text)map[2]).Value != "b") throw new System.InvalidOperationException("Staged Text values changed.");
                    var fixedBytes = LuminPackSerializer.Serialize(new Dictionary<int, Root> { [2] = Create(22), [1] = Create(11) });
                    var first = Create(0);
                    var fixedMap = new Dictionary<int, Root> { [1] = first, [2] = Create(0) };
                    LuminPackSerializer.Deserialize<Dictionary<int, Root>>(fixedBytes, ref fixedMap);
                    if (!ReferenceEquals(first, fixedMap[1]) && fixedMap[1] is not Fixed) throw new System.InvalidOperationException("Fixed case was not restored.");
                    if (Number(fixedMap[1]) != 11 || Number(fixedMap[2]) != 22) throw new System.InvalidOperationException("Fixed values changed.");
                    return fixedMap.First().Key;
                }

                private static Fixed Create(int number)
                {
                    var value = (Fixed)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Fixed));
                    typeof(Fixed).GetField("Number")!.SetValue(value, number);
                    return value;
                }

                private static int Number(Root value) => (int)typeof(Fixed).GetField("Number")!.GetValue(value)!;
            }
            """;
        string[] symbols = modern ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["NET5_0_OR_GREATER"];
        if (unity) symbols = [.. symbols, "UNITY_2022_3_OR_NEWER"];
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse, preprocessorSymbols: symbols);
        var caseTree = CSharpSyntaxTree.ParseText("using LuminPack.Attribute; " + fixedCase, parse, "Fixed.cs");
        var input = CSharpCompilation.Create("DictionaryUnionSlots_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parse, "Entry.cs"), caseTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = Driver(parse).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        string generated = Generated(driver);
        Require(HasSlots(generated) == slots, "Incorrect Union dictionary-slot eligibility: " + fixedCase);
        if (slots)
        {
            // Source expansion renames local functions, so the refill loop is located by its stable statements.
            string body = Regex.Match(generated, @"GetValueRefOrNullRef\(destination.*?remaining\d*:", RegexOptions.Singleline).Value;
            Require(body.Contains("GetType() != typeof(global::Fixed)", StringComparison.Ordinal), "The certified case lost its exact runtime type guard.");
            Require(!body.Contains("typeof(global::Text)", StringComparison.Ordinal), "A variable-length case entered the slot refill.");
            Require(body.Contains("Unsafe.IsNullRef", StringComparison.Ordinal) && body.Contains("goto remaining", StringComparison.Ordinal),
                "The slot body lost its non-inserting guard or its staged continuation.");
            Require(body.Contains("Unsafe.As<global::Fixed>(item)", StringComparison.Ordinal) && !body.Contains("item = new", StringComparison.Ordinal),
                "The slot body must refill the existing exact instance; construction belongs to the staged continuation.");
            Require(generated.Contains("ReferenceEquals(value.Comparer", StringComparison.Ordinal), "Missing default comparer identity guard.");
        }
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        int order = (int)Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null)!;
        Require(order == (slots ? 1 : 2), "Union dictionary refill ordering does not match its selected mechanism.");
        if (edits) VerifyEdits(driver, input, caseTree, parse);
    }

    /// <summary>Checks that case-only eligibility edits invalidate a retained driver exactly like fresh generation.</summary>
    /// <param name="initial">The completed eligible generation.</param>
    /// <param name="input">The input before any case edit.</param>
    /// <param name="fixedCase">The separate case declaration tree.</param>
    /// <param name="parse">Modern compiler and target settings.</param>
    private static void VerifyEdits(GeneratorDriver initial, CSharpCompilation input, SyntaxTree fixedCase, CSharpParseOptions parse)
    {
        foreach (string members in new[]
        {
            "public int Number; [LuminPackOnDeserializing] public static void BeforeRead() {}",
            "public int Number; [LuminPackConstructor] public Fixed(int number) => Number = number;",
            "public volatile int Number;",
            "public string Number = \"\";"
        })
        {
            var changedCase = CSharpSyntaxTree.ParseText("using LuminPack.Attribute; [LuminPackable] public sealed partial class Fixed : Root { " + members + " }", parse, "Fixed.cs");
            var changed = input.ReplaceSyntaxTree(fixedCase, changedCase);
            var retained = initial.RunGenerators(changed);
            var fresh = Driver(parse).RunGenerators(changed);
            Require(!HasSlots(Generated(retained)), "A case edit retained an uncertified Union slot refill: " + members);
            var actual = Sources(retained);
            var expected = Sources(fresh);
            Require(actual.Count == expected.Count && actual.All(pair => expected.GetValueOrDefault(pair.Key) == pair.Value),
                "Retained Union dictionary eligibility differs from fresh generation: " + members);
            retained = retained.RunGenerators(input);
            Require(HasSlots(Generated(retained)), "Removing a case exclusion did not restore Union slot reuse.");
        }
    }

    /// <summary>Detects the Union slot refill by its order proof and non-inserting slot lookup.</summary>
    /// <param name="generated">The complete generated source.</param>
    /// <returns>Whether the Union slot path was emitted.</returns>
    private static bool HasSlots(string generated) =>
        generated.Contains("order.MoveNext()", StringComparison.Ordinal) && generated.Contains("CollectionsMarshal.GetValueRefOrNullRef", StringComparison.Ordinal);

    /// <summary>Creates a fresh pair of generators under the fixture's parse options.</summary>
    /// <param name="parse">The language version and target symbols.</param>
    /// <returns>An unretained driver.</returns>
    private static GeneratorDriver Driver(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Concatenates all generated text.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The complete generated source.</returns>
    private static string Generated(GeneratorDriver driver) =>
        string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.SourceText.ToString()));

    /// <summary>Maps generated file identities to their exact text.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The generated files.</returns>
    private static Dictionary<string, string> Sources(GeneratorDriver driver) =>
        driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToDictionary(result => result.HintName, result => result.SourceText.ToString());

    /// <summary>Reports an incorrect generated Union dictionary contract.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
