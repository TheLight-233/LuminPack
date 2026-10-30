using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks generated string-list reads and their canonical element metadata.</summary>
internal static class SerializerStringListReadRegression
{
    /// <summary>Exercises compiled profiles, nested reuse and incremental element edits in every generation tier.</summary>
    /// <param name="references">The platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using Element = System.String;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed partial class Holder
            { public List<Element> Values; public List<Element> Tail {get;set;} }
            public static class Entry
            {
                public static void Check()
                {
                    Profile<LuminPackDefaultOptions>();
                    Profile<LuminPackUtf16LengthOptions>();
                    Profile<LuminPackUtf8LengthOptions>();
                    Profile<LuminPackUtf16TokenOptions>();
                    Profile<LuminPackUtf8TokenOptions>();
                    Profile<LuminPackUtf16VarIntOptions>();
                    Profile<LuminPackUtf8VarIntOptions>();
                }
                private static Element Value(int index) => typeof(Element)==typeof(string)
                    ? (Element)(object)("value-"+index) : (Element)(object)index;
                private static void Profile<T>() where T:struct,ILuminPackOptions
                {
                    var source = Enumerable.Range(0,256).Select(Value).ToList();
                    byte[] bytes=LuminPackSerializer.Serialize<T>(source);
                    List<Element> target=null;
                    LuminPackSerializer.Deserialize<T>(bytes,ref target);
                    var original=target; var first=target[0];
                    LuminPackSerializer.Deserialize<T>(bytes,ref target);
                    if(!ReferenceEquals(original,target) || !source.SequenceEqual(target)) throw new Exception("Root list reuse differs.");
                    if(typeof(Element)==typeof(string) && !ReferenceEquals(first,target[0])) throw new Exception("String identity changed.");
                    var holder = new Holder {Values=source,Tail=new(){Value(17)}};
                    bytes=LuminPackSerializer.Serialize<T>(holder);
                    Holder receiver=null;
                    LuminPackSerializer.Deserialize<T>(bytes,ref receiver);
                    var values=receiver.Values; var tail=receiver.Tail;
                    LuminPackSerializer.Deserialize<T>(bytes,ref receiver);
                    if(!ReferenceEquals(values,receiver.Values) || !ReferenceEquals(tail,receiver.Tail) || !source.SequenceEqual(values)) throw new Exception("Nested list storage changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview,
                preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var tree = CSharpSyntaxTree.ParseText(text, parse, "Entry.cs");
            var compilation = CSharpCompilation.Create("StringListReads" + mode + Guid.NewGuid().ToString("N"), [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
            Require(Trees(driver).Any(text => text.Contains(".ReadStrings<__LuminProfile>", StringComparison.Ordinal)), "The string list lost its compiled bulk reader.");
            Execute(output);
            string changed = text.Replace("using Element = System.String;", "using Element = System.Int32;", StringComparison.Ordinal);
            var edited = compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(changed, parse, "Entry.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out output, out _);
            Require(!Trees(driver).Any(text => text.Contains(".ReadStrings<", StringComparison.Ordinal)), "Primitive lists retained the string kernel.");
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(edited))), "The element edit retained stale string metadata.");
            Execute(output);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(compilation))), "Restoring ordinary storage retained stale exclusions.");
        }
        Console.WriteLine("Passed generated string-list reads, seven profiles, nested reuse and four-tier incremental element edits.");
    }

    /// <summary>Creates independent drivers for retained and fresh comparisons.</summary>
    /// <param name="parse">The consumer's language and target symbols.</param>
    /// <returns>The configured generator driver.</returns>
    private static GeneratorDriver Create(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Compiles and executes a complete generated consumer.</summary>
    /// <param name="output">The consumer after generation.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Captures complete deterministic generated source snapshots.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>Sorted hint names and source texts.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a focused invariant failure.</summary>
    /// <param name="condition">Whether the invariant holds.</param>
    /// <param name="message">The failing invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
