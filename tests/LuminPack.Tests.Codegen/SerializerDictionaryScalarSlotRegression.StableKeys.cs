using System.Reflection;
using System.Runtime.Loader;
using LuminPack.Core;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerDictionaryScalarSlotRegression
{
    /// <summary>Checks complete staged string-key selection, profile ownership, fallback and retained capability invalidation.</summary>
    /// <param name="references">Current runtime and platform metadata.</param>
    private static void VerifyStableStringKeys(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        byte[] runtime = File.ReadAllBytes(typeof(LuminPackReader).Assembly.Location);
        int entry = runtime.AsSpan().IndexOf("TryReadStableStringKeys\0"u8);
        Require(entry >= 0, "The optional stable-key runtime entry is missing.");
        runtime[entry] = (byte)'X';
        var withoutHelper = MetadataReference.CreateFromImage(runtime);
        var original = references.OfType<PortableExecutableReference>().Single(reference => reference.FilePath == typeof(LuminPackReader).Assembly.Location);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            string source = StableFixture.Replace("GenerationMode.MODE", "GenerationMode." + mode);
            var input = StableInput(mode + "StableStringKeys", source, parse, references);
            GeneratorDriver retained = StableDriver(parse);
            retained = retained.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            CheckStableOutput(output, diagnostics, retained, expected: true, execute: true);
            var changed = input.ReplaceSyntaxTree(input.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(source.Replace("using Element = System.Int64;", "using Element = UserScalar;"), parse, "Fixture.cs"));
            retained = retained.RunGeneratorsAndUpdateCompilation(changed, out output, out diagnostics);
            CheckStableOutput(output, diagnostics, retained, expected: false, execute: false);
            Require(StableCapture(StableDriver(parse).RunGenerators(changed)) == StableCapture(retained), "Retained element exclusion differs from fresh output.");
            retained = retained.RunGeneratorsAndUpdateCompilation(input, out output, out diagnostics);
            CheckStableOutput(output, diagnostics, retained, expected: true, execute: false);
            Require(StableCapture(StableDriver(parse).RunGenerators(input)) == StableCapture(retained), "Retained element restoration differs from fresh output.");
            var compatibility = input.ReplaceReference(original, withoutHelper);
            retained = retained.RunGeneratorsAndUpdateCompilation(compatibility, out output, out diagnostics);
            CheckStableOutput(output, diagnostics, retained, expected: false, execute: true);
            Require(StableCapture(StableDriver(parse).RunGenerators(compatibility)) == StableCapture(retained), "Optional API removal differs from fresh output.");
            retained = retained.RunGeneratorsAndUpdateCompilation(input, out output, out diagnostics);
            CheckStableOutput(output, diagnostics, retained, expected: true, execute: false);
            Require(StableCapture(StableDriver(parse).RunGenerators(input)) == StableCapture(retained), "Optional API restoration differs from fresh output.");
            foreach (string[] symbols in new[] { new[] { "NET5_0_OR_GREATER" }, new[] { "NET8_0_OR_GREATER", "UNITY_2022_3_OR_NEWER" } })
            {
                var excludedParse = parse.WithPreprocessorSymbols(symbols);
                var excluded = StableInput(mode + "ExcludedTarget" + symbols.Length, source, excludedParse, references);
                var driver = StableDriver(excludedParse).RunGeneratorsAndUpdateCompilation(excluded, out output, out diagnostics);
                CheckStableOutput(output, diagnostics, driver, expected: false, execute: false);
            }
            var safety = StableInput(mode + "StableInputBoundary", StableSafetyFixture.Replace("GenerationMode.MODE", "GenerationMode." + mode), parse, references);
            var safeDriver = StableDriver(parse).RunGeneratorsAndUpdateCompilation(safety, out output, out diagnostics);
            CheckStableOutput(output, diagnostics, safeDriver, expected: true, execute: true);
        }
        Console.WriteLine("Passed stable string-key refills: four tiers, profiles, fixed overrides, excluded targets/contracts, quotas, staged failure and retained/fresh capability invalidation.");
    }

    /// <summary>Creates one immutable consumer input with the requested actual runtime metadata.</summary>
    /// <param name="name">The isolated consumer assembly name.</param>
    /// <param name="source">The complete consumer source.</param>
    /// <param name="parse">The selected language and target symbols.</param>
    /// <param name="references">The actual platform and runtime metadata.</param>
    /// <returns>The optimized executable consumer compilation.</returns>
    private static CSharpCompilation StableInput(string name, string source, CSharpParseOptions parse, MetadataReference[] references)
        => CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parse, "Fixture.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));

    /// <summary>Creates a fresh complete generator pair.</summary>
    /// <param name="parse">The consumer language and target symbols.</param>
    /// <returns>A fresh driver containing both production generators.</returns>
    private static GeneratorDriver StableDriver(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Captures deterministic source output independent of generated-source enumeration order.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>Sources ordered by their stable hint names.</returns>
    private static string StableCapture(GeneratorDriver driver) => string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .OrderBy(source => source.HintName, StringComparer.Ordinal).Select(source => source.HintName + "\n" + source.SourceText));

    /// <summary>Requires correct selection and generic profile arguments before emitting or executing a consumer.</summary>
    /// <param name="output">The complete generated consumer compilation.</param>
    /// <param name="diagnostics">The generator diagnostics.</param>
    /// <param name="driver">The completed driver supplying operation syntax.</param>
    /// <param name="expected">Whether the optional reusable kernel must be selected.</param>
    /// <param name="execute">Whether to run the complete consumer assertions.</param>
    private static void CheckStableOutput(Compilation output, IEnumerable<Diagnostic> diagnostics, GeneratorDriver driver, bool expected, bool execute)
    {
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.ToString())));
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).SelectMany(source => source.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        int count = 0;
        foreach (var method in methods)
            foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "TryReadStableStringKeys" }))
            {
                if (method.Identifier.ValueText is not "RootRead") throw new InvalidOperationException("Fresh, JSON or writer operation gained stable-key mutation.");
                var arguments = ((GenericNameSyntax)((MemberAccessExpressionSyntax)call.Expression).Name).TypeArgumentList.Arguments;
                if (arguments.Count != 2 || !arguments[1].ToString().StartsWith("__LuminProfile", StringComparison.Ordinal) &&
                    !arguments[1].ToString().Contains("LuminPackFixedOptions")) throw new InvalidOperationException("The selected complete profile was lost.");
                count++;
            }
        if ((count != 0) != expected) throw new InvalidOperationException($"Optional kernel eligibility differs: {count} calls, expected={expected}.");
        using var pe = new MemoryStream();
        var result = output.Emit(pe);
        if (!result.Success) throw new InvalidOperationException(string.Join("\n", result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        if (!execute) return;
        pe.Position = 0;
        var context = new AssemblyLoadContext(output.AssemblyName!, isCollectible: true);
        try { context.LoadFromStream(pe).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null); }
        finally { context.Unload(); }
    }
    private const string StableFixture = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Runtime.InteropServices;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        using Element = System.Int64;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        
        [LuminPackable] public partial class Message
        {
            public Dictionary<string, Element> Values;
            public int Tail;
        }
        
        [LuminPackable] public partial class FixedMessage
        {
            [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.Token)]
            public Dictionary<string, Element> Values;
            public int Tail;
        }
        
        [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
        public partial struct UserScalar { public byte Tag; public int Value; }
        
        public static class Entry
        {
            public static void Check()
            {
                Profile<LuminPackUtf16LengthOptions>(); Profile<LuminPackUtf8LengthOptions>();
                Profile<LuminPackUtf16TokenOptions>(); Profile<LuminPackUtf8TokenOptions>();
                Profile<OtherProfile>();
            }
        
            private static void Profile<P>() where P : struct, ILuminPackOptions
            {
                var keys = new[] { "key_alpha", "key_\u4e16\u754c", "key_\ud83d\ude03", "key_zeta" };
                var source = new Message { Values = keys.ToDictionary(key => key, key => default(Element)), Tail = 53 };
                var target = new Message { Values = keys.ToDictionary(key => key, key => default(Element)) };
                var original = target.Values;
                byte[] bytes = LuminPackSerializer.Serialize<Message, P>(source);
                var fresh = LuminPackSerializer.Deserialize<Message, P>(bytes);
                if (!fresh.Values.Keys.SequenceEqual(keys) || fresh.Tail != 53) throw new Exception("Fresh graph differs.");
                for (int iteration = 0; iteration < 4; iteration++)
                {
                    var enumerator = target.Values.GetEnumerator(); enumerator.MoveNext();
                    if (LuminPackSerializer.Deserialize<Message, P>(bytes, ref target) != bytes.Length ||
                        !ReferenceEquals(target.Values, original) || !target.Values.Keys.SequenceEqual(keys) || target.Tail != 53)
                        throw new Exception("Stable-key generated root differs.");
                    bool invalidated = false;
                    try { enumerator.MoveNext(); } catch (InvalidOperationException) { invalidated = true; }
                    if (!invalidated) throw new Exception("The original Clear/Add version effect was lost.");
                }
                // Every call changes the complete key sequence, without changing the field contract.
                for (int iteration = 0; iteration < 4; iteration++)
                {
                    source.Values = keys.Select(key => key + iteration).ToDictionary(key => key, key => default(Element));
                    bytes = LuminPackSerializer.Serialize<Message, P>(source);
                    LuminPackSerializer.Deserialize<Message, P>(bytes, ref target);
                    if (!target.Values.Keys.SequenceEqual(source.Values.Keys)) throw new Exception("Changed generated keys differ.");
                }
                var fixedSource = new FixedMessage { Values = keys.ToDictionary(key => key, key => default(Element)), Tail = 71 };
                var fixedTarget = new FixedMessage { Values = keys.ToDictionary(key => key, key => default(Element)) };
                byte[] fixedBytes = LuminPackSerializer.Serialize<FixedMessage, P>(fixedSource);
                LuminPackSerializer.Deserialize<FixedMessage, P>(fixedBytes, ref fixedTarget);
                if (!fixedTarget.Values.Keys.SequenceEqual(keys) || fixedTarget.Tail != 71) throw new Exception("A fixed member profile was lost.");
                byte[] mapBytes = LuminPackSerializer.Serialize<Dictionary<string, Element>, P>(source.Values);
                for (int width = 8; width < mapBytes.Length; width++)
                {
                    ReadOnlySpan<byte> input = mapBytes.AsSpan(0, width);
                    var reader = new LuminPack.Core.LuminPackReader(ref input);
                    var map = source.Values.ToDictionary();
                    int version = LuminPack.Code.LuminPackMarshal.GetDictionaryView(map)._version;
                    bool failed = false;
                    try { LuminPackSerializer.Deserialize<Dictionary<string, Element>, P>(ref reader, ref map); }
                    catch { failed = true; }
                    if (!failed || reader.Position != 0 || LuminPack.Code.LuminPackMarshal.GetDictionaryView(map)._version != version ||
                        !map.Keys.SequenceEqual(source.Values.Keys)) throw new Exception("Generated dictionary failure published cursor or staging.");
                }
                string json = LuminPackSerializer.SerializeJson(source);
                if (!LuminPackSerializer.DeserializeJson<Message>(json).Values.Keys.SequenceEqual(source.Values.Keys)) throw new Exception("JSON changed.");
            }
        
            public readonly struct OtherProfile : ILuminPackOptions
            {
                public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
                public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
                public System.Buffers.StandardFormat StandardFormat => new('G');
                public int MaxJsonDepth => 1024;
                public bool ValidateJsonSyntax => false;
            }
        }
        """;

    private const string StableSafetyFixture = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        
        [LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 3)]
        public partial class Message { public Dictionary<string, long> Values; }
        
        public static class Entry
        {
            public static void Check()
            {
                var source = new Message { Values = new Dictionary<string, long> { { "long_key", 17 } } };
                byte[] bytes = LuminPackSerializer.Serialize(source);
                var destination = new Message { Values = new Dictionary<string, long> { { "long_key", 19 } } };
                bool rejected = false;
                try { LuminPackSerializer.Deserialize(bytes, ref destination); } catch (InvalidDataException) { rejected = true; }
                if (!rejected || destination.Values["long_key"] != 19) throw new Exception("Stable keys bypassed the selected string quota or precommit destination.");
                source.Values = new Dictionary<string, long> { { "key", 23 } };
                bytes = LuminPackSerializer.Serialize(source);
                destination = new Message { Values = new Dictionary<string, long> { { "key", 29 } } };
                LuminPackSerializer.Deserialize(bytes, ref destination);
                if (destination.Values["key"] != 23) throw new Exception("The original safe staging path was lost.");
            }
        }
        """;
}
