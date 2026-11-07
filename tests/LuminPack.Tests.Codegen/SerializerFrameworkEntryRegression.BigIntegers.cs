using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerFrameworkEntryRegression
{
    /// <summary>Checks integer roots, nested graphs and optional runtime APIs across generation modes and references.</summary>
    /// <param name="references">The framework and current runtime metadata.</param>
    internal static void VerifyBigIntegerPaths(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Numerics;
            using System.Text;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public class BigIntegerGraph {public BigInteger Value;public BigInteger? Maybe;public BigInteger[] Items;}
            public static class FrameworkEntry
            {
                public static void Check()
                {
                    var value=-(BigInteger.One<<8192)+17;
                    byte[] root=LuminPackSerializer.Serialize(value);
                    BigInteger result=default;
                    LuminPackSerializer.Deserialize(root,ref result);
                    if(value!=result||!root.AsSpan(4).SequenceEqual(value.ToByteArray()))throw new Exception("BigInteger binary root changed.");
                    var graph=new BigIntegerGraph{Value=value,Maybe=null,Items=new[]{BigInteger.Zero,BigInteger.One,-value,value}};
                    byte[] bytes=LuminPackSerializer.Serialize(graph);
                    BigIntegerGraph restored=null;
                    LuminPackSerializer.Deserialize(bytes,ref restored);
                    if(!LuminPackSerializer.Serialize(restored).AsSpan().SequenceEqual(bytes))throw new Exception("BigInteger binary graph changed.");
                    Json<LuminPackUtf8LengthOptions>(graph,Encoding.UTF8);
                    graph.Maybe=value;
                    Json<LuminPackUtf16LengthOptions>(graph,Encoding.Unicode);
                }
                private static void Json<TOptions>(BigIntegerGraph graph,Encoding encoding) where TOptions:struct,ILuminPackOptions
                {
                    string scalar=LuminPackSerializer.SerializeJson<BigInteger, TOptions>(graph.Value);
                    BigInteger value=default;
                    LuminPackSerializer.DeserializeJson<BigInteger, TOptions>(encoding.GetBytes(scalar),ref value);
                    if(graph.Value!=value||scalar!="\""+value.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\"")throw new Exception("BigInteger JSON root changed.");
                    string json=LuminPackSerializer.SerializeJson<BigIntegerGraph, TOptions>(graph);
                    BigIntegerGraph target=null;
                    LuminPackSerializer.DeserializeJson<BigIntegerGraph, TOptions>(encoding.GetBytes(json),ref target);
                    var original=target;
                    LuminPackSerializer.DeserializeJson<BigIntegerGraph, TOptions>(encoding.GetBytes(json),ref target);
                    if(!ReferenceEquals(original,target)||LuminPackSerializer.SerializeJson<BigIntegerGraph, TOptions>(target)!=json)
                        throw new Exception("BigInteger graph reuse changed.");
                }
                public static void NumericBinding(ref LuminPackJsonWriter writer)=>writer.Write<LuminPackUtf8LengthOptions>(1);
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("BigIntegerPaths" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckBigIntegerCalls(driver, true);
            Execute(output);
            if (mode != "Minimal") continue;
            string runtime = typeof(LuminPack.Core.LuminPackJsonWriter).Assembly.Location;
            var original = references.Single(reference => string.Equals(reference.Display, runtime, StringComparison.OrdinalIgnoreCase));
            var missing = MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(runtime, "WriteBigInteger", "ReadBigInteger"), filePath: "CoreWithoutBigIntegerText.dll");
            var compatibility = compilation.WithReferences(references.Where(reference => !ReferenceEquals(reference, original)).Append(missing));
            foreach (var target in new[] { compatibility, compilation })
            {
                driver = driver.RunGeneratorsAndUpdateCompilation(target, out output, out diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(target))), "A BigInteger API reference change retained stale output.");
                CheckBigIntegerCalls(driver, ReferenceEquals(target, compilation));
                using var image = new MemoryStream();
                var emitted = output.Emit(image);
                Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            }
        }
        Console.WriteLine("Passed BigInteger binary/JSON roots and graphs, four modes and retained/fresh runtime API changes.");
    }

    /// <summary>Checks calls follow the actual runtime API without depending on forwarding-root retention.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="available">Whether the named integer APIs are present.</param>
    private static void CheckBigIntegerCalls(GeneratorDriver driver, bool available)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.Numerics.BigInteger").ToArray();
        Require(methods.Length != 0, "BigInteger operations were not generated.");
        Require(methods.Any(method => method.ToString().Contains("LuminPackMarshal.ReadBigInteger", StringComparison.Ordinal)) == available, "BigInteger binary reading selected an unavailable API.");
        Require(methods.Any(method => method.ToString().Contains("writer.WriteBigInteger", StringComparison.Ordinal)) == available, "BigInteger writing selected an unavailable API.");
        Require(methods.Any(method => method.ToString().Contains("reader.ReadBigInteger", StringComparison.Ordinal)) == available, "BigInteger reading selected an unavailable API.");
        Require(available || methods.Any(method => method.ToString().Contains("global::System.Numerics.BigInteger.Parse", StringComparison.Ordinal)), "BigInteger compatibility parsing disappeared.");
    }
}
