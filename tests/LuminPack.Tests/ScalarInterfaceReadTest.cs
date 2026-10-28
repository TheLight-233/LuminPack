using System.Buffers.Binary;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Preserves scalar interface framing, validation and approved exact-storage reuse.</summary>
public class ScalarInterfaceReadTest
{
    /// <summary>Reads null, empty, small and large sequences through all three eligible concrete entries.</summary>
    [Fact]
    public void IntInterfacesKeepTheirOriginalArraysAndBytes()
    {
        foreach (int count in new[] { 0, 1, 8, 1000 })
        {
            int[] values = Enumerable.Range(-3, count).ToArray();
            byte[] bytes = Record(values);
            IEnumerable<int> sequence = values;
            ICollection<int> collection = values;
            IReadOnlyList<int> list = values;
            Assert.Equal(bytes, LuminPackSerializer.Serialize(sequence));
            Assert.Equal(bytes, LuminPackSerializer.Serialize(collection));
            Assert.Equal(bytes, LuminPackSerializer.Serialize(list));
            Check<IEnumerable<int>, int>(bytes, values, static (ReadOnlySpan<byte> data, ref IEnumerable<int>? value) => LuminPackSerializer.Deserialize(data, ref value));
            Check<ICollection<int>, int>(bytes, values, static (ReadOnlySpan<byte> data, ref ICollection<int>? value) => LuminPackSerializer.Deserialize(data, ref value));
            Check<IReadOnlyList<int>, int>(bytes, values, static (ReadOnlySpan<byte> data, ref IReadOnlyList<int>? value) => LuminPackSerializer.Deserialize(data, ref value));
        }
    }

    /// <summary>Retains complete copies of eight- and sixteen-byte scalar elements.</summary>
    [Fact]
    public void WideScalarsKeepAllTheirBytes()
    {
        long[] longs = [long.MinValue, -1, 0, long.MaxValue];
        Check<IEnumerable<long>, long>(Record(longs), longs, static (ReadOnlySpan<byte> data, ref IEnumerable<long>? value) => LuminPackSerializer.Deserialize(data, ref value));
        Guid[] ids = [Guid.Empty, Guid.Parse("12345678-abcd-4321-9876-1234567890ab")];
        Check<ICollection<Guid>, Guid>(Record(ids), ids, static (ReadOnlySpan<byte> data, ref ICollection<Guid>? value) => LuminPackSerializer.Deserialize(data, ref value));
        decimal[] decimals = [decimal.MinValue, 0m, 1.0000000000000000000000000001m, decimal.MaxValue];
        Check<IReadOnlyList<decimal>, decimal>(Record(decimals), decimals, static (ReadOnlySpan<byte> data, ref IReadOnlyList<decimal>? value) => LuminPackSerializer.Deserialize(data, ref value));
    }

    /// <summary>Propagates each binary profile while preserving the same raw element representation.</summary>
    [Fact]
    public void CompiledProfilesUseTheSameReadRoot()
    {
        byte[] bytes = Record(new[] { long.MinValue, 42L, long.MaxValue });
        CheckProfile<LuminPackUtf16LengthOptions>(bytes);
        CheckProfile<LuminPackUtf16TokenOptions>(bytes);
        CheckProfile<LuminPackUtf8LengthOptions>(bytes);
        CheckProfile<LuminPackUtf8TokenOptions>(bytes);
    }

    /// <summary>Rejects every truncated payload and forged count before publishing another array.</summary>
    [Fact]
    public void InvalidInputPreservesTheDestination()
    {
        byte[] bytes = Record(new[] { 5, 7, 11, 13 });
        IEnumerable<int> sentinel = new[] { 137 };
        for (int length = 0; length < bytes.Length; length++)
        {
            IEnumerable<int>? target = sentinel;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref target));
            Assert.Same(sentinel, target);
        }
        foreach (int count in new[] { -2, int.MinValue, int.MaxValue, int.MaxValue / 4 })
        {
            byte[] forged = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(forged, count);
            ICollection<int>? collection = new List<int> { 3 };
            object original = collection;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(forged.AsSpan(), ref collection));
            Assert.Same(original, collection);
            IReadOnlyList<int>? list = new[] { 5 };
            original = list;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(forged.AsSpan(), ref list));
            Assert.Same(original, list);
        }
    }

    /// <summary>Does not introduce a size prescan or a second traversal of a user iterator.</summary>
    [Fact]
    public void IteratorWritesRemainSinglePass()
    {
        int enumerations = 0;
        // A second traversal would change observable iterator behavior before serialization completes.
        IEnumerable<int> Enumerate()
        {
            if (++enumerations != 1) throw new InvalidOperationException("The iterator was visited twice.");
            yield return 3;
            yield return 7;
        }
        IEnumerable<int> values = Enumerate();
        Assert.Equal(Record(new[] { 3, 7 }), LuminPackSerializer.Serialize(values));
        Assert.Equal(1, enumerations);
    }

    /// <summary>Reads a concrete interface without entering the registration-only generic fallback.</summary>
    /// <typeparam name="T">The exact interface type.</typeparam>
    /// <param name="bytes">The validated or rejected input window.</param>
    /// <param name="value">The original and resulting destination.</param>
    /// <returns>The complete consumed width.</returns>
    private delegate int Read<T>(ReadOnlySpan<byte> bytes, ref T? value) where T : class;

    /// <summary>Checks null, empty and fresh-array construction followed by exact-array reuse.</summary>
    /// <typeparam name="TInterface">The concrete interface root.</typeparam>
    /// <typeparam name="T">The original element type.</typeparam>
    /// <param name="bytes">The original native element record.</param>
    /// <param name="expected">The expected complete values.</param>
    /// <param name="read">The concrete generated entry.</param>
    private static void Check<TInterface, T>(byte[] bytes, T[] expected, Read<TInterface> read)
        where TInterface : class, IEnumerable<T>
    {
        TInterface? first = null;
        Assert.Equal(bytes.Length, read(bytes, ref first));
        Assert.IsType<T[]>(first);
        Assert.Equal(expected, first!.ToArray());
        TInterface? second = first;
        Assert.Equal(bytes.Length, read(bytes, ref second));
        Assert.Equal(expected, second!.ToArray());
        Assert.Same(first, second);
        Assert.Equal(4, read(new byte[] { 255, 255, 255, 255 }, ref second));
        Assert.Null(second);
    }

    /// <summary>Checks concrete interface entries with a caller-selected binary profile.</summary>
    /// <typeparam name="TProfile">The compile-time profile.</typeparam>
    /// <param name="bytes">The raw count-prefixed long values.</param>
    private static void CheckProfile<TProfile>(byte[] bytes) where TProfile : struct, ILuminPackOptions
    {
        IEnumerable<long>? sequence = null;
        ICollection<long>? collection = null;
        IReadOnlyList<long>? list = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TProfile>(bytes.AsSpan(), ref sequence));
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TProfile>(bytes.AsSpan(), ref collection));
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TProfile>(bytes.AsSpan(), ref list));
        Assert.Equal(sequence, collection);
        Assert.Equal(sequence, list);
    }

    /// <summary>Builds the established native scalar record independently of the generated writer.</summary>
    /// <typeparam name="T">The fixed unmanaged element type.</typeparam>
    /// <param name="values">The complete payload values.</param>
    /// <returns>The original four-byte count and unchanged element storage.</returns>
    private static byte[] Record<T>(T[] values) where T : unmanaged
    {
        var payload = MemoryMarshal.AsBytes(values.AsSpan());
        byte[] bytes = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, values.Length);
        payload.CopyTo(bytes.AsSpan(4));
        return bytes;
    }
}
