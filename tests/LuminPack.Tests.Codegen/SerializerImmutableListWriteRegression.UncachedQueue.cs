using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks that retained list and queue writers follow optional traversal APIs across runtime reference changes.</summary>
    /// <param name="references">The actual runtime and framework metadata references.</param>
    private static void CheckTraversalCapabilities(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var compilation = CSharpCompilation.Create("QueueCapabilities" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText("""
                using System;
                using System.Collections.Immutable;
                using LuminPack;
                public static class QueueCapabilityEntry
                {
                    public static string Evidence()
                    {
                        var queue=ImmutableQueue<int>.Empty.Enqueue(7).Enqueue(11).Enqueue(19);
                        IImmutableQueue<int> contract=queue;
                        var list = ImmutableList.Create(11, 7, 19);
                        IImmutableList<int> listContract = list;
                        return Convert.ToBase64String(LuminPackSerializer.Serialize(queue))+":"+LuminPackSerializer.SerializeJson(contract)
                            +":"+LuminPackSerializer.SerializeJson(list)+":"+LuminPackSerializer.SerializeJson(listContract);
                    }
                }
                """, parse, "Entry.cs")], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess")!;
        bool available = access.GetMembers("TryGetImmutableQueueTraversal").Length != 0 && access.GetMembers("RentImmutableQueueReversal").Length != 0;
        if (!available) return;
        var original = references.OfType<PortableExecutableReference>().Single(reference =>
            SymbolEqualityComparer.Default.Equals(compilation.GetAssemblyOrModuleSymbol(reference), access.ContainingAssembly));
        var noTraversal = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "TryGetImmutableQueueTraversal")));
        var noRental = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "RentImmutableQueueReversal")));
        var noTreeCopy = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "LuminTreeStorage.CopyTo")));
        var driver = Create(parse);
        string? expected = null;
        foreach (var current in new[] { compilation, noTraversal, noRental, noTreeCopy, compilation })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(current, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(Trees(driver).Any(text => text.Contains(".TryGetImmutableQueueTraversal(", StringComparison.Ordinal)) ==
                !(ReferenceEquals(current, noTraversal) || ReferenceEquals(current, noRental)),
                "Queue traversal retained a stale runtime capability.");
            Require(Trees(driver).Count(text => text.Contains("LuminTreeStorage.CopyTo(", StringComparison.Ordinal)) ==
                (ReferenceEquals(current, noTreeCopy) ? 0 : 2), "Concrete and interface lists retained a stale tree copy capability.");
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))), "Traversal metadata edits differ from fresh generation.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            string result = (string)Assembly.Load(image.ToArray()).GetType("QueueCapabilityEntry")!.GetMethod("Evidence")!.Invoke(null, null)!;
            expected ??= result;
            Require(result == expected, "Traversal capability selection changed wire bytes.");
        }
    }

    private const string UncachedQueueSource = """
        [LuminPackable(GeneratorType.Custom)] public sealed class QueueFailItem
        {
            public int Id;
            public string Text;
            public static readonly List<int> Events=new List<int>(64);
            public static int ThrowOn=-1;
            public static readonly Exception Failure=new Exception("Selected queue element failure");
            private static void Observe(QueueFailItem value){Events.Add(value.Id);if(value.Id==ThrowOn)throw Failure;}
            public static void Serialize(ref LuminPack.Core.LuminPackWriter writer,in QueueFailItem value){Observe(value);writer.Write(value.Id);}
            public static void Deserialize(ref LuminPack.Core.LuminPackReader reader,ref QueueFailItem value)=>value=new QueueFailItem{Id=reader.Read<int>()};
            public static void SerializeJson(ref LuminPack.Core.LuminPackJsonWriter writer,in QueueFailItem value)=>WriteJson<LuminPackDefaultOptions>(ref writer,in value);
            public static void DeserializeJson(ref LuminPack.Core.LuminPackJsonReader reader,ref QueueFailItem value)=>ReadJson<LuminPackDefaultOptions>(ref reader,ref value);
            public static void WriteJson<P>(ref LuminPack.Core.LuminPackJsonWriter writer,in QueueFailItem value)where P:struct,ILuminPackOptions{Observe(value);writer.Write<P>(value.Id);}
            public static void ReadJson<P>(ref LuminPack.Core.LuminPackJsonReader reader,ref QueueFailItem value)where P:struct,ILuminPackOptions=>value=new QueueFailItem{Id=reader.Read<int,P>()};
        }
        [LuminPackable] public sealed class UncachedQueueGraph
        {
            public ImmutableQueue<int> Queue;
            public IImmutableQueue<int> Contract;
            public int Tail;
        }
        public static class UncachedQueueCases
        {
            public static void Check()
            {
                CheckProfile<LuminPackUtf8LengthOptions>();CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8TokenOptions>();CheckProfile<LuminPackUtf16TokenOptions>();
            }
            private static void CheckProfile<P>()where P:struct,ILuminPackOptions
            {
                bool available=typeof(LuminPack.Code.LuminPackCollectionAccess).GetMethod("TryGetImmutableQueueTraversal")!=null;
                var cache=typeof(ImmutableQueue<int>).GetField("_backwardsReversed",BindingFlags.NonPublic|BindingFlags.Instance);
                using var output=new LuminPack.Utility.LuminBufferWriter(true);
                foreach(int count in new[]{0,1,2,17,33,257})foreach(bool contract in new[]{false,true})foreach(bool json in new[]{false,true})
                {
                    int[] values=Enumerable.Range(0,count).Select(i=>i*7-13).ToArray();
                    ImmutableQueue<int> queue=Build(values);
                    Action write=()=>{output.ResetCore();if(json){if(contract)LuminPackSerializer.SerializeJson<P>((IImmutableQueue<int>)queue,output);else LuminPackSerializer.SerializeJson<P>(queue,output);}
                        else{if(contract)LuminPackSerializer.Serialize<P>((IImmutableQueue<int>)queue,output);else LuminPackSerializer.Serialize<P>(queue,output);}};
                    for(int i=0;i<64;i++)write();
                    long allocated=long.MaxValue;
                    for(int sample=0;sample<3;sample++)
                    {
                        queue=Build(values);
                        long before=GC.GetAllocatedBytesForCurrentThread();write();allocated=Math.Min(allocated,GC.GetAllocatedBytesForCurrentThread()-before);
                    }
                    if(available && allocated!=0)throw new Exception("First queue write allocated a reversal: "+allocated);
                    if(available && cache.GetValue(queue)!=null)throw new Exception("Serialization populated the lazy queue cache.");
                    byte[] actual=output.GetSpan().ToArray();output.ResetCore();
                    if(json)LuminPackSerializer.SerializeJson<P>(values,output);else LuminPackSerializer.Serialize<P>(values,output);
                    if(!actual.SequenceEqual(output.GetSpan().ToArray()))throw new Exception("Uncached queue framing or element order changed.");
                    if(!queue.SequenceEqual(values)||!queue.Enqueue(719).SequenceEqual(values.Append(719)))throw new Exception("Queue contents changed.");
                    write();if(!actual.SequenceEqual(output.GetSpan().ToArray()))throw new Exception("Cached queue traversal changed.");
                    var graph=new UncachedQueueGraph{Queue=Build(values),Contract=Build(values),Tail=719};
                    UncachedQueueGraph decoded=null;
                    if(json){var encoding=default(P).StringEncoding==LuminPackStringEncoding.UTF8?System.Text.Encoding.UTF8:System.Text.Encoding.Unicode;LuminPackSerializer.DeserializeJson<P>(encoding.GetBytes(LuminPackSerializer.SerializeJson<P>(graph)),ref decoded);}
                    else LuminPackSerializer.Deserialize<P>(LuminPackSerializer.Serialize<P>(graph),ref decoded);
                    if(!decoded.Queue.SequenceEqual(values)||!decoded.Contract.SequenceEqual(values)||decoded.Tail!=719)throw new Exception("Queue field traversal changed.");
                }
                string[] words={"alpha",null,"\u65e5\ud83d\ude00","quote\"\\"};
                var text=ImmutableQueue<string>.Empty;foreach(string word in words)text=text.Enqueue(word);
                foreach(bool json in new[]{false,true})
                {
                    output.ResetCore();if(json)LuminPackSerializer.SerializeJson<P>((IImmutableQueue<string>)text,output);else LuminPackSerializer.Serialize<P>((IImmutableQueue<string>)text,output);
                    byte[] actual=output.GetSpan().ToArray();output.ResetCore();
                    if(json)LuminPackSerializer.SerializeJson<P>(words,output);else LuminPackSerializer.Serialize<P>(words,output);
                    if(!actual.SequenceEqual(output.GetSpan().ToArray()))throw new Exception("Queue string/null framing changed.");
                }
                CheckFailure<P>(output,available);
            }
            private static ImmutableQueue<int> Build(int[] values)
            {
                var queue=ImmutableQueue<int>.Empty;foreach(int value in values)queue=queue.Enqueue(value);return queue;
            }
            private static void CheckFailure<P>(LuminPack.Utility.LuminBufferWriter output,bool available)where P:struct,ILuminPackOptions
            {
                unsafe{LuminPackSerializer.Register<QueueFailItem,P>(QueueFailItem.Serialize,QueueFailItem.Deserialize,QueueFailItem.WriteJson<P>,QueueFailItem.ReadJson<P>);}
                CheckJsonTreeFailures<P>(output);
                foreach(bool json in new[]{false,true})foreach(int failure in new[]{0,2})
                {
                    var queue=ImmutableQueue<QueueFailItem>.Empty;
                    for(int i=0;i<4;i++)queue=queue.Enqueue(new QueueFailItem{Id=i,Text="value"});
                    Action write=()=>{output.ResetCore();if(json)LuminPackSerializer.SerializeJson<P>(queue,output);else LuminPackSerializer.Serialize<P>(queue,output);};
                    QueueFailItem.ThrowOn=-1;QueueFailItem.Events.Clear();write();
                    var expected=System.Buffers.ArrayPool<QueueFailItem>.Shared.Rent(16);System.Buffers.ArrayPool<QueueFailItem>.Shared.Return(expected,true);
                    QueueFailItem.Events.Clear();QueueFailItem.ThrowOn=failure;Exception observed=null;
                    try{write();}catch(Exception error){observed=error;}finally{QueueFailItem.ThrowOn=-1;}
                    var actual=System.Buffers.ArrayPool<QueueFailItem>.Shared.Rent(16);
                    try
                    {
                        if(!ReferenceEquals(observed,QueueFailItem.Failure)||!QueueFailItem.Events.SequenceEqual(Enumerable.Range(0,failure+1)))throw new Exception("Uncached queue callback failure changed: json="+json+", failure="+failure+", events="+string.Join(",",QueueFailItem.Events)+", error="+observed);
                        if(available&&(!ReferenceEquals(expected,actual)||actual.Any(item=>item!=null)))throw new Exception("Queue reversal lease was lost or retained references.");
                    }
                    finally{System.Buffers.ArrayPool<QueueFailItem>.Shared.Return(actual,true);}
                }
            }
            private static void CheckJsonTreeFailures<P>(LuminPack.Utility.LuminBufferWriter output) where P:struct,ILuminPackOptions
            {
                var items = new[] { new QueueFailItem { Id = 2 }, new QueueFailItem { Id = 1 }, new QueueFailItem { Id = 3 } };
                var list = ImmutableList.CreateRange(items);
                var set = list.ToImmutableSortedSet(Comparer<QueueFailItem>.Create((a,b) => b.Id.CompareTo(a.Id)));
                var map = ImmutableSortedDictionary.CreateRange(Comparer<int>.Create((a,b) => b.CompareTo(a)),
                    list.Select(item => new KeyValuePair<int,QueueFailItem>(item.Id,item)));
                Check(items, () => LuminPackSerializer.SerializeJson<P>(list, output), new[] { 2, 1 });
                Check(items, () => LuminPackSerializer.SerializeJson<P>((IImmutableList<QueueFailItem>)list, output), new[] { 2, 1 });
                Check(set.ToArray(), () => LuminPackSerializer.SerializeJson<P>(set, output), new[] { 3, 2 });
                Check(map.ToArray(), () => LuminPackSerializer.SerializeJson<P>(map, output), new[] { 3, 2 });

                void Check<T>(T[] contents, Action write, int[] prefix)
                {
                    output.ResetCore();
                    QueueFailItem.ThrowOn = -1;
                    write();
                    byte[] complete = output.GetSpan().ToArray();
                    var pool = System.Buffers.ArrayPool<T>.Shared;
                    var expectedLease = pool.Rent(contents.Length);
                    Array.Fill(expectedLease, contents[0]);
                    pool.Return(expectedLease);
                    QueueFailItem.Events.Clear();
                    QueueFailItem.ThrowOn = prefix[1];
                    output.ResetCore();
                    Exception observed = null;
                    try { write(); }
                    catch (Exception error) { observed = error; }
                    finally { QueueFailItem.ThrowOn = -1; }
                    if (!ReferenceEquals(observed, QueueFailItem.Failure) || !QueueFailItem.Events.SequenceEqual(prefix))
                        throw new Exception("JSON tree formatter order or exception changed.");
                    var returned = pool.Rent(contents.Length);
                    try
                    {
                        if (!ReferenceEquals(expectedLease, returned) || returned.Any(item => !EqualityComparer<T>.Default.Equals(item, default)))
                            throw new Exception("JSON tree writer lost its lease or retained references.");
                    }
                    finally { pool.Return(returned, true); }
                    output.ResetCore();
                    write();
                    if (!output.GetSpan().SequenceEqual(complete)) throw new Exception("JSON tree write changed after failure.");
                }
            }
        }
        """;
}
