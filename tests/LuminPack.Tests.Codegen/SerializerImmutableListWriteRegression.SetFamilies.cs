using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks public set enumeration and result-only empty and singleton allocations in every generation tier.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void RunSetFamilies(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var compilation = CSharpCompilation.Create("SetFamilies" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(SetFamilySource.Replace("MODE", mode, StringComparison.Ordinal) + HashConstructionSource, parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var result = output.Emit(image);
            Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("SetFamilyEntry")!.GetMethod("Check")!.Invoke(null, null);
        }
        CheckFrozenEnumerationCapability(references);
        CheckHashConstructionCapability(references);
        Console.WriteLine("Passed set family writes, callback failures, derived enumeration, empty Frozen reads, immutable singleton allocations and metadata capability edits in four tiers.");
    }

    /// <summary>Invalidates retained interface dispatch when the consumer's public Frozen enumerator is unavailable.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    private static void CheckFrozenEnumerationCapability(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        var compilation = CSharpCompilation.Create("SetCapability" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText("""
                using System.Collections.Generic;
                using LuminPack;
                public static class SetCapabilityEntry
                {
                    public static byte[] Binary(IReadOnlySet<int> value)=>LuminPackSerializer.Serialize(value);
                    public static string Json(IReadOnlySet<int> value)=>LuminPackSerializer.SerializeJson(value);
                }
                """, parse, "Entry.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var frozen = compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenSet`1")!;
        var original = references.OfType<PortableExecutableReference>().Single(reference =>
            SymbolEqualityComparer.Default.Equals(compilation.GetAssemblyOrModuleSymbol(reference), frozen.ContainingAssembly));
        byte[] metadata = FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "GetEnumerator");
        var changed = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(metadata));
        var driver = Create(parse);
        foreach (var current in new[] { compilation, changed, compilation })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(current, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(Trees(driver).Any(text => text.Contains("WriteFrozenElements", StringComparison.Ordinal)) == ReferenceEquals(current, compilation),
                $"Set enumeration did not follow the actual framework capability: expected {ReferenceEquals(current, compilation)}, metadata {current.GetTypeByMetadataName("System.Collections.Frozen.FrozenSet`1")}, generated {Trees(driver).Any(text => text.Contains("WriteFrozenElements", StringComparison.Ordinal))}.");
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))), "A framework metadata edit retained stale set dispatch.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        }
    }

    private const string SetFamilySource = """
        using System;
        using System.Linq;
        using System.Text;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.Frozen;
        using System.Collections.Immutable;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        using LuminPack.Core;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable(GeneratorType.Custom)] public sealed class SetItem:IComparable<SetItem>
        {
            public int Id;
            public static Action Action;
            public static readonly List<int> Events=new();
            public int CompareTo(SetItem other)=>other is null?1:Id.CompareTo(other.Id);
            public static void Serialize(ref LuminPackWriter writer,in SetItem value){value.Before();writer.Write(value.Id);}
            public static void Deserialize(ref LuminPackReader reader,ref SetItem value){value=new SetItem{Id=reader.Read<int>()};}
            public static void SerializeJson(ref LuminPackJsonWriter writer,in SetItem value)=>WriteJson<LuminPackDefaultOptions>(ref writer,in value);
            public static void DeserializeJson(ref LuminPackJsonReader reader,ref SetItem value)=>ReadJson<LuminPackDefaultOptions>(ref reader,ref value);
            public static void WriteJson<P>(ref LuminPackJsonWriter writer,in SetItem value)where P:struct,ILuminPackOptions{value.Before();writer.Write<P>(value.Id);}
            public static void ReadJson<P>(ref LuminPackJsonReader reader,ref SetItem value)where P:struct,ILuminPackOptions{value=new SetItem{Id=reader.Read<int,P>()};}
            public void Before(){Events.Add(Id);var action=Action;Action=null;action?.Invoke();}
        }
        public sealed class ReversedSet:HashSet<int>,IEnumerable<int>
        {
            public int Visits;
            public ReversedSet():base(new[]{1,2,3}){}
            IEnumerator<int> IEnumerable<int>.GetEnumerator(){Visits++;return ((IEnumerable<int>)new[]{3,2,1}).GetEnumerator();}
        }
        [LuminPackable] public sealed class SetEnvelope { public FrozenSet<int> Items;public int Tail; }
        public static class SetFamilyEntry
        {
            private static object sink;
            public static void Check()
            {
                HashConstructionCases.Check();
                CheckProfile<LuminPackUtf8LengthOptions>();CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8TokenOptions>();CheckProfile<LuminPackUtf16TokenOptions>();
                int[] item={17};
                long hash=Allocated(()=>sink=ImmutableHashSet.Create(item[0]));
                long sorted=Allocated(()=>sink=ImmutableSortedSet.Create(item[0]));
                if(Allocated(()=>sink=LuminTreeStorage.CreateImmutableHashSet(item,1))!=hash ||
                    Allocated(()=>sink=LuminTreeStorage.CreateImmutableSortedSet(item,1))!=sorted)
                    throw new Exception("Singleton storage construction retained temporary allocations.");
                foreach(int[] invalid in new int[][]{null,Array.Empty<int>()})
                    if(Failure(()=>ImmutableSortedSet.CreateRange(new ArraySegment<int>(invalid,0,1)))!=Failure(()=>LuminTreeStorage.CreateImmutableSortedSet(invalid,1)))
                        throw new Exception("The singleton factory changed invalid-buffer diagnostics.");
            }
            private static string Failure(Action action)
            {
                try{action();return "success";}catch(Exception error){return error.GetType().FullName+":"+(error as ArgumentException)?.ParamName;}
            }
            private static long Allocated(Action action)
            {
                for(int warm=0;warm<256;warm++)action();
                long before=GC.GetAllocatedBytesForCurrentThread();for(int repeat=0;repeat<32;repeat++)action();
                return GC.GetAllocatedBytesForCurrentThread()-before;
            }
            private static void CheckProfile<P>()where P:struct,ILuminPackOptions
            {
                unsafe{LuminPackSerializer.Register<SetItem,P>(SetItem.Serialize,SetItem.Deserialize,SetItem.WriteJson<P>,SetItem.ReadJson<P>);}
                using var buffer=new LuminBufferWriter(true);
                ISet<int> emptySorted=new SortedSet<int>();IReadOnlySet<int> emptyReadOnlySorted=(SortedSet<int>)emptySorted;
                if(Allocated(()=>{buffer.ResetWrittenCount();LuminPackSerializer.Serialize<ISet<int>, P>(emptySorted,buffer);})!=0 ||
                   Allocated(()=>{buffer.ResetWrittenCount();LuminPackSerializer.Serialize<IReadOnlySet<int>, P>(emptyReadOnlySorted,buffer);})!=0)
                    throw new Exception("Empty sorted interface writing allocated a traversal stack.");
                buffer.ResetWrittenCount();
                foreach(int count in new[]{0,1,33})
                {
                    int[] items=Enumerable.Range(0,count).ToArray();ISet<int> set=new HashSet<int>(items);
                    IReadOnlySet<int> frozen=items.ToFrozenSet();
                    LuminPackSerializer.SerializeJson<int[], P>(items,buffer);byte[] expected=buffer.GetSpan().ToArray();
                    buffer.ResetWrittenCount();LuminPackSerializer.SerializeJson<ISet<int>, P>(set,buffer);
                    if(!buffer.GetSpan().SequenceEqual(expected))throw new Exception("Hash interface JSON order changed.");
                    if(Allocated(()=>{buffer.ResetWrittenCount();LuminPackSerializer.SerializeJson<ISet<int>, P>(set,buffer);})!=0)
                        throw new Exception("Hash interface JSON boxed its enumerator.");
                    if(Allocated(()=>{buffer.ResetWrittenCount();LuminPackSerializer.SerializeJson<IReadOnlySet<int>, P>(frozen,buffer);})!=0 ||
                       Allocated(()=>{buffer.ResetWrittenCount();LuminPackSerializer.Serialize<IReadOnlySet<int>, P>(frozen,buffer);})!=0)
                        throw new Exception("Frozen interface writing boxed its enumerator.");
                    buffer.ResetWrittenCount();LuminPackSerializer.Serialize<IReadOnlySet<int>, P>(frozen,buffer);IReadOnlySet<int> result=null;
                    LuminPackSerializer.Deserialize<IReadOnlySet<int>, P>(buffer.GetSpan(),ref result);
                    if(!result.SetEquals(items))throw new Exception("Frozen interface binary contents changed.");
                    buffer.ResetWrittenCount();
                }
                var derived=new ReversedSet();ISet<int> derivedInterface=derived;
                LuminPackSerializer.SerializeJson<ISet<int>, P>(derivedInterface,buffer);int[] reversed=null;
                LuminPackSerializer.DeserializeJson<int[], P>(buffer.GetSpan(),ref reversed);
                if(derived.Visits!=1 || !reversed.SequenceEqual(new[]{3,2,1}))throw new Exception("Derived set enumeration was bypassed.");
                buffer.ResetWrittenCount();LuminPackSerializer.Serialize<ISet<int>, P>(derivedInterface,buffer);
                reversed=null;LuminPackSerializer.Deserialize<int[], P>(buffer.GetSpan(),ref reversed);
                if(derived.Visits!=2 || !reversed.SequenceEqual(new[]{3,2,1}))throw new Exception("Derived binary enumeration was bypassed.");
                CheckCallbacks<P>();
                CheckReads<P>();
            }
            private static void CheckCallbacks<P>()where P:struct,ILuminPackOptions
            {
                using var buffer=new LuminBufferWriter(true);
                foreach(bool json in new[]{false,true})foreach(bool readOnly in new[]{false,true})foreach(bool remove in new[]{false,true})
                {
                    var first=new SetItem{Id=1};var second=new SetItem{Id=2};
                    var concrete=new SortedSet<SetItem>{first,second};ISet<SetItem> mutable=concrete;IReadOnlySet<SetItem> view=concrete;
                    var failure=new InvalidOperationException("Callback failure.");
                    SetItem.Events.Clear();SetItem.Action=()=>{if(remove)concrete.Remove(first);else throw failure;};
                    Exception observed=null;buffer.ResetWrittenCount();
                    try
                    {
                        if(readOnly){if(json)LuminPackSerializer.SerializeJson<IReadOnlySet<SetItem>, P>(view,buffer);else LuminPackSerializer.Serialize<IReadOnlySet<SetItem>, P>(view,buffer);}
                        else{if(json)LuminPackSerializer.SerializeJson<ISet<SetItem>, P>(mutable,buffer);else LuminPackSerializer.Serialize<ISet<SetItem>, P>(mutable,buffer);}
                    }
                    catch(Exception error){observed=error;}
                    finally{SetItem.Action=null;}
                    if(observed is not InvalidOperationException || (!remove&&!ReferenceEquals(observed,failure)) || !SetItem.Events.SequenceEqual(new[]{1}))
                        throw new Exception($"Set mutation detection or callback failure changed: JSON {json}, read-only {readOnly}, remove {remove}, error {observed}, events {string.Join(",",SetItem.Events)}.");
                }
            }
            private static void CheckReads<P>()where P:struct,ILuminPackOptions
            {
                byte[] empty=LuminPackSerializer.Serialize<FrozenSet<int>, P>(FrozenSet<int>.Empty);
                byte[] json=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes("[]");
                FrozenSet<int> frozen=null;
                if(Allocated(()=>{LuminPackSerializer.Deserialize<FrozenSet<int>, P>(empty,ref frozen);sink=frozen;})!=0 ||
                   Allocated(()=>{LuminPackSerializer.DeserializeJson<FrozenSet<int>, P>(json,ref frozen);sink=frozen;})!=0 ||
                   !ReferenceEquals(frozen,FrozenSet<int>.Empty))throw new Exception("Empty Frozen reading retained a temporary set.");
                foreach(string text in new[]{"[]","[ ]","[17]","[17,17]","[19,17]"})
                {
                    byte[] payload=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes("{\"Items\":"+text+",\"Tail\":123}");
                    var envelope=new SetEnvelope{Items=new[]{99}.ToFrozenSet(),Tail=-1};
                    LuminPackSerializer.DeserializeJson<SetEnvelope, P>(payload,ref envelope);
                    if(envelope.Tail!=123 || envelope.Items.Contains(99))throw new Exception("Frozen nested consumption or replacement changed.");
                }
                byte[] singleton=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes("[17]");
                ImmutableHashSet<int> hash=null;ImmutableSortedSet<int> sorted=null;IImmutableSet<int> contract=null;
                long hashResult=Allocated(()=>sink=ImmutableHashSet.Create(17));long sortedResult=Allocated(()=>sink=ImmutableSortedSet.Create(17));
                if(Allocated(()=>{hash=null;LuminPackSerializer.DeserializeJson<ImmutableHashSet<int>, P>(singleton,ref hash);sink=hash;})!=hashResult ||
                   Allocated(()=>{sorted=null;LuminPackSerializer.DeserializeJson<ImmutableSortedSet<int>, P>(singleton,ref sorted);sink=sorted;})!=sortedResult ||
                   Allocated(()=>{contract=null;LuminPackSerializer.DeserializeJson<IImmutableSet<int>, P>(singleton,ref contract);sink=contract;})!=hashResult)
                    throw new Exception("Immutable singleton reading retained a builder allocation.");
                if(!hash.SetEquals(new[]{17})||!sorted.SetEquals(new[]{17})||!contract.SetEquals(new[]{17}))throw new Exception("Singleton contents changed.");
            }
        }
        """;
}
