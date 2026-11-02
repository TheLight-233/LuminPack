using LuminPack.Option;
using LuminPack;
using LuminPack.Core;
using LuminPack.Attribute;
using System.Runtime.InteropServices;
using LuminPack.Utility;
using LuminPack.Security;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks occupied reference slots and malformed string records.</summary>
public class AuditSafetyRegressionTest
{
    /// <summary>Rejects incomplete dynamic payload destinations before copying or resetting the source.</summary>
    /// <param name="capacity">The destination's logical capacity.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(63)]
    public void BufferPayloadCopyChecksDestinationBeforeWriting(int capacity)
    {
        using var source = new LuminBufferWriter(true);
        byte[] payload = Enumerable.Repeat((byte)0x42, 64).ToArray();
        source.LoadSpan(payload);
        byte[] guarded = Enumerable.Repeat((byte)0xCC, 128).ToArray();
        Span<byte> destination = guarded.AsSpan(16, capacity);
        var writer = new LuminPackWriter(ref destination);
        bool rejected = false;
        try { source.WriteToAndReset(ref writer); }
        catch (Exception error)
        {
            Assert.StartsWith("Span out of range", error.Message);
            rejected = true;
        }
        Assert.True(rejected, "An incomplete payload destination was accepted.");
        Assert.All(guarded, value => Assert.Equal((byte)0xCC, value));
        Assert.Equal(0, writer.Position);
        Assert.Equal(payload, source.GetSpan().ToArray());
    }

    /// <summary>Grows at the published append position before copying and resets the source only after success.</summary>
    [Fact]
    public void BufferPayloadCopyGrowsBeforeAppending()
    {
        using var source = new LuminBufferWriter(true);
        using var destination = new LuminBufferWriter(true);
        byte[] payload = Enumerable.Range(0, 70_000).Select(index => (byte)index).ToArray();
        source.LoadSpan(payload);
        destination.LoadSpan(new byte[] { 11, 17, 23 });
        var writer = new LuminPackWriter(destination);
        source.WriteToAndReset(ref writer);
        destination.CompleteWrite(writer.Position);
        Assert.Equal(payload.Length + 3, writer.Position);
        Assert.Equal(new byte[] { 11, 17, 23 }, destination.GetSpan().Slice(0, 3).ToArray());
        Assert.Equal(payload, destination.GetSpan().Slice(3).ToArray());
        Assert.Equal(0, source.CurrentIndex);
    }

