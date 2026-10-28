using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks generated tier selection, independent overflow loops and bound invalidation.</summary>
internal static class SerializerCapacityTierRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.CSharp13, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using LuminPack;
        using LuminPack.Attribute;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable, LuminPackBounded(128)] public sealed class BoundedLeaf { public int Id; public string Text = "text"; }
        [LuminPackable, LuminPackBounded(8192)] public sealed class BoundedRoot
        { public string Text = "text"; public string[] Names = new[] { "a", "b" }; public List<BoundedLeaf> Values = new(); }
        [LuminPackable, LuminPackUnion(1, typeof(Case))] public abstract partial class Root { }
        [LuminPackable] public sealed partial class Case : Root { public int Id; }
        [LuminPackable] public sealed class Graph
        { public List<BoundedLeaf> Values; public HashSet<int> Numbers; public List<Root> Cases; public List<List<BoundedLeaf>> Nested; }
        public static class Entry
        {
            public static void Check()
            {
                var bounded = new BoundedRoot { Values = new List<BoundedLeaf> { new BoundedLeaf { Id = 17 } } };
                var copy = LuminPackSerializer.Deserialize<BoundedRoot>(LuminPackSerializer.Serialize(bounded));
                if (copy.Values[0].Id != 17) throw new Exception("The bounded record changed.");
                _ = LuminPackSerializer.Serialize(new List<BoundedLeaf> { new BoundedLeaf() });
                _ = LuminPackSerializer.Serialize(new List<string> { null, "", "text" });
                _ = LuminPackSerializer.Serialize(new HashSet<int> { 3, 7 });
                _ = LuminPackSerializer.Serialize(new HashSet<BoundedLeaf> { new BoundedLeaf() });
                _ = LuminPackSerializer.Serialize(new List<Root> { new Case { Id = 23 } });
                _ = LuminPackSerializer.Serialize(new List<List<BoundedLeaf>> { new() { new() } });
                _ = LuminPackSerializer.Serialize(new Graph { Values = new(), Numbers = new(), Cases = new(), Nested = new() });
            }
        }
        """;

    /// <summary>Checks every pruning mode, cold fallback structure and retained-versus-fresh attribute edits.</summary>
    /// <param name="references">Current runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var input = Compilation(references, Source.Replace("MODE", mode));
            var driver = Driver().RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.Identifier.ValueText == "RootWrite" && method.Body?.Statements.Count > 1 && method.ParameterList.Parameters.Count == 2 &&
                    method.ParameterList.Parameters[0].Type!.ToString().Contains("LuminPackWriter", StringComparison.Ordinal)).ToArray();
            var bounded = methods.Single(method => Payload(method) == "global::BoundedRoot");
            Require(Calls(bounded.Body!, "EnsureAdditionalCapacity").Count() == 1 && Calls(bounded.Body!, "CompleteReservedRecord").Count() == 1 &&
                !Calls(bounded.Body!, "TryEnsureCapacity").Any() && Calls(bounded.Body!, "Write").Any(call =>
                    call.ArgumentList.Arguments.LastOrDefault()?.Expression.IsKind(SyntaxKind.TrueLiteralExpression) == true),
                "A complete guaranteed record retained an internal container check or lost its root reservation.");
            var strings = methods.Single(method => Payload(method) == "global::System.Collections.Generic.List<string>");
            Require(!Calls(strings.Body!, "TryEnsureCapacity").Any() && Calls(strings.Body!, "Write").Any(),
                "Complete string operations must retain one collection traversal without a length prescan.");
            // A raw-scalar set is one contiguous record, reserved once like a raw list, with no per-element tier loop.
            var rawSet = methods.Single(method => Payload(method) == "global::System.Collections.Generic.HashSet<int>");
            Require(Calls(rawSet.Body!, "WriteHashSetRaw").Count() == 1 && !Calls(rawSet.Body!, "TryEnsureCapacity").Any() && !Calls(rawSet.Body!, "CheckBuffer").Any(),
                "A raw-scalar set lost its contiguous record write.");
            foreach (string payload in new[] { "global::System.Collections.Generic.List<global::BoundedLeaf>",
                "global::System.Collections.Generic.HashSet<global::BoundedLeaf>" })
            {
                var match = methods.SingleOrDefault(method => Payload(method) == payload);
                Require(match != null, "Missing " + payload + "; emitted: " + string.Join("; ", methods.Select(Payload)));
                var body = match!.Body!;
                var reservation = body.DescendantNodes().OfType<IfStatementSyntax>().FirstOrDefault(statement =>
                    statement.Condition is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "TryEnsureCapacity" } });
                Require(reservation != null, "No static reservation was generated for " + payload);
                Require(!Calls(reservation!.Statement, "CheckBuffer").Any() && !Calls(reservation.Statement, "EnsureAdditionalCapacity").Any(),
                    "A reserved element loop retained a capacity check: " + payload);
                Require(reservation.Else != null, "The size-limit fallback was removed.");
                if (!payload.EndsWith("List<string>", StringComparison.Ordinal))
                    Require(Calls(reservation.Else!.Statement, "CheckBuffer").Any(), "The cold traversal lost its element guards.");
            }
            var union = methods.Single(method => Payload(method) == "global::System.Collections.Generic.List<global::Root>");
            Require(!Calls(union.Body!, "TryEnsureCapacity").Any() && Calls(union.Body!, "CheckBuffer").Any(),
                "An unbounded virtual Union was treated as a fixed payload.");
            Execute(output);
            var tree = input.SyntaxTrees.Single();
            var changed = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(tree.ToString().Replace("Bounded(128)", "Bounded(192)"), Parse, "Models.cs"));
            var retained = driver.RunGeneratorsAndUpdateCompilation(changed, out var updated, out _);
            var fresh = Driver().RunGenerators(changed);
            Require(Files(retained).SequenceEqual(Files(fresh)), "An edited size guarantee retained stale container bounds.");
            Require(Files(retained).Any(source => source.Contains("192", StringComparison.Ordinal)), "The new bound was not emitted.");
            Execute(updated);
        }
        var invalid = Driver().RunGenerators(Compilation(references, Source.Replace("MODE", "Full").Replace("Bounded(128)", "Bounded(0)")));
        Require(invalid.GetRunResult().Diagnostics.Any(diagnostic => diagnostic.Id == "LuminPack109"), "An invalid guarantee did not produce its diagnostic.");
        Console.WriteLine("Passed static capacity tiers, check-free reserved loops, overflow fallback structure, root guarantees and retained/fresh bound edits in Full/Medium/Light/Minimal.");
    }

    /// <summary>Finds compiler-emitted invocations of one writer operation.</summary>
    /// <param name="node">The generated operation or branch.</param>
    /// <param name="name">The exact writer member name.</param>
    /// <returns>The matching invocations.</returns>
    private static IEnumerable<InvocationExpressionSyntax> Calls(SyntaxNode node, string name) => node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
        .Where(call => call.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == name);

    /// <summary>Gets the concrete generated payload type.</summary>
    /// <param name="method">A state serializer overload.</param>
    /// <returns>The normalized nonnullable payload spelling.</returns>
    private static string Payload(MethodDeclarationSyntax method) => method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?');

    /// <summary>Creates an independent consumer without runtime type probing in its generated loops.</summary>
    /// <param name="references">Current runtime references.</param>
    /// <param name="source">The complete fixture.</param>
    /// <returns>The consumer compilation.</returns>
    private static CSharpCompilation Compilation(MetadataReference[] references, string source) => CSharpCompilation.Create("CapacityTiers_" + Guid.NewGuid().ToString("N"),
        [CSharpSyntaxTree.ParseText(source, Parse, "Models.cs")], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));

    /// <summary>Creates an independent incremental driver.</summary>
    /// <returns>The complete pair of source generators.</returns>
    private static GeneratorDriver Driver() => CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);

    /// <summary>Gets stable generated output for retained-versus-fresh comparison.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>Ordered names and complete source bytes.</returns>
    private static string[] Files(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Compiles and executes actual generated serializer overloads.</summary>
    /// <param name="compilation">The generated consumer.</param>
    private static void Execute(Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Reports an actionable structural or execution mismatch.</summary>
    /// <param name="condition">The invariant being checked.</param>
    /// <param name="message">The failure explanation.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
