using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides generated json integer property test functionality.</summary>
public class GeneratedJsonIntegerPropertyTest
{
    /// <summary>Checks generated integer properties against an AOT-compatible JSON oracle and a round trip.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void GeneratedPropertiesKeepIntegerExtremesNamesAndFallbacks(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding };
        var jsonOption = new JsonSerializerOptions
        {
            IncludeFields = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var jsonContext = new GeneratedIntegerPropertyJsonContext(jsonOption);
        foreach (bool extreme in new[] { false, true })
        {
            var value = new GeneratedIntegerPropertyModel
            {
                ABCD = extreme ? int.MinValue : 0,
                IntValue = extreme ? int.MaxValue : -1,
                UIntValue = extreme ? uint.MaxValue : 1,
                LongValue = extreme ? long.MinValue : 0,
                ULongValue = extreme ? ulong.MaxValue : 0,
                ABCDEFGHIJKL = long.MaxValue,
                \u5206\u6570 = -37,
                @event = 19,
                X = -12,
                ThisFieldNameIsLonger = 71,
                Optional = extreme ? int.MinValue : null
            };
            string expected = JsonSerializer.Serialize(value, jsonContext.GeneratedIntegerPropertyModel);
            string actual = LuminPackSerializer.SerializeJson(value, option);
            Assert.Equal(expected, actual);
            Assert.Equal(actual, LuminPackSerializer.SerializeJson(
                LuminPackSerializer.DeserializeJson<GeneratedIntegerPropertyModel>(actual, option), option));
        }
    }

