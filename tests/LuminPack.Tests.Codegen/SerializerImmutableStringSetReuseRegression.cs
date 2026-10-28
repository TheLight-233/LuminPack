using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects immutable string set reuse qualification and incremental invalidation.</summary>
internal static class SerializerImmutableStringSetReuseRegression
{
    /// <summary>Compiles four tiers and compares retained and fresh generation after element-contract edits.</summary>
    /// <param name="references">The platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Immutable;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using Element = System.String;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
            public struct Portable { public byte Tag; public long Value; }
            public static class ImmutableSetEntry
            {
                public static int Sorted(ReadOnlySpan<byte> bytes, ref ImmutableSortedSet<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Hashed(ReadOnlySpan<byte> bytes, ref ImmutableHashSet<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int TextSorted(ReadOnlySpan<byte> bytes, ref ImmutableSortedSet<int?> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int TextHashed(ReadOnlySpan<byte> bytes, ref ImmutableHashSet<int?> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static void Check()
                {
                    var sorted = ImmutableSortedSet.CreateRange(new Element[1]);
                    var hashed = ImmutableHashSet.CreateRange(new Element[1]);
                    var a = sorted;
                    var b = hashed;
                    byte[] bytes = LuminPackSerializer.Serialize(sorted);
                    LuminPackSerializer.Deserialize(bytes, ref a);
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(hashed), ref b);
                    if (!ReferenceEquals(sorted, a) || !ReferenceEquals(hashed, b)) throw new Exception("Equal string set storage changed.");
                    ImmutableSortedSet<Element> freshSorted = null, nextSorted = null;
                    ImmutableHashSet<Element> freshHash = null, nextHash = null;
                    Sorted(bytes, ref freshSorted);
                    Sorted(bytes, ref nextSorted);
                    Hashed(LuminPackSerializer.Serialize(hashed), ref freshHash);
                    Hashed(LuminPackSerializer.Serialize(hashed), ref nextHash);
                    if (!ReferenceEquals(freshSorted, nextSorted) || !ReferenceEquals(freshHash, nextHash)) throw new Exception("Validated weak immutable candidates were not shared.");

                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview,
                preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("ImmutableSetReuse" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckSelection(driver, scalar: true);
            Assembly.Load(Emit(output)).GetType("ImmutableSetEntry")!.GetMethod("Check")!.Invoke(null, null);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(
                text.Replace("using Element = System.String;", "using Element = Portable;", StringComparison.Ordinal), parse, "Entry.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out output, out diagnostics);
            CheckSelection(driver, scalar: false);
            Emit(output);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(edited))), "A portable edit retained stale immutable set reuse.");
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics);
            CheckSelection(driver, scalar: true);
            Emit(output);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(compilation))), "Restoring scalars retained stale immutable set exclusions.");
        }
        Console.WriteLine("Passed immutable string set reuse, exclusions, four tiers and retained/fresh portable edits.");
    }

    /// <summary>Checks reuse gates, complete string reading and pool cleanup without broadening non-string roots.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="scalar">Whether the selected alias is a known framework scalar.</param>
    private static void CheckSelection(GeneratorDriver driver, bool scalar)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                     .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                     .Where(method => method.Identifier.ValueText == "RootRead" && method.ParameterList.Parameters.Count == 2))
        {
            string type = method.ParameterList.Parameters[1].Type!.ToString();
            if (!type.Contains(".ImmutableSortedSet<", StringComparison.Ordinal) && !type.Contains(".ImmutableHashSet<", StringComparison.Ordinal)) continue;
            bool expected = scalar && type.Contains("<string>", StringComparison.Ordinal);
            bool cached = expected;
            string body = method.ToString();
            Require(body.Contains("TryGetTarget(out candidate)", StringComparison.Ordinal) == cached, "Weak immutable candidate qualification differs: " + type);
            Require(body.Contains("bool hasPrior = same", StringComparison.Ordinal) == expected, "Immutable set reuse qualification differs: " + type);
            if (expected)
            {
                Require(body.Contains("ReferenceEquals(candidate.KeyComparer", StringComparison.Ordinal), "A destination comparer lost its exclusion gate.");
                Require(body.Contains("ReferenceEquals(prior, selected)", StringComparison.Ordinal), "Immutable string equality lost its complete candidate operation.");
                if (type.Contains("ImmutableSortedSet<", StringComparison.Ordinal))
                    Require(method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsOrderingProof), "Sorted strings lost the current culture ordering proof.");
                Require(body.Contains("ReadLength", StringComparison.Ordinal), "Immutable string input lost its minimum preallocation validation.");
                Require(method.DescendantNodes().OfType<FinallyClauseSyntax>().Count() >= 1, "An immutable set staging rental lost cleanup.");
            }
            selected.Add(type);
        }
        Require(selected.Count == 4, "The immutable set fixture omitted a selected or excluded root.");
    }

    /// <summary>Recognizes the adjacent-element ordering guard independently of generated loop-local names.</summary>
    /// <param name="call">A generated invocation within the sorted string root.</param>
    /// <returns>Whether the current comparer rejects nonascending adjacent staged values.</returns>
    private static bool IsOrderingProof(InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Compare" } ||
            call.Parent is not BinaryExpressionSyntax comparison || !comparison.IsKind(SyntaxKind.GreaterThanOrEqualExpression) ||
            comparison.Right is not LiteralExpressionSyntax { Token.ValueText: "0" } || call.ArgumentList.Arguments.Count != 2 ||
            call.ArgumentList.Arguments[0].Expression is not ElementAccessExpressionSyntax left ||
            call.ArgumentList.Arguments[1].Expression is not ElementAccessExpressionSyntax right ||
            left.Expression.ToString() != "items" || right.Expression.ToString() != "items" ||
            left.ArgumentList.Arguments.Count != 1 || right.ArgumentList.Arguments.Count != 1 ||
            left.ArgumentList.Arguments[0].Expression is not BinaryExpressionSyntax previous || !previous.IsKind(SyntaxKind.SubtractExpression) ||
            previous.Right is not LiteralExpressionSyntax { Token.ValueText: "1" }) return false;
        return previous.Left.ToString() == right.ArgumentList.Arguments[0].Expression.ToString();
    }

    /// <summary>Creates a generator driver for an independent retained or fresh run.</summary>
    /// <param name="parse">The consumer language and target symbols.</param>
    /// <returns>The two-generator driver.</returns>
    private static GeneratorDriver Create(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Emits one complete generated consumer and exposes compiler failures.</summary>
    /// <param name="output">The generated compilation.</param>
    /// <returns>The consumer image.</returns>
    private static byte[] Emit(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        return image.ToArray();
    }

    /// <summary>Captures deterministic generated text for retained and fresh comparisons.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>The ordered source snapshot.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a focused invariant failure.</summary>
    /// <param name="condition">Whether the invariant holds.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
