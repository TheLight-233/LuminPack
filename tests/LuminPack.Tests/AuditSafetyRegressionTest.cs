using LuminPack.Option;
using LuminPack;
using LuminPack.Core;
using LuminPack.Attribute;
using System.Runtime.InteropServices;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks occupied reference slots and malformed string records.</summary>
public class AuditSafetyRegressionTest
{
    /// <summary>Exercises the existing facades with a declaration-selected external-input policy.</summary>
    [Fact]
    public void CompiledInputOptionChecksDuringReadAndPreservesReuse()
    {
        var source = new AuditExternalMessage { Id = 17, Name = "bounded \u4e16\u754c", Values = new[] { 3, 5, 8 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(source));
        Assert.Equal("LPSAFE02", System.Text.Encoding.ASCII.GetString(bytes, 0, 8));
        var target = new AuditExternalMessage { Id = 91, Name = "unchanged", Values = new[] { 99 } };
        var existing = target;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(existing, target);
        Assert.Equal(source.Id, target.Id);
        Assert.Equal(source.Name, target.Name);
        Assert.Equal(source.Values, target.Values);
        var forged = (byte[])bytes.Clone();
        forged[24] ^= 1;
        target.Id = 91;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(forged, ref target));
        Assert.Same(existing, target);
        Assert.Equal(91, target.Id);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        string json = LuminPackSerializer.SerializeJson(source);
        Assert.Contains("\"$luminpack\":2", json);
        var restored = LuminPackSerializer.DeserializeJson<AuditExternalMessage>(json);
        Assert.Equal(source.Name, restored.Name);
        Assert.Equal(source.Values, restored.Values);
        byte[] overCount = LuminPackSerializer.Serialize(new AuditExternalMessage { Values = new int[5] });
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize<AuditExternalMessage>(overCount));
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson<AuditExternalMessage>(json.Replace("\"Id\":17", "\"Id\":17,\"Id\":19")));
        Assert.Equal(source.Id, LuminPackSerializer.DeserializeJson<AuditExternalMessage>(json.Replace("\"Id\"", "\"\\u0049d\"")).Id);
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson<AuditExternalMessage>(json.Replace("\"Id\":17", "\"Id\":17,\"\\u0049d\":19")));
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson<AuditExternalMessage>(json.Replace("\"Id\"", "\"Unknown\"")));
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<AuditExternalMessage>(json.Replace("\"Id\":17", "\"Id\":01")));
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson<AuditExternalMessage>(new string(' ', 5 * 1024 * 1024)));
        foreach (int length in Enumerable.Range(0, bytes.Length))
        {
            var shortened = bytes.AsSpan(0, length).ToArray();
            target.Id = 91;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(shortened, ref target));
            Assert.Equal(91, target.Id);
        }
        var packed = new AuditExternalPacked { Tag = 7, Count = 123456 };
        byte[] packedBytes = LuminPackSerializer.Serialize(packed);
        Assert.Equal(61, packedBytes.Length);
        Assert.Equal(packed.Count, LuminPackSerializer.Deserialize<AuditExternalPacked>(packedBytes).Count);
        var collections = new AuditExternalCollections { Numbers = new[] { 1, 2 }, Map = new Dictionary<int, string> { [3] = "three", [4] = "four" }, Set = new HashSet<int> { 7, 11 } };
        byte[] collectionBytes = LuminPackSerializer.Serialize(collections);
        Assert.Equal(2, LuminPackSerializer.Deserialize<AuditExternalCollections>(collectionBytes).Map.Count);
        string collectionJson = LuminPackSerializer.SerializeJson(collections);
        Assert.Equal(2, LuminPackSerializer.DeserializeJson<AuditExternalCollections>(collectionJson).Set.Count);
        foreach (string entry in new[] { "[]", "[3]", "[3,\"three\",5]", "null", "{}" })
        {
            string invalid = collectionJson.Replace("[3,\"three\"]", entry);
            Assert.NotEqual(collectionJson, invalid);
            Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson<AuditExternalCollections>(invalid));
        }
        var node = new AuditExternalNode { Value = 1, Next = new AuditExternalNode { Value = 2 } };
        byte[] nodeBytes = LuminPackSerializer.Serialize(node);
        Assert.Equal(2, LuminPackSerializer.Deserialize<AuditExternalNode>(nodeBytes).Next.Value);
        Assert.Throws<NotSupportedException>(() => LuminPackSerializer.Serialize(new AuditExternalDangerous { Type = typeof(int) }));
        var parent = new AuditExternalParent { Before = 8, Child = source, After = 13 };
        Assert.Equal(13, LuminPackSerializer.Deserialize<AuditExternalParent>(LuminPackSerializer.Serialize(parent)).After);
        Assert.Equal(13, LuminPackSerializer.DeserializeJson<AuditExternalParent>(LuminPackSerializer.SerializeJson(parent)).After);
        target.Id = 91;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson(json + "x", ref target));
        Assert.Equal(source.Id, target.Id);
        var badCount = (byte[])collectionBytes.Clone();
        BitConverter.GetBytes(int.MaxValue).CopyTo(badCount, 57);
        var destination = new AuditExternalCollections { Numbers = new[] { 99 } };
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(badCount, ref destination));
        Assert.Equal(new[] { 99 }, destination.Numbers);
        var duplicate = (byte[])collectionBytes.Clone();
        duplicate.AsSpan(duplicate.Length - 8, 4).CopyTo(duplicate.AsSpan(duplicate.Length - 4));
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(duplicate, ref destination));
        byte[] compressed = LuminCompressor.Compress(bytes.AsSpan(56));
        var compressedFrame = new byte[56 + compressed.Length];
        bytes.AsSpan(0, 56).CopyTo(compressedFrame);
        compressed.CopyTo(compressedFrame, 56);
        BitConverter.GetBytes(1).CopyTo(compressedFrame, 8);
        BitConverter.GetBytes(compressed.Length).CopyTo(compressedFrame, 12);
        Assert.Equal(source.Name, LuminPackSerializer.Deserialize<AuditExternalMessage>(compressedFrame).Name);
        BitConverter.GetBytes(int.MaxValue).CopyTo(compressedFrame, 16);
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(compressedFrame, ref target));
        byte[] overDepth = LuminPackSerializer.Serialize(new AuditExternalNode { Next = new AuditExternalNode { Next = new AuditExternalNode { Next = new AuditExternalNode() } } });
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize<AuditExternalNode>(overDepth));
        var plain = new AuditExternalGraph { Node = new AuditPlainNode { Next = new AuditPlainNode { Next = new AuditPlainNode { Next = new AuditPlainNode() } } } };
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize<AuditExternalGraph>(LuminPackSerializer.Serialize(plain)));
        byte[] overText = LuminPackSerializer.Serialize(new AuditExternalMessage { Name = new string('x', 129) });
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize<AuditExternalMessage>(overText));
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson<AuditExternalMessage>(LuminPackSerializer.SerializeJson(new AuditExternalMessage { Name = new string('x', 129) })));
        string repeatedSet = collectionJson.Replace("[7,11]", "[7,7]");
        Assert.NotEqual(collectionJson, repeatedSet);
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.DeserializeJson<AuditExternalCollections>(repeatedSet));

        var domains = new AuditExternalDomains { Id = 7, Name = null, Flag = true, Choice = AuditChoice.One, Bits = new[] { true, false } };
        byte[] domainBytes = LuminPackSerializer.Serialize(domains);
        var domainTarget = new AuditExternalDomains { Id = 91, Name = "retained", Bits = new[] { false, false } };
        int callbacks = 0;
        AuditExternalDomains.AfterRead = () => callbacks++;
        try
        {
            foreach (int offset in new[] { 65, 66, 74 })
            {
                byte[] invalid = (byte[])domainBytes.Clone(); invalid[offset] = 42;
                Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(invalid, ref domainTarget));
                Assert.Equal(7, domainTarget.Id);
                Assert.Equal(0, callbacks);
            }
            var sharedState = new LuminPackReaderOptionalState();
            AuditExternalDomains.AfterRead = () =>
            {
                ReadOnlySpan<byte> innerInput = bytes;
                var innerReader = new LuminPackReader(ref innerInput, sharedState);
                AuditExternalMessage inner = null!;
                LuminPackSerializer.Deserialize(ref innerReader, ref inner);
                Assert.Equal(source.Name, inner.Name);
                callbacks++;
            };
            ReadOnlySpan<byte> outerInput = domainBytes;
            var outerReader = new LuminPackReader(ref outerInput, sharedState);
            LuminPackSerializer.Deserialize(ref outerReader, ref domainTarget);
            Assert.Equal(1, callbacks);
        }
        finally { AuditExternalDomains.AfterRead = null; }
    }

    /// <summary>Rejects oversized stream and sequence staging before it can consume an unbounded source.</summary>
    /// <returns>The asynchronous ingestion verification.</returns>
    [Fact]
    public async Task CompiledInputOptionBoundsIngestion()
    {
        using var stream = new System.IO.MemoryStream(new byte[5 * 1024 * 1024]);
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(async () => await LuminPackSerializer.DeserializeAsync<AuditExternalMessage>(stream));
        Assert.InRange(stream.Position, 1, 4L * 1024 * 1024 + 1);
        var sequence = new System.Buffers.ReadOnlySequence<byte>(new byte[5 * 1024 * 1024]);
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize<AuditExternalMessage>(sequence));
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

