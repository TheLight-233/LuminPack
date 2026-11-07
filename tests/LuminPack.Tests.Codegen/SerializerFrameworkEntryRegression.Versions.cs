using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerFrameworkEntryRegression
{
    /// <summary>Checks version roots, nested graphs and optional runtime APIs across generation modes and references.</summary>
    /// <param name="references">The framework and current runtime metadata.</param>
    internal static void VerifyVersionPaths(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Text;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public class VersionGraph {public Version Value;public Version[] Items;}
            public static class FrameworkEntry
            {
                public static void Check()
                {
                    var value=new Version(int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue);
                    byte[] root=LuminPackSerializer.Serialize(value);
                    Version result=null;
                    LuminPackSerializer.Deserialize(root,ref result);
                    if(!value.Equals(result)||root.Length!=17)throw new Exception("Version binary root changed.");
                    var graph=new VersionGraph{Value=value,Items=new[]{new Version(1,2),new Version(1,2,3),value,null}};
                    byte[] bytes=LuminPackSerializer.Serialize(graph);
                    VersionGraph restored=null;
                    LuminPackSerializer.Deserialize(bytes,ref restored);
                    if(!LuminPackSerializer.Serialize(restored).AsSpan().SequenceEqual(bytes))throw new Exception("Version binary graph changed.");
                    Json<LuminPackUtf8LengthOptions>(graph,Encoding.UTF8);
                    Json<LuminPackUtf16LengthOptions>(graph,Encoding.Unicode);
                }
                private static void Json<TOptions>(VersionGraph graph,Encoding encoding) where TOptions:struct,ILuminPackOptions
                {
                    string scalar=LuminPackSerializer.SerializeJson<Version, TOptions>(graph.Value);
                    Version value=null;
                    LuminPackSerializer.DeserializeJson<Version, TOptions>(encoding.GetBytes(scalar),ref value);
                    if(!graph.Value.Equals(value)||scalar!="\""+value+"\"")throw new Exception("Version JSON root changed.");
                    string json=LuminPackSerializer.SerializeJson<VersionGraph, TOptions>(graph);
                    VersionGraph target=null;
                    LuminPackSerializer.DeserializeJson<VersionGraph, TOptions>(encoding.GetBytes(json),ref target);
                    var original=target;var component=target.Value;
                    LuminPackSerializer.DeserializeJson<VersionGraph, TOptions>(encoding.GetBytes(json),ref target);
                    if(!ReferenceEquals(original,target)||ReferenceEquals(component,target.Value)||LuminPackSerializer.SerializeJson<VersionGraph, TOptions>(target)!=json)
                        throw new Exception("Version graph reuse changed.");
                }
                public static void NullLiteral(ref LuminPackJsonWriter writer)=>writer.Write<LuminPackUtf8LengthOptions>(null);
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("VersionPaths" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckVersionCalls(driver, true);
            Execute(output);
            if (mode != "Minimal") continue;
            string runtime = typeof(LuminPack.Core.LuminPackJsonWriter).Assembly.Location;
            var original = references.Single(reference => string.Equals(reference.Display, runtime, StringComparison.OrdinalIgnoreCase));
            var missing = MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(runtime, "WriteVersion", "ReadVersion"), filePath: "CoreWithoutVersionText.dll");
            var compatibility = compilation.WithReferences(references.Where(reference => !ReferenceEquals(reference, original)).Append(missing));
            foreach (var target in new[] { compatibility, compilation })
            {
                driver = driver.RunGeneratorsAndUpdateCompilation(target, out output, out diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(target))), "A Version API reference change retained stale output.");
                CheckVersionCalls(driver, ReferenceEquals(target, compilation));
                using var image = new MemoryStream();
                var emitted = output.Emit(image);
                Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            }
        }
        Console.WriteLine("Passed Version binary/JSON roots and graphs, four modes and retained/fresh runtime API changes.");
    }

    /// <summary>Checks calls follow the actual runtime API without depending on forwarding-root retention.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="available">Whether the named version APIs are present.</param>
    private static void CheckVersionCalls(GeneratorDriver driver, bool available)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.Version").ToArray();
        Require(methods.Length != 0, "Version operations were not generated.");
        Require(methods.Any(method => method.ToString().Contains("writer.WriteVersion", StringComparison.Ordinal)) == available, "Version writing selected an unavailable API.");
        Require(methods.Any(method => method.ToString().Contains("reader.ReadVersion", StringComparison.Ordinal)) == available, "Version reading selected an unavailable API.");
        Require(available || methods.Any(method => method.ToString().Contains("global::System.Version.Parse", StringComparison.Ordinal)), "Version compatibility parsing disappeared.");
    }
}
