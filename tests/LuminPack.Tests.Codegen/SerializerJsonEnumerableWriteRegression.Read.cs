using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks complete scalar entry reads and nested publication across both formats.</summary>
internal static partial class SerializerJsonEnumerableWriteRegression
{
    /// <summary>Compares the new runtime entry with the original generated operations in all generation modes.</summary>
    /// <param name="references">The current platform and runtime metadata references.</param>
    internal static void RunDictionaryReads(MetadataReference[] references)
    {
        var runtime = references.Single(reference => Path.GetFileName(reference.Display) == "LuminPack.Core.dll");
        var missing = MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(runtime.Display!, "TryReadCompleteEntry"), filePath: "CoreWithoutCompleteEntry.dll");
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var input = CSharpCompilation.Create("DictionaryEntries" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(BuildDictionaryReadSource(mode), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CreateEntryDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var current, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(!EntryTrees(driver).Any(text => text.Contains(".TryReadCompleteEntry", StringComparison.Ordinal) || text.Contains(".TryReadEntry", StringComparison.Ordinal)), "Dictionary entries must use the selected key and value formatters independently.");
            string actual = ExecuteDictionaryEntriesBounded(current);
            var older = input.ReplaceReference(runtime, missing);
            driver = driver.RunGeneratorsAndUpdateCompilation(older, out var previous, out diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(!EntryTrees(driver).Any(text => text.Contains(".TryReadCompleteEntry", StringComparison.Ordinal)), "A missing runtime entry remained selected.");
            Require(EntryTrees(driver).SequenceEqual(EntryTrees(CreateEntryDriver(parse).RunGenerators(older))), "A retained driver kept stale complete-entry capabilities.");
            string expected = ExecuteDictionaryEntriesBounded(previous);
            if (actual != expected)
            {
                var left = expected.Split('\n'); var right = actual.Split('\n');
                int first = Enumerable.Range(0, Math.Min(left.Length,right.Length)).FirstOrDefault(index => left[index] != right[index]);
                throw new InvalidOperationException("Dictionary-entry reader state or publication changed: " + mode + "\n" + left[first] + "\n" + right[first]);
            }
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out current, out diagnostics);
            Require(EntryTrees(driver).SequenceEqual(EntryTrees(CreateEntryDriver(parse).RunGenerators(input))), "Restoring the runtime retained stale scalar entry operations.");
        }
        Console.WriteLine("Passed nested dictionary reads, binary controls, six profiles, four modes, and complete-entry API transitions.");
    }

    /// <summary>Builds nested dictionary readers and paired binary controls for each complete scalar entry shape.</summary>
    /// <param name="mode">The generation mode under test.</param>
    /// <returns>The concrete consumer source.</returns>
    private static string BuildDictionaryReadSource(string mode)
    {
        var shapes = new (string Type, string Factory)[]
        {
            ("Dictionary", "new Dictionary<K,V>(seed)"),
            ("SortedDictionary", "new SortedDictionary<K,V>(seed)"),
            ("SortedList", "new SortedList<K,V>(seed)"),
            ("ConcurrentDictionary", "new ConcurrentDictionary<K,V>(seed)"),
            ("ReadOnlyDictionary", "new ReadOnlyDictionary<K,V>(seed)"),
            ("ImmutableDictionary", "seed.ToImmutableDictionary()"),
            ("ImmutableSortedDictionary", "seed.ToImmutableSortedDictionary()"),
            ("FrozenDictionary", "seed.ToFrozenDictionary()"),
            ("IDictionary", "new Dictionary<K,V>(seed)"),
            ("IReadOnlyDictionary", "new Dictionary<K,V>(seed)"),
            ("IImmutableDictionary", "seed.ToImmutableDictionary()"),
            ("IDictionary", "new SortedList<K,V>(seed)"),
            ("IReadOnlyDictionary", "seed.ToFrozenDictionary()"),
            ("IImmutableDictionary", "seed.ToImmutableSortedDictionary()")
        };
        var kinds = new[] { ("string", "int"), ("string", "long"), ("string", "string"), ("int", "string"), ("int", "int") };
        var body = new System.Text.StringBuilder(); var models = new System.Text.StringBuilder();
        for (int kind = 0; kind < kinds.Length; kind++)
        for (int shape = 0; shape < shapes.Length; shape++)
        {
            var (key, value) = kinds[kind]; string model = "Model" + kind + "_" + shape;
            string type = shapes[shape].Type + "<" + key + "," + value + ">";
            string factory = shapes[shape].Factory.Replace("<K,V>", "<" + key + "," + value + ">", StringComparison.Ordinal);
            string text = DictionaryReadCase.Replace("KEYEXPR", key == "string" ? "\"key\"+i" : "i", StringComparison.Ordinal)
                .Replace("VALUEEXPR", value == "string" ? "\"value\"+i" : value == "long" ? "i*3L" : "i*3", StringComparison.Ordinal)
                .Replace("TYPE", type, StringComparison.Ordinal).Replace("FACTORY", factory, StringComparison.Ordinal)
                .Replace("MODEL", model, StringComparison.Ordinal).Replace("KIND", kind.ToString(), StringComparison.Ordinal).Replace("SHAPE", shape.ToString(), StringComparison.Ordinal)
                .Replace("SEEDKEY", key == "string" ? "\"old\"" : "-7", StringComparison.Ordinal).Replace("SEEDVALUE", value == "string" ? "\"previous\"" : "-9", StringComparison.Ordinal)
                .Replace("KEY", key, StringComparison.Ordinal).Replace("VALUE", value, StringComparison.Ordinal);
            body.AppendLine(text); models.AppendLine("[LuminPackable] public sealed class " + model + " { public " + type + " Map; public int Tail=91; }");
        }
        return DictionaryReadFixture.Replace("MODE", mode, StringComparison.Ordinal).Replace("BODY", body.ToString(), StringComparison.Ordinal) + models;
    }

