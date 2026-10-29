using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks compiled profiles on constrained generic definitions and consumer partial helpers.</summary>
internal static partial class SerializerCompiledOptionRegression
{
    /// <summary>Compiles and executes the production facade for closed unmanaged generic contracts.</summary>
    /// <param name="references">The platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            public partial class Owner
            {
                [LuminPackable] public class Value<T> where T : unmanaged { public T Item { get; set; } public T Other { get; set; } }
            }
            public static class Entry
            {
                public static void Check()
                {
                    var value = new Owner.Value<decimal> { Item = 17, Other = 19 };
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    if (LuminPackSerializer.Deserialize<Owner.Value<decimal>>(bytes).Other != 19) throw new System.Exception("Generic default changed.");
                    bytes = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(value);
                    if (LuminPackSerializer.Deserialize<Owner.Value<decimal>, LuminPackUtf16LengthOptions>(bytes).Item != 17) throw new System.Exception("Generic profile changed.");
                    _ = LuminPackSerializer.Serialize(new Owner.Value<int> { Item = 23 });
                }
            }
            namespace LuminPack { public static partial class LuminPackSerializer { public static int Helper() => 0; } }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        Execute("CompiledOptionGeneric", source, parse, references);
        VerifyUnionOptions(parse, references);
        VerifyRegisteredAssemblyDefaults(parse, references);
        VerifyDeclarationConstants(parse, references);
        VerifyVarIntProfiles(parse, references);
        VerifyVarIntIncremental(parse, references);
        Console.WriteLine("Passed constrained generic compiled profiles, partial-helper API selection, registered assembly defaults and option diagnostics.");
    }

    /// <summary>Checks generated variable-length string scopes, lists, Union slots, named versions and bounded reads.</summary>
    /// <param name="parse">The consuming language and framework symbols.</param>
    /// <param name="references">The platform and runtime references.</param>
    private static void VerifyVarIntProfiles(CSharpParseOptions parse, MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [assembly: LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)]
            [LuminPackable] public class Model
            {
                public List<string> Names = new();
                [LuminPackOption(StringRecording = LuminPackStringRecording.Length)] public string Legacy = "a";
                [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)] public string Utf8 = "\u4E2D";
            }
            [LuminPackable, LuminPackUnion(1, typeof(Case))] public abstract partial class Root { public string Name; }
            [LuminPackable] public partial class Case : Root { public List<string> Names = new(); }
            [LuminPackable(GeneratorType.VersionTolerant)] public class Named { public List<string> Names = new(); }
            [LuminPackable(GeneratorType.CircleReference)] public class Circle { [LuminPackOrder(0)] public string Text; [LuminPackOrder(1)] public Circle Next; }
            [LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 32)] public class Bounded { public List<string> Names = new(); public Dictionary<string,string> Map = new(); }
            [LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 32, StringEncoding = LuminPackStringEncoding.UTF8)] public class BoundedUtf8 { public List<string> Names = new(); public Dictionary<string,string> Map = new(); }
            public static class Entry
            {
                public static void Check()
                {
                    var list = Enumerable.Repeat("a",16).ToList();
                    byte[] bytes = LuminPackSerializer.Serialize(list);
                    if(bytes.Length!=36 || LuminPackSerializer.Sizeof(list)!=36)throw new Exception("Variable list size.");
                    List<string> copy=null; LuminPackSerializer.Deserialize(bytes,ref copy);
                    if(!copy.SequenceEqual(list))throw new Exception("Variable list read.");
                    string original=copy[0]; LuminPackSerializer.Deserialize(bytes,ref copy);
                    if(!ReferenceEquals(original,copy[0]))throw new Exception("List string reuse.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf8VarIntOptions>(list);
                    if(bytes.Length!=52 || LuminPackSerializer.Sizeof<List<string>,LuminPackUtf8VarIntOptions>(list)!=52)throw new Exception("UTF-8 variable list size.");
                    if(!LuminPackSerializer.Deserialize<List<string>,LuminPackUtf8VarIntOptions>(bytes).SequenceEqual(list))throw new Exception("UTF-8 variable list read.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(list);
                    if(bytes.Length!=84)throw new Exception("Fixed profile changed.");
                    var model=new Model{Names=list};bytes=LuminPackSerializer.Serialize(model);
                    if(LuminPackSerializer.Sizeof(model)!=bytes.Length || LuminPackSerializer.Deserialize<Model>(bytes).Utf8!=model.Utf8)throw new Exception("Member profile.");
                    Root root=new Case{Name="root",Names=list};bytes=LuminPackSerializer.Serialize(root);
                    if(((Case)LuminPackSerializer.Deserialize<Root>(bytes)).Names.Count!=16 || LuminPackSerializer.Sizeof(root)!=bytes.Length)throw new Exception("Union profile.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf8VarIntOptions>(root);
                    if(((Case)LuminPackSerializer.Deserialize<Root,LuminPackUtf8VarIntOptions>(bytes)).Name!="root")throw new Exception("UTF-8 Union profile.");
                    var named=new Named{Names=list};bytes=LuminPackSerializer.Serialize(named);
                    if(bytes[2]!=5 || LuminPackSerializer.Sizeof(named)!=bytes.Length || LuminPackSerializer.Deserialize<Named>(bytes).Names.Count!=16)throw new Exception("Named profile.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf8VarIntOptions>(named);
                    if(bytes[2]!=4 || LuminPackSerializer.Deserialize<Named,LuminPackUtf8VarIntOptions>(bytes).Names.Count!=16)throw new Exception("Named UTF-8 profile.");
                    var circle=new Circle{Text="circle"};circle.Next=circle;bytes=LuminPackSerializer.Serialize(circle);var restored=LuminPackSerializer.Deserialize<Circle>(bytes);
                    if(!ReferenceEquals(restored,restored.Next)||restored.Text!="circle")throw new Exception("Reference profile.");
                    var bounded=new Bounded{Names=list,Map=new Dictionary<string,string>{{"a","b"}}};bytes=LuminPackSerializer.Serialize(bounded);
                    if(LuminPackSerializer.Deserialize<Bounded>(bytes).Map["a"]!="b")throw new Exception("Bounded profile.");
                    var boundedUtf8=new BoundedUtf8{Names=new List<string>{null,"","\u4E2D","\uD83D\uDE00"},Map=new Dictionary<string,string>{{"\u4E2D","\u00E9"}}};bytes=LuminPackSerializer.Serialize(boundedUtf8);
                    if(LuminPackSerializer.Deserialize<BoundedUtf8>(bytes).Map["\u4E2D"]!="\u00E9")throw new Exception("Bounded UTF-8 profile.");
                    boundedUtf8.Names.Add(new string('a',33));bytes=LuminPackSerializer.Serialize(boundedUtf8);bool rejected=false;try{LuminPackSerializer.Deserialize<BoundedUtf8>(bytes);}catch(Exception){rejected=true;}if(!rejected)throw new Exception("Variable string budget bypassed.");
                    string json=LuminPackSerializer.SerializeJson(model);
                    if(LuminPackSerializer.DeserializeJson<Model>(json).Names.Count!=16)throw new Exception("JSON profile.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
            Execute("CompiledVarInt" + mode, source.Replace("MODE", mode), parse, references);
    }

    /// <summary>Checks that editing string recording invalidates retained output exactly like a fresh driver.</summary>
    /// <param name="parse">The consuming language and framework symbols.</param>
    /// <param name="references">The platform and runtime references.</param>
    private static void VerifyVarIntIncremental(CSharpParseOptions parse, MetadataReference[] references)
    {
        const string source = """
            using LuminPack; using LuminPack.Attribute; using LuminPack.Option;
            [assembly: LuminPackOption(StringRecording = LuminPackStringRecording.Length)]
            [LuminPackable] public class Item { public System.Collections.Generic.List<string> Names = new(); }
            public static class Entry { public static byte[] Write() => LuminPackSerializer.Serialize(new Item()); }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, parse);
        var input = CSharpCompilation.Create("VarIntIncremental", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver retained = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        retained = retained.RunGenerators(input);
        foreach (string recording in new[] { "VarInt", "Length" })
        {
            var edited = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(source.Replace("Recording.Length", "Recording." + recording), parse));
            retained = retained.RunGenerators(edited);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(edited);
            string[] actual = retained.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName + "\n" + s.SourceText).Order().ToArray();
            string[] expected = fresh.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName + "\n" + s.SourceText).Order().ToArray();
            if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Retained variable-length profile output differs from fresh generation.");
        }
    }

    /// <summary>Checks assembly-only component declarations and diagnostics for unsupported constants.</summary>
    /// <param name="parse">The consuming language and framework symbols.</param>
    /// <param name="references">The platform and runtime references.</param>
    private static void VerifyDeclarationConstants(CSharpParseOptions parse, MetadataReference[] references)
    {
        Execute("CompiledOptionEmpty", """
            [assembly: LuminPack.Attribute.LuminPackOption(MaxJsonDepth = int.MinValue, StandardFormat = "G4")]
            """, parse, references, execute: false);
        const string invalid = """
            [assembly: LuminPack.Attribute.LuminPackOption(
                StringEncoding = (LuminPack.Option.LuminPackStringEncoding)42,
                StringRecording = (LuminPack.Option.LuminPackStringRecording)42,
                StandardFormat = "G100")]
            """;
        var input = CSharpCompilation.Create("CompiledOptionInvalid", [CSharpSyntaxTree.ParseText(invalid, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGenerators(input);
        var diagnostics = driver.GetRunResult().Diagnostics;
        if (diagnostics.Count(diagnostic => diagnostic.Id == "LuminPack110") != 3 ||
            diagnostics.Any(diagnostic => diagnostic.Id is "CS8784" or "CS8785"))
            throw new InvalidOperationException("Invalid compiled constants did not produce their three declaration diagnostics.");
    }

    /// <summary>Checks assembly constants at unknown-type pointer, sizing and complete JSON boundaries.</summary>
    /// <param name="parse">The consuming language and framework symbols.</param>
    /// <param name="references">The platform and runtime references.</param>
    private static void VerifyRegisteredAssemblyDefaults(CSharpParseOptions parse, MetadataReference[] references)
    {
        const string source = """
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            [assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16, ValidateJsonSyntax = true, MaxJsonDepth = 32)]
            public sealed class Opaque { public string Text = ""; }
            public readonly struct AssemblyProfile : ILuminPackOptions
            {
                public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
                public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
                public System.Buffers.StandardFormat StandardFormat => new('G');
                public bool ValidateJsonSyntax => true;
                public int MaxJsonDepth => 32;
            }
            public static unsafe class Entry
            {
                public static void Check()
                {
                    LuminPackSerializer.Register<Opaque, AssemblyProfile>(&Write<AssemblyProfile>, &Read<AssemblyProfile>, &WriteJson<AssemblyProfile>, &ReadJson<AssemblyProfile>, &Size<AssemblyProfile>);
                    LuminPackSerializer.Register<Opaque, LuminPackDefaultOptions>(&Write<LuminPackDefaultOptions>, &Read<LuminPackDefaultOptions>, &WriteJson<LuminPackDefaultOptions>, &ReadJson<LuminPackDefaultOptions>, &Size<LuminPackDefaultOptions>);
                    var value = new Opaque { Text = "\u4E2D\uD83D\uDE00" };
                    byte[] expected = LuminPackSerializer.Serialize<string, AssemblyProfile>(value.Text);
                    byte[] bytes = LuminPackSerializer.Serialize<Opaque>(value);
                    if (!System.Linq.Enumerable.SequenceEqual(expected, bytes)) throw new System.Exception("Registered assembly encoding changed.");
                    if (LuminPackSerializer.Deserialize<Opaque>(bytes).Text != value.Text) throw new System.Exception("Registered assembly read changed.");
                    if (LuminPackSerializer.Sizeof<Opaque>(value) != bytes.Length) throw new System.Exception("Registered assembly sizing changed.");
                    string json = LuminPackSerializer.SerializeJson<Opaque>(value);
                    if (LuminPackSerializer.DeserializeJson<Opaque>(json).Text != value.Text) throw new System.Exception("Registered assembly JSON changed.");
                    bytes = LuminPackSerializer.Serialize<Opaque, LuminPackDefaultOptions>(value);
                    if (LuminPackSerializer.Deserialize<Opaque, LuminPackDefaultOptions>(bytes).Text != value.Text) throw new System.Exception("Explicit registered encoding changed.");
                    json = LuminPackSerializer.SerializeJson<Opaque, LuminPackDefaultOptions>(value);
                    if (LuminPackSerializer.DeserializeJson<Opaque, LuminPackDefaultOptions>(json).Text != value.Text) throw new System.Exception("Explicit registered JSON changed.");
                }
                public static void Write<TOptions>(ref LuminPackWriter writer, in Opaque value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.Serialize<string, TOptions>(ref writer, value.Text);
                public static void Read<TOptions>(ref LuminPackReader reader, ref Opaque value) where TOptions : struct, ILuminPackOptions
                {
                    string text = "";
                    LuminPackSerializer.Deserialize<string, TOptions>(ref reader, ref text);
                    value = new() { Text = text };
                }
                public static void WriteJson<TOptions>(ref LuminPackJsonWriter writer, in Opaque value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.SerializeJson<string, TOptions>(ref writer, value.Text);
                public static void ReadJson<TOptions>(ref LuminPackJsonReader reader, ref Opaque value) where TOptions : struct, ILuminPackOptions
                {
                    string text = "";
                    LuminPackSerializer.DeserializeJson<string, TOptions>(ref reader, ref text);
                    value = new() { Text = text };
                }
                public static void Size<TOptions>(ref LuminPackEvaluator evaluator, in Opaque value) where TOptions : struct, ILuminPackOptions
                {
                    string text = value.Text;
                    LuminPackSerializer.Sizeof<string, TOptions>(ref evaluator, ref text);
                }
            }
            """;
        Execute("CompiledOptionRegistered", source, parse, references);
    }

    /// <summary>Compiles and executes one isolated consumer through both production generators.</summary>
    /// <param name="name">The unique dynamic assembly name.</param>
    /// <param name="source">The consumer declarations and executable assertions.</param>
    /// <param name="parse">The consuming language and framework symbols.</param>
    /// <param name="references">The platform and runtime references.</param>
    /// <param name="execute">Whether the fixture also contains an executable Entry.Check assertion.</param>
    private static void Execute(string name, string source, CSharpParseOptions parse, MetadataReference[] references, bool execute = true)
    {
        var input = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        if (Environment.GetEnvironmentVariable("LUMINPACK_INPUT_CAPTURE") is { Length: > 0 } capture)
        {
            string directory = Path.Combine(capture, name);
            Directory.CreateDirectory(directory);
            foreach (var file in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources))
                File.WriteAllText(Path.Combine(directory, file.HintName), file.SourceText.ToString());
        }
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error + "\n" +
            error.Location.SourceTree?.GetText().Lines[error.Location.GetLineSpan().StartLinePosition.Line])));
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        if (!emit.Success) throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
        if (execute) Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
