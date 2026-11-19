using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using LuminPack.NinoBenchmark.AppData;
using LuminPack.NinoBenchmark.Workloads;

namespace LuminPack.NinoBenchmark;

internal static class Program
{
    /// <summary>Validates fixtures or measures both serializers with the same runtime settings.</summary>
    /// <param name="args">The verification selector or BenchmarkDotNet options.</param>
    /// <returns>Zero if all selected checks or measurements succeed.</returns>
    private static int Main(string[] args)
    {
        Type[] benchmarks = [typeof(WorkloadTest), typeof(AppDataTest)];
        if (args is ["--verify"])
            return FixtureVerification.Run(benchmarks);

        bool smoke = args.Contains("--smoke", StringComparer.Ordinal);
        Job job = smoke ? Job.Dry : Job.Default.WithId("TieredPGO")
            .WithLaunchCount(3)
            .WithEnvironmentVariable("DOTNET_TieredCompilation", "1")
            .WithEnvironmentVariable("DOTNET_TieredPGO", "1");
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job.WithGcServer(true))
            .AddExporter(JsonExporter.Full, CsvMeasurementsExporter.Default);
        var summaries = BenchmarkSwitcher.FromTypes(benchmarks)
            .Run(args.Where(argument => argument != "--smoke").ToArray(), config).ToArray();
        return summaries.Length != 0 && summaries.All(summary => summary.Reports.Length != 0 &&
            summary.Reports.All(report => report.Success)) ? 0 : 1;
    }
}
