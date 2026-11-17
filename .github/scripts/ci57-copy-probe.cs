using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
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

    /// <summary>Runs the original alignment probe or the rotating managed-array comparison.</summary>
    /// <param name="args">Pass --medium to compare medium-block copy implementations.</param>
    private static void Main(string[] args)
    {
        if (args is ["--medium"])
        {
            MeasureMediumCopies();
            return;
        }
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

    /// <summary>Measures managed copies at three source offsets and verifies both guard bytes.</summary>
    private static void MeasureMediumCopies()
    {
        bool wide = Vector512.IsHardwareAccelerated && Avx512F.IsSupported;
        Emit(new { Kind = "Environment", RuntimeInformation.ProcessArchitecture, RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount, GCSettings.IsServerGC, Vector256.IsHardwareAccelerated, Avx512 = wide,
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") });
        foreach (int length in new[] { 257, 512, 1024, 2048, 4096, 8192, 16384, 32768 })
        {
            byte[] source = new byte[length + 32];
            new Random(17).NextBytes(source);
            byte[][] targets = new byte[8][];
            for (int i = 0; i < targets.Length; i++) targets[i] = new byte[length + 2];
            foreach (int offset in new[] { 4, 5, 13 })
            {
                int variants = wide ? 4 : 3;
                for (int round = -1; round < Rounds; round++)
                for (int i = 0; i < variants; i++)
                {
                    int method = (i + round + variants) % variants;
                    foreach (var target in targets) Array.Fill(target, (byte)0xa5);
                    int calls = Math.Max(2048, 16 * 1024 * 1024 / length);
                    double ns = method switch
                    {
                        0 => Sample<LuminCopy>(source, offset, targets, length, calls),
                        1 => Sample<RuntimeCopy>(source, offset, targets, length, calls),
                        2 => Sample<SpanCopy>(source, offset, targets, length, calls),
                        _ => Sample<WideCopy>(source, offset, targets, length, calls)
                    };
                    foreach (var target in targets)
                        if (target[0] != 0xa5 || target[^1] != 0xa5 ||
                            !source.AsSpan(offset, length).SequenceEqual(target.AsSpan(1, length)))
                            throw new InvalidOperationException($"Invalid method {method}, {length} bytes at {offset}.");
                    if (round >= 0) Emit(new { Kind = "Copy", Method = method, Length = length, Offset = offset, Round = round, Ns = ns });
                }
            }
        }
    }

    private interface ICopy
    {
        /// <summary>Copies a bounded nonoverlapping range.</summary>
        /// <param name="target">The first writable byte.</param>
        /// <param name="source">The first readable byte.</param>
        /// <param name="length">The number of bytes to copy.</param>
        static abstract void Copy(ref byte target, ref byte source, int length);
    }

    private readonly struct LuminCopy : ICopy
    {
        /// <summary>Copies with the production kernel.</summary>
        /// <param name="target">The first writable byte.</param>
        /// <param name="source">The first readable byte.</param>
        /// <param name="length">The number of bytes to copy.</param>
        public static void Copy(ref byte target, ref byte source, int length) =>
            LuminPackMarshal.CopyBlockUnaligned(ref target, ref source, (uint)length);
    }

    private readonly struct RuntimeCopy : ICopy
    {
        /// <summary>Copies with the runtime cpblk intrinsic.</summary>
        /// <param name="target">The first writable byte.</param>
        /// <param name="source">The first readable byte.</param>
        /// <param name="length">The number of bytes to copy.</param>
        public static void Copy(ref byte target, ref byte source, int length) =>
            Unsafe.CopyBlockUnaligned(ref target, ref source, (uint)length);
    }

    private readonly struct SpanCopy : ICopy
    {
        /// <summary>Copies with the span implementation.</summary>
        /// <param name="target">The first writable byte.</param>
        /// <param name="source">The first readable byte.</param>
        /// <param name="length">The number of bytes to copy.</param>
        public static void Copy(ref byte target, ref byte source, int length) =>
            MemoryMarshal.CreateReadOnlySpan(ref source, length).CopyTo(MemoryMarshal.CreateSpan(ref target, length));
    }

    private readonly struct WideCopy : ICopy
    {
        /// <summary>Copies with four 512-bit vectors per aligned iteration and a bounded tail.</summary>
        /// <param name="target">The first writable byte.</param>
        /// <param name="source">The first readable byte.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Copy(ref byte target, ref byte source, int length)
        {
            Vector512.LoadUnsafe(ref source).StoreUnsafe(ref target);
            nuint offset = (~(nuint)Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref target) & 63) + 1;
            nuint end = (nuint)length;
            for (; offset + 256 <= end; offset += 256)
            {
                var a = Vector512.LoadUnsafe(ref source, offset);
                var b = Vector512.LoadUnsafe(ref source, offset + 64);
                var c = Vector512.LoadUnsafe(ref source, offset + 128);
                var d = Vector512.LoadUnsafe(ref source, offset + 192);
                a.StoreUnsafe(ref target, offset);
                b.StoreUnsafe(ref target, offset + 64);
                c.StoreUnsafe(ref target, offset + 128);
                d.StoreUnsafe(ref target, offset + 192);
            }
            for (; offset + 64 <= end; offset += 64)
                Vector512.LoadUnsafe(ref source, offset).StoreUnsafe(ref target, offset);
            Vector512.LoadUnsafe(ref source, end - 64).StoreUnsafe(ref target, end - 64);
        }
    }

    /// <summary>Times one implementation while rotating across eight target arrays.</summary>
    /// <typeparam name="T">The copy implementation.</typeparam>
    /// <param name="source">The array containing the input.</param>
    /// <param name="offset">The index of the first source byte.</param>
    /// <param name="targets">The guarded output arrays.</param>
    /// <param name="length">The number of payload bytes.</param>
    /// <param name="calls">The number of timed copies.</param>
    /// <returns>Mean nanoseconds per copy.</returns>
    private static double Sample<T>(byte[] source, int offset, byte[][] targets, int length, int calls) where T : struct, ICopy
    {
        ref byte first = ref source[offset];
        long start = Stopwatch.GetTimestamp();
        for (int call = 0; call < calls; call++)
            T.Copy(ref targets[call & 7][1], ref first, length);
        return Stopwatch.GetElapsedTime(start).TotalNanoseconds / calls;
    }

    /// <summary>Checks the legacy alignment probe against its source bytes.</summary>
    /// <param name="destination">The aligned destination.</param>
    /// <param name="source">The aligned source.</param>
    /// <param name="offset">The source displacement.</param>
    private static void CheckCopy(byte* destination, byte* source, int offset)
    {
        LuminPackMarshal.CopyBlockUnaligned(ref destination[0], ref source[offset], CopyBytes);
        if (!new ReadOnlySpan<byte>(destination, CopyBytes).SequenceEqual(new ReadOnlySpan<byte>(source + offset, CopyBytes)))
            throw new InvalidOperationException($"Incorrect copy at source offset {offset}.");
    }

    /// <summary>Times repeated fixed-length copies at one source offset.</summary>
    /// <param name="destination">The aligned destination.</param>
    /// <param name="source">The aligned source.</param>
    /// <param name="offset">The source displacement.</param>
    /// <returns>Mean nanoseconds per copy.</returns>
    private static double Measure(byte* destination, byte* source, int offset)
    {
        long start = Stopwatch.GetTimestamp();
        for (int call = 0; call < CallsPerRound; call++)
            LuminPackMarshal.CopyBlockUnaligned(ref destination[0], ref source[offset], CopyBytes);
        return Stopwatch.GetElapsedTime(start).TotalNanoseconds / CallsPerRound;
    }

    /// <summary>Writes one probe record as JSON Lines.</summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="record">The observation to emit.</param>
    private static void Emit<T>(T record) => Console.WriteLine(JsonSerializer.Serialize(record));
}
