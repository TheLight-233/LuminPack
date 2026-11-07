using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks storage-view binding when a read-only collection builder is nested in a generated object.</summary>
internal static class SerializerCollectionBuilderRegression
{
    /// <summary>Compiles and executes field operations with value and reference elements in each generation tier.</summary>
    /// <param name="references">The actual framework and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Linq;
            using System.Runtime.CompilerServices;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using LuminPack.Utility;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class CollectionBuilderGraph
            {
                public ReadOnlyCollectionBuilder<int> Numbers;
                public ReadOnlyCollectionBuilder<string> Text;
                public int Tail;
            }
            public static class CollectionBuilderEntry
            {
                public static void Check()
                {
                    Check<LuminPackUtf8LengthOptions>();Check<LuminPackUtf16LengthOptions>();
                    Check<LuminPackUtf8TokenOptions>();Check<LuminPackUtf16TokenOptions>();
                }
                private static void Check<P>()where P:struct,ILuminPackOptions
                {
                    using var buffer=new LuminBufferWriter(true);
                    foreach(int count in new[]{0,1,33})foreach(bool json in new[]{false,true})foreach(bool reused in new[]{false,true})
                    {
                        var numbers=Enumerable.Range(0,count).ToArray();var text=numbers.Select(item=>item.ToString()).ToArray();
                        var value=new CollectionBuilderGraph{Numbers=new(numbers),Text=new(text),Tail=123};
                        buffer.ResetCore();if(json)LuminPackSerializer.SerializeJson<CollectionBuilderGraph, P>(value,buffer);else LuminPackSerializer.Serialize<CollectionBuilderGraph, P>(value,buffer);
                        var old=new CollectionBuilderGraph{Numbers=new(new[]{-1}),Text=new(new[]{"old"}),Tail=-1};
                        CollectionBuilderGraph result=reused?old:null;
                        if(json)LuminPackSerializer.DeserializeJson<CollectionBuilderGraph, P>(buffer.GetSpan(),ref result);else LuminPackSerializer.Deserialize<CollectionBuilderGraph, P>(buffer.GetSpan(),ref result);
                        if(result.Tail!=123||!result.Numbers.SequenceEqual(numbers)||!result.Text.SequenceEqual(text)||reused&&!ReferenceEquals(old,result))
                            throw new Exception("Nested builder storage or object reuse changed.");
                    }
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("CollectionBuilderFields" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            if (diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error) || !emitted.Success)
                throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(emitted.Diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("CollectionBuilderEntry")!.GetMethod("Check")!.Invoke(null, null);
        }
        Console.WriteLine("Passed binary/JSON collection-builder fields and object reuse in four tiers.");
    }
}
