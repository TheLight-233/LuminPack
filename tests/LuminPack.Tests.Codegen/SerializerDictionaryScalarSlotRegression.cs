using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks string and known-scalar dictionary-slot eligibility, target gates and runtime ordering.</summary>
internal static partial class SerializerDictionaryScalarSlotRegression
{
    /// <summary>Compiles complete entries and verifies eligible versus excluded dictionary shapes.</summary>
    /// <param name="references">Current runtime and platform metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyStableStringKeys(references);
        foreach (string mode in new[] { "Full", "Minimal" })
            Verify(references, mode, "int", "string", "\"a\"", "\"b\"", "\"\"", slots: true);
        Verify(references, "Minimal", "int", "int", "11", "22", "0", slots: true, rebuilt: true);
        Verify(references, "Minimal", "int", "System.Guid", "new System.Guid(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)", "System.Guid.NewGuid()", "default", slots: true, rebuilt: true);
        Verify(references, "Minimal", "int", "System.Version", "new System.Version(1, 1)", "new System.Version(2, 2)", "new System.Version()", slots: false);
        Verify(references, "Minimal", "long", "string", "\"a\"", "\"b\"", "\"\"", slots: false);
        Verify(references, "Minimal", "int", "string", "\"a\"", "\"b\"", "\"\"", slots: false, modern: false);
        Verify(references, "Minimal", "int", "string", "\"a\"", "\"b\"", "\"\"", slots: false, unity: true);
        Console.WriteLine("Passed string/scalar dictionary-slot eligibility, class-value/key exclusions, modern/Unity gates and retained ordering.");
    }

    /// <summary>Verifies one generated program, including the observable refill order of its selected mechanism.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="mode">The generation reachability mode.</param>
    /// <param name="key">The dictionary key type.</param>
    /// <param name="value">The dictionary value type.</param>
    /// <param name="first">The incoming value of key 1.</param>
    /// <param name="second">The incoming value of key 2.</param>
    /// <param name="existing">The value initially stored in both existing slots.</param>
    /// <param name="slots">Whether the scalar slot path must be generated.</param>
    /// <param name="modern">Whether the consuming target declares modern .NET support.</param>
    /// <param name="unity">Whether Unity must keep the original staged mechanism.</param>
    /// <param name="rebuilt">Whether raw pairs take the bucket rebuild, which refills in incoming order before any slot path.</param>
    private static void Verify(MetadataReference[] references, string mode, string key, string value, string first, string second, string existing,
        bool slots, bool modern = true, bool unity = false, bool rebuilt = false)
    {
        string source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
            public static class Entry
            {
                public static int Run()
                {
                    {{value}} a = {{first}}, b = {{second}};
                    var bytes = LuminPackSerializer.Serialize(new Dictionary<{{key}}, {{value}}> { [2] = b, [1] = a });
                    var map = new Dictionary<{{key}}, {{value}}> { [1] = {{existing}}, [2] = {{existing}} };
                    LuminPackSerializer.Deserialize<Dictionary<{{key}}, {{value}}>>(bytes, ref map);
                    if (map.Count != 2 || !map.TryGetValue(1, out var one) || !Equals(one, a) || !map.TryGetValue(2, out var two) || !Equals(two, b))
                        throw new System.InvalidOperationException("Refilled values changed: " + string.Join(",", map) + " from " + System.Convert.ToHexString(bytes));
                    return (int)map.First().Key;
                }
            }
            """;
        string[] symbols = modern ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["NET5_0_OR_GREATER"];
        if (unity) symbols = [.. symbols, "UNITY_2022_3_OR_NEWER"];
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse, preprocessorSymbols: symbols);
        var input = CSharpCompilation.Create("DictionaryScalarSlots_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parse, "Entry.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        string generated = string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.SourceText.ToString()));
        // Source expansion renames local functions, so the refill is located by its stable statements.
        bool hasSlots = generated.Contains("order.MoveNext()", StringComparison.Ordinal) &&
            generated.Contains("CollectionsMarshal.GetValueRefOrNullRef(destination, key)", StringComparison.Ordinal);
        Require(hasSlots == slots, "Incorrect scalar dictionary-slot eligibility: " + key + " -> " + value + (unity ? " (Unity)" : modern ? "" : " (legacy)"));
        if (slots)
        {
            Require(generated.Contains("ReferenceEquals(value.Comparer", StringComparison.Ordinal), "Missing default comparer identity guard.");
            Require(generated.Contains("Unsafe.IsNullRef(ref slot)", StringComparison.Ordinal), "The slot body lost its non-inserting guard.");
        }
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        int order = (int)Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null)!;
        // The in-place refill keeps the existing enumeration order; the staged path and the raw-pair bucket rebuild use incoming order.
        Require(order == (slots && !rebuilt ? 1 : 2), "Scalar dictionary refill ordering does not match its selected mechanism.");
    }

    /// <summary>Reports an incorrect generated dictionary contract.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
