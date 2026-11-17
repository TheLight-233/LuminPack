using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using LuminPack.Code;

internal static unsafe class Ci57CopyProbe
{
    private const int CopyBytes = 16 * 1024;
    private const int Alignment = 64;
    private const int CallsPerRound = 40_000;
    private const int Rounds = 9;

    private static void Main()
    {
        bool avx512f = Avx512F.IsSupported;
        bool avx512bw = Avx512BW.IsSupported;
        Emit(new
        {
            Kind = "Environment", Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Runtime = RuntimeInformation.FrameworkDescription, Processors = Environment.ProcessorCount,
            ServerGC = GCSettings.IsServerGC, TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            Avx512F = avx512f, Avx512BW = avx512bw, CopyBytes, Alignment, CallsPerRound, Rounds
        });
        if (!avx512f || !avx512bw)
        {
            Emit(new { Kind = "Skipped", Reason = "CoreCLR does not expose both AVX-512F and AVX-512BW." });
            return;
        }
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
            Environment.ProcessorCount != 4 || !GCSettings.IsServerGC ||
            Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") != "0")
            throw new InvalidOperationException("The probe requires Linux x64, four processors and FullOpts with Server GC.");

        byte* source = null;
        byte* destination = null;
        try
        {
            source = (byte*)NativeMemory.AlignedAlloc(CopyBytes + Alignment, Alignment);
            destination = (byte*)NativeMemory.AlignedAlloc(CopyBytes, Alignment);
            if ((((nuint)source | (nuint)destination) & (Alignment - 1)) != 0)
                throw new InvalidOperationException("The copy buffers are not cache-line aligned.");
            uint pattern = 1;
            for (int index = 0; index < CopyBytes + Alignment; index++)
            {
                pattern = unchecked(pattern * 1664525 + 1013904223);
                source[index] = (byte)(pattern >> 24);
            }
            CheckCopy(destination, source, 13);
            CheckCopy(destination, source, 16);
            Measure(destination, source, 13);
            Measure(destination, source, 16);
            for (int round = 0; round < Rounds; round++)
            {
                bool first13 = (round & 1) == 0;
                double first = Measure(destination, source, first13 ? 13 : 16);
                double second = Measure(destination, source, first13 ? 16 : 13);
                double offset13 = first13 ? first : second;
                double offset16 = first13 ? second : first;
                Emit(new { Kind = "Sample", Round = round, FirstOffset = first13 ? 13 : 16,
                    Offset13Nanoseconds = offset13, Offset16Nanoseconds = offset16, Offset13Over16 = offset13 / offset16 });
            }
            CheckCopy(destination, source, 13);
            CheckCopy(destination, source, 16);
        }
        finally
        {
            NativeMemory.AlignedFree(destination);
            NativeMemory.AlignedFree(source);
        }
    }

    private static void CheckCopy(byte* destination, byte* source, int offset)
    {
        LuminPackMarshal.CopyBlockUnaligned(ref destination[0], ref source[offset], CopyBytes);
        if (!new ReadOnlySpan<byte>(destination, CopyBytes).SequenceEqual(new ReadOnlySpan<byte>(source + offset, CopyBytes)))
            throw new InvalidOperationException($"Incorrect copy at source offset {offset}.");
    }

    private static double Measure(byte* destination, byte* source, int offset)
    {
        long start = Stopwatch.GetTimestamp();
        for (int call = 0; call < CallsPerRound; call++)
            LuminPackMarshal.CopyBlockUnaligned(ref destination[0], ref source[offset], CopyBytes);
        return Stopwatch.GetElapsedTime(start).TotalNanoseconds / CallsPerRound;
    }

    private static void Emit<T>(T record) => Console.WriteLine(JsonSerializer.Serialize(record));
}
