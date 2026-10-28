using System.Buffers;
using System.Text.Json;
using System.Text.Unicode;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides short ascii transcoder test functionality.</summary>
public class ShortAsciiTranscoderTest
{
    /// <summary>Verifies empty and null utf8 headers overwrite both words in reused storage.</summary>
    [Fact]
    public void EmptyAndNullUtf8HeadersOverwriteBothWordsInReusedStorage()
    {
        foreach (string? value in new string?[] { null, string.Empty })
        {
            byte[] storage = Enumerable.Repeat((byte)0xCC, 10).ToArray();
            Span<byte> span = storage.AsSpan(1, 8);
            var writer = new LuminPackWriter(ref span);
            writer.Write<LuminPackUtf8LengthOptions>(value);
            int expected = value is null ? -1 : 0;
            Assert.Equal(expected, BitConverter.ToInt32(storage, 1));
            Assert.Equal(expected, BitConverter.ToInt32(storage, 5));
            Assert.Equal(0xCC, storage[0]);
            Assert.Equal(0xCC, storage[^1]);
        }
    }

    /// <summary>Verifies short binary decoder rejects forged ascii headers and truncation.</summary>
    [Fact]
    public void ShortBinaryDecoderRejectsForgedAsciiHeadersAndTruncation()
    {
        for (int length = 1; length <= 32; length++)
        {
            byte[] payload = new byte[8 + length];
            BitConverter.GetBytes(length).CopyTo(payload, 0);
            BitConverter.GetBytes(length).CopyTo(payload, 4);
            payload.AsSpan(8).Fill((byte)'x');
            Assert.Equal(new string('x', length), Read(payload));

            for (int position = 0; position < length; position++)
            {
                payload[8 + position] = 0xFF;
                Assert.ThrowsAny<Exception>(() => Read(payload));
                payload[8 + position] = (byte)'x';
            }

            // Valid non-ASCII UTF-8 still disagrees with the forged UTF-16 length.
            if (length >= 2)
            {
                payload[8] = 0xC3;
                payload[9] = 0xA9;
                Assert.ThrowsAny<Exception>(() => Read(payload));
            }
            payload.AsSpan(8).Fill((byte)'x');
            for (int prefix = 0; prefix < payload.Length; prefix++)
            {
                var truncated = payload.AsSpan(0, prefix).ToArray();
                Assert.ThrowsAny<Exception>(() => Read(truncated));
            }
        }

        // <summary>Reads  from the current input.</summary>
        // <param name="payload">The payload used by this operation.</param>
        // <returns>The result of the operation.</returns>
        static string? Read(byte[] payload)
        {
            ReadOnlySpan<byte> span = payload;
            var reader = new LuminPackReader(ref span);
            try { return reader.Read<LuminPackUtf8LengthOptions>((string?)null); }
            catch { Assert.Equal(0, reader.Position); throw; }
        }
    }

    /// <summary>Verifies short strings preserve transcoder status and boundaries.</summary>
    [Fact]
    public void ShortStringsPreserveTranscoderStatusAndBoundaries()
    {
        for (int length = 3; length <= 33; length++)
            for (int position = 0; position < length; position++)
                foreach (char inserted in new[] { 'A', '\0', '\u007f', 'é', '\u4e2d', '\uD800', '\uDC00' })
                    foreach (bool replace in new[] { true, false })
                        foreach (bool final in new[] { true, false })
                            foreach (int capacity in new[] { 0, length - 1, length, length * 3 })
                            {
                                var chars = new string('x', length).ToCharArray();
                                chars[position] = inserted;
                                byte[] actual = Enumerable.Repeat((byte)0xCC, capacity + 2).ToArray();
                                byte[] expected = new byte[capacity];
                                var expectedStatus = Utf8.FromUtf16(chars, expected, out int expectedRead, out int expectedWritten, replace, final);
                                var status = StringSerializer.Serialize(chars, actual.AsSpan(1, capacity), out int read, out int written, SerializeMode.Utf8,
                                    replaceInvalidSequences: replace, isFinalBlock: final);
                                Assert.Equal(expectedStatus, status);
                                Assert.Equal(expectedRead, read);
                                Assert.Equal(expectedWritten, written);
                                Assert.True(expected.AsSpan(0, written).SequenceEqual(actual.AsSpan(1, written)));
                                Assert.Equal(0xCC, actual[0]);
                                Assert.Equal(0xCC, actual[^1]);
                            }
    }

    /// <summary>Verifies every ascii character escapes and roundtrips at every vector position.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void EveryAsciiCharacterEscapesAndRoundtripsAtEveryVectorPosition(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, ValidateJsonSyntax = true };
        for (int length = 1; length <= 32; length++)
            for (int position = 0; position < length; position++)
                for (int code = 0; code < 128; code++)
                {
                    var chars = new string('a', length).ToCharArray();
                    chars[position] = (char)code;
                    string value = new(chars);
                    string json = LuminPackSerializer.SerializeJson(value, option);
                    using var document = JsonDocument.Parse(json);
                    Assert.Equal(value, document.RootElement.GetString());
                    Assert.Equal(value, LuminPackSerializer.DeserializeJson<string>(json, option));
                    Assert.Equal(value, LuminPackSerializer.Deserialize<string>(LuminPackSerializer.Serialize(value, option), option));
                }
    }
}
