using System;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks byte-distance boundaries against the actual netstandard asset, including 32-bit hosts.</summary>
public sealed class PortableByteCursorTest
{
    /// <summary>Preserves public cursor offsets without dereferencing intentionally out-of-window positions.</summary>
    [Fact]
    public void AllCursorKindsPreserveSignedOffsets()
    {
        Span<byte> storage = stackalloc byte[128];
        ReadOnlySpan<byte> input = storage;
        var readState = new LuminPackReaderOptionalState();
        var writeState = new LuminPackWriterOptionalState();
        var reader = new LuminPackReader(ref input);
        var writer = new LuminPackWriter(ref storage);
        var jsonReader = new LuminPackJsonReader(ref input, readState);
        var jsonWriter = new LuminPackJsonWriter(ref storage, writeState);
        foreach (int position in new[] { int.MinValue, -129, -1, 0, 1, 127, 128, 129, int.MaxValue })
        {
            reader.Position = writer.Position = jsonReader.Position = jsonWriter.Position = position;
            Assert.Equal(position, reader.Position);
            Assert.Equal(position, writer.Position);
            Assert.Equal(position, jsonReader.CurrentIndex);
            Assert.Equal(position, jsonWriter.CurrentIndex);
            Assert.Equal(1, reader.Cursor.Length);
            Assert.Equal(1, writer.Cursor.Length);
            Assert.Equal(1, jsonReader.Cursor.Length);
            Assert.Equal(1, jsonWriter.Cursor.Length);
        }
    }

    /// <summary>Preserves exact-end acceptance and negative/past-end rejection before either reader moves.</summary>
    [Fact]
    public void ReaderAdvancesKeepBoundsAndFailurePositions()
    {
        Span<byte> storage = stackalloc byte[16];
        ReadOnlySpan<byte> input = storage;
        var state = new LuminPackReaderOptionalState();
        foreach (int position in new[] { 0, 1, 15, 16, 17 })
        foreach (int count in new[] { int.MinValue, -1, 0, 1, 15, 16, 17, int.MaxValue })
        {
            var reader = new LuminPackReader(ref input) { Position = position };
            var json = new LuminPackJsonReader(ref input, state) { Position = position };
            bool invalid = count < 0 || position > input.Length || count > input.Length - position;
            Exception? binaryFailure = null, jsonFailure = null;
            try { reader.Advance(count); } catch (Exception error) { binaryFailure = error; }
            try { json.Advance(count); } catch (Exception error) { jsonFailure = error; }
            Assert.Equal(invalid, binaryFailure is not null);
            Assert.Equal(invalid, jsonFailure is not null);
            Assert.Equal(invalid ? position : position + count, reader.Position);
            Assert.Equal(invalid ? position : position + count, json.Position);
            if (invalid) Assert.Equal($"Span out of range with {(long)position + count}.", binaryFailure!.Message);
        }
    }

    /// <summary>Checks both JSON byte encodings at exact output/input bounds and every shortened quoted token.</summary>
    [Fact]
    public void JsonStringsKeepExactWindowsAndRejectTruncation()
    {
        CheckJson<LuminPackUtf8LengthOptions>(Encoding.UTF8);
        CheckJson<LuminPackUtf16LengthOptions>(Encoding.Unicode);
    }

    /// <summary>Runs one closed JSON profile with movable managed windows across parsing allocations and exceptions.</summary>
    /// <typeparam name="TOptions">The compile-time encoding profile.</typeparam>
    /// <param name="encoding">The matching byte encoding.</param>
    private static void CheckJson<TOptions>(Encoding encoding) where TOptions : struct, ILuminPackOptions
    {
        foreach (string value in new[] { "", "a", "abcd", new string('x', 16), new string('x', 32), new string('x', 33), new string('x', 257), "quote\" slash\\ line\n \u4E2D\u6587 \U0001F600" })
        {
            byte[] buffer = new byte[4096];
            var writeState = new LuminPackWriterOptionalState();
            var readState = new LuminPackReaderOptionalState();
            Span<byte> output = buffer.AsSpan(3, buffer.Length - 6);
            var writer = new LuminPackJsonWriter(ref output, writeState);
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            writer.Write<TOptions>(value);
            int length = writer.Position;
            string wire = encoding.GetString(buffer, 3, length);
            Assert.StartsWith("\"", wire);
            Assert.EndsWith("\"", wire);
            output = buffer.AsSpan(3, length);
            writer = new LuminPackJsonWriter(ref output, writeState);
            writer.Write<TOptions>(value);
            Assert.Equal(length, writer.Position);
            ReadOnlySpan<byte> framed = buffer.AsSpan(0, length + 3);
            var reader = new LuminPackJsonReader(ref framed, readState) { Position = 3 };
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            string restored = null!;
            reader.Read<TOptions>(ref restored);
            reader.EnsureEndOfDocument<TOptions>();
            Assert.Equal(value, restored);
            Assert.Equal(length + 3, reader.Position);
            for (int prefix = 0; prefix < length; prefix++)
            {
                ReadOnlySpan<byte> shortened = buffer.AsSpan(3, prefix);
                var shortReader = new LuminPackJsonReader(ref shortened, readState);
                bool rejected = false;
                try { shortReader.Read<TOptions>(ref restored); shortReader.EnsureEndOfDocument<TOptions>(); }
                catch (Exception) { rejected = true; }
                Assert.True(rejected);
            }
            Assert.Equal(0, buffer[2]);
            Assert.Equal(0, buffer[3 + length]);
        }
    }
}
