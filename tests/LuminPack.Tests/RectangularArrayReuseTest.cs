#if LUMINPACK_NEXT_GENERATOR
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the binary rectangular array slot reuse contract.</summary>
public sealed class RectangularArrayReuseTest
{
    /// <summary>Reuses matching slot graphs and supports null and new elements in row-major order.</summary>
    [Fact]
    public void MatchingShapeReusesIndependentSlots()
    {
        ReusableMatrixEntry?[,] source =
        {
            { new() { Id = 11, Items = [1, 2] }, null },
            { new() { Id = 13 }, new() { Id = 14 } },
        };
        ReusableMatrixEntry?[,] alias = { { new() { Items = [9, 9] }, new() }, { null, new() } };
        var first = alias[0, 0]!;
        var items = first.Items;
        var last = alias[1, 1];
        var nested = new ReusableMatrixContainer { Entries = alias };
        var bytes = LuminPackSerializer.Serialize(new ReusableMatrixContainer { Entries = source });
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref nested));
        Assert.Same(alias, nested.Entries);
        Assert.Same(first, nested.Entries[0, 0]);
        Assert.Same(items, first.Items);
        Assert.Equal(new[] { 1, 2 }, items);
        Assert.Null(nested.Entries[0, 1]);
        Assert.Equal(new int?[] { 11, null, 13, 14 }, nested.Entries.Cast<ReusableMatrixEntry?>().Select(entry => entry?.Id));
        Assert.Same(last, nested.Entries[1, 1]);
    }

    /// <summary>Checks empty and null values, different dimensions, lower bounds and array covariance.</summary>
    [Fact]
    public void IncompatibleArraysKeepReplacement()
    {
        ReusableMatrixEntry[,] source = { { new() { Id = 11 }, new() { Id = 12 } } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        foreach (var alias in new[]
        {
            new ReusableMatrixEntry[2, 1],
            (ReusableMatrixEntry[,])Array.CreateInstance(typeof(ReusableMatrixEntry), [1, 2], [0, -1]),
            new CovariantMatrixEntry[1, 2],
        })
        {
            var target = alias;
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.NotSame(alias, target);
            Assert.IsType<ReusableMatrixEntry[,]>(target);
            Assert.Equal(11, target[0, 0].Id);
        }

        ReusableMatrixEntry[,]? empty = new ReusableMatrixEntry[0, 2];
        var oldEmpty = empty;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(empty), ref empty);
        Assert.Same(oldEmpty, empty);
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize((ReusableMatrixEntry[,]?)null), ref empty);
        Assert.Null(empty);
    }

    /// <summary>Checks that undeclared elements reuse slots by default, while opt-out declarations, higher ranks and JSON replace existing arrays.</summary>
    [Fact]
    public void DefaultReusesWhileOptOutAndOtherFormatsKeepReplacement()
    {
        PlainMatrixEntry[,] plain = { { new() { Id = 11 } } };
        PlainMatrixEntry[,] plainAlias = { { new() { Id = -1 } } };
        var plainSlot = plainAlias[0, 0];
        var plainTarget = plainAlias;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(plain), ref plainTarget);
        Assert.Same(plainAlias, plainTarget);
        Assert.Same(plainSlot, plainTarget[0, 0]);
        Assert.Equal(11, plainSlot.Id);

        ReplacedMatrixEntry[,] replaced = { { new() { Id = 11 } } };
        ReplacedMatrixEntry[,] replacedAlias = { { new() { Id = -1 } } };
        var replacedTarget = replacedAlias;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(replaced), ref replacedTarget);
        Assert.NotSame(replacedAlias, replacedTarget);
        Assert.Equal(-1, replacedAlias[0, 0].Id);

        ReusableMatrixEntry[,,] cube = { { { new() { Id = 11 } } } };
        ReusableMatrixEntry[,,] cubeAlias = { { { new() { Id = -1 } } } };
        var cubeTarget = cubeAlias;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(cube), ref cubeTarget);
        Assert.NotSame(cubeAlias, cubeTarget);
        Assert.Equal(-1, cubeAlias[0, 0, 0].Id);

        ReusableMatrixEntry[,] matrix = { { new() { Id = 11 } } };
        ReusableMatrixEntry[,] jsonAlias = { { new() { Id = -1 } } };
        var jsonTarget = jsonAlias;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(matrix), ref jsonTarget);
        Assert.NotSame(jsonAlias, jsonTarget);
        Assert.Equal(-1, jsonAlias[0, 0].Id);
    }

    /// <summary>Rejects invalid shapes before mutation and publishes completed slots on a truncated payload.</summary>
    [Fact]
    public void FailedReadsPreserveShapeChecksAndCompletedSlots()
    {
        var first = new ReusableMatrixEntry { Id = 11, Items = [7] };
        ReusableMatrixEntry[,] source = { { first, new() { Id = 12 } } };
        ReusableMatrixEntry[,] alias = { { new() { Id = -1 }, new() { Id = -2 } } };
        var target = alias;
        byte[] bytes = LuminPackSerializer.Serialize(source);
        byte[] malformed = (byte[])bytes.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(9), 1);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(malformed, ref target));
        Assert.Same(alias, target);
        Assert.Equal(-1, alias[0, 0].Id);
        Assert.Equal(-2, alias[0, 1].Id);

        var firstAlias = alias[0, 0];
        var secondAlias = alias[0, 1];
        const int matrixHeaderBytes = 1 + 3 * sizeof(int);
        int throughFirst = matrixHeaderBytes + LuminPackSerializer.Serialize(first).Length;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, throughFirst), ref target));
        Assert.Same(alias, target);
        Assert.Same(firstAlias, alias[0, 0]);
        Assert.Same(secondAlias, alias[0, 1]);
        Assert.Equal(11, firstAlias.Id);
        Assert.Equal(-2, secondAlias.Id);
    }

    /// <summary>Requires the full raw matrix payload before overwriting a matching array.</summary>
    [Fact]
    public void RawMatrixRequiresTheCompleteWindowBeforeOverwritingAliases()
    {
        int[,] source = { { 11, -7 }, { 26, 18 } };
        int[,] alias = { { 5, 6 }, { 7, 8 } };
        var target = alias;
        byte[] wire = LuminPackSerializer.Serialize(source);

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        Assert.Same(alias, target);
        Assert.Equal(new[] { 5, 6, 7, 8 }, alias.Cast<int>());

        LuminPackSerializer.Deserialize(wire, ref target);
        Assert.Same(alias, target);
        Assert.Equal(source.Cast<int>(), alias.Cast<int>());
    }

    /// <summary>Round-trips the one-model nullable block without a per-element frame or a layout tag.</summary>
    /// <param name="nullPercent">The percentage of values that are absent.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(100)]
    public void NullableMatrixWritesOneContiguousBlock(int nullPercent)
    {
        const int headerWidth = 1 + 3 * sizeof(int);
        var source = new int?[5, 20];
        for (int i = 0; i < source.Length; i++)
            source[i / 20, i % 20] = i * 37 % 100 < nullPercent ? null : 100 - i;
        byte[] wire = LuminPackSerializer.Serialize(source);
        const int elementWidth = 2 * sizeof(int);
        Assert.Equal(headerWidth + source.Length * elementWidth, wire.Length);
        Assert.Equal(source.Length, BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(headerWidth - sizeof(int))));
        for (int i = 0; i < source.Length; i++)
        {
            int? item = source[i / 20, i % 20];
            var element = wire.AsSpan(headerWidth + i * elementWidth, elementWidth);
            Assert.Equal(item.HasValue ? 1 : 0, element[0]);
            if (item.HasValue)
                Assert.Equal(item.Value, BinaryPrimitives.ReadInt32LittleEndian(element.Slice(sizeof(int))));
        }
        int?[,]? target = null;
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref target));
        Assert.Equal(source.Cast<int?>(), target!.Cast<int?>());
    }

    /// <summary>Rejects malformed blocks before allocating or publishing a replacement rectangle.</summary>
    [Fact]
    public void NullableMatrixRejectsMalformedBlocksBeforeReplacement()
    {
        const int headerWidth = 1 + 3 * sizeof(int);
        int?[,] source = { { 10, null, 12, null } };
        byte[] wire = LuminPackSerializer.Serialize(source);
        int?[,] alias = { { -77 } };
        int?[,]? target = alias;
        Assert.ThrowsAny<Exception>(() =>
            LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        Assert.Same(alias, target);

        byte[] invalid = (byte[])wire.Clone();
        invalid[headerWidth + 2 * sizeof(int)] = 2;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(invalid, ref target));
        Assert.Same(alias, target);

        BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(headerWidth - sizeof(int)), source.Length + 1);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(invalid, ref target));
        Assert.Same(alias, target);
    }

    /// <summary>Uses actual native width for a rank-three block and keeps the existing shape and null headers.</summary>
    [Fact]
    public void NativeNullableRectangleKeepsRankAndWriteIndex()
    {
        NativeNullableRectangleCell?[,,] source = new NativeNullableRectangleCell?[1, 2, 2];
        source[0, 1, 0] = new NativeNullableRectangleCell { Stamp = -172, Code = 5 };
        const int headerWidth = 1 + 4 * sizeof(int);
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(headerWidth + source.Length * Unsafe.SizeOf<NativeNullableRectangleCell?>(), bytes.Length);
        NativeNullableRectangleCell?[,,]? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Equal(source.Cast<NativeNullableRectangleCell?>(), copy!.Cast<NativeNullableRectangleCell?>());
        Assert.Equal(new[] { 1, 2, 2 }, Enumerable.Range(0, 3).Select(copy!.GetLength));

        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(source, buffer);
        LuminPackSerializer.Serialize(source, buffer);
        Assert.Equal(bytes.Concat(bytes), buffer.GetSpan().ToArray());

        var empty = new NativeNullableRectangleCell?[2, 0, 3];
        byte[] emptyBytes = LuminPackSerializer.Serialize(empty);
        Assert.Equal(headerWidth, emptyBytes.Length);
        Assert.Equal(emptyBytes.Length, LuminPackSerializer.Deserialize(emptyBytes, ref copy));
        Assert.Equal(new[] { 2, 0, 3 }, Enumerable.Range(0, 3).Select(copy!.GetLength));
        Assert.Equal(new byte[] { 255 }, LuminPackSerializer.Serialize<NativeNullableRectangleCell?[,,]>(null!));
    }

    /// <summary>Decodes a foreign storage model and rejects an unknown model before replacing the destination.</summary>
    [Fact]
    public void TaggedNullableRectangleReadsBothModeledWidths()
    {
        const int headerWidth = 1 + 3 * sizeof(int);
        const int modelWidth = 16;
        TaggedNullableRectangleCell?[,] source = { { new() { Stamp = 2002, Code = 37 }, null } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.InRange(bytes[headerWidth], (byte)1, (byte)2);
        var foreign = new byte[headerWidth + 1 + 2 * modelWidth];
        bytes.AsSpan(0, headerWidth).CopyTo(foreign);
        foreign[headerWidth] = 2;
        var first = foreign.AsSpan(headerWidth + 1, modelWidth);
        first[0] = 1;
        BinaryPrimitives.WriteInt64LittleEndian(first.Slice(4), 2002);
        first[12] = 37;

        TaggedNullableRectangleCell?[,]? copy = null;
        Assert.Equal(foreign.Length, LuminPackSerializer.Deserialize(foreign, ref copy));
        Assert.Equal(source.Cast<TaggedNullableRectangleCell?>(), copy!.Cast<TaggedNullableRectangleCell?>());
        var alias = copy;
        foreign[headerWidth] = 255;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(foreign, ref copy));
        Assert.Same(alias, copy);
    }

    /// <summary>Rejects an invalid raw scalar Boolean in a present nullable without publishing the array.</summary>
    [Fact]
    public void NullableBooleanRectangleRejectsInvalidPayload()
    {
        bool?[,] source = { { true, null, false } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        const int headerWidth = 1 + 3 * sizeof(int);
        Assert.Equal(headerWidth + source.Length * 2, bytes.Length);
        bool?[,]? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Equal(source.Cast<bool?>(), copy!.Cast<bool?>());
        var alias = copy;
        bytes[headerWidth + 1] = 2;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Same(alias, copy);
    }

    /// <summary>Preserves the existing framed element contract for explicitly packed payloads.</summary>
    [Fact]
    public void PackedNullableRectangleKeepsElementFrames()
    {
        const int headerWidth = 1 + 3 * sizeof(int);
        PackedNullableRectangleCell?[,] source = { { new() { Stamp = -712, Code = 42 }, null } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(headerWidth + 1 + sizeof(long) + sizeof(byte) + 1, bytes.Length);
        Assert.Equal(0, bytes[headerWidth]);
        Assert.Equal(255, bytes[^1]);
        PackedNullableRectangleCell?[,]? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Equal(source.Cast<PackedNullableRectangleCell?>(), copy!.Cast<PackedNullableRectangleCell?>());
    }

    /// <summary>Enforces collection limits projected from the enclosing reader before touching the array field.</summary>
    [Fact]
    public void NullableRectangleHonorsEnclosingCollectionLimit()
    {
        var source = new BoundedNullableRectangle { Items = new int?[,] { { 3, 5 } } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var alias = new int?[,] { { -7 } };
        var target = new BoundedNullableRectangle { Items = alias };
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(alias, target.Items);
    }

    /// <summary>Reads framed queues of multi-model Tagged elements and checks markers before enqueueing.</summary>
    [Fact]
    public void FramedNullableQueueKeepsPublicationOrder()
    {
        var source = new ConcurrentQueue<long?>([10, null, 12, null]);
        byte[] wire = LuminPackSerializer.Serialize(source);
        ConcurrentQueue<long?>? fresh = null;
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref fresh));
        Assert.Equal(source.ToArray(), fresh!.ToArray());

        wire[sizeof(int) + 1 + sizeof(long)] = 2;
        fresh = null;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(wire, ref fresh));
        Assert.Null(fresh);

        ConcurrentQueue<long?>? reused = new([77]);
        var alias = reused;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(wire, ref reused));
        Assert.Same(alias, reused);
        Assert.Equal(new long?[] { 10 }, alias.ToArray());
    }
}

