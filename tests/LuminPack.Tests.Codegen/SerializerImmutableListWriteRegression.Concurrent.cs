using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Verifies concurrent snapshots, callback boundaries and optional APIs in every generation tier.</summary>
    /// <param name="references">The actual consumer metadata.</param>
    internal static void RunConcurrent(MetadataReference[] references)
    {
        VerifyConcurrentRuntime();
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        string[] helpers = ["EnumerateConcurrentDictionary", "EnumerateConcurrentStack", "RentConcurrentBagSnapshot", "TryWriteConcurrentBagRaw", "PublishConcurrentStack"];
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var initial = CSharpCompilation.Create("ConcurrentCases" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(ConcurrentSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var access = initial.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess")!;
            var original = references.OfType<PortableExecutableReference>().Single(reference =>
                SymbolEqualityComparer.Default.Equals(initial.GetAssemblyOrModuleSymbol(reference), access.ContainingAssembly));
            var driver = Create(parse);
            string? expected = null;
            foreach (string? missing in new string?[] { null, "EnumerateConcurrentDictionary", "EnumerateConcurrentStack", "RentConcurrentBagSnapshot", "TryWriteConcurrentBagRaw", "PublishConcurrentStack", null })
            {
                var current = missing is null ? initial : initial.ReplaceReference(original,
                    MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, missing)));
                driver = driver.RunGeneratorsAndUpdateCompilation(current, out var output, out var diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                string generated = string.Join("\n", Trees(driver));
                foreach (string helper in helpers)
                    Require(generated.Contains("LuminPackCollectionAccess." + helper + "(", StringComparison.Ordinal) == (helper != missing), "Stale concurrent helper: " + helper);
                Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))), "Concurrent capability edits differ from fresh generation.");
                using var image = new MemoryStream();
                var emitted = output.Emit(image);
                Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
                string evidence = (string)Assembly.Load(image.ToArray()).GetType("ConcurrentCases")!.GetMethod("Check")!.Invoke(null, [missing is null])!;
                expected ??= evidence;
                Require(evidence == expected, "Concurrent wire, callbacks or failed-read prefix changed.");
            }
        }
        Console.WriteLine("Passed concurrent binary/UTF8/UTF16, callbacks, failure prefixes, snapshots, four tiers and retained/fresh optional APIs.");
    }

    private const string ConcurrentSource = """
        using System;
        using System.Linq;
        using System.Text;
        using System.Collections.Concurrent;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public sealed class ConcurrentGraph
        {
            public ConcurrentDictionary<int,int> Map;
            public ConcurrentStack<int> Stack;
            public ConcurrentBag<int> Bag;
            public ConcurrentQueue<int> Queue;
            public BlockingCollection<int> Blocking;
            public int Tail;
        }
        [LuminPackable(GeneratorType.Custom)] public sealed class ConcurrentItem
        {
            public int Id;
            public static Action<int> Writing, Reading;
            public static void Serialize(ref LuminPack.Core.LuminPackWriter writer,in ConcurrentItem value){Writing?.Invoke(value.Id);writer.Write(value.Id);}
            public static void Deserialize(ref LuminPack.Core.LuminPackReader reader,ref ConcurrentItem value){int id=reader.Read<int>();Reading?.Invoke(id);value=new ConcurrentItem{Id=id};}
            public static void WriteJson<P>(ref LuminPack.Core.LuminPackJsonWriter writer,in ConcurrentItem value)where P:struct,ILuminPackOptions{Writing?.Invoke(value.Id);writer.Write<P>(value.Id);}
            public static void ReadJson<P>(ref LuminPack.Core.LuminPackJsonReader reader,ref ConcurrentItem value)where P:struct,ILuminPackOptions{int id=reader.Read<int,P>();Reading?.Invoke(id);value=new ConcurrentItem{Id=id};}
        }
        public static class ConcurrentCases
        {
            public static string Check(bool optimized)
            {
                var evidence=new StringBuilder();
                Check<LuminPackUtf8LengthOptions>(evidence);
                Check<LuminPackUtf16LengthOptions>(evidence);
                if(optimized){CheckAllocations<LuminPackUtf8LengthOptions>();CheckAllocations<LuminPackUtf16LengthOptions>();}
                return evidence.ToString();
            }
            private static ConcurrentStack<int> Sink;
            private static void CheckAllocations<P>()where P:struct,ILuminPackOptions
            {
                int[] values=Enumerable.Range(0,257).ToArray();
                var stack=new ConcurrentStack<int>(values);var bag=new ConcurrentBag<int>(values);var map=new ConcurrentDictionary<int,int>(values.ToDictionary(i=>i,i=>i));
                using var output=new LuminPack.Utility.LuminBufferWriter(true);
                foreach(bool json in new[]{false,true})
                {
                    Action write=()=>{output.ResetCore();if(json){LuminPackSerializer.SerializeJson<ConcurrentBag<int>, P>(bag,output);LuminPackSerializer.SerializeJson<ConcurrentStack<int>, P>(stack,output);LuminPackSerializer.SerializeJson<ConcurrentDictionary<int, int>, P>(map,output);}else{LuminPackSerializer.Serialize<ConcurrentBag<int>, P>(bag,output);LuminPackSerializer.Serialize<ConcurrentStack<int>, P>(stack,output);LuminPackSerializer.Serialize<ConcurrentDictionary<int, int>, P>(map,output);}};
                    if(Measure(write)!=0)throw new Exception("Concurrent traversal allocated after warmup.");
                }
                output.ResetCore();LuminPackSerializer.SerializeJson<ConcurrentStack<int>, P>(stack,output);byte[] bytes=output.GetSpan().ToArray();
                long finalGraph=Measure(()=>{Sink=new ConcurrentStack<int>();Sink.PushRange(values);});
                long reading=Measure(()=>{ConcurrentStack<int> value=null;LuminPackSerializer.DeserializeJson<ConcurrentStack<int>, P>(bytes,ref value);Sink=value;});
                if(reading!=finalGraph||Sink.Count!=values.Length)throw new Exception("Framework stack JSON allocated beyond final nodes.");
            }
            private static long Measure(Action action)
            {
                for(int i=0;i<64;i++)action();long best=long.MaxValue;
                for(int sample=0;sample<3;sample++){long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<64;i++)action();best=Math.Min(best,GC.GetAllocatedBytesForCurrentThread()-before);}
                return best;
            }
            private static unsafe void Check<P>(StringBuilder evidence)where P:struct,ILuminPackOptions
            {
                LuminPackSerializer.Register<ConcurrentItem,P>(ConcurrentItem.Serialize,ConcurrentItem.Deserialize,ConcurrentItem.WriteJson<P>,ConcurrentItem.ReadJson<P>);
                using var output=new LuminPack.Utility.LuminBufferWriter(true);
                foreach(int count in new[]{0,1,17,257})
                {
                    int[] values=Enumerable.Range(0,count).Select(i=>i*7-19).ToArray();
                    var graph=new ConcurrentGraph{Map=new ConcurrentDictionary<int,int>(values.ToDictionary(i=>i,i=>i+1)),Stack=new ConcurrentStack<int>(values),Bag=new ConcurrentBag<int>(values),Queue=new ConcurrentQueue<int>(values),Blocking=new BlockingCollection<int>(new ConcurrentQueue<int>(values)),Tail=719};
                    foreach(bool json in new[]{false,true})
                    {
                        output.ResetCore();if(json)LuminPackSerializer.SerializeJson<ConcurrentGraph, P>(graph,output);else LuminPackSerializer.Serialize<ConcurrentGraph, P>(graph,output);
                        byte[] bytes=output.GetSpan().ToArray();evidence.AppendLine(Convert.ToBase64String(bytes));
                        ConcurrentGraph copy=null;
                        if(json)LuminPackSerializer.DeserializeJson<ConcurrentGraph, P>(bytes,ref copy);else LuminPackSerializer.Deserialize<ConcurrentGraph, P>(bytes,ref copy);
                        if(copy.Tail!=719||copy.Map.Count!=count||values.Any(i=>copy.Map[i]!=i+1)||!copy.Stack.SequenceEqual(values.Reverse())||!copy.Queue.SequenceEqual(values)||!copy.Blocking.SequenceEqual(values)||!copy.Bag.OrderBy(i=>i).SequenceEqual(values))throw new Exception("Concurrent graph changed.");
                        copy.Blocking.Dispose();
                    }
                    graph.Blocking.Dispose();
                }
                foreach(string json in new[]{"null","[]","[1,2,3]","[1,2,","[1,2,invalid]","[1,{}]","[1,2,3]tail"})foreach(bool fresh in new[]{false,true})
                {
                    var value=fresh?null:new ConcurrentStack<int>(new[]{991});var old=value;
                    byte[] bytes=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes(json);
                    try{LuminPackSerializer.DeserializeJson<ConcurrentStack<int>, P>(bytes,ref value);evidence.Append("ok");}catch(Exception error){evidence.Append(error.GetType().FullName);}
                    evidence.Append(':').Append(ReferenceEquals(value,old)).Append(':').AppendLine(value==null?"null":string.Join(",",value));
                }
                var nullable=new ConcurrentStack<int?>(new int?[]{1,null,3});
                byte[] nullableBytes=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes(LuminPackSerializer.SerializeJson<ConcurrentStack<int?>, P>(nullable));
                ConcurrentStack<int?> nullableCopy=null;LuminPackSerializer.DeserializeJson<ConcurrentStack<int?>, P>(nullableBytes,ref nullableCopy);
                if(!nullable.SequenceEqual(nullableCopy))throw new Exception("Nullable stack order changed.");
                foreach(bool json in new[]{false,true})
                {
                    var bag=new ConcurrentBag<ConcurrentItem>(new[]{new ConcurrentItem{Id=1},new ConcurrentItem{Id=2},new ConcurrentItem{Id=3}});
                    ConcurrentItem.Writing=id=>{evidence.Append(id).Append(',');bag.Clear();bag.Add(new ConcurrentItem{Id=9});};
                    output.ResetCore();if(json)LuminPackSerializer.SerializeJson<ConcurrentBag<ConcurrentItem>, P>(bag,output);else LuminPackSerializer.Serialize<ConcurrentBag<ConcurrentItem>, P>(bag,output);
                    ConcurrentItem.Writing=null;evidence.AppendLine(Convert.ToBase64String(output.GetSpan().ToArray()));
                    ConcurrentItem.Writing=id=>throw new InvalidOperationException("write callback");bool failed=false;
                    try{output.ResetCore();if(json)LuminPackSerializer.SerializeJson<ConcurrentBag<ConcurrentItem>, P>(bag,output);else LuminPackSerializer.Serialize<ConcurrentBag<ConcurrentItem>, P>(bag,output);}catch(InvalidOperationException){failed=true;}finally{ConcurrentItem.Writing=null;}
                    if(!failed)throw new Exception("Missing callback failure.");
                    var worker=new System.Threading.Thread(()=>bag.Add(new ConcurrentItem{Id=17})){IsBackground=true};worker.Start();if(!worker.Join(5000))throw new Exception("Bag lock survived callback failure.");
                    var map=new ConcurrentDictionary<int,ConcurrentItem>(1,3);for(int i=0;i<7;i++)map[i]=new ConcurrentItem{Id=i};
                    ConcurrentItem.Writing=id=>{evidence.Append(id).Append(',');map.TryRemove(id+1,out _);for(int key=10;key<40;key++)map[key]=new ConcurrentItem{Id=key};};
                    output.ResetCore();if(json)LuminPackSerializer.SerializeJson<ConcurrentDictionary<int, ConcurrentItem>, P>(map,output);else LuminPackSerializer.Serialize<ConcurrentDictionary<int, ConcurrentItem>, P>(map,output);
                    ConcurrentItem.Writing=null;evidence.AppendLine(Convert.ToBase64String(output.GetSpan().ToArray()));
                    foreach(bool fail in new[]{false,true})
                    {
                        var stack=new ConcurrentStack<ConcurrentItem>(new[]{new ConcurrentItem{Id=1},new ConcurrentItem{Id=2},new ConcurrentItem{Id=3}});
                        output.ResetCore();if(json)LuminPackSerializer.SerializeJson<ConcurrentStack<ConcurrentItem>, P>(stack,output);else LuminPackSerializer.Serialize<ConcurrentStack<ConcurrentItem>, P>(stack,output);byte[] bytes=output.GetSpan().ToArray();
                        stack=new ConcurrentStack<ConcurrentItem>();
                        ConcurrentItem.Reading=id=>{evidence.Append(id).Append(':').Append(string.Join(",",stack.Select(v=>v.Id))).Append(';');if(fail&&id==2)throw new InvalidOperationException("read callback");};
                        try{if(json)LuminPackSerializer.DeserializeJson<ConcurrentStack<ConcurrentItem>, P>(bytes,ref stack);else LuminPackSerializer.Deserialize<ConcurrentStack<ConcurrentItem>, P>(bytes,ref stack);}catch(Exception error){evidence.Append(error.GetType().Name);}finally{ConcurrentItem.Reading=null;}
                        evidence.Append('=').AppendLine(string.Join(",",stack.Select(v=>v.Id)));
                    }
                }
            }
        }
        """;
}
