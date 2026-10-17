using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects complete string payloads and terminators independently of pooled buffer capacity.</summary>
public class StringCapacityBoundaryTest
{
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
        var option = new LuminPackSerializerOption { StringEncoding = encoding, StringRecording = recording };
        for (int variant = 0; variant < 3; variant++)
        {
            using var buffer = new LuminBufferWriter(true);
            string value = new string('x', buffer.TotalLength + 17) + "\u00e9\u4e2d\ud83c\udf1f";
            int expectedBytes = encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8.GetByteCount(value) : value.Length * 2;
            var state = new LuminPackWriterOptionalState();
            state.Init(option);
            var writer = new LuminPackWriter(buffer, state);
            int index = 0;
            if (variant == 2)
                writer.WriteString(ref index, value, expectedBytes);
            else
                Assert.Equal(expectedBytes, variant == 0 ? writer.WriteString(value) : writer.WriteString(ref index, value.AsSpan()));
            Assert.Equal(0, index);
            writer.Advance(expectedBytes + writer.StringRecordLength());
            writer.CheckBuffer();
            index = writer.GetCurrentSpanIndex();
            writer.Advance(writer.WriteUnmanaged(ref index, 123456789));
            ReadOnlySpan<byte> payload = writer.GetSpan();
            var readState = new LuminPackReaderOptionalState();
            readState.Init(option);
            var reader = new LuminPackReader(ref payload, readState);
            index = 0;
            Assert.Equal(value, reader.ReadStringAndAdvance(ref index));
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
                    var option = new LuminPackSerializerOption { StringEncoding = encoding, StringRecording = recording };
                    var state = new LuminPackWriterOptionalState();
                    state.Init(option);
                    int width = encoding == LuminPackStringEncoding.UTF8 ? 1 : 2;
                    int header = recording == LuminPackStringRecording.Token ? width : width == 1 ? 8 : 4;
                    int total = text.Length * width + header;
                    byte[] bytes = Enumerable.Repeat((byte)0xCC, total + 2).ToArray();
                    Span<byte> destination = bytes.AsSpan(1, total);
                    var writer = new LuminPackWriter(ref destination, state);
                    Assert.Equal(text.Length * width, writer.WriteString(text));
                    Assert.Equal(0xCC, bytes[0]);
                    Assert.Equal(0xCC, bytes[^1]);
                    Assert.ThrowsAny<Exception>(() => WriteWithoutFramingSpace(text, total - 1, state));
                }
    }

    /// <summary>Attempts a write whose fixed span has no room for the complete string record.</summary>
    /// <param name="text">The ASCII input.</param>
    /// <param name="capacity">The insufficient record capacity.</param>
    /// <param name="state">The selected string modes.</param>
    private static void WriteWithoutFramingSpace(string text, int capacity, LuminPackWriterOptionalState state)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, capacity + 2).ToArray();
        Span<byte> destination = bytes.AsSpan(1, capacity);
        var writer = new LuminPackWriter(ref destination, state);
        try { writer.WriteString(text); }
        finally
        {
            Assert.Equal(0xCC, bytes[0]);
            Assert.Equal(0xCC, bytes[^1]);
        }
    }
}
