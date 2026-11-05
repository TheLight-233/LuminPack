using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks frozen construction, scalar framing and runtime capability invalidation in every tier.</summary>
    /// <param name="references">The actual consumer metadata references.</param>
    internal static void RunFrozen(MetadataReference[] references)
    {
        CheckFrozenConstructionOwnership();
        var parse=new CSharpParseOptions(LanguageVersion.Preview,preprocessorSymbols:["NET8_0_OR_GREATER","NET9_0_OR_GREATER","NET10_0_OR_GREATER"]);
        foreach(string mode in new[]{"Full","Medium","Light","Minimal"})
        {
            var initial=CSharpCompilation.Create("FrozenCases"+Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(FrozenSource.Replace("MODE",mode,StringComparison.Ordinal),parse,"Entry.cs")],references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true,optimizationLevel:OptimizationLevel.Release));
            var access=initial.GetTypeByMetadataName("LuminPack.Utility.LuminFrozenConstruction")!;
            var original=references.OfType<PortableExecutableReference>().Single(r=>SymbolEqualityComparer.Default.Equals(initial.GetAssemblyOrModuleSymbol(r),access.ContainingAssembly));
            var driver=Create(parse);string? expected=null;
            foreach(string? missing in new string?[]{null,"RentDictionary","ReturnDictionary","RentSet","ReturnSet","WritePairs",null})
            {
                var current=missing is null?initial:initial.ReplaceReference(original,MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!,missing)));
                driver=driver.RunGeneratorsAndUpdateCompilation(current,out var output,out var diagnostics);
                Require(!diagnostics.Any(x=>x.Severity==DiagnosticSeverity.Error),string.Join(Environment.NewLine,diagnostics));
                string generated=string.Join("\n",Trees(driver));
                Require(generated.Contains("LuminFrozenConstruction.RentDictionary",StringComparison.Ordinal)==(missing is not ("RentDictionary" or "ReturnDictionary")),"Stale frozen dictionary rental capability.");
                Require(generated.Contains("LuminFrozenConstruction.RentSet",StringComparison.Ordinal)==(missing is not ("RentSet" or "ReturnSet")),"Stale frozen set rental capability.");
                Require(generated.Contains("LuminFrozenConstruction.WritePairs",StringComparison.Ordinal)==(missing!="WritePairs"),"Stale frozen pair capability.");
                Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))),"Frozen retained/fresh generation differs.");
                using var image=new MemoryStream();var emitted=output.Emit(image);
                Require(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics.Where(x=>x.Severity==DiagnosticSeverity.Error)));
                var type=Assembly.Load(image.ToArray()).GetType("FrozenFixture")!;
                string evidence=(string)type.GetMethod("Verify")!.Invoke(null,null)!;
                expected??=evidence;Require(evidence==expected,"Frozen wire, hash callbacks or failed-publication state changed.");
                var prepare=type.GetMethod("Prepare")!.CreateDelegate<Action<int,int>>();
                var write=type.GetMethod("Write")!.CreateDelegate<Action<int,int>>();
                prepare(257,0);
                for(int family=0;family<6;family++)for(int format=0;format<3;format++)
                {
                    for(int warm=0;warm<64;warm++)write(family,format);
                    long best=long.MaxValue;for(int sample=0;sample<3;sample++){long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<64;i++)write(family,format);best=Math.Min(best,GC.GetAllocatedBytesForCurrentThread()-before);}
                    Require(best==0,"Frozen write allocated after warmup.");
                }
            }
        }
        Console.WriteLine("Passed frozen binary/UTF8/UTF16, scalar and custom framing, hash/error ordering, ownership, warmed writes and four-tier capability invalidation.");
    }

    /// <summary>Checks exclusive ownership and that public freezing publishes storage independent of returned builders.</summary>
    private static void CheckFrozenConstructionOwnership()
    {
        var first=LuminPack.Utility.LuminFrozenConstruction.RentDictionary<int,string>(17);
        var nested=LuminPack.Utility.LuminFrozenConstruction.RentDictionary<int,string>(17);
        Require(!ReferenceEquals(first,nested),"Reentrant frozen construction shared a dictionary.");
        first.Add(1,"original");var frozen=System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(first);
        LuminPack.Utility.LuminFrozenConstruction.ReturnDictionary(first);
        var next=LuminPack.Utility.LuminFrozenConstruction.RentDictionary<int,string>(17);
        Require(next.Count==0,"Returned frozen dictionary retained entries.");next.Add(1,"replacement");
        Require(frozen[1]=="original","Returning a builder changed its frozen dictionary.");
        LuminPack.Utility.LuminFrozenConstruction.ReturnDictionary(next);LuminPack.Utility.LuminFrozenConstruction.ReturnDictionary(nested);
        var set=LuminPack.Utility.LuminFrozenConstruction.RentSet<string>(17);set.Add("original");
        var frozenSet=System.Collections.Frozen.FrozenSet.ToFrozenSet(set);
        LuminPack.Utility.LuminFrozenConstruction.ReturnSet(set);
        var nextSet=LuminPack.Utility.LuminFrozenConstruction.RentSet<string>(17);Require(nextSet.Count==0,"Returned frozen set retained entries.");nextSet.Add("replacement");
        Require(frozenSet.Count==1&&frozenSet.Contains("original"),"Returning a builder changed its frozen set.");LuminPack.Utility.LuminFrozenConstruction.ReturnSet(nextSet);
    }

    private const string FrozenSource = """
        using System;using System.Linq;using System.Text;using System.Collections.Generic;using System.Collections.Frozen;using LuminPack;using LuminPack.Attribute;using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public static class FrozenFixture {
        static FrozenFixture(){Register<LuminPackUtf8LengthOptions>();Register<LuminPackUtf16LengthOptions>();}
        static unsafe void Register<P>()where P:struct,ILuminPackOptions=>LuminPackSerializer.Register<FrozenProbe,P>(&FrozenProbe.Serialize,&FrozenProbe.Deserialize,&FrozenProbe.WriteJson<P>,&FrozenProbe.ReadJson<P>);
        static readonly LuminPack.Utility.LuminBufferWriter Output=new LuminPack.Utility.LuminBufferWriter(true);
        static byte[][][] Inputs;
        static FrozenDictionary<int,int> Value0;
        static FrozenSet<int> Value1;
        static FrozenDictionary<string,int> Value2;
        static FrozenSet<string> Value3;
        static FrozenDictionary<long,long> Value4;
        static FrozenSet<long> Value5;
        public static void Prepare(int count,int shape){var ints=Enumerable.Range(0,count).Select(i=>i*7-991).ToArray();var longs=ints.Select(i=>(long)i*1000000007).ToArray();var texts=ints.Select(i=>"key_"+i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Value0=ints.ToFrozenDictionary(x=>x,x=>x^85);
        Value1=ints.ToFrozenSet();
        Value2=texts.ToFrozenDictionary(x=>x,x=>x.Length);
        Value3=texts.ToFrozenSet();
        Value4=longs.ToFrozenDictionary(x=>x,x=>x^85L);
        Value5=longs.ToFrozenSet();
        Inputs=new byte[6][][];for(int family=0;family<6;family++){Inputs[family]=new byte[3][];for(int format=0;format<3;format++){Write(family,format);Inputs[family][format]=Output.GetSpan().ToArray();}}}
        public static void Write(int family,int format){Output.ResetCore();switch(family){
        case 0:if(format==0)LuminPackSerializer.Serialize(Value0,Output);else if(format==1)LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(Value0,Output);else LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(Value0,Output);break;
        case 1:if(format==0)LuminPackSerializer.Serialize(Value1,Output);else if(format==1)LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(Value1,Output);else LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(Value1,Output);break;
        case 2:if(format==0)LuminPackSerializer.Serialize(Value2,Output);else if(format==1)LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(Value2,Output);else LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(Value2,Output);break;
        case 3:if(format==0)LuminPackSerializer.Serialize(Value3,Output);else if(format==1)LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(Value3,Output);else LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(Value3,Output);break;
        case 4:if(format==0)LuminPackSerializer.Serialize(Value4,Output);else if(format==1)LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(Value4,Output);else LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(Value4,Output);break;
        case 5:if(format==0)LuminPackSerializer.Serialize(Value5,Output);else if(format==1)LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(Value5,Output);else LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(Value5,Output);break;
        }}
        public static unsafe object Read(int family,int format){var bytes=Inputs[family][format];fixed(byte* pinned=bytes){switch(family){
        case 0:{FrozenDictionary<int,int> value=null;if(format==0)LuminPackSerializer.Deserialize(bytes,ref value);else if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);return value;}
        case 1:{FrozenSet<int> value=null;if(format==0)LuminPackSerializer.Deserialize(bytes,ref value);else if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);return value;}
        case 2:{FrozenDictionary<string,int> value=null;if(format==0)LuminPackSerializer.Deserialize(bytes,ref value);else if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);return value;}
        case 3:{FrozenSet<string> value=null;if(format==0)LuminPackSerializer.Deserialize(bytes,ref value);else if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);return value;}
        case 4:{FrozenDictionary<long,long> value=null;if(format==0)LuminPackSerializer.Deserialize(bytes,ref value);else if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);return value;}
        case 5:{FrozenSet<long> value=null;if(format==0)LuminPackSerializer.Deserialize(bytes,ref value);else if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);return value;}
        default:throw new Exception("Family");}}}
        public static string Verify(){var report=new StringBuilder();foreach(int count in new[]{0,1,2,17,257,1024}){Prepare(count,0);for(int family=0;family<6;family++)for(int format=0;format<3;format++){Write(family,format);if(count<=257)report.AppendLine(family+":"+format+":"+Convert.ToBase64String(Output.GetSpan().ToArray()));var result=Read(family,format);if((int)result.GetType().GetProperty("Count").GetValue(result)!=count)throw new Exception("Frozen count changed");switch(family){
        case 0:if(!(Value0.All(pair=>((FrozenDictionary<int,int>)result).TryGetValue(pair.Key,out var v)&&v.Equals(pair.Value))))throw new Exception("Frozen lookup changed");break;
        case 1:if(!(((FrozenSet<int>)result).SetEquals(Value1)))throw new Exception("Frozen lookup changed");break;
        case 2:if(!(Value2.All(pair=>((FrozenDictionary<string,int>)result).TryGetValue(pair.Key,out var v)&&v.Equals(pair.Value))))throw new Exception("Frozen lookup changed");break;
        case 3:if(!(((FrozenSet<string>)result).SetEquals(Value3)))throw new Exception("Frozen lookup changed");break;
        case 4:if(!(Value4.All(pair=>((FrozenDictionary<long,long>)result).TryGetValue(pair.Key,out var v)&&v.Equals(pair.Value))))throw new Exception("Frozen lookup changed");break;
        case 5:if(!(((FrozenSet<long>)result).SetEquals(Value5)))throw new Exception("Frozen lookup changed");break;
        }}}VerifySpecial(report);return report.ToString();}
        static void VerifySpecial(StringBuilder report){
        foreach(int format in new[]{1,2})foreach(string json in new[]{"null","[]","[[1,2],[1,3]]","[[1,2],[3,4]","[[1,2],[3,invalid]]","[null,[1,2],[3,4],{}]"}){FrozenDictionary<int,int> value=new Dictionary<int,int>{{9,19}}.ToFrozenDictionary();var old=value;string outcome="ok";var bytes=(format==1?Encoding.UTF8:Encoding.Unicode).GetBytes(json);try{if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);}catch(Exception e){outcome=e.GetType().Name;}report.AppendLine(json+":"+format+":"+outcome+":"+ReferenceEquals(old,value)+":"+(value==null?"null":string.Join(",",value.Select(p=>p.Key+"="+p.Value))));}
        foreach(int format in new[]{1,2})foreach(string json in new[]{"[]","[1,2,1]","[1,2,invalid]","[1,2,","[1,{},2]"}){FrozenSet<int> value=new[]{9}.ToFrozenSet();var old=value;string outcome="ok";var bytes=(format==1?Encoding.UTF8:Encoding.Unicode).GetBytes(json);try{if(format==1)LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(bytes,ref value);else LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(bytes,ref value);}catch(Exception e){outcome=e.GetType().Name;}report.AppendLine(json+":"+format+":"+outcome+":"+ReferenceEquals(old,value)+":"+string.Join(",",value));}
        var nullable=new int?[]{1,null,3}.ToFrozenSet();var b=LuminPackSerializer.Serialize(nullable);FrozenSet<int?> n=null;LuminPackSerializer.Deserialize(b,ref n);if(!n.SetEquals(nullable))throw new Exception("Nullable set framing changed");report.AppendLine(Convert.ToBase64String(b));
        var oldSet=(FrozenSet<int>)Read(1,0);var oldMap=(FrozenDictionary<int,int>)Read(0,0);var oldItems=oldSet.ToArray();var oldPairs=oldMap.ToArray();Prepare(3,0);for(int i=0;i<16;i++){Read(1,0);Read(0,0);}if(!oldSet.SetEquals(oldItems)||oldMap.Count!=oldPairs.Length||oldPairs.Any(p=>oldMap[p.Key]!=p.Value))throw new Exception("Returned builders mutated published frozen results");
        CheckProbes<LuminPackUtf8LengthOptions>(report);CheckProbes<LuminPackUtf16LengthOptions>(report);
        }
        static void CheckProbes<P>(StringBuilder report)where P:struct,ILuminPackOptions{
        var set=Enumerable.Range(1,5).Select(i=>new FrozenProbe{Id=i}).ToFrozenSet();var map=set.ToFrozenDictionary(k=>k,k=>k.Id);
        foreach(bool json in new[]{false,true})foreach(bool fail in new[]{false,true}){
        byte[] setBytes,mapBytes;Output.ResetCore();if(json)LuminPackSerializer.SerializeJson<P>(set,Output);else LuminPackSerializer.Serialize<P>(set,Output);setBytes=Output.GetSpan().ToArray();Output.ResetCore();if(json)LuminPackSerializer.SerializeJson<P>(map,Output);else LuminPackSerializer.Serialize<P>(map,Output);mapBytes=Output.GetSpan().ToArray();
        FrozenProbe.Events.Clear();FrozenProbe.ThrowOn=fail?2:-1;var copySet=set;try{if(json)LuminPackSerializer.DeserializeJson<P>(setBytes,ref copySet);else LuminPackSerializer.Deserialize<P>(setBytes,ref copySet);}catch(Exception e){FrozenProbe.Events.Append(e.GetType().Name);}report.AppendLine(FrozenProbe.Events+":"+ReferenceEquals(set,copySet));
        FrozenProbe.Events.Clear();var copyMap=map;try{if(json)LuminPackSerializer.DeserializeJson<P>(mapBytes,ref copyMap);else LuminPackSerializer.Deserialize<P>(mapBytes,ref copyMap);}catch(Exception e){FrozenProbe.Events.Append(e.GetType().Name);}report.AppendLine(FrozenProbe.Events+":"+ReferenceEquals(map,copyMap));FrozenProbe.ThrowOn=-1;
        }}
        }
        [LuminPackable(GeneratorType.Custom)] public struct FrozenProbe:IEquatable<FrozenProbe>{
        public int Id;public static readonly StringBuilder Events=new StringBuilder();public static int ThrowOn=-1;
        public bool Equals(FrozenProbe other){Events.Append('E').Append(Id).Append(':').Append(other.Id).Append(';');return Id==other.Id;}
        public override bool Equals(object value)=>value is FrozenProbe p&&Equals(p);
        public override int GetHashCode(){Events.Append('H').Append(Id).Append(';');return 0;}
        static void Observed(int id){Events.Append('R').Append(id).Append(';');if(id==ThrowOn)throw new InvalidOperationException("read callback");}
        public static void Serialize(ref LuminPack.Core.LuminPackWriter writer,in FrozenProbe value)=>writer.Write(value.Id);
        public static void Deserialize(ref LuminPack.Core.LuminPackReader reader,ref FrozenProbe value){int id=reader.Read<int>();Observed(id);value=new FrozenProbe{Id=id};}
        public static void WriteJson<P>(ref LuminPack.Core.LuminPackJsonWriter writer,in FrozenProbe value)where P:struct,ILuminPackOptions=>writer.Write<P>(value.Id);
        public static void ReadJson<P>(ref LuminPack.Core.LuminPackJsonReader reader,ref FrozenProbe value)where P:struct,ILuminPackOptions{int id=reader.Read<int,P>();Observed(id);value=new FrozenProbe{Id=id};}
        }
        """;
}
