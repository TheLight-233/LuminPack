using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableQueueConstructionRegression
{
    /// <summary>Checks concrete and interface list factories against actual metadata in both formats and every tier.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void RunLists(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var modern = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var legacy = modern.WithPreprocessorSymbols("NET8_0_OR_GREATER");
            var initial = CSharpCompilation.Create("ListCases" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(ListSource.Replace("MODE", mode, StringComparison.Ordinal), modern, "ListCases.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var (driver, output) = Generate(initial, modern);
            CheckListSelection(driver, output, span: true);
            var compatibility = initial.RemoveAllSyntaxTrees().AddSyntaxTrees(initial.SyntaxTrees.Single().WithRootAndOptions(initial.SyntaxTrees.Single().GetRoot(), legacy));
            var missingApi = compatibility.AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                namespace System.Collections.Immutable
                {
                    public static class ImmutableList
                    {
                        public static ImmutableList<T> Create<T>(params T[] values) => CreateRange(values);
                        public static ImmutableList<T> Create<T>(System.ReadOnlySpan<T> values) where T : struct
                        {
                            var result = ImmutableList<T>.Empty;
                            foreach (var value in values) result = result.Add(value);
                            return result;
                        }
                        public static ImmutableList<T>.Builder CreateBuilder<T>() => ImmutableList<T>.Empty.ToBuilder();
                        public static ImmutableList<T> CreateRange<T>(System.Collections.Generic.IEnumerable<T> values) => ImmutableList<T>.Empty.AddRange(values);
                    }
                }
                """, legacy, "MissingListSpanFactory.cs"));
            var missingModern = missingApi.RemoveAllSyntaxTrees().AddSyntaxTrees(missingApi.SyntaxTrees.Select(tree => tree.WithRootAndOptions(tree.GetRoot(), modern)));
            foreach (var current in new[] { compatibility, missingApi, missingModern, compatibility, initial })
            {
                var parse = (CSharpParseOptions)current.SyntaxTrees.First().Options;
                driver = driver.WithUpdatedParseOptions(parse).RunGeneratorsAndUpdateCompilation(current, out output, out var diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                CheckListSelection(driver, output, span: !ReferenceEquals(current, missingApi) && !ReferenceEquals(current, missingModern));
                var (fresh, _) = Generate(current, parse);
                Require(Trees(driver).SequenceEqual(Trees(fresh)), "A referenced list factory or target-symbol edit retained stale construction.");
            }
        }
        Console.WriteLine("Passed concrete/interface immutable lists, binary/JSON profiles, allocation budgets, four tiers and retained/fresh factory changes.");
    }

    /// <summary>Rejects stale enumerable paths and executes result, failure and allocation invariants.</summary>
    /// <param name="driver">The completed generator run.</param>
    /// <param name="output">The resulting executable compilation.</param>
    /// <param name="span">Whether the referenced public factory has the complete unconstrained signature.</param>
    private static void CheckListSelection(GeneratorDriver driver, Compilation output, bool span)
    {
        string generated = string.Join("\n", Trees(driver));
        Require(generated.Contains("ImmutableList.Create(new global::System.ReadOnlySpan", StringComparison.Ordinal) == span,
            "List span construction does not match the actual factory.");
        Require(generated.Contains("ImmutableList.CreateRange(", StringComparison.Ordinal) != span,
            "Concrete or interface list construction retained the wrong enumerable fallback.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("ListCases")!.GetMethod("Check")!.Invoke(null, null);
    }

    private const string ListSource = """
        using System;
        using System.Linq;
        using System.Text;
        using System.Collections.Immutable;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public sealed class ListGraph
        {
            public ImmutableList<int> List;
            public IImmutableList<int> Contract;
            public int Tail;
        }
        public static class ListCases
        {
            public static void Check()
            {
                CheckProfile<LuminPackUtf8LengthOptions>();
                CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8TokenOptions>();
                CheckProfile<LuminPackUtf16TokenOptions>();
            }
            private static void CheckProfile<P>() where P:struct,ILuminPackOptions
            {
                var encoding=default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode;
                foreach(int count in new[]{0,1,2,3,16,17,257})
                {
                    int[] values=Enumerable.Range(0,count).Select(x=>x*7-31).ToArray();
                    var source=ImmutableList.CreateRange(values);
                    IImmutableList<int> sourceContract=source;
                    byte[] binary=LuminPackSerializer.Serialize<P>(source);
                    byte[] json=encoding.GetBytes(LuminPackSerializer.SerializeJson<P>(source));
                    if(!binary.SequenceEqual(LuminPackSerializer.Serialize<P>(sourceContract)) ||
                        LuminPackSerializer.SerializeJson<P>(source)!=LuminPackSerializer.SerializeJson<P>(sourceContract))
                        throw new Exception("Concrete and interface list bytes differ.");
                    foreach(bool isJson in new[]{false,true})
                    {
                        var original=ImmutableList.Create(999);
                        var restored=original;
                        IImmutableList<int> contract=original;
                        Action read=()=>{
                            if(isJson){LuminPackSerializer.DeserializeJson<P>(json,ref restored);LuminPackSerializer.DeserializeJson<P>(json,ref contract);}
                            else{LuminPackSerializer.Deserialize<P>(binary,ref restored);LuminPackSerializer.Deserialize<P>(binary,ref contract);}
                        };
                        read();
                        if(!restored.SequenceEqual(values)||!contract.SequenceEqual(values)||original[0]!=999)
                            throw new Exception("List reconstruction or immutable reuse changed.");
                        if(!restored.Add(5).SequenceEqual(values.Append(5)) || count>0 && !restored.RemoveAt(0).SequenceEqual(values.Skip(1)))
                            throw new Exception("Reconstructed list mutation changed.");
                        if(count>0 && typeof(ListCases).Assembly.GetType("System.Collections.Immutable.ImmutableList")==null)
                        {
                            long necessary=Allocated(()=>{GC.KeepAlive(ImmutableList.Create(new ReadOnlySpan<int>(values)));GC.KeepAlive(ImmutableList.Create(new ReadOnlySpan<int>(values)));});
                            long actual=Allocated(read);
                            if(actual!=necessary)throw new Exception("List read allocated temporary objects: "+actual+" vs "+necessary+", count="+count+", json="+isJson+", profile="+typeof(P));
                        }
                        var graph=new ListGraph{List=source,Contract=source,Tail=719};
                        ListGraph decoded=null;
                        if(isJson)LuminPackSerializer.DeserializeJson<P>(encoding.GetBytes(LuminPackSerializer.SerializeJson<P>(graph)),ref decoded);
                        else LuminPackSerializer.Deserialize<P>(LuminPackSerializer.Serialize<P>(graph),ref decoded);
                        if(!decoded.List.SequenceEqual(values)||!decoded.Contract.SequenceEqual(values)||decoded.Tail!=719)
                            throw new Exception("List field construction consumed the following field.");
                        restored=original;contract=original;
                        byte[] malformed=isJson?encoding.GetBytes("[1,\"broken\"]"):binary.AsSpan(0,binary.Length-1).ToArray();
                        bool failed=false;
                        try{if(isJson)LuminPackSerializer.DeserializeJson<P>(malformed,ref restored);else LuminPackSerializer.Deserialize<P>(malformed,ref restored);}catch{failed=true;}
                        if(!failed||!ReferenceEquals(restored,original))throw new Exception("A failed concrete list was published.");
                        failed=false;
                        try{if(isJson)LuminPackSerializer.DeserializeJson<P>(malformed,ref contract);else LuminPackSerializer.Deserialize<P>(malformed,ref contract);}catch{failed=true;}
                        if(!failed||!ReferenceEquals(contract,original))throw new Exception("A failed interface list was published.");
                    }
                }
                var text=ImmutableList.Create("", "alpha", "\u65e5\u672c", "\U0001F30D");
                IImmutableList<string> textContract=null;
                ImmutableList<string> textResult=null;
                var textBytes=LuminPackSerializer.Serialize<P>(text);
                LuminPackSerializer.Deserialize<P>(textBytes,ref textResult);
                LuminPackSerializer.Deserialize<P>(textBytes,ref textContract);
                if(!text.SequenceEqual(textResult)||!text.SequenceEqual(textContract))throw new Exception("String list changed.");
                textBytes=encoding.GetBytes(LuminPackSerializer.SerializeJson<P>(text));
                LuminPackSerializer.DeserializeJson<P>(textBytes,ref textResult);
                LuminPackSerializer.DeserializeJson<P>(textBytes,ref textContract);
                if(!text.SequenceEqual(textResult)||!text.SequenceEqual(textContract))throw new Exception("JSON string list changed.");
                ImmutableList<int> absent=null;
                IImmutableList<int> absentContract=ImmutableList.Create(9);
                LuminPackSerializer.Deserialize<P>(LuminPackSerializer.Serialize<P>(absent),ref absentContract);
                if(absentContract!=null)throw new Exception("Null binary list changed.");
                absentContract=ImmutableList.Create(9);
                LuminPackSerializer.DeserializeJson<P>(encoding.GetBytes("null"),ref absentContract);
                if(absentContract!=null)throw new Exception("Null JSON list changed.");
            }
            private static long Allocated(Action action)
            {
                for(int i=0;i<64;i++)action();
                long minimum=long.MaxValue;
                for(int sample=0;sample<3;sample++)
                {
                    long start=GC.GetAllocatedBytesForCurrentThread();
                    for(int i=0;i<32;i++)action();
                    minimum=Math.Min(minimum,GC.GetAllocatedBytesForCurrentThread()-start);
                }
                return minimum;
            }
        }
        """;
}
