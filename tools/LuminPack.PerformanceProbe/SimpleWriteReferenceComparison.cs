using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

/// <summary>Compares unchanged Simple writes from retained images against common reference operations.</summary>
/// <remarks>Every timed operation uses the same delegate boundary. Absolute times supplement the original benchmark.</remarks>
[ShortRunJob(RuntimeMoniker.Net10_0), MemoryDiagnoser]
public class SimpleWriteReferenceComparison
{
    private Action _baseline = null!, _candidate = null!, _memoryPack = null!, _nino = null!;
    private readonly List<Action> _cleanup = [];

    /// <summary>Loads both retained fixtures without rebuilding their serialization implementations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var baseline = Load(Environment.GetEnvironmentVariable("LUMINPACK_PERF_BASELINE")!);
        var candidate = Load(Environment.GetEnvironmentVariable("LUMINPACK_PERF_CANDIDATE")!);
        _baseline = Bind(baseline, "LuminPackSerialize");
        _candidate = Bind(candidate, "LuminPackSerialize");
        _memoryPack = Bind(baseline, "MemoryPackSerialize");
        _nino = Bind(baseline, "NinoSerialize");
    }

    /// <summary>Runs the original baseline LuminPack operation.</summary>
    [Benchmark(Baseline = true)]
    public void Baseline() => _baseline();

    /// <summary>Runs the candidate LuminPack operation on the original fixture shape.</summary>
    [Benchmark]
    public void Candidate() => _candidate();

    /// <summary>Runs the unchanged MemoryPack operation with its original destination reset.</summary>
    [Benchmark]
    public void MemoryPack() => _memoryPack();

    /// <summary>Runs the unchanged Nino operation with its original destination reset.</summary>
    [Benchmark]
    public void Nino() => _nino();

    /// <summary>Releases both original fixture buffers after the measurement.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var action in _cleanup) action();
    }

    /// <summary>Creates an original Simple fixture in its retained-image context.</summary>
    /// <param name="directory">The complete retained output directory.</param>
    /// <returns>The initialized original benchmark fixture.</returns>
    private object Load(string directory)
    {
        var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark.SimpleBenchmark", throwOnError: true)!;
        var fixture = Activator.CreateInstance(type)!;
        type.GetMethod("SetUp")!.Invoke(fixture, null);
        _cleanup.Add(Bind(fixture, "CleanUp"));
        return fixture;
    }

    /// <summary>Binds a benchmark method without reflection inside the timed loop.</summary>
    /// <param name="fixture">The initialized original fixture.</param>
    /// <param name="name">The unchanged operation name.</param>
    /// <returns>The common zero-argument operation delegate.</returns>
    private static Action Bind(object fixture, string name) => fixture.GetType().GetMethod(name)!.CreateDelegate<Action>(fixture);
}
