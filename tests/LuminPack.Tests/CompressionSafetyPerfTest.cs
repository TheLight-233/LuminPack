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
        RunCase(results, nameof(CompactLengthBoundariesRoundTrip), CompactLengthBoundariesRoundTrip);
        RunCase(results, nameof(PeriodicMatchesStayInsideDestination), PeriodicMatchesStayInsideDestination);
        RunCase(results, nameof(MatchCountersFindEveryMismatch), MatchCountersFindEveryMismatch);
        RunCase(results, nameof(CompactLengthsRejectMalformedFrames), CompactLengthsRejectMalformedFrames);
        RunCase(results, nameof(CompressedBoundsRejectOverflow), CompressedBoundsRejectOverflow);
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
            (byte)'L', (byte)'U', (byte)'M', (byte)'2',
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
        frame[3] = (byte)'2';
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
            (byte)'L', (byte)'U', (byte)'M', (byte)'2',
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

    /// <summary>Exercises compact-length transitions, stale tables, exact buffers and high-ratio allocating reads.</summary>
    private static void CompactLengthBoundariesRoundTrip()
    {
        foreach (int length in new[] { 0, 1, 14, 15, 19, 20, 21, 142, 143, 146, 147, 254, 255, 256, 269, 270, 273, 274, 397, 398, 401, 402,
                     16398, 16399, 16402, 16403, 65535, 65536, 131073, 2097171 })
        foreach (bool repetitive in new[] { false, true })
        {
            byte[] input = BuildPseudoRandomPayload(length);
            if (repetitive)
                input.AsSpan().Fill(0x41);
            int bound = LuminCompressor.GetMaxCompressedSize(length);
            byte[] buffer = Enumerable.Repeat((byte)0xA5, bound + 16).ToArray();
            int count = LuminCompressor.Compress(input, buffer.AsSpan(7, bound));
            Assert(count <= bound, "Compression exceeded its advertised bound.");
            Assert(buffer.AsSpan(0, 7).ToArray().All(b => b == 0xA5)
                && buffer.AsSpan(7 + count).ToArray().All(b => b == 0xA5), "Compression overwrote its exact output.");
            byte[] frame = buffer.AsSpan(7, count).ToArray();
            Assert(frame.AsSpan(0, 4).SequenceEqual("LUM2"u8), "The compact frame signature changed.");
            Assert(LuminCompressor.GetDecompressedSize(frame) == length, "The declared output length changed.");
            Assert(LuminCompressor.Decompress(frame).AsSpan().SequenceEqual(input), "A compact-length round trip failed.");
            if (repetitive && length >= 65536)
                Assert(count <= 22, "A long repeated run did not use a compact match length.");
        }
    }

    /// <summary>Builds exact frames for every short distance and vector boundary, including overlapping matches.</summary>
    private static void PeriodicMatchesStayInsideDestination()
    {
        int[] distances = Enumerable.Range(1, 17).Concat(new[] { 31, 32, 33, 63, 64, 65, 127, 256, 65535 }).ToArray();
        int[] lengths = Enumerable.Range(4, 65).Concat(new[] { 127, 128, 129, 255, 256, 257, 1023, 4097 }).ToArray();
        foreach (int distance in distances)
        foreach (int length in lengths)
        {
            byte[] expected = new byte[distance + length + 5];
            for (int i = 0; i < distance; i++)
                expected[i] = (byte)(i * 17 + 31);
            for (int i = distance; i < distance + length; i++)
                expected[i] = expected[i - distance];
            expected.AsSpan(distance + length).Fill(0xE3);
            var frame = new List<byte>("LUM2"u8.ToArray());
            frame.AddRange(BitConverter.GetBytes(expected.Length));
            frame.Add((byte)((Math.Min(distance, 15) << 4) | Math.Min(length - 4, 15)));
            if (distance >= 15)
                AppendLength(frame, distance - 15);
            frame.AddRange(expected.AsSpan(0, distance).ToArray());
            frame.Add((byte)distance);
            frame.Add((byte)(distance >> 8));
            if (length >= 19)
                AppendLength(frame, length - 19);
            frame.Add(0x50);
            frame.AddRange(expected.AsSpan(distance + length).ToArray());
            byte[] destination = Enumerable.Repeat((byte)0xA5, expected.Length + 16).ToArray();
            Assert(LuminCompressor.Decompress(frame.ToArray(), destination.AsSpan(7, expected.Length)) == expected.Length,
                "A periodic frame returned the wrong output length.");
            Assert(destination.AsSpan(7, expected.Length).SequenceEqual(expected),
                $"Periodic expansion failed for distance {distance}, length {length}.");
            Assert(destination.AsSpan(0, 7).ToArray().All(b => b == 0xA5)
                && destination.AsSpan(7 + expected.Length).ToArray().All(b => b == 0xA5),
                "Periodic expansion crossed the destination boundary.");
        }
    }

    /// <summary>Counts equal bytes through the production bounded match entry.</summary>
    /// <param name="left">The later input position.</param>
    /// <param name="right">The candidate input position.</param>
    /// <param name="end">The exclusive end of the later input.</param>
    /// <returns>The equal prefix length.</returns>
    private delegate int MatchCounter(ref byte left, ref byte right, ref byte end);

    /// <summary>Checks all mismatch positions across four-vector groups and bounded tails.</summary>
    private static void MatchCountersFindEveryMismatch()
    {
        var method = typeof(LuminCompressor).GetMethod("ExtendMatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var count = (MatchCounter)method.CreateDelegate(typeof(MatchCounter));
        byte[] left = BuildPseudoRandomPayload(1040);
        byte[] right = (byte[])left.Clone();
        foreach (int start in new[] { 0, 1, 7 })
        foreach (int length in new[] { 0, 1, 7, 8, 15, 16, 17, 31, 32, 33, 63, 64, 65, 127, 128, 129, 255, 256, 257, 1024 })
        {
            Assert(count(ref left[start], ref right[start], ref left[start + length]) == length,
                "An equal prefix stopped early.");
            for (int mismatch = 0; mismatch < length; mismatch++)
            {
                right[start + mismatch] ^= 0x80;
                Assert(count(ref left[start], ref right[start], ref left[start + length]) == mismatch,
                    $"Mismatch at {mismatch} of {length} bytes was not found exactly.");
                right[start + mismatch] ^= 0x80;
            }
        }
    }

    /// <summary>Rejects noncanonical, truncated and overflowing lengths before growth for high-ratio frames.</summary>
    private static void CompactLengthsRejectMalformedFrames()
    {
        byte[] expected = [(byte)'L', (byte)'U', (byte)'M', (byte)'2', 0x98, 1, 0, 0,
            0x1F, 0x41, 1, 0, 0xFF, 0x80, 1, 0x50, 0x41, 0x41, 0x41, 0x41, 0x41];
        byte[] repeated = Enumerable.Repeat((byte)0x41, 408).ToArray();
        Assert(LuminCompressor.Compress(repeated).AsSpan().SequenceEqual(expected),
            "The escaped-length wire fixture changed.");
        Assert(LuminCompressor.Decompress(expected).AsSpan().SequenceEqual(repeated),
            "The escaped-length wire fixture was not decoded.");
        var maximum = new List<byte>("LUM2"u8.ToArray());
        maximum.AddRange(BitConverter.GetBytes(int.MaxValue));
        maximum.AddRange(new byte[] { 0x1F, 0x41, 1, 0 });
        AppendLength(maximum, int.MaxValue - 20);
        maximum.Add(0);
        Assert(LuminCompressor.GetDecompressedSize(maximum.ToArray()) == int.MaxValue,
            "The largest structurally valid length could not be inspected without allocation.");
        foreach (byte[] extension in new byte[][] { [0x80], [0x80, 0], [0xFF, 0xFF, 0xFF, 0xFF, 8],
                     [0x80, 0x80, 0x80, 0x80, 0x80, 1], [0xFF, 0xFF, 0xFF, 0xFF, 7] })
        {
            var frame = new List<byte>("LUM2"u8.ToArray());
            frame.AddRange(BitConverter.GetBytes(1_000_000));
            frame.AddRange(new byte[] { 0x1F, 0x41, 1, 0 });
            frame.Add(255);
            frame.AddRange(extension);
            byte[] bytes = frame.ToArray();
            ExpectInvalidData(() => LuminCompressor.GetDecompressedSize(bytes), "A malformed compact length was accepted.");
            ExpectInvalidFrame(bytes, "A malformed high-ratio frame was accepted.");
        }
        byte[] valid = LuminCompressor.Compress(new byte[65536]);
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] truncated = valid.AsSpan(0, length).ToArray();
            ExpectInvalidFrame(truncated, "A truncated high-ratio frame was accepted.");
        }
        valid[3] = (byte)'Z';
        ExpectInvalidFrame(valid, "An earlier frame signature was accepted as compact lengths.");
    }

    /// <summary>Checks the advertised compression bound at its representable limits.</summary>
    private static void CompressedBoundsRejectOverflow()
    {
        Assert(LuminCompressor.GetMaxCompressedSize(0) == 8, "The empty-frame bound changed.");
        Assert(LuminCompressor.GetMaxCompressedSize(2_130_836_464) == int.MaxValue, "The maximum bound overflowed.");
        foreach (int length in new[] { -1, int.MinValue, 2_130_836_465, int.MaxValue })
        {
            try
            {
                LuminCompressor.GetMaxCompressedSize(length);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }
            throw new InvalidOperationException("An unrepresentable compression bound was accepted.");
        }
    }

    /// <summary>Appends a canonical base-128 length to an independently constructed wire fixture.</summary>
    /// <param name="frame">The frame receiving the extension.</param>
    /// <param name="length">The nonnegative extension value.</param>
    private static void AppendLength(List<byte> frame, int length)
    {
        if (length < 255)
        {
            frame.Add((byte)length);
            return;
        }
        frame.Add(255);
        uint value = (uint)(length - 255);
        do
        {
            uint next = value & 127;
            value >>= 7;
            frame.Add((byte)(next | (value == 0 ? 0u : 128u)));
        }
        while (value != 0);
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
