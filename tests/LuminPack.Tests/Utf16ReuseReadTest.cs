using System.Buffers.Binary;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects validated UTF-16 materialization, immutable reuse and generated reference graphs.</summary>
public sealed class Utf16ReuseReadTest
{
    /// <summary>Compares every code unit across scalar, vector and larger complete payload boundaries.</summary>
    /// <param name="length">The number of UTF-16 code units in the payload.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(2048)]
    public void EqualAndChangedStringsPreserveAllCodeUnits(int length)
    {
        string text = new(Enumerable.Range(0, length).Select(index => (char)(index * 7919)).ToArray());
        byte[] record = Record(text);
        string same = new(text.ToCharArray());
        CheckRecord<LuminPackUtf16LengthOptions>(record, text, same, reuse: true);
        CheckRecord<LuminPackDefaultOptions>(record, text, same, reuse: true);
        CheckRecord<LuminPackUtf16LengthOptions>(record, text, null, reuse: false);
        CheckRecord<LuminPackUtf16LengthOptions>(record, text, string.Empty, reuse: false);
        CheckRecord<LuminPackUtf16LengthOptions>(record, text, text + "x", reuse: false);
        foreach (int index in length <= 16 ? Enumerable.Range(0, length) : new[] { 0, length / 2, length - 1 }.Distinct())
        {
            char[] different = text.ToCharArray();
            different[index] ^= (char)0x8000;
            CheckRecord<LuminPackUtf16LengthOptions>(record, text, new string(different), reuse: false);
        }
    }

    /// <summary>Keeps null and empty record normalization and the exact completed byte count.</summary>
    [Fact]
    public void NullAndEmptyKeepTheirNormalization()
    {
        foreach (string? current in new string?[] { null, "", "previous" })
        {
            var absent = new byte[] { 255, 255, 255, 255 };
            Assert.Null(TryRead<LuminPackUtf16LengthOptions>(absent, 0, absent.Length, current, out var result, out int position));
            Assert.Null(result);
            Assert.Equal(4, position);
            Assert.Null(TryRead<LuminPackUtf16LengthOptions>(new byte[4], 0, 4, current, out result, out position));
            Assert.Same(string.Empty, result);
            Assert.Equal(4, position);
        }
    }

    /// <summary>Rejects every incomplete matching record before exposing a new cursor or string.</summary>
    [Fact]
    public void TruncatedAndMalformedRecordsDoNotAdvance()
    {
        foreach (int length in new[] { 1, 7, 8, 9, 16, 17, 32, 33, 129 })
        {
            string text = new('x', length);
            byte[] record = Record(text);
            for (int prefix = 0; prefix < record.Length; prefix++)
            {
                Assert.NotNull(TryRead<LuminPackUtf16LengthOptions>(record, 0, prefix, text, out var result, out int position));
                Assert.Null(result);
                Assert.Equal(0, position);
            }
        }
        // Odd headers are compact Latin-1 records, so only payloads exceeding the sixteen available bytes are incomplete.
        foreach (int header in new[] { int.MinValue, -3, -2, 18, (17 << 1) | 1, int.MaxValue, int.MaxValue - 1 })
        {
            var record = new byte[20];
            BinaryPrimitives.WriteInt32LittleEndian(record, header);
            Assert.NotNull(TryRead<LuminPackUtf16LengthOptions>(record, 0, record.Length, "previous", out _, out int position));
            Assert.Equal(0, position);
        }
        foreach (int length in new[] { 0, 1, 16 })
        {
            var record = new byte[20];
            BinaryPrimitives.WriteInt32LittleEndian(record, (length << 1) | 1);
            record.AsSpan(4, length).Fill(0xE9);
            Assert.Null(TryRead<LuminPackUtf16LengthOptions>(record, 0, record.Length, "previous", out var result, out int position));
            Assert.Equal(new string('é', length), result);
            Assert.Equal(4 + length, position);
        }
    }

