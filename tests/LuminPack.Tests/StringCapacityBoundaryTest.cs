using LuminPack;
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
    /// <param name = "index">The invalid current position.</param>
    /// <param name = "count">The requested additional byte count.</param>
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
    /// <param name = "bytes">The destination with canaries surrounding its writable window.</param>
    /// <param name = "index">The invalid current position.</param>
    /// <param name = "count">The requested additional byte count.</param>
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
            string value = new string (Enumerable.Range(0, length).Select(index => "\0\uD800A\uDC00\uFFFF\u4E2D"[index % 6]).ToArray());
            byte[] expected = MemoryMarshal.AsBytes(value.AsSpan()).ToArray();
            foreach (int start in new[]
            {
                0,
                1,
                3,
                7,
                15
            }

            )
            {
                // Nonempty text whose code units all fit one byte uses the compact Latin-1 record.
                bool compact = value.Length != 0 && value.All(character => character <= 'ÿ');
                byte[] payload = compact ? value.Select(character => (byte)character).ToArray() : expected;
                int size = sizeof(int) + payload.Length;
                byte[] bytes = Enumerable.Repeat((byte)0xCD, start + size + 16).ToArray();
                Span<byte> destination = bytes.AsSpan(start, size);
                var writer = new LuminPackWriter(ref destination);
                writer.Write<LuminPackUtf16LengthOptions>(value);
                Assert.Equal(payload.Length + 4, writer.Position);
                Assert.Equal(compact ? (value.Length << 1) | 1 : expected.Length, BitConverter.ToInt32(bytes, start));
                Assert.True(bytes.AsSpan(start + sizeof(int), payload.Length).SequenceEqual(payload));
                Assert.False(bytes.AsSpan(0, start).ContainsAnyExcept((byte)0xCD));
                Assert.False(bytes.AsSpan(start + size).ContainsAnyExcept((byte)0xCD));
            }
        }
    }

    /// <summary>Writes beyond a fresh native buffer through computed, span, and supplied-length operations.</summary>
    /// <param name = "encoding">The payload encoding.</param>
    /// <param name = "recording">The string framing mode.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Token)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Token)]
    public void FreshBufferGrowthPreservesCompleteStringAndTail(LuminPackStringEncoding encoding, LuminPackStringRecording recording)
    {
        var option = new TestSerializationProfile
        {
            StringEncoding = encoding,
            StringRecording = recording
        };
        using var buffer = new LuminBufferWriter(true);
        string value = new string('x', buffer.TotalLength + 17) + "\u00e9\u4e2d\ud83c\udf1f";
        int expectedBytes = encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8.GetByteCount(value) : value.Length * 2;
        var writer = new LuminPackWriter(buffer);
        writer.WriteFor(option, value);
        int framing = recording == LuminPackStringRecording.Token ? encoding == LuminPackStringEncoding.UTF8 ? 1 : 2 : encoding == LuminPackStringEncoding.UTF8 ? 8 : 4;
        Assert.Equal(expectedBytes + framing, writer.Position);
        writer.Write(123456789);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        Assert.Equal(value, reader.ReadFor(option));
        Assert.Equal(123456789, reader.Read<int>());
        Assert.Equal(payload.Length, reader.Position);
    }

    /// <summary>Checks exact fixed destinations and rejects missing terminator/header bytes without corrupting canaries.</summary>
    [Fact]
    public void ExactAndEmptyDestinationsKeepHeaderAndTokenBounds()
    {
        foreach (var encoding in new[]
        {
            LuminPackStringEncoding.UTF8,
            LuminPackStringEncoding.UTF16
        }

        )
            foreach (var recording in new[]
            {
                LuminPackStringRecording.Length,
                LuminPackStringRecording.Token
            }

            )
                foreach (string text in new[]
                {
                    "",
                    "a",
                    "abc"
                }

                )
                {
                    var option = new TestSerializationProfile
                    {
                        StringEncoding = encoding,
                        StringRecording = recording
                    };
                    var state = new LuminPackWriterOptionalState();
                    int width = encoding == LuminPackStringEncoding.UTF8 ? 1 : 2;
                    int header = recording == LuminPackStringRecording.Token ? width : width == 1 ? 8 : 4;
                    // Nonempty ASCII text in a UTF-16 length record uses the compact one-byte form.
                    int payloadWidth = encoding == LuminPackStringEncoding.UTF16 && recording == LuminPackStringRecording.Length && text.Length != 0 ? 1 : width;
                    int total = text.Length * payloadWidth + header;
                    byte[] bytes = Enumerable.Repeat((byte)0xCC, total + 2).ToArray();
                    Span<byte> destination = bytes.AsSpan(1, total);
                    var writer = new LuminPackWriter(ref destination, state);
                    writer.WriteFor(option, text);
                    Assert.Equal(total, writer.Position);
                    Assert.Equal(0xCC, bytes[0]);
                    Assert.Equal(0xCC, bytes[^1]);
                    Assert.ThrowsAny<Exception>(() => WriteWithoutFramingSpace(text, total - 1, state, option));
                }
    }

    /// <summary>Attempts a write whose fixed span has no room for the complete string record.</summary>
    /// <param name = "text">The ASCII input.</param>
    /// <param name = "capacity">The insufficient record capacity.</param>
    /// <param name = "state">The reference-tracking state.</param>
    /// <param name = "option">The closed string-profile test row.</param>
    private static void WriteWithoutFramingSpace(string text, int capacity, LuminPackWriterOptionalState state, TestSerializationProfile option)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, capacity + 2).ToArray();
        Span<byte> destination = bytes.AsSpan(1, capacity);
        var writer = new LuminPackWriter(ref destination, state);
        try
        {
            writer.WriteFor(option, text);
        }
        finally
        {
            Assert.Equal(0xCC, bytes[0]);
            Assert.Equal(0xCC, bytes[^1]);
        }
    }
}
