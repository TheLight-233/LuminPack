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
        Console.WriteLine("Passed discovered closed generic Union case compilation, reuse, and round trips.");
    }

    /// <summary>Compiles a Next consumer and executes its checks.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="source">Consumer source.</param>
    /// <param name="name">Unique compilation name.</param>
    /// <param name="inspect">An optional assertion over the emitted source.</param>
    private static void Execute(MetadataReference[] references, string source, string name, Action<GeneratorDriverRunResult>? inspect = null)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var compilation = TestCompilation.Create("IssueRegression" + name, [CSharpSyntaxTree.ParseText(source, parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        using var image = new MemoryStream();
        var result = output.Emit(image);
        if (!result.Success || diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(result.Diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error)));
        inspect?.Invoke(driver.GetRunResult());
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
