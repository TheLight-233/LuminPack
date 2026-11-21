extern alias ModelBasedAnalyzer;

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Verifies generated binary string pairs and their sequential read boundaries.</summary>
internal static class SerializerBinaryStringPairRegression
{
    /// <summary>Checks grouping with the current Core and, when provided, fallback binding against an older Core.</summary>
    /// <param name="references">The runtime and framework references.</param>
    /// <param name="olderCore">Optional metadata for the same runtime before the paired reader existed.</param>
    internal static void Run(MetadataReference[] references, string? olderCore)
    {
        Verify(references, expectPairs: true);
        if (olderCore != null)
        {
            var previous = references.Select(reference => Path.GetFileName(reference.Display) == "LuminPack.Core.dll"
                ? MetadataReference.CreateFromFile(olderCore) : reference).ToArray();
            Verify(previous, expectPairs: false);
        }
        Console.WriteLine(olderCore == null ? "Verified binary string pair generation and read boundaries."
            : "Verified binary string pair generation, read boundaries, and older Core compatibility.");
    }

    /// <summary>Compiles representative field shapes and checks the emitted calls against the referenced Core.</summary>
    /// <param name="references">The runtime and serializer assemblies for the test compilation.</param>
    /// <param name="expectPairs">Whether the selected Core exposes the paired string reader.</param>
    private static void Verify(MetadataReference[] references, bool expectPairs)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview,
            preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER"])
            .WithFeatures([new("InterceptorsNamespaces", "LuminPack")]);
        var sourceTree = CSharpSyntaxTree.ParseText(Source, parse, "StringPairs.cs");
        var input = CSharpCompilation.Create("BinaryStringPairs" + (expectPairs ? "Current" : "Previous"),
            [sourceTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        if (input.GetTypeByMetadataName("LuminPack.Attribute.LuminPackOptionAttribute")?.GetMembers("GeneratorType").IsEmpty == true)
        {
            string legacySource = Source
                .Replace("LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)",
                    "LuminPackable(GeneratorType.CircleReference)", StringComparison.Ordinal)
                .Replace("LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)",
                    "LuminPackable(GeneratorType.VersionTolerant)", StringComparison.Ordinal);
            input = input.ReplaceSyntaxTree(sourceTree, CSharpSyntaxTree.ParseText(legacySource, parse, sourceTree.FilePath));
        }
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        string generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.GetText().ToString()));
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        if (!emit.Success)
            throw new InvalidOperationException(string.Join("\n", emit.Diagnostics.Concat(diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error)));
        if (!expectPairs)
        {
            if (generated.Contains(".ReadStringPair<", StringComparison.Ordinal))
                throw new InvalidOperationException("The older Core acquired an unsupported pair call.");
            return;
        }

