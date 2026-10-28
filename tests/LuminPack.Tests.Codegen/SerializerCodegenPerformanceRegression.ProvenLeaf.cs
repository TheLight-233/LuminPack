using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerCodegenPerformanceRegression
{
    /// <summary>Checks pure array-record capacity proofs against the same complete excluded-class traversal.</summary>
    /// <param name="references">The actual platform and runtime metadata.</param>
    private static void VerifyProvenLeafWrites(MetadataReference[] references)
    {
        string[] modern = ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"];
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: modern);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            string source = ProvenLeafFixture.Replace("GenerationMode.MODE", "GenerationMode." + mode);
            var input = ProvenInput(source, parse, references);
            GeneratorDriver driver = ProvenDriver(parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            CheckProvenOutput(output, diagnostics, driver, expected: true);
            string actual = ExecuteProven(output);
            var fallbackInput = input.ReplaceSyntaxTree(input.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(
                source.Replace("sealed partial class Leaf", "partial class Leaf"), parse));
            driver = driver.RunGeneratorsAndUpdateCompilation(fallbackInput, out output, out diagnostics);
            CheckProvenOutput(output, diagnostics, driver, expected: false);
            RequireProven(actual == ExecuteProven(output), "The complete original and proven operations differ in wire, failure prefix, cursor, append or profile observations.");
            RequireProven(CaptureProven(driver) == CaptureProven(ProvenDriver(parse).RunGenerators(fallbackInput)), "Retained class exclusion differs from fresh generation.");
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out output, out diagnostics);
            CheckProvenOutput(output, diagnostics, driver, expected: true);
            RequireProven(CaptureProven(driver) == CaptureProven(ProvenDriver(parse).RunGenerators(input)), "Restored class eligibility differs from fresh generation.");
            var changedInput = input.ReplaceSyntaxTree(input.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(
                source.Replace("public string Text;", "public string Text { get; set; }"), parse));
            driver = driver.RunGeneratorsAndUpdateCompilation(changedInput, out output, out diagnostics);
            CheckProvenOutput(output, diagnostics, driver, expected: false);
            RequireProven(CaptureProven(driver) == CaptureProven(ProvenDriver(parse).RunGenerators(changedInput)), "A property edit retained a stale capacity proof.");
            foreach (string[] symbols in new[] { new[] { "NET5_0_OR_GREATER" }, modern.Append("UNITY_2022_3_OR_NEWER").ToArray() })
            {
                var excludedParse = parse.WithPreprocessorSymbols(symbols);
                var excluded = ProvenInput(source, excludedParse, references);
                var excludedDriver = ProvenDriver(excludedParse).RunGeneratorsAndUpdateCompilation(excluded, out output, out diagnostics);
                CheckProvenOutput(output, diagnostics, excludedDriver, expected: false);
            }
        }
        Console.WriteLine("Passed complete leaf-array write proofs: four tiers, profiles and getters, field-order bytes, short-window failure prefixes, append scopes, target exclusions and retained/fresh contract edits.");
    }

    /// <summary>Creates an executable consumer with actual canonical model symbols.</summary>
    /// <param name="source">The complete model and runtime assertions.</param>
    /// <param name="parse">The selected language and target symbols.</param>
    /// <param name="references">The actual platform and runtime metadata.</param>
    /// <returns>The complete optimized consumer input.</returns>
    private static CSharpCompilation ProvenInput(string source, CSharpParseOptions parse, MetadataReference[] references)
        => CSharpCompilation.Create("ProvenLeaf" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.ParseText(source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));

    /// <summary>Creates both production generators under the same consumer parse policy.</summary>
    /// <param name="parse">The consumer parse policy.</param>
    /// <returns>A fresh independent complete driver.</returns>
    private static GeneratorDriver ProvenDriver(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Requires complete compilation and restricts proof selection to the certified leaf-array root.</summary>
    /// <param name="output">The complete generated compilation.</param>
    /// <param name="diagnostics">Generator diagnostics.</param>
    /// <param name="driver">The completed driver and its operation syntax.</param>
    /// <param name="expected">Whether the original leaf remains eligible.</param>
    private static void CheckProvenOutput(Compilation output, IEnumerable<Diagnostic> diagnostics, GeneratorDriver driver, bool expected)
    {
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        RequireProven(errors.Length == 0, string.Join("\n", errors.Select(item => item.ToString())));
        var selected = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Body?.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Any(helper => helper.Identifier.ValueText.StartsWith("__writeOriginalLeaf", StringComparison.Ordinal)) == true).ToArray();
        RequireProven(expected ? selected.Length == 1 && selected[0].Identifier.ValueText == "RootWrite" && selected[0].ParameterList.Parameters[1].Type!.ToString().Contains("Leaf[]") : selected.Length == 0,
            "Incorrect pure/excluded operation selection: " + string.Join(",", selected.Select(method => method.ParameterList.ToString())));
    }

    /// <summary>Compares complete retained and fresh output independently of source enumeration order.</summary>
    /// <param name="driver">A completed driver.</param>
    /// <returns>Stable hint names and complete generated sources.</returns>
    private static string CaptureProven(GeneratorDriver driver) => string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .OrderBy(source => source.HintName, StringComparer.Ordinal).Select(source => source.HintName + "\n" + source.SourceText));

    /// <summary>Executes the complete wire, cursor, failure-prefix, configuration and append matrix.</summary>
    /// <param name="compilation">The successful generated consumer.</param>
    /// <returns>The complete deterministic behavior digest.</returns>
    private static string ExecuteProven(Compilation compilation)
    {
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        RequireProven(emitted.Success, string.Join("\n", emitted.Diagnostics));
        image.Position = 0;
        var context = new AssemblyLoadContext(compilation.AssemblyName!, isCollectible: true);
        try { return (string)context.LoadFromStream(image).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)!; }
        finally { context.Unload(); }
    }

    /// <summary>Reports a violated complete-operation or selection requirement.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The concrete failure.</param>
    private static void RequireProven(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private const string ProvenLeafFixture = """
        using System;
        using System.Buffers;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Runtime.CompilerServices;
        using System.Runtime.InteropServices;
        using System.Security.Cryptography;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        
        [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 16)] public struct Padded
        {
            [FieldOffset(0)] public byte Tag;
            [FieldOffset(8)] public long Value;
        }
        [LuminPackable] public sealed partial class Leaf
        {
            public byte Head;
            public long Number;
            public Padded Raw;
            public string Text;
            public int __leafRecordBound;
            public string Other;
            public bool Flag;
        }
        [LuminPackable] public sealed partial class Properties { public string Value { get; set; } }
        [LuminPackable] public sealed partial class VolatileField { public string Value; public volatile int Flag; }
        [LuminPackable] public sealed partial class PrivateField { private string value; public PrivateField() { value = "x"; } }
        [LuminPackable] public sealed partial class Callback
        {
            public string Value;
            [LuminPackOnSerializing] public void Before() { Value += "x"; }
        }
        [LuminPackable(GeneratorType.VersionTolerant)] public sealed partial class Evolving { public string Value; }
        [LuminPackable] public sealed partial class Nested { public string Value; public Leaf Child; }
        [LuminPackable] public sealed partial class NullableField { public string Value; public int? Optional; }
        [LuminPackable, LuminPackOption(UntrustedData = true)] public sealed partial class Guarded { public string Value; }
        [LuminPackable] public partial class Unsealed { public string Value; }
        [LuminPackable] public sealed partial class Holder { public Leaf[] Values; public int Tail; }
        public readonly struct OtherProfile : ILuminPackOptions
        {
            public static int Gets;
            public LuminPackStringEncoding StringEncoding { get { Gets++; return LuminPackStringEncoding.UTF16; } }
            public LuminPackStringRecording StringRecording { get { Gets++; return LuminPackStringRecording.Length; } }
            public StandardFormat StandardFormat => new('G');
            public int MaxJsonDepth => 1024;
            public bool ValidateJsonSyntax => false;
        }
        public static class Entry
        {
            private static readonly List<byte> Summary = new();
            private static int Cases;
            public static string Check()
            {
                Profile<LuminPackUtf16LengthOptions>(); Profile<LuminPackUtf8LengthOptions>();
                Profile<LuminPackUtf16TokenOptions>(); Profile<LuminPackUtf8TokenOptions>(); Profile<OtherProfile>();
                // Excluded roots are actual reachable operations, not declarations absent from generation.
                LuminPackSerializer.Serialize(new Properties[1]); LuminPackSerializer.Serialize(new VolatileField[1]);
                LuminPackSerializer.Serialize(new PrivateField[1]); LuminPackSerializer.Serialize(new Callback[1]);
                LuminPackSerializer.Serialize(new Evolving[1]); LuminPackSerializer.Serialize(new Nested[1]); LuminPackSerializer.Serialize(new Unsealed[1]);
                LuminPackSerializer.Serialize(new NullableField[1]); LuminPackSerializer.Serialize(new Guarded[1]);
                return Cases + ":" + Convert.ToHexString(SHA256.HashData(Summary.ToArray()));
            }
            private static unsafe void Profile<P>() where P : struct, ILuminPackOptions
            {
                foreach (int count in new[] { 0, 1, 2, 8, 33, 128 })
                foreach (int length in new[] { 0, 1, 3, 8, 17, 64, 257, 4097 })
                {
                    OtherProfile.Gets = 0;
                    var source = Enumerable.Range(0, count).Select(index => index % 7 == 6 ? null : new Leaf
                    {
                        Head = (byte)index, Number = long.MinValue + index, Raw = RawBits(index), Text = new string((index & 1) == 0 ? 'a' : '\u4E2D', length),
                        Other = index % 3 == 0 ? null : "tail_" + index, Flag = (index & 1) != 0, __leafRecordBound = 71 + index
                    }).ToArray();
                    byte[] bytes = LuminPackSerializer.Serialize<P>(source);
                    var expected = new List<byte>(BitConverter.GetBytes(count));
                    foreach (var value in source) expected.AddRange(LuminPackSerializer.Serialize<P>(value));
                    if (!bytes.AsSpan().SequenceEqual(expected.ToArray())) throw new Exception("Field-ordered record bytes differ.");
                    Summary.AddRange(SHA256.HashData(bytes));
                    var fresh = LuminPackSerializer.Deserialize<Leaf[], P>(bytes);
                    if (fresh.Length != count) throw new Exception("Fresh count differs.");
                    var writer = LuminBufferWriterPool.Rent();
                    try
                    {
                        for (int iteration = 0; iteration < 3; iteration++)
                        {
                            LuminPackSerializer.Serialize<P>(source, writer);
                            byte[] actual = writer.GetSpan().ToArray();
                            if (!actual.AsSpan(iteration * bytes.Length, bytes.Length).SequenceEqual(bytes)) throw new Exception("Append output differs.");
                        }
                    }
                    finally { LuminBufferWriterPool.Return(writer); }
                    Cases++;
                    if (count <= 2 && length <= 17)
                    {
                        int bound = count * (length * 2 + 80) + 8;
                        foreach (int width in Enumerable.Range(0, bound + 1))
                        {
                            byte[] storage = Enumerable.Repeat((byte)0xCC, bound + 64).ToArray();
                            string error = "none";
                            int cursor;
                            fixed (byte* pinned = storage)
                            {
                                Span<byte> window = new(pinned, width);
                                var state = new LuminPackWriter(ref window);
                                try { LuminPackSerializer.Serialize<P>(ref state, in source); }
                                catch (Exception exception) { error = exception.GetType().FullName; }
                                cursor = state.Position;
                            }
                            Summary.AddRange(System.Text.Encoding.UTF8.GetBytes(error)); Summary.AddRange(BitConverter.GetBytes(cursor));
                            Summary.AddRange(SHA256.HashData(storage)); Cases++;
                        }
                    }
                    Summary.AddRange(BitConverter.GetBytes(OtherProfile.Gets));
                }
            }
            private static Padded RawBits(int seed)
            {
                Padded value = default;
                ref byte first = ref Unsafe.As<Padded, byte>(ref value);
                for (int index = 0; index < Unsafe.SizeOf<Padded>(); index++) Unsafe.Add(ref first, index) = (byte)(seed * 17 + index);
                return value;
            }
        }
        """;
}
