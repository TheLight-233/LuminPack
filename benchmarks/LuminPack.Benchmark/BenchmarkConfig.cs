using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;
using System.Globalization;

namespace LuminPackBenchmark;

/// <summary>Uses reproducible execution settings and retains measurements from every process.</summary>
public class BenchmarkConfig : ManualConfig
{
    internal const string JitModeVariable = "LUMINPACK_BENCHMARK_JIT";
    internal const long Timestamp = 1_700_000_000;

    /// <summary>Separates profile-guided timings from the optional non-tiered JIT control.</summary>
    public BenchmarkConfig()
    {
        string mode = Environment.GetEnvironmentVariable(JitModeVariable) ?? "tiered";
        if (mode is not ("tiered" or "fullopts" or "both"))
            throw new ArgumentException($"{JitModeVariable} must be tiered, fullopts or both.");

        if (mode is "tiered" or "both") AddJob(CreateJob("TieredPGO", true));
        if (mode is "fullopts" or "both") AddJob(CreateJob("FullOpts", false));
        AddExporter(JsonExporter.Full, new CsvMeasurementsExporter(CsvSeparator.Comma,
            SummaryStyle.Default.WithCultureInfo(CultureInfo.InvariantCulture)));
    }

    /// <summary>Applies the same runtime, GC, warmup and sampling policy to every library.</summary>
    /// <param name="name">The JIT configuration displayed in reports.</param>
    /// <param name="tiered">Whether to enable tiered compilation and dynamic PGO.</param>
    /// <returns>A separately named three-process benchmark job.</returns>
    /// <summary>Builds the benchmark child project with the generator the host assembly was built with.</summary>
    /// <returns>The MSBuild arguments that select that generator.</returns>
    private static MsBuildArgument[] GeneratorArguments()
    {
        var metadata = typeof(BenchmarkConfig).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        string generator = metadata.TryGetValue("LuminPack.Generator", out string? value) && !string.IsNullOrEmpty(value) ? value : "Next";
        string? analyzer = metadata.TryGetValue("LuminPack.LegacyAnalyzer", out string? path) && !string.IsNullOrEmpty(path) ? path : null;
        var arguments = new List<MsBuildArgument> { new($"/p:LuminPackGenerator={generator}") };
        if (analyzer != null)
            arguments.Add(new MsBuildArgument($"/p:LuminPackLegacyAnalyzer={analyzer}"));
        if (generator == "Next" || analyzer != null)
            arguments.Add(new MsBuildArgument("/p:LuminPackLegacyGeneratorReference=false"));
        return arguments.ToArray();
    }

    private static Job CreateJob(string name, bool tiered) => Job.Default
        .WithArguments(GeneratorArguments())
        .WithId(name)
        .WithRuntime(CoreRuntime.Core10_0)
        .WithGcServer(true)
        .WithLaunchCount(3)
        .WithMinWarmupCount(8)
        .WithMaxWarmupCount(30)
        .WithIterationCount(15)
        .WithIterationTime(TimeInterval.FromSeconds(1))
        .WithMinIterationTime(TimeInterval.FromMilliseconds(500))
        .WithOutlierMode(OutlierMode.DontRemove)
        .WithEnvironmentVariable("DOTNET_TieredCompilation", tiered ? "1" : "0")
        .WithEnvironmentVariable("DOTNET_TieredPGO", tiered ? "1" : "0");
}
