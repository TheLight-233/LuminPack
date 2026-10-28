using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the inline fresh-string read paths against the shared record reader, including every declining record.</summary>
public class StringRecordFastPathTest
{
    private static readonly string?[] Values =
    {
        null, "", "a", "ab", "abc", "abcd", "abcdefgh", "abcdefghijklmnop", "abcdefghijklmnopq", new string('x', 40),
        "caf\u00E9", "\u89D2\u8272\u540D\u79F0", "\uD83C\uDF1F star", "mixed ascii and \u00FC"
    };

    /// <summary>Checks fresh reads of every length class return the written text and consume exactly the written record.</summary>
    [Fact]
    public void FreshReadsMatchTheWrittenRecords()
    {
        foreach (string? value in Values)
        {
            Assert.Equal(value, RoundTrip<LuminPackDefaultOptions>(value, out bool utf8Exact));
            Assert.True(utf8Exact);
            Assert.Equal(value, RoundTrip<LuminPackUtf16LengthOptions>(value, out bool utf16Exact));
            Assert.True(utf16Exact);
        }
    }

    /// <summary>Checks an equal reused string still keeps its instance; the fast paths apply only to fresh reads.</summary>
    [Fact]
    public void ReusedStringsKeepTheirInstance()
    {
        string current = "abcdefgh";
        Assert.Same(current, ReadWithCurrent<LuminPackDefaultOptions>(Write<LuminPackDefaultOptions>("abcdefgh"), current));
        Assert.Same(current, ReadWithCurrent<LuminPackUtf16LengthOptions>(Write<LuminPackUtf16LengthOptions>("abcdefgh"), current));
    }

    /// <summary>Checks malformed, odd and truncated records decline the fast paths and keep the shared reader's failures.</summary>
    [Fact]
    public void DecliningRecordsKeepTheirFailures()
    {
        // An equal byte and character count with a non-ASCII byte is malformed UTF-8.
        byte[] malformed = Header(5, 5).Concat(new byte[] { (byte)'a', (byte)'b', 0xC3, (byte)'d', (byte)'e' }).ToArray();
        Assert.ThrowsAny<Exception>(() => ReadFresh<LuminPackUtf8LengthOptions>(malformed));
        byte[] truncated = Header(10, 10).Concat(new byte[] { 1, 2, 3, 4, 5 }).ToArray();
        Assert.ThrowsAny<Exception>(() => ReadFresh<LuminPackUtf8LengthOptions>(truncated));

        // An odd UTF-16 header is a compact Latin-1 record; its payload must still be complete.
        byte[] compact = BitConverter.GetBytes((8 << 1) | 1).Concat(new byte[] { 97, 98, 99, 100, 101 }).ToArray();
        Assert.ThrowsAny<Exception>(() => ReadFresh<LuminPackUtf16LengthOptions>(compact));
        Assert.ThrowsAny<Exception>(() => ReadFresh<LuminPackDefaultOptions>(compact));
        Assert.ThrowsAny<Exception>(() => ReadFresh<LuminPackUtf16LengthOptions>(BitConverter.GetBytes(-3)));
        byte[] truncatedUtf16 = BitConverter.GetBytes(20).Concat(new byte[] { 97, 0, 98, 0, 99, 0 }).ToArray();
        Assert.ThrowsAny<Exception>(() => ReadFresh<LuminPackUtf16LengthOptions>(truncatedUtf16));
    }

    /// <summary>Builds a UTF-8 length header.</summary>
    /// <param name="bytes">The payload byte count.</param>
    /// <param name="characters">The UTF-16 character count.</param>
    /// <returns>The eight header bytes.</returns>
    private static byte[] Header(int bytes, int characters) => BitConverter.GetBytes(bytes).Concat(BitConverter.GetBytes(characters)).ToArray();

    /// <summary>Writes one string record.</summary>
    /// <typeparam name="TOptions">The string profile.</typeparam>
    /// <param name="value">The string.</param>
    /// <returns>The record bytes.</returns>
    private static byte[] Write<TOptions>(string? value) where TOptions : struct, ILuminPackOptions
    {
        Span<byte> bytes = new byte[512];
        var writer = new LuminPackWriter(ref bytes);
        writer.Write<TOptions>(value);
        return bytes[..writer.Position].ToArray();
    }

    /// <summary>Writes and freshly reads one record, checking the consumed width.</summary>
    /// <typeparam name="TOptions">The string profile.</typeparam>
    /// <param name="value">The string.</param>
    /// <param name="exact">Receives whether the read consumed exactly the written record.</param>
    /// <returns>The read string.</returns>
    private static string? RoundTrip<TOptions>(string? value, out bool exact) where TOptions : struct, ILuminPackOptions
    {
        byte[] record = Write<TOptions>(value);
        ReadOnlySpan<byte> payload = record;
        var reader = new LuminPackReader(ref payload);
        string? result = reader.Read<TOptions>((string?)null);
        exact = reader.Position == record.Length;
        return result;
    }

    /// <summary>Freshly reads one record.</summary>
    /// <typeparam name="TOptions">The string profile.</typeparam>
    /// <param name="record">The record bytes.</param>
    /// <returns>The read string.</returns>
    private static string? ReadFresh<TOptions>(byte[] record) where TOptions : struct, ILuminPackOptions => ReadWithCurrent<TOptions>(record, null);

    /// <summary>Reads one record with an available current string.</summary>
    /// <typeparam name="TOptions">The string profile.</typeparam>
    /// <param name="record">The record bytes.</param>
    /// <param name="current">The string available for reuse.</param>
    /// <returns>The read string.</returns>
    private static string? ReadWithCurrent<TOptions>(byte[] record, string? current) where TOptions : struct, ILuminPackOptions
    {
        ReadOnlySpan<byte> payload = record;
        var reader = new LuminPackReader(ref payload);
        return reader.Read<TOptions>(current);
    }
}
