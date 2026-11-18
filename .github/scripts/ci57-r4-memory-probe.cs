using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using LuminPack.Code;

/// <summary>Measures reused-destination copies and fully overwritten array allocations across sizes.</summary>
internal static class Ci57MemoryProbe
{
    private const int Rounds = 9;

    /// <summary>Runs the copy or allocation comparison.</summary>
    /// <param name="args"><c>copy</c> or <c>alloc</c>.</param>
    private static void Main(string[] args)
    {
        Emit(new
        {
            Kind = "Environment", Mode = args[0], Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            Cpu = Environment.GetEnvironmentVariable("CI57_CPU"), Environment.ProcessorCount, GCSettings.IsServerGC,
            GCSettings.LatencyMode, TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            V128 = Vector128.IsHardwareAccelerated, V256 = Vector256.IsHardwareAccelerated, V512 = Vector512.IsHardwareAccelerated,
            Avx2 = Avx2.IsSupported, Avx512F = Avx512F.IsSupported, AdvSimd = AdvSimd.IsSupported
        });
        if (args[0] == "copy")
            Copies();
        else
            Allocations();
    }

    // ---------------------------------------------------------------- copies

    private static readonly int[] CopySizes = [64, 128, 256, 257, 384, 512, 768, 1024, 1536, 2048, 3072, 4096, 8192, 16384, 32768, 65536, 262144];
    private static readonly string[] CopyNames = ["lumin", "runtime", "medium256", "wide512", "vector128x4"];

    /// <summary>Times each available copy method at every size and two source offsets.</summary>
    private static void Copies()
    {
        bool[] available = [true, true, Vector256.IsHardwareAccelerated, Vector512.IsHardwareAccelerated, Vector128.IsHardwareAccelerated];
        foreach (int length in CopySizes)
        {
            byte[] source = new byte[length + 64];
            new Random(length).NextBytes(source);
            int calls = Math.Clamp(32 * 1024 * 1024 / length, 4096, 2_000_000);
            // One target is the matrix reuse shape (cache resident); eight rotating targets spread the
            // source/target distance (4 KiB aliasing) and exceed L1 from 8 KiB upward.
            foreach (int count in new[] { 1, 8 })
            foreach (int offset in new[] { 4, 13 })
            {
                byte[][] targets = new byte[count][];
                for (int i = 0; i < targets.Length; i++)
                    targets[i] = new byte[length + 1];
                for (int round = -1; round < Rounds; round++)
                {
                    for (int step = 0; step < CopyNames.Length; step++)
                    {
                        int method = (step + round + CopyNames.Length) % CopyNames.Length;
                        if (!available[method])
                            continue;
                        foreach (var target in targets)
                            target[length] = 0x5a;
                        double ns = method switch
                        {
                            0 => Time<LuminCopy>(source, offset, targets, length, calls),
                            1 => Time<RuntimeCopy>(source, offset, targets, length, calls),
                            2 => Time<Medium256>(source, offset, targets, length, calls),
                            3 => Time<Wide512>(source, offset, targets, length, calls),
                            _ => Time<Vector128x4>(source, offset, targets, length, calls),
                        };
                        foreach (var target in targets)
                        {
                            if (target[length] != 0x5a || !source.AsSpan(offset, length).SequenceEqual(target.AsSpan(0, length)))
                                throw new InvalidOperationException($"{CopyNames[method]} copied {length} bytes at {offset} incorrectly.");
                        }

                        if (round >= 0)
                            Emit(new { Kind = "Copy", Method = CopyNames[method], Length = length, Offset = offset, Targets = count, Round = round, Ns = ns });
                    }
                }
            }
        }
    }

    /// <summary>A copy method under test.</summary>
    private interface ICopy
    {
        /// <summary>Copies a nonoverlapping range of at least 64 bytes.</summary>
        /// <param name="target">The first writable byte.</param>
        /// <param name="source">The first readable byte.</param>
        /// <param name="length">The byte count.</param>
        static abstract void Copy(ref byte target, ref byte source, uint length);
    }

