using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerFrameworkEntryRegression
{
    /// <summary>Checks memory JSON loops against their original framing and callback capture order.</summary>
    /// <param name="references">The platform and runtime metadata references.</param>
    internal static void RunMemoryLoops(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("MemoryLoops" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(MemoryLoopSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Execute(output);
            if (mode != "Minimal") continue;
            var owner = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")!.ContainingAssembly;
            var original = references.OfType<PortableExecutableReference>().Single(reference => SymbolEqualityComparer.Default.Equals(compilation.GetAssemblyOrModuleSymbol(reference), owner));
            var changed = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "WriteEnumerationElement")));
            foreach (var current in new[] { changed, compilation })
            {
                driver = driver.RunGeneratorsAndUpdateCompilation(current, out output, out diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                Create(parse).RunGeneratorsAndUpdateCompilation(current, out var fresh, out _);
                Require(output.SyntaxTrees.Select(tree => tree.ToString()).SequenceEqual(fresh.SyntaxTrees.Select(tree => tree.ToString())), "Memory enumeration retained a stale runtime capability.");
                Execute(output);
            }
        }
        Console.WriteLine("Passed memory JSON boundary state, mutable buffer callbacks, pooled writes and runtime capability edits in four tiers and six profiles.");
    }

    private const string MemoryLoopSource = """
        using System;using System.Buffers;using System.Collections.Generic;using System.Linq;using System.Text;using LuminPack;using LuminPack.Attribute;using LuminPack.Core;using LuminPack.Option;using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public static partial class FrameworkEntry
        {
            public static void Check(){for(Shape=0;Shape<4;Shape++){BoundaryProfile<LuminPackUtf8LengthOptions>();BoundaryProfile<LuminPackUtf16LengthOptions>();BoundaryProfile<LuminPackUtf8TokenOptions>();BoundaryProfile<LuminPackUtf16TokenOptions>();BoundaryProfile<LuminPackUtf8VarIntOptions>();BoundaryProfile<LuminPackUtf16VarIntOptions>();}Graph<LuminPackUtf8LengthOptions>();Graph<LuminPackUtf16LengthOptions>();Graph<LuminPackUtf8TokenOptions>();Graph<LuminPackUtf16TokenOptions>();Graph<LuminPackUtf8VarIntOptions>();Graph<LuminPackUtf16VarIntOptions>();Allocation();}
            static void Graph<P>()where P:struct,ILuminPackOptions
            {
                int[] items={-101,3,7,11,-103};long[] longs={-107,long.MinValue,1,long.MaxValue,-109};
                var value=new MemoryEnvelope{Views=new[]{new ReadOnlyMemory<int>(items,1,3),ReadOnlyMemory<int>.Empty},Mutable=new Memory<int>(items,1,3),Segments=CreateSequence(longs.AsSpan(1,3).ToArray()),Slice=new ArraySegment<long>(longs,1,3),Tail=731};
                using var output=new LuminBufferWriter(true);
                foreach(bool json in new[]{false,true})
                {
                    output.ResetCore();if(json)LuminPackSerializer.SerializeJson<MemoryEnvelope, P>(value,output);else LuminPackSerializer.Serialize<MemoryEnvelope, P>(value,output);
                    var bytes=output.GetSpan().ToArray();MemoryEnvelope result=null;
                    if(json)LuminPackSerializer.DeserializeJson<MemoryEnvelope, P>(bytes,ref result);else LuminPackSerializer.Deserialize<MemoryEnvelope, P>(bytes,ref result);
                    int[] storage={-113,0,0,0,-127};result.Mutable=new Memory<int>(storage,1,3);
                    if(json)LuminPackSerializer.DeserializeJson<MemoryEnvelope, P>(bytes,ref result);else LuminPackSerializer.Deserialize<MemoryEnvelope, P>(bytes,ref result);
                    if(result.Tail!=731||!result.Views[0].Span.SequenceEqual(items.AsSpan(1,3))||!result.Views[1].IsEmpty||!result.Mutable.Equals(new Memory<int>(storage,1,3))||storage[0]!=-113||storage[4]!=-127||!result.Mutable.Span.SequenceEqual(items.AsSpan(1,3))||!result.Segments.ToArray().SequenceEqual(longs.AsSpan(1,3).ToArray())||!result.Slice.AsSpan().SequenceEqual(longs.AsSpan(1,3)))throw new Exception("Nested memory graph or slice reuse changed.");
                }
            }
            static void Allocation(){using var buffer=new LuminBufferWriter(true);var source=Enumerable.Range(0,257).ToArray();var value=new ReadOnlyMemory<int>(source);var sequence=CreateSequence(source);for(int i=0;i<128;i++){buffer.ResetCore();LuminPackSerializer.SerializeJson<ReadOnlyMemory<int>, LuminPackUtf8LengthOptions>(value,buffer);buffer.ResetCore();LuminPackSerializer.SerializeJson<ReadOnlySequence<int>, LuminPackUtf8LengthOptions>(sequence,buffer);}long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<64;i++){buffer.ResetCore();LuminPackSerializer.SerializeJson<ReadOnlyMemory<int>, LuminPackUtf8LengthOptions>(value,buffer);buffer.ResetCore();LuminPackSerializer.SerializeJson<ReadOnlySequence<int>, LuminPackUtf8LengthOptions>(sequence,buffer);}if(GC.GetAllocatedBytesForCurrentThread()!=start)throw new Exception("Memory JSON writes allocated temporary objects.");}
        }
        [LuminPackable]public sealed class MemoryEnvelope{public ReadOnlyMemory<int>[] Views;public Memory<int> Mutable;public ReadOnlySequence<long> Segments;public ArraySegment<long> Slice;public int Tail;}
        public sealed class MemorySegment<T>:ReadOnlySequenceSegment<T>{public MemorySegment(Memory<T> value){Memory=value;}public MemorySegment<T> Add(Memory<T> value){var next=new MemorySegment<T>(value){RunningIndex=RunningIndex+Memory.Length};Next=next;return next;}}

        public static partial class FrameworkEntry
        {
            private static void BoundaryProfile<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                foreach (bool wide in new[] { false, true })
                {
                    for (int capacity = 0; capacity <= 96; capacity++)
                    {
                        string expected = FixedState<TProfile>(capacity,wide,false), actual = FixedState<TProfile>(capacity,wide,true);
                        if (expected != actual) throw new Exception("Fixed integer boundary changed: " + capacity + "/" + wide + "/" + expected + "/" + actual);
                    }
                    foreach (int initial in new[] { 1, 11, 12, 20, 21, 32 })
                    for (int failure = 0; failure <= 3; failure++)
                    {
                        string expected = StreamingState<TProfile>(initial,failure,wide,false), actual = StreamingState<TProfile>(initial,failure,wide,true);
                        if (expected != actual) throw new Exception("Streaming integer boundary changed: " + initial + "/" + failure + "/" + wide);
                    }
                }
            }
            private static unsafe string FixedState<TProfile>(int capacity,bool wide,bool generated) where TProfile : struct, ILuminPackOptions
            {
                byte[] storage = Enumerable.Repeat((byte)0xA5,capacity+16).ToArray();
                fixed (byte* address = storage)
                {
                    var span = new Span<byte>(address,capacity); var writer = new LuminPackJsonWriter(ref span,new LuminPackWriterOptionalState());
                    string error = "success";
                    try { WriteBoundary<TProfile>(ref writer,wide,generated,4); }
                    catch (Exception failure) { error = failure.GetType().FullName + ":" + (failure as ArgumentException)?.ParamName; }
                    if (storage.Skip(capacity).Any(value => value != 0xA5)) throw new Exception("An integer write escaped its provided span.");
                    return State(ref writer,error);
                }
            }
            private static string StreamingState<TProfile>(int initial,int failure,bool wide,bool generated) where TProfile : struct, ILuminPackOptions
            {
                ActiveInts=null;ActiveLongs=null;var sink = new MemorySink(failure); var writer = new LuminPackJsonWriter(sink,new LuminPackWriterOptionalState(),initial);
                string error = "success";
                try
                {
                    try { WriteBoundary<TProfile>(ref writer,wide,generated,600); writer.FlushToBufferWriter(); }
                    catch (Exception exception)
                    {
                        if (exception is System.IO.IOException && !ReferenceEquals(exception,sink.Failure)) throw new Exception("Streaming failure identity changed.");
                        error = exception.GetType().FullName + ":" + (exception as ArgumentException)?.ParamName;
                    }
                    return State(ref writer,error) + ":" + Convert.ToBase64String(sink.Bytes.ToArray()) + ":" + string.Join(",",sink.Events);
                }
                finally { writer.Dispose(); }
            }
            private static string State(ref LuminPackJsonWriter writer,string error)
                => error + ":" + writer.Position + ":" + writer.BytesWritten + ":" + writer.Depth + ":" + writer.IsFirstElement + ":" + Convert.ToBase64String(writer.Buffer.Slice(0,writer.Position).ToArray());
            public static int Shape;
            public static int[] ActiveInts;
            public static long[] ActiveLongs;
            private static void WriteBoundary<TProfile>(ref LuminPackJsonWriter writer,bool wide,bool generated,int count) where TProfile:struct,ILuminPackOptions
            {
                if(wide)
                {
                    var values=ActiveLongs=Enumerable.Range(0,count).Select(i=>i%3==0?long.MinValue:i%3==1?(long)1:long.MaxValue).ToArray();
                    if(generated)
                    {
                        switch(Shape)
                        {
                            case 0:{var value=new Memory<long>(values);LuminPackSerializer.SerializeJson<Memory<long>, TProfile>(ref writer,in value);break;}
                            case 1:{var value=new ReadOnlyMemory<long>(values);LuminPackSerializer.SerializeJson<ReadOnlyMemory<long>, TProfile>(ref writer,in value);break;}
                            case 2:{var value=new ArraySegment<long>(values);LuminPackSerializer.SerializeJson<ArraySegment<long>, TProfile>(ref writer,in value);break;}
                            case 3:{var value=CreateSequence<long>(values);LuminPackSerializer.SerializeJson<ReadOnlySequence<long>, TProfile>(ref writer,in value);break;}
                        }
                    }
                    else
                    {
                        writer.WriteArrayStart<TProfile>();bool separate=false;
                        if(Shape==0)
                        {
                            foreach(ref var item in values.AsSpan())
                            {
                                if(separate)writer.WriteByteRaw<TProfile>((byte)',');separate=true;writer.SetFirstElement(true);writer.Write<TProfile>(item);
                            }
                        }
                        else
                        {
                            foreach(long item in values)
                            {
                                if(separate)writer.WriteByteRaw<TProfile>((byte)',');separate=true;writer.SetFirstElement(true);writer.Write<TProfile>(item);
                            }
                        }
                        writer.WriteArrayEnd<TProfile>();
                    }
                }
                else
                {
                    var values=ActiveInts=Enumerable.Range(0,count).Select(i=>i%3==0?int.MinValue:i%3==1?(int)1:int.MaxValue).ToArray();
                    if(generated)
                    {
                        switch(Shape)
                        {
                            case 0:{var value=new Memory<int>(values);LuminPackSerializer.SerializeJson<Memory<int>, TProfile>(ref writer,in value);break;}
                            case 1:{var value=new ReadOnlyMemory<int>(values);LuminPackSerializer.SerializeJson<ReadOnlyMemory<int>, TProfile>(ref writer,in value);break;}
                            case 2:{var value=new ArraySegment<int>(values);LuminPackSerializer.SerializeJson<ArraySegment<int>, TProfile>(ref writer,in value);break;}
                            case 3:{var value=CreateSequence<int>(values);LuminPackSerializer.SerializeJson<ReadOnlySequence<int>, TProfile>(ref writer,in value);break;}
                        }
                    }
                    else
                    {
                        writer.WriteArrayStart<TProfile>();bool separate=false;
                        if(Shape==0)
                        {
                            foreach(ref var item in values.AsSpan())
                            {
                                if(separate)writer.WriteByteRaw<TProfile>((byte)',');separate=true;writer.SetFirstElement(true);writer.Write<TProfile>(item);
                            }
                        }
                        else
                        {
                            foreach(int item in values)
                            {
                                if(separate)writer.WriteByteRaw<TProfile>((byte)',');separate=true;writer.SetFirstElement(true);writer.Write<TProfile>(item);
                            }
                        }
                        writer.WriteArrayEnd<TProfile>();
                    }
                }
            }
            public static ReadOnlySequence<T> CreateSequence<T>(T[] values){if(values.Length<2)return new ReadOnlySequence<T>(values);var first=new MemorySegment<T>(values.AsMemory(0,1));var empty=first.Add(Memory<T>.Empty);var last=empty.Add(values.AsMemory(1));return new ReadOnlySequence<T>(first,0,last,last.Memory.Length);}

        }
        public sealed class MemorySink : System.Buffers.IBufferWriter<byte>
        {
            public readonly Exception Failure = new System.IO.IOException("Selected output failure.");
            public readonly List<byte> Bytes = new List<byte>(); public readonly List<string> Events = new List<string>();
            private readonly int failAt; private int requests; private byte[] current;
            public MemorySink(int failAt) { this.failAt=failAt; }
            private byte[] Next(int sizeHint)
            {
                Events.Add("get:"+sizeHint); requests++;
                if(FrameworkEntry.ActiveInts is not null)for(int i=0;i<FrameworkEntry.ActiveInts.Length;i++)FrameworkEntry.ActiveInts[i]=unchecked(FrameworkEntry.ActiveInts[i]+101);
                if(FrameworkEntry.ActiveLongs is not null)for(int i=0;i<FrameworkEntry.ActiveLongs.Length;i++)FrameworkEntry.ActiveLongs[i]=unchecked(FrameworkEntry.ActiveLongs[i]+101);
                if (failAt != 0 && requests == failAt+1) throw Failure;
                return current = new byte[Math.Max(1,sizeHint)];
            }
            public Span<byte> GetSpan(int sizeHint=0) => Next(sizeHint);
            public Memory<byte> GetMemory(int sizeHint=0) => Next(sizeHint);
            public void Advance(int count) { Events.Add("advance:"+count); for(int i=0;i<count;i++)Bytes.Add(current[i]); }
        }
        """;
}
