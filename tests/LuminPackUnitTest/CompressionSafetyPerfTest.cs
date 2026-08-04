using System.Buffers.Binary;
using System.IO;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>
/// Focused corruption, aliasing, and large-window regression coverage for LuminZ.
/// </summary>
internal static class CompressionSafetyPerfTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(LargeWindowMaintainsCompressionAndRoundTrips),
            LargeWindowMaintainsCompressionAndRoundTrips);
        RunCase(results, nameof(AliasedWriterRoundTripsWithoutOverwritingInput),
            AliasedWriterRoundTripsWithoutOverwritingInput);
        RunCase(results, nameof(EveryTruncatedPrefixIsRejected),
            EveryTruncatedPrefixIsRejected);
        RunCase(results, nameof(DeclaredOutputMismatchIsRejected),
            DeclaredOutputMismatchIsRejected);
        RunCase(results, nameof(EmptyFrameWithTrailingBodyIsRejected),
            EmptyFrameWithTrailingBodyIsRejected);
        RunCase(results, nameof(ExtendedLengthCannotExceedDeclaredOutput),
            ExtendedLengthCannotExceedDeclaredOutput);
        RunCase(results, nameof(ImplausibleOutputIsRejectedBeforeBufferGrowth),
            ImplausibleOutputIsRejectedBeforeBufferGrowth);
    }

    private static void LargeWindowMaintainsCompressionAndRoundTrips()
    {
        // Short, locally repeated records force the matcher to keep scanning
        // after both the 64 KiB and 128 KiB position-wrap boundaries.
        byte[] payload = BuildLargeWindowPayload(192 * 1024);
        using var source = CreateWriter(payload);
        using var compressed = new LuminBufferWriter(true);
        using var restored = new LuminBufferWriter(true);

        int compressedLength = source.CompressTo(compressed);
        Assert(compressedLength == compressed.CurrentIndex,
            "CompressTo did not publish its exact output length.");
        Assert(compressedLength < payload.Length / 2,
            $"Large-window compression regressed: {compressedLength} bytes for {payload.Length} input bytes.");

        int restoredLength = compressed.DecompressTo(restored);
        Assert(restoredLength == payload.Length,
            $"Decompressed {restoredLength} bytes; expected {payload.Length}.");
        Assert(restored.GetSpan().SequenceEqual(payload),
            "Large-window compression round-trip changed the payload.");
    }

    private static void AliasedWriterRoundTripsWithoutOverwritingInput()
    {
        byte[] payload = BuildLargeWindowPayload(96 * 1024);
        using var buffer = CreateWriter(payload);

        int compressedLength = buffer.CompressTo(buffer);
        Assert(compressedLength == buffer.CurrentIndex && compressedLength < payload.Length,
            "Aliased CompressTo did not publish a valid compressed payload.");

        int restoredLength = buffer.DecompressTo(buffer);
        Assert(restoredLength == payload.Length,
            $"Aliased DecompressTo restored {restoredLength} bytes; expected {payload.Length}.");
        Assert(buffer.GetSpan().SequenceEqual(payload),
            "Using the same LuminBufferWriter as source and destination corrupted the payload.");
    }

    private static void EveryTruncatedPrefixIsRejected()
    {
        byte[] payload = BuildPseudoRandomPayload(257);
        byte[] frame = Compress(payload);
        using var truncated = CreateWriter(frame);
        using var destination = new LuminBufferWriter(true);

        for (int length = 0; length < frame.Length; length++)
        {
            PublishLength(truncated, length);
            ExpectInvalidData(
                () => truncated.DecompressTo(destination),
                $"A {length}-byte prefix of a {frame.Length}-byte frame was accepted.");
        }

        PublishLength(truncated, frame.Length);
        Assert(truncated.DecompressTo(destination) == payload.Length,
            "The complete frame failed after truncated-prefix validation.");
        Assert(destination.GetSpan().SequenceEqual(payload),
            "The complete frame round-trip changed the payload.");
    }

    private static void DeclaredOutputMismatchIsRejected()
    {
        byte[] payload = BuildPseudoRandomPayload(511);
        byte[] frame = Compress(payload);
        int declaredLength = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(4));

        byte[] tooLong = (byte[])frame.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(tooLong.AsSpan(4), declaredLength + 1);
        ExpectInvalidFrame(tooLong, "A frame that produced fewer bytes than declared was accepted.");

        byte[] tooShort = (byte[])frame.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(tooShort.AsSpan(4), declaredLength - 1);
        ExpectInvalidFrame(tooShort, "A frame that produced more bytes than declared was accepted.");
    }

    private static void EmptyFrameWithTrailingBodyIsRejected()
    {
        byte[] frame =
        {
            (byte)'L', (byte)'U', (byte)'M', (byte)'Z',
            0, 0, 0, 0,
            0
        };

        ExpectInvalidFrame(frame, "An empty frame with a trailing body byte was accepted.");
    }

    private static void ExtendedLengthCannotExceedDeclaredOutput()
    {
        byte[] frame = new byte[8 + 1 + 64];
        frame[0] = (byte)'L';
        frame[1] = (byte)'U';
        frame[2] = (byte)'M';
        frame[3] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), 1);
        frame[8] = 0xF0;
        frame.AsSpan(9).Fill(byte.MaxValue);

        ExpectInvalidFrame(frame,
            "A literal-length chain larger than the declared output was accepted.");
    }

    private static void ImplausibleOutputIsRejectedBeforeBufferGrowth()
    {
        byte[] frame =
        {
            (byte)'L', (byte)'U', (byte)'M', (byte)'Z',
            0xFF, 0xFF, 0xFF, 0x7F,
            0
        };

        using var source = CreateWriter(frame);
        using var destination = new LuminBufferWriter(true);
        int originalCapacity = destination.TotalLength;
        ExpectInvalidData(
            () => source.DecompressTo(destination),
            "A tiny frame declaring an implausibly large output was accepted.");
        Assert(destination.TotalLength == originalCapacity,
            "The destination grew before the implausible output length was rejected.");
    }

    private static byte[] Compress(byte[] payload)
    {
        using var source = CreateWriter(payload);
        using var destination = new LuminBufferWriter(true);
        source.CompressTo(destination);
        return destination.GetSpan().ToArray();
    }

    private static void ExpectInvalidFrame(byte[] frame, string message)
    {
        using var source = CreateWriter(frame);
        using var destination = new LuminBufferWriter(true);
        ExpectInvalidData(() => source.DecompressTo(destination), message);
    }

    private static void ExpectInvalidData(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static byte[] BuildLargeWindowPayload(int length)
    {
        const int RecordSize = 32;
        byte[] payload = new byte[length];

        for (int record = 0, offset = 0; offset < payload.Length; record++, offset += RecordSize)
        {
            int recordLength = Math.Min(RecordSize, payload.Length - offset);
            int prefixLength = Math.Min(24, recordLength);
            for (int i = 0; i < prefixLength; i++)
                payload[offset + i] = (byte)(0x31 + (i * 17));

            uint state = unchecked((uint)record * 2_654_435_761u + 0x9E37_79B9u);
            for (int i = prefixLength; i < recordLength; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                payload[offset + i] = (byte)state;
            }
        }

        return payload;
    }

    private static byte[] BuildPseudoRandomPayload(int length)
    {
        byte[] payload = new byte[length];
        uint state = 0xA341_316Cu;
        for (int i = 0; i < payload.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            payload[i] = (byte)state;
        }

        return payload;
    }

    private static LuminBufferWriter CreateWriter(ReadOnlySpan<byte> payload)
    {
        var writer = new LuminBufferWriter(true);
        if (payload.Length > writer.DangerousGetBuffer().Length)
        {
            writer.Dispose();
            throw new ArgumentOutOfRangeException(nameof(payload),
                "Test payload exceeds the writer's initial buffer.");
        }

        payload.CopyTo(writer.DangerousGetBuffer());
        PublishLength(writer, payload.Length);
        return writer;
    }

    private static void PublishLength(LuminBufferWriter writer, int length)
    {
        var method = typeof(LuminBufferWriter).GetMethod(
            "SetWrittenCount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method is null)
            throw new MissingMethodException(typeof(LuminBufferWriter).FullName, "SetWrittenCount");
        method.Invoke(writer, [length]);
    }

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
