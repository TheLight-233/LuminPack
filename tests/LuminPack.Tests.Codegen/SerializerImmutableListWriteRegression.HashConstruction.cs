using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks exact capability selection and invalidates cached interface operations when runtime metadata changes.</summary>
    /// <param name="references">The actual framework and runtime references.</param>
    private static void CheckHashConstructionCapability(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        var compilation = CSharpCompilation.Create("HashCapabilities" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText("""
                using System;
                using System.Linq;
                using System.Collections.Immutable;
                using LuminPack;
                public static class HashCapabilityEntry
                {
                    public static byte[] Binary(IImmutableSet<int> value)=>LuminPackSerializer.Serialize(value);
                    public static void Read(byte[] bytes,ref IImmutableSet<int> value)=>LuminPackSerializer.Deserialize(bytes,ref value);
                    public static void Json(byte[] bytes,ref IImmutableSet<int> value)=>LuminPackSerializer.DeserializeJson(bytes,ref value);
                    public static string Evidence()
                    {
                        IImmutableSet<int> value=ImmutableHashSet.Create(3,7,11);
                        byte[] bytes=Binary(value);Read(bytes,ref value);
                        Json(System.Text.Encoding.Unicode.GetBytes("[3,7,11]"),ref value);
                        ImmutableSortedSet<int> sorted=null;
                        LuminPackSerializer.DeserializeJson("[3,7,11]",ref sorted);
                        return Convert.ToBase64String(bytes)+":"+string.Join(",",value)+":"+string.Join(",",sorted);
                    }
                }
                """, parse, "Entry.cs")], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var access = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage")!;
        var original = references.OfType<PortableExecutableReference>().Single(reference =>
            SymbolEqualityComparer.Default.Equals(compilation.GetAssemblyOrModuleSymbol(reference), access.ContainingAssembly));
        var changed = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "CreateFrameworkImmutableHashSet", "CreateOrderedSetIfSupported")));
        var driver = Create(parse);
        string? expected = null;
        foreach (var current in new[] { compilation, changed, compilation })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(current, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(Trees(driver).Any(text => text.Contains(".CreateFrameworkImmutableHashSet(", StringComparison.Ordinal)) == ReferenceEquals(current, compilation),
                "Immutable hash construction retained a stale runtime capability.");
            Require(Trees(driver).Any(text => text.Contains(".CreateOrderedSetIfSupported(", StringComparison.Ordinal)) == ReferenceEquals(current, compilation),
                "Ordered JSON set construction retained a stale runtime capability.");
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))), "A metadata edit retained stale immutable hash operations.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            string result = (string)Assembly.Load(image.ToArray()).GetType("HashCapabilityEntry")!.GetMethod("Evidence")!.Invoke(null, null)!;
            expected ??= result;
            Require(result == expected, "Immutable hash capability selection changed wire bytes or enumeration.");
        }
    }

    private const string HashConstructionSource = """
        [LuminPackable] public sealed class HashConstructionEnvelope
        {
            public ImmutableHashSet<int> Hash;
            public IImmutableSet<int> Contract;
            public ImmutableSortedSet<int> Sorted;
            public int Tail;
        }

        [LuminPackable(GeneratorType.Custom)] public sealed class ObservedHash:IEquatable<ObservedHash>
        {
            public int Id;
            public static readonly List<string> Events=new();
            public static int FailAt;
            public bool Equals(ObservedHash other)=>other is not null&&other.Id==Id;
            public override bool Equals(object other)=>other is ObservedHash item&&Equals(item);
            public override int GetHashCode(){Events.Add("hash"+Id);if(Id==FailAt)throw new InvalidOperationException("Selected hash failure");return Id;}
            public static void Serialize(ref LuminPackWriter writer,in ObservedHash value)=>writer.Write(value.Id);
            public static void Deserialize(ref LuminPackReader reader,ref ObservedHash value){value=new ObservedHash{Id=reader.Read<int>()};Events.Add("read"+value.Id);}
            public static void SerializeJson(ref LuminPackJsonWriter writer,in ObservedHash value)=>WriteJson<LuminPackDefaultOptions>(ref writer,in value);
            public static void DeserializeJson(ref LuminPackJsonReader reader,ref ObservedHash value)=>ReadJson<LuminPackDefaultOptions>(ref reader,ref value);
            public static void WriteJson<P>(ref LuminPackJsonWriter writer,in ObservedHash value)where P:struct,ILuminPackOptions=>writer.Write<P>(value.Id);
            public static void ReadJson<P>(ref LuminPackJsonReader reader,ref ObservedHash value)where P:struct,ILuminPackOptions{value=new ObservedHash{Id=reader.Read<int,P>()};Events.Add("read"+value.Id);}
        }
        public static class HashConstructionCases
        {
            private static object sink;
            public static void Check()
            {
                CheckProfile<LuminPackUtf8LengthOptions>();CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8TokenOptions>();CheckProfile<LuminPackUtf16TokenOptions>();
                CheckProfile<HashStrict8>();CheckProfile<HashStrict16>();
                float[] floats={BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),0,float.NaN,BitConverter.Int32BitsToSingle(unchecked((int)0x7fc00001))};
                IImmutableSet<float> decoded=null;
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(floats),ref decoded);
                if(!decoded.Select(BitConverter.SingleToInt32Bits).SequenceEqual(ImmutableHashSet.CreateRange(floats).Select(BitConverter.SingleToInt32Bits)))
                    throw new Exception("Floating equality changed the first stored representation.");
            }
            private static void CheckProfile<P>()where P:struct,ILuminPackOptions
            {
                var encoding=default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode;
                foreach(int[] items in new[]{Array.Empty<int>(),new[]{7},new[]{7,11},new[]{7,7},Enumerable.Range(-8,17).ToArray(),new[]{7,7,11},new[]{11,7,-3}})
                {
                    var original=ImmutableHashSet.Create(999);
                    var reference=ImmutableHashSet.CreateRange(items);
                    byte[] binary=LuminPackSerializer.Serialize<int[], P>(items),json=encoding.GetBytes("["+string.Join(",",items)+"]");
                    ImmutableHashSet<int> hash=original;IImmutableSet<int> contract=original;
                    LuminPackSerializer.Deserialize<IImmutableSet<int>, P>(binary,ref contract);
                    if(!contract.SequenceEqual(reference)||items.Length>1&&ReferenceEquals(contract,original))throw new Exception("Interface binary construction changed.");
                    LuminPackSerializer.Deserialize<ImmutableHashSet<int>, P>(binary,ref hash);
                    if(!hash.SequenceEqual(reference))throw new Exception("Concrete binary construction changed.");
                    var retained=hash;LuminPackSerializer.Deserialize<ImmutableHashSet<int>, P>(LuminPackSerializer.Serialize<ImmutableHashSet<int>, P>(hash),ref hash);
                    if(!ReferenceEquals(retained,hash))throw new Exception("Concrete binary reuse changed.");
                    LuminPackSerializer.DeserializeJson<ImmutableHashSet<int>, P>(json,ref hash);LuminPackSerializer.DeserializeJson<IImmutableSet<int>, P>(json,ref contract);
                    if(!hash.SequenceEqual(reference)||!contract.SequenceEqual(reference)||original.Single()!=999)throw new Exception("JSON immutable hashing changed.");
                    if(items.Length>1&&items.Distinct().Count()==items.Length)
                    {
                        long necessary=Allocated(()=>sink=LuminTreeStorage.CreateImmutableHashSet(items,items.Length));
                        if(Allocated(()=>{LuminPackSerializer.Deserialize<IImmutableSet<int>, P>(binary,ref contract);sink=contract;})!=necessary||
                           Allocated(()=>{LuminPackSerializer.DeserializeJson<IImmutableSet<int>, P>(json,ref contract);sink=contract;})!=necessary||
                           Allocated(()=>{LuminPackSerializer.DeserializeJson<ImmutableHashSet<int>, P>(json,ref hash);sink=hash;})!=necessary)
                            throw new Exception("Framework hash construction retained a temporary builder.");
                    }
                    var graph=new HashConstructionEnvelope{Hash=reference,Contract=reference,Sorted=ImmutableSortedSet.CreateRange(items),Tail=719};
                    HashConstructionEnvelope restored=null;
                    LuminPackSerializer.Deserialize<HashConstructionEnvelope, P>(LuminPackSerializer.Serialize<HashConstructionEnvelope, P>(graph),ref restored);
                    if(!restored.Hash.SequenceEqual(reference)||!restored.Contract.SequenceEqual(reference)||restored.Tail!=719)throw new Exception("Binary hash field changed.");
                    LuminPackSerializer.DeserializeJson<HashConstructionEnvelope, P>(encoding.GetBytes(LuminPackSerializer.SerializeJson<HashConstructionEnvelope, P>(graph)),ref restored);
                    if(!restored.Hash.SequenceEqual(reference)||!restored.Contract.SequenceEqual(reference)||!restored.Sorted.SetEquals(items)||restored.Tail!=719)throw new Exception("JSON hash field changed.");
                }
                long[] collisions={0,0x100000001L,0x200000002L,0x100000001L};
                IImmutableSet<long> collided=null;
                LuminPackSerializer.Deserialize<IImmutableSet<long>, P>(LuminPackSerializer.Serialize<long[], P>(collisions),ref collided);
                if(!collided.SequenceEqual(ImmutableHashSet.CreateRange(collisions)))throw new Exception("Binary collision ordering changed.");
                LuminPackSerializer.DeserializeJson<IImmutableSet<long>, P>(encoding.GetBytes("[0,4294967297,8589934594,4294967297]"),ref collided);
                if(!collided.SequenceEqual(ImmutableHashSet.CreateRange(collisions)))throw new Exception("JSON collision ordering changed.");
                string[] words={"alpha",null,"beta","alpha"};
                IImmutableSet<string> text=null;ImmutableHashSet<string> concreteText=null;
                byte[] textBinary=LuminPackSerializer.Serialize<string[], P>(words),textJson=encoding.GetBytes("[\"alpha\",null,\"beta\",\"alpha\"]");
                string[] decodedWords=null;LuminPackSerializer.Deserialize<string[], P>(textBinary,ref decodedWords);
                LuminPackSerializer.Deserialize<IImmutableSet<string>, P>(textBinary,ref text);LuminPackSerializer.DeserializeJson<ImmutableHashSet<string>, P>(textJson,ref concreteText);
                if(!text.SequenceEqual(ImmutableHashSet.CreateRange(decodedWords))||!concreteText.SequenceEqual(ImmutableHashSet.CreateRange(words)))
                    throw new Exception("String/null hash ordering changed for "+typeof(P).Name);
                var prior=ImmutableHashSet.Create("prior");text=prior;
                string[] expected=System.Buffers.ArrayPool<string>.Shared.Rent(16);System.Buffers.ArrayPool<string>.Shared.Return(expected,true);
                bool failed=false;try{LuminPackSerializer.Deserialize<IImmutableSet<string>, P>(textBinary.AsSpan(0,textBinary.Length-1),ref text);}catch{failed=true;}
                var actual=System.Buffers.ArrayPool<string>.Shared.Rent(16);
                try{if(!failed||!ReferenceEquals(prior,text)||!ReferenceEquals(expected,actual)||actual.Any(x=>x!=null))throw new Exception("Failed string staging lost ownership or clearing.");}
                finally{System.Buffers.ArrayPool<string>.Shared.Return(actual,true);if(!ReferenceEquals(expected,actual))System.Buffers.ArrayPool<string>.Shared.Return(expected,true);}
                CheckCallbacks<P>(encoding);
            }
            private static void CheckCallbacks<P>(Encoding encoding)where P:struct,ILuminPackOptions
            {
                unsafe{LuminPackSerializer.Register<ObservedHash,P>(ObservedHash.Serialize,ObservedHash.Deserialize,ObservedHash.WriteJson<P>,ObservedHash.ReadJson<P>);}
                var values=new[]{new ObservedHash{Id=1},new ObservedHash{Id=2},new ObservedHash{Id=3}};
                byte[] binary=LuminPackSerializer.Serialize<ObservedHash[], P>(values),json=encoding.GetBytes("[1,2,3]");
                foreach(bool isJson in new[]{false,true})
                {
                    IImmutableSet<ObservedHash> original=ImmutableHashSet.Create(new ObservedHash{Id=91});
                    var result=original;ObservedHash.Events.Clear();ObservedHash.FailAt=2;
                    bool failed=false;
                    try{if(isJson)LuminPackSerializer.DeserializeJson<IImmutableSet<ObservedHash>, P>(json,ref result);else LuminPackSerializer.Deserialize<IImmutableSet<ObservedHash>, P>(binary,ref result);}catch(InvalidOperationException error){failed=error.Message=="Selected hash failure";}
                    finally{ObservedHash.FailAt=0;}
                    string expected=isJson?"read1,read2,read3,hash1,hash2":"read1,hash1,read2,hash2";
                    if(!failed||!ReferenceEquals(result,original)||string.Join(",",ObservedHash.Events)!=expected)throw new Exception("User hashing changed callback order or failure publication.");
                }
            }
            private static long Allocated(Action action)
            {
                for(int i=0;i<64;i++)action();long best=long.MaxValue;
                for(int sample=0;sample<3;sample++){long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<32;i++)action();best=Math.Min(best,GC.GetAllocatedBytesForCurrentThread()-start);}return best;
            }
        }
        """;
}
