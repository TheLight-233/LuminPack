#if NET8_0_OR_GREATER
using System.Buffers.Binary;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks raw dense hash-set writes at the vector threshold and payload tail.</summary>
public sealed class DenseHashSetWriteTest
{
    /// <summary>Writes the same enumeration order through the concrete set and both interfaces.</summary>
    [Fact]
    public void DenseAndSparseSetsKeepTheirWireBytes()
    {
        foreach (int count in new[] { 15, 16, 17, 18, 19, 20, 256, 4096 })
        {
            var set = new HashSet<int>(Enumerable.Range(0, count).Select(index => unchecked((index + 1) * 486187739)));
            CheckBinaryRecord(set);
            set.RemoveWhere(value => (value & 7) == 0);
            CheckBinaryRecord(set);
        }
    }

    /// <summary>Verifies the count and element bytes in enumeration order across the set writers.</summary>
    /// <param name="set">The set to serialize.</param>
    private static void CheckBinaryRecord(HashSet<int> set)
    {
        byte[] expected = new byte[sizeof(int) + set.Count * sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(expected, set.Count);
        int index = sizeof(int);
        foreach (int value in set)
        {
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(index), value);
            index += sizeof(int);
        }

        Assert.Equal(expected, LuminPackSerializer.Serialize(set));
        Assert.Equal(expected, LuminPackSerializer.Serialize((ISet<int>)set));
        Assert.Equal(expected, LuminPackSerializer.Serialize((IReadOnlySet<int>)set));

        byte[] buffer = new byte[expected.Length + 2];
        buffer[0] = 0xA5;
        buffer[^1] = 0x5A;
        Span<byte> payload = buffer.AsSpan(1, expected.Length);
        var writer = new LuminPackWriter(ref payload);
        LuminPackMarshal.WriteHashSetRaw(ref writer, set);
        Assert.Equal(expected.Length, writer.Position);
        Assert.Equal(expected, payload.ToArray());
        Assert.Equal(0xA5, buffer[0]);
        Assert.Equal(0x5A, buffer[^1]);
    }
}
#endif
