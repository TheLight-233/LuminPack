using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public class JsonStreamingWriterTest
{
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void StreamingMatchesContiguousOutputAcrossChunks(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        var value = new IBufferModel
        {
            Id = 123,
            Name = new string('中', 30_000) + "😀\uD800\n\t\"\\" + new string('é', 2000),
            Numbers = Enumerable.Range(0, 2000).ToList()
        };
        using var contiguous = new LuminPack.Utility.LuminBufferWriter(true);
        contiguous.Option.StringEncoding = encoding;
        LuminPackSerializer.SerializeJson(value, contiguous);
        byte[] expectedBytes = contiguous.GetSpan().ToArray();
        var output = new ChunkWriter();
        LuminPackSerializer.SerializeJson(value, output, option);
        Assert.Equal(expectedBytes, output.ToArray());
        Assert.True(output.CommitCount > 1);
        Assert.InRange(output.MaxSizeHint, 1, 4096);
    }

    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void RawFragmentsAndEscapedPropertyNamesCrossChunks(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        string name = new string('中', 4000) + "\"\\\n😀";
        var value = new Dictionary<string, string> { [name] = new string('x', 9000) + "😀" };
        var output = new ChunkWriter();
        LuminPackSerializer.SerializeJson(value, output, option);
        string expected = LuminPackSerializer.SerializeJson(value, option);
        Assert.Equal(encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8.GetBytes(expected) : Encoding.Unicode.GetBytes(expected), output.ToArray());
        Assert.InRange(output.MaxSizeHint, 1, 4096);
    }

    [Fact]
    public void ExplicitStreamingWriterCountsAndFlushesExactlyOnce()
    {
        var output = new ChunkWriter();
        var writer = new LuminPackJsonWriter(output, new LuminPackWriterOptionalState(), 1);
        writer.WriteString("😀");
        writer.FlushToBufferWriter();
        long count = writer.BytesWritten;
        writer.FlushToBufferWriter();
        Assert.Equal(6, count);
        Assert.Equal(Encoding.UTF8.GetBytes("\"😀\""), output.ToArray());
        writer.WriteByteRaw((byte)' ');
        writer.FlushToBufferWriter();
        Assert.Equal(7, writer.BytesWritten);
        Assert.Equal(Encoding.UTF8.GetBytes("\"😀\" "), output.ToArray());
    }

    [Fact]
    public void OutputFailureDoesNotLeakSerializerOperationState()
    {
        var value = new IBufferModel { Id = 7, Name = new string('中', 10_000) };
        Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.SerializeJson(value, new FailingWriter()));
        var output = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson(value, output);
        Assert.Equal(Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson(value)), output.WrittenSpan.ToArray());
    }

    private sealed class FailingWriter : IBufferWriter<byte>
    {
        public void Advance(int count) => throw new InvalidOperationException("Injected output failure");
        public Memory<byte> GetMemory(int sizeHint = 0) => new byte[Math.Max(1, sizeHint)];
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }

    private sealed class ChunkWriter : IBufferWriter<byte>
    {
        private readonly MemoryStream _committed = new();
        private byte[]? _current;
        private int _capacity;
        public int MaxSizeHint { get; private set; }
        public int CommitCount { get; private set; }
        public void Advance(int count)
        {
            Assert.NotNull(_current);
            Assert.InRange(count, 0, _capacity);
            Assert.All(_current.Take(16), value => Assert.Equal((byte)0xCC, value));
            Assert.All(_current.Skip(16 + _capacity), value => Assert.Equal((byte)0xCC, value));
            _committed.Write(_current, 16, count);
            _current = null;
            CommitCount++;
        }
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Assert.Null(_current);
            _capacity = Math.Max(1, sizeHint);
            MaxSizeHint = Math.Max(MaxSizeHint, sizeHint);
            _current = Enumerable.Repeat((byte)0xCC, _capacity + 32).ToArray();
            return _current.AsMemory(16, _capacity);
        }
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
        public byte[] ToArray() => _committed.ToArray();
    }
}
