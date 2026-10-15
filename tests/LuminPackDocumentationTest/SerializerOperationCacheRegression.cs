using System.Reflection;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks embedded operation invalidation, concurrent facade isolation, and symbol-free plan cache lifetime.</summary>
internal static class SerializerOperationCacheRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Exercises the actual facade pipeline and retained plan cache across edits and independent compilations.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="compatibilityReferences">Optional older framework and runtime metadata for C# 10 compilation.</param>
    internal static void Run(MetadataReference[] references, MetadataReference[]? compatibilityReferences = null)
    {
        var before = Create(references, "OperationDependencies", "Value");
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(before, out var output, out _);
        CheckErrors(output, driver);
        var initial = Files(driver);
        var previousChild = before.SyntaxTrees.Single(tree => tree.FilePath == "Child.cs");
        var after = before.ReplaceSyntaxTree(previousChild, CSharpSyntaxTree.ParseText(previousChild.ToString().Replace("Value", "Renamed", StringComparison.Ordinal), Parse, "Child.cs"));
        driver = driver.RunGeneratorsAndUpdateCompilation(after, out output, out _);
        CheckErrors(output, driver);
        var changed = Files(driver);
        var fresh = Files(NewDriver().RunGenerators(after));
        Require(changed.OrderBy(pair => pair.Key).SequenceEqual(fresh.OrderBy(pair => pair.Key)), "An embedded child change retained stale operation text.");
        string parent = PayloadFile(initial, "global::Parent");
        string independent = PayloadFile(initial, "global::Independent");
        Require(initial[parent] != changed[parent] && changed[parent].Contains("Renamed", StringComparison.Ordinal), "Parent entry did not rebuild its embedded child operations.");
        Require(initial[independent] == changed[independent], "A child edit changed an independent facade partition.");

        // Current .NET 11 reference metadata itself uses C# 12 ref-readonly parameters.
        // A C# 10 execution gate therefore requires matching older framework references.
        var compatibilityParse = new CSharpParseOptions(compatibilityReferences == null ? LanguageVersion.CSharp12 : LanguageVersion.CSharp10, DocumentationMode.Parse);
        var compatibility = before.RemoveAllReferences().AddReferences(compatibilityReferences ?? references).RemoveAllSyntaxTrees().AddSyntaxTrees(before.SyntaxTrees.Select(tree =>
            CSharpSyntaxTree.ParseText(tree.ToString(), compatibilityParse, tree.FilePath)));
        GeneratorDriver compatibilityDriver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: compatibilityParse);
        compatibilityDriver = compatibilityDriver.RunGeneratorsAndUpdateCompilation(compatibility, out var compatibilityOutput, out _);
        CheckErrors(compatibilityOutput, compatibilityDriver);

        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 4 }, index =>
        {
            var input = Create(references, "ConcurrentOperations" + index, "Field" + index);
            var concurrent = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var compiled, out _);
            CheckErrors(compiled, concurrent);
            var files = Files(concurrent);
            string source = files[PayloadFile(files, "global::Parent")];
            Require(source.Contains("Field" + index, StringComparison.Ordinal), "Parallel operation plans used another compilation's field contract.");
            foreach (var other in Enumerable.Range(0, 8).Where(other => other != index))
                Require(!source.Contains("Field" + other, StringComparison.Ordinal), "Parallel concrete operation identities leaked across compilations.");
        });

        var lifetime = CreatePlanCache(references);
        for (int attempt = 0; attempt < 8 && lifetime.Compilation.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Require(!lifetime.Compilation.IsAlive, "A completed operation-plan cache retained its original compilation.");
        GC.KeepAlive(lifetime.Cache);
        Console.WriteLine("Passed embedded-child facade invalidation, eight parallel facade compilations, independent outputs, and operation cache compilation lifetime.");
    }

    /// <summary>Builds plans through the production cache and discards all transient semantic inputs.</summary>
    /// <param name="references">Current runtime references.</param>
    /// <returns>The retained cache and a weak handle to its original compilation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Cache, WeakReference Compilation) CreatePlanCache(MetadataReference[] references)
    {
        var input = Create(references, "PlanLifetime", "Value");
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.BinaryOperationPlanCache", throwOnError: true)!;
        var cache = Activator.CreateInstance(type, nonPublic: true)!;
        var get = type.GetMethod("Get", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var metadata = new MetaInfo(Parse, Parse.LanguageVersion, true, true, true);
        get.Invoke(cache, [input.GetTypeByMetadataName("Parent")!, input, metadata]);
        get.Invoke(cache, [input.GetTypeByMetadataName("Child")!, input, metadata]);
        return (cache, new WeakReference(input));
    }

    /// <summary>Creates independent file identities for an embedded child and unrelated payload.</summary>
    /// <param name="references">Compiler references.</param>
    /// <param name="assembly">The unique consumer assembly identity.</param>
    /// <param name="field">The child field name.</param>
    /// <returns>The production input compilation.</returns>
    private static CSharpCompilation Create(MetadataReference[] references, string assembly, string field)
        => CSharpCompilation.Create(assembly,
            [CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public sealed class Child { public int " + field + "; public string Text = \"child\"; }", Parse, "Child.cs"),
             CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public sealed class Parent { public System.Collections.Generic.List<Child> First = new(); public System.Collections.Generic.List<Child> Second = new(); public System.Collections.Generic.List<int> Numbers = new(); }", Parse, "Parent.cs"),
             CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public struct Independent { public long Item; }", Parse, "Independent.cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    /// <summary>Creates a generator driver covering both type and concrete facade emission.</summary>
    /// <returns>An independent driver.</returns>
    private static GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: Parse);

    /// <summary>Captures exact generated output values.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>Hint names and complete source text.</returns>
    private static Dictionary<string, string> Files(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .ToDictionary(file => file.HintName, file => file.SourceText.ToString(), StringComparer.Ordinal);

    /// <summary>Finds a concrete payload's stable serializer partition.</summary>
    /// <param name="files">Generated files.</param>
    /// <param name="type">The canonical payload type.</param>
    /// <returns>The concrete partition hint name.</returns>
    private static string PayloadFile(Dictionary<string, string> files, string type) => files.Single(pair => pair.Key.StartsWith("LuminPackSerializer.", StringComparison.Ordinal) &&
        CSharpSyntaxTree.ParseText(pair.Value, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method =>
            method.Identifier.ValueText == "Serialize" && method.TypeParameterList == null && method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == type))).Key;

    /// <summary>Rejects generator failures and invalid generated declarations.</summary>
    /// <param name="output">The generated compilation.</param>
    /// <param name="driver">The completed production driver.</param>
    private static void CheckErrors(Compilation output, GeneratorDriver driver)
    {
        Require(driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(result => result.Exception)));
        var errors = output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error + Environment.NewLine +
            (error.Location.SourceTree == null ? string.Empty : error.Location.SourceTree.GetText().Lines[error.Location.GetLineSpan().StartLinePosition.Line].ToString()))));
    }

    /// <summary>Reports an operation-cache invariant failure.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
