using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the tiny, long-ASCII and short-Unicode UTF-8 string kernels against the platform encoder.</summary>
public class Utf8StringKernelTest
{
    /// <summary>Checks exact record bytes and round trips for ASCII and mixed text around every block boundary.</summary>
    [Fact]
    public void EncodedRecordsMatchThePlatformEncoder()
    {
        string[] inserts = { "\u00E9", "\u89D2", "\uD834\uDD1E" };
        for (int length = 1; length <= 160; length++)
        {
            Check(new string((char)('a' + length % 26), length));
            if (length <= 32) continue;
            for (int position = 0; position < length; position += position < 40 ? 1 : 7)
                foreach (string insert in inserts)
                {
                    var builder = new StringBuilder(new string('x', length));
                    builder.Remove(position, 1).Insert(position, insert);
                    Check(builder.ToString());
                }
        }

        // <summary>Compares an explicit UTF-8 record with UTF-8 bytes and UTF-16 count, then round trips it.</summary>
        // <param name="text">The text being written.</param>
        static void Check(string text)
        {
            byte[] record = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(text);
            byte[] expected = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected.Length, BitConverter.ToInt32(record, 0));
            Assert.Equal(text.Length, BitConverter.ToInt32(record, 4));
            Assert.Equal(expected, record.AsSpan(8).ToArray());
            Assert.Equal(text, LuminPackSerializer.Deserialize<string, LuminPackUtf8LengthOptions>(record));
        }
    }

    /// <summary>Checks a lone surrogate after a long ASCII prefix fails exactly like the unchanged short path.</summary>
    [Fact]
    public void LongAsciiPrefixKeepsInvalidSurrogateFailures()
    {
        var shortFailure = Record.Exception(() => LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>("ab\uD800"));
        Assert.NotNull(shortFailure);
        foreach (int length in new[] { 33, 40, 64, 65, 100 })
            for (int position = 0; position < length; position += 3)
            {
                string text = new string('a', position) + "\uD800" + new string('b', length - position - 1);
                var failure = Record.Exception(() => LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(text));
                Assert.NotNull(failure);
                Assert.Equal(shortFailure!.GetType(), failure!.GetType());
            }
    }

    /// <summary>Checks forged equal-count headers with malformed long payloads fail without publishing the cursor.</summary>
    [Fact]
    public void LongEqualCountRecordsRejectMalformedBytes()
    {
        foreach (int length in new[] { 33, 48, 63, 64, 65, 97 })
        {
            byte[] payload = new byte[8 + length];
            BitConverter.GetBytes(length).CopyTo(payload, 0);
            BitConverter.GetBytes(length).CopyTo(payload, 4);
            payload.AsSpan(8).Fill((byte)'q');
            Assert.Equal(new string('q', length), Read(payload));
            for (int position = 0; position < length; position++)
            {
                payload[8 + position] = 0xC3;
                Assert.ThrowsAny<Exception>(() => Read(payload));
                payload[8 + position] = (byte)'q';
            }
        }

        // <summary>Reads one record and requires an unchanged cursor after a failure.</summary>
        // <param name="payload">The complete record.</param>
        // <returns>The decoded text.</returns>
        static string? Read(byte[] payload)
        {
            ReadOnlySpan<byte> span = payload;
            var reader = new LuminPackReader(ref span);
            try { return reader.Read<LuminPackUtf8LengthOptions>((string?)null); }
            catch { Assert.Equal(0, reader.Position); throw; }
        }
    }

    /// <summary>Checks short Unicode reuse keeps equal instances, replaces different ones and rejects malformed records.</summary>
    [Fact]
    public void ShortUnicodeReuseComparesDecodedText()
    {
        string original = "\u89D2\u8272_00042_\u88C5\u5907_\uD83C\uDF1F";
        byte[] record = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(original);
        string current = new(original.ToCharArray());
        Assert.Same(current, Read(record, current));
        string changed = "\u89D2\u8272_00043_\u88C5\u5907_\uD83C\uDF1F";
        Assert.Equal(original, Read(record, changed));
        Assert.NotSame(changed, Read(record, changed));
        byte[] malformed = (byte[])record.Clone();
        malformed[8] = 0xFF;
        Assert.ThrowsAny<Exception>(() => Read(malformed, current));

        // <summary>Reads one record against the supplied existing string.</summary>
        // <param name="bytes">The complete record.</param>
        // <param name="existing">The string available for reuse.</param>
        // <returns>The resulting string.</returns>
        static string? Read(byte[] bytes, string existing)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            return reader.Read<LuminPackUtf8LengthOptions>(existing);
        }
    }
}
