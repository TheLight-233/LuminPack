using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

/// <summary>Compares unchanged fixture operations from two independently retained build images.</summary>
/// <remarks>The timed delegate boundary is identical for both images. Use the original benchmark for absolute API timings.</remarks>
[ShortRunJob(RuntimeMoniker.Net10_0), MemoryDiagnoser]
public class SerializerComparison
{
    private Action _baseline = null!;
    private Action _candidate = null!;
    private readonly List<Action> _cleanup = [];
    private object? _sink;

    /// <summary>Gets or sets the original fixture operation to run.</summary>
    [Params("SimpleWrite", "SimpleRead", "CharacterWrite", "CharacterRead", "CharacterBufferWrite", "CharacterBufferRead")]
    public string Case { get; set; } = "SimpleWrite";

    /// <summary>Loads both implementations before timing, without rebuilding the retained baseline.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _baseline = Load(Environment.GetEnvironmentVariable("LUMINPACK_PERF_BASELINE")!);
        _candidate = Load(Environment.GetEnvironmentVariable("LUMINPACK_PERF_CANDIDATE")!);
    }

    /// <summary>Runs the retained baseline operation.</summary>
    [Benchmark(Baseline = true)]
    public void Baseline() => _baseline();

    /// <summary>Runs the current implementation against the same fixture shape.</summary>
    [Benchmark]
    public void Candidate() => _candidate();

    /// <summary>Releases fixture buffers and retains returned objects through the last measurement.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var action in _cleanup) action();
        GC.KeepAlive(_sink);
    }

    /// <summary>Binds the original fixture method without reflection in the measured loop.</summary>
    /// <param name="directory">A complete benchmark output directory, including dependency assemblies.</param>
    /// <returns>The original operation, or a wrapper retaining its returned object.</returns>
    private Action Load(string directory)
    {
        var assembly = BenchmarkImage.Load(directory);
        var (typeName, methodName, setupName, cleanupName) = Case switch
        {
            "SimpleWrite" => ("SimpleBenchmark", "LuminPackSerialize", "SetUp", "CleanUp"),
            "SimpleRead" => ("SimpleBenchmark", "LuminPackDeserialize", "SetUp", "CleanUp"),
            "CharacterWrite" => ("CharacterBinaryRatioBenchmark", "LuminPackWrite", "Setup", ""),
            "CharacterRead" => ("CharacterBinaryRatioBenchmark", "LuminPackRead", "Setup", ""),
            "CharacterBufferWrite" => ("SerializeBenchmark", "LuminPackSerialize", "SetUp", "CleanUp"),
            "CharacterBufferRead" => ("SerializeBenchmark", "LuminPackDeserialize", "SetUp", "CleanUp"),
            _ => throw new InvalidOperationException("Unknown fixture: " + Case)
        };
        var type = assembly.GetType("LuminPackBenchmark." + typeName, throwOnError: true)!;
        var instance = Activator.CreateInstance(type)!;
        type.GetMethod(setupName)!.Invoke(instance, null);
        if (cleanupName != "") _cleanup.Add(type.GetMethod(cleanupName)!.CreateDelegate<Action>(instance));
        var method = type.GetMethod(methodName)!;
        if (method.ReturnType == typeof(void)) return method.CreateDelegate<Action>(instance);
        var operation = method.CreateDelegate<Func<object>>(instance);
        return () => _sink = operation();
    }
}
