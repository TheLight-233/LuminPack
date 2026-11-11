using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

/// <summary>Runs the Next and legacy generators over one corpus in the same process and reports repeated timings.</summary>
/// <remarks>The harness measures generator time and allocations only. Generated file count is printed to show what each run
/// published, not as a quality target. Every full run builds a fresh compilation and driver, so JIT and package load are
/// warm but generator state is cold. The incremental scenarios reuse one driver and edit one file at a time.</remarks>
internal static class Program
{
    private const string PackableAttribute = "LuminPack.Attribute.LuminPackableAttribute";
    private const string ModelStep = "LuminPack.Next.PayloadModels";

    /// <summary>Runs the harness.</summary>
    /// <param name="args">Corpus, repetition and legacy generator options.</param>
    /// <returns>Zero when every requested benchmark ran.</returns>
    private static int Main(string[] args)
    {
        if (!TryParse(args, out Options options, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine("usage: GeneratorBenchmarks [--corpus <dir>] [--types <n>] [--runs <n>] [--core <dll>] " +
                "[--refs <dir>] [--old-dll <dll>] [--old-mode on|off|verify] [--skip-next] [--skip-old]");
            return 1;
        }
        string repository = FindRepositoryRoot();
        string coreDll = options.CoreDll ?? FindCore(repository);
        string oldDll = options.OldDll ?? Path.Combine(repository, "src", "LuminPack.Generator", "bin", "Release",
            "netstandard2.0", "LuminPack.Generator.dll");
        IReadOnlyList<CorpusFile> corpus = options.Corpus == null ? SyntheticCorpus(options.Types) : ReadCorpus(options.Corpus);
        CSharpParseOptions parse = CreateParseOptions();
        var references = LoadReferences(coreDll, options.References);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
            optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable, deterministic: true);

        Console.WriteLine("LuminPack generator benchmark");
        Console.WriteLine($"  corpus: {(options.Corpus == null ? "synthetic " + options.Types.ToString(CultureInfo.InvariantCulture) + " records" : options.Corpus)} " +
            $"({corpus.Count.ToString(CultureInfo.InvariantCulture)} files)");
        Console.WriteLine($"  runs: {options.Runs.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"  core: {coreDll}");
        Console.WriteLine($"  next: {typeof(NextGenerator).Assembly.GetName().Version}");
        Console.WriteLine($"  old:  {(File.Exists(oldDll) ? oldDll + " (" + options.OldMode + ")" : "not found")}");
        Console.WriteLine();
        if (options.References != null)
            Console.WriteLine($"  refs: {options.References}");
        Console.WriteLine($"{"scenario",-26} {"generator",-8} {"median s",10} {"spread s",10} {"alloc MB",10} {"files",6} {"source MB",10}");

        Func<ISourceGenerator[]> next = static () => new ISourceGenerator[] { new NextGenerator().AsSourceGenerator() };
        Func<ISourceGenerator[]>? legacy = null;
        if (!options.SkipOld && File.Exists(oldDll))
        {
            Assembly assembly = Assembly.LoadFrom(oldDll);
            SetLegacyMode(assembly, options.OldMode);
            legacy = () => CreateLegacyGenerators(assembly);
        }
        else if (!options.SkipOld)
        {
            Console.WriteLine($"legacy generator not found: {oldDll}");
        }

        if (!options.SkipNext)
        {
            RunFull("full generation", "next", next, corpus, references, parse, compilationOptions, options.Runs);
            RunIncremental("comment edit", "next", next, corpus, references, parse, compilationOptions, options.Runs, track: false);
            RunIncremental("member edit", "next", next, corpus, references, parse, compilationOptions, options.Runs, track: false);
            RunIncremental("incremental trace", "next", next, corpus, references, parse, compilationOptions, runs: 1, track: true);
            RunAnalysis("analysis only", corpus, references, parse, compilationOptions, options.Runs);
        }
        if (legacy != null)
        {
            RunFull("full generation", "old", legacy, corpus, references, parse, compilationOptions, options.Runs);
            RunIncremental("comment edit", "old", legacy, corpus, references, parse, compilationOptions, options.Runs, track: false);
            RunIncremental("member edit", "old", legacy, corpus, references, parse, compilationOptions, options.Runs, track: false);
        }
        return 0;
    }

    private static void RunFull(string scenario, string name, Func<ISourceGenerator[]> factory, IReadOnlyList<CorpusFile> corpus,
        MetadataReference[] references, CSharpParseOptions parse, CSharpCompilationOptions compilationOptions, int runs)
    {
        // One unrecorded run warms JIT, static state and the assembly load before the samples.
        Generate(factory(), corpus, references, parse, compilationOptions, track: false);
        var samples = new List<Measurement>(runs);
        int files = 0;
        for (int run = 0; run < runs; run++)
        {
            Measurement measurement = Generate(factory(), corpus, references, parse, compilationOptions, track: false, out files);
            samples.Add(measurement);
        }
        Report(scenario, name, samples, files);
    }

    private static void RunIncremental(string scenario, string name, Func<ISourceGenerator[]> factory, IReadOnlyList<CorpusFile> corpus,
        MetadataReference[] references, CSharpParseOptions parse, CSharpCompilationOptions compilationOptions, int runs, bool track)
    {
        var samples = new List<Measurement>(runs);
        int files = 0;
        for (int run = 0; run < runs; run++)
        {
            SyntaxTree[] trees = CreateTrees(corpus, parse);
            var compilation = CSharpCompilation.Create("GeneratorBenchmarks", trees, references, compilationOptions);
            GeneratorDriver driver = CSharpGeneratorDriver.Create(factory(), parseOptions: parse,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, track));
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
            var commentTree = CSharpSyntaxTree.ParseText(corpus[0].Text + Environment.NewLine + "// benchmark edit", parse,
                corpus[0].Path);
            var commentCompilation = compilation.ReplaceSyntaxTree(trees[0], commentTree);
            Measurement comment = Measure(() => driver = driver.RunGeneratorsAndUpdateCompilation(commentCompilation, out _, out _));
            // The comment edit touches the first file; the member edit adds a field to the first packable declaration.
            int payload = PayloadFile(corpus);
            var memberTree = CSharpSyntaxTree.ParseText(AddMember(payload == 0 ? commentTree.ToString() : corpus[payload].Text, parse), parse,
                corpus[payload].Path);
            var memberCompilation = commentCompilation.ReplaceSyntaxTree(payload == 0 ? commentTree : trees[payload], memberTree);
            Measurement member = Measure(() => driver = driver.RunGeneratorsAndUpdateCompilation(memberCompilation, out _, out _));
            files = CountFiles(driver);
            samples.Add(scenario == "comment edit" ? comment : member);
            if (track && run == 0)
                PrintModelSteps(driver);
        }
        Report(scenario, name, samples, files);
    }

