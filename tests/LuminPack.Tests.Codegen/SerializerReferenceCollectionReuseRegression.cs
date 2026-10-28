using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks exact reference-container reuse, unchanged exclusions and incremental formatter selection.</summary>
internal static class SerializerReferenceCollectionReuseRegression
{
    /// <summary>Compiles and executes the selected contracts in every generation tier and after mode edits.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Core;
            using LuminPack.Attribute;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class Plain { public int Number; public string Text { get; set; } }
            [LuminPackable(GeneratorType.CircleReference)] public sealed class Circle { [LuminPackOrder(0)]public Circle Next; }
            [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Versioned { [LuminPackOrder(0)]public int Number; }
            [LuminPackable(GeneratorType.Custom)] public sealed class Custom
            {
                public static void Serialize(ref LuminPackWriter writer, in Custom value) => writer.Write(137);
                public static void Deserialize(ref LuminPackReader reader, ref Custom value) { reader.Read<int>(); value = new(); }
            }
            [LuminPackable,LuminPackOption(StructLayout=LuminPackStructLayout.Packed)]public struct Portable { public byte Tag; public long Number; }
            [LuminPackable,LuminPackUnion(0,typeof(UnionCase))]public abstract partial class Union { }
            [LuminPackable]public sealed partial class UnionCase : Union { public int Number; }
            public static class Entry
            {
                public static int Open<T>(ReadOnlySpan<byte> bytes, ref IEnumerable<T> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int OpenDictionary<T>(ReadOnlySpan<byte> bytes, ref IDictionary<string,T> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Dictionary(ReadOnlySpan<byte> bytes, ref IDictionary<string,Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadOnlyDictionary(ReadOnlySpan<byte> bytes, ref IReadOnlyDictionary<string,Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int TextDictionary(ReadOnlySpan<byte> bytes, ref IDictionary<string,string> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int IntegerKeyDictionary(ReadOnlySpan<byte> bytes, ref IDictionary<int,Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ScalarDictionary(ReadOnlySpan<byte> bytes, ref IDictionary<string,int> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int CustomDictionary(ReadOnlySpan<byte> bytes, ref IDictionary<string,Custom> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int CircleDictionary(ReadOnlySpan<byte> bytes, ref IReadOnlyDictionary<string,Circle> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int VersionDictionary(ReadOnlySpan<byte> bytes, ref IDictionary<string,Versioned> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int UnionDictionary(ReadOnlySpan<byte> bytes, ref IDictionary<string,Union> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int PortableDictionary(ReadOnlySpan<byte> bytes, ref IDictionary<string,Portable> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Sequence(ReadOnlySpan<byte> bytes, ref IEnumerable<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Collection(ReadOnlySpan<byte> bytes, ref ICollection<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int List(ReadOnlySpan<byte> bytes, ref IList<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadOnlyList(ReadOnlySpan<byte> bytes, ref IReadOnlyList<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadOnlyCollection(ReadOnlySpan<byte> bytes, ref IReadOnlyCollection<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Text(ReadOnlySpan<byte> bytes, ref IEnumerable<string> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Memory(ReadOnlySpan<byte> bytes, ref Memory<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Segment(ReadOnlySpan<byte> bytes, ref ArraySegment<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int TextMemory(ReadOnlySpan<byte> bytes, ref Memory<string> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int TextSegment(ReadOnlySpan<byte> bytes, ref ArraySegment<string> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Map(ReadOnlySpan<byte> bytes, ref SortedList<string,Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int TextMap(ReadOnlySpan<byte> bytes, ref SortedList<string,string> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ScalarMap(ReadOnlySpan<byte> bytes, ref SortedList<string,int> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ManualMap(ReadOnlySpan<byte> bytes, ref SortedList<string,Custom> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int CircleMap(ReadOnlySpan<byte> bytes, ref SortedList<string,Circle> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int VersionMap(ReadOnlySpan<byte> bytes, ref SortedList<string,Versioned> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int IntegerKeyMap(ReadOnlySpan<byte> bytes, ref SortedList<int,Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadOnly(ReadOnlySpan<byte> bytes, ref ReadOnlyMemory<Plain> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Scalar(ReadOnlySpan<byte> bytes, ref IEnumerable<int> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Layout(ReadOnlySpan<byte> bytes, ref Memory<Portable> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Stateful(ReadOnlySpan<byte> bytes, ref ICollection<Circle> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Manual(ReadOnlySpan<byte> bytes, ref Memory<Custom> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Evolved(ReadOnlySpan<byte> bytes, ref ArraySegment<Versioned> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Polymorphic(ReadOnlySpan<byte> bytes, ref Memory<Union> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static void Check()
                {
                    IEnumerable<Plain> source = new[] { new Plain { Number = 137, Text = "text" }, null };
                    byte[] bytes = LuminPackSerializer.Serialize(source);
                    var backing = new[] { new Plain { Number = 719, Text = "old" }, new Plain() };
                    IEnumerable<Plain> target = backing;
                    var first = backing[0];
                    Sequence(bytes, ref target);
                    if (!ReferenceEquals(backing, target) || !ReferenceEquals(first, target.First()) || first.Number != 137 || first.Text != "text" || backing[1] != null)
                        throw new Exception("An interface-only consumer lost storage or requires concrete adapters.");
                    if (LuminPackSerializer.SerializeJson(target) != "[{\"Number\":137,\"Text\":\"text\"},null]") throw new Exception("JSON changed.");
                    var array = new[] { new Plain { Number = 719 }, new Plain { Number = 27 }, new Plain { Number = 981 } };
                    Memory<Plain> view = array.AsMemory(1, 1);
                    Memory<Plain> input = new[] { new Plain { Number = 137, Text = "new" } };
                    bytes = LuminPackSerializer.Serialize(input);
                    first = array[1];
                    Memory(bytes, ref view);
                    if (!MemoryMarshal.TryGetArray((ReadOnlyMemory<Plain>)view, out var storage) || !ReferenceEquals(storage.Array, array) || storage.Offset != 1 || !ReferenceEquals(first, view.Span[0]) || first.Number != 137 || array[0].Number != 719 || array[2].Number != 981)
                        throw new Exception("Mutable memory lost its selected slice or element.");
                    var map = new SortedList<string,Plain>{{"a",new Plain{Number=719,Text="old"}}};
                    first = map["a"];
                    var mapRoot = map;
                    var mapSource = new SortedList<string,Plain>{{"a",new Plain{Number=137,Text="new"}}};
                    bytes = LuminPackSerializer.Serialize(mapSource);
                    Map(bytes,ref map);
                    if (!ReferenceEquals(mapRoot,map) || !ReferenceEquals(first,map["a"]) || first.Number != 137 || first.Text != "new") throw new Exception("Sorted-list matching values did not reuse.");
                    var dictionary = new Dictionary<string,Plain>{{"a",new Plain{Number=719,Text="old"}}};
                    first = dictionary["a"];
                    IDictionary<string,Plain> dictionarySource = new Dictionary<string,Plain>{{"a",new Plain{Number=137,Text="new"}}};
                    bytes = LuminPackSerializer.Serialize(dictionarySource);
                    IDictionary<string,Plain> dictionaryTarget = dictionary;
                    Dictionary(bytes,ref dictionaryTarget);
                    if (!ReferenceEquals(dictionary,dictionaryTarget) || !ReferenceEquals(first,dictionaryTarget["a"]) || first.Number != 137 || first.Text != "new") throw new Exception("A dictionary-interface-only consumer lost storage.");
                    IReadOnlyDictionary<string,Plain> readOnlyDictionary = dictionary;
                    ReadOnlyDictionary(bytes,ref readOnlyDictionary);
                    if (!ReferenceEquals(dictionary,readOnlyDictionary) || !ReferenceEquals(first,readOnlyDictionary["a"])) throw new Exception("The read-only dictionary interface lost mutable backing storage.");
                    var current = new Circle(); current.Next = current;
                    ICollection<Circle> circles = new[] { current, current };
                    bytes = LuminPackSerializer.Serialize(circles);
                    circles = null;
                    Stateful(bytes, ref circles);
                    var items = circles.ToArray();
                    if (!ReferenceEquals(items[0], items[1]) || !ReferenceEquals(items[0], items[0].Next)) throw new Exception("Excluded reference tracking changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("ReferenceReuse" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Shape.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var (driver, output) = Generate(compilation, parse);
            CheckSelection(driver, ordinary: true);
            Execute(output);
            string editedText = text.Replace("[LuminPackable] public sealed class Plain", "[LuminPackable(GeneratorType.VersionTolerant)] public sealed class Plain", StringComparison.Ordinal)
                .Replace("public int Number; public string Text { get; set; }", "[LuminPackOrder(0)]public int Number; [LuminPackOrder(1)]public string Text { get; set; }", StringComparison.Ordinal);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(editedText, parse, "Shape.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var editedOutput, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckSelection(driver, ordinary: false);
            Emit(editedOutput);
            var (fresh, _) = Generate(edited, parse);
            Require(Trees(driver).SequenceEqual(Trees(fresh)), "An ordinary-to-versioned edit retained stale reuse.");
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var restored, out diagnostics);
            CheckSelection(driver, ordinary: true);
            var (freshRestored, _) = Generate(compilation, parse);
            Require(Trees(driver).SequenceEqual(Trees(freshRestored)), "Restoring ordinary storage retained a stale fallback.");
            Execute(restored);
        }
        Console.WriteLine("Passed reference-container reuse eligibility, exclusions, four tiers and retained/fresh formatter edits.");
    }

    /// <summary>Checks every selected root without relying on broad source-text matches.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="ordinary">Whether Plain retains the ordinary object formatter.</param>
    private static void CheckSelection(GeneratorDriver driver, bool ordinary)
    {
        foreach (var method in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                     .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                     .Where(method => method.Identifier.ValueText == "RootRead" && method.ParameterList.Parameters.Count == 2))
        {
            string type = method.ParameterList.Parameters[1].Type!.ToString();
            bool sequence = type.Contains(".IEnumerable<", StringComparison.Ordinal) || type.Contains(".ICollection<", StringComparison.Ordinal) ||
                type.Contains(".IList<", StringComparison.Ordinal) || type.Contains(".IReadOnlyList<", StringComparison.Ordinal) || type.Contains(".IReadOnlyCollection<", StringComparison.Ordinal);
            bool view = type.Contains(".Memory<", StringComparison.Ordinal) || type.Contains(".ArraySegment<", StringComparison.Ordinal);
            if (type.Contains(".IDictionary<", StringComparison.Ordinal) || type.Contains(".IReadOnlyDictionary<", StringComparison.Ordinal))
            {
                bool dictionaryExpected = type.Contains(", string>", StringComparison.Ordinal) || ordinary && type.Contains(", global::Plain>", StringComparison.Ordinal);
                Require(method.ToString().Contains("value.GetType() == typeof(", StringComparison.Ordinal) == dictionaryExpected, "Dictionary-interface reuse selection differs: " + type);
            }
            if (type.Contains(".SortedList<", StringComparison.Ordinal))
            {
                // String and framework-scalar keys reuse string or ordinary object values since 2026-10-26.
                bool mapExpected = type.Contains("SortedList<string, string>", StringComparison.Ordinal) ||
                    ordinary && (type.Contains("SortedList<string, global::Plain>", StringComparison.Ordinal) || type.Contains("SortedList<int, global::Plain>", StringComparison.Ordinal));
                // Only the matching-pair reader stages the prior storage; local function names are rewritten.
                Require(method.ToString().Contains("TryGetSortedListStorage(", StringComparison.Ordinal) == mapExpected, "Sorted-list value reuse selection differs: " + type);
            }
            if (type.Contains(".ReadOnlyMemory<", StringComparison.Ordinal))
                Require(!method.ToString().Contains("__storage.Count == length", StringComparison.Ordinal) &&
                    !method.ToString().Contains("TryGetExactArray(", StringComparison.Ordinal), "Read-only memory acquired mutable reuse.");
            if (!sequence && !view) continue;
            bool expected = type.Contains("<int>", StringComparison.Ordinal) || type.Contains("<string>", StringComparison.Ordinal) ||
                ordinary && type.Contains("<global::Plain>", StringComparison.Ordinal);
            // Memory reuse locates its exact array slice through TryGetExactArray; ArraySegment compares its own count.
            string marker = sequence ? "value.GetType() == typeof(" : type.Contains(".Memory<", StringComparison.Ordinal) ? "TryGetExactArray(" : "__storage.Count == length";
            bool selected = method.ToString().Contains(marker, StringComparison.Ordinal);
            Require(expected == selected, "Reference-storage selection differs: " + type);
        }
    }

    /// <summary>Runs a fresh generator driver for an exact consumer fixture.</summary>
    /// <param name="compilation">The fixture and references.</param>
    /// <param name="parse">The selected language and target symbols.</param>
    /// <returns>The retained driver and generated compilation.</returns>
    private static (GeneratorDriver Driver, Compilation Output) Generate(CSharpCompilation compilation, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        return (driver, output);
    }

    /// <summary>Emits a generated consumer and rejects compiler errors.</summary>
    /// <param name="output">The generated consumer.</param>
    /// <returns>The complete assembly image.</returns>
    private static byte[] Emit(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        return image.ToArray();
    }

    /// <summary>Executes the fixture without any caller-authored storage adapters.</summary>
    /// <param name="output">The generated consumer.</param>
    private static void Execute(Compilation output) => Assembly.Load(Emit(output)).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);

    /// <summary>Captures exact deterministic generated trees for incremental comparison.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>Sorted hint names and source texts.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports the precise failed invariant.</summary>
    /// <param name="condition">The required condition.</param>
    /// <param name="message">The diagnostic detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
