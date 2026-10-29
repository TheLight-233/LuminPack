using System.Buffers.Binary;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the compact Latin-1 form of UTF-16 length string records and the raw form it falls back to.</summary>
public sealed class CompactStringRecordTest
{
    /// <summary>Checks opt-in variable headers, exact-sized writes, payload guards and immutable reuse.</summary>
    [Fact]
    public void VarIntRecordsPreservePayloadsAndReuse()
    {
        VerifyVarIntRecords<LuminPackUtf16VarIntOptions>();
        VerifyVarIntRecords<LuminPackUtf8VarIntOptions>();
        Assert.Equal(new byte[] { 0 }, WriteVarInt<LuminPackUtf16VarIntOptions>(null));
        Assert.Equal(new byte[] { 1 }, WriteVarInt<LuminPackUtf16VarIntOptions>(""));
        Assert.Equal(new byte[] { 4, 97 }, WriteVarInt<LuminPackUtf16VarIntOptions>("a"));
        Assert.Equal(new byte[] { 3, 0x2D, 0x4E }, WriteVarInt<LuminPackUtf16VarIntOptions>("\u4E2D"));
        Assert.Equal(new byte[] { 0, 0 }, WriteVarInt<LuminPackUtf8VarIntOptions>(null));
        Assert.Equal(new byte[] { 1, 1 }, WriteVarInt<LuminPackUtf8VarIntOptions>(""));
        Assert.Equal(new byte[] { 2, 2, 97 }, WriteVarInt<LuminPackUtf8VarIntOptions>("a"));
        Assert.Equal(new byte[] { 3, 0, 0, 0, 97 }, LuminPackSerializer.Serialize("a"));
    }

    /// <summary>Checks noncanonical, truncated, overflowing and inconsistent variable headers before publication.</summary>
    [Fact]
    public void VarIntRecordsRejectInvalidHeadersBeforePublication()
    {
        foreach (byte[] bytes in new byte[][] { [], [128], [128, 0], [255, 255, 255, 255, 15], [128, 128, 128, 128, 128], [4] })
        {
            Assert.ThrowsAny<Exception>(() => ReadVarInt<LuminPackUtf16VarIntOptions>(bytes));
        }
        foreach (byte[] bytes in new byte[][] { [0], [0, 1], [1, 0], [2, 1, 97], [2, 3, 97], [2, 2, 255], [2, 2] })
            Assert.ThrowsAny<Exception>(() => ReadVarInt<LuminPackUtf8VarIntOptions>(bytes));
        foreach (byte[] bytes in new byte[][] { [128], [128, 0], [4] })
        {
            ReadOnlySpan<byte> input = bytes;
            var reader = new LuminPackReader(ref input);
            string? current = "previous";
            bool failed = false;
            try { reader.Read<LuminPackUtf16VarIntOptions>(ref current); }
            catch (Exception) { failed = true; }
            Assert.True(failed);
            Assert.Equal(0, reader.Position);
            Assert.Equal("previous", current);
        }
    }

