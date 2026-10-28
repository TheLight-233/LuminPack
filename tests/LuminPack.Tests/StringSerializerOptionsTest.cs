using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies that compiled profiles preserve the public transcoder's bounded and streaming contract.</summary>
public class StringSerializerOptionsTest
{
    /// <summary>Compares both encodings across malformed input, partial buffers and streaming flags.</summary>
    [Fact]
    public void ProfilesPreserveLegacyResults()
    {
        Verify<LuminPackDefaultOptions>(SerializeMode.Utf16);
        Verify<LuminPackUtf8LengthOptions>(SerializeMode.Utf8);
        Verify<LuminPackUtf16LengthOptions>(SerializeMode.Utf16);
        Verify<LuminPackUtf8TokenOptions>(SerializeMode.Utf8);
        Verify<LuminPackUtf16TokenOptions>(SerializeMode.Utf16);
    }

    /// <summary>Checks encoding and decoding independently, including invalid byte sequences and guard regions.</summary>
    /// <typeparam name="TOptions">The closed profile under test.</typeparam>
    /// <param name="mode">The equivalent legacy encoding selector.</param>
    private static void Verify<TOptions>(SerializeMode mode) where TOptions : struct, ILuminPackOptions
    {
        foreach (string text in new[] { "", "ascii", "\u00E9ab\u00FF", new string('\u4E2D', 65), "\uD83D\uDE00", "\uD800", "\uDC00X" })
            foreach (bool replace in new[] { false, true })
                foreach (bool final in new[] { false, true })
                    for (int capacity = 0; capacity <= text.Length * 3 + 1; capacity++)
                    {
                        byte[] expected = new byte[capacity];
                        byte[] guarded = Enumerable.Repeat((byte)0xCC, capacity + 16).ToArray();
                        var status = StringSerializer.Serialize(text, expected, out int read, out int written, mode, replace, final);
                        var actual = StringSerializer.Serialize<TOptions>(text, guarded.AsSpan(8, capacity), out int actualRead, out int actualWritten, replace, final);
                        Assert.Equal(status, actual);
                        Assert.Equal(read, actualRead);
                        Assert.Equal(written, actualWritten);
                        Assert.True(expected.AsSpan(0, written).SequenceEqual(guarded.AsSpan(8, written)));
                        Assert.All(guarded.Take(8).Concat(guarded.Skip(capacity + 8)), value => Assert.Equal((byte)0xCC, value));
                    }

        foreach (byte[] bytes in new byte[][] { [], [0x41], [0xC3, 0xA9, 0x41], [0xF0, 0x9F, 0x98, 0x80], [0xC3], [0xFF, 0x80], [0, 0xD8, 0x41, 0] })
            foreach (bool replace in new[] { false, true })
                foreach (bool final in new[] { false, true })
                    for (int capacity = 0; capacity <= bytes.Length + 1; capacity++)
                    {
                        char[] expected = new char[capacity];
                        char[] guarded = Enumerable.Repeat('\uCCCC', capacity + 16).ToArray();
                        var status = StringSerializer.Deserialize(bytes, expected, out int read, out int written, mode, replace, final);
                        var actual = StringSerializer.Deserialize<TOptions>(bytes, guarded.AsSpan(8, capacity), out int actualRead, out int actualWritten, replace, final);
                        Assert.Equal(status, actual);
                        Assert.Equal(read, actualRead);
                        Assert.Equal(written, actualWritten);
                        Assert.True(expected.AsSpan(0, written).SequenceEqual(guarded.AsSpan(8, written)));
                        Assert.All(guarded.Take(8).Concat(guarded.Skip(capacity + 8)), value => Assert.Equal('\uCCCC', value));
                    }
    }
}
