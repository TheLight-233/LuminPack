using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Option;
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
        Check(42, static value => LuminPackSerializer.Serialize(value));
        Check(long.MinValue, static value => LuminPackSerializer.Serialize(value));
        Check(3.25d, static value => LuminPackSerializer.Serialize(value));
        Check(1.5f, static value => LuminPackSerializer.Serialize(value));
        Check(decimal.MinusOne, static value => LuminPackSerializer.Serialize(value));
        Check(true, static value => LuminPackSerializer.Serialize(value));
        Check('\u00E9', static value => LuminPackSerializer.Serialize(value));
        Check(Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b"), static value => LuminPackSerializer.Serialize(value));
        Check(new DateTime(2026, 10, 22, 12, 34, 56, DateTimeKind.Utc), static value => LuminPackSerializer.Serialize(value));
        Check(new DateTimeOffset(2026, 10, 22, 12, 34, 56, TimeSpan.FromHours(8)), static value => LuminPackSerializer.Serialize(value));
        Check(TimeSpan.FromTicks(123456789), static value => LuminPackSerializer.Serialize(value));
        Check((Half)1.5f, static value => LuminPackSerializer.Serialize(value));
        Check(Int128.MaxValue, static value => LuminPackSerializer.Serialize(value));
        Check(new Vector3(1, 2, 3), static value => LuminPackSerializer.Serialize(value));
        Check(Matrix4x4.CreateRotationZ(0.5f), static value => LuminPackSerializer.Serialize(value));
    }

    /// <summary>Checks the direct built-in records, including their null and empty records.</summary>
    [Fact]
    public void DirectRecordsMatchThePooledPath()
    {
        Check<int[]?>(null, static value => LuminPackSerializer.Serialize(value));
        Check(Array.Empty<int>(), static value => LuminPackSerializer.Serialize(value));
        Check(Enumerable.Range(-5, 300).ToArray(), static value => LuminPackSerializer.Serialize(value));
        Check<List<long>?>(null, static value => LuminPackSerializer.Serialize(value));
        Check(new List<long>(), static value => LuminPackSerializer.Serialize(value));
        Check(Enumerable.Range(0, 70).Select(i => (long)i * i).ToList(), static value => LuminPackSerializer.Serialize(value));
        Check(new ArraySegment<int>(Enumerable.Range(0, 20).ToArray(), 3, 9), static value => LuminPackSerializer.Serialize(value));
        Check(new Memory<int>(Enumerable.Range(0, 20).ToArray(), 2, 5), static value => LuminPackSerializer.Serialize(value));
        Check(new ReadOnlyMemory<double>(new[] { 1.5, 2.5, 3.5 }), static value => LuminPackSerializer.Serialize(value));
        Check(default(ImmutableArray<int>), static value => LuminPackSerializer.Serialize(value));
        Check(ImmutableArray<int>.Empty, static value => LuminPackSerializer.Serialize(value));
        Check(ImmutableArray.Create(7, 8, 9), static value => LuminPackSerializer.Serialize(value));
        Check<int?>(null, static value => LuminPackSerializer.Serialize(value));
        Check<int?>(17, static value => LuminPackSerializer.Serialize(value));
        Check<Guid?>(Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b"), static value => LuminPackSerializer.Serialize(value));
        Check<Version?>(null, static value => LuminPackSerializer.Serialize(value));
        Check(new Version(1, 2), static value => LuminPackSerializer.Serialize(value));
        Check(new Version(1, 2, 3), static value => LuminPackSerializer.Serialize(value));
        Check(new Version(1, 2, 3, 4), static value => LuminPackSerializer.Serialize(value));
        Check(new KeyValuePair<int, long>(5, -6), static value => LuminPackSerializer.Serialize(value));
        Check((3, 4.5d), static value => LuminPackSerializer.Serialize(value));
        Check(Tuple.Create(1, 2), static value => LuminPackSerializer.Serialize(value));
        Check(BigInteger.Zero, static value => LuminPackSerializer.Serialize(value));
        Check(BigInteger.MinusOne, static value => LuminPackSerializer.Serialize(value));
        Check(BigInteger.Parse("123456789012345678901234567890"), static value => LuminPackSerializer.Serialize(value));
        Check(BigInteger.Pow(-7, 400), static value => LuminPackSerializer.Serialize(value));
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
        Assert.Equal(matrix, global::LuminPackUnitTest.ConcreteReads.Read<int[,]>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref int[,] concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

        var cube = new double[2, 0, 3];
        Assert.Equal(cube.GetLength(2), global::LuminPackUnitTest.ConcreteReads.Read<double[,,]>(LuminPackSerializer.Serialize(cube), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref double[,,] concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!.GetLength(2));
    }

    /// <summary>Checks the immutable stack read keeps the top element first and the raw immutable list keeps the list record.</summary>
    [Fact]
    public void ImmutableStackAndListKeepTheirOrder()
    {
        var stack = ImmutableStack.CreateRange(Enumerable.Range(0, 50));
        var stackCopy = global::LuminPackUnitTest.ConcreteReads.Read<ImmutableStack<int>>(LuminPackSerializer.Serialize(stack), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableStack<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!;
        Assert.Equal(stack.ToArray(), stackCopy.ToArray());
        var list = Enumerable.Range(0, 60).ToImmutableList();
        byte[] bytes = LuminPackSerializer.Serialize(list);
        Assert.Equal(LuminPackSerializer.Serialize(list.ToList()), bytes);
        Assert.Equal(list, global::LuminPackUnitTest.ConcreteReads.Read<ImmutableList<int>>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableList<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));
        Assert.Empty(global::LuminPackUnitTest.ConcreteReads.Read<ImmutableList<int>>(LuminPackSerializer.Serialize(ImmutableList<int>.Empty), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableList<int> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget))!);
    }

    /// <summary>Serializes through the allocating generic entry and the reusable-buffer entry, then reads both ways.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="write">The statically bound allocating entry.</param>
    private static void Check<T>(T value, Func<T, byte[]> write)
    {
        TestProfileSerializer.RegisterTestProfile<T, LuminPackDefaultOptions>();
        byte[] direct = write(value);
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
