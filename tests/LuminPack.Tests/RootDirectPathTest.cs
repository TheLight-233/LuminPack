using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that the stateless root entries for raw scalars and direct built-in records write and read exactly the bytes of the pooled path.</summary>
public class RootDirectPathTest
{
    /// <summary>Checks every raw scalar root through the generic entries against the reusable-buffer entries.</summary>
    [Fact]
    public void RawScalarsMatchThePooledPath()
    {
        Check(42);
        Check(long.MinValue);
        Check(3.25d);
        Check(1.5f);
        Check(decimal.MinusOne);
        Check(true);
        Check('é');
        Check(Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b"));
        Check(new DateTime(2026, 10, 22, 12, 34, 56, DateTimeKind.Utc));
        Check(new DateTimeOffset(2026, 10, 22, 12, 34, 56, TimeSpan.FromHours(8)));
        Check(TimeSpan.FromTicks(123456789));
        Check((Half)1.5f);
        Check(Int128.MaxValue);
        Check(new Vector3(1, 2, 3));
        Check(Matrix4x4.CreateRotationZ(0.5f));
    }

    /// <summary>Checks the direct built-in records, including their null and empty records.</summary>
    [Fact]
    public void DirectRecordsMatchThePooledPath()
    {
        Check<int[]?>(null);
        Check(Array.Empty<int>());
        Check(Enumerable.Range(-5, 300).ToArray());
        Check<List<long>?>(null);
        Check(new List<long>());
        Check(Enumerable.Range(0, 70).Select(i => (long)i * i).ToList());
        Check(new ArraySegment<int>(Enumerable.Range(0, 20).ToArray(), 3, 9));
        Check(new Memory<int>(Enumerable.Range(0, 20).ToArray(), 2, 5));
        Check(new ReadOnlyMemory<double>(new[] { 1.5, 2.5, 3.5 }));
        Check(default(ImmutableArray<int>));
        Check(ImmutableArray<int>.Empty);
        Check(ImmutableArray.Create(7, 8, 9));
        Check<int?>(null);
        Check<int?>(17);
        Check<Guid?>(Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b"));
        Check<Version?>(null);
        Check(new Version(1, 2));
        Check(new Version(1, 2, 3));
        Check(new Version(1, 2, 3, 4));
        Check(new KeyValuePair<int, long>(5, -6));
        Check((3, 4.5d));
        Check(Tuple.Create(1, 2));
        Check(BigInteger.Zero);
        Check(BigInteger.MinusOne);
        Check(BigInteger.Parse("123456789012345678901234567890"));
        Check(BigInteger.Pow(-7, 400));
    }

    /// <summary>Checks raw multidimensional arrays write their row-major payload as one block and round trip.</summary>
    [Fact]
    public void RawMultidimensionalArraysUseTheirRowMajorPayload()
    {
        var matrix = new int[3, 5];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 5; x++)
                matrix[y, x] = y * 100 + x;
        byte[] bytes = LuminPackSerializer.Serialize(matrix);
        var payload = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref matrix[0, 0], matrix.Length));
        Assert.Equal(1 + 2 * 4 + 4 + payload.Length, bytes.Length);
        Assert.True(bytes.AsSpan(bytes.Length - payload.Length).SequenceEqual(payload));
        Assert.Equal(matrix, LuminPackSerializer.Deserialize<int[,]>(bytes));

        var cube = new double[2, 0, 3];
        Assert.Equal(cube.GetLength(2), LuminPackSerializer.Deserialize<double[,,]>(LuminPackSerializer.Serialize(cube))!.GetLength(2));
    }

    /// <summary>Checks the immutable stack read keeps the top element first and the raw immutable list keeps the list record.</summary>
    [Fact]
    public void ImmutableStackAndListKeepTheirOrder()
    {
        var stack = ImmutableStack.CreateRange(Enumerable.Range(0, 50));
        var stackCopy = LuminPackSerializer.Deserialize<ImmutableStack<int>>(LuminPackSerializer.Serialize(stack))!;
        Assert.Equal(stack.ToArray(), stackCopy.ToArray());
        var list = Enumerable.Range(0, 60).ToImmutableList();
        byte[] bytes = LuminPackSerializer.Serialize(list);
        Assert.Equal(LuminPackSerializer.Serialize(list.ToList()), bytes);
        Assert.Equal(list, LuminPackSerializer.Deserialize<ImmutableList<int>>(bytes));
        Assert.Empty(LuminPackSerializer.Deserialize<ImmutableList<int>>(LuminPackSerializer.Serialize(ImmutableList<int>.Empty))!);
    }

    /// <summary>Serializes through the allocating generic entry and the reusable-buffer entry, then reads both ways.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="value">The value to check.</param>
    private static void Check<T>(T value)
    {
        byte[] direct = LuminPackSerializer.Serialize(value);
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            LuminPackSerializer.Serialize(value, buffer);
            Assert.Equal(buffer.GetSpan().ToArray(), direct);
            T fromDirect = LuminPackSerializer.Deserialize<T>(direct);
            T fromBuffer = LuminPackSerializer.Deserialize<T>(buffer);
            Assert.Equal(LuminPackSerializer.Serialize(fromBuffer), LuminPackSerializer.Serialize(fromDirect));
            T existing = default!;
            Assert.Equal(direct.Length, LuminPackSerializer.Deserialize(direct, ref existing));
            Assert.Equal(direct, LuminPackSerializer.Serialize(existing));
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
    }
}
