#if LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies version-tolerant struct reads preserve compatible member storage.</summary>
public class NinoVersionRegressionTest
{
    /// <summary>Reuses same-length slices for a public field and mirrored property on a schema match.</summary>
    [Fact]
    public void VersionedStructReusesMemoryAndArraySegmentSlices()
    {
        var source = new Current { Values = new[] { 17, 23 }, Segment = new[] { 29, 31 } };
        int[] memoryBacking = [1, 0, 0, 2];
        int[] segmentBacking = [3, 0, 0, 4];
        Memory<int> memory = memoryBacking.AsMemory(1, 2);
        var segment = new ArraySegment<int>(segmentBacking, 1, 2);
        var target = new Current { Values = memory, Segment = segment };
        byte[] bytes = LuminPackSerializer.Serialize(source);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));

        Assert.Equal(memory, target.Values);
        Assert.True(segment.Equals(target.Segment));
        Assert.Equal(new[] { 1, 17, 23, 2 }, memoryBacking);
        Assert.Equal(new[] { 3, 29, 31, 4 }, segmentBacking);
    }

    /// <summary>Reuses a mapped member and clears an absent member without mutating its old storage.</summary>
    [Fact]
    public void VersionedStructReusesMappedStorageAndClearsMissingMembers()
    {
        var source = new Earlier { Removed = 5, Values = new[] { 17, 23 } };
        int[] memoryBacking = [0, 0];
        int[] segmentBacking = [3, 4];
        Memory<int> memory = memoryBacking;
        var target = new Current { Values = memory, Segment = new ArraySegment<int>(segmentBacking) };
        byte[] bytes = LuminPackSerializer.Serialize(source);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));

        Assert.Equal(memory, target.Values);
        Assert.Equal(new[] { 17, 23 }, memoryBacking);
        Assert.Null(target.Segment.Array);
        Assert.Equal(new[] { 3, 4 }, segmentBacking);
    }

    /// <summary>Rejects an incomplete member frame before writing to the supplied slice.</summary>
    [Fact]
    public void VersionedStructChecksMemoryFrameBeforeRefill()
    {
        var source = new Earlier { Removed = 5, Values = new[] { 17, 23 } };
        int[] backing = [1, 2];
        Memory<int> memory = backing;
        var target = new Earlier { Values = memory };
        byte[] bytes = LuminPackSerializer.Serialize(source);

        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));

        Assert.Equal(memory, target.Values);
        Assert.Equal(new[] { 1, 2 }, backing);
    }

    /// <summary>Provides a versioned record with both direct and mirrored slice storage.</summary>
    [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
    public struct Current
    {
        public Memory<int> Values;
        public ArraySegment<int> Segment { get; set; }
    }

    /// <summary>Provides an earlier schema with a differently positioned memory field.</summary>
    [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
    public struct Earlier
    {
        public int Removed;
        public Memory<int> Values;
    }
}
#endif
