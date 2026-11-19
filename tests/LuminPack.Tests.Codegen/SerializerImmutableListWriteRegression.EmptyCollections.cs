using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks empty immutable construction and mutable reuse without temporary staging in every generation tier.</summary>
    /// <param name="references">The actual runtime and framework references.</param>
    internal static void RunEmptyCollections(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("EmptyCollections" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(EmptyCollectionsSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("EmptyCollectionsEntry")!.GetMethod("Check")!.Invoke(null, null);
        }
        Console.WriteLine("Passed empty collection identity, binary/JSON reuse, strict failures and cold immutable staging checks in four tiers.");
    }

    private const string EmptyCollectionsSource = """
        using Strict8 = global::LuminPack.Option.LuminPackOptions<global::LuminPack.Option.LuminPackUtf8LengthStrings, global::LuminPack.Option.LuminPackGeneralFormat, global::LuminPack.Option.LuminPackDefaultDepth, global::LuminPack.Option.LuminPackStrictJson>;
        using System;
        using System.Buffers;
        using System.Text;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.Concurrent;
        using System.Collections.Immutable;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public struct ColdItem { public int Value; }
        [LuminPackable] public sealed class EmptyGraph
        {
            public ImmutableArray<int> Array;
            public ImmutableList<int> List;
            public IImmutableList<int> ListInterface;
            public ImmutableHashSet<int> Set;
            public IImmutableSet<int> SetInterface;
            public ImmutableSortedSet<int> SortedSet;
            public ImmutableQueue<int> Queue;
            public IImmutableQueue<int> QueueInterface;
            public ImmutableStack<int> Stack;
            public IImmutableStack<int> StackInterface;
            public ImmutableDictionary<string,int> Map;
            public IImmutableDictionary<string,int> MapInterface;
            public ImmutableSortedDictionary<string,int> SortedMap;
        }

        public static class EmptyCollectionsEntry
        {
            public static void Check()
            {
                Run<LuminPackUtf8LengthOptions>(); Run<LuminPackUtf16LengthOptions>(); Run<Strict8>();
            }
            private static void Run<P>() where P:struct,ILuminPackOptions
            {
                var source=new EmptyGraph {
                    Array=ImmutableArray<int>.Empty,List=ImmutableList<int>.Empty,ListInterface=ImmutableList<int>.Empty,
                    Set=ImmutableHashSet<int>.Empty,SetInterface=ImmutableHashSet<int>.Empty,SortedSet=ImmutableSortedSet<int>.Empty,
                    Queue=ImmutableQueue<int>.Empty,QueueInterface=ImmutableQueue<int>.Empty,Stack=ImmutableStack<int>.Empty,StackInterface=ImmutableStack<int>.Empty,
                    Map=ImmutableDictionary<string,int>.Empty,MapInterface=ImmutableDictionary<string,int>.Empty,SortedMap=ImmutableSortedDictionary<string,int>.Empty
                };
                EmptyGraph result=null;
                LuminPackSerializer.Deserialize<EmptyGraph, P>(LuminPackSerializer.Serialize<EmptyGraph, P>(source),ref result); CheckGraph(result);
                LuminPackSerializer.DeserializeJson<EmptyGraph, P>(LuminPackSerializer.SerializeJson<EmptyGraph, P>(source),ref result); CheckGraph(result);
                byte[] empty=Encode<P>("[]"), absent=Encode<P>("null");
                var dictionary=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"A",1}};
                var retained=dictionary;
                LuminPackSerializer.DeserializeJson<Dictionary<string, int>, P>(empty,ref dictionary);
                if(!ReferenceEquals(retained,dictionary)||dictionary.Count!=0||dictionary.Comparer!=StringComparer.OrdinalIgnoreCase)throw new Exception("Dictionary reuse changed");
                var sorted=new SortedDictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"A",1}};
                var retainedSorted=sorted;
                LuminPackSerializer.DeserializeJson<SortedDictionary<string, int>, P>(empty,ref sorted);
                if(!ReferenceEquals(retainedSorted,sorted)||sorted.Count!=0||sorted.Comparer!=StringComparer.OrdinalIgnoreCase)throw new Exception("Sorted dictionary reuse changed");
                var concurrent=new ConcurrentDictionary<string,int>();
                ConcurrentDictionary<string,int> concurrentCopy=null;
                LuminPackSerializer.Deserialize<ConcurrentDictionary<string, int>, P>(LuminPackSerializer.Serialize<ConcurrentDictionary<string, int>, P>(concurrent),ref concurrentCopy);
                if(concurrentCopy is null||concurrentCopy.Count!=0)throw new Exception("Concurrent binary empty changed");
                var bits=new BitArray(0); BitArray bitsCopy=null;
                LuminPackSerializer.Deserialize<BitArray, P>(LuminPackSerializer.Serialize<BitArray, P>(bits),ref bitsCopy);
                LuminPackSerializer.DeserializeJson<BitArray, P>(LuminPackSerializer.SerializeJson<BitArray, P>(bits),ref bitsCopy);
                if(bitsCopy.Length!=0)throw new Exception("BitArray empty changed");

                var coldList=ImmutableList<ColdItem>.Empty;
                var coldMap=ImmutableDictionary<string,ColdItem>.Empty;
                LuminPackSerializer.DeserializeJson<ImmutableList<ColdItem>, P>(absent,ref coldList);
                LuminPackSerializer.DeserializeJson<ImmutableDictionary<string, ColdItem>, P>(absent,ref coldMap);
                Measure(()=>LuminPackSerializer.DeserializeJson<ImmutableList<ColdItem>, P>(empty,ref coldList));
                Measure(()=>LuminPackSerializer.DeserializeJson<ImmutableDictionary<string, ColdItem>, P>(empty,ref coldMap));
                if(!ReferenceEquals(coldList,ImmutableList<ColdItem>.Empty)||!ReferenceEquals(coldMap,ImmutableDictionary<string,ColdItem>.Empty))throw new Exception("Empty identity changed");
                if(default(P).ValidateJsonSyntax)
                {
                    var prior=ImmutableList.Create(7); var failed=prior; bool threw=false;
                    try{LuminPackSerializer.DeserializeJson<ImmutableList<int>, P>(Encode<P>("[1,"),ref failed);}catch(FormatException){threw=true;}
                    if(!threw||!ReferenceEquals(prior,failed))throw new Exception("Strict failure published a replacement");
                }
            }
            private static byte[] Encode<P>(string value)where P:struct,ILuminPackOptions=>(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes(value);
            private static void Measure(Action action)
            {
                long start=GC.GetAllocatedBytesForCurrentThread(); action();
                if(GC.GetAllocatedBytesForCurrentThread()!=start)throw new Exception("Empty read allocated temporary staging");
            }
            private static void CheckGraph(EmptyGraph value)
            {
                if(value.Array.IsDefault||value.Array.Length!=0||!ReferenceEquals(value.List,ImmutableList<int>.Empty)||
                    !ReferenceEquals(value.ListInterface,ImmutableList<int>.Empty)||!ReferenceEquals(value.Set,ImmutableHashSet<int>.Empty)||
                    !ReferenceEquals(value.SetInterface,ImmutableHashSet<int>.Empty)||!ReferenceEquals(value.SortedSet,ImmutableSortedSet<int>.Empty)||
                    !ReferenceEquals(value.Queue,ImmutableQueue<int>.Empty)||!ReferenceEquals(value.QueueInterface,ImmutableQueue<int>.Empty)||
                    !ReferenceEquals(value.Stack,ImmutableStack<int>.Empty)||!ReferenceEquals(value.StackInterface,ImmutableStack<int>.Empty)||
                    !ReferenceEquals(value.Map,ImmutableDictionary<string,int>.Empty)||!ReferenceEquals(value.MapInterface,ImmutableDictionary<string,int>.Empty)||
                    !ReferenceEquals(value.SortedMap,ImmutableSortedDictionary<string,int>.Empty))throw new Exception("Empty graph construction changed");
            }
        }
        """;
}
