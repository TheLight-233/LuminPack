using System.Buffers.Binary;
using System.Text;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides binary string hot path test functionality.</summary>
public class BinaryStringHotPathTest
{
    /// <summary>Uses different closed kernels over the same state without retaining a mutable mode snapshot.</summary>
    [Fact]
    public void WriterModeSnapshotsFollowOptionMutationsAndCopies()
    {
        var state = new LuminPackWriterOptionalState();
        foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
        foreach (var recording in new[] { LuminPackStringRecording.Length, LuminPackStringRecording.Token })
        {
            var profile = new TestSerializationProfile { StringEncoding = encoding, StringRecording = recording };
            Span<byte> destination = new byte[64];
            var writer = new LuminPackWriter(ref destination, state);
            Span<byte> expected = new byte[64];
            var original = new LuminPackWriter(ref expected, state);
            writer.WriteFor(profile, "abc");
            if (encoding == LuminPackStringEncoding.UTF16)
            {
                if (recording == LuminPackStringRecording.Token) original.Write<LuminPackUtf16TokenOptions>("abc");
                else original.Write<LuminPackUtf16LengthOptions>("abc");
            }
            else if (recording == LuminPackStringRecording.Token) original.Write<LuminPackUtf8TokenOptions>("abc");
            else original.Write<LuminPackUtf8LengthOptions>("abc");
            Assert.Equal(original.Position, writer.Position);
            Assert.True(destination.SequenceEqual(expected));
        }
    }

