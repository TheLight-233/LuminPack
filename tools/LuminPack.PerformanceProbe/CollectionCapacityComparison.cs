using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

/// <summary>Compares retained collection safety implementations with unchanged benchmark fixtures and references.</summary>
/// <remarks>All providers share the same delegate boundary, runtime, GC settings, and measurement job.</remarks>
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 3, warmupCount: 5, iterationCount: 8), IterationTime(500), GcServer(false), MemoryDiagnoser]
public class CollectionCapacityComparison
{
    private Action _operation = null!;
    private Action _cleanup = null!;
    private object? _sink;

    /// <summary>Gets the supported combinations, excluding the known-unsafe original polymorphism writer.</summary>
    public IEnumerable<string> Cases
    {
        get
        {
            foreach (string fixture in new[] { "SimpleWrite", "CharacterWrite", "CharacterRead", "PolyWrite", "PolyRead" })
                foreach (string provider in new[] { "Original", "Checked", "Candidate", "MemoryPack", "MessagePack" })
                    if (provider != "Original" || !fixture.StartsWith("Poly", StringComparison.Ordinal)) yield return fixture + ":" + provider;
        }
    }

    /// <summary>Gets or sets the unchanged fixture operation and retained provider.</summary>
    [ParamsSource(nameof(Cases))]
    public string Case { get; set; } = "SimpleWrite:Candidate";

    /// <summary>Binds the original fixture outside the measured operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        string[] parts = Case.Split(':');
        string provider = parts[1];
        string root = Environment.GetEnvironmentVariable("LUMINPACK_CAPACITY_IMAGES")!;
        string image = provider switch { "Original" => "original", "Checked" => "safe", _ => "candidate" };
        string fixture = parts[0].StartsWith("Simple", StringComparison.Ordinal) ? "SimpleBenchmark" :
            parts[0].StartsWith("Character", StringComparison.Ordinal) ? "SerializeBenchmark" : "PolymorphismBenchmark";
        string library = provider is "MemoryPack" or "MessagePack" ? provider : "LuminPack";
        string operation = library + (parts[0].EndsWith("Write", StringComparison.Ordinal) ? "Serialize" : "Deserialize");
        var type = BenchmarkImage.Load(Path.Combine(root, image)).GetType("LuminPackBenchmark." + fixture, true)!;
        object instance = Activator.CreateInstance(type)!;
        type.GetMethod("SetUp")!.Invoke(instance, null);
        _cleanup = type.GetMethod("CleanUp")!.CreateDelegate<Action>(instance);
        var method = type.GetMethod(operation)!;
        if (method.ReturnType == typeof(void)) _operation = method.CreateDelegate<Action>(instance);
        else
        {
            var read = method.CreateDelegate<Func<object>>(instance);
            _operation = () => _sink = read();
        }
    }

    /// <summary>Invokes exactly one unchanged fixture operation.</summary>
    [Benchmark]
    public void Invoke() => _operation();

    /// <summary>Releases the fixture's existing native writer and retains read results through measurement.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _cleanup();
        GC.KeepAlive(_sink);
    }
}