    private static void RunAnalysis(string scenario, IReadOnlyList<CorpusFile> corpus, MetadataReference[] references,
        CSharpParseOptions parse, CSharpCompilationOptions compilationOptions, int runs)
    {
        SyntaxTree[] trees = CreateTrees(corpus, parse);
        var samples = new List<Measurement>(runs);
        string coverage = string.Empty;
        for (int run = 0; run < runs; run++)
        {
            var compilation = CSharpCompilation.Create("GeneratorBenchmarks", trees, references, compilationOptions);
            GeneratorOptions options = GeneratorOptionsFactory.From(compilation);
            INamedTypeSymbol[] payloads = Payloads(compilation);
            samples.Add(Measure(() =>
            {
                int complete = 0, incomplete = 0, unsupported = 0;
                var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (INamedTypeSymbol symbol in payloads)
                {
                    ModelResult result = TypeModelFactory.TryCreate(symbol, compilation, options);
                    if (result.Model == null)
                        unsupported++;
                    else if (result.IsComplete)
                        complete++;
                    else
                        incomplete++;
                    if (result.Model is { } model)
                    {
                        string kind = model.Kind.ToString();
                        kinds[kind] = kinds.TryGetValue(kind, out int count) ? count + 1 : 1;
                    }
                }
                coverage = $"complete={complete} incomplete={incomplete} unsupported={unsupported} " +
                    string.Join(" ", kinds.Select(pair => pair.Key + "=" + pair.Value));
            }));
        }
        Report(scenario, "next", samples, files: 0);
        if (coverage.Length != 0)
            Console.WriteLine($"  {coverage}");
    }

    private static Measurement Generate(ISourceGenerator[] generators, IReadOnlyList<CorpusFile> corpus,
        MetadataReference[] references, CSharpParseOptions parse, CSharpCompilationOptions compilationOptions, bool track)
        => Generate(generators, corpus, references, parse, compilationOptions, track, out _);

