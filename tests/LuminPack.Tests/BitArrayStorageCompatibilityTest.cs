using System.Buffers.Binary;
using System.Collections;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks bit-count framing across the integer and byte runtime storage layouts.</summary>
public class BitArrayStorageCompatibilityTest
{
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
            AssertBits(source, LuminPackSerializer.Deserialize<BitArray>(expected)!);
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
            var result = LuminPackSerializer.Deserialize<BitArray>(record)!;
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
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize<BitArray>(null!));
        Assert.Null(LuminPackSerializer.Deserialize<BitArray>(new byte[] { 255, 255, 255, 255 }));
        Assert.NotNull(LuminPackSerializer.Deserialize<BitArray>(new byte[4]));
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
