using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    private sealed record Operation(string Name, Action Invoke);
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
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            Reference = json ? "System.Text.Json" : "MemoryPack",
            ReferenceVersion = (json ? typeof(JsonSerializer) : typeof(MemoryPack.MemoryPackSerializer))
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        });

        bool failed = false;
        var random = new Random(1729);
        foreach (var fixture in fixtures)
        {
            var work = new List<(string Library, Operation Operation, int Count)>();
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

                    void Add(string name, Action action) => work.Add((codec.Library, new(name, action), Calibrate(action)));
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
            foreach (var group in work.GroupBy(item => item.Operation.Name))
            {
                var operations = group.ToArray();
                for (int round = 0; round < rounds; round++)
                {
                    random.Shuffle(operations.AsSpan());
                    foreach (var entry in operations)
                    {
                        long allocated = GC.GetAllocatedBytesForCurrentThread();
                        double elapsed = Measure(entry.Operation.Invoke, entry.Count);
                        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                        measurements.Add(new(entry.Operation.Name, entry.Library, round, elapsed, (double)bytes / entry.Count));
                    }
                }
            }

            foreach (var sample in measurements)
                Emit(new { Kind = "Sample", Case = fixture.Name, sample.Operation, sample.Library, sample.Round, sample.Nanoseconds, sample.AllocatedBytes });
        }

        return failed ? 1 : 0;
    }

    /// <summary>Chooses a repetition count for an approximately five-millisecond sample.</summary>
    /// <param name="action">The benchmark operation to calibrate.</param>
    /// <returns>The number of calls per timing sample.</returns>
    private static int Calibrate(Action action)
    {
        int count = 16;
        while (count < 1_048_576)
        {
            double nanoseconds = Measure(action, count);
            if (nanoseconds * count >= 3_000_000)
                return Math.Clamp((int)(5_000_000 / nanoseconds), 16, 1_048_576);
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