    private static Measurement Generate(ISourceGenerator[] generators, IReadOnlyList<CorpusFile> corpus,
        MetadataReference[] references, CSharpParseOptions parse, CSharpCompilationOptions compilationOptions, bool track, out int files)
    {
        SyntaxTree[] trees = CreateTrees(corpus, parse);
        var compilation = CSharpCompilation.Create("GeneratorBenchmarks", trees, references, compilationOptions);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parse,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, track));
        Measurement measurement = Measure(() => driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _));
        files = CountFiles(driver);
        return measurement;
    }

    private static Measurement Measure(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        long after = GC.GetTotalAllocatedBytes(precise: true);
        return new Measurement(stopwatch.Elapsed.TotalSeconds, (after - before) / 1_000_000d);
    }

    private static void Report(string scenario, string name, List<Measurement> samples, int files)
    {
        double[] seconds = samples.Select(sample => sample.Seconds).ToArray();
        double[] allocations = samples.Select(sample => sample.AllocatedMegabytes).ToArray();
        Console.WriteLine($"{scenario,-26} {name,-8} {Median(seconds),10:F3} {(seconds.Max() - seconds.Min()),10:F3} " +
            $"{Median(allocations),10:F1} {files,6} {(files == 0 ? 0 : _lastSourceCharacters / 1_000_000d),10:F2}");
    }

    private static double Median(double[] values)
    {
        Array.Sort(values);
        int middle = values.Length / 2;
        return values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
    }

    private static void PrintModelSteps(GeneratorDriver driver)
    {
        var steps = driver.GetRunResult().Results[0].TrackedSteps;
        if (!steps.TryGetValue(ModelStep, out var runs))
            return;
        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var changed = new List<string>();
        foreach (var step in runs)
        foreach (var (value, reason) in step.Outputs)
        {
            string name = reason.ToString();
            reasons[name] = reasons.TryGetValue(name, out int count) ? count + 1 : 1;
            if (reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New)
                changed.Add(Describe(value));
        }
        Console.WriteLine($"  models: {string.Join(" ", reasons.Select(pair => pair.Key + "=" + pair.Value))}" +
            (changed.Count == 0 ? string.Empty : "; changed: " + string.Join(", ", changed)));
    }

    private static string Describe(object value)
    {
        object? result = value.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(value);
        object? model = result?.GetType().GetField("Model", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(result);
        if (model == null)
            return "<none>";
        object? identity = model.GetType().GetField("Identity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(model);
        return identity?.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(identity)?.ToString() ?? "?";
    }

    private static int CountFiles(GeneratorDriver driver)
    {
        var results = driver.GetRunResult().Results;
        _lastSourceCharacters = results.Sum(result => result.GeneratedSources.Sum(source => (long)source.SourceText.Length));
        return results.Sum(result => result.GeneratedSources.Length);
    }

    private static long _lastSourceCharacters;

    private static INamedTypeSymbol[] Payloads(Compilation compilation)
        => compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>()
            .Where(type => type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == PackableAttribute))
            .ToArray();

    private static SyntaxTree[] CreateTrees(IReadOnlyList<CorpusFile> corpus, CSharpParseOptions parse)
        => corpus.Select(file => CSharpSyntaxTree.ParseText(file.Text, parse, file.Path)).ToArray();

    /// <summary>Adds one public field to the first class or struct of a file, for the member-edit scenario.</summary>
    /// <param name="text">The original file.</param>
    /// <param name="parse">The parse options of the corpus.</param>
    /// <returns>The edited file.</returns>
    private static string AddMember(string text, CSharpParseOptions parse)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(text, parse).GetRoot();
        TypeDeclarationSyntax? declaration = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(member => member is ClassDeclarationSyntax or StructDeclarationSyntax)
            .OrderBy(member => IsPackable(member) ? 0 : 1).FirstOrDefault();
        if (declaration == null)
            return text;
        MemberDeclarationSyntax field = SyntaxFactory.ParseMemberDeclaration("public int Extra;")!;
        return root.ReplaceNode(declaration, declaration.AddMembers(field)).ToFullString();
    }

    private static bool IsPackable(TypeDeclarationSyntax declaration)
        => declaration.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() is "LuminPackable" or
            "LuminPackableAttribute" or "LuminPack.Attribute.LuminPackable");

    private static int PayloadFile(IReadOnlyList<CorpusFile> corpus)
    {
        for (int index = 0; index < corpus.Count; index++)
            if (corpus[index].Text.Contains("[LuminPackable", StringComparison.Ordinal))
                return index;
        return 0;
    }

    private static CSharpParseOptions CreateParseOptions()
        => new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse, preprocessorSymbols: new[]
        {
            "NET5_0_OR_GREATER", "NET6_0_OR_GREATER", "NET7_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER",
            "NET10_0_OR_GREATER", "NETCOREAPP", "NET", "RELEASE",
        }).WithFeatures(new[]
        {
            new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack"),
            new KeyValuePair<string, string>("InterceptorsPreviewNamespaces", "LuminPack"),
        });

    private static MetadataReference[] LoadReferences(string coreDll, string? extra)
    {
        var references = new List<MetadataReference>();
        string? trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> extraFiles = extra == null ? Array.Empty<string>() : Directory.GetFiles(extra, "*.dll")
            .Where(file => !Path.GetFileName(file).StartsWith("LuminPack", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(file).StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase));
        foreach (string path in (trusted?.Split(Path.PathSeparator) ?? Array.Empty<string>()).Concat(extraFiles))
        {
            if (!names.Add(Path.GetFileName(path)))
                continue;
            try
            {
                using var stream = File.OpenRead(path);
                using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
                if (pe.HasMetadata)
                    references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (IOException)
            {
            }
        }
        references.Add(MetadataReference.CreateFromFile(coreDll));
        return references.ToArray();
    }

    private static IReadOnlyList<CorpusFile> ReadCorpus(string directory)
        => Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(file => new CorpusFile(file, File.ReadAllText(file)))
            .ToArray();

    private static IReadOnlyList<CorpusFile> SyntheticCorpus(int types)
    {
        var files = new CorpusFile[types];
        for (int index = 0; index < types; index++)
        {
            var text = new StringBuilder();
            text.AppendLine("using LuminPack.Attribute;");
            text.AppendLine();
            text.AppendLine("namespace Bench");
            text.AppendLine("{");
            text.AppendLine("    [LuminPackable]");
            text.AppendLine("    public class Record" + index.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("    {");
            text.AppendLine("        public int Id;");
            text.AppendLine("        public string Name;");
            text.AppendLine("    }");
            text.AppendLine("}");
            files[index] = new CorpusFile("Record" + index.ToString(CultureInfo.InvariantCulture) + ".cs", text.ToString());
        }
        return files;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "LuminPack.sln")))
                return directory.FullName;
        throw new InvalidOperationException("LuminPack.sln was not found above the harness output directory.");
    }

    private static string FindCore(string repository)
    {
        string preferred = Path.Combine(repository, "src", "LuminPack.Core", "bin", "Release", "net10.0", "LuminPack.Core.dll");
        if (File.Exists(preferred))
            return preferred;
        string release = Path.Combine(repository, "src", "LuminPack.Core", "bin", "Release");
        string? candidate = Directory.Exists(release)
            ? Directory.GetFiles(release, "LuminPack.Core.dll", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        return candidate ?? throw new FileNotFoundException("LuminPack.Core.dll was not found under src/LuminPack.Core/bin/Release.");
    }

    private static ISourceGenerator[] CreateLegacyGenerators(Assembly assembly)
        => assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null &&
                typeof(IIncrementalGenerator).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => ((IIncrementalGenerator)Activator.CreateInstance(type)!).AsSourceGenerator())
            .ToArray();

    private static void SetLegacyMode(Assembly assembly, string mode)
    {
        Type? emission = assembly.GetType("LuminPack.SourceGenerator.Direct.DirectEmission");
        FieldInfo? field = emission?.GetField("Mode", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field?.FieldType.IsEnum == true)
            field.SetValue(null, Enum.Parse(field.FieldType, mode, ignoreCase: true));
    }

    private static bool TryParse(string[] args, out Options options, out string? error)
    {
        options = new Options();
        error = null;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--corpus" when index + 1 < args.Length:
                    options.Corpus = Path.GetFullPath(args[++index]);
                    break;
                case "--types" when index + 1 < args.Length && int.TryParse(args[index + 1], out int types) && types > 0:
                    options.Types = types;
                    index++;
                    break;
                case "--runs" when index + 1 < args.Length && int.TryParse(args[index + 1], out int runs) && runs > 0:
                    options.Runs = runs;
                    index++;
                    break;
                case "--core" when index + 1 < args.Length:
                    options.CoreDll = Path.GetFullPath(args[++index]);
                    break;
                case "--old-dll" when index + 1 < args.Length:
                    options.OldDll = Path.GetFullPath(args[++index]);
                    break;
                case "--old-mode" when index + 1 < args.Length:
                    options.OldMode = args[++index];
                    break;
                case "--refs" when index + 1 < args.Length:
                    options.References = Path.GetFullPath(args[++index]);
                    break;
                case "--skip-next":
                    options.SkipNext = true;
                    break;
                case "--skip-old":
                    options.SkipOld = true;
                    break;
                default:
                    error = "unknown or incomplete argument: " + argument;
                    return false;
            }
        }
        return true;
    }

    private readonly record struct Measurement(double Seconds, double AllocatedMegabytes);

    private sealed record CorpusFile(string Path, string Text);

    private sealed class Options
    {
        internal string? Corpus;
        internal int Types = 200;
        internal int Runs = 3;
        internal string? CoreDll;
        internal string? OldDll;
        internal string? References;
        internal string OldMode = "on";
        internal bool SkipNext;
        internal bool SkipOld;
    }
}
