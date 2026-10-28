using System.Reflection;
using LuminPack.Core;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Retains public priority-queue traversal when the referenced runtime lacks its optional heap helper.</summary>
internal static class SerializerPriorityQueueCompatibilityRegression
{
    /// <summary>Checks both runtime references and retained-driver invalidation after changing the runtime capability.</summary>
    /// <param name="references">The platform and current modern runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            [LuminPackable] public sealed class QueueHolder { public PriorityQueue<int,int> Queue; }
            public static class PriorityCases
            {
                public static byte[] Write(PriorityQueue<int,int> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Nested(QueueHolder value)=>LuminPackSerializer.Serialize(value);
                public static void Check()
                {
                    var value=new PriorityQueue<int,int>();value.Enqueue(19,7);value.Enqueue(23,3);value.Enqueue(31,5);
                    byte[] bytes=Write(value);PriorityQueue<int,int> copy=null;LuminPackSerializer.Deserialize(bytes,ref copy);
                    if(copy.Count!=3||copy.Dequeue()!=23||copy.Dequeue()!=31||copy.Dequeue()!=19)throw new Exception("Priority queue order changed");
                    var holder=new QueueHolder{Queue=value};byte[] nested=Nested(holder);QueueHolder restored=null;LuminPackSerializer.Deserialize(nested,ref restored);
                    if(restored.Queue.Count!=3||restored.Queue.Dequeue()!=23)throw new Exception("Nested priority queue changed");
                }
            }
            """;
        CheckCapacityTiers(references, source);
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        string runtime = typeof(LuminPackWriter).Assembly.Location;
        string compatibilityPath = Path.GetFullPath("src/LuminPack.Core/bin/Release/netstandard2.1/LuminPack.Core.dll");
        Require(File.Exists(compatibilityPath), "Build the netstandard2.1 runtime before the targeted compatibility regression.");
        var originalRuntime = references.Single(reference => string.Equals(reference.Display, runtime, StringComparison.OrdinalIgnoreCase));
        var compatibilityReferences = references.Where(reference => !ReferenceEquals(reference, originalRuntime))
            .Append(MetadataReference.CreateFromFile(compatibilityPath)).ToArray();
        var compatibility = CSharpCompilation.Create("PriorityCompatibility", [CSharpSyntaxTree.ParseText(source, parse)], compatibilityReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        var (driver, output) = Generate(compatibility, parse);
        Require(!Trees(driver).Any(text => text.Contains("TryGetPriorityQueueNodes", StringComparison.Ordinal)), "The pointer runtime acquired an unavailable helper call.");
        Require(Trees(driver).Any(text => text.Contains("UnorderedItems", StringComparison.Ordinal)), "The established enumeration fallback disappeared.");
        using (var image = new MemoryStream())
        {
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        }
        var modern = compatibility.WithReferences(references);
        driver = driver.RunGeneratorsAndUpdateCompilation(modern, out var updated, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        var (fresh, _) = Generate(modern, parse);
        Require(Trees(driver).Any(text => text.Contains("TryGetPriorityQueueNodes", StringComparison.Ordinal)), "The modern runtime lost its established heap access.");
        Require(Trees(driver).SequenceEqual(Trees(fresh), StringComparer.Ordinal), "Changing runtime capability produced stale retained output.");
        using (var image = new MemoryStream())
        {
            var emitted = updated.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("PriorityCases")!.GetMethod("Check")!.Invoke(null, null);
        }
        Console.WriteLine("Passed optional priority-queue heap capability, netstandard fallback compilation, modern execution and retained/fresh reference invalidation.");
    }

    /// <summary>Retains finite captured ranges and checked variable contracts in all generation tiers.</summary>
    /// <param name="references">The current platform and runtime metadata.</param>
    /// <param name="source">The executable queue fixture.</param>
    private static void CheckCapacityTiers(MetadataReference[] references, string source)
    {
        const string additional = """
            [LuminPack.Attribute.LuminPackable] public sealed class VariableElement { public string Text; }
            public static class AdditionalQueues
            {
                public static byte[] Wide(System.Collections.Generic.PriorityQueue<System.Guid,long> value)=>LuminPack.LuminPackSerializer.Serialize(value);
                public static int WideRead(System.ReadOnlySpan<byte> bytes,ref System.Collections.Generic.PriorityQueue<System.Guid,long> value)=>LuminPack.LuminPackSerializer.Deserialize(bytes,ref value);
                public static byte[] Text(System.Collections.Generic.PriorityQueue<string,int> value)=>LuminPack.LuminPackSerializer.Serialize(value);
                public static byte[] Variable(System.Collections.Generic.PriorityQueue<VariableElement,int> value)=>LuminPack.LuminPackSerializer.Serialize(value);
                public static int NullableRead(System.ReadOnlySpan<byte> bytes,ref System.Collections.Generic.PriorityQueue<int?,int> value)=>LuminPack.LuminPackSerializer.Deserialize(bytes,ref value);
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            string text = source.Replace("GenerationMode.Minimal", "GenerationMode." + mode, StringComparison.Ordinal) + additional;
            var input = CSharpCompilation.Create("PriorityTiers_" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Queues.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var (driver, output) = Generate(input, parse);
            var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(item => item.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            foreach (string pair in new[] { "int, int", "global::System.Guid, long" })
            {
                var write = Root(methods, "RootWrite", pair);
                var reservations = Calls(write, "TryEnsureCapacity").ToArray();
                Require(reservations.Length == 2, "A priority queue lost its captured-range or enumeration capacity tier: " + mode);
                foreach (var reservation in reservations)
                {
                    var branch = reservation.Ancestors().OfType<IfStatementSyntax>().First();
                    Require(!Calls(branch.Statement, "CheckBuffer").Any() && branch.Else is not null && Calls(branch.Else.Statement, "CheckBuffer").Any(),
                        "The reserved queue loop retained checks or lost the independent checked fallback.");
                }
                Require(write.ToString().Contains("= nodeCount;", StringComparison.Ordinal), "The queue bound rereads live Count instead of its captured heap range.");
                var read = Root(methods, "RootRead", pair);
                var width = Calls(read, "ReadLength").Single();
                var rent = Calls(read, "Rent").Single();
                Require(width.ArgumentList.Arguments.Count == 1 && width.SpanStart < rent.SpanStart && Calls(read, "Read").Count() >= 2,
                    "The raw queue lost its full-width pre-rent validation or individual field reads.");
            }
            foreach (string pair in new[] { "string, int", "global::VariableElement, int" })
                Require(!Calls(Root(methods, "RootWrite", pair), "TryEnsureCapacity").Any(), "An unbounded queue acquired an unsafe Count reservation.");
            var nullable = Root(methods, "RootRead", "int?, int");
            Require(!Calls(nullable, "ReadLength").Any(call => call.ArgumentList.Arguments.Count == 1), "Nullable field framing was replaced with tuple storage width.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("PriorityCases")!.GetMethod("Check")!.Invoke(null, null);
            var changed = input.ReplaceSyntaxTree(input.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(text.Replace("sealed class VariableElement { public string Text; }", "struct VariableElement { public int Text; }", StringComparison.Ordinal), parse, "Queues.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(changed, out _, out _);
            var changedMethods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(item => item.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            Require(Calls(Root(changedMethods, "RootWrite", "global::VariableElement, int"), "TryEnsureCapacity").Any(),
                "Changing an unbounded reference element to raw fixed storage did not enable its finite queue reservation.");
            var (fresh, _) = Generate(changed, parse);
            Require(Trees(driver).SequenceEqual(Trees(fresh), StringComparer.Ordinal), "A queue element edit retained stale capacity analysis.");
        }
        Console.WriteLine("Passed priority-queue capacity tiers, captured counts, independent fallback loops, pre-rent payload validation and retained/fresh element edits in all four modes.");
    }

    /// <summary>Finds the unique complete binary root for one closed queue.</summary>
    /// <param name="methods">The generated methods.</param>
    /// <param name="name">The root operation.</param>
    /// <param name="pair">The independently formatted element and priority spelling.</param>
    /// <returns>The complete concrete root.</returns>
    private static MethodDeclarationSyntax Root(MethodDeclarationSyntax[] methods, string name, string pair)
        => methods.Single(method => method.Identifier.ValueText == name && method.Body is not null &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.Collections.Generic.PriorityQueue<" + pair + ">");

    /// <summary>Finds exact operation calls inside a generated body.</summary>
    /// <param name="node">The generated method or branch.</param>
    /// <param name="name">The exact called member name.</param>
    /// <returns>All matching invocations.</returns>
    private static IEnumerable<InvocationExpressionSyntax> Calls(SyntaxNode node, string name)
        => node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Where(call =>
            call.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == name);

    /// <summary>Runs both production generators against one selected runtime.</summary>
    /// <param name="compilation">The targeted consumer compilation.</param>
    /// <param name="parse">The consumer parse options.</param>
    /// <returns>The retained driver and generated output.</returns>
    private static (GeneratorDriver Driver, Compilation Output) Generate(CSharpCompilation compilation, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        return (driver, output);
    }

    /// <summary>Orders complete generated files by stable hint identity.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>The complete symbol-free output for comparison.</returns>
    private static string[] Trees(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + "\n" + result.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports an exact compatibility or cache failure.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The diagnostic detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
