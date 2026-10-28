using System.Runtime.CompilerServices;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the one-byte storage layout tag and the conversion between layout models.</summary>
public class LuminStorageTagTest
{
    /// <summary>Checks that a layout equal to a model is written as one native block under that model's tag.</summary>
    [Fact]
    public void NativeLayoutIsWrittenAsABlock()
    {
        var source = Elements(5);
        byte[] wire = new byte[5 * LuminStorageTag<TagSample, TagSampleLayout>.MaxWidth];
        LuminStorageTag<TagSample, TagSampleLayout>.Pack(ref source[0], source.Length, ref wire[0]);
        int width = LuminStorageTag<TagSample, TagSampleLayout>.Width;
        Assert.Equal(Unsafe.SizeOf<TagSample>(), width);
        // The local layout equals one generated model on every supported process, so no conversion is involved.
        Assert.True(LuminStorageTag<TagSample, TagSampleLayout>.Tag is 1 or 2);
        var restored = new TagSample[5];
        LuminStorageTag<TagSample, TagSampleLayout>.Unpack(LuminStorageTag<TagSample, TagSampleLayout>.Tag, ref wire[0], 5, ref restored[0]);
        Assert.Equal(source, restored);
    }

    /// <summary>Checks reading elements written by a process with the other model's alignment.</summary>
    [Fact]
    public void ForeignModelIsConvertedToTheLocalLayout()
    {
        var source = Elements(4);
        foreach (byte tag in new byte[] { 1, 2 })
        {
            int width = LuminStorageTag<TagSample, TagSampleLayout>.WidthOf(tag);
            Assert.Equal(tag == 1 ? 16 : 12, width);
            byte[] wire = new byte[4 * width];
            for (int index = 0; index < source.Length; index++)
            {
                wire[index * width] = source[index].A;
                BitConverter.TryWriteBytes(wire.AsSpan(index * width + (tag == 1 ? 8 : 4), 8), source[index].B);
            }
            var restored = new TagSample[4];
            LuminStorageTag<TagSample, TagSampleLayout>.Unpack(tag, ref wire[0], 4, ref restored[0]);
            Assert.Equal(source, restored);
        }
    }

    /// <summary>Checks that a layout matching no model is converted to the canonical model on write and back on read.</summary>
    [Fact]
    public void UnknownLocalLayoutUsesTheCanonicalModel()
    {
        var source = Elements(3);
        Assert.Equal(1, LuminStorageTag<TagSample, ShiftedLayout>.Tag);
        int width = LuminStorageTag<TagSample, ShiftedLayout>.Width;
        Assert.Equal(16, width);
        // Fill the destination with noise: the converted form must not depend on it.
        byte[] wire = new byte[3 * width];
        wire.AsSpan().Fill(0xAB);
        LuminStorageTag<TagSample, ShiftedLayout>.Pack(ref source[0], 3, ref wire[0]);
        for (int index = 0; index < 3; index++)
        {
            Assert.Equal(source[index].A, wire[index * width + 8]);
            Assert.Equal(source[index].B, BitConverter.ToInt64(wire, index * width));
            Assert.All(wire.AsSpan(index * width + 9, 7).ToArray(), padding => Assert.Equal(0, padding));
        }
        var restored = new TagSample[3];
        LuminStorageTag<TagSample, ShiftedLayout>.Unpack(1, ref wire[0], 3, ref restored[0]);
        Assert.Equal(source, restored);
    }

    /// <summary>Checks that an unknown tag from untrusted input is rejected.</summary>
    [Fact]
    public void UnknownTagIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => LuminStorageTag<TagSample, TagSampleLayout>.WidthOf(0));
        Assert.Throws<InvalidDataException>(() => LuminStorageTag<TagSample, TagSampleLayout>.WidthOf(3));
        Assert.Throws<InvalidDataException>(() => LuminStorageTag<TagSample, TagSampleLayout>.WidthOf(255));
    }

    /// <summary>Creates distinct elements.</summary>
    /// <param name="count">The element count.</param>
    /// <returns>The elements.</returns>
    private static TagSample[] Elements(int count)
    {
        var elements = new TagSample[count];
        for (int index = 0; index < count; index++)
            elements[index] = new TagSample { A = (byte)(index + 1), B = 0x0102030405060708L * (index + 1) };
        return elements;
    }
}

/// <summary>A padded struct whose alignment differs between 32-bit and 64-bit processes.</summary>
public struct TagSample : IEquatable<TagSample>
{
    /// <summary>The narrow member preceding padding.</summary>
    public byte A;

    /// <summary>The wide member.</summary>
    public long B;

    /// <summary>Compares the data members.</summary>
    /// <param name="other">The other value.</param>
    /// <returns>Whether both members are equal.</returns>
    public bool Equals(TagSample other) => A == other.A && B == other.B;
}

/// <summary>Models <see cref="TagSample"/> with 8-byte and 4-byte alignment of the wide member.</summary>
public readonly struct TagSampleLayout : ILuminStorageLayout
{
    /// <summary>Gets the packed data width.</summary>
    public int WireSize => 9;

    /// <summary>Gets the model count.</summary>
    public int ModelCount => 2;

    /// <summary>Gets the element width of a model.</summary>
    /// <param name="tag">The model tag.</param>
    /// <returns>The width including padding.</returns>
    public int ModelSize(int tag) => tag == 1 ? 16 : 12;

    /// <summary>Probes the running layout from actual field addresses.</summary>
    /// <param name="storageOffsets">One entry per packed data byte.</param>
    public void Describe(Span<int> storageOffsets)
    {
        TagSample probe = default;
        ref byte origin = ref Unsafe.As<TagSample, byte>(ref probe);
        int a = (int)Unsafe.ByteOffset(ref origin, ref Unsafe.As<byte, byte>(ref probe.A));
        int b = (int)Unsafe.ByteOffset(ref origin, ref Unsafe.As<long, byte>(ref probe.B));
        storageOffsets[0] = a;
        for (int index = 0; index < 8; index++) storageOffsets[1 + index] = b + index;
    }

    /// <summary>Lists the generated offsets of a model.</summary>
    /// <param name="tag">The model tag.</param>
    /// <param name="storageOffsets">One entry per packed data byte.</param>
    public void DescribeModel(int tag, Span<int> storageOffsets)
    {
        storageOffsets[0] = 0;
        int b = tag == 1 ? 8 : 4;
        for (int index = 0; index < 8; index++) storageOffsets[1 + index] = b + index;
    }
}

/// <summary>A layout whose only model places the members in the opposite order, so the running layout never matches it.</summary>
public readonly struct ShiftedLayout : ILuminStorageLayout
{
    /// <summary>Gets the packed data width.</summary>
    public int WireSize => 9;

    /// <summary>Gets the model count.</summary>
    public int ModelCount => 1;

    /// <summary>Gets the element width of the model.</summary>
    /// <param name="tag">The model tag.</param>
    /// <returns>The width including padding.</returns>
    public int ModelSize(int tag) => 16;

    /// <summary>Probes the running layout from actual field addresses.</summary>
    /// <param name="storageOffsets">One entry per packed data byte.</param>
    public void Describe(Span<int> storageOffsets) => default(TagSampleLayout).Describe(storageOffsets);

    /// <summary>Lists the generated offsets of the model: the wide member first, the narrow member last.</summary>
    /// <param name="tag">The model tag.</param>
    /// <param name="storageOffsets">One entry per packed data byte.</param>
    public void DescribeModel(int tag, Span<int> storageOffsets)
    {
        storageOffsets[0] = 8;
        for (int index = 0; index < 8; index++) storageOffsets[1 + index] = index;
    }
}
