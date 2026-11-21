using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks complete JSON reuse through concrete and generic entries in every generation tier.</summary>
internal static class SerializerJsonReuseRegression
{
    /// <summary>Compiles and executes JSON graph reuse and compares retained generation with a fresh driver.</summary>
    /// <param name="references">The platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        string source = """
            using System;
            using System.Text;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public class Node { public int Number; public string Text = ""; }
            [LuminPackable] public class PresenceBase { BASE_PRESENCE_FIELDS }
            [LuminPackable] public class Presence<seenFields0, seenFields1, seenFields2> : PresenceBase
            {
                DERIVED_PRESENCE_FIELDS
                public Node Child;
            }
            [LuminPackable] public partial struct MixedPresence
            {
                public int A, B, C, D;
                [LuminPackInclude] private int Hidden;
                public int Property { get; set; }
                [LuminPackIgnore] public int HiddenValue => Hidden;
                public static MixedPresence Create() => new MixedPresence { A = 9, B = 9, C = 9, D = 9, Hidden = 9, Property = 9 };
            }
            [LuminPackable] public class Graph
            {
                public Node Child { get; set; } = new Node();
                public List<Node> Items = new List<Node>();
                public Node[] Array = System.Array.Empty<Node>();
                public Dictionary<string, Node> Map = new Dictionary<string, Node>();
            }
            [LuminPackable] public struct Managed { public Node Child; }
            [LuminPackable] public class ImmutableGraph
            {
                public ImmutableList<int> Numbers;
                public IImmutableList<string> Words;
                public ImmutableList<Node> Nodes;
            }
            [LuminPackable] public class Pooled
            {
                public static readonly Pooled Shared = new Pooled();
                public Node Child = new Node();
                [LuminPackPoolRent] public static Pooled Rent() => Shared;
            }
            [LuminPackable] public class Bound
            {
                public Node Child { get; }
                public Bound(Node child) { Child = child; }
            }
            [LuminPackable(GeneratorType.CircleReference)] public class Tracked
            {
                [LuminPackOrder(0)] public int Number;
                [LuminPackOrder(1)] public Tracked Next;
            }
            [LuminPackable, LuminPackUnion(0, typeof(Case)), LuminPackUnion(1, typeof(LoopCase))] public abstract partial class Root { }
            [LuminPackable] public partial class Case : Root { public Node Child = new Node(); }
            [LuminPackable] public partial class LoopCase : Root { public Graph Graph = new Graph(); }
            public static class JsonReuseEntry
            {
                public static void Check()
                {
                    CheckProfile<LuminPackUtf8LengthOptions>();
                    CheckProfile<LuminPackUtf16LengthOptions>();
                    CheckPresence<LuminPackUtf8LengthOptions>();
                    CheckPresence<LuminPackUtf16LengthOptions>();
                    CheckUnionLoops<LuminPackUtf8LengthOptions>();
                    CheckUnionLoops<LuminPackUtf16LengthOptions>();
                    CheckImmutableLists<LuminPackUtf8LengthOptions>();
                    CheckImmutableLists<LuminPackUtf16LengthOptions>();
                    var bound = new Bound(new Node());
                    var child = bound.Child;
                    LuminPackSerializer.DeserializeJson("{\"Child\":{\"Number\":8}}", ref bound);
                    Require(ReferenceEquals(child, bound.Child) && child.Number == 8, "Constructor storage");
                    var managed = new Managed { Child = child };
                    LuminPackSerializer.DeserializeJson("{\"Child\":{\"Number\":9}}", ref managed);
                    Require(ReferenceEquals(child, managed.Child) && child.Number == 9, "Managed value storage");
                    child = Pooled.Shared.Child;
                    Pooled pooled = null!; LuminPackSerializer.DeserializeJson("{\"Child\":{\"Number\":13}}", ref pooled);
                    Require(ReferenceEquals(pooled, Pooled.Shared) && ReferenceEquals(child, pooled.Child) && child.Number == 13, "Fresh pooled reuse");
                    var storage = new[] { new Node(), new Node() };
                    Memory<Node> memory = storage.AsMemory(1, 1);
                    LuminPackSerializer.DeserializeJson("[{\"Number\":14}]", ref memory);
                    Require(ReferenceEquals(storage[1], memory.Span[0]) && storage[1].Number == 14, "Mutable slice");
                    Root root = new Case();
                    object original = root;
                    LuminPackSerializer.DeserializeJson("{\"$type\":0,\"$value\":{\"Child\":{\"Number\":10}}}", ref root);
                    Require(ReferenceEquals(root, original) && ((Case)root).Child.Number == 10, "Union case");
                    var tracked = new Tracked();
                    original = tracked;
                    LuminPackSerializer.DeserializeJson("{\"$id\":0,\"Number\":11,\"Next\":{\"$ref\":0}}", ref tracked);
                    Require(ReferenceEquals(original, tracked) && ReferenceEquals(tracked, tracked.Next), "Tracked cycle");
                    IEnumerable<Node> sequence = new List<Node> { child };
                    original = sequence;
                    LuminPackSerializer.DeserializeJson("[{\"Number\":12}]", ref sequence);
                    Require(ReferenceEquals(original, sequence) && ReferenceEquals(child, ((List<Node>)sequence)[0]), "Interface storage");
                }
                private static void CheckImmutableLists<TOptions>() where TOptions : struct, ILuminPackOptions
                {
                    var encoding = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
                    foreach (int count in new[] { 0, 1, 2, 3, 4, 17, 129 })
                    {
                        var value = new ImmutableGraph
                        {
                            Numbers = ImmutableList.CreateRange(Enumerable.Range(0, count)),
                            Words = ImmutableList.CreateRange(Enumerable.Range(0, count).Select(index => index % 3 == 0 ? null : "word\u00e9\u4e2d" + index)),
                            Nodes = ImmutableList.CreateRange(Enumerable.Range(0, count).Select(index => new Node { Number = index, Text = "node" + index }))
                        };
                        string json = LuminPackSerializer.SerializeJson<ImmutableGraph, TOptions>(value);
                        byte[] bytes = encoding.GetBytes(json);
                        var original = value; var numbers = value.Numbers; var words = value.Words; var nodes = value.Nodes;
                        int consumed = LuminPackSerializer.DeserializeJson<ImmutableGraph, TOptions>(bytes, ref value);
                        Require(consumed == bytes.Length && ReferenceEquals(original, value), "Immutable list owner reuse");
                        Require(value.Numbers.SequenceEqual(numbers) && value.Words.SequenceEqual(words), "Immutable list ordering/nulls");
                        Require(value.Nodes.Count == count && LuminPackSerializer.SerializeJson<ImmutableGraph, TOptions>(value) == json, "Immutable object list values");
                        if (count != 0) Require(!ReferenceEquals(nodes[0], value.Nodes[0]) && nodes[0].Number == 0, "Immutable element construction");
                        if (count == 0) Require(ReferenceEquals(value.Numbers, ImmutableList<int>.Empty), "Immutable empty singleton");
                        ImmutableGraph fresh = null!;
                        LuminPackSerializer.DeserializeJson<ImmutableGraph, TOptions>(bytes, ref fresh);
                        Require(LuminPackSerializer.SerializeJson<ImmutableGraph, TOptions>(fresh) == json, "Fresh immutable lists");
                        bool failed = false;
                        var retained = value.Nodes;
                        try { LuminPackSerializer.DeserializeJson<ImmutableGraph, TOptions>(encoding.GetBytes("{\"Nodes\":[{\"Number\":1},{\"Number\":true}]}"), ref value); }
                        catch (InvalidOperationException) { failed = true; }
                        catch (FormatException) { failed = true; }
                        Require(failed && ReferenceEquals(value.Nodes, retained), "Immutable list failure publication");
                        LuminPackSerializer.DeserializeJson<ImmutableGraph, TOptions>(encoding.GetBytes("{\"Numbers\":null,\"Words\":null,\"Nodes\":null}"), ref value);
                        Require(value.Numbers is null && value.Words is null && value.Nodes is null, "Immutable list nulls");
                        Require(LuminPackSerializer.SerializeJson<ImmutableGraph, TOptions>(fresh) == json, "Immutable list survives pooled storage reuse");
                    }
                }
                private static void CheckUnionLoops<TOptions>() where TOptions : struct, ILuminPackOptions
                {
                    var graph = new Graph { Child = new Node { Number = 1 }, Items = new List<Node> { new Node { Number = 2 } }, Array = new[] { new Node { Number = 3 } } };
                    graph.Map["key"] = new Node { Number = 4 };
                    Root root = new LoopCase { Graph = graph };
                    string json = LuminPackSerializer.SerializeJson<Root, TOptions>(root);
                    var original = root; var child = graph.Child; var item = graph.Items[0]; var element = graph.Array[0]; var mapped = graph.Map["key"];
                    child.Number = item.Number = element.Number = mapped.Number = 99;
                    LuminPackSerializer.DeserializeJson<Root, TOptions>(json, ref root);
                    Require(ReferenceEquals(original, root) && ReferenceEquals(graph, ((LoopCase)root).Graph), "Union loop root reuse");
                    Require(ReferenceEquals(child, graph.Child) && child.Number == 1 && ReferenceEquals(item, graph.Items[0]) && item.Number == 2, "Union loop object/list reuse");
                    Require(ReferenceEquals(element, graph.Array[0]) && element.Number == 3 && ReferenceEquals(mapped, graph.Map["key"]) && mapped.Number == 4, "Union loop array/map reuse");
                    root = new Case();
                    LuminPackSerializer.DeserializeJson<Root, TOptions>(json, ref root);
                    Require(root is LoopCase replacement && replacement.Graph.Map["key"].Number == 4, "Union loop case replacement");
                    Root fresh = null!;
                    LuminPackSerializer.DeserializeJson<Root, TOptions>(json, ref fresh);
                    Require(LuminPackSerializer.SerializeJson<Root, TOptions>(fresh) == json, "Union loop fresh round trip");
                }
                private static void CheckPresence<TOptions>() where TOptions : struct, ILuminPackOptions
                {
                    var value = new Presence<int, string, long> { Child = new Node() };
                    var original = value;
                    var child = value.Child;
                    var fields = value.GetType().GetFields();
                    foreach (var field in fields)
                        if (field.FieldType == typeof(int)) field.SetValue(value, 99);
                    const string json = "{\"F64\":64,\"F31\":31,\"unknown\":{\"x\":[{},42]},\"F3\\u0032\":32,\"F00\":1,\"F63\":63,\"F31\":131,\"Child\":{\"Number\":7}}";
                    LuminPackSerializer.DeserializeJson<Presence<int, string, long>, TOptions>(json, ref value);
                    Require(ReferenceEquals(original, value) && ReferenceEquals(child, value.Child) && child.Number == 7, "Presence reuse");
                    foreach (var field in fields)
                    {
                        if (field.FieldType != typeof(int)) continue;
                        int expected = field.Name switch { "F00" => 1, "F31" => 131, "F32" => 32, "F63" => 63, "F64" => 64, _ => 0 };
                        Require((int)field.GetValue(value)! == expected, "Missing/duplicate inherited presence: " + field.Name);
                    }
                    value.F64 = 99;
                    bool failed = false;
                    try { LuminPackSerializer.DeserializeJson<Presence<int, string, long>, TOptions>("{\"F31\":17,\"F32\":true}", ref value); }
                    catch (InvalidOperationException) { failed = true; }
                    catch (FormatException) { failed = true; }
                    Require(failed && ReferenceEquals(original, value) && value.F31 == 17 && value.F64 == 99, "Presence failure order");
                    Presence<int, string, long> fresh = null!;
                    failed = false;
                    try { LuminPackSerializer.DeserializeJson<Presence<int, string, long>, TOptions>("{\"F31\":17,\"F32\":true}", ref fresh); }
                    catch (InvalidOperationException) { failed = true; }
                    catch (FormatException) { failed = true; }
                    Require(failed && fresh is null, "Presence fresh publication");
                    LuminPackSerializer.DeserializeJson<Presence<int, string, long>, TOptions>("{}", ref value);
                    Require(ReferenceEquals(original, value) && value.Child is null, "Presence empty object");
                    foreach (var field in fields)
                        if (field.FieldType == typeof(int)) Require((int)field.GetValue(value)! == 0, "Presence empty reset");
                    var mixed = MixedPresence.Create();
                    LuminPackSerializer.DeserializeJson<MixedPresence, TOptions>("{\"D\":44,\"Hidden\":55,\"Property\":66}", ref mixed);
                    Require(mixed.A == 0 && mixed.B == 0 && mixed.C == 0 && mixed.D == 44 && mixed.HiddenValue == 55 && mixed.Property == 66, "Presence staged members");
                }
                private static void CheckProfile<TOptions>() where TOptions : struct, ILuminPackOptions
                {
                    var node = new Node { Number = 7, Text = "same\u00E9" };
                    var target = new Graph { Child = node, Items = new List<Node> { new Node() }, Array = new[] { new Node() } };
                    target.Map["key"] = new Node();
                    string json = LuminPackSerializer.SerializeJson<Graph, TOptions>(target);
                    byte[] bytes = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(json);
                    var root = target; var child = target.Child; var text = child.Text;
                    var list = target.Items; var item = list[0]; var array = target.Array; var element = array[0]; var map = target.Map; var mapped = map["key"];
                    int consumed = LuminPackSerializer.DeserializeJson<Graph, TOptions>(bytes, ref target);
                    Require(consumed == bytes.Length && ReferenceEquals(root, target) && ReferenceEquals(child, target.Child), "Object graph");
                    Require(ReferenceEquals(text, child.Text) && ReferenceEquals(list, target.Items) && ReferenceEquals(item, list[0]), "List and string");
                    Require(ReferenceEquals(array, target.Array) && ReferenceEquals(element, array[0]), "Array");
                    Require(ReferenceEquals(map, target.Map) && ReferenceEquals(mapped, map["key"]), "Dictionary");
                }
                private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
            }
            """.Replace("BASE_PRESENCE_FIELDS", string.Join(" ", Enumerable.Range(0, 32).Select(index => "public int F" + index.ToString("D2") + ";")), StringComparison.Ordinal)
                .Replace("DERIVED_PRESENCE_FIELDS", string.Join(" ", Enumerable.Range(32, 33).Select(index => "public int F" + index.ToString("D2") + ";")), StringComparison.Ordinal);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var compilation = CSharpCompilation.Create("JsonReuse" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse, "Shape.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release)).WithLegacyPackableSyntax();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            if (diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error)) throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics));
            Execute(output);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(tree => tree.FilePath is not ("ConcreteReads.cs" or LegacyPackableSyntax.FileName)), CSharpSyntaxTree.ParseText(
                source.Replace("MODE", mode, StringComparison.Ordinal).Replace("public int Number;", "public long Number;", StringComparison.Ordinal)
                    .Replace("public int F31;", "public int Extra; public int F31;", StringComparison.Ordinal), parse, "Shape.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out output, out diagnostics);
            Execute(output);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(edited);
            if (!Trees(driver).SequenceEqual(Trees(fresh))) throw new InvalidOperationException("JSON reuse differs between retained and fresh generation.");
        }
        Console.WriteLine("Passed JSON reuse across four generation tiers and retained/fresh member edits.");
    }

    /// <summary>Compiles the generated consumer and executes its reference identity assertions.</summary>
    /// <param name="output">The complete generated compilation.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (!emitted.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("JsonReuseEntry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Captures deterministic generated text for incremental comparison.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>The sorted source identities and contents.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();
}