    /// <summary>Rejects compressed frames whose output width disagrees with the collection before touching reusable storage.</summary>
    /// <param name="decodedBytes">The forged frame's otherwise valid output width.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(12)]
    public void CompressedCollectionRequiresExactDecodedWidth(int decodedBytes)
    {
        byte[] compressed = LuminCompressor.Compress(new byte[decodedBytes]);
        byte[] record = new byte[8 + compressed.Length];
        BitConverter.GetBytes(2).CopyTo(record, 0);
        BitConverter.GetBytes(compressed.Length).CopyTo(record, 4);
        compressed.CopyTo(record, 8);
        int[] existing = { 1729, 8128 };
        int[]? target = existing;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackReader.Read(record, ref target, 0, true, true));
        Assert.Same(existing, target);
        Assert.Equal(new[] { 1729, 8128 }, target);
        target = null;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackReader.Read(record, ref target, 0, true, true));
        Assert.Null(target);
        Assert.Throws<System.IO.InvalidDataException>(() => ReadCompressedSpan(record, existing));
        Assert.Equal(new[] { 1729, 8128 }, existing);
    }
    /// <summary>Reads a compressed raw record into a caller-owned span.</summary>
    /// <param name="record">The complete encoded record.</param><param name="storage">The destination allocation.</param>
    private static void ReadCompressedSpan(byte[] record, int[] storage)
    {
        Span<int> target = storage;
        LuminPackReader.Read(record, ref target, 0, true, true);
    }
    /// <summary>Prevents platform jobs from silently exercising another architecture or SIMD width.</summary>
    [Fact]
    public void RequestedHardwareIsActuallySelected()
    {
        string architecture = Environment.GetEnvironmentVariable("LUMINPACK_REQUIRE_ARCH");
        if (!string.IsNullOrEmpty(architecture))
            Assert.Equal(architecture, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
        if (Environment.GetEnvironmentVariable("LUMINPACK_REQUIRE_AVX2") == "1")
            Assert.True(System.Runtime.Intrinsics.X86.Avx2.IsSupported);
        if (Environment.GetEnvironmentVariable("LUMINPACK_REQUIRE_SSSE3") == "1")
            Assert.True(System.Runtime.Intrinsics.X86.Ssse3.IsSupported);
        if (Environment.GetEnvironmentVariable("LUMINPACK_REQUIRE_AVX512") == "1")
        {
            Assert.True(System.Runtime.Intrinsics.X86.Avx512BW.IsSupported);
            Assert.True(System.Runtime.Intrinsics.Vector512.IsHardwareAccelerated);
        }
    }
    /// <summary>Fixed F14 groups remain usable without x86 intrinsics or with all intrinsics disabled.</summary>
    [Fact]
    public void F14UsesPortableTagOperations()
    {
        using var map = new LuminF14Map<int, int>();
        for (int i = 0; i < 200; i++) map.Add(i, i * 7);
        for (int i = 0; i < 200; i++)
        {
            Assert.True(map.TryGetValue(i, out int value));
            Assert.Equal(i * 7, value);
        }
        for (int i = 0; i < 200; i++) Assert.True(map.Remove(i));
        Assert.Equal(0, map.Count);
    }

    /// <summary>Positive recorded character counts require exact, strict UTF-8 decoding on every target.</summary>
    [Fact]
    public void Utf8LengthRecordsRejectMalformedBytesAndCounts()
    {
        Assert.ThrowsAny<Exception>(() => ReadUtf8(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 65 }));
        Assert.ThrowsAny<Exception>(() => ReadUtf8(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 255 }));
        Assert.Equal("\uD83D\uDE00", ReadUtf8(new byte[] { 4, 0, 0, 0, 2, 0, 0, 0, 240, 159, 152, 128 }));
    }

    /// <summary>Reads one pinned UTF-8 record, including on pointer-cursor compatibility targets.</summary>
    /// <param name="bytes">The length-prefixed record.</param>
    /// <returns>The decoded text.</returns>
    private static unsafe string ReadUtf8(byte[] bytes)
    {
        fixed (byte* first = bytes)
        {
            ReadOnlySpan<byte> span = new(first, bytes.Length);
            var reader = new LuminPackReader(ref span);
            return reader.Read<LuminPackUtf8LengthOptions>((string?)null)!;
        }
    }
    /// <summary>Strict UTF-8 JSON must reject invalid raw bytes in consumed and skipped tokens.</summary>
    /// <param name="escaped">Whether the malformed token also contains an escaped newline.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StrictJsonRejectsMalformedUtf8(bool escaped)
    {
        byte[] bytes = escaped ? new byte[] { 34, 255, 92, 110, 34 } : new byte[] { 34, 255, 34 };
        Assert.Throws<FormatException>(() => ReadStrict(bytes));
    }

    /// <summary>Reads one UTF-8 document with the strict profile.</summary>
    /// <param name="bytes">The complete document.</param>
    /// <returns>The decoded string.</returns>
    private static string ReadStrict(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackJsonReader(ref span, new LuminPackReaderOptionalState());
        reader.Read<LuminPackOptions<LuminPackUtf8LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>();
        return reader.Read<string, LuminPackOptions<LuminPackUtf8LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>();
    }

    /// <summary>Packed framework values use their complete known widths, including SDK reference assemblies.</summary>
    [Fact]
    public void PackedGuidPreservesEveryByte()
    {
        var value = new AuditPackedGuid { Tag = 17, Id = new Guid("00112233-4455-6677-8899-aabbccddeeff") };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        Assert.Equal(17, bytes.Length);
        Assert.Equal(value.Id.ToByteArray(), bytes.AsSpan(1).ToArray());
        AuditPackedGuid copy = default;
        LuminPackSerializer.Deserialize(bytes, ref copy);
        Assert.Equal(value.Id, copy.Id);
    }

    /// <summary>Overlapping fields must not inflate the minimum native payload beyond its actual stride.</summary>
    [Fact]
    public void OverlappingTaggedArrayReadsItsOwnOutput()
    {
        var value = new[] { new AuditOverlapping { A = 12345678901234, C = 27 } };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        AuditOverlapping[] copy = null!;
        LuminPackSerializer.Deserialize(bytes, ref copy);
        Assert.Equal(value[0].A, copy[0].A);
        Assert.Equal(value[0].C, copy[0].C);
    }

    /// <summary>A rejected tag does not resize or overwrite an existing target array.</summary>
    [Fact]
    public void TaggedValidationPrecedesReuseAllocation()
    {
        var value = new[] { new AuditOverlapping { A = 19, C = 3 }, new AuditOverlapping { A = 20, C = 4 } };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        bytes[4] = 255;
        var original = new[] { new AuditOverlapping { A = 42 } };
        var copy = original;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Same(original, copy);
        Assert.Equal(42, copy[0].A);
    }
    /// <summary>An empty map must not resolve the default key.</summary>
    [Fact]
    public void EmptyMapRejectsDefaultKey()
    {
        var map = new LuminCircleReferenceMap<uint, object>();
        Assert.False(map.TryGetValue(0, out _));
        Assert.False(map.ContainsKey(0));
        Assert.False(map.Remove(0));
        Assert.Equal(0, map.Count);
        Assert.ThrowsAny<Exception>(() => new LuminPackReaderOptionalState().GetObjectReference(0));
    }

    /// <summary>Removing an entry invalidates every lookup and cannot decrement the count again.</summary>
    [Fact]
    public void RemovedEntryCannotBeReadOrRemovedAgain()
    {
        var map = new LuminCircleReferenceMap<int, string>();
        map.Add(7, "previous");
        Assert.True(map.Remove(7));
        Assert.False(map.TryGetValue(7, out _));
        Assert.False(map.ContainsKey(7));
        Assert.False(map.Remove(7));
        Assert.Equal(0, map.Count);
        ref string value = ref map.GetValueRefOrAddDefault(7, out bool exists);
        Assert.False(exists);
        value = "current";
        Assert.True(map.TryGetValue(7, out string restored));
        Assert.Equal("current", restored);
        Assert.Equal(1, map.Count);
    }

    /// <summary>Deleting the first candidate cannot allow a duplicate still stored in the second table.</summary>
    [Fact]
    public void DeletedCandidateDoesNotHideAnExistingKey()
    {
        var map = new LuminCircleReferenceMap<int, string>(8);
        map.Add(1, "first");
        map.Add(9, "second");
        Assert.True(map.Remove(1));
        Assert.False(map.TryAdd(9, "duplicate"));
        Assert.Equal(1, map.Count);
        Assert.True(map.TryGetValue(9, out var value));
        Assert.Equal("second", value);
    }
}

/// <summary>A packed record containing opaque framework storage.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public struct AuditPackedGuid
{
    public byte Tag;
    public Guid Id;
}

/// <summary>An explicit union whose logical field widths exceed its native stride.</summary>
[LuminPackable, StructLayout(LayoutKind.Explicit)]
public struct AuditOverlapping
{
    [FieldOffset(0)] public long A;
    [FieldOffset(0)] public long B;
    [FieldOffset(8)] public byte C;
}