    /// <summary>Times one method over rotating targets.</summary>
    private static double Time<T>(byte[] source, int offset, byte[][] targets, int length, int calls) where T : ICopy
    {
        ref byte first = ref source[offset];
        int mask = targets.Length - 1;
        long start = Stopwatch.GetTimestamp();
        for (int call = 0; call < calls; call++)
            T.Copy(ref MemoryMarshal.GetArrayDataReference(targets[call & mask]), ref first, (uint)length);
        return Stopwatch.GetElapsedTime(start).TotalNanoseconds / calls;
    }

    /// <summary>The production kernel for a reused destination.</summary>
    private readonly struct LuminCopy : ICopy
    {
        public static void Copy(ref byte target, ref byte source, uint length) => LuminPackMarshal.CopyBlockUnaligned(ref target, ref source, length);
    }

    /// <summary>The runtime block copy (<c>cpblk</c>, which reaches <c>Buffer.Memmove</c>).</summary>
    private readonly struct RuntimeCopy : ICopy
    {
        public static void Copy(ref byte target, ref byte source, uint length) => Unsafe.CopyBlockUnaligned(ref target, ref source, length);
    }

    /// <summary>The production 256-bit medium kernel, measurable outside its production size range.</summary>
    private readonly struct Medium256 : ICopy
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Copy(ref byte destination, ref byte source, uint count)
        {
            Vector256.LoadUnsafe(ref source).StoreUnsafe(ref destination);
            nuint last = count - (nuint)Vector256<byte>.Count;
            nuint address = (nuint)Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref destination);
            nuint offset = (~address & (nuint)(Vector256<byte>.Count - 1)) + 1;
            for (; offset + 3 * (nuint)Vector256<byte>.Count <= last; offset += 4 * (nuint)Vector256<byte>.Count)
            {
                var a = Vector256.LoadUnsafe(ref source, offset);
                var b = Vector256.LoadUnsafe(ref source, offset + 32);
                var c = Vector256.LoadUnsafe(ref source, offset + 64);
                var d = Vector256.LoadUnsafe(ref source, offset + 96);
                a.StoreUnsafe(ref destination, offset);
                b.StoreUnsafe(ref destination, offset + 32);
                c.StoreUnsafe(ref destination, offset + 64);
                d.StoreUnsafe(ref destination, offset + 96);
            }

