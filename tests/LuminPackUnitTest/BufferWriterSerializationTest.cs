using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>Provides buffer writer serialization test functionality.</summary>
internal static class BufferWriterSerializationTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(ExternalBinarySerializationPublishesExactLength),
            ExternalBinarySerializationPublishesExactLength);
        RunCase(results, nameof(ReusedBinaryWriterDropsOldTail),
            ReusedBinaryWriterDropsOldTail);
        RunCase(results, nameof(ExternalJsonSerializationPublishesExactLength),
            ExternalJsonSerializationPublishesExactLength);
        RunCase(results, nameof(ToArrayAndResetReturnsExactPayloadAndClears),
            ToArrayAndResetReturnsExactPayloadAndClears);
        RunCase(results, nameof(WriteToAndResetAsyncWritesPublishedPayload),
            WriteToAndResetAsyncWritesPublishedPayload);
        RunCase(results, nameof(WriteToAndResetCopiesPublishedPayload),
            WriteToAndResetCopiesPublishedPayload);
        RunCase(results, nameof(PoolReturnRentClearsPublishedLength),
            PoolReturnRentClearsPublishedLength);
        RunCase(results, nameof(ResetClearsPublishedPayloadAndAllowsReuse),
            ResetClearsPublishedPayloadAndAllowsReuse);
        RunCase(results, nameof(InPlaceCompressionRoundTripsPublishedPayload),
            InPlaceCompressionRoundTripsPublishedPayload);
        RunCase(results, nameof(AliasedAndResetOperationsAreRejected),
            AliasedAndResetOperationsAreRejected);
        RunCase(results, nameof(NullUnmanagedArrayRoundTrips),
            NullUnmanagedArrayRoundTrips);
        RunCase(results, nameof(Utf16TokenSizeMatchesSerializedLength),
            Utf16TokenSizeMatchesSerializedLength);
    }

    /// <summary>Verifies external binary serialization publishes exact length.</summary>
    private static void ExternalBinarySerializationPublishesExactLength()
    {
        const string value = "external-writer-\u4EAE\u5EA6";
        var expected = LuminPackSerializer.Serialize(value);

        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, writer);

        Assert(writer.CurrentIndex == expected.Length,
            $"CurrentIndex was {writer.CurrentIndex}, expected {expected.Length}.");
        Assert(writer.GetSpan().Length == writer.CurrentIndex,
            "GetSpan did not expose exactly the published bytes.");
        Assert(writer.GetSpan().SequenceEqual(expected),
            "External writer payload differed from the array-returning overload.");
        Assert(LuminPackSerializer.Deserialize<string>(writer.GetSpan()) == value,
            "Published payload could not be deserialized.");
    }

    /// <summary>Verifies reused binary writer drops old tail.</summary>
    private static void ReusedBinaryWriterDropsOldTail()
    {
        var longValue = new string('L', 16 * 1024) + "-long-tail";
        const string shortValue = "short";
        var expectedLong = LuminPackSerializer.Serialize(longValue);
        var expectedShort = LuminPackSerializer.Serialize(shortValue);

        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(longValue, writer);
        Assert(writer.CurrentIndex == expectedLong.Length && writer.GetSpan().SequenceEqual(expectedLong),
            "First serialization did not publish the expected long payload.");

        LuminPackSerializer.Serialize(shortValue, writer);
        Assert(writer.CurrentIndex == expectedShort.Length,
            $"Reused writer kept an old length of {writer.CurrentIndex}; expected {expectedShort.Length}.");
        Assert(writer.GetSpan().Length == expectedShort.Length,
            "Reused writer exposed bytes beyond the short payload.");
        Assert(writer.GetSpan().SequenceEqual(expectedShort),
            "Reused writer retained data from the previous payload.");
        Assert(LuminPackSerializer.Deserialize<string>(writer.GetSpan()) == shortValue,
            "Short payload from the reused writer could not be deserialized.");
    }

    /// <summary>Verifies external json serialization publishes exact length.</summary>
    private static void ExternalJsonSerializationPublishesExactLength()
    {
        var longValue = new string('J', 8 * 1024) + "-json-tail";
        const string shortValue = "json-short";

        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.SerializeJson(longValue, writer);
        var expectedLong = Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson(longValue));
        Assert(writer.CurrentIndex == expectedLong.Length && writer.GetSpan().SequenceEqual(expectedLong),
            "External JSON writer did not publish the expected long payload.");

        LuminPackSerializer.SerializeJson(shortValue, writer);
        var expectedShort = Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson(shortValue));
        Assert(writer.CurrentIndex == expectedShort.Length,
            $"Reused JSON writer kept an old length of {writer.CurrentIndex}; expected {expectedShort.Length}.");
        Assert(writer.GetSpan().Length == expectedShort.Length,
            "Reused JSON writer exposed bytes beyond the short payload.");
        Assert(writer.GetSpan().SequenceEqual(expectedShort),
            "Reused JSON writer retained data from the previous payload.");
    }

    /// <summary>Verifies to array and reset returns exact payload and clears.</summary>
    private static void ToArrayAndResetReturnsExactPayloadAndClears()
    {
        const string value = "to-array-and-reset";
        var expected = LuminPackSerializer.Serialize(value);

        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, writer);
        var actual = writer.ToArrayAndReset();

        Assert(actual.Length == expected.Length,
            $"ToArrayAndReset returned {actual.Length} bytes; expected {expected.Length}.");
        Assert(actual.AsSpan().SequenceEqual(expected),
            "ToArrayAndReset returned bytes outside the published payload.");
        Assert(writer.CurrentIndex == 0, "ToArrayAndReset did not clear CurrentIndex.");
        Assert(writer.GetSpan().IsEmpty, "ToArrayAndReset did not clear the published span.");
        Assert(LuminPackSerializer.Deserialize<string>(actual) == value,
            "ToArrayAndReset payload could not be deserialized.");
    }

    /// <summary>Writes to and reset async writes published payload to the current output.</summary>
    private static void WriteToAndResetAsyncWritesPublishedPayload()
    {
        const string value = "async-stream-payload-\u4EAE\u5EA6";
        var expected = LuminPackSerializer.Serialize(value);

        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, writer);
        using var stream = new MemoryStream();
        writer.WriteToAndResetAsync(stream, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        Assert(stream.ToArray().AsSpan().SequenceEqual(expected),
            "WriteToAndResetAsync did not write the published payload.");
        Assert(writer.CurrentIndex == 0 && writer.GetSpan().IsEmpty,
            "WriteToAndResetAsync did not reset the published length after a successful write.");
    }

    /// <summary>Writes to and reset copies published payload to the current output.</summary>
    private static void WriteToAndResetCopiesPublishedPayload()
    {
        const string value = "sync-writer-payload";
        var expected = LuminPackSerializer.Serialize(value);

        using var source = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, source);
        var destination = new byte[expected.Length];
        var destinationSpan = destination.AsSpan();
        var destinationWriter = new LuminPackWriter(ref destinationSpan);
        source.WriteToAndReset(ref destinationWriter);

        Assert(destinationWriter.CurrentIndex == expected.Length && destination.AsSpan().SequenceEqual(expected),
            "WriteToAndReset did not copy the published payload.");
        Assert(source.CurrentIndex == 0 && source.GetSpan().IsEmpty,
            "WriteToAndReset did not reset the source after a successful copy.");
    }

    /// <summary>Verifies pool return rent clears published length.</summary>
    private static void PoolReturnRentClearsPublishedLength()
    {
        var writer = LuminBufferWriterPool.Rent();
        try
        {
            LuminPackSerializer.Serialize("pooled-buffer-payload", writer);
            Assert(writer.CurrentIndex > 0, "Pool test did not publish a payload before Return.");
        }
        finally
        {
            LuminBufferWriterPool.Return(writer);
        }

        writer = LuminBufferWriterPool.Rent();
        try
        {
            Assert(writer.CurrentIndex == 0,
                $"Rented writer exposed a stale CurrentIndex of {writer.CurrentIndex}.");
            Assert(writer.GetSpan().IsEmpty, "Rented writer exposed stale bytes from its previous owner.");
        }
        finally
        {
            LuminBufferWriterPool.Return(writer);
        }
    }

    /// <summary>Verifies reset clears published payload and allows reuse.</summary>
    private static void ResetClearsPublishedPayloadAndAllowsReuse()
    {
        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize("before-reset", writer);
        Assert(writer.CurrentIndex > 0, "Reset test did not publish an initial payload.");

        writer.Reset();
        Assert(writer.CurrentIndex == 0 && writer.GetSpan().IsEmpty,
            "Reset did not clear a completed serialization.");

        const string value = "after-reset";
        LuminPackSerializer.Serialize(value, writer);
        Assert(LuminPackSerializer.Deserialize<string>(writer.GetSpan()) == value,
            "Buffer writer could not be reused after Reset released its buffer.");
    }

    /// <summary>Verifies in place compression round trips published payload.</summary>
    private static void InPlaceCompressionRoundTripsPublishedPayload()
    {
        var value = new string('A', 12 * 1024) + new string('B', 4 * 1024);

        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, writer);
        var original = writer.GetSpan().ToArray();

        var compressedLength = writer.Compress();
        Assert(compressedLength == writer.CurrentIndex,
            "Compress did not publish the compressed length.");
        Assert(writer.GetSpan().Length == compressedLength,
            "Compress exposed bytes beyond the compressed payload.");
        Assert(compressedLength < original.Length,
            "Compression test payload was not compressed.");

        var decompressedLength = writer.Decompress();
        Assert(decompressedLength == original.Length,
            $"Decompress returned {decompressedLength} bytes; expected {original.Length}.");
        Assert(writer.CurrentIndex == original.Length && writer.GetSpan().Length == original.Length,
            "Decompress did not publish the restored payload length.");
        Assert(writer.GetSpan().SequenceEqual(original),
            "In-place compression round-trip changed the serialized payload.");
        Assert(LuminPackSerializer.Deserialize<string>(writer.GetSpan()) == value,
            "Decompressed payload could not be deserialized.");
    }

    /// <summary>Verifies aliased and reset operations are rejected.</summary>
    private static void AliasedAndResetOperationsAreRejected()
    {
        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(new string('x', 1024), writer);
        var original = writer.GetSpan().ToArray();

        AssertThrows(() => writer.CompressToAndReset(writer),
            "CompressToAndReset accepted the same source and destination.");
        Assert(writer.GetSpan().SequenceEqual(original),
            "Rejected aliased CompressToAndReset cleared or changed the source.");

        writer.Compress();
        var compressed = writer.GetSpan().ToArray();
        AssertThrows(() => writer.DecompressToAndReset(writer),
            "DecompressToAndReset accepted the same source and destination.");
        Assert(writer.GetSpan().SequenceEqual(compressed),
            "Rejected aliased DecompressToAndReset cleared or changed the source.");
    }

    /// <summary>Verifies null unmanaged array round trips.</summary>
    private static void NullUnmanagedArrayRoundTrips()
    {
        int[]? value = null;
        var payload = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<int[]?>(payload);
        Assert(result is null, "A null unmanaged array was not preserved.");
    }

    /// <summary>Verifies utf16 token size matches serialized length.</summary>
    private static void Utf16TokenSizeMatchesSerializedLength()
    {
        var option = LuminPackSerializerOption.Token with
        {
            StringEncoding = LuminPackStringEncoding.UTF16
        };

        foreach (var value in new string?[] { null, string.Empty, "A", "\u4EAE\u5EA6", "emoji😀" })
        {
            var payload = LuminPackSerializer.Serialize(value, option);
            var size = LuminPackSerializer.Sizeof(value, option);
            Assert(size == payload.Length,
                $"UTF-16 token Sizeof returned {size}, but serialization wrote {payload.Length} bytes.");
            Assert(LuminPackSerializer.Deserialize<string?>(payload, option) == (value ?? string.Empty),
                "UTF-16 token string did not round-trip according to the protocol's null/empty normalization.");
        }
    }

    /// <summary>Checks that generated fixed-size writes validate capacity before modifying the destination.</summary>
    private static void GeneratedFixedBlockChecksSpanBeforeWriting()
    {
        var value = new FixedBlockSafetyModel
        {
            First = 0x10203040,
            Second = 0x1020304050607080,
            Third = 0x1234
        };
        var expected = LuminPackSerializer.Serialize(value);

        var guarded = new byte[expected.Length + 2];
        guarded[0] = 0xA5;
        guarded[^1] = 0x5A;
        var exact = guarded.AsSpan(1, expected.Length);
        var exactWriter = new LuminPackWriter(ref exact);
        exactWriter.WriteValue(value);

        Assert(exactWriter.CurrentIndex == expected.Length,
            "Generated fixed block published an unexpected length.");
        Assert(exact.SequenceEqual(expected),
            "Generated fixed block changed the serialized representation.");
        Assert(guarded[0] == 0xA5 && guarded[^1] == 0x5A,
            "Generated fixed block overwrote an exact-span canary.");

        var tooSmallBuffer = new byte[expected.Length + 1];
        tooSmallBuffer[0] = 0xA5;
        tooSmallBuffer[^1] = 0x5A;
        var tooSmall = tooSmallBuffer.AsSpan(1, expected.Length - 1);
        tooSmall.Fill(0xCC);
        var shortWriter = new LuminPackWriter(ref tooSmall);
        var threw = false;
        try
        {
            shortWriter.WriteValue(value);
        }
        catch (Exception ex) when (ex.Message.StartsWith("Span out of range", StringComparison.Ordinal))
        {
            threw = true;
        }

        Assert(threw, "Generated fixed block accepted a destination one byte too short.");
        Assert(tooSmallBuffer[0] == 0xA5 && tooSmallBuffer[^1] == 0x5A,
            "Generated fixed block overwrote a short-span canary before throwing.");
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
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Verifies assert throws.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}

/// <summary>Provides fixed block safety model functionality.</summary>
[LuminPackable]
public sealed class FixedBlockSafetyModel
{
    /// <summary>Gets or sets the first.</summary>
    public int First { get; set; }
    /// <summary>Gets or sets the second.</summary>
    public long Second { get; set; }
    /// <summary>Gets or sets the third.</summary>
    public short Third { get; set; }
}
