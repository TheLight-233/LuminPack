using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks dictionary entry closure before following entries and containing-object fields.</summary>
internal static partial class SerializerJsonEnumerableWriteRegression
{
    /// <summary>Runs independent missing-value and nested-array oracles with a bounded execution time.</summary>
    /// <param name="references">The actual platform and runtime metadata.</param>
    internal static void RunDictionaryBoundaries(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var input = CSharpCompilation.Create("DictionaryBoundaries" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(BuildDictionaryBoundarySource(mode), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            CreateEntryDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            ExecuteDictionaryEntriesBounded(output);
        }
        Console.WriteLine("Passed dictionary progress over ignored values, missing values, following entries/fields, nested arrays and reuse in four modes and six profiles.");
    }

    /// <summary>Bounds executable fixture evaluation so a parser progress regression cannot hang the test process.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    /// <returns>The fixture result transcript.</returns>
    private static string ExecuteDictionaryEntriesBounded(Compilation output) =>
        Task.Run(() => ExecuteEntries(output)).WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();

    /// <summary>Builds concrete dictionary shapes with independent expected contents and following fields.</summary>
    /// <param name="mode">The generation mode under test.</param>
    /// <returns>The consumer source.</returns>
    private static string BuildDictionaryBoundarySource(string mode)
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
        var body = new System.Text.StringBuilder(); var models = new System.Text.StringBuilder();
        for (int shape = 0; shape < shapes.Length; shape++)
        {
            foreach (bool arrays in new[] { false, true })
            {
                string value = arrays ? "int[]" : "int", model = "Boundary" + shape + (arrays ? "Arrays" : "Scalar");
                string type = shapes[shape].Type + "<string," + value + ">";
                string factory = shapes[shape].Factory.Replace("<K,V>", "<string," + value + ">", StringComparison.Ordinal);
                body.AppendLine(BoundaryCase.Replace("MODEL", model, StringComparison.Ordinal).Replace("TYPE", type, StringComparison.Ordinal)
                    .Replace("FACTORY", factory, StringComparison.Ordinal).Replace("VALUE", value, StringComparison.Ordinal)
                    .Replace("ARRAYS", arrays ? "true" : "false", StringComparison.Ordinal));
                models.AppendLine("[LuminPackable] public sealed class " + model + " { public " + type + " Map; public int Tail=91; }");
            }
        }
        return BoundaryFixture.Replace("MODE", mode, StringComparison.Ordinal).Replace("BODY", body.ToString(), StringComparison.Ordinal) + models;
    }

    private const string BoundaryFixture = """
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
        using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public readonly struct Strict8:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF8;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        public readonly struct Strict16:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF16;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        public static class DictionaryEntryCases
        {
         public static string Check(){Run<LuminPackUtf8LengthOptions>();Run<LuminPackUtf16LengthOptions>();Run<LuminPackUtf8TokenOptions>();Run<LuminPackUtf16TokenOptions>();Run<Strict8>();Run<Strict16>();return "ok";}
         static void Run<P>()where P:struct,ILuminPackOptions { BODY }
         static string Values<T>(IEnumerable<KeyValuePair<string,T>> map)=>string.Join("|",map.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>pair.Key+":"+(pair.Value is int[] values?string.Join(",",values):pair.Value is null?"null":pair.Value.ToString())));
        }
        """;
    private const string BoundaryCase = """
        {
         foreach(bool reuse in new[]{false,true})
         foreach(bool extra in new[]{false,true})
         foreach(string ignored in new[]{"", "1,", "\"ignored\\u0020value\",", "true,false,null,", "{\"nested\":[[\"hidden\",5]],\"text\":\"skip\"},"})
         {
          var seed=new Dictionary<string,VALUE>();TYPE prior=FACTORY;MODEL target=reuse?new MODEL{Map=prior}:null;
          string second=ARRAYS?"[1,2]":"2";
          string first=extra?(ARRAYS?"[3]":"3")+",9":"";
          string json="{\"Map\":["+ignored+"[\"k\""+(extra?","+first:"")+"],"+ignored+"[\"m\","+second+"]"+(ignored.Length==0?"":","+ignored.TrimEnd(','))+"],\"Tail\":123}";
          byte[] bytes=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes(json);
          LuminPackSerializer.DeserializeJson<P>(bytes,ref target);
          string expected="k:"+(extra?"3":ARRAYS?"null":"0")+"|m:"+(ARRAYS?"1,2":"2");
          if(target.Tail!=123||Values(target.Map)!=expected)throw new Exception("Dictionary entry consumed a following entry or field: MODEL / "+typeof(P).Name+" / "+Values(target.Map));
          if(ignored.Length!=0)
          {
           TYPE empty=reuse?target.Map:null;
           var emptyBytes=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes("["+ignored.TrimEnd(',')+"]");
           LuminPackSerializer.DeserializeJson<P>(emptyBytes,ref empty);
           if(empty is null||Values(empty)!="")throw new Exception("Ignored dictionary values produced entries: MODEL");
          }
         }
         if(default(P).ValidateJsonSyntax)
         foreach(string invalid in new[]{"[1,]", "[{\"nested\":[1,]}]", "[\"\\uZZZZ\"]", "[{\"nested\":1]"})
         {
          TYPE target=null;bool rejected=false;
          var bytes=(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes(invalid);
          try{LuminPackSerializer.DeserializeJson<P>(bytes,ref target);}catch(FormatException){rejected=true;}
          if(!rejected)throw new Exception("Invalid ignored dictionary value was accepted: MODEL / "+invalid);
         }
        }
        """;
}
