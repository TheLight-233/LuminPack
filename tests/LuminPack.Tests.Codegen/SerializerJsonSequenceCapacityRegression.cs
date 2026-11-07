using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks fresh sequence capacity, old API fallback and unchanged array/hash construction.</summary>
internal static class SerializerJsonSequenceCapacityRegression
{
    /// <summary>Executes direct and nested collection reads in every generation mode and both JSON encodings.</summary>
    /// <param name="references">The current runtime and platform metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using System.Collections.ObjectModel;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public partial class Root { public List<int> Items; public Queue<int> Queue; }
            public static class CapacityEntry
            {
                public static void Check()
                {
                    Check<LuminPackUtf8LengthOptions>(); Check<LuminPackUtf16LengthOptions>();
                    Check<LuminPackUtf8TokenOptions>(); Check<LuminPackUtf16TokenOptions>();
                }
                private static void Check<T>() where T:struct, ILuminPackOptions
                {
                    int[] items=Enumerable.Range(0,17).ToArray();
                    string json="["+string.Join(",",items)+"]";
                    List<int> list=null; LuminPackSerializer.DeserializeJson<List<int>, T>(json,ref list);
                    if(list.Capacity!=17||!list.SequenceEqual(items))throw new Exception("Fresh List sizing.");
                    var original=list;list.Capacity=64;
                    LuminPackSerializer.DeserializeJson<List<int>, T>(json,ref list);
                    if(!ReferenceEquals(original,list)||list.Capacity!=64)throw new Exception("List reuse.");
                    IEnumerable<int> sequence=null; LuminPackSerializer.DeserializeJson<IEnumerable<int>, T>(json,ref sequence);
                    if(sequence is not List<int> storage||storage.Capacity!=17)throw new Exception("Sequence storage.");
                    Queue<int> queue=null;LuminPackSerializer.DeserializeJson<Queue<int>, T>(json,ref queue);
                    if(!queue.SequenceEqual(items))throw new Exception("Queue order.");
                    Collection<int> collection=null;LuminPackSerializer.DeserializeJson<Collection<int>, T>(json,ref collection);
                    ObservableCollection<int> observable=null;LuminPackSerializer.DeserializeJson<ObservableCollection<int>, T>(json,ref observable);
                    ReadOnlyCollection<int> readOnly=null;LuminPackSerializer.DeserializeJson<ReadOnlyCollection<int>, T>(json,ref readOnly);
                    ReadOnlyObservableCollection<int> readOnlyObservable=null;LuminPackSerializer.DeserializeJson<ReadOnlyObservableCollection<int>, T>(json,ref readOnlyObservable);
                    foreach(var value in new IEnumerable<int>[]{collection,observable,readOnly,readOnlyObservable})
                        if(!value.SequenceEqual(items))throw new Exception("Sequence wrapper contents.");
                    var root=new Root{Items=list,Queue=queue};
                    var encoded=LuminPackSerializer.SerializeJson<Root, T>(root);Root copy=null;
                    LuminPackSerializer.DeserializeJson<Root, T>(encoded,ref copy);
                    if(copy.Items.Capacity!=17||!copy.Queue.SequenceEqual(items))throw new Exception("Nested sequence.");
                    var bytes=LuminPackSerializer.Serialize(root);Root binary=null;LuminPackSerializer.Deserialize(bytes,ref binary);
                    if(!binary.Items.SequenceEqual(items)||!binary.Queue.SequenceEqual(items))throw new Exception("Binary control.");
                    List<int> prefix=null;bool failed=false;
                    try{LuminPackSerializer.DeserializeJson<List<int>, T>("[1,2,\"bad\"]",ref prefix);}catch(Exception){failed=true;}
                    if(!failed||prefix==null||!prefix.SequenceEqual(new[]{1,2}))throw new Exception("Failed-read prefix.");
                    int[] array=null;LuminPackSerializer.DeserializeJson<int[], T>(json,ref array);
                    HashSet<int> set=null;LuminPackSerializer.DeserializeJson<HashSet<int>, T>("[1,1,1]",ref set);
                    Dictionary<int,int> map=null;LuminPackSerializer.DeserializeJson<Dictionary<int, int>, T>("[[1,2],[1,3]]",ref map);
                    if(array.Length!=17||set.Count!=1||map[1]!=3)throw new Exception("Unchanged array/hash paths.");
                    IGrouping<int,int> group=null;
                    LuminPackSerializer.DeserializeJson<IGrouping<int, int>, T>("{\"Key\":7,\"Elements\":"+json+"}",ref group);
                    if(group.Key!=7||!group.SequenceEqual(items))throw new Exception("Grouping sequence.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var compilation = CSharpCompilation.Create("SequenceCapacity" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics));
            string generated = string.Join("\n", driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
            if (!generated.Contains("GetArrayCapacityHint", StringComparison.Ordinal) ||
                generated.Contains("GetArrayLengthHint", StringComparison.Ordinal) || generated.Contains("GetEntryArrayCapacityHint", StringComparison.Ordinal))
                throw new InvalidOperationException("Sequence sizing selection changed array/hash construction.");
            using var image = new MemoryStream();
            var result = output.Emit(image);
            if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
            Assembly.Load(image.ToArray()).GetType("CapacityEntry")!.GetMethod("Check")!.Invoke(null, null);
            var retained = driver.RunGenerators(compilation).GetRunResult();
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(compilation);
            if (!Trees(retained).SequenceEqual(Trees(fresh.GetRunResult()))) throw new InvalidOperationException("Retained sequence generation changed.");
        }
        CheckMetadata(references);
        Console.WriteLine("Passed sequence capacity, wrappers, binary controls, prefix reuse and optional API metadata in all generation modes.");
    }

    /// <summary>Checks helper signatures before allowing emitted calls against older or incompatible assets.</summary>
    /// <param name="references">The platform references, excluding the actual Core asset for these synthetic contracts.</param>
    private static void CheckMetadata(MetadataReference[] references)
    {
        var available = typeof(LuminPackAggregateGenerator).Assembly.GetType("LuminPack.SourceGenerator.CodeEmitters.JsonCapacityEmitter")!
            .GetMethod("Available", BindingFlags.Static | BindingFlags.NonPublic)!;
        const string methods = "public int GetArrayLengthHint()=>0;public int GetArrayCapacityHint(int minimum)=>0;public int GetEntryArrayCapacityHint()=>0;" +
            "public int GetArrayLengthHint<T>()where T:struct,LuminPack.Option.ILuminPackOptions=>0;" +
            "public int GetArrayCapacityHint<T>(int minimum)where T:struct,LuminPack.Option.ILuminPackOptions=>0;" +
            "public int GetEntryArrayCapacityHint<T>()where T:struct,LuminPack.Option.ILuminPackOptions=>0;";
        foreach (string candidate in new[] { methods, "", methods.Replace("int minimum", "long minimum", StringComparison.Ordinal),
                     methods.Replace("public int GetArrayLengthHint()", "public static int GetArrayLengthHint()", StringComparison.Ordinal),
                     methods.Replace("where T:struct,", "where T:", StringComparison.Ordinal) })
        {
            string source = "namespace LuminPack.Option{public interface ILuminPackOptions{}}namespace LuminPack.Core{public ref struct LuminPackJsonReader{" + candidate + "}}";
            var compilation = CSharpCompilation.Create("MetadataProbe", [CSharpSyntaxTree.ParseText(source)],
                references.Where(r => r.Display is null || !r.Display.EndsWith("LuminPack.Core.dll", StringComparison.OrdinalIgnoreCase)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            bool actual = (bool)available.Invoke(null, [compilation])!;
            if (actual != (candidate == methods)) throw new InvalidOperationException("Optional sequence helper metadata was accepted incorrectly.");
        }
    }

    /// <summary>Gets deterministic generated identities and source text.</summary>
    /// <param name="result">The completed generator driver.</param><returns>The ordered generated trees.</returns>
    private static IEnumerable<string> Trees(GeneratorDriverRunResult result) => result.Results.SelectMany(r => r.GeneratedSources)
        .Select(s => s.HintName + "\n" + s.SourceText).OrderBy(s => s, StringComparer.Ordinal);
}
