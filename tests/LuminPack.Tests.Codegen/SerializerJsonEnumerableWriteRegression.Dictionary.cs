using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Preserves complete dictionary-entry state across bounded, streaming and older-runtime paths.</summary>
internal static partial class SerializerJsonEnumerableWriteRegression
{
    /// <summary>Compares the new runtime entry with the original generated operations in all generation modes.</summary>
    /// <param name="references">The current platform and runtime metadata references.</param>
    internal static void RunDictionaryEntries(MetadataReference[] references)
    {
        var runtime = references.Single(reference => Path.GetFileName(reference.Display) == "LuminPack.Core.dll");
        var missing = MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(runtime.Display!, "WriteIntegerEntry"), filePath: "CoreWithoutIntegerEntry.dll");
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var input = CSharpCompilation.Create("DictionaryEntries" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(BuildDictionarySource(mode), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CreateEntryDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var current, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(EntryTrees(driver).Any(text => text.Contains(".WriteIntegerEntry", StringComparison.Ordinal)), "The complete-entry operation was not selected.");
            string actual = ExecuteEntries(current);
            var older = input.ReplaceReference(runtime, missing);
            driver = driver.RunGeneratorsAndUpdateCompilation(older, out var previous, out diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(!EntryTrees(driver).Any(text => text.Contains(".WriteIntegerEntry", StringComparison.Ordinal)), "A missing runtime entry remained selected.");
            Require(EntryTrees(driver).SequenceEqual(EntryTrees(CreateEntryDriver(parse).RunGenerators(older))), "A retained driver kept stale integer-entry capabilities.");
            string expected = ExecuteEntries(previous);
            if (actual != expected)
            {
                var left = expected.Split('\n'); var right = actual.Split('\n');
                int first = Enumerable.Range(0, Math.Min(left.Length,right.Length)).FirstOrDefault(index => left[index] != right[index]);
                throw new InvalidOperationException("Dictionary-entry cursor, prefix, depth or output event changed: " + mode + "\n" + left[first] + "\n" + right[first]);
            }
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out current, out diagnostics);
            Require(EntryTrees(driver).SequenceEqual(EntryTrees(CreateEntryDriver(parse).RunGenerators(input))), "Restoring the runtime retained stale scalar entry operations.");
        }
        Console.WriteLine("Passed 14 dictionary implementations, int/long entries, four profiles and modes, fixed and streaming failures, and retained/fresh optional API transitions.");
    }