/// <summary>An explicitly native value whose rectangular nullable records use its current CLR layout.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public struct NativeNullableRectangleCell
{
    /// <summary>The native 64-bit field.</summary>
    public long Stamp;

    /// <summary>The byte following the stamp.</summary>
    public byte Code;
}

/// <summary>A value whose nullable rectangle uses a one-byte tag to distinguish its modeled ABI widths.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
public struct TaggedNullableRectangleCell
{
    /// <summary>The field whose alignment differs between models.</summary>
    public long Stamp;

    /// <summary>The byte following the stamp.</summary>
    public byte Code;
}

/// <summary>An explicitly packed value whose rectangular nullable records retain their element framing.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public struct PackedNullableRectangleCell
{
    /// <summary>The native 64-bit field.</summary>
    public long Stamp;

    /// <summary>The byte following the stamp.</summary>
    public byte Code;
}

/// <summary>Applies a collection limit to a nested rectangular nullable array.</summary>
[LuminPackable, LuminPackOption(MaxCollectionLength = 1)]
public sealed class BoundedNullableRectangle
{
    /// <summary>The rectangular array reached by the checked reader.</summary>
    public int?[,]? Items;
}

/// <summary>An ordinary element that opts in to independent slot reuse.</summary>
[LuminPackable, LuminPackOption(ReuseRectangularArraySlots = true)]
public partial class ReusableMatrixEntry
{
    /// <summary>The entry identifier.</summary>
    public int Id;

    /// <summary>The nested list belonging to this slot.</summary>
    public List<int> Items = [];
}

/// <summary>Supplies a covariant runtime array that needs replacement.</summary>
public sealed class CovariantMatrixEntry : ReusableMatrixEntry;

/// <summary>A regular element with the default slot reuse contract.</summary>
[LuminPackable]
public partial class PlainMatrixEntry
{
    /// <summary>The entry identifier.</summary>
    public int Id;
}

/// <summary>An element that opts out of slot reuse and keeps array replacement.</summary>
[LuminPackable, LuminPackOption(ReuseRectangularArraySlots = false)]
public partial class ReplacedMatrixEntry
{
    /// <summary>The entry identifier.</summary>
    public int Id;
}

/// <summary>A payload containing an opted-in rectangular array.</summary>
[LuminPackable]
public partial class ReusableMatrixContainer
{
    /// <summary>The row-major entries.</summary>
    public ReusableMatrixEntry?[,] Entries = new ReusableMatrixEntry?[0, 0];
}
#endif
