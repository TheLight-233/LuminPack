using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects complete chunked text records at variable-length write boundaries.</summary>
public class StringBuilderCapacityTest
{
    /// <summary>Reserves the entire text before copying chunks into a fresh native buffer.</summary>
    [Fact]
    public void ChunkedTextGrowsFreshBufferBeforeCopying()
    {
        var value = new StringBuilder();
        for (int index = 0; index < 91; index++)
            value.Append(new string((char)('a' + index % 26), 4093)).Append('\u4e2d');
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, buffer);
        Assert.True(buffer.TotalLength > 32 * 1024);
        byte[] bytes = LuminPackSerializer.Serialize(value);
        Assert.Equal(sizeof(int) + value.Length * sizeof(char), bytes.Length);
        Assert.Equal(value.ToString(), LuminPackSerializer.Deserialize<StringBuilder>(bytes).ToString());
        Assert.Equal(bytes, LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<StringBuilder>(bytes)));
    }

    /// <summary>Rejects insufficient caller storage before writing either the header or chunk bytes.</summary>
    /// <param name="capacity">The caller's writable byte count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(97)]
    public void ShortFixedSpanPreservesCanaries(int capacity)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCD, capacity + 2).ToArray();
        Assert.ThrowsAny<Exception>(() => WriteTooSmall(bytes, capacity));
        Assert.All(bytes, value => Assert.Equal((byte)0xCD, value));
    }

    /// <summary>Writes chunked text into a deliberately undersized span.</summary>
    /// <param name="bytes">The destination and surrounding canaries.</param>
    /// <param name="capacity">The writable range inside those canaries.</param>
    private static void WriteTooSmall(byte[] bytes, int capacity)
    {
        Span<byte> span = bytes.AsSpan(1, capacity);
        var writer = new LuminPackWriter(ref span);
        var value = new StringBuilder(new string('x', 50000));
        LuminPackSerializer.Serialize(ref writer, in value);
    }
}
