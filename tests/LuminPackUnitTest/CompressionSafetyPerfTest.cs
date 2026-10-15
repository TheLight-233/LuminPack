using System.Buffers.Binary;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>
/// Focused corruption, aliasing, and large-window regression coverage for LuminZ.
/// </summary>
internal static class CompressionSafetyPerfTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
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

    /// <summary>Verifies large window maintains compression and round trips.</summary>
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

    /// <summary>Verifies aliased writer round trips without overwriting input.</summary>
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

    /// <summary>Verifies every truncated prefix is rejected.</summary>
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

    /// <summary>Verifies declared output mismatch is rejected.</summary>
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

    /// <summary>Verifies empty frame with trailing body is rejected.</summary>
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

    /// <summary>Verifies extended length cannot exceed declared output.</summary>
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

    /// <summary>Verifies implausible output is rejected before buffer growth.</summary>
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

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="payload">The payload used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static byte[] Compress(byte[] payload)
    {
        using var source = CreateWriter(payload);
        using var destination = new LuminBufferWriter(true);
        source.CompressTo(destination);
        return destination.GetSpan().ToArray();
    }

    /// <summary>Verifies expect invalid frame.</summary>
    /// <param name="frame">The frame used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void ExpectInvalidFrame(byte[] frame, string message)
    {
        using var source = CreateWriter(frame);
        using var destination = new LuminBufferWriter(true);
        ExpectInvalidData(() => source.DecompressTo(destination), message);
    }

    /// <summary>Verifies expect invalid data.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
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

    /// <summary>Verifies build large window payload.</summary>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Verifies build pseudo random payload.</summary>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Creates writer from the supplied input.</summary>
    /// <param name="payload">The payload used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static LuminBufferWriter CreateWriter(ReadOnlySpan<byte> payload)
    {
        var writer = new LuminBufferWriter(true);
        writer.EnsureCapacity(payload.Length);
        payload.CopyTo(writer.DangerousGetBuffer());
        PublishLength(writer, payload.Length);
        return writer;
    }

    /// <summary>Verifies publish length.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    private static void PublishLength(LuminBufferWriter writer, int length)
    {
        var method = typeof(LuminBufferWriter).GetMethod(
            "SetWrittenCount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method is null)
            throw new MissingMethodException(typeof(LuminBufferWriter).FullName, "SetWrittenCount");
        method.Invoke(writer, [length]);
    }

    /// <summary>Verifies run case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
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

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
