using System.Buffers.Binary;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks generated Int32 dictionary writes across the small dense path and its fallbacks.</summary>
public sealed class SmallDenseDictionaryWriteTest
{
    /// <summary>Writes the original count and pairs to an exact window at a nonzero cursor.</summary>
    /// <param name="count">The number of pairs in the input.</param>
    /// <param name="sparse">Whether the dictionary should have a removed entry.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(8, false)]
    [InlineData(9, false)]
    [InlineData(4, true)]
    public void ExactWindowKeepsCountAndPairOrder(int count, bool sparse)
    {
        var dictionary = CreateDictionary(count, sparse);
        byte[] expected = GetWireBytes(dictionary);
        byte[] bytes = new byte[expected.Length + 3];
        bytes.AsSpan().Fill(0xA5);
        Span<byte> window = bytes.AsSpan(1, expected.Length + 1);
        var writer = new LuminPackWriter(ref window);
        writer.Advance(1);

        LuminPackSerializer.Serialize<Dictionary<int, int>, LuminPackUtf16LengthOptions>(ref writer, dictionary);

        Assert.Equal(window.Length, writer.Position);
        Assert.Equal(expected, window[1..].ToArray());
        Assert.Equal(0xA5, bytes[0]);
        Assert.Equal(0xA5, bytes[1]);
        Assert.Equal(0xA5, bytes[^1]);
    }

    /// <summary>Retains the short-window error and leaves the original cursor and bytes intact.</summary>
    [Fact]
    public void ShortWindowPreservesCursorAndOutput()
    {
        var dictionary = CreateDictionary(4, sparse: false);
        byte[] bytes = new byte[GetWireBytes(dictionary).Length];
        bytes.AsSpan().Fill(0xA5);
        Span<byte> window = bytes.AsSpan(0, bytes.Length - 1);
        var writer = new LuminPackWriter(ref window);
        bool rejected = false;

        try
        {
            LuminPackSerializer.Serialize<Dictionary<int, int>, LuminPackUtf16LengthOptions>(ref writer, dictionary);
        }
        catch (Exception error) when (error.Message.StartsWith("Span out of range", StringComparison.Ordinal))
        {
            rejected = true;
        }

        Assert.True(rejected);
        Assert.Equal(0, writer.Position);
        Assert.All(bytes, value => Assert.Equal(0xA5, value));
    }

    /// <summary>Rejects a cursor outside its window even when it points into the backing array.</summary>
    /// <param name="beforeWindow">Whether to place the cursor before the window instead of after it.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RawPairWriterRejectsCursorOutsideWindow(bool beforeWindow)
    {
        var dictionary = CreateDictionary(4, sparse: false);
        int length = GetWireBytes(dictionary).Length;
        byte[] bytes = new byte[length + 4];
        bytes.AsSpan().Fill(0xA5);
        Span<byte> window = bytes.AsSpan(2, length);
        var writer = new LuminPackWriter(ref window);
        int offset = beforeWindow ? -1 : length + 1;
        writer.Cursor = ref bytes[2 + offset];

        Assert.False(LuminPackMarshal.TryWriteSmallDenseInt32Dictionary(ref writer, dictionary));
        Assert.Equal(offset, writer.Position);
        Assert.All(bytes, value => Assert.Equal(0xA5, value));
    }

#if LUMINPACK_NEXT_GENERATOR
    /// <summary>Checks dense and sparse dictionary fields publish the same Version frame and following value.</summary>
    /// <param name="count">The number of live pairs to write at the capacity boundary.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(120)]
    public void DenseRawDictionaryKeepsTheVersionMemberBoundary(int count)
    {
        var dense = new DictionaryRecord { Tail = 123 };
        for (int i = 0; i < count; i++)
        {
            int key = -2 * i - 1;
            dense.Pairs.Add(key, 0x100000003L - 0x200000001L * i);
            dense.Scores.Add(key, i - 0.25);
            dense.Flags.Add(key, (byte)(i * 11));
        }
        var sparse = new DictionaryRecord
        {
            Pairs = new Dictionary<int, long>(dense.Pairs) { [0] = 1 },
            Scores = new Dictionary<int, double>(dense.Scores) { [0] = 1 },
            Flags = new Dictionary<int, byte>(dense.Flags) { [0] = 1 },
            Tail = dense.Tail
        };
        Assert.True(sparse.Pairs.Remove(0));
        Assert.True(sparse.Scores.Remove(0));
        Assert.True(sparse.Flags.Remove(0));

        byte[] bytes = LuminPackSerializer.Serialize(sparse);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(dense));

        byte[] guarded = new byte[bytes.Length + 2];
        guarded.AsSpan().Fill(0xA5);
        Span<byte> window = guarded.AsSpan(1, bytes.Length);
        var writer = new LuminPackWriter(ref window);
        LuminPackSerializer.Serialize<DictionaryRecord, LuminPackUtf16LengthOptions>(ref writer, dense);
        Assert.Equal(bytes.Length, writer.Position);
        Assert.Equal(bytes, window.ToArray());
        Assert.Equal(0xA5, guarded[0]);
        Assert.Equal(0xA5, guarded[^1]);

        int offset = 3 + sizeof(ulong) + 4 * sizeof(ulong);
        foreach (int width in new[] { sizeof(long), sizeof(double), sizeof(byte) })
        {
            int payload = sizeof(int) + count * (sizeof(int) + width);
            Assert.Equal(payload, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)));
            Assert.Equal(count, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + sizeof(int))));
            offset += sizeof(int) + payload;
        }
        Assert.Equal(123, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)));

        DictionaryRecord? decoded = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref decoded));
        Assert.Equal(dense.Pairs.ToArray(), decoded!.Pairs.ToArray());
        Assert.Equal(dense.Scores.ToArray(), decoded.Scores.ToArray());
        Assert.Equal(dense.Flags.ToArray(), decoded.Flags.ToArray());
        Assert.Equal(123, decoded.Tail);
    }

    [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
    public sealed class DictionaryRecord
    {
        [LuminPackOrder(0)] public Dictionary<int, long> Pairs = new();
        [LuminPackOrder(1)] public Dictionary<int, double> Scores = new();
        [LuminPackOrder(2)] public Dictionary<int, byte> Flags = new();
        [LuminPackOrder(3)] public int Tail;
    }
#endif

    /// <summary>Creates integer pairs whose enumeration order differs from sorted order.</summary>
    /// <param name="count">The final number of pairs.</param>
    /// <param name="sparse">Whether to leave an unused slot before the live entries.</param>
    /// <returns>The dictionary to serialize.</returns>
    private static Dictionary<int, int> CreateDictionary(int count, bool sparse)
    {
        var dictionary = new Dictionary<int, int>();
        if (sparse)
            dictionary.Add(0, 0);
        for (int index = 0; index < count; index++)
            dictionary.Add(-2 * index - 1, 7 * index + 3);
        if (sparse)
            dictionary.Remove(0);
        return dictionary;
    }

    /// <summary>Builds the original count and packed signed pairs in dictionary enumeration order.</summary>
    /// <param name="dictionary">The fixture to encode.</param>
    /// <returns>The expected wire bytes.</returns>
    private static byte[] GetWireBytes(Dictionary<int, int> dictionary)
    {
        byte[] bytes = new byte[sizeof(int) + dictionary.Count * (2 * sizeof(int))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, dictionary.Count);
        int offset = sizeof(int);
        foreach (var pair in dictionary)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), pair.Key);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + sizeof(int)), pair.Value);
            offset += 2 * sizeof(int);
        }
        return bytes;
    }
}
