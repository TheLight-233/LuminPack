using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPackBenchmark;

[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]

/// <summary>Provides program functionality.</summary>
class Program
{
    /// <summary>Measures main for the configured benchmark input.</summary>
    /// <param name="args">The args used by this operation.</param>
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--binary-paired-probe")
        {
            BinaryPairedProbe.Run(args[1]);
            return;
        }

        if (args.Length == 1 && args[0] == "--fixed-field-assembly-probe")
        {
            CharacterAssemblyProbe.RunFixedFields();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-assembly-probe")
        {
            CharacterAssemblyProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--verify-optimization-fixtures")
        {
            new CharacterFixedFieldsRatioBenchmark().Setup();
            new CharacterBinaryRatioBenchmark().Setup();
            new CharacterJsonRatioBenchmark().Setup();
            foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
            {
                var benchmark = new BinaryOptimizationBenchmark { Encoding = encoding };
                try { benchmark.Setup(); }
                finally { benchmark.Cleanup(); }
            }
            foreach (var shape in new[] { "Reversed", "Indented", "Utf16" })
                new JsonInputShapeOptimizationBenchmark { Shape = shape }.Setup();
            new SerializerComparisonBenchmark().Setup();
            var graph = new ContinuationOptimizationBenchmark();
            try { graph.Setup(); }
            finally { graph.Cleanup(); }
            var medium = new MediumStringOptimizationBenchmark();
            try { medium.Setup(); }
            finally { medium.Cleanup(); }
            Console.WriteLine("All optimization fixtures preserve their full object graphs.");
            return;
        }

        if (args.Length == 1 && args[0] == "--buffer-pool-jit-probe")
        {
            BufferWriterPoolJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-jit-probe")
        {
            CharacterSaveDataJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-concrete-jit-probe")
        {
            CharacterSaveDataConcreteJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--dictionary-bucket-jit-probe")
        {
            DictionaryBucketJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-json-jit-probe")
        {
            CharacterSaveDataJsonJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--json-initializer-jit-probe")
        {
            CharacterSaveDataJsonJitProbe.RunInitializerControl();
            return;
        }

        if (args.Length == 0 || !args[0].StartsWith("-", StringComparison.Ordinal))
        {
            var benchmarks = typeof(Program).Assembly.GetTypes()
                .Where(type => type.IsVisible && !type.IsAbstract && !type.ContainsGenericParameters &&
                    type.GetMethods().Any(method => method.IsDefined(typeof(BenchmarkAttribute), inherit: true)))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            var selected = args.Length == 0 ? SelectBenchmarks(benchmarks) : MatchBenchmarks(benchmarks, args[0]);
            if (selected.Length == 0)
            {
                if (args.Length != 0)
                {
                    Console.Error.WriteLine($"No unique benchmark matches '{args[0]}'. Use its exact class name or full type name.");
                    Environment.ExitCode = 1;
                }
                return;
            }

            BenchmarkSwitcher.FromTypes(selected).RunAll(args: args.Skip(1).ToArray());
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    /// <summary>Selects benchmarks by an exact class name, a displayed number, or an explicit wildcard.</summary>
    /// <param name="benchmarks">Runnable benchmark types in display order.</param>
    /// <returns>The selected types, or an empty array when selection is cancelled.</returns>
    private static Type[] SelectBenchmarks(Type[] benchmarks)
    {
        for (int index = 0; index < benchmarks.Length; index++)
            Console.WriteLine($"  {index}: {benchmarks[index].FullName}");

        while (true)
        {
            Console.Write("Enter an exact benchmark name, its number, or * for all (empty to cancel): ");
            string? input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return [];
            var selected = MatchBenchmarks(benchmarks, input);
            if (selected.Length != 0) return selected;
            Console.WriteLine("No unique match. Enter an exact class name, full type name, or displayed number.");
        }
    }

    /// <summary>Resolves a selection without interpreting a name as a first-letter shortcut.</summary>
    /// <param name="benchmarks">Runnable benchmark types in display order.</param>
    /// <param name="input">An exact name, a zero-based index, or the explicit all-types wildcard.</param>
    /// <returns>Matching types, or an empty array for an invalid or ambiguous selection.</returns>
    private static Type[] MatchBenchmarks(Type[] benchmarks, string input)
    {
        if (input == "*") return benchmarks;
        if (int.TryParse(input, out int index))
            return index >= 0 && index < benchmarks.Length ? [benchmarks[index]] : [];

        var matches = benchmarks.Where(type => string.Equals(type.Name, input, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.FullName, input, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches : [];
    }

}
