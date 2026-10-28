using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Protects span-based queue construction and the older enumerable fallback.</summary>
internal static class SerializerImmutableQueueConstructionRegression
{
    /// <summary>Executes queue profiles and failed-publication checks in every generation tier and both target branches.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            global using System.Linq;
            using System;
            using System.Collections.Immutable;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            public static class QueueCases
            {
                public static void Check()
                {
                    CheckProfile<LuminPackUtf16LengthOptions>();
                    CheckProfile<LuminPackUtf8LengthOptions>();
                    CheckProfile<LuminPackUtf16TokenOptions>();
                    CheckProfile<LuminPackUtf8TokenOptions>();
                }
                private static void CheckProfile<TProfile>() where TProfile : struct, ILuminPackOptions
                {
                    foreach (int length in new[] { 0, 1, 2, 3, 16, 257 })
                    {
                        var values = Enumerable.Range(0, length).Select(i => i * 7 - 13).ToArray();
                        var source = ImmutableQueue.CreateRange(values);
                        byte[] bytes = LuminPackSerializer.Serialize<TProfile>(source);
                        ImmutableQueue<int> fresh = null;
                        if (LuminPackSerializer.Deserialize<TProfile>(bytes, ref fresh) != bytes.Length || !fresh.SequenceEqual(values))
                            throw new Exception("Queue values or consumed width changed.");
                        var original = ImmutableQueue.Create(999);
                        var reuse = original;
                        LuminPackSerializer.Deserialize<TProfile>(bytes, ref reuse);
                        if (!reuse.SequenceEqual(values) || !original.SequenceEqual(new[] { 999 }))
                            throw new Exception("Immutable queue reuse mutated its old value.");
                        if (!reuse.Enqueue(23).SequenceEqual(values.Append(23)))
                            throw new Exception("The reconstructed queue cannot enqueue.");
                        if (length != 0 && !reuse.Dequeue().SequenceEqual(values.Skip(1)))
                            throw new Exception("The reconstructed queue cannot dequeue.");
                        IImmutableQueue<int> contract = source;
                        byte[] interfaceBytes = LuminPackSerializer.Serialize<TProfile>(contract);
                        if (!interfaceBytes.SequenceEqual(bytes)) throw new Exception("Interface queue wire bytes changed.");
                        contract = original;
                        if (LuminPackSerializer.Deserialize<TProfile>(bytes, ref contract) != bytes.Length || !contract.SequenceEqual(values))
                            throw new Exception("Interface queue reconstruction changed.");
                        for (int prefix = 0; prefix < bytes.Length; prefix++)
                        {
                            reuse = original;
                            contract = original;
                            bool failed = false;
                            try { LuminPackSerializer.Deserialize<TProfile>(bytes.AsSpan(0, prefix), ref reuse); }
                            catch { failed = true; }
                            if (!failed || !ReferenceEquals(reuse, original)) throw new Exception("Truncated concrete queue was published.");
                            failed = false;
                            try { LuminPackSerializer.Deserialize<TProfile>(bytes.AsSpan(0, prefix), ref contract); }
                            catch { failed = true; }
                            if (!failed || !ReferenceEquals(contract, original)) throw new Exception("Truncated interface queue was published.");
                        }
                    }
                    var text = ImmutableQueue.Create("", "alpha", "\u65e5\u672c", "\U0001F30D");
                    byte[] textBytes = LuminPackSerializer.Serialize<TProfile>(text);
                    ImmutableQueue<string> result = null;
                    LuminPackSerializer.Deserialize<TProfile>(textBytes, ref result);
                    if (!result.SequenceEqual(text)) throw new Exception("Queue strings or order changed.");
                    IImmutableQueue<string> textContract = null;
                    LuminPackSerializer.Deserialize<TProfile>(textBytes, ref textContract);
                    if (!textContract.SequenceEqual(text)) throw new Exception("Interface string queue changed.");
                    ImmutableQueue<int> absent = null;
                    byte[] nullBytes = LuminPackSerializer.Serialize<TProfile>(absent);
                    absent = ImmutableQueue.Create(19);
                    if (LuminPackSerializer.Deserialize<TProfile>(nullBytes, ref absent) != nullBytes.Length || absent != null)
                        throw new Exception("Null concrete queue framing changed.");
                    IImmutableQueue<int> absentContract = ImmutableQueue.Create(19);
                    if (LuminPackSerializer.Deserialize<TProfile>(nullBytes, ref absentContract) != nullBytes.Length || absentContract != null)
                        throw new Exception("Null interface queue framing changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var modern = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var legacy = modern.WithPreprocessorSymbols("NET8_0_OR_GREATER");
            var initial = CSharpCompilation.Create("QueueCases" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), modern, "QueueCases.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var (driver, output) = Generate(initial, modern);
            VerifySelection(driver, span: true);
            Execute(output);
            var compatibility = initial.RemoveAllSyntaxTrees().AddSyntaxTrees(initial.SyntaxTrees.Single().WithRootAndOptions(initial.SyntaxTrees.Single().GetRoot(), legacy));
            driver = driver.WithUpdatedParseOptions(legacy).RunGeneratorsAndUpdateCompilation(compatibility, out output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            VerifySelection(driver, span: false);
            Execute(output);
            var (fresh, _) = Generate(compatibility, legacy);
            Require(Trees(driver).SequenceEqual(Trees(fresh)), "Changing target symbols retained a stale queue construction branch.");
            driver = driver.WithUpdatedParseOptions(modern).RunGeneratorsAndUpdateCompilation(initial, out output, out diagnostics);
            VerifySelection(driver, span: true);
            Execute(output);
            var (restored, _) = Generate(initial, modern);
            Require(Trees(driver).SequenceEqual(Trees(restored)), "Restoring target symbols retained a stale queue fallback.");
        }
        Console.WriteLine("Passed immutable queue profiles, boundaries, four tiers and retained/fresh target changes.");
    }

    /// <summary>Runs both production generators for an isolated queue consumer.</summary>
    /// <param name="compilation">The initial consumer compilation.</param>
    /// <param name="parse">The selected target symbols.</param>
    /// <returns>The retained driver and generated compilation.</returns>
    private static (GeneratorDriver Driver, Compilation Output) Generate(CSharpCompilation compilation, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        return (driver, output);
    }

    /// <summary>Checks that the active binary construction uses the supported BCL overload.</summary>
    /// <param name="driver">The completed generator run.</param>
    /// <param name="span">Whether the target supports span queue construction.</param>
    private static void VerifySelection(GeneratorDriver driver, bool span)
    {
        string generated = string.Join("\n", Trees(driver));
        Require(generated.Contains(span ? "ImmutableQueue.Create(new global::System.ReadOnlySpan" : "ImmutableQueue.CreateRange(new global::System.ArraySegment", StringComparison.Ordinal),
            "The queue construction does not match the target framework.");
    }

    /// <summary>Compiles and executes all targeted queue invariants.</summary>
    /// <param name="output">The generated consumer.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("QueueCases")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Orders complete generated snapshots for retained-versus-fresh comparison.</summary>
    /// <param name="driver">The completed generator run.</param>
    /// <returns>The generated source snapshots.</returns>
    private static string[] Trees(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + "\n" + result.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a failed construction or generator invariant.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
