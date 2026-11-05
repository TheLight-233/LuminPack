using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks platform-compatible UTF-8 fallback across bounded kernels and their BCL token consumers.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class PortableUtf8FallbackTest
{
    /// <summary>Preserves host decoding, capacity exceptions, partial output and both destination guards.</summary>
    [Fact]
    public void PlatformDecoderMatchesBclWindows()
    {
        foreach (byte[] source in Inputs())
        foreach (int capacity in new[] { 0, 1, 2, source.Length / 3, source.Length, source.Length + 1 }.Distinct())
        {
            char[] expected = Enumerable.Repeat('\u55aa', capacity + 2).ToArray();
            char[] actual = (char[])expected.Clone();
            int expectedCount = 0, actualCount = 0;
            Exception? expectedError = Record.Exception(() => expectedCount = Encoding.UTF8.GetChars(source, expected.AsSpan(1, capacity)));
            Exception? actualError = Record.Exception(() => actualCount = Utf8PlatformDecoder.Decode(source, actual.AsSpan(1, capacity)));
            Assert.Equal(expectedError?.GetType(), actualError?.GetType());
            Assert.Equal(expectedError?.Message, actualError?.Message);
            Assert.Equal(expectedCount, actualCount);
            Assert.Equal(expected, actual);
            actual.AsSpan().Fill('\u55aa');
            bool fits = Utf8PlatformDecoder.TryDecode(source, actual.AsSpan(1, capacity), out actualCount);
            string decoded = Encoding.UTF8.GetString(source);
            Assert.Equal(decoded.Length <= capacity, fits);
            if (fits)
            {
                Assert.Equal(decoded.Length, actualCount);
                Assert.Equal(decoded, new string(actual, 1, actualCount));
            }
            else Assert.All(actual, value => Assert.Equal('\u55aa', value));
            Assert.Equal('\u55aa', actual[0]);
            Assert.Equal('\u55aa', actual[^1]);
        }
    }

    /// <summary>Runs the portable legacy kernel directly so modern test hosts cover its distinct replacement rules.</summary>
    [Fact]
    public void LegacyDecoderKeepsReplacementPrefixes()
    {
        MethodInfo method = typeof(Utf8PlatformDecoder).GetMethod("DecodeUtf8LegacyComplete", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.NotNull(method);
        var decode = (DecodeOperation)method.CreateDelegate(typeof(DecodeOperation));
        foreach (var row in new[]
        {
            (new byte[] { 0xED, 0xA0, 0x80 }, "\ufffd\ufffd"),
            (new byte[] { 0xE0, 0x80, 0x80 }, "\ufffd\ufffd"),
            (new byte[] { 0xF0, 0x80, 0x80, 0x80 }, "\ufffd\ufffd\ufffd"),
            (new byte[] { 0xF4, 0x90, 0x80, 0x80 }, "\ufffd\ufffd\ufffd"),
            (new byte[] { 0xC0, 0x80 }, "\ufffd\ufffd"),
            (new byte[] { 0xF0, 0x9F, 0x98 }, "\ufffd")
        })
        {
            byte[] source = Encoding.UTF8.GetBytes(new string('x', 129)).Concat(row.Item1).Concat(Encoding.UTF8.GetBytes(new string('\u4e2d', 129))).ToArray();
            char[] output = new char[source.Length];
            Assert.Equal(new string('x', 129) + row.Item2 + new string('\u4e2d', 129), new string(output, 0, decode(source, output)));
        }
    }

    /// <summary>Checks fresh and reused builders, character bounds and parser failures against host BCL text.</summary>
    [Fact]
    public void PlatformReadersKeepResultsAndFailures()
    {
        foreach (byte[] content in Inputs())
        {
            string text = Encoding.UTF8.GetString(content);
            byte[] json = new byte[content.Length + 2];
            json[0] = json[^1] = (byte)'"';
            content.CopyTo(json, 1);
            var state = new LuminPackReaderOptionalState();
            StringBuilder? builder = null;
            ReadBuilder(json, state, ref builder);
            Assert.Equal(text, builder!.ToString());
            var retained = builder;
            ReadBuilder(json, state, ref builder);
            Assert.Same(retained, builder);
            Assert.Equal(text, builder!.ToString());
            char[] two = new char[2];
            Exception? charError = Record.Exception(() => Encoding.UTF8.GetChars(content, two));
            char actual = default;
            Exception? readerError = Record.Exception(() => actual = ReadCharacter(json, state));
            Assert.Equal(charError?.GetType(), readerError?.GetType());
            Assert.Equal(charError?.Message, readerError?.Message);
            if (charError is null) Assert.Equal(text.Length == 0 ? '\0' : text[0], actual);
            Exception? versionError = Record.Exception(() => Version.Parse(text));
            Exception? versionReaderError = Record.Exception(() => ReadVersion(json, state));
            Assert.Equal(versionError?.GetType(), versionReaderError?.GetType());
            Assert.Equal(versionError?.Message, versionReaderError?.Message);
            Exception? integerError = Record.Exception(() => BigInteger.Parse(text, CultureInfo.InvariantCulture));
            Exception? integerReaderError = Record.Exception(() => ReadInteger(json, state));
            Assert.Equal(integerError?.GetType(), integerReaderError?.GetType());
            Assert.Equal(integerError?.Message, integerReaderError?.Message);
        }
    }

    /// <summary>Requires retained builder reads to allocate no temporary objects after setup.</summary>
    [Fact]
    public void PlatformBuilderReuseDoesNotAllocate()
    {
        foreach (byte[] content in Inputs())
        {
            byte[] json = new byte[content.Length + 2];
            json[0] = json[^1] = (byte)'"';
            content.CopyTo(json, 1);
            var state = new LuminPackReaderOptionalState();
            StringBuilder? value = new StringBuilder(content.Length + 16);
            for (int i = 0; i < 64; i++) ReadBuilder(json, state, ref value);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) ReadBuilder(json, state, ref value);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }

    /// <summary>Compares byte property names with their platform-decoded character names in UTF-16 JSON.</summary>
    [Fact]
    public void PlatformPropertyNamesKeepUtf16Bytes()
    {
        foreach (byte[] content in Inputs())
        {
            string text = Encoding.UTF8.GetString(content);
            byte[] first = new byte[text.Length * 6 + 32], second = new byte[first.Length];
            var state = new LuminPackWriterOptionalState();
            fixed (byte* p = first)
            fixed (byte* q = second)
            {
                Span<byte> a = first, b = second;
                var left = new LuminPackJsonWriter(ref a, state);
                var right = new LuminPackJsonWriter(ref b, state);
                left.WritePropertyName<LuminPackUtf16LengthOptions>(content);
                right.WritePropertyName<LuminPackUtf16LengthOptions>(text.AsSpan());
                Assert.Equal(left.Position, right.Position);
                Assert.Equal(first, second);
            }
        }
    }

    /// <summary>Checks snapshot admission on both sides of the stack limit and allocation-free warm cache hits.</summary>
    [Fact]
    public void PlatformTimeZoneSnapshotsKeepAdmissionAndReplacement()
    {
        foreach (int length in new[] { 8, 80, 128, 257 })
        {
            var zone = TimeZoneInfo.CreateCustomTimeZone("Utf8Fallback", TimeSpan.FromHours(2), new string('\u4e2d', length), "Standard");
            byte[] content = Encoding.UTF8.GetBytes(zone.ToSerializedString());
            content[Array.IndexOf(content, (byte)0xe4)] = 0xff;
            string snapshot = Encoding.UTF8.GetString(content);
            string expected = TimeZoneInfo.FromSerializedString(snapshot).ToSerializedString();
            byte[] json = new byte[content.Length + 2];
            json[0] = json[^1] = (byte)'"';
            content.CopyTo(json, 1);
            var state = new LuminPackReaderOptionalState();
            TimeZoneInfo result = ReadTimeZone(json, state);
            Assert.Equal(expected, result.ToSerializedString());
            if (snapshot.Length <= 256)
            {
                for (int i = 0; i < 64; i++) Assert.Same(result, ReadTimeZone(json, state));
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 128; i++) GC.KeepAlive(ReadTimeZone(json, state));
                Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            }
        }
    }

    /// <summary>Reads a time-zone snapshot from pinned input.</summary>
    /// <param name="bytes">The JSON input.</param><param name="state">The reader state.</param><returns>The zone.</returns>
    private static TimeZoneInfo ReadTimeZone(byte[] bytes, LuminPackReaderOptionalState state)
    {
        fixed (byte* p = bytes) { ReadOnlySpan<byte> input = bytes; var reader = new LuminPackJsonReader(ref input, state); return reader.ReadTimeZoneInfo<LuminPackUtf8LengthOptions>()!; }
    }

    /// <summary>Supplies representative ASCII, Unicode, numeric and malformed text without JSON quote or escape bytes.</summary>
    /// <returns>The complete raw token contents.</returns>
    private static byte[][] Inputs() => new[]
    {
        Array.Empty<byte>(), Encoding.UTF8.GetBytes("1.2.3.4"), Encoding.UTF8.GetBytes("123456789012345678901234567890"),
        Encoding.UTF8.GetBytes("\u00e9\u4e2d\ud83d\ude00"), Encoding.UTF8.GetBytes(new string('x', 129)),
        Encoding.UTF8.GetBytes(new string('\u4e2d', 257)), Encoding.UTF8.GetBytes(new string('\u00e9', 4096)),
        new byte[] { 0x80 }, new byte[] { 0xC0, 0x80 }, new byte[] { 0xC2 }, new byte[] { 0xED, 0xA0, 0x80 },
        new byte[] { 0xF0, 0x9F }, new byte[] { 0xF4, 0x90, 0x80, 0x80 }, new byte[] { 0x41, 0xF0, 0x9F, 0x98, 0x80, 0x80 }
    };

    /// <summary>Reads a builder while keeping the portable cursor's input pinned.</summary>
    /// <param name="bytes">The JSON input.</param><param name="state">The reused reader state.</param><param name="value">The reused target.</param>
    private static void ReadBuilder(byte[] bytes, LuminPackReaderOptionalState state, ref StringBuilder? value)
    {
        fixed (byte* p = bytes) { ReadOnlySpan<byte> input = bytes; var reader = new LuminPackJsonReader(ref input, state); reader.ReadStringBuilder<LuminPackUtf8LengthOptions>(ref value); }
    }

    /// <summary>Reads a bounded character token from pinned input.</summary>
    /// <param name="bytes">The JSON input.</param><param name="state">The reader state.</param><returns>The character.</returns>
    private static char ReadCharacter(byte[] bytes, LuminPackReaderOptionalState state)
    {
        fixed (byte* p = bytes) { ReadOnlySpan<byte> input = bytes; var reader = new LuminPackJsonReader(ref input, state); char value = default; reader.Read<LuminPackUtf8LengthOptions>(ref value); return value; }
    }

    /// <summary>Reads a version token from pinned input.</summary>
    /// <param name="bytes">The JSON input.</param><param name="state">The reader state.</param><returns>The version.</returns>
    private static Version? ReadVersion(byte[] bytes, LuminPackReaderOptionalState state)
    {
        fixed (byte* p = bytes) { ReadOnlySpan<byte> input = bytes; var reader = new LuminPackJsonReader(ref input, state); return reader.ReadVersion<LuminPackUtf8LengthOptions>(); }
    }

    /// <summary>Reads an integer token from pinned input.</summary>
    /// <param name="bytes">The JSON input.</param><param name="state">The reader state.</param><returns>The integer.</returns>
    private static BigInteger ReadInteger(byte[] bytes, LuminPackReaderOptionalState state)
    {
        fixed (byte* p = bytes) { ReadOnlySpan<byte> input = bytes; var reader = new LuminPackJsonReader(ref input, state); return reader.ReadBigInteger<LuminPackUtf8LengthOptions>(); }
    }

    /// <summary>Invokes an already bound private kernel without reflection allocations inside a sample.</summary>
    /// <param name="source">The complete input.</param><param name="destination">The destination.</param><returns>The character count.</returns>
    private delegate int DecodeOperation(ReadOnlySpan<byte> source, Span<char> destination);
}
