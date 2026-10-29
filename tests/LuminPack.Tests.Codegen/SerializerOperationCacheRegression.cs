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
        VerifySemanticModels(references);
        VerifyCurrentPlans(references);
        VerifyCompiledProfiles(references);
        VerifyForwardingCapture();
        VerifyCancellation(references);
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
        string collection = CollectionFile(initial);
        Require(initial[parent] == changed[parent], "A child edit unnecessarily changed its parent's closed collection calls.");
        Require(initial[collection] != changed[collection] && changed[collection].Contains("Renamed", StringComparison.Ordinal), "The closed collection did not rebuild its embedded child operations.");
        Require(initial[independent] == changed[independent], "A child edit changed an independent facade partition.");

        // Current .NET 11 reference metadata itself uses C# 12 ref-readonly parameters.
        // A C# 10 execution gate therefore requires matching older framework references.
        var compatibilityParse = new CSharpParseOptions(compatibilityReferences == null ? LanguageVersion.CSharp12 : LanguageVersion.CSharp10, DocumentationMode.Parse);
        var compatibility = before.RemoveAllReferences().AddReferences(compatibilityReferences ?? references).RemoveAllSyntaxTrees().AddSyntaxTrees(before.SyntaxTrees.Select(tree =>
            CSharpSyntaxTree.ParseText(tree.ToString(), compatibilityParse, tree.FilePath)));
        GeneratorDriver compatibilityDriver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: compatibilityParse);
        compatibilityDriver = compatibilityDriver.RunGeneratorsAndUpdateCompilation(compatibility, out var compatibilityOutput, out _);
        CheckErrors(compatibilityOutput, compatibilityDriver);

        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 4 }, index =>
        {
            var input = Create(references, "ConcurrentOperations" + index, "Field" + index);
            var concurrent = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var compiled, out _);
            CheckErrors(compiled, concurrent);
            var files = Files(concurrent);
            string source = files[CollectionFile(files)];
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
        Console.WriteLine("Passed shared plans, immutable option overlays, captured forwarding, cancellation/resume, closed-collection invalidation, eight parallel facade compilations, independent outputs, and compilation/syntax lifetime.");
    }

    /// <summary>Requires cancelled fragment generation to stop before work and leave its reusable cache valid.</summary>
    /// <param name="references">Runtime metadata for the fragment fixture.</param>
    private static void VerifyCancellation(MetadataReference[] references)
    {
        var input = Create(references, "CancelledFragments", "Value");
        var assembly = typeof(LuminPackSourceGenerator).Assembly;
        var metadata = new MetaInfo(Parse, Parse.LanguageVersion, true, true, true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        foreach (var item in new[]
        {
            (Generator: "LuminPack.Code.Core.LuminPackExtensionGenerator", Method: "GenerateInvocationFiles", Cache: "LuminPack.SourceGenerator.InvocationSupportCache"),
            (Generator: "LuminPack.Code.Core.LuminPackSerializerGenerator", Method: "GenerateSerializerFiles", Cache: "LuminPack.SourceGenerator.SerializerFacadeCache")
        })
        {
            var method = assembly.GetType(item.Generator, throwOnError: true)!.GetMethod(item.Method, BindingFlags.Static | BindingFlags.NonPublic)!;
            var cacheType = assembly.GetType(item.Cache, throwOnError: true)!;
            var cache = Activator.CreateInstance(cacheType, nonPublic: true)!;
            bool cancelled = false;
            try { method.Invoke(null, [input, metadata, null, cache, cancellation.Token]); }
            catch (TargetInvocationException exception) when (exception.InnerException is OperationCanceledException) { cancelled = true; }
            Require(cancelled, item.Method + " ignored a cancelled generation request.");
            string actual = FragmentFiles(method.Invoke(null, [input, metadata, null, cache, CancellationToken.None])!);
            string expected = FragmentFiles(method.Invoke(null, [input, metadata, null, Activator.CreateInstance(cacheType, nonPublic: true)!, CancellationToken.None])!);
            Require(actual == expected && actual.Length != 0, item.Method + " retained invalid partial output after cancellation.");
        }
    }

    /// <summary>Reads frozen file values returned by an internal fragment producer.</summary>
    /// <param name="files">The returned immutable sequence of generated files.</param>
    /// <returns>Stable filenames and exact text for cancellation/resume parity.</returns>
    private static string FragmentFiles(object files) => string.Join("\n", ((System.Collections.IEnumerable)files).Cast<object>().Select(file =>
        file.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(file) + "\n" +
        file.GetType().GetField("Text", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(file)).Order(StringComparer.Ordinal));

    /// <summary>Requires independent pipeline caches to share exact plans without merging settings or streaming modes.</summary>
    /// <param name="references">Runtime metadata for the current-compilation fixture.</param>
    private static void VerifyCurrentPlans(MetadataReference[] references)
    {
        var input = Create(references, "CurrentPlanSharing", "Value");
        var assembly = typeof(LuminPackSourceGenerator).Assembly;
        var type = assembly.GetType("LuminPack.SourceGenerator.BinaryOperationPlanCache", throwOnError: true)!;
        var get = type.GetMethod("Get", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var metadata = new MetaInfo(Parse, Parse.LanguageVersion, true, true, true);
        var payload = input.GetTypeByMetadataName("Independent")!;
        var plans = new object[8];
        var metric = assembly.GetType("LuminPack.SourceGenerator.IncrementalGenerationMetrics", throwOnError: true)!
            .GetField("BinaryOperationPlanBuilds", BindingFlags.Static | BindingFlags.NonPublic)!;
        long before = (long)metric.GetValue(null)!;
        Parallel.For(0, plans.Length, index => plans[index] = get.Invoke(Activator.CreateInstance(type, nonPublic: true),
            [payload, input, metadata, false])!);
        Require(plans.All(plan => ReferenceEquals(plan, plans[0])) && (long)metric.GetValue(null)! - before == 1,
            "Independent pipeline caches rebuilt the same current-compilation operation plan.");
        var cache = Activator.CreateInstance(type, nonPublic: true)!;
        Require(!ReferenceEquals(plans[0], get.Invoke(cache, [payload, input, metadata, true])), "Streaming and trusted plans were merged.");
        metadata.IsForUnity = true;
        Require(!ReferenceEquals(plans[0], get.Invoke(cache, [payload, input, metadata, false])), "Unity and modern plan settings were merged.");
        metadata.IsForUnity = false;
        Require(ReferenceEquals(plans[0], get.Invoke(cache, [payload, input, metadata, false])), "A copied settings guard changed after caller mutation.");
        var unused = CreateUnusedPlan(references);
        for (int attempt = 0; attempt < 8 && unused.Plan.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Require(!unused.Plan.IsAlive, "The compilation-local sharing registry retained a plan after its pipeline cache was released.");
        GC.KeepAlive(unused.Input);
    }

    /// <summary>Releases the pipeline cache while retaining the compilation that owns its sharing registry.</summary>
    /// <param name="references">Runtime metadata for the plan-lifetime fixture.</param>
    /// <returns>The live compilation and a weak handle to its no-longer-needed frozen plan.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Compilation Input, WeakReference Plan) CreateUnusedPlan(MetadataReference[] references)
    {
        var input = Create(references, "UnusedCurrentPlan", "Value");
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.BinaryOperationPlanCache", throwOnError: true)!;
        var cache = Activator.CreateInstance(type, nonPublic: true)!;
        var plan = type.GetMethod("Get", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache,
            [input.GetTypeByMetadataName("Independent")!, input, new MetaInfo(Parse, Parse.LanguageVersion, true, true, true), false])!;
        return (input, new WeakReference(plan));
    }

    /// <summary>Checks allocation-free unchanged overlays, inherited defaults and imported-scope isolation.</summary>
    /// <param name="references">Runtime metadata for the option-scope fixture.</param>
    private static void VerifyCompiledProfiles(MetadataReference[] references)
    {
        const string source = """
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackOption(StringRecording = LuminPackStringRecording.VarInt, UntrustedData = true, MaxStringLength = 15)]
            [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]
            public class Owner
            {
                [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16)] public string Value;
            }
            [LuminPackOption(MaxStringLength = 23)] public class LimitOnly { }
            """;
        var input = CSharpCompilation.Create("ResolvedProfiles", [CSharpSyntaxTree.ParseText(source, Parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.CompiledOptions", throwOnError: true)!;
        var resolve = type.GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic)!;
        var owner = input.GetTypeByMetadataName("Owner")!;
        var member = owner.GetMembers("Value").Single();
        var original = resolve.Invoke(null, [owner, input, true])!;
        var apply = type.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Require(ReferenceEquals(original, apply.Invoke(original, [Array.Empty<AttributeData>()])), "An empty option overlay allocated a replacement profile.");
        var inputPolicy = type.GetField("Input", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(original)!;
        var applyPolicy = inputPolicy.GetType().GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Require(ReferenceEquals(inputPolicy, applyPolicy.Invoke(inputPolicy, [owner.GetAttributes()])), "A string-only declaration cloned its input policy.");
        var limited = apply.Invoke(original, [input.GetTypeByMetadataName("LimitOnly")!.GetAttributes()])!;
        var limitedPolicy = type.GetField("Input", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(limited)!;
        Require(!ReferenceEquals(inputPolicy, limitedPolicy) &&
            (int)inputPolicy.GetType().GetField("String", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(inputPolicy)! == 15 &&
            (int)limitedPolicy.GetType().GetField("String", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(limitedPolicy)! == 23,
            "An input-policy override mutated the inherited policy instead of copying it.");
        var field = resolve.Invoke(null, [member, input, true])!;
        Require((int)type.GetField("Encoding", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(original)! == 0 &&
            (int)type.GetField("Encoding", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(field)! == 1 &&
            (int)type.GetField("Recording", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(field)! == 2,
            "Resolved options lost assembly inheritance or an explicit default-valued member override.");
        var edited = input.ReplaceSyntaxTree(input.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(source.Replace(
            "StringEncoding.UTF8", "StringEncoding.UTF16", StringComparison.Ordinal), Parse));
        var changed = resolve.Invoke(null, [edited.GetTypeByMetadataName("Owner")!, edited, true])!;
        Require(!ReferenceEquals(original, changed) && (int)type.GetField("Encoding", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(changed)! == 1,
            "Resolved profiles crossed a compilation boundary.");

        var library = CSharpCompilation.Create("ImportedProfileLibrary", [CSharpSyntaxTree.ParseText("public class Imported { public int Value; }", Parse)],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var consumer = CSharpCompilation.Create("ImportedProfileConsumer", [CSharpSyntaxTree.ParseText(
            "[assembly: LuminPack.Attribute.LuminPackable(typeof(Imported))] [assembly: LuminPack.Attribute.LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF8)]", Parse)],
            references.Append(library.ToMetadataReference()), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var imported = consumer.GetTypeByMetadataName("Imported")!;
        var local = resolve.Invoke(null, [imported, consumer, true])!;
        var declared = resolve.Invoke(null, [imported, consumer, false])!;
        Require((int)type.GetField("Encoding", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(local)! == 0 &&
            (int)type.GetField("Encoding", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(declared)! == 1,
            "Imported and declaring assembly profiles shared one cache entry.");
    }

    /// <summary>Compares syntax-captured lazy targets with the original parsing fallback and keeps conditional fallback.</summary>
    private static void VerifyForwardingCapture()
    {
        const string source = """
            namespace LuminPack {
            public static partial class LuminPackSerializer {
                internal static void Serialize(ref Writer writer, in Payload value) { __LuminRoots_123.RootWrite(ref writer, in value); }
                internal static void Serialize<TProfile>(ref Writer writer, in Payload value) { __LuminRoots_123.RootWrite<TProfile>(ref writer, in value); }
            } }
            """;
        var assembly = typeof(LuminPackSourceGenerator).Assembly;
        var type = assembly.GetType("LuminPack.Code.Core.LazyForwardingTargets", throwOnError: true)!;
        var parse = type.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!;
        var capture = type.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)!;
        var metric = assembly.GetType("LuminPack.SourceGenerator.IncrementalGenerationMetrics", throwOnError: true)!
            .GetField("LazyForwardingParses", BindingFlags.Static | BindingFlags.NonPublic)!;
        string capturedText = new(source.ToCharArray());
        var expected = parse.Invoke(null, [source, "Payload"])!;
        long before = (long)metric.GetValue(null)!;
        capture.Invoke(null, [capturedText, "Payload", CSharpSyntaxTree.ParseText(capturedText, Parse).GetRoot()]);
        var actual = parse.Invoke(null, [capturedText, "Payload"])!;
        var key = type.GetProperty("Key", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Require((string)key.GetValue(actual)! == (string)key.GetValue(expected)! && (long)metric.GetValue(null)! == before,
            "Capturing final syntax changed lazy targets or reparsed the complete contribution.");
        string conditional = "#if SELECTED\n" + source + "\n#endif";
        before = (long)metric.GetValue(null)!;
        capture.Invoke(null, [conditional, "Payload", CSharpSyntaxTree.ParseText(conditional, Parse.WithPreprocessorSymbols("SELECTED")).GetRoot()]);
        var fallback = parse.Invoke(null, [conditional, "Payload"])!;
        Require((string)key.GetValue(fallback)! == string.Empty && (long)metric.GetValue(null)! == before + 1,
            "Conditional forwarding changed the original text-parsing branch selection.");
        var lifetime = CreateCapturedTargets(source);
        for (int attempt = 0; attempt < 8 && lifetime.Root.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Require(!lifetime.Root.IsAlive, "Captured lazy forwarding targets retained the completed syntax tree.");
        GC.KeepAlive(lifetime.Source);
    }

    /// <summary>Discards the captured root while retaining the exact string that keys its forwarding facts.</summary>
    /// <param name="source">The contribution whose forwarding declarations are inspected.</param>
    /// <returns>The live source key and a weak handle to the syntax root.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string Source, WeakReference Root) CreateCapturedTargets(string source)
    {
        string text = new(source.ToCharArray());
        var root = CSharpSyntaxTree.ParseText(text, Parse).GetRoot();
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.LazyForwardingTargets", throwOnError: true)!;
        type.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [text, "Payload", root]);
        return (text, new WeakReference(root));
    }

    /// <summary>Shares binding within one compilation without reusing models after source edits.</summary>
    /// <param name="references">Current runtime metadata references.</param>
    private static void VerifySemanticModels(MetadataReference[] references)
    {
        var input = Create(references, "SemanticModelReuse", "Value");
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.FileSemanticObservationCache", throwOnError: true)!;
        var factory = type.GetMethod("For", BindingFlags.Static | BindingFlags.NonPublic)!;
        var get = type.GetMethod("GetSemanticModel", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var context = factory.Invoke(null, [input]);
        var tree = input.SyntaxTrees.Single(tree => tree.FilePath == "Child.cs");
        var original = (SemanticModel)get.Invoke(context, [tree])!;
        Require(ReferenceEquals(original, get.Invoke(context, [tree])), "Source analyses rebuilt the same file's semantic model.");
        var changed = input.WithOptions(input.Options.WithOptimizationLevel(OptimizationLevel.Release));
        var changedContext = factory.Invoke(null, [changed]);
        var current = (SemanticModel)get.Invoke(changedContext, [tree])!;
        Require(!ReferenceEquals(original, current) && ReferenceEquals(current.Compilation, changed), "A semantic model escaped its owning compilation.");
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
        get.Invoke(cache, [input.GetTypeByMetadataName("Parent")!, input, metadata, false]);
        get.Invoke(cache, [input.GetTypeByMetadataName("Child")!, input, metadata, false]);
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
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);

    /// <summary>Captures exact generated output values.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>Hint names and complete source text.</returns>
    private static Dictionary<string, string> Files(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .ToDictionary(file => file.HintName, file => file.SourceText.ToString(), StringComparer.Ordinal);

    /// <summary>Finds a concrete payload's stable state implementation partition.</summary>
    /// <param name="files">Generated files.</param>
    /// <param name="type">The canonical payload type.</param>
    /// <returns>The concrete partition hint name.</returns>
    private static string PayloadFile(Dictionary<string, string> files, string type) => files.Single(pair => pair.Key.EndsWith(".Extension.g.cs", StringComparison.Ordinal) &&
        CSharpSyntaxTree.ParseText(pair.Value, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method =>
            method.Identifier.ValueText == "Serialize" && method.TypeParameterList == null && method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().TrimEnd('?') == type))).Key;

    /// <summary>Finds the closed collection body that owns the embedded child contract.</summary>
    /// <param name="files">Generated partitions for this compilation.</param>
    /// <returns>The list formatter's stable partition hint.</returns>
    private static string CollectionFile(Dictionary<string, string> files) => files.Single(pair => pair.Key.StartsWith("LuminPack.Formatter.", StringComparison.Ordinal) &&
        CSharpSyntaxTree.ParseText(pair.Value, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method =>
            method.Identifier.ValueText == "RootWrite" && method.ParameterList.Parameters.Count == 2 &&
            method.ParameterList.Parameters[1].Type?.ToString().TrimEnd('?') == "global::System.Collections.Generic.List<global::Child>")).Key;

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
