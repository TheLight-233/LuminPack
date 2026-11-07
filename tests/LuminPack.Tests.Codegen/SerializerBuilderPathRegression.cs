using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks builder binary/JSON graphs and optional runtime snapshot selection together.</summary>
internal static class SerializerBuilderPathRegression
{
    /// <summary>Executes all generation tiers and checks retained output across runtime capability changes.</summary>
    /// <param name="references">The current framework and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        SerializerCollectionBuilderRegression.Run(references);
        const string source = """
            using System;
            using System.Text;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public class BuilderGraph { public StringBuilder Text; public StringBuilder[] Items; }
            public static class BuilderCases
            {
                public static void Check()
                {
                    var text = new StringBuilder(1);
                    for(int i=0;i<8192;i++) text.Append("x\u00e9\u4e2d\"\\\n"[i%6]);
                    var graph = new BuilderGraph { Text=text, Items=new[]{text,new StringBuilder(),null} };
                    byte[] scalar=LuminPackSerializer.Serialize(text);
                    StringBuilder scalarCopy=null;
                    LuminPackSerializer.Deserialize(scalar,ref scalarCopy);
                    if(scalarCopy.ToString()!=text.ToString())throw new Exception("Scalar binary bytes changed.");
                    byte[] binary=LuminPackSerializer.Serialize(graph);
                    BuilderGraph restored=null;
                    LuminPackSerializer.Deserialize(binary,ref restored);
                    var target=restored.Text;
                    LuminPackSerializer.Deserialize(binary,ref restored);
                    if(!ReferenceEquals(target,restored.Text)||restored.Text.ToString()!=text.ToString()||restored.Items[2]!=null)throw new Exception("Nested binary reuse changed.");
                    Json<LuminPackUtf8LengthOptions>(graph,Encoding.UTF8);
                    Json<LuminPackUtf16LengthOptions>(graph,Encoding.Unicode);
                }
                private static void Json<TOptions>(BuilderGraph graph,Encoding encoding) where TOptions:struct,ILuminPackOptions
                {
                    string scalar=LuminPackSerializer.SerializeJson<StringBuilder, TOptions>(graph.Text);
                    StringBuilder scalarCopy=null;
                    LuminPackSerializer.DeserializeJson<StringBuilder, TOptions>(encoding.GetBytes(scalar),ref scalarCopy);
                    if(scalarCopy.ToString()!=graph.Text.ToString())throw new Exception("Scalar JSON changed.");
                    string json=LuminPackSerializer.SerializeJson<BuilderGraph, TOptions>(graph);
                    BuilderGraph restored=null;
                    LuminPackSerializer.DeserializeJson<BuilderGraph, TOptions>(encoding.GetBytes(json),ref restored);
                    var target=restored.Text;
                    LuminPackSerializer.DeserializeJson<BuilderGraph, TOptions>(encoding.GetBytes(json),ref restored);
                    if(!ReferenceEquals(target,restored.Text)||restored.Text.ToString()!=graph.Text.ToString()||restored.Items[2]!=null)throw new Exception("Nested JSON reuse changed.");
                }
                public static void NullLiteralStillBinds(ref LuminPackJsonWriter writer) => writer.Write<LuminPackUtf8LengthOptions>(null);
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("BuilderPaths" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckPaths(driver, true);
            byte[] image = Emit(output);
            Assembly.Load(image).GetType("BuilderCases")!.GetMethod("Check")!.Invoke(null, null);
            if (mode == "Minimal") CheckReferenceChanges(references, compilation, parse, driver);
        }
        var fallback = new System.Text.StringBuilder();
        StringBuilderEmitterFallback(fallback);
        Require(fallback.ToString().Contains("GetChunks", StringComparison.Ordinal) && fallback.ToString().Contains("value.ToString()", StringComparison.Ordinal),
            "The unavailable span-copy capability lost its established target fallbacks.");
        Console.WriteLine("Passed builder binary/JSON roots and graphs, four tiers, null overload compatibility and retained/fresh runtime API changes.");
    }

    /// <summary>Exercises the preserved source fallback without assuming every supported framework has the span API.</summary>
    /// <param name="output">The emitted fallback source.</param>
    private static void StringBuilderEmitterFallback(System.Text.StringBuilder output) =>
        LuminPack.SourceGenerator.CodeEmitters.StringBuilderEmitter.GenerateSerializeCode(new LuminPack.Code.LuminLocalFieldData(), output);

    /// <summary>Checks modern and compatibility references invalidate optional calls in both directions.</summary>
    /// <param name="references">The modern references.</param>
    /// <param name="compilation">The existing consumer.</param>
    /// <param name="parse">The syntax settings.</param>
    /// <param name="driver">The retained modern driver.</param>
    private static void CheckReferenceChanges(MetadataReference[] references, CSharpCompilation compilation, CSharpParseOptions parse, GeneratorDriver driver)
    {
        string runtime = typeof(LuminPack.Core.LuminPackJsonWriter).Assembly.Location;
        var original = references.Single(reference => string.Equals(reference.Display, runtime, StringComparison.OrdinalIgnoreCase));
        var oldApi = MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(runtime, "WriteStringBuilder", "ReadStringBuilder"), filePath: "CoreWithoutBuilderSnapshot.dll");
        var compatibility = compilation.WithReferences(references.Where(reference => !ReferenceEquals(reference, original)).Append(oldApi));
        foreach (var target in new[] { compatibility, compilation })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(target, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(target))), "Changing the builder runtime capability retained stale generated output.");
            CheckPaths(driver, ReferenceEquals(target, compilation));
            Emit(output);
        }
    }

    /// <summary>Checks binary writes and JSON operations select their actual available capabilities.</summary>
    /// <param name="driver">The completed generator.</param>
    /// <param name="snapshot">Whether the runtime snapshot API is available.</param>
    private static void CheckPaths(GeneratorDriver driver, bool snapshot)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.Text.StringBuilder").ToArray();
        var writes = methods.Where(method => method.Identifier.ValueText == "RootWrite").ToArray();
        Require(writes.Length != 0 && writes.All(method => method.ToString().Contains(".CopyTo(", StringComparison.Ordinal) && !method.ToString().Contains("GetChunks", StringComparison.Ordinal)), "Builder binary copies lost their direct reserved destination.");
        var json = methods.Where(method => method.Identifier.ValueText == "RootWriteJson").ToArray();
        Require(json.Length != 0 && json.All(method => method.ToString().Contains("WriteStringBuilder", StringComparison.Ordinal) == snapshot), "Builder JSON writes selected an unavailable runtime API.");
        var reads = methods.Where(method => method.Identifier.ValueText is "RootReadJson" or "DeserializeJson").ToArray();
        Require(reads.Length != 0 && reads.Any(method => method.ToString().Contains("ReadStringBuilder", StringComparison.Ordinal)) == snapshot, "Builder JSON reads selected an unavailable runtime API.");
        Require(snapshot || reads.Any(method => method.Identifier.ValueText == "RootReadJson" && method.ToString().Contains("new global::System.Text.StringBuilder", StringComparison.Ordinal)), "Builder JSON reads lost their compatibility implementation.");
    }

    /// <summary>Creates an independent incremental driver.</summary>
    /// <param name="parse">The syntax settings.</param>
    /// <returns>A fresh driver.</returns>
    private static GeneratorDriver Create(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Compiles the generated consumer and returns its complete image.</summary>
    /// <param name="output">The generated compilation.</param>
    /// <returns>The compiled image.</returns>
    private static byte[] Emit(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        return image.ToArray();
    }

    /// <summary>Captures exact generated identities and source text.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The sorted source snapshot.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a failed focused invariant.</summary>
    /// <param name="condition">Whether the invariant holds.</param>
    /// <param name="message">The failed invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
