using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerCompiledOptionRegression
{
    /// <summary>Executes only the untrusted-input read-site check contracts.</summary>
    /// <param name="references">The actual framework and runtime references.</param>
    internal static void VerifyInputFacts(MetadataReference[] references)
        => VerifyInputFacts(new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]), references);

    /// <summary>Checks frozen read-site limits, recursive depth counting and retained-driver invalidation in both emission tiers.</summary>
    /// <param name="parse">The supported consuming language and framework.</param>
    /// <param name="references">The current runtime metadata.</param>
    private static void VerifyInputFacts(CSharpParseOptions parse, MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackOption(UntrustedData=true, MaxDepth=4, MaxCollectionLength=8)]
            public class ProofRoot { public ProofChild Child; }
            [LuminPackable]
            public class ProofChild { public int Number; public bool Flag; public bool[] Bits = Array.Empty<bool>(); }
            [LuminPackable]
            public class TrustedTwin { public ProofChild Child; }
            public static class Entry
            {
                public static void Check()
                {
                    var value = new ProofRoot { Child = new ProofChild { Number=17, Flag=true, Bits=new[] { true, false, true } } };
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    if (!bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(new TrustedTwin { Child = value.Child })))
                        throw new Exception("Untrusted records changed the ordinary wire.");
                    ProofRoot copy = null;
                    if (LuminPackSerializer.Deserialize(bytes, ref copy) != bytes.Length || copy.Child.Number != 17 ||
                        !copy.Child.Flag || copy.Child.Bits.Length != 3 || copy.Child.Bits[1])
                        throw new Exception("Read-site checks changed the original read.");
                    // Root marker, child marker, Number, Flag, Bits count, Bits.
                    foreach (int offset in new[] { 1, 6, 12 })
                    {
                        byte[] forged = (byte[])bytes.Clone();
                        forged[offset] = offset == 1 ? (byte)0 : (byte)2;
                        ProofRoot target = new ProofRoot();
                        bool rejected = false;
                        try { LuminPackSerializer.Deserialize(forged, ref target); }
                        catch (System.IO.InvalidDataException) { rejected = true; }
                        if (!rejected || target.Child != null)
                            throw new Exception("A nested record bypassed its checks or was published after a failure.");
                    }
                    var previous = copy.Child;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!ReferenceEquals(previous, copy.Child)) throw new Exception("The checked child stopped reusing its original instance.");
                    value.Child.Bits = new bool[9];
                    bool oversized = false;
                    try { LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(value), ref copy); }
                    catch (System.IO.InvalidDataException) { oversized = true; }
                    if (!oversized) throw new Exception("The frozen collection limit was bypassed.");
                    value.Child = null;
                    bytes = LuminPackSerializer.Serialize(value);
                    copy = new ProofRoot { Child = new ProofChild() };
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy.Child != null) throw new Exception("A null child retained a previously populated value.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            var original = CSharpSyntaxTree.ParseText(source.Replace("MODE", mode), parse, "InputFacts.cs");
            var input = TestCompilation.Create("InputFacts" + mode, [original], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            VerifyInputFactOutput(ref driver, input, parse, recursive: false);
            var recursive = CSharpSyntaxTree.ParseText(source.Replace("MODE", mode)
                .Replace("Bits = Array.Empty<bool>();", "Bits = Array.Empty<bool>(); public ProofChild Next;"), parse, "InputFacts.cs");
            var changed = input.ReplaceSyntaxTree(original, recursive);
            VerifyInputFactOutput(ref driver, changed, parse, recursive: true);
            VerifyInputFactOutput(ref driver, input, parse, recursive: false);
        }
    }

    /// <summary>Executes an evolved graph and compares its complete retained output with a fresh generator.</summary>
    /// <param name="driver">The retained driver updated by the graph edit.</param>
    /// <param name="input">The finite or recursive input graph.</param>
    /// <param name="parse">The exact source language settings.</param>
    /// <param name="recursive">Whether the graph is recursive and must count its depth.</param>
    private static void VerifyInputFactOutput(ref GeneratorDriver driver, Compilation input, CSharpParseOptions parse, bool recursive)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.ToString())));
        var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        string text = string.Concat(sources.Select(file => file.SourceText.ToString()));
        const string policy = "global::LuminPack.Security.LuminPackInputPolicy.";
        // Boolean domains are checked by the separate or the fused load-and-check kernels.
        bool Has(params string[] calls) => calls.Any(call => text.Contains(policy + call + "(", StringComparison.Ordinal));
        if (text.Contains("LuminPackSafeSchema", StringComparison.Ordinal) || !text.Contains(policy + "ReadNullMarker(ref reader, 1)", StringComparison.Ordinal) ||
            !Has("CheckBoolean", "ReadBoolean", "ReadBatch") || !Has("CheckBooleans", "ReadBooleans") ||
            !text.Contains(", 8)", StringComparison.Ordinal))
            throw new InvalidOperationException("The generated reads lost their frozen read-site checks.");
        if (text.Contains(policy + "EnterDepth(ref reader, 4)", StringComparison.Ordinal) != recursive)
            throw new InvalidOperationException("Depth counting must follow exactly the recursive input graph.");
        GeneratorDriver fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        fresh = fresh.RunGenerators(input);
        var retainedText = sources.Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal);
        var freshText = fresh.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal);
        if (!retainedText.SequenceEqual(freshText)) throw new InvalidOperationException("A retained input graph reused stale checks.");
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        if (!emit.Success) throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
