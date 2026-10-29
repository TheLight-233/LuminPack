using System.Reflection;
using System.Runtime.CompilerServices;
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
        var versioned = Replace(full, "A.cs", "[LuminPack.Attribute.LuminPackable]", "[LuminPack.Attribute.LuminPackable(LuminPack.Attribute.GeneratorType.VersionTolerant)]");
        var oldName = Replace(versioned, "A.cs", "public int Value", "[LuminPack.Attribute.LuminPackOldName(\"Original\")] public int Value");
        Verify("version member alias added", versioned, oldName, 1);
        Verify("version member alias edited", oldName, Replace(oldName, "A.cs", "Original", "Former"), 1);
        Verify("version member alias removed", oldName, versioned, 1);
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
        VerifyAttributeDiscovery(resolvedReferences);
        VerifyReferenceLifetime(references);
        VerifyReferenceInvalidation(resolvedReferences);
        VerifyPartitionCacheLifetime();
        VerifyDependencySharing(resolvedReferences);
    }

    /// <summary>Requires a replaced compilation reference to invalidate unchanged source declarations.</summary>
    /// <param name="references">Platform and runtime references for the consumer.</param>
    private static void VerifyReferenceInvalidation(MetadataReference[] references)
    {
        var dependency = CSharpCompilation.Create("ReferenceConstants",
            new[] { CSharpSyntaxTree.ParseText("public static class Constants { public const int Order = 0; }", ParseOptions) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var changed = dependency.ReplaceSyntaxTree(dependency.SyntaxTrees.Single(),
            CSharpSyntaxTree.ParseText("public static class Constants { public const int Order = 2; }", ParseOptions));
        var consumer = CSharpCompilation.Create("ReferenceInvalidation", new[]
        {
            CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public class Model { [LuminPack.Attribute.LuminPackOrder(Constants.Order)] public int Value; [LuminPack.Attribute.LuminPackOrder(1)] public long Other; }", ParseOptions, "Model.cs")
        }, references.Append(dependency.ToMetadataReference()), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        Verify("compilation reference changed", consumer, consumer.WithReferences(references.Append(changed.ToMetadataReference())), 1);
    }

    /// <summary>Checks alias discovery and emits a partially declared payload exactly once.</summary>
    /// <param name="references">Runtime metadata for the discovery compilation.</param>
    private static void VerifyAttributeDiscovery(MetadataReference[] references)
    {
        var files = new Dictionary<string, string>
        {
            ["Alias.cs"] = "global using Pack = LuminPack.Attribute.LuminPackableAttribute;",
            ["Model.cs"] = "namespace Discovery; [Pack] public partial class Model { public int Value; }",
            ["Partial.cs"] = "namespace Discovery; public partial class Model { public long Other; }",
            ["Record.cs"] = "namespace Discovery; [LuminPack.Attribute.LuminPackableAttribute] public partial record Record { public int Value; }",
            ["Value.cs"] = "namespace Discovery; [Pack] public partial record struct Value { public int Number; }",
            ["Utility.cs"] = "namespace Discovery; public class Utility { public int Value; }"
        };
        var input = CSharpCompilation.Create("AttributeDiscovery",
            files.Select(file => CSharpSyntaxTree.ParseText(file.Value, ParseOptions, file.Key)), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGenerators(input);
        Require(!driver.GetRunResult().Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "Partial or aliased attribute discovery reported a generator error.");
        var sources = Sources(driver);
        foreach (string name in new[] { "Model", "Record", "Value" })
            Require(sources.ContainsKey("Discovery." + name + ".Extension.g.cs"), "Attribute discovery omitted " + name + ".");
        Require(!sources.ContainsKey("Discovery.Utility.Extension.g.cs"), "Unattributed utility entered the type pipeline.");
        Verify("partial storage changed", input, Replace(input, "Partial.cs", "long Other", "long Renamed"), 1);
        var removed = Replace(input, "Model.cs", "[Pack]", string.Empty);
        Verify("attribute removed", input, removed);
        Verify("attribute restored", removed, input);
        Verify("attribute alias changed", input, Replace(input, "Alias.cs", "LuminPack.Attribute.LuminPackableAttribute", "System.ObsoleteAttribute"));
    }

    /// <summary>Requires retained project guards to release compilation-backed metadata references.</summary>
    /// <param name="references">Platform metadata used by both temporary compilations.</param>
    private static void VerifyReferenceLifetime(MetadataReference[] references)
    {
        var lifetime = CreateReferenceGuard(references);
        for (int attempt = 0; attempt < 8 && (lifetime.Consumer.IsAlive || lifetime.Reference.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Require(!lifetime.Consumer.IsAlive, "A retained project guard kept its consumer compilation alive.");
        Require(!lifetime.Reference.IsAlive, "A retained project guard kept a compilation-backed reference alive.");
        GC.KeepAlive(lifetime.Guard);
    }

    /// <summary>Retains only the symbol-free project guard after using a source compilation reference.</summary>
    /// <param name="references">Platform metadata for the reference fixture.</param>
    /// <returns>A live guard and weak handles to the compilations it must not retain.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Guard, WeakReference Consumer, WeakReference Reference) CreateReferenceGuard(MetadataReference[] references)
    {
        var dependency = CSharpCompilation.Create("GuardDependency",
            new[] { CSharpSyntaxTree.ParseText("public class Dependency { public int Value; }", ParseOptions) }, references);
        var consumer = CSharpCompilation.Create("GuardConsumer",
            new[] { CSharpSyntaxTree.ParseText("public class Consumer { public Dependency Value; }", ParseOptions, "Consumer.cs") },
            references.Append(dependency.ToMetadataReference()));
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.FileSemanticObservationCache", throwOnError: true)!;
        var context = type.GetMethod("For", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { consumer })!;
        var guard = type.GetProperty("ProjectGuard", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;
        return (guard, new WeakReference(consumer), new WeakReference(dependency));
    }

    /// <summary>Checks that cache pruning preserves current values and releases removed output text.</summary>
    private static void VerifyPartitionCacheLifetime()
    {
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.PartitionedSourceCache", throwOnError: true)!;
        var cache = Activator.CreateInstance(type, nonPublic: true)!;
        var wrap = type.GetMethod("Wrap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        /// <summary>Builds one independently keyed partition through the production cache.</summary>
        /// <param name="identity">The partition identity and body.</param>
        /// <returns>The frozen complete file.</returns>
        object Wrap(string identity) => wrap.Invoke(cache, new object?[] { "Lifetime", identity, "", identity, "", null, null })!;
        var first = Wrap("first");
        var second = Wrap("second");
        string name = (string)second.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(second)!;
        type.GetMethod("Retain", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache, new object[] { new[] { name } });
        Require(ReferenceEquals(second, Wrap("second")), "Pruning discarded a current partition's cached value.");
        Require(!ReferenceEquals(first, Wrap("first")), "Pruning retained a removed partition's output.");
        var fields = type.GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)!;
        type.GetMethod("Retain", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache, new object[] { Array.Empty<string>() });
        Require(((System.Collections.IDictionary)fields.GetValue(cache)!).Count == 0, "An empty output batch kept obsolete partitions.");
    }

    /// <summary>Requires different output caches to share one compilation-local dependency closure.</summary>
    /// <param name="references">Runtime metadata for the shared-closure fixture.</param>
    private static void VerifyDependencySharing(MetadataReference[] references)
    {
        var input = CSharpCompilation.Create("SharedDependencyClosure", new[]
        {
            CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public class Model { public int Value; }", ParseOptions, "Model.cs")
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var counters = ReadCounters();
        NewDriver().RunGenerators(input);
        var current = ReadCounters();
        Require(current["TypeDependencyCacheHits"] > counters["TypeDependencyCacheHits"], "Output caches repeated the same dependency traversal.");
        Require(current["TypeDependencyBuilds"] > counters["TypeDependencyBuilds"], "The dependency-sharing fixture did not build a closure.");
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
        new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() }, parseOptions: ParseOptions,
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
