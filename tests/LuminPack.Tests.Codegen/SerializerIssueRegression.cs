extern alias ModelBasedAnalyzer;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Exercises reported model-based generator regressions.</summary>
internal static partial class SerializerIssueRegression
{
    /// <summary>Compiles and executes the reported generic Union cases.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunUnion(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers.Binary;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public abstract partial class ProbeBox<T> { public T V; }
            [LuminPackable] public sealed partial class ProbeBoxA : ProbeBox<int> { }
            [LuminPackable] public sealed partial class ProbeBoxB : ProbeBox<int> { }
            [LuminPackable] public sealed partial class ProbeText : ProbeBox<string> { }
            [LuminPackable, LuminPackUnion(1, typeof(ProbeRoute))] public abstract partial class ProbeRouteBase { }
            [LuminPackable] public sealed partial class ProbeRoute : ProbeRouteBase
            {
                public int Distance;
                public int Stages;
                public ProbeStop Stop;
            }
            [LuminPackable] public sealed partial class ProbeStop { public string Name; public int Code; }
            public static class Entry
            {
                public static void Check()
                {
                    ProbeBox<int> value = new ProbeBoxA { V = 42 };
                    var bytes = LuminPackSerializer.Serialize(value);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x6C9033EDu) throw new Exception("First Union hash changed");
                    ProbeBox<int> copy = null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy is not ProbeBoxA { V: 42 }) throw new Exception("Union binary round trip");
                    var existing = copy;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!ReferenceEquals(existing, copy) || copy.V != 42) throw new Exception("Union reuse");
                    value = new ProbeBoxB { V = 73 };
                    bytes = LuminPackSerializer.Serialize(value);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x69902F34u) throw new Exception("Second Union hash changed");
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy is not ProbeBoxB { V: 73 }) throw new Exception("Union case replacement");
                    string json = LuminPackSerializer.SerializeJson(value);
                    copy = null;
                    LuminPackSerializer.DeserializeJson(json, ref copy);
                    if (copy is not ProbeBoxB { V: 73 }) throw new Exception("Union JSON round trip");

                    ProbeBox<int>[] numbers = [null, new ProbeBoxA { V = 42 }, value];
                    ProbeBox<int>[] numberCopies = null;
                    var numberBytes = LuminPackSerializer.Serialize(numbers);
                    LuminPackSerializer.Deserialize(numberBytes, ref numberCopies);
                    if (numberCopies is not [null, ProbeBoxA { V: 42 }, ProbeBoxB { V: 73 }]) throw new Exception("Closed array read");
                    var reused = numberCopies[1];
                    LuminPackSerializer.Deserialize(numberBytes, ref numberCopies);
                    if (!ReferenceEquals(reused, numberCopies[1])) throw new Exception("Closed array case reuse");

                    ProbeBox<string>[] texts = [new ProbeText { V = "\u65E5\u672C\u8A9E" }];
                    ProbeBox<string>[] textCopies = null;
                    var textBytes = LuminPackSerializer.Serialize(texts);
                    LuminPackSerializer.Deserialize(textBytes, ref textCopies);
                    if (textCopies is not [ProbeText { V: "\u65E5\u672C\u8A9E" }]) throw new Exception("Managed closed array read");
                    try
                    {
                        LuminPackSerializer.Deserialize(textBytes, ref numberCopies);
                        throw new Exception("Tag of an unrelated closed Union root was accepted");
                    }
                    catch (Exception error) when (error.Message.Contains("Unknown Union hash")) { }

                    var route = new ProbeRoute { Distance = 500, Stages = 3, Stop = new ProbeStop { Name = "Central", Code = 9 } };
                    var routeBytes = LuminPackSerializer.Serialize(route);
                    ProbeRoute routeCopy = null;
                    LuminPackSerializer.Deserialize(routeBytes, ref routeCopy);
                    if (routeCopy is not { Distance: 500, Stages: 3, Stop: { Name: "Central", Code: 9 } }) throw new Exception("Nested object read");
                    ProbeRouteBase unionRoute = route;
                    ProbeRouteBase unionRouteCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(unionRoute), ref unionRouteCopy);
                    if (unionRouteCopy is not ProbeRoute { Distance: 500, Stages: 3, Stop: { Name: "Central", Code: 9 } }) throw new Exception("Nested payload read");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
            Execute(references, source.Replace("MODE", mode, StringComparison.Ordinal), "Union" + mode, InspectUnion);
        Console.WriteLine("Passed discovered closed generic Union case compilation, reuse, and round trips.");
    }

    /// <summary>Checks that closed Union readers keep only their own hashes and variable cases stay out of the switch.</summary>
    /// <param name="result">The generated consumer source.</param>
    private static void InspectUnion(GeneratorDriverRunResult result)
    {
        var owners = result.GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()).ToArray();
        CheckReader("ProbeBoxOfIntFormatter", 2, outlined: false);
        CheckReader("ProbeBoxOfStringFormatter", 1, outlined: true);
        var nestedRead = owners.Where(item => item.Identifier.ValueText == "ProbeRouteFormatter").SelectMany(item => item.Members)
            .OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.ValueText == "Read")
            .SelectMany(method => method.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            .SingleOrDefault(method => method.Identifier.ValueText == "ReadNested");
        if (nestedRead == null || !nestedRead.AttributeLists.ToString().Contains("NoInlining", StringComparison.Ordinal))
            throw new InvalidOperationException("The two parent framings did not share one outlined child reader.");

        void CheckReader(string owner, int cases, bool outlined)
        {
            var read = owners.Where(item => item.Identifier.ValueText == owner).SelectMany(item => item.Members)
                .OfType<MethodDeclarationSyntax>().SingleOrDefault(method => method.Identifier.ValueText == "Read" && method.Body != null);
            var dispatch = read?.Body?.Statements.OfType<SwitchStatementSyntax>().SingleOrDefault();
            if (dispatch == null || dispatch.Sections.SelectMany(arm => arm.Labels).OfType<CaseSwitchLabelSyntax>().Count() != cases)
                throw new InvalidOperationException(owner + " did not emit its own closed Union switch.");
            if (read!.Body!.Statements.OfType<LocalFunctionStatementSyntax>().Any(method => method.AttributeLists.ToString().Contains("NoInlining", StringComparison.Ordinal)) != outlined)
                throw new InvalidOperationException(owner + " has the wrong case boundary for its payloads.");
        }
    }

    /// <summary>Compiles a Next consumer and executes its checks.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="source">Consumer source.</param>
    /// <param name="name">Unique compilation name.</param>
    /// <param name="inspect">An optional assertion over the emitted source.</param>
    private static void Execute(MetadataReference[] references, string source, string name, Action<GeneratorDriverRunResult>? inspect = null) =>
        CompileConsumer(references, source, name, inspect).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);

    /// <summary>Compiles a Next consumer for independently bound serializer entry points.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="source">Consumer source.</param>
    /// <param name="name">Unique compilation name.</param>
    /// <param name="inspect">An optional assertion over the emitted source.</param>
    /// <returns>The generated consumer assembly.</returns>
    private static Assembly CompileConsumer(MetadataReference[] references, string source, string name, Action<GeneratorDriverRunResult>? inspect = null)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var compilation = TestCompilation.Create("IssueRegression" + name, [CSharpSyntaxTree.ParseText(source, parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        using var image = new MemoryStream();
        var result = output.Emit(image);
        if (!result.Success || diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(result.Diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error)));
        inspect?.Invoke(driver.GetRunResult());
        return Assembly.Load(image.ToArray());
    }
}