    /// <summary>Creates an independent generator driver for metadata-change comparisons.</summary>
    /// <param name="parse">The consumer language and target symbols.</param>
    /// <returns>The fresh driver.</returns>
    private static GeneratorDriver CreateEntryDriver(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Captures stable generated-source identities and text.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>The sorted source snapshot.</returns>
    private static string[] EntryTrees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Runs a compiled consumer against the current runtime, observing the complete original or optimized operation.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    /// <returns>The fixed and streaming operation transcript.</returns>
    private static string ExecuteEntries(Compilation output)
    {
        using var image = new MemoryStream(); var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        return (string)Assembly.Load(image.ToArray()).GetType("DictionaryEntryCases")!.GetMethod("Check")!.Invoke(null,null)!;
    }

    /// <summary>Builds concrete entry calls so profile binding and nested generated paths are exercised.</summary>
    /// <param name="mode">The generation mode under test.</param>
    /// <returns>The complete consumer source.</returns>
    private static string BuildDictionarySource(string mode)
    {
        var shapes = new (string Type,string Factory)[]
        {
            ("Dictionary", "new Dictionary<int,V>(map)"), ("SortedDictionary", "new SortedDictionary<int,V>(map)"),
            ("SortedList", "new SortedList<int,V>(map)"), ("ConcurrentDictionary", "new ConcurrentDictionary<int,V>(map)"),
            ("ReadOnlyDictionary", "new ReadOnlyDictionary<int,V>(map)"), ("ImmutableDictionary", "map.ToImmutableDictionary()"),
            ("ImmutableSortedDictionary", "map.ToImmutableSortedDictionary()"), ("FrozenDictionary", "map.ToFrozenDictionary()"),
            ("IDictionary", "new Dictionary<int,V>(map)"), ("IReadOnlyDictionary", "new Dictionary<int,V>(map)"),
            ("IImmutableDictionary", "map.ToImmutableDictionary()"), ("IDictionary", "new SortedList<int,V>(map)"),
            ("IReadOnlyDictionary", "map.ToFrozenDictionary()"), ("IImmutableDictionary", "map.ToImmutableSortedDictionary()")
        };
        string source = DictionaryEntryFixture.Replace("MODE", mode, StringComparison.Ordinal);
        var models = new System.Text.StringBuilder();
        foreach (string element in new[] { "int", "long" })
        {
            var cases = new System.Text.StringBuilder("switch(shape){");
            for (int index = 0; index < shapes.Length; index++)
            {
                string type = shapes[index].Type + "<int," + element + ">", model = "Model" + index + element;
                models.AppendLine("[LuminPackable] public sealed class " + model + " { public " + type + " Value;public int Tail=91; }");
                cases.AppendLine("case " + index + ": { " + type + " value=" + shapes[index].Factory.Replace("V",element,StringComparison.Ordinal) + ";");
                cases.AppendLine("if(nested){var model=new " + model + "{Value=value};LuminPackSerializer.SerializeJson<P>(ref w,in model);}else LuminPackSerializer.SerializeJson<P>(ref w,in value);break;}");
            }
            cases.Append('}'); source = source.Replace(element == "int" ? "NARROW_CASES" : "WIDE_CASES", cases.ToString(), StringComparison.Ordinal);
        }
        return source + models;
    }

    private const string DictionaryEntryFixture = """
        using System;
        using System.Linq;
        using System.Collections.Generic;
        using System.Collections.Concurrent;
        using System.Collections.Immutable;
        using System.Collections.Frozen;
        using System.Collections.ObjectModel;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        using System.Buffers;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public sealed class Sink:IBufferWriter<byte>
        {
         public readonly Exception Failure=new System.IO.IOException("Selected output failure");
         public readonly List<string> Events=new();public readonly List<byte> Bytes=new();
         private byte[] current;private int step;private readonly int failure;
         public Sink(int fail){failure=fail;}
         public void Advance(int count){Events.Add("advance:"+count);if(++step==failure)throw Failure;Bytes.AddRange(current.AsSpan(0,count).ToArray());}
         public Memory<byte> GetMemory(int hint=0){Events.Add("get:"+hint);if(++step==failure)throw Failure;current=new byte[Math.Max(hint,1)];return current;}
         public Span<byte> GetSpan(int hint=0)=>GetMemory(hint).Span;
        }
        public static class DictionaryEntryCases
        {
         private static readonly System.Text.StringBuilder Transcript=new();
         public static string Check(){Transcript.Clear();Run<LuminPackUtf8LengthOptions>();Run<LuminPackUtf16LengthOptions>();Run<LuminPackUtf8TokenOptions>();Run<LuminPackUtf16TokenOptions>();return Transcript.ToString();}
         static void Run<P>()where P:struct,ILuminPackOptions
         {
          foreach(bool wide in new[]{false,true}) foreach(bool nested in new[]{false,true})
          {
           for(int shape=0;shape<14;shape++)for(int capacity=0;capacity<=96;capacity++)Fixed<P>(shape,wide,nested,capacity);
           for(int shape=0;shape<14;shape++)foreach(int initial in new[]{1,14,26,35,64})for(int fail=0;fail<=5;fail++)Stream<P>(shape,wide,nested,initial,fail);
          }
         }
         static unsafe void Fixed<P>(int shape,bool wide,bool nested,int capacity)where P:struct,ILuminPackOptions
         {
          byte[] storage=Enumerable.Repeat((byte)0xCC,capacity+16).ToArray();string error="ok";
          fixed(byte* pointer=storage)
          {
           Span<byte> span=new(pointer,capacity);var w=new LuminPackJsonWriter(ref span,new LuminPackWriterOptionalState());
           try{Write<P>(ref w,shape,wide,nested,4);}catch(Exception ex){error=ex.GetType().FullName+":"+(ex as ArgumentException)?.ParamName;}
           if(storage.AsSpan(capacity).ContainsAnyExcept((byte)0xCC))throw new Exception("Escaped validated storage");
           Transcript.AppendLine($"{typeof(P).Name},fixed,{shape},{wide},{nested},{capacity},{State(ref w,error)},{Convert.ToBase64String(storage)}");
          }
         }
         static void Stream<P>(int shape,bool wide,bool nested,int initial,int fail)where P:struct,ILuminPackOptions
         {
          var sink=new Sink(fail);var w=default(LuminPackJsonWriter);string error="ok";
          try{w=new LuminPackJsonWriter(sink,new LuminPackWriterOptionalState(),initial);Write<P>(ref w,shape,wide,nested,33);w.FlushToBufferWriter();}
          catch(Exception ex){error=ReferenceEquals(ex,sink.Failure)?"selected":ex.GetType().FullName+":"+(ex as ArgumentException)?.ParamName;}
          Transcript.AppendLine($"{typeof(P).Name},stream,{shape},{wide},{nested},{initial},{fail},{State(ref w,error)},{Convert.ToBase64String(sink.Bytes.ToArray())},{string.Join("/",sink.Events)}");
          w.Dispose();
         }
         static string State(ref LuminPackJsonWriter w,string error)=>$"{error}:{w.CurrentIndex}:{w.BytesWritten}:{w.Depth}:{w.IsFirstElement}:{Convert.ToBase64String(w.GetSpan())}";
         static void Write<P>(ref LuminPackJsonWriter w,int shape,bool wide,bool nested,int count)where P:struct,ILuminPackOptions
         {
          if(wide){var map=Enumerable.Range(0,count).ToDictionary(i=>i==0?int.MinValue:i==1?int.MaxValue:i,i=>i%3==0?long.MinValue:i%3==1?long.MaxValue:-1L);WIDE_CASES}
          else{var map=Enumerable.Range(0,count).ToDictionary(i=>i==0?int.MinValue:i==1?int.MaxValue:i,i=>i%3==0?int.MinValue:i%3==1?int.MaxValue:-1);NARROW_CASES}
         }
        }
        """;
}
