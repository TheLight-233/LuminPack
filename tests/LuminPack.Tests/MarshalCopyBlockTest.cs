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
    /// <summary>Checks standard XXH3 outputs across all short/long boundaries and unaligned input slices.</summary>
    [Fact]
    public void RefHashKernelsRetainAllLengthBoundaries()
    {
        byte[] input = new byte[8192];
        new Random(73681).NextBytes(input);
        using var digest = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        Span<byte> word = stackalloc byte[8];
        for (int length = 0; length <= 4096; length++)
        for (int offset = 0; offset < 16; offset++)
        {
            var data = input.AsSpan(offset, length);
            ulong hash = LuminPack.Internal.XxHash3.Hash64Utf8(data);
            Assert.Equal(hash, LuminPack.Internal.XxHash3.Hash64(ref MemoryMarshal.GetReference(data), (uint)data.Length));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(word, hash);
            digest.AppendData(word);
            var chars = MemoryMarshal.Cast<byte, char>(data.Slice(0, length & ~1));
            Assert.Equal(LuminPack.Internal.XxHash3.Hash64Utf8(MemoryMarshal.AsBytes(chars)),
                LuminPack.Internal.XxHash3.Hash64Utf16(chars));
        }
        // Independently computed with System.IO.Hashing 10.0.13, XxHash3.HashToUInt64 and seed zero.
        Assert.Equal("286B64BC5B07CA994AA38EF7A5CF748D96D3FFC1ED9538CB31F28F26E621A4A8", Convert.ToHexString(digest.GetHashAndReset()));
    }

    /// <summary>Checks independent standard seed-zero vectors for the JSON name length bands.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="expected">The System.IO.Hashing 10.0.13 reference value.</param>
    [Theory]
    [InlineData("", 0x2D06800538D394C2UL)]
    [InlineData("a", 0xE6C632B61E964E1FUL)]
    [InlineData("abc", 0x78AF5F94892F3950UL)]
    [InlineData("Item", 0x6DA5FB32BA7D29CDUL)]
    [InlineData("Count", 0xA89017B09331A439UL)]
    [InlineData("Enabled", 0xB1908C6E6BE576B4UL)]
    [InlineData("Timestamp", 0x4751805ADDEBC0B4UL)]
    [InlineData("DisplayName", 0xDBFB57CBDE200BEAUL)]
    [InlineData("InventoryItemCount", 0x01E85C9D52144266UL)]
    [InlineData("\u540d\u79f0\U0001f680", 0x1843DA27FF5A8226UL)]
    public void NameHashMatchesStandardVectors(string name, ulong expected)
    {
        Assert.Equal(expected, LuminPack.Internal.XxHash3.Hash64Utf8(System.Text.Encoding.UTF8.GetBytes(name)));
    }

    /// <summary>Regresses length-insensitive short hashes and commutative long block accumulation.</summary>
    [Fact]
    public void NameHashDistinguishesRepeatedLengthsAndBlockPermutations()
    {
        Assert.Equal(0x2D06800538D394C2UL, LuminPack.Internal.XxHash3.Hash64(ref Unsafe.NullRef<byte>(), 0));
        var utf8 = new HashSet<ulong>();
        var utf16 = new HashSet<ulong>();
        for (int length = 0; length <= 256; length++)
        {
            string name = new string('a', length);
            Assert.True(utf8.Add(LuminPack.Internal.XxHash3.Hash64Utf8(System.Text.Encoding.UTF8.GetBytes(name))));
            Assert.True(utf16.Add(LuminPack.Internal.XxHash3.Hash64Utf16(name)));
        }
        const string first = "AAAAAAAA11111111BBBBBBBB22222222";
        const string second = "BBBBBBBB22222222AAAAAAAA11111111";
        Assert.NotEqual(LuminPack.Internal.XxHash3.Hash64Utf8(System.Text.Encoding.UTF8.GetBytes(first)),
            LuminPack.Internal.XxHash3.Hash64Utf8(System.Text.Encoding.UTF8.GetBytes(second)));
        Assert.NotEqual(LuminPack.Internal.XxHash3.Hash64Utf16(first), LuminPack.Internal.XxHash3.Hash64Utf16(second));
    }

    /// <summary>Checks every short and long tail against inaccessible pages, including unaligned UTF-16 names.</summary>
    [Fact]
    public void NameHashDoesNotReadOutsideGuardedPages()
    {
        if (!OperatingSystem.IsWindows())
            return;
        nuint page = (nuint)Environment.SystemPageSize;
        byte* pages = (byte*)AllocatePages(null, page * 3, 0x3000, 0x01);
        try
        {
            Assert.True(pages != null);
            Assert.True(ProtectPages(pages + page, page, 0x04, out _));
            new Random(79231).NextBytes(new Span<byte>(pages + page, (int)page));
            Assert.True(ProtectPages(pages + page, page, 0x02, out _));
            for (int length = 0; length <= (int)page; length++)
            foreach (bool atEnd in new[] { false, true })
            {
                byte* source = atEnd ? pages + page * 2 - length : pages + page;
                var input = new ReadOnlySpan<byte>(source, length);
                Assert.Equal(LuminPack.Internal.XxHash3.Hash64Utf8(input.ToArray()), LuminPack.Internal.XxHash3.Hash64Utf8(input));
                var chars = new ReadOnlySpan<char>(source, length / 2);
                Assert.Equal(LuminPack.Internal.XxHash3.Hash64Utf8(MemoryMarshal.AsBytes(chars)), LuminPack.Internal.XxHash3.Hash64Utf16(chars));
            }
        }
        finally
        {
            if (pages != null)
                ReleasePages(pages, 0, 0x8000);
        }
    }

    /// <summary>Checks that span conversion retains partial output and legacy malformed-surrogate behavior.</summary>
    [Fact]
    public void SpanMarshalEncodingRetainsFailurePrefixes()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCD, 12).ToArray();
        Assert.Equal(-1, LuminPackMarshal.GetBytes("a\u00E9".AsSpan(), bytes.AsSpan(2, 2)));
        Assert.Equal((byte)'a', bytes[2]);
        Assert.Equal((byte)0xCD, bytes[3]);
        Assert.Equal(4, LuminPackMarshal.GetBytes("a\uD800b".AsSpan(), bytes.AsSpan(2, 8)));
        Assert.Equal(new byte[] { 97, 239, 191, 189 }, bytes.AsSpan(2, 4).ToArray());
        Assert.Equal((byte)0xCD, bytes[1]);
        Assert.Equal((byte)0xCD, bytes[6]);
        Assert.True(LuminPackMarshal.MemoryCompare(bytes, bytes));
        Assert.False(LuminPackMarshal.MemoryCompare(bytes, bytes.AsSpan(1)));
    }

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
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length);
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
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(source);
            NativeMemory.AlignedFree(destination);
        }
    }

    /// <summary>Checks zero count accepts null references without access.</summary>
    [Fact]
    public void EmptyCopiesDoNotAccessNullStorage()
    {
        LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.NullRef<byte>(), ref Unsafe.NullRef<byte>(), 0);
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
                CheckCopy(destination + 64 + destinationOffset, source + sourceOffset, length);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(source);
            NativeMemory.AlignedFree(destination);
        }
    }

    /// <summary>Checks protected endpoints of the public reference-based copy.</summary>
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

            foreach (int length in new[] { 64, 65, 95, 96, 97, 127, 128, 129, 159, 160, 161, 191, 192, 193, 223, 224, 225, 255, 256, 257, 511, 512, 513, 767, 768, 769, 1023, 1024, 1025, 4095, 4096, 4097, 8191, 8192, 8193, 16383, 16384, 16385, 32767, 32768, 32769, 1048575, 1048576, 1048577 })
            foreach (bool sourceEnd in new[] { false, true })
            foreach (bool destinationEnd in new[] { false, true })
            {
                byte* source = sourceEnd ? sourcePages + page + capacity - length : sourcePages + page;
                byte* destination = destinationEnd ? destinationPages + page + capacity - length : destinationPages + page;
                CheckGuardedKernel(publicReference, destination, source, length);
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

    /// <summary>Checks compact string widening at both edges of an accessible native page.</summary>
    [Fact]
    public void CompactStringReadsStayInsideGuardedPages()
    {
        if (!OperatingSystem.IsWindows()) return;
        nuint page = (nuint)Environment.SystemPageSize;
        byte* pages = (byte*)AllocatePages(null, page * 3, 0x3000, 0x01);
        try
        {
            Assert.True(pages != null, "Unable to reserve guarded test pages.");
            Assert.True(ProtectPages(pages + page, page, 0x04, out _));
            for (int length = 1; length <= 16; length++)
            for (int value = 0; value < 256; value++)
            foreach (bool atEnd in new[] { false, true })
            {
                int width = sizeof(int) + length;
                byte* record = atEnd ? pages + 2 * page - width : pages + page;
                var input = new Span<byte>(record, width);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(input, (length << 1) | 1);
                char[] chars = new char[length];
                for (int i = 0; i < length; i++)
                    chars[i] = (char)(input[sizeof(int) + i] = (byte)(value + i * 73));
                string expected = new string(chars);
                Assert.Equal(expected, ReadGuardedCompactString(record, width, null));
                Assert.Same(expected, ReadGuardedCompactString(record, width, expected));
                Assert.Equal(expected, ReadGuardedCompactString(record, width, new string('\u0100', length)));
            }
        }
        finally
        {
            if (pages != null) ReleasePages(pages, 0, 0x8000);
        }
    }

    /// <summary>Reads one complete literal record from its exact native window.</summary>
    /// <param name="record">The beginning of the guarded record.</param>
    /// <param name="width">The complete readable width.</param>
    /// <param name="current">The immutable candidate, if any.</param>
    /// <returns>The decoded or reused string.</returns>
    private static string? ReadGuardedCompactString(byte* record, int width, string? current)
    {
        ReadOnlySpan<byte> input = new ReadOnlySpan<byte>(record, width);
        var reader = new global::LuminPack.Core.LuminPackReader(ref input);
        string? result = reader.Read<global::LuminPack.Option.LuminPackUtf16LengthOptions>(current);
        Assert.Equal(width, reader.Position);
        return result;
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
        ulong expectedHash = LuminPack.Internal.XxHash3.Hash64Utf8(source);
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
                Assert.Equal(expectedHash, LuminPack.Internal.XxHash3.Hash64(ref source[0], (uint)source.Length));
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

    /// <summary>Checks a disjoint copy and sixteen untouched bytes on each side of its destination.</summary>
    /// <param name="destination">The writable range with canary headroom.</param>
    /// <param name="source">The readable non-overlapping range.</param>
    /// <param name="length">The exact byte count.</param>
    private static void CheckCopy(byte* destination, byte* source, int length)
    {
        new Span<byte>(destination - 16, length + 32).Fill(0xA5);
        LuminPackMarshal.CopyBlockUnaligned(ref *destination, ref *source, (uint)length);
        Assert.True(new ReadOnlySpan<byte>(source, length).SequenceEqual(new ReadOnlySpan<byte>(destination, length)));
        bool canariesIntact = true;
        for (int index = 1; index <= 16; index++)
            canariesIntact &= destination[-index] == 0xA5 && destination[length + index - 1] == 0xA5;
        Assert.True(canariesIntact);
    }
}
