using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerCompiledOptionRegression
{
    /// <summary>Executes only the bounded input proof and reader ownership contracts.</summary>
    /// <param name="references">The actual framework and runtime references.</param>
    internal static void VerifyInputFacts(MetadataReference[] references)
        => VerifyInputFacts(new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]), references);

    /// <summary>Checks finite depth proofs, recursive fallback and retained-driver invalidation in both emission tiers.</summary>
    /// <param name="parse">The supported consuming language and framework.</param>
    /// <param name="references">The current runtime metadata.</param>
    private static void VerifyInputFacts(CSharpParseOptions parse, MetadataReference[] references)
    {
        VerifyFixedInput(parse, references);
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackOption(UntrustedData=true, MaxDepth=4)]
            public class ProofRoot { public ProofChild Child; }
            [LuminPackable]
            public class ProofChild { public int Number; public bool Flag; public bool[] Bits = Array.Empty<bool>(); }
            public static class Entry
            {
                public static void Check()
                {
                    var value = new ProofRoot { Child = new ProofChild { Number=17, Flag=true, Bits=new[] { true, false, true } } };
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    ProofRoot copy = null;
                    if (LuminPackSerializer.Deserialize(bytes, ref copy) != bytes.Length || copy.Child.Number != 17 ||
                        !copy.Child.Flag || copy.Child.Bits.Length != 3 || copy.Child.Bits[1])
                        throw new Exception("Frozen input facts changed the original read.");
                    foreach (int offset in new[] { 57, 62 })
                    {
                        byte[] forged = (byte[])bytes.Clone();
                        forged[offset] = offset == 57 ? (byte)0 : (byte)2;
                        ProofRoot target = new ProofRoot();
                        bool rejected = false;
                        try { LuminPackSerializer.Deserialize(forged, ref target); }
                        catch (System.IO.InvalidDataException) { rejected = true; }
                        if (!rejected || target.Child != null)
                            throw new Exception("An inline child bypassed typed validation before publication.");
                    }
                    var previous = copy.Child;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!ReferenceEquals(previous, copy.Child)) throw new Exception("The guarded child stopped reusing its original instance.");
                    value.Child = null;
                    bytes = LuminPackSerializer.Serialize(value);
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
            VerifyInputFactOutput(ref driver, input, parse, 4);
            var recursive = CSharpSyntaxTree.ParseText(source.Replace("MODE", mode)
                .Replace("Bits = Array.Empty<bool>();", "Bits = Array.Empty<bool>(); public ProofChild Next;"), parse, "InputFacts.cs");
            var changed = input.ReplaceSyntaxTree(original, recursive);
            VerifyInputFactOutput(ref driver, changed, parse, 0);
            VerifyInputFactOutput(ref driver, input, parse, 4);
        }
    }

    /// <summary>Executes an evolved graph and compares its complete retained output with a fresh generator.</summary>
    /// <param name="driver">The retained driver updated by the graph edit.</param>
    /// <param name="input">The finite or recursive input graph.</param>
    /// <param name="parse">The exact source language settings.</param>
    /// <param name="expectedDepth">The root's finite bound or recursive sentinel.</param>
    private static void VerifyInputFactOutput(ref GeneratorDriver driver, Compilation input, CSharpParseOptions parse, int expectedDepth)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.ToString())));
        var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        var schemas = sources.SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, parse).GetRoot()
            .DescendantNodes().OfType<ObjectCreationExpressionSyntax>()).Where(node => node.Type.ToString() == "global::LuminPack.Security.LuminPackSafeSchema" &&
                node.ArgumentList!.Arguments[0].Expression is LiteralExpressionSyntax literal &&
                literal.Token.ValueText.StartsWith("LuminPack.Safe.2|0:global::ProofRoot|", StringComparison.Ordinal)).ToArray();
        if (schemas.Length == 0 || schemas.Any(schema => schema.ArgumentList!.Arguments.Single(argument => argument.NameColon?.Name.Identifier.ValueText == "maxSchemaDepth")
                .Expression.ToString() != expectedDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            throw new InvalidOperationException("The generated finite-depth proof did not track the closed input graph.");
        GeneratorDriver fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        fresh = fresh.RunGenerators(input);
        var retainedText = sources.Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal);
        var freshText = fresh.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal);
        if (!retainedText.SequenceEqual(freshText)) throw new InvalidOperationException("A retained input graph reused stale validation facts.");
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        if (!emit.Success) throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
