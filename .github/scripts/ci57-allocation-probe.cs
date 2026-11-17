using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Ci57AllocationProbe
{
    /// <summary>Compares fully overwritten arrays and records elapsed time, allocated bytes and collections.</summary>
    private static void Main()
    {
        Console.WriteLine(JsonSerializer.Serialize(new { Kind = "Environment", RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.FrameworkDescription, Environment.ProcessorCount, GCSettings.IsServerGC,
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") }));
        foreach (int kib in new[] { 2, 4, 8, 16, 32, 64, 128 })
        {
            int bytes = kib * 1024;
            byte[] source = new byte[bytes];
            new Random(73).NextBytes(source);
            int calls = 16 * 1024 * 1024 / bytes;
            for (int round = -1; round < 9; round++)
            for (int variant = 0; variant < 2; variant++)
            {
                bool zeroed = ((round + variant) & 1) == 0;
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < calls; i++)
                {
                    byte[] target = zeroed ? new byte[bytes] : GC.AllocateUninitializedArray<byte>(bytes);
                    source.AsSpan().CopyTo(target);
                    GC.KeepAlive(target);
                }
                double ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds / calls;
                double perOp = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)calls;
                if (round >= 0)
                    Console.WriteLine(JsonSerializer.Serialize(new { Kind = "Allocation", KiB = kib, Zeroed = zeroed,
                        Round = round, Ns = ns, BytesPerOp = perOp, Gen0 = GC.CollectionCount(0) - g0,
                        Gen1 = GC.CollectionCount(1) - g1, Gen2 = GC.CollectionCount(2) - g2 }));
            }
        }
    }
}
