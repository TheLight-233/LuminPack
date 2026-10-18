using System.Text;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects complete string payloads and terminators independently of pooled buffer capacity.</summary>
public class StringCapacityBoundaryTest
{
    /// <summary>Rejects invalid dynamic reservations before an unowned destination can be modified.</summary>
    /// <param name="index">The invalid current position.</param>
    /// <param name="count">The requested additional byte count.</param>
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, -1)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void InvalidReservationsPreserveTheEntireDestination(int index, int count)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCD, 18).ToArray();
        Assert.ThrowsAny<Exception>(() => ReserveInvalidDestination(bytes, index, count));
        Assert.All(bytes, value => Assert.Equal((byte)0xCD, value));
    }

    /// <summary>Attempts one invalid reservation through the public dynamic-capacity API.</summary>
    /// <param name="bytes">The destination with canaries surrounding its writable window.</param>
    /// <param name="index">The invalid current position.</param>
    /// <param name="count">The requested additional byte count.</param>
    private static void ReserveInvalidDestination(byte[] bytes, int index, int count)
    {
        Span<byte> destination = bytes.AsSpan(1, 16);
        var writer = new LuminPackWriter(ref destination);
        writer.CurrentIndex = index;
        writer.EnsureAdditionalCapacity(count);
    }

    /// <summary>Checks exact raw UTF-16 payload coverage across scalar/vector widths and unaligned destinations.</summary>
    [Fact]
    public void ShortUtf16CopiesPreserveRawCodeUnitsAndExactBoundaries()
    {
        for (int length = 0; length <= 256; length++)
        {
            string value = new string(Enumerable.Range(0, length).Select(index => "\0\uD800A\uDC00\uFFFF\u4E2D"[index % 6]).ToArray());
            byte[] expected = MemoryMarshal.AsBytes(value.AsSpan()).ToArray();
            foreach (int start in new[] { 0, 1, 3, 7, 15 })
            {
                int size = sizeof(int) + expected.Length;
                byte[] bytes = Enumerable.Repeat((byte)0xCD, start + size + 16).ToArray();
                Span<byte> destination = bytes.AsSpan(start, size);
                var writer = new LuminPackWriter(ref destination);
                Assert.Equal(expected.Length, writer.WriteUtf16WithLength(0, value));
                Assert.Equal(expected.Length, BitConverter.ToInt32(bytes, start));
                Assert.True(bytes.AsSpan(start + sizeof(int), expected.Length).SequenceEqual(expected));
                Assert.False(bytes.AsSpan(0, start).ContainsAnyExcept((byte)0xCD));
                Assert.False(bytes.AsSpan(start + size).ContainsAnyExcept((byte)0xCD));
            }
        }
    }

    /// <summary>Writes beyond a fresh native buffer through computed, span, and supplied-length operations.</summary>
    /// <param name="encoding">The payload encoding.</param>
    /// <param name="recording">The string framing mode.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Token)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Token)]
    public void FreshBufferGrowthPreservesCompleteStringAndTail(LuminPackStringEncoding encoding, LuminPackStringRecording recording)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, StringRecording = recording };
        for (int variant = 0; variant < 3; variant++)
        {
            using var buffer = new LuminBufferWriter(true);
            string value = new string('x', buffer.TotalLength + 17) + "\u00e9\u4e2d\ud83c\udf1f";
            int expectedBytes = encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8.GetByteCount(value) : value.Length * 2;
            var state = new LuminPackWriterOptionalState();
            var writer = new LuminPackWriter(buffer, state);
            int index = 0;
            if (variant == 2)
                writer.WriteStringFor(option,ref index, value, expectedBytes);
            else
                Assert.Equal(expectedBytes, variant == 0 ? writer.WriteStringFor(option,value) : writer.WriteStringFor(option,ref index, value.AsSpan()));
            Assert.Equal(0, index);
            writer.Advance(expectedBytes + writer.StringRecordLengthFor(option));
            writer.CheckBuffer();
            index = writer.GetCurrentSpanIndex();
            writer.Advance(writer.WriteUnmanaged(ref index, 123456789));
            ReadOnlySpan<byte> payload = writer.GetSpan();
            var readState = new LuminPackReaderOptionalState();
            var reader = new LuminPackReader(ref payload, readState);
            index = 0;
            Assert.Equal(value, reader.ReadStringAndAdvanceFor(option,ref index));
            Assert.Equal(123456789, BitConverter.ToInt32(payload[index..]));
            Assert.Equal(payload.Length - sizeof(int), index);
        }
    }

    /// <summary>Checks exact fixed destinations and rejects missing terminator/header bytes without corrupting canaries.</summary>
    [Fact]
    public void ExactAndEmptyDestinationsKeepHeaderAndTokenBounds()
    {
        foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
            foreach (var recording in new[] { LuminPackStringRecording.Length, LuminPackStringRecording.Token })
                foreach (string text in new[] { "", "a", "abc" })
                {
                    var option = new TestSerializationProfile { StringEncoding = encoding, StringRecording = recording };
                    var state = new LuminPackWriterOptionalState();
                    int width = encoding == LuminPackStringEncoding.UTF8 ? 1 : 2;
                    int header = recording == LuminPackStringRecording.Token ? width : width == 1 ? 8 : 4;
                    int total = text.Length * width + header;
                    byte[] bytes = Enumerable.Repeat((byte)0xCC, total + 2).ToArray();
                    Span<byte> destination = bytes.AsSpan(1, total);
                    var writer = new LuminPackWriter(ref destination, state);
                    Assert.Equal(text.Length * width, writer.WriteStringFor(option,text));
                    Assert.Equal(0xCC, bytes[0]);
                    Assert.Equal(0xCC, bytes[^1]);
                    Assert.ThrowsAny<Exception>(() => WriteWithoutFramingSpace(text, total - 1, state, option));
                }
    }

    /// <summary>Attempts a write whose fixed span has no room for the complete string record.</summary>
    /// <param name="text">The ASCII input.</param>
    /// <param name="capacity">The insufficient record capacity.</param>
    /// <param name="state">The reference-tracking state.</param>
    /// <param name="option">The closed string-profile test row.</param>
    private static void WriteWithoutFramingSpace(string text, int capacity, LuminPackWriterOptionalState state, TestSerializationProfile option)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, capacity + 2).ToArray();
        Span<byte> destination = bytes.AsSpan(1, capacity);
        var writer = new LuminPackWriter(ref destination, state);
        try { writer.WriteStringFor(option, text); }
        finally
        {
            Assert.Equal(0xCC, bytes[0]);
            Assert.Equal(0xCC, bytes[^1]);
        }
    }
}