    private const string DictionaryReadFixture = """
        using System;
        using System.Linq;
        using System.Text;
        using System.Buffers;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Collections.Concurrent;
        using System.Collections.ObjectModel;
        using System.Collections.Frozen;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public readonly struct Strict8:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF8;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        public readonly struct Strict16:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF16;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        public static class DictionaryEntryCases
        {
         private static readonly StringBuilder Transcript=new();
         public static void Main()=>Console.Write(Check());
         public static string Check(){Transcript.Clear();Run<LuminPackUtf8LengthOptions>();Run<LuminPackUtf16LengthOptions>();Run<LuminPackUtf8TokenOptions>();Run<LuminPackUtf16TokenOptions>();Run<Strict8>();Run<Strict16>();return Transcript.ToString();}
         static void Run<P>()where P:struct,ILuminPackOptions
         {
          BODY
         }
         static string[] Inputs(int kind)
         {
          string key=kind<3?"\"escaped\\u006bey\"":"1",val=kind==2||kind==3?"\"value\\u0062\"":kind==1?"9223372036854775807":"123";
          return new[]{"[["+key+","+val+"]]","[["+key+","+val+",0]]","[["+key+","+val+",\"x\",[1,{\"a\":2}]],["+key+","+val+"]]","[["+key+"]]","[["+key+",null]]","[["+key+",true]]","[["+key+",3000000000]]","[["+key+",1.25]]","[[true,"+val+"]]","[[\"bad\\q\","+val+"]]","[[\"bad\\uD800\","+val+"]]","[[\"ok\\uD83C\\uDF1F\","+val+"]]","[["+key+",\"bad\\q\"]]","[["+key+",\"bad\\uDC00\"]]","[["+key+",\"ok\\uD83C\\uDF1F\"]]","[["+key+",\"broken\u0001value\"]]","[["+key+","+val+",\"broken\\q\"]]"," [ [ "+key+" , "+val+" ] ] ","null","[]"};
         }
         static string Snapshot<K,V>(IEnumerable<KeyValuePair<K,V>> value) => value==null?"null":string.Join("|",value.Select(pair=>Escape(pair.Key)+":"+Escape(pair.Value)).OrderBy(x=>x,StringComparer.Ordinal));
         static string Escape<T>(T value)=>value==null?"null":Convert.ToBase64String(Encoding.Unicode.GetBytes(value.ToString()));
         static string State(ref LuminPackJsonReader reader,string error)=>$"{error}:{reader.Position}:{reader.Depth}:{reader.CurrentTokenType}:{reader.GrammarState}:{reader.ObjectContainerBits}";
        }
        """;
    private const string DictionaryReadCase = """
        {
         foreach(int count in new[]{0,1,7})
         {
          var seed=Enumerable.Range(0,count).ToDictionary(i=>KEYEXPR,i=>VALUEEXPR);TYPE source=FACTORY;var model=new MODEL{Map=source};
          byte[] bytes=LuminPackSerializer.Serialize<MODEL,P>(model);MODEL fresh=null;LuminPackSerializer.Deserialize<MODEL,P>(bytes,ref fresh);var reuse=new MODEL{Map=FACTORY};LuminPackSerializer.Deserialize<MODEL,P>(bytes,ref reuse);
          if(Snapshot(source)!=Snapshot(fresh.Map)||Snapshot(source)!=Snapshot(reuse.Map))throw new Exception("Binary integration mismatch");
          Transcript.AppendLine($"{typeof(P).Name},binary,KIND,SHAPE,{count},{Convert.ToBase64String(bytes)},{Snapshot(fresh.Map)},{Snapshot(reuse.Map)}");
         }
         foreach(bool reused in new[]{false,true})
         {
          int index=0;foreach(string document in Inputs(KIND))
          {
           var seed=new Dictionary<KEY,VALUE>{[SEEDKEY]=SEEDVALUE};MODEL target=reused?new MODEL{Map=FACTORY}:null;var prior=target;var priorMap=target?.Map;
           byte[] input=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes("{\"Map\":"+document+",\"Tail\":123}");unsafe { fixed (byte* pinnedInput=input) { ReadOnlySpan<byte> span=new(pinnedInput,input.Length);
           var reader=new LuminPackJsonReader(ref span,new LuminPackReaderOptionalState());string error="ok";
           try{LuminPackSerializer.DeserializeJson<MODEL,P>(ref reader,ref target);}catch(Exception ex){error=ex.GetType().FullName+":"+(ex as ArgumentException)?.ParamName;}
           Transcript.AppendLine($"{typeof(P).Name},json,KIND,SHAPE,{reused},{index++},{State(ref reader,error)},{ReferenceEquals(prior,target)},{ReferenceEquals(priorMap,target?.Map)},{target?.Tail},{Snapshot(target?.Map)}");reader.OptionState.Reset(); } }
          }
         }
        }
        """;
}
