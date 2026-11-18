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
            using System.Collections.Generic;
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
            [LuminPackable, LuminPackUnion(1, typeof(ProbeClaimA)), LuminPackUnion(2, typeof(ProbeClaimB))]
            public abstract partial class ProbeClaimBase { public new Type GetType() => typeof(ProbeClaimB); }
            [LuminPackable] public sealed partial class ProbeClaimA : ProbeClaimBase { public int V; }
            [LuminPackable] public sealed partial class ProbeClaimB : ProbeClaimBase { public int V; }
            public interface IProbeTypeClaim { Type GetType(); }
            [LuminPackable, LuminPackUnion(1, typeof(ProbeExplicitA)), LuminPackUnion(2, typeof(ProbeExplicitB))]
            public partial interface IProbeClaim : IProbeTypeClaim { }
            [LuminPackable] public sealed partial class ProbeExplicitA : IProbeClaim
            {
                public int V;
                Type IProbeTypeClaim.GetType() => typeof(ProbeExplicitB);
            }
            [LuminPackable] public sealed partial class ProbeExplicitB : IProbeClaim
            {
                public int V;
                Type IProbeTypeClaim.GetType() => typeof(ProbeExplicitA);
            }
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

                    List<ProbeRouteBase> routes = [null, unionRoute];
                    List<ProbeRouteBase> routeCopies = null;
                    var routesBytes = LuminPackSerializer.Serialize(routes);
                    LuminPackSerializer.Deserialize(routesBytes, ref routeCopies);
                    if (routeCopies is not [null, ProbeRoute { Stop: { Name: "Central", Code: 9 } }]) throw new Exception("Fresh Union list read");
                    var reusedRoute = routeCopies[1];
                    LuminPackSerializer.Deserialize(routesBytes, ref routeCopies);
                    if (!ReferenceEquals(reusedRoute, routeCopies[1])) throw new Exception("Union list case reuse");

                    ProbeClaimBase claim = new ProbeClaimA { V = 5 };
                    ProbeClaimBase claimCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(claim), ref claimCopy);
                    if (claimCopy is not ProbeClaimA { V: 5 }) throw new Exception("The class GetType member changed binary dispatch");
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(claim), ref claimCopy);
                    if (claimCopy is not ProbeClaimA { V: 5 }) throw new Exception("The class GetType member changed JSON dispatch");

                    IProbeClaim explicitClaim = new ProbeExplicitA { V = 9 };
                    IProbeClaim explicitCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(explicitClaim), ref explicitCopy);
                    if (explicitCopy is not ProbeExplicitA { V: 9 }) throw new Exception("The interface GetType member changed binary dispatch");
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(explicitClaim), ref explicitCopy);
                    if (explicitCopy is not ProbeExplicitA { V: 9 }) throw new Exception("The interface GetType member changed JSON dispatch");
                }
            }
            """;
        // The definition uses virtual slots, while the exercised closed roots each have few enough cases for exact comparisons.
        string unrelatedCases = string.Join("\n", Enumerable.Range(0, 30).Select(index =>
            $"[LuminPackable] public sealed partial class ProbeOther{index} : ProbeBox<byte> {{ }}"));
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
            Execute(references, source.Replace("MODE", mode, StringComparison.Ordinal) + "\n" + unrelatedCases, "Union" + mode, InspectUnion);
        Console.WriteLine("Passed discovered closed generic Union case compilation, reuse, and round trips.");
    }

    /// <summary>Checks the case dispatch and inlining boundaries of closed Union roots and collection elements.</summary>
    /// <param name="result">The generated consumer source.</param>
    private static void InspectUnion(GeneratorDriverRunResult result)
    {
        var owners = result.GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()).ToArray();
        CheckExactWriter("ProbeBoxOfIntFormatter", 2);
        CheckExactWriter("ProbeBoxOfStringFormatter", 1);
        CheckExactWriter("ProbeRouteBaseFormatter", 1);
        CheckExactWriter("ProbeClaimBaseFormatter", 2, "caseValue");
        CheckExactWriter("IProbeClaimFormatter", 2, "caseValue");
        CheckReader("ProbeBoxOfIntFormatter", 2);
        CheckReader("ProbeBoxOfStringFormatter", 1);
        CheckReader("ProbeRouteBaseFormatter", 1);

        var listWrite = Operation("ListOfProbeRouteBaseFormatter", "Write");
        var listRead = Operation("ListOfProbeRouteBaseFormatter", "Read");
        string writeOptions = listWrite.AttributeLists.ToString();
        if (!writeOptions.Contains("NoInlining", StringComparison.Ordinal) || !writeOptions.Contains("AggressiveOptimization", StringComparison.Ordinal))
            throw new InvalidOperationException("The Union list writer did not retain its optimized loop boundary.");
        if (listWrite.DescendantTokens().Any(token => token.ValueText == "loopWriter") ||
            listRead.DescendantTokens().Any(token => token.ValueText == "loopReader"))
            throw new InvalidOperationException("The Union list copied its operation state around case dispatch.");
        if (!listRead.DescendantNodes().OfType<SwitchStatementSyntax>().Any() || HasOutlinedCase(listRead))
            throw new InvalidOperationException("The fresh list read did not keep its Union cases in the loop.");

        var routeMembers = result.GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(item => item.Identifier.ValueText == "ProbeRouteBaseState").SelectMany(item => item.Members)
            .OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.ValueText is "Write" or "Read").ToArray();
        if (routeMembers.Length == 0 || routeMembers.Any(method => !method.AttributeLists.ToString().Contains("AggressiveInlining", StringComparison.Ordinal)))
            throw new InvalidOperationException("The Union member state did not forward through inlined entries.");

        MethodDeclarationSyntax Operation(string owner, string name) =>
            owners.Where(item => item.Identifier.ValueText == owner).SelectMany(item => item.Members)
                .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == name && method.Body != null);

        void CheckExactWriter(string owner, int cases, string receiver = "value")
        {
            var comparisons = Operation(owner, "Write").DescendantNodes().OfType<BinaryExpressionSyntax>()
                .Where(condition => condition.IsKind(SyntaxKind.EqualsExpression) && condition.Right is TypeOfExpressionSyntax).ToArray();
            if (comparisons.Length != cases || comparisons.Any(condition => condition.Left is not InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax candidate, Name.Identifier.ValueText: "GetType" }
                } || candidate.Identifier.ValueText != receiver))
                throw new InvalidOperationException(owner + " did not compare the runtime type directly in every case branch.");
        }

        void CheckReader(string owner, int cases)
        {
            var read = Operation(owner, "Read");
            var dispatch = read.Body!.Statements.OfType<SwitchStatementSyntax>().SingleOrDefault();
            if (dispatch == null || dispatch.Sections.SelectMany(arm => arm.Labels).OfType<CaseSwitchLabelSyntax>().Count() != cases)
                throw new InvalidOperationException(owner + " did not emit its own closed Union switch.");
            if (HasOutlinedCase(read))
                throw new InvalidOperationException(owner + " outlined a case read from its Union switch.");
        }

        static bool HasOutlinedCase(MethodDeclarationSyntax read) =>
            read.Body!.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Any(method =>
                method.Identifier.ValueText.Contains("ReadCase", StringComparison.Ordinal));
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
