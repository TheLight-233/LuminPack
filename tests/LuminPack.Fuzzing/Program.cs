using SharpFuzz;

namespace Fuzzing;

/// <summary>Provides corpus generation, deterministic smoke testing, replay and libFuzzer entry points.</summary>
internal static class Program
{
    /// <summary>Dispatches an explicitly selected fuzzing operation.</summary>
    /// <param name="args">The operation and its arguments.</param>
    /// <returns>Zero on success, one on failure, or two for invalid usage.</returns>
    private static int Main(string[] args)
    {
        // The native bridge supports only one process argument, used by dotnet for the assembly path.
        if (args.Length == 0 && Environment.GetEnvironmentVariable("LUMINPACK_FUZZ_TARGET") is string selected)
            args = ["--libfuzzer", selected];
        if (args.Length == 2 && args[0] == "--libfuzzer")
        {
            var target = Find(args[1]);
            Fuzzer.LibFuzzer.Run(input => target.Run(input));
            return 0;
        }
        try
        {
            switch (args)
            {
                case ["--seed", var directory]:
                    Corpus.Write(directory);
                    Console.WriteLine($"Wrote and verified seeds for {Targets.All.Length} targets in {Path.GetFullPath(directory)}.");
                    return 0;
                case ["--replay", var name, var path]:
                    Replay(Find(name), path);
                    return 0;
                case ["--smoke", var corpus, var output]:
                    Smoke(corpus, output, 1000, 233);
                    return 0;
                case ["--smoke", var corpus, var output, var iterations, var seed]:
                    Smoke(corpus, output, int.Parse(iterations), int.Parse(seed));
                    return 0;
                default:
                    Console.Error.WriteLine("Usage: --seed <corpus> | --replay <target> <file-or-directory> | --smoke <corpus> <output> [iterations seed] | --libfuzzer <target>");
                    Console.Error.WriteLine("Targets: " + string.Join(", ", Targets.All.Select(target => target.Name)));
                    return 2;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>Resolves a target name before processing any input.</summary>
    /// <param name="name">The exact target name.</param>
    /// <returns>The requested target.</returns>
    private static IFuzzTarget Find(string name) => Targets.All.SingleOrDefault(target => target.Name == name)
        ?? throw new ArgumentException($"Unknown fuzz target: {name}");

    /// <summary>Replays a file or a corpus directory, preserving unexpected failures as a nonzero exit.</summary>
    /// <param name="target">The parser and invariants.</param>
    /// <param name="path">A file or directory containing raw encoded inputs.</param>
    private static void Replay(IFuzzTarget target, string path)
    {
        string[] files = Directory.Exists(path) ? Directory.GetFiles(path).Order(StringComparer.Ordinal).ToArray() : [path];
        FuzzTarget.Require(files.Length != 0, "The replay corpus is empty.");
        foreach (string file in files)
        {
            Console.WriteLine($"Replay {target.Name}: {file}");
            FuzzTarget.Require(new FileInfo(file).Length <= FuzzTarget.MaxInputBytes, "The replay input exceeds the harness size cap.");
            Console.WriteLine(target.Run(File.ReadAllBytes(file)) ? "Accepted; invariants passed." : "Rejected malformed input.");
        }
    }

    /// <summary>Runs reproducible seed replay, all seed truncations and a fixed mutation budget per target.</summary>
    /// <param name="corpus">The root with a directory per target.</param>
    /// <param name="output">The directory receiving the current input and reproduction metadata.</param>
    /// <param name="iterations">The mutation count per target.</param>
    /// <param name="seed">The deterministic random seed.</param>
    private static void Smoke(string corpus, string output, int iterations, int seed)
    {
        FuzzTarget.Require(iterations > 0, "The mutation count must be positive.");
        VerifyExceptionFilter();
        Directory.CreateDirectory(output);
        foreach (var target in Targets.All)
        {
            // Positive seeds must reach the complete parser and invariant path on every smoke run.
            foreach (var _ in target.Seeds()) { }
            string[] files = Directory.GetFiles(Path.Combine(corpus, target.Name)).Order(StringComparer.Ordinal).ToArray();
            FuzzTarget.Require(files.Length != 0, $"The {target.Name} corpus is empty.");
            var inputs = files.Select(file =>
            {
                FuzzTarget.Require(new FileInfo(file).Length <= FuzzTarget.MaxInputBytes, "A corpus input exceeds the size cap.");
                return File.ReadAllBytes(file);
            }).ToArray();
            int count = 0, accepted = 0;
            foreach (byte[] bytes in inputs)
                for (int length = 0; length <= bytes.Length; length++)
                    RunSaved(bytes.AsSpan(0, length), $"prefix {length}/{bytes.Length}");
            var random = new Random(seed);
            for (int iteration = 0; iteration < iterations; iteration++)
                RunSaved(Corpus.Mutate(inputs[random.Next(inputs.Length)], random), $"mutation {iteration}");
            Console.WriteLine($"{target.Name}: {count} inputs, {accepted} accepted, {count - accepted} rejected; seed={seed}, mutations={iterations}.");

            /// <summary>Persists the current input before execution so fatal process failures remain replayable.</summary>
            /// <param name="input">The exact bytes to run.</param>
            /// <param name="description">The deterministic case description.</param>
            void RunSaved(ReadOnlySpan<byte> input, string description)
            {
                string path = Path.Combine(output, target.Name + "-last-input.bin");
                File.WriteAllBytes(path, input.ToArray());
                File.WriteAllText(Path.Combine(output, target.Name + "-last-input.txt"),
                    $"target={target.Name}\nseed={seed}\ncase={description}\nreplay: dotnet run --project tests/LuminPack.Fuzzing -c Release -- --replay {target.Name} \"{Path.GetFullPath(path)}\"\n");
                if (target.Run(input)) accepted++;
                count++;
            }
        }
    }

    /// <summary>Ensures common programmer, capability and resource failures cannot be classified as malformed input.</summary>
    private static void VerifyExceptionFilter()
    {
        foreach (Exception exception in new Exception[]
        {
            new IndexOutOfRangeException(), new ArgumentOutOfRangeException(), new NullReferenceException(),
            new OutOfMemoryException(), new NotSupportedException(), new InvalidOperationException("Invariant failure")
        })
            FuzzTarget.Require(!FuzzTarget.IsRejection(exception, json: false) && !FuzzTarget.IsRejection(exception, json: true),
                $"The exception filter hides {exception.GetType().Name}.");
    }
}
