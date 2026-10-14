using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

public class AssemblyStringWriterTest
{
    [Fact]
    public void DirectUtf8LengthPathPreservesOffsetHeadersAndBounds()
    {
        for (int length = 3; length <= 33; length++)
        {
            string value = new('x', length);
            byte[] bytes = Enumerable.Repeat((byte)0xCC, length + 12).ToArray();
            Span<byte> span = bytes.AsSpan(1, length + 10);
            var writer = new LuminPackWriter(ref span);
            Assert.Equal(length, writer.WriteUtf8WithLength(2, value));
            Assert.Equal(length, BitConverter.ToInt32(bytes, 3));
            Assert.Equal(length, BitConverter.ToInt32(bytes, 7));
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(value), bytes.AsSpan(11, length).ToArray());
            Assert.All(bytes.Take(3).Concat(bytes.Skip(11 + length)), b => Assert.Equal((byte)0xCC, b));
        }
    }

    [Fact]
    public void FixedBufferShortageKeepsOutsideSentinelsIntact()
    {
        for (int length = 4; length <= 32; length++)
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, length + 9).ToArray();
            Assert.ThrowsAny<Exception>(() => WriteIntoInsufficientSpan(bytes, new string('x', length)));
            Assert.Equal((byte)0xCC, bytes[0]);
            Assert.Equal((byte)0xCC, bytes[^1]);
        }

        static void WriteIntoInsufficientSpan(byte[] bytes, string value)
        {
            Span<byte> span = bytes.AsSpan(1, bytes.Length - 2);
            var writer = new LuminPackWriter(ref span);
            writer.WriteUtf8WithLength(0, value);
        }
    }
}
