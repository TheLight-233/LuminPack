using System.Buffers.Binary;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Preserves the approved all-build Reader checks at fixed cursor and dynamic record boundaries.</summary>
public class ReaderFixedBoundsPolicyTest
{
    /// <summary>Rejects negative, overflowing and overlong JSON cursor movement without changing its position.</summary>
    /// <param name="count">The invalid advance width.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(5)]
    [InlineData(int.MaxValue)]
    public unsafe void JsonAdvanceChecksInEveryBuild(int count)
    {
        byte[] backing = new byte[4];
        fixed (byte* pinned = backing)
        {
            ReadOnlySpan<byte> bytes = new(pinned, backing.Length);
            var reader = new LuminPackJsonReader(ref bytes, new LuminPackReaderOptionalState());
            Exception? failure = null;
            try { reader.Advance(count); }
            catch (Exception error) { failure = error; }
            Assert.NotNull(failure);
            Assert.Equal(0, reader.Position);
            reader.Advance(backing.Length);
            Assert.Equal(backing.Length, reader.Position);
        }
    }
    /// <summary>Rejects a fixed cursor advance beyond the input in both Debug and Release.</summary>
    [Fact]
    public void FixedAdvanceChecksInEveryBuild()
    {
        ReadOnlySpan<byte> bytes = new byte[4];
        var reader = new LuminPackReader(ref bytes);
        Exception? failure = null;
        try
        {
            reader.Advance(5);
        }
        catch (Exception error)
        {
            failure = error;
        }
        Assert.NotNull(failure);
        Assert.Equal(0, reader.Position);
    }

    /// <summary>Rejects a generated fixed-width Nullable advance beyond the supplied input window.</summary>
    /// <param name="length">The truncated input width, excluding at least one payload byte.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void GeneratedFixedAdvanceChecksInEveryBuild(int length)
    {
        // Retain complete backing storage so an address-only kernel stays inside the allocation;
        // the advancing Reader must still reject its shorter logical input window.
        byte[] record = [0, 137, 0, 0, 0];
        int? destination = null;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(record.AsSpan(0, length), ref destination));
    }

    /// <summary>Preserves the exact Nullable scalar marker, payload, size and profile-independent representation.</summary>
    [Fact]
    public void FixedNullableValuesKeepTheirBytes()
    {
        foreach (int? source in new int?[] { null, 0, 137, -1, int.MinValue, int.MaxValue })
        {
            byte[] expected = source.HasValue ? new byte[5] : [255];
            if (source.HasValue) BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(1), source.Value);
            byte[] record = LuminPackSerializer.Serialize(source);
            Assert.Equal(expected, record);
            Assert.Equal(expected, LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(source));
            int? destination = 719;
            Assert.Equal(record.Length, LuminPackSerializer.Deserialize(record.AsSpan(), ref destination));
            Assert.Equal(source, destination);
        }
        Guid? identifier = Guid.Parse("2ED4AAC7-847C-4BB5-8EC4-E0212F6ABD91");
        byte[] identifierRecord = LuminPackSerializer.Serialize(identifier);
        Assert.Equal(17, identifierRecord.Length);
        Assert.Equal(0, identifierRecord[0]);
        Assert.Equal(identifier.Value.ToByteArray(), identifierRecord.AsSpan(1).ToArray());
        Guid? identifierCopy = null;
        Assert.Equal(identifierRecord.Length, LuminPackSerializer.Deserialize(identifierRecord.AsSpan(), ref identifierCopy));
        Assert.Equal(identifier, identifierCopy);
        long? wide = long.MinValue;
        byte[] wideRecord = LuminPackSerializer.Serialize(wide);
        Assert.Equal(9, wideRecord.Length);
        Assert.Equal(wide.Value, BinaryPrimitives.ReadInt64LittleEndian(wideRecord.AsSpan(1)));
        long? wideCopy = null;
        Assert.Equal(wideRecord.Length, LuminPackSerializer.Deserialize(wideRecord.AsSpan(), ref wideCopy));
        Assert.Equal(wide, wideCopy);
    }

    /// <summary>Rejects a dynamic bulk read before copying into the destination in every configuration.</summary>
    [Fact]
    public void DynamicSpanReadValidatesBeforeMutation()
    {
        ReadOnlySpan<byte> bytes = new byte[3];
        var reader = new LuminPackReader(ref bytes);
        int[] destination = [137];
        Exception? failure = null;
        try
        {
            reader.Read(destination.AsSpan());
        }
        catch (Exception error)
        {
            failure = error;
        }
        Assert.NotNull(failure);
        Assert.Equal(137, destination[0]);
        Assert.Equal(0, reader.Position);
    }

    /// <summary>Validates complete collection payloads independently of the fixed-access assertion API.</summary>
    [Fact]
    public void DynamicPayloadGuardRemainsActive()
    {
        ReadOnlySpan<byte> bytes = new byte[4];
        var reader = new LuminPackReader(ref bytes);
        Exception? failure = null;
        try
        {
            reader.EnsureCollectionPayload(0, 5);
        }
        catch (Exception error)
        {
            failure = error;
        }
        Assert.NotNull(failure);
        Assert.Equal(0, reader.Position);
    }

    /// <summary>Rejects a truncated raw Queue payload even when its count and stored metadata fit the input.</summary>
    [Fact]
    public void QueuePayloadGuardRemainsActive()
    {
        var source = new Queue<int>([11, 29]);
        byte[] record = LuminPackSerializer.Serialize(source);
        Assert.Equal(24, record.Length);
        byte[] truncated = record[..^sizeof(int)];
        Queue<int>? destination = null;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(truncated.AsSpan(), ref destination));
        Assert.NotNull(destination);
#if NET8_0_OR_GREATER
        Assert.All(LuminPackMarshal.GetQueueSpan(destination).ToArray(), item => Assert.Equal(0, item));
#else
        // The legacy field-wise path preserves completed elements; it must not read or publish the truncated one.
        Assert.Equal(new[] { 11, 0 }, LuminPackMarshal.GetQueueSpan(destination).ToArray());
#endif
    }

    /// <summary>Rejects impossible allocation counts before allocating a Queue destination.</summary>
    /// <param name="count">The invalid encoded collection count.</param>
    [Theory]
    [InlineData(-2)]
    [InlineData(int.MaxValue)]
    public void InvalidQueueCountsDoNotAllocate(int count)
    {
        byte[] record = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(record, count);
        Queue<int>? destination = null;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(record.AsSpan(), ref destination));
        Assert.Null(destination);
    }

    /// <summary>Retains Queue framing and fresh/reused storage across empty, small and larger records.</summary>
    /// <param name="count">The number of values in the complete record.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(1000)]
    public void ValidQueueRecordsKeepTheirSemantics(int count)
    {
        var source = new Queue<int>(Enumerable.Range(0, count));
        byte[] record = LuminPackSerializer.Serialize(source);
        Queue<int>? fresh = null;
        Assert.Equal(record.Length, LuminPackSerializer.Deserialize(record.AsSpan(), ref fresh));
        Assert.Equal(source, fresh);
        var reused = new Queue<int>([7, 8, 9]);
        Queue<int>? destination = reused;
        Assert.Equal(record.Length, LuminPackSerializer.Deserialize(record.AsSpan(), ref destination));
        Assert.Same(reused, destination);
        Assert.Equal(source, destination);
    }
}
