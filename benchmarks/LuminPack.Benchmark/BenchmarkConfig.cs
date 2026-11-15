using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;
using System.Globalization;
using System.Reflection;

namespace LuminPackBenchmark;

/// <summary>Uses reproducible execution settings and retains measurements from every process.</summary>
public class BenchmarkConfig : ManualConfig
{
    internal const string JitModeVariable = "LUMINPACK_BENCHMARK_JIT";
    internal const string SatoriVariable = "LUMINPACK_BENCHMARK_SATORI";
    internal const long Timestamp = 1_700_000_000;

    /// <summary>Separates profile-guided timings from the optional non-tiered JIT control.</summary>
    public BenchmarkConfig()
    {
        string mode = Environment.GetEnvironmentVariable(JitModeVariable) ?? "tiered";
        if (mode is not ("tiered" or "fullopts" or "both"))
            throw new ArgumentException($"{JitModeVariable} must be tiered, fullopts or both.");

        SatoriRuntime.RequestMode satori = SatoriRuntime.RequestedMode();
        string? satoriRoot = satori == SatoriRuntime.RequestMode.Off ? null : SatoriRuntime.Resolve();
        if (mode is "tiered" or "both")
        {
            if (satori != SatoriRuntime.RequestMode.Only) AddJob(CreateJob("TieredPGO", true, null));
            if (satori != SatoriRuntime.RequestMode.Off) AddJob(CreateJob("TieredPGO-Satori", true, satoriRoot));
        }
        if (mode is "fullopts" or "both")
        {
            if (satori != SatoriRuntime.RequestMode.Only) AddJob(CreateJob("FullOpts", false, null));
            if (satori != SatoriRuntime.RequestMode.Off) AddJob(CreateJob("FullOpts-Satori", false, satoriRoot));
        }
        AddExporter(JsonExporter.Full, new CsvMeasurementsExporter(CsvSeparator.Comma,
            SummaryStyle.Default.WithCultureInfo(CultureInfo.InvariantCulture)));
    }

    /// <summary>Applies the same runtime, GC, warmup and sampling policy to every library.</summary>
    /// <param name="name">The JIT configuration displayed in reports.</param>
    /// <param name="tiered">Whether to enable tiered compilation and dynamic PGO.</param>
    /// <param name="satoriRoot">The prepared Satori runtime root, or null to use the stock runtime.</param>
    /// <returns>A separately named three-process benchmark job.</returns>
    private static Job CreateJob(string name, bool tiered, string? satoriRoot)
    {
        Job job = Job.Default
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
            .WithToolchain(CreateToolchain(satoriRoot is not null))
            .WithEnvironmentVariable("DOTNET_TieredCompilation", tiered ? "1" : "0")
            .WithEnvironmentVariable("DOTNET_TieredPGO", tiered ? "1" : "0");
        return satoriRoot is null
            ? job.WithEnvironmentVariable(SatoriVariable, "0")
            : SatoriRuntime.Apply(job, satoriRoot);
    }

    /// <summary>Selects the stock dll launch or the apphost launch that can resolve the private runtime.</summary>
    /// <param name="satori">Whether the measured processes use the prepared Satori runtime.</param>
    /// <returns>A toolchain that records the benchmark project path at build time.</returns>
    private static IToolchain CreateToolchain(bool satori) => satori
        ? new Toolchain("Satori", new ProjectPathCsProjGenerator("net10.0", appHost: true), new DotNetCliBuilder("net10.0"), new Executor())
        : new Toolchain("CsProj", new ProjectPathCsProjGenerator("net10.0", appHost: false), new DotNetCliBuilder("net10.0"), new DotNetCliExecutor(null!));

    /// <summary>Builds the benchmark child project with the generator the host assembly was built with.</summary>
    /// <returns>The MSBuild arguments that select that generator.</returns>
    private static MsBuildArgument[] GeneratorArguments()
    {
        var metadata = typeof(BenchmarkConfig).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        string generator = metadata.TryGetValue("LuminPack.Generator", out string? value) && !string.IsNullOrEmpty(value) ? value : "Next";
        string? analyzer = metadata.TryGetValue("LuminPack.LegacyAnalyzer", out string? path) && !string.IsNullOrEmpty(path) ? path : null;
        var arguments = new List<MsBuildArgument> { new($"/p:LuminPackGenerator={generator}") };
        if (analyzer != null)
            arguments.Add(new MsBuildArgument($"/p:LuminPackLegacyAnalyzer={analyzer}"));
        if (generator == "Next" || analyzer != null)
            arguments.Add(new MsBuildArgument("/p:LuminPackLegacyGeneratorReference=false"));
        return arguments.ToArray();
    }
}

/// <summary>Runs measured processes under the repository's prepared experimental Satori runtime.</summary>
internal static class SatoriRuntime
{
    /// <summary>Selects whether measured processes use the stock runtime, the Satori runtime, or both.</summary>
    internal enum RequestMode
    {
        /// <summary>Measure only the stock runtime.</summary>
        Off,

        /// <summary>Measure only the prepared Satori runtime.</summary>
        Only,

