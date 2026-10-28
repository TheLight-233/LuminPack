using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks two-byte UTF-8 reuse, strict validation and transitions to mixed text under the explicit UTF-8 length profile.</summary>
public class Utf8TwoByteReuseTest
{
    /// <summary>Checks equal strings retain identity and every changed character is detected around vector boundaries.</summary>
    [Fact]
    public void TwoByteRunsKeepOnlyEqualInstances()
    {
        foreach (int length in new[] { 7, 8, 9, 15, 16, 17, 31, 32, 33, 64, 129 })
        {
            char[] chars = Enumerable.Range(0, length).Select(i => (char)(0x80 + i * 37 % 0x780)).ToArray();
            string text = new(chars);
            byte[] record = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(text);
            string equal = new(chars);
            Assert.Same(equal, Read(record, equal));
            for (int index = 0; index < length; index++)
            {
                char original = chars[index];
                chars[index] = (char)(original ^ 1);
                string different = new(chars);
                Assert.Equal(text, Read(record, different));
                Assert.NotSame(different, Read(record, different));
                chars[index] = original;
            }
        }
    }

    /// <summary>Checks an average two-byte width does not assume that every character has a two-byte encoding.</summary>
    [Fact]
    public void MixedTailsUseTheValidatingFallback()
    {
        foreach (int prefix in new[] { 1, 7, 8, 9, 15, 16, 17, 32, 64 })
        {
            foreach (string tail in new[] { "a\u89D2", "a\uD83C\uDF1F\u89D2", "\u007F\u0800", "\u0080\u07FF" })
            {
                string text = new string('\u00E9', prefix) + tail;
                Assert.Equal(text.Length * 2, Encoding.UTF8.GetByteCount(text));
                byte[] record = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(text);
                string equal = new(text.ToCharArray());
                Assert.Same(equal, Read(record, equal));
                Assert.Equal(text, Read(record, new string('\u00E9', text.Length)));
            }
        }
    }

    /// <summary>Checks malformed leads and continuation bytes cannot pass an equality shortcut.</summary>
    [Fact]
    public void MalformedTwoByteRecordsAreRejected()
    {
        foreach (int length in new[] { 8, 9, 16, 17, 33, 65 })
        {
            string text = new('\u00E9', length);
            byte[] record = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(text);
            for (int offset = 8; offset < record.Length; offset++)
            {
                byte saved = record[offset];
                record[offset] = (offset & 1) == 0 ? (byte)0xC0 : (byte)0xFF;
                Assert.ThrowsAny<Exception>(() => Read(record, text));
                record[offset] = saved;
            }
        }
    }

    /// <summary>Reads a complete string record against existing storage.</summary>
    /// <param name="record">The complete binary record.</param>
    /// <param name="existing">The string eligible for reuse.</param>
    /// <returns>The resulting string.</returns>
    private static string? Read(byte[] record, string existing)
    {
        ReadOnlySpan<byte> span = record;
        var reader = new LuminPackReader(ref span);
        return reader.Read<LuminPackUtf8LengthOptions>(existing);
    }
}