    /// <summary>Compares exact sizes and guarded writes across scalar, vector and header boundaries.</summary>
    /// <typeparam name="TOptions">The variable-length encoding profile.</typeparam>
    private static void VerifyVarIntRecords<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var texts = new List<string?> { null, "", "a", "\0", "\u00E9", "\u4E2D", "\uD83D\uDE00" };
        foreach (int length in new[] { 2, 3, 7, 8, 15, 16, 31, 32, 42, 43, 62, 63, 64, 126, 127, 128, 8191, 8192, 16383, 16384 })
        {
            texts.Add(new string('a', length));
            texts.Add(new string('\u4E2D', length));
        }
        foreach (string? text in texts)
        {
            byte[] bytes = WriteVarInt<TOptions>(text);
            Assert.Equal(text, ReadVarInt<TOptions>(bytes));
            ReadOnlySpan<byte> input = bytes;
            var reader = new LuminPackReader(ref input);
            Assert.Same(text, reader.Read<TOptions>(text));
            Assert.Equal(bytes.Length, reader.Position);
            foreach (int cut in new[] { 0, 1, bytes.Length / 2, bytes.Length - 1 }.Distinct().Where(c => c >= 0 && c < bytes.Length))
                Assert.ThrowsAny<Exception>(() => ReadVarInt<TOptions>(bytes.AsSpan(0, cut).ToArray()));
        }
    }

    /// <summary>Writes into the exact evaluated size and checks bytes on both sides of the destination.</summary>
    /// <typeparam name="TOptions">The variable-length encoding profile.</typeparam>
    /// <param name="text">The nullable string.</param>
    /// <returns>The encoded record.</returns>
    private static byte[] WriteVarInt<TOptions>(string? text) where TOptions : struct, ILuminPackOptions
    {
        int size = 0;
        var evaluator = new LuminPackEvaluator(ref size);
        size = evaluator.GetStringLength<TOptions>(ref text);
        byte[] guarded = Enumerable.Repeat((byte)0xCD, size + 16).ToArray();
        Span<byte> output = guarded.AsSpan(8, size);
        var writer = new LuminPackWriter(ref output);
        writer.Write<TOptions>(text);
        Assert.Equal(size, writer.Position);
        Assert.All(guarded.Take(8).Concat(guarded.Skip(8 + size)), b => Assert.Equal((byte)0xCD, b));
        return output.ToArray();
    }

    /// <summary>Reads one complete variable-length string record.</summary>
    /// <typeparam name="TOptions">The variable-length encoding profile.</typeparam>
    /// <param name="bytes">The record bytes.</param>
    /// <returns>The decoded nullable string.</returns>
    private static string? ReadVarInt<TOptions>(byte[] bytes) where TOptions : struct, ILuminPackOptions
    {
        ReadOnlySpan<byte> input = bytes;
        var reader = new LuminPackReader(ref input);
        string? value = reader.Read<TOptions>((string?)null);
        Assert.Equal(bytes.Length, reader.Position);
        return value;
    }

    /// <summary>Returns Latin-1 and non-Latin-1 texts across every short-length kernel and longer vector paths.</summary>
    /// <returns>Texts of lengths 1 to 70, 255, 256, 1000 and 100,000.</returns>
    private static IEnumerable<string> Texts()
    {
        foreach (int length in Enumerable.Range(1, 70).Concat(new[] { 255, 256, 1000, 100_000 }))
        {
            yield return new string('x', length);
            yield return string.Create(length, 0, static (span, _) =>
            {
                for (int i = 0; i < span.Length; i++)
                    span[i] = (char)(i * 37 % 256);
            });
            // One character above U+00FF at the start, the middle and the end exercises a failed narrowing at
            // every block position.
            foreach (int at in new[] { 0, length / 2, length - 1 })
            {
                char[] chars = new string('\u00e9', length).ToCharArray();
                chars[at] = '\u0100';
                yield return new string(chars);
            }
        }
    }

    /// <summary>Checks exact framing, fresh reads and equal-storage reuse for both payload forms.</summary>
    [Fact]
    public void RecordsUseTheCompactFormExactlyForLatin1Text()
    {
        foreach (string text in Texts())
        {
            bool latin1 = text.All(c => c <= '\u00ff');
            byte[] bytes = LuminPackSerializer.Serialize(text);
            int header = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            if (latin1)
            {
                Assert.Equal((text.Length << 1) | 1, header);
                Assert.Equal(4 + text.Length, bytes.Length);
                Assert.Equal(text.Select(c => (byte)c), bytes.Skip(4));
            }
            else
            {
                Assert.Equal(text.Length * 2, header);
                Assert.True(MemoryMarshal.AsBytes(text.AsSpan()).SequenceEqual(bytes.AsSpan(4)));
            }

            string? fresh = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref fresh));
            Assert.Equal(text, fresh);
            string? existing = new string(text.ToCharArray());
            string? reused = existing;
            LuminPackSerializer.Deserialize(bytes, ref reused);
            Assert.Same(existing, reused);
        }
    }

    /// <summary>Checks every classification failure position around grouped vector tails against literal records.</summary>
    [Fact]
    public void ReservedLongRecordsPreserveLiteralBytesAndRawBoundCanaries()
    {
        foreach (int length in new[] { 63, 64, 65, 95, 96, 97, 127, 128, 129, 191, 192, 193, 255, 256, 257 })
        {
            char[] chars = Enumerable.Range(0, length).Select(index => (char)((index * 73 + 91) & 255)).ToArray();
            for (int changed = -1; changed < length; changed++)
            {
                char previous = changed < 0 ? default : chars[changed];
                if (changed >= 0) chars[changed] = changed % 2 == 0 ? '\u0100' : '\uD800';
                string text = new string(chars);
                bool compact = changed < 0;
                int rawBound = sizeof(int) + length * sizeof(char);
                int width = sizeof(int) + length * (compact ? 1 : sizeof(char));
                byte[] expected = new byte[width];
                BinaryPrimitives.WriteInt32LittleEndian(expected, compact ? length * 2 + 1 : length * 2);
                if (compact)
                    for (int index = 0; index < length; index++) expected[sizeof(int) + index] = (byte)chars[index];
                else
                    MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(expected.AsSpan(sizeof(int)));
                foreach (int offset in new[] { 0, 1, 15, 31, 63 })
                {
                    byte[] output = new byte[rawBound + 128];
                    output.AsSpan().Fill(0xCD);
                    int written = LuminPackWriter.WriteReservedUtf16Record(ref output[offset], text);
                    Assert.Equal(width, written);
                    Assert.True(output.AsSpan(offset, width).SequenceEqual(expected));
                    Assert.False(output.AsSpan(0, offset).ContainsAnyExcept((byte)0xCD));
                    Assert.False(output.AsSpan(offset + rawBound).ContainsAnyExcept((byte)0xCD));
                }
                if (changed >= 0) chars[changed] = previous;
            }
        }
    }

    /// <summary>Checks that a candidate differing in any single character is replaced.</summary>
    [Fact]
    public void ReuseRejectsEveryDifferingCharacter()
    {
        foreach (int length in new[] { 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 64, 300 })
        {
            string text = new string('a', length);
            byte[] bytes = LuminPackSerializer.Serialize(text);
            for (int at = 0; at < length; at++)
            {
                foreach (char other in new[] { 'b', '\u00e1', '\u0161' })
                {
                    char[] chars = text.ToCharArray();
                    chars[at] = other;
                    string? candidate = new string(chars);
                    LuminPackSerializer.Deserialize(bytes, ref candidate);
                    Assert.Equal(text, candidate);
                }
            }
        }
    }

    /// <summary>Checks that raw UTF-16 records written before the compact form still decode and reuse.</summary>
    [Fact]
    public void RawRecordsOfLatin1TextRemainReadable()
    {
        foreach (string text in new[] { "a", "abcd", "abcdefgh", new string('q', 33), "caf\u00e9" })
        {
            byte[] bytes = new byte[4 + text.Length * 2];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, text.Length * 2);
            MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(bytes.AsSpan(4));
            string? fresh = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref fresh));
            Assert.Equal(text, fresh);
            string? existing = new string(text.ToCharArray());
            string? reused = existing;
            LuminPackSerializer.Deserialize(bytes, ref reused);
            Assert.Same(existing, reused);
            byte[] raw = Wrap(bytes);
            CompactModel? model = null;
            LuminPackSerializer.Deserialize(raw, ref model);
            Assert.Equal(text, Assert.Single(model!.Names));
            var previous = model.Names[0];
            LuminPackSerializer.Deserialize(raw, ref model);
            Assert.Same(previous, model!.Names[0]);
        }
    }

    /// <summary>Checks null, empty and malformed headers, including truncated compact payloads.</summary>
    [Fact]
    public void HeadersAreValidatedBeforeThePayloadIsRead()
    {
        string? value = "previous";
        LuminPackSerializer.Deserialize(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, ref value);
        Assert.Null(value);
        value = "previous";
        LuminPackSerializer.Deserialize(new byte[] { 0, 0, 0, 0 }, ref value);
        Assert.Equal(string.Empty, value);
        value = "previous";
        LuminPackSerializer.Deserialize(new byte[] { 1, 0, 0, 0 }, ref value);
        Assert.Equal(string.Empty, value);
        foreach (byte[] malformed in new[]
        {
            new byte[] { 0xFD, 0xFF, 0xFF, 0xFF },
            new byte[] { 7, 0, 0, 0, (byte)'a', (byte)'b' },
            new byte[] { 0xFF, 0xFF, 0xFF, 0x7F, (byte)'a' },
            new byte[] { 8, 0, 0, 0, (byte)'a', 0, (byte)'b', 0 },
            new byte[] { 3, 0 },
        })
        {
            string? fresh = null;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(malformed, ref fresh));
            string? candidate = "ab";
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(malformed, ref candidate));
            Assert.ThrowsAny<Exception>(() => { CompactModel? model = null; LuminPackSerializer.Deserialize(Wrap(malformed), ref model); });
            Assert.ThrowsAny<Exception>(() => { var model = new CompactModel { Names = { "ab" } }; LuminPackSerializer.Deserialize(Wrap(malformed), ref model); });
        }
    }

    /// <summary>Checks that a destination sized for the compact record accepts it without growth.</summary>
    [Fact]
    public void ExactlySizedDestinationsHoldTheCompactRecord()
    {
        var state = new LuminPackWriterOptionalState();
        foreach (string text in new[] { "a", "abcdefg", new string('z', 16), new string('z', 300) })
        {
            Span<byte> destination = new byte[4 + text.Length];
            var writer = new LuminPackWriter(ref destination, state);
            writer.Write<LuminPackUtf16LengthOptions>(text);
            Assert.Equal(destination.Length, writer.Position);
            ReadOnlySpan<byte> input = destination;
            var reader = new LuminPackReader(ref input);
            Assert.Equal(text, reader.Read<LuminPackUtf16LengthOptions>((string?)null));
        }

        Assert.ThrowsAny<Exception>(() =>
        {
            Span<byte> local = new byte[5];
            var writer = new LuminPackWriter(ref local, state);
            writer.Write<LuminPackUtf16LengthOptions>("\u0100");
        });
    }

    /// <summary>Checks buffer growth, nested collection reuse and the size evaluator against the written bytes.</summary>
    [Fact]
    public void NestedRecordsGrowAndReuseThroughEveryReader()
    {
        var value = new CompactModel();
        foreach (string text in Texts().Take(200))
            value.Names.Add(text);
        value.Names.Add(null);
        value.Names.Add("");
        byte[] direct = LuminPackSerializer.Serialize(value);
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, buffer);
        Assert.Equal(direct, buffer.ToArrayAndReset());
        CompactModel? restored = null;
        LuminPackSerializer.Deserialize(direct, ref restored);
        Assert.Equal(value.Names, restored!.Names);
        var names = restored.Names.ToArray();
        LuminPackSerializer.Deserialize(direct, ref restored);
        for (int i = 0; i < names.Length; i++)
            Assert.Same(names[i], restored.Names[i]);
    }

    /// <summary>Checks that time zone snapshots written in the compact form still reuse cached zones.</summary>
    [Fact]
    public void TimeZoneSnapshotsRoundTripInTheCompactForm()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Compact/Zone", TimeSpan.FromHours(3), "Compact Zone", "Compact Standard");
        byte[] bytes = LuminPackSerializer.Serialize(zone);
        TimeZoneInfo? first = null, second = null;
        LuminPackSerializer.Deserialize(bytes, ref first);
        LuminPackSerializer.Deserialize(bytes, ref second);
        Assert.Equal(zone.ToSerializedString(), first!.ToSerializedString());
        Assert.Same(first, second);
    }

    /// <summary>Frames one malformed string record as the single element of a model list.</summary>
    /// <param name="record">The string record.</param>
    /// <returns>The model payload.</returns>
    private static byte[] Wrap(byte[] record)
    {
        byte[] model = LuminPackSerializer.Serialize(new CompactModel { Names = { "" } });
        // The empty element's four-byte record is the payload's final field.
        return model.AsSpan(0, model.Length - 4).ToArray().Concat(record).ToArray();
    }

    /// <summary>Holds reusable strings read through the generated list path.</summary>
    [LuminPackable]
    public sealed partial class CompactModel
    {
        /// <summary>Stores the strings.</summary>
        public List<string?> Names = new();
    }

    /// <summary>Checks that the allocating time-zone writer sizes its array to the exact compact or raw record.</summary>
    [Fact]
    public void TimeZoneSnapshotsAreExactlySized()
    {
        foreach (string display in new[] { "Display", "\u00C9t\u00E9", "\u4E2D\u6587" })
        {
            var zone = TimeZoneInfo.CreateCustomTimeZone("Compact/" + display.Length, TimeSpan.FromMinutes(345), display, "Standard");
            string text = zone.ToSerializedString();
            byte[] bytes = LuminPackSerializer.Serialize(zone);
            bool latin1 = text.All(c => c <= 'ÿ');
            Assert.Equal(1 + 4 + (latin1 ? text.Length : text.Length * 2), bytes.Length);
            TimeZoneInfo? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
            Assert.Equal(text, copy!.ToSerializedString());
        }
    }
}