    /// <summary>Verifies combined writes preserve cursor sentinels and following commas.</summary>
    [Fact]
    public void CombinedWritesPreserveCursorSentinelsAndFollowingCommas()
    {
        byte[] prefix = Encoding.UTF8.GetBytes(",\"IntValue\":");
        foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, 80).ToArray();
            Span<byte> span = bytes.AsSpan(1, 78);
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
            writer.Advance(3);
            writer.WriteGeneratedInteger<LuminPack.Option.LuminPackUtf8LengthOptions>(BitConverter.ToUInt64(prefix), BitConverter.ToUInt64(prefix, prefix.Length - 8), prefix.Length, value);
            writer.Write<LuminPack.Option.LuminPackUtf8LengthOptions>(99);
            byte[] expected = Encoding.UTF8.GetBytes(",\"IntValue\":" + value.ToString(CultureInfo.InvariantCulture) + ",99");
            Assert.Equal(expected, bytes.AsSpan(4, expected.Length).ToArray());
            Assert.Equal(3 + expected.Length, writer.CurrentIndex);
            // Numeric formatting may use packed stores in reserved spare space;
            // sentinels outside the supplied span must remain untouched.
            Assert.All(bytes.Take(4).Concat(bytes.Skip(79)), b => Assert.Equal((byte)0xCC, b));
        }
    }

    /// <summary>Verifies streaming growth keeps prefix and value contiguous after cursor reset.</summary>
    [Fact]
    public void StreamingGrowthKeepsPrefixAndValueContiguousAfterCursorReset()
    {
        byte[] prefix = Encoding.UTF8.GetBytes(",\"LongValue\":");
        var output = new ExactOutput();
        var writer = new LuminPackJsonWriter(output, new LuminPackWriterOptionalState(), 5);
        writer.WriteRaw("12345"u8);
        writer.WriteGeneratedInteger<LuminPack.Option.LuminPackUtf8LengthOptions>(BitConverter.ToUInt64(prefix), BitConverter.ToUInt64(prefix, prefix.Length - 8), prefix.Length, long.MinValue);
        writer.Write<LuminPack.Option.LuminPackUtf8LengthOptions>(17);
        writer.FlushToBufferWriter();
        byte[] expected = Encoding.UTF8.GetBytes("12345,\"LongValue\":-9223372036854775808,17");
        Assert.Equal(expected, output.Bytes.ToArray());
        Assert.Equal(expected.Length, writer.BytesWritten);
        Assert.True(output.Commits >= 2);
    }

    /// <summary>Verifies invalid prefix cursor encoding and capacity fail before writing.</summary>
    [Fact]
    public void InvalidPrefixCursorEncodingAndCapacityFailBeforeWriting()
    {
        foreach (int length in new[] { -1, 0, 7, 17, int.MaxValue })
            Check(length, 0, false, 64);
        foreach (int cursor in new[] { -1, 65, int.MaxValue })
            Check(8, cursor, false, 64);
        Check(8, 0, true, 64);
        Check(8, 0, false, 18); // prefix + maximum signed-int length needs 19 bytes

        // <summary>Checks  against the operation's requirements.</summary>
        // <param name="length">The number of bytes or elements to process.</param>
        // <param name="cursor">The cursor used by this operation.</param>
        // <param name="utf16">The utf16 used by this operation.</param>
        // <param name="capacity">The requested storage capacity.</param>
        static void Check(int length, int cursor, bool utf16, int capacity)
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, capacity + 2).ToArray();
            Assert.ThrowsAny<Exception>(() => Write(bytes, length, cursor, utf16));
            Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));
        }
        // <summary>Writes  to the current output.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        // <param name="length">The number of bytes or elements to process.</param>
        // <param name="cursor">The cursor used by this operation.</param>
        // <param name="utf16">The utf16 used by this operation.</param>
        static void Write(byte[] bytes, int length, int cursor, bool utf16)
        {
            Span<byte> span = bytes.AsSpan(1, bytes.Length - 2);
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
            writer.Position = cursor;
            if (utf16) writer.WriteGeneratedInteger<LuminPackUtf16LengthOptions>(0, 0, length, 0); else writer.WriteGeneratedInteger<LuminPackDefaultOptions>(0, 0, length, 0);
        }
    }

    /// <summary>Provides exact output functionality.</summary>
    private sealed class ExactOutput : IBufferWriter<byte>
    {
        private byte[] _current = Array.Empty<byte>();
        /// <summary>Stores the bytes.</summary>
        public readonly List<byte> Bytes = new();
        /// <summary>Stores the commits.</summary>
        public int Commits;
        /// <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        public void Advance(int count) { Bytes.AddRange(_current.AsSpan(0, count).ToArray()); Commits++; }
        /// <summary>Obtains the memory for the supplied input.</summary>
        /// <param name="sizeHint">The size hint used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Memory<byte> GetMemory(int sizeHint = 0) { _current = new byte[Math.Max(sizeHint, 1)]; return _current; }
        /// <summary>Obtains the span for the supplied input.</summary>
        /// <param name="sizeHint">The size hint used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }
}

/// <summary>Provides reflection-free JSON metadata for the integer-property regression oracle.</summary>
[JsonSourceGenerationOptions(IncludeFields = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(GeneratedIntegerPropertyModel))]
internal partial class GeneratedIntegerPropertyJsonContext : JsonSerializerContext
{
}

/// <summary>Provides generated integer property model functionality.</summary>
[LuminPackable]
public sealed partial class GeneratedIntegerPropertyModel
{
    /// <summary>Stores the abcd.</summary>
    public int ABCD;
    /// <summary>Stores the int value.</summary>
    public int IntValue;
    /// <summary>Stores the u int value.</summary>
    public uint UIntValue;
    /// <summary>Stores the long value.</summary>
    public long LongValue;
    /// <summary>Stores the u long value.</summary>
    public ulong ULongValue;
    /// <summary>Stores the abcdefghijkl.</summary>
    public long ABCDEFGHIJKL;
    /// <summary>Stores the score in the Unicode-name regression fixture.</summary>
    public int \u5206\u6570;
    /// <summary>Stores the event.</summary>
    public int @event;
    /// <summary>Stores the x.</summary>
    public int X;
    /// <summary>Stores the this field name is longer.</summary>
    public int ThisFieldNameIsLonger;
    /// <summary>Stores the optional.</summary>
    public int? Optional;
}
