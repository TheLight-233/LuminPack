using System.Buffers.Binary;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Internal;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Preserves the approved all-build Reader checks at fixed cursor and dynamic record boundaries.</summary>
public class ReaderFixedBoundsPolicyTest
{
    /// <summary>Preserves negative-width rejection, past-end rejection, zero-width behavior and wide diagnostic arithmetic.</summary>
    [Fact]
    public void CursorBoundsDiagnosticsKeepRequestedWidthsAndMessages()
    {
        byte[] storage = new byte[64];
        foreach (int position in new[] {0,1,8,9})
        foreach (int count in new[] {int.MinValue,-1,0,1,7,8,9,int.MaxValue})
        foreach (bool advance in new[] {false,true})
        {
            ReadOnlySpan<byte> window = storage.AsSpan(0,8);
            var reader = new LuminPackReader(ref window) {Position = position};
            bool invalid = count < 0 || (long)(8-position) < (long)(uint)count;
            Exception? failure = null;
            try { if (advance) reader.Advance(count); else reader.EnsureReadable(count); }
            catch (Exception error) { failure = error; }
            if (invalid)
            {
                Assert.NotNull(failure);
                Assert.Equal($"Span out of range with {(long)position+count}.",failure.Message);
                Assert.Contains("LuminPackException",failure.GetType().FullName);
                Assert.Equal(position,reader.Position);
            }
            else
            {
                Assert.Null(failure);
                Assert.Equal(position+(advance?count:0),reader.Position);
            }
        }
    }


    /// <summary>Compares fused version prefixes with the established header and metadata operations.</summary>
    [Fact]
    public void FixedVersionPrefixesKeepEveryCompiledProfile()
    {
        CheckVersionPrefix<LuminPackDefaultOptions>();
        CheckVersionPrefix<LuminPackUtf8LengthOptions>();
        CheckVersionPrefix<LuminPackUtf8TokenOptions>();
        CheckVersionPrefix<LuminPackUtf16TokenOptions>();
        CheckVersionPrefix<LuminPackUtf8VarIntOptions>();
        CheckVersionPrefix<LuminPackUtf16VarIntOptions>();
    }

    /// <summary>Checks exact-capacity prefix writes and surrounding guards with one closed profile.</summary>
    /// <typeparam name="TOptions">The actual document profile.</typeparam>
    private static void CheckVersionPrefix<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        foreach (int length in new[] { 0, 1, 3, 4, 15, 16, 31, 32, 127, 128 })
        {
            byte[] metadata = Enumerable.Range(0, length).Select(index => (byte)(index * 17)).ToArray();
            byte[] prefix = LuminPackVersionField.CreateDefaultPrefix(metadata, 8);
            byte[] actual = Enumerable.Repeat((byte)0xCC, prefix.Length + 16).ToArray();
            byte[] expected = (byte[])actual.Clone();
            Span<byte> target = actual.AsSpan(8, prefix.Length);
            Span<byte> reference = expected.AsSpan(8, prefix.Length);
            var writer = new LuminPackWriter(ref target);
            var original = new LuminPackWriter(ref reference);
            writer.WriteVersionPrefix<TOptions>(prefix);
            original.WriteVersionHeader<TOptions>(8);
            original.WriteVersionMetadata(metadata, 0);
            Assert.Equal(expected, actual);
            Assert.Equal(prefix.Length, writer.Position);
        }
    }

    /// <summary>Retains every tagged integer representation, checked conversion and truncated-payload failure.</summary>
    [Fact]
    public void TaggedReferenceIntegersKeepCheckedReads()
    {
        foreach (sbyte code in new[] { VarIntCodes.Byte, VarIntCodes.SByte, VarIntCodes.UInt16, VarIntCodes.Int16,
                     VarIntCodes.UInt32, VarIntCodes.Int32, VarIntCodes.UInt64, VarIntCodes.Int64 })
        {
            Span<byte> buffer = new byte[16];
            var writer = new LuminPackWriter(ref buffer);
            writer.Write(code);
            switch (code)
            {
                case VarIntCodes.Byte: writer.Write((byte)42); break;
                case VarIntCodes.SByte: writer.Write((sbyte)42); break;
                case VarIntCodes.UInt16: writer.Write((ushort)42); break;
                case VarIntCodes.Int16: writer.Write((short)42); break;
                case VarIntCodes.UInt32: writer.Write(42u); break;
                case VarIntCodes.Int32: writer.Write(42); break;
                case VarIntCodes.UInt64: writer.Write(42ul); break;
                case VarIntCodes.Int64: writer.Write(42L); break;
            }
            ReadOnlySpan<byte> input = buffer[..writer.Position];
            var reader = new LuminPackReader(ref input);
            Assert.Equal(42, reader.ReadVarIntInt32());
            Assert.Equal(input.Length, reader.Position);
            reader = new LuminPackReader(ref input);
            Assert.Equal(42u, reader.ReadVarIntUInt32());
            Assert.Equal(input.Length, reader.Position);
            for (int length = 0; length < input.Length; length++)
            {
                ReadOnlySpan<byte> shortened = input[..length];
                var shortReader = new LuminPackReader(ref shortened);
                Exception? failure = null;
                try { shortReader.ReadVarIntInt32(); } catch (Exception error) { failure = error; }
                Assert.NotNull(failure);
                Assert.InRange(shortReader.Position, 0, length);
                shortReader = new LuminPackReader(ref shortened);
                failure = null;
                try { shortReader.ReadVarIntUInt32(); } catch (Exception error) { failure = error; }
                Assert.NotNull(failure);
                Assert.InRange(shortReader.Position, 0, length);
            }
        }
        foreach (sbyte value in new sbyte[] { -120, -1, 0, 127 })
        {
            ReadOnlySpan<byte> input = new[] { unchecked((byte)value) };
            var reader = new LuminPackReader(ref input);
            Assert.Equal(value, reader.ReadVarIntInt32());
            reader = new LuminPackReader(ref input);
            if (value >= 0) Assert.Equal((uint)value, reader.ReadVarIntUInt32());
            else
            {
                Exception? failure = null;
                try { reader.ReadVarIntUInt32(); } catch (Exception error) { failure = error; }
                Assert.IsType<OverflowException>(failure);
            }
            Assert.Equal(1, reader.Position);
        }
        byte[] wide = new byte[9];
        wide[0] = unchecked((byte)VarIntCodes.UInt64);
        BinaryPrimitives.WriteUInt64LittleEndian(wide.AsSpan(1), uint.MaxValue);
        ReadOnlySpan<byte> wideInput = wide;
        var wideReader = new LuminPackReader(ref wideInput);
        Assert.Equal(uint.MaxValue, wideReader.ReadVarIntUInt32());
        wideReader = new LuminPackReader(ref wideInput);
        Exception? overflow = null;
        try { wideReader.ReadVarIntInt32(); } catch (Exception error) { overflow = error; }
        Assert.IsType<OverflowException>(overflow);
    }

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
