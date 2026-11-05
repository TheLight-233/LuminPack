using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerFrameworkEntryRegression
{
    /// <summary>Checks portable bit storage, exact optional APIs and retained generator invalidation.</summary>
    /// <param name="references">The actual runtime and framework references.</param>
    internal static void RunBitStorage(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var initial = CSharpCompilation.Create("BitStorage" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(BitStorageSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var owner = initial.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess")!.ContainingAssembly;
            var original = references.OfType<PortableExecutableReference>().Single(reference => SymbolEqualityComparer.Default.Equals(initial.GetAssemblyOrModuleSymbol(reference), owner));
            var cases = new List<(CSharpCompilation Compilation, bool Write, bool Read)> { (initial, true, true) };
            if (mode == "Minimal")
            {
                cases.Add((initial.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "TryWriteBitArrayPayload"))), false, true));
                cases.Add((initial.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "TryCreateBitArrayPayload"))), true, false));
                cases.Add((initial.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "TryWriteBitArrayPayload", "TryCreateBitArrayPayload"))), false, false));
                cases.Add((initial, true, true));
            }
            var driver = Create(parse);
            foreach (var item in cases)
            {
                driver = driver.RunGeneratorsAndUpdateCompilation(item.Compilation, out var output, out var diagnostics);
                Require(!diagnostics.Any(error => error.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                string generated = string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText));
                Require(generated.Contains(".TryWriteBitArrayPayload(", StringComparison.Ordinal) == item.Write, "Bit storage writing retained a stale API capability.");
                Require(generated.Contains(".TryCreateBitArrayPayload(", StringComparison.Ordinal) == item.Read, "Bit storage construction retained a stale API capability.");
                Create(parse).RunGeneratorsAndUpdateCompilation(item.Compilation, out var fresh, out _);
                Require(output.SyntaxTrees.Select(tree => tree.ToString()).SequenceEqual(fresh.SyntaxTrees.Select(tree => tree.ToString())), "Bit storage metadata edits differ from fresh generation.");
                Execute(output);
            }
        }
        Console.WriteLine("Passed portable BitArray binary/JSON wire, publication, four tiers and optional API invalidation.");
    }

    private const string BitStorageSource = """
        using System;using System.Collections;using System.Buffers.Binary;using System.Linq;using LuminPack;using LuminPack.Attribute;using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable]public sealed partial class BitEnvelope{public BitArray Bits;public int Tail;}
        public static partial class FrameworkEntry
        {
            public static void Check()
            {
                foreach(int count in new[]{0,1,7,8,9,31,32,33,63,64,65,127,128,129,257,4096})foreach(int shape in new[]{0,1,2})
                {
                    var bits=new BitArray(count);for(int i=0;i<count;i++)bits[i]=i%7==0||i%11==2;
                    if(shape==1)bits.Not();if(shape==2&&count>3){bits.Length=count-3;bits.Length=count;}
                    var words=new int[(count+31)/32];bits.CopyTo(words,0);var expected=new byte[4+words.Length*4];
                    BinaryPrimitives.WriteInt32LittleEndian(expected,count);System.Runtime.InteropServices.MemoryMarshal.AsBytes(words.AsSpan()).CopyTo(expected.AsSpan(4));
                    byte[] bytes=LuminPackSerializer.Serialize(bits);if(!bytes.SequenceEqual(expected))throw new Exception("Portable CopyTo framing changed.");
                    BitArray value=new BitArray(3,true),original=value;
                    if(LuminPackSerializer.Deserialize(bytes,ref value)!=bytes.Length||ReferenceEquals(value,original))throw new Exception("Portable fresh publication changed.");
                    Same(bits,value);
                    for(int prefix=0;prefix<bytes.Length;prefix+=Math.Max(1,bytes.Length/7))
                    {
                        value=original;bool failed=false;try{LuminPackSerializer.Deserialize(bytes.AsSpan(0,prefix),ref value);}catch{failed=true;}
                        if(!failed||!ReferenceEquals(value,original)||!value[0])throw new Exception("Truncated storage was published.");
                    }
                    var graph=new BitEnvelope{Bits=bits,Tail=719};BitEnvelope decoded=null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(graph),ref decoded);Same(bits,decoded.Bits);if(decoded.Tail!=719)throw new Exception("Nested tail changed.");
                    Json<LuminPackUtf8LengthOptions>(graph);Json<LuminPackUtf16LengthOptions>(graph);Json<LuminPackUtf8TokenOptions>(graph);Json<LuminPackUtf16TokenOptions>(graph);
                }
                BitArray absent=new BitArray(3);LuminPackSerializer.Deserialize(new byte[]{255,255,255,255},ref absent);if(absent!=null)throw new Exception("Null bit framing changed.");
            }
            private static void Json<P>(BitEnvelope source)where P:struct,ILuminPackOptions
            {
                string json=LuminPackSerializer.SerializeJson<P>(source);BitEnvelope value=null;LuminPackSerializer.DeserializeJson<P>(json,ref value);
                Same(source.Bits,value.Bits);if(value.Tail!=719||LuminPackSerializer.SerializeJson<P>(value)!=json)throw new Exception("Bit JSON framing changed.");
            }
            private static void Same(BitArray a,BitArray b){if(a.Length!=b.Length)throw new Exception("Bit length changed.");for(int i=0;i<a.Length;i++)if(a[i]!=b[i])throw new Exception("Bit value changed.");}
        }
        """;
}
