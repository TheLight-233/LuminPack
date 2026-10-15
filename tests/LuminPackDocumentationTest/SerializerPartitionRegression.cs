using System.Reflection;
using System.Text.RegularExpressions;
using LuminPack.Code.Core;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks stable facade partitions, unchanged legacy method bodies, and independent source publication.</summary>
internal static class SerializerPartitionRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Verifies file identity and compiler output across member, call, and unrelated edits.</summary>
    /// <param name="references">Platform and runtime references for the regression compilation.</param>
    internal static void Run(MetadataReference[] references)
    {
        var files = new Dictionary<string, string>
        {
            ["A.cs"] = "[LuminPack.Attribute.LuminPackable] public sealed class A { public int Value = 7; }",
            ["B.cs"] = "[LuminPack.Attribute.LuminPackable] public sealed class B { public long Other; public int[] Values = []; public System.Collections.Generic.List<int> Items = []; }",
            ["Names.cs"] = "namespace One { [LuminPack.Attribute.LuminPackable] public sealed class Same { public int Value; } } namespace Two { [LuminPack.Attribute.LuminPackable] public sealed class Same { public long Value; } }",
            ["Utility.cs"] = "internal static class Utility { internal static int Touch() => 17; }",
        };
        var input = CSharpCompilation.Create("FacadePartitions", files.Select(file => CSharpSyntaxTree.ParseText(file.Value, Parse, file.Key)), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var initial = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var initialOutput, out _);
        CheckErrors(initialOutput);
        var initialFiles = FacadeFiles(initial);
        Require(initialFiles.ContainsKey("LuminPackSerializer.g.cs") && initialFiles.Count > 4, "Expected common support and independently named concrete files.");
        foreach (string name in initialFiles.Keys.Where(name => name != "LuminPackSerializer.g.cs"))
            Require(Regex.IsMatch(name, "^LuminPackSerializer\\.[0-9a-f]{64}\\.g\\.cs$"), "A concrete filename does not encode a complete stable identity: " + name);
        Require(initialFiles.Values.All(text => text.Contains("public static partial class LuminPackSerializer", StringComparison.Ordinal)), "Facade files must share the same partial type.");
        Require(!initialFiles["LuminPackSerializer.g.cs"].Contains("global::A value", StringComparison.Ordinal), "Common facade support contains a concrete payload body.");

        var metadata = new MetaInfo(Parse, Parse.LanguageVersion, true, true, true);
        string legacy = LuminPackSerializerGenerator.GenerateSerializerClass(input, metadata);
        var expectedMethods = MethodInventory([legacy]);
        var actualMethods = MethodInventory(initialFiles.Values);
        Require(expectedMethods.Count == actualMethods.Count && expectedMethods.All(pair => actualMethods.GetValueOrDefault(pair.Key) == pair.Value),
            "Partitioning changed a concrete/generic method signature, implementation, attribute, default value, or XML contract.");
        Require(PayloadFiles(initialFiles, "global::One.Same").Single() != PayloadFiles(initialFiles, "global::Two.Same").Single(), "Same simple names collided across namespaces.");

        VerifyEdit("unrelated body", input, Replace(input, "Utility.cs", "17", "18"), expectedFiles: 0, expectedFragments: 0);
        VerifyEdit("model initializer", input, Replace(input, "A.cs", "= 7", "= 8"), expectedFiles: 0, expectedFragments: 1);
        VerifyEdit("independent model member", input, Replace(input, "A.cs", "int Value", "int Renamed"), expectedFiles: 1, expectedFragments: 1);
        var added = input.AddSyntaxTrees(CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public sealed class Added { public int NewValue; }", Parse, "Added.cs"));
        var addition = NewDriver().RunGeneratorsAndUpdateCompilation(added, out var additionOutput, out _);
        CheckErrors(additionOutput);
        var additionFiles = FacadeFiles(addition);
        Require(initialFiles.All(pair => additionFiles.GetValueOrDefault(pair.Key) == pair.Value), "Adding a type renamed or rewrote an existing facade partition.");
        VerifyLayoutPartitions(references);
        VerifyFormatterPartitions(references);
        Console.WriteLine("Passed facade partition signatures/bodies/XML, stable identities, arrays, independent edits, and unchanged existing files after type addition.");
    }

    /// <summary>Checks shared mirrors, base storage, and independent layout publication after a private-field edit.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyLayoutPartitions(MetadataReference[] references)
    {
        var sources = new Dictionary<string, string>
        {
            ["A.cs"] = "[LuminPack.Attribute.LuminPackable] public class A { [LuminPack.Attribute.LuminPackInclude] private int Value; public int Other; }",
            ["B.cs"] = "[LuminPack.Attribute.LuminPackable] public class B { [LuminPack.Attribute.LuminPackInclude] private long Independent; }",
            ["Shared.cs"] = "[LuminPack.Attribute.LuminPackable] public class Shared { [LuminPack.Attribute.LuminPackInclude] private long SharedValue; }",
            ["Parents.cs"] = "[LuminPack.Attribute.LuminPackable] public class First { public Shared Item = new(); } [LuminPack.Attribute.LuminPackable] public class Second { public Shared Item = new(); }",
            ["Base.cs"] = "[LuminPack.Attribute.LuminPackable] public class Base { [LuminPack.Attribute.LuminPackInclude] private int BaseValue; } [LuminPack.Attribute.LuminPackable] public class Derived : Base { [LuminPack.Attribute.LuminPackInclude] private long DerivedValue; }",
        };
        var input = CSharpCompilation.Create("LayoutPartitions", sources.Select(pair => CSharpSyntaxTree.ParseText(pair.Value, Parse, pair.Key)), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        CheckErrors(output);
        var before = AllFiles(driver);
        var layoutFiles = before.Where(pair => pair.Key.StartsWith("LuminPack.LocalLayouts.", StringComparison.Ordinal)).ToArray();
        Require(layoutFiles.Length >= 5 && !before.ContainsKey("LuminPack.LocalLayouts.g.cs"), "Expected independently named layout declarations.");
        var mirrors = layoutFiles.SelectMany(pair => CSharpSyntaxTree.ParseText(pair.Value, Parse).GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            .Where(type => type.Identifier.ValueText.StartsWith("Local", StringComparison.Ordinal)).ToArray();
        Require(mirrors.Select(type => type.Identifier.ValueText).Distinct(StringComparer.Ordinal).Count() == mirrors.Length,
            "Shared or inherited layout declarations were emitted more than once.");
        var a = mirrors.Single(type => type.Identifier.ValueText == "LocalA");
        Require(a.Members.OfType<FieldDeclarationSyntax>().SelectMany(field => field.Declaration.Variables).Select(variable => variable.Identifier.ValueText)
            .SequenceEqual(new[] { "Value", "Other" }), "Layout partition changed private or public field order.");
        long callbacks = ReadMetric("LayoutFileOutputCallbacks");
        var after = Replace(input, "A.cs", "int Value", "int Renamed");
        driver = driver.RunGeneratorsAndUpdateCompilation(after, out output, out _);
        CheckErrors(output);
        var changed = AllFiles(driver);
        Require(layoutFiles.Count(pair => changed.GetValueOrDefault(pair.Key) != pair.Value) == 1 && ReadMetric("LayoutFileOutputCallbacks") - callbacks == 1,
            "A private-field edit republished unrelated or shared layout files.");
        Require(before.Where(pair => pair.Key.StartsWith("LuminPack.Dispatch.", StringComparison.Ordinal)).All(pair => changed.GetValueOrDefault(pair.Key) == pair.Value),
            "A private-field rename rewrote generic dispatch signatures or switches.");
        var fresh = AllFiles(NewDriver().RunGenerators(after));
        Require(changed.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(fresh.OrderBy(pair => pair.Key, StringComparer.Ordinal)), "Layout partitions differ between retained and fresh generation.");
    }

    /// <summary>Checks independently emitted no-owner formatters and stable dispatch after their implementation changes.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyFormatterPartitions(MetadataReference[] references)
    {
        var input = CSharpCompilation.Create("FormatterPartitions", new[]
        {
            CSharpSyntaxTree.ParseText("public enum Choice { First, Second }", Parse, "Choice.cs"),
            CSharpSyntaxTree.ParseText("public static class Entry { public static byte[] Call(Choice value) => LuminPack.LuminPackSerializer.Serialize(in value); }", Parse, "Entry.cs"),
            CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public sealed class Marker { public int Value; }", Parse, "Marker.cs"),
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        CheckErrors(output);
        var original = AllFiles(driver);
        Require(original.Keys.Any(name => name.StartsWith("LuminPack.Formatter.", StringComparison.Ordinal)) &&
            original.Keys.Count(name => name.StartsWith("LuminPack.Dispatch.", StringComparison.Ordinal)) == 6 && !original.ContainsKey("LuminPack.SerializerInvocation.Extension.g.cs"),
            "Independent formatter and six dispatch/registration partitions were not emitted.");
        string legacy = GeneratedInvocation(input);
        var expected = MethodInventory([legacy]);
        var actual = MethodInventory(original.Where(pair => pair.Key.StartsWith("LuminPack.Formatter.", StringComparison.Ordinal) || pair.Key.StartsWith("LuminPack.Dispatch.", StringComparison.Ordinal)).Select(pair => pair.Value));
        Require(expected.Count == actual.Count && expected.All(pair => actual.GetValueOrDefault(pair.Key) == pair.Value), "Formatter or generic dispatch partition changed executable method text or documentation.");
        long count = ReadMetric("InvocationFormatterBuilds");
        var edited = Replace(input, "Choice.cs", "Second", "Renamed");
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out output, out _);
        CheckErrors(output);
        var changed = AllFiles(driver);
        Require(ReadMetric("InvocationFormatterBuilds") - count == 1, "Enum edit rerendered unrelated no-owner formatter bodies.");
        Require(original.Where(pair => pair.Key.StartsWith("LuminPack.Dispatch.", StringComparison.Ordinal)).All(pair => changed.GetValueOrDefault(pair.Key) == pair.Value), "Enum member edit changed stable dispatch files.");
    }

    /// <summary>Builds documented legacy invocation source through the preserved public compatibility entry point.</summary>
    /// <param name="input">The current formatter fixture.</param>
    /// <returns>Legacy invocation declarations with the same generated method documentation.</returns>
    private static string GeneratedInvocation(CSharpCompilation input)
    {
        var utility = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.GeneratedSourceDocumentation", throwOnError: true)!;
        var complete = utility.GetMethod("Complete", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (string)complete.Invoke(null, [LuminPackExtensionGenerator.GenerateSerializerInvocationSupport(input, new MetaInfo(Parse, Parse.LanguageVersion, true, true, true)), Parse])!;
    }

    /// <summary>Captures all generator files as pure comparable values.</summary>
    /// <param name="driver">The completed production driver.</param>
    /// <returns>Stable hint names and exact file texts.</returns>
    private static Dictionary<string, string> AllFiles(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .ToDictionary(file => file.HintName, file => file.SourceText.ToString(), StringComparer.Ordinal);

    /// <summary>Checks exact changed files and execution counters against a fresh current driver.</summary>
    /// <param name="name">The scenario description.</param>
    /// <param name="before">The initial source compilation.</param>
    /// <param name="after">The compilation after one edit.</param>
    /// <param name="expectedFiles">The expected number of changed concrete facade files.</param>
    /// <param name="expectedFragments">The expected number of concrete overload blocks actually rendered.</param>
    private static void VerifyEdit(string name, CSharpCompilation before, CSharpCompilation after, int expectedFiles, int expectedFragments)
    {
        var driver = NewDriver().RunGenerators(before);
        var previous = FacadeFiles(driver);
        long filesBefore = ReadMetric("PartitionedFileBuilds");
        long fragmentsBefore = ReadMetric("SerializerFragmentBuilds");
        long callbacksBefore = ReadMetric("AggregateFileOutputCallbacks");
        long plansBefore = ReadMetric("BinaryOperationPlanBuilds");
        driver = driver.RunGeneratorsAndUpdateCompilation(after, out var output, out _);
        CheckErrors(output);
        var actual = FacadeFiles(driver);
        Require(actual.Count == previous.Count && actual.Count(pair => previous.GetValueOrDefault(pair.Key) != pair.Value) == expectedFiles,
            name + ": changed an unrelated concrete facade file.");
        Require(actual["LuminPackSerializer.g.cs"] == previous["LuminPackSerializer.g.cs"], name + ": common generic/helper support changed.");
        Require(ReadMetric("PartitionedFileBuilds") - filesBefore == expectedFiles, name + ": recopied unchanged facade bodies.");
        Require(ReadMetric("SerializerFragmentBuilds") - fragmentsBefore == expectedFragments, name + ": rerendered unrelated overload blocks, including arrays.");
        Require(ReadMetric("BinaryOperationPlanBuilds") - plansBefore == expectedFragments, name + ": rebuilt an unaffected binary operation template.");
        Require(ReadMetric("AggregateFileOutputCallbacks") - callbacksBefore == expectedFiles, name + ": republished an unchanged aggregate file.");
        var fresh = NewDriver().RunGenerators(after);
        Require(actual.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(FacadeFiles(fresh).OrderBy(pair => pair.Key, StringComparer.Ordinal)),
            name + ": retained partitions differ from fresh generation.");
    }

    /// <summary>Inventories every serializer method, retaining its complete body and documentation.</summary>
    /// <param name="sources">Legacy aggregate text or independent generated files.</param>
    /// <returns>Method signatures mapped to exact declaration text.</returns>
    private static Dictionary<string, string> MethodInventory(IEnumerable<string> sources)
        => sources.SelectMany(text => CSharpSyntaxTree.ParseText(text, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .ToDictionary(method => method.Identifier.ValueText + method.TypeParameterList + method.ParameterList.ToString(),
                method => method.ToFullString().Trim(), StringComparer.Ordinal);

    /// <summary>Finds facade files declaring concrete methods for a canonical payload name.</summary>
    /// <param name="files">The generated facade partitions.</param>
    /// <param name="payload">The canonical formatter type name.</param>
    /// <returns>Matching hint names.</returns>
    private static IEnumerable<string> PayloadFiles(Dictionary<string, string> files, string payload)
        => files.Where(pair => CSharpSyntaxTree.ParseText(pair.Value, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Any(method => method.TypeParameterList == null && method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == payload))).Select(pair => pair.Key);

    /// <summary>Reads one internal generation counter without changing production instrumentation.</summary>
    /// <param name="name">The counter field name.</param>
    /// <returns>The current process-wide count.</returns>
    private static long ReadMetric(string name) => (long)typeof(LuminPackSourceGenerator).Assembly
        .GetType("LuminPack.SourceGenerator.IncrementalGenerationMetrics", throwOnError: true)!
        .GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

    /// <summary>Captures stable per-file facade source values.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>Hint names mapped to exact source text.</returns>
    private static Dictionary<string, string> FacadeFiles(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
        .ToDictionary(file => file.HintName, file => file.SourceText.ToString(), StringComparer.Ordinal);

    /// <summary>Replaces one existing source file with a deterministic edited tree.</summary>
    /// <param name="compilation">The initial compilation.</param>
    /// <param name="path">The file being edited.</param>
    /// <param name="before">The exact old source fragment.</param>
    /// <param name="after">The replacement fragment.</param>
    /// <returns>The edited compilation.</returns>
    private static CSharpCompilation Replace(CSharpCompilation compilation, string path, string before, string after)
    {
        var tree = compilation.SyntaxTrees.Single(tree => tree.FilePath == path);
        return compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(tree.ToString().Replace(before, after, StringComparison.Ordinal), Parse, path));
    }

    /// <summary>Creates an independent driver with production type and aggregate generators.</summary>
    /// <returns>A driver with tracked steps enabled.</returns>
    private static GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: Parse,
        driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    /// <summary>Requires compilable generated output.</summary>
    /// <param name="compilation">The updated compilation being checked.</param>
    private static void CheckErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }

    /// <summary>Reports a failed partition or incremental boundary.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure description.</param>
    /// <exception cref="InvalidOperationException">The invariant failed.</exception>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
