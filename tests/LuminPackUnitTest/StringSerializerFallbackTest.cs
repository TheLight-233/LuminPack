using System.Buffers;
using System.Text;
using System.Text.Unicode;
using LuminPack.Utility;

namespace LuminPackUnitTest;

internal static class StringSerializerFallbackTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(ValidUtf16MatchesPlatformTranscoder), ValidUtf16MatchesPlatformTranscoder);
        RunCase(results, nameof(InvalidUtf16MatchesPlatformTranscoder), InvalidUtf16MatchesPlatformTranscoder);
        RunCase(results, nameof(ValidUtf8MatchesPlatformTranscoder), ValidUtf8MatchesPlatformTranscoder);
        RunCase(results, nameof(InvalidUtf8MatchesPlatformTranscoder), InvalidUtf8MatchesPlatformTranscoder);
        RunCase(results, nameof(EncodingUtf8RoundTripsMatch), EncodingUtf8RoundTripsMatch);
        RunCase(results, nameof(EmojiFourByteSequenceIsAccepted), EmojiFourByteSequenceIsAccepted);
    }

    private static void ValidUtf16MatchesPlatformTranscoder()
    {
        string[] values =
        {
            string.Empty,
            "ASCII",
            "中文",
            "😀",
            "LuminPack-中文-😀-end",
            "aé中😀z",
        };

        foreach (string value in values)
        {
            int required = Encoding.UTF8.GetByteCount(value);
            foreach (int destinationLength in Capacities(required))
            foreach (bool finalBlock in new[] { false, true })
            foreach (bool replace in new[] { false, true })
                CompareFromUtf16(value, destinationLength, replace, finalBlock);
        }
    }

    private static void InvalidUtf16MatchesPlatformTranscoder()
    {
        string[] values =
        {
            "\uD83D",
            "\uDE00",
            "A\uD83D",
            "\uD83DA",
            "\uD83D\uD83D",
            "\uDE00A",
        };

        foreach (string value in values)
        foreach (int destinationLength in new[] { 0, 1, 2, 3, 4, 5, 6, 8 })
        foreach (bool finalBlock in new[] { false, true })
        foreach (bool replace in new[] { false, true })
            CompareFromUtf16(value, destinationLength, replace, finalBlock);
    }

    private static void ValidUtf8MatchesPlatformTranscoder()
    {
        string[] values =
        {
            string.Empty,
            "ASCII",
            "中文",
            "😀",
            "LuminPack-中文-😀-end",
            "aé中😀z",
        };

        foreach (string value in values)
        {
            byte[] source = Encoding.UTF8.GetBytes(value);
            foreach (int destinationLength in Capacities(value.Length))
            foreach (bool finalBlock in new[] { false, true })
            foreach (bool replace in new[] { false, true })
                CompareToUtf16(source, destinationLength, replace, finalBlock);
        }
    }

    private static void InvalidUtf8MatchesPlatformTranscoder()
    {
        byte[][] values =
        {
            [0x80],                         // continuation without leader
            [0xC0, 0x80],                   // overlong two-byte form
            [0xE0, 0x80, 0x80],             // overlong three-byte form
            [0xED, 0xA0, 0x80],             // UTF-8 encoded surrogate
            [0xF4, 0x90, 0x80, 0x80],       // > U+10FFFF
            [0xF5, 0x80, 0x80, 0x80],       // invalid four-byte leader
            [0xC2],                         // incomplete two-byte form
            [0xE4, 0xB8],                   // incomplete three-byte form
            [0xF0, 0x9F, 0x98],             // incomplete emoji
            [0xE2, 0x28, 0xA1],             // invalid second continuation
            [0xE2, 0x82, 0x41],             // invalid third continuation
            [0xF0, 0x9F, 0x41, 0x80],       // invalid third continuation
            [0xF0, 0x9F, 0x98, 0x41],       // invalid fourth continuation
            [0x41, 0xF0, 0x9F, 0x98],       // valid prefix + incomplete emoji
        };

        foreach (byte[] value in values)
        foreach (int destinationLength in new[] { 0, 1, 2, 3, 4, 5, 8 })
        foreach (bool finalBlock in new[] { false, true })
        foreach (bool replace in new[] { false, true })
            CompareToUtf16(value, destinationLength, replace, finalBlock);
    }

    private static void EncodingUtf8RoundTripsMatch()
    {
        const string value = "ASCII-中文-😀-é";
        byte[] expectedBytes = Encoding.UTF8.GetBytes(value);
        byte[] actualBytes = new byte[expectedBytes.Length];

        var encodeStatus = StringSerializer.SerializeAsUtf8(
            value, actualBytes, out int charsRead, out int bytesWritten, true, true);

        Assert(encodeStatus == OperationStatus.Done && charsRead == value.Length &&
               bytesWritten == expectedBytes.Length && actualBytes.SequenceEqual(expectedBytes),
            "Fallback UTF-16 to UTF-8 output differs from Encoding.UTF8.");

        char[] actualChars = new char[value.Length];
        var decodeStatus = StringSerializer.DeserializeFromUtf8(
            expectedBytes, actualChars, out int bytesRead, out int charsWritten, true, true);

        Assert(decodeStatus == OperationStatus.Done && bytesRead == expectedBytes.Length &&
               charsWritten == value.Length && new string(actualChars) == Encoding.UTF8.GetString(expectedBytes),
            "Fallback UTF-8 to UTF-16 output differs from Encoding.UTF8.");
    }

    private static void EmojiFourByteSequenceIsAccepted()
    {
        byte[] emoji = [0xF0, 0x9F, 0x98, 0x80];
        Span<char> destination = stackalloc char[2];

        var status = StringSerializer.DeserializeFromUtf8(
            emoji, destination, out int bytesRead, out int charsWritten, false, true);

        Assert(status == OperationStatus.Done && bytesRead == 4 && charsWritten == 2 &&
               destination.SequenceEqual("😀"),
            "A legal four-byte UTF-8 emoji was rejected by the fallback decoder.");
    }

    private static void CompareFromUtf16(
        string source,
        int destinationLength,
        bool replaceInvalidSequences,
        bool isFinalBlock)
    {
        byte[] expected = new byte[destinationLength];
        byte[] actual = new byte[destinationLength];

        var expectedStatus = Utf8.FromUtf16(
            source, expected, out int expectedRead, out int expectedWritten,
            replaceInvalidSequences, isFinalBlock);
        var actualStatus = StringSerializer.SerializeAsUtf8(
            source, actual, out int actualRead, out int actualWritten,
            replaceInvalidSequences, isFinalBlock);

        Assert(expectedStatus == actualStatus && expectedRead == actualRead &&
               expectedWritten == actualWritten &&
               expected.AsSpan(0, expectedWritten).SequenceEqual(actual.AsSpan(0, actualWritten)),
            $"FromUtf16 mismatch: source={Escape(source)}, dest={destinationLength}, " +
            $"replace={replaceInvalidSequences}, final={isFinalBlock}; " +
            $"expected={expectedStatus}/{expectedRead}/{expectedWritten}, " +
            $"actual={actualStatus}/{actualRead}/{actualWritten}.");
    }

    private static void CompareToUtf16(
        byte[] source,
        int destinationLength,
        bool replaceInvalidSequences,
        bool isFinalBlock)
    {
        char[] expected = new char[destinationLength];
        char[] actual = new char[destinationLength];

        var expectedStatus = Utf8.ToUtf16(
            source, expected, out int expectedRead, out int expectedWritten,
            replaceInvalidSequences, isFinalBlock);
        var actualStatus = StringSerializer.DeserializeFromUtf8(
            source, actual, out int actualRead, out int actualWritten,
            replaceInvalidSequences, isFinalBlock);

        Assert(expectedStatus == actualStatus && expectedRead == actualRead &&
               expectedWritten == actualWritten &&
               expected.AsSpan(0, expectedWritten).SequenceEqual(actual.AsSpan(0, actualWritten)),
            $"ToUtf16 mismatch: source={Convert.ToHexString(source)}, dest={destinationLength}, " +
            $"replace={replaceInvalidSequences}, final={isFinalBlock}; " +
            $"expected={expectedStatus}/{expectedRead}/{expectedWritten}, " +
            $"actual={actualStatus}/{actualRead}/{actualWritten}.");
    }

    private static IEnumerable<int> Capacities(int required)
    {
        yield return 0;
        if (required > 0)
            yield return required - 1;
        yield return required;
        yield return required + 1;
    }

    private static string Escape(string value)
        => string.Concat(value.Select(character => char.IsSurrogate(character)
            ? $"\\u{(int)character:X4}"
            : character.ToString()));

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
