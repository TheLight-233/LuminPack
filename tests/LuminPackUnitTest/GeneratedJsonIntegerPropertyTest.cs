using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public class GeneratedJsonIntegerPropertyTest
{
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void GeneratedPropertiesKeepIntegerExtremesNamesAndFallbacks(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        var jsonOption = new JsonSerializerOptions {
            IncludeFields = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        foreach (bool extreme in new[] { false, true })
        {
            var value = new GeneratedIntegerPropertyModel {
                ABCD = extreme ? int.MinValue : 0, IntValue = extreme ? int.MaxValue : -1,
                UIntValue = extreme ? uint.MaxValue : 1, LongValue = extreme ? long.MinValue : 0,
                ULongValue = extreme ? ulong.MaxValue : 0, ABCDEFGHIJKL = long.MaxValue,
                分数 = -37, @event = 19, X = -12, ThisFieldNameIsLonger = 71,
                Optional = extreme ? int.MinValue : null
            };
            string expected = JsonSerializer.Serialize(value, jsonOption);
            string actual = LuminPackSerializer.SerializeJson(value, option);
            Assert.Equal(expected, actual);
            Assert.Equal(actual, LuminPackSerializer.SerializeJson(
                LuminPackSerializer.DeserializeJson<GeneratedIntegerPropertyModel>(actual, option), option));
        }
    }

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
            writer.WriteGeneratedInteger(BitConverter.ToUInt64(prefix), BitConverter.ToUInt64(prefix, prefix.Length - 8), prefix.Length, value);
            writer.WriteInt(99);
            byte[] expected = Encoding.UTF8.GetBytes(",\"IntValue\":" + value.ToString(CultureInfo.InvariantCulture) + ",99");
            Assert.Equal(expected, bytes.AsSpan(4, expected.Length).ToArray());
            Assert.Equal(3 + expected.Length, writer.CurrentIndex);
            // Numeric formatting may use packed stores in reserved spare space;
            // sentinels outside the supplied span must remain untouched.
            Assert.All(bytes.Take(4).Concat(bytes.Skip(79)), b => Assert.Equal((byte)0xCC, b));
        }
    }

    [Fact]
    public void StreamingGrowthKeepsPrefixAndValueContiguousAfterCursorReset()
    {
        byte[] prefix = Encoding.UTF8.GetBytes(",\"LongValue\":");
        var output = new ExactOutput();
        var writer = new LuminPackJsonWriter(output, new LuminPackWriterOptionalState(), 5);
        writer.WriteRaw("12345"u8);
        writer.WriteGeneratedInteger(BitConverter.ToUInt64(prefix), BitConverter.ToUInt64(prefix, prefix.Length - 8), prefix.Length, long.MinValue);
        writer.WriteInt(17);
        writer.FlushToBufferWriter();
        byte[] expected = Encoding.UTF8.GetBytes("12345,\"LongValue\":-9223372036854775808,17");
        Assert.Equal(expected, output.Bytes.ToArray());
        Assert.Equal(expected.Length, writer.BytesWritten);
        Assert.True(output.Commits >= 2);
    }

    [Fact]
    public void InvalidPrefixCursorEncodingAndCapacityFailBeforeWriting()
    {
        foreach (int length in new[] { -1, 0, 7, 17, int.MaxValue })
            Check(length, 0, false, 64);
        foreach (int cursor in new[] { -1, 65, int.MaxValue })
            Check(8, cursor, false, 64);
        Check(8, 0, true, 64);
        Check(8, 0, false, 18); // prefix + maximum signed-int length needs 19 bytes

        static void Check(int length, int cursor, bool utf16, int capacity)
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, capacity + 2).ToArray();
            Assert.ThrowsAny<Exception>(() => Write(bytes, length, cursor, utf16));
            Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));
        }
        static void Write(byte[] bytes, int length, int cursor, bool utf16)
        {
            Span<byte> span = bytes.AsSpan(1, bytes.Length - 2);
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState(new LuminPackSerializerOption {
                StringEncoding = utf16 ? LuminPackStringEncoding.UTF16 : LuminPackStringEncoding.UTF8
            }));
            writer.CurrentIndex = cursor;
            writer.WriteGeneratedInteger(0, 0, length, 0);
        }
    }

    private sealed class ExactOutput : IBufferWriter<byte>
    {
        private byte[] _current = Array.Empty<byte>();
        public readonly List<byte> Bytes = new();
        public int Commits;
        public void Advance(int count) { Bytes.AddRange(_current.AsSpan(0, count).ToArray()); Commits++; }
        public Memory<byte> GetMemory(int sizeHint = 0) { _current = new byte[Math.Max(sizeHint, 1)]; return _current; }
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }
}

[LuminPackable]
public sealed partial class GeneratedIntegerPropertyModel
{
    public int ABCD;
    public int IntValue;
    public uint UIntValue;
    public long LongValue;
    public ulong ULongValue;
    public long ABCDEFGHIJKL;
    public int 分数;
    public int @event;
    public int X;
    public int ThisFieldNameIsLonger;
    public int? Optional;
}
