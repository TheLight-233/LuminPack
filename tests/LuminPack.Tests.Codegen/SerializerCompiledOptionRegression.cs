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
                    if (global::LuminPackUnitTest.ConcreteReads.Read<Owner.Value<decimal>>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Owner.Value<decimal> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Other != 19) throw new System.Exception("Generic default changed.");
                    bytes = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(value);
                    if (global::LuminPackUnitTest.ConcreteReads.Read<Owner.Value<decimal>>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Owner.Value<decimal> concreteReadTarget) => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(concreteReadInput, ref concreteReadTarget)).Item != 17) throw new System.Exception("Generic profile changed.");
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
            using System.Collections.Immutable;
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
            [LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 32, StringEncoding = LuminPackStringEncoding.UTF8)] public class BoundedImmutable { public ImmutableArray<string> Names = ImmutableArray<string>.Empty; }
            public static class Entry
            {
                public static void Check()
                {
                    var list = Enumerable.Repeat("a",16).ToList();
                    byte[] bytes = LuminPackSerializer.Serialize(list);
                    if(bytes.Length!=36)throw new Exception("Variable list size.");
                    List<string> copy=null; LuminPackSerializer.Deserialize(bytes,ref copy);
                    if(!copy.SequenceEqual(list))throw new Exception("Variable list read.");
                    string original=copy[0]; LuminPackSerializer.Deserialize(bytes,ref copy);
                    if(!ReferenceEquals(original,copy[0]))throw new Exception("List string reuse.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf8VarIntOptions>(list);
                    if(bytes.Length!=52)throw new Exception("UTF-8 variable list size.");
                    if(!global::LuminPackUnitTest.ConcreteReads.Read<List<string>>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref List<string> concreteReadTarget) => LuminPackSerializer.Deserialize<LuminPackUtf8VarIntOptions>(concreteReadInput, ref concreteReadTarget)).SequenceEqual(list))throw new Exception("UTF-8 variable list read.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(list);
                    if(bytes.Length!=84)throw new Exception("Fixed profile changed.");
                    var model=new Model{Names=list};bytes=LuminPackSerializer.Serialize(model);
                    if(global::LuminPackUnitTest.ConcreteReads.Read<Model>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Model concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Utf8!=model.Utf8)throw new Exception("Member profile.");
                    Root root=new Case{Name="root",Names=list};bytes=LuminPackSerializer.Serialize(root);
                    if(((Case)global::LuminPackUnitTest.ConcreteReads.Read<Root>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Root concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))).Names.Count!=16)throw new Exception("Union profile.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf8VarIntOptions>(root);
                    if(((Case)global::LuminPackUnitTest.ConcreteReads.Read<Root>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Root concreteReadTarget) => LuminPackSerializer.Deserialize<LuminPackUtf8VarIntOptions>(concreteReadInput, ref concreteReadTarget))).Name!="root")throw new Exception("UTF-8 Union profile.");
                    var named=new Named{Names=list};bytes=LuminPackSerializer.Serialize(named);
                    if(bytes[2]!=5 || global::LuminPackUnitTest.ConcreteReads.Read<Named>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Named concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Names.Count!=16)throw new Exception("Named profile.");
                    bytes=LuminPackSerializer.Serialize<LuminPackUtf8VarIntOptions>(named);
                    if(bytes[2]!=4 || global::LuminPackUnitTest.ConcreteReads.Read<Named>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Named concreteReadTarget) => LuminPackSerializer.Deserialize<LuminPackUtf8VarIntOptions>(concreteReadInput, ref concreteReadTarget)).Names.Count!=16)throw new Exception("Named UTF-8 profile.");
                    var circle=new Circle{Text="circle"};circle.Next=circle;bytes=LuminPackSerializer.Serialize(circle);var restored=global::LuminPackUnitTest.ConcreteReads.Read<Circle>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Circle concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                    if(!ReferenceEquals(restored,restored.Next)||restored.Text!="circle")throw new Exception("Reference profile.");
                    var bounded=new Bounded{Names=list,Map=new Dictionary<string,string>{{"a","b"}}};bytes=LuminPackSerializer.Serialize(bounded);
                    if(global::LuminPackUnitTest.ConcreteReads.Read<Bounded>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Bounded concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Map["a"]!="b")throw new Exception("Bounded profile.");
                    var boundedUtf8=new BoundedUtf8{Names=new List<string>{null,"","\u4E2D","\uD83D\uDE00"},Map=new Dictionary<string,string>{{"\u4E2D","\u00E9"}}};bytes=LuminPackSerializer.Serialize(boundedUtf8);
                    if(global::LuminPackUnitTest.ConcreteReads.Read<BoundedUtf8>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BoundedUtf8 concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Map["\u4E2D"]!="\u00E9")throw new Exception("Bounded UTF-8 profile.");
                    boundedUtf8.Names.Add(new string('a',33));bytes=LuminPackSerializer.Serialize(boundedUtf8);bool rejected=false;try{global::LuminPackUnitTest.ConcreteReads.Read<BoundedUtf8>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BoundedUtf8 concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));}catch(Exception){rejected=true;}if(!rejected)throw new Exception("Variable string budget bypassed.");
                    var immutable = new BoundedImmutable { Names = ImmutableArray.Create("a", "\u4E2D", new string('a', 32)) };
                    bytes = LuminPackSerializer.Serialize(immutable);
                    var immutableCopy = global::LuminPackUnitTest.ConcreteReads.Read<BoundedImmutable>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BoundedImmutable concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                    if (!immutableCopy.Names.SequenceEqual(immutable.Names)) throw new Exception("Bounded immutable strings changed.");
                    string retainedImmutable = immutableCopy.Names[1];
                    LuminPackSerializer.Deserialize(bytes, ref immutableCopy);
                    if (!ReferenceEquals(retainedImmutable, immutableCopy.Names[1])) throw new Exception("Bounded immutable string reuse changed.");
                    immutable.Names = ImmutableArray.Create(new string('a', 33));
                    bytes = LuminPackSerializer.Serialize(immutable);
                    rejected = false;
                    try { global::LuminPackUnitTest.ConcreteReads.Read<BoundedImmutable>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BoundedImmutable concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)); } catch (System.IO.InvalidDataException) { rejected = true; }
                    if (!rejected) throw new Exception("Immutable string budget bypassed.");
                    string invalidUtf8Json = LuminPackSerializer.SerializeJson(boundedUtf8);
                    rejected = false;
                    try { global::LuminPackUnitTest.ConcreteReads.ReadJson<BoundedUtf8>(invalidUtf8Json, static (string concreteReadInput, ref BoundedUtf8 concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)); }
                    catch (System.IO.InvalidDataException) { rejected = true; }
                    if (!rejected) throw new Exception("UTF-8 JSON string budget bypassed.");
                    bounded.Names = new List<string> { new string('a', 33) };
                    string invalidUtf16Json = LuminPackSerializer.SerializeJson(bounded);
                    rejected = false;
                    try { global::LuminPackUnitTest.ConcreteReads.ReadJson<Bounded>(invalidUtf16Json, static (string concreteReadInput, ref Bounded concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)); }
                    catch (System.IO.InvalidDataException) { rejected = true; }
                    if (!rejected) throw new Exception("UTF-16 JSON string budget bypassed.");
                    foreach (string text in new[] { new string('a', 32), new string('\n', 32), string.Concat(Enumerable.Repeat("\uD83D\uDE00", 16)) })
                    {
                        bounded.Names = new List<string> { text };
                        string boundedJson = LuminPackSerializer.SerializeJson(bounded);
                        var boundedCopy = global::LuminPackUnitTest.ConcreteReads.ReadJson<Bounded>(boundedJson, static (string concreteReadInput, ref Bounded concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget));
                        string retainedText = boundedCopy.Names[0];
                        LuminPackSerializer.DeserializeJson(boundedJson, ref boundedCopy);
                        if (!ReferenceEquals(retainedText, boundedCopy.Names[0])) throw new Exception("Bounded UTF-16 JSON string reuse changed.");
                        boundedUtf8.Names = new List<string> { text };
                        string utf8Json = LuminPackSerializer.SerializeJson(boundedUtf8);
                        var utf8Copy = global::LuminPackUnitTest.ConcreteReads.ReadJson<BoundedUtf8>(utf8Json, static (string concreteReadInput, ref BoundedUtf8 concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget));
                        retainedText = utf8Copy.Names[0];
                        LuminPackSerializer.DeserializeJson(utf8Json, ref utf8Copy);
                        if (!ReferenceEquals(retainedText, utf8Copy.Names[0])) throw new Exception("Bounded UTF-8 JSON string reuse changed.");
                    }
                    string json=LuminPackSerializer.SerializeJson(model);
                    if(global::LuminPackUnitTest.ConcreteReads.ReadJson<Model>(json, static (string concreteReadInput, ref Model concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)).Names.Count!=16)throw new Exception("JSON profile.");
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
        var input = TestCompilation.Create("VarIntIncremental", [tree], references,
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
        var input = TestCompilation.Create("CompiledOptionInvalid", [CSharpSyntaxTree.ParseText(invalid, parse)], references,
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
                    LuminPackSerializer.Register<Opaque, AssemblyProfile>(&Write<AssemblyProfile>, &Read<AssemblyProfile>, &WriteJson<AssemblyProfile>, &ReadJson<AssemblyProfile>);
                    LuminPackSerializer.Register<Opaque, LuminPackDefaultOptions>(&Write<LuminPackDefaultOptions>, &Read<LuminPackDefaultOptions>, &WriteJson<LuminPackDefaultOptions>, &ReadJson<LuminPackDefaultOptions>);
                    var value = new Opaque { Text = "\u4E2D\uD83D\uDE00" };
                    byte[] expected = LuminPackSerializer.Serialize<AssemblyProfile>(value.Text);
                    byte[] bytes = LuminPackSerializer.Serialize<Opaque>(value);
                    if (!System.Linq.Enumerable.SequenceEqual(expected, bytes)) throw new System.Exception("Registered assembly encoding changed.");
                    if (LuminPackSerializer.Deserialize<Opaque>(bytes).Text != value.Text) throw new System.Exception("Registered assembly read changed.");
                    string json = LuminPackSerializer.SerializeJson<Opaque>(value);
                    if (LuminPackSerializer.DeserializeJson<Opaque>(json).Text != value.Text) throw new System.Exception("Registered assembly JSON changed.");
                    bytes = LuminPackSerializer.Serialize<Opaque, LuminPackDefaultOptions>(value);
                    if (LuminPackSerializer.Deserialize<Opaque, LuminPackDefaultOptions>(bytes).Text != value.Text) throw new System.Exception("Explicit registered encoding changed.");
                    json = LuminPackSerializer.SerializeJson<Opaque, LuminPackDefaultOptions>(value);
                    if (LuminPackSerializer.DeserializeJson<Opaque, LuminPackDefaultOptions>(json).Text != value.Text) throw new System.Exception("Explicit registered JSON changed.");
                }
                public static void Write<TOptions>(ref LuminPackWriter writer, in Opaque value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.Serialize<TOptions>(ref writer, value.Text);
                public static void Read<TOptions>(ref LuminPackReader reader, ref Opaque value) where TOptions : struct, ILuminPackOptions
                {
                    string text = "";
                    LuminPackSerializer.Deserialize<TOptions>(ref reader, ref text);
                    value = new() { Text = text };
                }
                public static void WriteJson<TOptions>(ref LuminPackJsonWriter writer, in Opaque value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.SerializeJson<TOptions>(ref writer, value.Text);
                public static void ReadJson<TOptions>(ref LuminPackJsonReader reader, ref Opaque value) where TOptions : struct, ILuminPackOptions
                {
                    string text = "";
                    LuminPackSerializer.DeserializeJson<TOptions>(ref reader, ref text);
                    value = new() { Text = text };
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
        var input = TestCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parse)], references,
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
