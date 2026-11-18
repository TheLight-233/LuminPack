using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks that priority pairs select each JSON value once, including strict token accounting.</summary>
    /// <param name="references">The actual platform and runtime references.</param>
    internal static void RunPriorityJson(MetadataReference[] references)
    {
        const string source = """
            using System;using System.Linq;using System.Text;using System.Buffers;using System.Collections.Generic;
            using LuminPack;using LuminPack.Attribute;using LuminPack.Option;using LuminPack.Utility;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            public readonly struct Strict8:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF8;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
            public readonly struct Strict16:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF16;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
            [LuminPackable] public sealed class PriorityEnvelope{public PriorityQueue<string,int> Queue;public int Tail;}
            public static class PriorityJsonEntry
            {
                public static void Check(){Check<Strict8>();Check<Strict16>();Check<LuminPackUtf8LengthOptions>();Check<LuminPackUtf16LengthOptions>();}
                private static void Check<P>()where P:struct,ILuminPackOptions
                {
                    using var output=new LuminBufferWriter(true);
                    foreach(int count in new[]{0,1,2,33})foreach(bool reuse in new[]{false,true})
                    {
                        var value=new PriorityQueue<int,int>(Enumerable.Range(0,count).Select(i=>(i,i+7)));
                        output.ResetWrittenCount();LuminPackSerializer.SerializeJson<PriorityQueue<int, int>, P>(value,output);
                        PriorityQueue<int,int> result=reuse?new(new[]{(-1,-1)}):null;var prior=result;
                        LuminPackSerializer.DeserializeJson<PriorityQueue<int, int>, P>(output.GetSpan(),ref result);
                        if(!result.UnorderedItems.SequenceEqual(value.UnorderedItems)||reuse&&!ReferenceEquals(prior,result))throw new Exception("Priority pair tokens or reuse changed.");
                        var graph=new PriorityEnvelope{Queue=new(Enumerable.Range(0,count).Select(i=>("item"+i,i+7))),Tail=137};
                        output.ResetWrittenCount();LuminPackSerializer.SerializeJson<PriorityEnvelope, P>(graph,output);PriorityEnvelope restored=null;
                        LuminPackSerializer.DeserializeJson<PriorityEnvelope, P>(output.GetSpan(),ref restored);
                        if(restored.Tail!=137||!restored.Queue.UnorderedItems.SequenceEqual(graph.Queue.UnorderedItems))throw new Exception("Nested priority pair tokens changed.");
                    }
                    foreach(string ignored in new[]{"1", "\"ignored\"", "true,false,null", "{\"nested\":[[\"hidden\",5]]}"})
                    foreach(bool reuse in new[]{false,true})
                    {
                        var encoding=default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode;
                        PriorityQueue<string,int> result=reuse?new(new[]{("old",-1)}):null;var prior=result;
                        LuminPackSerializer.DeserializeJson<PriorityQueue<string, int>, P>(encoding.GetBytes("["+ignored+"]"),ref result);
                        if(result.Count!=0||reuse&&!ReferenceEquals(prior,result))throw new Exception("Ignored priority value changed reuse or contents.");
                        var graph=new PriorityEnvelope{Queue=result};
                        string json="{\"Queue\":["+ignored+",[\"first\",1],"+ignored+",[\"second\",2],"+ignored+"],\"Tail\":137}";
                        LuminPackSerializer.DeserializeJson<PriorityEnvelope, P>(encoding.GetBytes(json),ref graph);
                        if(graph.Tail!=137||graph.Queue.Count!=2||graph.Queue.Dequeue()!="first"||graph.Queue.Dequeue()!="second")throw new Exception("Ignored priority value consumed a pair or following field.");
                    }
                    if(!default(P).ValidateJsonSyntax)return;
                    foreach(string text in new[]{"[[1,]]","[[1 2]]","[[1,2,3]]","[[1,2]"})
                    {
                        var bytes=default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8.GetBytes(text):Encoding.Unicode.GetBytes(text);
                        PriorityQueue<int,int> result=null;bool rejected=false;
                        try{LuminPackSerializer.DeserializeJson<PriorityQueue<int, int>, P>(bytes,ref result);}catch(FormatException){rejected=true;}
                        if(!rejected)throw new Exception("Malformed priority pair was accepted: "+text);
                    }
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("PriorityJson" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Task.Run(() => Assembly.Load(image.ToArray()).GetType("PriorityJsonEntry")!.GetMethod("Check")!.Invoke(null, null))
                .WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
        }
        Console.WriteLine("Passed strict/permissive UTF-8/UTF-16 priority JSON, ignored values, nested boundaries, reuse and malformed pairs in four tiers.");
    }
}