        var pairs = Regex.Matches(generated, @"\.ReadStringPair<[^>]+>\(ref \w+\.(\w+), ref \w+\.(\w+)\)")
            .Select(match => (match.Groups[1].Value, match.Groups[2].Value)).ToArray();
        if (!pairs.Contains(("A", "B")) || !pairs.Contains(("C", "D")) ||
            pairs.Any(pair => pair != ("A", "B") && pair != ("C", "D") && pair != ("Left", "Right")))
            throw new InvalidOperationException($"Unexpected grouping across roots, list elements, or excluded records: {string.Join(", ", pairs)}");
        var types = driver.GetRunResult().GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>());
        if (!types.Any(type => type.Identifier.ValueText.StartsWith("ListOf", StringComparison.Ordinal) &&
            Regex.IsMatch(type.ToString(), @"\.ReadStringPair<[^>]+>\(ref \w+\.Left, ref \w+\.Right\)")))
            throw new InvalidOperationException("The concrete list reader did not group its element's consecutive fields.");
        Assembly.Load(image.ToArray()).GetType("Cases")!.GetMethod("Verify")!.Invoke(null, null);
    }

    private const string Source = """
        using System;
        using System.Buffers.Binary;
        using System.Collections.Generic;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;

        [LuminPackable] public sealed class PairRecord
        {
            public string A; public string B; public string C; public string D; public string E;
        }
        [LuminPackable] public sealed class PairItem
        {
            public int Index; public string Left; public string Right;
            public int Quantity; public double Price; public bool Paid; public byte Status;
        }
        [LuminPackable] public sealed class PairEnvelope { public List<PairItem> Items; }
        [LuminPackable] public sealed class SplitRecord { public string A1; public int Gap; public string B1; }
        [LuminPackable] public sealed class CallbackRecord
        {
            public string A2; public string B2;
            [LuminPackOnDeserialized] public void After() { }
        }
        [LuminPackable] public sealed class MirroredRecord { public string A3 { get; set; } public string B3; }
        [LuminPackable] public sealed class VolatileRecord { public volatile string A4; public string B4; }
        [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)] public sealed class VersionRecord { public string A5; public string B5; }
        [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)] public sealed class TrackedRecord
        {
            [LuminPackOrder(0)] public string A6; [LuminPackOrder(1)] public string B6;
        }
        [LuminPackable, LuminPackUnion(1, typeof(UnionCase))] public partial interface IUnionRecord { }
        [LuminPackable] public sealed partial class UnionCase : IUnionRecord { public string A7; public string B7; }
        [LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 3)]
        public sealed class BoundedRecord { public string A8; public string B8; }
        [LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]
        public sealed class Utf8Record { public string A9; public string B9; }

        public static class Cases
        {
            public static void Verify()
            {
                var value = new PairRecord { A = "eight888", B = "nine99999", C = "third", D = "fourth", E = new string('x', 72) };
                byte[] bytes = LuminPackSerializer.Serialize(value);
                var current = new PairRecord { A = new string(value.A.ToCharArray()), B = "old", C = "old", D = "old", E = "old" };
                string retained = current.A;
                if (LuminPackSerializer.Deserialize(bytes, ref current) != bytes.Length || !ReferenceEquals(retained, current.A) ||
                    current.B != value.B || current.C != value.C || current.D != value.D || current.E != value.E)
                    throw new Exception("Paired read changed a field, lost retained text, or consumed the wrong length.");
                var list = new List<PairRecord> { value, new() { A = "", B = null, C = "\u4E2D", D = new string('y', 25), E = "tail" } };
                byte[] listBytes = LuminPackSerializer.Serialize(list);
                var copied = LuminPackSerializer.Deserialize<List<PairRecord>>(listBytes);
                if (copied.Count != 2 || copied[0].D != "fourth" || copied[1].A != "" || copied[1].B != null ||
                    copied[1].C != "\u4E2D" || copied[1].D != new string('y', 25) || copied[1].E != "tail")
                    throw new Exception("List element pairing changed mixed-width or null fields.");
                var items = new List<PairItem> { new() { Index = 1, Left = "left-hand", Right = "right-hand" },
                    new() { Index = 2, Left = "second-left", Right = "second-right" } };
                byte[] itemBytes = LuminPackSerializer.Serialize(new PairEnvelope { Items = items });
                var itemCopies = LuminPackSerializer.Deserialize<PairEnvelope>(itemBytes).Items;
                if (itemCopies.Count != 2 || itemCopies[0].Right != "right-hand" || itemCopies[1].Left != "second-left")
                    throw new Exception("The concrete list reader changed an element's paired fields.");

                ReadOnlySpan<byte> complete = bytes;
                var sequential = new LuminPackReader(ref complete);
                _ = sequential.Read<byte>();
                _ = sequential.Read<LuminPackUtf16LengthOptions>((string)null);
                int secondStart = sequential.Position;
                var corrupt = (byte[])bytes.Clone();
                BinaryPrimitives.WriteInt32LittleEndian(corrupt.AsSpan(secondStart), int.MaxValue);
                CheckFailure(corrupt);
                CheckFailure(bytes.AsSpan(0, secondStart + 2));

                void CheckFailure(ReadOnlySpan<byte> input)
                {
                    var reader = new LuminPackReader(ref input);
                    var reuse = new PairRecord { A = "old A", B = "old B", C = "old C" };
                    Exception error = null;
                    try { LuminPackSerializer.Deserialize(ref reader, ref reuse); }
                    catch (Exception exception) { error = exception; }
                    if (error == null || reader.Position != secondStart || reuse.A != value.A || reuse.B != "old B" || reuse.C != "old C")
                        throw new Exception("A failed second field changed the cursor or publication boundary.");
                }
            }
        }
        """;
}
