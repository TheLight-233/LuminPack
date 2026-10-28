using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerCodegenPerformanceRegression
{
    /// <summary>Protects complete ordinary-object and virtual Union payload proofs and their original fallback observations.</summary>
    /// <param name="references">The actual platform and runtime references.</param>
    private static void VerifyProvenObjectWrites(MetadataReference[] references)
    {
        string[] modern = ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"];
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: modern);
            string source = ProvenObjectFixture.Replace("GenerationMode.MODE", "GenerationMode." + mode);
            var input = ProvenInput(source, parse, references);
            var driver = ProvenDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            CheckProvenObjectOutput(output, diagnostics, driver, expected: true);
            const string original = "16059:6C4C154FDD72024226C580221E88B0C7858267D1B1EBC4C358CB0FB924837E0A";
            RequireProven(ExecuteProven(output) == original, "Complete object/Union wire, failure-prefix, cursor, append or profile observations changed from the original generator.");
            string changedSource = source.Replace("public string Text;", "public string Text { get; set; }")
                .Replace("public string Value; }", "public string Value { get; set; } }")
                .Replace("public string OtherText;", "public string OtherText { get; set; }");
            var changed = input.ReplaceSyntaxTree(input.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(changedSource, parse));
            driver = driver.RunGeneratorsAndUpdateCompilation(changed, out output, out diagnostics);
            CheckProvenObjectOutput(output, diagnostics, driver, expected: false);
            RequireProven(CaptureProven(driver) == CaptureProven(ProvenDriver(parse).RunGenerators(changed)), "An object field-to-property edit retained stale proof selection.");
            RequireProven(ExecuteProven(output) == original, "Excluded original object/Union operations differ from the qualified behavior.");
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out output, out diagnostics);
            CheckProvenObjectOutput(output, diagnostics, driver, expected: true);
            RequireProven(CaptureProven(driver) == CaptureProven(ProvenDriver(parse).RunGenerators(input)), "Restored object fields did not recover fresh generation.");
            foreach (string[] symbols in new[] { new[] { "NET5_0_OR_GREATER" }, modern.Append("UNITY_2022_3_OR_NEWER").ToArray() })
            {
                var excludedParse = parse.WithPreprocessorSymbols(symbols);
                var excluded = ProvenInput(source, excludedParse, references);
                var excludedDriver = ProvenDriver(excludedParse).RunGeneratorsAndUpdateCompilation(excluded, out output, out diagnostics);
                CheckProvenObjectOutput(output, diagnostics, excludedDriver, expected: false);
            }
        }
        Console.WriteLine("Passed complete object and virtual Union write proofs: four tiers, raw padding, profiles/getters, original byte/failure-prefix/cursor digest, append scopes, target gates and retained/fresh field edits.");
    }

    /// <summary>Checks compilation and confines the proof to the actual eligible complete operations.</summary>
    /// <param name="output">The completed consumer compilation.</param>
    /// <param name="diagnostics">Generator diagnostics.</param>
    /// <param name="driver">The current complete generator result.</param>
    /// <param name="expected">Whether the fixture's direct fields remain eligible.</param>
    private static void CheckProvenObjectOutput(Compilation output, System.Collections.Generic.IEnumerable<Diagnostic> diagnostics, GeneratorDriver driver, bool expected)
    {
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        RequireProven(errors.Length == 0, string.Join("\n", errors.Select(item => item.ToString())));
        var selected = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Body?.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Any(helper => helper.Identifier.ValueText.StartsWith("__writeOriginalObject", StringComparison.Ordinal)) == true).ToArray();
        string[] owners = selected.Select(method => method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?')).Distinct().Order().ToArray();
        RequireProven(expected ? selected.Length == 5 && owners.SequenceEqual(new[] { "global::Child", "global::Leaf", "global::Unsealed" }) : selected.Length == 0,
            "An excluded contract gained an object proof or an eligible complete operation was lost.");
    }

    private const string ProvenObjectFixture = """
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
        [LuminPackable, LuminPackUnion(7, typeof(Child))] public abstract partial class Parent
        { public int Identity; public string Name; }
        [LuminPackable] public sealed partial class Child : Parent
        { public Padded Raw; public long Number; public string OtherText; }
        public static class Entry
        {
            private static readonly List<byte> Summary = new();
            private static int Cases;
            public static string Check()
            {
                System.Console.WriteLine("PROFILE UTF16 LENGTH"); Profile<LuminPackUtf16LengthOptions>(); System.Console.WriteLine("PROFILE UTF8 LENGTH"); Profile<LuminPackUtf8LengthOptions>();
                System.Console.WriteLine("PROFILE UTF16 TOKEN"); Profile<LuminPackUtf16TokenOptions>(); System.Console.WriteLine("PROFILE UTF8 TOKEN"); Profile<LuminPackUtf8TokenOptions>(); System.Console.WriteLine("PROFILE CUSTOM"); Profile<OtherProfile>();
                // Excluded roots are actual reachable operations, not declarations absent from generation.
                LuminPackSerializer.Serialize(new Properties[1]); LuminPackSerializer.Serialize(new VolatileField[1]);
                LuminPackSerializer.Serialize(new PrivateField[1]); LuminPackSerializer.Serialize(new Callback[1]);
                LuminPackSerializer.Serialize(new Evolving[1]); LuminPackSerializer.Serialize(new Nested[1]); LuminPackSerializer.Serialize(new Unsealed[1]);
                LuminPackSerializer.Serialize(new NullableField[1]); LuminPackSerializer.Serialize(new Guarded[1]);
                ObjectProfile<LuminPackUtf16LengthOptions>(); ObjectProfile<LuminPackUtf8LengthOptions>();
                ObjectProfile<LuminPackUtf16TokenOptions>(); ObjectProfile<LuminPackUtf8TokenOptions>();
                ObjectProfile<LuminPackFixedOptions<LuminPackUtf16LengthOptions>>(); ObjectProfile<LuminPackDefaultOptions>();
                ObjectProfile<OtherProfile>();
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
            private static unsafe void ObjectProfile<P>() where P : struct, ILuminPackOptions
            {
                foreach (int length in new[] { 0, 1, 8, 64, 4097 })
                foreach (bool present in new[] { false, true })
                {
                    OtherProfile.Gets = 0;
                    Parent source = present ? new Child { Identity = length, Name = new string('a', length), Raw = RawBits(length), Number = long.MinValue + length, OtherText = "\u4E2D_tail" } : null;
                    byte[] bytes = LuminPackSerializer.Serialize<P>(source);
                    Summary.AddRange(SHA256.HashData(bytes));
                    var restored = LuminPackSerializer.Deserialize<Parent, P>(bytes);
                    if ((source is null) != (restored is null) || restored is Child actual && (actual.Identity != length || actual.Name != ((Child)source).Name || actual.OtherText != ((Child)source).OtherText))
                        throw new Exception("Union field round trip differs.");
                    var writer = LuminBufferWriterPool.Rent();
                    try
                    {
                        for (int iteration = 0; iteration < 3; iteration++)
                        {
                            LuminPackSerializer.Serialize<P>(source, writer);
                            if (!writer.GetSpan().Slice(iteration * bytes.Length, bytes.Length).SequenceEqual(bytes)) throw new Exception("Union append differs.");
                        }
                    }
                    finally { LuminBufferWriterPool.Return(writer); }
                    if (length <= 64)
                    foreach (int width in Enumerable.Range(0, length * 2 + 110))
                    {
                        byte[] storage = Enumerable.Repeat((byte)0xCC, length * 2 + 200).ToArray();
                        string error = "none"; int cursor;
                        fixed (byte* pointer = storage)
                        {
                            Span<byte> window = new(pointer, width);
                            var state = new LuminPackWriter(ref window);
                            try { LuminPackSerializer.Serialize<P>(ref state, in source); }
                            catch (Exception exception) { error = exception.GetType().FullName; }
                            cursor = state.Position;
                        }
                        Summary.AddRange(System.Text.Encoding.UTF8.GetBytes(error)); Summary.AddRange(BitConverter.GetBytes(cursor));
                        Summary.AddRange(SHA256.HashData(storage)); Cases++;
                    }
                    Summary.AddRange(BitConverter.GetBytes(OtherProfile.Gets)); Cases++;
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
