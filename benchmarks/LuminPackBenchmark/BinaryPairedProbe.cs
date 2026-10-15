using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LuminPackBenchmark;

// An exploratory, alternating-library probe. Final claims are also checked with
// BenchmarkDotNet. Keep all raw samples, including GC and scheduling outliers.
/// <summary>Provides binary paired probe functionality.</summary>
internal static class BinaryPairedProbe
{
    private static object? _sink;

    /// <summary>Measures run for the configured benchmark input.</summary>
    /// <param name="scope">The scope used by this operation.</param>
    internal static void Run(string scope)
    {
        if (OperatingSystem.IsWindows())
            Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        var cases = new List<(string Name, Func<object> Lp, Func<object> Mp)>();
        if (scope is "graph" or "all")
        {
            var graph = new CharacterBinaryRatioBenchmark();
            graph.Setup();
            cases.Add(("GraphWrite", graph.LuminPackWrite, graph.MemoryPackWrite));
            cases.Add(("GraphRead", graph.LuminPackRead, graph.MemoryPackRead));
        }
        if (scope != "graph")
        {
            foreach (string shape in scope == "all" ? new[] { "Short", "Medium", "Long", "Unicode", "Utf16" } : new[] { scope })
            {
                var strings = new BinaryStringRatioBenchmark { Shape = shape };
                strings.Setup();
                cases.Add((shape + "Write", strings.LuminPackWrite, strings.MemoryPackWrite));
                cases.Add((shape + "Read", strings.LuminPackRead, strings.MemoryPackRead));
            }
        }
        Console.WriteLine("Case,Sample,First,LP_ns,MP_ns,Ratio,LP_bytes,MP_bytes");
        foreach (var test in cases)
        {
            // Warm both methods for elapsed time, allowing background Tier1/PGO.
            var warmup = Stopwatch.StartNew();
            do { Measure(test.Lp, 4096); Measure(test.Mp, 4096); } while (warmup.Elapsed.TotalSeconds < 5);
            for (int pass = 0; pass < 16; pass++)
            {
                (double Ns, double Bytes) lp, mp;
                if ((pass & 1) == 0) { lp = Measure(test.Lp, 16384); mp = Measure(test.Mp, 16384); }
                else { mp = Measure(test.Mp, 16384); lp = Measure(test.Lp, 16384); }
                Console.WriteLine(FormattableString.Invariant($"{test.Name},{pass},{((pass & 1) == 0 ? "LP" : "MP")},{lp.Ns:F3},{mp.Ns:F3},{lp.Ns / mp.Ns:F6},{lp.Bytes:F1},{mp.Bytes:F1}"));
            }
        }
        GC.KeepAlive(_sink);
    }

    /// <summary>Measures measure for the configured benchmark input.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (double Ns, double Bytes) Measure(Func<object> action, int count)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++) _sink = action();
        long elapsed = Stopwatch.GetTimestamp() - start;
        return (elapsed * (1_000_000_000.0 / Stopwatch.Frequency) / count,
            (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)count);
    }
}
