using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest.UnionPartitions;

/// <summary>Checks adaptive type-partitioned Union list dispatch and tiered write dispatch against per-element records.</summary>
public class UnionPartitionTest
{
    /// <summary>Writes the same bytes as one record per element and reads back every element for each case pattern.</summary>
    /// <param name="pattern">The case sequence.</param>
    [Theory]
    [InlineData("random")]
    [InlineData("runs")]
    [InlineData("cycle")]
    [InlineData("short")]
    public void ListBytesAndValuesMatchPerElementRecords(string pattern)
    {
        var source = Create(pattern switch { "short" => 31, "random" => 1000, _ => 700 }, pattern);
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(Expected(source), bytes);
        var copy = LuminPackSerializer.Deserialize<List<PartitionRoot>>(bytes);
        AssertEqual(source, copy);
    }

    /// <summary>Partitions arrays and immutable arrays with the same records as lists.</summary>
    /// <param name="pattern">The case sequence.</param>
    [Theory]
    [InlineData("random")]
    [InlineData("runs")]
    [InlineData("cycle")]
    public void ArraysMatchPerElementRecords(string pattern)
    {
        var source = Create(900, pattern);
        byte[] expected = Expected(source);
        PartitionRoot[] array = source.ToArray();
        byte[] arrayBytes = LuminPackSerializer.Serialize(array);
        Assert.Equal(expected, arrayBytes);
        AssertEqual(source, LuminPackSerializer.Deserialize<PartitionRoot[]>(arrayBytes)!.ToList());
        var immutable = System.Collections.Immutable.ImmutableArray.CreateRange(source);
        byte[] immutableBytes = LuminPackSerializer.Serialize(immutable);
        Assert.Equal(expected, immutableBytes);
        AssertEqual(source, LuminPackSerializer.Deserialize<System.Collections.Immutable.ImmutableArray<PartitionRoot>>(immutableBytes).ToList());
        var reused = Create(900, pattern).ToArray();
        var existing = reused[7];
        LuminPackSerializer.Deserialize(arrayBytes, ref reused);
        AssertEqual(source, reused.ToList());
        Assert.Same(existing, reused[7]);
    }

    /// <summary>Takes the per-element fallback for null elements in an otherwise partitioned chunk.</summary>
    [Fact]
    public void NullElementsKeepTheirRecords()
    {
        var source = Create(600, "random");
        source[10] = null!;
        source[300] = null!;
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(Expected(source), bytes);
        AssertEqual(source, LuminPackSerializer.Deserialize<List<PartitionRoot>>(bytes));
    }

    /// <summary>Refills existing elements through the ordinary reuse path instead of allocating partitioned ones.</summary>
    [Fact]
    public void ExistingElementsAreReused()
    {
        var source = Create(600, "random");
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var target = Create(600, "random");
        var existing = target[5];
        LuminPackSerializer.Deserialize(bytes, ref target);
        AssertEqual(source, target);
        Assert.Same(existing, target[5]);
    }

    /// <summary>Rejects truncated input and unknown tags inside a partitioned chunk.</summary>
    [Fact]
    public void InvalidRecordsAreRejected()
    {
        var source = Create(600, "random");
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<List<PartitionRoot>>(bytes.AsSpan(0, bytes.Length - 1).ToArray()));
        byte[] unknown = (byte[])bytes.Clone();
        unknown[4 + source.Take(100).Sum(item => LuminPackSerializer.Serialize(item).Length)] = 200;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<List<PartitionRoot>>(unknown));
    }

    /// <summary>Keeps guarded and direct tier records identical to their original virtual records.</summary>
    [Fact]
    public void TieredDispatchKeepsRecords()
    {
        PairRoot left = new PairLeft { Id = 7, Left = 8 };
        PairRoot right = new PairRight { Id = 9, Right = 10.5 };
        Assert.Equal(new byte[] { 0, 7, 0, 0, 0, 8, 0, 0, 0 }, LuminPackSerializer.Serialize(left));
        var copy = LuminPackSerializer.Deserialize<PairRoot>(LuminPackSerializer.Serialize(right));
        Assert.Equal(10.5, Assert.IsType<PairRight>(copy).Right);
        var list = new List<PairRoot> { left, right, left };
        var listCopy = LuminPackSerializer.Deserialize<List<PairRoot>>(LuminPackSerializer.Serialize(list));
        Assert.Equal(8, Assert.IsType<PairLeft>(listCopy[2]).Left);
    }

    /// <summary>Builds a list whose case sequence follows the requested pattern.</summary>
    /// <param name="count">The element count.</param>
    /// <param name="pattern">The case sequence.</param>
    /// <returns>The populated list.</returns>
    private static List<PartitionRoot> Create(int count, string pattern)
    {
        var random = new Random(0x51A7);
        var list = new List<PartitionRoot>(count);
        for (int index = 0; index < count; index++)
        {
            int kind = pattern switch { "runs" => index / 40 % 5, "cycle" => index % 5, _ => random.Next(5) };
            list.Add(kind switch
            {
                0 => new PartitionA { Id = index, A = index * 3 },
                1 => new PartitionB { Id = index, B = index * 5L },
                2 => new PartitionC { Id = index, C = index / 2.0, Extra = -index },
                3 => new PartitionD { Id = index, X = index, Y = index + 1, Z = index + 2 },
                _ => new PartitionE { Id = index, E = (short)index },
            });
        }
        return list;
    }

    /// <summary>Concatenates the list header and each element's own Union record.</summary>
    /// <param name="source">The source list.</param>
    /// <returns>The expected list bytes.</returns>
    private static byte[] Expected(List<PartitionRoot> source)
    {
        var bytes = new List<byte>(BitConverter.GetBytes(source.Count));
        foreach (var item in source) bytes.AddRange(LuminPackSerializer.Serialize(item));
        return bytes.ToArray();
    }

    /// <summary>Compares element types and fields.</summary>
    /// <param name="expected">The source list.</param>
    /// <param name="actual">The deserialized list.</param>
    private static void AssertEqual(List<PartitionRoot> expected, List<PartitionRoot>? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Count, actual!.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            if (expected[index] is null) { Assert.Null(actual[index]); continue; }
            Assert.IsType(expected[index].GetType(), actual[index]);
            Assert.Equal(LuminPackSerializer.Serialize(expected[index]), LuminPackSerializer.Serialize(actual[index]));
        }
    }
}

[LuminPackable]
[LuminPackUnion(0, typeof(PartitionA))]
[LuminPackUnion(1, typeof(PartitionB))]
[LuminPackUnion(2, typeof(PartitionC))]
[LuminPackUnion(3, typeof(PartitionD))]
[LuminPackUnion(4, typeof(PartitionE))]
public abstract partial class PartitionRoot { public int Id; }

[LuminPackable] public sealed partial class PartitionA : PartitionRoot { public int A; }
[LuminPackable] public sealed partial class PartitionB : PartitionRoot { public long B; }
[LuminPackable] public sealed partial class PartitionC : PartitionRoot { public double C; public int Extra; }
[LuminPackable] public sealed partial class PartitionD : PartitionRoot { public int X; public int Y; public int Z; }
[LuminPackable] public sealed partial class PartitionE : PartitionRoot { public short E; }

[LuminPackable]
[LuminPackUnion(0, typeof(PairLeft))]
[LuminPackUnion(1, typeof(PairRight))]
public abstract partial class PairRoot { public int Id; }

[LuminPackable] public sealed partial class PairLeft : PairRoot { public int Left; }
[LuminPackable] public sealed partial class PairRight : PairRoot { public double Right; }
