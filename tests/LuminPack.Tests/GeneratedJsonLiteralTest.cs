using System.Buffers;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides generated json literal test functionality.</summary>
public class GeneratedJsonLiteralTest
{
    /// <summary>Verifies invalid word length and cursor are rejected before writing.</summary>
    [Fact]
    public void InvalidWordLengthAndCursorAreRejectedBeforeWriting()
    {
        foreach (int count in new[] { int.MinValue, -1, 0, 7, 17, int.MaxValue })
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, 32).ToArray();
            Assert.ThrowsAny<Exception>(() => Copy(bytes, count, 0));
            Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));
        }
        foreach (int cursor in new[] { -1, 33, int.MaxValue })
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, 32).ToArray();
            Assert.ThrowsAny<Exception>(() => Copy(bytes, 8, cursor));
            Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));
        }

        // <summary>Copies  to the supplied destination.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        // <param name="count">The number of entries involved in the operation.</param>
        // <param name="cursor">The cursor used by this operation.</param>
        static void Copy(byte[] bytes, int count, int cursor)
        {
            Span<byte> span = bytes;
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
            writer.Position = cursor;
            writer.WriteGeneratedLiteral(0, 0, count);
        }
    }

    /// <summary>Verifies static literal copies exactly with offset and sentinels.</summary>
    /// <param name="utf16">The utf16 used by this operation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaticLiteralCopiesExactlyWithOffsetAndSentinels(bool utf16)
    {
        const string literal = ",\u0022\u540D\u5B57\u0022:";
        byte[] expected = utf16 ? Encoding.Unicode.GetBytes(literal) : Encoding.UTF8.GetBytes(literal);
        byte[] bytes = Enumerable.Repeat((byte)0xCC, expected.Length + 4).ToArray();
        Span<byte> span = bytes.AsSpan(1, expected.Length + 2);
        var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
        writer.Advance(2);
        writer.WriteGeneratedLiteral(BitConverter.ToUInt64(expected, 0),
            BitConverter.ToUInt64(expected, expected.Length - 8), expected.Length);
        Assert.Equal(expected, bytes.AsSpan(3, expected.Length).ToArray());
        Assert.All(bytes.Take(3).Concat(bytes.Skip(3 + expected.Length)), b => Assert.Equal((byte)0xCC, b));
    }

    /// <summary>Verifies fixed buffer capacity is checked before literal copy.</summary>
    /// <param name="utf16">The utf16 used by this operation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedBufferCapacityIsCheckedBeforeLiteralCopy(bool utf16)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 4).ToArray();
        Assert.ThrowsAny<Exception>(() => Copy(bytes, utf16));
        Assert.All(bytes, b => Assert.Equal((byte)0xCC, b));

        // <summary>Copies  to the supplied destination.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        // <param name="utf16">The utf16 used by this operation.</param>
        static void Copy(byte[] bytes, bool utf16)
        {
            Span<byte> span = bytes.AsSpan(1, 2);
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
            writer.WriteGeneratedLiteral(0UL, 0UL, utf16 ? 16 : 8);
        }
    }

    /// <summary>Checks streaming chunk transitions and destination failures against the retained character sequence.</summary>
    [Fact]
    public void FixedTokensPreserveStreamingObservationOrder()
    {
        foreach (string token in new[] { "true", "false", "null" })
        foreach (bool first in new[] { false, true })
        foreach (bool fail in new[] { false, true })
        for (int remaining = 0; remaining <= 12; remaining++)
        {
            var expected = StreamToken(token, first, remaining, fail, original: true);
            var actual = StreamToken(token, first, remaining, fail, original: false);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>Checks every short window, leading comma, odd offset and unchanged sentinel against the original operation.</summary>
    [Fact]
    public void FixedTokensPreserveAllShortWindowPrefixes()
    {
        foreach (string token in new[] { "true", "false", "null" })
        foreach (bool first in new[] { false, true })
        foreach (int offset in new[] { 0, 1, 3 })
        for (int length = 0; length <= 16; length++)
        {
            var expected = WriteToken(token, first, offset, length, original: true);
            var actual = WriteToken(token, first, offset, length, original: false);
            Assert.Equal(expected.Bytes, actual.Bytes);
            Assert.Equal(expected.Position, actual.Position);
            Assert.Equal(expected.First, actual.First);
            Assert.Equal(expected.Exception, actual.Exception);
        }
    }

    /// <summary>Executes either the retained character operation or the complete public token writer over a pinned window.</summary>
    /// <param name="token">The fixed literal being written.</param>
    /// <param name="first">The initial first-element state.</param>
    /// <param name="offset">The unaligned offset after the leading guard.</param>
    /// <param name="length">The complete destination window length.</param>
    /// <param name="original">Whether to replay the original character writes.</param>
    /// <returns>The full backing storage, cursor, element state and exception type.</returns>
    private static unsafe (byte[] Bytes, int Position, bool First, Type? Exception) WriteToken(string token, bool first, int offset, int length, bool original)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, length + offset + 32).ToArray();
        fixed (byte* pinned = bytes)
        {
            Span<byte> span = new Span<byte>(pinned + 16 + offset, length);
            var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
            writer.SetFirstElement(first);
            Type? exception = null;
            try
            {
                if (original)
                {
                    if (!first) writer.WriteByteRaw<LuminPackDefaultOptions>((byte)',');
                    writer.SetFirstElement(false);
                    foreach (char value in token) writer.WriteByteRaw<LuminPackDefaultOptions>((byte)value);
                }
                else if (token == "null") writer.WriteNull<LuminPackDefaultOptions>();
                else writer.Write<LuminPackDefaultOptions>(token == "true");
            }
            catch (Exception failure) { exception = failure.GetType(); }
            if (pinned[0] != 0xCC) throw new InvalidOperationException("The leading guard changed.");
            return (bytes, writer.Position, writer.IsFirstElement, exception);
        }
    }

    /// <summary>Writes a token near a chunk boundary and captures callbacks, publication and failure state.</summary>
    /// <param name="token">The fixed literal being written.</param>
    /// <param name="first">The initial first-element state.</param>
    /// <param name="remaining">The initial chunk bytes remaining after the raw prefix.</param>
    /// <param name="fail">Whether the second acquisition throws the retained exception.</param>
    /// <param name="original">Whether to replay the original character writes.</param>
    /// <returns>The exact callback, output and writer-state observation.</returns>
    private static string StreamToken(string token, bool first, int remaining, bool fail, bool original)
    {
        var output = new TokenOutput(fail);
        var writer = new LuminPackJsonWriter(output, new LuminPackWriterOptionalState(), 4096);
        string failure = "none";
        int position = 0;
        long written = 0;
        bool firstAfter = false;
        try
        {
            writer.WriteRaw(new byte[4096 - remaining]);
            writer.SetFirstElement(first);
            if (original)
            {
                if (!first) writer.WriteByteRaw<LuminPackDefaultOptions>((byte)',');
                writer.SetFirstElement(false);
                foreach (char value in token) writer.WriteByteRaw<LuminPackDefaultOptions>((byte)value);
            }
            else if (token == "null") writer.WriteNull<LuminPackDefaultOptions>();
            else writer.Write<LuminPackDefaultOptions>(token == "true");
            writer.FlushToBufferWriter();
        }
        catch (Exception exception)
        {
            Assert.Same(output.Failure, exception);
            failure = exception.GetType().Name;
        }
        finally
        {
            position = writer.Position;
            written = writer.BytesWritten;
            firstAfter = writer.IsFirstElement;
            writer.Dispose();
        }
        return string.Join(";", output.Calls) + ":" + Convert.ToHexString(output.Bytes.ToArray()) +
            $":{position}:{written}:{firstAfter}:{failure}";
    }

    /// <summary>Retains exact external acquisition/commit order and an optional second-acquisition failure.</summary>
    /// <param name="fail">Whether the second acquisition throws the retained exception.</param>
    private sealed class TokenOutput(bool fail) : IBufferWriter<byte>
    {
        internal readonly List<string> Calls = [];
        internal readonly List<byte> Bytes = [];
        internal readonly Exception Failure = new InvalidOperationException("destination failure");
        private byte[]? current;
        private int acquisitions;

        /// <summary>Commits the requested prefix of the current chunk.</summary>
        /// <param name="count">The number of initialized bytes to commit.</param>
        public void Advance(int count)
        {
            Calls.Add("Advance:" + count);
            Bytes.AddRange(current!.AsSpan(0, count).ToArray());
            current = null;
        }

        /// <summary>Acquires a complete chunk or throws the retained destination failure.</summary>
        /// <param name="sizeHint">The minimum required chunk length.</param>
        /// <returns>The complete newly acquired chunk.</returns>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Calls.Add("Get:" + sizeHint);
            if (++acquisitions == 2 && fail) throw Failure;
            current = new byte[Math.Max(4096, sizeHint)];
            return current;
        }

        /// <summary>Acquires the same complete tracked chunk as the memory entry point.</summary>
        /// <param name="sizeHint">The minimum required chunk length.</param>
        /// <returns>The complete newly acquired chunk.</returns>
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }
}
