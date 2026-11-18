extern alias ModelBasedAnalyzer;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Exercises reported model-based generator regressions.</summary>
internal static partial class SerializerIssueRegression
{
    /// <summary>Compiles and executes the reported generic Union cases.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunUnion(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers.Binary;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public abstract partial class ProbeBox<T> { public T V; }
            [LuminPackable] public sealed partial class ProbeBoxA : ProbeBox<int> { }
            [LuminPackable] public sealed partial class ProbeBoxB : ProbeBox<int> { }
            public static class Entry
            {
                public static void Check()
                {
                    ProbeBox<int> value = new ProbeBoxA { V = 42 };
                    var bytes = LuminPackSerializer.Serialize(value);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x6C9033EDu) throw new Exception("First Union hash changed");
                    ProbeBox<int> copy = null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy is not ProbeBoxA { V: 42 }) throw new Exception("Union binary round trip");
                    var existing = copy;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!ReferenceEquals(existing, copy) || copy.V != 42) throw new Exception("Union reuse");
                    value = new ProbeBoxB { V = 73 };
                    bytes = LuminPackSerializer.Serialize(value);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x69902F34u) throw new Exception("Second Union hash changed");
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy is not ProbeBoxB { V: 73 }) throw new Exception("Union case replacement");
                    string json = LuminPackSerializer.SerializeJson(value);
                    copy = null;
                    LuminPackSerializer.DeserializeJson(json, ref copy);
                    if (copy is not ProbeBoxB { V: 73 }) throw new Exception("Union JSON round trip");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
            Execute(references, source.Replace("MODE", mode, StringComparison.Ordinal), "Union" + mode);
        VerifyClosedVirtualUnion(references);
        Console.WriteLine("Passed discovered closed generic Union round trips, fixed wire, reuse, and registered fallback.");
    }

    /// <summary>Exercises a closed binary formatter whose open Union definition exceeds the exact-dispatch tier.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyClosedVirtualUnion(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers.Binary;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            [LuminPackable] public abstract partial class WideBox<T> { public T Value; }
            CASES
            public sealed class External : WideBox<int> { }
            public static class Entry
            {
                public static void Check()
                {
                    LuminPackSerializer.Register<WideBox<int>, External>(77,
                        static (ref LuminPackWriter writer, ref WideBox<int> value) =>
                        {
                            writer.EnsureAdditionalCapacity(5);
                            writer.WriteUnionHeader(77);
                            writer.Write(value.Value);
                        },
                        static (ref LuminPackReader reader, ref WideBox<int> value) =>
                            value = new External { Value = reader.Read<int>() },
                        static (ref LuminPackJsonWriter writer, ref WideBox<int> value) => throw new NotSupportedException(),
                        static (ref LuminPackJsonReader reader, ref WideBox<int> value) => throw new NotSupportedException());

                    WideBox<int>[] input = [new IntCase0 { Value = 41 }, null, new External { Value = 43 }, new IntCase7 { Value = 47 }];
                    byte[] bytes = LuminPackSerializer.Serialize(input);
                    if (bytes.Length != 33 ||
                        !bytes.AsSpan(4, 8).SequenceEqual(LuminPackSerializer.Serialize(input[0])) ||
                        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)) != 0 ||
                        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)) != uint.MaxValue ||
                        bytes[20] != 77)
                        throw new Exception("Closed Union case framing changed.");

                    WideBox<int>[] copy = null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy[0] is not IntCase0 { Value: 41 } || copy[1] != null ||
                        copy[2] is not External { Value: 43 } || copy[3] is not IntCase7 { Value: 47 })
                        throw new Exception("Closed Union case binding changed.");
                    var first = copy[0];
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!ReferenceEquals(first, copy[0]))
                        throw new Exception("Closed Union reuse changed.");

                    // With eight occupied slots, every input hash reaches a generated comparison.
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1u);
                    try
                    {
                        LuminPackSerializer.Deserialize(bytes, ref copy);
                        throw new Exception("An unknown hash reached a Union case.");
                    }
                    catch (Exception error) when (error.Message.Contains("Unknown Union hash"))
                    {
                        if (!ReferenceEquals(first, copy[0]) || copy[0].Value != 41)
                            throw new Exception("Hash rejection changed the existing Union case.");
                    }
                    copy = null;
                    try
                    {
                        LuminPackSerializer.Deserialize(bytes, ref copy);
                        throw new Exception("A fresh Union case accepted a colliding hash.");
                    }
                    catch (Exception error) when (error.Message.Contains("Unknown Union hash"))
                    {
                        if (copy[0] != null)
                            throw new Exception("A colliding hash was published to a fresh Union array.");
                    }
                }
            }
            """;
        var declarations = Enumerable.Range(0, 8).Select(index =>
            $"[LuminPackable] public sealed partial class IntCase{index} : WideBox<int> {{ }}")
            .Concat(Enumerable.Range(0, 25).Select(index =>
                $"[LuminPackable] public sealed partial class LongCase{index} : WideBox<long> {{ }}"));
        Execute(references, source.Replace("CASES", string.Join(Environment.NewLine, declarations), StringComparison.Ordinal),
            "ClosedVirtualUnion", result =>
            {
                string[] sources = result.Results.SelectMany(item => item.GeneratedSources).Select(item => item.SourceText.ToString()).ToArray();
                if (!sources.Any(text => text.Contains("struct WideBoxOfIntFormatter", StringComparison.Ordinal) &&
                        text.Contains("var runtimeType = value.GetType();", StringComparison.Ordinal)) ||
                    !sources.Any(text => text.Contains("WideBox<int>[]", StringComparison.Ordinal) &&
                        text.Contains("unchecked(tag * ", StringComparison.Ordinal) &&
                        text.Contains(">> 29", StringComparison.Ordinal) &&
                        text.Contains("goto default;", StringComparison.Ordinal)))
                    throw new InvalidOperationException("The closed Union owner did not retain the exact write or checked dense read dispatch.");
            });
    }

    /// <summary>Compiles a Next consumer and executes its checks.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="source">Consumer source.</param>
    /// <param name="name">Unique compilation name.</param>
    /// <param name="inspect">An optional check of the generated contract.</param>
    private static void Execute(MetadataReference[] references, string source, string name, Action<GeneratorDriverRunResult>? inspect = null)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var compilation = TestCompilation.Create("IssueRegression" + name, [CSharpSyntaxTree.ParseText(source, parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        var driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        var generated = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        using var image = new MemoryStream();
        var result = output.Emit(image);
        if (!result.Success || diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(result.Diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error)));
        inspect?.Invoke(generated.GetRunResult());
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
