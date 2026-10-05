using System.Buffers;
using System.Text.Unicode;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public class Utf8BmpTranscoderTest
{
    [Fact]
    public void BmpPrefixMatchesAllPlatformStatusAndBoundaryResults()
    {
        string prefix = new string('中', 40);
        foreach (string suffix in new[] { "", "ascii", "é", "😀", "\uD800", "\uDC00", "\uD800X", "\uD800\n\uDC00" })
        foreach (bool replace in new[] { false, true })
        foreach (bool final in new[] { false, true })
        {
            string source = prefix + suffix;
            for (int capacity = 0; capacity <= source.Length * 3 + 4; capacity++)
            {
                byte[] expected = new byte[capacity];
                byte[] guarded = Enumerable.Repeat((byte)0xCC, capacity + 32).ToArray();
                var baseline = Utf8.FromUtf16(source, expected, out int expectedRead, out int expectedWritten, replace, final);
                var actual = StringSerializer.Serialize(source, guarded.AsSpan(16, capacity), out int read, out int written,
                    replaceInvalidSequences: replace, isFinalBlock: final);
                Assert.Equal(baseline, actual);
                Assert.Equal(expectedRead, read);
                Assert.Equal(expectedWritten, written);
                Assert.True(expected.AsSpan(0, written).SequenceEqual(guarded.AsSpan(16, written)));
                Assert.All(guarded.Take(16).Concat(guarded.Skip(capacity + 16)), value => Assert.Equal((byte)0xCC, value));
            }
        }
    }
}
