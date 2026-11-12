using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Pipeline;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Checks semantic invalidation and retained-driver equality across independent consumer edits.</summary>
    /// <param name="references">The platform and runtime metadata.</param>
    /// <param name="parse">The common consumer language and framework settings.</param>
    /// <returns>Zero after all incremental and dependency contracts pass.</returns>
    private static int VerifyIncremental(MetadataReference[] references, CSharpParseOptions parse)
    {
        const string models = """
            using System.Collections.Generic;
            using LuminPack.Attribute;
            [LuminPackable] public sealed class Item
            {
                public int Number;
                public string Text;
                public List<int> Values;
                public Dictionary<int, string> Labels;
            }
            [LuminPackable] public sealed class Other { public string Name; }
            """;
        const string calls = """
            using System.Collections.Generic;
            using LuminPack;
            public static class Consumer
            {
                public static void Main() { }
                public static object Run(Item value) => LuminPackSerializer.Serialize(value);
                public static byte[] Values(List<int> value) => LuminPackSerializer.Serialize(value);
            }
            """;
        const string noise = "public static class Noise { public static int Value() => 1; }";
        SyntaxTree modelTree = CSharpSyntaxTree.ParseText(models, parse, "Models.cs");
        SyntaxTree callTree = CSharpSyntaxTree.ParseText(calls, parse, "Calls.cs");
        SyntaxTree noiseTree = CSharpSyntaxTree.ParseText(noise, parse, "Noise.cs");
        var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true,
            optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable);
        var original = CSharpCompilation.Create("NextIncrementalContracts", new[] { modelTree, callTree, noiseTree }, references, options);
        var tracking = new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true);
        GeneratorDriver CreateDriver() => CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() },
            parseOptions: parse, driverOptions: tracking);
        GeneratorDriver retained = CreateDriver();
        CheckRun(original, "initial");
        var comments = original.ReplaceSyntaxTree(noiseTree, CSharpSyntaxTree.ParseText(noise + "\n// Unrelated edit.\n", parse, "Noise.cs"));
        CheckRun(comments, "unrelated comment");
        var steps = retained.GetRunResult().Results[0].TrackedSteps["LuminPack.Next.CollectedCalls"];
        foreach (var step in steps)
            foreach (var output in step.Outputs)
                RequireIncremental(output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    "An unrelated edit invalidated the collected call facts.");
        const string unrelated = "public static class Noise { public static byte[] Serialize(int value) => System.Array.Empty<byte>(); " +
            "public static byte[] Value() => Serialize(1); }";
        CheckRun(original.ReplaceSyntaxTree(noiseTree, CSharpSyntaxTree.ParseText(unrelated, parse, "Noise.cs")), "unrelated serializer name");
        CheckRun(original.ReplaceSyntaxTree(callTree, CSharpSyntaxTree.ParseText(calls + "\n// Changed interception location.\n", parse, "Calls.cs")),
            "call location");
        CheckRun(original.ReplaceSyntaxTree(modelTree, CSharpSyntaxTree.ParseText(models.Replace("public int Number;", "public long Number;"),
            parse, "Models.cs")), "member type");
        CheckRun(original.ReplaceSyntaxTree(callTree, CSharpSyntaxTree.ParseText(calls.Replace("List<int>", "List<long>"), parse, "Calls.cs")),
            "root element type");
        CheckRun(original.ReplaceSyntaxTree(callTree, CSharpSyntaxTree.ParseText(calls.Replace("Serialize(value);", "SerializeJson(value);")
            .Replace("public static byte[] Values", "public static string Values"), parse, "Calls.cs")), "operation direction");
        CheckRun(original.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            "[assembly: LuminPack.Attribute.LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF8)]",
            parse, "Options.cs")), "assembly profile");
        CheckRun(original.WithAssemblyName("RenamedIncrementalContracts"), "assembly identity");
        const string wrapper = """
            public static class Wrapper
            {
                public static byte[] Write<T>(T value) => LuminPack.LuminPackSerializer.Serialize(value);
                public static byte[] Closed() => Write(new System.Collections.Generic.HashSet<short>());
            }
            """;
        CheckRun(original.AddSyntaxTrees(CSharpSyntaxTree.ParseText(wrapper, parse, "Wrapper.cs")), "source generic wrapper");
        CheckRun(original.ReplaceSyntaxTree(modelTree, CSharpSyntaxTree.ParseText(
            models.Replace("[LuminPackable] public sealed class Other { public string Name; }", string.Empty), parse, "Models.cs")),
            "removed declaration");
        CheckRun(original.WithOptions(options.WithAllowUnsafe(false)), "unsafe setting");
        CheckRun(original, "restored input");
        VerifyFormatterDependencies(original, modelTree, models, parse);
        Console.WriteLine("Next incremental contracts: 13 retained/fresh compilations and formatter dependency checks passed.");
        return 0;

        void CheckRun(CSharpCompilation input, string scenario)
        {
            retained = retained.RunGeneratorsAndUpdateCompilation(input, out var retainedOutput, out var retainedDiagnostics);
            GeneratorDriver fresh = CreateDriver().RunGeneratorsAndUpdateCompilation(input, out var freshOutput, out var freshDiagnostics);
            var actual = Sources(retained);
            var expected = Sources(fresh);
            RequireIncremental(actual.Count == expected.Count, scenario + ": generated file count differs.");
            foreach (var file in expected)
                RequireIncremental(actual.TryGetValue(file.Key, out string? text) && text == file.Value,
                    scenario + ": retained source differs for " + file.Key);
            RequireIncremental(DiagnosticSnapshot(retainedOutput, retainedDiagnostics) == DiagnosticSnapshot(freshOutput, freshDiagnostics),
                scenario + ": retained diagnostics differ.");
            foreach (Diagnostic diagnostic in retainedDiagnostics.Concat(retainedOutput.GetDiagnostics()))
                RequireIncremental(diagnostic.Severity != DiagnosticSeverity.Error, scenario + ": " + diagnostic);
        }
    }

    /// <summary>Checks the dependency cache against equal facts, changed graphs and project settings.</summary>
    /// <param name="compilation">The original consumer input.</param>
    /// <param name="modelTree">The declaration tree that edits replace.</param>
    /// <param name="models">The original declarations.</param>
    /// <param name="parse">The consumer parse options.</param>
    private static void VerifyFormatterDependencies(CSharpCompilation compilation, SyntaxTree modelTree, string models, CSharpParseOptions parse)
    {
        var options = GeneratorOptionsFactory.From(compilation);
        var model = TypeModelFactory.TryCreate(compilation.GetTypeByMetadataName("Item")!, compilation, options).Model!;
        var listType = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!
            .Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        var root = TypeModelFactory.TryCreateRoot(listType, compilation, options)!;
        RequireIncremental(ReferenceEquals(root, TypeModelFactory.TryCreateRoot(listType, compilation, options)),
            "Repeated closed roots did not reuse their compilation-owned model.");
        var payloads = new List<Model.TypeModel> { model };
        var roots = new List<Model.MemberModel> { root };
        var cached = new SharedFormatterInputs(payloads, roots, options);
        var equal = compilation.ReplaceSyntaxTree(modelTree, CSharpSyntaxTree.ParseText(models + "\n// Same semantic model.", parse, "Models.cs"));
        var equalOptions = GeneratorOptionsFactory.From(equal);
        var equalModel = TypeModelFactory.TryCreate(equal.GetTypeByMetadataName("Item")!, equal, equalOptions).Model!;
        RequireIncremental(cached.Matches(new List<Model.TypeModel> { equalModel }, roots, equalOptions),
            "Value-equal models from a new compilation invalidated the shared owners.");
        var changed = compilation.ReplaceSyntaxTree(modelTree, CSharpSyntaxTree.ParseText(
            models.Replace("List<int>", "List<long>"), parse, "Models.cs"));
        var changedOptions = GeneratorOptionsFactory.From(changed);
        var changedModel = TypeModelFactory.TryCreate(changed.GetTypeByMetadataName("Item")!, changed, changedOptions).Model!;
        RequireIncremental(!cached.Matches(new List<Model.TypeModel> { changedModel }, roots, changedOptions),
            "A changed member graph reused stale shared owners.");
        var renamedOptions = GeneratorOptionsFactory.From(compilation.WithAssemblyName("ChangedOwnerNames"));
        RequireIncremental(!cached.Matches(payloads, roots, renamedOptions), "Changed project options reused stale owners.");
        roots.Add(root);
        RequireIncremental(!cached.Matches(payloads, roots, options), "The cache retained a mutable caller root list.");
        roots.Clear();
        RequireIncremental(!cached.Matches(payloads, roots, options), "A removed root reused stale owners.");
        var reordered = SharedFormatterSet.Collect(new[] { model, equalModel }, options, roots: new[] { root, root });
        RequireIncremental(cached.Set.Equals(reordered), "Duplicate dependencies changed the sorted owner catalog.");
    }

    /// <summary>Compares complete compiler and generator diagnostic identities and locations.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    /// <param name="generatorDiagnostics">The generator's diagnostics.</param>
    /// <returns>An ordinal diagnostic snapshot.</returns>
    private static string DiagnosticSnapshot(Compilation output, IEnumerable<Diagnostic> generatorDiagnostics)
        => string.Join("\n", generatorDiagnostics.Concat(output.GetDiagnostics()).Select(static diagnostic => diagnostic.ToString())
            .Order(StringComparer.Ordinal));

    /// <summary>Reports a failed incremental contract through the generator exception helper.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The contract failure.</param>
    private static void RequireIncremental(bool condition, string message)
    {
        if (!condition)
            LuminPackExceptionHelper.ThrowInvalidOperation(message);
    }
}
