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


    private static readonly LuminPackVersionMapCache EmptyVersionCache = CreateVersionCache();

    /// <summary>Creates the owner of an immutable descriptor table for positional framing tests.</summary>
    /// <returns>A cache with no mapped receiving slots.</returns>
    private static LuminPackVersionMapCache CreateVersionCache() => new(static (uint _, out ulong expected) =>
    {
        expected = 0;
        return -1;
    });

    /// <summary>Checks full and short record framing under every compiled profile.</summary>
    [Fact]
    public void VersionDescriptorsKeepEveryCompiledProfile()
    {
        CheckVersionDescriptors<LuminPackUtf8LengthOptions>(0);
        CheckVersionDescriptors<LuminPackUtf8TokenOptions>(1);
        CheckVersionDescriptors<LuminPackUtf16LengthOptions>(2);
        CheckVersionDescriptors<LuminPackUtf16TokenOptions>(3);
        CheckVersionDescriptors<LuminPackUtf8VarIntOptions>(4);
        CheckVersionDescriptors<LuminPackUtf16VarIntOptions>(5);
    }

    /// <summary>Checks exact-capacity writes and subsequent reads with one closed profile.</summary>
    /// <typeparam name="TOptions">The actual document profile.</typeparam>
    /// <param name="profile">The expected binary profile tag.</param>
    private static void CheckVersionDescriptors<TOptions>(byte profile) where TOptions : struct, ILuminPackOptions
    {
        foreach (int count in new[] { 0, 1, 3, 8, 16, 17, 249 })
        {
            byte[] descriptors = new byte[count * 8];
            for (int i = 0; i < count; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(descriptors.AsSpan(i * 8), (uint)i);
            ulong schema = LuminPackVersionRecord.ComputeSchema(descriptors);
            var cache = CreateVersionCache();
            int completeWidth = 11 + descriptors.Length;
            byte[] storage = Enumerable.Repeat((byte)0xCC, completeWidth + 5).ToArray();
            Span<byte> output = storage.AsSpan(1, completeWidth + 3);
            var writer = new LuminPackWriter(ref output, new LuminPackWriterOptionalState());
            writer.WriteVersionRecord<TOptions>(schema, descriptors, cache);
            Assert.Equal(completeWidth, writer.Position);
            writer.WriteVersionRecord<TOptions>(schema, descriptors, cache);
            Assert.Equal(output.Length, writer.Position);
            Assert.Equal(0xCC, storage[0]);
            Assert.Equal(0xCC, storage[^1]);
            Assert.True(output[..3].SequenceEqual(new byte[] { 251, (byte)(0x40 | profile), (byte)count }));
            Assert.Equal(schema, BinaryPrimitives.ReadUInt64LittleEndian(output[3..]));
            Assert.True(output[11..completeWidth].SequenceEqual(descriptors));
            Assert.True(output[completeWidth..].SequenceEqual(new byte[] { 251, (byte)(0x50 | profile), 0 }));

            ReadOnlySpan<byte> input = output;
            var reader = new LuminPackReader(ref input, new LuminPackReaderOptionalState());
            var full = reader.ReadVersionRecord<TOptions>(schema, descriptors, cache);
            Assert.False(full.IsNull);
            Assert.False(full.IsLegacy);
            Assert.Null(full.Map);
            Assert.Equal(completeWidth, reader.Position);
            var registered = reader.ReadVersionRecord<TOptions>(schema, descriptors, cache);
            Assert.False(registered.IsNull);
            Assert.Null(registered.Map);
            Assert.Equal(input.Length, reader.Position);
        }
    }

    /// <summary>Checks the hashed registry, multibyte ordinals and rollback below the small-table boundary.</summary>
    [Fact]
    public void VersionScopesRollBackHashedRegistrations()
    {
        var models = Enumerable.Range(0, 129).Select(index =>
        {
            byte[] descriptors = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(descriptors, (uint)index);
            return (Schema: LuminPackVersionRecord.ComputeSchema(descriptors), Descriptors: descriptors, Cache: CreateVersionCache());
        }).ToArray();
        Span<byte> output = new byte[3000];
        var writer = new LuminPackWriter(ref output, new LuminPackWriterOptionalState());
        foreach (var model in models.AsSpan(0, 8))
            writer.WriteVersionRecord<LuminPackDefaultOptions>(model.Schema, model.Descriptors, model.Cache);

        int lengthPosition = writer.ReserveVersionLength();
        int scope = writer.EnterVersionScope();
        foreach (var model in models.AsSpan(8))
            writer.WriteVersionRecord<LuminPackDefaultOptions>(model.Schema, model.Descriptors, model.Cache);
        var last = models[^1];
        int extendedPosition = writer.Position;
        writer.WriteVersionRecord<LuminPackDefaultOptions>(last.Schema, last.Descriptors, last.Cache);
        Assert.True(output.Slice(extendedPosition, 4).SequenceEqual(new byte[] { 251, 0x52, 0x80, 0x01 }));
        writer.ExitVersionScope(scope);
        writer.CompleteVersionLength(lengthPosition);

        var retained = models[7];
        int outsidePosition = writer.Position;
        writer.WriteVersionRecord<LuminPackDefaultOptions>(retained.Schema, retained.Descriptors, retained.Cache);
        Assert.True(output.Slice(outsidePosition, 3).SequenceEqual(new byte[] { 251, 0x52, 7 }));
        outsidePosition = writer.Position;
        writer.WriteVersionRecord<LuminPackDefaultOptions>(last.Schema, last.Descriptors, last.Cache);
        Assert.Equal(0x42, output[outsidePosition + 1]);
        outsidePosition = writer.Position;
        writer.WriteVersionRecord<LuminPackDefaultOptions>(last.Schema, last.Descriptors, last.Cache);
        Assert.True(output.Slice(outsidePosition, 3).SequenceEqual(new byte[] { 251, 0x52, 8 }));

        ReadOnlySpan<byte> input = output[..writer.Position];
        using var state = new LuminPackReaderOptionalState();
        var reader = new LuminPackReader(ref input, state);
        foreach (var model in models.AsSpan(0, 8))
            Assert.Null(reader.ReadVersionRecord<LuminPackDefaultOptions>(model.Schema, model.Descriptors, model.Cache).Map);

        var field = reader.EnterVersionField();
        foreach (var model in models.AsSpan(8))
            Assert.Null(reader.ReadVersionRecord<LuminPackDefaultOptions>(model.Schema, model.Descriptors, model.Cache).Map);
        Assert.Null(reader.ReadVersionRecord<LuminPackDefaultOptions>(last.Schema, last.Descriptors, last.Cache).Map);
        reader.ValidateVersionFieldEnd(field);
        reader.ExitVersionField(field);
        Assert.Equal(8, state.VersionRegistry.Count);

        Assert.Null(reader.ReadVersionRecord<LuminPackDefaultOptions>(retained.Schema, retained.Descriptors, retained.Cache).Map);
        Assert.Null(reader.ReadVersionRecord<LuminPackDefaultOptions>(last.Schema, last.Descriptors, last.Cache).Map);
        Assert.Null(reader.ReadVersionRecord<LuminPackDefaultOptions>(last.Schema, last.Descriptors, last.Cache).Map);
        Assert.Equal(input.Length, reader.Position);
        Assert.Equal(9, state.VersionRegistry.Count);
    }

    /// <summary>Records an empty model in the caller-owned state without a reference table or payload.</summary>
    /// <param name="state">The reader state whose operation lifetime is under test.</param>
    internal static void RegisterEmptyVersion(LuminPackReaderOptionalState state)
    {
        Span<byte> buffer = stackalloc byte[11];
        RegisterEmptyVersion(new LuminPackWriterOptionalState(), buffer);
        ulong schema = LuminPackVersionRecord.ComputeSchema(ReadOnlySpan<byte>.Empty);
        ReadOnlySpan<byte> input = buffer;
        var reader = new LuminPackReader(ref input, state);
        reader.ReadVersionRecord<LuminPackDefaultOptions>(schema, ReadOnlySpan<byte>.Empty, EmptyVersionCache);
    }

    /// <summary>Records an empty model in the operation state using independent framing storage.</summary>
    /// <param name="state">The writer state whose operation lifetime is under test.</param>
    /// <param name="payload">An optional writable window for a subsequent read.</param>
    internal static void RegisterEmptyVersion(LuminPackWriterOptionalState state, scoped Span<byte> payload = default)
    {
        if (payload.IsEmpty)
            payload = stackalloc byte[11];
        ulong schema = LuminPackVersionRecord.ComputeSchema(ReadOnlySpan<byte>.Empty);
        var writer = new LuminPackWriter(ref payload, state);
        writer.WriteVersionRecord<LuminPackDefaultOptions>(schema, ReadOnlySpan<byte>.Empty, EmptyVersionCache);
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

    /// <summary>Preserves JSON cursor checks, checked diagnostic overflow and the position after rejection.</summary>
    [Fact]
    public void JsonAdvanceKeepsCheckedDiagnosticsAndCursor()
    {
        byte[] storage = new byte[16];
        foreach (int position in new[] { 0, 1, 8, 9 })
        foreach (int count in new[] { int.MinValue, -1, 0, 1, 7, 8, 9, int.MaxValue })
        {
            ReadOnlySpan<byte> window = storage.AsSpan(0, 8);
            var reader = new LuminPackJsonReader(ref window, new LuminPackReaderOptionalState()) { Position = position };
            long end = (long)position + count;
            bool invalid = position > window.Length || count < 0 || end > window.Length;
            Exception? failure = null;
            try { reader.Advance(count); }
            catch (Exception error) { failure = error; }
            if (!invalid)
            {
                Assert.Null(failure);
                Assert.Equal((int)end, reader.Position);
                continue;
            }

            if (end > int.MaxValue || end < int.MinValue)
                Assert.IsType<OverflowException>(failure);
            else
            {
                Assert.NotNull(failure);
                Assert.Equal("LuminPack.Code.LuminPackExceptionHelper+LuminPackException", failure.GetType().FullName);
                Assert.Equal($"Span out of range with {end}.", failure!.Message);
            }
            Assert.Equal(position, reader.Position);
        }
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
            Assert.Equal(expected, LuminPackSerializer.Serialize<int?, LuminPackUtf8LengthOptions>(source));
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
