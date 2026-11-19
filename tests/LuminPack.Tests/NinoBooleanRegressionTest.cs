#if LUMINPACK_NEXT_GENERATOR
using System.Buffers;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks canonical Boolean storage at the binary root boundaries.</summary>
public class NinoBooleanRegressionTest
{
    /// <summary>Rejects noncanonical root Booleans through value and ref entry points.</summary>
    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)128)]
    [InlineData((byte)255)]
    public void ScalarRootRejectsNoncanonicalStorage(byte invalid)
    {
        byte[] bytes = [invalid];
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<bool>(bytes));
        bool target = false;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<bool>(new ReadOnlySequence<byte>(bytes)));
        Assert.Throws<InvalidDataException>(() => ReadBooleanFromState(bytes));
        Assert.False(target);
        Assert.False(LuminPackSerializer.Deserialize<bool>(new byte[] { 0 }));
        Assert.True(LuminPackSerializer.Deserialize<bool>(new byte[] { 1 }));
    }

    /// <summary>Runs the advancing Boolean root on caller-owned input.</summary>
    /// <param name="bytes">The encoded value.</param>
    private static void ReadBooleanFromState(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span);
        bool value = false;
        LuminPackSerializer.Deserialize(ref reader, ref value);
    }

    /// <summary>Checks valid data, storage reuse and malformed elements at vector boundaries and tails.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(4097)]
    public void CollectionRootsRejectNoncanonicalStorage(int length)
    {
        bool[] values = Enumerable.Range(0, length).Select(index => index % 3 == 0).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(values);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(new List<bool>(values)));

        bool[] array = new bool[length];
        bool[] arrayStorage = array;
        List<bool> list = new(length);
        List<bool> listStorage = list;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref array));
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref list));
        Assert.Same(arrayStorage, array);
        Assert.Same(listStorage, list);
        Assert.Equal(values, array);
        Assert.Equal(values, list);

        foreach (int index in new[] { 0, length / 2, length - 1 }.Distinct())
        {
            foreach (byte invalid in new byte[] { 2, 128, 255 })
            {
                bytes[sizeof(int) + index] = invalid;
                Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<bool[]>(bytes));
                Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<List<bool>>(bytes));
                Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref array));
                Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref list));
            }
            bytes[sizeof(int) + index] = values[index] ? (byte)1 : (byte)0;
        }
    }

    /// <summary>Preserves list data when its storage also contains the caller's input record.</summary>
    [Fact]
    public void BooleanListRefReadPreservesOverlappingPayload()
    {
        bool[] values = Enumerable.Range(0, 33).Select(index => index % 3 == 0).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(new List<bool>(values));
        List<bool> list = new(new bool[bytes.Length]);
        List<bool> original = list;
        Span<byte> input = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(list));
        bytes.CopyTo(input);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize((ReadOnlySpan<byte>)input, ref list));
        Assert.Same(original, list);
        Assert.Equal(values, list);
    }

    /// <summary>Keeps the null and empty collection records distinct.</summary>
    [Fact]
    public void BooleanRootsKeepNullAndEmptyDistinct()
    {
        byte[] absent = LuminPackSerializer.Serialize((bool[]?)null);
        byte[] empty = LuminPackSerializer.Serialize(Array.Empty<bool>());

        Assert.Null(LuminPackSerializer.Deserialize<bool[]>(absent));
        Assert.Null(LuminPackSerializer.Deserialize<List<bool>>(absent));
        Assert.Empty(LuminPackSerializer.Deserialize<bool[]>(empty));
        Assert.Empty(LuminPackSerializer.Deserialize<List<bool>>(empty));
    }
}
#endif
