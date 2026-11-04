using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks sequence enumeration, observable callbacks and allocation behavior in every generation tier.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void RunSequenceFamilies(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("SequenceFamilies" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(SequenceFamilySource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            foreach (var method in output.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.Identifier.ValueText == "RootWrite" && method.ParameterList.Parameters.Count == 2 &&
                    method.ParameterList.Parameters[1].Type!.ToString() == "global::System.Collections.Generic.IEnumerable<int>"))
            {
                int states = method.Body!.DescendantNodes(node => node is not LocalFunctionStatementSyntax).OfType<VariableDeclarationSyntax>()
                    .Count(declaration => declaration.Type.ToString().EndsWith("LuminPackWriter", StringComparison.Ordinal));
                Require(states <= 2, "Sequence dispatch reserved a separate complete writer for each mutually exclusive helper.");
            }
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("SequenceFamilyEntry")!.GetMethod("Check")!.Invoke(null, null);
        }
        CheckSequenceCapabilities(references, parse);
        Console.WriteLine("Passed sequence family traversal, observable notifications and reuse, builder fields, allocations and optional runtime metadata edits in four tiers.");
    }

    /// <summary>Checks that old runtime metadata retains public traversal and observable insertion fallbacks.</summary>
    /// <param name="references">The original framework references.</param>
    /// <param name="parse">The consuming language options.</param>
    private static void CheckSequenceCapabilities(MetadataReference[] references, CSharpParseOptions parse)
    {
        var compilation = CSharpCompilation.Create("SequenceCapabilities" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText("""
                using System.Collections.Generic;
                using System.Collections.ObjectModel;
                using LuminPack;
                public static class SequenceCapabilityEntry
                {
                    public static byte[] Binary(IEnumerable<int> value)=>LuminPackSerializer.Serialize(value);
                    public static string Json(Collection<int> value)=>LuminPackSerializer.SerializeJson(value);
                    public static void Read(byte[] bytes,ref ObservableCollection<int> value)=>LuminPackSerializer.DeserializeJson(bytes,ref value);
                }
                """, parse, "Entry.cs")], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess")!;
        var original = references.OfType<PortableExecutableReference>().Single(reference =>
            SymbolEqualityComparer.Default.Equals(compilation.GetAssemblyOrModuleSymbol(reference), access.ContainingAssembly));
        var metadata = FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "GetSequenceStorage", "AppendObservable");
        var changed = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(metadata));
        var driver = Create(parse);
        foreach (var current in new[] { compilation, changed, compilation })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(current, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            foreach (string name in new[] { "GetSequenceStorage", "AppendObservable" })
                Require(Trees(driver).Any(text => text.Contains(name, StringComparison.Ordinal)) == ReferenceEquals(current, compilation),
                    "Sequence dispatch retained a stale runtime capability: " + name);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))), "A runtime metadata edit retained stale sequence operations.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        }
    }

    private const string SequenceFamilySource = """
        using System;
        using System.Linq;
        using System.Text;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.ObjectModel;
        using System.Collections.Specialized;
        using System.ComponentModel;
        using System.Runtime.CompilerServices;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public sealed class SequenceEnvelope
        {
            public ReadOnlyCollectionBuilder<int> Builder;
            public ObservableCollection<int> Observable;
            public IEnumerable<int> Items;
            public int Tail;
        }
        [LuminPackable(GeneratorType.Custom)] public sealed class SequenceItem
        {
            public int Id;
            public static Action<int> Loaded;
            public static Action Writing;
            public static void Serialize(ref LuminPackWriter writer,in SequenceItem value){Writing?.Invoke();writer.Write(value.Id);}
            public static void Deserialize(ref LuminPackReader reader,ref SequenceItem value){value=new SequenceItem{Id=reader.Read<int>()};Loaded?.Invoke(value.Id);}
            public static void SerializeJson(ref LuminPackJsonWriter writer,in SequenceItem value)=>WriteJson<LuminPackDefaultOptions>(ref writer,in value);
            public static void DeserializeJson(ref LuminPackJsonReader reader,ref SequenceItem value)=>ReadJson<LuminPackDefaultOptions>(ref reader,ref value);
            public static void WriteJson<P>(ref LuminPackJsonWriter writer,in SequenceItem value)where P:struct,ILuminPackOptions{Writing?.Invoke();writer.Write<P>(value.Id);}
            public static void ReadJson<P>(ref LuminPackJsonReader reader,ref SequenceItem value)where P:struct,ILuminPackOptions{value=new SequenceItem{Id=reader.Read<int,P>()};Loaded?.Invoke(value.Id);}
        }
        public sealed class ReversedSequence:List<int>,IEnumerable<int>
        {
            public int Visits;
            public ReversedSequence():base(new[]{1,2,3}){}
            IEnumerator<int> IEnumerable<int>.GetEnumerator(){Visits++;return ((IEnumerable<int>)new[]{3,2,1}).GetEnumerator();}
        }
        public sealed class ObservedSequence:ObservableCollection<SequenceItem>
        {
            public int Inserts;
            protected override void InsertItem(int index,SequenceItem item){Inserts++;base.InsertItem(index,item);}
        }
        public static class SequenceFamilyEntry
        {
            public static void Check()
            {
                CheckProfile<LuminPackUtf8LengthOptions>();CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8TokenOptions>();CheckProfile<LuminPackUtf16TokenOptions>();
            }
            private static long Allocated(Action action)
            {
                for(int warm=0;warm<256;warm++)action();
                long before=GC.GetAllocatedBytesForCurrentThread();for(int repeat=0;repeat<32;repeat++)action();
                return GC.GetAllocatedBytesForCurrentThread()-before;
            }
            private static void CheckProfile<P>()where P:struct,ILuminPackOptions
            {
                unsafe{LuminPackSerializer.Register<SequenceItem,P>(&SequenceItem.Serialize,&SequenceItem.Deserialize,&SequenceItem.WriteJson<P>,&SequenceItem.ReadJson<P>);}
                using var buffer=new LuminBufferWriter(true);
                foreach(int count in new[]{0,1,33})
                {
                    int[] items=Enumerable.Range(0,count).ToArray();
                    IEnumerable<int>[] sequences={items,items.ToList(),new Queue<int>(items),new Stack<int>(items),new LinkedList<int>(items),
                        new Collection<int>(items.ToList()),new ObservableCollection<int>(items),new ReadOnlyCollection<int>(items),
                        new ReadOnlyObservableCollection<int>(new ObservableCollection<int>(items))};
                    foreach(var sequence in sequences)
                    {
                        if(Allocated(()=>{buffer.ResetCore();LuminPackSerializer.Serialize<P>(sequence,buffer);})!=0 ||
                           Allocated(()=>{buffer.ResetCore();LuminPackSerializer.SerializeJson<P>(sequence,buffer);})!=0)
                            throw new Exception("Sequence writing allocated an enumerator: "+sequence.GetType());
                        foreach(bool json in new[]{false,true})
                        {
                            buffer.ResetCore();if(json)LuminPackSerializer.SerializeJson<P>(sequence,buffer);else LuminPackSerializer.Serialize<P>(sequence,buffer);
                            IEnumerable<int> result=null;if(json)LuminPackSerializer.DeserializeJson<P>(buffer.GetSpan(),ref result);else LuminPackSerializer.Deserialize<P>(buffer.GetSpan(),ref result);
                            if(!result.SequenceEqual(sequence))throw new Exception("Sequence order or interface framing changed.");
                        }
                    }
                    ICollection<int> collection=new LinkedList<int>(items);IList<int> list=new ObservableCollection<int>(items);
                    IReadOnlyCollection<int> readOnly=new Queue<int>(items);IReadOnlyList<int> indexed=new ReadOnlyCollection<int>(items);
                    if(Allocated(()=>{buffer.ResetCore();LuminPackSerializer.Serialize<P>(collection,buffer);LuminPackSerializer.SerializeJson<P>(collection,buffer);})!=0 ||
                       Allocated(()=>{buffer.ResetCore();LuminPackSerializer.Serialize<P>(list,buffer);LuminPackSerializer.SerializeJson<P>(list,buffer);})!=0 ||
                       Allocated(()=>{buffer.ResetCore();LuminPackSerializer.Serialize<P>(readOnly,buffer);LuminPackSerializer.SerializeJson<P>(readOnly,buffer);})!=0 ||
                       Allocated(()=>{buffer.ResetCore();LuminPackSerializer.Serialize<P>(indexed,buffer);LuminPackSerializer.SerializeJson<P>(indexed,buffer);})!=0)
                        throw new Exception("A sequence interface retained traversal allocations.");
                    var envelope=new SequenceEnvelope{Builder=new ReadOnlyCollectionBuilder<int>(items),Observable=new ObservableCollection<int>(items),Items=items,Tail=123};
                    foreach(bool json in new[]{false,true})
                    {
                        buffer.ResetCore();if(json)LuminPackSerializer.SerializeJson<P>(envelope,buffer);else LuminPackSerializer.Serialize<P>(envelope,buffer);
                        SequenceEnvelope result=null;if(json)LuminPackSerializer.DeserializeJson<P>(buffer.GetSpan(),ref result);else LuminPackSerializer.Deserialize<P>(buffer.GetSpan(),ref result);
                        if(result.Tail!=123||!result.Builder.SequenceEqual(items)||!result.Observable.SequenceEqual(items)||!result.Items.SequenceEqual(items))
                            throw new Exception("Nested sequence storage was bound to the wrong value.");
                    }
                }
                var derived=new ReversedSequence();IEnumerable<int> contract=derived;
                foreach(bool json in new[]{false,true})
                {
                    buffer.ResetCore();if(json)LuminPackSerializer.SerializeJson<P>(contract,buffer);else LuminPackSerializer.Serialize<P>(contract,buffer);
                    int[] result=null;if(json)LuminPackSerializer.DeserializeJson<P>(buffer.GetSpan(),ref result);else LuminPackSerializer.Deserialize<P>(buffer.GetSpan(),ref result);
                    if(!result.SequenceEqual(new[]{3,2,1}))throw new Exception("Derived enumeration was bypassed.");
                }
                if(derived.Visits!=2)throw new Exception("Derived public traversal did not run exactly once.");
                var wrapper=new ReadOnlyCollection<int>(derived);buffer.ResetCore();LuminPackSerializer.SerializeJson<P>(wrapper,buffer);
                if(derived.Visits!=3)throw new Exception("A wrapper bypassed its derived backing enumerator.");
                ObservableCollection<int> reuse=new(Enumerable.Range(1,33));
                byte[] input=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes("[1,2,3]");
                if(Allocated(()=>LuminPackSerializer.DeserializeJson<P>(input,ref reuse))!=0||!reuse.SequenceEqual(new[]{1,2,3}))
                    throw new Exception("Unobserved JSON reuse allocated notification payloads.");
                CheckNotifications<P>(input);CheckMutation<P>();
            }
            private static void CheckNotifications<P>(byte[] input)where P:struct,ILuminPackOptions
            {
                foreach(bool json in new[]{false,true})foreach(int scenario in new[]{0,1,2,3,4,5,6})
                {
                    ObservableCollection<SequenceItem> value=scenario==6?new ObservedSequence():new ObservableCollection<SequenceItem>();
                    var events=new List<string>();NotifyCollectionChangedEventHandler collection=(sender,args)=>events.Add("c:"+args.Action);
                    PropertyChangedEventHandler property=(sender,args)=>events.Add("p:"+args.PropertyName);
                    if(scenario==1||scenario==3)value.CollectionChanged+=collection;
                    if(scenario==2||scenario==3)((INotifyPropertyChanged)value).PropertyChanged+=property;
                    SequenceItem.Loaded=id=>{if(scenario==4&&id==1)value.CollectionChanged+=collection;if(scenario==5&&id==2)value.CollectionChanged-=collection;};
                    if(scenario==5)value.CollectionChanged+=collection;
                    byte[] bytes=json?input:LuminPackSerializer.Serialize<P>(new ObservableCollection<SequenceItem>{new(){Id=1},new(){Id=2},new(){Id=3}});
                    try{if(json)LuminPackSerializer.DeserializeJson<P>(bytes,ref value);else LuminPackSerializer.Deserialize<P>(bytes,ref value);}
                    finally{SequenceItem.Loaded=null;}
                    if(!value.Select(item=>item.Id).SequenceEqual(new[]{1,2,3}))throw new Exception("Observable result contents changed.");
                    int collectionCount=events.Count(item=>item.StartsWith("c:"));int propertyCount=events.Count(item=>item.StartsWith("p:"));
                    int expectedCollection=(scenario==1||scenario==3||scenario==5?1:0)+(json?(scenario==1||scenario==3||scenario==4?3:scenario==5?1:0):0);
                    int expectedProperty=(scenario==2||scenario==3?(json?8:2):0);
                    if(collectionCount!=expectedCollection||propertyCount!=expectedProperty)throw new Exception($"Observable notifications changed: {json}/{scenario}/{string.Join(",",events)}.");
                    if(value is ObservedSequence custom&&custom.Inserts!=(json?3:0))throw new Exception("A derived insertion override was bypassed.");
                }
                ObservableCollection<SequenceItem> fresh=null;int freshEvents=0;
                SequenceItem.Loaded=id=>{if(id==1)fresh.CollectionChanged+=(sender,args)=>freshEvents++;};
                try{LuminPackSerializer.DeserializeJson<P>(input,ref fresh);}finally{SequenceItem.Loaded=null;}
                if(freshEvents!=3)throw new Exception("A subscription made by the first item formatter was ignored.");
                var reused=new ObservableCollection<SequenceItem>();int calls=0;
                reused.CollectionChanged+=(sender,args)=>{if(args.Action==NotifyCollectionChangedAction.Add){calls++;reused.Add(new SequenceItem{Id=9});}};
                reused.CollectionChanged+=(sender,args)=>{};
                Exception failure=null;try{LuminPackSerializer.DeserializeJson<P>(input,ref reused);}catch(Exception error){failure=error;}
                if(failure is not InvalidOperationException||calls!=1||reused.Count!=1)throw new Exception("Observable reentrancy checking changed.");
            }
            private static void CheckMutation<P>()where P:struct,ILuminPackOptions
            {
                foreach(bool json in new[]{false,true})
                {
                    var queue=new Queue<SequenceItem>(new[]{new SequenceItem{Id=1},new SequenceItem{Id=2}});IEnumerable<SequenceItem> value=queue;
                    SequenceItem.Writing=()=>{SequenceItem.Writing=null;queue.Enqueue(new SequenceItem{Id=3});};
                    Exception failure=null;using var buffer=new LuminBufferWriter(true);
                    try{if(json)LuminPackSerializer.SerializeJson<P>(value,buffer);else LuminPackSerializer.Serialize<P>(value,buffer);}catch(Exception error){failure=error;}
                    finally{SequenceItem.Writing=null;}
                    if(failure is not InvalidOperationException)throw new Exception("Concrete sequence mutation detection was lost.");
                }
            }
        }
        """;
}
