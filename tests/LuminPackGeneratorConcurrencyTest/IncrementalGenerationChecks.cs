using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks retained-driver reuse and invalidation against fresh current generation.</summary>
internal static class IncrementalGenerationChecks
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp10);

    /// <summary>Verifies independent type edits, inferred payload changes, overloads, aliases, and fallback.</summary>
    /// <param name="references">The runtime references available to the test process.</param>
    internal static void Run(MetadataReference[] references)
    {
        var serializer = CSharpCompilation.Create("IncrementalSerializerReference",
            new[] { CSharpSyntaxTree.ParseText("namespace LuminPack { public static class LuminPackSerializer { public static void Serialize<T>(in T value) {} } }", ParseOptions) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        Require(serializer.Emit(image).Success, "Could not build the serializer signature fixture.");
        var resolvedReferences = references.Append(MetadataReference.CreateFromImage(image.ToArray())).ToArray();
        var files = new Dictionary<string, string>
        {
            ["Options.cs"] = "[assembly: LuminPack.Attribute.LuminPackGeneratorOptions(LuminPack.Attribute.LuminPackGenerationMode.Full)]",
            ["Aliases.cs"] = "global using Payload = IncrementalChecks.A;",
            ["A.cs"] = "namespace IncrementalChecks; [LuminPack.Attribute.LuminPackable] public partial class A { public int Value = 1; }",
            ["B.cs"] = "namespace IncrementalChecks; [LuminPack.Attribute.LuminPackable] public partial class B { public long Other; }",
            ["Forward.cs"] = "namespace IncrementalChecks; public static class Calls { public static void Save<T>(T value) => LuminPack.LuminPackSerializer.Serialize(value); }",
            ["Factory.cs"] = "namespace IncrementalChecks; public static class Factory { public static A Make() => new(); public static A Pick(object value) => new(); }",
            ["Entry.cs"] = "namespace IncrementalChecks; public static class Entry { public static void Run() { Calls.Save(Factory.Make()); } }",
            ["Utility.cs"] = "namespace IncrementalChecks; internal static class Utility { internal static int Touch() => 17; }"
        };
        var full = CSharpCompilation.Create("IncrementalChecks",
            files.Select(file => CSharpSyntaxTree.ParseText(file.Value, ParseOptions, file.Key)), resolvedReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        Verify("unrelated body", full, Replace(full, "Utility.cs", "17", "18"), 0);
        Verify("one model initializer", full, Replace(full, "A.cs", "= 1", "= 2"), 1);
        Verify("one model member", full, Replace(full, "A.cs", "int Value", "int Renamed"), 1);
        Verify("new observed formatter", full,
            Replace(full, "Utility.cs", "=> 17;", "{ var value = new System.Collections.Generic.List<decimal>(); return value.Count; }"));

        var minimal = Replace(full, "Options.cs", ".Full", ".Minimal");
        Verify("unrelated body", minimal, Replace(minimal, "Utility.cs", "17", "18"), 0);
        Verify("inferred factory result", minimal, Replace(minimal, "Factory.cs", "A Make()", "B Make()"), expectedRoot: "B");
        var overload = Replace(minimal, "Entry.cs", "Factory.Make()", "Factory.Pick(new A())");
        Verify("new overload in another file", overload,
            Replace(overload, "Factory.cs", "public static A Pick(object", "public static B Pick(A value) => new(); public static A Pick(object"), expectedRoot: "B");
        var alias = Replace(minimal, "Entry.cs", "Factory.Make()", "new Payload()");
        Verify("global payload alias", alias, Replace(alias, "Aliases.cs", "IncrementalChecks.A", "IncrementalChecks.B"), expectedRoot: "B");
        var fallback = Replace(minimal, "Utility.cs", "=> 17;", "{ int Local(int value) => value; return Local(17); }");
        Verify("retained unsupported local function", fallback, Replace(fallback, "A.cs", "= 1", "= 2"), requireFallback: true);
        var compiledOptions = Replace(minimal, "A.cs", "public int Value = 1;",
            "[LuminPack.Attribute.LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF8)] public string Text = \"value\";");
        Verify("compiled member option", compiledOptions, Replace(compiledOptions, "A.cs", "StringEncoding.UTF8", "StringEncoding.UTF16"));
        Verify("compiled assembly option", minimal, Replace(minimal, "Options.cs", ".Minimal)]",
            ".Minimal)] [assembly: LuminPack.Attribute.LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF16)]"));
        var unmarkedString = Replace(minimal, "A.cs", "public int Value = 1;", "public string Text = \"value\";");
        Verify("compiled default-valued assembly declaration", unmarkedString, Replace(unmarkedString, "Options.cs", ".Minimal)]",
            ".Minimal)] [assembly: LuminPack.Attribute.LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF8)]"));
        var declaredOptions = Replace(minimal, "A.cs", "public partial class A",
            "[LuminPack.Attribute.LuminPackOption(ValidateJsonSyntax = false)] public partial class A");
        Verify("compiled root JSON validation", declaredOptions, Replace(declaredOptions, "A.cs", "ValidateJsonSyntax = false", "ValidateJsonSyntax = true"));
        VerifyDiagnosticReuse(resolvedReferences, "Shared.cs");
        VerifyDiagnosticReuse(resolvedReferences, string.Empty);
    }

    /// <summary>Compares exact generated sources/diagnostics and checks that cached work is skipped.</summary>
    /// <param name="name">The scenario description used in failures.</param>
    /// <param name="before">The initial compilation.</param>
    /// <param name="after">The edited compilation.</param>
    /// <param name="modelBuilds">The expected affected model count, when prescribed.</param>
    /// <param name="expectedRoot">The only root expected from a resolved Minimal call.</param>
    /// <param name="requireFallback">Whether an unsupported unchanged file must be freshly observed.</param>
    private static void Verify(string name, CSharpCompilation before, CSharpCompilation after, long? modelBuilds = null,
        string? expectedRoot = null, bool requireFallback = false)
    {
        Require(!after.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), name + ": invalid input fixture.");
        GeneratorDriver retained = NewDriver().RunGenerators(before);
        var original = Sources(retained);
        var counters = ReadCounters();
        retained = retained.RunGenerators(after);
        var delta = ReadCounters().ToDictionary(pair => pair.Key, pair => pair.Value - counters.GetValueOrDefault(pair.Key));
        var fresh = NewDriver().RunGenerators(after);
        var actual = Sources(retained);
        var expected = Sources(fresh);
        Require(expected.Count != 0 && actual.Count == expected.Count && expected.All(pair => actual.GetValueOrDefault(pair.Key) == pair.Value),
            name + ": retained generation differs from fresh current output.");
        Require(!retained.GetRunResult().Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), name + ": generator reported an error.");
        Require(Diagnostics(retained).SequenceEqual(Diagnostics(fresh)), name + ": diagnostics changed.");
        if (modelBuilds.HasValue)
        {
            Require(delta["PackableModelBuilds"] == modelBuilds.Value, name + ": unexpected number of models analyzed.");
            Require(delta["PackableAnalysisTransforms"] == modelBuilds.Value, name + ": an unaffected analysis transform executed.");
            var steps = retained.GetRunResult().Results
                .SelectMany(result => result.TrackedSteps.Where(pair => pair.Key == "LuminPack.Incremental.TypeEmission").SelectMany(pair => pair.Value))
                .SelectMany(step => step.Outputs).ToArray();
            Require(steps.Length > 0 && steps.Count(output => output.Reason != IncrementalStepRunReason.Cached) == modelBuilds.Value,
                name + ": Roslyn did not cache the unaffected analysis steps.");
        }
        if (expectedRoot != null)
        {
            Require(actual.ContainsKey("IncrementalChecks." + expectedRoot + ".Extension.g.cs"), name + ": expected root is absent.");
            string other = expectedRoot == "A" ? "B" : "A";
            Require(!actual.ContainsKey("IncrementalChecks." + other + ".Extension.g.cs"), name + ": stale root was retained.");
        }
        if (requireFallback)
            Require(delta["InvocationFileFallbacks"] > 0, name + ": unsupported invocation did not request fresh binding.");
        if (name == "unrelated body")
        {
            Require(delta["AggregateBuilds"] == 0 && delta["AggregateCacheHits"] == 1,
                name + ": unchanged aggregate construction executed.");
            var aggregateSteps = retained.GetRunResult().Results
                .SelectMany(result => result.TrackedSteps.Where(pair => pair.Key == "LuminPack.Incremental.AggregateBatch").SelectMany(pair => pair.Value))
                .SelectMany(step => step.Outputs).ToArray();
            Require(aggregateSteps.Length == 1 && aggregateSteps[0].Reason == IncrementalStepRunReason.Cached,
                name + ": Roslyn did not cache the aggregate construction step.");
        }
        if (name == "new observed formatter")
            Require(delta["AggregateBuilds"] == 1 && actual.Keys.Except(original.Keys, StringComparer.Ordinal)
                .Any(name => name.StartsWith("LuminPackSerializer.", StringComparison.Ordinal)),
                name + ": a new formatter did not invalidate the aggregate input.");
    }

    /// <summary>Checks stable diagnostic outputs and exact source trees with duplicate or empty paths.</summary>
    /// <param name="references">The runtime and resolved serializer references.</param>
    /// <param name="path">The intentionally shared diagnostic source path.</param>
    private static void VerifyDiagnosticReuse(MetadataReference[] references, string path)
    {
        var input = CSharpCompilation.Create("DiagnosticReuse" + path.Length,
            new[]
            {
                CSharpSyntaxTree.ParseText("namespace DiagnosticReuse; [LuminPack.Attribute.LuminPackable] public sealed class Good { public int Value; }", ParseOptions, path),
                CSharpSyntaxTree.ParseText("namespace DiagnosticReuse; [LuminPack.Attribute.LuminPackable] public interface IBad {} [LuminPack.Attribute.LuminPackable] public sealed partial class Message : IBad { public int Value; }", ParseOptions, path),
                CSharpSyntaxTree.ParseText("namespace DiagnosticReuse; internal static class Utility { internal static int Touch() => 17; }", ParseOptions, "Utility.cs")
            }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver retained = NewDriver().RunGenerators(input);
        Require(retained.GetRunResult().Diagnostics.Any(diagnostic => diagnostic.Location.IsInSource), "Diagnostic reuse fixture has no source diagnostic.");
        var before = ReadCounters();
        var edited = Replace(input, "Utility.cs", "17", "18");
        retained = retained.RunGenerators(edited);
        var after = ReadCounters();
        Require(after["PackableAnalysisTransforms"] == before["PackableAnalysisTransforms"], "Unrelated edit reran diagnostic type analysis.");
        Require(after["PackableDiagnosticCallbacks"] == before["PackableDiagnosticCallbacks"], "Unrelated edit reran diagnostic source output.");
        Require(Diagnostics(retained).SequenceEqual(Diagnostics(NewDriver().RunGenerators(edited))), "Cached source diagnostics differ from fresh generation.");
        foreach (var diagnostic in retained.GetRunResult().Diagnostics.Where(diagnostic => diagnostic.Location.IsInSource))
            Require(ReferenceEquals(diagnostic.Location.SourceTree, edited.SyntaxTrees.ElementAt(1)), "Diagnostic resolved to the wrong duplicate-path tree.");

        var oldTree = edited.SyntaxTrees.ElementAt(1);
        var shiftedTree = CSharpSyntaxTree.ParseText("\n" + oldTree.GetText(), ParseOptions, path);
        edited = edited.ReplaceSyntaxTree(oldTree, shiftedTree);
        retained = retained.RunGenerators(edited);
        Require(Diagnostics(retained).SequenceEqual(Diagnostics(NewDriver().RunGenerators(edited))), "Shifted source diagnostics differ from fresh generation.");
        foreach (var diagnostic in retained.GetRunResult().Diagnostics.Where(diagnostic => diagnostic.Location.IsInSource))
            Require(ReferenceEquals(diagnostic.Location.SourceTree, shiftedTree), "Diagnostic retained a replaced source tree.");
    }

    /// <summary>Creates both public generator entry points with the same input language settings.</summary>
    /// <returns>A fresh driver used as the source of truth for the edited compilation.</returns>
    private static GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator() }, parseOptions: ParseOptions,
        driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

    /// <summary>Replaces one input tree while retaining all other tree identities.</summary>
    /// <param name="compilation">The compilation to update.</param>
    /// <param name="path">The edited file.</param>
    /// <param name="find">The exact source fragment to replace.</param>
    /// <param name="replacement">The new source fragment.</param>
    /// <returns>The edited compilation.</returns>
    private static CSharpCompilation Replace(CSharpCompilation compilation, string path, string find, string replacement)
    {
        var tree = compilation.SyntaxTrees.Single(tree => tree.FilePath == path);
        string text = tree.GetText().ToString();
        Require(text.Contains(find, StringComparison.Ordinal), "Missing fixture edit: " + find);
        return compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(text.Replace(find, replacement, StringComparison.Ordinal), ParseOptions, path));
    }

    /// <summary>Reads internal counters to distinguish avoided analysis from identical regenerated text.</summary>
    /// <returns>A snapshot taken outside generator execution.</returns>
    private static Dictionary<string, long> ReadCounters() => typeof(LuminPackSourceGenerator).Assembly
        .GetType("LuminPack.SourceGenerator.IncrementalGenerationMetrics", throwOnError: true)!
        .GetFields(BindingFlags.Static | BindingFlags.NonPublic).Where(field => field.FieldType == typeof(long))
        .ToDictionary(field => field.Name, field => (long)field.GetValue(null)!);

    /// <summary>Captures generated names, encoding, and complete text for exact comparison.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>Each generated source's stable fingerprint.</returns>
    private static Dictionary<string, string> Sources(GeneratorDriver driver) => driver.GetRunResult().Results
        .SelectMany(result => result.GeneratedSources).ToDictionary(source => source.HintName,
            source => (source.SourceText.Encoding?.WebName ?? "<null>") + ":" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(source.SourceText.ToString()))));

    /// <summary>Includes diagnostic identity, severity, message, and source position in parity checks.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>Diagnostics in stable comparison order.</returns>
    private static string[] Diagnostics(GeneratorDriver driver) => driver.GetRunResult().Diagnostics.Select(diagnostic =>
        diagnostic.Id + ":" + diagnostic.Severity + ":" + diagnostic.Location.Kind + ":" + diagnostic.Location.GetLineSpan() + ":" + diagnostic.GetMessage())
        .Order(StringComparer.Ordinal).ToArray();

    /// <summary>Fails the executable regression check with a specific explanation.</summary>
    /// <param name="condition">The invariant being verified.</param>
    /// <param name="message">The failure explanation.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
