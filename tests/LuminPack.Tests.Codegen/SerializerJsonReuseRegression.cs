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
        const string source = """
            using System;
            using System.Text;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public class Node { public int Number; public string Text = ""; }
            [LuminPackable] public class Graph
            {
                public Node Child { get; set; } = new Node();
                public List<Node> Items = new List<Node>();
                public Node[] Array = System.Array.Empty<Node>();
                public Dictionary<string, Node> Map = new Dictionary<string, Node>();
            }
            [LuminPackable] public struct Managed { public Node Child; }
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
            [LuminPackable, LuminPackUnion(0, typeof(Case))] public abstract partial class Root { }
            [LuminPackable] public partial class Case : Root { public Node Child = new Node(); }
            public static class JsonReuseEntry
            {
                public static void Check()
                {
                    CheckProfile<LuminPackDefaultOptions>();
                    CheckProfile<LuminPackUtf16LengthOptions>();
                    var bound = new Bound(new Node());
                    var child = bound.Child;
                    LuminPackSerializer.DeserializeJson("{\"Child\":{\"Number\":8}}", ref bound);
                    Require(ReferenceEquals(child, bound.Child) && child.Number == 8, "Constructor storage");
                    var managed = new Managed { Child = child };
                    LuminPackSerializer.DeserializeJson<Managed>("{\"Child\":{\"Number\":9}}", ref managed);
                    Require(ReferenceEquals(child, managed.Child) && child.Number == 9, "Managed value storage");
                    child = Pooled.Shared.Child;
                    var pooled = LuminPackSerializer.DeserializeJson<Pooled>("{\"Child\":{\"Number\":13}}");
                    Require(ReferenceEquals(pooled, Pooled.Shared) && ReferenceEquals(child, pooled.Child) && child.Number == 13, "Fresh pooled reuse");
                    var storage = new[] { new Node(), new Node() };
                    Memory<Node> memory = storage.AsMemory(1, 1);
                    LuminPackSerializer.DeserializeJson<Memory<Node>>("[{\"Number\":14}]", ref memory);
                    Require(ReferenceEquals(storage[1], memory.Span[0]) && storage[1].Number == 14, "Mutable slice");
                    Root root = new Case();
                    object original = root;
                    LuminPackSerializer.DeserializeJson<Root>("{\"$type\":0,\"$value\":{\"Child\":{\"Number\":10}}}", ref root);
                    Require(ReferenceEquals(root, original) && ((Case)root).Child.Number == 10, "Union case");
                    var tracked = new Tracked();
                    original = tracked;
                    LuminPackSerializer.DeserializeJson<Tracked>("{\"$id\":0,\"Number\":11,\"Next\":{\"$ref\":0}}", ref tracked);
                    Require(ReferenceEquals(original, tracked) && ReferenceEquals(tracked, tracked.Next), "Tracked cycle");
                    IEnumerable<Node> sequence = new List<Node> { child };
                    original = sequence;
                    LuminPackSerializer.DeserializeJson<IEnumerable<Node>>("[{\"Number\":12}]", ref sequence);
                    Require(ReferenceEquals(original, sequence) && ReferenceEquals(child, ((List<Node>)sequence)[0]), "Interface storage");
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
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var compilation = CSharpCompilation.Create("JsonReuse" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse, "Shape.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            if (diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error)) throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics));
            Execute(output);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(
                source.Replace("MODE", mode, StringComparison.Ordinal).Replace("public int Number;", "public long Number;", StringComparison.Ordinal), parse, "Shape.cs"));
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