    /// <summary>Verifies both dispatch overloads preserve all modes.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    /// <param name="recording">The recording used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Token)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Token)]
    public void BothDispatchOverloadsPreserveAllModes(LuminPackStringEncoding encoding, LuminPackStringRecording recording)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, StringRecording = recording };
        var state = new LuminPackWriterOptionalState();
        var readState = new LuminPackReaderOptionalState();
        foreach (string? text in new[] { null, "", "a", "abcd1234", "\u4E2D\u6587\u00E9\uD83C\uDF1F" })
        {
            Span<byte> first = new byte[256], second = new byte[256];
            var writer1 = new LuminPackWriter(ref first, state);
            var writer2 = new LuminPackWriter(ref second, state);
            writer1.Advance(3);
            writer2.Advance(3);
            writer1.WriteFor(option, text);
            writer2.WriteFor(option, text);
            Assert.Equal(writer1.Position, writer2.Position);
            Assert.Equal(first.ToArray(), second.ToArray());
            var reader = new LuminPackReader(ref first, readState) { Position = 3 };
            Assert.Equal(recording == LuminPackStringRecording.Token ? text ?? "" : text, reader.ReadFor(option));
            Assert.Equal(writer1.Position, reader.Position);
        }
    }

    /// <summary>Verifies dispatch and direct writer match wire format at every short boundary.</summary>
    [Fact]
    public void DispatchAndDirectWriterMatchWireFormatAtEveryShortBoundary()
    {
        foreach (int length in Enumerable.Range(0, 70).Concat(new[] { 127, 128, 129, 257 }))
            foreach (string text in new[] { new string('x', length), new string('\u4e2d', length), string.Concat(Enumerable.Repeat("\u00e9\ud83c\udf1f", length)) })
                foreach (bool direct in new[] { false, true })
                {
                    var expected = Encoding.UTF8.GetBytes(text);
                    byte[] bytes = Enumerable.Repeat((byte)0xCC, expected.Length + 14).ToArray();
                    Span<byte> destination = bytes.AsSpan(1, expected.Length + 12);
                    var writer = new LuminPackWriter(ref destination);
                    int index = 4;
                    writer.Position = index;
                    // The library default is UTF-16; this checks the explicit UTF-8 length profile's two entries.
                    if (direct) writer.Write<LuminPackUtf8LengthOptions>(text); else writer.Write<LuminPackUtf8LengthOptions>(text, capacityReserved: false);
                    int written = writer.Position - index - 8;
                    Assert.Equal(expected.Length, written);
                    Assert.Equal(4, index);
                    Assert.Equal(expected.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(5)));
                    Assert.Equal(text.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(9)));
                    Assert.Equal(expected, bytes.AsSpan(13, written).ToArray());
                    Assert.All(bytes.Take(5).Concat(bytes.Skip(13 + written)), b => Assert.Equal((byte)0xCC, b));
                    ReadOnlySpan<byte> input = bytes.AsSpan(1, expected.Length + 12);
                    var reader = new LuminPackReader(ref input);
                    reader.Position = index;
                    Assert.Equal(text, reader.Read<LuminPackUtf8LengthOptions>((string?)null));
                    Assert.Equal(12 + written, reader.Position);
                }
    }

    /// <summary>Verifies slow decoder preserves unknown length and rejects forged length without advancing.</summary>
    [Fact]
    public void SlowDecoderPreservesUnknownLengthAndRejectsForgedLengthWithoutAdvancing()
    {
        foreach (string text in new[] { "abc", "abcdefgh", new string('a', 69), "\u4E2D\u6587\u00E9\uD83C\uDF1F" })
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            foreach (int declared in new[] { 0, -1, text.Length, text.Length + 1, int.MaxValue })
            {
                byte[] bytes = new byte[8 + payload.Length];
                BinaryPrimitives.WriteInt32LittleEndian(bytes, payload.Length);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), declared);
                payload.CopyTo(bytes, 8);
                if (declared <= 0 || declared == text.Length) Assert.Equal(text, Read(bytes));
                else Assert.ThrowsAny<Exception>(() => Read(bytes));
            }
        }

        // <summary>Reads  from the current input.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        // <returns>The result of the operation.</returns>
        static string? Read(byte[] bytes)
        {
            ReadOnlySpan<byte> input = bytes;
            var reader = new LuminPackReader(ref input);
            try { return reader.Read<LuminPackUtf8LengthOptions>((string?)null); }
            catch { Assert.Equal(0, reader.Position); throw; }
        }
    }

    /// <summary>Preserves exact payloads and destination boundaries for compact Latin-1 and raw UTF-16 records.</summary>
    [Fact]
    public void ComputedUtf16LengthsCopyExactRawCodeUnits()
    {
        var options = new TestSerializationProfile { StringEncoding = LuminPackStringEncoding.UTF16 };
        var state = new LuminPackWriterOptionalState();
        foreach (string text in Enumerable.Range(0, 70).Select(length => new string('x', length))
            .Concat(Enumerable.Range(1, 70).Select(length => new string('\u4e2d', length)))
            .Concat(new[] { "\uD800", "\uDC00", "first\0last", "\u4e2d\ud83c\udf1f\u00e9", new string('y', 100_000), new string('\u4e2e', 100_000) }))
        {
            bool compact = text.Length > 0 && text.All(c => c <= '\u00ff');
            int payloadLength = text.Length * (compact ? 1 : sizeof(char));
            byte[] bytes = Enumerable.Repeat((byte)0xCC, text.Length * sizeof(char) + 6).ToArray();
            Span<byte> destination = bytes.AsSpan(1, text.Length * sizeof(char) + sizeof(int));
            var writer = new LuminPackWriter(ref destination, state);
            writer.Write<LuminPackUtf16LengthOptions>(text);
            int written = writer.Position - 4;
            Assert.Equal(payloadLength, written);
            Assert.Equal(compact ? (text.Length << 1) | 1 : payloadLength, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(1)));
            if (compact)
                Assert.True(text.Select(c => (byte)c).SequenceEqual(bytes.Skip(5).Take(payloadLength)));
            else
                Assert.True(MemoryMarshal.AsBytes(text.AsSpan()).SequenceEqual(bytes.AsSpan(5, payloadLength)));
            Assert.Equal(0xCC, bytes[0]);
            Assert.Equal(0xCC, bytes[^1]);
        }
    }

    /// <summary>Verifies growth and invalid utf16 use the original fallback.</summary>
    [Fact]
    public void GrowthAndInvalidUtf16UseTheOriginalFallback()
    {
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            foreach (string text in new[] { new string('x', 200_000), new string('\u4e2d', 100_000), "short_after_growth" })
            {
                var writer = new LuminPackWriter(buffer);
                writer.Write<LuminPackUtf8LengthOptions>(text);
                int written = writer.Position - 8;
                Assert.Equal(Encoding.UTF8.GetByteCount(text), written);
                ReadOnlySpan<byte> input = writer.GetSpan();
                var reader = new LuminPackReader(ref input);
                Assert.Equal(text, reader.Read<LuminPackUtf8LengthOptions>((string?)null));
                Assert.Equal(input.Length, reader.Position);
            }
        }
        finally { LuminBufferWriterPool.Return(buffer); }

        foreach (string invalid in new[] { "\uD800", "abcd\uD800efgh", "\uDC00" + new string('x', 70) })
            Assert.ThrowsAny<Exception>(() => WriteInvalid(invalid));

        // <summary>Writes invalid to the current output.</summary>
        // <param name="value">The value consumed or produced by this operation.</param>
        static void WriteInvalid(string value)
        {
            Span<byte> span = new byte[512];
            var writer = new LuminPackWriter(ref span);
            writer.Write<LuminPackUtf8LengthOptions>(value);
        }
    }
}
