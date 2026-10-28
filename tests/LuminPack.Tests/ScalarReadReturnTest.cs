using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks fixed aggregate returns and the unchanged nullable and immutable record contracts.</summary>
public class ScalarReadReturnTest
{
    /// <summary>Reads complete sixteen-byte storage at every short unaligned input offset.</summary>
    [Fact]
    public void FixedAggregateReturnsPreserveAllBytes()
    {
        CheckStorage(new Complex(-3.25, double.PositiveInfinity));
        CheckStorage(UInt128.MaxValue);
        CheckStorage(Int128.MinValue);
        CheckStorage(decimal.MinValue);
        CheckStorage(Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b"));
        CheckStorage(new Vector4(1.25f, -2.5f, 3.75f, 4));
        CheckStorage(Quaternion.Identity);
    }

    /// <summary>Checks null and present nullable values against the ref API and compiled profile.</summary>
    [Fact]
    public void NullableReturnsRetainFramingAndProfiles()
    {
        CheckNullable<int>(null);
        CheckNullable<int>(int.MinValue);
        CheckNullable<long>(long.MaxValue);
        CheckNullable<Guid>(null);
        CheckNullable<Guid>(Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b"));
        CheckNullable<Complex>(new Complex(3, 4));
    }

    /// <summary>Checks immutable staging slices, empty/null records and managed elements.</summary>
    /// <param name="count">The number of elements, including pool-bucket boundaries.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(1000)]
    public void ImmutableListConstructionUsesOnlyTheDecodedSlice(int count)
    {
        var integers = Enumerable.Range(-count, count).ToImmutableList();
        byte[] bytes = LuminPackSerializer.Serialize(integers);
        Assert.Equal(integers.ToArray(), LuminPackSerializer.Deserialize<ImmutableList<int>>(bytes).ToArray());
        ImmutableList<int> destination = ImmutableList.Create(int.MaxValue);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref destination));
        Assert.Equal(integers.ToArray(), destination.ToArray());

        var strings = Enumerable.Range(0, count).Select(index => index % 3 == 0 ? null : "value_" + index).ToImmutableList();
        byte[] stringBytes = LuminPackSerializer.Serialize(strings);
        Assert.Equal(strings.ToArray(), LuminPackSerializer.Deserialize<ImmutableList<string?>>(stringBytes).ToArray());
        Assert.Null(LuminPackSerializer.Deserialize<ImmutableList<int>?>(LuminPackSerializer.Serialize<ImmutableList<int>?>(null)));
    }

    /// <summary>Compares a raw value return and generated scalar return at unaligned byte locations.</summary>
    /// <typeparam name="T">A known sixteen-byte unmanaged scalar.</typeparam>
    /// <param name="expected">The complete scalar storage.</param>
    private static void CheckStorage<T>(T expected) where T : unmanaged
    {
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(16, Unsafe.SizeOf<T>());
        Assert.Equal(16, bytes.Length);
        for (int offset = 0; offset < 16; offset++)
        {
            byte[] padded = new byte[32];
            bytes.CopyTo(padded, offset);
            var source = padded.AsSpan(offset, 16);
            T actual = LuminPackReader.Read<T>(ref MemoryMarshal.GetReference(source));
            Assert.Equal(bytes, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref actual, 1)).ToArray());
            Assert.Equal(expected, LuminPackSerializer.Deserialize<T>(source));
        }
    }

    /// <summary>Compares every nullable entry with the same tag and payload.</summary>
    /// <typeparam name="T">The nullable framework scalar.</typeparam>
    /// <param name="expected">The nullable value.</param>
    private static void CheckNullable<T>(T? expected) where T : struct
    {
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(expected, LuminPackSerializer.Deserialize<T?>(bytes));
        Assert.Equal(expected, LuminPackSerializer.Deserialize<T?, LuminPackDefaultOptions>(bytes));
        T? destination = default;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref destination));
        Assert.Equal(expected, destination);
        if (expected.HasValue) Assert.Equal(0, bytes[0]);
        else Assert.Equal(new byte[] { 255 }, bytes);
    }
}
