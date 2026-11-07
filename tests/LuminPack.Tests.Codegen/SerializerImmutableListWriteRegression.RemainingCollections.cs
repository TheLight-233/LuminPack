using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks grouping allocations, concurrent snapshots and priority traversal without running the full suite.</summary>
    /// <param name="references">The actual runtime and framework references.</param>
    internal static void RunRemainingCollections(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("RemainingCollections" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(RemainingCollectionsSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("RemainingCollectionsEntry")!.GetMethod("Check")!.Invoke(null, null);
            if (mode == "Minimal") CheckRemainingCapabilities(compilation, references, parse);
        }
        Console.WriteLine("Passed grouping/lookup allocations, snapshots, priority mutation checks, JSON property choices and old runtime fallbacks in four tiers.");
    }

    /// <summary>Removes optional runtime operations and checks retained generation against a fresh driver.</summary>
    /// <param name="compilation">The complete fixture compilation.</param>
    /// <param name="references">The actual metadata references.</param>
    /// <param name="parse">The consuming language options.</param>
    private static void CheckRemainingCapabilities(CSharpCompilation compilation, MetadataReference[] references, CSharpParseOptions parse)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess")!;
        var original = references.OfType<PortableExecutableReference>().Single(reference =>
            SymbolEqualityComparer.Default.Equals(compilation.GetAssemblyOrModuleSymbol(reference), access.ContainingAssembly));
        string[] names = ["GetGroupingStorage", "GetEntryEnumerator", "ReadStringChoice", "TryGetPriorityQueueTraversal", "CheckPriorityQueueTraversalVersion"];
        var changed = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, names)));
        var driver = Create(parse);
        string? expected = null;
        foreach (var current in new[] { compilation, changed, compilation })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(current, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            foreach (string name in names)
                Require(Trees(driver).Any(text => text.Contains(name, StringComparison.Ordinal)) == ReferenceEquals(current, compilation), "Stale collection capability: " + name);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))), "Runtime metadata edits retained stale collection operations.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            var entry = Assembly.Load(image.ToArray()).GetType("RemainingCollectionsEntry")!;
            string result = (string)entry.GetMethod("Evidence")!.Invoke(null, null)!;
            expected ??= result;
            Require(result == expected, "Optional collection traversal changed wire bytes, exceptions or callback order.");
        }
    }

    private const string RemainingCollectionsSource = """
        using System;
        using System.Linq;
        using System.Text;
        using System.Collections.Generic;
        using System.Collections.Concurrent;
        using System.Numerics;
        using System.Buffers;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public readonly struct RemainingStrict8:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF8;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        public readonly struct RemainingStrict16:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF16;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        [LuminPackable] public sealed class RemainingEnvelope
        {
            public IGrouping<int,int> Group;
            public IGrouping<int,int> OtherGroup;
            public ILookup<int,int> Lookup;
            public PriorityQueue<int,int> Priority;
            public ConcurrentBag<int> Bag;
            public BlockingCollection<int> Blocking;
            public Complex Complex;
            public Plane Plane;
            public int Tail;
        }
        [LuminPackable(GeneratorType.Custom)] public sealed class RemainingItem
        {
            public int Id;
            public static Action<int> Writing;
            public static void Serialize(ref LuminPackWriter writer,in RemainingItem value){Writing?.Invoke(value.Id);writer.Write(value.Id);}
            public static void Deserialize(ref LuminPackReader reader,ref RemainingItem value)=>value=new RemainingItem{Id=reader.Read<int>()};
            public static void SerializeJson(ref LuminPackJsonWriter writer,in RemainingItem value)=>WriteJson<LuminPackDefaultOptions>(ref writer,in value);
            public static void DeserializeJson(ref LuminPackJsonReader reader,ref RemainingItem value)=>ReadJson<LuminPackDefaultOptions>(ref reader,ref value);
            public static void WriteJson<P>(ref LuminPackJsonWriter writer,in RemainingItem value)where P:struct,ILuminPackOptions{Writing?.Invoke(value.Id);writer.Write<P>(value.Id);}
            public static void ReadJson<P>(ref LuminPackJsonReader reader,ref RemainingItem value)where P:struct,ILuminPackOptions=>value=new RemainingItem{Id=reader.Read<int,P>()};
        }
        public static class RemainingCollectionsEntry
        {
            public static void Check()
            {
                Check<LuminPackUtf8LengthOptions>();Check<LuminPackUtf16LengthOptions>();
                Check<LuminPackUtf8TokenOptions>();Check<LuminPackUtf16TokenOptions>();
                Check<RemainingStrict8>();Check<RemainingStrict16>();
                Evidence();
            }
            private static long Allocated(Action action)
            {
                for(int i=0;i<64;i++)action();long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<32;i++)action();return GC.GetAllocatedBytesForCurrentThread()-before;
            }
            private static void Check<P>()where P:struct,ILuminPackOptions
            {
                using var output=new LuminBufferWriter(true);
                foreach(int count in new[]{0,1,16,32,33})
                {
                    var items=Enumerable.Range(0,count).ToArray();
                    IGrouping<int,int> group=new LuminPackGrouping<int,int>(1,items);
                    ILookup<int,int> lookup=new LuminPackLookup<int,int>(new Dictionary<int,IGrouping<int,int>>{{1,group}});
                    if(Allocated(()=>{output.ResetCore();LuminPackSerializer.Serialize<IGrouping<int, int>, P>(group,output);LuminPackSerializer.SerializeJson<IGrouping<int, int>, P>(group,output);})!=0||
                       Allocated(()=>{output.ResetCore();LuminPackSerializer.Serialize<ILookup<int, int>, P>(lookup,output);LuminPackSerializer.SerializeJson<ILookup<int, int>, P>(lookup,output);})!=0)
                        throw new Exception("Grouping or lookup traversal allocated.");
                    var value=new RemainingEnvelope{Group=group,OtherGroup=group,Lookup=lookup,Priority=new(items.Select(x=>(x,x))),Bag=new(items),Blocking=new(new ConcurrentQueue<int>(items)),Complex=new(2,3),Plane=new(1,2,3,4),Tail=137};
                    foreach(bool json in new[]{false,true})foreach(bool reused in new[]{false,true})
                    {
                        output.ResetCore();if(json)LuminPackSerializer.SerializeJson<RemainingEnvelope, P>(value,output);else LuminPackSerializer.Serialize<RemainingEnvelope, P>(value,output);
                        RemainingEnvelope result=reused?new RemainingEnvelope{Priority=new(),Bag=new(),Blocking=new()}:null;
                        var old=result;var priorBlocking=result?.Blocking;
                        if(json)LuminPackSerializer.DeserializeJson<RemainingEnvelope, P>(output.GetSpan(),ref result);else LuminPackSerializer.Deserialize<RemainingEnvelope, P>(output.GetSpan(),ref result);
                        if(result.Tail!=137||result.Complex!=value.Complex||result.Plane!=value.Plane||!result.Group.SequenceEqual(items)||
                           !result.OtherGroup.SequenceEqual(items)||!result.Lookup[1].SequenceEqual(items)||!result.Priority.UnorderedItems.Select(x=>x.Element).SequenceEqual(items)||
                           !result.Bag.OrderBy(x=>x).SequenceEqual(items)||!result.Blocking.SequenceEqual(items)||
                           reused&&(!ReferenceEquals(old,result)||ReferenceEquals(priorBlocking,result.Blocking)))
                            throw new Exception("Remaining collection shape, heap or reuse changed.");
                        priorBlocking?.Dispose();result.Blocking.Dispose();
                    }
                    value.Blocking.Dispose();
                }
                var scalar=new Complex(2,3);output.ResetCore();LuminPackSerializer.SerializeJson<Complex, P>(scalar,output);byte[] bytes=output.GetSpan().ToArray();
                Complex resultScalar=default;
                if(Allocated(()=>LuminPackSerializer.DeserializeJson<Complex, P>(bytes,ref resultScalar))!=0||resultScalar!=scalar)throw new Exception("Complex property names allocated.");
                var plane=new Plane(1,2,3,4);output.ResetCore();LuminPackSerializer.SerializeJson<Plane, P>(plane,output);bytes=output.GetSpan().ToArray();Plane resultPlane=default;
                if(Allocated(()=>LuminPackSerializer.DeserializeJson<Plane, P>(bytes,ref resultPlane))!=0||resultPlane!=plane)throw new Exception("Plane property names allocated.");
                var queues=Enumerable.Range(0,32).Select(i=>new PriorityQueue<int,int>(new[]{(1,1)})).ToArray();
                var warmQueue=new PriorityQueue<int,int>(new[]{(1,1)});output.ResetCore();LuminPackSerializer.SerializeJson<PriorityQueue<int, int>, P>(warmQueue,output);
                long start=GC.GetAllocatedBytesForCurrentThread();
                foreach(var queue in queues){output.ResetCore();LuminPackSerializer.SerializeJson<PriorityQueue<int, int>, P>(queue,output);}
                if(GC.GetAllocatedBytesForCurrentThread()!=start)throw new Exception("Priority traversal allocated a view on fresh queues.");
            }
            public static string Evidence()
            {
                var report=new StringBuilder();Evidence<LuminPackUtf8LengthOptions>(report);Evidence<LuminPackUtf16LengthOptions>(report);
                Evidence<LuminPackUtf8TokenOptions>(report);Evidence<LuminPackUtf16TokenOptions>(report);return report.ToString();
            }
            private static void Evidence<P>(StringBuilder report)where P:struct,ILuminPackOptions
            {
                unsafe{LuminPackSerializer.Register<RemainingItem,P>(RemainingItem.Serialize,RemainingItem.Deserialize,RemainingItem.WriteJson<P>,RemainingItem.ReadJson<P>);}
                using var output=new LuminBufferWriter(true);
                foreach(string text in new[]{"{\"Key\":1,\"Elements\":[2,3]}","{\"K\\u0065y\":2,\"Elements\":[],\"ignored\":[1],\"Key\":3}","{\"Elements\":[4],\"\u00e9\":null,\"Key\":4}","{\"Key\":1,\"Elements\":null}","{\"Key\":1,\"Elements\":["})
                {
                    var bytes=default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8.GetBytes(text):Encoding.Unicode.GetBytes(text);
                    IGrouping<int,int> value=new LuminPackGrouping<int,int>(9,new[]{9});var old=value;
                    try{LuminPackSerializer.DeserializeJson<IGrouping<int, int>, P>(bytes,ref value);report.Append(value.Key).Append(':');try{report.Append(string.Join(",",value));}catch(Exception e){report.Append(e.GetType().Name);}}
                    catch(Exception e){report.Append(e.GetType().Name).Append(':').Append(ReferenceEquals(old,value));}
                    report.AppendLine();
                }
                foreach(bool json in new[]{false,true})foreach(int mutation in new[]{0,1,2,3})
                {
                    var queue=new PriorityQueue<RemainingItem,int>(new[]{(new RemainingItem{Id=1},1),(new RemainingItem{Id=2},2)});
                    RemainingItem.Writing=id=>{report.Append(id).Append(',');if(id==(mutation==3?2:1)){if(mutation==1)queue.Clear();if(mutation==2)queue.EnsureCapacity(100);if(mutation==3)queue.Enqueue(new RemainingItem{Id=3},3);}};
                    output.ResetCore();try{if(json)LuminPackSerializer.SerializeJson<PriorityQueue<RemainingItem, int>, P>(queue,output);else LuminPackSerializer.Serialize<PriorityQueue<RemainingItem, int>, P>(queue,output);report.Append("ok");}
                    catch(Exception e){report.Append(e.GetType().Name);}
                    report.Append(':').Append(Convert.ToBase64String(output.GetSpan())).AppendLine();RemainingItem.Writing=null;
                }
                foreach(bool json in new[]{false,true})foreach(int mutation in new[]{0,1,2,3})
                {
                    var dict=new Dictionary<int,IGrouping<int,RemainingItem>>();
                    dict.Add(1,new LuminPackGrouping<int,RemainingItem>(1,new[]{new RemainingItem{Id=1}}));
                    dict.Add(2,new LuminPackGrouping<int,RemainingItem>(2,new[]{new RemainingItem{Id=2}}));
                    ILookup<int,RemainingItem> lookup=new LuminPackLookup<int,RemainingItem>(dict);
                    RemainingItem.Writing=id=>{report.Append(id).Append(',');if(id==1){if(mutation==1)dict.Clear();if(mutation==2)dict.Remove(2);if(mutation==3)dict.Add(3,new LuminPackGrouping<int,RemainingItem>(3,Array.Empty<RemainingItem>()));}};
                    output.ResetCore();try{if(json)LuminPackSerializer.SerializeJson<ILookup<int, RemainingItem>, P>(lookup,output);else LuminPackSerializer.Serialize<ILookup<int, RemainingItem>, P>(lookup,output);report.Append("ok");}
                    catch(Exception e){report.Append(e.GetType().Name);}
                    report.Append(':').Append(Convert.ToBase64String(output.GetSpan())).AppendLine();RemainingItem.Writing=null;
                }
                foreach(int count in new[]{0,16,17,32})foreach(int failAt in new[]{-1,1})
                {
                    var source=new PriorityQueue<int,int>(Enumerable.Range(0,count).Select(i=>(i,i)));
                    output.ResetCore();LuminPackSerializer.Serialize<PriorityQueue<int, int>, P>(source,output);
                    int calls=0;var comparer=Comparer<int>.Create((left,right)=>{report.Append(left).Append('/').Append(right).Append(',');if(++calls==failAt)throw new InvalidOperationException("Comparer sentinel");return left.CompareTo(right);});
                    var result=new PriorityQueue<int,int>(comparer);var old=result;
                    try{LuminPackSerializer.Deserialize<PriorityQueue<int, int>, P>(output.GetSpan(),ref result);report.Append("ok");}catch(Exception e){report.Append(e.GetType().Name);}
                    report.Append(':').Append(ReferenceEquals(old,result)).Append(':').Append(result.Count).Append(':').Append(string.Join(",",result.UnorderedItems)).AppendLine();
                }
                foreach(bool stack in new[]{false,true})foreach(bool json in new[]{false,true})
                {
                    IProducerConsumerCollection<int> storage=stack?(IProducerConsumerCollection<int>)new ConcurrentStack<int>(new[]{1,2,3}):new ConcurrentQueue<int>(new[]{1,2,3});
                    var blocking=new BlockingCollection<int>(storage,8);blocking.CompleteAdding();output.ResetCore();
                    if(json)LuminPackSerializer.SerializeJson<BlockingCollection<int>, P>(blocking,output);else LuminPackSerializer.Serialize<BlockingCollection<int>, P>(blocking,output);
                    report.Append(Convert.ToBase64String(output.GetSpan())).Append(':').Append(blocking.Count).Append(':').Append(blocking.IsAddingCompleted).AppendLine();
                    blocking.Dispose();output.ResetCore();try{if(json)LuminPackSerializer.SerializeJson<BlockingCollection<int>, P>(blocking,output);else LuminPackSerializer.Serialize<BlockingCollection<int>, P>(blocking,output);report.Append("accepted disposed");}catch(Exception e){report.Append(e.GetType().Name);}
                    report.Append(':').Append(Convert.ToBase64String(output.GetSpan())).AppendLine();
                }
                foreach(bool json in new[]{false,true})
                {
                    var bag=new ConcurrentBag<RemainingItem>(new[]{new RemainingItem{Id=1},new RemainingItem{Id=2}});
                    RemainingItem.Writing=id=>{report.Append(id).Append(',');bag.Clear();bag.Add(new RemainingItem{Id=3});};
                    output.ResetCore();if(json)LuminPackSerializer.SerializeJson<ConcurrentBag<RemainingItem>, P>(bag,output);else LuminPackSerializer.Serialize<ConcurrentBag<RemainingItem>, P>(bag,output);
                    report.Append(':').Append(Convert.ToBase64String(output.GetSpan())).AppendLine();RemainingItem.Writing=null;
                }
            }
        }
        """;
}
