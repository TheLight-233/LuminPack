using System;
using System.Buffers;
using System.Linq;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;
namespace LuminPackUnitTest;
/// <summary>Checks complete UTF-8 JSON writes and their bounded fallbacks on portable and modern targets.</summary>
[Collection(nameof(RuntimeStateCollection))]
public unsafe class PortableJsonStringCapacityTest
{
    /// <summary>Preserves the existing quoted representation for all code units at packed-prefix boundaries.</summary>
    [Fact]
    public void EveryCodeUnitKeepsQuotedBytes()
    {
        char[] text = Enumerable.Repeat('x', 65).ToArray();
        byte[] output = new byte[1024];
        var state = new LuminPackWriterOptionalState();
        fixed(byte* pinned = output)
        foreach (int position in new[] { 0, 7, 15, 31, 64 })
        {
            for (int code = 0; code <= char.MaxValue; code++)
            {
                text[position] = (char)code;
                string value = new string(text);
                byte[] expected = Encoding.UTF8.GetBytes(Quote(value));
                Span<byte> window = output.AsSpan(3, output.Length - 20);
                var writer = new LuminPackJsonWriter(ref window, state);
                writer.Write<LuminPackUtf8LengthOptions>(value);
                Assert.Equal(expected.Length, writer.Position);
                Assert.True(expected.AsSpan().SequenceEqual(window.Slice(0, writer.Position)));
            }
            text[position] = 'x';
        }
    }

    /// <summary>Preserves exact and short fixed windows, guard bytes, and external streaming output.</summary>
    [Fact]
    public void CapacityBoundariesAndStreamingKeepBytes()
    {
        var state = new LuminPackWriterOptionalState();
        foreach (int length in new[] { 0, 1, 7, 8, 15, 16, 31, 32, 64, 257, 4096 })
        foreach (string pattern in new[] { "x", "\u00e9", "\u4e2d", "\ud83d\ude00", "\ud800x\udc00", "\n\"\\\0", "abcdefg\u4e2d", "abcdefg\n" })
        {
            string value = string.Concat(Enumerable.Repeat(pattern, length));
            byte[] expected = Encoding.UTF8.GetBytes(Quote(value));
            foreach (int capacity in new[] { 0, 1, 2, 7, 8, 15, 16, 31, 32, expected.Length - 1, expected.Length, value.Length * 3 + 2, value.Length * 6 + 2 }.Distinct())
            {
                byte[] guarded = Enumerable.Repeat((byte)0xa5, capacity + 20).ToArray();
                fixed (byte* pinned = guarded)
                {
                    Span<byte> window = guarded.AsSpan(3, capacity);
                    var writer = new LuminPackJsonWriter(ref window, state);
                    bool failed = false;
                    try { writer.Write<LuminPackUtf8LengthOptions>(value); }
                    catch (Exception) { failed = true; }
                    Assert.Equal(capacity < expected.Length, failed);
                    Assert.InRange(writer.Position, 0, capacity);
                    Assert.True(expected.AsSpan(0, writer.Position).SequenceEqual(window.Slice(0, writer.Position)));
                    Assert.True(guarded.AsSpan(0, 3).ToArray().All(x => x == 0xa5));
                    Assert.True(guarded.AsSpan(capacity + 3).ToArray().All(x => x == 0xa5));
                }
            }
            foreach (int chunk in new[] { 1, 7, 16, 31, 4096 })
            {
                var output = new ArrayBufferWriter<byte>();
                var writer = new LuminPackJsonWriter(output, state, chunk);
                try
                {
                    writer.Write<LuminPackUtf8LengthOptions>(value);
                    writer.FlushToBufferWriter();
                    Assert.True(expected.AsSpan().SequenceEqual(output.WrittenSpan));
                }
                finally { writer.Dispose(); }
            }
        }
    }

    /// <summary>Rejects extra managed allocation with reusable state and output, including malformed surrogate input.</summary>
    [Fact]
    public void WarmWritesDoNotAllocate()
    {
        long control = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(new byte[64]);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() > control);
        byte[] output = new byte[32768];
        var state = new LuminPackWriterOptionalState();
        foreach (string value in new[] { new string('x', 4096), new string('\u4e2d', 4096), new string('x', 256) + "\ud800", new string('\n', 1024) })
        fixed (byte* pinned = output)
        {
            Span<byte> window = output;
            for (int i = 0; i < 64; i++) { var warm = new LuminPackJsonWriter(ref window, state); warm.Write<LuminPackUtf8LengthOptions>(value); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++) { var writer = new LuminPackJsonWriter(ref window, state); writer.Write<LuminPackUtf8LengthOptions>(value); }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
    /// <summary>Builds the established JSON string spelling independently of the runtime encoder.</summary>
    /// <param name="value">The original UTF-16 content.</param>
    /// <returns>The quoted text before UTF-8 replacement encoding.</returns>
    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        foreach (char c in value)
            text.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", < ' ' => "\\u" + ((int)c).ToString("x4"), _ => c.ToString() });
        return text.Append('"').ToString();
    }
}
