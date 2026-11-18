using System.Buffers.Binary;
using System.Collections;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks bit-count framing across the integer and byte runtime storage layouts.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class BitArrayStorageCompatibilityTest
{
    private static BitArray? s_allocationSink;

    /// <summary>Checks optional final storage allocates only its published owner and backing array.</summary>
    [Fact]
    public void PortableFinalStorageMatchesConstructionAllocations()
    {
        foreach (int length in new[] { 1, 33, 257, 4096 })
        {
            for (int iteration = 0; iteration < 64; iteration++)
            {
                Assert.True(global::LuminPack.Code.LuminPackCollectionAccess.TryCreateBitArrayPayload(length, out var fresh, out var bytes));
                Assert.Equal(((length + 31) >> 5) * 4, bytes.Length);
                bytes.Fill(0xA5);
                fresh!.Length = length;
                s_allocationSink = fresh;
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 128; iteration++)
                s_allocationSink = new BitArray((length + 31) & ~31) { Length = length };
            long expected = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 128; iteration++)
            {
                global::LuminPack.Code.LuminPackCollectionAccess.TryCreateBitArrayPayload(length, out var fresh, out _);
                fresh!.Length = length;
                s_allocationSink = fresh;
            }
            Assert.Equal(expected, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        s_allocationSink = null;
        Assert.False(global::LuminPack.Code.LuminPackCollectionAccess.TryCreateBitArrayPayload(0, out _, out _));
        Assert.False(global::LuminPack.Code.LuminPackCollectionAccess.TryCreateBitArrayPayload(int.MaxValue, out _, out _));
    }

    /// <summary>Checks direct writes preserve CopyTo padding without changing input and leave excess capacity to fallback.</summary>
    [Fact]
    public void PortableDirectWritesPreserveCopyToAndSourceStorage()
    {
        using var output = new global::LuminPack.Utility.LuminBufferWriter(true);
        foreach (int length in new[] { 1, 7, 31, 32, 33, 65, 257 })
        {
            Assert.True(global::LuminPack.Code.LuminPackCollectionAccess.TryCreateBitArrayPayload(length, out var source, out var payload));
            payload.Fill(0xFF);
            source!.Length = length;
            byte[] original = payload.ToArray();
            var words = new int[(length + 31) / 32];
            source.CopyTo(words, 0);
            byte[] expected = new byte[4 + words.Length * 4];
            BinaryPrimitives.WriteInt32LittleEndian(expected, length);
            global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(words.AsSpan()).CopyTo(expected.AsSpan(4));
            output.ResetWrittenCount();
            var writer = new global::LuminPack.Core.LuminPackWriter(output);
            Assert.True(global::LuminPack.Code.LuminPackCollectionAccess.TryWriteBitArrayPayload(ref writer, source));
            Assert.Equal(expected, writer.Buffer[..writer.Position].ToArray());
            Assert.Equal(original, payload.ToArray());
        }
        var excess = new BitArray(96, true) { Length = 32 };
        var fallbackWriter = new global::LuminPack.Core.LuminPackWriter(output);
        int position = fallbackWriter.Position;
        Assert.False(global::LuminPack.Code.LuminPackCollectionAccess.TryWriteBitArrayPayload(ref fallbackWriter, excess));
        Assert.Equal(position, fallbackWriter.Position);
    }

    /// <summary>Checks established word-padded bytes, fresh reads and reuse on each target runtime.</summary>
    [Fact]
    public void BitArraysKeepTheWordPaddedWire()
    {
        foreach (int length in new[] { 0, 1, 7, 8, 9, 31, 32, 33, 63, 64, 65, 127, 128, 129, 257 })
        {
            var source = new BitArray(length);
            byte[] expected = new byte[4 + ((length + 31) / 32) * 4];
            BinaryPrimitives.WriteInt32LittleEndian(expected, length);
            for (int index = 0; index < length; index++)
            {
                source[index] = index % 3 == 0 || index % 11 == 1;
                if (source[index]) expected[4 + index / 8] |= (byte)(1 << (index & 7));
            }

            Assert.Equal(expected, LuminPackSerializer.Serialize(source));
            AssertBits(source, global::LuminPackUnitTest.ConcreteReads.Read<BitArray>(expected, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BitArray concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!);
            var reused = new BitArray(length, true);
            var original = reused;
            LuminPackSerializer.Deserialize(expected, ref reused);
            Assert.Same(original, reused);
            AssertBits(source, reused);
            Assert.Equal(expected, LuminPackSerializer.Serialize(reused));
        }
    }

    /// <summary>Checks legacy nonzero padding remains readable while byte storage restores its runtime invariant.</summary>
    [Fact]
    public void LegacyHighPaddingBitsDoNotLeakFromByteStorage()
    {
        foreach (int length in new[] { 1, 7, 8, 9, 31, 33, 63, 65 })
        {
            byte[] record = new byte[4 + ((length + 31) / 32) * 4];
            record.AsSpan(4).Fill(0xFF);
            BinaryPrimitives.WriteInt32LittleEndian(record, length);
            var result = global::LuminPackUnitTest.ConcreteReads.Read<BitArray>(record, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BitArray concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!;
            Assert.Equal(length, result.Length);
            for (int index = 0; index < length; index++) Assert.True(result[index]);

#if NET10_0_OR_GREATER
            var words = new int[(length + 31) / 32];
            result.CopyTo(words, 0);
            Assert.Equal((1u << (length & 31)) - 1, (uint)words[^1]);
#endif
        }
    }

    /// <summary>Checks incomplete word payloads fail before changing the reused bit storage.</summary>
    [Fact]
    public void TruncatedPayloadDoesNotMutateReusedStorage()
    {
        var reused = new BitArray(33, true);
        var original = reused;
        byte[] record = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(record, 33);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(record, ref reused));
        Assert.Same(original, reused);
        for (int index = 0; index < reused.Length; index++) Assert.True(reused[index]);
    }

    /// <summary>Checks the null marker is distinct from an empty bit array.</summary>
    [Fact]
    public void NullAndEmptyRemainDistinct()
    {
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize((BitArray)null!));
        Assert.Null(global::LuminPackUnitTest.ConcreteReads.Read<BitArray>(new byte[] { 255, 255, 255, 255 }, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BitArray concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
        Assert.NotNull(global::LuminPackUnitTest.ConcreteReads.Read<BitArray>(new byte[4], static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BitArray concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
    }

    /// <summary>Compares logical bit values without relying on either backing array representation.</summary>
    /// <param name="expected">The expected logical bits.</param>
    /// <param name="actual">The reconstructed logical bits.</param>
    private static void AssertBits(BitArray expected, BitArray actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }
}