            for (; offset <= last; offset += 32)
                Vector256.LoadUnsafe(ref source, offset).StoreUnsafe(ref destination, offset);
            Vector256.LoadUnsafe(ref source, last).StoreUnsafe(ref destination, last);
        }
    }

    /// <summary>A 512-bit variant: 64-byte aligned stores, four vectors per iteration, overlapping head and tail.</summary>
    private readonly struct Wide512 : ICopy
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Copy(ref byte destination, ref byte source, uint count)
        {
            Vector512.LoadUnsafe(ref source).StoreUnsafe(ref destination);
            nuint last = count - (nuint)Vector512<byte>.Count;
            nuint address = (nuint)Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref destination);
            nuint offset = (~address & (nuint)(Vector512<byte>.Count - 1)) + 1;
            for (; offset + 3 * (nuint)Vector512<byte>.Count <= last; offset += 4 * (nuint)Vector512<byte>.Count)
            {
                var a = Vector512.LoadUnsafe(ref source, offset);
                var b = Vector512.LoadUnsafe(ref source, offset + 64);
                var c = Vector512.LoadUnsafe(ref source, offset + 128);
                var d = Vector512.LoadUnsafe(ref source, offset + 192);
                a.StoreUnsafe(ref destination, offset);
                b.StoreUnsafe(ref destination, offset + 64);
                c.StoreUnsafe(ref destination, offset + 128);
                d.StoreUnsafe(ref destination, offset + 192);
            }

            for (; offset <= last; offset += 64)
                Vector512.LoadUnsafe(ref source, offset).StoreUnsafe(ref destination, offset);
            Vector512.LoadUnsafe(ref source, last).StoreUnsafe(ref destination, last);
        }
    }

    /// <summary>A 128-bit variant for targets without wide vectors (Arm64 AdvSimd, SSE-only x64).</summary>
    private readonly struct Vector128x4 : ICopy
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Copy(ref byte destination, ref byte source, uint count)
        {
            Vector128.LoadUnsafe(ref source).StoreUnsafe(ref destination);
            nuint last = count - (nuint)Vector128<byte>.Count;
            nuint address = (nuint)Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref destination);
            nuint offset = (~address & (nuint)(Vector128<byte>.Count - 1)) + 1;
            for (; offset + 3 * (nuint)Vector128<byte>.Count <= last; offset += 4 * (nuint)Vector128<byte>.Count)
            {
                var a = Vector128.LoadUnsafe(ref source, offset);
                var b = Vector128.LoadUnsafe(ref source, offset + 16);
                var c = Vector128.LoadUnsafe(ref source, offset + 32);
                var d = Vector128.LoadUnsafe(ref source, offset + 48);
                a.StoreUnsafe(ref destination, offset);
                b.StoreUnsafe(ref destination, offset + 16);
                c.StoreUnsafe(ref destination, offset + 32);
                d.StoreUnsafe(ref destination, offset + 48);
            }

            for (; offset <= last; offset += 16)
                Vector128.LoadUnsafe(ref source, offset).StoreUnsafe(ref destination, offset);
            Vector128.LoadUnsafe(ref source, last).StoreUnsafe(ref destination, last);
        }
    }

    // ---------------------------------------------------------- allocations

    private static readonly int[] AllocationSizes = [256, 1024, 2048, 3072, 4096, 8192, 16384, 32768, 65536, 84000, 98304, 163840, 524288, 1048576];
    private static readonly string[] AllocationNames = ["new+fill", "uninit+fill", "new+touch", "uninit+touch"];

    /// <summary>Times zeroed and uninitialized byte arrays, each fully overwritten or touched at both ends.</summary>
    private static void Allocations()
    {
        byte[] source = new byte[AllocationSizes[^1]];
        new Random(73).NextBytes(source);
        foreach (int bytes in AllocationSizes)
        {
            int calls = Math.Clamp(128 * 1024 * 1024 / bytes, 256, 500_000);
            for (int round = -1; round < Rounds; round++)
            {
                for (int step = 0; step < AllocationNames.Length; step++)
                {
                    int variant = (step + round + AllocationNames.Length) % AllocationNames.Length;
                    bool zeroed = (variant & 1) == 0, fill = variant < 2;
                    int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                    long allocated = GC.GetAllocatedBytesForCurrentThread();
                    long start = Stopwatch.GetTimestamp();
                    long checksum = 0;
                    for (int i = 0; i < calls; i++)
                    {
                        byte[] target = zeroed ? new byte[bytes] : GC.AllocateUninitializedArray<byte>(bytes);
                        if (fill)
                            Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetArrayDataReference(target), ref MemoryMarshal.GetArrayDataReference(source), (uint)bytes);
                        else
                        {
                            target[0] = 1;
                            target[^1] = 1;
                        }

                        checksum += target[bytes >> 1];
                    }

                    double ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds / calls;
                    double perOp = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)calls;
                    if (round >= 0)
                        Emit(new { Kind = "Allocation", Variant = AllocationNames[variant], Bytes = bytes, Round = round, Ns = ns,
                            BytesPerOp = perOp, Gen0 = GC.CollectionCount(0) - g0, Gen1 = GC.CollectionCount(1) - g1,
                            Gen2 = GC.CollectionCount(2) - g2, Checksum = checksum });
                }
            }
        }
    }

    /// <summary>Writes one JSON Lines record.</summary>
    private static void Emit<T>(T record) => Console.WriteLine(JsonSerializer.Serialize(record));
}
