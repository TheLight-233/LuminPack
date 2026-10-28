using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks exact ranges, dispatch boundaries and managed storage for the public Marshal copier.</summary>
[Collection(nameof(RuntimeStateCollection))]
public unsafe class MarshalCopyBlockTest
{
    /// <summary>Checks all short lengths and source/destination cache-line offsets through both public overloads.</summary>
    [Fact]
    public void ShortCopiesPreserveExactRangesForEveryAlignment()
    {
        byte* source = (byte*)NativeMemory.AlignedAlloc(512, 64);
        byte* destination = (byte*)NativeMemory.AlignedAlloc(512, 64);
        try
        {
            new Random(732).NextBytes(new Span<byte>(source, 512));
            for (int length = 0; length <= 257; length++)
            for (int sourceOffset = 0; sourceOffset < 64; sourceOffset++)
            for (int destinationOffset = 0; destinationOffset < 64; destinationOffset++)
            {
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length, false);
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length, true);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(source);
            NativeMemory.AlignedFree(destination);
        }
    }

    /// <summary>Checks the small, native and non-temporal dispatch boundaries with unaligned complete payloads.</summary>
    /// <param name="length">The exact payload byte count.</param>
    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(767)]
    [InlineData(768)]
    [InlineData(769)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8191)]
    [InlineData(8192)]
    [InlineData(8193)]
    [InlineData(16383)]
    [InlineData(16384)]
    [InlineData(16385)]
    [InlineData(32767)]
    [InlineData(32768)]
    [InlineData(32769)]
    [InlineData(65536)]
    [InlineData(1048575)]
    [InlineData(1048576)]
    [InlineData(1048577)]
    [InlineData(4194304)]
    [InlineData(8388608)]
    public void BoundaryCopiesMatchSourceAndPreserveCanaries(int length)
    {
        byte* source = (byte*)NativeMemory.AlignedAlloc((nuint)((length + 255) & ~63), 64);
        byte* destination = (byte*)NativeMemory.AlignedAlloc((nuint)((length + 255) & ~63), 64);
        try
        {
            new Random(length).NextBytes(new Span<byte>(source, length + 64));
            foreach (int sourceOffset in new[] { 0, 1, 15, 31, 63 })
            foreach (int destinationOffset in new[] { 0, 1, 15, 31, 63 })
            {
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length, false);
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length, true);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(source);
            NativeMemory.AlignedFree(destination);
        }
    }

    /// <summary>Checks zero count accepts null native and managed references without access.</summary>
    [Fact]
    public void EmptyCopiesDoNotAccessNullStorage()
    {
        LuminPackMarshal.CopyBlockUnaligned(null, null, 0);
        LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.NullRef<byte>(), ref Unsafe.NullRef<byte>(), 0);
    }

    /// <summary>Validates short 64-byte lane schedules even when the host splits Vector512 operations.</summary>
    [Fact]
    public void Vector512ShortKernelPreservesExactRanges()
    {
        if (Unsafe.SizeOf<IntPtr>() != 8)
            return;

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var shortCopy = typeof(LuminPackMarshal).GetMethod("CopyVector512Block", flags)!.CreateDelegate<RangeCopy>();
        byte* source = (byte*)NativeMemory.AlignedAlloc(512, 64);
        byte* destination = (byte*)NativeMemory.AlignedAlloc(512, 64);
        try
        {
            new Random(512).NextBytes(new Span<byte>(source, 512));
            for (int length = 64; length <= 256; length++)
            for (int sourceOffset = 0; sourceOffset < 64; sourceOffset++)
            for (int destinationOffset = 0; destinationOffset < 64; destinationOffset++)
                CheckVectorKernel(shortCopy, destination + 64 + destinationOffset, source + sourceOffset, length);
        }
        finally
        {
            NativeMemory.AlignedFree(source);
            NativeMemory.AlignedFree(destination);
        }
    }

    /// <summary>Checks every length across the short-to-native boundary and former medium dispatch bands.</summary>
    [Fact]
    public void MediumCopiesPreserveExactRangesForEveryRemainder()
    {
        byte* source = (byte*)NativeMemory.AlignedAlloc(2048, 64);
        byte* destination = (byte*)NativeMemory.AlignedAlloc(2048, 64);
        try
        {
            new Random(431).NextBytes(new Span<byte>(source, 2048));
            for (int length = 257; length <= 1280; length++)
            foreach (int sourceOffset in new[] { 0, 1, 31, 63 })
            foreach (int destinationOffset in new[] { 0, 1, 31, 63 })
            {
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length, false);
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length, true);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(source);
            NativeMemory.AlignedFree(destination);
        }
    }

    /// <summary>Checks protected endpoints of public paths and split 512-bit cached schedules on Windows.</summary>
    [Fact]
    public void CopiesDoNotCrossProtectedPageBoundaries()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const int capacity = 2 * 1024 * 1024;
        nuint page = (nuint)Environment.SystemPageSize;
        byte* sourcePages = (byte*)AllocatePages(null, (nuint)capacity + 2 * page, 0x3000, 0x01);
        byte* destinationPages = (byte*)AllocatePages(null, (nuint)capacity + 2 * page, 0x3000, 0x01);
        try
        {
            Assert.True(sourcePages != null && destinationPages != null, "Unable to reserve guarded test pages.");
            Assert.True(ProtectPages(sourcePages + page, capacity, 0x04, out _));
            Assert.True(ProtectPages(destinationPages + page, capacity, 0x04, out _));
            new Span<byte>(sourcePages + page, capacity).Fill(0x73);
            Assert.True(ProtectPages(sourcePages + page, capacity, 0x02, out _));
            RangeCopy publicReference = LuminPackMarshal.CopyBlockUnaligned;
            RangeCopy publicPointer = static (ref byte destination, ref byte source, uint count) =>
                LuminPackMarshal.CopyBlockUnaligned(Unsafe.AsPointer(ref destination), Unsafe.AsPointer(ref source), count);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            RangeCopy? wideShort = null;
            if (Unsafe.SizeOf<IntPtr>() == 8)
            {
                wideShort = typeof(LuminPackMarshal).GetMethod("CopyVector512Block", flags)!.CreateDelegate<RangeCopy>();
            }

            foreach (int length in new[] { 64, 65, 95, 96, 97, 127, 128, 129, 159, 160, 161, 191, 192, 193, 223, 224, 225, 255, 256, 257, 511, 512, 513, 767, 768, 769, 1023, 1024, 1025, 4095, 4096, 4097, 8191, 8192, 8193, 16383, 16384, 16385, 32767, 32768, 32769, 1048575, 1048576, 1048577 })
            foreach (bool sourceEnd in new[] { false, true })
            foreach (bool destinationEnd in new[] { false, true })
            {
                byte* source = sourceEnd ? sourcePages + page + capacity - length : sourcePages + page;
                byte* destination = destinationEnd ? destinationPages + page + capacity - length : destinationPages + page;
                CheckGuardedKernel(publicReference, destination, source, length);
                CheckGuardedKernel(publicPointer, destination, source, length);
                if (length <= 256 && wideShort != null)
                    CheckGuardedKernel(wideShort, destination, source, length);
            }
        }
        finally
        {
            if (sourcePages != null)
                ReleasePages(sourcePages, 0, 0x8000);
            if (destinationPages != null)
                ReleasePages(destinationPages, 0, 0x8000);
        }
    }

    /// <summary>Checks large managed sources remain valid while the GC compacts their storage.</summary>
    [Fact]
    public void ManagedRangesRemainValidDuringCompactingGc()
    {
        byte[] source = new byte[1048577];
        byte[] destination = new byte[source.Length + 2];
        byte[] mediumSource = new byte[4095];
        byte[] mediumDestination = new byte[4097];
        new Random(1129).NextBytes(source);
        new Random(8191).NextBytes(mediumSource);
        using var cancellation = new CancellationTokenSource();
        GCLargeObjectHeapCompactionMode previousMode = GCSettings.LargeObjectHeapCompactionMode;
        Task pressure = Task.Run(() =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                Thread.Yield();
            }
        });
        try
        {
            for (int iteration = 0; iteration < 128; iteration++)
            {
                LuminPackMarshal.CopyBlockUnaligned(ref destination[1], ref source[0], (uint)source.Length);
                LuminPackMarshal.CopyBlockUnaligned(ref mediumDestination[1], ref mediumSource[0], (uint)mediumSource.Length);
            }
            Assert.True(source.AsSpan().SequenceEqual(destination.AsSpan(1, source.Length)));
            Assert.True(mediumSource.AsSpan().SequenceEqual(mediumDestination.AsSpan(1, mediumSource.Length)));
            Assert.Equal((byte)0, mediumDestination[0]);
            Assert.Equal((byte)0, mediumDestination[^1]);
            Assert.Equal((byte)0, destination[0]);
            Assert.Equal((byte)0, destination[^1]);
        }
        finally
        {
            cancellation.Cancel();
            pressure.GetAwaiter().GetResult();
            GCSettings.LargeObjectHeapCompactionMode = previousMode;
        }
    }

    /// <summary>Invokes a cached kernel with tracked storage references.</summary>
    /// <param name="destination">Writable exact range.</param>
    /// <param name="source">Readable non-overlapping exact range.</param>
    /// <param name="count">Exact byte count.</param>
    private delegate void RangeCopy(ref byte destination, ref byte source, uint count);

    /// <summary>Checks an exact copy whose source or destination can touch an inaccessible page.</summary>
    /// <param name="copy">The selected public or cached kernel.</param>
    /// <param name="destination">The writable exact range.</param>
    /// <param name="source">The read-only exact range.</param>
    /// <param name="length">The exact byte count.</param>
    private static void CheckGuardedKernel(RangeCopy copy, byte* destination, byte* source, int length)
    {
        new Span<byte>(destination, length).Fill(0xA5);
        copy(ref *destination, ref *source, (uint)length);
        Assert.True(new ReadOnlySpan<byte>(source, length).SequenceEqual(new ReadOnlySpan<byte>(destination, length)));
    }

    /// <summary>Reserves native pages with an explicit initial protection.</summary>
    /// <param name="address">The preferred base address or null.</param>
    /// <param name="size">The complete reservation size.</param>
    /// <param name="allocationType">Windows reservation and commitment flags.</param>
    /// <param name="protection">The initial page protection.</param>
    /// <returns>The reserved base address, or zero on failure.</returns>
    [DllImport("kernel32.dll", EntryPoint = "VirtualAlloc", SetLastError = true)]
    private static extern nint AllocatePages(void* address, nuint size, uint allocationType, uint protection);

    /// <summary>Changes the protection of committed native pages.</summary>
    /// <param name="address">The first committed page.</param>
    /// <param name="size">The protected byte count.</param>
    /// <param name="protection">The requested page protection.</param>
    /// <param name="previousProtection">The prior protection of the first page.</param>
    /// <returns>Whether the protection changed successfully.</returns>
    [DllImport("kernel32.dll", EntryPoint = "VirtualProtect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProtectPages(void* address, nuint size, uint protection, out uint previousProtection);

    /// <summary>Releases a complete native page reservation.</summary>
    /// <param name="address">The reservation base address.</param>
    /// <param name="size">Zero when releasing the complete reservation.</param>
    /// <param name="freeType">The Windows release flag.</param>
    /// <returns>Whether the reservation was released successfully.</returns>
    [DllImport("kernel32.dll", EntryPoint = "VirtualFree", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleasePages(void* address, nuint size, uint freeType);

    /// <summary>Verifies a directly selected vector schedule and its adjacent canaries.</summary>
    /// <param name="copy">The selected cached kernel.</param>
    /// <param name="destination">Writable range with canary headroom.</param>
    /// <param name="source">Readable non-overlapping range.</param>
    /// <param name="length">Exact byte count.</param>
    private static void CheckVectorKernel(RangeCopy copy, byte* destination, byte* source, int length)
    {
        new Span<byte>(destination - 16, length + 32).Fill(0xA5);
        copy(ref *destination, ref *source, (uint)length);
        Assert.True(new ReadOnlySpan<byte>(source, length).SequenceEqual(new ReadOnlySpan<byte>(destination, length)));
        bool canariesIntact = true;
        for (int index = 1; index <= 16; index++)
            canariesIntact &= destination[-index] == 0xA5 && destination[length + index - 1] == 0xA5;
        Assert.True(canariesIntact);
    }

    /// <summary>Checks a disjoint copy and sixteen untouched bytes on each side of its destination.</summary>
    /// <param name="destination">The writable range with canary headroom.</param>
    /// <param name="source">The readable non-overlapping range.</param>
    /// <param name="length">The exact byte count.</param>
    /// <param name="useReferences">Whether to invoke the managed-reference overload.</param>
    private static void CheckCopy(byte* destination, byte* source, int length, bool useReferences)
    {
        new Span<byte>(destination - 16, length + 32).Fill(0xA5);
        if (useReferences)
            LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.AsRef<byte>(destination), ref Unsafe.AsRef<byte>(source), (uint)length);
        else
            LuminPackMarshal.CopyBlockUnaligned(destination, source, (uint)length);
        Assert.True(new ReadOnlySpan<byte>(source, length).SequenceEqual(new ReadOnlySpan<byte>(destination, length)));
        bool canariesIntact = true;
        for (int index = 1; index <= 16; index++)
            canariesIntact &= destination[-index] == 0xA5 && destination[length + index - 1] == 0xA5;
        Assert.True(canariesIntact);
    }
}
