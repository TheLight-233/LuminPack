using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Loader;
using System.Text.Json;

if (args.Length != 4)
    throw new ArgumentException("Usage: <baseline-image> <pre-profile-image> <candidate-image> <results.json>");

Type[] fixtures = args.Take(3).Select(directory =>
{
    var context = new ImageContext(Path.GetFullPath(directory));
    return context.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(directory, "LuminPack.StringPerformanceProbe.Fixture.dll"))).GetType("StringFixture", true)!;
}).ToArray();
var results = new List<object>();
string[] shapes = ["ASCII", "Latin1", "MixedLatin1", "AsciiLatin1", "Latin2", "CJK", "Emoji", "Mixed"];
Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; AVX512BW={Avx512BW.IsSupported}; AVX2={Avx2.IsSupported}; SSSE3={Ssse3.IsSupported}");
if (Environment.GetEnvironmentVariable("LUMINPACK_STRING_CODEGEN") == "1")
{
    foreach (Type fixture in fixtures)
        foreach (string shape in shapes)
        {
            fixture.GetMethod("Setup")!.Invoke(null, [shape, 4096]);
            foreach (string method in new[] { "LegacyEncode", "RuntimeModeEncode", "LegacyDecode", "ProfileEncode", "ProfileDecode", "ProfileUtf16Encode", "ProfileUtf16Decode" })
                fixture.GetMethod(method)?.Invoke(null, null);
        }
    return;
}
foreach (string shape in shapes)
    foreach (int length in new[] { 8, 32, 256, 4096 })
    {
        foreach (Type fixture in fixtures) fixture.GetMethod("Setup")!.Invoke(null, [shape, length]);
        // Equal contents alone do not control vector load/store alignment. Share the
        // exact same framework objects across images to avoid measuring GC layout.
        foreach (string field in new[] { "_text", "_encoded", "_utf16", "_destination", "_decoded" })
        {
            object? buffer = fixtures[0].GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            foreach (Type fixture in fixtures.Skip(1)) fixture.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, buffer);
        }
        var cases = new (string Name, Func<int> Action)[]
        {
            ("baseline.encode", Bind(fixtures[0], "LegacyEncode")),
            ("preprofile.encode", Bind(fixtures[1], "LegacyEncode")),
            ("candidate.encode", Bind(fixtures[2], "LegacyEncode")),
            ("profile.encode", Bind(fixtures[2], "ProfileEncode")),
            ("mode.encode", Bind(fixtures[2], "RuntimeModeEncode")),
            ("bcl.encode", Bind(fixtures[2], "BclEncode")),
            ("baseline.decode", Bind(fixtures[0], "LegacyDecode")),
            ("candidate.decode", Bind(fixtures[2], "LegacyDecode")),
            ("profile.decode", Bind(fixtures[2], "ProfileDecode")),
            ("bcl.decode", Bind(fixtures[2], "BclDecode")),
            ("baseline.utf16", Bind(fixtures[0], "LegacyUtf16Encode")),
            ("candidate.utf16", Bind(fixtures[2], "LegacyUtf16Encode")),
            ("profile.utf16", Bind(fixtures[2], "ProfileUtf16Encode"))
        };
        int[] iterations = new int[cases.Length];
        for (int i = 0; i < cases.Length; i++)
        {
            // Calibrate outside measurement; each retained image gets its own warmup.
            int count = 256;
            double elapsed;
            do { elapsed = Measure(cases[i].Action, count, out _); if (elapsed < 2) count *= 2; } while (elapsed < 2);
            iterations[i] = Math.Max(256, (int)(count * 6.0 / elapsed));
        }
        double[][] samples = cases.Select(_ => new double[9]).ToArray();
        double[] allocations = new double[cases.Length];
        for (int sample = 0; sample < 9; sample++)
            for (int position = 0; position < cases.Length; position++)
            {
                int i = (position + sample * 5) % cases.Length;
                double milliseconds = Measure(cases[i].Action, iterations[i], out long bytes);
                samples[i][sample] = milliseconds * 1_000_000 / iterations[i];
                allocations[i] = Math.Max(allocations[i], (double)bytes / iterations[i]);
            }
        for (int i = 0; i < cases.Length; i++)
        {
            int reference = i < 6 ? 5 : i < 10 ? 9 : 10;
            double[] ratios = samples[i].Zip(samples[reference], (value, baseline) => value / baseline).Order().ToArray();
            double[] ordered = samples[i].Order().ToArray();
            results.Add(new { Shape = shape, Length = length, Case = cases[i].Name, MedianNs = ordered[4], IqrNs = ordered[6] - ordered[2],
                Reference = cases[reference].Name, MedianRatio = ratios[4], RatioIqr = ratios[6] - ratios[2], AllocatedBytes = allocations[i], SamplesNs = samples[i] });
        }
        Console.WriteLine($"{shape,-12} {length,4}: encode profile/baseline={MedianRatio(samples[3], samples[0]):F3}, profile/BCL={MedianRatio(samples[3], samples[5]):F3}");
    }
File.WriteAllText(args[3], JsonSerializer.Serialize(new { Runtime = RuntimeInformation.FrameworkDescription, Avx512BW = Avx512BW.IsSupported,
    Avx2 = Avx2.IsSupported, Ssse3 = Ssse3.IsSupported, Stopwatch.Frequency, Samples = 9, Results = results }, new JsonSerializerOptions { WriteIndented = true }));

// Reflection and input setup never enter the timed operation.
static Func<int> Bind(Type fixture, string name) => fixture.GetMethod(name)!.CreateDelegate<Func<int>>();

// Observe the result while keeping all buffers and delegate allocations outside the interval.
static double Measure(Func<int> action, int iterations, out long allocated)
{
    long initialAllocation = GC.GetAllocatedBytesForCurrentThread();
    long start = Stopwatch.GetTimestamp();
    int result = 0;
    for (int i = 0; i < iterations; i++) result ^= action();
    long end = Stopwatch.GetTimestamp();
    allocated = GC.GetAllocatedBytesForCurrentThread() - initialAllocation;
    GC.KeepAlive(result);
    return (end - start) * 1000.0 / Stopwatch.Frequency;
}

// Pair samples from the same rotation to retain sensitivity to changing machine load.
static double MedianRatio(double[] values, double[] references) => values.Zip(references, (value, reference) => value / reference).Order().ElementAt(values.Length / 2);

/// <summary>Resolves each retained runtime image independently without timed reflection.</summary>
internal sealed class ImageContext(string directory) : AssemblyLoadContext(isCollectible: false)
{
    /// <summary>Loads runtime dependencies from the same retained image.</summary>
    /// <param name="assemblyName">The requested assembly identity.</param>
    /// <returns>The matching image-local assembly, or the shared framework fallback.</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string path = Path.Combine(directory, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