/// <summary>An ordinary reusable contract whose safety constants are selected at compilation.</summary>
[LuminPackable]
[LuminPackOption(UntrustedData = true, MaxCollectionLength = 4, MaxStringLength = 128)]
public partial class AuditExternalMessage
{
    /// <summary>The scalar payload.</summary>
    public int Id;
    /// <summary>The bounded text payload.</summary>
    public string Name = "";
    /// <summary>The bounded collection payload.</summary>
    public int[] Values = Array.Empty<int>();
}

/// <summary>A portable headerless value under the selected external policy.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, StructLayout = LuminPackStructLayout.Packed)]
public struct AuditExternalPacked
{
    public byte Tag;
    public int Count;
}

/// <summary>Collection framing and duplicate validation under one root budget.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxCollectionLength = 4, StringEncoding = LuminPackStringEncoding.UTF8)]
public partial class AuditExternalCollections
{
    public int[] Numbers = Array.Empty<int>();
    public Dictionary<int, string> Map = new();
    public HashSet<int> Set = new();
}

/// <summary>A finite recursive graph with a shared nesting quota.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxDepth = 4)]
public partial class AuditExternalNode
{
    public int Value;
    public AuditExternalNode? Next;
}

/// <summary>A contract that must fail closed rather than invoking a dangerous formatter.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true)]
public partial class AuditExternalDangerous
{
    public Type? Type;
}

/// <summary>A trusted parent retaining its original framing around a separately selected external child.</summary>
[LuminPackable]
public partial class AuditExternalParent
{
    public int Before;
    public AuditExternalMessage Child = new();
    public int After;
}

/// <summary>Protects recursive unannotated children reached from an external root.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxDepth = 4)]
public partial class AuditExternalGraph
{
    public AuditPlainNode? Node;
}

/// <summary>A recursive ordinary type whose own trusted entry remains unchanged.</summary>
[LuminPackable]
public partial class AuditPlainNode
{
    public int Value;
    public AuditPlainNode? Next;
}

/// <summary>Exercises domain validation before callbacks and explicit-state reentrancy.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true)]
public partial class AuditExternalDomains
{
    public static Action? AfterRead;
    public int Id;
    public string? Name;
    public bool Flag;
    public AuditChoice Choice;
    public bool[] Bits = Array.Empty<bool>();
    /// <summary>Runs trusted application work only after the original successful field decoding.</summary>
    [LuminPackOnDeserialized]
    public void ReadCompleted() => AfterRead?.Invoke();
}

/// <summary>A closed enum domain for external records.</summary>
public enum AuditChoice { Zero, One }

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
