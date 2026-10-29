using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides json streaming writer test functionality.</summary>
public class JsonStreamingWriterTest
{
    /// <summary>Verifies streaming matches contiguous output across chunks.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void StreamingMatchesContiguousOutputAcrossChunks(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding };
        var value = new IBufferModel
        {
            Id = 123,
            Name = new string('\u4e2d', 30_000) + "😀\uD800\n\t\"\\" + new string('é', 2000),
            Numbers = Enumerable.Range(0, 2000).ToList()
        };
        using var contiguous = new LuminPack.Utility.LuminBufferWriter(true);
        LuminPackSerializer.SerializeJson(value, contiguous, option);
        byte[] expectedBytes = contiguous.GetSpan().ToArray();
        var output = new ChunkWriter();
        LuminPackSerializer.SerializeJson(value, output, option);
        Assert.Equal(expectedBytes, output.ToArray());
        Assert.True(output.CommitCount > 1);
        Assert.InRange(output.MaxSizeHint, 1, 4096);
    }

    /// <summary>Verifies raw fragments and escaped property names cross chunks.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void RawFragmentsAndEscapedPropertyNamesCrossChunks(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding };
        string name = new string('\u4e2d', 4000) + "\"\\\n😀";
        var value = new Dictionary<string, string> { [name] = new string('x', 9000) + "😀" };
        var output = new ChunkWriter();
        LuminPackSerializer.SerializeJson(value, output, option);
        string expected = LuminPackSerializer.SerializeJson(value, option);
        Assert.Equal(encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8.GetBytes(expected) : Encoding.Unicode.GetBytes(expected), output.ToArray());
        Assert.InRange(output.MaxSizeHint, 1, 4096);
    }

    /// <summary>Verifies explicit streaming writer counts and flushes exactly once.</summary>
    [Fact]
    public void ExplicitStreamingWriterCountsAndFlushesExactlyOnce()
    {
        var output = new ChunkWriter();
        var writer = new LuminPackJsonWriter(output, new LuminPackWriterOptionalState(), 1);
        writer.Write<LuminPack.Option.LuminPackUtf8LengthOptions>("😀");
        writer.FlushToBufferWriter();
        long count = writer.BytesWritten;
        writer.FlushToBufferWriter();
        Assert.Equal(6, count);
        Assert.Equal(Encoding.UTF8.GetBytes("\"😀\""), output.ToArray());
        writer.WriteByteRaw<LuminPack.Option.LuminPackUtf8LengthOptions>((byte)' ');
        writer.FlushToBufferWriter();
        Assert.Equal(7, writer.BytesWritten);
        Assert.Equal(Encoding.UTF8.GetBytes("\"😀\" "), output.ToArray());
    }

    /// <summary>Checks sparse UTF-16 escapes across external output segments and final flushing.</summary>
    [Fact]
    public void Utf16LiteralRunsKeepExternalSegmentPublication()
    {
        foreach (int length in new[] { 0, 7, 15, 16, 17, 31, 32, 33, 256, 4096 })
        {
            string prefix = new('x', length);
            string suffix = new('\u4E2D', length);
            string value = prefix + "\n\"\\\0" + suffix + "\t";
            var output = new ChunkWriter();
            var writer = new LuminPackJsonWriter(output, new LuminPackWriterOptionalState(), 1);
            writer.Write<LuminPack.Option.LuminPackDefaultOptions>(value);
            writer.FlushToBufferWriter();
            byte[] expected = Encoding.Unicode.GetBytes("\"" + prefix + "\\n\\\"\\\\\\u0000" + suffix + "\\t\"");
            Assert.Equal(expected, output.ToArray());
            Assert.Equal(expected.Length, writer.BytesWritten);
            int commits = output.CommitCount;
            writer.FlushToBufferWriter();
            Assert.Equal(commits, output.CommitCount);
        }
    }

    /// <summary>Checks sparse escapes, exact destinations and growth around the scalar and vector run boundaries.</summary>
    /// <param name="length">The number of ordinary code units between escapes.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(256)]
    [InlineData(20_000)]
    public void Utf16SparseEscapesKeepExactBytesAcrossDestinations(int length)
    {
        string ascii = new('x', length);
        string unicode = new('\u4E2D', length);
        string content = "\n" + ascii + "\"" + unicode + "\\\t\0\b\f" + ascii + "\r" + unicode + "\uD83D\uDE80";
        string escaped = "\\n" + ascii + "\\\"" + unicode + "\\\\\\t\\u0000\\u0008\\u000c" + ascii + "\\r" + unicode + "\uD83D\uDE80";
        foreach (bool property in new[] { false, true })
        {
            byte[] expected = Encoding.Unicode.GetBytes(property ? "{\"" + escaped + "\":null}" : "\"" + escaped + "\"");
            byte[] guarded = Enumerable.Repeat((byte)0xCC, expected.Length + 32).ToArray();
            Span<byte> destination = guarded.AsSpan(16, expected.Length);
            var writer = new LuminPackJsonWriter(ref destination, new LuminPackWriterOptionalState());
            if (property)
            {
                writer.WriteObjectStart<LuminPackDefaultOptions>();
                writer.WritePropertyName<LuminPackDefaultOptions>(content.AsSpan());
                writer.WriteNull<LuminPackDefaultOptions>();
                writer.WriteObjectEnd<LuminPackDefaultOptions>();
            }
            else writer.Write<LuminPackDefaultOptions>(content);
            Assert.Equal(expected, writer.GetSpan().ToArray());
            Assert.All(guarded.Take(16), value => Assert.Equal((byte)0xCC, value));
            Assert.All(guarded.Skip(expected.Length + 16), value => Assert.Equal((byte)0xCC, value));

            Array.Fill(guarded, (byte)0xCC);
            Span<byte> shortDestination = guarded.AsSpan(16, expected.Length - 2);
            var shortWriter = new LuminPackJsonWriter(ref shortDestination, new LuminPackWriterOptionalState());
            bool rejected = false;
            try
            {
                if (property)
                {
                    shortWriter.WriteObjectStart<LuminPackDefaultOptions>();
                    shortWriter.WritePropertyName<LuminPackDefaultOptions>(content.AsSpan());
                    shortWriter.WriteNull<LuminPackDefaultOptions>();
                    shortWriter.WriteObjectEnd<LuminPackDefaultOptions>();
                }
                else shortWriter.Write<LuminPackDefaultOptions>(content);
            }
            catch (Exception) { rejected = true; }
            Assert.True(rejected, "An undersized UTF-16 destination was accepted.");
            Assert.All(guarded.Take(16), value => Assert.Equal((byte)0xCC, value));
            Assert.All(guarded.Skip(shortDestination.Length + 16), value => Assert.Equal((byte)0xCC, value));

            using var buffer = new LuminPack.Utility.LuminBufferWriter(true);
            var growing = new LuminPackJsonWriter(buffer, new LuminPackWriterOptionalState());
            if (property)
            {
                growing.WriteObjectStart<LuminPackDefaultOptions>();
                growing.WritePropertyName<LuminPackDefaultOptions>(content.AsSpan());
                growing.WriteNull<LuminPackDefaultOptions>();
                growing.WriteObjectEnd<LuminPackDefaultOptions>();
            }
            else growing.Write<LuminPackDefaultOptions>(content);
            Assert.Equal(expected, growing.GetSpan().ToArray());
        }
    }

    /// <summary>Checks changing escape density after a sparse prefix without losing a following literal tail.</summary>
    [Fact]
    public void Utf16SparseRunsKeepDenseTransitionsAndFollowingValues()
    {
        foreach (int run in new[] { 1, 15, 16, 17, 31, 32, 33, 64 })
        {
            string ordinary = new('\u4E2D', run);
            string alternating = string.Concat(Enumerable.Repeat(ordinary + "\n", 128));
            string value = "\n" + new string('x', 256) + "\t" + alternating + new string('\\', 128) + new string('z', 1024);
            string escaped = "\\n" + new string('x', 256) + "\\t" +
                string.Concat(Enumerable.Repeat(ordinary + "\\n", 128)) + new string('\\', 256) + new string('z', 1024);
            using var buffer = new LuminPack.Utility.LuminBufferWriter(true);
            var writer = new LuminPackJsonWriter(buffer, new LuminPackWriterOptionalState());
            writer.WriteArrayStart<LuminPackDefaultOptions>();
            writer.Write<LuminPackDefaultOptions>(value);
            writer.Write<LuminPackDefaultOptions>("tail");
            writer.WriteArrayEnd<LuminPackDefaultOptions>();
            Assert.Equal(Encoding.Unicode.GetBytes("[\"" + escaped + "\",\"tail\"]"), writer.GetSpan().ToArray());
        }
    }

    /// <summary>Preserves the escape captured before an external output callback changes a mutable property name.</summary>
    [Fact]
    public void Utf16EscapeIsCapturedBeforeExternalPrefixPublication()
    {
        string prefix = new('x', 5000);
        char[] name = (prefix + "\nsuffix").ToCharArray();
        int requests = 0;
        var output = new ChunkWriter
        {
            OnGetMemory = () =>
            {
                if (++requests == 2) name[prefix.Length] = '\t';
            }
        };
        var writer = new LuminPackJsonWriter(output, new LuminPackWriterOptionalState(), 4096);
        writer.WriteObjectStart<LuminPack.Option.LuminPackDefaultOptions>();
        writer.WritePropertyName<LuminPack.Option.LuminPackDefaultOptions>(name.AsSpan());
        writer.Write<LuminPack.Option.LuminPackDefaultOptions>(7);
        writer.WriteObjectEnd<LuminPack.Option.LuminPackDefaultOptions>();
        writer.FlushToBufferWriter();
        Assert.Equal('\t', name[prefix.Length]);
        Assert.Equal(Encoding.Unicode.GetBytes("{\"" + prefix + "\\nsuffix\":7}"), output.ToArray());
    }

    /// <summary>Verifies output failure does not leak serializer operation state.</summary>
    [Fact]
    public void OutputFailureDoesNotLeakSerializerOperationState()
    {
        var value = new IBufferModel { Id = 7, Name = new string('\u4e2d', 10_000) };
        Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.SerializeJson(value, new FailingWriter()));
        var output = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson(value, output);
        // The default JSON byte encoding is UTF-16.
        Assert.Equal(Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(value)), output.WrittenSpan.ToArray());
    }

    /// <summary>Provides failing writer functionality.</summary>
    private sealed class FailingWriter : IBufferWriter<byte>
    {
        /// <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        public void Advance(int count) => throw new InvalidOperationException("Injected output failure");
        /// <summary>Obtains the memory for the supplied input.</summary>
        /// <param name="sizeHint">The size hint used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Memory<byte> GetMemory(int sizeHint = 0) => new byte[Math.Max(1, sizeHint)];
        /// <summary>Obtains the span for the supplied input.</summary>
        /// <param name="sizeHint">The size hint used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }

    /// <summary>Provides chunk writer functionality.</summary>
    private sealed class ChunkWriter : IBufferWriter<byte>
    {
        private readonly MemoryStream _committed = new();
        private byte[]? _current;
        private int _capacity;
        /// <summary>Gets or sets the max size hint.</summary>
        public int MaxSizeHint { get; private set; }
        /// <summary>Gets or sets the commit count.</summary>
        public int CommitCount { get; private set; }
        /// <summary>Gets or sets an optional callback before a newly acquired output segment is exposed.</summary>
        public Action? OnGetMemory { get; set; }
        /// <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
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
        /// <summary>Obtains the memory for the supplied input.</summary>
        /// <param name="sizeHint">The size hint used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Assert.Null(_current);
            _capacity = Math.Max(1, sizeHint);
            MaxSizeHint = Math.Max(MaxSizeHint, sizeHint);
            _current = Enumerable.Repeat((byte)0xCC, _capacity + 32).ToArray();
            OnGetMemory?.Invoke();
            return _current.AsMemory(16, _capacity);
        }
        /// <summary>Obtains the span for the supplied input.</summary>
        /// <param name="sizeHint">The size hint used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
        /// <summary>Verifies to array.</summary>
        /// <returns>The result of the operation.</returns>
        public byte[] ToArray() => _committed.ToArray();
    }
}