        /// <summary>Measure the stock runtime and add separate Satori jobs.</summary>
        Both,
    }

    /// <summary>Reads the requested measured-runtime mode.</summary>
    /// <returns>Off for unset, zero, false or off; Both for both; Only for any other value.</returns>
    internal static RequestMode RequestedMode()
    {
        string? value = Environment.GetEnvironmentVariable(BenchmarkConfig.SatoriVariable);
        if (string.IsNullOrEmpty(value) || value == "0" ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return RequestMode.Off;
        }

        return value.Equals("both", StringComparison.OrdinalIgnoreCase) ? RequestMode.Both : RequestMode.Only;
    }

    /// <summary>Gets whether the caller explicitly requested the Satori runtime.</summary>
    /// <returns>True when the opt-in variable selects the Satori-only or both mode.</returns>
    internal static bool IsRequested() => RequestedMode() != RequestMode.Off;

    /// <summary>Resolves the Satori runtime root that the build prepared for benchmark children.</summary>
    /// <returns>The absolute root of the private host and shared framework.</returns>
    internal static string Resolve()
    {
        string? root = typeof(BenchmarkConfig).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "LuminPack.SatoriRuntime")?.Value;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(Path.Combine(root, "host", "fxr")) ||
            !Directory.Exists(Path.Combine(root, "shared", "Microsoft.NETCore.App")))
        {
            throw new InvalidOperationException(
                $"{BenchmarkConfig.SatoriVariable} requires the prepared Satori runtime. Build the benchmark project with a " +
                ".NET 11 SDK and the default LuminPackUseSatori=true, or unset the variable.");
        }

        return root;
    }

    /// <summary>Points a measured process at the private runtime and permits its net10.0 target to roll forward.</summary>
    /// <param name="job">The job whose measured child processes use the runtime.</param>
    /// <param name="root">The prepared Satori runtime root.</param>
    /// <returns>The job with the runtime environment applied.</returns>
    internal static Job Apply(Job job, string root) => job
        .WithEnvironmentVariable(BenchmarkConfig.SatoriVariable, "1")
        .WithEnvironmentVariable("DOTNET_ROOT", root)
        .WithEnvironmentVariable("DOTNET_ROOT_X64", root)
        .WithEnvironmentVariable("DOTNET_ROOT_ARM64", root)
        .WithEnvironmentVariable("DOTNET_ROOT(x64)", root)
        .WithEnvironmentVariable("DOTNET_ROOT(arm64)", root)
        .WithEnvironmentVariable("DOTNET_ROLL_FORWARD", "Major");

    /// <summary>Fails the measured process when Satori was requested but the stock runtime is active.</summary>
    /// <exception cref="InvalidOperationException">Satori GC is not active.</exception>
    internal static void EnsureSatoriIfRequested()
    {
        if (!IsRequested()) return;
        if (GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? enabled) && enabled is true) return;
        throw new InvalidOperationException(
            $"{BenchmarkConfig.SatoriVariable} requests Satori GC, but this benchmark process runs on the stock runtime " +
            $"({System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; " +
            $"DOTNET_ROOT='{Environment.GetEnvironmentVariable("DOTNET_ROOT")}'; " +
            $"DOTNET_ROOT_X64='{Environment.GetEnvironmentVariable("DOTNET_ROOT_X64")}'; " +
            $"DOTNET_ROLL_FORWARD='{Environment.GetEnvironmentVariable("DOTNET_ROLL_FORWARD")}').");
    }
}

/// <summary>Builds the generated project from the recorded benchmark project path.</summary>
internal sealed class ProjectPathCsProjGenerator : CsProjGenerator
{
    private readonly bool _appHost;

    /// <summary>Creates a generator for the supplied target framework moniker.</summary>
    /// <param name="targetFrameworkMoniker">The target framework of the benchmark project.</param>
    /// <param name="appHost">Whether the measured process runs the native apphost instead of the dll.</param>
    internal ProjectPathCsProjGenerator(string targetFrameworkMoniker, bool appHost)
        : base(targetFrameworkMoniker, null!, null!, null!)
    {
        _appHost = appHost;
    }

    /// <summary>Returns the native apphost extension when the measured process must resolve DOTNET_ROOT itself.</summary>
    /// <returns>The platform apphost extension, or the framework-dependent dll extension.</returns>
    protected override string GetExecutableExtension()
    {
        if (!_appHost) return base.GetExecutableExtension();
        return OperatingSystem.IsWindows() ? ".exe" : string.Empty;
    }

    /// <summary>Uses the project path recorded at build time instead of searching the repository.</summary>
    /// <param name="benchmarkTarget">The benchmark target type.</param>
    /// <param name="logger">The logger used when the recorded path is unavailable.</param>
    /// <returns>The benchmark project file.</returns>
    protected override FileInfo GetProjectFilePath(Type benchmarkTarget, ILogger logger)
    {
        string? path = benchmarkTarget.Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "LuminPack.ProjectPath")?.Value;
        return !string.IsNullOrEmpty(path) && File.Exists(path)
            ? new FileInfo(path)
            : base.GetProjectFilePath(benchmarkTarget, logger);
    }
}
