extern alias ModelBasedAnalyzer;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks independent model versions and generator diagnostics for named binary contracts.</summary>
internal static class SerializerNamedVersionRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Compiles independent versions with identical CLR names and checks nested bidirectional evolution.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string template = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            namespace VersionModels;
            [LuminPackable(GeneratorType.VersionTolerant)] public class Child { CHILD }
            [LuminPackable(GeneratorType.VersionTolerant)] public class Root { ROOT }
            [LuminPackable(GeneratorType.VersionTolerant)] public class Envelope<T> { public T Value; }
            [LuminPackable(GeneratorType.VersionTolerant)] public class TypeShapes
            {
                public Guid Id; public DateTime Date; public DateTimeOffset Offset; public TimeSpan Duration;
                public nint Native; public nuint UnsignedNative; public int? Optional;
                public (long Number, string Text) Pair; public Child[,] Grid;
                public Dictionary<string, List<Child[]>> Nested;
            }
            public static class Entry
            {
                public static byte[] WriteShapes() => LuminPackSerializer.Serialize(new TypeShapes());
                public static byte[] Write() => LuminPackSerializer.Serialize(new Envelope<Root> { Value = new Root
                {
                    UNKNOWN
                    Id = 17, Child = new Child { Number = 19, TEXT = "nested" },
                    Children = new List<Child> { new Child { Number = 23, TEXT = "list" }, new Child { Number = 29, TEXT = "short" } }
                }});
                public static string Read(byte[] bytes)
                {
                    var value = global::LuminPackUnitTest.ConcreteReads.Read<Envelope<Root>>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Envelope<Root> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Value;
                    return value.Id + ":" + value.Child.Number + ":" + value.Child.TEXT + ":" + value.Children[0].Number + ":" + value.Children[0].TEXT + ":" + value.Children[1].Number + ":" + value.Children[1].TEXT;
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            string oldSource = template.Replace("MODE", mode)
                .Replace("CHILD", "public int Number; public string Name; public string Removed;")
                .Replace("ROOT", "public List<Child> Removed; public int Id; public Child Child; public List<Child> Children;")
                .Replace("UNKNOWN", "Removed = new List<Child> { new Child { Number = -1 } },")
                .Replace("TEXT", "Name");
            string newSource = template.Replace("MODE", mode)
                .Replace("CHILD", "public bool Added; [LuminPackOldName(\"Name\")] public string Display; public int Number;")
                .Replace("ROOT", "public List<Child> Added; public List<Child> Children; public Child Child; public int Id;")
                .Replace("UNKNOWN", "Added = new List<Child> { new Child { Number = -2 } },")
                .Replace("TEXT", "Display");
            var oldEntry = Compile(oldSource, "NamedOld" + mode, references).GetType("VersionModels.Entry")!;
            var newEntry = Compile(newSource, "NamedNew" + mode, references).GetType("VersionModels.Entry")!;
            byte[] oldBytes = (byte[])oldEntry.GetMethod("Write")!.Invoke(null, null)!;
            byte[] newBytes = (byte[])newEntry.GetMethod("Write")!.Invoke(null, null)!;
            const string expected = "17:19:nested:23:list:29:short";
            Require((string)newEntry.GetMethod("Read")!.Invoke(null, [oldBytes])! == expected, "Nested forward evolution failed in " + mode);
            Require((string)oldEntry.GetMethod("Read")!.Invoke(null, [newBytes])! == expected, "Nested backward evolution failed in " + mode);
        }
        VerifyVersionDiagnostics(references);
        Console.WriteLine("Passed scoped nested evolution across independent Full/Minimal assemblies and descriptor diagnostics.");
    }

    /// <summary>Checks invalid aliases, distinct names with equal keys and excessive member counts.</summary>
    /// <param name="references">Runtime and platform references.</param>
    private static void VerifyVersionDiagnostics(MetadataReference[] references)
    {
        const string model = "using LuminPack.Attribute; [LuminPackable(GeneratorType.VersionTolerant)] public class Invalid ";
        foreach (string members in new[]
        {
            "[LuminPackOldName(\"\")] public int A;",
            "[LuminPackOldName(null)] public int A;",
            "[LuminPackOldName(\"A\")] public int A;",
            "[LuminPackOldName(\"B\")] public int A; public int B;",
            "[LuminPackOldName(\"old\")] public int A; [LuminPackOldName(\"old\")] public int B;",
            "public int costarring; public int liquid;",
            "[LuminPackOldName(\"costarring\")] public int A; public int liquid;"
        })
            Check(model + "{ " + members + " }", "LuminPack062");

        Check(model + ": Base { [LuminPackOldName(\"liquid\")] public int Value; } public class Base { public int costarring; }",
            "LuminPack062");
        string fields = string.Join(" ", Enumerable.Range(0, 250).Select(i => "public int Field" + i + ";"));
        Check(model + "{ " + fields + " }", "LuminPack018");

        void Check(string source, string diagnostic)
        {
            var input = TestCompilation.Create("InvalidVersionDescriptor", [CSharpSyntaxTree.ParseText(source, Parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var result = NewDriver().RunGenerators(input).GetRunResult();
            Require(result.Results.All(item => item.Exception == null) && result.Diagnostics.Any(item => item.Id == diagnostic),
                "Missing descriptor diagnostic " + diagnostic + " for " + source);
        }
    }

    /// <summary>Generates, compiles and loads a single independent model version.</summary>
    /// <param name="source">The version's model and entry points.</param>
    /// <param name="name">The unique assembly identity.</param>
    /// <param name="references">Runtime and platform references.</param>
    /// <returns>The independently loaded model assembly.</returns>
    private static Assembly Compile(string source, string name, MetadataReference[] references)
    {
        var input = TestCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, Parse, name + ".cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Require(driver.GetRunResult().Results.All(item => item.Exception == null) && errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(item => item.ToString()).Concat(driver.GetRunResult().Results.Select(item => item.Exception?.ToString()))));
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        VerifyGeneratedDescriptors(driver);
        return Assembly.Load(image.ToArray());
    }

    /// <summary>Checks the descriptor representation and ordinary tiered source shape of versioned readers.</summary>
    /// <param name="driver">The completed model generation.</param>
    private static void VerifyGeneratedDescriptors(GeneratorDriver driver)
    {
        var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        string[] versioned = sources.SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Select(method => method.ToString()).Where(method => method.Contains("ReadVersionRecord", StringComparison.Ordinal) ||
                method.Contains("WriteVersionRecord", StringComparison.Ordinal)).ToArray();
        Require(versioned.Length != 0, "No versioned reader or writer was generated.");
        foreach (string method in versioned)
        {
            string[] prohibited = method.Split('\n').Where(line => line.Contains("stackalloc", StringComparison.Ordinal) ||
                line.Contains("ThreadStatic", StringComparison.Ordinal) ||
                line.Contains("ReadEvolvedFields", StringComparison.Ordinal) ||
                System.Text.RegularExpressions.Regex.IsMatch(line, @"SizeOf<(global::System.IntPtr|nint)>\(\)\s*(==|!=)\s*(4|8)"))
                .Select(line => line.Trim()).ToArray();
            Require(prohibited.Length == 0, "A versioned binary method contains legacy staging or platform selection: " +
                string.Join(" | ", prohibited));
        }
        string generated = string.Join(Environment.NewLine, sources.Select(source => source.SourceText.ToString()));
        Require(generated.Contains("SlotOf", StringComparison.Ordinal) &&
            generated.Contains("ReadMapped", StringComparison.Ordinal) &&
            generated.Contains("SkipVersionField", StringComparison.Ordinal),
            "The mapped version loop, slot lookup or unknown member skip was lost.");
    }

    /// <summary>Creates an isolated driver for one compilation.</summary>
    /// <returns>The current serialization and mapping generators.</returns>
    private static GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(
        [new ModelBasedGenerator().AsSourceGenerator()], parseOptions: Parse);

    /// <summary>Reports an evolution or generator regression.</summary>
    /// <param name="condition">The expected invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
