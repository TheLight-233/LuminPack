using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using LuminPack.SourceGenerator.Next.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    private const string BinaryFolder = "LuminPack.SourceGenerator.LuminPackSourceGenerator/";
    private const string JsonFolder = "LuminPack.SourceGenerator.LuminPackJsonGenerator/";
    private const string FormatterFolder = "LuminPack.SourceGenerator.LuminPackFormatterGenerator/";

    /// <summary>Compiles and executes publication contracts, including retained-driver invalidation.</summary>
    /// <param name="references">The common compiler metadata references.</param>
    /// <param name="parse">The consumer parse options.</param>
    /// <param name="coreDll">The Core assembly used to execute the fixture.</param>
    /// <returns>Zero when every publication contract passes.</returns>
    private static int VerifyPublication(MetadataReference[] references, CSharpParseOptions parse, string coreDll)
    {
        string source = PublicationFixture;
        for (int index = 0; index < 20; index++)
            source += $"\n[LuminPackable] public sealed partial class Fixed{index} : IShape {{ public int Value; public long Other; }}\n";
        var tree = CSharpSyntaxTree.ParseText(source, parse, "PublicationFixture.cs");
        var compilation = CSharpCompilation.Create("NextPublicationContracts", new[] { tree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        ISourceGenerator[] Generators() => new[] { new LuminPackSourceGenerator().AsSourceGenerator() };
        GeneratorDriver driver = CSharpGeneratorDriver.Create(Generators(), parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var run = driver.GetRunResult();
        VerifyOutputReport(driver, compilation);
        string[] errors = diagnostics.Concat(output.GetDiagnostics()).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString()).ToArray();
        if (errors.Length != 0)
            return LuminPackExceptionHelper.ThrowInvalidOperation(string.Join(Environment.NewLine, errors.Take(18)));
        var formatter = run.Results.Single();
        var hints = formatter.GeneratedSources.Select(file => file.HintName).ToHashSet(StringComparer.Ordinal);
        Check(hints.Contains(BinaryFolder + "LuminPackGen.LuminPackSerializer.g.cs"), "Binary facade folder");
        Check(hints.Contains(JsonFolder + "LuminPackGen.LuminPackSerializer.g.cs"), "JSON facade folder");
        Check(hints.Contains(FormatterFolder + "LuminPackGen.System.g.cs"), "Scalar formatter grouping");
        Check(hints.Contains(FormatterFolder + "LuminPackGen.Array.g.cs"), "Array formatter grouping");
        Check(hints.Contains(FormatterFolder + "LuminPackGen.System.Collections.Generic.List`1.g.cs"), "Open formatter file identity");
        string all = string.Join("\n", run.Results.SelectMany(result => result.GeneratedSources).Select(file => file.SourceText.ToString()));
        Check(!Regex.IsMatch(all, @"\b(?:RootWrite|RootRead|DeserializeUnion|WritePolymorphismValue)\w*\s*[<(]"), "Direct operation publication");
        Check(!all.Contains("struct LuminPackObjectFraming"), "Core-owned framing markers");
        Check(!all.Contains("public static void Register<TMember>"), "Central Union registration");
        Check(all.Contains("Utf16SnapshotRecordLength(snapshot)", StringComparison.Ordinal) &&
            all.Contains("WriteSmallHashSetRaw(ref writer, exact)", StringComparison.Ordinal) &&
            all.Contains("SerializeInterfaceSlow", StringComparison.Ordinal), "Allocating entry specialization");
        var roots = output.GetTypeByMetadataName("LuminPack.LuminPackSerializer")!;
        CheckRootHint("ListOfStringFormatter", "Write", "NoInlining");
        CheckRootHint("ListOfStringFormatter", "Read", "NoInlining");
        CheckRootHint("SortedSetOfStringFormatter", "Read", "NoInlining");
        CheckRootHint("IListOfIntFormatter", "Read", null);
        CheckRootHint("IListOfIntFormatter", "Write", "NoInlining");
        VerifyPublicationLayout(formatter.GeneratedSources);
        var plainParse = parse.WithFeatures(Array.Empty<KeyValuePair<string, string>>());
        var layoutTree = CSharpSyntaxTree.ParseText("using LuminPack.Attribute; [LuminPackable] public partial class FirstRecord { public int Value; public string Name; } " +
            "[LuminPackable] public partial class SecondRecord { public int Value; public string Name; }", plainParse, "ApiLayout.cs");
        var layoutCompilation = CSharpCompilation.Create("NextApiLayout", new[] { layoutTree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver layoutDriver = CSharpGeneratorDriver.Create(Generators(), parseOptions: plainParse)
            .RunGeneratorsAndUpdateCompilation(layoutCompilation, out var layoutOutput, out var layoutDiagnostics);
        foreach (Diagnostic diagnostic in layoutDiagnostics.Concat(layoutOutput.GetDiagnostics()))
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                return LuminPackExceptionHelper.ThrowInvalidOperation(diagnostic.ToString());
        Check(VerifyPublicationLayout(layoutDriver.GetRunResult().Results[0].GeneratedSources) != 0, "Retained API layout coverage");
        var editedLayout = layoutCompilation.ReplaceSyntaxTree(layoutTree, CSharpSyntaxTree.ParseText(
            layoutTree.ToString().Replace("public int Value;", "public int Value; public long Added;"), plainParse, "ApiLayout.cs"));
        layoutDriver = layoutDriver.RunGeneratorsAndUpdateCompilation(editedLayout, out _, out _);
        GeneratorDriver freshLayout = CSharpGeneratorDriver.Create(Generators(), parseOptions: plainParse)
            .RunGeneratorsAndUpdateCompilation(editedLayout, out _, out _);
        Check(Snapshot(layoutDriver).SequenceEqual(Snapshot(freshLayout)), "Retained API layout incremental equality");
        VerifyPublicationLayout(layoutDriver.GetRunResult().Results[0].GeneratedSources);
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        if (!emitted.Success)
            return LuminPackExceptionHelper.ThrowInvalidOperation(string.Join(Environment.NewLine, emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(coreDll));
        Assembly assembly = Assembly.Load(stream.ToArray());
        Check(HasNoInlining("ListOfStringFormatter", "Write") && HasNoInlining("ListOfStringFormatter", "Read") &&
            HasNoInlining("SortedSetOfStringFormatter", "Read"), "Runtime no-inlining flags");
        foreach (string owner in new[] { "ConcurrentQueueOfStringFormatter", "ConcurrentStackOfStringFormatter",
                     "ConcurrentBagOfStringFormatter", "ConcurrentDictionaryOfStringAndStringFormatter",
                     "BlockingCollectionOfStringFormatter" })
        {
            foreach (string operation in new[] { "Write", "Read", "WriteJson", "ReadJson" })
                Check(HasNoInlining(owner, operation), owner + "." + operation + " stays outside its callers");
        }
        int checks = (int)assembly.GetType("PublicationFixture")!.GetMethod("Exercise")!.Invoke(null, null)!;
        Console.WriteLine($"Runtime publication contracts: {checks} checks passed.");
        Dictionary<string, string> expectedSources = Sources(driver);
        Parallel.For(0, 4, iteration =>
        {
            GeneratorDriver concurrent = CSharpGeneratorDriver.Create(Generators(), parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
            Dictionary<string, string> actualSources = Sources(concurrent);
            Check(actualSources.Count == expectedSources.Count, "Concurrent publication count");
            foreach (var file in expectedSources)
                Check(actualSources.TryGetValue(file.Key, out string? text) && text == file.Value, "Concurrent publication ownership");
        });
        foreach (string change in new[] { source + "\n// Incremental comment.\n", source.Replace("public int Number;", "public int Number; public int Added;") })
        {
            var updated = compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(change, parse, "PublicationFixture.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(updated, out _, out diagnostics);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(Generators(), parseOptions: parse).RunGeneratorsAndUpdateCompilation(updated, out _, out var freshDiagnostics);
            Check(Snapshot(driver).SequenceEqual(Snapshot(fresh)), "Retained and fresh driver equality");
            Check(!diagnostics.Concat(freshDiagnostics).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "Incremental generator diagnostics");
        }
        Console.WriteLine("Source shape, compilation, wire profiles, reuse, registration, concurrency and incremental contracts passed.");
        return 0;
        bool HasNoInlining(string owner, string operation)
        {
            var method = assembly.GetType("LuminPack.LuminPackSerializer+" + owner)!
                .GetMethod(operation, BindingFlags.Static | BindingFlags.NonPublic)!;
            return (method.GetMethodImplementationFlags() & MethodImplAttributes.NoInlining) != 0;
        }

        void CheckRootHint(string owner, string operation, string? hint)
        {
            var method = roots.GetTypeMembers(owner).Single().GetMembers(operation).OfType<IMethodSymbol>().Single();
            var attributes = method.DeclaringSyntaxReferences.Single().GetSyntax() is MethodDeclarationSyntax declaration
                ? declaration.AttributeLists.ToString() : string.Empty;
            Check(hint == null ? !attributes.Contains("MethodImpl", StringComparison.Ordinal) :
                attributes.Contains("MethodImplOptions." + hint, StringComparison.Ordinal), owner + "." + operation + " inlining policy");
        }
        static string[] Snapshot(GeneratorDriver current) => current.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal).ToArray();
        static void Check(bool condition, string name)
        {
            if (!condition)
                LuminPackExceptionHelper.ThrowInvalidOperation(name);
        }
    }

    /// <summary>Checks opt-in output accounting and retained-driver option changes.</summary>
    /// <param name="driver">The ordinary publication driver.</param>
    /// <param name="compilation">The unchanged consumer.</param>
    private static void VerifyOutputReport(GeneratorDriver driver, Compilation compilation)
    {
        const string hint = FormatterFolder + "LuminPackGen.OutputReport.g.cs";
        var ordinary = Sources(driver);
        RequireIncremental(!ordinary.ContainsKey(hint), "Output accounting must be opt-in.");
        var enabled = driver.WithUpdatedAnalyzerConfigOptions(new ReportOptions(true)).RunGenerators(compilation);
        var reported = Sources(enabled);
        RequireIncremental(reported.TryGetValue(hint, out string? report), "The requested output report was not emitted.");
        RequireIncremental(reported.Count == ordinary.Count + 1, "Output accounting changed the publication.");
        foreach (var file in ordinary)
            RequireIncremental(reported[file.Key] == file.Value, "Output accounting changed generated source.");
        foreach (var (family, folder) in new[] { ("Binary", BinaryFolder), ("Json", JsonFolder), ("Formatters", FormatterFolder) })
        {
            var files = ordinary.Where(file => file.Key.StartsWith(folder, StringComparison.Ordinal)).ToArray();
            string row = FormattableString.Invariant($"// {family} {files.Length} {files.Sum(file => file.Value.Length)} {files.Sum(file => System.Text.Encoding.UTF8.GetByteCount(file.Value))}\n");
            RequireIncremental(report!.Contains(row, StringComparison.Ordinal), "The output report did not match final source for " + family);
        }
        RequireIncremental(Regex.IsMatch(report!, @"// Payloads ([1-9]\d*) \1 \1 \1\n") &&
            Regex.IsMatch(report!, @"// SharedRoots ([1-9]\d*) \1 \1 \1\n"), "A library retains all four directions.");
        RequireIncremental(report!.Contains("// ProjectOrForced 1 1 1 1\n// DirectUsage 0 0 0 0\n// FacadeFallback 0 0 0 0\n", StringComparison.Ordinal),
            "A library's project-level retention should account for all four directions.");
        var disabled = enabled.WithUpdatedAnalyzerConfigOptions(new ReportOptions(false)).RunGenerators(compilation);
        RequireIncremental(Sources(disabled).Count == ordinary.Count && !Sources(disabled).ContainsKey(hint),
            "Turning off output accounting must remove the report.");
    }

    private sealed class ReportOptions(bool enabled) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Values(enabled);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

        private sealed class Values(bool enabled) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                value = enabled ? "true" : "false";
                return key == "build_property.LuminPackReportGeneratorOutput";
            }
        }
    }

    /// <summary>Checks final source order and generated-container cleanup on compiler-owned syntax trees.</summary>
    /// <param name="files">The final generated contributions.</param>
    /// <returns>The number of retained convenience APIs checked.</returns>
    private static int VerifyPublicationLayout(IEnumerable<GeneratedSourceResult> files)
    {
        int orderedApis = 0;
        foreach (var file in files)
        {
            if (file.HintName == FormatterFolder + "LuminPackGen.Generated.g.cs" ||
                file.HintName.EndsWith("/LuminPackGen.LuminPackSerializer.g.cs", StringComparison.Ordinal))
                continue;
            var syntax = file.SyntaxTree.GetRoot();
            int lastAlgorithm = 0;
            int firstApi = int.MaxValue;
            foreach (TypeDeclarationSyntax declaration in syntax.DescendantNodes().OfType<TypeDeclarationSyntax>())
                if (declaration.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal))
                    Check(declaration.Members.Count != 0, "No empty extension carrier: " + file.HintName);
            foreach (NamespaceDeclarationSyntax declaration in syntax.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
                Check(declaration.Members.Count != 0, "No empty payload namespace: " + file.HintName);
            foreach (MethodDeclarationSyntax method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                string name = method.Identifier.ValueText;
                bool api = method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } &&
                    (name is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" ||
                        name.StartsWith("Serialize_", StringComparison.Ordinal) || name.StartsWith("Deserialize_", StringComparison.Ordinal) ||
                        name.StartsWith("SerializeJson_", StringComparison.Ordinal) || name.StartsWith("DeserializeJson_", StringComparison.Ordinal));
                if (api)
                {
                    firstApi = Math.Min(firstApi, method.SpanStart);
                    orderedApis++;
                }
                else
                    lastAlgorithm = Math.Max(lastAlgorithm, method.Span.End);
            }
            Check(firstApi == int.MaxValue || lastAlgorithm < firstApi, "Algorithms precede convenience APIs: " + file.HintName);
        }
        return orderedApis;
        static void Check(bool condition, string message)
        {
            if (!condition)
                LuminPackExceptionHelper.ThrowInvalidOperation(message);
        }
    }

    private const string PublicationFixture = """
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
[LuminPackable, LuminPackUnion(1, typeof(TextShape))]
public partial interface IShape { }
[LuminPackable]
public sealed partial class TextShape : IShape { public int Number; public string Name; }
[LuminPackable]
public sealed partial class Ordinary { public int Number; public string Name; public int[] Values; public List<int> Items; public List<long> Longs; public int @event; }
[LuminPackable, LuminPackOption(UntrustedData = true, MaxCollectionLength = 2, MaxStringLength = 8)]
public sealed partial class Bounded { public Ordinary Child; }
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed partial class Versioned { [LuminPackOrder(0)] public int Value; [LuminPackOrder(2)] public string Name; }
[LuminPackable(GeneratorType.CircleReference)]
public sealed partial class Node { [LuminPackOrder(0)] public string Name; [LuminPackOrder(1)] public Node Next; }
[LuminPackable]
public partial interface IGenericRoot<T> { }
[LuminPackable]
public sealed partial class GenericItem<T> : IGenericRoot<T> { public T Value; public string Name; }
[LuminPackable]
public sealed partial class Constructed
{
    public int Number { get; }
    public string Name { get; }
    public int Extra { get; set; }
    [LuminPackConstructor] public Constructed(int number, string name) { Number = number; Name = name; }
}
[LuminPackable]
public sealed partial class Rented
{
    public int Number;
    [LuminPackPoolRent] public static Rented Rent() => new Rented { Number = 9 };
}
public sealed class ExternalShape : IShape { public int Number; }
[LuminPackable, LuminPackOption(UntrustedData = false)]
public sealed class PublishedTrustedFlag { public bool Flag; }
[LuminPackable, LuminPackOption(UntrustedData = false)]
public sealed class PublishedTrustedFlagHolder { public PublishedTrustedFlag Child; public bool Flag; }
[LuminPackable]
public sealed partial class ConcurrentRecord
{
    public ConcurrentQueue<string> Queue = new();
    public ConcurrentStack<string> Stack = new();
    public ConcurrentBag<string> Bag = new();
    public ConcurrentDictionary<string, string> Dictionary = new();
    public BlockingCollection<string> Blocking = new();
}
public static class PublicationFixture
{
    private static int s_checks;
    private static void Check(bool value) { if (!value) throw new Exception("Runtime publication contract failed."); s_checks++; }
    public static int Exercise()
    {
        Profile<LuminPackUtf8LengthOptions>(); Profile<LuminPackUtf8TokenOptions>(); Profile<LuminPackUtf8VarIntOptions>();
        Profile<LuminPackUtf16LengthOptions>(); Profile<LuminPackUtf16TokenOptions>(); Profile<LuminPackUtf16VarIntOptions>();
        CheckAllocatingEntries();
        CheckConcurrentCollections();
        var ordinary = new Ordinary { Number = 7, Name = "value", Values = new[] { 1, 2 }, Items = new List<int> { 3, 4 }, Longs = new List<long> { 5 }, @event = 42 };
        byte[] bytes = LuminPackSerializer.Serialize(ordinary);
        Ordinary read = null;
        LuminPackSerializer.Deserialize(bytes, ref read);
        Check(read.Number == ordinary.Number && read.Items.Count == 2 && read.Longs[0] == 5 && read.@event == 42);
        Ordinary same = read;
        LuminPackSerializer.Deserialize(bytes, ref read);
        Check(ReferenceEquals(same, read));
        using (var buffer = LuminBufferWriterPool.Rent())
        {
            LuminPackSerializer.Serialize(ordinary, buffer);
            Check(buffer.GetSpan().SequenceEqual(bytes));
        }
        bool boundedRejected = false;
        var bounded = new Bounded { Child = new Ordinary { Name = "too-long-input", Values = new int[3] } };
        byte[] boundedBytes = LuminPackSerializer.Serialize(bounded);
        Bounded boundedRead = null;
        try { LuminPackSerializer.Deserialize(boundedBytes, ref boundedRead); } catch { boundedRejected = true; }
        Check(boundedRejected);
        LuminPackSerializer.Register<IShape, ExternalShape>(77, Write, Read, WriteJson, ReadJson);
        IShape external = new ExternalShape { Number = 123 };
        byte[] registered = LuminPackSerializer.Serialize(external);
        IShape registeredRead = null;
        LuminPackSerializer.Deserialize(registered, ref registeredRead);
        Check(registeredRead is ExternalShape result && result.Number == 123);
        string registeredJson = LuminPackSerializer.SerializeJson<IShape, LuminPackUtf16LengthOptions>(external);
        IShape jsonRead = null;
        LuminPackSerializer.DeserializeJson<IShape, LuminPackUtf16LengthOptions>(registeredJson, ref jsonRead);
        Check(jsonRead is ExternalShape jsonResult && jsonResult.Number == 123);
        Check(LuminPackUnionFormatter<IShape>.TryGetFormatter(77, out _));
        bool rejected = false;
        try { LuminPackSerializer.Register<IShape, ExternalShape>(1, Write, Read, WriteJson, ReadJson); } catch (ArgumentException) { rejected = true; }
        Check(rejected);
        rejected = false;
        try { LuminPackSerializer.Register<IShape, ExternalShape>(77, Write, Read, WriteJson, ReadJson); } catch (ArgumentException) { rejected = true; }
        Check(rejected);
        var versioned = new Versioned { Value = 5, Name = "schema" };
        Versioned versionedRead = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(versioned), ref versionedRead);
        Check(versionedRead.Value == 5 && versionedRead.Name == "schema");
        var node = new Node { Name = "cycle" }; node.Next = node;
        Node nodeRead = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(node), ref nodeRead);
        Check(ReferenceEquals(nodeRead.Next, nodeRead));
        Node nodeJson = null;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(node), ref nodeJson);
        Check(ReferenceEquals(nodeJson.Next, nodeJson));
        IGenericRoot<string> generic = new GenericItem<string> { Value = "closed", Name = "slot" };
        IGenericRoot<string> genericRead = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(generic), ref genericRead);
        Check(genericRead is GenericItem<string> item && item.Value == "closed");
        IGenericRoot<string> genericJson = null;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(generic), ref genericJson);
        Check(genericJson is GenericItem<string> jsonItem && jsonItem.Value == "closed");
        var constructed = new Constructed(12, "constructor") { Extra = 17 };
        Constructed constructedRead = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(constructed), ref constructedRead);
        Check(constructedRead.Number == 12 && constructedRead.Extra == 17 && constructedRead.Name == "constructor");
        Rented rented = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new Rented { Number = 31 }), ref rented);
        Check(rented.Number == 31);
        var flags = new PublishedTrustedFlagHolder { Child = new PublishedTrustedFlag { Flag = true }, Flag = true };
        PublishedTrustedFlag flagRead = null;
        byte[] flagBytes = LuminPackSerializer.Serialize(flags.Child);
        Check(LuminPackSerializer.Deserialize(flagBytes, ref flagRead) == flagBytes.Length && flagRead.Flag);
        PublishedTrustedFlagHolder holderRead = null;
        byte[] holderBytes = LuminPackSerializer.Serialize(flags);
        Check(LuminPackSerializer.Deserialize(holderBytes, ref holderRead) == holderBytes.Length && holderRead.Child.Flag && holderRead.Flag);
        return s_checks;
    }

    private static void CheckAllocatingEntries()
    {
        TimeZoneInfo zone = TimeZoneInfo.Utc;
        byte[] snapshot = LuminPackSerializer.Serialize(zone);
        byte[] profiled = LuminPackSerializer.Serialize<TimeZoneInfo, LuminPackUtf8VarIntOptions>(zone);
        using var buffer = LuminBufferWriterPool.Rent();
        LuminPackSerializer.Serialize(zone, buffer);
        Check(snapshot.AsSpan().SequenceEqual(buffer.GetSpan()) && LuminPackSerializer.Deserialize<TimeZoneInfo>(snapshot).Equals(zone));
        Check(LuminPackSerializer.Deserialize<TimeZoneInfo, LuminPackUtf8VarIntOptions>(profiled).Equals(zone));
        Check(LuminPackSerializer.Serialize((TimeZoneInfo)null).AsSpan().SequenceEqual(new byte[] { 255 }));

        IList<int> list = new List<int> { 1, 3 };
        IList<int> alternate = new[] { 1, 3 };
        var textList = new List<string> { "one", "two" };
        var sorted = new SortedSet<string>(StringComparer.Ordinal) { "two", "one" };
        Check(LuminPackSerializer.Deserialize<List<string>>(LuminPackSerializer.Serialize(textList))[1] == "two");
        Check(LuminPackSerializer.Deserialize<SortedSet<string>>(LuminPackSerializer.Serialize(sorted)).SetEquals(sorted));
        byte[] exactList = LuminPackSerializer.Serialize(list);
        Check(exactList.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(alternate)) &&
            LuminPackSerializer.Deserialize<IList<int>>(exactList)[1] == 3);
        Check(LuminPackSerializer.Deserialize<IList<int>>(LuminPackSerializer.Serialize((IList<int>)null)) == null);

        ISet<byte> set = new HashSet<byte> { 1, 2, 3 };
        set.Remove(2);
        ISet<byte> alternateSet = new SortedSet<byte> { 1, 3 };
        byte[] exactSet = LuminPackSerializer.Serialize(set);
        Check(exactSet.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(alternateSet)) &&
            LuminPackSerializer.Deserialize<ISet<byte>>(exactSet).SetEquals(alternateSet));
        IReadOnlySet<int> readOnlySet = new HashSet<int> { 7, 9 };
        Check(LuminPackSerializer.Deserialize<IReadOnlySet<int>>(LuminPackSerializer.Serialize(readOnlySet)).SetEquals(readOnlySet));
    }
    private static void CheckConcurrentCollections()
    {
        var value = new ConcurrentRecord();
        value.Queue.Enqueue("queue");
        value.Stack.Push("stack");
        value.Bag.Add("bag");
        value.Dictionary["key"] = "value";
        value.Blocking.Add("blocked");

        var binary = LuminPackSerializer.Deserialize<ConcurrentRecord>(LuminPackSerializer.Serialize(value));
        var json = LuminPackSerializer.DeserializeJson<ConcurrentRecord>(LuminPackSerializer.SerializeJson(value));
        foreach (var result in new[] { binary, json })
        {
            Check(result.Queue.TryPeek(out var queue) && queue == "queue");
            Check(result.Stack.TryPeek(out var stack) && stack == "stack");
            Check(result.Bag.TryPeek(out var bag) && bag == "bag");
            Check(result.Dictionary.TryGetValue("key", out var entry) && entry == "value");
            Check(result.Blocking.TryTake(out var blocked) && blocked == "blocked");
        }

        foreach (int count in new[] { 0, 1, 65 })
        {
            var map = new ConcurrentDictionary<int, string>();
            for (int i = 0; i < count; i++)
                map[i] = i % 2 == 0 ? null : "value";
            byte[] bytes = LuminPackSerializer.Serialize(map);
            ConcurrentDictionary<int, string> fresh = null;
            LuminPackSerializer.Deserialize(bytes, ref fresh);
            Check(fresh.Count == count);
            foreach (var pair in map)
                Check(fresh.TryGetValue(pair.Key, out var item) && item == pair.Value);
            if (count > 0)
            {
                fresh = null;
                bool rejected = false;
                try { LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref fresh); }
                catch (Exception error) when (error.GetType().Name == "LuminPackException") { rejected = true; }
                Check(rejected && fresh == null);
            }
        }

        CheckConcurrentIntegerJson<LuminPackUtf8LengthOptions>();
        CheckConcurrentIntegerJson<LuminPackUtf16LengthOptions>();
        CheckConcurrentReadEdges();
        CheckConcurrentBinaryPairs<LuminPackUtf16LengthOptions>();
        CheckConcurrentBinaryPairs<LuminPackUtf8VarIntOptions>();
    }

    private static void CheckConcurrentReadEdges()
    {
        ConcurrentStack<string> stack = new ConcurrentStack<string>(new[] { "old" });
        var same = stack;
        LuminPackSerializer.DeserializeJson<ConcurrentStack<string>, LuminPackUtf8LengthOptions>("[]", ref stack);
        Check(ReferenceEquals(same, stack) && stack.IsEmpty);
        bool rejected = false;
        try { LuminPackSerializer.DeserializeJson<ConcurrentStack<string>, LuminPackUtf8LengthOptions>("[\"first\",\"second\",\"", ref stack); }
        catch (FormatException) { rejected = true; }
        Check(rejected && ReferenceEquals(same, stack) && string.Join(",", stack) == "second,first");
    }

    private static void CheckConcurrentBinaryPairs<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new ConcurrentDictionary<string, Guid>();
        for (int i = 0; i < 1024; i++)
            source.TryAdd(i + "\u952e" + new string('x', 48), new Guid(i, 2, 3, 1, 2, 3, 4, 5, 6, 7, 8));

        using var buffer = LuminBufferWriterPool.Rent();
        LuminPackSerializer.Serialize<ConcurrentDictionary<string, Guid>, TOptions>(source, buffer);
        byte[] bytes = buffer.GetSpan().ToArray();
        Check(bytes.Length > 32768);
        Span<byte> output = new byte[bytes.Length];
        var writer = new LuminPackWriter(ref output);
        LuminPackSerializer.Serialize<ConcurrentDictionary<string, Guid>, TOptions>(ref writer, in source);
        Check(writer.CurrentIndex == bytes.Length && output.SequenceEqual(bytes));
        var read = LuminPackSerializer.Deserialize<ConcurrentDictionary<string, Guid>, TOptions>(bytes);
        Check(read.Count == source.Count);
        foreach (var pair in source)
            Check(read.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static void CheckConcurrentIntegerJson<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var queue = new ConcurrentQueue<int>(new[] { int.MinValue, 0, int.MaxValue });
        var stack = new ConcurrentStack<long>(new[] { long.MinValue, 0, long.MaxValue });
        var bag = new ConcurrentBag<int>(new[] { 1, 2, 3 });
        var ints = new ConcurrentDictionary<int, int>();
        ints.TryAdd(int.MinValue, int.MaxValue);
        ints.TryAdd(0, -1);
        var longs = new ConcurrentDictionary<int, long>();
        longs.TryAdd(int.MaxValue, long.MinValue);
        longs.TryAdd(0, long.MaxValue);
        var other = new ConcurrentDictionary<long, long>();
        other.TryAdd(long.MinValue, long.MaxValue);
        other.TryAdd(0, -1);

        Check(LuminPackSerializer.SerializeJson<ConcurrentQueue<int>, TOptions>(queue) == "[-2147483648,0,2147483647]");
        Check(LuminPackSerializer.SerializeJson<ConcurrentStack<long>, TOptions>(stack) == "[9223372036854775807,0,-9223372036854775808]");
        Check(LuminPackSerializer.SerializeJson<ConcurrentBag<int>, TOptions>(bag) == "[" + string.Join(",", bag) + "]");
        Check(LuminPackSerializer.SerializeJson<ConcurrentDictionary<int, int>, TOptions>(ints) == PairArray(ints));
        Check(LuminPackSerializer.SerializeJson<ConcurrentDictionary<int, long>, TOptions>(longs) == PairArray(longs));
        Check(LuminPackSerializer.SerializeJson<ConcurrentDictionary<long, long>, TOptions>(other) == PairArray(other));

        static string PairArray<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> source)
        {
            var entries = new List<string>();
            foreach (var pair in source)
                entries.Add("[" + pair.Key + "," + pair.Value + "]");
            return "[" + string.Join(",", entries) + "]";
        }
    }
    private static void Profile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        IShape value = new TextShape { Number = 321, Name = "\u4e2d\u6587 / \"text\"" };
        byte[] bytes = LuminPackSerializer.Serialize<IShape, TOptions>(value);
        IShape read = null;
        LuminPackSerializer.Deserialize<IShape, TOptions>(bytes, ref read);
        Check(read is TextShape text && text.Number == 321 && text.Name == ((TextShape)value).Name);
        IShape same = read;
        LuminPackSerializer.Deserialize<IShape, TOptions>(bytes, ref read);
        Check(ReferenceEquals(same, read));
        IShape different = new Fixed0();
        LuminPackSerializer.Deserialize<IShape, TOptions>(bytes, ref different);
        Check(different is TextShape);
        string json = LuminPackSerializer.SerializeJson<IShape, TOptions>(value);
        IShape jsonRead = null;
        LuminPackSerializer.DeserializeJson<IShape, TOptions>(json, ref jsonRead);
        Check(jsonRead is TextShape item && item.Number == 321 && item.Name == ((TextShape)value).Name);
        Check(LuminPackSerializer.SerializeJson<IShape, TOptions>(jsonRead) == json);
        bool failed = false;
        try { LuminPackSerializer.Deserialize<IShape, TOptions>(bytes.AsSpan(0, bytes.Length - 1), ref read); } catch { failed = true; }
        Check(failed);
    }
    private static void Write(ref LuminPackWriter writer, ref IShape value) { writer.WriteUnionHeader(77); writer.Write(((ExternalShape)value).Number); }
    private static void Read(ref LuminPackReader reader, ref IShape value) { value = new ExternalShape { Number = reader.Read<int>() }; }
    private static void WriteJson(ref LuminPackJsonWriter writer, ref IShape value)
    {
        writer.WriteObjectStart<LuminPackUtf16LengthOptions>();
        writer.WritePropertyName<LuminPackUtf16LengthOptions>(LuminPackConstUtf8.TypeU16);
        writer.Write<LuminPackUtf16LengthOptions>(77);
        writer.WritePropertyName<LuminPackUtf16LengthOptions>(LuminPackConstUtf8.ValueU16);
        writer.Write<LuminPackUtf16LengthOptions>(((ExternalShape)value).Number);
        writer.WriteObjectEnd<LuminPackUtf16LengthOptions>();
    }
    private static void ReadJson(ref LuminPackJsonReader reader, ref IShape value) { value = new ExternalShape { Number = reader.Read<int, LuminPackUtf16LengthOptions>() }; }
}
""";
}