    /// <summary>Retains object, child, property, list and dictionary identities across complete generated reads.</summary>
    [Fact]
    public void GeneratedReferenceGraphKeepsNestedReuseAndFreshDefaults()
    {
        var source = new Utf16ReusePayload
        {
            Name = "ordinary property",
            Child = new Utf16ReuseChild { Text = "\u4e2d\u6587-child" },
            Tags = new List<string> { "small", "a longer string containing more than sixteen code units", "\u00e9clair" },
            Map = new Dictionary<string, string> { ["key"] = "value" }
        };
        byte[] record = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(source);
        Utf16ReusePayload? copy = null;
        Assert.Equal(record.Length, LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(record, ref copy));
        Assert.Equal(source.Name, copy!.Name);
        Assert.Equal(source.Child.Text, copy.Child.Text);
        var child = copy.Child;
        var tags = copy.Tags;
        var map = copy.Map;
        string name = copy.Name;
        string childText = child.Text;
        string[] items = tags.ToArray();
        var identity = copy;
        LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(record, ref copy);
        Assert.Same(identity, copy);
        Assert.Same(child, copy!.Child);
        Assert.Same(tags, copy.Tags);
        Assert.Same(map, copy.Map);
        Assert.Same(name, copy.Name);
        Assert.Same(childText, copy.Child.Text);
        for (int index = 0; index < items.Length; index++) Assert.Same(items[index], copy.Tags[index]);
        source.Name = "changed! property";
        source.Child.Text = "different text";
        source.Tags[1] = "different content with the same destination list";
        record = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(source);
        LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(record, ref copy);
        Assert.Same(identity, copy);
        Assert.Same(child, copy!.Child);
        Assert.Equal(source.Name, copy.Name);
        Assert.Equal(source.Child.Text, copy.Child.Text);
        Assert.Equal(source.Tags, copy.Tags);
    }

    /// <summary>Checks the complete record over several unaligned windows with immutable guards.</summary>
    /// <typeparam name="TOptions">The selected complete UTF-16 profile.</typeparam>
    /// <param name="record">The exact independently constructed record.</param>
    /// <param name="expected">The original code units.</param>
    /// <param name="current">The immutable destination candidate.</param>
    /// <param name="reuse">Whether identity must be retained.</param>
    private static void CheckRecord<TOptions>(byte[] record, string expected, string? current, bool reuse)
        where TOptions : struct, ILuminPackOptions
    {
        foreach (int offset in new[] { 0, 1, 3, 7 })
        {
            byte[] guarded = Enumerable.Repeat((byte)0xAC, offset + record.Length + 9).ToArray();
            record.CopyTo(guarded, offset);
            Assert.Null(TryRead<TOptions>(guarded, offset, record.Length + 3, current, out var result, out int position));
            Assert.Equal(expected, result);
            Assert.Equal(record.Length, position);
            if (reuse) Assert.Same(current, result);
            else Assert.NotSame(current, result);
            Assert.All(guarded.AsSpan(0, offset).ToArray(), item => Assert.Equal((byte)0xAC, item));
            Assert.All(guarded.AsSpan(offset + record.Length).ToArray(), item => Assert.Equal((byte)0xAC, item));
            Assert.Equal(record, guarded.AsSpan(offset, record.Length).ToArray());
        }
    }

    /// <summary>Creates a raw code-unit fixture without encoder replacement of unpaired surrogates.</summary>
    /// <param name="text">The exact UTF-16 payload.</param>
    /// <returns>The little-endian length record.</returns>
    private static byte[] Record(string text)
    {
        var record = new byte[4 + text.Length * 2];
        BinaryPrimitives.WriteInt32LittleEndian(record, text.Length * 2);
        MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(record.AsSpan(4));
        return record;
    }

    /// <summary>Reads a stationary window and records the original failure cursor without capturing a ref struct.</summary>
    /// <typeparam name="TOptions">The complete binary string profile.</typeparam>
    /// <param name="record">The original managed input.</param>
    /// <param name="offset">The first accessible byte.</param>
    /// <param name="length">The accessible window width.</param>
    /// <param name="current">The immutable reuse candidate.</param>
    /// <param name="result">The parsed result, or null on failure.</param>
    /// <param name="position">The final Reader cursor.</param>
    /// <returns>The original parsing failure, or null on success.</returns>
    private static unsafe Exception? TryRead<TOptions>(byte[] record, int offset, int length, string? current,
        out string? result, out int position) where TOptions : struct, ILuminPackOptions
    {
        fixed (byte* pointer = record)
        {
            ReadOnlySpan<byte> input = new(pointer + offset, length);
            var reader = new LuminPackReader(ref input);
            result = null;
            try
            {
                result = reader.Read<TOptions>(current);
                position = reader.Position;
                return null;
            }
            catch (Exception error)
            {
                position = reader.Position;
                return error;
            }
        }
    }
}

/// <summary>Represents an ordinary mutable graph with initialized string and collection storage.</summary>
[LuminPackable]
public sealed class Utf16ReusePayload
{
    public string Name { get; set; } = string.Empty;
    public Utf16ReuseChild Child = new();
    public List<string> Tags = new();
    public Dictionary<string, string> Map = new();
}

/// <summary>Represents nested ordinary reference storage.</summary>
[LuminPackable]
public sealed class Utf16ReuseChild
{
    public string Text = string.Empty;
}
