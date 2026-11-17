using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    private const int WarmupMinCalls = 256;
    private const int TieredWarmupMilliseconds = 300;
    private const int FullOptsWarmupMilliseconds = 15;

    private sealed record Operation(string Library, string Name, Action Invoke);

    private sealed class WarmupState(Operation operation, int batchSize)
    {
        internal readonly Operation Operation = operation;
        internal readonly int BatchSize = batchSize;
        internal long Calls;
        internal double Milliseconds;
    }
    private sealed record Measurement(string Operation, string Library, int Round, double Nanoseconds, double AllocatedBytes);

    /// <summary>Runs the command-line program with the supplied arguments.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>Zero on success, or a nonzero exit code on failure.</returns>
    private static int Main(string[] args)
    {
        if (args.Length != 4 || args[0] is not ("binary" or "json") ||
            args[1] is not ("main" or "small" or "regular" or "large" or "changing") ||
            !int.TryParse(args[2], out int rounds) || rounds < 0)
        {
            Console.Error.WriteLine("Usage: binary|json main|small|regular|large|changing rounds output.jsonl");
            Console.Error.WriteLine("Use 0 rounds for round-trip validation only.");
            return 2;
        }

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        int concreteCalls = EntryAudit.Check();
        bool json = args[0] == "json";
        string? tiering = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation");
        int warmupMilliseconds = tiering == "0" ? FullOptsWarmupMilliseconds : TieredWarmupMilliseconds;
        string scene = args[1];
        Config.Count = scene switch { "small" => 8, "large" => 4096, _ => 256 };
        var fixtures = scene == "main"
            ? Fixtures.MainCases().Where(fixture => fixture.Name != (json ? "SerializeBenchmark" : "JsonBenchmark")).ToArray()
            : Fixtures.CollectionCases();
        using var output = new StreamWriter(args[3]) { AutoFlush = true };
        void Emit(object row) => output.WriteLine(JsonSerializer.Serialize(row));
        Emit(new
        {
            Kind = "Environment",
            ConcreteCalls = concreteCalls,
            Format = args[0],
            Scene = scene,
            Runtime = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OS = RuntimeInformation.OSDescription,
            ServerGC = GCSettings.IsServerGC,
            ProcessorCount = Environment.ProcessorCount,
            Rounds = rounds,
            TieredCompilation = tiering,
            TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
            QuickJit = Environment.GetEnvironmentVariable("DOTNET_TC_QuickJit"),
            QuickJitForLoops = Environment.GetEnvironmentVariable("DOTNET_TC_QuickJitForLoops"),
            CallCountingDelayMilliseconds = Environment.GetEnvironmentVariable("DOTNET_TC_CallCountingDelayMs"),
            WarmupStrategy = rounds == 0 ? null : "interleaved-active-time",
            WarmupMinCalls = rounds == 0 ? 0 : WarmupMinCalls,
            WarmupMinMilliseconds = rounds == 0 ? 0 : warmupMilliseconds,
            References = json
                ? new Dictionary<string, string?> { ["System.Text.Json"] = VersionOf(typeof(JsonSerializer)) }
                : new Dictionary<string, string?>
                {
                    ["MemoryPack"] = VersionOf(typeof(MemoryPack.MemoryPackSerializer)),
                    ["Nino"] = VersionOf(typeof(Nino.Core.NinoSerializer))
                }
        });

        bool failed = false;
        var random = new Random(1729);
        foreach (var fixture in fixtures)
        {
            var work = new List<Operation>();
            foreach (Codec codec in fixture.Prepare(json, scene == "changing"))
            {
                try
                {
                    int bytes = codec.Validate();
                    Emit(new { Kind = "Validated", Case = fixture.Name, codec.Library, Bytes = bytes });
                    if (!codec.SupportsReuse)
                        Emit(new { Kind = "Unavailable", Case = fixture.Name, codec.Library, Operation = "reuse", Reason = codec.ReuseUnavailable });
                    if (rounds == 0)
                        continue;

                    Add("write", codec.Write);
                    Add("fresh", codec.Fresh);
                    if (codec.SupportsReuse)
                        Add("reuse", codec.Reuse);

                    void Add(string name, Action action) => work.Add(new(codec.Library, name, action));
                }
                catch (Exception exception) when (codec.Library == "LuminPack" ||
                    exception is NotSupportedException or InvalidOperationException or JsonException or MemoryPack.MemoryPackSerializationException)
                {
                    failed |= codec.Library == "LuminPack";
                    Emit(new { Kind = "ValidationFailure", Case = fixture.Name, codec.Library, Reason = exception.GetBaseException().Message });
                    if (codec.Library == "LuminPack")
                        Console.Error.WriteLine($"{fixture.Name}: {exception.Message}");
                }
            }

            var measurements = new List<Measurement>();
            foreach (var group in work.GroupBy(item => item.Name))
            {
                var warmup = WarmUp(group, random, warmupMilliseconds);
                foreach (var state in warmup)
                {
                    var operation = state.Operation;
                    Emit(new { Kind = "Warmup", Case = fixture.Name, Operation = operation.Name, operation.Library, state.Calls, state.Milliseconds });
                }

                var operations = warmup.Select(state => (state.Operation, Count: Calibrate(state.Operation.Invoke))).ToArray();
                for (int round = 0; round < rounds; round++)
                {
                    random.Shuffle(operations.AsSpan());
                    foreach (var entry in operations)
                    {
                        long allocated = GC.GetAllocatedBytesForCurrentThread();
                        double elapsed = Measure(entry.Operation.Invoke, entry.Count);
                        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                        measurements.Add(new(entry.Operation.Name, entry.Operation.Library, round, elapsed, (double)bytes / entry.Count));
                    }
                }
            }

            foreach (var sample in measurements)
                Emit(new { Kind = "Sample", Case = fixture.Name, sample.Operation, sample.Library, sample.Round, sample.Nanoseconds, sample.AllocatedBytes });
        }

        return failed ? 1 : 0;
    }

    /// <summary>Reads the library's informational version for the benchmark environment.</summary>
    /// <param name="type">A type from the measured library.</param>
    /// <returns>The recorded version, if available.</returns>
    private static string? VersionOf(Type type)
        => type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>Warms each library on the same operation in shuffled batches before final calibration.</summary>
    /// <param name="operations">The supported library operations in this comparison.</param>
    /// <param name="random">The instance that shuffles execution order.</param>
    /// <param name="minimumMilliseconds">The minimum active time per library, excluding other libraries.</param>
    /// <returns>The warmup observations for the comparison.</returns>
    private static WarmupState[] WarmUp(IEnumerable<Operation> operations, Random random, int minimumMilliseconds)
    {
        var states = operations.Select(operation => new WarmupState(operation, Calibrate(operation.Invoke))).ToArray();
        int remaining = states.Length;
        do
        {
            random.Shuffle(states.AsSpan());
            foreach (var state in states)
            {
                if (state.Calls >= WarmupMinCalls && state.Milliseconds >= minimumMilliseconds)
                    continue;
                state.Milliseconds += Measure(state.Operation.Invoke, state.BatchSize) * state.BatchSize / 1_000_000;
                state.Calls += state.BatchSize;
                if (state.Calls >= WarmupMinCalls && state.Milliseconds >= minimumMilliseconds)
                    remaining--;
            }
        }
        while (remaining != 0);
        return states;
    }

    /// <summary>Chooses a repetition count for an approximately five-millisecond sample.</summary>
    /// <param name="action">The benchmark operation to calibrate.</param>
    /// <returns>The number of calls per timing sample.</returns>
    private static int Calibrate(Action action)
    {
        int count = 1;
        while (count < 1_048_576)
        {
            double nanoseconds = Measure(action, count);
            if (nanoseconds * count >= 3_000_000)
                return Math.Clamp((int)(5_000_000 / nanoseconds), 1, 1_048_576);
            count *= 2;
        }

        return count;
    }

    /// <summary>Times repeated calls to a benchmark operation.</summary>
    /// <param name="action">The benchmark operation under measurement.</param>
    /// <param name="count">The number of calls in the sample.</param>
    /// <returns>Elapsed nanoseconds per call.</returns>
    private static double Measure(Action action, int count)
    {
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++)
            action();
        return Stopwatch.GetElapsedTime(start).TotalNanoseconds / count;
    }
}
