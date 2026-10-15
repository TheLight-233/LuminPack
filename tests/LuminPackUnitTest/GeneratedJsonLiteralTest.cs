using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides generated json literal test functionality.</summary>
public class GeneratedJsonLiteralTest
{
    /// <summary>Verifies invalid word length and cursor are rejected before writing.</summary>
    [Fact]
    public void InvalidWordLengthAndCursorAreRejectedBeforeWriting()
    {
        foreach (int count in new[] { int.MinValue, -1, 0, 7, 17, int.MaxValue })
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, 32).ToArray();
            Assert.ThrowsAny<Exception>(() => Copy(bytes, count, 0));
            Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));
        }
        foreach (int cursor in new[] { -1, 33, int.MaxValue })
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, 32).ToArray();
            Assert.ThrowsAny<Exception>(() => Copy(bytes, 8, cursor));
            Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));
        }

        // <summary>Copies  to the supplied destination.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        // <param name="count">The number of entries involved in the operation.</param>
        // <param name="cursor">The cursor used by this operation.</param>
        static void Copy(byte[] bytes, int count, int cursor)
        {
            Span<byte> span = bytes;
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
            writer.CurrentIndex = cursor;
            writer.WriteGeneratedLiteral(0, 0, count);
        }
    }

    /// <summary>Verifies static literal copies exactly with offset and sentinels.</summary>
    /// <param name="utf16">The utf16 used by this operation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaticLiteralCopiesExactlyWithOffsetAndSentinels(bool utf16)
    {
        const string literal = ",\u0022\u540D\u5B57\u0022:";
        byte[] expected = utf16 ? Encoding.Unicode.GetBytes(literal) : Encoding.UTF8.GetBytes(literal);
        byte[] bytes = Enumerable.Repeat((byte)0xCC, expected.Length + 4).ToArray();
        Span<byte> span = bytes.AsSpan(1, expected.Length + 2);
        var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
        writer.Advance(2);
        writer.WriteGeneratedLiteral(BitConverter.ToUInt64(expected, 0),
            BitConverter.ToUInt64(expected, expected.Length - 8), expected.Length);
        Assert.Equal(expected, bytes.AsSpan(3, expected.Length).ToArray());
        Assert.All(bytes.Take(3).Concat(bytes.Skip(3 + expected.Length)), b => Assert.Equal((byte)0xCC, b));
    }

    /// <summary>Verifies fixed buffer capacity is checked before literal copy.</summary>
    /// <param name="utf16">The utf16 used by this operation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedBufferCapacityIsCheckedBeforeLiteralCopy(bool utf16)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 4).ToArray();
        Assert.ThrowsAny<Exception>(() => Copy(bytes, utf16));
        Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));

        // <summary>Copies  to the supplied destination.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        // <param name="utf16">The utf16 used by this operation.</param>
        static void Copy(byte[] bytes, bool utf16)
        {
            Span<byte> span = bytes.AsSpan(1, 2);
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
            writer.WriteGeneratedLiteral(0UL, 0UL, utf16 ? 16 : 8);
        }
    }
}
