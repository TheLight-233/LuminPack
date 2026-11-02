using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerCompiledOptionRegression
{
    /// <summary>Checks array proof propagation, closed element widths, reentry and retained callback invalidation.</summary>
    /// <param name="parse">The consumer language settings.</param>
    /// <param name="references">The actual runtime metadata.</param>
    private static void VerifyArrayInput(CSharpParseOptions parse, MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.IO;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackOption(UntrustedData=true)]
            public class ProofArrays
            {
                public bool[] Flags = Array.Empty<bool>();
                public int[] Values = Array.Empty<int>();
                public bool Tail;
                public static int Calls;
                /*CALLBACK*/
            }
            [LuminPackable, LuminPackOption(UntrustedData=true, MaxObjectCount=2, MaxDepth=2)]
            public class ProofEmptyArray { public bool[] Flags; public bool Tail; }
            [LuminPackable, LuminPackOption(UntrustedData=true)]
            public class ProofArrayWidths
            {
                public byte[] Bytes; public sbyte[] Signed; public short[] Shorts; public ushort[] UShorts;
                public uint[] UInts; public long[] Longs; public ulong[] ULongs; public float[] Floats; public double[] Doubles;
                public char[] Chars; public Guid[] Guids; public TimeSpan[] Durations;
            }
            [LuminPackable, LuminPackOption(UntrustedData=true, MaxObjectCount=1)]
            public class ProofArrayEnvelope
            {
                public int Number;
                public static byte[] ChildBytes;
                public static int Completed;
                [LuminPackOnDeserialized] public void Complete()
                {
                    ProofArrays child = null;
                    LuminPackSerializer.Deserialize(ChildBytes, ref child);
                    if (!child.Flags[0] || child.Values[1] != 7 || !child.Tail) throw new Exception("A proven child charged its reentrant parent.");
                    Completed++;
                }
            }
            public static class Entry
            {
                public static void Check()
                {
                    var source = new ProofArrays { Flags=new[]{true,false,true}, Values=new[]{3,7,11}, Tail=true };
                    byte[] bytes=LuminPackSerializer.Serialize(source);
                    ProofArrays copy=null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    var flags=copy.Flags; var values=copy.Values; var instance=copy;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!ReferenceEquals(instance,copy) || !ReferenceEquals(flags,copy.Flags) || !ReferenceEquals(values,copy.Values) || !copy.Tail)
                        throw new Exception("The array proof changed reuse or following fields.");
                    foreach(int offset in new[]{61,64,bytes.Length-1})
                    {
                        var forged=(byte[])bytes.Clone(); forged[offset]=offset==64 ? (byte)255 : (byte)2;
                        bool failed=false; try{LuminPackSerializer.Deserialize(forged,ref copy);}
                        catch(Exception ex) when(ex is InvalidDataException || offset==64 && ex.GetType().Name=="LuminPackException"){failed=true;}
                        if(!failed)throw new Exception("A Boolean or array header bypassed the guarded fallback.");
                    }
                    foreach(var item in new[]{new ProofArrays(),new ProofArrays{Flags=null,Values=null,Tail=true}})
                    {
                        bytes=LuminPackSerializer.Serialize(item); LuminPackSerializer.Deserialize(bytes,ref copy);
                        if(copy.Flags?.Length!=item.Flags?.Length || copy.Values?.Length!=item.Values?.Length || copy.Tail!=item.Tail)
                            throw new Exception("A null or empty array changed its representation.");
                    }
                    var empty=new ProofEmptyArray{Flags=Array.Empty<bool>(),Tail=true};
                    bytes=LuminPackSerializer.Serialize(empty); ProofEmptyArray emptyCopy=null;
                    LuminPackSerializer.Deserialize(bytes,ref emptyCopy);
                    if(emptyCopy.Flags.Length!=0 || !emptyCopy.Tail)throw new Exception("A conservative depth bound rejected valid empty input.");
                    bytes=LuminPackSerializer.Serialize(new ProofEmptyArray{Flags=new[]{true},Tail=true});
                    bool depthFailed=false;try{LuminPackSerializer.Deserialize(bytes,ref emptyCopy);}catch(InvalidDataException){depthFailed=true;}
                    if(!depthFailed)throw new Exception("An array proof bypassed the original value depth.");
                    var widths=new ProofArrayWidths{Bytes=new byte[]{255},Signed=new sbyte[]{-1},Shorts=new short[]{-17},UShorts=new ushort[]{65535},
                        UInts=new uint[]{uint.MaxValue},Longs=new long[]{long.MinValue},ULongs=new ulong[]{ulong.MaxValue},Floats=new[]{1.5f},Doubles=new[]{2.5},
                        Chars=new[]{'x'},Guids=new[]{new Guid("73dbeead-3c93-4a90-a937-0b6104fe7be1")},Durations=new[]{TimeSpan.FromTicks(123)}};
                    bytes=LuminPackSerializer.Serialize(widths); ProofArrayWidths widthsCopy=null;
                    LuminPackSerializer.Deserialize(bytes,ref widthsCopy);
                    if(widthsCopy.Bytes[0]!=255 || widthsCopy.Signed[0]!=-1 || widthsCopy.Shorts[0]!=-17 || widthsCopy.UShorts[0]!=65535 ||
                        widthsCopy.UInts[0]!=uint.MaxValue || widthsCopy.Longs[0]!=long.MinValue || widthsCopy.ULongs[0]!=ulong.MaxValue ||
                        widthsCopy.Floats[0]!=1.5f || widthsCopy.Doubles[0]!=2.5 || widthsCopy.Chars[0]!='x' || widthsCopy.Guids[0]!=widths.Guids[0] ||
                        widthsCopy.Durations[0]!=widths.Durations[0])throw new Exception("A primitive-array width changed its payload.");
                    ProofArrayEnvelope.ChildBytes=LuminPackSerializer.Serialize(source);
                    bytes=LuminPackSerializer.Serialize(new ProofArrayEnvelope{Number=17}); ProofArrayEnvelope envelope=null;
                    LuminPackSerializer.Deserialize(bytes,ref envelope);
                    if(envelope.Number!=17 || ProofArrayEnvelope.Completed!=1)throw new Exception("The reentrant parent did not complete.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            var original = CSharpSyntaxTree.ParseText(source.Replace("MODE", mode), parse, "ArrayInput.cs");
            var input = TestCompilation.Create("ArrayInput" + mode, [original], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            VerifyArrayInputOutput(ref driver, input, parse, true);
            var callback = CSharpSyntaxTree.ParseText(source.Replace("MODE", mode).Replace("/*CALLBACK*/",
                "[LuminPackOnDeserialized] public void Complete() { Calls++; }"), parse, "ArrayInput.cs");
            VerifyArrayInputOutput(ref driver, input.ReplaceSyntaxTree(original, callback), parse, false);
            VerifyArrayInputOutput(ref driver, input, parse, true);
        }
    }

    /// <summary>Executes the complete retained array output and compares all files with a fresh driver.</summary>
    /// <param name="driver">The retained generator instance.</param><param name="input">The current array contract.</param>
    /// <param name="parse">The exact language settings.</param><param name="proven">Whether callback-free proof code must exist.</param>
    private static void VerifyArrayInputOutput(ref GeneratorDriver driver, Compilation input, CSharpParseOptions parse, bool proven)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(item => item.ToString())));
        var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal).ToArray();
        if (sources.Where(text => text.StartsWith("ProofArrays.Extension", StringComparison.Ordinal)).Any(text => text.Contains(".TrySkipProvenArray(")) != proven)
            throw new InvalidOperationException("The array proof did not track callback invalidation.");
        GeneratorDriver fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        fresh = fresh.RunGenerators(input);
        var reference = fresh.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal);
        if (!sources.SequenceEqual(reference)) throw new InvalidOperationException("The retained array proof differs from fresh output.");
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        if (!emit.Success) throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
